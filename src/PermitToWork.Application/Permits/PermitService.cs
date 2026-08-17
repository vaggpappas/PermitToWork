using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Application.Permits;

public interface IPermitService
{
    Task<PagedResult<PermitSummaryDto>> SearchAsync(PermitSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The permits a given employee is on, as crew or as Receiver.
    /// <para>
    /// A separate method rather than another flag on <see cref="PermitSearchRequest"/>,
    /// because naming somebody else is a different privilege from searching. Keeping the
    /// employee id off the bound request means the ordinary search endpoint has no field
    /// through which it could be asked, whatever anyone puts in the query string.
    /// </para>
    /// </summary>
    Task<PagedResult<PermitSummaryDto>> SearchForCrewMemberAsync(
        Guid employeeId,
        PermitSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<PermitDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermitTypeDto>> GetPermitTypesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreatePermitRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdatePermitRequest request, CancellationToken cancellationToken = default);

    Task AddWorkerAsync(Guid id, AddPermitWorkerRequest request, CancellationToken cancellationToken = default);

    Task RemoveWorkerAsync(Guid id, Guid employeeId, CancellationToken cancellationToken = default);

    Task<Guid> AddEquipmentAsync(Guid id, AddPermitEquipmentRequest request, CancellationToken cancellationToken = default);

    Task RemoveEquipmentAsync(Guid id, Guid equipmentId, CancellationToken cancellationToken = default);

    Task<Guid> AttachDocumentAsync(Guid id, DocumentUpload upload, CancellationToken cancellationToken = default);

    Task<DocumentDownload> GetDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken = default);

    Task RemoveDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken = default);

    Task SubmitAsync(Guid id, CancellationToken cancellationToken = default);

    Task ApproveAsync(Guid id, ApprovePermitRequest request, CancellationToken cancellationToken = default);

    Task RejectAsync(Guid id, PermitReasonRequest request, CancellationToken cancellationToken = default);

    Task SuspendAsync(Guid id, PermitReasonRequest request, CancellationToken cancellationToken = default);

    Task ResumeAsync(Guid id, CancellationToken cancellationToken = default);

    Task CloseAsync(Guid id, ClosePermitRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync(Guid id, PermitReasonRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Permit use cases.
/// <para>
/// Every method here does the same three things: work out who is acting, load the
/// aggregate, hand off. There is not one <c>if</c> about permit status in this file —
/// which transitions are legal is <see cref="Permit"/>'s business, and an illegal one
/// throws a <see cref="DomainException"/> that becomes a 422.
/// </para>
/// </summary>
public sealed class PermitService(
    IPermitRepository permits,
    IFacilityApproverRepository panels,
    IEmployeeRepository employees,
    IFileStorage files,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IPermitService
{
    public Task<PagedResult<PermitSummaryDto>> SearchAsync(
        PermitSearchRequest request,
        CancellationToken cancellationToken = default) =>
        permits.SearchAsync(request, currentUser.EmployeeId, cancellationToken: cancellationToken);

    public Task<PagedResult<PermitSummaryDto>> SearchForCrewMemberAsync(
        Guid employeeId,
        PermitSearchRequest request,
        CancellationToken cancellationToken = default) =>
        // The company query filter still applies underneath: a contractor supervisor asking
        // about somebody outside their own company gets an empty page, not a refusal, because
        // as far as their scope is concerned that person does not exist.
        permits.SearchAsync(request, currentUser.EmployeeId, employeeId, cancellationToken);

    public async Task<PermitDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await permits.GetDetailAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Permit), id);

    public Task<IReadOnlyList<PermitTypeDto>> GetPermitTypesAsync(CancellationToken cancellationToken = default) =>
        permits.GetPermitTypesAsync(cancellationToken);

    public Task<IReadOnlyList<LookupDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        permits.GetCategoriesAsync(cancellationToken);

    public async Task<Guid> CreateAsync(CreatePermitRequest request, CancellationToken cancellationToken = default)
    {
        var author = RequireCurrentEmployee();
        var validity = DateTimeRange.Create(request.ValidFrom, request.ValidTo);

        var facilityId = await permits.GetFacilityOfLocationAsync(request.LocationId, cancellationToken)
                         ?? throw new NotFoundException(nameof(Location), request.LocationId);

        if (await employees.FindAsync(request.ReceiverId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Employee), request.ReceiverId);
        }

        var types = await permits.GetPermitTypesAsync(cancellationToken);
        var type = types.FirstOrDefault(t => t.Id == request.PermitTypeId)
                   ?? throw new NotFoundException(nameof(PermitType), request.PermitTypeId);

        // The requirements are copied onto the permit, not referenced — a record of what
        // the rules were when it was raised.
        var requirements = await permits.GetRequirementsAsync(request.PermitTypeId, cancellationToken);
        var number = await permits.NextNumberAsync(type.Code, validity.Start.Year, cancellationToken);

        var permit = new Permit(
            PermitNumber.Create(number),
            request.PermitTypeId,
            request.CategoryId,
            request.WorkDescription,
            facilityId,
            request.LocationId,
            validity,
            author,
            request.ReceiverId,
            requirements,
            request.Project,
            request.Notes);

        permits.Add(permit);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return permit.Id;
    }

    public async Task UpdateAsync(Guid id, UpdatePermitRequest request, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        var facilityId = await permits.GetFacilityOfLocationAsync(request.LocationId, cancellationToken)
                         ?? throw new NotFoundException(nameof(Location), request.LocationId);

        permit.UpdateContent(
            actor,
            request.CategoryId,
            request.WorkDescription,
            facilityId,
            request.LocationId,
            DateTimeRange.Create(request.ValidFrom, request.ValidTo),
            request.ReceiverId,
            request.Project,
            request.Notes);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AddWorkerAsync(Guid id, AddPermitWorkerRequest request, CancellationToken cancellationToken = default)
    {
        var permit = await RequireAsync(id, cancellationToken);

        // Loaded with certifications, because the permit is about to interrogate them.
        var employee = await employees.FindAsync(request.EmployeeId, cancellationToken)
                       ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        permit.AddWorker(employee, request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveWorkerAsync(Guid id, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        permit.RemoveWorker(employeeId, actor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddEquipmentAsync(
        Guid id,
        AddPermitEquipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        var item = permit.AddEquipment(actor, request.Description, request.Identifier, request.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }

    public async Task RemoveEquipmentAsync(Guid id, Guid equipmentId, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        permit.RemoveEquipment(equipmentId, actor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AttachDocumentAsync(
        Guid id,
        DocumentUpload upload,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        // Checked before a byte is written. Saving first and validating after would mean an
        // oversized file has already cost the disk space it was rejected for.
        DocumentPolicy.EnsureAcceptable(upload.FileName, upload.ContentType, upload.Length);

        var storageKey = await files.SaveAsync(upload.Content, upload.FileName, cancellationToken);

        try
        {
            var document = permit.AttachDocument(
                actor, upload.FileName, upload.ContentType, upload.Length, storageKey);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return document.Id;
        }
        catch
        {
            // The bytes are on disk but the row will not be. Without this, a permit that
            // refuses the attachment leaves an orphan file nobody can reach or account for.
            await files.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<DocumentDownload> GetDocumentAsync(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        // Through the permit, not by document id alone — so the company scope that governs
        // the permit governs its attachments too. A document is not reachable by guessing
        // its id.
        var permit = await RequireAsync(id, cancellationToken);

        var document = permit.Documents.SingleOrDefault(d => d.Id == documentId)
                       ?? throw new NotFoundException("Document", documentId);

        var content = await files.OpenAsync(document.StorageKey, cancellationToken)
                      ?? throw new NotFoundException("Document file", documentId);

        return new DocumentDownload(content, document.FileName, document.ContentType);
    }

    public async Task RemoveDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        var removed = permit.RemoveDocument(documentId, actor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Only after the row is gone. The other order risks deleting the file and then
        // failing to save, leaving a document row pointing at nothing.
        await files.DeleteAsync(removed.StorageKey, cancellationToken);
    }

    public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        // The panel is read here and copied onto the permit by Submit. The aggregate never
        // reaches out for it, so the snapshot is exactly what this call saw.
        var panel = await panels.GetPanelAsync(permit.FacilityId, cancellationToken);

        permit.Submit(actor, panel);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid id, ApprovePermitRequest request, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        // Whether this person is entitled to approve is the permit's question, not ours —
        // it holds the panel that was captured at submission.
        permit.Approve(actor, request.Comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid id, PermitReasonRequest request, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        permit.Reject(actor, request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SuspendAsync(Guid id, PermitReasonRequest request, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        permit.Suspend(actor, request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        permit.Resume(actor, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseAsync(Guid id, ClosePermitRequest request, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        // Close refuses anybody but the creator. That check is in the aggregate, so it
        // holds for every caller including any future one.
        permit.Close(actor, request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid id, PermitReasonRequest request, CancellationToken cancellationToken = default)
    {
        var actor = RequireCurrentEmployee();
        var permit = await RequireAsync(id, cancellationToken);

        permit.Cancel(actor, request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Permit> RequireAsync(Guid id, CancellationToken cancellationToken) =>
        await permits.FindAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Permit), id);

    /// <summary>
    /// Every permit action is attributable to a person, not to a login. Somebody with an
    /// account but no employee record cannot raise, sign or close anything — and saying so
    /// once here is better than a null check in fourteen methods.
    /// </summary>
    private Guid RequireCurrentEmployee() =>
        currentUser.EmployeeId
        ?? throw new DomainException(
            "Your login is not linked to an employee record, so you cannot act on permits.");
}
