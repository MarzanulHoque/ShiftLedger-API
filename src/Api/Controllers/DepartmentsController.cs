using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Departments;

namespace ShiftLedger.Api.Controllers;

[ApiController]
[Route("api/v1/departments")]
[Authorize(Roles = "Admin")]
public class DepartmentsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> Get() => Ok(await mediator.Send(new GetDepartmentsQuery()));

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateDepartmentCommand command) => Ok(await mediator.Send(command));

    [HttpPut("{id:guid}")]
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
    public async Task<IActionResult> Delete(Guid id)
    {
        await mediator.Send(new DeleteDepartmentCommand(id));
        return NoContent();
    }
}
