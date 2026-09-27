using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tests.Gameplay;
using static AlterCourse.Core.Tests.Characterization.M6ABaselineRecords;
using CorePowerAllocationOutcome = AlterCourse.Core.Gameplay.PowerAllocationOutcome;
using CorePowerAllocationResult = AlterCourse.Core.Gameplay.PowerAllocationResult;
using CoreSystemRepairOutcome = AlterCourse.Core.Gameplay.SystemRepairOutcome;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// The single adapter between the M6A baseline characterization and Core's ship-system representation.
/// </summary>
/// <remarks>
/// <para>
/// CONTRACT: this class is the only code under <c>Characterization/</c> allowed to touch Core's ship-system
/// representation — installed systems and their definitions, the exact <see cref="PowerAllocation"/> keyed by
/// installed id, <see cref="SystemRepairState"/>, weapon readiness, the Engineering projection, and every command
/// that selects an installation. It translates them into the representation-neutral records of
/// <see cref="M6ABaselineRecords"/> and translates canonical tuples back into commands.
/// </para>
/// <para>
/// Each record field keeps its M6A meaning: <c>Conditions.Shields</c> is the condition of the ship's sole shield
/// installation (resolved by kind through the typed singleton rule), an allocation tuple is the exact allocation of
/// the four consumer installations by kind, a kind-keyed command addresses the ship's installation of that kind,
/// and Core's generic outcomes are mapped back to the frozen M6A vocabulary (<see cref="PowerAllocationOutcome"/>,
/// <see cref="SystemRepairOutcome"/>). Do not edit <see cref="M6ABaselineExpectations"/> to make a migrated run
/// pass — that file is the baseline.
/// </para>
/// <para>
/// Doubles are rounded to <see cref="PinnedDigits"/> decimal places unless the probe is built with
/// <c>exact: true</c>. Pinned expectations use rounding so literals stay readable (0.55, not
/// 0.55000000000000004); continuation equivalence uses exact probes so a save/load drift of any size fails.
/// </para>
/// </remarks>
internal sealed class M6ABaselineProbe(ShipDefinitionCatalog catalog, bool exact = false)
{
    internal const int PinnedDigits = 9;

    internal DefinitionFacts Definition(string definitionId)
    {
        // Tuning now lives on the system definitions the design's default loadout references, by kind.
        ShipDefinition design = catalog.GetRequired(new ShipDefinitionId(definitionId));
        SystemDefinition[] loadout =
        [
            .. design.InitialLoadout.Systems.Select(system =>
                catalog.SystemDefinitions.GetRequired(system.DefinitionId)
            ),
        ];
        T Of<T>()
            where T : SystemDefinition => loadout.OfType<T>().Single();
        PowerGenerationSystemDefinition generation = Of<PowerGenerationSystemDefinition>();
        SensorSystemDefinition sensors = Of<SensorSystemDefinition>();
        ImpulsePropulsionSystemDefinition impulse = Of<ImpulsePropulsionSystemDefinition>();
        ShieldSystemDefinition shields = Of<ShieldSystemDefinition>();
        DirectedEnergyWeaponSystemDefinition weapons = Of<DirectedEnergyWeaponSystemDefinition>();
        DirectedEnergyWeaponDefinition weapon = weapons.Weapon;
        return new DefinitionFacts(
            generation.NominalOutput.Value,
            new AllocationTuple(
                sensors.Power!.NominalDemand.Value,
                impulse.Power!.NominalDemand.Value,
                shields.Power!.NominalDemand.Value,
                weapons.Power!.NominalDemand.Value
            ),
            Round(sensors.PassiveRange.Value),
            Round(impulse.MaximumTacticalSpeed.Value),
            sensors.ActiveScanDuration.Milliseconds,
            Round(weapon.Range.Value),
            Round(weapon.BaseNormalizedDamage),
            weapon.Cooldown.Milliseconds,
            new RepairDurations(
                sensors.Repair!.FullRepairDuration.Milliseconds,
                impulse.Repair!.FullRepairDuration.Milliseconds,
                shields.Repair!.FullRepairDuration.Milliseconds,
                weapons.Repair!.FullRepairDuration.Milliseconds
            )
        );
    }

    internal ShipOutcome Ship(GameSimulation game, long shipId) =>
        Ship(game.CaptureState(), new ShipInstanceId(shipId));

    internal ShipOutcome Player(GameSimulation game) => Ship(game.CaptureState(), game.CaptureState().PlayerShipId);

    internal OutcomeSequence<ShipOutcome> AllShips(GameSimulation game)
    {
        SimulationState state = game.CaptureState();
        return Sequence([.. state.Ships.Select(ship => Ship(state, ship.InstanceId))]);
    }

    internal static long PlayerShipId(GameSimulation game) => game.CaptureState().PlayerShipId.Value;

    internal static long TimeMs(GameSimulation game) => game.CaptureState().Time.Milliseconds;

    internal PlayerViewOutcome View(GameSimulation game)
    {
        PlayerShipProjection ship = game.GetPlayerProjection().Ship;
        EngineeringProjection engineering = ship.Engineering;
        return new PlayerViewOutcome(
            engineering.NominalGeneration.Value,
            engineering.AvailablePower.Value,
            engineering.Reserve.Value,
            new AllocationTuple(
                RowAllocation(engineering, ShipSystemKind.Sensors),
                RowAllocation(engineering, ShipSystemKind.ImpulsePropulsion),
                RowAllocation(engineering, ShipSystemKind.Shields),
                RowAllocation(engineering, ShipSystemKind.DirectedEnergyWeapons)
            ),
            Round(engineering.EffectivePassiveSensorRange.Value),
            Round(engineering.EffectiveMaximumTacticalSpeed.Value),
            engineering.ActiveRepair is null ? null : Round(engineering.ActiveRepair.Progress),
            ship.Sensors.ActiveScanProgress is { } progress ? Round(progress) : null,
            (
                ship.Combat.NextDirectedEnergyReadyAt
                ?? throw new InvalidOperationException("Every M6A baseline scenario player has a weapon.")
            ).Milliseconds
        );
    }

    // The baseline scenarios all use the five-installation production loadout, so each kind has exactly one
    // projected row; Single() failing here would mean a scenario no longer matches the pinned records.
    private static int RowAllocation(EngineeringProjection engineering, ShipSystemKind kind) =>
        engineering.Systems.Single(row => row.Kind == kind).Allocation!.Value.Value;

    internal static OutcomeSequence<EventOutcome> Events(IEnumerable<PlayerAdvanceEvent> events) =>
        Sequence([
            .. events.Select(item => new EventOutcome(
                item.Kind,
                item.OccurredAt.Milliseconds,
                item.SensorContactId?.Value,
                item.SystemKind
            )),
        ]);

    internal DefenseOutcome? Defense(GameSimulation game)
    {
        DefensiveCombatDecisionExplanation? decision = game.LastDefensiveCombatDecisionExplanation;
        if (decision is null)
            return null;
        return new DefenseOutcome(
            decision.SelectedAction,
            decision.TieRule,
            Sequence([.. decision.Candidates.Select(item => item.Action)]),
            decision.Candidates.Single(item => item.Action == DefensiveCombatDecisionAction.ReturnFire).FireRejection,
            decision.TargetSystem,
            decision.ResultingCourse is { } course ? Round(course.Heading.Value) : null,
            decision.ResultingCourse is { } resulting ? Round(resulting.Speed.Value) : null,
            decision.ApplicationOutcome,
            decision.CourseOutcome
        );
    }

    /// <summary>Returns the observer's contact id for a target ship; knowledge identity, not target identity.</summary>
    internal static long ContactOf(GameSimulation game, long observerId, long targetShipId) =>
        game.CaptureState()
            .GetRequiredShip(new ShipInstanceId(observerId))
            .SensorKnowledge.Contacts.Single(item => item.TargetShipId == new ShipInstanceId(targetShipId))
            .Id.Value;

    internal static PowerAllocationResult SetAllocation(GameSimulation game, AllocationTuple allocation) =>
        Translate(game, game.SetPowerAllocation(ToAllocation(PlayerEngineering(game), allocation)));

    /// <summary>Applies Balanced, or the priority allocation for the player's installation of a consumer kind.</summary>
    internal static PowerAllocationResult ApplyPreset(GameSimulation game, PresetChoice choice) =>
        Translate(
            game,
            choice.Priority is { } kind
                ? game.ApplyPriorityAllocation(Installed(PlayerEngineering(game), kind).Id)
                : game.ApplyBalancedAllocation()
        );

    internal static SystemRepairResult BeginRepair(GameSimulation game, ShipSystemKind system, double target) =>
        new(
            game.BeginSystemRepair(
                Installed(PlayerEngineering(game), system).Id,
                new SystemCondition(target)
            ).Outcome switch
            {
                CoreSystemRepairOutcome.Accepted => SystemRepairOutcome.Accepted,
                CoreSystemRepairOutcome.RepairAlreadyActive => SystemRepairOutcome.RepairAlreadyActive,
                CoreSystemRepairOutcome.TargetDoesNotImproveCondition =>
                    SystemRepairOutcome.TargetDoesNotImproveCondition,
                CoreSystemRepairOutcome.NotRepairable => SystemRepairOutcome.UnsupportedSystem,
                var other => throw new InvalidOperationException($"Repair outcome {other} has no M6A equivalent."),
            }
        );

    internal static FireDirectedEnergyResult Fire(GameSimulation game, long contactId, ShipSystemKind system) =>
        game.FireDirectedEnergy(new(new SensorContactId(contactId), system));

    /// <summary>
    /// Commits one trusted NPC shot from the proof defender at the player's <paramref name="system"/>, returning
    /// the continued game and the player-visible events.
    /// </summary>
    internal static (GameSimulation Game, OutcomeSequence<EventOutcome> Events) Incoming(
        M6CombatProofFixture fixture,
        GameSimulation game,
        ShipSystemKind system
    )
    {
        ShipDirectedEnergyApplicationResult hit = fixture.Incoming(game, system);
        return (fixture.Restore(hit.CandidateState), Events(hit.ResolvedEvents));
    }

    /// <summary>Authors a trusted exact allocation on any ship; used only to shape NPC scenario starts.</summary>
    internal static GameSimulation WithAllocation(
        M6CombatProofFixture fixture,
        GameSimulation game,
        long shipId,
        AllocationTuple allocation
    )
    {
        SimulationState state = game.CaptureState();
        ShipState ship = state.GetRequiredShip(new ShipInstanceId(shipId));
        return fixture.Restore(
            state.ReplaceShip(
                ship.InstanceId,
                ship with
                {
                    Engineering = ship.Engineering.WithAllocation(ToAllocation(ship.Engineering, allocation)),
                }
            )
        );
    }

    /// <summary>Builds a complete exact allocation over the ship's four consumer installations, resolved by kind.</summary>
    private static PowerAllocation ToAllocation(ShipEngineeringState engineering, AllocationTuple allocation) =>
        new([
            new(Installed(engineering, ShipSystemKind.Sensors).Id, new PowerUnits(allocation.Sensors)),
            new(Installed(engineering, ShipSystemKind.ImpulsePropulsion).Id, new PowerUnits(allocation.Impulse)),
            new(Installed(engineering, ShipSystemKind.Shields).Id, new PowerUnits(allocation.Shields)),
            new(
                Installed(engineering, ShipSystemKind.DirectedEnergyWeapons).Id,
                new PowerUnits(allocation.DirectedEnergy)
            ),
        ]);

    /// <summary>Maps Core's generic allocation outcome back to the frozen M6A vocabulary.</summary>
    /// <remarks>
    /// A consumer-demand refusal names the offending installation; its kind selects the per-kind M6A member. The
    /// probe always sends complete allocations over installed consumers, so incomplete or unknown-consumer outcomes
    /// mean the probe itself is broken and throw.
    /// </remarks>
    private static PowerAllocationResult Translate(GameSimulation game, CorePowerAllocationResult result) =>
        new(
            result.Outcome switch
            {
                CorePowerAllocationOutcome.Accepted => PowerAllocationOutcome.Accepted,
                CorePowerAllocationOutcome.AvailablePowerExceeded => PowerAllocationOutcome.AvailablePowerExceeded,
                CorePowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum =>
                    PowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum,
                CorePowerAllocationOutcome.ConsumerDemandExceeded => DemandOutcome(
                    PlayerEngineering(game).Systems.GetRequired(result.Consumer!.Value).Kind
                ),
                var other => throw new InvalidOperationException($"Allocation outcome {other} has no M6A equivalent."),
            },
            result.ResolvedEvents
        );

    private static PowerAllocationOutcome DemandOutcome(ShipSystemKind kind) =>
        kind == ShipSystemKind.Sensors ? PowerAllocationOutcome.SensorDemandExceeded
        : kind == ShipSystemKind.ImpulsePropulsion ? PowerAllocationOutcome.ImpulseDemandExceeded
        : kind == ShipSystemKind.Shields ? PowerAllocationOutcome.ShieldDemandExceeded
        : kind == ShipSystemKind.DirectedEnergyWeapons ? PowerAllocationOutcome.DirectedEnergyDemandExceeded
        : throw new InvalidOperationException($"Kind {kind.Value} is not a consumer.");

    private static ShipEngineeringState PlayerEngineering(GameSimulation game)
    {
        SimulationState state = game.CaptureState();
        return state.GetRequiredShip(state.PlayerShipId).Engineering;
    }

    private static InstalledSystem Installed(ShipEngineeringState engineering, ShipSystemKind kind) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, kind)
        ?? throw new InvalidOperationException($"The ship has no installed {kind.Value} system.");

    private static int AllocationOf(ShipEngineeringState engineering, ShipSystemKind kind) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, kind)?.Allocation?.Value ?? 0;

    private double ConditionOf(ShipEngineeringState engineering, ShipSystemKind kind) =>
        Round(ShipSystemAdmission.SupportedSingle(engineering.Systems, kind)?.Condition.Value ?? 0);

    private double CapabilityOf(ShipEngineeringState engineering, ShipSystemKind kind) =>
        Round(
            ShipSystemAdmission.SupportedSingle(engineering.Systems, kind) is { } system
                ? ShipEngineeringState.Capability(system)
                : 0
        );

    private ShipOutcome Ship(SimulationState state, ShipInstanceId shipId)
    {
        ShipState ship = state.GetRequiredShip(shipId);
        ActiveSensorScanState? scan = ship.SensorKnowledge.ActiveScan;
        CombatStimulus? stimulus = ship.Combat.PendingStimulus;
        return new ShipOutcome(
            shipId.Value,
            state.Time.Milliseconds,
            Engineering(ship),
            new MotionOutcome(
                Round(ship.TacticalPosition.XKilometers),
                Round(ship.TacticalPosition.YKilometers),
                Round(ship.TacticalMotion.Heading.Value),
                Round(ship.TacticalMotion.Speed.Value)
            ),
            new CombatOutcome(
                ship.Combat.ReadinessOf(
                    Installed(ship.Engineering, ShipSystemKind.DirectedEnergyWeapons).Id
                )!.ReadyAt.Milliseconds,
                stimulus is null
                    ? null
                    : new StimulusOutcome(
                        stimulus.ContactId.Value,
                        stimulus.ObservedAt.Milliseconds,
                        stimulus.DueTime.Milliseconds
                    )
            ),
            Sequence([
                .. ship.SensorKnowledge.Contacts.Select(contact => new ContactOutcome(
                    contact.Id.Value,
                    contact.TargetShipId.Value,
                    contact.Status,
                    contact.Identification,
                    contact.KnownVesselDisplayName,
                    contact.KnownDesignDisplayName
                )),
            ]),
            scan is null
                ? null
                : new ScanOutcome(
                    scan.TargetContactId.Value,
                    scan.StartedAt.Milliseconds,
                    scan.ExpectedCompletion.Milliseconds
                )
        );
    }

    private EngineeringOutcome Engineering(ShipState ship)
    {
        ShipEngineeringState engineering = ship.Engineering;
        SystemRepairState? repair = engineering.ActiveRepair;
        return new EngineeringOutcome(
            engineering.AvailablePower.Value,
            engineering.Reserve.Value,
            new AllocationTuple(
                AllocationOf(engineering, ShipSystemKind.Sensors),
                AllocationOf(engineering, ShipSystemKind.ImpulsePropulsion),
                AllocationOf(engineering, ShipSystemKind.Shields),
                AllocationOf(engineering, ShipSystemKind.DirectedEnergyWeapons)
            ),
            new ConditionSet(
                ConditionOf(engineering, ShipSystemKind.PowerGeneration),
                ConditionOf(engineering, ShipSystemKind.Sensors),
                ConditionOf(engineering, ShipSystemKind.ImpulsePropulsion),
                ConditionOf(engineering, ShipSystemKind.Shields),
                ConditionOf(engineering, ShipSystemKind.DirectedEnergyWeapons)
            ),
            new CapabilitySet(
                CapabilityOf(engineering, ShipSystemKind.Sensors),
                CapabilityOf(engineering, ShipSystemKind.ImpulsePropulsion),
                CapabilityOf(engineering, ShipSystemKind.Shields),
                CapabilityOf(engineering, ShipSystemKind.DirectedEnergyWeapons)
            ),
            repair is null
                ? null
                : new RepairOutcome(
                    engineering.Systems.GetRequired(repair.Target).Kind,
                    Round(repair.StartingCondition.Value),
                    Round(repair.TargetCondition.Value),
                    repair.StartedAt.Milliseconds,
                    repair.ExpectedCompletion.Milliseconds
                )
        );
    }

    private double Round(double value) => exact ? value : Math.Round(value, PinnedDigits, MidpointRounding.ToEven);
}
