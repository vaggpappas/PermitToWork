using Microsoft.EntityFrameworkCore;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.ReferenceData;
using PermitToWork.Domain.Permits;

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

    public async Task<IReadOnlyList<ReferenceItemDto>> ListForAdminAsync(
        ReferenceKind kind,
        Guid? parentId = null,
        CancellationToken cancellationToken = default) =>
        kind switch
        {
            ReferenceKind.Company => await context.Companies
                .AsNoTracking()
                .OrderBy(c => c.Name)
                // Extra carries whatever is worth showing but does not fit the shared shape —
                // a company's kind, a permit type's requirements. One column beats eight DTOs.
                .Select(c => new ReferenceItemDto(c.Id, c.Code, c.Name, null, c.IsActive, null, c.Kind.ToString()))
                .ToListAsync(cancellationToken),

            ReferenceKind.Facility => await context.Facilities
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .Select(f => new ReferenceItemDto(f.Id, f.Code, f.Name, f.Description, f.IsActive, null, null))
                .ToListAsync(cancellationToken),

            ReferenceKind.Building => await context.Buildings
                .AsNoTracking()
                .Where(b => parentId == null || b.FacilityId == parentId)
                .OrderBy(b => b.Name)
                .Select(b => new ReferenceItemDto(
                    b.Id, b.Code, b.Name, b.Description, b.IsActive, b.FacilityId, null))
                .ToListAsync(cancellationToken),

            ReferenceKind.Location => await context.Locations
                .AsNoTracking()
                .Where(l => parentId == null || l.BuildingId == parentId)
                .OrderBy(l => l.Name)
                .Select(l => new ReferenceItemDto(
                    l.Id, l.Code, l.Name, l.Description, l.IsActive, l.BuildingId, null))
                .ToListAsync(cancellationToken),

            ReferenceKind.Trade => await context.Trades
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new ReferenceItemDto(t.Id, t.Code, t.Name, null, t.IsActive, null, null))
                .ToListAsync(cancellationToken),

            ReferenceKind.CertificationType => await context.CertificationTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new ReferenceItemDto(t.Id, t.Code, t.Name, null, t.IsActive, null, null))
                .ToListAsync(cancellationToken),

            ReferenceKind.Category => await context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new ReferenceItemDto(c.Id, c.Code, c.Name, null, c.IsActive, null, null))
                .ToListAsync(cancellationToken),

            ReferenceKind.PermitType => await context.PermitTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new ReferenceItemDto(
                    t.Id,
                    t.Code,
                    t.Name,
                    t.Description,
                    t.IsActive,
                    null,
                    string.Join(", ",
                        from requirement in context.Set<PermitTypeCertification>()
                        join type in context.CertificationTypes on requirement.CertificationTypeId equals type.Id
                        where requirement.PermitTypeId == t.Id
                        select type.Name)))
                .ToListAsync(cancellationToken),

            _ => []
        };
}
