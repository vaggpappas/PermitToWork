using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Application.Employees;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeSummaryDto>> SearchAsync(EmployeeSearchRequest request, CancellationToken cancellationToken = default);

    Task<EmployeeDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

    Task AssignManagerAsync(Guid id, Guid? managerId, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(Guid id, EmploymentAction action, CancellationToken cancellationToken = default);

    Task AssignAccessRoleAsync(Guid id, AccessRole role, CancellationToken cancellationToken = default);

    Task<Guid> AddCertificationAsync(Guid id, AddCertificationRequest request, CancellationToken cancellationToken = default);

    Task RemoveCertificationAsync(Guid id, Guid certificationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Employee use cases.
/// <para>
/// The service's job is orchestration and nothing else: check the things the domain cannot
/// see (does this company id exist, is this number already used), then hand off to the
/// aggregate and commit. Every rule about what an employee <em>is</em> stays in
/// <see cref="Employee"/> — which is why this class has no <c>if</c> statements about
/// employment status anywhere in it.
/// </para>
/// </summary>
public sealed class EmployeeService(
    IEmployeeRepository employees,
    IReferenceDataRepository referenceData,
    IUnitOfWork unitOfWork) : IEmployeeService
{
    public Task<PagedResult<EmployeeSummaryDto>> SearchAsync(
        EmployeeSearchRequest request,
        CancellationToken cancellationToken = default) =>
        employees.SearchAsync(request, cancellationToken);

    public async Task<EmployeeDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await employees.GetDetailAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(Employee), id);

    public async Task<Guid> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        // Value objects first: if the email is malformed, nothing else matters and the
        // caller gets one clear message instead of a database constraint error.
        var name = PersonName.Create(request.FirstName, request.LastName);
        var contact = ContactInfo.Create(request.Email, request.PhoneNumber);

        if (await employees.EmailIsTakenAsync(contact.Email, cancellationToken: cancellationToken))
        {
            throw new ConflictException($"'{contact.Email}' is already on another employee record.");
        }

        if (!await referenceData.CompanyExistsAsync(request.CompanyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Company), request.CompanyId);
        }

        if (!await referenceData.TradeExistsAsync(request.TradeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Trade), request.TradeId);
        }

        // Generated after the company is known to exist, since the badge carries its code.
        var number = await employees.NextNumberAsync(request.CompanyId, cancellationToken);

        var employee = new Employee(
            number,
            name,
            contact,
            request.CompanyId,
            request.TradeId,
            request.JobTitle,
            request.HireDate);

        employee.UpdateProfile(
            name,
            contact,
            request.JobTitle,
            request.TradeId,
            request.DateOfBirth,
            ToAddress(request.Address));

        if (request.ManagerId is { } managerId)
        {
            await RequireExistsAsync(managerId, cancellationToken);
            employee.AssignManager(managerId);
        }

        employees.Add(employee);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await RequireAsync(id, cancellationToken);
        var contact = ContactInfo.Create(request.Email, request.PhoneNumber);

        if (await employees.EmailIsTakenAsync(contact.Email, id, cancellationToken))
        {
            throw new ConflictException($"'{contact.Email}' is already on another employee record.");
        }

        if (!await referenceData.TradeExistsAsync(request.TradeId, cancellationToken))
        {
            throw new NotFoundException(nameof(Trade), request.TradeId);
        }

        employee.UpdateProfile(
            PersonName.Create(request.FirstName, request.LastName),
            contact,
            request.JobTitle,
            request.TradeId,
            request.DateOfBirth,
            ToAddress(request.Address));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignManagerAsync(Guid id, Guid? managerId, CancellationToken cancellationToken = default)
    {
        var employee = await RequireAsync(id, cancellationToken);

        if (managerId is { } manager)
        {
            await RequireExistsAsync(manager, cancellationToken);
        }

        // The domain rejects self-management; this method does not need to know that rule.
        employee.AssignManager(managerId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeStatusAsync(Guid id, EmploymentAction action, CancellationToken cancellationToken = default)
    {
        var employee = await RequireAsync(id, cancellationToken);

        // Which transitions are legal is the aggregate's business — an illegal one throws
        // a DomainException and becomes a 409. This switch only translates the verb.
        switch (action)
        {
            case EmploymentAction.Suspend:
                employee.Suspend();
                break;
            case EmploymentAction.Reinstate:
                employee.Reinstate();
                break;
            case EmploymentAction.Terminate:
                employee.Terminate();
                break;
            default:
                throw new DomainException($"Unknown employment action '{action}'.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignAccessRoleAsync(Guid id, AccessRole role, CancellationToken cancellationToken = default)
    {
        var employee = await RequireAsync(id, cancellationToken);

        // The aggregate refuses to give a role to somebody who has been terminated.
        employee.AssignAccessRole(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddCertificationAsync(
        Guid id,
        AddCertificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var employee = await RequireAsync(id, cancellationToken);

        if (!await referenceData.CertificationTypeExistsAsync(request.CertificationTypeId, cancellationToken))
        {
            throw new NotFoundException(nameof(CertificationType), request.CertificationTypeId);
        }

        var certification = employee.AddCertification(
            request.CertificationTypeId,
            request.IssuedBy,
            request.IssuedOn,
            request.ExpiresOn,
            request.ReferenceNumber);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return certification.Id;
    }

    public async Task RemoveCertificationAsync(Guid id, Guid certificationId, CancellationToken cancellationToken = default)
    {
        var employee = await RequireAsync(id, cancellationToken);

        employee.RemoveCertification(certificationId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Employee> RequireAsync(Guid id, CancellationToken cancellationToken) =>
        await employees.FindAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Employee), id);

    private async Task RequireExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await employees.FindAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Employee), id);
        }
    }

    private static Address? ToAddress(AddressDto? dto) =>
        dto is null ? null : Address.Create(dto.Street, dto.City, dto.PostalCode, dto.Country);
}
