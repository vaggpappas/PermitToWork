using FluentAssertions;
using NSubstitute;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Common;
using PermitToWork.Application.Teams;
using PermitToWork.Domain.Organization;
using Xunit;

namespace PermitToWork.Tests.Application;

public class TeamServiceTests
{
    private readonly ITeamRepository _teams = Substitute.For<ITeamRepository>();
    private readonly IEmployeeRepository _employees = Substitute.For<IEmployeeRepository>();
    private readonly IReferenceDataRepository _referenceData = Substitute.For<IReferenceDataRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly TeamService _service;
    private readonly Employee _leader = Given.AnEmployee();

    public TeamServiceTests()
    {
        _service = new TeamService(_teams, _employees, _referenceData, _unitOfWork);

        _referenceData.FacilityExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _employees.FindAsync(_leader.Id, Arg.Any<CancellationToken>()).Returns(_leader);
        _teams
            .NextCodeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("MEC-2026-0001");
    }

    [Fact]
    public async Task TeamService_CreatesTeamWithItsLeader()
    {
        Team? added = null;
        _teams.Add(Arg.Do<Team>(t => added = t));

        await _service.CreateAsync(ARequest());

        added.Should().NotBeNull();

        // The code is generated, never supplied — CreateTeamRequest has no field for one.
        added!.Code.Should().Be("MEC-2026-0001");

        // The point of requiring a leader up front: a team is never member-less, not even
        // for the instant between two calls. An empty team would be invisible to the
        // contractor who created it, because team visibility runs through membership.
        added!.Memberships.Should().ContainSingle();
        added.Memberships.Single().Role.Should().Be(TeamRole.Leader);
        added.Memberships.Single().EmployeeId.Should().Be(_leader.Id);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TeamService_RejectsUnknownFacility()
    {
        _referenceData.FacilityExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var create = async () => await _service.CreateAsync(ARequest());

        await create.Should().ThrowAsync<NotFoundException>().WithMessage("*Facility*");
    }

    [Fact]
    public async Task TeamService_RejectsLeaderTheCallerCannotSee()
    {
        var strangerId = Guid.CreateVersion7();
        _employees.FindAsync(strangerId, Arg.Any<CancellationToken>()).Returns((Employee?)null);

        var create = async () => await _service.CreateAsync(ARequest() with { LeaderEmployeeId = strangerId });

        // The employee repository is company-scoped, so a contractor naming somebody from
        // another company gets the same answer as for an id that does not exist.
        await create.Should().ThrowAsync<NotFoundException>().WithMessage("*Employee*");
    }

    [Fact]
    public async Task TeamService_ReportsNotFound_When_TeamIsInvisible()
    {
        _teams.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Team?)null);

        var addMember = async () => await _service.AddMemberAsync(
            Guid.CreateVersion7(),
            new AddTeamMemberRequest { EmployeeId = _leader.Id });

        await addMember.Should().ThrowAsync<NotFoundException>().WithMessage("*Team*");
    }

    [Fact]
    public async Task TeamService_DoesNotSave_When_TheAggregateRefuses()
    {
        var team = Given.ATeam();
        team.AddMember(_leader.Id, TeamRole.Leader, Given.Today);
        _teams.FindAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

        var joinTwice = async () => await _service.AddMemberAsync(
            team.Id,
            new AddTeamMemberRequest { EmployeeId = _leader.Id, JoinedOn = Given.Today });

        await joinTwice.Should().ThrowAsync<Exception>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private CreateTeamRequest ARequest() => new()
    {
        Name = "Mechanical Crew A",
        FacilityId = Given.FacilityId,
        LeaderEmployeeId = _leader.Id
    };
}
