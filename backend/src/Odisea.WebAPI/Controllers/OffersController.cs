using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Controllers;

[ApiController]
[Route("api/v1/offers")]
public class OffersController(IAppDbContext db, IOperatorContext operatorCtx) : ControllerBase
{
    // ── Public reads ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List(string? country, string? city, decimal? maxPrice, CancellationToken ct)
    {
        var q = db.Offers.Where(o => o.Status == OfferStatus.Published);
        if (!string.IsNullOrWhiteSpace(country)) q = q.Where(o => o.Country == country);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(o => o.City == city);
        if (maxPrice is { } mp) q = q.Where(o => o.Price <= mp);
        var list = await q.OrderBy(o => o.Price).ToListAsync(ct);
        var supplierNames = await SupplierNamesFor(list, ct);
        return Ok(list.Select(o => o.ToDto(supplierNames)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var o = await db.Offers
            .Include(x => x.PriceVariants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return NotFound();
        var supplierNames = await SupplierNamesFor([o], ct);
        return Ok(o.ToDto(supplierNames));
    }

    // ── Operator manual entry ──────────────────────────────────────────────────

    // The operator's own catalog, including drafts (the public List only shows
    // published offers across all suppliers).
    [Authorize(Policy = "OperatorAdmin")]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();
        var list = await db.Offers
            .Where(o => o.OwningOperatorId == operatorId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return Ok(list.Select(o => o.ToDto()));
    }

    [Authorize(Policy = "OperatorAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateOfferRequest req, CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();

        if (!TryBuildOffer(req.Title, req.Description, req.Country, req.City, req.Price,
                req.Currency, req.BoardBasis, req.Transport, req.DurationNights,
                req.StartDate, req.EndDate, req.Tags, req.ImageUrl,
                out var board, out var transport, out var error))
            return Problem(title: "Validation", detail: error, statusCode: 400);

        var offer = new Offer
        {
            Title = req.Title,
            Description = req.Description,
            OwnerType = OwnerType.Operator,
            Visibility = Visibility.PlatformShared,
            OwningOperatorId = operatorId,
            Country = req.Country,
            City = req.City,
            Price = req.Price,
            Currency = req.Currency,
            BoardBasis = board,
            Transport = transport,
            DurationNights = req.DurationNights,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Tags = req.Tags ?? [],
            ImageUrl = req.ImageUrl ?? string.Empty,
            Status = OfferStatus.Draft,
        };
        db.Offers.Add(offer);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = offer.Id }, offer.ToDto());
    }

    [Authorize(Policy = "OperatorAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOfferRequest req, CancellationToken ct)
    {
        var (offer, error) = await FindOwnedOffer(id, ct);
        if (error is not null) return error;

        if (!TryBuildOffer(req.Title, req.Description, req.Country, req.City, req.Price,
                req.Currency, req.BoardBasis, req.Transport, req.DurationNights,
                req.StartDate, req.EndDate, req.Tags, req.ImageUrl,
                out var board, out var transport, out var validationError))
            return Problem(title: "Validation", detail: validationError, statusCode: 400);

        offer!.Title = req.Title;
        offer.Description = req.Description;
        offer.Country = req.Country;
        offer.City = req.City;
        offer.Price = req.Price;
        offer.Currency = req.Currency;
        offer.BoardBasis = board;
        offer.Transport = transport;
        offer.DurationNights = req.DurationNights;
        offer.StartDate = req.StartDate;
        offer.EndDate = req.EndDate;
        offer.Tags = req.Tags ?? [];
        offer.ImageUrl = req.ImageUrl ?? string.Empty;
        offer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(offer.ToDto());
    }

    [Authorize(Policy = "OperatorAdmin")]
    [HttpPost("{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct) => SetStatus(id, OfferStatus.Published, ct);

    [Authorize(Policy = "OperatorAdmin")]
    [HttpPost("{id:guid}/unpublish")]
    public Task<IActionResult> Unpublish(Guid id, CancellationToken ct) => SetStatus(id, OfferStatus.Draft, ct);

    private async Task<IActionResult> SetStatus(Guid id, OfferStatus status, CancellationToken ct)
    {
        var (offer, error) = await FindOwnedOffer(id, ct);
        if (error is not null) return error;

        offer!.Status = status;
        offer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(offer.ToDto());
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    // Loads an offer and confirms it belongs to the calling operator. Returns a
    // 404 for both "not found" and "not yours" so we don't leak existence.
    private async Task<(Offer? offer, IActionResult? error)> FindOwnedOffer(Guid id, CancellationToken ct)
    {
        var operatorId = operatorCtx.RequireOperator();
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (offer is null || offer.OwningOperatorId != operatorId)
            return (null, NotFound());
        return (offer, null);
    }

    private static bool TryBuildOffer(
        string title, string description, string country, string city, decimal price,
        string currency, string boardBasis, string transport, int durationNights,
        DateOnly? startDate, DateOnly? endDate, List<string>? tags, string? imageUrl,
        out BoardBasis board, out Transport transportEnum, out string? error)
    {
        board = default;
        transportEnum = default;
        error = null;

        if (string.IsNullOrWhiteSpace(title))
            return Fail("Title is required.", out error);
        if (string.IsNullOrWhiteSpace(country))
            return Fail("Country is required.", out error);
        if (price < 0)
            return Fail("Price must be non-negative.", out error);
        if (!Enum.TryParse(boardBasis, ignoreCase: true, out board))
            return Fail($"Invalid board basis: {boardBasis}.", out error);
        if (!Enum.TryParse(transport, ignoreCase: true, out transportEnum))
            return Fail($"Invalid transport: {transport}.", out error);

        return true;
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> SupplierNamesFor(
        IReadOnlyCollection<Offer> offers, CancellationToken ct)
    {
        var ids = offers
            .Where(o => o.Source is not null)
            .Select(o => o.Source!.SupplierConnectionId)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await db.SupplierConnections
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }
}
