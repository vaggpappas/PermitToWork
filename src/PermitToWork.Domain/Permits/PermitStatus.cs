namespace PermitToWork.Domain.Permits;

/// <summary>
/// Where a permit is in its life.
/// <para>
/// The only way to move between these is a transition method on <see cref="Permit"/>.
/// There is no setter, so no caller can put a permit into a state it could not have
/// reached legitimately — which is the whole point of modelling it as a machine rather
/// than as a column somebody updates.
/// </para>
/// <code>
///   Draft ──submit──► Pending ──all approve, or one decisive──► Active ⇄ Suspended
///                        │                                        │         │
///                        └──any approver refuses──► Rejected      └──close──┴──► Closed
///
///   Draft / Pending / Active / Suspended ──withdrawn──► Cancelled
///   Pending / Active / Suspended ──window passes──────► Expired
/// </code>
/// </summary>
public enum PermitStatus
{
    /// <summary>Being written. The only state in which the content can change.</summary>
    Draft = 1,

    /// <summary>Submitted, waiting on its approvers.</summary>
    Pending = 2,

    /// <summary>Fully approved. Work may proceed.</summary>
    Active = 3,

    /// <summary>The creator has declared the work finished.</summary>
    Closed = 4,

    /// <summary>
    /// An approver refused. Terminal — a rejected permit is rewritten from scratch, not
    /// revived, so that what was refused stays on the record as refused.
    /// </summary>
    Rejected = 5,

    /// <summary>Work stopped, without the authorisation being torn up. Weather, an alarm, a shift change.</summary>
    Suspended = 6,

    /// <summary>Called off. Distinct from Closed, which means the job was done.</summary>
    Cancelled = 7,

    /// <summary>
    /// Its validity window passed. Terminal, and the reason expiry exists: without it an
    /// abandoned permit reads as live work forever, which is the first thing a safety audit
    /// looks for.
    /// </summary>
    Expired = 8
}

/// <summary>One approver's answer on one permit.</summary>
public enum ApprovalDecision
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>What happened to a permit. Recorded on every transition and every change of crew.</summary>
public enum PermitEventKind
{
    Created = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Activated = 5,
    Closed = 6,
    WorkerAdded = 7,
    WorkerRemoved = 8,
    EquipmentAdded = 9,
    EquipmentRemoved = 10,
    DocumentAttached = 11,
    DocumentRemoved = 12,
    ContentChanged = 13,
    Suspended = 14,
    Resumed = 15,
    Cancelled = 16,
    Expired = 17
}
