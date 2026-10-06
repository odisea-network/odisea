using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Odisea.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
}
