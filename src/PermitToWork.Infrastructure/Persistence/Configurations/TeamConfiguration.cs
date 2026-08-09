using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(t => t.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Memberships)
            .WithOne()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Team.Memberships))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("TeamMemberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.JoinedOn).IsRequired();
        builder.Property(m => m.LeftOn);

        // Restrict, not Cascade: deleting an employee who has ever been on a team would
        // erase the record of who was in the crew. Employees are terminated, not deleted.
        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Drives "which teams is this person in" without a table scan.
        builder.HasIndex(m => new { m.EmployeeId, m.LeftOn });
        builder.HasIndex(m => new { m.TeamId, m.EmployeeId });

        // The "one active leader per team" rule is enforced by the Team aggregate rather
        // than here: a filtered unique index cannot express "active on a given date",
        // since LeftOn being null is only part of the condition.
    }
}
