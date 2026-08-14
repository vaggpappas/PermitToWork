using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Application.Teams;

public interface ITeamService
{
    Task<PagedResult<TeamSummaryDto>> SearchAsync(TeamSearchRequest request, CancellationToken cancellationToken = default);

    Task<TeamDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamSummaryDto>> GetForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default);

    Task AddMemberAsync(Guid id, AddTeamMemberRequest request, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(Guid id, Guid employeeId, RemoveTeamMemberRequest request, CancellationToken cancellationToken = default);

    Task ChangeMemberRoleAsync(Guid id, Guid employeeId, ChangeMemberRoleRequest request, CancellationToken cancellationToken = default);

    Task DisbandAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Team use cases.
/// <para>
/// As with employees, the rules live in the aggregate. "One leader at a time", "you cannot
/// join twice", "a disbanded team takes no new members" are all enforced by
/// <see cref="Team"/>. This class checks the things the aggregate cannot see — that a
/// facility id is real, that an employee exists and is visible to the caller — and then
/// gets out of the way.
/// </para>
/// </summary>
public sealed class TeamService(
    ITeamRepository teams,
    IEmployeeRepository employees,
    IReferenceDataRepository referenceData,
    IUnitOfWork unitOfWork) : ITeamService
{
    public Task<PagedResult<TeamSummaryDto>> SearchAsync(
        TeamSearchRequest request,
        CancellationToken cancellationToken = default) =>
        teams.SearchAsync(request, cancellationToken);

    public async Task<TeamDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await teams.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Team), id);

    public Task<IReadOnlyList<TeamSummaryDto>> GetForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        teams.GetForEmployeeAsync(employeeId, cancellationToken);

    public async Task<Guid> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        if (!await referenceData.FacilityExistsAsync(request.FacilityId, cancellationToken))
        {
            throw new NotFoundException(nameof(Facility), request.FacilityId);
        }

        await RequireEmployeeAsync(request.LeaderEmployeeId, cancellationToken);

        var code = await teams.NextCodeAsync(request.Name, Today.Year, cancellationToken);
        var team = new Team(code, request.Name, request.FacilityId, request.Description);

        // Created with its leader in the same unit of work, so a member-less team never
        // reaches the database — not even briefly, and not if the next call fails.
        team.AddMember(request.LeaderEmployeeId, TeamRole.Leader, Today);

        teams.Add(team);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return team.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = await RequireAsync(id, cancellationToken);

        team.Rename(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMemberAsync(Guid id, AddTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        var team = await RequireAsync(id, cancellationToken);
        await RequireEmployeeAsync(request.EmployeeId, cancellationToken);

        team.AddMember(request.EmployeeId, request.Role, request.JoinedOn ?? Today);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid id,
        Guid employeeId,
        RemoveTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var team = await RequireAsync(id, cancellationToken);

        team.RemoveMember(employeeId, request.LeftOn ?? Today);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeMemberRoleAsync(
        Guid id,
        Guid employeeId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var team = await RequireAsync(id, cancellationToken);

        team.ChangeMemberRole(employeeId, request.Role, Today);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DisbandAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await RequireAsync(id, cancellationToken);

        team.Disband(Today);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The application layer is where the clock is allowed to be read. The domain takes
    /// dates as parameters so its rules stay testable without freezing time.
    /// </summary>
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<Team> RequireAsync(Guid id, CancellationToken cancellationToken) =>
        await teams.FindAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Team), id);

    /// <summary>
    /// Note this goes through the employee repository, which is company-scoped. A
    /// contractor trying to add someone from another company gets a 404 — the same answer
    /// as for an id that does not exist, which is the answer they should get.
    /// </summary>
    private async Task RequireEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (await employees.FindAsync(employeeId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Employee), employeeId);
        }
    }
}
