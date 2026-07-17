using Microsoft.AspNetCore.Mvc;

namespace ShiftLedger.Api.Controllers;

/// <summary>
/// Minimal liveness endpoint so the API exposes one verifiable route from phase P0.
/// A real <c>/health</c> check (including DB connectivity) is added in a later phase
/// (see docs/10_Deployment_and_Environments.md §6).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class PingController : ControllerBase
{
    /// <summary>Returns a simple liveness payload. GET /api/v1/ping</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "ShiftLedger.Api" });
}
