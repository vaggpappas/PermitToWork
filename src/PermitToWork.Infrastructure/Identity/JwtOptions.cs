using PermitToWork.Domain.Organization;

namespace PermitToWork.Infrastructure.Identity;

/// <summary>
/// Token settings, bound from the <c>Jwt</c> configuration section.
/// The signing key is deliberately not given a default: an application that cannot find
/// one should refuse to start rather than quietly sign tokens with a value an attacker can
/// read in the source repository.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "PermitToWork.Api";
    public string Audience { get; init; } = "PermitToWork.Client";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; init; } = 60;
}

/// <summary>
/// Claim types this application issues itself. Prefixed so they can never collide with a
/// registered JWT claim or with anything ASP.NET Core adds.
/// </summary>
public static class PermitToWorkClaims
{
    public const string EmployeeId = "ptw:employee_id";
    public const string CompanyId = "ptw:company_id";

    /// <summary>Either <see cref="ScopeAllCompanies"/> or absent, in which case the company claim applies.</summary>
    public const string Scope = "ptw:scope";

    public const string ScopeAllCompanies = "all";
}

/// <summary>
/// Role names as strings, for <c>[Authorize(Roles = …)]</c>.
/// <para>
/// Constants rather than loose strings because a typo in
/// <c>[Authorize(Roles = "Adminstrator")]</c> does not fail to compile — it fails to
/// authorise, silently. Each one matches a member of
/// <see cref="Domain.Organization.AccessRole"/> exactly, which is where the value actually
/// lives; these are only the spelling used in the token and the attributes.
/// </para>
/// </summary>
public static class ApplicationRoles
{
    public const string Administrator = nameof(AccessRole.Administrator);
    public const string SafetyOfficer = nameof(AccessRole.SafetyOfficer);
    public const string Supervisor = nameof(AccessRole.Supervisor);
    public const string Responsible = nameof(AccessRole.Responsible);
    public const string Employee = nameof(AccessRole.Employee);

    /// <summary>
    /// Who sees every company's data. Everyone else is scoped to their own employer,
    /// contractor or not.
    /// </summary>
    public static bool GrantsSiteWideAccess(AccessRole role) =>
        role is AccessRole.Administrator or AccessRole.SafetyOfficer;
}
