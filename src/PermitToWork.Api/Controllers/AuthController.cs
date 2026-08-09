using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Accounts;

namespace PermitToWork.Api.Controllers;

/// <summary>Registration and sign-in.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuthController(IAccountService accounts) : ControllerBase
{
    /// <summary>
    /// Attaches a login to the employee record an administrator created for this email.
    /// </summary>
    /// <response code="200">Registered. The response carries a bearer token.</response>
    /// <response code="400">No employee record is awaiting registration, or the password was rejected.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(request, cancellationToken);
        return ToResponse(result);
    }

    /// <summary>Exchanges an email and password for a bearer token.</summary>
    /// <response code="200">Signed in.</response>
    /// <response code="400">The email or password was wrong.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await accounts.LoginAsync(request, cancellationToken);
        return ToResponse(result);
    }

    /// <summary>
    /// Echoes back what the API believes about the caller. Useful for confirming from the
    /// browser that a token carries the roles and company scope you expect.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me([FromServices] ICurrentUser currentUser) => Ok(new
    {
        currentUser.UserId,
        currentUser.EmployeeId,
        Scope = currentUser.Scope switch
        {
            DataScope.All => "all companies",
            DataScope.SingleCompany company => $"company {company.CompanyId}",
            _ => "nothing"
        },
        Roles = User.FindAll("role").Select(c => c.Value)
    });

    private IActionResult ToResponse(AuthenticationResult result)
    {
        if (!result.Succeeded)
        {
            // 400 rather than 401: the request was understood and rejected on its merits.
            // A 401 would invite the browser to prompt for credentials we do not use.
            return BadRequest(new ProblemDetails
            {
                Title = "Authentication failed",
                Detail = string.Join(" ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new AuthenticationResponse(result.AccessToken!, result.ExpiresAtUtc!.Value));
    }
}

/// <summary>A bearer token and the moment it stops working.</summary>
public sealed record AuthenticationResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
