using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// What a non-administrator is refused.
/// <para>
/// The unit tests cannot see any of this: role checks live in attributes, and an attribute
/// only means something once the authorisation filter runs. A mistyped role name in
/// <c>[Authorize(Roles = …)]</c> compiles perfectly and fails silently — these are the tests
/// that catch it.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(ApiFactory api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [RequiresDatabaseFact]
    public async Task AnOrdinaryEmployee_CannotCreateEmployees()
    {
        var client = await SignInAsPlainEmployeeAsync();

        var response = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Someone",
            lastName = "New",
            email = $"new.{Guid.NewGuid():N}@example.test",
            companyId = Guid.NewGuid(),
            tradeId = Guid.NewGuid(),
            jobTitle = "Fitter",
            hireDate = "2024-01-05",
        });

        // 403, not 401: they are authenticated, and simply not allowed.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresDatabaseFact]
    public async Task AnOrdinaryEmployee_CannotReadTheAuditLog()
    {
        var client = await SignInAsPlainEmployeeAsync();

        var response = await client.GetAsync("/api/audit");

        // The trail crosses every company boundary the rest of the application enforces,
        // which is exactly why only administrators may read it.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresDatabaseFact]
    public async Task AnOrdinaryEmployee_CannotAdministerReferenceData()
    {
        var client = await SignInAsPlainEmployeeAsync();

        var response = await client.PostAsJsonAsync("/api/admin/trades", new { code = "SNEAK", name = "Sneaky" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresDatabaseFact]
    public async Task AnOrdinaryEmployee_CannotSearchAnotherPersonsPermits()
    {
        var client = await SignInAsPlainEmployeeAsync();

        var response = await client.GetAsync($"/api/permits/assigned-to/{Guid.NewGuid()}");

        // Their own assignments are theirs to see. Where a *colleague* is working is a
        // supervisory question, and it lives on its own route precisely so that this check
        // exists in one place rather than as a query parameter somebody has to remember.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresDatabaseFact]
    public async Task AnOrdinaryEmployee_CanStillSeeTheirOwnPermits()
    {
        var client = await SignInAsPlainEmployeeAsync();

        var response = await client.GetAsync("/api/permits?assignedToMe=true&order=Schedule");

        // The flag resolves to the caller's own employee id on the server. There is no field
        // on the request through which it could be pointed at anybody else.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresDatabaseFact]
    public async Task AnOrdinaryEmployee_CanStillReadWhatTheyNeed()
    {
        var client = await SignInAsPlainEmployeeAsync();

        // Refusing everything would be easy and wrong. A worker has to be able to see the
        // lists the application is built on.
        (await client.GetAsync("/api/employees")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/teams")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/permits")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/lookups/trades")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Creates an employee, registers a login against it, and signs in. The record is
    /// created by an administrator first, because that is the only way an account can come
    /// to exist here.
    /// </summary>
    private async Task<HttpClient> SignInAsPlainEmployeeAsync()
    {
        var administrator = await api.SignInAsAdministratorAsync();
        var run = Guid.NewGuid().ToString("N")[..8];
        var email = $"worker.{run}@example.test";

        var companies = await administrator.GetFromJsonAsync<JsonElement>("/api/lookups/companies", Json);
        var trades = await administrator.GetFromJsonAsync<JsonElement>("/api/lookups/trades", Json);

        var created = await administrator.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Plain",
            lastName = "Worker",
            email,
            companyId = companies[0].GetProperty("id").GetGuid(),
            tradeId = trades[0].GetProperty("id").GetGuid(),
            jobTitle = "Fitter",
            hireDate = "2024-01-05",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        // Registration claims that record. It grants no role — everyone starts read-only.
        var client = api.CreateClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Worker!23456",
        });

        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await registered.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                body.GetProperty("accessToken").GetString());

        return client;
    }
}
