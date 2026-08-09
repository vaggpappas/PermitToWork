using Microsoft.AspNetCore.Mvc;

namespace PermitToWork.Api.Controllers;

/// <summary>Liveness probe. Also the smoke test that the host, routing and Swagger all work.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns 200 when the API is up.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        utc = DateTimeOffset.UtcNow
    });
}
