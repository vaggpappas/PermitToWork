using FluentAssertions;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.Organization;
using Xunit;

namespace PermitToWork.Tests.Domain;

/// <summary>
/// Team membership. The rules worth protecting here are the ones that span more than one
/// membership — one leader at a time, no joining twice — because those are exactly the
/// ones that cannot be enforced by a database constraint or by a caller being careful.
/// </summary>
public class TeamTests
{
    private static readonly Guid Nadia = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Luis = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Marta = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    private static readonly DateOnly January = new(2026, 1, 1);
    private static readonly DateOnly June = new(2026, 6, 1);

    #region Joining and leaving

    [Fact]
    public void Team_StartsActive()
    {
        Given.ATeam().Status.Should().Be(TeamStatus.Active);
    }

    [Fact]
    public void Team_AddsMember()
    {
        var team = Given.ATeam();

        team.AddMember(Nadia, TeamRole.Member, January);

        team.HasActiveMember(Nadia, January).Should().BeTrue();
        team.ActiveMembershipsOn(January).Should().HaveCount(1);
    }

    [Fact]
    public void Team_RejectsDuplicateActiveMember()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Member, January);

        var joinTwice = () => team.AddMember(Nadia, TeamRole.Deputy, June);

        joinTwice.Should().Throw<DomainException>()
            .WithMessage("*already an active member*");
    }

    [Fact]
    public void Team_AllowsRejoin_When_PreviousMembershipEnded()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Member, January);
        team.RemoveMember(Nadia, June);

        var rejoin = () => team.AddMember(Nadia, TeamRole.Member, June);

        // Leaving is a half-open interval: June is the first day she is not a member, so
        // she can rejoin the same day without the two spells overlapping.
        rejoin.Should().NotThrow();
        team.Memberships.Should().HaveCount(2);
    }

    [Fact]
    public void Team_KeepsHistory_When_MemberLeaves()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Member, January);

        team.RemoveMember(Nadia, June);

        // The row survives; it gains a leaving date. Who was on the crew in March is the
        // first question an incident investigation asks.
        team.Memberships.Should().HaveCount(1);
        team.HasActiveMember(Nadia, new DateOnly(2026, 3, 1)).Should().BeTrue();
        team.HasActiveMember(Nadia, June).Should().BeFalse();
    }

    [Fact]
    public void Team_RejectsRemovalOfNonMember()
    {
        var team = Given.ATeam();

        var removeStranger = () => team.RemoveMember(Luis, June);

        removeStranger.Should().Throw<DomainException>()
            .WithMessage("*not an active member*");
    }

    [Fact]
    public void TeamMembership_IsActiveOnTheJoinDate()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Member, January);

        team.HasActiveMember(Nadia, January).Should().BeTrue();
        team.HasActiveMember(Nadia, January.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void TeamMembership_IsNotActiveOnTheLeavingDate()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Member, January);
        team.RemoveMember(Nadia, June);

        team.HasActiveMember(Nadia, June.AddDays(-1)).Should().BeTrue();
        team.HasActiveMember(Nadia, June).Should().BeFalse();
    }

    #endregion

    #region Leadership

    [Fact]
    public void Team_ReportsItsLeader()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);
        team.AddMember(Luis, TeamRole.Member, January);

        team.LeaderOn(January)!.EmployeeId.Should().Be(Nadia);
    }

    [Fact]
    public void Team_HasNoLeader_When_NobodyLeads()
    {
        var team = Given.ATeam();
        team.AddMember(Luis, TeamRole.Member, January);

        team.LeaderOn(January).Should().BeNull();
    }

    [Fact]
    public void Team_RejectsSecondLeader()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);

        var secondLeader = () => team.AddMember(Luis, TeamRole.Leader, January);

        // The leader is derived from the memberships rather than stored on the team, so
        // this rule is the only thing keeping that derivation single-valued.
        secondLeader.Should().Throw<DomainException>()
            .WithMessage("*already has a leader*");
    }

    [Fact]
    public void Team_AllowsNewLeader_When_ThePreviousOneLeft()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);
        team.RemoveMember(Nadia, June);

        var promote = () => team.AddMember(Luis, TeamRole.Leader, June);

        promote.Should().NotThrow();
        team.LeaderOn(June)!.EmployeeId.Should().Be(Luis);
    }

    [Fact]
    public void Team_ChangesMemberRole()
    {
        var team = Given.ATeam();
        team.AddMember(Luis, TeamRole.Member, January);

        team.ChangeMemberRole(Luis, TeamRole.Deputy, June);

        team.ActiveMembershipsOn(June).Single().Role.Should().Be(TeamRole.Deputy);
    }

    [Fact]
    public void Team_RejectsPromotionToLeader_When_SomebodyElseLeads()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);
        team.AddMember(Luis, TeamRole.Member, January);

        var promoteSecond = () => team.ChangeMemberRole(Luis, TeamRole.Leader, June);

        promoteSecond.Should().Throw<DomainException>()
            .WithMessage("*already has a leader*");
    }

    [Fact]
    public void Team_AcceptsPromotion_When_MemberIsAlreadyTheLeader()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);

        var noop = () => team.ChangeMemberRole(Nadia, TeamRole.Leader, June);

        // Setting the leader to the person who already leads must not trip the
        // "already has a leader" check against themselves.
        noop.Should().NotThrow();
        team.LeaderOn(June)!.EmployeeId.Should().Be(Nadia);
    }

    [Fact]
    public void Team_RejectsRoleChangeForNonMember()
    {
        var team = Given.ATeam();

        var changeStranger = () => team.ChangeMemberRole(Marta, TeamRole.Deputy, June);

        changeStranger.Should().Throw<DomainException>()
            .WithMessage("*not an active member*");
    }

    #endregion

    #region Disbanding

    [Fact]
    public void Team_Disbands_AndEndsEveryMembership()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);
        team.AddMember(Luis, TeamRole.Member, January);

        team.Disband(June);

        team.Status.Should().Be(TeamStatus.Disbanded);
        team.ActiveMembershipsOn(June).Should().BeEmpty();
        team.Memberships.Should().OnlyContain(m => m.LeftOn == June);
    }

    [Fact]
    public void Team_RejectsNewMembers_When_Disbanded()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);
        team.Disband(June);

        var addAfterwards = () => team.AddMember(Luis, TeamRole.Member, June);

        addAfterwards.Should().Throw<DomainException>()
            .WithMessage("*has been disbanded*");
    }

    [Fact]
    public void Team_RejectsSecondDisband()
    {
        var team = Given.ATeam();
        team.AddMember(Nadia, TeamRole.Leader, January);
        team.Disband(June);

        var disbandAgain = () => team.Disband(June);

        disbandAgain.Should().Throw<DomainException>()
            .WithMessage("*has been disbanded*");
    }

    #endregion
}
