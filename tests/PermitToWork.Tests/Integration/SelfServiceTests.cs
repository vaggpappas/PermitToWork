using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// What a person may do to their own record.
/// <para>
/// The interesting assertions here are the negative ones. A worker editing their own phone
/// number is unremarkable; a worker editing their own <em>trade</em> would let them onto
/// permits they are not qualified for, and the certification rule that the domain tests guard
/// so carefully would be sidestepped from an entirely different direction.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SelfServiceTests(ApiFactory api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [RequiresDatabaseFact]
    public async Task APersonCanReadTheirOwnRecord()
    {
        var (client, email) = await SignInAsPlainEmployeeAsync();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/employees/me", Json);

        me.GetProperty("email").GetString().Should().Be(email);
        me.GetProperty("employeeNumber").GetString().Should().NotBeNullOrEmpty();
    }

    [RequiresDatabaseFact]
    public async Task APersonCanChangeTheirOwnContactDetails()
    {
        var (client, _) = await SignInAsPlainEmployeeAsync();

        var response = await client.PutAsJsonAsync("/api/employees/me/contact", new
        {
            phoneNumber = "+30 210 555 0134",
            address = new
            {
                street = "12 Ermou",
                city = "Athens",
                postalCode = "10563",
                country = "Greece",
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/employees/me", Json);
        me.GetProperty("phoneNumber").GetString().Should().Be("+30 210 555 0134");
        me.GetProperty("address").GetProperty("city").GetString().Should().Be("Athens");
    }

    [RequiresDatabaseFact]
    public async Task ChangingContactDetails_LeavesTheTradeAlone()
    {
        var (client, _) = await SignInAsPlainEmployeeAsync();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/employees/me", Json);
        var tradeId = before.GetProperty("tradeId").GetGuid();
        var jobTitle = before.GetProperty("jobTitle").GetString();

        // Sent anyway, the way a curious user with the browser console would send it. There
        // is no field on UpdateMyContactRequest to receive any of it, so it is simply not
        // bound — no check to forget, and nothing to keep in step with the endpoint.
        var response = await client.PutAsJsonAsync("/api/employees/me/contact", new
        {
            phoneNumber = "+30 210 555 0199",
            tradeId = Guid.NewGuid(),
            jobTitle = "Site Director",
            firstName = "Promoted",
            accessRole = "Administrator",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/employees/me", Json);

        after.GetProperty("tradeId").GetGuid().Should().Be(tradeId);
        after.GetProperty("jobTitle").GetString().Should().Be(jobTitle);
        after.GetProperty("accessRole").GetString().Should().Be("Employee");
        after.GetProperty("phoneNumber").GetString().Should().Be("+30 210 555 0199");
    }

    [RequiresDatabaseFact]
    public async Task APersonStillCannotEditSomebodyElse()
    {
        var (client, _) = await SignInAsPlainEmployeeAsync();
        var administrator = await api.SignInAsAdministratorAsync();

        var somebodyElse = await administrator.GetFromJsonAsync<JsonElement>("/api/employees", Json);
        var victimId = somebodyElse.GetProperty("items")[0].GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/employees/{victimId}", new
        {
            firstName = "Edited",
            lastName = "ByAWorker",
            email = "edited@example.test",
            jobTitle = "Fitter",
            tradeId = Guid.NewGuid(),
        });

        // The self-service route has no id in it at all. The route that does still requires
        // a role, and that has not changed.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresDatabaseFact]
    public async Task AHalfFilledAddress_IsRefusedWithAReason()
    {
        var (client, _) = await SignInAsPlainEmployeeAsync();

        var response = await client.PutAsJsonAsync("/api/employees/me/contact", new
        {
            address = new { street = "12 Ermou", city = "", postalCode = "", country = "" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>An employee record created by an administrator, then claimed by registration.</summary>
    private async Task<(HttpClient Client, string Email)> SignInAsPlainEmployeeAsync()
    {
        var administrator = await api.SignInAsAdministratorAsync();
        var run = Guid.NewGuid().ToString("N")[..8];
        var email = $"self.{run}@example.test";

        var companies = await administrator.GetFromJsonAsync<JsonElement>("/api/lookups/companies", Json);
        var trades = await administrator.GetFromJsonAsync<JsonElement>("/api/lookups/trades", Json);

        var created = await administrator.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Self",
            lastName = "Service",
            email,
            companyId = companies[0].GetProperty("id").GetGuid(),
            tradeId = trades[0].GetProperty("id").GetGuid(),
            jobTitle = "Pipe Fitter",
            hireDate = "2023-02-01",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

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

        return (client, email);
    }
}
