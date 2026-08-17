using FluentAssertions;
using NSubstitute;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Application.Employees;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;
using Xunit;

namespace PermitToWork.Tests.Application;

/// <summary>
/// The employee service's own job — which is only the checks the aggregate cannot make for
/// itself: is this badge number already used, does this company id exist.
/// <para>
/// Substitutes appear here because the repository is a genuine boundary. Nothing in this
/// file re-tests a domain rule; those are covered against the aggregate directly, where
/// they do not need a fake database to be true.
/// </para>
/// </summary>
public class EmployeeServiceTests
{
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IReferenceDataRepository _referenceData = Substitute.For<IReferenceDataRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    // Only the self-service methods read this, and none of the tests below are about them —
    // the "is it really my own record" question is answered over HTTP in the integration
    // suite, where a real token decides who the caller is.
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly IEmailSender _emails = Substitute.For<IEmailSender>();

    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        _service = new EmployeeService(
            _employees,
            _referenceData,
            _currentUser,
            _emails,
            new ApplicationLinks("http://localhost:4200"),
            _unitOfWork);

        // The happy path by default; each test spoils exactly the one thing it is about.
        _referenceData.CompanyExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _referenceData.TradeExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _referenceData.CertificationTypeExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _employees
            .NextNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(EmployeeNumber.Create("ACME-0007"));
    }

    [Fact]
    public async Task EmployeeService_CreatesEmployee_AndSavesOnce()
    {
        Employee? added = null;
        _employees.Add(Arg.Do<Employee>(e => added = e));

        var id = await _service.CreateAsync(ARequest());

        added.Should().NotBeNull();
        added!.Id.Should().Be(id);
        added.Status.Should().Be(EmploymentStatus.Active);

        // The badge number comes from the generator, never from the request — there is no
        // field on CreateEmployeeRequest to supply one.
        added.Number.Value.Should().Be("ACME-0007");

        // And everyone starts read-only, whatever the person creating them can do.
        added.AccessRole.Should().Be(AccessRole.Employee);

        // One commit, not one per field. The whole creation is a single unit of work.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmployeeService_RejectsDuplicateEmail()
    {
        _employees.EmailIsTakenAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        var create = async () => await _service.CreateAsync(ARequest());

        await create.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task EmployeeService_RejectsUnknownCompany()
    {
        _referenceData.CompanyExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var create = async () => await _service.CreateAsync(ARequest());

        await create.Should().ThrowAsync<NotFoundException>().WithMessage("*Company*");
    }

    [Fact]
    public async Task EmployeeService_RejectsUnknownTrade()
    {
        _referenceData.TradeExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var create = async () => await _service.CreateAsync(ARequest());

        await create.Should().ThrowAsync<NotFoundException>().WithMessage("*Trade*");
    }

    [Fact]
    public async Task EmployeeService_DoesNotGenerateANumber_When_TheCompanyIsUnknown()
    {
        _referenceData.CompanyExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var create = async () => await _service.CreateAsync(ARequest());

        await create.Should().ThrowAsync<NotFoundException>();

        // The badge carries the company code, so generating one for a company that does not
        // exist would either throw somewhere less helpful or invent a prefix.
        await _employees.DidNotReceive().NextNumberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmployeeService_AssignsAccessRole()
    {
        var employee = Given.AnEmployee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        await _service.AssignAccessRoleAsync(employee.Id, AccessRole.Responsible);

        employee.AccessRole.Should().Be(AccessRole.Responsible);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmployeeService_ReportsNotFound_When_EmployeeMissing()
    {
        _employees.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((EmployeeDetailDto?)null);

        var get = async () => await _service.GetAsync(Guid.CreateVersion7());

        // Also the answer when the company scope hid the row — the two are deliberately
        // indistinguishable to the caller.
        await get.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EmployeeService_SuspendsThroughTheAggregate()
    {
        var employee = Given.AnEmployee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        await _service.ChangeStatusAsync(employee.Id, EmploymentAction.Suspend);

        employee.Status.Should().Be(EmploymentStatus.Suspended);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmployeeService_DoesNotSave_When_TheAggregateRefuses()
    {
        var employee = Given.AnEmployee();
        _employees.FindAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var reinstateAnActiveEmployee = async () =>
            await _service.ChangeStatusAsync(employee.Id, EmploymentAction.Reinstate);

        await reinstateAnActiveEmployee.Should().ThrowAsync<Exception>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CreateEmployeeRequest ARequest() => new()
    {
        FirstName = "Nadia",
        LastName = "Kowalski",
        Email = "nadia.kowalski@acme.example",
        CompanyId = Given.AcmeCompanyId,
        TradeId = Given.WelderTradeId,
        JobTitle = "Welder",
        HireDate = Given.HireDate
    };
}
