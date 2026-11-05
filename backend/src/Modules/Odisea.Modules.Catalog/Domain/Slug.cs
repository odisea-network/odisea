using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Odisea.Modules.Catalog.Domain;

/// Lowercase kebab-case identifiers for URLs and tags. Bulgarian Cyrillic is transliterated with
/// the streamlined system (the one on passports), other scripts lose their diacritics.
public static partial class Slug
{
    public const int MaxLength = 120;

    private static readonly Dictionary<char, string> Cyrillic = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ж'] = "zh",
        ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f",
        ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sht", ['ъ'] = "a", ['ь'] = "y",
        ['ю'] = "yu", ['я'] = "ya", ['ы'] = "y", ['э'] = "e", ['ё'] = "yo",
    };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && Pattern().IsMatch(value);

    /// Empty when nothing usable survives (e.g. a name made of punctuation only).
    public static string From(string text, int maxLength = MaxLength)
    {
        var latin = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (Cyrillic.TryGetValue(ch, out var mapped))
                latin.Append(mapped);
            else if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                latin.Append(ch);
        }

        var slug = new StringBuilder(latin.Length);
        var pendingDash = false;
        foreach (var ch in latin.ToString())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && slug.Length > 0)
                    slug.Append('-');
                pendingDash = false;
                slug.Append(ch);
            }
            else
            {
                pendingDash = true;
            }
        }

        var result = slug.ToString();
        if (result.Length > maxLength)
        {
            // Cut on a word boundary when there is one, so "antalya-easter-20" does not happen.
            result = result[..maxLength];
            var lastDash = result.LastIndexOf('-');
            result = lastDash > 0 ? result[..lastDash] : result;
        }
        return result;
    }
}
