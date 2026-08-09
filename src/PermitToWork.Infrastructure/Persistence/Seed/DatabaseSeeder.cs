using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermitToWork.Domain.Organization;
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
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager);
        var (company, facility, trade) = await SeedReferenceDataAsync(context, cancellationToken);
        await SeedAdministratorAsync(context, userManager, configuration, company, trade, logger, cancellationToken);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }
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

    private static async Task SeedAdministratorAsync(
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
            return;
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
            return;
        }

        await userManager.AddToRoleAsync(user, ApplicationRoles.Administrator);

        // The administrator gets an employee record like everyone else, so that the
        // "who created this permit" trail points at a person rather than at a bare login.
        var employee = new Employee(
            EmployeeNumber.Create("EMP-00001"),
            PersonName.Create("System", "Administrator"),
            ContactInfo.Create(email, null),
            company.Id,
            trade.Id,
            "System Administrator",
            DateOnly.FromDateTime(DateTime.UtcNow));

        employee.LinkToUser(user.Id);
        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Seeded administrator {Email}. Change this password before deploying anywhere real.", email);
    }
}
