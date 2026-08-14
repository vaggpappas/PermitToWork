using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Application.Teams;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Infrastructure.Persistence.Repositories;

internal sealed class TeamRepository(
    PermitToWorkDbContext context,
    ICurrentUser currentUser,
    CounterStore counters) : ITeamRepository
{
    /// <summary>
    /// Every query in this class starts here. This is the company boundary for teams.
    /// <para>
    /// The trick is that <c>context.Employees</c> is <em>already</em> narrowed by the
    /// global query filter, so "a member I can see" needs no company id of its own — the
    /// rule is expressed once, in the DbContext, and reused here. For a caller whose scope
    /// is <c>Nothing</c> the employee set is empty, so no team matches, and the failure
    /// mode is an empty list rather than everyone's data.
    /// </para>
    /// </summary>
    private IQueryable<Team> Visible =>
        currentUser.Scope is DataScope.All
            ? context.Teams
            : context.Teams.Where(t => t.Memberships.Any(m =>
                m.LeftOn == null && context.Employees.Any(e => e.Id == m.EmployeeId)));

    public Task<Team?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Visible
            .Include(t => t.Memberships)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<PagedResult<TeamSummaryDto>> SearchAsync(
        TeamSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = Visible.AsNoTracking();

        if (request.FacilityId is { } facilityId)
        {
            query = query.Where(t => t.FacilityId == facilityId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        var term = request.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(t => EF.Functions.Like(t.Name, pattern) || EF.Functions.Like(t.Code, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<TeamSummaryDto>.Empty(request.PageSize);
        }

        var page = query
            .OrderBy(t => t.Name)
            .Skip(request.Skip)
            .Take(request.PageSize);

        var items = await (
                from t in page
                join f in context.Facilities on t.FacilityId equals f.Id
                select new TeamSummaryDto(
                    t.Id,
                    t.Code,
                    t.Name,
                    t.FacilityId,
                    f.Name,
                    t.Status,
                    // LeftOn == null rather than the domain's IsActiveOn(today): SQL cannot
                    // call a domain method, and for a membership that has already started
                    // the two agree. They differ only for a future-dated join, which the
                    // API does not currently allow.
                    t.Memberships.Count(m => m.LeftOn == null),
                    (from m in t.Memberships
                     where m.LeftOn == null && m.Role == TeamRole.Leader
                     join e in context.Employees on m.EmployeeId equals e.Id
                     select e.Name.First + " " + e.Name.Last).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<TeamSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<TeamDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var header = await (
                from t in Visible.AsNoTracking()
                join f in context.Facilities on t.FacilityId equals f.Id
                where t.Id == id
                select new
                {
                    t.Id,
                    t.Code,
                    t.Name,
                    t.Description,
                    t.FacilityId,
                    FacilityName = f.Name,
                    t.Status
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        // Joining through context.Employees means a contractor sees only the members from
        // their own company, for the same reason they only see the team at all.
        var members = await (
                from m in context.Set<TeamMembership>().AsNoTracking()
                join e in context.Employees on m.EmployeeId equals e.Id
                join tr in context.Trades on e.TradeId equals tr.Id
                join c in context.Companies on e.CompanyId equals c.Id
                where m.TeamId == id
                orderby m.LeftOn == null ? 0 : 1, e.Name.Last, e.Name.First
                select new TeamMemberDto(
                    m.Id,
                    e.Id,
                    e.Number.Value,
                    e.Name.First,
                    e.Name.Last,
                    tr.Name,
                    c.Name,
                    m.Role,
                    m.JoinedOn,
                    m.LeftOn))
            .ToListAsync(cancellationToken);

        return new TeamDetailDto(
            header.Id,
            header.Code,
            header.Name,
            header.Description,
            header.FacilityId,
            header.FacilityName,
            header.Status,
            members);
    }

    public async Task<IReadOnlyList<TeamSummaryDto>> GetForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        await (
                from t in Visible.AsNoTracking()
                join f in context.Facilities on t.FacilityId equals f.Id
                where t.Memberships.Any(m => m.EmployeeId == employeeId && m.LeftOn == null)
                orderby t.Name
                select new TeamSummaryDto(
                    t.Id,
                    t.Code,
                    t.Name,
                    t.FacilityId,
                    f.Name,
                    t.Status,
                    t.Memberships.Count(m => m.LeftOn == null),
                    (from m in t.Memberships
                     where m.LeftOn == null && m.Role == TeamRole.Leader
                     join e in context.Employees on m.EmployeeId equals e.Id
                     select e.Name.First + " " + e.Name.Last).FirstOrDefault()))
            .ToListAsync(cancellationToken);

    public async Task<string> NextCodeAsync(string teamName, int year, CancellationToken cancellationToken = default)
    {
        var prefix = $"{LettersFrom(teamName)}-{year}";

        // A separate sequence per prefix and year, so MEC-2026 and MEC-2027 each start at
        // one. Atomic, for the same reason badge numbers are.
        var next = await counters.NextAsync($"team:{prefix}", cancellationToken);

        return $"{prefix}-{next:D4}";
    }

    /// <summary>
    /// The first three letters of the name, upper-cased.
    /// <para>
    /// Non-letters are dropped first, so "3rd Shift Crew" gives RDS rather than 3RD — the
    /// code has to survive being read aloud over a radio. Short names are padded with X so
    /// the prefix is always exactly three characters and the sequence parsing below can
    /// rely on a fixed offset.
    /// </para>
    /// </summary>
    private static string LettersFrom(string teamName)
    {
        var letters = new string(teamName.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();

        return letters.Length == 3 ? letters : letters.PadRight(3, 'X');
    }

    // Codes are unique across the whole site, so the check must see past the caller's
    // scope — otherwise a contractor is told a code is free when it is not, and the insert
    // dies on the unique index instead of returning a clean 409.
    public Task<bool> CodeIsTakenAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalised = code.Trim().ToUpperInvariant();
        return context.Teams.AnyAsync(t => t.Code == normalised, cancellationToken);
    }

    public void Add(Team team) => context.Teams.Add(team);
}
