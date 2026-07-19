using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Reports;

namespace ShiftLedger.Infrastructure.Reports;

// Renders any ReportData table as a PDF (QuestPDF) or Excel workbook (ClosedXML).
// One generic renderer keeps all five report types consistent and adding a sixth free.
public class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        // Free Community license (docs/00 locked stack).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ToPdf(ReportData report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().Text(report.Title).FontSize(16).SemiBold();

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var i = 0; i < report.Columns.Count; i++)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var column in report.Columns)
                        {
                            header.Cell().BorderBottom(1).PaddingBottom(4).Text(column).SemiBold();
                        }
                    });

                    foreach (var row in report.Rows)
                    {
                        foreach (var cell in row)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .PaddingVertical(3).Text(Format(cell));
                        }
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ToExcel(ReportData report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Truncate(report.Title, 31)); // Excel's sheet-name limit

        for (var c = 0; c < report.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = report.Columns[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        for (var r = 0; r < report.Rows.Count; r++)
        {
            for (var c = 0; c < report.Rows[r].Count; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = ToCellValue(report.Rows[r][c]);
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Typed values keep their native type in Excel (numbers sum, dates sort); everything else is text.
    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => Blank.Value,
        decimal d => d,
        int i => i,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        DateTime dt => dt,
        bool b => b,
        _ => value.ToString(),
    };

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.00"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        _ => value.ToString() ?? string.Empty,
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
