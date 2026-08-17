namespace PermitToWork.Application.Abstractions;

/// <summary>
/// What the caller was doing when something changed — the HTTP verb, the path, the address
/// it came from.
/// <para>
/// Separate from <see cref="ICurrentUser"/> because it answers a different question: that
/// one is <em>who</em>, this one is <em>through what</em>. When an audit line says a
/// document was deleted, the path tells you whether it came from the permit screen or
/// somebody poking the API directly.
/// </para>
/// <para>
/// Every member is nullable. Background work has no request, and an audit line written by
/// the expiry sweep should say so rather than invent one.
/// </para>
/// </summary>
public interface IRequestContext
{
    string? Method { get; }

    string? Path { get; }

    string? IpAddress { get; }
}
