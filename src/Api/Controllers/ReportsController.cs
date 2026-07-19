using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Reports;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Api.Controllers;

// Reports (Admin-only). format=json (default, feeds the UI table) | pdf | excel (downloads).
[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController(ISender mediator, IReportExporter exporter) : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("{type}")]
    public async Task<IActionResult> Get(
        ReportType type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? mechanicId,
        [FromQuery] JobStatus? status,
        [FromQuery] string format = "json")
    {
        var report = await mediator.Send(new GetReportQuery(type, from, to, mechanicId, status));

        return format.ToLowerInvariant() switch
        {
            "json" => Ok(report),
            "pdf" => File(exporter.ToPdf(report), "application/pdf", $"{type}.pdf"),
            "excel" => File(exporter.ToExcel(report), ExcelContentType, $"{type}.xlsx"),
            _ => BadRequest($"Unknown format '{format}'. Use json, pdf, or excel."),
        };
    }
}
