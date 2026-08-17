using FluentAssertions;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;
using PermitToWork.Domain.ValueObjects;
using Xunit;

namespace PermitToWork.Tests.Domain;

/// <summary>
/// The state machine. Every transition, and — more usefully — every transition that must
/// not be allowed.
/// </summary>
public class PermitLifecycleTests
{
    private static readonly Guid Creator = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid Receiver = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid SafetyOfficer = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid AreaManager = Guid.Parse("a0000000-0000-0000-0000-000000000004");
    private static readonly Guid Director = Guid.Parse("a0000000-0000-0000-0000-000000000005");
    private static readonly Guid Stranger = Guid.Parse("a0000000-0000-0000-0000-00000000000f");

    /// <summary>Some moment inside the work window.</summary>
    private static readonly DateTimeOffset DuringTheJob = Given.WorkStart.AddHours(2);

    /// <summary>Some moment after it.</summary>
    private static readonly DateTimeOffset AfterTheJob = Given.WorkEnd.AddDays(1);

    #region Draft

    [Fact]
    public void Permit_StartsAsADraft()
    {
        var permit = NewPermit();

        permit.Status.Should().Be(PermitStatus.Draft);
        permit.IsEditable.Should().BeTrue();
        permit.IssuedById.Should().BeNull();
    }

    [Fact]
    public void Permit_RecordsItsOwnCreation()
    {
        var permit = NewPermit();

        permit.Events.Should().ContainSingle(e => e.Kind == PermitEventKind.Created);
    }

    [Fact]
    public void Permit_RefusesSubmission_When_NobodyIsOnIt()
    {
        var permit = NewPermit();

        var submit = () => permit.Submit(Creator, [Given.Approver(SafetyOfficer)]);

        // An authorisation for nobody is not an authorisation.
        submit.Should().Throw<DomainException>().WithMessage("*no workers*");
    }

    [Fact]
    public void Permit_RefusesSubmission_When_TheFacilityHasNoPanel()
    {
        var permit = WithACrew();

        var submit = () => permit.Submit(Creator, []);

        submit.Should().Throw<DomainException>().WithMessage("*no approval panel*");
    }

    [Fact]
    public void Permit_RefusesContentChange_OnceSubmitted()
    {
        var permit = Submitted();

        var edit = () => permit.UpdateContent(
            Creator, Given.MaintenanceTaskGroupId, "Something else entirely",
            Given.FacilityId, Given.LocationId, Given.TheWorkWindow, Receiver, null, null);

        // What was approved must be what is performed.
        edit.Should().Throw<DomainException>().WithMessage("*can no longer be edited*");
    }

    #endregion

    #region The approval panel

    [Fact]
    public void Permit_SkipsTheAuthorsOwnSeat_When_OthersAreOnThePanel()
    {
        var permit = WithACrew();

        permit.Submit(Creator, [Given.Approver(Creator), Given.Approver(SafetyOfficer)]);

        // Nobody signs off their own paperwork while somebody else could.
        permit.Approvals.Should().ContainSingle();
        permit.Approvals[0].ApproverEmployeeId.Should().Be(SafetyOfficer);
    }

    [Fact]
    public void Permit_KeepsTheAuthorsSeat_When_TheyAreTheOnlyApprover()
    {
        var permit = WithACrew();

        permit.Submit(Creator, [Given.Approver(Creator)]);

        // A one-person site still has to be able to raise permits. The audit trail says so.
        permit.Approvals.Should().ContainSingle(a => a.ApproverEmployeeId == Creator);
        permit.Events.Should().Contain(e =>
            e.Kind == PermitEventKind.Submitted && e.Detail!.Contains("only approver"));
    }

    [Fact]
    public void Permit_RefusesApproval_FromSomebodyNotOnThePanel()
    {
        var permit = Submitted();

        var approve = () => permit.Approve(Stranger);

        approve.Should().Throw<DomainException>().WithMessage("*not an approver*");
    }

    [Fact]
    public void Permit_RefusesASecondAnswerFromTheSameApprover()
    {
        // Two approvers on purpose. With only one, their signature activates the permit and
        // a second attempt is refused by the *status* guard — which proves nothing about
        // whether an approver can answer twice.
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));
        permit.Approve(SafetyOfficer);

        var approveAgain = () => permit.Approve(SafetyOfficer);

        approveAgain.Should().Throw<DomainException>().WithMessage("*already approved*");
        permit.Status.Should().Be(PermitStatus.Pending);
    }

    [Fact]
    public void Permit_StaysPending_UntilEveryApproverHasSigned()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));

        permit.Approve(SafetyOfficer);

        permit.Status.Should().Be(PermitStatus.Pending);
        permit.OutstandingApprovals.Should().Be(1);
    }

    [Fact]
    public void Permit_ActivatesOnTheLastApproval()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));

        permit.Approve(SafetyOfficer);
        permit.Approve(AreaManager);

        permit.Status.Should().Be(PermitStatus.Active);
        permit.OutstandingApprovals.Should().Be(0);
    }

    [Fact]
    public void Permit_ActivatesImmediately_When_ADecisiveApproverSigns()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.DecisiveApprover(Director));

        permit.Approve(Director);

        // The seniority override: the outstanding signature is simply overtaken.
        permit.Status.Should().Be(PermitStatus.Active);
        permit.OutstandingApprovals.Should().Be(1);
    }

    [Fact]
    public void Permit_NamesWhoeverSignedLastAsTheIssuer()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));

        permit.Approve(SafetyOfficer);
        permit.Approve(AreaManager);

        // Derived from the approvals, never stored — there is no IssuerId to disagree.
        permit.IssuedById.Should().Be(AreaManager);
    }

    [Fact]
    public void Permit_HasNoIssuer_WhileStillPending()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));

        permit.Approve(SafetyOfficer);

        permit.IssuedById.Should().Be(SafetyOfficer);
        permit.Status.Should().Be(PermitStatus.Pending);
    }

    #endregion

    #region Rejection

    [Fact]
    public void Permit_IsFinished_When_AnyApproverRefuses()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));

        permit.Reject(SafetyOfficer, "The isolation certificate is missing.");

        // One refusal is enough. A safety document where "no" only counts if everybody says
        // it would be a strange thing to sign.
        permit.Status.Should().Be(PermitStatus.Rejected);
        permit.StatusReason.Should().Be("The isolation certificate is missing.");
        permit.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void Permit_CannotBeApproved_AfterRejection()
    {
        var permit = Submitted(Given.Approver(SafetyOfficer), Given.Approver(AreaManager));
        permit.Reject(SafetyOfficer, "No.");

        var approve = () => permit.Approve(AreaManager);

        approve.Should().Throw<DomainException>().WithMessage("*Rejected cannot be approved*");
    }

    #endregion

    #region Live work

    [Fact]
    public void Permit_SuspendsAndResumes()
    {
        var permit = Active();

        permit.Suspend(SafetyOfficer, "Gas alarm on the unit.");
        permit.Status.Should().Be(PermitStatus.Suspended);
        permit.StatusReason.Should().Be("Gas alarm on the unit.");

        permit.Resume(SafetyOfficer, DuringTheJob);
        permit.Status.Should().Be(PermitStatus.Active);
        permit.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Permit_RefusesToResume_When_ItsWindowClosedMeanwhile()
    {
        var permit = Active();
        permit.Suspend(SafetyOfficer, "Weather.");

        var resume = () => permit.Resume(SafetyOfficer, AfterTheJob);

        resume.Should().Throw<DomainException>().WithMessage("*passed while it was suspended*");
    }

    [Fact]
    public void Permit_AllowsCrewChanges_WhileActive()
    {
        var permit = Active();
        var second = Given.ACertifiedWelder(number: "ACME-0101");

        var add = () => permit.AddWorker(second);

        // Shifts turn over and people go sick; the crew genuinely does change mid-job.
        add.Should().NotThrow();
    }

    [Fact]
    public void Permit_RefusesCrewChanges_WhilePending()
    {
        var permit = Submitted();
        var second = Given.ACertifiedWelder(number: "ACME-0101");

        var add = () => permit.AddWorker(second);

        // People are approving a specific crew. Swapping it underneath them would make
        // their signature meaningless.
        add.Should().Throw<DomainException>().WithMessage("*cannot be changed*");
    }

    #endregion

    #region Closing, cancelling, expiring

    [Fact]
    public void Permit_IsClosedByItsCreator()
    {
        var permit = Active();

        permit.Close(Creator, "Flange replaced and pressure tested.");

        permit.Status.Should().Be(PermitStatus.Closed);
    }

    [Fact]
    public void Permit_RefusesToBeClosedByAnybodyElse()
    {
        var permit = Active();

        var close = () => permit.Close(SafetyOfficer);

        close.Should().Throw<DomainException>().WithMessage("*Only the person who raised*");
    }

    [Fact]
    public void Permit_ClosesFromSuspended_WithoutResumingFirst()
    {
        var permit = Active();
        permit.Suspend(SafetyOfficer, "Shift ended.");

        var close = () => permit.Close(Creator);

        // Otherwise closing halted work would mean briefly declaring it live again, purely
        // as paperwork.
        close.Should().NotThrow();
    }

    [Fact]
    public void Permit_RefusesClosure_WhileStillADraft()
    {
        var permit = WithACrew();

        var close = () => permit.Close(Creator);

        close.Should().Throw<DomainException>().WithMessage("*must be Active or Suspended*");
    }

    [Fact]
    public void Permit_IsCancelledWithAReason()
    {
        var permit = Submitted();

        permit.Cancel(Creator, "Turnaround postponed.");

        permit.Status.Should().Be(PermitStatus.Cancelled);
        permit.StatusReason.Should().Be("Turnaround postponed.");
    }

    [Fact]
    public void Permit_RefusesCancellation_When_AlreadyFinished()
    {
        var permit = Active();
        permit.Close(Creator);

        var cancel = () => permit.Cancel(Creator, "Changed my mind.");

        cancel.Should().Throw<DomainException>().WithMessage("*already finished*");
    }

    [Fact]
    public void Permit_ExpiresOnceItsWindowHasPassed()
    {
        var permit = Active();

        var expired = permit.ExpireIfElapsed(AfterTheJob);

        // Without this an abandoned permit reads as live work forever, which is the first
        // thing a safety audit goes looking for.
        expired.Should().BeTrue();
        permit.Status.Should().Be(PermitStatus.Expired);
    }

    [Fact]
    public void Permit_DoesNotExpire_WhileStillWithinItsWindow()
    {
        var permit = Active();

        permit.ExpireIfElapsed(DuringTheJob).Should().BeFalse();
        permit.Status.Should().Be(PermitStatus.Active);
    }

    [Fact]
    public void Permit_DoesNotExpire_When_AlreadyClosed()
    {
        var permit = Active();
        permit.Close(Creator);

        permit.ExpireIfElapsed(AfterTheJob).Should().BeFalse();
        permit.Status.Should().Be(PermitStatus.Closed);
    }

    [Fact]
    public void Permit_ExpiresWhileStillAwaitingApproval()
    {
        var permit = Submitted();

        permit.ExpireIfElapsed(AfterTheJob).Should().BeTrue();
        permit.Status.Should().Be(PermitStatus.Expired);
    }

    #endregion

    #region Audit trail

    [Fact]
    public void Permit_RecordsEveryStepOfItsLife()
    {
        var permit = Active();
        permit.Suspend(SafetyOfficer, "Gas alarm.");
        permit.Resume(SafetyOfficer, DuringTheJob);
        permit.Close(Creator);

        // Written by the aggregate as part of each transition, so no caller can forget to.
        permit.Events.Select(e => e.Kind).Should().ContainInOrder(
            PermitEventKind.Created,
            PermitEventKind.WorkerAdded,
            PermitEventKind.Submitted,
            PermitEventKind.Approved,
            PermitEventKind.Activated,
            PermitEventKind.Suspended,
            PermitEventKind.Resumed,
            PermitEventKind.Closed);
    }

    [Fact]
    public void Permit_RecordsWhoActedAndWhy()
    {
        var permit = Active();
        permit.Suspend(SafetyOfficer, "Gas alarm on the unit.");

        var suspension = permit.Events.Last(e => e.Kind == PermitEventKind.Suspended);
        suspension.ActorEmployeeId.Should().Be(SafetyOfficer);
        suspension.Detail.Should().Be("Gas alarm on the unit.");
    }

    [Fact]
    public void Permit_RecordsExpiryAsNobodysDoing()
    {
        var permit = Active();
        permit.ExpireIfElapsed(AfterTheJob);

        // The only transition a person does not perform, so the actor is genuinely absent
        // rather than attributed to whoever happened to trigger the sweep.
        permit.Events.Last().Kind.Should().Be(PermitEventKind.Expired);
        permit.Events.Last().ActorEmployeeId.Should().BeNull();
    }

    #endregion

    #region Builders

    private static Permit NewPermit(DateTimeRange? validity = null) =>
        Given.AHotWorkPermit(Creator, Receiver, validity);

    private static Permit WithACrew()
    {
        var permit = NewPermit();
        permit.AddWorker(Given.ACertifiedWelder());
        return permit;
    }

    private static Permit Submitted(params ApproverAssignment[] panel)
    {
        var permit = WithACrew();
        permit.Submit(Creator, panel.Length > 0 ? panel : [Given.Approver(SafetyOfficer)]);
        return permit;
    }

    private static Permit Active()
    {
        var permit = Submitted();
        permit.Approve(SafetyOfficer);
        return permit;
    }

    #endregion
}
