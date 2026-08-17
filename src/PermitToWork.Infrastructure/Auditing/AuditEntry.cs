using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PermitToWork.Infrastructure.Auditing;

/// <summary>What happened to a row.</summary>
public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3
}

/// <summary>
/// One change to one row: who, what, when, and what the values were before and after.
/// <para>
/// This is the technical audit — every insert, update and delete the application makes,
/// captured automatically. It sits alongside <c>PermitEvent</c>, which is the business
/// audit: "approved by Maria because the isolation was confirmed". Both are worth having.
/// One answers "what did the system do", the other "what did the organisation decide", and
/// neither substitutes for the other.
/// </para>
/// <para>
/// Append-only. There is no method here to change or remove a line, and nothing in the
/// application updates this table — a log that can be tidied up is not evidence.
/// </para>
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;

    public AuditAction Action { get; init; }

    /// <summary>The type that changed — "Employee", or "Employee.PersonName" for an owned value.</summary>
    public string EntityType { get; init; } = null!;

    /// <summary>The primary key of the row. For an owned value, the key of its owner.</summary>
    public string EntityId { get; init; } = null!;

    public Guid? ActorUserId { get; init; }

    public Guid? ActorEmployeeId { get; init; }

    /// <summary>
    /// Copied, not joined. An audit line has to remain readable after the employee record
    /// it names has been terminated, renamed, or had its email changed — that is precisely
    /// the moment somebody goes looking.
    /// </summary>
    public string? ActorDescription { get; init; }

    public string? RequestMethod { get; init; }

    public string? RequestPath { get; init; }

    public string? IpAddress { get; init; }

    /// <summary>JSON: property names to their old and new values. Null for deletes with no detail.</summary>
    public string? Changes { get; init; }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", "audit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(120).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ActorDescription).HasMaxLength(320);
        builder.Property(e => e.RequestMethod).HasMaxLength(10);
        builder.Property(e => e.RequestPath).HasMaxLength(400);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.Changes).HasMaxLength(4000);

        // The three questions this table gets asked: what happened lately, what happened to
        // this record, and what has this person been doing.
        builder.HasIndex(e => e.OccurredOn).IsDescending();
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => e.ActorEmployeeId);

        // No foreign keys anywhere on this table, deliberately. An audit line must outlive
        // whatever it refers to; a FK would either block the deletion or cascade the
        // evidence away with it.
    }
}
