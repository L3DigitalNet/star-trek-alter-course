using System.Collections.ObjectModel;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Validates typed world starts and derives a complete deterministic simulation aggregate.</summary>
public sealed class GameBootstrap
{
    private readonly ReadOnlyCollection<ShipStart> _shipStarts;
    private readonly ReadOnlyCollection<FactionStart> _factionStarts;

    /// <summary>Initializes an immutable bootstrap declaration in canonical ship-identity order.</summary>
    public GameBootstrap(
        SimulationTime initialTime,
        StrategicMap strategicMap,
        ShipInstanceId playerShipId,
        IEnumerable<ShipStart> shipStarts,
        IEnumerable<FactionStart>? factionStarts = null
    )
    {
        ArgumentNullException.ThrowIfNull(strategicMap);
        ArgumentNullException.ThrowIfNull(shipStarts);
        ShipStart[] materialized = shipStarts.Take(SimulationState.MaximumShips + 1).ToArray();
        FactionStart[] materializedFactions = (factionStarts ?? []).Take(SimulationState.MaximumFactions + 1).ToArray();
        ValidateStarts(materialized, playerShipId);
        ValidateFactionStarts(materializedFactions, materialized, playerShipId, strategicMap);
        ValidateInitialTime(initialTime);

        InitialTime = initialTime;
        StrategicMap = strategicMap;
        PlayerShipId = playerShipId;
        _shipStarts = Array.AsReadOnly(materialized.OrderBy(start => start.InstanceId.Value).ToArray());
        _factionStarts = Array.AsReadOnly(materializedFactions.OrderBy(start => start.Id.Value).ToArray());
    }

    /// <summary>Gets the initial authoritative simulation time.</summary>
    public SimulationTime InitialTime { get; }

    /// <summary>Gets the semantic strategic map used by the world.</summary>
    public StrategicMap StrategicMap { get; }

    /// <summary>Gets the sole player-controlled ship identity.</summary>
    public ShipInstanceId PlayerShipId { get; }

    /// <summary>Gets ship declarations in ascending instance-identity order.</summary>
    public IReadOnlyList<ShipStart> ShipStarts => _shipStarts;

    /// <summary>Gets faction declarations in ascending runtime-identity order.</summary>
    public IReadOnlyList<FactionStart> FactionStarts => _factionStarts;

    private static void ValidateFactionStarts(
        FactionStart[] factions,
        ShipStart[] ships,
        ShipInstanceId playerShipId,
        StrategicMap map
    )
    {
        if (factions.Length > SimulationState.MaximumFactions || factions.Any(faction => faction is null))
        {
            throw new ArgumentException(
                $"A bootstrap supports at most {SimulationState.MaximumFactions} nonnull faction starts.",
                nameof(factions)
            );
        }

        if (
            factions.Any(faction => faction.Id.Value <= 0)
            || factions.Select(faction => faction.Id).Distinct().Count() != factions.Length
        )
        {
            throw new ArgumentException(
                "Bootstrap faction starts require unique initialized identities.",
                nameof(factions)
            );
        }

        HashSet<FactionId> factionIds = [.. factions.Select(faction => faction.Id)];
        if (ships.Any(ship => ship.DirectControllerFactionId is { } controller && !factionIds.Contains(controller)))
        {
            throw new ArgumentException("Every ship controller must resolve to a declared faction.", nameof(ships));
        }

        if (ships.Single(ship => ship.InstanceId == playerShipId).DirectControllerFactionId is not null)
        {
            throw new ArgumentException(
                "The player ship cannot declare an autonomous faction controller.",
                nameof(ships)
            );
        }

        foreach (FactionStart faction in factions)
        {
            if (faction.PresenceTargetLocationId is { } target)
            {
                map.GetLocation(target);
            }
        }
    }

    private static void ValidateStarts(ShipStart[] shipStarts, ShipInstanceId playerShipId)
    {
        if (shipStarts.Length > SimulationState.MaximumShips)
        {
            throw new ArgumentException(
                $"A bootstrap supports at most {SimulationState.MaximumShips} ship starts.",
                nameof(shipStarts)
            );
        }

        if (shipStarts.Length == 0 || shipStarts.Any(start => start is null))
        {
            throw new ArgumentException("A bootstrap requires at least one nonnull ship start.", nameof(shipStarts));
        }

        if (shipStarts.Any(start => start.InstanceId.Value <= 0 || start.InstanceId.Value >= long.MaxValue - 1))
        {
            throw new ArgumentException(
                "Bootstrap ship starts require initialized identities with allocator continuation headroom.",
                nameof(shipStarts)
            );
        }

        if (
            shipStarts.Any(start =>
                string.IsNullOrWhiteSpace(start.VesselDisplayName)
                || start.VesselDisplayName.Length > ShipState.MaximumVesselDisplayNameLength
                || start.Strategic is null
            )
        )
        {
            throw new ArgumentException(
                $"Bootstrap ship starts require a vessel display name of at most {ShipState.MaximumVesselDisplayNameLength} characters and strategic state.",
                nameof(shipStarts)
            );
        }

        if (shipStarts.Select(start => start.InstanceId).Distinct().Count() != shipStarts.Length)
        {
            throw new ArgumentException("Bootstrap ship starts require unique identities.", nameof(shipStarts));
        }

        if (playerShipId.Value <= 0 || shipStarts.Count(start => start.InstanceId == playerShipId) != 1)
        {
            throw new ArgumentException("Player ship identity must resolve exactly once.", nameof(playerShipId));
        }

        if (shipStarts.Single(start => start.InstanceId == playerShipId).ActiveOrder is not null)
        {
            throw new ArgumentException("The player ship cannot declare an autonomous order.", nameof(shipStarts));
        }
    }

    private static void ValidateInitialTime(SimulationTime initialTime)
    {
        if (initialTime.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException("Initial time must align to the fixed simulation step.", nameof(initialTime));
        }

        if (initialTime.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds)
        {
            throw new ArgumentException(
                "Initial time must retain one fixed-step of continuation headroom.",
                nameof(initialTime)
            );
        }
    }

    /// <summary>Validates catalog-dependent declarations and creates a new live simulation.</summary>
    public GameSimulation CreateSimulation(ShipDefinitionCatalog catalog) =>
        CreateSimulation(catalog, FactionDefinitionCatalog.Empty);

    /// <summary>Validates both definition catalogs and creates a faction-aware live simulation.</summary>
    public GameSimulation CreateSimulation(ShipDefinitionCatalog catalog, FactionDefinitionCatalog factionCatalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factionCatalog);
        var scheduler = SimulationScheduler.Create();
        var orderIdAllocator = ShipOrderIdAllocator.Create();
        List<ShipState> ships = [];

        // Canonical ship order plus repair-before-travel-before-order scheduling defines stable work identities and
        // sequences without exposing either scheduler concern to authored declarations.
        foreach (ShipStart start in _shipStarts)
        {
            ships.Add(CreateShip(start, catalog, ref scheduler, ref orderIdAllocator));
        }

        long nextShipId = checked(_shipStarts[^1].InstanceId.Value + 1);
        FactionState[] factions = CreateFactions(factionCatalog, ref scheduler);
        var candidate = new SimulationState(
            InitialTime,
            scheduler,
            ShipInstanceIdAllocator.Restore(nextShipId),
            StrategicMap,
            PlayerShipId,
            ships,
            orderIdAllocator,
            factions
        );
        return GameSimulation.RestoreState(candidate, catalog, factionCatalog);
    }

    private ShipState CreateShip(
        ShipStart start,
        ShipDefinitionCatalog catalog,
        ref SimulationScheduler scheduler,
        ref ShipOrderIdAllocator orderIdAllocator
    )
    {
        ShipDefinition definition = catalog.GetRequired(start.DefinitionId);
        SystemRepairState? repair = CreateRepair(start, definition, ref scheduler);
        var engineering = new ShipEngineeringState(
            start.GenerationCondition,
            start.SensorCondition,
            start.ImpulseCondition,
            start.Allocation,
            repair
        );
        engineering.Validate(definition.Engineering);
        double maximumSpeed =
            definition.MaximumTacticalSpeed.Value * engineering.ImpulseCapability(definition.Engineering);
        if (start.TacticalMotion.Speed.Value > maximumSpeed)
        {
            throw new ArgumentException("Tactical speed exceeds current effective propulsion.", nameof(catalog));
        }

        ShipStrategicState strategic = start.Strategic switch
        {
            AtLocationStart atLocation => CreateAtLocation(atLocation),
            TravelingStart traveling => CreateTraveling(start, traveling, ref scheduler),
            _ => throw new ArgumentException("Ship strategic start kind is unsupported.", nameof(catalog)),
        };
        ShipOrder? order = CreateOrder(start, strategic, ref scheduler, ref orderIdAllocator);
        return new ShipState(
            start.InstanceId,
            start.DefinitionId,
            start.VesselDisplayName,
            start.TacticalPosition,
            start.TacticalMotion,
            engineering,
            strategic,
            order,
            directControllerFactionId: start.DirectControllerFactionId
        );
    }

    private FactionState[] CreateFactions(FactionDefinitionCatalog catalog, ref SimulationScheduler scheduler)
    {
        List<FactionState> factions = [];
        foreach (FactionStart start in _factionStarts)
        {
            catalog.GetRequired(start.DefinitionId);
            EstablishPresenceObjectiveState? objective = start.PresenceTargetLocationId is { } target
                ? new EstablishPresenceObjectiveState(target, FactionObjectiveStatus.Pending)
                : null;
            PendingFactionDecisionWake? pendingWake = null;
            if (objective is not null)
            {
                (scheduler, ScheduledWork work) = scheduler.Schedule(
                    InitialTime,
                    ScheduledWorkTarget.ForFaction(start.Id),
                    ScheduledWorkKind.FactionDecisionWake
                );
                pendingWake = new PendingFactionDecisionWake(work.Id, work.DueTime);
            }
            factions.Add(new FactionState(start.Id, start.DefinitionId, objective, pendingWake));
        }

        return [.. factions];
    }

    private SystemRepairState? CreateRepair(
        ShipStart ship,
        ShipDefinition definition,
        ref SimulationScheduler scheduler
    )
    {
        if (ship.SystemRepair is not SystemRepairStart start)
        {
            return null;
        }

        SimulationDuration duration;
        try
        {
            duration = definition.Engineering.RepairDurationFor(start.TargetSystem);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Active system repair target is unsupported.", nameof(ship), exception);
        }

        SimulationTime completion = start.StartedAt.AdvanceBy(duration);
        if (
            start.TargetCondition.Value <= start.StartingCondition.Value
            || start.StartedAt.Milliseconds > InitialTime.Milliseconds
            || InitialTime.Milliseconds >= completion.Milliseconds
        )
        {
            throw new ArgumentException(
                "Active system repair declaration is outside its valid interval.",
                nameof(ship)
            );
        }

        (scheduler, ScheduledWork work) = scheduler.Schedule(
            completion,
            ship.InstanceId,
            ScheduledWorkKind.SystemRepairCompletion
        );
        var repair = new SystemRepairState(
            start.TargetSystem,
            start.StartingCondition,
            start.TargetCondition,
            start.StartedAt,
            completion,
            work.Id
        );
        SystemCondition declared =
            start.TargetSystem == ShipSystemId.Sensors ? ship.SensorCondition : ship.ImpulseCondition;
        if (declared != repair.ConditionAt(InitialTime))
        {
            throw new ArgumentException("System condition must match active repair progress.", nameof(ship));
        }

        return repair;
    }

    private AtLocationState CreateAtLocation(AtLocationStart start)
    {
        StrategicMap.GetLocation(start.LocationId);
        return new AtLocationState(start.LocationId);
    }

    private TravelingState CreateTraveling(ShipStart ship, TravelingStart start, ref SimulationScheduler scheduler)
    {
        StrategicRoute route =
            StrategicMap.FindRoute(start.Origin, start.Destination)
            ?? throw new ArgumentException("Active travel must follow a declared map route.", nameof(start));
        SimulationTime arrival = start.Departure.AdvanceBy(route.Duration);
        if (
            start.Origin == start.Destination
            || start.Departure.Milliseconds > InitialTime.Milliseconds
            || InitialTime.Milliseconds >= arrival.Milliseconds
            || ship.TacticalMotion != default
        )
        {
            throw new ArgumentException("Active travel declaration is invalid at initial time.", nameof(start));
        }

        (scheduler, ScheduledWork work) = scheduler.Schedule(arrival, ship.InstanceId, ScheduledWorkKind.TravelArrival);
        return new TravelingState(new TravelState(start.Origin, start.Destination, start.Departure, arrival, work.Id));
    }

    private ShipOrder? CreateOrder(
        ShipStart ship,
        ShipStrategicState strategicState,
        ref SimulationScheduler scheduler,
        ref ShipOrderIdAllocator allocator
    )
    {
        if (ship.ActiveOrder is null)
        {
            return null;
        }

        (allocator, ShipOrderId id) = allocator.Allocate();
        return ship.ActiveOrder switch
        {
            TravelToOrderStart travelTo => CreateTravelToOrder(travelTo, strategicState, id),
            PatrolRouteOrderStart patrol => CreatePatrolOrder(patrol, strategicState, id),
            HoldUntilOrderStart hold => CreateHoldOrder(ship, hold, strategicState, id, ref scheduler),
            _ => throw new ArgumentException("Ship order start kind is unsupported.", nameof(ship)),
        };
    }

    private static TravelToOrder CreateTravelToOrder(
        TravelToOrderStart start,
        ShipStrategicState strategicState,
        ShipOrderId id
    )
    {
        if (strategicState is not TravelingState traveling || traveling.Travel.Destination != start.Destination)
        {
            throw new ArgumentException(
                "A TravelTo order start must match active travel to its destination.",
                nameof(start)
            );
        }

        return new TravelToOrder(id, start.Destination);
    }

    private PatrolRouteOrder CreatePatrolOrder(
        PatrolRouteOrderStart start,
        ShipStrategicState strategicState,
        ShipOrderId id
    )
    {
        foreach (LocationId waypoint in start.Waypoints)
        {
            StrategicMap.GetLocation(waypoint);
        }

        for (int index = 0; index < start.Waypoints.Length; index++)
        {
            LocationId origin = start.Waypoints[index];
            LocationId destination = start.Waypoints[(index + 1) % start.Waypoints.Length];
            if (StrategicMap.FindRoute(origin, destination) is null)
            {
                throw new ArgumentException(
                    "Every adjacent patrol waypoint, including wraparound, requires a route.",
                    nameof(start)
                );
            }
        }

        int previousIndex = (start.NextWaypointIndex - 1 + start.Waypoints.Length) % start.Waypoints.Length;
        if (
            strategicState is not TravelingState traveling
            || traveling.Travel.Origin != start.Waypoints[previousIndex]
            || traveling.Travel.Destination != start.Waypoints[start.NextWaypointIndex]
        )
        {
            throw new ArgumentException("A patrol order start must match its declared current leg.", nameof(start));
        }

        return new PatrolRouteOrder(id, start.Waypoints, start.NextWaypointIndex);
    }

    private HoldUntilOrder CreateHoldOrder(
        ShipStart ship,
        HoldUntilOrderStart start,
        ShipStrategicState strategicState,
        ShipOrderId id,
        ref SimulationScheduler scheduler
    )
    {
        if (strategicState is not AtLocationState || start.Until.Milliseconds <= InitialTime.Milliseconds)
        {
            throw new ArgumentException(
                "A HoldUntil order start requires an at-location ship and a future wake time.",
                nameof(start)
            );
        }

        if (
            start.Until.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0
            || start.Until.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
        )
        {
            throw new ArgumentException(
                "A HoldUntil wake must align to the fixed simulation step and retain continuation headroom.",
                nameof(start)
            );
        }

        (scheduler, ScheduledWork wake) = scheduler.Schedule(start.Until, ship.InstanceId, ScheduledWorkKind.OrderWake);
        return new HoldUntilOrder(id, start.Until, wake.Id);
    }
}
