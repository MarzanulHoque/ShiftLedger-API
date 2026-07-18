using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.EmployeeProfiles;
using ShiftLedger.Application.PayRates;

namespace ShiftLedger.Api.Controllers;

// A user's pay profile and effective-dated rate history (admin-provisioned).
[ApiController]
[Route("api/v1/users/{userId:guid}")]
[Authorize(Roles = "Admin")]
public class EmployeeProfilesController(ISender mediator) : ControllerBase
{
    [HttpGet("employee-profile")]
    public async Task<ActionResult<EmployeeProfileDto>> GetProfile(Guid userId) =>
        Ok(await mediator.Send(new GetEmployeeProfileQuery(userId)));

    [HttpPut("employee-profile")]
    public async Task<ActionResult<EmployeeProfileDto>> UpsertProfile(Guid userId, UpsertEmployeeProfileRequest body) =>
        Ok(await mediator.Send(new UpsertEmployeeProfileCommand(userId, body.RateType, body.PayCycle)));

    [HttpGet("pay-rates")]
    public async Task<ActionResult<IReadOnlyList<PayRateDto>>> GetPayRates(Guid userId) =>
        Ok(await mediator.Send(new GetPayRatesQuery(userId)));

    [HttpPost("pay-rates")]
    public async Task<ActionResult<Guid>> AddPayRate(Guid userId, AddPayRateRequest body) =>
        Ok(await mediator.Send(new AddPayRateCommand(userId, body.Amount, body.EffectiveFrom)));
}
