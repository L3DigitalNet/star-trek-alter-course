using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Builds the approved six-ship faction proof from the tracked production content.</summary>
internal sealed class FactionAssignmentProofFixture
{
    internal static readonly FactionId FactionA = new(1);
    internal static readonly FactionId FactionB = new(2);
    internal static readonly ShipInstanceId PlayerId = new(1);
    internal static readonly ShipInstanceId FactionBShipId = new(2);
    internal static readonly ShipInstanceId PreferredShipId = new(5);
    internal static readonly ShipInstanceId AlternateShipId = new(6);
    internal static readonly LocationId Dawn = new("dawn-anchor");
    internal static readonly LocationId Vesper = new("vesper-reach");
    internal static readonly LocationId Meridian = new("meridian-drift");
    internal static readonly SimulationTime ArrivalTime = new(14_000);
    internal static readonly GameSaveMetadata Metadata = new(
        "faction-assignment-proof",
        "Faction Assignment Proof",
        new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)
    );

    internal FactionAssignmentProofFixture()
    {
        string root = FindRepositoryRoot();
        ShipCatalog = LoadShips(root);
        FactionCatalog = LoadFactions(root);
    }

    internal ShipDefinitionCatalog ShipCatalog { get; }
    internal FactionDefinitionCatalog FactionCatalog { get; }

    internal GameSimulation Create(bool preferredCommitted = false, bool reverseDeclarations = false)
    {
        ShipStart[] ships = CreateShipStarts(preferredCommitted);
        FactionStart[] factions =
        [
            new(FactionA, new FactionDefinitionId("faction-a"), Vesper),
            new(FactionB, new FactionDefinitionId("faction-b")),
        ];
        IEnumerable<ShipStart> shipDeclarations = reverseDeclarations ? ships.Reverse() : ships;
        IEnumerable<FactionStart> factionDeclarations = reverseDeclarations ? factions.Reverse() : factions;
        return new GameBootstrap(
            new SimulationTime(0),
            CreateMap(),
            PlayerId,
            shipDeclarations,
            factionDeclarations
        ).CreateSimulation(ShipCatalog, FactionCatalog);
    }

    internal GameSimulation CreateKnowledgeVariant(bool revealForeignTruth)
    {
        ShipStart[] ships = CreateShipStarts(false);
        ships[3] = ships[3] with
        {
            Strategic = new AtLocationStart(revealForeignTruth ? Meridian : Vesper),
            ActiveOrder = revealForeignTruth ? new HoldUntilOrderStart(new SimulationTime(5_000)) : null,
        };
        GameSimulation game = new GameBootstrap(
            new SimulationTime(0),
            CreateMap(),
            PlayerId,
            ships,
            [
                new FactionStart(FactionA, new FactionDefinitionId("faction-a"), Vesper),
                new FactionStart(FactionB, new FactionDefinitionId("faction-b")),
            ]
        ).CreateSimulation(ShipCatalog, FactionCatalog);
        return revealForeignTruth ? AddValidatedNpcKnowledgeAndScan(game) : game;
    }

    internal GameSimulation Assign(GameSimulation game)
    {
        SimulationAdvanceTraceResult assigned = GameSimulation.AdvanceTo(
            game.CaptureState(),
            game.CaptureState().Time,
            ShipCatalog,
            FactionCatalog
        );
        return GameSimulation.RestoreState(assigned.State, ShipCatalog, FactionCatalog);
    }

    internal GameSimulation RoundTrip(GameSimulation game, string sourceName) =>
        GamePersistence
            .Deserialize(GamePersistence.Serialize(game, Metadata), ShipCatalog, FactionCatalog, sourceName)
            .Simulation;

    internal GameSimulation ContinueTo(GameSimulation game, SimulationTime target)
    {
        SimulationAdvanceTraceResult advanced = GameSimulation.AdvanceTo(
            game.CaptureState(),
            target,
            ShipCatalog,
            FactionCatalog
        );
        return GameSimulation.RestoreState(advanced.State, ShipCatalog, FactionCatalog);
    }

    private GameSimulation AddValidatedNpcKnowledgeAndScan(GameSimulation game)
    {
        game.BootstrapHiddenCautiousContactObservation(PreferredShipId);
        SimulationState observed = game.CaptureState();
        ShipState observer = observed.GetRequiredShip(PreferredShipId);
        SensorContactTrack contact = observer.SensorKnowledge.Contacts.Single(track =>
            track.TargetShipId == new ShipInstanceId(4)
        );
        SimulationTime completion = observed.Time.AdvanceBy(
            ShipCatalog.GetRequired(observer.DefinitionId).ActiveScanDuration
        );
        (SimulationScheduler scheduler, ScheduledWork work) = observed.Scheduler.Schedule(
            completion,
            PreferredShipId,
            ScheduledWorkKind.ActiveSensorScanCompletion
        );
        SensorKnowledge knowledge = observer.SensorKnowledge with
        {
            ActiveScan = new ActiveSensorScanState(contact.Id, observed.Time, completion, work.Id),
        };
        SimulationState scanned = observed.ReplaceShip(
            PreferredShipId,
            observer with
            {
                SensorKnowledge = knowledge,
            }
        ) with
        {
            Scheduler = scheduler,
        };
        return GameSimulation.RestoreState(scanned, ShipCatalog, FactionCatalog);
    }

    private static ShipStart[] CreateShipStarts(bool preferredCommitted)
    {
        var definition = new ShipDefinitionId("pathfinder");
        var nominal = new SystemCondition(1);
        var full = new PowerAllocation(new PowerUnits(70), new PowerUnits(50));
        var stopped = new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(0));
        return
        [
            CreatePlayer(definition, nominal, stopped),
            CreateShip(2, "USS Wayfarer", definition, new TacticalPosition(-2, 4), Vesper, FactionB),
            CreateTravelingShip(definition, nominal, full, stopped),
            CreateShip(4, "Survey Vessel Kestrel", definition, new TacticalPosition(21.25, -7.5), Dawn, null),
            CreateShip(
                5,
                "Expedition Vessel Aurora",
                definition,
                default,
                Meridian,
                FactionA,
                preferredCommitted ? new HoldUntilOrderStart(new SimulationTime(5_000)) : null
            ),
            CreateShip(6, "Expedition Vessel Resolute", definition, default, Meridian, FactionA),
        ];
    }

    private static ShipStart CreatePlayer(ShipDefinitionId definition, SystemCondition nominal, TacticalMotion stopped)
    {
        var damaged = new SystemCondition(0.4);
        return new ShipStart(
            PlayerId,
            definition,
            "USS Pathfinder",
            new TacticalPosition(3.25, -7.5),
            stopped,
            new SystemCondition(0.625),
            damaged,
            nominal,
            new PowerAllocation(new PowerUnits(44), new PowerUnits(31)),
            new AtLocationStart(Dawn),
            new SystemRepairStart(ShipSystemId.Sensors, damaged, nominal, new SimulationTime(0))
        );
    }

    private static ShipStart CreateTravelingShip(
        ShipDefinitionId definition,
        SystemCondition nominal,
        PowerAllocation full,
        TacticalMotion stopped
    ) =>
        new(
            new ShipInstanceId(3),
            definition,
            "USS Horizon",
            new TacticalPosition(6, 1.5),
            stopped,
            nominal,
            nominal,
            nominal,
            full,
            new TravelingStart(Vesper, Meridian, new SimulationTime(0))
        );

    private static ShipStart CreateShip(
        long id,
        string name,
        ShipDefinitionId definition,
        TacticalPosition position,
        LocationId location,
        FactionId? controller,
        ShipOrderStart? order = null
    ) =>
        new(
            new ShipInstanceId(id),
            definition,
            name,
            position,
            default,
            new SystemCondition(1),
            new SystemCondition(1),
            new SystemCondition(1),
            new PowerAllocation(new PowerUnits(70), new PowerUnits(50)),
            new AtLocationStart(location),
            activeOrder: order,
            directControllerFactionId: controller
        );

    private static StrategicMap CreateMap() =>
        new(
            [
                new StrategicLocation(Dawn, "Dawn Anchor", new StrategicMapPosition(-5.5, 2.25)),
                new StrategicLocation(Vesper, "Vesper Reach", new StrategicMapPosition(8.125, 11.75)),
                new StrategicLocation(Meridian, "Meridian Drift", new StrategicMapPosition(17.4, -3.6)),
            ],
            [
                new StrategicRoute(Dawn, Vesper, new SimulationDuration(12_000)),
                new StrategicRoute(Vesper, Meridian, new SimulationDuration(14_000)),
            ]
        );

    private static ShipDefinitionCatalog LoadShips(string root)
    {
        string schema = File.ReadAllText(
            Path.Combine(root, "src/AlterCourse.Godot/content/schemas/ship-definition-v4.schema.json")
        );
        string path = Path.Combine(root, "src/AlterCourse.Godot/content/ships/pathfinder.json");
        return new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText(path, File.ReadAllText(path)),
        ]);
    }

    private static FactionDefinitionCatalog LoadFactions(string root)
    {
        string schema = File.ReadAllText(
            Path.Combine(root, "src/AlterCourse.Godot/content/schemas/faction-definition-v1.schema.json")
        );
        string directory = Path.Combine(root, "src/AlterCourse.Godot/content/factions");
        return new FactionDefinitionCatalogLoader(schema).LoadCatalog(
            Directory
                .EnumerateFiles(directory, "*.json")
                .Select(path => FactionDefinitionContent.FromText(path, File.ReadAllText(path)))
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
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
