using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Users;

namespace ShiftLedger.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Get() => Ok(await mediator.Send(new GetUsersQuery()));

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateUserCommand command) => Ok(await mediator.Send(command));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserCommand command)
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
        await mediator.Send(new DeleteUserCommand(id));
        return NoContent();
    }
}
