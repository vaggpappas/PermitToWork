using System.ComponentModel.DataAnnotations;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Permits;

namespace PermitToWork.Application.Permits;

#region Read models

public sealed record PermitSummaryDto(
    Guid Id,
    string PermitNumber,
    string PermitTypeName,
    string PermitTypeCode,
    string CategoryName,
    string? Project,
    string WorkDescription,
    string FacilityName,
    string LocationName,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    PermitStatus Status,
    string CreatedByName,
    string ReceiverName,
    int WorkerCount,
    int OutstandingApprovals)
{
    /// <summary>Whether the window has closed on a permit nobody has finished off.</summary>
    public bool IsOverdue =>
        Status is PermitStatus.Pending or PermitStatus.Active or PermitStatus.Suspended
        && ValidTo < DateTimeOffset.UtcNow;
}

public sealed record PermitApprovalDto(
    Guid Id,
    Guid ApproverEmployeeId,
    string ApproverName,
    bool IsDecisive,
    ApprovalDecision Decision,
    DateTimeOffset? DecidedOn,
    string? Comment);

public sealed record PermitWorkerDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    string TradeName,
    string CompanyName,
    string? Note);

public sealed record PermitEquipmentDto(Guid Id, string Description, string? Identifier, int Quantity);

public sealed record PermitDocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeInBytes,
    string UploadedByName,
    DateTimeOffset UploadedOn);

public sealed record PermitEventDto(
    Guid Id,
    PermitEventKind Kind,
    string? ActorName,
    string? Detail,
    DateTimeOffset OccurredOn);

public sealed record PermitDetailDto(
    Guid Id,
    string PermitNumber,
    Guid PermitTypeId,
    string PermitTypeName,
    Guid CategoryId,
    string CategoryName,
    string? Project,
    string WorkDescription,
    string? Notes,
    Guid FacilityId,
    string FacilityName,
    Guid LocationId,
    string LocationName,
    string BuildingName,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    PermitStatus Status,
    string? StatusReason,
    Guid CreatedById,
    string CreatedByName,
    Guid ReceiverId,
    string ReceiverName,
    Guid? IssuedById,
    string? IssuedByName,
    IReadOnlyList<string> RequiredCertifications,
    IReadOnlyList<PermitApprovalDto> Approvals,
    IReadOnlyList<PermitWorkerDto> Workers,
    IReadOnlyList<PermitEquipmentDto> Equipment,
    IReadOnlyList<PermitDocumentDto> Documents,
    IReadOnlyList<PermitEventDto> History)
{
    public int OutstandingApprovals => Approvals.Count(a => a.Decision is ApprovalDecision.Pending);

    /// <summary>
    /// What the client should offer. Computed here rather than reimplemented in the
    /// browser, so the buttons on screen and the rules on the server cannot drift apart.
    /// The server still refuses anything illegal — this only decides what is worth showing.
    /// </summary>
    public bool CanEdit => Status is PermitStatus.Draft;

    public bool CanSubmit => Status is PermitStatus.Draft && Workers.Count > 0;

    public bool CanChangeResources => Status is PermitStatus.Draft or PermitStatus.Active;

    public bool CanClose => Status is PermitStatus.Active or PermitStatus.Suspended;

    public bool CanSuspend => Status is PermitStatus.Active;

    public bool CanResume => Status is PermitStatus.Suspended;

    public bool CanCancel =>
        Status is not (PermitStatus.Closed or PermitStatus.Rejected
            or PermitStatus.Cancelled or PermitStatus.Expired);
}

/// <summary>A permit type with what it demands, for the "new permit" form.</summary>
public sealed record PermitTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<string> RequiredCertifications);

#endregion

#region Requests

public sealed record PermitSearchRequest : PageRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public PermitStatus? Status { get; init; }

    public Guid? PermitTypeId { get; init; }

    public Guid? FacilityId { get; init; }

    /// <summary>Only permits waiting on the signed-in user's signature.</summary>
    public bool AwaitingMyApproval { get; init; }

    /// <summary>Only permits the signed-in user raised.</summary>
    public bool RaisedByMe { get; init; }

    /// <summary>
    /// Only permits the signed-in user is actually on — as a member of the crew, or as the
    /// Receiver accountable for the work.
    /// </summary>
    /// <remarks>
    /// Note what this request deliberately cannot express: <em>somebody else's</em> permits.
    /// There is no employee id here to set. Reading another person's assignments goes
    /// through its own endpoint with its own role check, so an open endpoint can never be
    /// turned into one by adding a query parameter to it.
    /// </remarks>
    public bool AssignedToMe { get; init; }

    public PermitOrder Order { get; init; } = PermitOrder.Newest;
}

/// <summary>
/// How a page of permits is sorted.
/// <para>
/// An enum rather than a sort-column string, so "order by whatever the client sent" cannot
/// happen and every ordering the application supports is visible in one place.
/// </para>
/// </summary>
public enum PermitOrder
{
    /// <summary>Most recent validity first. A permit book is read from the top.</summary>
    Newest = 0,

    /// <summary>
    /// What a person needs to do: permits live right now, then the soonest upcoming.
    /// </summary>
    Schedule = 1
}

public sealed record CreatePermitRequest
{
    [Required]
    public Guid PermitTypeId { get; init; }

    [Required]
    public Guid CategoryId { get; init; }

    [Required, StringLength(2000, MinimumLength = 10)]
    public string WorkDescription { get; init; } = string.Empty;

    [Required]
    public Guid LocationId { get; init; }

    [Required]
    public DateTimeOffset ValidFrom { get; init; }

    [Required]
    public DateTimeOffset ValidTo { get; init; }

    /// <summary>The person accountable for the work being done properly, on site.</summary>
    [Required]
    public Guid ReceiverId { get; init; }

    [StringLength(150)]
    public string? Project { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed record UpdatePermitRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required, StringLength(2000, MinimumLength = 10)]
    public string WorkDescription { get; init; } = string.Empty;

    [Required]
    public Guid LocationId { get; init; }

    [Required]
    public DateTimeOffset ValidFrom { get; init; }

    [Required]
    public DateTimeOffset ValidTo { get; init; }

    [Required]
    public Guid ReceiverId { get; init; }

    [StringLength(150)]
    public string? Project { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed record AddPermitWorkerRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    [StringLength(200)]
    public string? Note { get; init; }
}

public sealed record AddPermitEquipmentRequest
{
    [Required, StringLength(200)]
    public string Description { get; init; } = string.Empty;

    [StringLength(60)]
    public string? Identifier { get; init; }

    [Range(1, 9999)]
    public int Quantity { get; init; } = 1;
}

public sealed record ApprovePermitRequest
{
    [StringLength(500)]
    public string? Comment { get; init; }
}

/// <summary>Rejecting, suspending and cancelling all demand a reason. None of them is routine.</summary>
public sealed record PermitReasonRequest
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ClosePermitRequest
{
    [StringLength(500)]
    public string? Note { get; init; }
}

#endregion

#region Facility approval panel

public sealed record FacilityApproverDto(
    Guid Id,
    Guid FacilityId,
    Guid EmployeeId,
    string EmployeeName,
    string EmployeeNumber,
    string JobTitle,
    bool IsDecisive,
    bool IsActive);

public sealed record AddFacilityApproverRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    /// <summary>Whether this person's signature alone activates a permit.</summary>
    public bool IsDecisive { get; init; }
}

#endregion
