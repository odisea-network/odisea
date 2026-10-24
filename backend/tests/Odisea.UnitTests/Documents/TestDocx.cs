using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Odisea.UnitTests.Documents;

/// Builds small .docx files in memory the way Word would lay them out, and reads them back.
internal static class TestDocx
{
    public static byte[] Build(Action<Body> body, Action<Header>? header = null, Action<Footer>? footer = null)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var content = new Body();
            main.Document = new Document(content);
            body(content);

            var section = new SectionProperties();
            if (header is not null)
            {
                var part = main.AddNewPart<HeaderPart>();
                part.Header = new Header();
                header(part.Header);
                section.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(part) });
            }
            if (footer is not null)
            {
                var part = main.AddNewPart<FooterPart>();
                part.Footer = new Footer();
                footer(part.Footer);
                section.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(part) });
            }
            content.Append(section);
        }
        return stream.ToArray();
    }

    /// One paragraph whose text is split across runs exactly as given.
    public static Paragraph Paragraph(params string[] runs) =>
        new(runs.Select(text => (OpenXmlElement)Run(text)));

    public static Run Run(string text, bool bold = false)
    {
        var run = new Run();
        if (bold)
            run.Append(new RunProperties(new Bold()));
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    public static TableRow Row(params string[] cells) =>
        new(cells.Select(text => (OpenXmlElement)new TableCell(Paragraph(text))));

    public static string BodyText(byte[] docx)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart!.Document!.Body!.InnerText;
    }

    public static string HeaderText(byte[] docx)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var document = WordprocessingDocument.Open(stream, false);
        return string.Concat(document.MainDocumentPart!.HeaderParts.Select(h => h.Header!.InnerText));
    }

    public static string FooterText(byte[] docx)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var document = WordprocessingDocument.Open(stream, false);
        return string.Concat(document.MainDocumentPart!.FooterParts.Select(f => f.Footer!.InnerText));
    }

    public static T With<T>(byte[] docx, Func<Body, T> read)
    {
        using var stream = new MemoryStream(docx, writable: false);
        using var document = WordprocessingDocument.Open(stream, false);
        return read(document.MainDocumentPart!.Document!.Body!);
    }
}
