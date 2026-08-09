namespace PermitToWork.Domain.Common;

/// <summary>
/// The handful of checks that every value object and entity constructor repeats.
/// Internal on purpose: guarding is an implementation detail of the domain, not part of
/// the vocabulary it exposes to the rest of the application.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Trims, rejects null/blank/over-long, and returns the cleaned value — so callers
    /// assign the result rather than validating and assigning as two separate steps that
    /// can drift apart.
    /// </summary>
    internal static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException($"{field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    /// <summary>
    /// As <see cref="Required(string?, string, int)"/>, but blank collapses to null
    /// instead of throwing.
    /// </summary>
    internal static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    internal static Guid Required(Guid value, string field) =>
        value == Guid.Empty ? throw new DomainException($"{field} is required.") : value;
}
