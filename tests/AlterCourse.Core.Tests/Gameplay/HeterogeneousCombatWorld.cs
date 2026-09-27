using AlterCourse.Core.AI;
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
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>
/// Builds a world whose same-class ships carry different actual loadouts, then drives it into simultaneous repair,
/// scan, weapon cooldown, and defensive-stimulus continuation.
/// </summary>
/// <remarks>
/// <para>
/// Every ship uses design <c>pathfinder</c>, but only the defender and the faction-A ships take the design default.
/// The player has no shields (absent kind), an alternate sensor definition, nonconsecutive installed ids 3/17/40/41,
/// and an allocator advanced to 900; the scan target has no combat systems at ids 1/2/6; the faction-B ship carries
/// the alternate sensors and shields but no weapon. That combination exercises every way two same-class ships may
/// legitimately differ (ADR 0014, OP §4) through public bootstrap only.
/// </para>
/// <para>
/// The alternate definition <c>test.long-range-sensors</c> exists only in test content: order 200 like the standard
/// sensors, demand 60, repair 5,000 ms, passive range 45 km, scan 1,500 ms. The player's impulse starts at condition
/// 0.5 so an impulse repair can run through combat without being interrupted (return fire aims at weapons).
/// </para>
/// </remarks>
internal sealed class HeterogeneousCombatWorld
{
    internal static readonly ShipInstanceId Player = new(1);
    internal static readonly ShipInstanceId Defender = new(2);
    internal static readonly ShipInstanceId ScanTarget = new(3);
    internal static readonly ShipInstanceId FactionBShip = new(4);
    internal static readonly ShipInstanceId FactionAShip = new(5);

    internal static readonly InstalledSystemId PlayerGenerator = new(3);
    internal static readonly InstalledSystemId PlayerSensors = new(17);
    internal static readonly InstalledSystemId PlayerImpulse = new(40);
    internal static readonly InstalledSystemId PlayerWeapons = new(41);
    internal const long PlayerNextInstalledSystemId = 900;

    internal static readonly GameSaveMetadata Metadata = new(
        "heterogeneous-continuation",
        "Heterogeneous continuation",
        new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero)
    );

    private static readonly LocationId Dawn = new("dawn-anchor");
    private static readonly LocationId Vesper = new("vesper-reach");
    private static readonly LocationId Meridian = new("meridian-drift");

    private readonly SystemDefinition[] _standard;
    private readonly bool _withFactions;

    internal HeterogeneousCombatWorld(double baseDamage = 0.25, bool withFactions = false)
    {
        _standard = TestShipContent.PathfinderSystems(PathfinderTuning.Production with { BaseDamage = baseDamage });
        _withFactions = withFactions;
        Catalog = WithDefaultLoadout(_ => true);
        Factions = withFactions ? new FactionAssignmentProofFixture().FactionCatalog : FactionDefinitionCatalog.Empty;
    }

    /// <summary>Gets the test-only alternate definition of the existing sensors kind.</summary>
    internal static SensorSystemDefinition LongRangeSensors { get; } =
        new(
            new SystemDefinitionId("test.long-range-sensors"),
            "Long-range sensors",
            200,
            true,
            new SystemRepairCapability(new SimulationDuration(5000), 300),
            new SystemPowerDemand(new PowerUnits(60)),
            new DistanceKilometers(45),
            new SimulationDuration(1500)
        );

    /// <summary>Gets the catalog whose design default is the full five-system pathfinder loadout.</summary>
    internal ShipDefinitionCatalog Catalog { get; }

    /// <summary>Gets the faction catalog (empty unless the world was built with factions).</summary>
    internal FactionDefinitionCatalog Factions { get; }

    /// <summary>
    /// Returns a catalog with the same system definitions but a design default loadout filtered by
    /// <paramref name="keep"/> — a compatible class-default change that must never alter a saved world.
    /// </summary>
    internal ShipDefinitionCatalog WithDefaultLoadout(Func<SystemDefinition, bool> keep) =>
        TestShipContent.Catalog(
            TestShipContent.Systems([.. _standard, LongRangeSensors]),
            TestShipContent.Design("pathfinder", "Pathfinder class", [.. _standard.Where(keep)])
        );

    /// <summary>Bootstraps the world; the defender keeps a cautious posture so hails are acknowledged.</summary>
    internal GameSimulation Create()
    {
        List<ShipStart> ships =
        [
            Ship(Player, Dawn, 0, PlayerLoadout()),
            Ship(
                Defender,
                Dawn,
                10,
                TestShipStarts.Pathfinder(
                    sensorPower: 70,
                    impulsePower: 5,
                    shieldPower: 15,
                    weaponPower: 30,
                    shields: 1,
                    weapons: 1
                )
            ),
            Ship(ScanTarget, Dawn, -30, ScanTargetLoadout()),
        ];
        List<FactionStart> factions = [];
        if (_withFactions)
        {
            ships.Add(Ship(FactionBShip, Vesper, 0, FactionBLoadout()) with { DirectControllerFactionId = new(2) });
            ships.Add(
                Ship(FactionAShip, Meridian, 0, TestShipStarts.Pathfinder()) with
                {
                    DirectControllerFactionId = new(1),
                }
            );
            factions.Add(
                new(new FactionId(1), new FactionDefinitionId("faction-a"), Vesper, ObservationResponsePosture.Enabled)
            );
            factions.Add(
                new(
                    new FactionId(2),
                    new FactionDefinitionId("faction-b"),
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                )
            );
        }

        GameSimulation game = new GameBootstrap(new SimulationTime(0), Map(), Player, ships, factions).CreateSimulation(
            Catalog,
            Factions
        );
        SimulationState initial = game.CaptureState();
        ShipState defender = initial.GetRequiredShip(Defender);
        return Restore(
            initial.ReplaceShip(
                Defender,
                defender with
                {
                    AutonomousState = defender.AutonomousState with
                    {
                        ContactPosture = ShipContactPosture.CautiousContact,
                    },
                }
            )
        );
    }

    /// <summary>
    /// Returns the world at the instant every specialized continuation is live at once: the player's impulse repair,
    /// the player's scan of the scan target from installation 17, the player's weapon cooldown, and the defender's
    /// pending defensive stimulus.
    /// </summary>
    internal GameSimulation Engaged()
    {
        GameSimulation game = Create();
        game.AdvanceFixedSteps(1);
        SensorContactId defender = ContactOf(game, Defender);
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(defender).Outcome);
        game.AdvanceFixedSteps(checked((int)(LongRangeSensors.ActiveScanDuration.Milliseconds / StepMilliseconds)));
        Assert.Equal(HailOutcome.Acknowledged, game.RequestHail(defender).Outcome);
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(PlayerImpulse, new SystemCondition(1)).Outcome
        );
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(defender, ShipSystemKind.DirectedEnergyWeapons)).Outcome
        );
        Assert.Equal(
            ActiveSensorScanOutcome.Accepted,
            game.RequestActiveSensorScan(ContactOf(game, ScanTarget)).Outcome
        );
        return game;
    }

    internal GameSimulation Restore(SimulationState state) =>
        GameSimulation.RestoreState(state, Catalog, state.Factions.IsEmpty ? FactionDefinitionCatalog.Empty : Factions);

    internal static long StepMilliseconds => SimulationFixedStep.Duration.Milliseconds;

    internal static SensorContactId ContactOf(GameSimulation game, ShipInstanceId target) =>
        game.CaptureState()
            .GetRequiredShip(game.CaptureState().PlayerShipId)
            .SensorKnowledge.Contacts.Single(contact => contact.TargetShipId == target)
            .Id;

    private static ShipStart Ship(ShipInstanceId id, LocationId location, double x, ShipSystemsStart systems) =>
        new(
            id,
            new ShipDefinitionId("pathfinder"),
            "Pathfinder " + id.Value,
            new TacticalPosition(x, 0),
            default,
            new AtLocationStart(location),
            systems
        );

    private static ShipSystemsStart PlayerLoadout() =>
        ShipSystemsStart.Explicit(
            PlayerNextInstalledSystemId,
            [
                Installed(PlayerWeapons, "pathfinder.directed-energy-weapons", 1, 30),
                Installed(PlayerImpulse, "pathfinder.impulse-propulsion", 0.5, 20),
                Installed(PlayerSensors, LongRangeSensors.Id.Value, 1, 60),
                Installed(PlayerGenerator, "pathfinder.power-generation", 1, null),
            ]
        );

    private static ShipSystemsStart ScanTargetLoadout() =>
        ShipSystemsStart.Explicit(
            7,
            [
                Installed(new(1), "pathfinder.power-generation", 1, null),
                Installed(new(2), "pathfinder.sensors", 1, 70),
                Installed(new(6), "pathfinder.impulse-propulsion", 1, 50),
            ]
        );

    private static ShipSystemsStart FactionBLoadout() =>
        ShipSystemsStart.Explicit(
            12,
            [
                Installed(new(1), "pathfinder.power-generation", 1, null),
                Installed(new(2), LongRangeSensors.Id.Value, 1, 60),
                Installed(new(4), "pathfinder.shields", 1, 10),
                Installed(new(11), "pathfinder.impulse-propulsion", 1, 50),
            ]
        );

    private static InstalledSystemStart Installed(
        InstalledSystemId id,
        string definition,
        double condition,
        int? power
    ) =>
        new(
            id,
            new SystemDefinitionId(definition),
            new SystemCondition(condition),
            power is null ? null : new PowerUnits(power.Value)
        );

    private static StrategicMap Map() =>
        new(
            [
                new StrategicLocation(Dawn, "Dawn Anchor", new StrategicMapPosition(-5.5, 2.25)),
                new StrategicLocation(Vesper, "Vesper Reach", new StrategicMapPosition(8.125, 11.75)),
                new StrategicLocation(Meridian, "Meridian Drift", new StrategicMapPosition(17.4, -3.6)),
            ],
            [
                new StrategicRoute(Dawn, Vesper, new SimulationDuration(12000)),
                new StrategicRoute(Vesper, Meridian, new SimulationDuration(14000)),
            ]
        );
}
