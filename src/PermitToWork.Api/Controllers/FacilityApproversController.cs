using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Permits;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// The standing approval panel for a facility — who signs permits there, and who can sign
/// alone.
/// <para>
/// Reading is open to any authenticated user: if your permit is going to somebody for
/// signature, you should be able to see who. Changing it is administrators only, and that
/// is not a matter of taste — this list is the entire approval control for the site, and
/// anybody who can edit it can seat themselves as a decisive approver and sign off their
/// own work.
/// </para>
/// </summary>
[ApiController]
[Route("api/facilities/{facilityId:guid}/approvers")]
[Authorize]
[Produces("application/json")]
public sealed class FacilityApproversController(IFacilityApproverService panel) : ControllerBase
{
    /// <summary>The panel for a facility, decisive approvers first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FacilityApproverDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FacilityApproverDto>>> Get(
        Guid facilityId,
        CancellationToken cancellationToken) =>
        Ok(await panel.GetPanelAsync(facilityId, cancellationToken));

    /// <summary>Seats somebody on the panel.</summary>
    /// <response code="409">They are already on it. Being seated twice is not a stronger approval.</response>
    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(
        Guid facilityId,
        AddFacilityApproverRequest request,
        CancellationToken cancellationToken)
    {
        var id = await panel.AddAsync(facilityId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { facilityId }, new { id });
    }

    /// <summary>
    /// Sets whether this approver's signature alone activates a permit.
    /// </summary>
    [HttpPut("{approverId:guid}/decisive")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetDecisive(
        Guid approverId,
        [FromBody] SetDecisiveRequest request,
        CancellationToken cancellationToken)
    {
        await panel.SetDecisiveAsync(approverId, request.IsDecisive, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Takes somebody off the panel. Permits already submitted keep their copy of this
    /// seat, so a signature given last week stays a signature.
    /// </summary>
    [HttpDelete("{approverId:guid}")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid approverId, CancellationToken cancellationToken)
    {
        await panel.RemoveAsync(approverId, cancellationToken);
        return NoContent();
    }
}

public sealed record SetDecisiveRequest(bool IsDecisive);
