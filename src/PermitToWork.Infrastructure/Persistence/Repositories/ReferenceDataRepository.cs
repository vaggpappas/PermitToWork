using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;

namespace PermitToWork.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reference data reads. Everything here is inactive-filtered and ordered by name, because
/// every caller is filling a dropdown and none of them wants retired rows.
/// </summary>
internal sealed class ReferenceDataRepository(PermitToWorkDbContext context) : IReferenceDataRepository
{
    public async Task<IReadOnlyList<LookupDto>> GetCompaniesAsync(CancellationToken cancellationToken = default) =>
        await context.Companies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new LookupDto(c.Id, c.Code, c.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupDto>> GetTradesAsync(CancellationToken cancellationToken = default) =>
        await context.Trades
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new LookupDto(t.Id, t.Code, t.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupDto>> GetCertificationTypesAsync(CancellationToken cancellationToken = default) =>
        await context.CertificationTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new LookupDto(t.Id, t.Code, t.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupDto>> GetFacilitiesAsync(CancellationToken cancellationToken = default) =>
        await context.Facilities
            .AsNoTracking()
            .Where(f => f.IsActive)
            .OrderBy(f => f.Name)
            .Select(f => new LookupDto(f.Id, f.Code, f.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupDto>> GetBuildingsAsync(Guid facilityId, CancellationToken cancellationToken = default) =>
        await context.Buildings
            .AsNoTracking()
            .Where(b => b.IsActive && b.FacilityId == facilityId)
            .OrderBy(b => b.Name)
            .Select(b => new LookupDto(b.Id, b.Code, b.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupDto>> GetLocationsAsync(Guid buildingId, CancellationToken cancellationToken = default) =>
        await context.Locations
            .AsNoTracking()
            .Where(l => l.IsActive && l.BuildingId == buildingId)
            .OrderBy(l => l.Name)
            .Select(l => new LookupDto(l.Id, l.Code, l.Name))
            .ToListAsync(cancellationToken);

    public Task<bool> CompanyExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Companies.AnyAsync(c => c.Id == id && c.IsActive, cancellationToken);

    public Task<bool> TradeExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Trades.AnyAsync(t => t.Id == id && t.IsActive, cancellationToken);

    public Task<bool> CertificationTypeExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.CertificationTypes.AnyAsync(t => t.Id == id && t.IsActive, cancellationToken);

    public Task<bool> FacilityExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Facilities.AnyAsync(f => f.Id == id && f.IsActive, cancellationToken);
}
