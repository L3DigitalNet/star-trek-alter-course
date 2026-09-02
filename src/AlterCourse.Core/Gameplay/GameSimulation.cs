using AlterCourse.Core.Content;
using AlterCourse.Core.Player;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Owns authoritative mutation and immutable definition content for the simulation.</summary>
public sealed class GameSimulation
{
    private const int SameBoundaryExecutionBudget = 1024;
    private const long ShipStepWorkBudget = 1_000_000;

    // Local arrival frames use one non-origin offset so tactical positions remain continuous values,
    // not a hidden grid or strategic-map projection.
    private static readonly TacticalPosition ArrivalPosition = new(0.25, -0.75);
    private readonly ShipDefinitionCatalog _shipCatalog;
    private SimulationState _state;

    private GameSimulation(SimulationState state, ShipDefinitionCatalog shipCatalog)
    {
        ArgumentNullException.ThrowIfNull(shipCatalog);
        state.Validate(shipCatalog);
        _state = state;
        _shipCatalog = shipCatalog;
    }

    /// <summary>Returns a fresh read-only projection of player-known simulation state.</summary>
    public PlayerProjection GetPlayerProjection() => Project(_state);

    /// <summary>Validates and schedules persistent strategic travel for the player ship.</summary>
    public TravelRequestResult RequestTravel(TravelIntent intent)
    {
        ShipState playerShip = _state.GetRequiredShip(_state.PlayerShipId);
        if (playerShip.StrategicState is TravelingState)
        {
            return new TravelRequestResult(TravelOutcome.AlreadyTraveling);
        }

        var atLocation = (AtLocationState)playerShip.StrategicState;
        if (intent.Destination == atLocation.LocationId)
        {
            return new TravelRequestResult(TravelOutcome.SameLocation);
        }

        StrategicRoute? route = _state.StrategicMap.FindRoute(atLocation.LocationId, intent.Destination);
        if (route is null)
        {
            return new TravelRequestResult(TravelOutcome.RouteUnavailable);
        }

        SimulationTime arrival = _state.Time.AdvanceBy(route.Duration);
        (SimulationScheduler scheduler, ScheduledWork arrivalWork) = _state.Scheduler.Schedule(
            arrival,
            playerShip.InstanceId,
            ScheduledWorkKind.TravelArrival
        );
        var travel = new TravelState(atLocation.LocationId, intent.Destination, _state.Time, arrival, arrivalWork.Id);
        ShipState travelingShip = playerShip with
        {
            StrategicState = new TravelingState(travel),
            TacticalMotion = default,
        };
        SimulationState candidate = _state.ReplaceShip(playerShip.InstanceId, travelingShip) with
        {
            Scheduler = scheduler,
        };
        Commit(candidate);
        return new TravelRequestResult(TravelOutcome.Accepted);
    }

    /// <summary>Validates and changes only the player ship's local tactical motion.</summary>
    public SetTacticalCourseResult SetTacticalCourse(SetTacticalCourseIntent intent)
    {
        ShipState playerShip = _state.GetRequiredShip(_state.PlayerShipId);
        if (playerShip.StrategicState is TravelingState)
        {
            return new SetTacticalCourseResult(SetTacticalCourseOutcome.UnavailableWhileTraveling);
        }

        ShipDefinition definition = _shipCatalog.GetRequired(playerShip.DefinitionId);
        if (intent.Speed.Value > definition.MaximumTacticalSpeed.Value)
        {
            return new SetTacticalCourseResult(SetTacticalCourseOutcome.SpeedExceedsMaximum);
        }

        SimulationState candidate = _state.ReplaceShip(
            playerShip.InstanceId,
            playerShip with
            {
                TacticalMotion = new TacticalMotion(intent.Heading, intent.Speed),
            }
        );
        Commit(candidate);
        return new SetTacticalCourseResult(SetTacticalCourseOutcome.Accepted);
    }

    /// <summary>Advances by explicit one-hundred-millisecond steps and returns resolved consequences.</summary>
    public SimulationAdvanceResult AdvanceFixedSteps(int stepCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stepCount);
        int milliseconds = checked(stepCount * checked((int)SimulationFixedStep.Duration.Milliseconds));
        SimulationTime target = _state.Time.AdvanceBy(new SimulationDuration(milliseconds));
        (SimulationState candidate, IReadOnlyList<ScheduledWorkKind> resolvedKinds) = AdvanceTo(_state, target);
        Commit(candidate);
        return new SimulationAdvanceResult(_state.Time, resolvedKinds, Project(_state));
    }

    /// <summary>Advances through the ordinary scheduler path to the earliest current event boundary.</summary>
    public AdvanceUntilResult AdvanceUntilNextScheduledEvent()
    {
        if (_state.Scheduler.OutstandingWork.IsDefaultOrEmpty)
        {
            PlayerProjection unchanged = Project(_state);
            return new AdvanceUntilResult(
                AdvanceUntilOutcome.NoScheduledEvent,
                _state.Time,
                new ReadOnlyValueList<ScheduledWorkKind>([]),
                unchanged
            );
        }

        SimulationTime boundary = _state.Scheduler.OutstandingWork[0].DueTime;
        (SimulationState candidate, IReadOnlyList<ScheduledWorkKind> resolvedKinds) = AdvanceTo(_state, boundary);
        Commit(candidate);
        return new AdvanceUntilResult(
            AdvanceUntilOutcome.ScheduledEventResolved,
            _state.Time,
            resolvedKinds,
            Project(_state)
        );
    }

    // Persistence translates explicit snapshots without gaining a public mutation path into live state.
    internal SimulationState CaptureState() => _state;

    internal static GameSimulation RestoreState(SimulationState restoredState, ShipDefinitionCatalog shipCatalog) =>
        new(restoredState, shipCatalog);

    private static (SimulationState State, IReadOnlyList<ScheduledWorkKind> ResolvedKinds) AdvanceTo(
        SimulationState initial,
        SimulationTime target
    )
    {
        initial.Time.AdvanceTo(target);
        long elapsed = target.Milliseconds - initial.Time.Milliseconds;
        if (elapsed % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new InvalidOperationException("Advancement boundaries must be fixed-step aligned.");
        }

        long stepCount = elapsed / SimulationFixedStep.Duration.Milliseconds;
        // The whole call is admitted before the first immutable replacement so a rejected catch-up
        // cannot expose a partially advanced aggregate to its caller.
        if (stepCount > ShipStepWorkBudget / initial.Ships.Length)
        {
            throw new InvalidOperationException(
                $"Advancement exceeds the finite {ShipStepWorkBudget} ship-step work budget."
            );
        }

        SimulationState current = initial;
        List<ScheduledWorkKind> resolvedKinds = [];

        while (current.Time.Milliseconds < target.Milliseconds)
        {
            SimulationTime boundary = target;
            if (
                !current.Scheduler.OutstandingWork.IsDefaultOrEmpty
                && current.Scheduler.OutstandingWork[0].DueTime.Milliseconds <= target.Milliseconds
            )
            {
                boundary = current.Scheduler.OutstandingWork[0].DueTime;
            }

            current = AdvanceSegment(current, boundary);
            current = ResolveDueAtCurrentBoundary(current, resolvedKinds);
        }

        current = ResolveDueAtCurrentBoundary(current, resolvedKinds);
        return (current, new ReadOnlyValueList<ScheduledWorkKind>(resolvedKinds));
    }

    private static SimulationState AdvanceSegment(SimulationState state, SimulationTime boundary)
    {
        long elapsed = boundary.Milliseconds - state.Time.Milliseconds;
        if (elapsed < 0 || elapsed % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new InvalidOperationException("Advancement boundaries must be monotonic and fixed-step aligned.");
        }

        SimulationState current = state;
        while (current.Time.Milliseconds < boundary.Milliseconds)
        {
            SimulationTime nextTime = current.Time.AdvanceBy(SimulationFixedStep.Duration);
            var replacements = new ShipState[current.Ships.Length];
            for (int index = 0; index < current.Ships.Length; index++)
            {
                ShipState ship = current.Ships[index];
                ShipState advanced = ship;
                if (ship.StrategicState is AtLocationState)
                {
                    advanced = advanced with
                    {
                        TacticalPosition = ship.TacticalPosition.Advance(
                            ship.TacticalMotion,
                            SimulationFixedStep.Duration.Milliseconds / 1000.0
                        ),
                    };
                }

                if (ship.SensorRepair is SensorRepairState repair)
                {
                    advanced = advanced with { SensorIntegrity = repair.IntegrityAt(nextTime) };
                }

                replacements[index] = advanced;
            }

            current = current.ReplaceShips(replacements) with { Time = nextTime };
        }

        return current;
    }

    private static SimulationState ResolveDueAtCurrentBoundary(
        SimulationState state,
        List<ScheduledWorkKind> resolvedKinds
    )
    {
        SimulationState current = state;
        int executions = 0;

        // Repeating batch dequeue permits consequences to schedule same-boundary work without
        // leaving it overdue; the finite budget prevents an infinite consequence cycle.
        while (true)
        {
            (SimulationScheduler scheduler, IReadOnlyList<ScheduledWork> dueWork) = current.Scheduler.DequeueDue(
                current.Time
            );
            if (dueWork.Count == 0)
            {
                return current;
            }

            current = current with { Scheduler = scheduler };
            foreach (ScheduledWork work in dueWork)
            {
                executions = checked(executions + 1);
                if (executions > SameBoundaryExecutionBudget)
                {
                    throw new InvalidOperationException(
                        "Scheduled work exceeded the finite same-boundary execution budget."
                    );
                }

                current = ResolveScheduledWork(current, work);
                resolvedKinds.Add(work.Kind);
            }
        }
    }

    private static SimulationState ResolveScheduledWork(SimulationState state, ScheduledWork work) =>
        work.Kind switch
        {
            ScheduledWorkKind.SensorRepairCompletion => CompleteSensorRepair(state, work),
            ScheduledWorkKind.TravelArrival => CompleteTravel(state, work),
            _ => throw new InvalidOperationException("Scheduled work kind is unsupported."),
        };

    private static SimulationState CompleteSensorRepair(SimulationState state, ScheduledWork work)
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        SensorRepairState? repair = ship.SensorRepair;
        if (repair is null || repair.ScheduledCompletionId != work.Id)
        {
            throw new InvalidOperationException("Sensor completion lacks matching active repair.");
        }

        return state.ReplaceShip(
            ship.InstanceId,
            ship with
            {
                SensorIntegrity = repair.TargetIntegrity,
                SensorRepair = null,
            }
        );
    }

    private static SimulationState CompleteTravel(SimulationState state, ScheduledWork work)
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        if (ship.StrategicState is not TravelingState traveling || traveling.Travel.ScheduledArrivalId != work.Id)
        {
            throw new InvalidOperationException("Arrival lacks matching active travel.");
        }

        return state.ReplaceShip(
            ship.InstanceId,
            ship with
            {
                StrategicState = new AtLocationState(traveling.Travel.Destination),
                TacticalPosition = ArrivalPosition,
                TacticalMotion = default,
            }
        );
    }

    private static PlayerProjection Project(SimulationState state)
    {
        ShipState playerShip = state.GetRequiredShip(state.PlayerShipId);
        return new PlayerProjection(
            state.Time,
            ProjectStrategic(state, playerShip),
            ProjectShip(state, playerShip),
            new ReadOnlyValueList<PlayerAction>(GetAvailableActions(playerShip))
        );
    }

    private static StrategicProjection ProjectStrategic(SimulationState state, ShipState playerShip)
    {
        StrategicLocationProjection[] locations =
        [
            .. state.StrategicMap.Locations.Select(location => new StrategicLocationProjection(
                location.Id,
                location.DisplayName,
                location.Position
            )),
        ];
        StrategicRouteProjection[] routes =
        [
            .. state.StrategicMap.Routes.Select(route => new StrategicRouteProjection(
                route.Origin,
                route.Destination,
                route.Duration
            )),
        ];

        StrategicLocationProjection? currentLocation = null;
        TravelProjection? travel = null;
        if (playerShip.StrategicState is AtLocationState atLocation)
        {
            StrategicLocation location = state.StrategicMap.GetLocation(atLocation.LocationId);
            currentLocation = new StrategicLocationProjection(location.Id, location.DisplayName, location.Position);
        }
        else if (playerShip.StrategicState is TravelingState traveling)
        {
            TravelState active = traveling.Travel;
            travel = new TravelProjection(
                active.Origin,
                active.Destination,
                active.Departure,
                active.ExpectedArrival,
                active.IsActive
            );
        }

        return new StrategicProjection(
            new ReadOnlyValueList<StrategicLocationProjection>(locations),
            new ReadOnlyValueList<StrategicRouteProjection>(routes),
            currentLocation,
            travel
        );
    }

    private static PlayerShipProjection ProjectShip(SimulationState state, ShipState playerShip)
    {
        SensorRepairState? repair = playerShip.SensorRepair;
        return new PlayerShipProjection(
            playerShip.InstanceId,
            playerShip.DefinitionId,
            playerShip.VesselDisplayName,
            new TacticalProjection(
                new TacticalPositionProjection(
                    playerShip.TacticalPosition.XKilometers,
                    playerShip.TacticalPosition.YKilometers
                ),
                playerShip.TacticalMotion.Heading.Value,
                playerShip.TacticalMotion.Speed.Value
            ),
            new SensorProjection(
                playerShip.SensorIntegrity.Value,
                repair?.ProgressAt(state.Time) ?? 1,
                repair is not null
            )
        );
    }

    private static PlayerAction[] GetAvailableActions(ShipState playerShip) =>
        playerShip.StrategicState is AtLocationState
            ? [PlayerAction.Travel, PlayerAction.SetTacticalCourse, PlayerAction.AdvanceTime]
            : [PlayerAction.AdvanceTime];

    private void Commit(SimulationState candidate)
    {
        candidate.Validate(_shipCatalog);
        _state = candidate;
    }
}
