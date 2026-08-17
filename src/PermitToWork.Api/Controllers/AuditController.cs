using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Auditing;
using PermitToWork.Application.Common;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// The audit trail — every insert, update and delete the application has made, with who,
/// when, and the values before and after.
/// <para>
/// Administrators only. The trail necessarily crosses every company boundary the rest of
/// the application enforces, because "who changed this" is not a question that can be
/// answered within one company's slice of the data. That is exactly why nobody else can
/// read it.
/// </para>
/// <para>
/// Read-only, and there is no endpoint to delete or amend a line. A log that can be tidied
/// up is not evidence.
/// </para>
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = ApplicationRoles.Administrator)]
[Produces("application/json")]
public sealed class AuditController(IAuditRepository audit) : ControllerBase
{
    /// <summary>Searches the trail. Newest first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> Search(
        [FromQuery] AuditSearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await audit.SearchAsync(request, cancellationToken));

    /// <summary>
    /// Everything that ever happened to one record, oldest first — the history of a single
    /// employee, permit or team.
    /// </summary>
    [HttpGet("{entityType}/{entityId}")]
    [ProducesResponseType<IReadOnlyList<AuditEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> ForRecord(
        string entityType,
        string entityId,
        CancellationToken cancellationToken) =>
        Ok(await audit.ForRecordAsync(entityType, entityId, cancellationToken));
}
