using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

public enum TeamStatus
{
    Active = 1,
    Disbanded = 2
}

/// <summary>
/// A work crew based at a facility. Aggregate root for its memberships — all joining,
/// leaving and role changes go through here, because only the team can see all the
/// memberships at once and therefore only the team can enforce rules that span them.
/// </summary>
public class Team : Entity
{
    private readonly List<TeamMembership> _memberships = [];

    private Team() { }

    public Team(string code, string name, Guid facilityId, string? description = null)
    {
        Code = Guard.Required(code, "Team code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Team name", 150);
        FacilityId = Guard.Required(facilityId, "Facility");
        Description = Guard.Optional(description, "Description", 500);
        Status = TeamStatus.Active;
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid FacilityId { get; private set; }
    public TeamStatus Status { get; private set; }

    public IReadOnlyList<TeamMembership> Memberships => _memberships;

    #region Queries

    public IEnumerable<TeamMembership> ActiveMembershipsOn(DateOnly asOf) =>
        _memberships.Where(m => m.IsActiveOn(asOf));

    /// <summary>
    /// Who leads the team on a given day, if anyone.
    /// <para>
    /// Deliberately derived from the memberships instead of stored as a
    /// <c>Team.LeaderId</c> column. Holding both would be two representations of one fact
    /// that can disagree — the "redundant" trap. The single source of truth is the
    /// membership whose role is <see cref="TeamRole.Leader"/>, and
    /// <see cref="AddMember"/> and <see cref="ChangeMemberRole"/> guarantee there is at
    /// most one of those active at a time.
    /// </para>
    /// </summary>
    public TeamMembership? LeaderOn(DateOnly asOf) =>
        ActiveMembershipsOn(asOf).SingleOrDefault(m => m.Role is TeamRole.Leader);

    public bool HasActiveMember(Guid employeeId, DateOnly asOf) =>
        ActiveMembershipsOn(asOf).Any(m => m.EmployeeId == employeeId);

    #endregion

    #region Membership changes

    public TeamMembership AddMember(Guid employeeId, TeamRole role, DateOnly joinedOn)
    {
        RequireActive();

        if (HasActiveMember(employeeId, joinedOn))
        {
            throw new DomainException("This employee is already an active member of the team.");
        }

        if (role is TeamRole.Leader && LeaderOn(joinedOn) is not null)
        {
            throw new DomainException($"Team '{Code}' already has a leader.");
        }

        var membership = new TeamMembership(Id, employeeId, role, joinedOn);
        _memberships.Add(membership);
        return membership;
    }

    public void RemoveMember(Guid employeeId, DateOnly leftOn)
    {
        var membership = ActiveMembershipsOn(leftOn).SingleOrDefault(m => m.EmployeeId == employeeId)
                         ?? throw new DomainException("This employee is not an active member of the team.");

        membership.End(leftOn);
    }

    public void ChangeMemberRole(Guid employeeId, TeamRole newRole, DateOnly asOf)
    {
        RequireActive();

        var membership = ActiveMembershipsOn(asOf).SingleOrDefault(m => m.EmployeeId == employeeId)
                         ?? throw new DomainException("This employee is not an active member of the team.");

        if (membership.Role == newRole)
        {
            return;
        }

        if (newRole is TeamRole.Leader && LeaderOn(asOf) is { } currentLeader && currentLeader.EmployeeId != employeeId)
        {
            throw new DomainException($"Team '{Code}' already has a leader. Change the current leader's role first.");
        }

        membership.ChangeRole(newRole);
    }

    #endregion

    #region Lifecycle

    public void Rename(string name, string? description)
    {
        Name = Guard.Required(name, "Team name", 150);
        Description = Guard.Optional(description, "Description", 500);
    }

    /// <summary>Closes the team and ends every remaining membership on the same day.</summary>
    public void Disband(DateOnly on)
    {
        RequireActive();

        foreach (var membership in ActiveMembershipsOn(on).ToList())
        {
            membership.End(on);
        }

        Status = TeamStatus.Disbanded;
    }

    private void RequireActive()
    {
        if (Status is TeamStatus.Disbanded)
        {
            throw new DomainException($"Team '{Code}' has been disbanded.");
        }
    }

    #endregion
}
