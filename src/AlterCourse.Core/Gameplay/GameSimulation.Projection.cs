using AlterCourse.Core.Content;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Player-facing Engineering and Combat projection assembly.</summary>
public sealed partial class GameSimulation
{
    // TEMPORARY BRIDGE (removed by leg L5): the Engineering projection keeps its pre-substrate public shape (named
    // per-kind conditions, allocations, capabilities, and the kind-addressed EngineeringAction list) so the unchanged
    // Godot presenter keeps compiling. Every value is read from the ship's actual installations through
    // ShipSystemAdmission.SupportedSingle; an absent kind projects zeros and offers no action. This is a read-only
    // view, never a second authority: Core commands and validation do not read it. L5 replaces it with the
    // installed-system rows and generic actions of the substrate design.
    private static EngineeringProjection ProjectEngineering(SimulationState state, ShipState ship)
    {
        ShipEngineeringState engineering = ship.Engineering;
        InstalledSystem? generator = Single(engineering, ShipSystemKind.PowerGeneration);
        InstalledSystem? sensors = Single(engineering, ShipSystemKind.Sensors);
        InstalledSystem? impulse = Single(engineering, ShipSystemKind.ImpulsePropulsion);
        InstalledSystem? shields = Single(engineering, ShipSystemKind.Shields);
        InstalledSystem? weapons = Single(engineering, ShipSystemKind.DirectedEnergyWeapons);
        SystemRepairState? repair = engineering.ActiveRepair;
        return new EngineeringProjection(
            engineering.NominalGeneration,
            engineering.AvailablePower,
            sensors?.Allocation ?? default,
            impulse?.Allocation ?? default,
            engineering.Reserve,
            generator?.Condition ?? default,
            sensors?.Condition ?? default,
            impulse?.Condition ?? default,
            CapabilityOf(sensors),
            CapabilityOf(impulse),
            EffectivePassiveSensorRange(engineering),
            EffectiveMaximumTacticalSpeed(engineering),
            repair is null
                ? null
                : new SystemRepairProjection(
                    engineering.Systems.GetRequired(repair.Target).Kind,
                    repair.ProgressAt(state.Time),
                    repair.ExpectedCompletion
                ),
            new ReadOnlyValueList<EngineeringActionProjection>(ProjectEngineeringActions(ship)),
            shields?.Condition ?? default,
            weapons?.Condition ?? default,
            CapabilityOf(shields),
            CapabilityOf(weapons),
            shields?.Allocation ?? default,
            weapons?.Allocation ?? default,
            shields?.Definition.Power?.NominalDemand ?? default,
            weapons?.Definition.Power?.NominalDemand ?? default
        );
    }

    /// <summary>
    /// Emits the legacy action list in its base order. Availability comes from the generic commands' own validation,
    /// so the adapter decides no rule.
    /// </summary>
    private static EngineeringActionProjection[] ProjectEngineeringActions(ShipState ship)
    {
        var actions = new List<EngineeringActionProjection>();
        AddAllocationAction(actions, EngineeringAction.Balanced, ship, ship.Engineering.BalancedAllocation());
        AddPriorityAction(actions, EngineeringAction.PrioritizeSensors, ship, ShipSystemKind.Sensors);
        AddPriorityAction(actions, EngineeringAction.PrioritizePropulsion, ship, ShipSystemKind.ImpulsePropulsion);
        AddPriorityAction(actions, EngineeringAction.PrioritizeShields, ship, ShipSystemKind.Shields);
        AddPriorityAction(
            actions,
            EngineeringAction.PrioritizeDirectedEnergyWeapons,
            ship,
            ShipSystemKind.DirectedEnergyWeapons
        );
        AddRepairAction(actions, EngineeringAction.BeginShieldRepair, ship, ShipSystemKind.Shields);
        AddRepairAction(
            actions,
            EngineeringAction.BeginDirectedEnergyRepair,
            ship,
            ShipSystemKind.DirectedEnergyWeapons
        );
        AddRepairAction(actions, EngineeringAction.BeginSensorRepair, ship, ShipSystemKind.Sensors);
        AddRepairAction(actions, EngineeringAction.BeginImpulseRepair, ship, ShipSystemKind.ImpulsePropulsion);
        actions.Add(new EngineeringActionProjection(EngineeringAction.ReturnToCommand, true));
        return [.. actions];
    }

    private static void AddPriorityAction(
        List<EngineeringActionProjection> actions,
        EngineeringAction action,
        ShipState ship,
        ShipSystemKind kind
    )
    {
        if (Single(ship.Engineering, kind) is { Definition.Power: not null } consumer)
        {
            AddAllocationAction(actions, action, ship, ship.Engineering.PriorityAllocation(consumer.Id));
        }
    }

    private static void AddAllocationAction(
        List<EngineeringActionProjection> actions,
        EngineeringAction action,
        ShipState ship,
        PowerAllocation allocation
    )
    {
        bool available = ValidateAllocation(ship, allocation).Outcome == PowerAllocationOutcome.Accepted;
        actions.Add(
            new EngineeringActionProjection(
                action,
                available,
                available ? null : EngineeringActionUnavailableReason.CurrentSpeedTooHigh
            )
        );
    }

    private static void AddRepairAction(
        List<EngineeringActionProjection> actions,
        EngineeringAction action,
        ShipState ship,
        ShipSystemKind kind
    )
    {
        if (Single(ship.Engineering, kind) is not { } target)
        {
            return;
        }

        EngineeringActionUnavailableReason? reason =
            target.Definition.Repair is null ? EngineeringActionUnavailableReason.UnsupportedSystem
            : ship.Engineering.ActiveRepair is not null ? EngineeringActionUnavailableReason.RepairAlreadyActive
            : target.Condition.Value == 1 ? EngineeringActionUnavailableReason.SystemAlreadyNominal
            : null;
        actions.Add(new EngineeringActionProjection(action, reason is null, reason));
    }

    private static InstalledSystem? Single(ShipEngineeringState engineering, ShipSystemKind kind) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, kind);

    private static double CapabilityOf(InstalledSystem? system) =>
        system is null ? 0 : ShipEngineeringState.Capability(system);

    /// <summary>
    /// Projects the player's combat view. Aim kinds are the catalog's public damage-target vocabulary — identical for
    /// every contact — and the per-contact outcome uses aim-independent prerequisites, so nothing pre-shot varies with
    /// the target's hidden loadout.
    /// </summary>
    private static CombatProjection ProjectCombat(SimulationState state, ShipState ship, ShipDefinitionCatalog catalog)
    {
        CombatOwnFacts own = CombatFacts(state, ship);
        var aimKinds = new ReadOnlyValueList<ShipSystemKind>(catalog.SystemDefinitions.DamageTargetKinds);

        // Without a weapon there is no readiness entry; the legacy non-null field reports the current time (ready,
        // zero remaining) while WeaponRange and Cooldown stay null to mark the capability absent.
        SimulationTime readyAt = own.WeaponId is null ? state.Time : own.ReadyAt;
        return new(
            own.Weapon?.Range,
            own.Weapon?.Cooldown,
            readyAt,
            new SimulationDuration(Math.Max(0, readyAt.Milliseconds - state.Time.Milliseconds)),
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
                        CombatLegality.EvaluatePrerequisites(own, contact.ToActorSafeSnapshot(), context),
                        aimKinds
                    );
                })
            )
        );
    }
}
