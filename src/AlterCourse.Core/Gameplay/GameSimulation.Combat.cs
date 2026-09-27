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

    /// <summary>
    /// Gathers the firing ship's own facts from its sole installed weapon; without one the weapon fields are empty
    /// and legality reports <see cref="FireDirectedEnergyOutcome.WeaponOffline"/>.
    /// </summary>
    private static CombatOwnFacts CombatFacts(SimulationState state, ShipState ship)
    {
        InstalledSystem? weapon = ShipSystemAdmission.SupportedSingle(
            ship.Engineering.Systems,
            ShipSystemKind.DirectedEnergyWeapons
        );
        return new(
            state.Time,
            ship.StrategicState is AtLocationState,
            ship.TacticalPosition,
            ship.TacticalMotion,
            EffectiveMaximumTacticalSpeed(ship.Engineering),
            weapon?.Id,
            (weapon?.Definition as DirectedEnergyWeaponSystemDefinition)?.Weapon,
            weapon?.Condition ?? default,
            weapon is null ? default : ShipEngineeringState.Capability(weapon),
            weapon is null ? new SimulationTime(0) : ship.Combat.ReadinessOf(weapon.Id)!.ReadyAt
        );
    }

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
        SensorContactTrack? contact = attacker.SensorKnowledge.Contacts.FirstOrDefault(item =>
            item.Id == intent.ContactId
        );
        ShipState? victim = contact is null ? null : state.GetRequiredShip(contact.TargetShipId);
        CombatOwnFacts own = CombatFacts(state, attacker);
        FireDirectedEnergyOutcome outcome = CombatLegality.Evaluate(
            own,
            contact?.ToActorSafeSnapshot(),
            victim is not null && SameCombatContext(attacker, victim),
            intent.TargetSystem,
            catalog.SystemDefinitions.DamageTargetKinds
        );
        if (outcome != FireDirectedEnergyOutcome.Accepted)
            return new CombatApplication(outcome, state);

        return ApplyAcceptedShot(
            state,
            catalog,
            attacker,
            own,
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
        CombatOwnFacts own,
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
        ShipEngineeringState before = victim.Engineering;
        (ShipEngineeringState damaged, ShieldDamageResult impact, InstalledSystem? shield, InstalledSystem? receiver) =
            ResolveHit(before, own.Weapon!.BaseNormalizedDamage * own.WeaponCapability.Value, intent.TargetSystem);
        DamageRepairResolution repairResult = CancelHitRepair(
            state,
            victim,
            damaged,
            impact,
            shield?.Id,
            receiver?.Id,
            canceledRepairs
        );
        damaged = repairResult.Engineering;
        SimulationScheduler scheduler = repairResult.Scheduler;
        bool repairInterrupted = repairResult.Interrupted;
        ShipState hit = ReconcileDamagedShip(victim, damaged);
        ShipState fired = attacker with
        {
            Combat = attacker.Combat.WithReadiness(
                new DirectedEnergyReadiness(own.WeaponId!.Value, state.Time.AdvanceBy(own.Weapon.Cooldown))
            ),
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
            shield,
            receiver,
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

    /// <summary>Applies one shot's damage to the victim's actual installations.</summary>
    /// <remarks>
    /// Call order is load-bearing: shields resolve first (a shieldless victim absorbs nothing), then the receiver
    /// reads its post-shield condition so an aimed shield installation takes both legs, as before the substrate. The
    /// receiver is resolved against the victim's actual installations; when the aimed kind is absent the residual
    /// damage has no receiver and is discarded — never redirected to another installation or to a hull pool.
    /// </remarks>
    private static (
        ShipEngineeringState Damaged,
        ShieldDamageResult Impact,
        InstalledSystem? Shield,
        InstalledSystem? Receiver
    ) ResolveHit(ShipEngineeringState before, double damage, ShipSystemKind aimKind)
    {
        InstalledSystem? shield = ShipSystemAdmission.SupportedSingle(before.Systems, ShipSystemKind.Shields);
        ShieldDamageResult impact = ShieldDamage.Resolve(
            damage,
            shield?.Condition ?? default,
            shield is null ? default : ShipEngineeringState.PowerSatisfaction(shield)
        );
        ShipEngineeringState damaged = shield is null
            ? before
            : before.WithCondition(shield.Id, impact.ShieldCondition);
        InstalledSystem? receiver = ShipSystemAdmission.SupportedSingle(damaged.Systems, aimKind);
        if (receiver is not null)
        {
            damaged = damaged.WithCondition(receiver.Id, impact.ApplyTo(receiver.Condition));
        }

        return (damaged, impact, shield, receiver);
    }

    private static ShipState ReconcileDamagedShip(ShipState victim, ShipEngineeringState damaged)
    {
        damaged = damaged.WithAllocation(damaged.ReconcileAvailablePower());
        double maxSpeed = EffectiveMaximumTacticalSpeed(damaged).Value;
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
        InstalledSystemId? shield,
        InstalledSystemId? receiver,
        HashSet<ScheduledWork>? canceledRepairs
    )
    {
        SimulationScheduler scheduler = state.Scheduler;
        bool repairInterrupted = false;

        // Positive absorption interrupts repair of the actual shield installation and positive penetration repair of
        // the actual receiver; damage anywhere else leaves the repair running. An absent receiver damaged nothing.
        if (
            victim.Engineering.ActiveRepair is { } repair
            && (
                (shield == repair.Target && impact.AbsorbedDamage > 0)
                || (receiver == repair.Target && impact.PenetratingDamage > 0)
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
            damaged = damaged.WithRepair(null);
            repairInterrupted = true;
        }
        return new DamageRepairResolution(damaged, scheduler, repairInterrupted);
    }

    private static void ReportCombatImpact(
        SimulationState state,
        ShipInstanceId attackerId,
        SensorContactTrack contact,
        ShipState victim,
        ShipSystemKind aimKind,
        InstalledSystem? shield,
        InstalledSystem? receiver,
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
            ReportAttackerFeedback(state, contact.Id, aimKind, impact, events);
        }

        if (victim.InstanceId == state.PlayerShipId)
        {
            // The victim owner sees its actual damage: its own kinds and installed identities.
            if (impact.AbsorbedDamage > 0 && shield is not null)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.OwnSystemDamaged,
                        state.Time,
                        observedAttacker,
                        shield.Kind,
                        shield.Id
                    )
                );
            if (impact.PenetratingDamage > 0 && receiver is not null)
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.OwnSystemDamaged,
                        state.Time,
                        observedAttacker,
                        receiver.Kind,
                        receiver.Id
                    )
                );
            if (repairInterrupted)
            {
                InstalledSystem interrupted = before.Systems.GetRequired(before.ActiveRepair!.Target);
                events.Add(
                    new PlayerAdvanceEvent(
                        PlayerAdvanceEventKind.SystemRepairInterrupted,
                        state.Time,
                        observedAttacker,
                        interrupted.Kind,
                        interrupted.Id
                    )
                );
            }
            if (reconciled != before.Allocation)
                events.Add(new PlayerAdvanceEvent(PlayerAdvanceEventKind.PowerBrownout, state.Time));
            if (motion != victim.TacticalMotion)
                events.Add(new PlayerAdvanceEvent(PlayerAdvanceEventKind.ForcedDeceleration, state.Time));
        }
    }

    /// <summary>
    /// Attacker feedback depends only on the shot and the shield interaction, never on receiver presence, so a present
    /// and an absent receiver are indistinguishable to the attacker (subsystem damage stays unconfirmed). Installed
    /// identities never appear on attacker-facing events, and ShieldImpact always names shields whatever the aim.
    /// </summary>
    private static void ReportAttackerFeedback(
        SimulationState state,
        SensorContactId contactId,
        ShipSystemKind aimKind,
        ShieldDamageResult impact,
        List<PlayerAdvanceEvent> events
    )
    {
        events.Add(new PlayerAdvanceEvent(PlayerAdvanceEventKind.DirectedEnergyFired, state.Time, contactId, aimKind));
        if (impact.AbsorbedDamage > 0)
            events.Add(
                new PlayerAdvanceEvent(
                    PlayerAdvanceEventKind.ShieldImpact,
                    state.Time,
                    contactId,
                    ShipSystemKind.Shields
                )
            );
        if (impact.PenetratingDamage > 0)
            events.Add(
                new PlayerAdvanceEvent(PlayerAdvanceEventKind.SubsystemPenetration, state.Time, contactId, aimKind)
            );
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
        bool sameContext =
            ship.StrategicState is AtLocationState at
            && contact is not null
            && (contact.ObservedAtLocationId is null || contact.ObservedAtLocationId == at.LocationId);
        var input = new DefensiveCombatDecisionInput(
            CombatFacts(cleared, ship),
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

    private void RememberLatestCombatDecision(IReadOnlyList<ScheduledConsequenceTrace> traces)
    {
        DefensiveCombatDecisionExplanation? latest = traces
            .Select(trace => trace.CombatDecision)
            .LastOrDefault(decision => decision is not null);
        if (latest is not null)
            LastDefensiveCombatDecisionExplanation = latest;
    }
}
