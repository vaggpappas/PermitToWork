using PermitToWork.Application.Abstractions;

namespace PermitToWork.Application.Permits;

public interface IPermitExpiryService
{
    /// <summary>Expires every permit whose window has closed. Returns how many changed.</summary>
    Task<int> ExpireElapsedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Marks permits whose validity has run out.
/// <para>
/// Without this, a permit nobody remembered to close stays Active for ever, and the
/// register says work is going on in a place where it finished last March. That is exactly
/// what a safety audit goes looking for, and it is the reason the Expired state exists at
/// all — a state nothing can reach is just a comment.
/// </para>
/// <para>
/// The decision itself stays in the aggregate: this class finds candidates and asks each
/// one, and <c>ExpireIfElapsed</c> answers. It returns whether it changed anything, so a
/// permit that has since been closed is skipped rather than quietly rewritten.
/// </para>
/// </summary>
public sealed class PermitExpiryService(IPermitRepository permits, IUnitOfWork unitOfWork) : IPermitExpiryService
{
    public async Task<int> ExpireElapsedAsync(CancellationToken cancellationToken = default)
    {
        var asOf = DateTimeOffset.UtcNow;
        var candidates = await permits.FindElapsedAsync(asOf, cancellationToken);

        var expired = candidates.Count(permit => permit.ExpireIfElapsed(asOf));

        if (expired > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return expired;
    }
}
