using System.Text.Json;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Catalog.Filtering;

public sealed class FilterValidationException(string message) : Exception(message);

// Translates a FilterSpec into a typed predicate over IQueryable<Offer>.
// We never build raw SQL from input — every condition goes through an explicit
// whitelist of (field, op) pairs.
//
// NOTE: nested any/all groups and parameter substitution are deferred to Increment 2.
public static class FilterResolver
{
    private static readonly HashSet<string> AllowedFields =
        new(StringComparer.OrdinalIgnoreCase) { "country", "city", "maxPrice", "board", "transport", "tag" };

    private static readonly HashSet<string> AllowedOps =
        new(StringComparer.OrdinalIgnoreCase) { "eq", "in", "lte", "contains" };

    public static IQueryable<Offer> Apply(IQueryable<Offer> q, FilterSpec spec)
    {
        if (spec.All == null) return q;

        foreach (var c in spec.All)
        {
            if (!AllowedFields.Contains(c.Field))
                throw new FilterValidationException($"Unknown filter field: {c.Field}");
            if (!AllowedOps.Contains(c.Op))
                throw new FilterValidationException($"Unknown filter op: {c.Op}");

            q = (c.Field.ToLowerInvariant(), c.Op.ToLowerInvariant()) switch
            {
                ("country", "eq") => q.Where(o => o.Country == GetString(c.Value)),
                ("country", "in") => ApplyCountryIn(q, GetStringArray(c.Value)),
                ("city", "eq") => q.Where(o => o.City == GetString(c.Value)),
                ("city", "in") => ApplyCityIn(q, GetStringArray(c.Value)),
                ("maxprice", "lte") => q.Where(o => o.Price <= GetDecimal(c.Value)),
                ("board", "eq") => ApplyBoardEq(q, GetString(c.Value)),
                ("transport", "eq") => ApplyTransportEq(q, GetString(c.Value)),
                ("tag", "contains") => q.Where(o => o.Tags.Contains(GetString(c.Value))),
                _ => throw new FilterValidationException($"Unsupported field/op combination: {c.Field}/{c.Op}"),
            };
        }
        return q;
    }

    private static IQueryable<Offer> ApplyCountryIn(IQueryable<Offer> q, string[] vs) =>
        q.Where(o => vs.Contains(o.Country));

    private static IQueryable<Offer> ApplyCityIn(IQueryable<Offer> q, string[] vs) =>
        q.Where(o => vs.Contains(o.City));

    private static IQueryable<Offer> ApplyBoardEq(IQueryable<Offer> q, string raw)
    {
        if (!Enum.TryParse<BoardBasis>(raw, true, out var b))
            throw new FilterValidationException($"Invalid board value: {raw}");
        return q.Where(o => o.BoardBasis == b);
    }

    private static IQueryable<Offer> ApplyTransportEq(IQueryable<Offer> q, string raw)
    {
        if (!Enum.TryParse<Transport>(raw, true, out var t))
            throw new FilterValidationException($"Invalid transport value: {raw}");
        return q.Where(o => o.Transport == t);
    }

    private static string GetString(JsonElement e) =>
        e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? string.Empty
            : throw new FilterValidationException($"Expected string, got {e.ValueKind}");

    private static decimal GetDecimal(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number
            ? e.GetDecimal()
            : throw new FilterValidationException($"Expected number, got {e.ValueKind}");

    private static string[] GetStringArray(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array)
            throw new FilterValidationException($"Expected array, got {e.ValueKind}");
        var arr = new List<string>();
        foreach (var item in e.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new FilterValidationException("Expected string array values");
            arr.Add(item.GetString() ?? string.Empty);
        }
        return arr.ToArray();
    }
}
