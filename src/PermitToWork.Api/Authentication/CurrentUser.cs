using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using PermitToWork.Application.Abstractions;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Authentication;

/// <summary>
/// Reads the caller's identity off the validated bearer token.
/// <para>
/// This is the only class in the solution that knows the answer comes from HTTP. Everyone
/// else asks <see cref="ICurrentUser"/>, which is why the domain and the persistence layer
/// can be tested without a request in sight.
/// </para>
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated is true;

    public Guid? UserId => ReadGuid(JwtRegisteredClaimNames.Sub);

    public Guid? EmployeeId => ReadGuid(PermitToWorkClaims.EmployeeId);

    public DataScope Scope
    {
        get
        {
            if (!IsAuthenticated)
            {
                return new DataScope.Nothing();
            }

            if (Principal!.FindFirst(PermitToWorkClaims.Scope)?.Value == PermitToWorkClaims.ScopeAllCompanies)
            {
                return new DataScope.All();
            }

            // An authenticated caller with no company claim is not given the benefit of the
            // doubt. Falling back to Nothing means a malformed token sees an empty database
            // rather than everyone's.
            return ReadGuid(PermitToWorkClaims.CompanyId) is { } companyId
                ? new DataScope.SingleCompany(companyId)
                : new DataScope.Nothing();
        }
    }

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirst(claimType)?.Value, out var value) ? value : null;
}
