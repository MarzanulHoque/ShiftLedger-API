using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Dashboards;

namespace ShiftLedger.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController(ISender mediator) : ControllerBase
{
    // The owner's day view. `date` is the caller's local calendar date (T8); defaults to UTC today.
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminDashboardDto>> Admin([FromQuery] DateOnly? date)
        => Ok(await mediator.Send(new GetAdminDashboardQuery(date)));

    // A mechanic's own queue (any authenticated user — scoped to the caller in the handler).
    [HttpGet("me")]
    public async Task<ActionResult<MyDashboardDto>> Me()
        => Ok(await mediator.Send(new GetMyDashboardQuery()));
}
