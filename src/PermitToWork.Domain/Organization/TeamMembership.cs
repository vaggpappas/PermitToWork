using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

/// <summary>What an employee does inside a team.</summary>
public enum TeamRole
{
    Member = 1,
    Deputy = 2,
    Leader = 3
}

/// <summary>
/// One employee's spell in one team. An entity rather than a plain many-to-many join,
/// because membership has a start, an end and a role — facts a skip navigation cannot hold.
/// <para>
/// Created only via <see cref="Team.AddMember"/>: the team is the aggregate root and is the
/// only thing that can check the "one leader at a time" rule.
/// </para>
/// </summary>
public class TeamMembership : Entity
{
    private TeamMembership() { }

    internal TeamMembership(Guid teamId, Guid employeeId, TeamRole role, DateOnly joinedOn)
    {
        TeamId = Guard.Required(teamId, "Team");
        EmployeeId = Guard.Required(employeeId, "Employee");
        Role = role;
        JoinedOn = joinedOn;
    }

    public Guid TeamId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public TeamRole Role { get; private set; }
    public DateOnly JoinedOn { get; private set; }

    /// <summary>
    /// The day they left. Null means "still a member" — a real absence of a date rather
    /// than a sentinel like <c>9999-12-31</c> that every query would have to know about.
    /// </summary>
    public DateOnly? LeftOn { get; private set; }

    /// <summary>
    /// Half-open interval: the leaving date is the first day they are no longer a member.
    /// Stated once here so no caller has to remember whether the end is inclusive.
    /// </summary>
    public bool IsActiveOn(DateOnly asOf) => asOf >= JoinedOn && (LeftOn is null || asOf < LeftOn);

    internal void End(DateOnly leftOn)
    {
        if (leftOn < JoinedOn)
        {
            throw new DomainException("A member cannot leave a team before they joined it.");
        }

        LeftOn = leftOn;
    }

    internal void ChangeRole(TeamRole role) => Role = role;
}
