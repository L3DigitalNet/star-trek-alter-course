using System.Collections.ObjectModel;
using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Validates typed world starts and derives a complete deterministic simulation aggregate.</summary>
public sealed class GameBootstrap
{
    private readonly ReadOnlyCollection<ShipStart> _shipStarts;

    /// <summary>Initializes an immutable bootstrap declaration in canonical ship-identity order.</summary>
    public GameBootstrap(
        SimulationTime initialTime,
        StrategicMap strategicMap,
        ShipInstanceId playerShipId,
        IEnumerable<ShipStart> shipStarts
    )
    {
        ArgumentNullException.ThrowIfNull(strategicMap);
        ArgumentNullException.ThrowIfNull(shipStarts);
        ShipStart[] materialized = shipStarts.ToArray();
        if (materialized.Length == 0 || materialized.Any(start => start is null))
        {
            throw new ArgumentException("A bootstrap requires at least one nonnull ship start.", nameof(shipStarts));
        }

        if (materialized.Any(start => start.InstanceId.Value <= 0))
        {
            throw new ArgumentException("Bootstrap ship starts require initialized identities.", nameof(shipStarts));
        }

        if (materialized.Any(start => string.IsNullOrWhiteSpace(start.VesselDisplayName) || start.Strategic is null))
        {
            throw new ArgumentException(
                "Bootstrap ship starts require a vessel display name and strategic state.",
                nameof(shipStarts)
            );
        }

        if (materialized.Select(start => start.InstanceId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Bootstrap ship starts require unique identities.", nameof(shipStarts));
        }

        if (playerShipId.Value <= 0 || materialized.Count(start => start.InstanceId == playerShipId) != 1)
        {
            throw new ArgumentException("Player ship identity must resolve exactly once.", nameof(playerShipId));
        }

        if (initialTime.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException("Initial time must align to the fixed simulation step.", nameof(initialTime));
        }

        InitialTime = initialTime;
        StrategicMap = strategicMap;
        PlayerShipId = playerShipId;
        _shipStarts = Array.AsReadOnly(materialized.OrderBy(start => start.InstanceId.Value).ToArray());
    }

    /// <summary>Gets the initial authoritative simulation time.</summary>
    public SimulationTime InitialTime { get; }

    /// <summary>Gets the semantic strategic map used by the world.</summary>
    public StrategicMap StrategicMap { get; }

    /// <summary>Gets the sole player-controlled ship identity.</summary>
    public ShipInstanceId PlayerShipId { get; }

    /// <summary>Gets ship declarations in ascending instance-identity order.</summary>
    public IReadOnlyList<ShipStart> ShipStarts => _shipStarts;

    /// <summary>Validates catalog-dependent declarations and creates a new live simulation.</summary>
    public GameSimulation CreateSimulation(ShipDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var scheduler = SimulationScheduler.Create();
        List<ShipState> ships = [];

        // Ship order and repair-before-travel scheduling jointly define stable work identities and sequences.
        foreach (ShipStart start in _shipStarts)
        {
            ShipDefinition definition = catalog.GetRequired(start.DefinitionId);
            if (start.TacticalMotion.Speed.Value > definition.MaximumTacticalSpeed.Value)
            {
                throw new ArgumentException("Tactical speed exceeds the ship definition maximum.", nameof(catalog));
            }

            SensorRepairState? repair = CreateRepair(start, definition, ref scheduler);
            ShipStrategicState strategicState = start.Strategic switch
            {
                AtLocationStart atLocation => CreateAtLocation(atLocation),
                TravelingStart traveling => CreateTraveling(start, traveling, ref scheduler),
                _ => throw new ArgumentException("Ship strategic start kind is unsupported.", nameof(catalog)),
            };
            ships.Add(
                new ShipState(
                    start.InstanceId,
                    start.DefinitionId,
                    start.VesselDisplayName,
                    start.TacticalPosition,
                    start.TacticalMotion,
                    start.SensorIntegrity,
                    repair,
                    strategicState
                )
            );
        }

        long nextShipId = checked(_shipStarts[^1].InstanceId.Value + 1);
        var candidate = new SimulationState(
            InitialTime,
            scheduler,
            ShipInstanceIdAllocator.Restore(nextShipId),
            StrategicMap,
            PlayerShipId,
            ships
        );
        return GameSimulation.RestoreState(candidate, catalog);
    }

    private SensorRepairState? CreateRepair(
        ShipStart ship,
        ShipDefinition definition,
        ref SimulationScheduler scheduler
    )
    {
        if (ship.SensorRepair is not SensorRepairStart start)
        {
            return null;
        }

        SimulationTime completion = start.StartedAt.AdvanceBy(definition.SensorRepairDuration);
        if (
            start.TargetIntegrity.Value <= start.StartingIntegrity.Value
            || start.StartedAt.Milliseconds > InitialTime.Milliseconds
            || InitialTime.Milliseconds >= completion.Milliseconds
        )
        {
            throw new ArgumentException(
                "Active sensor repair declaration is outside its valid interval.",
                nameof(ship)
            );
        }

        (scheduler, ScheduledWork work) = scheduler.Schedule(
            completion,
            ship.InstanceId,
            ScheduledWorkKind.SensorRepairCompletion
        );
        var repair = new SensorRepairState(
            start.StartingIntegrity,
            start.TargetIntegrity,
            start.StartedAt,
            completion,
            work.Id
        );
        if (ship.SensorIntegrity != repair.IntegrityAt(InitialTime))
        {
            throw new ArgumentException("Sensor integrity must match active repair progress.", nameof(ship));
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
}
