using PermitToWork.Application.Abstractions;

namespace PermitToWork.Api.Authentication;

/// <summary>
/// Reads the current request's verb, path and origin address for the audit log.
/// <para>
/// Everything is null when there is no request — the expiry sweep, the seeder on startup.
/// That is the point: a background job did not come through a URL, and the log should say
/// nothing rather than something plausible and wrong.
/// </para>
/// </summary>
public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? Method => accessor.HttpContext?.Request.Method;

    public string? Path => accessor.HttpContext?.Request.Path.Value;

    /// <summary>
    /// The remote address as the server sees it. Behind a reverse proxy this is the proxy
    /// unless forwarded headers are configured — worth knowing before trusting it in an
    /// investigation.
    /// </summary>
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
