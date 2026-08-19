using Xunit;

namespace PermitToWork.Tests.Integration;

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when SQL Server is not
/// running.
/// <para>
/// These tests need a real database — the counter table is driven by T-SQL that no in-memory
/// provider implements. But somebody who has just cloned the repository and run
/// <c>dotnet test</c> should get a clear "start the container" message, not a screen of
/// connection failures that looks like the code is broken.
/// </para>
/// <para>
/// The probe itself lives in <see cref="SqlServer"/>, because <see cref="ApiFactory"/> has to
/// ask the same question before it tries to build a database.
/// </para>
/// </summary>
public sealed class RequiresDatabaseFactAttribute : FactAttribute
{
    public RequiresDatabaseFactAttribute()
    {
        if (!SqlServer.IsRunning)
        {
            Skip = SqlServer.NotRunning;
        }
    }
}
