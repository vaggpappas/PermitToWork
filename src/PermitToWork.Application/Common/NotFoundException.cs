namespace PermitToWork.Application.Common;

/// <summary>
/// The thing you asked for is not there — or is not there <em>for you</em>, because the
/// company scope filtered it out. Those two cases are deliberately indistinguishable: a
/// contractor who could tell "does not exist" from "exists but is not yours" could map
/// the rest of the site one id at a time.
/// </summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}

/// <summary>
/// The request is well-formed and permitted, but conflicts with the current state — a
/// duplicate employee number, an email already registered. Distinct from
/// <see cref="Domain.Common.DomainException"/>, which means a business rule was broken.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
