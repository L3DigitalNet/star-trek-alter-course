using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Simulation;

/// <summary>Verifies closed ship-or-faction scheduled-work targeting.</summary>
public sealed class FactionSchedulerTargetTests
{
    /// <summary>Confirms persisted enum identities remain stable and the two target domains stay distinct.</summary>
    [Fact]
    public void TargetAndWorkKindNumericIdentitiesAreStable()
    {
        Assert.Equal(1, (int)ScheduledWorkTargetKind.Ship);
        Assert.Equal(2, (int)ScheduledWorkTargetKind.Faction);
        Assert.Equal(7, (int)ScheduledWorkKind.FactionDecisionWake);

        var ship = ScheduledWorkTarget.ForShip(new ShipInstanceId(41));
        var faction = ScheduledWorkTarget.ForFaction(new FactionId(41));

        Assert.NotEqual(ship, faction);
        Assert.Equal(new ShipInstanceId(41), ship.ShipId);
        Assert.Null(ship.FactionId);
        Assert.Equal(new FactionId(41), faction.FactionId);
        Assert.Null(faction.ShipId);
    }

    /// <summary>Confirms identity and target factories reject their uninitialized values.</summary>
    [Fact]
    public void IdentitiesAndTargetsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FactionId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FactionId(-1));
        Assert.Throws<ArgumentException>(() => ScheduledWorkTarget.ForShip(default));
        Assert.Throws<ArgumentException>(() => ScheduledWorkTarget.ForFaction(default));
        Assert.Throws<ArgumentException>(() =>
            new ScheduledWork(
                new ScheduledWorkId(1),
                new SimulationTime(100),
                0,
                default(ScheduledWorkTarget),
                ScheduledWorkKind.TravelArrival
            )
        );
    }

    /// <summary>Confirms every known work kind accepts only its settled target domain.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ShipWorkKindsRejectFactionTargets(int numericKind)
    {
        var kind = (ScheduledWorkKind)numericKind;
        Assert.Throws<ArgumentException>(() => Work(1, 0, Faction(1), kind));
    }

    /// <summary>Confirms faction decisions reject ship targets and unknown kinds remain closed.</summary>
    [Fact]
    public void FactionWorkRejectsShipTargetsAndUnknownKinds()
    {
        Assert.Throws<ArgumentException>(() => Work(1, 0, Ship(1), ScheduledWorkKind.FactionDecisionWake));
        Assert.Throws<ArgumentOutOfRangeException>(() => Work(1, 0, Faction(1), (ScheduledWorkKind)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Work(1, 0, Faction(1), (ScheduledWorkKind)999));
    }

    /// <summary>Confirms mixed targets round-trip with same-time sequence ordering independent of restore input order.</summary>
    [Fact]
    public void MixedTargetsRoundTripInPersistedSequenceOrder()
    {
        ScheduledWork ship = Work(3, 8, Ship(19), ScheduledWorkKind.OrderWake);
        ScheduledWork faction = Work(4, 9, Faction(19), ScheduledWorkKind.FactionDecisionWake);

        var restored = SimulationScheduler.Restore(5, 10, [faction, ship]);

        Assert.Equal(new[] { ship, faction }, restored.OutstandingWork);
        Assert.Equal(Ship(19), restored.OutstandingWork[0].Target);
        Assert.Equal(Faction(19), restored.OutstandingWork[1].Target);
        Assert.Equal(new ShipInstanceId(19), restored.OutstandingWork[0].TargetShipId);
        Assert.Throws<InvalidOperationException>(() => _ = restored.OutstandingWork[1].TargetShipId);
    }

    /// <summary>Confirms exact correlation includes target domain and cancellation still removes only the stable identity.</summary>
    [Fact]
    public void ContainsExactAndCancelPreserveTargetDomain()
    {
        (SimulationScheduler afterShip, ScheduledWork ship) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(100), new ShipInstanceId(7), ScheduledWorkKind.OrderWake);
        (SimulationScheduler scheduled, ScheduledWork faction) = afterShip.Schedule(
            new SimulationTime(100),
            Faction(7),
            ScheduledWorkKind.FactionDecisionWake
        );

        Assert.True(
            scheduled.ContainsExact(faction.Id, Faction(7), faction.DueTime, ScheduledWorkKind.FactionDecisionWake)
        );
        Assert.False(
            scheduled.ContainsExact(faction.Id, Ship(7), faction.DueTime, ScheduledWorkKind.FactionDecisionWake)
        );
        Assert.True(scheduled.ContainsExact(ship.Id, new ShipInstanceId(7), ship.DueTime, ScheduledWorkKind.OrderWake));

        (SimulationScheduler following, bool removed) = scheduled.Cancel(faction.Id);

        Assert.True(removed);
        Assert.Equal(new[] { ship }, following.OutstandingWork);
    }

    /// <summary>Confirms restore rejects default work and target-kind mismatches cannot enter through scheduling.</summary>
    [Fact]
    public void RestoreAndScheduleFailClosed()
    {
        Assert.Throws<ArgumentException>(() => SimulationScheduler.Restore(2, 1, [default]));
        Assert.Throws<ArgumentException>(() =>
            SimulationScheduler
                .Create()
                .Schedule(new SimulationTime(100), default(ScheduledWorkTarget), ScheduledWorkKind.FactionDecisionWake)
        );
        Assert.Throws<ArgumentException>(() =>
            SimulationScheduler.Create().Schedule(new SimulationTime(100), Faction(2), ScheduledWorkKind.TravelArrival)
        );
    }

    /// <summary>Confirms the bounded scheduler reserves one independent wake for every supported faction.</summary>
    [Fact]
    public void OutstandingWorkBoundIncludesEveryFactionWake()
    {
        Assert.Equal(256, SimulationState.MaximumFactions);
        Assert.Equal(66_816, SimulationScheduler.MaximumOutstandingWork);
    }

    private static ScheduledWork Work(long id, long sequence, ScheduledWorkTarget target, ScheduledWorkKind kind) =>
        new(new ScheduledWorkId(id), new SimulationTime(500), sequence, target, kind);

    private static ScheduledWorkTarget Ship(long value) => ScheduledWorkTarget.ForShip(new ShipInstanceId(value));

    private static ScheduledWorkTarget Faction(long value) => ScheduledWorkTarget.ForFaction(new FactionId(value));
}
