using FluentAssertions;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Employees;
using Xunit;

namespace PermitToWork.Tests.Application;

/// <summary>
/// The wording of the invitation.
/// <para>
/// Testing prose looks unusual, and would be pointless for most text. This message is
/// different: it is the only instruction a new hire ever gets, it arrives when nobody is
/// available to explain it, and the one thing it must not do is imply a password has been
/// issued to them. That is a claim about the design, not a phrase, and it is worth pinning.
/// </para>
/// </summary>
public class WelcomeEmailTests
{
    private static readonly ApplicationLinks Links = new("http://localhost:4200/");

    [Fact]
    public void Invitation_IsAddressedToTheNewEmployee()
    {
        var message = WelcomeEmail.For("Marta Nowak", "marta@acme.test", "Acme Maintenance Services", Links.LoginUrl);

        message.To.Should().Be("marta@acme.test");
        message.Body.Should().Contain("Marta Nowak");
    }

    [Fact]
    public void Invitation_NamesTheCompanyTheyWillBeWorkingFor()
    {
        var message = WelcomeEmail.For("Marta Nowak", "marta@acme.test", "Acme Maintenance Services", Links.LoginUrl);

        // A contractor may work for several employers on one site. "A profile was created for
        // you" without saying by whom is a message that cannot be acted on.
        message.Subject.Should().Contain("Acme Maintenance Services");
        message.Body.Should().Contain("Acme Maintenance Services");
    }

    [Fact]
    public void Invitation_SendsThemToRegister_WithAUsableLink()
    {
        var message = WelcomeEmail.For("Marta Nowak", "marta@acme.test", "Acme", Links.LoginUrl);

        // One slash, not two: BaseUrl may or may not have a trailing one, and ApplicationLinks
        // is what settles it.
        message.Body.Should().Contain("http://localhost:4200/login");
        message.Body.Should().NotContain("4200//login");
        message.Body.Should().Contain("Register");
    }

    [Fact]
    public void Invitation_NeverImpliesAPasswordWasIssued()
    {
        var message = WelcomeEmail.For("Marta Nowak", "marta@acme.test", "Acme", Links.LoginUrl);

        // The whole registration design rests on this: the employer asserts employment, the
        // person asserts identity. A message hinting at a temporary password would have people
        // ringing an administrator asking to be told what it is.
        // Asserted as two fragments rather than one sentence: the body is a raw string
        // literal, so where it wraps depends on the line endings the file was checked out
        // with, and a test that fails on a git setting is testing the wrong thing.
        message.Body.Should().Contain("pick a password of your own");
        message.Body.Should().Contain("set one for you");
    }

    [Fact]
    public void Invitation_TellsThemWhatToDoIfItWasNotExpected()
    {
        var message = WelcomeEmail.For("Marta Nowak", "marta@acme.test", "Acme", Links.LoginUrl);

        message.Body.Should().Contain("not expecting this message");
    }
}
