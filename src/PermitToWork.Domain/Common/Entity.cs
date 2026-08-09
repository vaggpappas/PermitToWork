namespace PermitToWork.Domain.Common;

/// <summary>
/// Base for every persisted domain object. Identity is the <see cref="Id"/> and nothing
/// else: two employees with identical names are still two different employees, and the
/// same employee with a corrected surname is still the same employee.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// A UUIDv7 — time-ordered, so rows land at the end of the clustered index instead of
    /// fragmenting it the way random GUIDs do. Assigned by the domain at construction,
    /// never by the database, so an object is fully valid before it is ever saved.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}
