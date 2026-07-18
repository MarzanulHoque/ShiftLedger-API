using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftLedger.Application.OrgSettings.Queries.GetOrgSettings;

namespace ShiftLedger.Api.Controllers;

[ApiController]
[Route("api/v1/org-settings")]
[Authorize]
public class OrgSettingsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrgSettingsDto>> Get() => Ok(await mediator.Send(new GetOrgSettingsQuery()));
}
