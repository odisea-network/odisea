using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.WebAPI.Endpoints;

public static class OffersEndpoints
{
    public static IEndpointRouteBuilder MapOffersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/offers").WithTags("Offers");

        group.MapGet("/", async (
            [FromServices] IAppDbContext db,
            string? country, string? city, decimal? maxPrice,
            CancellationToken ct) =>
        {
            var q = db.Offers.Where(o => o.Status == OfferStatus.Published);
            if (!string.IsNullOrWhiteSpace(country)) q = q.Where(o => o.Country == country);
            if (!string.IsNullOrWhiteSpace(city)) q = q.Where(o => o.City == city);
            if (maxPrice is { } mp) q = q.Where(o => o.Price <= mp);
            var list = await q.OrderBy(o => o.Price).ToListAsync(ct);
            return Results.Ok(list.Select(Mappings.ToDto));
        }).WithName("ListOffers");

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] IAppDbContext db, CancellationToken ct) =>
        {
            var o = await db.Offers.FirstOrDefaultAsync(x => x.Id == id, ct);
            return o is null ? Results.NotFound() : Results.Ok(o.ToDto());
        }).WithName("GetOffer");

        return app;
    }
}
