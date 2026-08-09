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
/// The four application roles. Constants rather than loose strings because a typo in
/// <c>[Authorize(Roles = "Adminstrator")]</c> does not fail to compile — it fails to
/// authorise, silently, in production.
/// </summary>
public static class ApplicationRoles
{
    public const string Administrator = "Administrator";
    public const string SafetyOfficer = "SafetyOfficer";
    public const string Supervisor = "Supervisor";
    public const string Employee = "Employee";

    public static readonly IReadOnlyList<string> All =
        [Administrator, SafetyOfficer, Supervisor, Employee];

    /// <summary>
    /// The roles whose holders see every company's data. Everyone else is scoped to their
    /// own employer, contractor or not.
    /// </summary>
    public static bool GrantsSiteWideAccess(string role) =>
        role is Administrator or SafetyOfficer;
}
