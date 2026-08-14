using PermitToWork.Application.Common;
using PermitToWork.Application.Teams;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Application.Abstractions;

/// <summary>
/// Team persistence.
/// <para>
/// Every method here is already narrowed to the teams the caller may see. Unlike employee
/// scoping — which is a column comparison and therefore an EF global query filter — a team
/// is visible because of who is <em>in</em> it, which spans a relationship. That is
/// expressed once as a predicate inside the implementation rather than as a global filter,
/// because a subquery in a global filter nests inside the employee filter and is a
/// reliable source of slow, surprising SQL.
/// </para>
/// </summary>
public interface ITeamRepository
{
    /// <summary>Loads the aggregate with its memberships, for a command to change.</summary>
    Task<Team?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<TeamSummaryDto>> SearchAsync(
        TeamSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Teams the given employee is currently a member of.</summary>
    Task<IReadOnlyList<TeamSummaryDto>> GetForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The next free team code for a name and year — "Mechanical Crew A" in 2026 becomes
    /// <c>MEC-2026-0001</c>.
    /// </summary>
    Task<string> NextCodeAsync(string teamName, int year, CancellationToken cancellationToken = default);

    Task<bool> CodeIsTakenAsync(string code, CancellationToken cancellationToken = default);

    void Add(Team team);
}
