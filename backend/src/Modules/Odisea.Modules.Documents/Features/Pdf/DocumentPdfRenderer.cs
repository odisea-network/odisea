using System.Reflection;
using Odisea.Modules.Documents.Domain;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Odisea.Modules.Documents.Features.Pdf;

public class DocumentPdfRenderer
{
    private const string Font = "DejaVu Sans";

    static DocumentPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        foreach (var resource in new[] { "DejaVuSans.ttf", "DejaVuSans-Bold.ttf" })
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"Odisea.Modules.Documents.Fonts.{resource}")
                ?? throw new InvalidOperationException($"Embedded font {resource} missing.");
            FontManager.RegisterFontWithCustomName(Font, stream);
        }
    }

    public byte[] Render(FinancialDocument d)
    {
        var title = d.Kind switch
        {
            DocumentKind.Invoice => "ФАКТУРА",
            DocumentKind.CreditNote => "КРЕДИТНО ИЗВЕСТИЕ",
            DocumentKind.DebitNote => "ДЕБИТНО ИЗВЕСТИЕ",
            _ => "ПРОФОРМА ФАКТУРА",
        };

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily(Font).FontSize(9));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(title).FontSize(18).Bold();
                        c.Item().Text($"№ {d.Number} / {d.IssueDate:dd.MM.yyyy} г.").FontSize(11);
                        c.Item().Text("Оригинал").Italic().FontSize(8);
                    });
                    row.ConstantItem(220).Column(c =>
                    {
                        if (d.Status == DocumentStatus.Annulled)
                            c.Item().AlignRight().Text("АНУЛИРАН").FontSize(14).Bold();
                        c.Item().AlignRight().Text($"Дата на данъчно събитие: {d.TaxEventDate:dd.MM.yyyy} г.");
                        if (d.RelatedDocumentNumber is not null)
                            c.Item().AlignRight()
                                .Text($"Към фактура № {d.RelatedDocumentNumber} / {d.RelatedDocumentDate:dd.MM.yyyy} г.");
                        if (d.BookingRef is not null)
                            c.Item().AlignRight().Text($"Резервация: {d.BookingRef}");
                    });
                });
                header.Item().PaddingTop(8).LineHorizontal(1);
            });

            page.Content().PaddingTop(10).Column(content =>
            {
                content.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => Party(e, "ДОСТАВЧИК",
                        d.SupplierName, d.SupplierEik, d.SupplierVatNumber,
                        d.SupplierAddress, d.SupplierCity, d.SupplierMol));
                    row.ConstantItem(20);
                    row.RelativeItem().Element(e => Party(e, "ПОЛУЧАТЕЛ",
                        d.RecipientName, d.RecipientEik, d.RecipientVatNumber,
                        d.RecipientAddress, d.RecipientCity, d.RecipientMol));
                });

                content.Item().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(24);
                        cols.RelativeColumn();
                        cols.ConstantColumn(90);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("№");
                        h.Cell().Element(HeaderCell).Text("Наименование на стоката/услугата");
                        h.Cell().Element(HeaderCell).AlignRight().Text($"Стойност ({d.Currency})");
                    });
                    table.Cell().Element(BodyCell).Text("1");
                    table.Cell().Element(BodyCell).Text(d.Description);
                    table.Cell().Element(BodyCell).AlignRight().Text($"{d.Total:N2}");
                });

                content.Item().PaddingTop(10).AlignRight().Column(totals =>
                {
                    if (d.VatTreatment is VatTreatment.Standard20 or VatTreatment.Accommodation9)
                    {
                        totals.Item().AlignRight().Text($"Данъчна основа: {d.TaxBase:N2} {d.Currency}");
                        totals.Item().AlignRight().Text($"ДДС {d.VatRatePercent:N0}%: {d.VatAmount:N2} {d.Currency}");
                    }
                    totals.Item().AlignRight()
                        .Text($"Сума за плащане: {d.Total:N2} {d.Currency}").FontSize(12).Bold();
                });

                if (d.LegalNote is not null)
                    content.Item().PaddingTop(12).Text(d.LegalNote).Italic().FontSize(8);

                if (d.SupplierIban is not null)
                    content.Item().PaddingTop(12).Column(pay =>
                    {
                        pay.Item().Text("Плащане по банков път:").Bold();
                        pay.Item().Text($"IBAN: {d.SupplierIban}" +
                            (d.SupplierBankName is null ? "" : $"  ·  {d.SupplierBankName}"));
                        if (d.DueDate is { } due)
                            pay.Item().Text($"Срок за плащане: {due:dd.MM.yyyy} г.");
                    });

                if (d.Status == DocumentStatus.Annulled && d.AnnulmentReason is not null)
                    content.Item().PaddingTop(12)
                        .Text($"Анулиран (чл. 116 ЗДДС). Основание: {d.AnnulmentReason}").Bold();
            });

            page.Footer().Column(footer =>
            {
                footer.Item().LineHorizontal(0.5f);
                footer.Item().PaddingTop(4).Text(
                    "Документът е съставен електронно и е валиден без подпис и печат (чл. 114 ЗДДС, чл. 6 ЗСч).")
                    .FontSize(7);
                footer.Item().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(7));
                    text.Span($"Издадено чрез Odisea · {DateTime.UtcNow:dd.MM.yyyy} · стр. ");
                    text.CurrentPageNumber();
                    text.Span(" от ");
                    text.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Party(IContainer container, string role,
        string name, string eik, string? vat, string address, string city, string? mol)
    {
        container.Column(c =>
        {
            c.Item().Text(role).FontSize(8).Bold();
            c.Item().Text(name).FontSize(10).Bold();
            c.Item().Text($"ЕИК: {eik}");
            if (vat is not null)
                c.Item().Text($"ИН по ЗДДС: {vat}");
            c.Item().Text($"Адрес: {city}, {address}");
            if (mol is not null)
                c.Item().Text($"МОЛ: {mol}");
        });
    }

    private static IContainer HeaderCell(IContainer c) =>
        c.BorderBottom(1).PaddingVertical(4).DefaultTextStyle(s => s.Bold());

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).PaddingVertical(6);
}
