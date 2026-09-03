using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

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
        string root = FindRepositoryRoot();
        string schema = File.ReadAllText(
            Path.Combine(root, "src/AlterCourse.Godot/content/schemas/ship-definition-v4.schema.json")
        );
        string definition = File.ReadAllText(Path.Combine(root, "src/AlterCourse.Godot/content/ships/pathfinder.json"));
        Catalog = new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText("pathfinder.json", definition),
        ]);
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
        var damaged = new SensorIntegrity(0.4);
        var repaired = new SensorIntegrity(1);
        TacticalMotion stopped = default;
        return
        [
            new(
                new ShipInstanceId(1),
                definitionId,
                "USS Pathfinder",
                new TacticalPosition(3.25, -7.5),
                stopped,
                damaged,
                new AtLocationStart(new LocationId("dawn-anchor")),
                new SensorRepairStart(damaged, repaired, initialTime)
            ),
            new(
                new ShipInstanceId(2),
                definitionId,
                "USS Wayfarer",
                new TacticalPosition(-2, 4),
                stopped,
                damaged,
                new AtLocationStart(new LocationId("vesper-reach")),
                new SensorRepairStart(damaged, repaired, initialTime)
            ),
            new(
                new ShipInstanceId(3),
                definitionId,
                "USS Horizon",
                new TacticalPosition(6, 1.5),
                stopped,
                repaired,
                new TravelingStart(new LocationId("vesper-reach"), new LocationId("meridian-drift"), initialTime)
            ),
            new(
                new ShipInstanceId(4),
                definitionId,
                "Survey Vessel Kestrel",
                new TacticalPosition(21.25, -7.5),
                stopped,
                repaired,
                new AtLocationStart(new LocationId("dawn-anchor"))
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

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "AlterCourse.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
