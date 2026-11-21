using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Features.Ledger;

/// The agency's own list of tour operators. Scoped by the caller's agency id; writes need the
/// ReservationLedger feature, reads keep working after a downgrade.
public sealed class TourOperatorService(WorkspaceDbContext db, IAgencyEntitlements entitlements, IClock clock)
{
    public async Task<List<TourOperatorDto>> ListAsync(Guid agencyId, bool includeArchived, CancellationToken ct)
    {
        var query = db.TourOperators.AsNoTracking().Where(o => o.AgencyId == agencyId);
        if (!includeArchived)
            query = query.Where(o => !o.IsArchived);
        return [.. (await query.OrderBy(o => o.NameKey).ToListAsync(ct)).Select(o => o.ToDto())];
    }

    public async Task<TourOperatorDto> CreateAsync(Guid agencyId, SaveTourOperatorRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var name = LedgerRules.Required(request.Name, 200, "name");
        await EnsureNameFreeAsync(agencyId, name, except: null, ct);

        var tourOperator = new TourOperator
        {
            AgencyId = agencyId,
            Name = name,
            NameKey = TourOperator.KeyOf(name),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };
        ApplyContact(tourOperator, request);
        db.TourOperators.Add(tourOperator);
        await db.SaveChangesAsync(ct);
        return tourOperator.ToDto();
    }

    public async Task<TourOperatorDto> UpdateAsync(Guid id, Guid agencyId, SaveTourOperatorRequest request, CancellationToken ct)
    {
        await entitlements.RequireAsync(agencyId, AgencyFeature.ReservationLedger, ct);

        var tourOperator = await LoadAsync(id, agencyId, ct);
        var name = LedgerRules.Required(request.Name, 200, "name");
        await EnsureNameFreeAsync(agencyId, name, except: id, ct);

        tourOperator.Name = name;
        tourOperator.NameKey = TourOperator.KeyOf(name);
        ApplyContact(tourOperator, request);
        tourOperator.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return tourOperator.ToDto();
    }

    /// Archived operators stay on their reservations but cannot be picked for new ones.
    public async Task<TourOperatorDto> SetArchivedAsync(Guid id, Guid agencyId, LedgerActor actor, bool archived, CancellationToken ct)
    {
        if (!actor.IsAdmin)
            throw new LedgerAdminRequiredException("Only an agency admin can archive or restore a tour operator.");

        var tourOperator = await LoadAsync(id, agencyId, ct);
        if (tourOperator.IsArchived != archived)
        {
            tourOperator.IsArchived = archived;
            tourOperator.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return tourOperator.ToDto();
    }

    private async Task<TourOperator> LoadAsync(Guid id, Guid agencyId, CancellationToken ct) =>
        await db.TourOperators.FirstOrDefaultAsync(o => o.Id == id && o.AgencyId == agencyId, ct)
        ?? throw new LedgerNotFoundException();

    private async Task EnsureNameFreeAsync(Guid agencyId, string name, Guid? except, CancellationToken ct)
    {
        var key = TourOperator.KeyOf(name);
        if (await db.TourOperators.AnyAsync(o => o.AgencyId == agencyId && o.NameKey == key && o.Id != except, ct))
            throw new LedgerConflictException("operator_name_taken", $"A tour operator named '{name}' already exists.");
    }

    private static void ApplyContact(TourOperator tourOperator, SaveTourOperatorRequest request)
    {
        tourOperator.ContactEmail = LedgerRules.Optional(request.ContactEmail, 320, "contactEmail");
        tourOperator.ContactPhone = LedgerRules.Optional(request.ContactPhone, 30, "contactPhone");
        tourOperator.Notes = LedgerRules.Optional(request.Notes, 2000, "notes");
    }
}
