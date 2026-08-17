namespace PermitToWork.Application.Abstractions;

/// <summary>
/// One message, already written. Whoever sends it decides how — SMTP, a file on disk, or
/// nothing at all in a test — and none of that is visible from the Application layer.
/// </summary>
/// <param name="To">The recipient's address.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Body">Plain text. No HTML: this application sends notifications, not marketing.</param>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>
/// Sends email.
/// <para>
/// The interface lives here and every implementation lives in Infrastructure, which is what
/// lets <see cref="Employees.EmployeeService"/> announce a new hire without knowing whether
/// there is a mail server in this deployment at all.
/// </para>
/// <para>
/// Implementations must not throw for a delivery failure. The caller has usually just
/// committed something more important than the email, and cannot undo it — see the note in
/// <c>EmployeeService.CreateAsync</c>.
/// </para>
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
