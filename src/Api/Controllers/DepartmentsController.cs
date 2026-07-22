using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Departments;

namespace ShiftLedger.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize(Roles = "SuperAdmin,DepartmentAdmin")]
public class DepartmentsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> Get() => Ok(await mediator.Send(new GetDepartmentsQuery()));

    // Rule P9 plan: department CRUD is SuperAdmin-only (RB0 already grants org-wide CRUD; this
    // narrows write access away from DepartmentAdmin, who keeps read-only access above).
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<Guid>> Create(CreateDepartmentCommand command) => Ok(await mediator.Send(command));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, UpdateDepartmentCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }
        await mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteDepartmentCommand(id));
        return NoContent();
    }
}
