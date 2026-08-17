using System.ComponentModel.DataAnnotations;
using PermitToWork.Application.Common;

namespace PermitToWork.Application.Auditing;

public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredOn,
    string Action,
    string EntityType,
    string EntityId,
    Guid? ActorEmployeeId,
    string ActorName,
    string? RequestMethod,
    string? RequestPath,
    string? IpAddress,
    string? Changes);

public sealed record AuditSearchRequest : PageRequest
{
    /// <summary>Matches the entity type, the path, or the actor's name.</summary>
    [StringLength(120)]
    public string? Search { get; init; }

    /// <summary>Created, Updated or Deleted.</summary>
    [StringLength(20)]
    public string? Action { get; init; }

    /// <summary>Everything that happened to one record — "Employee" plus its id.</summary>
    [StringLength(120)]
    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    /// <summary>Everything one person has done.</summary>
    public Guid? ActorEmployeeId { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}

public interface IAuditRepository
{
    Task<PagedResult<AuditEntryDto>> SearchAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Everything that ever happened to one record, oldest first.</summary>
    Task<IReadOnlyList<AuditEntryDto>> ForRecordAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);
}
