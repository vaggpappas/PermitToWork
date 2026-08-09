using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.ValueObjects;

/// <summary>
/// A person's name as one concept. Two loose strings invite the classic bug where
/// arguments are passed in the wrong order and nothing complains.
/// </summary>
public sealed record PersonName
{
    private const int MaxPartLength = 80;

    public string First { get; }
    public string Last { get; }

    private PersonName(string first, string last)
    {
        First = first;
        Last = last;
    }

    public static PersonName Create(string? first, string? last) => new(
        Guard.Required(first, "First name", MaxPartLength),
        Guard.Required(last, "Last name", MaxPartLength));

    /// <summary>Display form. Derived, never stored.</summary>
    public string Full => $"{First} {Last}";

    public override string ToString() => Full;
}
