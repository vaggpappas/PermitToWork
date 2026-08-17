using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// Authentication over the real pipeline — the JWT handler, the claim mapping, the
/// authorisation filters. None of this is reachable from a unit test, and all of it is
/// wiring that a rename or a misplaced <c>UseAuthentication</c> would break silently.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthenticationTests(ApiFactory api)
{
    [RequiresDatabaseFact]
    public async Task Login_IssuesAToken()
    {
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiFactory.AdministratorEmail,
            password = ApiFactory.AdministratorPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!.Should().ContainKey("accessToken");
    }

    [RequiresDatabaseFact]
    public async Task Login_RefusesAWrongPassword_WithoutSayingWhichPartWasWrong()
    {
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiFactory.AdministratorEmail,
            password = "definitely-not-the-password",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync();

        // The same message as for an unknown email. Otherwise this endpoint becomes a way
        // to find out who has an account here.
        problem.Should().Contain("Invalid email or password");
    }

    [RequiresDatabaseFact]
    public async Task ProtectedEndpoints_RefuseAnonymousCallers()
    {
        var client = api.CreateClient();

        var response = await client.GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresDatabaseFact]
    public async Task Health_IsOpen()
    {
        var client = api.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresDatabaseFact]
    public async Task TheToken_CarriesTheRoleAndTheCompanyScope()
    {
        var client = await api.SignInAsAdministratorAsync();

        var me = await client.GetFromJsonAsync<Dictionary<string, object>>("/api/auth/me");

        // Proves the whole chain: the role came from Employee.AccessRole, was written as a
        // "role" claim, survived MapInboundClaims being off, and was read back by
        // CurrentUser. Any link breaking leaves this empty.
        me!["roles"].ToString().Should().Contain("Administrator");
        me["scope"].ToString().Should().Be("all companies");
        me["employeeId"].ToString().Should().NotBeNullOrEmpty();
    }

    [RequiresDatabaseFact]
    public async Task Registering_AgainstAnUnknownEmail_IsRefused()
    {
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "nobody@nowhere.test",
            password = "Str0ng!Passw0rd",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Nobody creates their own employee record. An administrator enters it first.
        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("No employee record is awaiting registration");
    }
}
