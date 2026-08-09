using System.Text.RegularExpressions;
using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.ValueObjects;

/// <summary>
/// The badge number an employee is known by on site — the identifier humans actually use,
/// as opposed to the database <see cref="Entity.Id"/>.
/// <para>
/// It is a type rather than a <c>string</c> so that a method taking an
/// <c>EmployeeNumber</c> cannot be handed a job title by mistake, and so that the
/// "what counts as a valid number" rule lives in exactly one place.
/// </para>
/// </summary>
public sealed partial record EmployeeNumber
{
    private const int MinLength = 3;
    private const int MaxLength = 20;

    public string Value { get; }

    private EmployeeNumber(string value) => Value = value;

    /// <summary>
    /// Normalises to upper case and validates. Deliberately permissive about the shape
    /// (<c>EMP-00142</c>, <c>ACME-991</c>, <c>W12345</c> all pass) because contractors
    /// bring their own numbering schemes and a strict pattern would reject real data.
    /// </summary>
    public static EmployeeNumber Create(string? value)
    {
        var candidate = Guard.Required(value, "Employee number", MaxLength).ToUpperInvariant();

        if (candidate.Length < MinLength)
        {
            throw new DomainException($"Employee number must be at least {MinLength} characters.");
        }

        if (!AllowedCharacters().IsMatch(candidate))
        {
            throw new DomainException("Employee number may contain only letters, digits and hyphens.");
        }

        return new EmployeeNumber(candidate);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex AllowedCharacters();
}
