using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

internal sealed partial record SimulationState
{
    private void ValidateCombatState(ShipState ship, HashSet<ScheduledWorkId> workIds)
    {
        ShipCombatState combat = ship.Combat ?? throw new InvalidOperationException("Combat state cannot be null.");

        // Readiness exists for exactly the installed weapons: a missing entry would make an installed weapon
        // unfireable forever, and an extra one would be phantom state for an absent or non-weapon installation.
        InstalledSystem[] weapons =
        [
            .. ship.Engineering.Systems.ByIdentity.Where(system =>
                system.Definition is DirectedEnergyWeaponSystemDefinition
            ),
        ];
        if (!combat.WeaponReadiness.Select(entry => entry.Weapon).SequenceEqual(weapons.Select(weapon => weapon.Id)))
            throw new InvalidOperationException("Weapon readiness must list exactly the installed weapons.");
        foreach (DirectedEnergyReadiness entry in combat.WeaponReadiness)
        {
            long ready = entry.ReadyAt.Milliseconds;
            var weapon = (DirectedEnergyWeaponSystemDefinition)
                ship.Engineering.Systems.GetRequired(entry.Weapon).Definition;
            if (
                ready % SimulationFixedStep.Duration.Milliseconds != 0
                || ready > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
                || (ready > Time.Milliseconds && ready - Time.Milliseconds > weapon.Weapon.Cooldown.Milliseconds)
            )
                throw new InvalidOperationException("Weapon readiness is outside its authored time bounds.");
        }

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
