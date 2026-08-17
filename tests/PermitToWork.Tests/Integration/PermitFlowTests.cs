using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// The permit lifecycle end to end, through HTTP.
/// <para>
/// The domain tests already prove the rules. What these prove is that the rules survive the
/// journey: that a <c>DomainException</c> becomes a 422 with its message intact, that the
/// generated permit number reaches the client, that the counter table hands out
/// non-colliding numbers under real SQL Server, and that the flags the UI draws its buttons
/// from say what the aggregate would say.
/// </para>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PermitFlowTests(ApiFactory api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [RequiresDatabaseFact]
    public async Task APermit_RunsFromDraftToClosed_AndRefusesAnUncertifiedWorker()
    {
        var client = await api.SignInAsAdministratorAsync();
        var run = Guid.NewGuid().ToString("N")[..8];

        var (companyId, tradeId, hotWorkCertificationId) = await ReferenceDataAsync(client);
        var (hotWorkTypeId, categoryId, locationId) = await PermitReferenceAsync(client);

        // A welder who holds a Hot Work certificate, and a labourer who does not.
        var welderId = await CreateEmployeeAsync(client, $"luis.{run}@example.test", companyId, tradeId, "Welder");
        await GiveCertificateAsync(client, welderId, hotWorkCertificationId);
        var labourerId = await CreateEmployeeAsync(client, $"marta.{run}@example.test", companyId, tradeId, "Operative");

        // ---------- raise it ----------
        var created = await client.PostAsJsonAsync("/api/permits", new
        {
            permitTypeId = hotWorkTypeId,
            categoryId,
            workDescription = "Replace the flange on the north header.",
            locationId,
            validFrom = DateTimeOffset.UtcNow.AddDays(1),
            validTo = DateTimeOffset.UtcNow.AddDays(3),
            receiverId = welderId,
            project = "Unit 3 Turnaround",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var permitId = await IdOfAsync(created);

        var permit = await client.GetFromJsonAsync<JsonElement>($"/api/permits/{permitId}", Json);
        permit.GetProperty("permitNumber").GetString().Should().MatchRegex(@"^HW-\d{4}-\d{4}$");
        permit.GetProperty("status").GetString().Should().Be("Draft");
        permit.GetProperty("canSubmit").GetBoolean().Should().BeFalse();

        // ---------- the certification rule, over HTTP ----------
        var refused = await client.PostAsJsonAsync($"/api/permits/{permitId}/workers", new { employeeId = labourerId });

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // The aggregate's own sentence, carried out through the exception handler unchanged.
        var detail = await refused.Content.ReadAsStringAsync();
        detail.Should().Contain("Hot Work").And.Contain("whole permit period");

        var accepted = await client.PostAsJsonAsync($"/api/permits/{permitId}/workers", new { employeeId = welderId });
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ---------- submit and approve ----------
        (await client.PostAsync($"/api/permits/{permitId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        permit = await client.GetFromJsonAsync<JsonElement>($"/api/permits/{permitId}", Json);
        permit.GetProperty("status").GetString().Should().Be("Pending");
        permit.GetProperty("approvals").GetArrayLength().Should().BeGreaterThan(0);

        // The crew is frozen while people are signing.
        var frozen = await client.PostAsJsonAsync($"/api/permits/{permitId}/workers", new { employeeId = labourerId });
        frozen.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await client.PostAsJsonAsync($"/api/permits/{permitId}/approve", new { comment = "Isolation confirmed." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        permit = await client.GetFromJsonAsync<JsonElement>($"/api/permits/{permitId}", Json);

        // The seeded administrator is a decisive approver, so one signature activates it.
        permit.GetProperty("status").GetString().Should().Be("Active");
        permit.GetProperty("issuedByName").GetString().Should().NotBeNullOrEmpty();

        // ---------- close it ----------
        (await client.PostAsJsonAsync($"/api/permits/{permitId}/close", new { note = "Done and tested." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        permit = await client.GetFromJsonAsync<JsonElement>($"/api/permits/{permitId}", Json);
        permit.GetProperty("status").GetString().Should().Be("Closed");
        permit.GetProperty("canClose").GetBoolean().Should().BeFalse();
    }

    [RequiresDatabaseFact]
    public async Task APersonsPermits_IncludeTheOnesTheyReceive_AndExcludeEverybodyElses()
    {
        var client = await api.SignInAsAdministratorAsync();
        var run = Guid.NewGuid().ToString("N")[..8];

        var (companyId, tradeId, _) = await ReferenceDataAsync(client);
        var (_, categoryId, locationId) = await PermitReferenceAsync(client);

        var receiver = await CreateEmployeeAsync(client, $"recv.{run}@example.test", companyId, tradeId, "Welder");
        var bystander = await CreateEmployeeAsync(client, $"else.{run}@example.test", companyId, tradeId, "Fitter");

        // Cold Work requires no certification, so this exercises the assignment filter
        // without dragging the certification rule into it.
        var coldWork = await ColdWorkTypeAsync(client);

        var created = await client.PostAsJsonAsync("/api/permits", new
        {
            permitTypeId = coldWork,
            categoryId,
            workDescription = "Replace the gasket on the transfer pump.",
            locationId,
            validFrom = DateTimeOffset.UtcNow.AddDays(1),
            validTo = DateTimeOffset.UtcNow.AddDays(2),
            receiverId = receiver,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var permitId = await IdOfAsync(created);

        // The Receiver was never added to the crew, and still has to see it: they are the
        // person accountable for the work actually happening.
        var theirs = await client.GetFromJsonAsync<JsonElement>(
            $"/api/permits/assigned-to/{receiver}?order=Schedule", Json);

        theirs.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should().Contain(permitId);

        var somebodyElses = await client.GetFromJsonAsync<JsonElement>(
            $"/api/permits/assigned-to/{bystander}", Json);

        somebodyElses.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should().NotContain(permitId);
    }

    [RequiresDatabaseFact]
    public async Task BadgeNumbers_AreHandedOutWithoutColliding()
    {
        var client = await api.SignInAsAdministratorAsync();
        var run = Guid.NewGuid().ToString("N")[..8];

        var (companyId, tradeId, _) = await ReferenceDataAsync(client);

        var first = await CreateEmployeeAsync(client, $"a.{run}@example.test", companyId, tradeId, "Fitter");
        var second = await CreateEmployeeAsync(client, $"b.{run}@example.test", companyId, tradeId, "Fitter");

        var one = await client.GetFromJsonAsync<JsonElement>($"/api/employees/{first}", Json);
        var two = await client.GetFromJsonAsync<JsonElement>($"/api/employees/{second}", Json);

        // Exercises the MERGE in CounterStore against real SQL Server, which is the whole
        // reason these tests do not run on an in-memory provider.
        one.GetProperty("employeeNumber").GetString()
            .Should().NotBe(two.GetProperty("employeeNumber").GetString());
    }

    [RequiresDatabaseFact]
    public async Task ADuplicateEmail_IsAConflict_NotACrash()
    {
        var client = await api.SignInAsAdministratorAsync();
        var run = Guid.NewGuid().ToString("N")[..8];
        var email = $"twice.{run}@example.test";

        var (companyId, tradeId, _) = await ReferenceDataAsync(client);
        await CreateEmployeeAsync(client, email, companyId, tradeId, "Fitter");

        var again = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Someone",
            lastName = "Else",
            email,
            companyId,
            tradeId,
            jobTitle = "Fitter",
            hireDate = "2024-01-05",
        });

        // 409 from the service, rather than a 500 from the unique index underneath it.
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #region Setting the scene

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid CompanyId, Guid TradeId, Guid CertificationTypeId)> ReferenceDataAsync(
        HttpClient client)
    {
        var companies = await client.GetFromJsonAsync<JsonElement>("/api/lookups/companies", Json);
        var trades = await client.GetFromJsonAsync<JsonElement>("/api/lookups/trades", Json);
        var certifications = await client.GetFromJsonAsync<JsonElement>("/api/lookups/certification-types", Json);

        return (
            companies[0].GetProperty("id").GetGuid(),
            trades[0].GetProperty("id").GetGuid(),
            FindByCode(certifications, "HOTWORK"));
    }

    private static async Task<(Guid PermitTypeId, Guid CategoryId, Guid LocationId)> PermitReferenceAsync(
        HttpClient client)
    {
        var types = await client.GetFromJsonAsync<JsonElement>("/api/permit-types", Json);
        var categories = await client.GetFromJsonAsync<JsonElement>("/api/categories", Json);
        var facilities = await client.GetFromJsonAsync<JsonElement>("/api/lookups/facilities", Json);

        var facilityId = facilities[0].GetProperty("id").GetGuid();
        var buildings = await client.GetFromJsonAsync<JsonElement>(
            $"/api/lookups/facilities/{facilityId}/buildings", Json);

        var buildingId = buildings[0].GetProperty("id").GetGuid();
        var locations = await client.GetFromJsonAsync<JsonElement>(
            $"/api/lookups/buildings/{buildingId}/locations", Json);

        return (
            FindByCode(types, "HW"),
            categories[0].GetProperty("id").GetGuid(),
            locations[0].GetProperty("id").GetGuid());
    }

    private static async Task<Guid> ColdWorkTypeAsync(HttpClient client) =>
        FindByCode(await client.GetFromJsonAsync<JsonElement>("/api/permit-types", Json), "CW");

    private static Guid FindByCode(JsonElement items, string code) =>
        items.EnumerateArray()
            .First(item => item.GetProperty("code").GetString() == code)
            .GetProperty("id")
            .GetGuid();

    private static async Task<Guid> CreateEmployeeAsync(
        HttpClient client,
        string email,
        Guid companyId,
        Guid tradeId,
        string jobTitle)
    {
        var response = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Test",
            lastName = email.Split('.')[0],
            email,
            companyId,
            tradeId,
            jobTitle,
            hireDate = "2021-04-12",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await IdOfAsync(response);
    }

    private static async Task GiveCertificateAsync(HttpClient client, Guid employeeId, Guid certificationTypeId)
    {
        var response = await client.PostAsJsonAsync($"/api/employees/{employeeId}/certifications", new
        {
            certificationTypeId,
            issuedBy = "Hellenic Welding Institute",
            issuedOn = "2024-01-15",
            expiresOn = "2032-01-15",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion
}
