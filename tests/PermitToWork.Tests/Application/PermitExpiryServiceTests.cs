using FluentAssertions;
using NSubstitute;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.Permits;
using PermitToWork.Domain.Permits;
using PermitToWork.Domain.ValueObjects;
using Xunit;

namespace PermitToWork.Tests.Application;

/// <summary>
/// The sweep that makes <see cref="PermitStatus.Expired"/> reachable. What is worth testing
/// here is not that a permit expires — the aggregate already proves that — but that the
/// service asks every candidate, counts honestly, and does not write when it has nothing
/// to say.
/// </summary>
public class PermitExpiryServiceTests
{
    private static readonly Guid Creator = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid Receiver = Guid.Parse("e0000000-0000-0000-0000-000000000002");
    private static readonly Guid Approver = Guid.Parse("e0000000-0000-0000-0000-000000000003");

    private readonly IPermitRepository _permits = Substitute.For<IPermitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PermitExpiryService _service;

    public PermitExpiryServiceTests()
    {
        _service = new PermitExpiryService(_permits, _unitOfWork);
    }

    [Fact]
    public async Task ExpirySweep_ExpiresPermitsPastTheirWindow()
    {
        var stale = ActivePermitEndingInThePast();
        _permits.FindElapsedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([stale]);

        var expired = await _service.ExpireElapsedAsync();

        expired.Should().Be(1);
        stale.Status.Should().Be(PermitStatus.Expired);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpirySweep_DoesNotSave_When_NothingHasElapsed()
    {
        _permits.FindElapsedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([]);

        var expired = await _service.ExpireElapsedAsync();

        // Every quarter of an hour, for ever. A sweep that opened a transaction each time it
        // found nothing would be the noisiest thing in the log.
        expired.Should().Be(0);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpirySweep_LeavesFinishedPermitsAlone()
    {
        var stale = ActivePermitEndingInThePast();
        stale.Close(Creator);

        _permits.FindElapsedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([stale]);

        var expired = await _service.ExpireElapsedAsync();

        // The aggregate refuses, so a permit closed between the query and the sweep is not
        // quietly rewritten as Expired. The count reflects what actually changed.
        expired.Should().Be(0);
        stale.Status.Should().Be(PermitStatus.Closed);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpirySweep_CountsOnlyWhatItChanged()
    {
        var stale = ActivePermitEndingInThePast();
        var alsoStale = ActivePermitEndingInThePast();
        var finished = ActivePermitEndingInThePast();
        finished.Close(Creator);

        _permits
            .FindElapsedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([stale, alsoStale, finished]);

        var expired = await _service.ExpireElapsedAsync();

        expired.Should().Be(2);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>An active permit whose window closed yesterday — the case the sweep exists for.</summary>
    private static Permit ActivePermitEndingInThePast()
    {
        var window = DateTimeRange.Create(
            DateTimeOffset.UtcNow.AddDays(-3),
            DateTimeOffset.UtcNow.AddDays(-1));

        var permit = Given.APermit(Creator, Receiver, validity: window);
        permit.AddWorker(Given.AnUncertifiedWorker());
        permit.Submit(Creator, [Given.DecisiveApprover(Approver)]);
        permit.Approve(Approver);

        return permit;
    }
}
