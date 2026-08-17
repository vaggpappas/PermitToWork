using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Application.Permits;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// Permits to work.
/// <para>
/// The roles below decide who may <em>attempt</em> an action. Whether the attempt is
/// legitimate — is this permit in the right state, is this person actually an approver on
/// it, did the author raise it — is decided by the aggregate, which is why there is no
/// business logic in this file. A role check cannot know that a permit is already Closed.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class PermitsController(IPermitService permits) : ControllerBase
{
    /// <summary>Who may raise and write permits.</summary>
    private const string AuthorRoles =
        $"{ApplicationRoles.Administrator},{ApplicationRoles.Supervisor},{ApplicationRoles.Responsible}";

    /// <summary>Who may halt live work.</summary>
    private const string ControlRoles =
        $"{ApplicationRoles.Administrator},{ApplicationRoles.SafetyOfficer},{ApplicationRoles.Responsible}";

    #region Reading

    /// <summary>Searches permits, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PermitSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PermitSummaryDto>>> Search(
        [FromQuery] PermitSearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await permits.SearchAsync(request, cancellationToken));

    /// <summary>One permit, with its approvals, crew, equipment and full history.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PermitDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PermitDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await permits.GetAsync(id, cancellationToken));

    /// <summary>Permit types and the certifications each demands.</summary>
    [HttpGet("/api/permit-types")]
    [ProducesResponseType<IReadOnlyList<PermitTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermitTypeDto>>> PermitTypes(CancellationToken cancellationToken) =>
        Ok(await permits.GetPermitTypesAsync(cancellationToken));

    /// <summary>Categories — what kind of work this is.</summary>
    [HttpGet("/api/categories")]
    [ProducesResponseType<IReadOnlyList<LookupDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> Categories(CancellationToken cancellationToken) =>
        Ok(await permits.GetCategoriesAsync(cancellationToken));

    #endregion

    #region Writing the permit

    /// <summary>
    /// Raises a permit as a draft. The number is generated from the permit type and the
    /// year; the facility is derived from the location.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreatePermitRequest request, CancellationToken cancellationToken)
    {
        var id = await permits.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    /// <summary>Edits a draft. Refused once submitted — what was approved must be what is performed.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, UpdatePermitRequest request, CancellationToken cancellationToken)
    {
        await permits.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Crew and equipment

    /// <summary>
    /// Adds somebody to the crew.
    /// </summary>
    /// <response code="422">
    /// They lack a certification this permit requires, are already on it, or are not
    /// actively employed.
    /// </response>
    [HttpPost("{id:guid}/workers")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddWorker(
        Guid id,
        AddPermitWorkerRequest request,
        CancellationToken cancellationToken)
    {
        await permits.AddWorkerAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/workers/{employeeId:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveWorker(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        await permits.RemoveWorkerAsync(id, employeeId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/equipment")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddEquipment(
        Guid id,
        AddPermitEquipmentRequest request,
        CancellationToken cancellationToken)
    {
        var equipmentId = await permits.AddEquipmentAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id = equipmentId });
    }

    [HttpDelete("{id:guid}/equipment/{equipmentId:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveEquipment(Guid id, Guid equipmentId, CancellationToken cancellationToken)
    {
        await permits.RemoveEquipmentAsync(id, equipmentId, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Sends the draft to the facility's approval panel, which is copied onto the permit.
    /// </summary>
    /// <response code="422">No workers on it, or the facility has no approval panel.</response>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        await permits.SubmitAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Signs the permit. Any authenticated user may call this — being an approver is a
    /// property of this permit's panel, not of a role, so the aggregate decides. It
    /// activates on the last outstanding signature, or immediately for a decisive approver.
    /// </summary>
    /// <response code="422">Not an approver on this permit, already answered, or not Pending.</response>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(
        Guid id,
        ApprovePermitRequest request,
        CancellationToken cancellationToken)
    {
        await permits.ApproveAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Refuses the permit. Terminal — a corrected one is raised fresh.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(
        Guid id,
        PermitReasonRequest request,
        CancellationToken cancellationToken)
    {
        await permits.RejectAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Stops the work without withdrawing the authorisation.</summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = ControlRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Suspend(
        Guid id,
        PermitReasonRequest request,
        CancellationToken cancellationToken)
    {
        await permits.SuspendAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Restarts suspended work, unless the window closed meanwhile.</summary>
    [HttpPost("{id:guid}/resume")]
    [Authorize(Roles = ControlRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
    {
        await permits.ResumeAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Declares the work finished. Only the person who raised the permit may — enforced in
    /// the aggregate, so no role list here could get it wrong.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Close(Guid id, ClosePermitRequest request, CancellationToken cancellationToken)
    {
        await permits.CloseAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Calls the work off. Distinct from closing, which means it was done.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = ControlRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        Guid id,
        PermitReasonRequest request,
        CancellationToken cancellationToken)
    {
        await permits.CancelAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion
}
