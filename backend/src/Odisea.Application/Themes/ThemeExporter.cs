using System.Text;
using System.Text.Json;
using Odisea.Domain.Entities;

namespace Odisea.Application.Themes;

public static class ThemeExporter
{
    public static string ExportCss(Theme theme)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/* Odisea theme export — auto-generated */");
        sb.AppendLine(".odc-scope {");

        foreach (var (key, value) in theme.Tokens.Foundation)
            sb.AppendLine($"  --odc-{CssName(key)}: {value};");

        foreach (var (key, value) in theme.Tokens.Semantic)
            sb.AppendLine($"  --odc-{CssName(key)}: {value};");

        foreach (var (key, value) in theme.Tokens.Component)
            sb.AppendLine($"  --odc-{CssName(key)}: {value};");

        sb.Append("}");
        return sb.ToString();
    }

    public static string ExportJson(Theme theme)
    {
        var tree = new
        {
            name    = theme.Name,
            version = theme.Version,
            tokens  = new
            {
                foundation = theme.Tokens.Foundation,
                semantic   = theme.Tokens.Semantic,
                component  = theme.Tokens.Component,
            }
        };

        return JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = true });
    }

    // camelCase → kebab-case, e.g. "fontBody" → "font-body"
    private static string CssName(string camel)
    {
        var sb = new StringBuilder();
        foreach (var ch in camel)
        {
            if (char.IsUpper(ch))
            {
                sb.Append('-');
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }
}
