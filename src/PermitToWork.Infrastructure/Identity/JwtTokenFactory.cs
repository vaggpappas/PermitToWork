using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Infrastructure.Identity;

/// <summary>
/// Turns a signed-in user into a bearer token.
/// <para>
/// The token carries the caller's data scope as a claim, which is what makes every
/// subsequent request cheap: the API does not have to look up "which company does this
/// person work for" on the way into each query.
/// </para>
/// </summary>
internal sealed class JwtTokenFactory(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>
    /// Issues a token for a login.
    /// <para>
    /// The role comes from the employee record, not from a role table — so changing what
    /// somebody may do is one field on one row, and there is no second place that can
    /// disagree with it. A login with no employee record gets no role at all, which is
    /// correct: it can authenticate but do nothing.
    /// </para>
    /// </summary>
    public (string AccessToken, DateTimeOffset ExpiresAtUtc) Create(
        Guid userId,
        string email,
        Employee? employee)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it via user secrets or an environment variable.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        if (employee is not null)
        {
            claims.Add(new Claim(PermitToWorkClaims.EmployeeId, employee.Id.ToString()));
            claims.Add(new Claim(PermitToWorkClaims.CompanyId, employee.CompanyId.ToString()));

            // "role" rather than the long WS-Federation URI, matched by RoleClaimType on
            // the validation side. Short, readable in jwt.io, no claim-mapping surprises.
            claims.Add(new Claim("role", employee.AccessRole.ToString()));

            if (ApplicationRoles.GrantsSiteWideAccess(employee.AccessRole))
            {
                claims.Add(new Claim(PermitToWorkClaims.Scope, PermitToWorkClaims.ScopeAllCompanies));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
