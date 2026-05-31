using System.Text.Json;

namespace Odisea.Domain.ValueObjects;

// Root semantics: All-as-AND ⨯ Any-as-OR ⨯ each Groups[i] are all ANDed together.
// Parameter substitution is still deferred to a later Increment 2 PR.
public class FilterSpec
{
    public List<FilterCondition> All { get; set; } = new();
    public List<FilterCondition> Any { get; set; } = new();
    public List<FilterGroup> Groups { get; set; } = new();
}

public class FilterCondition
{
    public string Field { get; set; } = string.Empty;
    public string Op { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}

public class FilterGroup
{
    public string Op { get; set; } = "all"; // "all" | "any" (case-insensitive)
    public List<FilterCondition> Conditions { get; set; } = new();
    public List<FilterGroup> Groups { get; set; } = new();
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
