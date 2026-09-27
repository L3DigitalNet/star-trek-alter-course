using AlterCourse.Core.Content;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Player-facing Engineering and Combat projection assembly.</summary>
public sealed partial class GameSimulation
{
    /// <summary>
    /// Projects the player's actual installations as generic rows plus the Core-owned action list. Nothing here is a
    /// second authority: availability is decided by running the same validation the commands run, and every row is
    /// read from the ship's installed-system collection.
    /// </summary>
    private static EngineeringProjection ProjectEngineering(SimulationState state, ShipState ship)
    {
        ShipEngineeringState engineering = ship.Engineering;
        SystemRepairState? repair = engineering.ActiveRepair;
        InstalledSystem? repairTarget = repair is null ? null : engineering.Systems.GetRequired(repair.Target);
        return new EngineeringProjection(
            engineering.NominalGeneration,
            engineering.AvailablePower,
            new PowerUnits(checked((int)engineering.Allocation.Total)),
            engineering.Reserve,
            EffectivePassiveSensorRange(engineering),
            EffectiveMaximumTacticalSpeed(engineering),
            ProjectSystems(engineering.Systems),
            repairTarget is null || repair is null
                ? null
                : new SystemRepairProjection(
                    repairTarget.Id,
                    repairTarget.Kind,
                    repairTarget.Definition.ComponentLabel,
                    repair.ProgressAt(state.Time),
                    repair.ExpectedCompletion
                ),
            ProjectEngineeringActions(ship)
        );
    }

    /// <summary>
    /// Projects one row per installation in canonical common order. It reads only common installation facts, never
    /// a typed singleton, so it is valid for any collection storage admits — including several of one kind.
    /// </summary>
    internal static IReadOnlyList<InstalledSystemProjection> ProjectSystems(InstalledSystemCollection systems) =>
        new ReadOnlyValueList<InstalledSystemProjection>(systems.InCommonOrder.Select(ProjectSystem));

    private static InstalledSystemProjection ProjectSystem(InstalledSystem system)
    {
        bool consumer = system.Definition.Power is not null;
        return new InstalledSystemProjection(
            system.Id,
            system.Kind,
            system.Definition.Id,
            system.Definition.ComponentLabel,
            system.Condition,
            system.Definition.Power?.NominalDemand,
            consumer ? system.Allocation : null,
            consumer ? ShipEngineeringState.Capability(system) : null,
            system.Definition.Repair?.FullRepairDuration
        );
    }

    /// <summary>
    /// Emits the action list in its contract order: Balance; Prioritize for each consumer in common order;
    /// BeginRepair for each repairable installation by (authored repair-action order, installed id); ReturnToCommand.
    /// </summary>
    /// <remarks>
    /// Iterating actual installations means an absent kind simply contributes no action, and a nonrepairable
    /// installation (the generator) gets no repair action, so there is no "unsupported system" reason to report.
    /// With production content this reproduces the pre-substrate button sequence exactly: the repair order comes
    /// from content (shields, weapons, sensors, impulse), not from a presentation-side table.
    /// </remarks>
    internal static IReadOnlyList<EngineeringActionProjection> ProjectEngineeringActions(ShipState ship)
    {
        ShipEngineeringState engineering = ship.Engineering;
        var actions = new List<EngineeringActionProjection>
        {
            AllocationAction(EngineeringOperation.Balance, null, ship, engineering.BalancedAllocation()),
        };
        actions.AddRange(
            engineering.Systems.Consumers.Select(consumer =>
                AllocationAction(
                    EngineeringOperation.Prioritize,
                    consumer.Id,
                    ship,
                    engineering.PriorityAllocation(consumer.Id)
                )
            )
        );
        actions.AddRange(
            engineering
                .Systems.InCommonOrder.Where(system => system.Definition.Repair is not null)
                .OrderBy(system => system.Definition.Repair!.ActionOrder)
                .ThenBy(system => system.Id.Value)
                .Select(system => RepairAction(engineering, system))
        );
        actions.Add(new EngineeringActionProjection(EngineeringOperation.ReturnToCommand, null, true));
        return new ReadOnlyValueList<EngineeringActionProjection>(actions);
    }

    private static EngineeringActionProjection AllocationAction(
        EngineeringOperation operation,
        InstalledSystemId? target,
        ShipState ship,
        PowerAllocation allocation
    )
    {
        bool available = ValidateAllocation(ship, allocation).Outcome == PowerAllocationOutcome.Accepted;
        return new EngineeringActionProjection(
            operation,
            target,
            available,
            available ? null : EngineeringActionUnavailableReason.CurrentSpeedTooHigh
        );
    }

    /// <summary>
    /// Reason precedence matches the repair command: an occupied repair slot is reported before a nominal target.
    /// </summary>
    private static EngineeringActionProjection RepairAction(ShipEngineeringState engineering, InstalledSystem target)
    {
        EngineeringActionUnavailableReason? reason =
            engineering.ActiveRepair is not null ? EngineeringActionUnavailableReason.RepairAlreadyActive
            : target.Condition.Value == 1 ? EngineeringActionUnavailableReason.SystemAlreadyNominal
            : null;
        return new EngineeringActionProjection(EngineeringOperation.BeginRepair, target.Id, reason is null, reason);
    }

    private static CombatSystemStatusProjection? CombatStatus(ShipEngineeringState engineering, ShipSystemKind kind) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, kind) is { } system
            ? new CombatSystemStatusProjection(
                system.Id,
                system.Condition,
                system.Allocation ?? default,
                ShipEngineeringState.Capability(system)
            )
            : null;

    /// <summary>
    /// Projects the player's combat view. Aim kinds are the catalog's public damage-target vocabulary — identical for
    /// every contact — and the per-contact outcome uses aim-independent prerequisites, so nothing pre-shot varies with
    /// the target's hidden loadout.
    /// </summary>
    private static CombatProjection ProjectCombat(SimulationState state, ShipState ship, ShipDefinitionCatalog catalog)
    {
        CombatOwnFacts own = CombatFacts(state, ship);
        var aimKinds = new ReadOnlyValueList<ShipSystemKind>(catalog.SystemDefinitions.DamageTargetKinds);

        // Without a weapon there is no readiness entry: readiness is null and nothing remains to cool down.
        SimulationTime? readyAt = own.WeaponId is null ? null : own.ReadyAt;
        return new(
            own.Weapon?.Range,
            own.Weapon?.Cooldown,
            readyAt,
            new SimulationDuration(
                readyAt is { } ready ? Math.Max(0, ready.Milliseconds - state.Time.Milliseconds) : 0
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
                        CombatLegality.EvaluatePrerequisites(own, contact.ToActorSafeSnapshot(), context),
                        aimKinds
                    );
                })
            ),
            CombatStatus(ship.Engineering, ShipSystemKind.Shields),
            CombatStatus(ship.Engineering, ShipSystemKind.DirectedEnergyWeapons)
        );
    }
}
