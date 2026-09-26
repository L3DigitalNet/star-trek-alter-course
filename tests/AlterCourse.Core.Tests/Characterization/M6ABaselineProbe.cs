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

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// The single adapter between the M6A baseline characterization and Core's ship-system representation.
/// </summary>
/// <remarks>
/// <para>
/// CONTRACT: this class is the only code under <c>Characterization/</c> allowed to touch the fixed-field
/// ship-system API — the five named conditions and <c>Allocation</c> on <see cref="ShipEngineeringState"/>, the
/// four named fields of <see cref="PowerAllocation"/>, the named demand and repair-duration fields of
/// <see cref="ShipEngineeringDefinition"/>, <see cref="SystemRepairState"/>, the named fields of
/// <see cref="EngineeringProjection"/>, directed-energy readiness on the ship combat state, and every command
/// that selects a system by kind. It translates them into the representation-neutral records of
/// <see cref="M6ABaselineRecords"/> and translates canonical tuples back into commands.
/// </para>
/// <para>
/// When issue #121 replaces named fields with installed-system instances, rewrite this class (and scenario
/// setup in <see cref="M6ABaselineScenarios"/> only where it authors state) so each record field keeps its
/// meaning: <c>Conditions.Shields</c> is the condition of the ship's shield installation, an allocation tuple is
/// the exact allocation per installed consumer in canonical order, and so on. Do not edit
/// <see cref="M6ABaselineExpectations"/> to make a migrated run pass — that file is the baseline.
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
        ShipDefinition definition = catalog.GetRequired(new ShipDefinitionId(definitionId));
        ShipEngineeringDefinition engineering = definition.Engineering;
        DirectedEnergyWeaponDefinition weapon = definition.DirectedEnergyWeapon!;
        return new DefinitionFacts(
            engineering.NominalGeneration.Value,
            new AllocationTuple(
                engineering.NominalSensorDemand.Value,
                engineering.NominalImpulseDemand.Value,
                engineering.NominalShieldDemand.Value,
                engineering.NominalDirectedEnergyDemand.Value
            ),
            Round(definition.PassiveSensorRange.Value),
            Round(definition.MaximumTacticalSpeed.Value),
            definition.ActiveScanDuration.Milliseconds,
            Round(weapon.Range.Value),
            Round(weapon.BaseNormalizedDamage),
            weapon.Cooldown.Milliseconds,
            new RepairDurations(
                engineering.SensorRepairDuration.Milliseconds,
                engineering.ImpulseRepairDuration.Milliseconds,
                engineering.ShieldRepairDuration.Milliseconds,
                engineering.DirectedEnergyRepairDuration.Milliseconds
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
                engineering.SensorAllocation.Value,
                engineering.ImpulseAllocation.Value,
                engineering.ShieldAllocation.Value,
                engineering.DirectedEnergyAllocation.Value
            ),
            Round(engineering.EffectivePassiveSensorRange.Value),
            Round(engineering.EffectiveMaximumTacticalSpeed.Value),
            engineering.ActiveRepair is null ? null : Round(engineering.ActiveRepair.Progress),
            ship.Sensors.ActiveScanProgress is { } progress ? Round(progress) : null,
            ship.Combat.NextDirectedEnergyReadyAt.Milliseconds
        );
    }

    internal static OutcomeSequence<EventOutcome> Events(IEnumerable<PlayerAdvanceEvent> events) =>
        Sequence([
            .. events.Select(item => new EventOutcome(
                item.Kind,
                item.OccurredAt.Milliseconds,
                item.SensorContactId?.Value,
                item.ShipSystemId
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
        game.SetPowerAllocation(ToAllocation(allocation));

    /// <summary>Applies a Core-generated preset; priority presets are selected by consumer kind.</summary>
    internal static PowerAllocationResult ApplyPreset(GameSimulation game, PresetChoice choice) =>
        game.ApplyPowerAllocationPreset(
            choice.Priority switch
            {
                null => PowerAllocationPreset.Balanced,
                { } kind when kind == ShipSystemKind.Sensors => PowerAllocationPreset.PrioritizeSensors,
                { } kind when kind == ShipSystemKind.ImpulsePropulsion => PowerAllocationPreset.PrioritizePropulsion,
                { } kind when kind == ShipSystemKind.Shields => PowerAllocationPreset.PrioritizeShields,
                { } kind when kind == ShipSystemKind.DirectedEnergyWeapons =>
                    PowerAllocationPreset.PrioritizeDirectedEnergyWeapons,
                { } kind => throw new ArgumentOutOfRangeException(nameof(choice), kind, "Kind is not a consumer."),
            }
        );

    internal static SystemRepairResult BeginRepair(GameSimulation game, ShipSystemKind system, double target) =>
        game.BeginSystemRepair(system, new SystemCondition(target));

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
                    Engineering = ship.Engineering with { Allocation = ToAllocation(allocation) },
                }
            )
        );
    }

    private static PowerAllocation ToAllocation(AllocationTuple allocation) =>
        new(
            new PowerUnits(allocation.Sensors),
            new PowerUnits(allocation.Impulse),
            new PowerUnits(allocation.Shields),
            new PowerUnits(allocation.DirectedEnergy)
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
                ship.Combat.NextDirectedEnergyReadyAt.Milliseconds,
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
        ShipEngineeringDefinition definition = catalog.GetRequired(ship.DefinitionId).Engineering;
        ShipEngineeringState engineering = ship.Engineering;
        SystemRepairState? repair = engineering.ActiveRepair;
        return new EngineeringOutcome(
            engineering.AvailablePower(definition).Value,
            engineering.Reserve(definition).Value,
            new AllocationTuple(
                engineering.Allocation.Sensors.Value,
                engineering.Allocation.ImpulsePropulsion.Value,
                engineering.Allocation.Shields.Value,
                engineering.Allocation.DirectedEnergyWeapons.Value
            ),
            new ConditionSet(
                Round(engineering.GenerationCondition.Value),
                Round(engineering.SensorCondition.Value),
                Round(engineering.ImpulseCondition.Value),
                Round(engineering.ShieldCondition.Value),
                Round(engineering.DirectedEnergyCondition.Value)
            ),
            new CapabilitySet(
                Round(engineering.SensorCapability(definition)),
                Round(engineering.ImpulseCapability(definition)),
                Round(engineering.ShieldCapability(definition)),
                Round(engineering.DirectedEnergyCapability(definition))
            ),
            repair is null
                ? null
                : new RepairOutcome(
                    repair.TargetSystem,
                    Round(repair.StartingCondition.Value),
                    Round(repair.TargetCondition.Value),
                    repair.StartedAt.Milliseconds,
                    repair.ExpectedCompletion.Milliseconds
                )
        );
    }

    private double Round(double value) => exact ? value : Math.Round(value, PinnedDigits, MidpointRounding.ToEven);
}
