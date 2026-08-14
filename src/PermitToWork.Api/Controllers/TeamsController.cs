using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Common;
using PermitToWork.Application.Teams;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// Work teams and their membership. Which teams a caller can see is decided in the
/// repository, not here — a contractor sees teams that include at least one of their own
/// people, and sees only their own people inside them.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class TeamsController(ITeamService teams) : ControllerBase
{
    /// <summary>
    /// Who may change a team: administrators, and employees marked Responsible.
    /// Everyone else can read a team and see its members, and nothing more.
    /// </summary>
    private const string ManagerRoles =
        $"{ApplicationRoles.Administrator},{ApplicationRoles.Responsible}";

    /// <summary>Searches teams by name or code.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TeamSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TeamSummaryDto>>> Search(
        [FromQuery] TeamSearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await teams.SearchAsync(request, cancellationToken));

    /// <summary>One team with its full membership history.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TeamDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await teams.GetAsync(id, cancellationToken));

    /// <summary>The teams an employee is currently in.</summary>
    [HttpGet("/api/employees/{employeeId:guid}/teams")]
    [ProducesResponseType<IReadOnlyList<TeamSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamSummaryDto>>> ForEmployee(
        Guid employeeId,
        CancellationToken cancellationToken) =>
        Ok(await teams.GetForEmployeeAsync(employeeId, cancellationToken));

    /// <summary>
    /// Creates a team together with its leader. The leader is required, so a team with no
    /// members never exists.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var id = await teams.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    /// <summary>Renames a team or changes its description.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        await teams.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Adds a member.</summary>
    /// <response code="422">Already a member, the team is disbanded, or it already has a leader.</response>
    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddMember(
        Guid id,
        AddTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        await teams.AddMemberAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Ends a membership. The row stays — who was on the crew is exactly what an incident
    /// investigation asks about — it just gains a leaving date.
    /// </summary>
    [HttpDelete("{id:guid}/members/{employeeId:guid}")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid employeeId,
        [FromBody] RemoveTeamMemberRequest? request,
        CancellationToken cancellationToken)
    {
        await teams.RemoveMemberAsync(id, employeeId, request ?? new RemoveTeamMemberRequest(), cancellationToken);
        return NoContent();
    }

    /// <summary>Promotes or demotes a member.</summary>
    /// <response code="422">Promoting to leader while somebody else already leads.</response>
    [HttpPut("{id:guid}/members/{employeeId:guid}/role")]
    [Authorize(Roles = ManagerRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeMemberRole(
        Guid id,
        Guid employeeId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        await teams.ChangeMemberRoleAsync(id, employeeId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Closes the team and ends every remaining membership on the same day.</summary>
    [HttpPost("{id:guid}/disband")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Disband(Guid id, CancellationToken cancellationToken)
    {
        await teams.DisbandAsync(id, cancellationToken);
        return NoContent();
    }
}
