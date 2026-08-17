namespace PermitToWork.Application.Abstractions;

/// <summary>
/// Writes to the reference tables — companies, the facility hierarchy, trades,
/// certification types, categories, permit types.
/// <para>
/// Generic on purpose, and this is one of the few places that is justified. Every one of
/// these operations is "an entity by id" or "does this code already exist", with no rule
/// that differs per table. Written out per type it would be two dozen methods that all say
/// the same thing, and the next lookup table would need three more.
/// </para>
/// <para>
/// The rules that <em>are</em> different — a building belongs to a facility, a permit type
/// demands certifications — stay on the entities, where they can be enforced.
/// </para>
/// </summary>
public interface IReferenceDataWriter
{
    Task<TEntity?> FindAsync<TEntity>(Guid id, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Whether a code is already used by a row of this type, optionally ignoring one.
    /// <para>
    /// Codes are the human-facing identifier and are unique per table at the database
    /// level. Asking first turns a constraint violation into a 409 with a sentence.
    /// </para>
    /// </summary>
    Task<bool> CodeIsTakenAsync<TEntity>(
        string code,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
        where TEntity : class;

    void Add<TEntity>(TEntity entity) where TEntity : class;
}
