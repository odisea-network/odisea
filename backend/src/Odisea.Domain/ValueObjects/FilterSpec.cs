using System.Text.Json;

namespace Odisea.Domain.ValueObjects;

// FLAT filter spec for Increment 1: all conditions ANDed together.
// Nested any/all groups and parameter substitution are deferred to Increment 2.
public class FilterSpec
{
    public List<FilterCondition> All { get; set; } = new();
}

public class FilterCondition
{
    public string Field { get; set; } = string.Empty;
    public string Op { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}

public record SortSpec(string Field, string Direction);

// Parameter binding is deferred to Increment 2. We persist the definitions now
// so seeded collections can declare them without a runtime contract yet.
public class ParameterDef
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string | number | enum | date
    public JsonElement? Default { get; set; }
    public List<string>? AllowedValues { get; set; }
}
