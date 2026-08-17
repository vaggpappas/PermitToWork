namespace PermitToWork.Infrastructure.Email;

/// <summary>
/// Where mail comes from, and where it goes.
/// <para>
/// <see cref="SmtpHost"/> is the switch. Left empty — which is the default, and what a fresh
/// clone gets — messages are written to disk as files instead of being sent. That is not a
/// stub: the files are real RFC 5322 messages that open in any mail client, so the feature
/// can be demonstrated without a mail account existing anywhere.
/// </para>
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Who the messages appear to be from.</summary>
    public string FromAddress { get; init; } = "no-reply@permittowork.local";

    public string FromName { get; init; } = "Permit To Work";

    /// <summary>
    /// Where the application lives, for the link in the invitation. Configuration rather than
    /// a constant, because the address in an email that goes to a real person cannot be
    /// "localhost" once this is deployed anywhere.
    /// </summary>
    public string ApplicationUrl { get; init; } = "http://localhost:4200";

    /// <summary>Empty means write files instead of sending.</summary>
    public string? SmtpHost { get; init; }

    public int SmtpPort { get; init; } = 587;

    public bool UseStartTls { get; init; } = true;

    public string? SmtpUser { get; init; }

    /// <summary>
    /// Never in appsettings.json. User secrets in development, an environment variable in
    /// deployment — the same rule as the JWT signing key.
    /// </summary>
    public string? SmtpPassword { get; init; }

    /// <summary>Where the file-drop sender writes. Relative paths resolve next to the binary.</summary>
    public string Outbox { get; init; } = "outbox";
}
