using System.Text.Json;
using Odisea.Application.Themes;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.UnitTests;

public class ThemeExporterTests
{
    [Fact]
    public void ExportCss_produces_odc_scope_block()
    {
        var theme = DefaultTheme();
        var css = ThemeExporter.ExportCss(theme);

        Assert.Contains(".odc-scope {", css);
        Assert.Contains("--odc-accent:", css);
        Assert.Contains("--odc-bg:", css);
        Assert.Contains("--odc-card-ratio:", css);
        Assert.EndsWith("}", css.TrimEnd());
    }

    [Fact]
    public void ExportCss_converts_camelCase_keys_to_kebab_case()
    {
        var theme = DefaultTheme();
        theme.Tokens.Foundation["fontBody"] = "Onest";

        var css = ThemeExporter.ExportCss(theme);

        Assert.Contains("--odc-font-body:", css);
        Assert.DoesNotContain("--odc-fontBody:", css);
    }

    [Fact]
    public void ExportCss_includes_all_three_token_sections()
    {
        var theme = DefaultTheme();
        var css = ThemeExporter.ExportCss(theme);

        // Foundation token
        Assert.Contains($"--odc-accent: {theme.Tokens.Foundation["accent"]};", css);
        // Semantic token
        Assert.Contains($"--odc-bg: {theme.Tokens.Semantic["bg"]};", css);
        // Component token
        Assert.Contains($"--odc-card-ratio: {theme.Tokens.Component["cardRatio"]};", css);
    }

    [Fact]
    public void ExportJson_round_trips_all_token_groups()
    {
        var theme = DefaultTheme();
        var json = ThemeExporter.ExportJson(theme);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(theme.Name,    root.GetProperty("name").GetString());
        Assert.Equal(theme.Version, root.GetProperty("version").GetInt32());

        var tokens = root.GetProperty("tokens");
        Assert.Equal(
            theme.Tokens.Foundation["accent"],
            tokens.GetProperty("foundation").GetProperty("accent").GetString());
        Assert.Equal(
            theme.Tokens.Semantic["bg"],
            tokens.GetProperty("semantic").GetProperty("bg").GetString());
        Assert.Equal(
            theme.Tokens.Component["cardRatio"],
            tokens.GetProperty("component").GetProperty("cardRatio").GetString());
    }

    [Fact]
    public void ExportJson_is_indented()
    {
        var theme = DefaultTheme();
        var json = ThemeExporter.ExportJson(theme);

        Assert.Contains("\n", json);
        Assert.Contains("  ", json);
    }

    [Fact]
    public void ExportCss_empty_token_groups_produce_valid_block()
    {
        var theme = new Theme
        {
            Name    = "Empty",
            Status  = ThemeStatus.Draft,
            Version = 1,
            Tokens  = new ThemeTokens(),
        };

        var css = ThemeExporter.ExportCss(theme);

        Assert.Contains(".odc-scope {", css);
        Assert.EndsWith("}", css.TrimEnd());
    }

    // ---- helpers ----

    private static Theme DefaultTheme() => new()
    {
        Name    = "Test Theme",
        Status  = ThemeStatus.Draft,
        Version = 1,
        Tokens  = ThemeTokens.Default(),
    };
}
