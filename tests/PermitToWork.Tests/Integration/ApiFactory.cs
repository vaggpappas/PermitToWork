using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermitToWork.Infrastructure.Persistence;
using PermitToWork.Infrastructure.Persistence.Seed;
using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// Boots the real API in memory, against a real SQL Server database of its own.
/// <para>
/// Not an in-memory provider, and not by preference. <c>CounterStore</c> issues a T-SQL
/// <c>MERGE</c> to hand out badge and permit numbers, so anything that creates an employee,
/// a team or a permit simply cannot run on EF InMemory or SQLite. Substituting the database
/// would mean these tests exercise a different application from the one that ships — which
/// is the failure mode integration tests exist to avoid.
/// </para>
/// <para>
/// Everything above the database is genuine: routing, model binding, the authentication
/// handler, the authorisation filters, the exception handler, the query filters and the
/// audit interceptor. These are the parts the unit tests deliberately do not touch.
/// </para>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Not "Server", which would hide WebApplicationFactory.Server, and not "SqlServer",
    // which would shadow the SqlServer class this file now calls. A constant that has to
    // dodge two names in its own scope is worth naming for what it actually holds.
    private const string Instance = "Server=localhost,1433;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True;Encrypt=False";

    /// <summary>Its own database, dropped and rebuilt per run, so it can never disturb development data.</summary>
    public const string ConnectionString = $"{Instance};Database=PermitToWork_IntegrationTests";

    /// <summary>Used only to ask whether SQL Server is running at all.</summary>
    public const string ProbeConnectionString = $"{Instance};Database=master;Connect Timeout=3";

    public const string AdministratorEmail = "admin@permittowork.local";
    public const string AdministratorPassword = "Admin!23456";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Settings go in as environment variables, and that is not a shortcut.
    /// <para>
    /// <c>Program.cs</c> reads <c>builder.Configuration</c> while it registers services —
    /// <c>AddInfrastructure</c> and <c>AddJwtAuthentication</c> both need their values before
    /// <c>builder.Build()</c> is ever called. <c>ConfigureAppConfiguration</c> cannot help
    /// there: <c>WebApplicationFactory</c> defers those callbacks until build time, by which
    /// point the signing key has already been looked for and not found.
    /// </para>
    /// <para>
    /// Environment variables are read by <c>WebApplication.CreateBuilder</c> itself, so they
    /// are in place before the first line of <c>Program</c> runs. The double underscore is
    /// how .NET spells the <c>:</c> section separator in an environment variable name.
    /// </para>
    /// </summary>
    static ApiFactory()
    {
        // Not Development: that would run the startup seeder before the database exists and
        // log a failure for something InitializeAsync does properly a moment later.
        Set("ASPNETCORE_ENVIRONMENT", "Testing");

        Set("ConnectionStrings__PermitToWorkDb", ConnectionString);
        Set("Jwt__SigningKey", "integration-tests-signing-key-not-a-secret-0123456789abcdef");
        Set("Seed__AdministratorEmail", AdministratorEmail);
        Set("Seed__AdministratorPassword", AdministratorPassword);

        // The expiry sweep still runs once at startup, which is harmless. This only stops it
        // firing again in the middle of a test run.
        Set("PermitExpiry__IntervalMinutes", "600");

        static void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);
    }

    public async Task InitializeAsync()
    {
        // Nothing to prepare if every test in the collection is going to skip. Touching
        // Services here would build the host, start the expiry worker and have it fail
        // against a database nobody is going to reach — twenty seconds of alarming log
        // output in front of a result that is simply "SQL Server is not running".
        if (!SqlServer.IsRunning)
        {
            return;
        }

        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PermitToWorkDbContext>();

        // Migrate rather than EnsureCreated: this then also proves the migrations produce a
        // schema the application can actually run against, which EnsureCreated would not.
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Same guard, and this is the one that mattered: a throw in collection cleanup is an
        // error on the run even when no test failed, so without it `dotnet test` reported
        // "failed: 0" and still exited non-zero.
        if (SqlServer.IsRunning)
        {
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<PermitToWorkDbContext>();

            await context.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>A client carrying a real bearer token, obtained the way a browser obtains one.</summary>
    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    public Task<HttpClient> SignInAsAdministratorAsync() =>
        SignInAsync(AdministratorEmail, AdministratorPassword);

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
}

/// <summary>
/// One API host shared by every integration test class. Building it — and rebuilding the
/// database — for each class would turn a two second suite into a two minute one.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
