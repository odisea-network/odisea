using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Collections;
using Odisea.Application.Catalog.Dtos;
using Odisea.Application.Catalog.Filtering;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.WebAPI.Endpoints;

public static class CollectionsEndpoints
{
    public static IEndpointRouteBuilder MapCollectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/collections").WithTags("Collections");

        group.MapGet("/", async ([FromServices] IAppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Collections.OrderBy(c => c.Name).ToListAsync(ct);
            return Results.Ok(list.Select(Mappings.ToDto));
        }).WithName("ListCollections");

        group.MapGet("/{idOrSlug}", async (string idOrSlug, [FromServices] IAppDbContext db, CancellationToken ct) =>
        {
            var c = await FindCollectionAsync(db, idOrSlug, ct);
            return c is null ? Results.NotFound() : Results.Ok(c.ToDto());
        }).WithName("GetCollection");

        group.MapGet("/{idOrSlug}/offers", async (
            string idOrSlug, [FromServices] IAppDbContext db, CancellationToken ct) =>
        {
            var c = await FindCollectionAsync(db, idOrSlug, ct);
            if (c is null) return Results.NotFound();
            try
            {
                var offers = await CollectionResolver.ResolveAsync(c, db.Offers.AsQueryable(), ct);
                return Results.Ok(offers.Select(Mappings.ToDto));
            }
            catch (FilterValidationException ex)
            {
                return Results.Problem(title: "Invalid filter", detail: ex.Message, statusCode: 400);
            }
        }).WithName("ResolveCollection");

        group.MapPost("/", async (
            CreateCollectionRequest req, [FromServices] IAppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Slug))
                return Results.Problem(title: "Validation", detail: "Name and Slug are required", statusCode: 400);

            try { FilterResolver.Apply(db.Offers.AsQueryable(), req.Filter); }
            catch (FilterValidationException ex)
            {
                return Results.Problem(title: "Invalid filter", detail: ex.Message, statusCode: 400);
            }

            var entity = new Collection
            {
                AgencyId = req.AgencyId,
                Name = req.Name,
                Slug = req.Slug,
                Status = CollectionStatus.Draft,
                Filter = req.Filter,
                Sort = req.Sort ?? new SortSpec("price", "asc"),
                PinnedOfferIds = req.PinnedOfferIds ?? new(),
                ExcludedOfferIds = req.ExcludedOfferIds ?? new(),
            };
            db.Collections.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/collections/{entity.Id}", entity.ToDto());
        }).WithName("CreateCollection");

        return app;
    }

    private static async Task<Collection?> FindCollectionAsync(IAppDbContext db, string idOrSlug, CancellationToken ct)
    {
        if (Guid.TryParse(idOrSlug, out var id))
            return await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
        return await db.Collections.FirstOrDefaultAsync(c => c.Slug == idOrSlug, ct);
    }
}
