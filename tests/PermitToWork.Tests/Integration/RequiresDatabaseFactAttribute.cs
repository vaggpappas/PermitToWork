using Microsoft.Data.SqlClient;
using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when SQL Server is not
/// running.
/// <para>
/// These tests need a real database — the counter table is driven by T-SQL that no
/// in-memory provider implements. But somebody who has just cloned the repository and run
/// <c>dotnet test</c> should get a clear "start the container" message, not a screen of
/// connection failures that looks like the code is broken.
/// </para>
/// <para>
/// The probe runs once per test session and the answer is cached, so this costs one
/// connection attempt rather than one per test.
/// </para>
/// </summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DatabaseIsReachable = new(Probe, isThreadSafe: true);

    public RequiresDatabaseFactAttribute()
    {
        if (!DatabaseIsReachable.Value)
        {
            Skip = "SQL Server is not reachable. Start it with: docker compose up -d sqlserver";
        }
    }

    private static bool Probe()
    {
        try
        {
            using var connection = new SqlConnection(ApiFactory.ProbeConnectionString);
            connection.Open();

            return true;
        }
        catch
        {
            return false;
        }
    }
}
