using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Data.Configurations;

// Value comparers so EF Core can detect changes on mutable jsonb-backed properties.
// Cheap and correct enough for Increment 1: serialize-and-compare.
internal static class JsonValueComparers
{
    public static readonly ValueComparer<List<string>> StringList =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<string>>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? new List<string>());

    public static readonly ValueComparer<List<Guid>> GuidList =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<Guid>>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? new List<Guid>());

    public static readonly ValueComparer<FilterSpec> FilterSpec =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<FilterSpec>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? new FilterSpec());

    public static readonly ValueComparer<List<ParameterDef>> ParameterList =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<ParameterDef>>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? new List<ParameterDef>());

    public static readonly ValueComparer<string[]> StringArray =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<string[]>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? Array.Empty<string>());

    // SortSpec is an immutable record — a no-op snapshot is fine.
    public static readonly ValueComparer<SortSpec> Sort =
        new(
            (a, b) => a == b,
            v => v.GetHashCode(),
            v => v);

    public static readonly ValueComparer<ThemeTokens> ThemeTokens =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<ThemeTokens>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? new ThemeTokens());

    public static readonly ValueComparer<ExperienceConfig> ExperienceConfig =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<ExperienceConfig>(
                    JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null) ?? new ExperienceConfig());
}
