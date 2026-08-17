using System.Text.RegularExpressions;
using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.ValueObjects;

/// <summary>
/// The number a permit is known by on site — <c>HW-2026-0001</c>.
/// <para>
/// Type code, year, sequence. The type code is first on purpose: somebody reading a number
/// over a radio should know it is hot work before they know anything else about it.
/// </para>
/// </summary>
public sealed partial record PermitNumber
{
    public string Value { get; }

    private PermitNumber(string value) => Value = value;

    public static PermitNumber Create(string? value)
    {
        var candidate = Guard.Required(value, "Permit number", 20).ToUpperInvariant();

        if (!Pattern().IsMatch(candidate))
        {
            throw new DomainException(
                $"'{candidate}' is not a valid permit number. Expected a form like HW-2026-0001.");
        }

        return new PermitNumber(candidate);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z]{2,4}-[0-9]{4}-[0-9]{4}$")]
    private static partial Regex Pattern();
}
