using Microsoft.AspNetCore.Identity;

namespace PermitToWork.Infrastructure.Identity;

/// <summary>
/// A login. Credentials and nothing else.
/// <para>
/// Deliberately empty: every profile fact — name, trade, company, certifications — lives
/// on <c>Employee</c> in the Domain, which knows nothing about ASP.NET Core Identity.
/// Putting profile data here would drag an infrastructure concern into the middle of the
/// domain model and make the profile unusable for people who have no account yet, which
/// is the normal state for a contractor crew entered by an administrator.
/// </para>
/// The link between the two is <c>Employee.UserId</c>.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>;

/// <summary>A role: Administrator, SafetyOfficer, Supervisor, Employee.</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string name) : base(name) { }
}
