using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

/// <summary>
/// A seat on a facility's standing approval panel: somebody whose sign-off a permit at
/// this site needs.
/// <para>
/// Configured per facility by an administrator, not chosen per permit. That is the point —
/// if the creator of a permit picked its own approvers, they would pick the ones likely to
/// say yes.
/// </para>
/// </summary>
public class FacilityApprover : Entity
{
    private FacilityApprover() { }

    public FacilityApprover(Guid facilityId, Guid employeeId, bool isDecisive = false)
    {
        FacilityId = Guard.Required(facilityId, "Facility");
        EmployeeId = Guard.Required(employeeId, "Employee");
        IsDecisive = isDecisive;
    }

    public Guid FacilityId { get; private set; }
    public Guid EmployeeId { get; private set; }

    /// <summary>
    /// Whether this person's approval alone activates a permit, without waiting for the
    /// rest of the panel.
    /// <para>
    /// The seniority override. Held here rather than on the permit because who may sign
    /// alone is a standing fact about a person's authority at a site — not something the
    /// author of a permit gets to decide about their own paperwork.
    /// </para>
    /// </summary>
    public bool IsDecisive { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void MakeDecisive() => IsDecisive = true;

    public void MakeOrdinary() => IsDecisive = false;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
