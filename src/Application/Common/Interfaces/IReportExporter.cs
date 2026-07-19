using ShiftLedger.Application.Reports;

namespace ShiftLedger.Application.Common.Interfaces;

// Renders a tabular report for download. Implemented in Infrastructure (QuestPDF / ClosedXML)
// so the Application layer stays free of rendering libraries.
public interface IReportExporter
{
    byte[] ToPdf(ReportData report);
    byte[] ToExcel(ReportData report);
}
