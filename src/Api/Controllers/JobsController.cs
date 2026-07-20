using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Common.Models;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Api.Controllers;

// Service Jobs (P4). Admin-only endpoints are marked; the shared endpoints authorize any
// authenticated user and the handler scopes a mechanic to their own jobs (Rules R2/R3).
[ApiController]
[Route("api/v1/jobs")]
public class JobsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<JobDto>>> Get(
        [FromQuery] JobStatus? status, [FromQuery] Guid? mechanicId,
        [FromQuery] int? page, [FromQuery] int? pageSize)
        => Ok(await mediator.Send(new GetJobsQuery(status, mechanicId, page, pageSize)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDto>> GetById(Guid id) => Ok(await mediator.Send(new GetJobQuery(id)));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Guid>> Create(CreateJobCommand command) => Ok(await mediator.Send(command));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, UpdateJobCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }
        await mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteJobCommand(id));
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeJobStatusRequest request)
    {
        await mediator.Send(new ChangeJobStatusCommand(id, request.NewStatus));
        return NoContent();
    }

    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assign(Guid id, AssignMechanicRequest request)
    {
        await mediator.Send(new AssignMechanicCommand(id, request.MechanicId));
        return NoContent();
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<JobCommentDto>>> GetComments(Guid id)
        => Ok(await mediator.Send(new GetJobCommentsQuery(id)));

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<Guid>> AddComment(Guid id, AddJobCommentRequest request)
        => Ok(await mediator.Send(new AddJobCommentCommand(id, request.Body)));

    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<JobHistoryEntryDto>>> GetHistory(Guid id)
        => Ok(await mediator.Send(new GetJobHistoryQuery(id)));
}

// Request bodies for the sub-resource actions (the id comes from the route).
public record ChangeJobStatusRequest(JobStatus NewStatus);
public record AssignMechanicRequest(Guid MechanicId);
public record AddJobCommentRequest(string Body);
