using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Features.Programs;

public sealed class ProgramNotFoundException(string message) : DomainException(message);

/// Program content, readiness and publishing. Departures and allowed hotels stay with the controller.
public class ProgramService(CatalogDbContext db, ProgramImageService images, IClock clock)
{
    public async Task<ProgramDetailDto> GetDetailAsync(Guid id, bool withReadiness, CancellationToken ct)
    {
        var program = await LoadAsync(id, ct);
        return await ToDetailAsync(program, withReadiness, ct);
    }

    public async Task<ProgramDetailDto> ToDetailAsync(Domain.Program program, bool withReadiness, CancellationToken ct)
    {
        var gallery = await images.ListAsync(program.Id, ct);
        var readiness = withReadiness ? (await ReadinessAsync(program, ct)).ToDto() : null;
        return program.ToDetailDto(gallery, readiness);
    }

    public async Task<ReadinessReport> ReadinessAsync(Domain.Program program, CancellationToken ct)
    {
        var hotelIds = program.Hotels.Select(h => h.HotelId).ToList();
        var mapped = hotelIds.Count == 0
            ? 0
            : await db.HotelMappings.CountAsync(
                m => hotelIds.Contains(m.HotelId) && m.ProviderCode == program.ProviderCode, ct);
        var gallery = await images.InfoAsync(program.Id, ct);
        return ProgramReadiness.Evaluate(program, gallery, mapped, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
    }

    public async Task<ReadinessReport> ReadinessAsync(Guid id, CancellationToken ct) =>
        await ReadinessAsync(await LoadAsync(id, ct), ct);

    public async Task<ProgramDetailDto> PublishAsync(Guid id, CancellationToken ct)
    {
        var program = await LoadAsync(id, ct);
        program.Publish(await ReadinessAsync(program, ct));
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(program, withReadiness: true, ct);
    }

    public async Task<ProgramDetailDto> SaveContentAsync(Guid id, SaveProgramContentRequest request, CancellationToken ct)
    {
        var program = await LoadAsync(id, ct);
        await ApplyContentAsync(program, request, ct);
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(program, withReadiness: true, ct);
    }

    /// Validates and copies a content request onto a (tracked or new) program. Itinerary days are
    /// replaced as a whole; the caller saves.
    public async Task ApplyContentAsync(Domain.Program program, SaveProgramContentRequest request, CancellationToken ct)
    {
        // Checked first: it is the only rule that needs the database, and nothing is touched on failure.
        string? slug = null;
        if (request.Slug is not null)
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (!Slug.IsValid(slug))
                throw new ProgramValidationException(
                    "Slug must be lowercase letters, digits and single hyphens, e.g. 'antalya-easter-2027'.");
            if (slug != program.Slug && await db.Programs.AnyAsync(p => p.Slug == slug && p.Id != program.Id, ct))
                throw new ProgramValidationException($"Slug '{slug}' is already used by another program.");
        }

        var c = program.Content;
        c.Tagline = Clean(request.Tagline);
        c.Summary = Clean(request.Summary);
        c.Highlights = CleanList(request.Highlights, "Highlights", ProgramContent.MaxHighlights, 200);
        c.Inclusions = CleanList(request.Inclusions, "Inclusions", ProgramContent.MaxListItems, 300);
        c.Exclusions = CleanList(request.Exclusions, "Exclusions", ProgramContent.MaxListItems, 300);
        c.Tags = CleanTags(request.Tags);
        c.TransportMode = request.TransportMode;
        c.TransportDetails = Clean(request.TransportDetails);
        c.DeparturePoint = Clean(request.DeparturePoint);
        if (request.MinGroupSize is { } min && request.MaxGroupSize is { } max && min > max)
            throw new ProgramValidationException("MinGroupSize cannot exceed MaxGroupSize.");
        c.MinGroupSize = request.MinGroupSize;
        c.MaxGroupSize = request.MaxGroupSize;
        c.PracticalInfo = Clean(request.PracticalInfo);
        c.CancellationPolicy = Clean(request.CancellationPolicy);
        c.PaymentTerms = Clean(request.PaymentTerms);
        c.InsurancePolicy = Clean(request.InsurancePolicy);
        c.ChildPolicy = Clean(request.ChildPolicy);

        ReplaceItinerary(program, request.Itinerary ?? []);
        if (slug is not null)
            program.Slug = slug;
    }

    /// A slug no other program has: the name's slug, suffixed with -2, -3, … when taken.
    public async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var root = Slug.From(name, Slug.MaxLength - 4);
        if (root.Length == 0)
            root = "program";
        var taken = await db.Programs
            .Where(p => p.Slug == root || p.Slug.StartsWith(root + "-"))
            .Select(p => p.Slug)
            .ToListAsync(ct);
        if (!taken.Contains(root))
            return root;
        for (var n = 2; ; n++)
        {
            var candidate = $"{root}-{n}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    private void ReplaceItinerary(Domain.Program program, ItineraryDayRequest[] days)
    {
        if (days.Length > ProgramItineraryDay.MaxDays)
            throw new ProgramValidationException($"An itinerary can have at most {ProgramItineraryDay.MaxDays} days.");
        var ordered = days.OrderBy(d => d.Day).ToList();
        for (var i = 0; i < ordered.Count; i++)
            if (ordered[i].Day != i + 1)
                throw new ProgramValidationException("Itinerary days must run 1, 2, 3… without gaps or repeats.");

        // Rows carry client-generated ids, so a day reached only through the navigation would be
        // tracked as Modified rather than Added; tell the context explicitly what is new and what goes.
        db.ProgramItineraryDays.RemoveRange(program.Itinerary.ToList());
        program.Itinerary.Clear();
        foreach (var day in ordered)
        {
            var row = new ProgramItineraryDay
            {
                ProgramId = program.Id,
                Day = day.Day,
                Title = day.Title.Trim(),
                Description = Clean(day.Description),
                Meals = day.Meals,
                Overnight = Clean(day.Overnight),
            };
            program.Itinerary.Add(row);
            db.ProgramItineraryDays.Add(row);
        }
    }

    private static string[] CleanList(string[]? items, string field, int maxItems, int maxLength)
    {
        var cleaned = (items ?? []).Select(i => i?.Trim() ?? "").Where(i => i.Length > 0).ToArray();
        if (cleaned.Length > maxItems)
            throw new ProgramValidationException($"{field} can have at most {maxItems} items.");
        if (cleaned.Any(i => i.Length > maxLength))
            throw new ProgramValidationException($"Each of {field} must be at most {maxLength} characters.");
        return cleaned;
    }

    private static string[] CleanTags(string[]? tags)
    {
        var cleaned = (tags ?? []).Select(t => t?.Trim().ToLowerInvariant() ?? "").Where(t => t.Length > 0)
            .Distinct().ToArray();
        if (cleaned.Length > ProgramContent.MaxTags)
            throw new ProgramValidationException($"At most {ProgramContent.MaxTags} tags.");
        var bad = cleaned.FirstOrDefault(t => t.Length > 40 || !Slug.IsValid(t));
        if (bad is not null)
            throw new ProgramValidationException($"Tag '{bad}' must be lowercase kebab-case, at most 40 characters.");
        return cleaned;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<Domain.Program> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Programs
            .Include(p => p.Departures)
            .Include(p => p.Hotels)
            .Include(p => p.Itinerary)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new ProgramNotFoundException($"Program {id} does not exist.");
}
