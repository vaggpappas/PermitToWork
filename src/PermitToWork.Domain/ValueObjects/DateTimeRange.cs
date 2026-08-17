using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.ValueObjects;

/// <summary>
/// A period with a start and an end, as one concept.
/// <para>
/// Two loose <c>DateTimeOffset</c> fields can be assigned in the wrong order, and every
/// method that receives them has to re-check. Here the check happens once, at construction,
/// and "ends before it starts" is a state that cannot be represented.
/// </para>
/// </summary>
public sealed record DateTimeRange
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    private DateTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    public static DateTimeRange Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainException("The end of the period must be after its start.");
        }

        return new DateTimeRange(start, end);
    }

    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Half-open: the end instant is the first moment outside the period. Stated once here
    /// so no caller has to decide whether the end is inclusive.
    /// </summary>
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    public bool HasPassed(DateTimeOffset asOf) => asOf >= End;

    public override string ToString() => $"{Start:g} – {End:g}";
}
