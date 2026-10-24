using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Odisea.Modules.Documents.Domain;

namespace Odisea.Modules.Documents.Features.Contracts;

public sealed record TemplateInspection(IReadOnlyList<string> Placeholders);

public sealed record RenderResult(byte[] Content, IReadOnlyList<string> Unresolved);

/// Fills {{Placeholder}} tokens in a .docx (body, headers, footers, footnotes). Word splits text
/// into runs at arbitrary points (spell-check, formatting, edit history), so a token is matched on
/// the paragraph's joined text and written back into the run where it starts; neighbouring runs
/// keep their own formatting. Pure and stateless, safe as a singleton.
public sealed partial class DocxTemplateRenderer
{
    public const int MaxTemplateBytes = 5 * 1024 * 1024;

    private const string WordMainContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_#]+)*)\s*\}\}")]
    private static partial Regex Placeholder();

    /// Validates an uploaded template and lists its placeholders in order of first appearance.
    public TemplateInspection Inspect(byte[] bytes)
    {
        if (bytes.Length == 0)
            throw new ContractValidationException("The template file is empty.");
        if (bytes.Length > MaxTemplateBytes)
            throw new ContractValidationException($"The template exceeds {MaxTemplateBytes / 1024 / 1024} MB.");
        if (bytes.Length < ZipMagic.Length || !bytes[..ZipMagic.Length].SequenceEqual(ZipMagic))
            throw new ContractValidationException("The file is not a .docx document.");

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var document = WordprocessingDocument.Open(stream, isEditable: false);
            var main = document.MainDocumentPart;
            if (main?.Document?.Body is null)
                throw new ContractValidationException("The file is not a Word document.");
            if (main.ContentType != WordMainContentType || main.VbaProjectPart is not null)
                throw new ContractValidationException("Macro-enabled documents are not accepted; save the template as .docx.");

            var placeholders = Roots(main)
                .SelectMany(PlaceholderNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new TemplateInspection(placeholders);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException or IOException)
        {
            throw new ContractValidationException("The file is not a valid .docx document.");
        }
    }

    public RenderResult Render(byte[] template, MergeData data)
    {
        using var stream = new MemoryStream();
        stream.Write(template);
        stream.Position = 0;

        var unresolved = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var document = WordprocessingDocument.Open(stream, isEditable: true))
        {
            var main = document.MainDocumentPart
                ?? throw new ContractValidationException("The template has no document body.");
            foreach (var root in Roots(main))
            {
                ExpandRepeatingRows(root, data);
                foreach (var paragraph in root.Descendants<Paragraph>().ToList())
                    ReplaceInParagraph(paragraph, name => data.Scalars.GetValueOrDefault(name), unresolved);
                root.Save();
            }
        }

        return new RenderResult(stream.ToArray(), [.. unresolved]);
    }

    private static IEnumerable<OpenXmlPartRootElement> Roots(MainDocumentPart main)
    {
        if (main.Document is { } document)
            yield return document;
        foreach (var part in main.HeaderParts)
            if (part.Header is { } header)
                yield return header;
        foreach (var part in main.FooterParts)
            if (part.Footer is { } footer)
                yield return footer;
        if (main.FootnotesPart?.Footnotes is { } footnotes)
            yield return footnotes;
        if (main.EndnotesPart?.Endnotes is { } endnotes)
            yield return endnotes;
    }

    /// A table row that mentions a collection ("{{Passengers.FullName}}") is cloned once per item.
    private static void ExpandRepeatingRows(OpenXmlElement root, MergeData data)
    {
        foreach (var row in root.Descendants<TableRow>().ToList())
        {
            // A row nested inside an already-expanded row is detached by now.
            if (!row.Ancestors().Contains(root))
                continue;

            var collection = PlaceholderNames(row)
                .Select(data.CollectionOf)
                .FirstOrDefault(name => name is not null);
            if (collection is null)
                continue;

            foreach (var item in data.Collections[collection])
            {
                var clone = (TableRow)row.CloneNode(true);
                foreach (var paragraph in clone.Descendants<Paragraph>().ToList())
                    ReplaceInParagraph(paragraph, name => MergeData.ResolveItem(collection, item, name), unresolved: null);
                row.InsertBeforeSelf(clone);
            }
            row.Remove();
        }
    }

    private static IEnumerable<string> PlaceholderNames(OpenXmlElement scope) =>
        scope.Descendants<Paragraph>()
            .Select(paragraph => string.Concat(OwnTexts(paragraph).Select(t => t.Text)))
            .SelectMany(text => Placeholder().Matches(text).Select(m => m.Groups[1].Value));

    private static void ReplaceInParagraph(Paragraph paragraph, Func<string, string?> resolve, ISet<string>? unresolved)
    {
        var texts = OwnTexts(paragraph);
        if (texts.Count == 0)
            return;
        var joined = string.Concat(texts.Select(t => t.Text));
        var matches = Placeholder().Matches(joined);
        if (matches.Count == 0)
            return;

        var starts = new int[texts.Count];
        for (int i = 0, offset = 0; i < texts.Count; i++)
        {
            starts[i] = offset;
            offset += texts[i].Text.Length;
        }

        // Back to front, so the offsets of earlier matches stay valid while later text changes.
        for (var m = matches.Count - 1; m >= 0; m--)
        {
            var match = matches[m];
            var name = match.Groups[1].Value;
            var value = resolve(name);
            if (value is null)
            {
                unresolved?.Add(name);
                continue;
            }
            Splice(texts, starts, match.Index, match.Index + match.Length, value);
        }
    }

    /// Replaces joined-text range [from, to) with value. The value lands in the run where the
    /// token starts; text after the token stays in its own run, so formatting survives.
    private static void Splice(List<Text> texts, int[] starts, int from, int to, string value)
    {
        var first = NodeAt(texts, starts, from);
        var last = NodeAt(texts, starts, to - 1);
        var prefix = texts[first].Text[..(from - starts[first])];
        var suffix = texts[last].Text[(to - starts[last])..];

        for (var i = first + 1; i < last; i++)
            texts[i].Text = string.Empty;
        if (last != first)
        {
            texts[last].Text = suffix;
            suffix = string.Empty;
        }
        WriteValue(texts[first], prefix, value, suffix);
    }

    private static int NodeAt(List<Text> texts, int[] starts, int offset)
    {
        for (var i = texts.Count - 1; i >= 0; i--)
            if (starts[i] <= offset && offset < starts[i] + texts[i].Text.Length)
                return i;
        throw new InvalidOperationException("Placeholder offset outside the paragraph text.");
    }

    /// Line breaks in a value (addresses, multi-line terms) become Word line breaks in the same run.
    private static void WriteValue(Text node, string prefix, string value, string suffix)
    {
        var lines = value.Replace("\r\n", "\n").Split('\n');
        node.Text = prefix + lines[0] + (lines.Length == 1 ? suffix : string.Empty);
        node.Space = SpaceProcessingModeValues.Preserve;
        if (lines.Length == 1)
            return;

        OpenXmlElement anchor = node;
        for (var i = 1; i < lines.Length; i++)
        {
            anchor = anchor.InsertAfterSelf(new Break());
            anchor = anchor.InsertAfterSelf(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        if (suffix.Length > 0)
            anchor.InsertAfterSelf(new Text(suffix) { Space = SpaceProcessingModeValues.Preserve });
    }

    /// Text nodes of this paragraph only: a text box inside a run carries its own paragraphs,
    /// which are visited separately.
    private static List<Text> OwnTexts(Paragraph paragraph) =>
        paragraph.Descendants<Text>()
            .Where(t => ReferenceEquals(t.Ancestors<Paragraph>().FirstOrDefault(), paragraph))
            .ToList();
}
