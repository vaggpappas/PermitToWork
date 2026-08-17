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
public sealed class PermitsController(IPermitService permits, IPermitExpiryService expiry) : ControllerBase
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

    #region Documents

    /// <summary>
    /// What may be attached, so the client can say so before somebody picks the wrong file.
    /// <para>
    /// The limits are served rather than duplicated in the browser: the hint above the file
    /// picker and the rule the server enforces are then the same sentence.
    /// </para>
    /// </summary>
    [HttpGet("/api/permits/document-policy")]
    [ProducesResponseType<DocumentPolicyDto>(StatusCodes.Status200OK)]
    public ActionResult<DocumentPolicyDto> DocumentPolicyInfo() => Ok(DocumentPolicy.Describe());

    /// <summary>Attaches a method statement, risk assessment, drawing or photograph.</summary>
    /// <response code="409">Too large, empty, or not a permitted kind of file.</response>
    [HttpPost("{id:guid}/documents")]
    [Authorize(Roles = AuthorRoles)]
    // A little over the policy limit, so an oversized file is refused by the policy with a
    // readable message rather than by Kestrel with a bare 413.
    [RequestSizeLimit(DocumentPolicy.MaxBytes + 512 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AttachDocument(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No file",
                Detail = "Choose a file to attach.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var content = file.OpenReadStream();

        var documentId = await permits.AttachDocumentAsync(
            id,
            new DocumentUpload(file.FileName, file.ContentType, file.Length, content),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id }, new { id = documentId });
    }

    /// <summary>Downloads an attachment.</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await permits.GetDocumentAsync(id, documentId, cancellationToken);

        // The stored content type, never one the caller asked for, and the original file
        // name only as a download name — it never touches a path.
        return File(document.Content, document.ContentType, document.FileName);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await permits.RemoveDocumentAsync(id, documentId, cancellationToken);
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

    /// <summary>
    /// Runs the expiry sweep now, rather than waiting for the timer.
    /// <para>
    /// The same work the background worker does every fifteen minutes. It exists so the
    /// behaviour can be demonstrated and tested without waiting, and so an administrator
    /// can force it after a long outage.
    /// </para>
    /// </summary>
    [HttpPost("expire-elapsed")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExpireElapsed(CancellationToken cancellationToken)
    {
        var expired = await expiry.ExpireElapsedAsync(cancellationToken);
        return Ok(new { expired });
    }

    #endregion
}
