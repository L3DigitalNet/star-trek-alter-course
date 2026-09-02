using AlterCourse.Core.Player;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Owns all authoritative mutation for the first playable simulation slice.</summary>
public sealed class GameSimulation
{
    private const int SameBoundaryExecutionBudget = 1024;
    private SimulationState state;

    private GameSimulation(SimulationState state)
    {
        state.Validate();
        this.state = state;
    }

    /// <summary>Returns a fresh read-only projection of player-known simulation state.</summary>
    public PlayerProjection GetPlayerProjection() => Project(state);

    /// <summary>Validates and schedules persistent strategic travel without advancing time.</summary>
    public TravelRequestResult RequestTravel(TravelIntent intent)
    {
        if (state.StrategicState is TravelingState)
        {
            return new TravelRequestResult(TravelOutcome.AlreadyTraveling);
        }

        var atLocation = (AtLocationState)state.StrategicState;
        if (intent.Destination == atLocation.LocationId)
        {
            return new TravelRequestResult(TravelOutcome.SameLocation);
        }

        StrategicRoute? route = state.StrategicMap.FindRoute(
            atLocation.LocationId,
            intent.Destination
        );
        if (route is null)
        {
            return new TravelRequestResult(TravelOutcome.RouteUnavailable);
        }

        SimulationTime arrival = state.Time.AdvanceBy(route.Duration);
        (SimulationScheduler scheduler, ScheduledWork arrivalWork) = state.Scheduler.Schedule(
            arrival,
            ScheduledWorkKind.TravelArrival
        );
        var travel = new TravelState(
            atLocation.LocationId,
            intent.Destination,
            state.Time,
            arrival,
            arrivalWork.Id
        );
        SimulationState candidate = state with
        {
            Scheduler = scheduler,
            StrategicState = new TravelingState(travel),
        };
        Commit(candidate);
        return new TravelRequestResult(TravelOutcome.Accepted);
    }

    /// <summary>Validates and changes only authoritative local tactical motion.</summary>
    public SetTacticalCourseResult SetTacticalCourse(SetTacticalCourseIntent intent)
    {
        if (state.StrategicState is TravelingState)
        {
            return new SetTacticalCourseResult(
                SetTacticalCourseOutcome.UnavailableWhileTraveling
            );
        }

        if (intent.Speed.Value > state.PlayerShipDefinition.MaximumTacticalSpeed.Value)
        {
            return new SetTacticalCourseResult(SetTacticalCourseOutcome.SpeedExceedsMaximum);
        }

        SimulationState candidate = state with
        {
            PlayerShip = state.PlayerShip with
            {
                TacticalMotion = new TacticalMotion(intent.Heading, intent.Speed),
            },
        };
        Commit(candidate);
        return new SetTacticalCourseResult(SetTacticalCourseOutcome.Accepted);
    }

    /// <summary>Advances by an explicit number of authoritative one-hundred-millisecond steps.</summary>
    public void AdvanceFixedSteps(int stepCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stepCount);
        int milliseconds = checked(
            stepCount * checked((int)SimulationFixedStep.Duration.Milliseconds)
        );
        SimulationTime target = state.Time.AdvanceBy(new SimulationDuration(milliseconds));
        (SimulationState candidate, _) = AdvanceTo(state, target);
        Commit(candidate);
    }

    /// <summary>Advances through the ordinary scheduler path to the earliest current event boundary.</summary>
    public AdvanceUntilResult AdvanceUntilNextScheduledEvent()
    {
        if (state.Scheduler.OutstandingWork.IsDefaultOrEmpty)
        {
            PlayerProjection unchanged = Project(state);
            return new AdvanceUntilResult(
                AdvanceUntilOutcome.NoScheduledEvent,
                state.Time,
                new ReadOnlyValueList<ScheduledWorkKind>([]),
                unchanged
            );
        }

        SimulationTime boundary = state.Scheduler.OutstandingWork[0].DueTime;
        (SimulationState candidate, IReadOnlyList<ScheduledWorkKind> resolvedKinds) = AdvanceTo(
            state,
            boundary
        );
        Commit(candidate);
        return new AdvanceUntilResult(
            AdvanceUntilOutcome.ScheduledEventResolved,
            state.Time,
            resolvedKinds,
            Project(state)
        );
    }

    // This same-assembly seam preserves aggregate ownership: persistence can translate explicit
    // snapshot models without gaining a second public mutation path into live simulation state.
    internal SimulationState CaptureState() => state;

    internal static GameSimulation RestoreState(SimulationState restoredState) =>
        new(restoredState);

    private static (SimulationState State, IReadOnlyList<ScheduledWorkKind> ResolvedKinds) AdvanceTo(
        SimulationState initial,
        SimulationTime target
    )
    {
        initial.Time.AdvanceTo(target);
        SimulationState current = initial;
        List<ScheduledWorkKind> resolvedKinds = [];

        while (current.Time.Milliseconds < target.Milliseconds)
        {
            SimulationTime boundary = target;
            if (
                !current.Scheduler.OutstandingWork.IsDefaultOrEmpty
                && current.Scheduler.OutstandingWork[0].DueTime.Milliseconds
                    <= target.Milliseconds
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
            throw new InvalidOperationException(
                "Advancement boundaries must be monotonic and fixed-step aligned."
            );
        }

        if (state.StrategicState is TravelingState)
        {
            return AdvanceClock(state, boundary);
        }

        SimulationState current = state;
        while (current.Time.Milliseconds < boundary.Milliseconds)
        {
            SimulationTime nextTime = current.Time.AdvanceBy(SimulationFixedStep.Duration);
            TacticalPosition nextPosition = current.PlayerShip.TacticalPosition.Advance(
                current.PlayerShip.TacticalMotion,
                SimulationFixedStep.Duration.Milliseconds / 1000.0
            );
            current = AdvanceClock(
                current with
                {
                    PlayerShip = current.PlayerShip with { TacticalPosition = nextPosition },
                },
                nextTime
            );
        }

        return current;
    }

    private static SimulationState AdvanceClock(SimulationState state, SimulationTime time)
    {
        PlayerShipState ship = state.PlayerShip;
        if (ship.SensorRepair is SensorRepairState repair)
        {
            ship = ship with { SensorIntegrity = repair.IntegrityAt(time) };
        }

        return state with { Time = time, PlayerShip = ship };
    }

    private static SimulationState ResolveDueAtCurrentBoundary(
        SimulationState state,
        List<ScheduledWorkKind> resolvedKinds
    )
    {
        SimulationState current = state;
        int executions = 0;

        // Dequeue snapshots one batch. Repeating permits consequences to schedule same-boundary
        // work later without silently leaving it overdue; the budget prevents an infinite cycle.
        while (true)
        {
            (SimulationScheduler scheduler, IReadOnlyList<ScheduledWork> dueWork) =
                current.Scheduler.DequeueDue(current.Time);
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

    private static SimulationState ResolveScheduledWork(
        SimulationState state,
        ScheduledWork work
    ) =>
        work.Kind switch
        {
            ScheduledWorkKind.SensorRepairCompletion => CompleteSensorRepair(state, work),
            ScheduledWorkKind.TravelArrival => CompleteTravel(state, work),
            _ => throw new InvalidOperationException("Scheduled work kind is unsupported."),
        };

    private static SimulationState CompleteSensorRepair(
        SimulationState state,
        ScheduledWork work
    )
    {
        SensorRepairState? repair = state.PlayerShip.SensorRepair;
        if (repair is null || repair.ScheduledCompletionId != work.Id)
        {
            throw new InvalidOperationException("Sensor completion lacks matching active repair.");
        }

        return state with
        {
            PlayerShip = state.PlayerShip with
            {
                SensorIntegrity = repair.TargetIntegrity,
                SensorRepair = null,
            },
        };
    }

    private static SimulationState CompleteTravel(SimulationState state, ScheduledWork work)
    {
        if (
            state.StrategicState is not TravelingState traveling
            || traveling.Travel.ScheduledArrivalId != work.Id
        )
        {
            throw new InvalidOperationException("Arrival lacks matching active travel.");
        }

        return state with
        {
            StrategicState = new AtLocationState(traveling.Travel.Destination),
            PlayerShip = state.PlayerShip with
            {
                TacticalPosition = new TacticalPosition(0.25, -0.75),
                TacticalMotion = default,
            },
        };
    }

    private static PlayerProjection Project(SimulationState state)
    {
        return new PlayerProjection(
            state.Time,
            ProjectStrategic(state),
            ProjectShip(state),
            new ReadOnlyValueList<PlayerAction>(GetAvailableActions(state))
        );
    }

    private static StrategicProjection ProjectStrategic(SimulationState state)
    {
        StrategicLocationProjection[] locations =
        [
            .. state.StrategicMap.Locations.Select(location =>
                new StrategicLocationProjection(location.Id, location.DisplayName, location.Position)
            ),
        ];
        StrategicRouteProjection[] routes =
        [
            .. state.StrategicMap.Routes.Select(route =>
                new StrategicRouteProjection(route.Origin, route.Destination, route.Duration)
            ),
        ];

        StrategicLocationProjection? currentLocation = null;
        TravelProjection? travel = null;
        if (state.StrategicState is AtLocationState atLocation)
        {
            StrategicLocation location = state.StrategicMap.GetLocation(atLocation.LocationId);
            currentLocation = new StrategicLocationProjection(
                location.Id,
                location.DisplayName,
                location.Position
            );
        }
        else if (state.StrategicState is TravelingState traveling)
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

    private static PlayerShipProjection ProjectShip(SimulationState state)
    {
        SensorRepairState? repair = state.PlayerShip.SensorRepair;
        return new PlayerShipProjection(
            state.PlayerShip.InstanceId,
            state.PlayerShip.DefinitionId,
            state.PlayerShipDefinition.DisplayName,
            new TacticalProjection(
                new TacticalPositionProjection(
                    state.PlayerShip.TacticalPosition.XKilometers,
                    state.PlayerShip.TacticalPosition.YKilometers
                ),
                state.PlayerShip.TacticalMotion.Heading.Value,
                state.PlayerShip.TacticalMotion.Speed.Value
            ),
            new SensorProjection(
                state.PlayerShip.SensorIntegrity.Value,
                repair?.ProgressAt(state.Time) ?? 1,
                repair is not null
            )
        );
    }

    private static PlayerAction[] GetAvailableActions(SimulationState state) =>
        state.StrategicState is AtLocationState
            ? [PlayerAction.Travel, PlayerAction.SetTacticalCourse, PlayerAction.AdvanceTime]
            : [PlayerAction.AdvanceTime];

    private void Commit(SimulationState candidate)
    {
        candidate.Validate();
        state = candidate;
    }
}
