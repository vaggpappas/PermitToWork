using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;

namespace PermitToWork.Infrastructure.Persistence.Repositories;

internal sealed class ReferenceDataWriter(PermitToWorkDbContext context) : IReferenceDataWriter
{
    public async Task<TEntity?> FindAsync<TEntity>(Guid id, CancellationToken cancellationToken = default)
        where TEntity : class =>
        await context.Set<TEntity>().FindAsync([id], cancellationToken);

    public Task<bool> CodeIsTakenAsync<TEntity>(
        string code,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var normalised = code.Trim().ToUpperInvariant();

        // EF.Property reaches Code and Id without the entities having to implement an
        // interface for the benefit of persistence. Every reference table has both columns;
        // if one ever does not, this fails loudly at that call rather than silently.
        return context.Set<TEntity>().AnyAsync(
            entity => EF.Property<string>(entity, "Code") == normalised
                      && (exceptId == null || EF.Property<Guid>(entity, "Id") != exceptId),
            cancellationToken);
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => context.Set<TEntity>().Add(entity);
}
