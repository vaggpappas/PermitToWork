using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Application.Permits;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;

namespace PermitToWork.Infrastructure.Persistence.Repositories;

internal sealed class PermitRepository(
    PermitToWorkDbContext context,
    ICurrentUser currentUser,
    CounterStore counters) : IPermitRepository
{
    /// <summary>
    /// The company boundary for permits: you see one if somebody you can see is involved in
    /// it — as its author, its receiver, an approver, or a worker on it.
    /// <para>
    /// <c>context.Employees</c> is already narrowed by the global query filter, so the rule
    /// is written once in the DbContext and reused here. A caller whose scope is
    /// <c>Nothing</c> matches no employees and therefore no permits.
    /// </para>
    /// </summary>
    private IQueryable<Permit> Visible =>
        currentUser.Scope is DataScope.All
            ? context.Permits
            : context.Permits.Where(p =>
                context.Employees.Any(e => e.Id == p.CreatedById)
                || context.Employees.Any(e => e.Id == p.ReceiverId)
                || p.Workers.Any(w => context.Employees.Any(e => e.Id == w.EmployeeId))
                || p.Approvals.Any(a => context.Employees.Any(e => e.Id == a.ApproverEmployeeId)));

    public Task<Permit?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Visible
            .Include(p => p.RequiredCertifications)
            .Include(p => p.Approvals)
            .Include(p => p.Workers)
            .Include(p => p.Equipment)
            .Include(p => p.Documents)
            .Include(p => p.Events)
            // Six collections in one query would multiply into a result set of thousands of
            // near-duplicate rows. Split queries trade one round trip for several small ones.
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PagedResult<PermitSummaryDto>> SearchAsync(
        PermitSearchRequest request,
        Guid? currentEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var query = Visible.AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (request.PermitTypeId is { } typeId)
        {
            query = query.Where(p => p.PermitTypeId == typeId);
        }

        if (request.FacilityId is { } facilityId)
        {
            query = query.Where(p => p.FacilityId == facilityId);
        }

        if (request.RaisedByMe && currentEmployeeId is { } author)
        {
            query = query.Where(p => p.CreatedById == author);
        }

        if (request.AwaitingMyApproval && currentEmployeeId is { } approver)
        {
            query = query.Where(p => p.Status == PermitStatus.Pending
                                     && p.Approvals.Any(a => a.ApproverEmployeeId == approver
                                                             && a.Decision == ApprovalDecision.Pending));
        }

        var term = request.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(p => EF.Functions.Like(p.WorkDescription, pattern)
                                     || EF.Functions.Like(p.Project!, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<PermitSummaryDto>.Empty(request.PageSize);
        }

        // Newest first: a permit book is read from the top.
        var page = query
            .OrderByDescending(p => p.Validity.Start)
            .Skip(request.Skip)
            .Take(request.PageSize);

        var items = await (
                from p in page
                join type in context.PermitTypes on p.PermitTypeId equals type.Id
                join category in context.Categories on p.CategoryId equals category.Id
                join facility in context.Facilities on p.FacilityId equals facility.Id
                join location in context.Locations on p.LocationId equals location.Id
                select new PermitSummaryDto(
                    p.Id,
                    p.Number.Value,
                    type.Name,
                    type.Code,
                    category.Name,
                    p.Project,
                    p.WorkDescription,
                    facility.Name,
                    location.Name,
                    p.Validity.Start,
                    p.Validity.End,
                    p.Status,

                    // Written out rather than factored into a helper: this is an expression
                    // tree, and EF can only translate what it can see. A local function here
                    // does not compile at all (CS8110), and a private method would compile
                    // and then fail at runtime as untranslatable.
                    //
                    // IgnoreQueryFilters is deliberate. On a permit you are already entitled
                    // to see, you are entitled to know who raised it and who is accountable
                    // for it — a safety document that hides its own signatories is useless.
                    context.Employees
                        .IgnoreQueryFilters()
                        .Where(e => e.Id == p.CreatedById)
                        .Select(e => e.Name.First + " " + e.Name.Last)
                        .FirstOrDefault() ?? "—",

                    context.Employees
                        .IgnoreQueryFilters()
                        .Where(e => e.Id == p.ReceiverId)
                        .Select(e => e.Name.First + " " + e.Name.Last)
                        .FirstOrDefault() ?? "—",

                    p.Workers.Count,
                    p.Approvals.Count(a => a.Decision == ApprovalDecision.Pending)))
            .ToListAsync(cancellationToken);

        return new PagedResult<PermitSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<PermitDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var permit = await Visible
            .AsNoTracking()
            .Include(p => p.RequiredCertifications)
            .Include(p => p.Approvals)
            .Include(p => p.Workers)
            .Include(p => p.Equipment)
            .Include(p => p.Documents)
            .Include(p => p.Events)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (permit is null)
        {
            return null;
        }

        // Everybody this permit mentions, resolved in one query rather than one per name.
        var mentioned = new List<Guid> { permit.CreatedById, permit.ReceiverId };
        mentioned.AddRange(permit.Approvals.Select(a => a.ApproverEmployeeId));
        mentioned.AddRange(permit.Workers.Select(w => w.EmployeeId));
        mentioned.AddRange(permit.Documents.Select(d => d.UploadedById));
        mentioned.AddRange(permit.Events.Where(e => e.ActorEmployeeId is not null).Select(e => e.ActorEmployeeId!.Value));

        var ids = mentioned.Distinct().ToList();

        var people = await (
                from e in context.Employees.IgnoreQueryFilters().AsNoTracking()
                join trade in context.Trades on e.TradeId equals trade.Id
                join company in context.Companies on e.CompanyId equals company.Id
                where ids.Contains(e.Id)
                select new
                {
                    e.Id,
                    Number = e.Number.Value,
                    FullName = e.Name.First + " " + e.Name.Last,
                    TradeName = trade.Name,
                    CompanyName = company.Name
                })
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var lookups = await (
                from type in context.PermitTypes.AsNoTracking()
                where type.Id == permit.PermitTypeId
                select type.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var categoryName = await context.Categories
            .Where(g => g.Id == permit.CategoryId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var place = await (
                from location in context.Locations.AsNoTracking()
                join building in context.Buildings on location.BuildingId equals building.Id
                join facility in context.Facilities on building.FacilityId equals facility.Id
                where location.Id == permit.LocationId
                select new { LocationName = location.Name, BuildingName = building.Name, FacilityName = facility.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return new PermitDetailDto(
            permit.Id,
            permit.Number.Value,
            permit.PermitTypeId,
            lookups ?? "—",
            permit.CategoryId,
            categoryName ?? "—",
            permit.Project,
            permit.WorkDescription,
            permit.Notes,
            permit.FacilityId,
            place?.FacilityName ?? "—",
            permit.LocationId,
            place?.LocationName ?? "—",
            place?.BuildingName ?? "—",
            permit.Validity.Start,
            permit.Validity.End,
            permit.Status,
            permit.StatusReason,
            permit.CreatedById,
            Name(permit.CreatedById),
            permit.ReceiverId,
            Name(permit.ReceiverId),
            permit.IssuedById,
            permit.IssuedById is { } issuer ? Name(issuer) : null,
            permit.RequiredCertifications.Select(r => r.Name).ToList(),
            permit.Approvals
                .OrderBy(a => a.IsDecisive ? 0 : 1)
                .Select(a => new PermitApprovalDto(
                    a.Id, a.ApproverEmployeeId, Name(a.ApproverEmployeeId),
                    a.IsDecisive, a.Decision, a.DecidedOn, a.Comment))
                .ToList(),
            permit.Workers
                .Select(w =>
                {
                    var found = people.GetValueOrDefault(w.EmployeeId);

                    return new PermitWorkerDto(
                        w.Id,
                        w.EmployeeId,
                        found?.Number ?? "—",
                        found?.FullName ?? "Unknown",
                        found?.TradeName ?? "—",
                        found?.CompanyName ?? "—",
                        w.Note);
                })
                .ToList(),
            permit.Equipment
                .Select(e => new PermitEquipmentDto(e.Id, e.Description, e.Identifier, e.Quantity))
                .ToList(),
            permit.Documents
                .OrderByDescending(d => d.UploadedOn)
                .Select(d => new PermitDocumentDto(
                    d.Id, d.FileName, d.ContentType, d.SizeInBytes, Name(d.UploadedById), d.UploadedOn))
                .ToList(),
            permit.Events
                .OrderByDescending(e => e.OccurredOn)
                .Select(e => new PermitEventDto(
                    e.Id,
                    e.Kind,
                    e.ActorEmployeeId is { } actor ? Name(actor) : null,
                    e.Detail,
                    e.OccurredOn))
                .ToList());

        string Name(Guid employeeId) =>
            people.TryGetValue(employeeId, out var found) ? found.FullName : "Unknown";
    }

    public async Task<string> NextNumberAsync(
        string permitTypeCode,
        int year,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{permitTypeCode.ToUpperInvariant()}-{year}";
        var next = await counters.NextAsync($"permit:{prefix}", cancellationToken);

        return $"{prefix}-{next:D4}";
    }

    public async Task<IReadOnlyList<CertificationRequirement>> GetRequirementsAsync(
        Guid permitTypeId,
        CancellationToken cancellationToken = default) =>
        await (
                from requirement in context.Set<PermitTypeCertification>().AsNoTracking()
                join type in context.CertificationTypes on requirement.CertificationTypeId equals type.Id
                where requirement.PermitTypeId == permitTypeId
                select new CertificationRequirement(type.Id, type.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PermitTypeDto>> GetPermitTypesAsync(CancellationToken cancellationToken = default) =>
        await context.PermitTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new PermitTypeDto(
                t.Id,
                t.Code,
                t.Name,
                t.Description,
                (from requirement in context.Set<PermitTypeCertification>()
                 join type in context.CertificationTypes on requirement.CertificationTypeId equals type.Id
                 where requirement.PermitTypeId == t.Id
                 select type.Name).ToList()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await context.Categories
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new LookupDto(g.Id, g.Code, g.Name))
            .ToListAsync(cancellationToken);

    public async Task<Guid?> GetFacilityOfLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        // Derived from the hierarchy rather than trusted from the caller, then stored on the
        // permit — so a later re-parenting of a room cannot move an issued permit to a
        // different facility's approval panel.
        var facilityId = await (
                from location in context.Locations.AsNoTracking()
                join building in context.Buildings on location.BuildingId equals building.Id
                where location.Id == locationId
                select (Guid?)building.FacilityId)
            .FirstOrDefaultAsync(cancellationToken);

        return facilityId;
    }

    public void Add(Permit permit) => context.Permits.Add(permit);
}

internal sealed class FacilityApproverRepository(PermitToWorkDbContext context) : IFacilityApproverRepository
{
    public async Task<IReadOnlyList<ApproverAssignment>> GetPanelAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default) =>
        await context.FacilityApprovers
            .AsNoTracking()
            .Where(a => a.FacilityId == facilityId && a.IsActive)
            .Select(a => new ApproverAssignment(a.EmployeeId, a.IsDecisive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FacilityApproverDto>> GetPanelDetailAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default) =>
        await (
                // IgnoreQueryFilters: the panel is a property of the facility, not of the
                // viewer's company, and a half-shown approval panel is worse than none.
                from approver in context.FacilityApprovers.AsNoTracking()
                join employee in context.Employees.IgnoreQueryFilters()
                    on approver.EmployeeId equals employee.Id
                where approver.FacilityId == facilityId
                orderby approver.IsDecisive descending, employee.Name.Last
                select new FacilityApproverDto(
                    approver.Id,
                    approver.FacilityId,
                    approver.EmployeeId,
                    employee.Name.First + " " + employee.Name.Last,
                    employee.Number.Value,
                    employee.JobTitle,
                    approver.IsDecisive,
                    approver.IsActive))
            .ToListAsync(cancellationToken);

    public Task<FacilityApprover?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FacilityApprovers.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> AlreadySeatedAsync(
        Guid facilityId,
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        context.FacilityApprovers.AnyAsync(
            a => a.FacilityId == facilityId && a.EmployeeId == employeeId, cancellationToken);

    public void Add(FacilityApprover approver) => context.FacilityApprovers.Add(approver);

    public void Remove(FacilityApprover approver) => context.FacilityApprovers.Remove(approver);
}
