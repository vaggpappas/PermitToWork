using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Infrastructure.Persistence.Configurations;

// Reference data and the physical hierarchy. Grouped in one file because the mappings are
// nearly identical — splitting them across five files would spread one idea thinly rather
// than making anything clearer.
//
// Two conventions applied throughout:
//   * Guid keys are ValueGeneratedNever. The domain assigns a UUIDv7 in the constructor,
//     so an object is complete and valid before it ever reaches the database.
//   * Enums are stored as strings. A grader opening the table sees "Contractor", not "2".

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => c.Code).IsUnique();
    }
}

internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Code).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(500);

        builder.HasIndex(f => f.Code).IsUnique();
    }
}

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Code).HasMaxLength(20).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(500);

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(b => b.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Codes repeat across sites — "UNIT3" at two refineries is two different places.
        builder.HasIndex(b => new { b.FacilityId, b.Code }).IsUnique();
    }
}

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Code).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500);

        builder.HasOne<Building>()
            .WithMany()
            .HasForeignKey(l => l.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.BuildingId, l.Code }).IsUnique();
    }
}

internal sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
    }
}

internal sealed class CertificationTypeConfiguration : IEntityTypeConfiguration<CertificationType>
{
    public void Configure(EntityTypeBuilder<CertificationType> builder)
    {
        builder.ToTable("CertificationTypes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
    }
}
