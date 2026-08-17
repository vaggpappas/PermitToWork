using PermitToWork.Application.Common;
using PermitToWork.Application.Permits;
using PermitToWork.Domain.Permits;

namespace PermitToWork.Application.Abstractions;

/// <summary>
/// Permit persistence. Same split as employees and teams: the aggregate for commands,
/// projections for queries.
/// <para>
/// Everything here is narrowed to what the caller may see. A contractor sees a permit when
/// one of their own people is its creator, its receiver, an approver on it, or a worker on
/// it — expressed once, in the implementation.
/// </para>
/// </summary>
public interface IPermitRepository
{
    /// <summary>Loads the aggregate with everything a transition might touch.</summary>
    Task<Permit?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PermitDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<PermitSummaryDto>> SearchAsync(
        PermitSearchRequest request,
        Guid? currentEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>The next number for a permit type, e.g. HW-2026-0001.</summary>
    Task<string> NextNumberAsync(string permitTypeCode, int year, CancellationToken cancellationToken = default);

    /// <summary>The certifications a permit type demands, with their names, for the snapshot.</summary>
    Task<IReadOnlyList<CertificationRequirement>> GetRequirementsAsync(
        Guid permitTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermitTypeDto>> GetPermitTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Which facility a location belongs to, via its building.</summary>
    Task<Guid?> GetFacilityOfLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    void Add(Permit permit);
}

/// <summary>The standing approval panels. Read when a permit is submitted; managed by administrators.</summary>
public interface IFacilityApproverRepository
{
    Task<IReadOnlyList<ApproverAssignment>> GetPanelAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacilityApproverDto>> GetPanelDetailAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default);

    Task<Domain.Organization.FacilityApprover?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> AlreadySeatedAsync(Guid facilityId, Guid employeeId, CancellationToken cancellationToken = default);

    void Add(Domain.Organization.FacilityApprover approver);

    void Remove(Domain.Organization.FacilityApprover approver);
}
