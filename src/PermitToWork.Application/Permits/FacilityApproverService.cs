using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Application.Permits;

public interface IFacilityApproverService
{
    Task<IReadOnlyList<FacilityApproverDto>> GetPanelAsync(Guid facilityId, CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(Guid facilityId, AddFacilityApproverRequest request, CancellationToken cancellationToken = default);

    Task SetDecisiveAsync(Guid approverId, bool isDecisive, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid approverId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Who signs permits at a given site.
/// <para>
/// Administrators only, and for a reason worth stating: this list is the entire approval
/// control for that facility. Somebody who can edit it can quietly seat themselves as a
/// decisive approver and sign off anything.
/// </para>
/// </summary>
public sealed class FacilityApproverService(
    IFacilityApproverRepository approvers,
    IEmployeeRepository employees,
    IReferenceDataRepository referenceData,
    IUnitOfWork unitOfWork) : IFacilityApproverService
{
    public Task<IReadOnlyList<FacilityApproverDto>> GetPanelAsync(
        Guid facilityId,
        CancellationToken cancellationToken = default) =>
        approvers.GetPanelDetailAsync(facilityId, cancellationToken);

    public async Task<Guid> AddAsync(
        Guid facilityId,
        AddFacilityApproverRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await referenceData.FacilityExistsAsync(facilityId, cancellationToken))
        {
            throw new NotFoundException(nameof(Facility), facilityId);
        }

        if (await employees.FindAsync(request.EmployeeId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Employee), request.EmployeeId);
        }

        if (await approvers.AlreadySeatedAsync(facilityId, request.EmployeeId, cancellationToken))
        {
            // Being on the panel twice is not a stronger approval — it is a permit that can
            // never be fully signed.
            throw new ConflictException("That person is already on this facility's approval panel.");
        }

        var approver = new FacilityApprover(facilityId, request.EmployeeId, request.IsDecisive);
        approvers.Add(approver);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return approver.Id;
    }

    public async Task SetDecisiveAsync(Guid approverId, bool isDecisive, CancellationToken cancellationToken = default)
    {
        var approver = await RequireAsync(approverId, cancellationToken);

        if (isDecisive)
        {
            approver.MakeDecisive();
        }
        else
        {
            approver.MakeOrdinary();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid approverId, CancellationToken cancellationToken = default)
    {
        var approver = await RequireAsync(approverId, cancellationToken);

        // Removed from the panel, not from history. Permits already submitted keep their
        // copy of this seat, so a signature given last week stays a signature.
        approvers.Remove(approver);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<FacilityApprover> RequireAsync(Guid id, CancellationToken cancellationToken) =>
        await approvers.FindAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(FacilityApprover), id);
}
