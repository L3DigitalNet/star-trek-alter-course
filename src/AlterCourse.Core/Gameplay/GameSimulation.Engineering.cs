using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>
/// Installed-system engineering: generic power and repair commands plus the typed ship-level capability rules.
/// </summary>
/// <remarks>
/// <para>
/// ADDRESSING: public commands take a ship-local <see cref="InstalledSystemId"/> and bind the player ship
/// internally. Each delegates to a trusted transition that resolves the installation only within the addressed
/// ship, so the same local id on another ship can never be mutated; a miss is an <c>Unknown*</c> outcome with no
/// state change. Candidates are committed only on <c>Accepted</c>.
/// </para>
/// <para>
/// Typed rules (speed, passive range, available power) obtain "the" impulse drive or sensor exclusively through
/// <see cref="ShipSystemAdmission.SupportedSingle"/>. Absence is zero capability, never a placeholder.
/// </para>
/// </remarks>
public sealed partial class GameSimulation
{
    /// <summary>Validates and atomically applies a complete exact player-ship power allocation.</summary>
    public PowerAllocationResult SetPowerAllocation(PowerAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        return CommitAllocation(ApplyShipAllocation(_state, _shipCatalog, _state.PlayerShipId, allocation));
    }

    /// <summary>Generates, validates, and atomically applies the Balanced allocation over installed demands.</summary>
    public PowerAllocationResult ApplyBalancedAllocation() =>
        CommitAllocation(ApplyShipBalancedAllocation(_state, _shipCatalog, _state.PlayerShipId));

    /// <summary>Generates, validates, and atomically applies a priority allocation for one installed consumer.</summary>
    public PowerAllocationResult ApplyPriorityAllocation(InstalledSystemId consumer) =>
        CommitAllocation(
            ApplyShipPriorityAllocation(_state, _shipCatalog, new ShipSystemAddress(_state.PlayerShipId, consumer))
        );

    /// <summary>Validates and schedules one player-ship analytical repair of an installed system.</summary>
    public SystemRepairResult BeginSystemRepair(InstalledSystemId target, SystemCondition targetCondition)
    {
        RepairApplication application = ApplyShipRepair(
            _state,
            _shipCatalog,
            new ShipSystemAddress(_state.PlayerShipId, target),
            targetCondition
        );
        if (application.Outcome == SystemRepairOutcome.Accepted)
        {
            Commit(application.CandidateState);
        }

        return new SystemRepairResult(application.Outcome);
    }

    internal static EngineeringApplication ApplyShipBalancedAllocation(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipInstanceId ship
    ) => ApplyShipAllocation(state, catalog, ship, state.GetRequiredShip(ship).Engineering.BalancedAllocation());

    internal static EngineeringApplication ApplyShipPriorityAllocation(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipSystemAddress consumer
    )
    {
        ShipEngineeringState engineering = state.GetRequiredShip(consumer.Ship).Engineering;
        if (!engineering.Systems.Consumers.Any(system => system.Id == consumer.System))
        {
            return Refused(state, PowerAllocationOutcome.UnknownConsumer, consumer.System);
        }

        return ApplyShipAllocation(state, catalog, consumer.Ship, engineering.PriorityAllocation(consumer.System));
    }

    /// <summary>
    /// Validates an exact allocation for one ship in the Binding outcome order, then applies it and reconciles
    /// observation exactly as the pre-substrate command did.
    /// </summary>
    internal static EngineeringApplication ApplyShipAllocation(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipInstanceId shipId,
        PowerAllocation exact
    )
    {
        ArgumentNullException.ThrowIfNull(exact);
        ShipState ship = state.GetRequiredShip(shipId);
        (PowerAllocationOutcome outcome, InstalledSystemId? consumer) = ValidateAllocation(ship, exact);
        if (outcome != PowerAllocationOutcome.Accepted)
        {
            return Refused(state, outcome, consumer);
        }

        ShipState updated = ship with { Engineering = ship.Engineering.WithAllocation(exact) };
        List<PlayerAdvanceEvent> playerEvents = [];
        SimulationState candidate = ObserveAllShips(state.ReplaceShip(shipId, updated), catalog, playerEvents);
        return new EngineeringApplication(PowerAllocationOutcome.Accepted, null, candidate, playerEvents);
    }

    internal static RepairApplication ApplyShipRepair(
        SimulationState state,
        ShipDefinitionCatalog catalog,
        ShipSystemAddress address,
        SystemCondition targetCondition
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ShipState ship = state.GetRequiredShip(address.Ship);
        if (ship.Engineering.ActiveRepair is not null)
        {
            return new RepairApplication(SystemRepairOutcome.RepairAlreadyActive, state);
        }

        if (!ship.Engineering.Systems.TryGet(address.System, out InstalledSystem? target))
        {
            return new RepairApplication(SystemRepairOutcome.UnknownSystem, state);
        }

        if (target.Definition.Repair is not { } capability)
        {
            return new RepairApplication(SystemRepairOutcome.NotRepairable, state);
        }

        if (targetCondition.Value <= target.Condition.Value)
        {
            return new RepairApplication(SystemRepairOutcome.TargetDoesNotImproveCondition, state);
        }

        SimulationTime completion = state.Time.AdvanceBy(capability.FullRepairDuration);
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            completion,
            ship.InstanceId,
            ScheduledWorkKind.SystemRepairCompletion
        );
        var repair = new SystemRepairState(
            target.Id,
            target.Condition,
            targetCondition,
            state.Time,
            completion,
            work.Id
        );
        ShipState updated = ship with { Engineering = ship.Engineering.WithRepair(repair) };
        return new RepairApplication(
            SystemRepairOutcome.Accepted,
            state.ReplaceShip(ship.InstanceId, updated) with
            {
                Scheduler = scheduler,
            }
        );
    }

    /// <summary>Gets the effective tactical-speed ceiling from the sole impulse installation, or zero without one.</summary>
    internal static SpeedKilometersPerSecond EffectiveMaximumTacticalSpeed(ShipEngineeringState engineering) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, ShipSystemKind.ImpulsePropulsion) is { } impulse
            ? new(
                ((ImpulsePropulsionSystemDefinition)impulse.Definition).MaximumTacticalSpeed.Value
                    * ShipEngineeringState.Capability(impulse).Value
            )
            : new(0);

    /// <summary>Gets the effective passive range from the sole sensor installation, or zero without one.</summary>
    internal static DistanceKilometers EffectivePassiveSensorRange(ShipEngineeringState engineering) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, ShipSystemKind.Sensors) is { } sensors
            ? new(
                ((SensorSystemDefinition)sensors.Definition).PassiveRange.Value
                    * ShipEngineeringState.Capability(sensors).Value
            )
            : new(0);

    /// <summary>Gets whether the ship can currently sense at all (positive range and positive sensor capability).</summary>
    internal static bool HasEffectiveSensorCapability(ShipEngineeringState engineering) =>
        ShipSystemAdmission.SupportedSingle(engineering.Systems, ShipSystemKind.Sensors) is { } sensors
        && ((SensorSystemDefinition)sensors.Definition).PassiveRange.Value > 0
        && ShipEngineeringState.Capability(sensors).Value > 0;

    /// <summary>
    /// Gets whether the ship's active repair targets an installation of <paramref name="kind"/>.
    /// </summary>
    /// <remarks>
    /// <c>GetRequired</c>, not <c>TryGet</c>: a repair whose target is missing is invalid state and must fail loudly
    /// rather than silently disable the stationary-observer observation-expansion guard that depends on this.
    /// </remarks>
    private static bool IsRepairingKind(ShipEngineeringState engineering, ShipSystemKind kind) =>
        engineering.ActiveRepair is { } repair && engineering.Systems.GetRequired(repair.Target).Kind == kind;

    private static (PowerAllocationOutcome Outcome, InstalledSystemId? Consumer) ValidateAllocation(
        ShipState ship,
        PowerAllocation exact
    )
    {
        IReadOnlyList<InstalledSystem> consumers = ship.Engineering.Systems.Consumers;

        // 1. Every listed key must be an installed consumer on this ship: covers generator/nonconsumer keys,
        //    foreign-ship identities, and unknown identities alike.
        foreach (PowerAllocationEntry entry in exact.Entries)
        {
            if (!consumers.Any(consumer => consumer.Id == entry.Consumer))
            {
                return (PowerAllocationOutcome.UnknownConsumer, entry.Consumer);
            }
        }

        // 2. Complete key set: a missing consumer would otherwise keep a stale allocation (a sparse patch).
        foreach (InstalledSystem consumer in consumers)
        {
            if (!exact.TryGet(consumer.Id, out _))
            {
                return (PowerAllocationOutcome.IncompleteAllocation, consumer.Id);
            }
        }

        // 3. Demand in canonical order — the same first-failure order as the pre-substrate sensor, impulse, shield,
        //    weapon checks — then 4. total supply.
        foreach (InstalledSystem consumer in consumers)
        {
            exact.TryGet(consumer.Id, out PowerUnits allocation);
            if (allocation > consumer.Definition.Power!.NominalDemand)
            {
                return (PowerAllocationOutcome.ConsumerDemandExceeded, consumer.Id);
            }
        }

        if (exact.Total > ship.Engineering.AvailablePower.Value)
        {
            return (PowerAllocationOutcome.AvailablePowerExceeded, null);
        }

        // 5. Voluntary allocation may not invalidate the ship's current speed; forced brownout clamps instead.
        ShipEngineeringState proposed = ship.Engineering.WithAllocation(exact);
        return ship.TacticalMotion.Speed.Value > EffectiveMaximumTacticalSpeed(proposed).Value
            ? (PowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum, null)
            : (PowerAllocationOutcome.Accepted, null);
    }

    private static EngineeringApplication Refused(
        SimulationState state,
        PowerAllocationOutcome outcome,
        InstalledSystemId? consumer
    ) => new(outcome, consumer, state, []);

    private PowerAllocationResult CommitAllocation(EngineeringApplication application)
    {
        if (application.Outcome == PowerAllocationOutcome.Accepted)
        {
            Commit(application.CandidateState);
        }

        return new PowerAllocationResult(
            application.Outcome,
            new ReadOnlyValueList<PlayerAdvanceEvent>(application.Events),
            application.Consumer
        );
    }
}
