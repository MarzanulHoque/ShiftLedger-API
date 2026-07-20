using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Infrastructure.Reports;

// Renders a single Bill as a printable customer-facing invoice (QuestPDF, same Community license
// as ReportExporter). Kept separate from ReportExporter — an invoice is a fixed one-record
// document layout, not a generic tabular report.
public class InvoiceExporter : IInvoiceExporter
{
    private const string Brand = "#BF5A2C";
    private const string Ink = "#1C2530";
    private const string Muted = "#5B6B76";
    private const string Good = "#3E7D4C";

    static InvoiceExporter()
    {
        // Free Community license (docs/00 locked stack) — idempotent alongside ReportExporter's
        // own static ctor in case this type is used before that one ever runs.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ToPdf(InvoiceDto invoice)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("ShiftLedger").FontSize(20).Bold().FontColor(Brand);
                        col.Item().Text("Bike Service Shop").FontSize(9).FontColor(Muted);
                    });
                    row.ConstantItem(180).Column(col =>
                    {
                        col.Item().AlignRight().Text("INVOICE").FontSize(16).Bold();
                        col.Item().AlignRight().Text($"#{invoice.BillId.ToString()[..8].ToUpperInvariant()}").FontColor(Muted);
                        col.Item().AlignRight().Text(
                            invoice.IsPaid && invoice.PaidAtUtc is { } paidAt
                                ? $"Paid {paidAt:yyyy-MM-dd}"
                                : "Unpaid").FontColor(invoice.IsPaid ? Good : Muted);
                    });
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    col.Spacing(16);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Service").FontSize(9).Bold().FontColor(Muted);
                            c.Item().Text(invoice.JobTitle).FontSize(13).Bold();
                            c.Item().Text(invoice.BikeModel).FontColor(Muted);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Details").FontSize(9).Bold().FontColor(Muted);
                            c.Item().Text($"Mechanic: {invoice.MechanicName ?? "Unassigned"}");
                            c.Item().Text($"Received: {invoice.ReceivedDate:yyyy-MM-dd}");
                            if (invoice.DueDate is { } due)
                            {
                                c.Item().Text($"Due: {due:yyyy-MM-dd}");
                            }
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            foreach (var title in new[] { "Description", "Type", "Qty", "Unit price", "Line total" })
                            {
                                header.Cell().BorderBottom(1).BorderColor(Ink).PaddingBottom(4)
                                    .Text(title).Bold().FontSize(9);
                            }
                        });

                        foreach (var line in invoice.Lines)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(line.Description);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(line.Type.ToString());
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(line.Quantity.ToString("0.##"));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(Money(line.UnitPrice, invoice.CurrencyCode));
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(Money(line.LineTotal, invoice.CurrencyCode));
                        }
                    });

                    col.Item().AlignRight().Row(row =>
                    {
                        row.ConstantItem(160).Text("Total").Bold().FontSize(12);
                        row.ConstantItem(120).AlignRight().Text(Money(invoice.Total, invoice.CurrencyCode)).Bold().FontSize(12);
                    });
                });

                page.Footer().AlignCenter().Text("Thank you for your business.").FontColor(Muted).FontSize(9);
            });
        });

        return document.GeneratePdf();
    }

    private static string Money(decimal amount, string currencyCode) => $"{amount:0.00} {currencyCode}";
}
