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
/// <para>
/// There is no role here either, and no Identity role tables in the database. Roles are a
/// property of the person, not of the login, so <c>Employee.AccessRole</c> owns that fact
/// and the token's role claim is issued from it.
/// </para>
/// The link between the two is <c>Employee.UserId</c>.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>;
