using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Drives the production observation-response world and bounded recurring-contact proof worlds.</summary>
internal sealed class ObservationResponseProofFixture
{
    internal static readonly FactionId FactionA = new(1);
    internal static readonly FactionId FactionB = new(2);
    internal static readonly ShipInstanceId AObserver = new(5);
    internal static readonly ShipInstanceId AResponder = new(6);
    internal static readonly ShipInstanceId BObserver = new(2);
    internal static readonly ShipInstanceId BResponder = new(3);
    internal static readonly ShipInstanceId RecurringTarget = new(6);
    internal static readonly LocationId Vesper = new("vesper-reach");
    internal static readonly LocationId Meridian = new("meridian-drift");
    internal static readonly GameSaveMetadata Metadata = new(
        "observation-response-proof",
        "Observation Response Proof",
        new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero)
    );

    internal ObservationResponseProofFixture()
    {
        ShipCatalog = FactionBootstrapTests.FactionTestWorld.CreateShipCatalog("pathfinder");
        FactionCatalog = FactionBootstrapTests.FactionTestWorld.FactionCatalog;
    }

    internal ShipDefinitionCatalog ShipCatalog { get; }

    internal FactionDefinitionCatalog FactionCatalog { get; }

    internal GameSimulation CreateProduction() => FirstGameSetup.Create(ShipCatalog, FactionCatalog);

    internal SimulationAdvanceTraceResult AdvanceTo(SimulationState state, long milliseconds) =>
        GameSimulation.AdvanceTo(state, new SimulationTime(milliseconds), ShipCatalog, FactionCatalog);

    internal GameSimulation Restore(SimulationState state) =>
        GameSimulation.RestoreState(state, ShipCatalog, FactionCatalog);

    internal GameSimulation RoundTrip(GameSimulation simulation, string sourceName) =>
        GamePersistence
            .Deserialize(GamePersistence.Serialize(simulation, Metadata), ShipCatalog, FactionCatalog, sourceName)
            .Simulation;

    /// <summary>Moves an NPC through the ordinary typed travel application and scheduler arrival path.</summary>
    /// <remarks>
    /// Production exposes no player command for an NPC. The fixture uses the same validated travel application as
    /// faction assignment without manufacturing a faction order. Advancing one fixed step lets the observation pass
    /// make departure consequences authoritative before return.
    /// </remarks>
    internal SimulationState MoveNpcTo(SimulationState state, ShipInstanceId shipId, LocationId destination)
    {
        ShipTravelApplicationResult travel = GameSimulation.ApplyShipTravel(
            state,
            new ShipTravelCommand(shipId, destination)
        );
        Assert.Equal(TravelOutcome.Accepted, travel.Outcome);
        return AdvanceTo(
            travel.CandidateState,
            travel.CandidateState.Time.Milliseconds + SimulationFixedStep.Duration.Milliseconds
        ).State;
    }

    /// <summary>Creates two independently controlled observer/responder pairs and one neutral shuttle target.</summary>
    /// <remarks>
    /// The long-horizon driver moves only the neutral target through ordinary finite travel commands, then dwells
    /// beyond the contact-loss interval. Each return therefore creates a real Lost-to-Current episode rather than
    /// a fixture-authored report or contact.
    /// </remarks>
    internal GameSimulation CreateRecurringWorld()
    {
        LocationId alpha = FactionBootstrapTests.FactionTestWorld.Alpha;
        LocationId beta = FactionBootstrapTests.FactionTestWorld.Beta;
        LocationId gamma = FactionBootstrapTests.FactionTestWorld.Gamma;
        LocationId isolated = new("isolated");
        var map = new StrategicMap(
            [
                new StrategicLocation(alpha, "Alpha", default),
                new StrategicLocation(beta, "Beta", default),
                new StrategicLocation(gamma, "Gamma", default),
                new StrategicLocation(isolated, "Isolated", default),
            ],
            [
                new StrategicRoute(alpha, beta, new SimulationDuration(3000)),
                new StrategicRoute(beta, gamma, new SimulationDuration(3000)),
                new StrategicRoute(gamma, alpha, new SimulationDuration(3000)),
            ]
        );
        ShipStart[] ships =
        [
            CreateRecurringShip(1, isolated, null),
            CreateRecurringShip(2, alpha, FactionA),
            CreateRecurringShip(3, alpha, FactionA),
            CreateRecurringShip(4, beta, FactionB),
            CreateRecurringShip(5, beta, FactionB),
            CreateRecurringShip(6, gamma, null),
        ];
        FactionStart[] factions =
        [
            new(
                FactionA,
                FactionBootstrapTests.FactionTestWorld.DefinitionA,
                ObservationResponsePosture: ObservationResponsePosture.Enabled
            ),
            new(
                FactionB,
                FactionBootstrapTests.FactionTestWorld.DefinitionB,
                ObservationResponsePosture: ObservationResponsePosture.Enabled
            ),
        ];
        return new GameBootstrap(new SimulationTime(0), map, ships[0].InstanceId, ships, factions).CreateSimulation(
            ShipCatalog,
            FactionCatalog
        );
    }

    private static ShipStart CreateRecurringShip(long id, LocationId location, FactionId? controller) =>
        FactionBootstrapTests.FactionTestWorld.CreateShip(id, location, controller) with
        {
            DefinitionId = new ShipDefinitionId("pathfinder"),
        };
}
