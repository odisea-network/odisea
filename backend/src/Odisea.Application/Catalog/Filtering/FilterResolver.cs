using System.Linq.Expressions;
using System.Text.Json;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Catalog.Filtering;

public sealed class FilterValidationException(string message) : Exception(message);

// Translates a FilterSpec into a typed predicate over IQueryable<Offer>.
// Every leaf goes through an explicit whitelist of (field, op) pairs — raw SQL
// is never built from input.
//
// Root semantics: All-as-AND ⨯ Any-as-OR ⨯ each Groups[i] are all ANDed together.
// Empty group = identity true (no-op), so a UI builder that drops the last child
// gets a usable result instead of a 400.
//
// Parameter substitution remains deferred to a later Increment 2 PR.
public static class FilterResolver
{
    private static readonly HashSet<string> AllowedFields =
        new(StringComparer.OrdinalIgnoreCase) { "country", "city", "maxPrice", "board", "transport", "tag" };

    private static readonly HashSet<string> AllowedOps =
        new(StringComparer.OrdinalIgnoreCase) { "eq", "in", "lte", "contains" };

    private const int MaxDepth = 4;
    private const int MaxNodeCount = 64;

    public static IQueryable<Offer> Apply(IQueryable<Offer> q, FilterSpec spec)
    {
        var all = spec.All ?? new();
        var any = spec.Any ?? new();
        var groups = spec.Groups ?? new();

        if (all.Count == 0 && any.Count == 0 && groups.Count == 0)
            return q;

        var p = Expression.Parameter(typeof(Offer), "o");
        var nodeCount = 0;
        var parts = new List<Expression>();

        if (all.Count > 0)
            parts.Add(Combine(all.Select(c => CountAndBuildLeaf(c, p, ref nodeCount)), Expression.AndAlso));

        if (any.Count > 0)
            parts.Add(Combine(any.Select(c => CountAndBuildLeaf(c, p, ref nodeCount)), Expression.OrElse));

        foreach (var g in groups)
        {
            nodeCount++;
            CheckNodeCount(nodeCount);
            parts.Add(BuildGroup(g, p, depth: 1, ref nodeCount));
        }

        var body = parts.Aggregate(Expression.AndAlso);
        var lambda = Expression.Lambda<Func<Offer, bool>>(body, p);
        return q.Where(lambda);
    }

    private static Expression BuildGroup(FilterGroup g, ParameterExpression p, int depth, ref int nodeCount)
    {
        if (depth > MaxDepth)
            throw new FilterValidationException($"Filter nesting too deep (max {MaxDepth})");

        var op = (g.Op ?? "all").ToLowerInvariant();
        if (op != "all" && op != "any")
            throw new FilterValidationException($"Unknown group op: {g.Op}");

        var conditions = g.Conditions ?? new();
        var subGroups = g.Groups ?? new();

        var bodies = new List<Expression>();

        foreach (var c in conditions)
            bodies.Add(CountAndBuildLeaf(c, p, ref nodeCount));

        foreach (var sub in subGroups)
        {
            nodeCount++;
            CheckNodeCount(nodeCount);
            bodies.Add(BuildGroup(sub, p, depth + 1, ref nodeCount));
        }

        if (bodies.Count == 0)
            return Expression.Constant(true); // empty group is identity

        var combine = op == "any"
            ? (Func<Expression, Expression, Expression>)Expression.OrElse
            : Expression.AndAlso;
        return Combine(bodies, combine);
    }

    private static Expression Combine(IEnumerable<Expression> bodies, Func<Expression, Expression, Expression> combine) =>
        bodies.Aggregate(combine);

    private static Expression CountAndBuildLeaf(FilterCondition c, ParameterExpression p, ref int nodeCount)
    {
        nodeCount++;
        CheckNodeCount(nodeCount);
        return BuildLeafBody(c, p);
    }

    private static void CheckNodeCount(int count)
    {
        if (count > MaxNodeCount)
            throw new FilterValidationException($"Filter has too many nodes (max {MaxNodeCount})");
    }

    private static Expression BuildLeafBody(FilterCondition c, ParameterExpression p)
    {
        if (!AllowedFields.Contains(c.Field))
            throw new FilterValidationException($"Unknown filter field: {c.Field}");
        if (!AllowedOps.Contains(c.Op))
            throw new FilterValidationException($"Unknown filter op: {c.Op}");

        Expression<Func<Offer, bool>> lambda = (c.Field.ToLowerInvariant(), c.Op.ToLowerInvariant()) switch
        {
            ("country", "eq") => CountryEq(GetString(c.Value)),
            ("country", "in") => CountryIn(GetStringArray(c.Value)),
            ("city", "eq") => CityEq(GetString(c.Value)),
            ("city", "in") => CityIn(GetStringArray(c.Value)),
            ("maxprice", "lte") => MaxPriceLte(GetDecimal(c.Value)),
            ("board", "eq") => BoardEq(ParseEnum<BoardBasis>(GetString(c.Value), "board")),
            ("transport", "eq") => TransportEq(ParseEnum<Transport>(GetString(c.Value), "transport")),
            ("tag", "contains") => TagContains(GetString(c.Value)),
            _ => throw new FilterValidationException($"Unsupported field/op combination: {c.Field}/{c.Op}"),
        };
        return new ParameterReplacer(lambda.Parameters[0], p).Visit(lambda.Body)!;
    }

    private static Expression<Func<Offer, bool>> CountryEq(string v) => o => o.Country == v;
    private static Expression<Func<Offer, bool>> CountryIn(string[] vs) => o => vs.Contains(o.Country);
    private static Expression<Func<Offer, bool>> CityEq(string v) => o => o.City == v;
    private static Expression<Func<Offer, bool>> CityIn(string[] vs) => o => vs.Contains(o.City);
    private static Expression<Func<Offer, bool>> MaxPriceLte(decimal v) => o => o.Price <= v;
    private static Expression<Func<Offer, bool>> BoardEq(BoardBasis b) => o => o.BoardBasis == b;
    private static Expression<Func<Offer, bool>> TransportEq(Transport t) => o => o.Transport == t;
    private static Expression<Func<Offer, bool>> TagContains(string v) => o => o.Tags.Contains(v);

    private static T ParseEnum<T>(string raw, string fieldName) where T : struct
    {
        if (!Enum.TryParse<T>(raw, true, out var v))
            throw new FilterValidationException($"Invalid {fieldName} value: {raw}");
        return v;
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

    // Swaps the lambda's auto-generated parameter for the shared one so all
    // leaves and groups reference the same Offer instance when combined.
    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
