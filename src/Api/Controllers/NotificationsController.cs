using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Common.Models;
using ShiftLedger.Application.Notifications;

namespace ShiftLedger.Api.Controllers;

// Own-data only: every query/command is scoped to the authenticated caller in its handler.
[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> Get(
        [FromQuery] bool unreadOnly = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        => Ok(await mediator.Send(new GetNotificationsQuery(unreadOnly, page, pageSize)));

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await mediator.Send(new MarkNotificationReadCommand(id));
        return NoContent();
    }
}
