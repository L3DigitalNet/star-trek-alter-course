using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

public sealed partial class GameSimulation
{
    internal DefensiveCombatDecisionExplanation? LastDefensiveCombatDecisionExplanation { get; private set; }

    /// <summary>Atomically fires at a concrete system using only the player's current identified local contact.</summary>
    public FireDirectedEnergyResult FireDirectedEnergy(FireDirectedEnergyIntent intent)
    {
        List<PlayerAdvanceEvent> events = [];
        CombatApplication application = ApplyDirectedEnergy(
            _state,
            _shipCatalog,
            _state.PlayerShipId,
            intent,
            events,
            null
        );
        if (application.Outcome == FireDirectedEnergyOutcome.Accepted)
            Commit(application.State);
        return new FireDirectedEnergyResult(application.Outcome, new ReadOnlyValueList<PlayerAdvanceEvent>(events));
    }

    /// <summary>Applies the same actor-local shot transition for trusted Core callers without committing an aggregate.</summary>
    internal static ShipDirectedEnergyApplicationResult ApplyShipDirectedEnergy(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipInstanceId attackerId,
        FireDirectedEnergyIntent intent
    )
    {
        List<PlayerAdvanceEvent> events = [];
        CombatApplication result = ApplyDirectedEnergy(state, catalog, attackerId, intent, events, null);
        return new ShipDirectedEnergyApplicationResult(
            result.Outcome,
            result.State,
            new ReadOnlyValueList<PlayerAdvanceEvent>(events)
        );
    }

    private static CombatOwnFacts CombatFacts(SimulationState state, ShipState ship, ShipDefinition definition) =>
        new(
            state.Time,
            ship.StrategicState is AtLocationState,
            ship.TacticalPosition,
            ship.TacticalMotion,
            EffectiveMaximumTacticalSpeed(definition, ship.Engineering),
            definition.DirectedEnergyWeapon,
            ship.Engineering.DirectedEnergyCondition,
            ship.Engineering.DirectedEnergyCapability(definition.Engineering),
            ship.Combat.NextDirectedEnergyReadyAt
        );

    private static bool SameCombatContext(ShipState observer, ShipState target) =>
        observer.StrategicState is AtLocationState own
        && target.StrategicState is AtLocationState other
        && own.LocationId == other.LocationId;

    private sealed record CombatApplication(FireDirectedEnergyOutcome Outcome, SimulationState State);

    private static CombatApplication ApplyDirectedEnergy(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipInstanceId attackerId,
        FireDirectedEnergyIntent intent,
        List<PlayerAdvanceEvent> events,
        HashSet<ScheduledWork>? canceledRepairs,
        ObservationPublicationCollector? collector = null
    )
    {
        ShipState attacker = state.GetRequiredShip(attackerId);
        ShipDefinition definition = catalog.GetRequired(attacker.DefinitionId);
        SensorContactTrack? contact = attacker.SensorKnowledge.Contacts.FirstOrDefault(item =>
            item.Id == intent.ContactId
        );
        ShipState? victim = contact is null ? null : state.GetRequiredShip(contact.TargetShipId);
        FireDirectedEnergyOutcome outcome = CombatLegality.Evaluate(
            CombatFacts(state, attacker, definition),
            contact?.ToActorSafeSnapshot(),
            victim is not null && SameCombatContext(attacker, victim),
            intent.TargetSystem
        );
        if (outcome != FireDirectedEnergyOutcome.Accepted)
            return new CombatApplication(outcome, state);

        return ApplyAcceptedShot(
            state,
            catalog,
            attacker,
            definition,
            victim!,
            contact!,
            intent,
            events,
            canceledRepairs,
            collector
        );
    }

    private static CombatApplication ApplyAcceptedShot(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipState attacker,
        ShipDefinition definition,
        ShipState victim,
        SensorContactTrack contact,
        FireDirectedEnergyIntent intent,
        List<PlayerAdvanceEvent> events,
        HashSet<ScheduledWork>? canceledRepairs,
        ObservationPublicationCollector? collector
    )
    {
        SensorContactId? observedAttacker = victim!
            .SensorKnowledge.Contacts.FirstOrDefault(item =>
                item.TargetShipId == attacker.InstanceId && item.Status == SensorContactStatus.Current
            )
            ?.Id;
        ShipDefinition victimDefinition = catalog.GetRequired(victim.DefinitionId);
        ShipEngineeringState before = victim.Engineering;
        double damage =
            definition.DirectedEnergyWeapon!.BaseNormalizedDamage
            * attacker.Engineering.DirectedEnergyCapability(definition.Engineering);
        ShieldDamageResult impact = ShieldDamage.Resolve(
            damage,
            before.ShieldCondition,
            before.ShieldPowerSatisfaction(victimDefinition.Engineering)
        );
        ShipEngineeringState damaged = before with { ShieldCondition = impact.ShieldCondition };
        damaged = damaged.WithCondition(intent.TargetSystem, impact.ApplyTo(damaged.ConditionFor(intent.TargetSystem)));
        DamageRepairResolution repairResult = CancelHitRepair(
            state,
            victim,
            damaged,
            impact,
            intent.TargetSystem,
            canceledRepairs
        );
        damaged = repairResult.Engineering;
        SimulationScheduler scheduler = repairResult.Scheduler;
        bool repairInterrupted = repairResult.Interrupted;
        ShipState hit = ReconcileDamagedShip(victim, victimDefinition, damaged);
        ShipState fired = attacker with
        {
            Combat = attacker.Combat with
            {
                NextDirectedEnergyReadyAt = state.Time.AdvanceBy(definition.DirectedEnergyWeapon.Cooldown),
            },
        };
        SimulationState candidate = state
            .ReplaceShip(attacker.InstanceId, fired)
            .ReplaceShip(victim.InstanceId, hit) with
        {
            Scheduler = scheduler,
        };
        ReportCombatImpact(
            state,
            attacker.InstanceId,
            contact!,
            victim,
            intent.TargetSystem,
            before,
            impact,
            repairInterrupted,
            hit.Engineering.Allocation,
            hit.TacticalMotion,
            observedAttacker,
            events
        );
        candidate = ObserveAllShips(candidate, catalog, events, collector);
        candidate = AdmitCombatStimulus(candidate, candidate.GetRequiredShip(victim.InstanceId), observedAttacker);
        return new CombatApplication(FireDirectedEnergyOutcome.Accepted, candidate);
    }

    private static ShipState ReconcileDamagedShip(
        ShipState victim,
        ShipDefinition definition,
        ShipEngineeringState damaged
    )
    {
        PowerAllocation reconciled = damaged.ReconcileAvailablePower(definition.Engineering);
        damaged = damaged with { Allocation = reconciled };
        double maxSpeed = EffectiveMaximumTacticalSpeed(definition, damaged).Value;
        TacticalMotion motion =
            victim.TacticalMotion.Speed.Value > maxSpeed
                ? new TacticalMotion(victim.TacticalMotion.Heading, new SpeedKilometersPerSecond(maxSpeed))
                : victim.TacticalMotion;
        return victim with { Engineering = damaged, TacticalMotion = motion };
    }

    private sealed record DamageRepairResolution(
        ShipEngineeringState Engineering,
        SimulationScheduler Scheduler,
        bool Interrupted
    );

    private static DamageRepairResolution CancelHitRepair(
        SimulationState state,
        ShipState victim,
        ShipEngineeringState damaged,
        ShieldDamageResult impact,
        ShipSystemKind targetSystem,
        HashSet<ScheduledWork>? canceledRepairs
    )
    {
        SimulationScheduler scheduler = state.Scheduler;
        bool repairInterrupted = false;
        if (
            victim.Engineering.ActiveRepair is { } repair
            && (
                (repair.TargetSystem == ShipSystemKind.Shields && impact.AbsorbedDamage > 0)
                || (repair.TargetSystem == targetSystem && impact.PenetratingDamage > 0)
            )
        )
        {
            // Every due item has already left the scheduler during a batch. Preserve the entire exact correlation
            // here so only this canceled completion can be suppressed later; unmatched orphan repairs still fail.
            var canceled = new ScheduledWork(
                repair.ScheduledCompletionId,
                repair.ExpectedCompletion,
                0,
                victim.InstanceId,
                ScheduledWorkKind.SystemRepairCompletion
            );
            (scheduler, bool removed) = scheduler.Cancel(repair.ScheduledCompletionId);
            if (!removed && (canceledRepairs is null || repair.ExpectedCompletion != state.Time))
                throw new InvalidOperationException("Damage cancellation lacks the exact repair completion.");
            canceledRepairs?.Add(canceled);
            damaged = damaged with { ActiveRepair = null };
            repairInterrupted = true;
        }
        return new DamageRepairResolution(damaged, scheduler, repairInterrupted);
    }

    private static void ReportCombatImpact(
        SimulationState state,
        ShipInstanceId attackerId,
        SensorContactTrack contact,
        ShipState victim,
        ShipSystemKind targetSystem,
        ShipEngineeringState before,
        ShieldDamageResult impact,
        bool repairInterrupted,
        PowerAllocation reconciled,
        TacticalMotion motion,
        SensorContactId? observedAttacker,
        List<PlayerAdvanceEvent> events
    )
    {
        if (attackerId == state.PlayerShipId)
        {
            events.Add(
                new PlayerAdvanceEvent(PlayerAdvanceEventKind.DirectedEnergyFired, state.Time, contact.Id, targetSystem)
            );
            if (impact.AbsorbedDamage > 0)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.ShieldImpact,
                        state.Time,
                        contact.Id,
                        ShipSystemKind.Shields
                    )
                );
            if (impact.PenetratingDamage > 0)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.SubsystemPenetration,
                        state.Time,
                        contact.Id,
                        targetSystem
                    )
                );
        }
        if (victim.InstanceId == state.PlayerShipId)
        {
            if (impact.AbsorbedDamage > 0)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.OwnSystemDamaged,
                        state.Time,
                        observedAttacker,
                        ShipSystemKind.Shields
                    )
                );
            if (impact.PenetratingDamage > 0)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.OwnSystemDamaged,
                        state.Time,
                        observedAttacker,
                        targetSystem
                    )
                );
            if (repairInterrupted)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.SystemRepairInterrupted,
                        state.Time,
                        observedAttacker,
                        before.ActiveRepair!.TargetSystem
                    )
                );
            if (reconciled != before.Allocation)
                events.Add(new PlayerAdvanceEvent(PlayerAdvanceEventKind.PowerBrownout, state.Time));
            if (motion != victim.TacticalMotion)
                events.Add(new PlayerAdvanceEvent(PlayerAdvanceEventKind.ForcedDeceleration, state.Time));
        }
    }

    private static SimulationState AdmitCombatStimulus(
        SimulationState state,
        ShipState victim,
        SensorContactId? observedAttacker
    )
    {
        if (victim.InstanceId == state.PlayerShipId || victim.Combat.PendingStimulus is not null)
            return state;
        if (observedAttacker is null)
            return state;
        SimulationTime due = state.Time.AdvanceBy(SimulationFixedStep.Duration);
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            due,
            victim.InstanceId,
            ScheduledWorkKind.ShipCombatDecisionWake
        );
        return state.ReplaceShip(
            victim.InstanceId,
            victim with
            {
                Combat = victim.Combat with
                {
                    PendingStimulus = new CombatStimulus(observedAttacker.Value, state.Time, due, work.Id),
                },
            }
        ) with
        {
            Scheduler = scheduler,
        };
    }

    private static SimulationState CompleteCombatWake(
        SimulationState state,
        ScheduledWork work,
        ShipDefinitionCatalog catalog,
        List<PlayerAdvanceEvent> events,
        HashSet<ScheduledWork> canceledRepairs,
        List<ScheduledConsequenceTrace> traces,
        ObservationPublicationCollector collector
    )
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        CombatStimulus stimulus =
            ship.Combat.PendingStimulus ?? throw new InvalidOperationException("Combat wake lacks stimulus.");
        if (stimulus.ScheduledWorkId != work.Id || stimulus.DueTime != work.DueTime)
            throw new InvalidOperationException("Combat wake lacks exact correlation.");
        SimulationState cleared = state.ReplaceShip(
            ship.InstanceId,
            ship with
            {
                Combat = ship.Combat with { PendingStimulus = null },
            }
        );
        ship = cleared.GetRequiredShip(ship.InstanceId);
        SensorContactTrack? contact = ship.SensorKnowledge.Contacts.FirstOrDefault(item =>
            item.Id == stimulus.ContactId
        );
        ShipDefinition definition = catalog.GetRequired(ship.DefinitionId);
        bool sameContext =
            ship.StrategicState is AtLocationState at
            && contact is not null
            && (contact.ObservedAtLocationId is null || contact.ObservedAtLocationId == at.LocationId);
        var input = new DefensiveCombatDecisionInput(
            CombatFacts(cleared, ship, definition),
            contact?.ToActorSafeSnapshot(),
            sameContext,
            stimulus
        );
        DefensiveCombatDecisionExplanation decision = DefensiveCombatDecisionPolicy.Evaluate(input);
        (SimulationState candidate, decision) = ApplyDefensiveDecision(
            cleared,
            ship,
            catalog,
            decision,
            events,
            canceledRepairs,
            collector
        );
        traces.Add(
            Trace(
                state,
                work,
                null,
                ScheduledConsequenceRule.ShipCombatDecisionWake,
                ScheduledConsequenceAction.WakeShipCombatDecision,
                true
            ) with
            {
                CombatDecision = decision,
            }
        );
        return candidate;
    }

    private static (SimulationState State, DefensiveCombatDecisionExplanation Decision) ApplyDefensiveDecision(
        SimulationState state,
        ShipState ship,
        ShipDefinitionCatalog catalog,
        DefensiveCombatDecisionExplanation decision,
        List<PlayerAdvanceEvent> events,
        HashSet<ScheduledWork> canceledRepairs,
        ObservationPublicationCollector collector
    )
    {
        SimulationState candidate = state;
        if (decision.SelectedAction == DefensiveCombatDecisionAction.ReturnFire)
        {
            CombatApplication shot = ApplyDirectedEnergy(
                state,
                catalog,
                ship.InstanceId,
                new FireDirectedEnergyIntent(decision.ContactId, ShipSystemKind.DirectedEnergyWeapons),
                events,
                canceledRepairs,
                collector
            );
            candidate = shot.State;
            decision = decision with { ApplicationOutcome = shot.Outcome };
        }
        else if (decision.ResultingCourse is { } course)
        {
            TacticalCourseApplicationResult move = ApplyTacticalCourse(
                state,
                catalog,
                new TargetableTacticalCourseCommand(ship.InstanceId, course.Heading, course.Speed)
            );
            candidate = move.CandidateState;
            decision = decision with { CourseOutcome = move.Outcome };
        }
        return (candidate, decision);
    }

    private static CombatProjection ProjectCombat(SimulationState state, ShipState ship, ShipDefinition definition) =>
        new(
            definition.DirectedEnergyWeapon?.Range,
            definition.DirectedEnergyWeapon?.Cooldown,
            ship.Combat.NextDirectedEnergyReadyAt,
            new SimulationDuration(
                Math.Max(0, ship.Combat.NextDirectedEnergyReadyAt.Milliseconds - state.Time.Milliseconds)
            ),
            new ReadOnlyValueList<CombatTargetProjection>(
                ship.SensorKnowledge.Contacts.Select(contact =>
                {
                    bool context =
                        ship.StrategicState is AtLocationState at
                        && (contact.ObservedAtLocationId is null || contact.ObservedAtLocationId == at.LocationId);
                    double distance = CombatLegality.Distance(ship.TacticalPosition, contact.LastObservedPosition);
                    return new CombatTargetProjection(
                        contact.Id,
                        context && double.IsFinite(distance) ? new DistanceKilometers(distance) : null,
                        CombatLegality.Evaluate(
                            CombatFacts(state, ship, definition),
                            contact.ToActorSafeSnapshot(),
                            context,
                            ShipSystemKind.DirectedEnergyWeapons
                        ),
                        new ReadOnlyValueList<ShipSystemKind>(CombatLegality.SupportedSystems)
                    );
                })
            )
        );

    private void RememberLatestCombatDecision(IReadOnlyList<ScheduledConsequenceTrace> traces)
    {
        DefensiveCombatDecisionExplanation? latest = traces
            .Select(trace => trace.CombatDecision)
            .LastOrDefault(decision => decision is not null);
        if (latest is not null)
            LastDefensiveCombatDecisionExplanation = latest;
    }
}
