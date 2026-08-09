using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // EmployeeNumber is a single value, so it collapses to one column via a converter
        // rather than an owned type. Create() runs on the way back out of the database,
        // which means badly-formed data cannot be loaded and quietly used.
        builder.Property(e => e.Number)
            .HasConversion(number => number.Value, value => EmployeeNumber.Create(value))
            .HasColumnName("EmployeeNumber")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(e => e.Number).IsUnique();

        // Multi-field value objects become columns on this same table. No join, no second
        // entity to think about — the name is part of the employee row, as it should be.
        builder.OwnsOne(e => e.Name, name =>
        {
            name.Property(n => n.First).HasColumnName("FirstName").HasMaxLength(80).IsRequired();
            name.Property(n => n.Last).HasColumnName("LastName").HasMaxLength(80).IsRequired();
        });
        builder.Navigation(e => e.Name).IsRequired();

        builder.OwnsOne(e => e.Contact, contact =>
        {
            contact.Property(c => c.Email).HasColumnName("Email").HasMaxLength(254).IsRequired();
            contact.Property(c => c.PhoneNumber).HasColumnName("PhoneNumber").HasMaxLength(30);
            contact.HasIndex(c => c.Email).IsUnique();
        });
        builder.Navigation(e => e.Contact).IsRequired();

        // The address is optional, so every one of its columns must be nullable — that is
        // how "no address on file" is represented. The domain still refuses to build a
        // half-filled Address, so a row either has all four or none.
        builder.OwnsOne(e => e.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("AddressStreet").HasMaxLength(200).IsRequired(false);
            address.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(100).IsRequired(false);
            address.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(20).IsRequired(false);
            address.Property(a => a.Country).HasColumnName("AddressCountry").HasMaxLength(100).IsRequired(false);
        });
        builder.Navigation(e => e.Address).IsRequired(false);

        builder.Property(e => e.JobTitle).HasMaxLength(120).IsRequired();
        builder.Property(e => e.HireDate).IsRequired();
        builder.Property(e => e.DateOfBirth);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Trade>()
            .WithMany()
            .HasForeignKey(e => e.TradeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-reference for the reporting line. NoAction because a cycle of any length
        // would otherwise make SQL Server reject the cascade path outright.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Filtered: many employees have no account yet, and NULL is not "taken".
        builder.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(e => e.CompanyId);

        // Certifications are reached only through the aggregate root, so the collection is
        // exposed as IReadOnlyList and EF reads and writes the backing field directly.
        // Nothing outside Employee can add to the list.
        builder.HasMany(e => e.Certifications)
            .WithOne()
            .HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Employee.Certifications))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CertificationConfiguration : IEntityTypeConfiguration<Certification>
{
    public void Configure(EntityTypeBuilder<Certification> builder)
    {
        builder.ToTable("Certifications");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.IssuedBy).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ReferenceNumber).HasMaxLength(50);
        builder.Property(c => c.IssuedOn).IsRequired();
        builder.Property(c => c.ExpiresOn).IsRequired();

        builder.HasOne<CertificationType>()
            .WithMany()
            .HasForeignKey(c => c.CertificationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // "Who is qualified for this, and when does it lapse" is the query the permit
        // module will run constantly.
        builder.HasIndex(c => new { c.CertificationTypeId, c.ExpiresOn });
    }
}
