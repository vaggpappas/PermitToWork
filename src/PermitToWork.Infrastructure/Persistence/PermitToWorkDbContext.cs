using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;
using PermitToWork.Domain.Organization;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Infrastructure.Persistence;

/// <summary>
/// The single unit of work over the whole database.
/// <para>
/// Identity and the domain live in one context but separate schemas — <c>identity</c> and
/// <c>org</c> — so the boundary is visible to anyone reading the database without needing
/// a second connection string and a second transaction to keep in step.
/// </para>
/// </summary>
public class PermitToWorkDbContext : IdentityUserContext<ApplicationUser, Guid>
{
    private readonly bool _seesEveryCompany;
    private readonly Guid _companyScope;

    public PermitToWorkDbContext(DbContextOptions<PermitToWorkDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        // Collapsed to two fields in the constructor because an EF query filter has to be
        // a simple expression over the context instance — it cannot pattern-match a record
        // hierarchy. The scope is read once per request, not once per query.
        (_seesEveryCompany, _companyScope) = currentUser.Scope switch
        {
            DataScope.All => (true, Guid.Empty),
            DataScope.SingleCompany company => (false, company.CompanyId),

            // Nothing. Guid.Empty matches no company, because Company's constructor
            // rejects an empty id — so "no scope" shows no rows instead of all of them.
            _ => (false, Guid.Empty)
        };
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<CertificationType> CertificationTypes => Set<CertificationType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("org");

        // Identity's tables would otherwise sprawl across the domain schema. There are four
        // of them rather than the usual eight, because this context derives from
        // IdentityUserContext instead of IdentityDbContext — no roles, no user-roles, no
        // role-claims. Roles live on Employee.AccessRole, so those tables would only be a
        // second place for the same fact to be wrong in.
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") is true
                                 || t.ClrType == typeof(ApplicationUser)))
        {
            entityType.SetSchema("identity");
        }

        builder.ApplyConfigurationsFromAssembly(typeof(PermitToWorkDbContext).Assembly);

        // The company boundary, in one line, applied to every query against Employees that
        // anyone will ever write. Putting it here rather than in EmployeeConfiguration is
        // deliberate: a security rule should be somewhere a reviewer can find in one look,
        // not one file among a dozen mapping classes.
        //
        // To step outside it you have to write IgnoreQueryFilters(), which is greppable and
        // will show up in review. That is the only way past it.
        builder.Entity<Employee>()
            .HasQueryFilter(e => _seesEveryCompany || e.CompanyId == _companyScope);
    }
}
