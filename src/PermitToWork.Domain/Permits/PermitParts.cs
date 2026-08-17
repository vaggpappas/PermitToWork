using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Permits;

// Everything that hangs off a Permit. All of these have internal constructors: they exist
// only inside the aggregate, and the compiler is what enforces that rather than a comment.

/// <summary>What a permit type demands. Used to build a permit; not itself persisted.</summary>
public sealed record CertificationRequirement(Guid CertificationTypeId, string Name);

/// <summary>
/// A certification this permit requires, captured when the permit was raised.
/// <para>
/// A copy of the rule as it stood, not a pointer to the rule as it stands. If somebody adds
/// a requirement to Hot Work next spring, permits issued last week do not retroactively
/// become non-compliant — and an investigator reading an old permit sees the policy that
/// actually applied to it. The name travels with the id so the record stays readable even
/// if the certification type is later renamed.
/// </para>
/// </summary>
public class PermitCertificationRequirement : Entity
{
    private PermitCertificationRequirement() { }

    internal PermitCertificationRequirement(Guid permitId, Guid certificationTypeId, string name)
    {
        PermitId = Guard.Required(permitId, "Permit");
        CertificationTypeId = Guard.Required(certificationTypeId, "Certification type");
        Name = Guard.Required(name, "Certification name", 100);
    }

    public Guid PermitId { get; private set; }
    public Guid CertificationTypeId { get; private set; }
    public string Name { get; private set; } = null!;
}

/// <summary>
/// A seat on the facility's approval panel, copied onto this permit when it was submitted,
/// plus the answer given.
/// <para>
/// A snapshot for the same reason the certification requirements are: it records who was on
/// the panel at the moment this permit went out. Adding somebody to the facility panel next
/// month must not silently make last week's permits incompletely approved.
/// </para>
/// </summary>
public class PermitApproval : Entity
{
    private PermitApproval() { }

    internal PermitApproval(Guid permitId, Guid approverEmployeeId, bool isDecisive)
    {
        PermitId = Guard.Required(permitId, "Permit");
        ApproverEmployeeId = Guard.Required(approverEmployeeId, "Approver");
        IsDecisive = isDecisive;
        Decision = ApprovalDecision.Pending;
    }

    public Guid PermitId { get; private set; }
    public Guid ApproverEmployeeId { get; private set; }

    /// <summary>Whether this person's approval alone is enough to activate the permit.</summary>
    public bool IsDecisive { get; private set; }

    public ApprovalDecision Decision { get; private set; }
    public DateTimeOffset? DecidedOn { get; private set; }
    public string? Comment { get; private set; }

    public bool IsOutstanding => Decision is ApprovalDecision.Pending;

    internal void Approve(string? comment)
    {
        RequireUndecided();

        Decision = ApprovalDecision.Approved;
        Comment = Guard.Optional(comment, "Comment", 500);
        DecidedOn = DateTimeOffset.UtcNow;
    }

    internal void Reject(string reason)
    {
        RequireUndecided();

        Decision = ApprovalDecision.Rejected;
        Comment = Guard.Required(reason, "Reason", 500);
        DecidedOn = DateTimeOffset.UtcNow;
    }

    private void RequireUndecided()
    {
        if (!IsOutstanding)
        {
            throw new DomainException($"You have already {Decision.ToString().ToLowerInvariant()} this permit.");
        }
    }
}

/// <summary>Somebody the facility panel puts on a permit, and whether they can sign alone.</summary>
public sealed record ApproverAssignment(Guid EmployeeId, bool IsDecisive);

/// <summary>One person on the crew.</summary>
public class PermitWorker : Entity
{
    private PermitWorker() { }

    internal PermitWorker(Guid permitId, Guid employeeId, string? note)
    {
        PermitId = Guard.Required(permitId, "Permit");
        EmployeeId = Guard.Required(employeeId, "Employee");
        Note = Guard.Optional(note, "Note", 200);
        AddedOn = DateTimeOffset.UtcNow;
    }

    public Guid PermitId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }
}

/// <summary>A tool, machine or vehicle the work needs.</summary>
public class PermitEquipment : Entity
{
    private PermitEquipment() { }

    internal PermitEquipment(Guid permitId, string description, string? identifier, int quantity)
    {
        if (quantity < 1)
        {
            throw new DomainException("Equipment quantity must be at least one.");
        }

        PermitId = Guard.Required(permitId, "Permit");
        Description = Guard.Required(description, "Equipment description", 200);
        Identifier = Guard.Optional(identifier, "Identifier", 60);
        Quantity = quantity;
    }

    public Guid PermitId { get; private set; }
    public string Description { get; private set; } = null!;

    /// <summary>Asset or serial number, where the item has one.</summary>
    public string? Identifier { get; private set; }

    public int Quantity { get; private set; }
}

/// <summary>A file attached to the permit — a method statement, a risk assessment, a drawing.</summary>
public class PermitDocument : Entity
{
    private PermitDocument() { }

    internal PermitDocument(
        Guid permitId,
        string fileName,
        string contentType,
        long sizeInBytes,
        string storageKey,
        Guid uploadedById)
    {
        if (sizeInBytes <= 0)
        {
            throw new DomainException("An attached document cannot be empty.");
        }

        PermitId = Guard.Required(permitId, "Permit");
        FileName = Guard.Required(fileName, "File name", 255);
        ContentType = Guard.Required(contentType, "Content type", 100);
        SizeInBytes = sizeInBytes;
        StorageKey = Guard.Required(storageKey, "Storage key", 260);
        UploadedById = Guard.Required(uploadedById, "Uploader");
        UploadedOn = DateTimeOffset.UtcNow;
    }

    public Guid PermitId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeInBytes { get; private set; }

    /// <summary>
    /// Where the bytes live. Opaque to the domain on purpose — a path today, an object key
    /// tomorrow, and nothing here needs to change.
    /// </summary>
    public string StorageKey { get; private set; } = null!;

    public Guid UploadedById { get; private set; }
    public DateTimeOffset UploadedOn { get; private set; }
}

/// <summary>
/// One line of the permit's history. Append-only: there is no method to change or delete
/// one, because the value of an audit trail is exactly that it cannot be tidied up.
/// </summary>
public class PermitEvent : Entity
{
    private PermitEvent() { }

    internal PermitEvent(Guid permitId, PermitEventKind kind, Guid? actorEmployeeId, string? detail)
    {
        PermitId = Guard.Required(permitId, "Permit");
        Kind = kind;
        ActorEmployeeId = actorEmployeeId;
        Detail = Guard.Optional(detail, "Detail", 500);
        OccurredOn = DateTimeOffset.UtcNow;
    }

    public Guid PermitId { get; private set; }
    public PermitEventKind Kind { get; private set; }

    /// <summary>Null when the system acted rather than a person — expiry, for instance.</summary>
    public Guid? ActorEmployeeId { get; private set; }

    public string? Detail { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
}
