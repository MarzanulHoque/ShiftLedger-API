using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.Auth.Commands.ForgotPassword;
using ShiftLedger.Application.Auth.Commands.Login;
using ShiftLedger.Application.Auth.Commands.RefreshToken;
using ShiftLedger.Application.Auth.Commands.ResetPassword;
using ShiftLedger.Application.Common.Models;

namespace ShiftLedger.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController(ISender mediator) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login(LoginCommand command) => Ok(await mediator.Send(command));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResult>> Refresh(RefreshTokenCommand command) => Ok(await mediator.Send(command));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command)
    {
        await mediator.Send(command);
        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command)
    {
        await mediator.Send(command);
        return Ok();
    }
}
