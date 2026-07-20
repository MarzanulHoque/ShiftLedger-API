using ShiftLedger.Application.Bills;

namespace ShiftLedger.Application.Common.Interfaces;

// Renders a single bill as a printable customer-facing invoice. Implemented in Infrastructure
// (QuestPDF) so the Application layer stays free of rendering libraries.
public interface IInvoiceExporter
{
    byte[] ToPdf(InvoiceDto invoice);
}
