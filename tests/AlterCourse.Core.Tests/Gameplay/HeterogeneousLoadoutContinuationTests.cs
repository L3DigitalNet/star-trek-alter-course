using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>
/// Verifies that heterogeneous same-class ships keep their actual installations and every in-flight specialized
/// continuation across a V10 save and a load under a compatibly changed class default, and that the resumed world
/// continues exactly like the uninterrupted one.
/// </summary>
public sealed class HeterogeneousLoadoutContinuationTests
{
    private const int ContinuationSteps = 80;

    private readonly HeterogeneousCombatWorld _world = new();

    /// <summary>
    /// A save taken with repair, scan, cooldown, and stimulus all active loads under a design default that no longer
    /// installs shields or weapons, yet every ship keeps exactly its saved installations and the re-captured save is
    /// byte-identical.
    /// </summary>
    [Fact]
    public void CompatibleClassDefaultChangeLeavesEverySavedInstallationAndContinuationIntact()
    {
        GameSimulation game = _world.Engaged();
        AssertAllContinuationsActive(game.CaptureState());
        byte[] saved = GamePersistence.Serialize(game, HeterogeneousCombatWorld.Metadata);
        ShipDefinitionCatalog changedDefault = ChangedDefault();
        Assert.Equal(3, changedDefault.GetRequired(new("pathfinder")).InitialLoadout.Systems.Count);

        GameSimulation resumed = GamePersistence
            .Deserialize(saved, changedDefault, "heterogeneous-continuation.json")
            .Simulation;

        SimulationState before = game.CaptureState();
        SimulationState after = resumed.CaptureState();
        foreach (ShipState ship in before.Ships)
        {
            ShipState loaded = after.GetRequiredShip(ship.InstanceId);
            Assert.Equal(ship.Engineering, loaded.Engineering);
            Assert.Equal(ship.Combat, loaded.Combat);
            Assert.Equal(ship.SensorKnowledge.NextContactId, loaded.SensorKnowledge.NextContactId);
            Assert.Equal(ship.SensorKnowledge.Contacts.ToArray(), loaded.SensorKnowledge.Contacts.ToArray());
            Assert.Equal(ship.SensorKnowledge.ActiveScan, loaded.SensorKnowledge.ActiveScan);
        }

        // The defender took the old five-system default at bootstrap; the changed default must not strip it.
        Assert.Equal(5, after.GetRequiredShip(HeterogeneousCombatWorld.Defender).Engineering.Systems.Count);
        Assert.Equal(
            [3L, 17L, 40L, 41L],
            after
                .GetRequiredShip(HeterogeneousCombatWorld.Player)
                .Engineering.Systems.ByIdentity.Select(system => system.Id.Value)
        );
        Assert.Equal(
            HeterogeneousCombatWorld.PlayerNextInstalledSystemId,
            after.GetRequiredShip(HeterogeneousCombatWorld.Player).Engineering.InstallationIds.NextId
        );
        Assert.Equal(saved, GamePersistence.Serialize(resumed, HeterogeneousCombatWorld.Metadata));
    }

    /// <summary>
    /// The resumed world (loaded under the changed default) and the uninterrupted world produce identical traces,
    /// player events, scheduler identity, and V10 bytes while repair, scan, cooldown, and stimulus all resolve.
    /// </summary>
    [Fact]
    public void ResumedWorldContinuesExactlyLikeUninterruptedWorld()
    {
        GameSimulation game = _world.Engaged();
        ShipDefinitionCatalog changedDefault = ChangedDefault();
        GameSimulation resumed = GamePersistence
            .Deserialize(
                GamePersistence.Serialize(game, HeterogeneousCombatWorld.Metadata),
                changedDefault,
                "heterogeneous-continuation.json"
            )
            .Simulation;
        SimulationTime target = game.CaptureState()
            .Time.AdvanceBy(new SimulationDuration(ContinuationSteps * HeterogeneousCombatWorld.StepMilliseconds));

        SimulationAdvanceTraceResult uninterrupted = GameSimulation.AdvanceTo(
            game.CaptureState(),
            target,
            _world.Catalog
        );
        SimulationAdvanceTraceResult continued = GameSimulation.AdvanceTo(
            resumed.CaptureState(),
            target,
            changedDefault
        );

        Assert.Equal(uninterrupted.Traces.ToArray(), continued.Traces.ToArray());
        Assert.Equal(uninterrupted.PlayerEvents.ToArray(), continued.PlayerEvents.ToArray());
        Assert.Equal(uninterrupted.State.Scheduler.NextWorkId, continued.State.Scheduler.NextWorkId);
        Assert.Equal(uninterrupted.State.Scheduler.NextSequence, continued.State.Scheduler.NextSequence);
        Assert.Equal(
            uninterrupted.State.Scheduler.OutstandingWork.ToArray(),
            continued.State.Scheduler.OutstandingWork.ToArray()
        );
        Assert.Equal(
            GamePersistence.Serialize(_world.Restore(uninterrupted.State), HeterogeneousCombatWorld.Metadata),
            GamePersistence.Serialize(
                GameSimulation.RestoreState(continued.State, changedDefault),
                HeterogeneousCombatWorld.Metadata
            )
        );
        AssertAllContinuationsResolved(continued);
    }

    /// <summary>Rejected commands on the loaded heterogeneous world leave its V10 bytes, and so every counter, unchanged.</summary>
    [Fact]
    public void RejectedCommandsLeaveResumedV10BytesUnchanged()
    {
        GameSimulation resumed = GamePersistence
            .Deserialize(
                GamePersistence.Serialize(_world.Engaged(), HeterogeneousCombatWorld.Metadata),
                ChangedDefault(),
                "heterogeneous-continuation.json"
            )
            .Simulation;
        byte[] before = GamePersistence.Serialize(resumed, HeterogeneousCombatWorld.Metadata);

        // The active impulse repair occupies the ship's repair slot, so the outcome order (design §5.1) refuses even
        // a foreign id first as RepairAlreadyActive; ForeignInstalledIdIsUnknownOnThePlayerShipAfterLoad covers it
        // once the slot is free.
        Assert.Equal(
            SystemRepairOutcome.RepairAlreadyActive,
            resumed.BeginSystemRepair(new InstalledSystemId(4), new SystemCondition(1)).Outcome
        );
        PowerAllocationResult generator = resumed.ApplyPriorityAllocation(HeterogeneousCombatWorld.PlayerGenerator);
        Assert.Equal(PowerAllocationOutcome.UnknownConsumer, generator.Outcome);
        PowerAllocationResult foreign = resumed.SetPowerAllocation(
            new PowerAllocation([
                new(HeterogeneousCombatWorld.PlayerSensors, new PowerUnits(60)),
                new(HeterogeneousCombatWorld.PlayerImpulse, new PowerUnits(20)),
                new(HeterogeneousCombatWorld.PlayerWeapons, new PowerUnits(30)),
                new(new InstalledSystemId(4), new PowerUnits(0)),
            ])
        );
        Assert.Equal(PowerAllocationOutcome.UnknownConsumer, foreign.Outcome);
        Assert.Equal(new InstalledSystemId(4), foreign.Consumer);
        PowerAllocationResult incomplete = resumed.SetPowerAllocation(
            new PowerAllocation([new(HeterogeneousCombatWorld.PlayerSensors, new PowerUnits(60))])
        );
        Assert.Equal(PowerAllocationOutcome.IncompleteAllocation, incomplete.Outcome);
        Assert.Equal(
            ActiveSensorScanOutcome.AlreadyIdentified,
            resumed
                .RequestActiveSensorScan(HeterogeneousCombatWorld.ContactOf(resumed, HeterogeneousCombatWorld.Defender))
                .Outcome
        );

        Assert.Equal(before, GamePersistence.Serialize(resumed, HeterogeneousCombatWorld.Metadata));
    }

    /// <summary>After the repair completes, ownership refuses a repair of an id that only another ship installs.</summary>
    [Fact]
    public void ForeignInstalledIdIsUnknownOnThePlayerShipAfterLoad()
    {
        GameSimulation game = _world.Engaged();
        game.AdvanceFixedSteps(ContinuationSteps);
        GameSimulation resumed = GamePersistence
            .Deserialize(
                GamePersistence.Serialize(game, HeterogeneousCombatWorld.Metadata),
                ChangedDefault(),
                "after-repair.json"
            )
            .Simulation;
        Assert.Null(resumed.CaptureState().GetRequiredShip(HeterogeneousCombatWorld.Player).Engineering.ActiveRepair);
        byte[] before = GamePersistence.Serialize(resumed, HeterogeneousCombatWorld.Metadata);

        Assert.Equal(
            SystemRepairOutcome.UnknownSystem,
            resumed.BeginSystemRepair(new InstalledSystemId(4), new SystemCondition(1)).Outcome
        );
        Assert.Equal(
            SystemRepairOutcome.NotRepairable,
            resumed.BeginSystemRepair(HeterogeneousCombatWorld.PlayerGenerator, new SystemCondition(1)).Outcome
        );
        Assert.Equal(before, GamePersistence.Serialize(resumed, HeterogeneousCombatWorld.Metadata));
    }

    private ShipDefinitionCatalog ChangedDefault() =>
        _world.WithDefaultLoadout(definition =>
            definition.Kind != ShipSystemKind.Shields && definition.Kind != ShipSystemKind.DirectedEnergyWeapons
        );

    private static void AssertAllContinuationsActive(SimulationState state)
    {
        ShipState player = state.GetRequiredShip(HeterogeneousCombatWorld.Player);
        Assert.Equal(HeterogeneousCombatWorld.PlayerImpulse, player.Engineering.ActiveRepair!.Target);
        Assert.Equal(HeterogeneousCombatWorld.PlayerSensors, player.SensorKnowledge.ActiveScan!.Sensor);
        Assert.Equal(
            new SimulationDuration(1500),
            new SimulationDuration(
                player.SensorKnowledge.ActiveScan.ExpectedCompletion.Milliseconds
                    - player.SensorKnowledge.ActiveScan.StartedAt.Milliseconds
            )
        );
        Assert.True(
            player.Combat.ReadinessOf(HeterogeneousCombatWorld.PlayerWeapons)!.ReadyAt.Milliseconds
                > state.Time.Milliseconds
        );
        Assert.NotNull(state.GetRequiredShip(HeterogeneousCombatWorld.Defender).Combat.PendingStimulus);
        Assert.Empty(player.Engineering.Systems.OfKind(ShipSystemKind.Shields));
    }

    private static void AssertAllContinuationsResolved(SimulationAdvanceTraceResult continued)
    {
        ShipState player = continued.State.GetRequiredShip(HeterogeneousCombatWorld.Player);
        Assert.Null(player.Engineering.ActiveRepair);
        Assert.Equal(1, player.Engineering.Systems.GetRequired(HeterogeneousCombatWorld.PlayerImpulse).Condition.Value);
        Assert.Null(player.SensorKnowledge.ActiveScan);
        Assert.Null(continued.State.GetRequiredShip(HeterogeneousCombatWorld.Defender).Combat.PendingStimulus);
        // The defender's return fire lands on the player's actual weapon installation (41), proving the receiver is
        // resolved against the saved loadout rather than a class-default slot.
        Assert.True(player.Engineering.Systems.GetRequired(HeterogeneousCombatWorld.PlayerWeapons).Condition.Value < 1);
        Assert.Contains(
            continued.PlayerEvents,
            item =>
                item.Kind == PlayerAdvanceEventKind.OwnSystemDamaged
                && item.InstalledSystemId == HeterogeneousCombatWorld.PlayerWeapons
        );
    }
}
