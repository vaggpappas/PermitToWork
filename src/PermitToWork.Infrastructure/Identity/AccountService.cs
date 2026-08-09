using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Accounts;
using PermitToWork.Domain.Organization;
using PermitToWork.Infrastructure.Persistence;

namespace PermitToWork.Infrastructure.Identity;

/// <summary>
/// Registration and sign-in over ASP.NET Core Identity.
/// <para>
/// Registration <em>claims</em> an employee record rather than creating one. An
/// administrator enters the employee first — with their badge number, employer and trade —
/// and the person then registers against that email to attach a login. Nobody gets to
/// assert for themselves which company they work for, which is the whole basis of the
/// company scoping that follows.
/// </para>
/// </summary>
internal sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    PermitToWorkDbContext context,
    JwtTokenFactory tokenFactory) : IAccountService
{
    /// <summary>
    /// One message for "no such employee", "already registered" and "email unknown".
    /// Distinguishing them would turn this endpoint into a way to discover who works here.
    /// </summary>
    private const string CannotRegister =
        "No employee record is awaiting registration for this email address. Contact your administrator.";

    public async Task<AuthenticationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = Normalise(request.Email);

        // IgnoreQueryFilters is deliberate and safe here: the caller is anonymous, so the
        // company scope is Nothing and the filter would match no rows at all. The lookup
        // is by exact email against a record that must already exist, so this cannot be
        // used to enumerate employees.
        var employee = await context.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Contact.Email == email, cancellationToken);

        if (employee is null || employee.UserId is not null)
        {
            return AuthenticationResult.Failure(CannotRegister);
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return AuthenticationResult.Failure(CannotRegister);
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return AuthenticationResult.Failure(created.Errors.Select(e => e.Description).ToArray());
        }

        await userManager.AddToRoleAsync(user, ApplicationRoles.Employee);

        // The domain refuses a second link, so a race between two registrations for the
        // same email ends in a DomainException rather than a silently stolen account.
        employee.LinkToUser(user.Id);
        await context.SaveChangesAsync(cancellationToken);

        return await IssueTokenAsync(user, employee);
    }

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = Normalise(request.Email);
        var user = await userManager.FindByEmailAsync(email);

        // One message for both "no such user" and "wrong password", so the response cannot
        // be used to check whether an email address has an account here.
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return AuthenticationResult.Failure("Invalid email or password.");
        }

        var employee = await context.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.UserId == user.Id, cancellationToken);

        return await IssueTokenAsync(user, employee);
    }

    private async Task<AuthenticationResult> IssueTokenAsync(ApplicationUser user, Employee? employee)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = tokenFactory.Create(user.Id, user.Email!, roles, employee);

        return AuthenticationResult.Success(accessToken, expiresAt);
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
