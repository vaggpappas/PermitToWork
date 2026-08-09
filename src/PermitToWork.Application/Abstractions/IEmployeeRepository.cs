using PermitToWork.Application.Common;
using PermitToWork.Application.Employees;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Application.Abstractions;

/// <summary>
/// Employee persistence, split by intent.
/// <para>
/// The <c>Get…</c> methods that return <see cref="Employee"/> hand back the real aggregate
/// for a command to change. The methods returning DTOs project in the database and are
/// read-only. Keeping the two apart stops the usual drift where a query slowly acquires
/// <c>Include</c>s until every list page loads the whole object graph.
/// </para>
/// </summary>
public interface IEmployeeRepository
{
    /// <summary>Loads the aggregate, with its certifications, for modification.</summary>
    Task<Employee?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmployeeDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<EmployeeSummaryDto>> SearchAsync(
        EmployeeSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> NumberIsTakenAsync(EmployeeNumber number, CancellationToken cancellationToken = default);

    Task<bool> EmailIsTakenAsync(string email, Guid? exceptEmployeeId = null, CancellationToken cancellationToken = default);

    void Add(Employee employee);
}

/// <summary>
/// Reference data: companies, trades, certification types and the facility hierarchy.
/// One interface rather than six, because every one of them is "give me the active rows,
/// or tell me whether this id is real" and six interfaces would say nothing extra.
/// </summary>
public interface IReferenceDataRepository
{
    Task<IReadOnlyList<LookupDto>> GetCompaniesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetTradesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetCertificationTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetFacilitiesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetBuildingsAsync(Guid facilityId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetLocationsAsync(Guid buildingId, CancellationToken cancellationToken = default);

    Task<bool> CompanyExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> TradeExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> CertificationTypeExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> FacilityExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record LookupDto(Guid Id, string Code, string Name);

/// <summary>
/// Commits everything the repositories have staged, in one transaction.
/// <para>
/// Separate from the repositories on purpose: a service that adds an employee and records
/// a certification should decide for itself when that becomes permanent. Repositories that
/// save on every call quietly remove that choice, and with it the ability to make two
/// changes atomically.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
