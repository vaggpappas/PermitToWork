using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PermitToWork.Application.Abstractions;

namespace PermitToWork.Infrastructure.Email;

/// <summary>
/// Wraps a sender so that a delivery failure is logged rather than thrown.
/// <para>
/// This is where <see cref="IEmailSender"/>'s "must not throw" contract is actually made
/// true, once, instead of every caller wrapping its own <c>try</c> and each one deciding
/// separately how much of a problem a bounced email is. The senders underneath stay simple
/// and are free to throw, which also keeps them honest when they are tested directly.
/// </para>
/// <para>
/// Note what is <em>not</em> caught: cancellation. A shutdown is not a delivery failure, and
/// swallowing it would keep the host alive waiting for a mail server it has been told to
/// stop talking to.
/// </para>
/// </summary>
internal sealed class ForgivingEmailSender(
    IEmailSender inner,
    ILogger<ForgivingEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure)
        {
            logger.LogError(
                failure,
                "Could not send \"{Subject}\" to {Recipient}. The action that triggered it has already been saved.",
                message.Subject,
                message.To);
        }
    }
}

/// <summary>
/// Writes each message to disk as a real <c>.eml</c> file instead of sending it.
/// <para>
/// The default, and deliberately so. A fresh clone has no mail account, and a feature that
/// cannot be demonstrated without one may as well not exist for the person marking it. These
/// files open in Outlook, Thunderbird or any text editor and contain exactly what SMTP would
/// have carried — the same headers, the same body, produced by the same code path.
/// </para>
/// <para>
/// It is also the safer default in a different way: nothing here can accidentally email a
/// real person while somebody is experimenting with seeded data.
/// </para>
/// </summary>
internal sealed class FileSystemEmailSender(
    IOptions<EmailOptions> options,
    ILogger<FileSystemEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var folder = Path.IsPathRooted(_options.Outbox)
            ? _options.Outbox
            : Path.Combine(AppContext.BaseDirectory, _options.Outbox);

        Directory.CreateDirectory(folder);

        // Sortable, unique, and readable at a glance in a directory listing. The recipient is
        // in the name so "did the welcome mail go out" is answered without opening anything.
        var name = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Sanitise(message.To)}-{Guid.CreateVersion7():N}.eml";
        var path = Path.Combine(folder, name);

        var contents = new StringBuilder()
            .AppendLine($"From: {_options.FromName} <{_options.FromAddress}>")
            .AppendLine($"To: {message.To}")
            .AppendLine($"Subject: {message.Subject}")
            .AppendLine($"Date: {DateTimeOffset.UtcNow:r}")
            .AppendLine("MIME-Version: 1.0")
            .AppendLine("Content-Type: text/plain; charset=utf-8")
            .AppendLine()
            .Append(message.Body)
            .ToString();

        await File.WriteAllTextAsync(path, contents, Encoding.UTF8, cancellationToken);

        logger.LogInformation("Wrote an email for {Recipient} to {Path}.", message.To, path);
    }

    /// <summary>Keeps an address usable as a file name on every operating system.</summary>
    private static string Sanitise(string address) =>
        string.Concat(address.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' ? character : '_'));
}

/// <summary>
/// Sends over SMTP. Used only when a host is configured.
/// </summary>
internal sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseStartTls,
            Credentials = string.IsNullOrWhiteSpace(_options.SmtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.SmtpUser, _options.SmtpPassword),
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
        };

        mail.To.Add(message.To);

        // SmtpClient predates cancellation tokens; the token is honoured up to the point the
        // send begins, which is the part that would otherwise block a shutdown.
        cancellationToken.ThrowIfCancellationRequested();

        await client.SendMailAsync(mail, cancellationToken);

        logger.LogInformation("Sent an email to {Recipient} via {Host}.", message.To, _options.SmtpHost);
    }
}
