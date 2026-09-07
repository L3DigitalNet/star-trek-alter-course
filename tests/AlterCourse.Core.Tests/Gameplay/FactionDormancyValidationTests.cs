using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Protects consequential faction continuation without treating completion as permanent occupation.</summary>
public sealed class FactionDormancyValidationTests
{
    /// <summary>Rejects a removed decision wake when a candidate can act now or at a known release boundary.</summary>
    [Theory]
    [InlineData("idle")]
    [InlineData("hold")]
    [InlineData("travel")]
    public void MissingWakeCannotSilentlyDisablePendingObjective(string activity)
    {
        SimulationState state = CreateState(activity);
        FactionState faction = state.Factions[0];
        (SimulationScheduler scheduler, bool removed) = state.Scheduler.Cancel(faction.PendingDecisionWake!.WorkId);
        Assert.True(removed);
        SimulationState corrupted = state.ReplaceFaction(faction.Id, faction with { PendingDecisionWake = null }) with
        {
            Scheduler = scheduler,
        };

        Assert.Throws<InvalidOperationException>(() =>
            corrupted.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    /// <summary>Retains genuine no-action dormancy when no route or future release can enable assignment.</summary>
    [Fact]
    public void UnreachableObjectiveMayRemainDormant()
    {
        SimulationState state = CreateState("idle", unreachable: true);

        SimulationState dormant = GameSimulation
            .AdvanceTo(state, state.Time, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;

        Assert.Equal(FactionObjectiveStatus.Pending, dormant.Factions[0].PresenceObjective!.Status);
        Assert.Null(dormant.Factions[0].PendingDecisionWake);
        dormant.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    /// <summary>Completion remains a durable consequence after the ship later undertakes ordinary travel.</summary>
    [Fact]
    public void CompletedPresenceDoesNotReserveTheShipAtTheTargetForever()
    {
        SimulationState completed = GameSimulation
            .AdvanceTo(
                CreateState("idle"),
                new SimulationTime(1000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Equal(FactionObjectiveStatus.Satisfied, completed.Factions[0].PresenceObjective!.Status);

        ShipTravelApplicationResult departing = GameSimulation.ApplyShipTravel(
            completed,
            new ShipTravelCommand(new ShipInstanceId(2), FactionTestWorld.Alpha)
        );

        Assert.Equal(TravelOutcome.Accepted, departing.Outcome);
        SimulationState continued = GameSimulation
            .AdvanceTo(
                departing.CandidateState,
                new SimulationTime(1100),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Equal(FactionObjectiveStatus.Satisfied, continued.Factions[0].PresenceObjective!.Status);
    }

    private static SimulationState CreateState(string activity, bool unreachable = false) =>
        FactionTestWorld
            .CreateBootstrap(
                [
                    new FactionStart(
                        FactionTestWorld.FactionA,
                        FactionTestWorld.DefinitionA,
                        unreachable ? FactionTestWorld.Gamma : FactionTestWorld.Beta
                    ),
                ],
                controlledShips: true,
                alternateControlled: false,
                firstNpcOrder: string.Equals(activity, "hold", StringComparison.Ordinal)
                    ? new HoldUntilOrderStart(new SimulationTime(500))
                    : null,
                firstNpcStrategic: string.Equals(activity, "travel", StringComparison.Ordinal)
                    ? new TravelingStart(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationTime(0))
                    : null
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
}
