using FluentAssertions;
using ShiftLedger.Application.Reports;
using ShiftLedger.Infrastructure.Reports;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Reports;

// Exporter smoke tests — no database needed (not in the Database collection).
public class ReportExporterTests
{
    private static readonly ReportData Sample = new(
        "Revenue",
        ["Date", "Bills paid", "Revenue"],
        [
            [new DateOnly(2026, 7, 18), 2, 1300.50m],
            [new DateOnly(2026, 7, 19), 1, 777m],
        ]);

    [Fact]
    public void ToPdf_ProducesPdfBytes()
    {
        var bytes = new ReportExporter().ToPdf(Sample);
        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void ToExcel_ProducesWorkbookBytes()
    {
        var bytes = new ReportExporter().ToExcel(Sample);
        bytes.Should().NotBeEmpty();
        // XLSX is a ZIP container: "PK" magic bytes.
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }
}
