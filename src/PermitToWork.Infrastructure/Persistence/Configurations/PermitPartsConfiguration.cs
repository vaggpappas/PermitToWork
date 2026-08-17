using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;

namespace PermitToWork.Infrastructure.Persistence.Configurations;

internal sealed class PermitCertificationRequirementConfiguration
    : IEntityTypeConfiguration<PermitCertificationRequirement>
{
    public void Configure(EntityTypeBuilder<PermitCertificationRequirement> builder)
    {
        builder.ToTable("PermitCertificationRequirements", "ptw");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();

        // No foreign key to CertificationTypes on purpose. This row is a record of what was
        // required at the time, and it must survive the certification type being deleted or
        // renamed — which is exactly why the name is copied alongside the id.
        builder.HasIndex(r => r.PermitId);
    }
}

internal sealed class PermitApprovalConfiguration : IEntityTypeConfiguration<PermitApproval>
{
    public void Configure(EntityTypeBuilder<PermitApproval> builder)
    {
        builder.ToTable("PermitApprovals", "ptw");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Comment).HasMaxLength(500);
        builder.Ignore(a => a.IsOutstanding);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(a => a.ApproverEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One seat per approver per permit, enforced by the database as well as by Submit.
        builder.HasIndex(a => new { a.PermitId, a.ApproverEmployeeId }).IsUnique();

        // Drives "what is waiting for me to sign", the busiest query in the module.
        builder.HasIndex(a => new { a.ApproverEmployeeId, a.Decision });
    }
}

internal sealed class PermitWorkerConfiguration : IEntityTypeConfiguration<PermitWorker>
{
    public void Configure(EntityTypeBuilder<PermitWorker> builder)
    {
        builder.ToTable("PermitWorkers", "ptw");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Note).HasMaxLength(200);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(w => w.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.PermitId, w.EmployeeId }).IsUnique();
        builder.HasIndex(w => w.EmployeeId);
    }
}

internal sealed class PermitEquipmentConfiguration : IEntityTypeConfiguration<PermitEquipment>
{
    public void Configure(EntityTypeBuilder<PermitEquipment> builder)
    {
        builder.ToTable("PermitEquipment", "ptw");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Identifier).HasMaxLength(60);
    }
}

internal sealed class PermitDocumentConfiguration : IEntityTypeConfiguration<PermitDocument>
{
    public void Configure(EntityTypeBuilder<PermitDocument> builder)
    {
        builder.ToTable("PermitDocuments", "ptw");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(260).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(d => d.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PermitEventConfiguration : IEntityTypeConfiguration<PermitEvent>
{
    public void Configure(EntityTypeBuilder<PermitEvent> builder)
    {
        builder.ToTable("PermitEvents", "ptw");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(500);

        // No foreign key to Employee for the actor. An audit line must outlive the employee
        // record it refers to — deleting a person cannot be allowed to rewrite history, and
        // a Restrict here would instead make the person undeletable, which is a different
        // and worse answer.
        builder.HasIndex(e => new { e.PermitId, e.OccurredOn });
    }
}
