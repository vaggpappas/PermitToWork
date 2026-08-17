using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Infrastructure.Persistence.Configurations;

// The permit module. Same conventions as the rest: Guid keys assigned by the domain, enums
// stored as strings, and every collection reached only through the aggregate root, so EF
// reads and writes the backing fields rather than the read-only properties.

internal sealed class PermitTypeConfiguration : IEntityTypeConfiguration<PermitType>
{
    public void Configure(EntityTypeBuilder<PermitType> builder)
    {
        builder.ToTable("PermitTypes", "ptw");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Code).HasMaxLength(4).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);

        builder.HasIndex(t => t.Code).IsUnique();

        builder.HasMany(t => t.RequiredCertifications)
            .WithOne()
            .HasForeignKey(r => r.PermitTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(PermitType.RequiredCertifications))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PermitTypeCertificationConfiguration : IEntityTypeConfiguration<PermitTypeCertification>
{
    public void Configure(EntityTypeBuilder<PermitTypeCertification> builder)
    {
        builder.ToTable("PermitTypeCertifications", "ptw");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasOne<CertificationType>()
            .WithMany()
            .HasForeignKey(r => r.CertificationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.PermitTypeId, r.CertificationTypeId }).IsUnique();
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "ptw");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Code).HasMaxLength(20).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(g => g.Code).IsUnique();
    }
}

internal sealed class FacilityApproverConfiguration : IEntityTypeConfiguration<FacilityApprover>
{
    public void Configure(EntityTypeBuilder<FacilityApprover> builder)
    {
        builder.ToTable("FacilityApprovers");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(a => a.FacilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One seat per person per facility — being on the panel twice is not a stronger
        // approval, it is a permit that can never be fully signed.
        builder.HasIndex(a => new { a.FacilityId, a.EmployeeId }).IsUnique();
    }
}

internal sealed class PermitConfiguration : IEntityTypeConfiguration<Permit>
{
    public void Configure(EntityTypeBuilder<Permit> builder)
    {
        builder.ToTable("Permits", "ptw");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Number)
            .HasConversion(number => number.Value, value => PermitNumber.Create(value))
            .HasColumnName("PermitNumber")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(p => p.Number).IsUnique();

        // The validity window is one concept, so it maps to two columns on this row rather
        // than a joined table. Ordering is guaranteed by the value object, not by a CHECK.
        builder.OwnsOne(p => p.Validity, validity =>
        {
            validity.Property(v => v.Start).HasColumnName("ValidFrom").IsRequired();
            validity.Property(v => v.End).HasColumnName("ValidTo").IsRequired();
        });
        builder.Navigation(p => p.Validity).IsRequired();

        builder.Property(p => p.WorkDescription).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.Project).HasMaxLength(150);
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.Property(p => p.StatusReason).HasMaxLength(500);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // IssuedById is derived from the approvals and has no column. Storing it would be a
        // second copy of a fact the approvals already hold.
        builder.Ignore(p => p.IssuedById);
        builder.Ignore(p => p.OutstandingApprovals);
        builder.Ignore(p => p.IsEditable);
        builder.Ignore(p => p.CanChangeResources);
        builder.Ignore(p => p.IsFinished);
        builder.Ignore(p => p.IsLive);

        builder.HasOne<PermitType>().WithMany().HasForeignKey(p => p.PermitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Facility>().WithMany().HasForeignKey(p => p.FacilityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(p => p.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(p => p.ReceiverId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.FacilityId);

        ConfigureOwnedCollection(builder, p => p.RequiredCertifications, r => r.PermitId);
        ConfigureOwnedCollection(builder, p => p.Approvals, a => a.PermitId);
        ConfigureOwnedCollection(builder, p => p.Workers, w => w.PermitId);
        ConfigureOwnedCollection(builder, p => p.Equipment, e => e.PermitId);
        ConfigureOwnedCollection(builder, p => p.Documents, d => d.PermitId);
        ConfigureOwnedCollection(builder, p => p.Events, e => e.PermitId);
    }

    /// <summary>
    /// Wires one of the permit's collections: cascade delete, and field access so EF writes
    /// the backing list directly. The properties are <c>IReadOnlyList</c> precisely so that
    /// nothing outside the aggregate can add to them, which also means EF cannot use them.
    /// </summary>
    private static void ConfigureOwnedCollection<TChild>(
        EntityTypeBuilder<Permit> builder,
        System.Linq.Expressions.Expression<Func<Permit, IEnumerable<TChild>?>> navigation,
        System.Linq.Expressions.Expression<Func<TChild, object?>> foreignKey)
        where TChild : class
    {
        builder.HasMany(navigation)
            .WithOne()
            .HasForeignKey(foreignKey)
            .OnDelete(DeleteBehavior.Cascade);

        var name = ((System.Linq.Expressions.MemberExpression)navigation.Body).Member.Name;
        builder.Metadata.FindNavigation(name)!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
