using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Gameplay;

internal sealed class Milestone3ProofFixture
{
    internal static readonly GameSaveMetadata Metadata = new(
        "milestone-3-proof",
        "Milestone 3 Proof",
        new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero)
    );

    internal Milestone3ProofFixture()
    {
        Catalog = TestShipContent.Production();
    }

    internal ShipDefinitionCatalog Catalog { get; }

    internal GameSimulation CreateDefault() => FirstGameSetup.Create(Catalog);

    internal GameSimulation CreateWithBootstrapOrder(bool reversed)
    {
        ShipStart[] starts = CreateStarts();
        IEnumerable<ShipStart> ordered = reversed ? starts.Reverse() : starts;
        GameSimulation simulation = new GameBootstrap(
            new SimulationTime(0),
            CreateMap(),
            starts[0].InstanceId,
            ordered
        ).CreateSimulation(Catalog);
        simulation.BootstrapHiddenCautiousContactObservation(starts[3].InstanceId);
        return simulation;
    }

    /// <summary>
    /// Creates the proof world with one declared Kestrel order and nothing else changed.
    /// </summary>
    /// <remarks>
    /// Shares <see cref="CreateStarts"/> with <see cref="CreateWithBootstrapOrder"/> so a caller comparing two
    /// worlds that differ only in Kestrel's hidden order cannot accidentally vary anything else about the world.
    /// </remarks>
    internal GameSimulation CreateWithKestrelOrder(ShipOrderStart? kestrelOrder)
    {
        ShipStart[] starts = CreateStarts();
        starts[3] = starts[3] with { ActiveOrder = kestrelOrder };
        GameSimulation simulation = new GameBootstrap(
            new SimulationTime(0),
            CreateMap(),
            starts[0].InstanceId,
            starts
        ).CreateSimulation(Catalog);
        simulation.BootstrapHiddenCautiousContactObservation(starts[3].InstanceId);
        return simulation;
    }

    internal GameSimulation RoundTrip(GameSimulation simulation, string sourceName) =>
        GamePersistence.Deserialize(GamePersistence.Serialize(simulation, Metadata), Catalog, sourceName).Simulation;

    internal static ShipState Player(GameSimulation simulation) =>
        simulation.CaptureState().GetRequiredShip(simulation.CaptureState().PlayerShipId);

    internal static ShipState Kestrel(GameSimulation simulation) =>
        simulation
            .CaptureState()
            .Ships.Single(ship =>
                string.Equals(ship.VesselDisplayName, "Survey Vessel Kestrel", StringComparison.Ordinal)
            );

    private static ShipStart[] CreateStarts()
    {
        var initialTime = new SimulationTime(0);
        var definitionId = new ShipDefinitionId("pathfinder");
        // Pre-combat proof starts: generation and impulse nominal at 70/50, shields and weapons installed offline.
        ShipSystemsStart damaged = TestShipStarts.Pathfinder(sensors: 0.4);
        ShipSystemsStart repaired = TestShipStarts.Pathfinder();
        SystemRepairStart sensorRepair = TestShipStarts.Repair(
            TestShipContent.Sensors,
            0.4,
            1,
            initialTime.Milliseconds
        );
        TacticalMotion stopped = default;
        return
        [
            new(
                new ShipInstanceId(1),
                definitionId,
                "USS Pathfinder",
                new TacticalPosition(3.25, -7.5),
                stopped,
                new AtLocationStart(new LocationId("dawn-anchor")),
                damaged,
                sensorRepair
            ),
            new(
                new ShipInstanceId(2),
                definitionId,
                "USS Wayfarer",
                new TacticalPosition(-2, 4),
                stopped,
                new AtLocationStart(new LocationId("vesper-reach")),
                damaged,
                sensorRepair
            ),
            new(
                new ShipInstanceId(3),
                definitionId,
                "USS Horizon",
                new TacticalPosition(6, 1.5),
                stopped,
                new TravelingStart(new LocationId("vesper-reach"), new LocationId("meridian-drift"), initialTime),
                repaired
            ),
            new(
                new ShipInstanceId(4),
                definitionId,
                "Survey Vessel Kestrel",
                new TacticalPosition(21.25, -7.5),
                stopped,
                new AtLocationStart(new LocationId("dawn-anchor")),
                repaired
            ),
        ];
    }

    private static StrategicMap CreateMap()
    {
        var dawn = new StrategicLocation(
            new LocationId("dawn-anchor"),
            "Dawn Anchor",
            new StrategicMapPosition(-5.5, 2.25)
        );
        var vesper = new StrategicLocation(
            new LocationId("vesper-reach"),
            "Vesper Reach",
            new StrategicMapPosition(8.125, 11.75)
        );
        var meridian = new StrategicLocation(
            new LocationId("meridian-drift"),
            "Meridian Drift",
            new StrategicMapPosition(17.4, -3.6)
        );
        return new StrategicMap(
            [dawn, vesper, meridian],
            [
                new StrategicRoute(dawn.Id, vesper.Id, new SimulationDuration(12000)),
                new StrategicRoute(vesper.Id, meridian.Id, new SimulationDuration(14000)),
            ]
        );
    }
}
