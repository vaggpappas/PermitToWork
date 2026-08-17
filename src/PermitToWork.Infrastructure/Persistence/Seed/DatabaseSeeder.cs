using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;
using PermitToWork.Domain.ValueObjects;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Infrastructure.Persistence.Seed;

/// <summary>
/// Brings a fresh database up to the point where someone can actually log in and do
/// something: the four roles, one administrator, and enough reference data that the
/// dropdowns are not empty.
/// <para>
/// Every step checks before it writes, so running this on an already-seeded database is a
/// no-op. That is what makes it safe to call on every startup instead of remembering to
/// run it once.
/// </para>
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));
        var context = provider.GetRequiredService<PermitToWorkDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        // No roles to seed: they are a value on the employee record, not rows in a table.
        var (company, facility, trade) = await SeedReferenceDataAsync(context, cancellationToken);
        await SeedPermitReferenceDataAsync(context, cancellationToken);
        var administrator = await SeedAdministratorAsync(
            context, userManager, configuration, company, trade, logger, cancellationToken);

        await SeedApprovalPanelAsync(context, facility, administrator, cancellationToken);
    }

    /// <summary>
    /// Permit types with the certifications they demand, and the task groups work is
    /// classified under. The certification links are what make the hard block real.
    /// </summary>
    private static async Task SeedPermitReferenceDataAsync(
        PermitToWorkDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.TaskGroups.AnyAsync(cancellationToken))
        {
            context.TaskGroups.AddRange(
                new TaskGroup("MAINT", "Maintenance"),
                new TaskGroup("INSP", "Inspection"),
                new TaskGroup("CONST", "Construction"),
                new TaskGroup("CLEAN", "Cleaning"));
        }

        if (await context.PermitTypes.AnyAsync(cancellationToken))
        {
            return;
        }

        var certifications = await context.CertificationTypes
            .ToDictionaryAsync(t => t.Code, t => t.Id, cancellationToken);

        var hotWork = new PermitType("HW", "Hot Work", "Welding, cutting, grinding — anything producing a spark.");
        var confinedSpace = new PermitType("CS", "Confined Space Entry", "Vessels, tanks, pits and trenches.");
        var height = new PermitType("WH", "Working at Height", "Anywhere a fall is possible.");
        var electrical = new PermitType("EL", "Electrical", "Work on or near live equipment.");
        var coldWork = new PermitType("CW", "Cold Work", "General mechanical work with no special hazard.");

        Require(hotWork, "HOTWORK");
        Require(confinedSpace, "CONFINED");
        Require(height, "HEIGHT");
        Require(electrical, "LOTO");
        // Cold work requires nothing — deliberately, so there is a type that demonstrates
        // the rule not firing as well as types that demonstrate it firing.

        context.PermitTypes.AddRange(hotWork, confinedSpace, height, electrical, coldWork);
        await context.SaveChangesAsync(cancellationToken);

        void Require(PermitType type, string certificationCode)
        {
            if (certifications.TryGetValue(certificationCode, out var certificationTypeId))
            {
                type.RequireCertification(certificationTypeId);
            }
        }
    }

    /// <summary>
    /// Gives the seeded facility an approval panel with the administrator on it, marked
    /// decisive — otherwise a fresh installation can raise permits but never approve one.
    /// </summary>
    private static async Task SeedApprovalPanelAsync(
        PermitToWorkDbContext context,
        Facility facility,
        Employee? administrator,
        CancellationToken cancellationToken)
    {
        if (administrator is null || await context.FacilityApprovers.AnyAsync(cancellationToken))
        {
            return;
        }

        context.FacilityApprovers.Add(new FacilityApprover(facility.Id, administrator.Id, isDecisive: true));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<(Company Company, Facility Facility, Trade Trade)> SeedReferenceDataAsync(
        PermitToWorkDbContext context,
        CancellationToken cancellationToken)
    {
        var company = await context.Companies.FirstOrDefaultAsync(c => c.Code == "OWNER", cancellationToken);
        if (company is null)
        {
            company = new Company("OWNER", "Hellenic Industrial Works", CompanyKind.Owner);
            context.Companies.Add(company);
            context.Companies.Add(new Company("ACME", "Acme Maintenance Services", CompanyKind.Contractor));
        }

        var facility = await context.Facilities.FirstOrDefaultAsync(f => f.Code == "NORTH", cancellationToken);
        if (facility is null)
        {
            facility = new Facility("NORTH", "Refinery North", "Primary processing site");
            context.Facilities.Add(facility);

            var building = new Building(facility.Id, "UNIT3", "Distillation Unit 3");
            context.Buildings.Add(building);
            context.Locations.Add(new Location(building.Id, "L2E", "Level 2 East"));
            context.Locations.Add(new Location(building.Id, "PUMP", "Pump House"));
        }

        var trade = await context.Trades.FirstOrDefaultAsync(t => t.Code == "SUPV", cancellationToken);
        if (trade is null)
        {
            trade = new Trade("SUPV", "Supervisor");
            context.Trades.AddRange(
                trade,
                new Trade("WELD3", "Welder Gr.3"),
                new Trade("PIPE", "Pipe Fitter"),
                new Trade("ELEC", "Electrician"));
        }

        if (!await context.CertificationTypes.AnyAsync(cancellationToken))
        {
            context.CertificationTypes.AddRange(
                new CertificationType("HOTWORK", "Hot Work"),
                new CertificationType("CONFINED", "Confined Space Entry"),
                new CertificationType("HEIGHT", "Working at Height"),
                new CertificationType("LOTO", "Lockout / Tagout"),
                new CertificationType("FIRSTAID", "First Aid"));
        }

        await context.SaveChangesAsync(cancellationToken);
        return (company, facility, trade);
    }

    /// <summary>Returns the administrator's employee record, so the caller can seat them on the panel.</summary>
    private static async Task<Employee?> SeedAdministratorAsync(
        PermitToWorkDbContext context,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        Company company,
        Trade trade,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var email = configuration["Seed:AdministratorEmail"] ?? "admin@permittowork.local";
        var password = configuration["Seed:AdministratorPassword"] ?? "Admin!23456";

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return await context.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Contact.Email == email, cancellationToken);
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogError("Could not seed the administrator account: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return null;
        }

        // Through the same counter the API uses, so the first employee created afterwards
        // gets 0002 rather than colliding with this one.
        var sequence = await new CounterStore(context).NextAsync($"employee:{company.Code}", cancellationToken);

        // The administrator gets an employee record like everyone else, so that the
        // "who created this permit" trail points at a person rather than at a bare login —
        // and because the record is where the access role lives.
        var employee = new Employee(
            EmployeeNumber.Create($"{company.Code}-{sequence:D4}"),
            PersonName.Create("System", "Administrator"),
            ContactInfo.Create(email, null),
            company.Id,
            trade.Id,
            "System Administrator",
            DateOnly.FromDateTime(DateTime.UtcNow));

        employee.AssignAccessRole(AccessRole.Administrator);
        employee.LinkToUser(user.Id);
        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Seeded administrator {Email}. Change this password before deploying anywhere real.", email);

        return employee;
    }
}
