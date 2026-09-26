using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

internal sealed partial record SimulationState
{
    private void ValidateCombatState(ShipState ship, ShipDefinition definition, HashSet<ScheduledWorkId> workIds)
    {
        ShipCombatState combat = ship.Combat ?? throw new InvalidOperationException("Combat state cannot be null.");
        long ready = combat.NextDirectedEnergyReadyAt.Milliseconds;
        if (
            ready % SimulationFixedStep.Duration.Milliseconds != 0
            || ready > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
            || (
                ready > Time.Milliseconds
                && (
                    definition.DirectedEnergyWeapon is not { } weapon
                    || ready - Time.Milliseconds > weapon.Cooldown.Milliseconds
                )
            )
        )
            throw new InvalidOperationException("Weapon readiness is outside its authored time bounds.");
        if (combat.PendingStimulus is not { } stimulus)
            return;
        if (
            ship.InstanceId == PlayerShipId
            || stimulus.ContactId.Value <= 0
            || !ship.SensorKnowledge.Contacts.Any(contact => contact.Id == stimulus.ContactId)
            || stimulus.ObservedAt.Milliseconds > Time.Milliseconds
            || stimulus.ObservedAt.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0
            || stimulus.DueTime.Milliseconds - stimulus.ObservedAt.Milliseconds
                != SimulationFixedStep.Duration.Milliseconds
            || stimulus.DueTime.Milliseconds < Time.Milliseconds
            || stimulus.DueTime.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
            || stimulus.ScheduledWorkId.Value <= 0
        )
            throw new InvalidOperationException("Defensive stimulus violates observer or future-wake bounds.");
        EnsureUniqueContactWorkId(stimulus.ScheduledWorkId, workIds);
        EnsureExactlyCorrelated(
            ship.InstanceId,
            stimulus.ScheduledWorkId,
            stimulus.DueTime,
            ScheduledWorkKind.ShipCombatDecisionWake
        );
    }
}
