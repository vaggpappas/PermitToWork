using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Auditing;
using PermitToWork.Application.Common;
using PermitToWork.Infrastructure.Persistence;

namespace PermitToWork.Infrastructure.Auditing;

internal sealed class AuditRepository(PermitToWorkDbContext context) : IAuditRepository
{
    public async Task<PagedResult<AuditEntryDto>> SearchAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = context.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(e => e.Action.ToString() == action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();

            // StartsWith rather than equals, so asking for "Employee" also returns the
            // owned values recorded as "Employee.PersonName".
            query = query.Where(e => e.EntityType.StartsWith(entityType));
        }

        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            var entityId = request.EntityId.Trim();
            query = query.Where(e => e.EntityId == entityId);
        }

        if (request.ActorEmployeeId is { } actor)
        {
            query = query.Where(e => e.ActorEmployeeId == actor);
        }

        if (request.From is { } from)
        {
            query = query.Where(e => e.OccurredOn >= from);
        }

        if (request.To is { } to)
        {
            query = query.Where(e => e.OccurredOn <= to);
        }

        var term = request.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(e => EF.Functions.Like(e.EntityType, pattern)
                                     || EF.Functions.Like(e.RequestPath!, pattern)
                                     || EF.Functions.Like(e.Changes!, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<AuditEntryDto>.Empty(request.PageSize);
        }

        var page = await query
            // Newest first: the question is nearly always "what just happened".
            .OrderByDescending(e => e.OccurredOn)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = await ResolveActorsAsync(page, cancellationToken);

        return new PagedResult<AuditEntryDto>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<AuditEntryDto>> ForRecordAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var entries = await context.AuditEntries
            .AsNoTracking()
            .Where(e => e.EntityType.StartsWith(entityType) && e.EntityId == entityId)
            .OrderBy(e => e.OccurredOn)
            .ToListAsync(cancellationToken);

        return await ResolveActorsAsync(entries, cancellationToken);
    }

    /// <summary>
    /// Turns actor ids into names, in one query rather than one per line.
    /// <para>
    /// IgnoreQueryFilters because an audit reader is asking who did something, and the
    /// answer must not depend on which company the reader belongs to — the endpoint is
    /// administrators only for exactly that reason. Anyone no longer resolvable falls back
    /// to the description stored at the time.
    /// </para>
    /// </summary>
    private async Task<List<AuditEntryDto>> ResolveActorsAsync(
        List<AuditEntry> entries,
        CancellationToken cancellationToken)
    {
        var actorIds = entries
            .Where(e => e.ActorEmployeeId is not null)
            .Select(e => e.ActorEmployeeId!.Value)
            .Distinct()
            .ToList();

        var names = actorIds.Count == 0
            ? []
            : await context.Employees
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(employee => actorIds.Contains(employee.Id))
                .Select(employee => new { employee.Id, Name = employee.Name.First + " " + employee.Name.Last })
                .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);

        return entries.Select(entry => new AuditEntryDto(
            entry.Id,
            entry.OccurredOn,
            entry.Action.ToString(),
            entry.EntityType,
            entry.EntityId,
            entry.ActorEmployeeId,
            entry.ActorEmployeeId is { } id && names.TryGetValue(id, out var name)
                ? name
                : entry.ActorDescription ?? "the system",
            entry.RequestMethod,
            entry.RequestPath,
            entry.IpAddress,
            entry.Changes)).ToList();
    }
}
