using FluentAssertions;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;
using Xunit;

namespace PermitToWork.Tests.Domain;

/// <summary>
/// Employment status, age and certifications, tested against the aggregate directly.
/// No database, no mocks — these are rules about what an employee is, and they should be
/// provable without either.
/// </summary>
public class EmployeeTests
{
    #region Employment status

    [Fact]
    public void Employee_StartsActive()
    {
        var employee = Given.AnEmployee();

        employee.Status.Should().Be(EmploymentStatus.Active);
    }

    [Fact]
    public void Employee_Suspends_When_Active()
    {
        var employee = Given.AnEmployee();

        employee.Suspend();

        employee.Status.Should().Be(EmploymentStatus.Suspended);
    }

    [Fact]
    public void Employee_RejectsSuspension_When_AlreadySuspended()
    {
        var employee = Given.AnEmployee();
        employee.Suspend();

        var suspendAgain = () => employee.Suspend();

        suspendAgain.Should().Throw<DomainException>()
            .WithMessage("*Only an active employee can be suspended*");
    }

    [Fact]
    public void Employee_Reinstates_When_Suspended()
    {
        var employee = Given.AnEmployee();
        employee.Suspend();

        employee.Reinstate();

        employee.Status.Should().Be(EmploymentStatus.Active);
    }

    [Fact]
    public void Employee_RejectsReinstatement_When_Active()
    {
        var employee = Given.AnEmployee();

        var reinstate = () => employee.Reinstate();

        reinstate.Should().Throw<DomainException>()
            .WithMessage("*Only a suspended employee can be reinstated*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Employee_Terminates_From_ActiveOrSuspended(bool suspendFirst)
    {
        var employee = Given.AnEmployee();
        if (suspendFirst)
        {
            employee.Suspend();
        }

        employee.Terminate();

        employee.Status.Should().Be(EmploymentStatus.Terminated);
    }

    [Fact]
    public void Employee_RejectsTermination_When_AlreadyTerminated()
    {
        var employee = Given.AnEmployee();
        employee.Terminate();

        var terminateAgain = () => employee.Terminate();

        terminateAgain.Should().Throw<DomainException>()
            .WithMessage("*already been terminated*");
    }

    #endregion

    #region Age

    [Fact]
    public void Employee_ReportsNoAge_When_DateOfBirthUnknown()
    {
        var employee = Given.AnEmployee();

        employee.AgeOn(Given.Today).Should().BeNull();
    }

    [Fact]
    public void Employee_ComputesAge_FromDateOfBirth()
    {
        var employee = WithDateOfBirth(new DateOnly(1990, 8, 3));

        employee.AgeOn(new DateOnly(2026, 8, 14)).Should().Be(36);
    }

    [Fact]
    public void Employee_ComputesAge_When_BirthdayHasNotHappenedYet()
    {
        var employee = WithDateOfBirth(new DateOnly(1990, 8, 3));

        // The day before their birthday they are still a year younger. This is the case
        // that a naive year subtraction gets wrong.
        employee.AgeOn(new DateOnly(2026, 8, 2)).Should().Be(35);
    }

    [Fact]
    public void Employee_ComputesAge_OnTheBirthdayItself()
    {
        var employee = WithDateOfBirth(new DateOnly(1990, 8, 3));

        employee.AgeOn(new DateOnly(2026, 8, 3)).Should().Be(36);
    }

    [Fact]
    public void Employee_RejectsDateOfBirth_When_NotBeforeHireDate()
    {
        var employee = Given.AnEmployee();

        var setBirthAfterHire = () => WithDateOfBirth(Given.HireDate, employee);

        setBirthAfterHire.Should().Throw<DomainException>()
            .WithMessage("*before the hire date*");
    }

    [Fact]
    public void Employee_StartsWithNoPrivileges()
    {
        // Read-only until somebody decides otherwise. A new record that could already
        // change data would be a standing grant nobody made.
        Given.AnEmployee().AccessRole.Should().Be(AccessRole.Employee);
    }

    [Fact]
    public void Employee_LosesAccessRole_When_Terminated()
    {
        var employee = Given.AnEmployee();
        employee.AssignAccessRole(AccessRole.Responsible);

        employee.Terminate();

        // Revoking access is part of what terminating means, not a separate step on a
        // checklist that somebody has to remember.
        employee.AccessRole.Should().Be(AccessRole.Employee);
    }

    [Fact]
    public void Employee_RejectsAccessRole_When_Terminated()
    {
        var employee = Given.AnEmployee();
        employee.Terminate();

        var promote = () => employee.AssignAccessRole(AccessRole.Supervisor);

        promote.Should().Throw<DomainException>()
            .WithMessage("*terminated employee cannot be given an access role*");
    }

    #endregion

    #region Reporting line and account

    [Fact]
    public void Employee_RejectsSelfAsManager()
    {
        var employee = Given.AnEmployee();

        var manageSelf = () => employee.AssignManager(employee.Id);

        manageSelf.Should().Throw<DomainException>()
            .WithMessage("*cannot report to themselves*");
    }

    [Fact]
    public void Employee_ClearsManager_When_AssignedNull()
    {
        var employee = Given.AnEmployee();
        employee.AssignManager(Guid.CreateVersion7());

        employee.AssignManager(null);

        employee.ManagerId.Should().BeNull();
    }

    [Fact]
    public void Employee_LinksToUserAccount()
    {
        var employee = Given.AnEmployee();
        var userId = Guid.CreateVersion7();

        employee.LinkToUser(userId);

        employee.UserId.Should().Be(userId);
    }

    [Fact]
    public void Employee_RejectsSecondUserLink()
    {
        var employee = Given.AnEmployee();
        employee.LinkToUser(Guid.CreateVersion7());

        var linkAgain = () => employee.LinkToUser(Guid.CreateVersion7());

        // Without this, registering twice would silently hand the record to whoever
        // registered last.
        linkAgain.Should().Throw<DomainException>()
            .WithMessage("*already linked*");
    }

    #endregion

    #region Certifications

    [Fact]
    public void Employee_HasValidCertification_When_TodayFallsInsideTheDates()
    {
        var employee = Given.AnEmployee();
        employee.AddCertification(
            Given.HotWorkCertificationTypeId,
            "Hellenic Welding Institute",
            new DateOnly(2025, 1, 15),
            new DateOnly(2027, 1, 15));

        employee.HasValidCertification(Given.HotWorkCertificationTypeId, Given.Today).Should().BeTrue();
    }

    [Fact]
    public void Employee_HasNoValidCertification_When_Expired()
    {
        var employee = Given.AnEmployee();
        employee.AddCertification(
            Given.HotWorkCertificationTypeId,
            "Hellenic Welding Institute",
            new DateOnly(2023, 1, 15),
            new DateOnly(2025, 1, 15));

        employee.HasValidCertification(Given.HotWorkCertificationTypeId, Given.Today).Should().BeFalse();
    }

    [Fact]
    public void Employee_HasNoValidCertification_When_HeldForAnotherType()
    {
        var employee = Given.AnEmployee();
        employee.AddCertification(
            Given.HotWorkCertificationTypeId,
            "Hellenic Welding Institute",
            new DateOnly(2025, 1, 15),
            new DateOnly(2027, 1, 15));

        employee.HasValidCertification(Given.ConfinedSpaceCertificationTypeId, Given.Today).Should().BeFalse();
    }

    [Fact]
    public void Employee_KeepsExpiredCertifications_When_Renewed()
    {
        var employee = Given.AnEmployee();
        employee.AddCertification(Given.HotWorkCertificationTypeId, "HWI", new DateOnly(2023, 1, 15), new DateOnly(2025, 1, 15));
        employee.AddCertification(Given.HotWorkCertificationTypeId, "HWI", new DateOnly(2025, 1, 10), new DateOnly(2027, 1, 10));

        // Renewals are added, not overwritten: an audit asks who was qualified in 2024,
        // and an overwritten row cannot answer.
        employee.Certifications.Should().HaveCount(2);
        employee.HasValidCertification(Given.HotWorkCertificationTypeId, new DateOnly(2024, 6, 1)).Should().BeTrue();
        employee.HasValidCertification(Given.HotWorkCertificationTypeId, Given.Today).Should().BeTrue();
    }

    [Fact]
    public void Certification_RejectsExpiryBeforeIssue()
    {
        var employee = Given.AnEmployee();

        var backwards = () => employee.AddCertification(
            Given.HotWorkCertificationTypeId,
            "HWI",
            new DateOnly(2027, 1, 15),
            new DateOnly(2025, 1, 15));

        backwards.Should().Throw<DomainException>()
            .WithMessage("*must expire after*");
    }

    [Fact]
    public void Certification_RejectsExpiryOnTheIssueDate()
    {
        var employee = Given.AnEmployee();

        var sameDay = () => employee.AddCertification(
            Given.HotWorkCertificationTypeId,
            "HWI",
            new DateOnly(2025, 1, 15),
            new DateOnly(2025, 1, 15));

        sameDay.Should().Throw<DomainException>();
    }

    [Fact]
    public void Employee_RemovesCertification()
    {
        var employee = Given.AnEmployee();
        var certification = employee.AddCertification(
            Given.HotWorkCertificationTypeId, "HWI", new DateOnly(2025, 1, 15), new DateOnly(2027, 1, 15));

        employee.RemoveCertification(certification.Id);

        employee.Certifications.Should().BeEmpty();
    }

    [Fact]
    public void Employee_RejectsRemovalOfUnknownCertification()
    {
        var employee = Given.AnEmployee();

        var removeStranger = () => employee.RemoveCertification(Guid.CreateVersion7());

        removeStranger.Should().Throw<DomainException>()
            .WithMessage("*no such certification*");
    }

    #endregion

    private static Employee WithDateOfBirth(DateOnly dateOfBirth, Employee? existing = null)
    {
        var employee = existing ?? Given.AnEmployee();

        employee.UpdateProfile(
            PersonName.Create("Nadia", "Kowalski"),
            ContactInfo.Create("nadia.kowalski@acme.example", null),
            "Welder",
            Given.WelderTradeId,
            dateOfBirth,
            null);

        return employee;
    }
}
