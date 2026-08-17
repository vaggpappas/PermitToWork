using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;

namespace PermitToWork.Application.ReferenceData;

public interface IReferenceDataService
{
    Task<Guid> CreateCompanyAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateFacilityAsync(CreatePlaceRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateBuildingAsync(Guid facilityId, CreatePlaceRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateLocationAsync(Guid buildingId, CreatePlaceRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateTradeAsync(CreateLookupRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateCertificationTypeAsync(CreateLookupRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreateCategoryAsync(CreateLookupRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreatePermitTypeAsync(CreatePermitTypeRequest request, CancellationToken cancellationToken = default);

    Task RenameCompanyAsync(Guid id, RenameLookupRequest request, CancellationToken cancellationToken = default);

    Task UpdateFacilityAsync(Guid id, UpdatePlaceRequest request, CancellationToken cancellationToken = default);

    Task UpdateBuildingAsync(Guid id, UpdatePlaceRequest request, CancellationToken cancellationToken = default);

    Task UpdateLocationAsync(Guid id, UpdatePlaceRequest request, CancellationToken cancellationToken = default);

    Task RenameTradeAsync(Guid id, RenameLookupRequest request, CancellationToken cancellationToken = default);

    Task RenameCertificationTypeAsync(Guid id, RenameLookupRequest request, CancellationToken cancellationToken = default);

    Task RenameCategoryAsync(Guid id, RenameLookupRequest request, CancellationToken cancellationToken = default);

    Task UpdatePermitTypeAsync(Guid id, UpdatePermitTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retires or restores a reference row. Nothing is ever deleted — permits, badges and
    /// team codes point at these, and a deleted trade would leave employees referring to
    /// something that no longer exists.
    /// </summary>
    Task SetActiveAsync(ReferenceKind kind, Guid id, bool isActive, CancellationToken cancellationToken = default);
}

/// <summary>Which reference table an activation request is about.</summary>
public enum ReferenceKind
{
    Company = 1,
    Facility = 2,
    Building = 3,
    Location = 4,
    Trade = 5,
    CertificationType = 6,
    Category = 7,
    PermitType = 8
}

/// <summary>
/// Administration of the reference tables.
/// <para>
/// Every create does the same two things — refuse a duplicate code, then build the entity
/// and let its constructor validate the rest. The variation between tables lives in the
/// entities, which is why this class is long but not complicated.
/// </para>
/// </summary>
public sealed class ReferenceDataService(
    IReferenceDataWriter writer,
    IReferenceDataRepository referenceData,
    IUnitOfWork unitOfWork) : IReferenceDataService
{
    #region Creating

    public async Task<Guid> CreateCompanyAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireCodeIsFreeAsync<Company>(request.Code, cancellationToken: cancellationToken);

        var company = new Company(request.Code, request.Name, request.Kind);
        writer.Add(company);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return company.Id;
    }

    public async Task<Guid> CreateFacilityAsync(
        CreatePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireCodeIsFreeAsync<Facility>(request.Code, cancellationToken: cancellationToken);

        var facility = new Facility(request.Code, request.Name, request.Description);
        writer.Add(facility);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return facility.Id;
    }

    public async Task<Guid> CreateBuildingAsync(
        Guid facilityId,
        CreatePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireAsync<Facility>(facilityId, cancellationToken);

        // Codes repeat across sites — "UNIT3" at two refineries is two different places —
        // so this check is not global. The unique index is on (FacilityId, Code).
        var buildings = await referenceData.GetBuildingsAsync(facilityId, cancellationToken);
        if (buildings.Any(b => string.Equals(b.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"This facility already has a building coded '{request.Code}'.");
        }

        var building = new Building(facilityId, request.Code, request.Name, request.Description);
        writer.Add(building);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return building.Id;
    }

    public async Task<Guid> CreateLocationAsync(
        Guid buildingId,
        CreatePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await RequireAsync<Building>(buildingId, cancellationToken);

        var locations = await referenceData.GetLocationsAsync(buildingId, cancellationToken);
        if (locations.Any(l => string.Equals(l.Code, request.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"This building already has a location coded '{request.Code}'.");
        }

        var location = new Location(buildingId, request.Code, request.Name, request.Description);
        writer.Add(location);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return location.Id;
    }

    public Task<Guid> CreateTradeAsync(CreateLookupRequest request, CancellationToken cancellationToken = default) =>
        CreateSimpleAsync(request, code => new Trade(code, request.Name), cancellationToken);

    public Task<Guid> CreateCertificationTypeAsync(
        CreateLookupRequest request,
        CancellationToken cancellationToken = default) =>
        CreateSimpleAsync(request, code => new CertificationType(code, request.Name), cancellationToken);

    public Task<Guid> CreateCategoryAsync(CreateLookupRequest request, CancellationToken cancellationToken = default) =>
        CreateSimpleAsync(request, code => new Category(code, request.Name), cancellationToken);

    public async Task<Guid> CreatePermitTypeAsync(
        CreatePermitTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireCodeIsFreeAsync<PermitType>(request.Code, cancellationToken: cancellationToken);

        var permitType = new PermitType(request.Code, request.Name, request.Description);
        await ApplyRequirementsAsync(permitType, request.RequiredCertificationTypeIds, cancellationToken);

        writer.Add(permitType);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return permitType.Id;
    }

    #endregion

    #region Editing

    public async Task RenameCompanyAsync(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = await RequireAsync<Company>(id, cancellationToken);
        company.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateFacilityAsync(
        Guid id,
        UpdatePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var facility = await RequireAsync<Facility>(id, cancellationToken);
        facility.Rename(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBuildingAsync(
        Guid id,
        UpdatePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var building = await RequireAsync<Building>(id, cancellationToken);
        building.Rename(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLocationAsync(
        Guid id,
        UpdatePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var location = await RequireAsync<Location>(id, cancellationToken);
        location.Rename(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameTradeAsync(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var trade = await RequireAsync<Trade>(id, cancellationToken);
        trade.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameCertificationTypeAsync(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var certificationType = await RequireAsync<CertificationType>(id, cancellationToken);
        certificationType.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameCategoryAsync(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await RequireAsync<Category>(id, cancellationToken);
        category.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePermitTypeAsync(
        Guid id,
        UpdatePermitTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var permitType = await RequireAsync<PermitType>(id, cancellationToken);

        permitType.Rename(request.Name, request.Description);
        await ApplyRequirementsAsync(permitType, request.RequiredCertificationTypeIds, cancellationToken);

        // Permits already raised keep the requirements they were raised under — those were
        // copied onto the permit at the time. This only changes what future permits demand.
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(
        ReferenceKind kind,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        switch (kind)
        {
            case ReferenceKind.Company:
                Toggle(await RequireAsync<Company>(id, cancellationToken), c => c.Reactivate(), c => c.Deactivate());
                break;
            case ReferenceKind.Facility:
                Toggle(await RequireAsync<Facility>(id, cancellationToken), f => f.Reactivate(), f => f.Deactivate());
                break;
            case ReferenceKind.Building:
                Toggle(await RequireAsync<Building>(id, cancellationToken), b => b.Reactivate(), b => b.Deactivate());
                break;
            case ReferenceKind.Location:
                Toggle(await RequireAsync<Location>(id, cancellationToken), l => l.Reactivate(), l => l.Deactivate());
                break;
            case ReferenceKind.Trade:
                Toggle(await RequireAsync<Trade>(id, cancellationToken), t => t.Reactivate(), t => t.Deactivate());
                break;
            case ReferenceKind.CertificationType:
                Toggle(await RequireAsync<CertificationType>(id, cancellationToken), t => t.Reactivate(), t => t.Deactivate());
                break;
            case ReferenceKind.Category:
                Toggle(await RequireAsync<Category>(id, cancellationToken), c => c.Reactivate(), c => c.Deactivate());
                break;
            case ReferenceKind.PermitType:
                Toggle(await RequireAsync<PermitType>(id, cancellationToken), t => t.Reactivate(), t => t.Deactivate());
                break;
            default:
                throw new NotFoundException(nameof(ReferenceKind), kind);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        void Toggle<T>(T entity, Action<T> reactivate, Action<T> deactivate)
        {
            if (isActive)
            {
                reactivate(entity);
            }
            else
            {
                deactivate(entity);
            }
        }
    }

    #endregion

    #region Helpers

    private async Task<Guid> CreateSimpleAsync<TEntity>(
        CreateLookupRequest request,
        Func<string, TEntity> build,
        CancellationToken cancellationToken)
        where TEntity : Domain.Common.Entity
    {
        await RequireCodeIsFreeAsync<TEntity>(request.Code, cancellationToken: cancellationToken);

        var entity = build(request.Code);
        writer.Add(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    private async Task ApplyRequirementsAsync(
        PermitType permitType,
        IReadOnlyList<Guid> certificationTypeIds,
        CancellationToken cancellationToken)
    {
        var known = await referenceData.GetCertificationTypesAsync(cancellationToken);

        foreach (var certificationTypeId in certificationTypeIds.Distinct())
        {
            if (known.All(k => k.Id != certificationTypeId))
            {
                throw new NotFoundException(nameof(CertificationType), certificationTypeId);
            }

            permitType.RequireCertification(certificationTypeId);
        }

        // Anything no longer listed is dropped. The aggregate owns the collection, so this
        // stays a matter of asking it twice rather than editing a list from outside.
        foreach (var existing in permitType.RequiredCertifications.ToList())
        {
            if (!certificationTypeIds.Contains(existing.CertificationTypeId))
            {
                permitType.StopRequiringCertification(existing.CertificationTypeId);
            }
        }
    }

    private async Task RequireCodeIsFreeAsync<TEntity>(
        string code,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (await writer.CodeIsTakenAsync<TEntity>(code, exceptId, cancellationToken))
        {
            throw new ConflictException($"The code '{code.Trim().ToUpperInvariant()}' is already in use.");
        }
    }

    private async Task<TEntity> RequireAsync<TEntity>(Guid id, CancellationToken cancellationToken)
        where TEntity : class =>
        await writer.FindAsync<TEntity>(id, cancellationToken)
        ?? throw new NotFoundException(typeof(TEntity).Name, id);

    #endregion
}
