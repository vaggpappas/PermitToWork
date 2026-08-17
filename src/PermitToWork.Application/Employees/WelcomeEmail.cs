using PermitToWork.Application.Abstractions;

namespace PermitToWork.Application.Employees;

/// <summary>
/// The message a new hire receives when their record is created.
/// <para>
/// A plain function on its own, not a method on the service, because composing the text and
/// deciding when to send it are different jobs — and this way the wording can be asserted in
/// a test without a repository, a mail server or a database anywhere near it.
/// </para>
/// </summary>
public static class WelcomeEmail
{
    public static EmailMessage For(string fullName, string email, string companyName, string loginUrl)
    {
        var body =
            $"""
             Hello {fullName},

             An employee profile has been created for you at {companyName}.

             It is not active yet. To finish setting it up, register a login against this
             email address — the one this message was sent to:

                 {loginUrl}

             Choose "Register", enter {email}, and pick a password of your own. Nobody has
             set one for you, and nobody here can see it.

             Once you have registered you will be able to see the teams you belong to, the
             certifications recorded against your name and the permits you are working under.

             If you were not expecting this message, please tell your site administrator.

             --
             Permit To Work
             This mailbox is not monitored.
             """;

        return new EmailMessage(email, $"Your {companyName} employee profile is ready", body);
    }
}
