using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Domain.Permits;

/// <summary>
/// An authorisation to carry out hazardous work: what, where, when, by whom, and approved
/// by whom.
/// <para>
/// Aggregate root. Its status changes only through the transition methods below — there is
/// no setter — so a permit can never hold a state it could not legitimately have reached.
/// Every transition writes to the audit trail as part of making the change, so the history
/// is not something a caller can forget to record.
/// </para>
/// </summary>
public class Permit : Entity
{
    private readonly List<PermitCertificationRequirement> _requiredCertifications = [];
    private readonly List<PermitApproval> _approvals = [];
    private readonly List<PermitWorker> _workers = [];
    private readonly List<PermitEquipment> _equipment = [];
    private readonly List<PermitDocument> _documents = [];
    private readonly List<PermitEvent> _events = [];

    private Permit() { }

    public Permit(
        PermitNumber number,
        Guid permitTypeId,
        Guid categoryId,
        string workDescription,
        Guid facilityId,
        Guid locationId,
        DateTimeRange validity,
        Guid createdById,
        Guid receiverId,
        IEnumerable<CertificationRequirement> requiredCertifications,
        string? project = null,
        string? notes = null)
    {
        Number = number;
        PermitTypeId = Guard.Required(permitTypeId, "Permit type");
        CategoryId = Guard.Required(categoryId, "Category");
        WorkDescription = Guard.Required(workDescription, "Work description", 2000);

        // The facility is stored as well as the location, even though a location knows its
        // building and a building knows its facility. Not redundancy — it is the key the
        // approval panel is chosen by, and a permit must not change hands because somebody
        // later re-parented a room.
        FacilityId = Guard.Required(facilityId, "Facility");
        LocationId = Guard.Required(locationId, "Location");

        Validity = validity;
        CreatedById = Guard.Required(createdById, "Creator");
        ReceiverId = Guard.Required(receiverId, "Receiver");
        Project = Guard.Optional(project, "Project", 150);
        Notes = Guard.Optional(notes, "Notes", 2000);
        Status = PermitStatus.Draft;

        _requiredCertifications.AddRange(requiredCertifications.Select(requirement =>
            new PermitCertificationRequirement(Id, requirement.CertificationTypeId, requirement.Name)));

        Record(PermitEventKind.Created, createdById, $"Permit {number} raised.");
    }

    #region State

    public PermitNumber Number { get; private set; } = null!;
    public Guid PermitTypeId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string? Project { get; private set; }
    public string WorkDescription { get; private set; } = null!;
    public Guid FacilityId { get; private set; }
    public Guid LocationId { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeRange Validity { get; private set; } = null!;
    public PermitStatus Status { get; private set; }

    /// <summary>Who wrote the permit. The only person who may close it.</summary>
    public Guid CreatedById { get; private set; }

    /// <summary>Who is accountable for the work being done properly, on site.</summary>
    public Guid ReceiverId { get; private set; }

    /// <summary>Why it was rejected, if it was.</summary>
    public string? StatusReason { get; private set; }

    public IReadOnlyList<PermitCertificationRequirement> RequiredCertifications => _requiredCertifications;
    public IReadOnlyList<PermitApproval> Approvals => _approvals;
    public IReadOnlyList<PermitWorker> Workers => _workers;
    public IReadOnlyList<PermitEquipment> Equipment => _equipment;
    public IReadOnlyList<PermitDocument> Documents => _documents;
    public IReadOnlyList<PermitEvent> Events => _events;

    /// <summary>
    /// The issuer: whoever's approval completed the permit.
    /// <para>
    /// Derived, never stored. "Issuer" is not an input somebody types — it is a fact about
    /// what happened, and the approvals already record it. A separate <c>IssuerId</c>
    /// column would be a second copy of that fact, and the copy is the one that would end
    /// up disagreeing.
    /// </para>
    /// </summary>
    public Guid? IssuedById => _approvals
        .Where(a => a.Decision is ApprovalDecision.Approved)
        .OrderByDescending(a => a.DecidedOn)
        .Select(a => (Guid?)a.ApproverEmployeeId)
        .FirstOrDefault();

    public int OutstandingApprovals => _approvals.Count(a => a.IsOutstanding);

    /// <summary>Content may only be changed while the permit is still being written.</summary>
    public bool IsEditable => Status is PermitStatus.Draft;

    /// <summary>
    /// Whether the crew can still be changed. Draft and Active, but deliberately not
    /// Pending: people are approving a specific crew, and swapping it underneath them would
    /// make their signature meaningless. Once the permit is live, crews genuinely do change
    /// — shifts turn over, people go sick — so it reopens.
    /// </summary>
    public bool CanChangeResources => Status is PermitStatus.Draft or PermitStatus.Active;

    public bool IsFinished =>
        Status is PermitStatus.Closed
            or PermitStatus.Rejected
            or PermitStatus.Cancelled
            or PermitStatus.Expired;

    /// <summary>Whether this permit authorises anybody to be doing anything right now.</summary>
    public bool IsLive => Status is PermitStatus.Active;

    #endregion

    #region Content

    public void UpdateContent(
        Guid actorId,
        Guid categoryId,
        string workDescription,
        Guid facilityId,
        Guid locationId,
        DateTimeRange validity,
        Guid receiverId,
        string? project,
        string? notes)
    {
        RequireEditable();

        CategoryId = Guard.Required(categoryId, "Category");
        WorkDescription = Guard.Required(workDescription, "Work description", 2000);
        FacilityId = Guard.Required(facilityId, "Facility");
        LocationId = Guard.Required(locationId, "Location");
        Validity = validity;
        ReceiverId = Guard.Required(receiverId, "Receiver");
        Project = Guard.Optional(project, "Project", 150);
        Notes = Guard.Optional(notes, "Notes", 2000);

        Record(PermitEventKind.ContentChanged, actorId, null);
    }

    #endregion

    #region Crew, equipment and documents

    /// <summary>
    /// Adds a worker, refusing anybody who does not hold every certification this permit
    /// requires.
    /// <para>
    /// The employee is passed in rather than looked up because <see cref="Employee"/> is a
    /// separate aggregate — but the decision is made here, in the permit, where the rule
    /// belongs. No service can add a worker while skipping the check, because there is no
    /// other way in.
    /// </para>
    /// <para>
    /// Validity is checked at both ends of the permit window. A certificate that lapses
    /// halfway through a three-day job is precisely the case a single-date check misses.
    /// </para>
    /// </summary>
    public PermitWorker AddWorker(Employee employee, string? note = null)
    {
        RequireResourcesChangeable();

        if (_workers.Any(w => w.EmployeeId == employee.Id))
        {
            throw new DomainException($"{employee.Name.Full} is already on this permit.");
        }

        if (employee.Status is not EmploymentStatus.Active)
        {
            throw new DomainException($"{employee.Name.Full} is not an active employee.");
        }

        var startOfWork = DateOnly.FromDateTime(Validity.Start.UtcDateTime);
        var endOfWork = DateOnly.FromDateTime(Validity.End.UtcDateTime);

        foreach (var requirement in _requiredCertifications)
        {
            var validThroughout =
                employee.HasValidCertification(requirement.CertificationTypeId, startOfWork) &&
                employee.HasValidCertification(requirement.CertificationTypeId, endOfWork);

            if (!validThroughout)
            {
                throw new DomainException(
                    $"{employee.Name.Full} does not hold a valid {requirement.Name} certification " +
                    "covering the whole permit period.");
            }
        }

        var worker = new PermitWorker(Id, employee.Id, note);
        _workers.Add(worker);
        Record(PermitEventKind.WorkerAdded, null, employee.Name.Full);

        return worker;
    }

    public void RemoveWorker(Guid employeeId, Guid actorId)
    {
        RequireResourcesChangeable();

        var worker = _workers.SingleOrDefault(w => w.EmployeeId == employeeId)
                     ?? throw new DomainException("That person is not on this permit.");

        _workers.Remove(worker);
        Record(PermitEventKind.WorkerRemoved, actorId, null);
    }

    public PermitEquipment AddEquipment(Guid actorId, string description, string? identifier, int quantity)
    {
        RequireResourcesChangeable();

        var item = new PermitEquipment(Id, description, identifier, quantity);
        _equipment.Add(item);
        Record(PermitEventKind.EquipmentAdded, actorId, item.Description);

        return item;
    }

    public void RemoveEquipment(Guid equipmentId, Guid actorId)
    {
        RequireResourcesChangeable();

        var item = _equipment.SingleOrDefault(e => e.Id == equipmentId)
                   ?? throw new DomainException("That equipment is not on this permit.");

        _equipment.Remove(item);
        Record(PermitEventKind.EquipmentRemoved, actorId, item.Description);
    }

    public PermitDocument AttachDocument(
        Guid actorId,
        string fileName,
        string contentType,
        long sizeInBytes,
        string storageKey)
    {
        if (IsFinished)
        {
            throw new DomainException("A finished permit cannot take new documents.");
        }

        var document = new PermitDocument(Id, fileName, contentType, sizeInBytes, storageKey, actorId);
        _documents.Add(document);
        Record(PermitEventKind.DocumentAttached, actorId, fileName);

        return document;
    }

    public PermitDocument RemoveDocument(Guid documentId, Guid actorId)
    {
        var document = _documents.SingleOrDefault(d => d.Id == documentId)
                       ?? throw new DomainException("That document is not attached to this permit.");

        _documents.Remove(document);
        Record(PermitEventKind.DocumentRemoved, actorId, document.FileName);

        // Returned so the caller can delete the bytes it is now responsible for.
        return document;
    }

    #endregion

    #region Transitions

    /// <summary>
    /// Sends a finished draft to the facility's approval panel.
    /// <para>
    /// The panel is passed in and copied onto the permit, so what is recorded is who was on
    /// the panel at this moment. Adding somebody to the facility next month must not
    /// silently make this permit incompletely approved.
    /// </para>
    /// </summary>
    public void Submit(Guid actorId, IEnumerable<ApproverAssignment> panel)
    {
        RequireStatus(PermitStatus.Draft, "submitted");

        // An authorisation for nobody is not an authorisation.
        if (_workers.Count == 0)
        {
            throw new DomainException("A permit cannot be submitted with no workers on it.");
        }

        var seats = panel.DistinctBy(assignment => assignment.EmployeeId).ToList();

        if (seats.Count == 0)
        {
            throw new DomainException(
                "This facility has no approval panel. Ask an administrator to set one up.");
        }

        // Nobody approves their own paperwork while there is somebody else who could. If
        // the author sits on the panel alongside others, their own seat is skipped for this
        // permit. If they are the *only* approver at this facility, they keep it — a
        // one-person site still has to be able to raise permits, and the audit trail
        // records that the same person wrote and signed it.
        var others = seats.Where(seat => seat.EmployeeId != CreatedById).ToList();
        var selfApproving = others.Count == 0;
        var approvers = selfApproving ? seats : others;

        foreach (var approver in approvers)
        {
            _approvals.Add(new PermitApproval(Id, approver.EmployeeId, approver.IsDecisive));
        }

        Status = PermitStatus.Pending;
        Record(
            PermitEventKind.Submitted,
            actorId,
            selfApproving
                ? "Sent for approval. The author is the facility's only approver and will sign it themselves."
                : $"Sent to {approvers.Count} approver(s).");
    }

    /// <summary>
    /// An approver signs. The permit activates when the last outstanding approval comes in,
    /// or immediately if this approver can sign alone.
    /// </summary>
    public void Approve(Guid approverId, string? comment = null)
    {
        RequireStatus(PermitStatus.Pending, "approved");

        var approval = FindApproval(approverId);
        approval.Approve(comment);
        Record(PermitEventKind.Approved, approverId, comment);

        if (approval.IsDecisive)
        {
            Activate(approverId, "Approved outright by a decisive approver.");
        }
        else if (_approvals.All(a => a.Decision is ApprovalDecision.Approved))
        {
            Activate(approverId, "All approvers have signed.");
        }
    }

    /// <summary>
    /// An approver refuses. Terminal: the permit is finished and a corrected one is raised
    /// fresh, so that what was refused stays on the record as refused.
    /// </summary>
    public void Reject(Guid approverId, string reason)
    {
        RequireStatus(PermitStatus.Pending, "rejected");

        var approval = FindApproval(approverId);
        approval.Reject(reason);

        Status = PermitStatus.Rejected;
        StatusReason = approval.Comment;
        Record(PermitEventKind.Rejected, approverId, StatusReason);
    }

    /// <summary>
    /// The creator declares the work finished.
    /// <para>
    /// Only they may — the person who raised the permit is the one who knows the job is
    /// actually done, and tying it to them means closure is always attributable.
    /// </para>
    /// </summary>
    public void Close(Guid actorId, string? note = null)
    {
        // Suspended counts: work can be halted and then simply finish, and forcing a resume
        // first would put a permit briefly back into "live work" purely as paperwork.
        if (Status is not (PermitStatus.Active or PermitStatus.Suspended))
        {
            throw new DomainException($"A permit in {Status} cannot be closed. It must be Active or Suspended.");
        }

        if (actorId != CreatedById)
        {
            throw new DomainException("Only the person who raised this permit can close it.");
        }

        Status = PermitStatus.Closed;
        Record(PermitEventKind.Closed, actorId, note);
    }

    /// <summary>Stops the work without tearing up the authorisation.</summary>
    public void Suspend(Guid actorId, string reason)
    {
        RequireStatus(PermitStatus.Active, "suspended");

        Status = PermitStatus.Suspended;
        StatusReason = Guard.Required(reason, "Reason", 500);
        Record(PermitEventKind.Suspended, actorId, StatusReason);
    }

    /// <summary>Restarts suspended work — unless the window closed while it was stopped.</summary>
    public void Resume(Guid actorId, DateTimeOffset asOf)
    {
        RequireStatus(PermitStatus.Suspended, "resumed");

        if (Validity.HasPassed(asOf))
        {
            throw new DomainException(
                "This permit's validity passed while it was suspended. Raise a new one.");
        }

        Status = PermitStatus.Active;
        StatusReason = null;
        Record(PermitEventKind.Resumed, actorId, null);
    }

    /// <summary>
    /// Calls the work off. Distinct from <see cref="Close"/>, which means the job was
    /// actually done — a permit book where "finished" and "abandoned" look the same is
    /// useless to the person reading it a year later.
    /// <para>
    /// The domain does not restrict who may cancel; the API limits it to the creator and
    /// administrators. This is the one transition where that split is deliberate, because
    /// an administrator has to be able to clear somebody else's abandoned permit.
    /// </para>
    /// </summary>
    public void Cancel(Guid actorId, string reason)
    {
        if (IsFinished)
        {
            throw new DomainException($"A permit in {Status} is already finished.");
        }

        Status = PermitStatus.Cancelled;
        StatusReason = Guard.Required(reason, "Reason", 500);
        Record(PermitEventKind.Cancelled, actorId, StatusReason);
    }

    /// <summary>
    /// Marks a permit whose window has passed.
    /// <para>
    /// Driven by the clock rather than by a person, so the actor is null — the only
    /// transition nobody performs. Returns whether it actually changed anything, so a
    /// sweep can report how many it caught without inspecting each one.
    /// </para>
    /// </summary>
    public bool ExpireIfElapsed(DateTimeOffset asOf)
    {
        if (Status is not (PermitStatus.Pending or PermitStatus.Active or PermitStatus.Suspended))
        {
            return false;
        }

        if (!Validity.HasPassed(asOf))
        {
            return false;
        }

        Status = PermitStatus.Expired;
        Record(PermitEventKind.Expired, null, $"Validity ended {Validity.End:g}.");

        return true;
    }

    private void Activate(Guid actorId, string detail)
    {
        Status = PermitStatus.Active;
        StatusReason = null;
        Record(PermitEventKind.Activated, actorId, detail);
    }

    #endregion

    #region Guards

    private PermitApproval FindApproval(Guid approverId) =>
        _approvals.SingleOrDefault(a => a.ApproverEmployeeId == approverId)
        ?? throw new DomainException("You are not an approver on this permit.");

    private void RequireStatus(PermitStatus expected, string verb)
    {
        if (Status != expected)
        {
            throw new DomainException($"A permit in {Status} cannot be {verb}. It must be {expected}.");
        }
    }

    private void RequireEditable()
    {
        if (!IsEditable)
        {
            throw new DomainException(
                $"A permit in {Status} can no longer be edited — what was approved must be what is performed.");
        }
    }

    private void RequireResourcesChangeable()
    {
        if (!CanChangeResources)
        {
            throw new DomainException(
                $"The crew and equipment of a permit in {Status} cannot be changed.");
        }
    }

    private void Record(PermitEventKind kind, Guid? actorId, string? detail) =>
        _events.Add(new PermitEvent(Id, kind, actorId, detail));

    #endregion
}
