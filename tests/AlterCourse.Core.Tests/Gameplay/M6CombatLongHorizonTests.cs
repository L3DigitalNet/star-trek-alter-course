using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
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
/// Exercises sustained real defense, a heterogeneous mixed combat/repair/scan/faction world over a long horizon, and
/// the widest supported installed-system shape, each with bounded work and byte-stable V10 continuation.
/// </summary>
public sealed class M6CombatLongHorizonTests
{
    private const int ShotCount = 512;
    private const int MixedShotCount = 256;
    private const int MixedWeaponRepairInterval = 16;
    private const int MixedSizeSampleInterval = 32;
    private const long MixedDormantMilliseconds = 3_600_000;

    // 128 MiB, the retained V10 save envelope (content-assets-and-persistence; OP §13).
    private const long SaveEnvelopeBytes = 128L * 1024 * 1024;
    private readonly M6CombatProofFixture _fixture = new(lowDamage: true);

    private static SimulationTime ReadyAt(GameSimulation game) =>
        M6CombatProofFixture.Player(game).Combat.ReadinessOf(TestShipContent.Weapons)!.ReadyAt;

    /// <summary>Proves recurring defense stays active without cooldown work, damage resets or save divergence.</summary>
    [Fact]
    public void RepeatedLegitimateDefenseRemainsBoundedAndConvergesAcrossMidStimulusSave()
    {
        GameSimulation game = _fixture.Pair();
        GameSimulation? resumed = null;
        SensorContactId contact = M6CombatProofFixture.Contact(game, M6CombatProofFixture.Defender);
        long startedAt = game.CaptureState().Time.Milliseconds;
        long startingWorkId = game.CaptureState().Scheduler.NextWorkId;
        long startingSequence = game.CaptureState().Scheduler.NextSequence;
        int decisions = 0;
        int maximumOutstanding = 0;
        Assert.Empty(game.CaptureState().Scheduler.OutstandingWork);
        for (int shot = 0; shot < ShotCount; shot++)
        {
            if (shot > 0)
            {
                (game, resumed) = AdvanceTogether(game, resumed, ReadyAt(game), responding: false);
            }
            FireDirectedEnergyResult fired = game.FireDirectedEnergy(
                new(contact, ShipSystemKind.DirectedEnergyWeapons)
            );
            Assert.Equal(FireDirectedEnergyOutcome.Accepted, fired.Outcome);
            if (resumed is not null)
                Assert.Equal(fired, resumed.FireDirectedEnergy(new(contact, ShipSystemKind.DirectedEnergyWeapons)));
            SimulationState pending = game.CaptureState();
            CombatStimulus stimulus = pending.GetRequiredShip(M6CombatProofFixture.Defender).Combat.PendingStimulus!;
            AssertPendingCorrelation(pending, stimulus);
            maximumOutstanding = Math.Max(maximumOutstanding, pending.Scheduler.OutstandingWork.Length);
            if (shot == ShotCount / 2 - 1)
                resumed = _fixture.RoundTrip(game);
            if (resumed is not null)
                _fixture.AssertEquivalent(game, resumed);
            (game, resumed) = AdvanceTogether(game, resumed, stimulus.DueTime, responding: true);
            decisions++;
        }
        Assert.Equal(ShotCount, decisions);
        Assert.Equal(1, maximumOutstanding);
        Assert.Equal(startingWorkId + ShotCount, game.CaptureState().Scheduler.NextWorkId);
        Assert.Equal(startingSequence + ShotCount, game.CaptureState().Scheduler.NextSequence);
        Assert.InRange(
            TestEngineering.ConditionOf(
                M6CombatProofFixture.Player(game).Engineering,
                ShipSystemKind.DirectedEnergyWeapons
            ),
            0.9,
            0.999
        );
        Assert.True(
            TestEngineering.ConditionOf(
                game.CaptureState().GetRequiredShip(M6CombatProofFixture.Defender).Engineering,
                ShipSystemKind.Shields
            ) < 1
        );
        long elapsed = game.CaptureState().Time.Milliseconds - startedAt;
        Assert.True(elapsed > 1_000_000);
        _fixture.AssertEquivalent(game, Assert.IsType<GameSimulation>(resumed));
        AssertDormantContinuation(game, resumed!);
        Console.WriteLine(
            $"shots={ShotCount}; decisions={decisions}; elapsedMs={elapsed}; maxOutstanding={maximumOutstanding}; workAllocated={game.CaptureState().Scheduler.NextWorkId - startingWorkId}"
        );
    }

    /// <summary>
    /// Runs a populated heterogeneous world — factions A and B, alternate sensors, an absent kind, gapped ids —
    /// through hundreds of shots with recurring return fire, interrupted and completed repairs, scans, and faction
    /// travel. Work and save size stay flat, a mid-horizon V10 resume matches the uninterrupted run byte for byte,
    /// and an independent second run is identical.
    /// </summary>
    [Fact]
    public void HeterogeneousMixedWorldStaysBoundedDeterministicAndResumable()
    {
        MixedRun first = RunMixed(resumeAtShot: MixedShotCount / 2);
        MixedRun second = RunMixed(resumeAtShot: null);

        Assert.Equal(first.FinalSave, second.FinalSave);
        Assert.Equal(first.EventLog, second.EventLog);
        Assert.Equal(first.FinalSave, first.ResumedFinalSave);

        // Measured: the declared ceiling is SimulationScheduler.MaximumOutstandingWork (69,120, six correlated kinds
        // for each of 256 ships plus faction work). This five-ship world measured at most 5 (engagement start-up) and
        // 2 in steady state; the flat-queue rule compares the two halves of the horizon rather than pinning either.
        Assert.InRange(first.MaximumOutstanding, 1, SimulationScheduler.MaximumOutstandingWork);
        Assert.True(first.LateMaximumOutstanding <= first.EarlyMaximumOutstanding, first.Describe());
        Assert.Contains(first.EventLog, line => line.StartsWith("SystemRepairInterrupted", StringComparison.Ordinal));
        Assert.Contains(first.EventLog, line => line.StartsWith("SystemRepairCompleted", StringComparison.Ordinal));
        Assert.Contains(first.EventLog, line => line.StartsWith("ActiveSensorScanCompleted", StringComparison.Ordinal));
        Assert.Contains(first.EventLog, line => line.StartsWith("OwnSystemDamaged", StringComparison.Ordinal));
        Assert.True(first.FactionTravelArrivals > 0, first.Describe());

        // Measured save sizes: the first sample precedes scan identification and faction reports; after it only
        // numeric widths (times, conditions) vary. The 256-byte allowance is a conservative bound on those widths,
        // far below what a per-shot leak would add over 224 further shots.
        int[] settled = [.. first.SaveSizes.Skip(1)];
        Assert.True(settled.Max() - settled.Min() <= 256, first.Describe());
        Assert.True(first.DormantOutstanding <= first.LateMaximumOutstanding, first.Describe());
        Assert.True(first.SaveSizes.Max() < SaveEnvelopeBytes, first.Describe());
        Console.WriteLine(first.Describe());
    }

    /// <summary>
    /// The widest supported installed-system shape — 256 ships, five installations each (one per kind, the typed
    /// maximum) at nineteen-digit installed ids with an exhausted allocator, 64-character definition ids, and
    /// seven-digit allocations, plus an active repair and weapon readiness on every ship — round-trips byte-stably
    /// and within the save envelope.
    /// </summary>
    /// <remarks>
    /// The measured delta against the same world at narrow identities is compared with the conservative per-ship V10
    /// engineering/readiness/scan alternative of design §7.6 (4,783 bytes per ship, which assumes the storage maximum
    /// of 16 installations). The measured value is pinned so a width regression is noticed; the conservative bound
    /// is the contract.
    /// </remarks>
    [Fact]
    public void HighWidthInstalledShapeRoundTripsWithinSaveEnvelope()
    {
        const long conservativePerShipDeltaBytes = 4_783;
        (GameSimulation wide, ShipDefinitionCatalog wideCatalog) = CreateWidthWorld(highWidth: true);
        (GameSimulation narrow, _) = CreateWidthWorld(highWidth: false);

        byte[] saved = GamePersistence.Serialize(wide, Milestone3ProofFixture.Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, wideCatalog, "high-width-installed.json");
        int narrowBytes = GamePersistence.Serialize(narrow, Milestone3ProofFixture.Metadata).Length;

        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
        SimulationState restored = loaded.Simulation.CaptureState();
        Assert.Equal(SimulationState.MaximumShips, restored.Ships.Length);
        Assert.All(
            restored.Ships,
            ship =>
            {
                Assert.Equal(5, ship.Engineering.Systems.Count);
                Assert.True(ship.Engineering.InstallationIds.IsExhausted);
                Assert.Equal(InstalledSystemId.MaximumValue, ship.Engineering.Systems.ByIdentity[^1].Id.Value);
                Assert.NotNull(ship.Engineering.ActiveRepair);
                Assert.Single(ship.Combat.WeaponReadiness);
            }
        );
        // Measured: 467,097 bytes wide versus 345,162 narrow, a 121,935-byte delta (about 476 bytes per ship).
        Assert.Equal(467_097, saved.Length);
        Assert.Equal(121_935, saved.Length - narrowBytes);
        Assert.InRange(saved.Length - narrowBytes, 1, SimulationState.MaximumShips * conservativePerShipDeltaBytes);
        Assert.InRange(saved.Length, 1, SaveEnvelopeBytes);
        Console.WriteLine(
            $"highWidthBytes={saved.Length}; narrowBytes={narrowBytes}; delta={saved.Length - narrowBytes}"
        );
    }

    private static MixedRun RunMixed(int? resumeAtShot)
    {
        var world = new HeterogeneousCombatWorld(baseDamage: 0.0001, withFactions: true);
        GameSimulation game = world.Engaged();
        GameSimulation? resumed = null;
        var run = new MixedRun();
        SensorContactId defender = HeterogeneousCombatWorld.ContactOf(game, HeterogeneousCombatWorld.Defender);
        for (int shot = 0; shot < MixedShotCount; shot++)
        {
            if (shot > 0)
            {
                SimulationTime readyAt = PlayerWeaponReadyAt(game);
                game = AdvanceMixed(world, game, readyAt, run);
                resumed = resumed is null ? null : AdvanceMixed(world, resumed, readyAt, null);
                FireAtDefender(game, defender);
                if (resumed is not null)
                    FireAtDefender(resumed, defender);
            }

            if (shot % MixedWeaponRepairInterval == 0)
            {
                // Return fire lands on this weapon every cycle, so each attempt is interrupted: the repair slot keeps
                // being reused without leaking scheduled work.
                TryRepairWeapon(game);
                if (resumed is not null)
                    TryRepairWeapon(resumed);
            }

            run.Sample(shot, game.CaptureState(), MixedShotCount);
            if (shot % MixedSizeSampleInterval == 0)
                run.SaveSizes.Add(GamePersistence.Serialize(game, HeterogeneousCombatWorld.Metadata).Length);
            if (shot == resumeAtShot)
                resumed = RoundTrip(world, game);
        }

        // A dormant hour after the last shot: no stimulus, repair, or scan remains to schedule, so the horizon ends
        // with no work beyond what the settled world already carried.
        SimulationTime dormantUntil = game.CaptureState()
            .Time.AdvanceBy(new SimulationDuration(MixedDormantMilliseconds));
        game = AdvanceMixed(world, game, dormantUntil, run);
        resumed = resumed is null ? null : AdvanceMixed(world, resumed, dormantUntil, null);
        run.DormantOutstanding = game.CaptureState().Scheduler.OutstandingWork.Length;
        run.FinalSave = GamePersistence.Serialize(game, HeterogeneousCombatWorld.Metadata);
        run.ResumedFinalSave = resumed is null
            ? []
            : GamePersistence.Serialize(resumed, HeterogeneousCombatWorld.Metadata);
        return run;
    }

    private static GameSimulation AdvanceMixed(
        HeterogeneousCombatWorld world,
        GameSimulation game,
        SimulationTime target,
        MixedRun? run
    )
    {
        SimulationAdvanceTraceResult advanced = GameSimulation.AdvanceTo(
            game.CaptureState(),
            target,
            world.Catalog,
            world.Factions
        );
        run?.Record(advanced);
        return world.Restore(advanced.State);
    }

    private static void FireAtDefender(GameSimulation game, SensorContactId defender) =>
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(defender, ShipSystemKind.DirectedEnergyWeapons)).Outcome
        );

    private static void TryRepairWeapon(GameSimulation game)
    {
        ShipState player = game.CaptureState().GetRequiredShip(HeterogeneousCombatWorld.Player);
        if (
            player.Engineering.ActiveRepair is null
            && player.Engineering.Systems.GetRequired(HeterogeneousCombatWorld.PlayerWeapons).Condition.Value < 1
        )
        {
            Assert.Equal(
                SystemRepairOutcome.Accepted,
                game.BeginSystemRepair(HeterogeneousCombatWorld.PlayerWeapons, new SystemCondition(1)).Outcome
            );
        }
    }

    private static SimulationTime PlayerWeaponReadyAt(GameSimulation game) =>
        game.CaptureState()
            .GetRequiredShip(HeterogeneousCombatWorld.Player)
            .Combat.ReadinessOf(HeterogeneousCombatWorld.PlayerWeapons)!
            .ReadyAt;

    private static GameSimulation RoundTrip(HeterogeneousCombatWorld world, GameSimulation game) =>
        GamePersistence
            .Deserialize(
                GamePersistence.Serialize(game, HeterogeneousCombatWorld.Metadata),
                world.Catalog,
                world.Factions,
                "heterogeneous-mixed.json"
            )
            .Simulation;

    /// <summary>
    /// Builds 256 ships whose five installations use either maximum-width identities (ids ending at
    /// <see cref="InstalledSystemId.MaximumValue"/>, exhausted allocator, 64-character definition ids, and a
    /// 1,000,000-unit generator feeding seven-digit allocations) or the narrow 1–5 layout with short ids.
    /// </summary>
    private static (GameSimulation Game, ShipDefinitionCatalog Catalog) CreateWidthWorld(bool highWidth)
    {
        SystemDefinition[] definitions = WidthDefinitions(highWidth);
        ShipDefinitionCatalog catalog = TestShipContent.Catalog(
            TestShipContent.Systems(definitions),
            TestShipContent.Design("pathfinder", "Pathfinder class", definitions)
        );
        long first = highWidth ? InstalledSystemId.MaximumValue - 4 : 1;
        int share = highWidth ? 250_000 : 25;
        var location = new LocationId("width");
        var map = new StrategicMap([new StrategicLocation(location, "Width", default)], []);
        ShipStart[] ships =
        [
            .. Enumerable
                .Range(0, SimulationState.MaximumShips)
                .Select(index => new ShipStart(
                    new ShipInstanceId(index + 1),
                    new ShipDefinitionId("pathfinder"),
                    "Width " + index,
                    // Spread far beyond sensor range so the measurement isolates installed-system width.
                    new TacticalPosition(index * 1_000, 0),
                    default,
                    new AtLocationStart(location),
                    ShipSystemsStart.Explicit(
                        highWidth ? long.MaxValue : 6,
                        definitions.Select(
                            (definition, offset) =>
                                new InstalledSystemStart(
                                    new InstalledSystemId(first + offset),
                                    definition.Id,
                                    new SystemCondition(offset == 1 ? 0.5 : 1),
                                    definition.Power is null ? null : new PowerUnits(share)
                                )
                        )
                    ),
                    new SystemRepairStart(
                        new InstalledSystemId(first + 1),
                        new SystemCondition(0.5),
                        new SystemCondition(1),
                        new SimulationTime(0)
                    )
                )),
        ];
        GameSimulation game = new GameBootstrap(
            new SimulationTime(0),
            map,
            ships[0].InstanceId,
            ships
        ).CreateSimulation(catalog);
        return (game, catalog);
    }

    private static SystemDefinition[] WidthDefinitions(bool highWidth)
    {
        string Id(char marker) =>
            highWidth ? marker + new string('x', SystemDefinitionId.MaximumLength - 1) : marker.ToString();
        int demand = highWidth ? 1_000_000 : 100;
        var repair = new SystemRepairCapability(new SimulationDuration(8000), 100);
        var power = new SystemPowerDemand(new PowerUnits(demand));
        return
        [
            new PowerGenerationSystemDefinition(new(Id('g')), "Generation", 100, true, new PowerUnits(demand)),
            new SensorSystemDefinition(
                new(Id('s')),
                "Sensors",
                200,
                true,
                repair,
                power,
                new DistanceKilometers(30),
                new SimulationDuration(2000)
            ),
            new ImpulsePropulsionSystemDefinition(
                new(Id('i')),
                "Impulse",
                300,
                true,
                repair,
                power,
                new SpeedKilometersPerSecond(10)
            ),
            new ShieldSystemDefinition(new(Id('h')), "Shields", 400, true, repair, power),
            new DirectedEnergyWeaponSystemDefinition(
                new(Id('w')),
                "Weapons",
                500,
                true,
                repair,
                power,
                new DirectedEnergyWeaponDefinition(new DistanceKilometers(20), 0.25, new SimulationDuration(2000))
            ),
        ];
    }

    /// <summary>Measurements collected over one mixed run.</summary>
    private sealed class MixedRun
    {
        internal List<string> EventLog { get; } = [];
        internal List<int> SaveSizes { get; } = [];
        internal byte[] FinalSave { get; set; } = [];
        internal byte[] ResumedFinalSave { get; set; } = [];
        internal int MaximumOutstanding { get; private set; }
        internal int EarlyMaximumOutstanding { get; private set; }
        internal int LateMaximumOutstanding { get; private set; }
        internal int FactionTravelArrivals { get; private set; }
        internal int DormantOutstanding { get; set; }

        internal void Record(SimulationAdvanceTraceResult advanced)
        {
            foreach (PlayerAdvanceEvent item in advanced.PlayerEvents)
            {
                EventLog.Add($"{item.Kind}@{item.OccurredAt.Milliseconds}:{item.InstalledSystemId?.Value}");
            }

            FactionTravelArrivals += advanced.Traces.Count(trace =>
                trace.WorkKind == ScheduledWorkKind.TravelArrival
                && trace.Target.ShipId is { } ship
                && advanced.State.GetRequiredShip(ship).DirectControllerFactionId is not null
            );
        }

        internal void Sample(int shot, SimulationState state, int shots)
        {
            int outstanding = state.Scheduler.OutstandingWork.Length;
            MaximumOutstanding = Math.Max(MaximumOutstanding, outstanding);
            // The first shots include engagement start-up (scan, first repair); the flat-queue comparison starts
            // after that warm-up so it measures steady state against steady state.
            if (shot >= shots / 8 && shot < shots / 2)
                EarlyMaximumOutstanding = Math.Max(EarlyMaximumOutstanding, outstanding);
            else if (shot >= shots / 2)
                LateMaximumOutstanding = Math.Max(LateMaximumOutstanding, outstanding);
        }

        internal string Describe() =>
            $"mixedShots={MixedShotCount}; events={EventLog.Count}; maxOutstanding={MaximumOutstanding}; "
            + $"earlyMax={EarlyMaximumOutstanding}; lateMax={LateMaximumOutstanding}; "
            + $"dormantOutstanding={DormantOutstanding}; factionArrivals={FactionTravelArrivals}; "
            + $"saveSizes=[{string.Join(",", SaveSizes)}]";
    }

    private void AssertDormantContinuation(GameSimulation game, GameSimulation resumed)
    {
        SimulationTime dormantUntil = game.CaptureState().Time.AdvanceBy(new SimulationDuration(60_000));
        SimulationAdvanceTraceResult dormant = _fixture.AdvanceTrace(game, dormantUntil);
        Assert.DoesNotContain(dormant.Traces, trace => trace.CombatDecision is not null);
        Assert.Empty(dormant.State.Scheduler.OutstandingWork);
        _fixture.AssertEquivalent(_fixture.Restore(dormant.State), _fixture.AdvanceTo(resumed, dormantUntil));
    }

    private void AssertPendingCorrelation(SimulationState pending, CombatStimulus stimulus)
    {
        ScheduledWork work = Assert.Single(pending.Scheduler.OutstandingWork);
        Assert.Equal(ScheduledWorkKind.ShipCombatDecisionWake, work.Kind);
        Assert.Equal(stimulus.ScheduledWorkId, work.Id);
        Assert.Equal(stimulus.DueTime, work.DueTime);
        Assert.Equal(M6CombatProofFixture.Defender, work.TargetShipId);
        Assert.Equal(pending.Time, stimulus.ObservedAt);
        Assert.Equal(pending.Time.AdvanceBy(SimulationFixedStep.Duration), stimulus.DueTime);
        Assert.Equal(
            pending
                .GetRequiredShip(M6CombatProofFixture.Defender)
                .SensorKnowledge.Contacts.Single(item => item.TargetShipId == pending.PlayerShipId)
                .Id,
            stimulus.ContactId
        );
        Assert.All(
            pending.Ships.Where(ship => ship.InstanceId != M6CombatProofFixture.Defender),
            ship => Assert.Null(ship.Combat.PendingStimulus)
        );
        pending.Validate(_fixture.Catalog);
    }

    private (GameSimulation Game, GameSimulation? Resumed) AdvanceTogether(
        GameSimulation game,
        GameSimulation? resumed,
        SimulationTime time,
        bool responding
    )
    {
        SimulationAdvanceTraceResult advanced = _fixture.AdvanceTrace(game, time);
        if (responding)
        {
            ScheduledConsequenceTrace trace = Assert.Single(advanced.Traces, item => item.CombatDecision is not null);
            Assert.Equal(DefensiveCombatDecisionAction.ReturnFire, trace.CombatDecision!.SelectedAction);
            Assert.Equal(FireDirectedEnergyOutcome.Accepted, trace.CombatDecision.ApplicationOutcome);
        }
        else
            Assert.DoesNotContain(advanced.Traces, trace => trace.CombatDecision is not null);
        GameSimulation next = _fixture.Restore(advanced.State);
        Assert.Empty(next.CaptureState().Scheduler.OutstandingWork);
        Assert.All(next.CaptureState().Ships, ship => Assert.Null(ship.Combat.PendingStimulus));
        GameSimulation? loadedNext = null;
        if (resumed is not null)
        {
            SimulationAdvanceTraceResult loaded = _fixture.AdvanceTrace(resumed, time);
            Assert.Equal(advanced.Traces.ToArray(), loaded.Traces.ToArray());
            Assert.Equal(advanced.PlayerEvents.ToArray(), loaded.PlayerEvents.ToArray());
            loadedNext = _fixture.Restore(loaded.State);
            _fixture.AssertEquivalent(next, loadedNext);
        }
        return (next, loadedNext);
    }
}
