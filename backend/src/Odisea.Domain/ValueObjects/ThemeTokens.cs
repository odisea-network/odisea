namespace Odisea.Domain.ValueObjects;

public class ThemeTokens
{
    public Dictionary<string, string> Foundation { get; set; } = new();
    public Dictionary<string, string> Semantic { get; set; } = new();
    public Dictionary<string, string> Component { get; set; } = new();

    public static ThemeTokens Default() => new()
    {
        Foundation = new()
        {
            ["accent"]    = "#1a5a61",
            ["fontBody"]  = "Onest",
            ["fontHead"]  = "Onest",
            ["radius"]    = "8",
        },
        Semantic = new()
        {
            ["price"]   = "#0e1618",
            ["bg"]      = "#f7f9f8",
            ["surface"] = "#ffffff",
        },
        Component = new()
        {
            ["cardRatio"] = "4 / 3",
        },
    };
}
