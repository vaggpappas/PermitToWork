namespace PermitToWork.Application.Abstractions;

/// <summary>
/// How much of the database the caller is allowed to see.
/// <para>
/// A closed hierarchy — the private constructor means only the three nested cases can
/// exist. This is the point: a nullable <c>Guid? CompanyId</c> would have been shorter,
/// but "null" would have to mean either "sees everything" or "sees nothing", and whichever
/// one it meant, the other would be one forgotten null-check away. Here the three
/// possibilities are named and the compiler makes you handle them.
/// </para>
/// </summary>
public abstract record DataScope
{
    private DataScope() { }

    /// <summary>Site-wide access. Administrators and safety officers.</summary>
    public sealed record All : DataScope;

    /// <summary>Restricted to one company's data. Contractors.</summary>
    public sealed record SingleCompany(Guid CompanyId) : DataScope;

    /// <summary>
    /// No access. The default for anonymous callers and for anyone whose token is missing
    /// the claims needed to work out a scope — so a broken or truncated token fails
    /// closed and shows nothing, rather than failing open and showing everything.
    /// </summary>
    public sealed record Nothing : DataScope;
}

/// <summary>
/// Who is making the current request. Implemented in the API layer over the HTTP context;
/// declared here so the Application and Infrastructure layers can depend on the question
/// without depending on ASP.NET Core to answer it.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    /// <summary>The employee record this login is attached to, if any.</summary>
    Guid? EmployeeId { get; }

    DataScope Scope { get; }
}
