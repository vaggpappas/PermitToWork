using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PermitToWork.Application.Abstractions;

namespace PermitToWork.Infrastructure.Auditing;

/// <summary>
/// Writes an audit line for every row the application inserts, updates or deletes.
/// <para>
/// Deliberately an interceptor rather than a call in each service. A service that has to
/// remember to log is a service that will not, on the one endpoint written in a hurry —
/// and that endpoint is invariably the one somebody asks about. Here, a new feature is
/// audited the day it is written by nobody doing anything.
/// </para>
/// <para>
/// It runs inside the same <c>SaveChanges</c> as the change itself, so the audit line and
/// the change are one transaction: either both land or neither does. A log that can
/// disagree with the data it describes is worse than no log.
/// </para>
/// </summary>
internal sealed class AuditSaveChangesInterceptor(
    ICurrentUser currentUser,
    IRequestContext requestContext) : SaveChangesInterceptor
{
    /// <summary>
    /// Never recorded, whatever their value. Password hashes and security stamps would turn
    /// the audit table into the most attractive thing in the database.
    /// </summary>
    private static readonly string[] Redacted =
        ["Password", "Hash", "Stamp", "Token", "Secret", "Key"];

    /// <summary>
    /// Tables whose churn says nothing about intent. Counters tick on every permit raised,
    /// and auditing the audit table is a loop.
    /// </summary>
    private static readonly string[] NotAudited =
        [nameof(AuditEntry), "Counter"];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Capture(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Capture(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Capture(DbContext context)
    {
        // Before the save, while original values are still available and before EF resets
        // entry states. Keys are assigned by the domain in the constructor, so a new row's
        // id is already known here and no second round trip is needed.
        context.ChangeTracker.DetectChanges();

        var entries = context.ChangeTracker
            .Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => !NotAudited.Contains(entry.Metadata.ClrType.Name))
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var actor = Describe();
        var lines = entries.Select(entry => ToAuditEntry(entry, actor)).Where(line => line is not null).ToList();

        foreach (var line in lines)
        {
            context.Add(line!);
        }
    }

    private AuditEntry? ToAuditEntry(EntityEntry entry, string? actor)
    {
        var changes = Describe(entry);

        // A Modified entry with nothing meaningful changed — a value object reassigned to
        // an equal value, say — is noise rather than history.
        if (entry.State is EntityState.Modified && changes.Count == 0)
        {
            return null;
        }

        // Owned values (a person's name, a permit's validity) are separate entries in the
        // change tracker but not separate things to a reader. Naming them "Employee.PersonName"
        // keeps them attached to what they belong to, and their key is already the owner's.
        var ownership = entry.Metadata.FindOwnership();
        var entityType = ownership is null
            ? entry.Metadata.ClrType.Name
            : $"{ownership.PrincipalEntityType.ClrType.Name}.{entry.Metadata.ClrType.Name}";

        var key = string.Join(
            ", ",
            entry.Properties
                .Where(property => property.Metadata.IsPrimaryKey())
                .Select(property => property.CurrentValue ?? property.OriginalValue));

        var json = changes.Count == 0 ? null : JsonSerializer.Serialize(changes);

        return new AuditEntry
        {
            Action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            },
            EntityType = entityType,
            EntityId = string.IsNullOrEmpty(key) ? "—" : key,
            ActorUserId = currentUser.UserId,
            ActorEmployeeId = currentUser.EmployeeId,
            ActorDescription = actor,
            RequestMethod = requestContext.Method,
            RequestPath = requestContext.Path,
            IpAddress = requestContext.IpAddress,
            Changes = json is { Length: > 4000 } ? json[..4000] : json
        };
    }

    private static Dictionary<string, object?> Describe(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (Redacted.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is not null:
                    changes[name] = property.CurrentValue;
                    break;

                case EntityState.Deleted:
                    changes[name] = property.OriginalValue;
                    break;

                case EntityState.Modified when property.IsModified
                                               && !Equals(property.OriginalValue, property.CurrentValue):
                    // Both sides. "Status changed" is a note; "Status: Active → Suspended"
                    // is an answer.
                    changes[name] = new { from = property.OriginalValue, to = property.CurrentValue };
                    break;
            }
        }

        return changes;
    }

    /// <summary>
    /// Who acted, as text. Null when nobody did — the expiry sweep, or the seeder on a
    /// fresh database — and a null actor is a truer record than attributing it to whoever
    /// happened to start the process.
    /// </summary>
    private string? Describe()
    {
        if (currentUser.EmployeeId is { } employeeId)
        {
            return $"employee {employeeId}";
        }

        return currentUser.UserId is { } userId ? $"user {userId}" : null;
    }
}
