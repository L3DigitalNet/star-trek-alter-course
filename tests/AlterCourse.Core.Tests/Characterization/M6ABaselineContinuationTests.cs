using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Ships;
using static AlterCourse.Core.Tests.Characterization.M6ABaselineRecords;
using Expected = AlterCourse.Core.Tests.Characterization.M6ABaselineExpectations;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// Proves save-at-T-then-load continues identically to the uninterrupted run, by outcome-record equality.
/// </summary>
/// <remarks>
/// The checkpoint holds every kind of pending ship-system work at once — an active repair, an in-flight scan,
/// player weapon cooldown, and a pending defensive stimulus — so no pending clock can be dropped or re-based by
/// a save/load without a record differing. Comparison uses an exact probe (no rounding) and records every
/// fixed step, not only the endpoint, so a transient divergence that later reconverges still fails.
/// </remarks>
public sealed class M6ABaselineContinuationTests
{
    private const int HorizonSteps = 80;

    /// <summary>Pins the mixed checkpoint and proves V9 save/load continuation equals the uninterrupted run.</summary>
    [Fact]
    public void MixedCheckpointContinuesIdenticallyAfterSaveAndLoad()
    {
        var exact = new M6ABaselineScenarios(exact: true);
        GameSimulation uninterrupted = exact.MixedCheckpoint();
        // Round trip through the real persistence API (GamePersistence Serialize then Deserialize).
        GameSimulation resumed = exact.Fixture.RoundTrip(uninterrupted);

        var pinned = new M6ABaselineScenarios();
        ShipOutcome checkpointPlayer = pinned.Probe.Player(uninterrupted);
        Assert.NotNull(checkpointPlayer.Engineering.Repair);
        Assert.NotNull(checkpointPlayer.ActiveScan);
        Assert.True(checkpointPlayer.Combat.WeaponReadyAtMs > Expected.ContinuationCheckpointMs);
        Assert.NotNull(pinned.Probe.Ship(uninterrupted, M6ABaselineScenarios.Defender).Combat.Stimulus);
        Assert.Equal(Expected.ContinuationCheckpointPlayer, checkpointPlayer);
        Assert.Equal(
            Expected.ContinuationCheckpointDefenderCombat,
            pinned.Probe.Ship(uninterrupted, M6ABaselineScenarios.Defender).Combat
        );
        Assert.Equal(exact.Probe.AllShips(uninterrupted), exact.Probe.AllShips(resumed));

        OutcomeSequence<StepOutcome> first = exact.Continue(uninterrupted, HorizonSteps);
        OutcomeSequence<StepOutcome> second = exact.Continue(resumed, HorizonSteps);
        Assert.Equal(first, second);

        Assert.Equal(Expected.ContinuationEvents, Sequence([.. first.Items.SelectMany(step => step.Events.Items)]));
        Assert.Equal(Expected.ReturnFireDecision, pinned.Probe.Defense(uninterrupted));
        Assert.Equal(Expected.ContinuationFinalPlayer, pinned.Probe.Player(uninterrupted));

        // Readiness survives too: both runs accept the same next shot with the same visible consequences.
        long contact = M6ABaselineProbe.ContactOf(
            uninterrupted,
            M6ABaselineScenarios.Player,
            M6ABaselineScenarios.Defender
        );
        FireDirectedEnergyResult shot = M6ABaselineProbe.Fire(uninterrupted, contact, ShipSystemKind.Sensors);
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, shot.Outcome);
        Assert.Equal(shot, M6ABaselineProbe.Fire(resumed, contact, ShipSystemKind.Sensors));
        Assert.Equal(exact.Probe.AllShips(uninterrupted), exact.Probe.AllShips(resumed));
    }
}
