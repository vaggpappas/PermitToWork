namespace PermitToWork.Application.Accounts;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// The outcome of a sign-in attempt.
/// <para>
/// Built through the two factory methods rather than a positional constructor so that
/// "succeeded, but there is no token" cannot be constructed. Failure carries messages that
/// are safe to show a user; anything more specific belongs in the log, not the response.
/// </para>
/// </summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(string? accessToken, DateTimeOffset? expiresAtUtc, IReadOnlyList<string> errors)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
        Errors = errors;
    }

    public string? AccessToken { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
    public IReadOnlyList<string> Errors { get; }

    public bool Succeeded => AccessToken is not null;

    public static AuthenticationResult Success(string accessToken, DateTimeOffset expiresAtUtc) =>
        new(accessToken, expiresAtUtc, []);

    public static AuthenticationResult Failure(params string[] errors) =>
        new(null, null, errors);
}

/// <summary>
/// Registration and sign-in. Implemented in Infrastructure, where ASP.NET Core Identity
/// lives — the rest of the application only ever sees these two methods.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Claims the employee record that was created for this email address by an
    /// administrator, and attaches a new login to it.
    /// </summary>
    Task<AuthenticationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
