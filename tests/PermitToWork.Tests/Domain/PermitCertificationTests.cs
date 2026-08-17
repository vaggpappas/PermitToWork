using FluentAssertions;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using Xunit;

namespace PermitToWork.Tests.Domain;

/// <summary>
/// The hard block: a permit refuses anybody who is not qualified for the work it authorises.
/// This is the rule that makes the whole certification model earn its place, so it gets its
/// own file.
/// </summary>
public class PermitCertificationTests
{
    private static readonly Guid Creator = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid Receiver = Guid.Parse("c0000000-0000-0000-0000-000000000002");

    [Fact]
    public void Permit_AcceptsWorker_When_TheyHoldTheRequiredCertification()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);
        var welder = Given.ACertifiedWelder();

        permit.AddWorker(welder);

        permit.Workers.Should().ContainSingle(w => w.EmployeeId == welder.Id);
    }

    [Fact]
    public void Permit_RefusesWorker_When_TheyHoldNoSuchCertification()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);
        var labourer = Given.AnUncertifiedWorker();

        var add = () => permit.AddWorker(labourer);

        add.Should().Throw<DomainException>()
            .WithMessage("*does not hold a valid Hot Work certification*");
        permit.Workers.Should().BeEmpty();
    }

    [Fact]
    public void Permit_RefusesWorker_When_TheirCertificationLapsesPartWayThrough()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);

        // Qualified on the first morning of the job, not on the last. A check against the
        // start date alone would wave this person through, and they would spend the third
        // day doing hot work on an expired ticket.
        var welder = Given.ACertifiedWelder(expiresOn: new DateOnly(2026, 9, 2));

        var add = () => permit.AddWorker(welder);

        add.Should().Throw<DomainException>()
            .WithMessage("*covering the whole permit period*");
    }

    [Fact]
    public void Permit_AcceptsAnybody_When_ItRequiresNoCertification()
    {
        // Cold work demands nothing, so the rule has to be capable of not firing as well as
        // of firing.
        var permit = Given.APermit(Creator, Receiver);
        var labourer = Given.AnUncertifiedWorker();

        permit.AddWorker(labourer);

        permit.Workers.Should().ContainSingle();
    }

    [Fact]
    public void Permit_RefusesTheSamePersonTwice()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);
        var welder = Given.ACertifiedWelder();
        permit.AddWorker(welder);

        var addAgain = () => permit.AddWorker(welder);

        addAgain.Should().Throw<DomainException>().WithMessage("*already on this permit*");
    }

    [Fact]
    public void Permit_RefusesWorker_When_TheyAreNotActivelyEmployed()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);
        var welder = Given.ACertifiedWelder();
        welder.Suspend();

        var add = () => permit.AddWorker(welder);

        add.Should().Throw<DomainException>().WithMessage("*not an active employee*");
    }

    [Fact]
    public void Permit_KeepsItsRequirements_EvenIfTheRulesLaterChange()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);

        // The requirement was copied onto the permit when it was raised. Nothing about the
        // PermitType lookup can reach back and alter what this permit demanded — which is
        // the whole point of taking a snapshot.
        permit.RequiredCertifications.Should().ContainSingle();
        permit.RequiredCertifications[0].Name.Should().Be("Hot Work");
        permit.RequiredCertifications[0].CertificationTypeId.Should().Be(Given.HotWorkCertificationTypeId);
    }

    [Fact]
    public void Permit_RemovesWorker()
    {
        var permit = Given.AHotWorkPermit(Creator, Receiver);
        var welder = Given.ACertifiedWelder();
        permit.AddWorker(welder);

        permit.RemoveWorker(welder.Id, Creator);

        permit.Workers.Should().BeEmpty();
    }
}
