using System.IO.Compression;
using DocumentFormat.OpenXml.Wordprocessing;
using Odisea.Modules.Documents.Domain;
using Odisea.Modules.Documents.Features.Contracts;
using Xunit;

namespace Odisea.UnitTests.Documents;

public class DocxTemplateRendererTests
{
    private readonly DocxTemplateRenderer _renderer = new();

    private static MergeData Data(params (string Name, string Value)[] scalars)
    {
        var data = new MergeData();
        foreach (var (name, value) in scalars)
            data.Scalars[name] = value;
        return data;
    }

    private static List<Dictionary<string, string>> Passengers(params string[] names) =>
        names.Select((name, i) => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["#"] = (i + 1).ToString(),
            ["FullName"] = name,
            ["Type"] = "Adult",
        }).ToList();

    [Fact]
    public void Fills_a_placeholder_split_across_runs_and_keeps_the_first_runs_formatting()
    {
        // Word often splits "{{Lead.FirstName}}" over three runs after spell-check or a formatting change.
        var template = TestDocx.Build(body => body.Append(new Paragraph(
            TestDocx.Run("Dear {{Le", bold: true), TestDocx.Run("ad.First"), TestDocx.Run("Name}}, welcome"))));

        var result = _renderer.Render(template, Data(("Lead.FirstName", "Ivan")));

        Assert.Empty(result.Unresolved);
        Assert.Equal("Dear Ivan, welcome", TestDocx.BodyText(result.Content));
        var runs = TestDocx.With(result.Content, body => body.Descendants<Run>()
            .Select(r => (Text: r.InnerText, Bold: r.RunProperties?.Bold is not null)).ToList());
        Assert.Equal(("Dear Ivan", true), runs[0]);
        Assert.Equal((", welcome", false), runs[^1]);
    }

    [Fact]
    public void Fills_several_placeholders_in_one_paragraph_and_in_header_and_footer()
    {
        var template = TestDocx.Build(
            body => body.Append(TestDocx.Paragraph("{{Booking.Ref}} / {{Agency.Name}} / {{Booking.Ref}}")),
            header: h => h.Append(TestDocx.Paragraph("Договор {{Booking.Ref}}")),
            footer: f => f.Append(TestDocx.Paragraph("{{Company.Name}} · {{Today}}")));

        var result = _renderer.Render(template, Data(
            ("Booking.Ref", "ODI-2027-000007"), ("Agency.Name", "Blue Horizon"), ("Company.Name", "Одисея"), ("Today", "25.10.2026")));

        Assert.Empty(result.Unresolved);
        Assert.Equal("ODI-2027-000007 / Blue Horizon / ODI-2027-000007", TestDocx.BodyText(result.Content));
        Assert.Equal("Договор ODI-2027-000007", TestDocx.HeaderText(result.Content));
        Assert.Equal("Одисея · 25.10.2026", TestDocx.FooterText(result.Content));
    }

    [Fact]
    public void Multi_line_values_become_line_breaks_inside_the_run()
    {
        var template = TestDocx.Build(body => body.Append(TestDocx.Paragraph("Address: {{Agency.Address}}.")));

        var result = _renderer.Render(template, Data(("Agency.Address", "ул. Тестова 1\n1000 София")));

        var (text, breaks) = TestDocx.With(result.Content, body =>
            (body.InnerText, body.Descendants<Break>().Count()));
        Assert.Equal("Address: ул. Тестова 11000 София.", text);
        Assert.Equal(1, breaks);
    }

    [Fact]
    public void Repeats_a_table_row_per_passenger_with_row_numbers_and_drops_the_template_row()
    {
        var template = TestDocx.Build(body => body.Append(new Table(
            TestDocx.Row("No", "Name", "Type"),
            TestDocx.Row("{{Passengers.#}}", "{{Passengers.FullName}}", "{{Passengers.Type}} · {{Booking.Ref}}"))));
        var data = Data(("Booking.Ref", "R1"));
        data.Collections["Passengers"] = Passengers("Ivan Petrov", "Maria Petrova");

        var result = _renderer.Render(template, data);

        Assert.Empty(result.Unresolved);
        var rows = TestDocx.With(result.Content, body => body.Descendants<TableRow>()
            .Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToArray()).ToList());
        Assert.Equal(3, rows.Count);
        Assert.Equal(["No", "Name", "Type"], rows[0]);
        Assert.Equal(["1", "Ivan Petrov", "Adult · R1"], rows[1]);
        Assert.Equal(["2", "Maria Petrova", "Adult · R1"], rows[2]);
    }

    [Fact]
    public void Removes_a_repeating_row_when_the_collection_is_empty()
    {
        var template = TestDocx.Build(body => body.Append(new Table(
            TestDocx.Row("Name"), TestDocx.Row("{{Passengers.FullName}}"))));
        var data = Data();
        data.Collections["Passengers"] = [];

        var rows = TestDocx.With(_renderer.Render(template, data).Content,
            body => body.Descendants<TableRow>().Count());

        Assert.Equal(1, rows);
    }

    [Fact]
    public void A_scalar_that_shares_the_collection_prefix_does_not_repeat_the_row()
    {
        var template = TestDocx.Build(body => body.Append(new Table(
            TestDocx.Row("Travellers", "{{Passengers.Count}}"))));
        var data = Data(("Passengers.Count", "2"));
        data.Collections["Passengers"] = Passengers("A B", "C D");

        var rows = TestDocx.With(_renderer.Render(template, data).Content, body => body.Descendants<TableRow>()
            .Select(r => r.InnerText).ToList());

        Assert.Equal(["Travellers2"], rows);
    }

    [Fact]
    public void Reports_unresolved_placeholders_and_leaves_them_in_place()
    {
        var template = TestDocx.Build(body => body.Append(TestDocx.Paragraph("{{SpecialTerms}} for {{Booking.Ref}} {{ spaced }}")));

        var result = _renderer.Render(template, Data(("Booking.Ref", "R1")));

        Assert.Equal(["spaced", "SpecialTerms"], result.Unresolved);
        Assert.Equal("{{SpecialTerms}} for R1 {{ spaced }}", TestDocx.BodyText(result.Content));
    }

    [Fact]
    public void Placeholder_names_are_case_insensitive()
    {
        var template = TestDocx.Build(body => body.Append(TestDocx.Paragraph("{{booking.ref}}")));

        var result = _renderer.Render(template, Data(("Booking.Ref", "R1")));

        Assert.Equal("R1", TestDocx.BodyText(result.Content));
    }

    [Fact]
    public void Inspect_lists_distinct_placeholders_in_order_of_first_appearance_across_parts()
    {
        var template = TestDocx.Build(
            body =>
            {
                body.Append(TestDocx.Paragraph("{{Booking.Ref}} {{Agency.Name}}"));
                body.Append(new Table(TestDocx.Row("{{Passengers.FullName}}", "{{Booking.Ref}}")));
            },
            header: h => h.Append(TestDocx.Paragraph("{{Company.Name}}")));

        var inspection = _renderer.Inspect(template);

        Assert.Equal(["Booking.Ref", "Agency.Name", "Passengers.FullName", "Company.Name"], inspection.Placeholders);
    }

    [Fact]
    public void Inspect_rejects_files_that_are_not_word_documents()
    {
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        using (var entry = new StreamWriter(archive.CreateEntry("readme.txt").Open()))
            entry.Write("not a document");

        Assert.Throws<ContractValidationException>(() => _renderer.Inspect([]));
        Assert.Throws<ContractValidationException>(() => _renderer.Inspect("hello"u8.ToArray()));
        Assert.Throws<ContractValidationException>(() => _renderer.Inspect(zip.ToArray()));
        var oversize = new byte[DocxTemplateRenderer.MaxTemplateBytes + 1];
        "PK\u0003\u0004"u8.CopyTo(oversize);
        var ex = Assert.Throws<ContractValidationException>(() => _renderer.Inspect(oversize));
        Assert.Contains("exceeds", ex.Message);
    }
}
