using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies complete typed faction bootstrap and legacy zero-faction compatibility.</summary>
public sealed class FactionBootstrapTests
{
    /// <summary>Confirms the original bootstrap overload remains a four-ship zero-faction world.</summary>
    [Fact]
    public void LegacyOneCatalogBootstrapCreatesZeroFactions()
    {
        SimulationState state = FactionTestWorld
            .CreateBootstrap()
            .CreateSimulation(FactionTestWorld.ShipCatalog)
            .CaptureState();

        Assert.Empty(state.Factions);
        Assert.All(state.Ships, ship => Assert.Null(ship.DirectControllerFactionId));
    }

    /// <summary>Confirms the production overload adds two factions and two ships without changing legacy setup.</summary>
    [Fact]
    public void FirstGameSetupRetainsLegacyFourShipsAndAddsSixShipFactionOverload()
    {
        ShipDefinitionCatalog catalog = FactionTestWorld.CreateShipCatalog("pathfinder");

        SimulationState legacy = FirstGameSetup.Create(catalog).CaptureState();
        SimulationState production = FirstGameSetup.Create(catalog, FactionTestWorld.FactionCatalog).CaptureState();

        Assert.Equal(4, legacy.Ships.Length);
        Assert.Empty(legacy.Factions);
        Assert.Equal(6, production.Ships.Length);
        Assert.Equal(2, production.Factions.Length);
        Assert.Null(production.GetRequiredShip(new ShipInstanceId(1)).DirectControllerFactionId);
        Assert.Equal(
            FactionTestWorld.FactionB,
            production.GetRequiredShip(new ShipInstanceId(2)).DirectControllerFactionId
        );
        Assert.Equal(
            FactionTestWorld.FactionB,
            production.GetRequiredShip(new ShipInstanceId(3)).DirectControllerFactionId
        );
        Assert.Equal(
            FactionTestWorld.FactionA,
            production.GetRequiredShip(new ShipInstanceId(5)).DirectControllerFactionId
        );
        Assert.Equal(
            FactionTestWorld.FactionA,
            production.GetRequiredShip(new ShipInstanceId(6)).DirectControllerFactionId
        );
        Assert.All(
            production.Factions,
            faction => Assert.Equal(ObservationResponsePosture.Enabled, faction.Observation!.Posture)
        );
    }

    /// <summary>Confirms faction starts and initial work use stable runtime-identity order.</summary>
    [Fact]
    public void CanonicalizesFactionsAndSchedulesOneInitialWakePerObjective()
    {
        FactionStart first = new(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta);
        FactionStart second = new(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB);
        GameBootstrap bootstrap = FactionTestWorld.CreateBootstrap(
            factionStarts: [second, first],
            controlledShips: true
        );
        SimulationState state = bootstrap
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();

        Assert.Equal(new long[] { 1, 2 }, bootstrap.FactionStarts.Select(start => start.Id.Value));
        Assert.Equal(new long[] { 1, 2 }, state.Factions.Select(faction => faction.Id.Value));
        ScheduledWork wake = Assert.Single(state.Scheduler.OutstandingWork);
        Assert.Equal(ScheduledWorkTargetKind.Faction, wake.Target.Kind);
        Assert.Equal(FactionTestWorld.FactionA, wake.Target.FactionId);
        Assert.Equal(ScheduledWorkKind.FactionDecisionWake, wake.Kind);
        Assert.Equal(state.Time, wake.DueTime);
    }

    /// <summary>Confirms bootstrap rejects every unresolved faction, controller, and objective reference.</summary>
    [Fact]
    public void RejectsInvalidFactionDefinitionsControllersObjectivesAndPlayerControl()
    {
        FactionStart valid = new(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta);
        FactionStart duplicate = valid with { DefinitionId = FactionTestWorld.DefinitionB };

        Assert.Throws<ArgumentException>(() => FactionTestWorld.CreateBootstrap(factionStarts: [valid, duplicate]));
        Assert.Throws<ArgumentException>(() =>
            FactionTestWorld.CreateBootstrap(factionStarts: [valid], danglingController: true)
        );
        Assert.Throws<ArgumentException>(() =>
            FactionTestWorld.CreateBootstrap(factionStarts: [valid], playerControlled: true)
        );
        Assert.Throws<ArgumentException>(() =>
            FactionTestWorld.CreateBootstrap(
                factionStarts: [valid with { PresenceTargetLocationId = new LocationId("missing") }]
            )
        );
        Assert.Throws<KeyNotFoundException>(() =>
            FactionTestWorld
                .CreateBootstrap(factionStarts: [valid with { DefinitionId = new FactionDefinitionId("missing") }])
                .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    // Shared constructors keep the three boundary-focused suites on one identical aggregate shape.
    internal static class FactionTestWorld
    {
        internal static readonly ShipDefinitionId ShipDefinition = new("test-ship");
        internal static readonly FactionDefinitionId DefinitionA = new("faction-a");
        internal static readonly FactionDefinitionId DefinitionB = new("faction-b");
        internal static readonly FactionId FactionA = new(1);
        internal static readonly FactionId FactionB = new(2);
        internal static readonly LocationId Alpha = new("alpha");
        internal static readonly LocationId Beta = new("beta");
        internal static readonly LocationId Gamma = new("gamma");

        internal static ShipDefinitionCatalog ShipCatalog { get; } = CreateShipCatalog();
        internal static FactionDefinitionCatalog FactionCatalog { get; } =
            new(
                new Dictionary<FactionDefinitionId, FactionDefinition>
                {
                    [DefinitionA] = new(DefinitionA, "Faction A"),
                    [DefinitionB] = new(DefinitionB, "Faction B"),
                }
            );

        internal static GameBootstrap CreateBootstrap(
            IEnumerable<FactionStart>? factionStarts = null,
            bool controlledShips = false,
            bool danglingController = false,
            bool playerControlled = false,
            bool alternateControlled = true,
            ShipOrderStart? firstNpcOrder = null,
            ShipStrategicStart? firstNpcStrategic = null
        )
        {
            FactionId? firstController =
                danglingController ? new FactionId(99)
                : controlledShips ? FactionA
                : null;
            bool includesFactionB = factionStarts?.Any(start => start.Id == FactionB) == true;
            ShipStart[] ships =
            [
                CreateShip(1, Alpha, playerControlled ? FactionA : null),
                CreateShip(2, Alpha, firstController, firstNpcOrder, firstNpcStrategic),
                CreateShip(3, Alpha, controlledShips && alternateControlled ? FactionA : null),
                CreateShip(4, Beta, controlledShips && includesFactionB ? FactionB : null),
            ];
            return new GameBootstrap(new SimulationTime(0), CreateMap(), ships[0].InstanceId, ships, factionStarts);
        }

        internal static SimulationState DormantFactionState(
            ShipOrderStart? firstNpcOrder = null,
            ShipStrategicStart? firstNpcStrategic = null,
            bool includeFactionB = false
        )
        {
            FactionStart[] factions = includeFactionB
                ? [new FactionStart(FactionA, DefinitionA, Beta), new FactionStart(FactionB, DefinitionB)]
                : [new FactionStart(FactionA, DefinitionA, Beta)];
            SimulationState state = CreateBootstrap(
                    factions,
                    controlledShips: true,
                    firstNpcOrder: firstNpcOrder,
                    firstNpcStrategic: firstNpcStrategic
                )
                .CreateSimulation(ShipCatalog, FactionCatalog)
                .CaptureState();
            PendingFactionDecisionWake wake = state.Factions[0].PendingDecisionWake!;
            (SimulationScheduler scheduler, bool removed) = state.Scheduler.Cancel(wake.WorkId);
            Assert.True(removed);
            return state.ReplaceFaction(FactionA, state.Factions[0] with { PendingDecisionWake = null }) with
            {
                Scheduler = scheduler,
            };
        }

        internal static StrategicMap CreateMap(bool includeAlphaBeta = true, bool cyclic = false)
        {
            var routes = new List<StrategicRoute>();
            if (includeAlphaBeta)
                routes.Add(new StrategicRoute(Alpha, Beta, new SimulationDuration(1000)));
            if (cyclic)
            {
                routes.Add(new StrategicRoute(Beta, Gamma, new SimulationDuration(1000)));
                routes.Add(new StrategicRoute(Gamma, Alpha, new SimulationDuration(1000)));
            }
            return new StrategicMap(
                [
                    new StrategicLocation(Alpha, "Alpha", default),
                    new StrategicLocation(Beta, "Beta", default),
                    new StrategicLocation(Gamma, "Gamma", default),
                ],
                routes
            );
        }

        internal static ShipStart CreateShip(
            long id,
            LocationId location,
            FactionId? controller,
            ShipOrderStart? order = null,
            ShipStrategicStart? strategic = null
        ) =>
            new(
                new ShipInstanceId(id),
                ShipDefinition,
                $"Ship {id}",
                default,
                default,
                new SystemCondition(1),
                new SystemCondition(1),
                new SystemCondition(1),
                new PowerAllocation(new PowerUnits(70), new PowerUnits(50)),
                strategic ?? new AtLocationStart(location),
                activeOrder: order,
                directControllerFactionId: controller
            );

        internal static ShipDefinitionCatalog CreateShipCatalog(string definitionIdentity = "test-ship")
        {
            var definition = new ShipDefinition(
                new ShipDefinitionId(definitionIdentity),
                "Test Ship",
                new SpeedKilometersPerSecond(10),
                new DistanceKilometers(10),
                new SimulationDuration(100),
                new SimulationDuration(1000)
            );
            return new ShipDefinitionCatalog(
                new Dictionary<ShipDefinitionId, ShipDefinition> { [definition.Id] = definition }
            );
        }
    }
}
