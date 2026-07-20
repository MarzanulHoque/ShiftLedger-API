using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Models;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Api.Controllers;

// Billing (P5). All billing is the owner's domain — Admin-only (Rule R2: mechanics have no
// billing access). Routed under api/v1 so the job-scoped bill endpoints read naturally.
[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public class BillsController(ISender mediator, IInvoiceExporter invoiceExporter) : ControllerBase
{
    [HttpGet("bills")]
    public async Task<ActionResult<PagedResult<BillSummaryDto>>> Get(
        [FromQuery] bool? isPaid, [FromQuery] int? page, [FromQuery] int? pageSize)
        => Ok(await mediator.Send(new GetBillsQuery(isPaid, page, pageSize)));

    [HttpGet("jobs/{jobId:guid}/bill")]
    public async Task<ActionResult<BillDto>> GetForJob(Guid jobId)
        => Ok(await mediator.Send(new GetJobBillQuery(jobId)));

    [HttpPost("jobs/{jobId:guid}/bill")]
    public async Task<ActionResult<Guid>> Create(Guid jobId)
        => Ok(await mediator.Send(new CreateBillCommand(jobId)));

    [HttpPost("bills/{billId:guid}/line-items")]
    public async Task<ActionResult<Guid>> AddLine(Guid billId, LineItemRequest request)
        => Ok(await mediator.Send(new AddLineItemCommand(
            billId, request.Type, request.Description, request.Quantity, request.UnitPrice)));

    [HttpPut("bills/{billId:guid}/line-items/{lineId:guid}")]
    public async Task<IActionResult> UpdateLine(Guid billId, Guid lineId, LineItemRequest request)
    {
        await mediator.Send(new UpdateLineItemCommand(
            billId, lineId, request.Type, request.Description, request.Quantity, request.UnitPrice));
        return NoContent();
    }

    [HttpDelete("bills/{billId:guid}/line-items/{lineId:guid}")]
    public async Task<IActionResult> DeleteLine(Guid billId, Guid lineId)
    {
        await mediator.Send(new DeleteLineItemCommand(billId, lineId));
        return NoContent();
    }

    [HttpPatch("bills/{billId:guid}/pay")]
    public async Task<IActionResult> SetPaid(Guid billId, SetPaidRequest request)
    {
        await mediator.Send(new SetBillPaidCommand(billId, request.IsPaid));
        return NoContent();
    }

    [HttpGet("bills/{billId:guid}/invoice")]
    public async Task<IActionResult> GetInvoice(Guid billId)
    {
        var invoice = await mediator.Send(new GetBillInvoiceQuery(billId));
        return File(invoiceExporter.ToPdf(invoice), "application/pdf", $"invoice-{billId.ToString()[..8]}.pdf");
    }
}

// Request bodies (ids come from the route).
public record LineItemRequest(LineItemType Type, string Description, decimal Quantity, decimal UnitPrice);
public record SetPaidRequest(bool IsPaid);
