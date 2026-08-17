namespace PermitToWork.Application.Abstractions;

/// <summary>
/// Where this deployment lives, for links inside messages sent to people.
/// <para>
/// A value rather than an interface, because there is nothing to substitute: it is one
/// string, and a test that wants a different one passes a different string. Injected rather
/// than read from configuration here, because the Application layer does not know what
/// configuration is — that is Infrastructure's job, and this is the shape it hands over.
/// </para>
/// <para>
/// It exists at all because "http://localhost:4200" in an email that reaches a real person
/// is a link to their own laptop.
/// </para>
/// </summary>
public sealed record ApplicationLinks(string BaseUrl)
{
    public string LoginUrl => $"{BaseUrl.TrimEnd('/')}/login";
}
