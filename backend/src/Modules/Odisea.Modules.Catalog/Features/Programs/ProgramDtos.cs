using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Features.Programs;

public record ProgramDto(
    Guid Id, string Name, string Slug, Guid DestinationId, string ProviderCode,
    string? Season, string? Description, string? Tagline, string? Summary, IReadOnlyList<string> Tags,
    string? CoverImageUrl, string Status, DateTimeOffset CreatedAt);

public record ProgramDetailDto(
    Guid Id, string Name, string Slug, Guid DestinationId, string ProviderCode,
    string? Season, string? Description, string Status,
    ProgramContentDto Content,
    IReadOnlyList<ItineraryDayDto> Itinerary,
    IReadOnlyList<ProgramImageDto> Images,
    IReadOnlyList<DepartureDto> Departures,
    IReadOnlyList<Guid> HotelIds,
    // Operator view only; null in the agency catalogue.
    ProgramReadinessDto? Readiness);

public record ProgramContentDto(
    string? Tagline,
    string? Summary,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> Inclusions,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> Tags,
    TransportMode TransportMode,
    string? TransportDetails,
    string? DeparturePoint,
    int? MinGroupSize,
    int? MaxGroupSize,
    string? PracticalInfo,
    string? CancellationPolicy,
    string? PaymentTerms,
    string? InsurancePolicy,
    string? ChildPolicy);

public record ItineraryDayDto(Guid Id, int Day, string Title, string? Description, MealPlan Meals, string? Overnight);

public record ProgramImageDto(
    Guid Id, string Url, string ContentType, int SizeBytes, int Width, int Height,
    string? Caption, string? AltText, int SortOrder, bool IsCover, DateTimeOffset CreatedAt);

public record ReadinessCheckDto(string Code, string Title, string Detail, bool Required, bool Passed);

public record ProgramReadinessDto(int Score, bool CanPublish, IReadOnlyList<ReadinessCheckDto> Checks);

public record DepartureDto(Guid Id, DateOnly StartDate, DateOnly EndDate, int Nights, string? TransportNote);

public record CreateProgramRequest(
    [Required, MaxLength(200)] string Name,
    [Required] Guid DestinationId,
    [Required, MaxLength(30)] string ProviderCode,
    [MaxLength(50)] string? Season,
    [MaxLength(Domain.Program.MaxDescriptionLength)] string? Description,
    // Optional: everything the content endpoint accepts, so one call can create a finished program.
    SaveProgramContentRequest? Content = null);

public record UpdateProgramRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(50)] string? Season,
    [MaxLength(Domain.Program.MaxDescriptionLength)] string? Description);

/// Replaces the whole content block and itinerary; omitted lists mean empty.
public record SaveProgramContentRequest(
    [MaxLength(160)] string? Tagline,
    [MaxLength(600)] string? Summary,
    string[]? Highlights,
    string[]? Inclusions,
    string[]? Exclusions,
    string[]? Tags,
    TransportMode TransportMode,
    [MaxLength(4000)] string? TransportDetails,
    [MaxLength(200)] string? DeparturePoint,
    [Range(1, 999)] int? MinGroupSize,
    [Range(1, 999)] int? MaxGroupSize,
    [MaxLength(8000)] string? PracticalInfo,
    [MaxLength(8000)] string? CancellationPolicy,
    [MaxLength(4000)] string? PaymentTerms,
    [MaxLength(4000)] string? InsurancePolicy,
    [MaxLength(2000)] string? ChildPolicy,
    ItineraryDayRequest[]? Itinerary,
    // Null keeps the current slug.
    [MaxLength(Slug.MaxLength)] string? Slug = null);

public record ItineraryDayRequest(
    [Range(1, ProgramItineraryDay.MaxDays)] int Day,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    MealPlan Meals,
    [MaxLength(200)] string? Overnight);

public record CreateDepartureRequest(
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    [MaxLength(500)] string? TransportNote);

public record UpdateProgramImageRequest(
    [MaxLength(200)] string? Caption,
    [MaxLength(300)] string? AltText,
    bool IsCover);

public record ReorderProgramImagesRequest([Required] Guid[] ImageIds);

public static class ProgramDtoMapping
{
    public const string PublicImagePath = "/api/v1/public/program-images/";

    public static string ImageUrl(Guid imageId) => PublicImagePath + imageId;

    public static ProgramDto ToDto(this Domain.Program p, Guid? coverImageId) => new(
        p.Id, p.Name, p.Slug, p.DestinationId, p.ProviderCode, p.Season, p.Description,
        p.Content.Tagline, p.Content.Summary, p.Content.Tags,
        coverImageId is { } cover ? ImageUrl(cover) : null,
        p.Status.ToString(), p.CreatedAt);

    public static DepartureDto ToDto(this ProgramDeparture d) =>
        new(d.Id, d.StartDate, d.EndDate, d.Period.Nights, d.TransportNote);

    public static ProgramContentDto ToDto(this ProgramContent c) => new(
        c.Tagline, c.Summary, c.Highlights, c.Inclusions, c.Exclusions, c.Tags,
        c.TransportMode, c.TransportDetails, c.DeparturePoint, c.MinGroupSize, c.MaxGroupSize,
        c.PracticalInfo, c.CancellationPolicy, c.PaymentTerms, c.InsurancePolicy, c.ChildPolicy);

    public static ItineraryDayDto ToDto(this ProgramItineraryDay d) =>
        new(d.Id, d.Day, d.Title, d.Description, d.Meals, d.Overnight);

    public static ReadinessCheckDto ToDto(this ReadinessCheck c) => new(c.Code, c.Title, c.Detail, c.Required, c.Passed);

    public static ProgramReadinessDto ToDto(this ReadinessReport r) =>
        new(r.Score, r.CanPublish, [.. r.Checks.Select(c => c.ToDto())]);

    public static ProgramDetailDto ToDetailDto(
        this Domain.Program p, IReadOnlyList<ProgramImageDto> images, ProgramReadinessDto? readiness) => new(
        p.Id, p.Name, p.Slug, p.DestinationId, p.ProviderCode, p.Season, p.Description, p.Status.ToString(),
        p.Content.ToDto(),
        [.. p.Itinerary.OrderBy(d => d.Day).Select(d => d.ToDto())],
        images,
        [.. p.Departures.OrderBy(d => d.StartDate).Select(d => d.ToDto())],
        [.. p.Hotels.Select(h => h.HotelId)],
        readiness);
}
