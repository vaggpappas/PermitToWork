using Microsoft.Data.SqlClient;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// Whether SQL Server is running at all.
/// <para>
/// One probe, one cached answer, asked by everything that would otherwise fail slowly and
/// confusingly without it: the skip attribute on each test, and the fixture that would
/// otherwise migrate and drop a database that is not there.
/// </para>
/// <para>
/// Shared deliberately. When the attribute knew this and the fixture did not, every test
/// skipped correctly and the run still exited non-zero, because the fixture's cleanup threw
/// on a connection nothing was ever going to make. "All tests skipped" and "the build
/// failed" are not the same message.
/// </para>
/// </summary>
internal static class SqlServer
{
    private static readonly Lazy<bool> Reachable = new(Probe, isThreadSafe: true);

    /// <summary>The message shown on every skipped test, with the command that fixes it.</summary>
    public const string NotRunning = "SQL Server is not reachable. Start it with: docker compose up -d sqlserver";

    public static bool IsRunning => Reachable.Value;

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
