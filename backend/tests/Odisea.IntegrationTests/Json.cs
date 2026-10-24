using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odisea.IntegrationTests;

/// Mirrors what an external API client sends: camelCase and enums by name.
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
