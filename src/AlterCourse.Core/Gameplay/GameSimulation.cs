using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
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
    private const int TotalConsequenceExecutionBudget = 10_000;
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
        var command = new ShipTravelCommand(_state.PlayerShipId, intent.Destination);
        ShipTravelApplicationResult application = ApplyShipTravel(_state, command, _shipCatalog);
        if (application.Outcome == TravelOutcome.Accepted)
        {
            Commit(application.CandidateState);
        }

        return new TravelRequestResult(application.Outcome);
    }

    internal static ShipTravelApplicationResult ApplyShipTravel(
        SimulationState state,
        ShipTravelCommand command,
        ShipDefinitionCatalog shipCatalog
    )
    {
        ShipState targetShip = state.GetRequiredShip(command.TargetShipId);
        if (targetShip.StrategicState is TravelingState)
        {
            return new ShipTravelApplicationResult(TravelOutcome.AlreadyTraveling, state);
        }

        var atLocation = (AtLocationState)targetShip.StrategicState;
        if (command.Destination == atLocation.LocationId)
        {
            return new ShipTravelApplicationResult(TravelOutcome.SameLocation, state);
        }

        StrategicRoute? route = state.StrategicMap.FindRoute(atLocation.LocationId, command.Destination);
        if (route is null)
        {
            return new ShipTravelApplicationResult(TravelOutcome.RouteUnavailable, state);
        }

        SimulationTime arrival = state.Time.AdvanceBy(route.Duration);
        (SimulationScheduler scheduler, ScheduledWork arrivalWork) = state.Scheduler.Schedule(
            arrival,
            targetShip.InstanceId,
            ScheduledWorkKind.TravelArrival
        );
        var travel = new TravelState(atLocation.LocationId, command.Destination, state.Time, arrival, arrivalWork.Id);
        ShipState travelingShip = targetShip with
        {
            StrategicState = new TravelingState(travel),
            TacticalMotion = default,
        };
        SimulationState candidate = state.ReplaceShip(targetShip.InstanceId, travelingShip) with
        {
            Scheduler = scheduler,
        };
        candidate.Validate(shipCatalog);
        return new ShipTravelApplicationResult(TravelOutcome.Accepted, candidate);
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
        SimulationAdvanceTraceResult advance = AdvanceTo(_state, target, _shipCatalog);
        Commit(advance.State);
        return new SimulationAdvanceResult(
            _state.Time,
            PlayerVisibleKinds(advance.Traces, _state.PlayerShipId),
            Project(_state)
        );
    }

    /// <summary>Advances through hidden work to the next consequence targeting the player ship.</summary>
    public AdvanceUntilResult AdvanceUntilNextPlayerRelevantEvent()
    {
        ScheduledWork? nextPlayerWork = _state
            .Scheduler.OutstandingWork.Cast<ScheduledWork?>()
            .FirstOrDefault(work => work!.Value.TargetShipId == _state.PlayerShipId);
        if (nextPlayerWork is null)
        {
            PlayerProjection unchanged = Project(_state);
            return new AdvanceUntilResult(
                AdvanceUntilOutcome.NoScheduledEvent,
                _state.Time,
                new ReadOnlyValueList<ScheduledWorkKind>([]),
                unchanged
            );
        }

        SimulationTime boundary = nextPlayerWork.Value.DueTime;
        SimulationAdvanceTraceResult advance = AdvanceTo(_state, boundary, _shipCatalog);
        Commit(advance.State);
        return new AdvanceUntilResult(
            AdvanceUntilOutcome.ScheduledEventResolved,
            _state.Time,
            PlayerVisibleKinds(advance.Traces, _state.PlayerShipId),
            Project(_state)
        );
    }

    /// <summary>Advances safely to the next player-relevant scheduled consequence.</summary>
    public AdvanceUntilResult AdvanceUntilNextScheduledEvent() => AdvanceUntilNextPlayerRelevantEvent();

    internal OrderCancellationResult CancelOrder(ShipOrderId orderId)
    {
        ShipState? owner = _state.Ships.FirstOrDefault(ship => ship.ActiveOrder?.Id == orderId);
        if (owner is null)
        {
            return new OrderCancellationResult(OrderCancellationOutcome.NotFound);
        }

        SimulationScheduler scheduler = _state.Scheduler;
        if (owner.ActiveOrder is HoldUntilOrder hold)
        {
            (scheduler, bool removed) = scheduler.Cancel(hold.ScheduledWakeId);
            if (!removed)
            {
                throw new InvalidOperationException("A HoldUntil cancellation lacks its exact scheduled wake.");
            }
        }

        SimulationState candidate = _state.ReplaceShip(owner.InstanceId, owner with { ActiveOrder = null }) with
        {
            Scheduler = scheduler,
        };
        Commit(candidate);
        return new OrderCancellationResult(OrderCancellationOutcome.Cancelled);
    }

    // Persistence translates explicit snapshots without gaining a public mutation path into live state.
    internal SimulationState CaptureState() => _state;

    internal static GameSimulation RestoreState(SimulationState restoredState, ShipDefinitionCatalog shipCatalog) =>
        new(restoredState, shipCatalog);

    internal static SimulationAdvanceTraceResult AdvanceTo(
        SimulationState initial,
        SimulationTime target,
        ShipDefinitionCatalog shipCatalog
    )
    {
        initial.Time.AdvanceTo(target);
        long elapsed = target.Milliseconds - initial.Time.Milliseconds;
        if (elapsed % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new InvalidOperationException("Advancement boundaries must be fixed-step aligned.");
        }

        SimulationState current = initial;
        List<ScheduledConsequenceTrace> traces = [];
        long actualShipSteps = 0;
        int totalExecutions = 0;

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

            current = AdvanceSegment(current, boundary, ref actualShipSteps);
            current = ResolveDueAtCurrentBoundary(current, traces, ref totalExecutions, shipCatalog);
        }

        current = ResolveDueAtCurrentBoundary(current, traces, ref totalExecutions, shipCatalog);
        return new SimulationAdvanceTraceResult(current, new ReadOnlyValueList<ScheduledConsequenceTrace>(traces));
    }

    private static SimulationState AdvanceSegment(
        SimulationState state,
        SimulationTime boundary,
        ref long actualShipSteps
    )
    {
        long elapsed = boundary.Milliseconds - state.Time.Milliseconds;
        if (elapsed < 0 || elapsed % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new InvalidOperationException("Advancement boundaries must be monotonic and fixed-step aligned.");
        }

        // With no local motion, repair integrity is the only step-derived state and is already a pure
        // function of time, so materializing it at the boundary is fixed-step equivalent.
        if (state.Ships.All(ship => ship.StrategicState is not AtLocationState || ship.TacticalMotion == default))
        {
            return AdvanceStrategically(state, boundary);
        }

        SimulationState current = state;
        while (current.Time.Milliseconds < boundary.Milliseconds)
        {
            long attemptedShipSteps = checked(actualShipSteps + current.Ships.Length);
            if (attemptedShipSteps > ShipStepWorkBudget)
            {
                throw new InvalidOperationException(
                    $"Advancement exceeds the {ShipStepWorkBudget} actual ship-step work budget at attempted unit {attemptedShipSteps}."
                );
            }

            actualShipSteps = attemptedShipSteps;
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

    private static SimulationState AdvanceStrategically(SimulationState state, SimulationTime boundary)
    {
        var replacements = new ShipState[state.Ships.Length];
        for (int index = 0; index < state.Ships.Length; index++)
        {
            ShipState ship = state.Ships[index];
            replacements[index] = ship.SensorRepair is SensorRepairState repair
                ? ship with
                {
                    SensorIntegrity = repair.IntegrityAt(boundary),
                }
                : ship;
        }

        return state.ReplaceShips(replacements) with
        {
            Time = boundary,
        };
    }

    private static SimulationState ResolveDueAtCurrentBoundary(
        SimulationState state,
        List<ScheduledConsequenceTrace> traces,
        ref int totalExecutions,
        ShipDefinitionCatalog shipCatalog
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

                int attemptedTotal = checked(totalExecutions + 1);
                if (attemptedTotal > TotalConsequenceExecutionBudget)
                {
                    throw new InvalidOperationException(
                        $"Scheduled work exceeds the {TotalConsequenceExecutionBudget} total consequence execution budget at attempt {attemptedTotal}."
                    );
                }

                totalExecutions = attemptedTotal;
                (current, ScheduledConsequenceTrace trace) = ResolveScheduledWork(current, work, shipCatalog);
                traces.Add(trace);
            }
        }
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) ResolveScheduledWork(
        SimulationState state,
        ScheduledWork work,
        ShipDefinitionCatalog shipCatalog
    ) =>
        work.Kind switch
        {
            ScheduledWorkKind.SensorRepairCompletion => CompleteSensorRepair(state, work),
            ScheduledWorkKind.TravelArrival => CompleteTravel(state, work, shipCatalog),
            ScheduledWorkKind.OrderWake => CompleteHold(state, work),
            _ => throw new InvalidOperationException("Scheduled work kind is unsupported."),
        };

    private static (SimulationState State, ScheduledConsequenceTrace Trace) CompleteSensorRepair(
        SimulationState state,
        ScheduledWork work
    )
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        SensorRepairState? repair = ship.SensorRepair;
        if (repair is null || repair.ScheduledCompletionId != work.Id)
        {
            throw new InvalidOperationException("Sensor completion lacks matching active repair.");
        }

        SimulationState candidate = state.ReplaceShip(
            ship.InstanceId,
            ship with
            {
                SensorIntegrity = repair.TargetIntegrity,
                SensorRepair = null,
            }
        );
        return (
            candidate,
            Trace(
                state,
                work,
                null,
                ScheduledConsequenceRule.SensorRepairCompletion,
                ScheduledConsequenceAction.CompleteSensorRepair,
                true
            )
        );
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) CompleteTravel(
        SimulationState state,
        ScheduledWork work,
        ShipDefinitionCatalog shipCatalog
    )
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        if (ship.StrategicState is not TravelingState traveling || traveling.Travel.ScheduledArrivalId != work.Id)
        {
            throw new InvalidOperationException("Arrival lacks matching active travel.");
        }

        ShipOrder? order = ship.ActiveOrder;
        SimulationState arrived = state.ReplaceShip(
            ship.InstanceId,
            ship with
            {
                StrategicState = new AtLocationState(traveling.Travel.Destination),
                TacticalPosition = ArrivalPosition,
                TacticalMotion = default,
                ActiveOrder = order is TravelToOrder ? null : order,
            }
        );

        if (order is PatrolRouteOrder patrol)
        {
            return ContinuePatrol(state, arrived, ship, patrol, work, shipCatalog);
        }

        return (
            arrived,
            Trace(
                state,
                work,
                order,
                order is TravelToOrder
                    ? ScheduledConsequenceRule.TravelToArrival
                    : ScheduledConsequenceRule.OrderlessTravelArrival,
                order is TravelToOrder
                    ? ScheduledConsequenceAction.CompleteTravelTo
                    : ScheduledConsequenceAction.FinishTravel,
                true
            )
        );
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) ContinuePatrol(
        SimulationState state,
        SimulationState arrived,
        ShipState ship,
        PatrolRouteOrder patrol,
        ScheduledWork work,
        ShipDefinitionCatalog shipCatalog
    )
    {
        int followingIndex = (patrol.NextWaypointIndex + 1) % patrol.Waypoints.Length;
        var continuedOrder = new PatrolRouteOrder(patrol.Id, patrol.Waypoints, followingIndex);
        arrived = arrived.ReplaceShip(
            ship.InstanceId,
            arrived.GetRequiredShip(ship.InstanceId) with
            {
                ActiveOrder = continuedOrder,
            }
        );
        ShipTravelApplicationResult application = ApplyShipTravel(
            arrived,
            new ShipTravelCommand(ship.InstanceId, continuedOrder.Waypoints[followingIndex]),
            shipCatalog
        );
        if (application.Outcome != TravelOutcome.Accepted)
        {
            throw new InvalidOperationException("A validated patrol could not begin its declared next leg.");
        }

        return (
            application.CandidateState,
            Trace(
                state,
                work,
                patrol,
                ScheduledConsequenceRule.PatrolWaypointArrival,
                ScheduledConsequenceAction.ContinuePatrol,
                false
            )
        );
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) CompleteHold(
        SimulationState state,
        ScheduledWork work
    )
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        if (
            ship.ActiveOrder is not HoldUntilOrder hold
            || hold.ScheduledWakeId != work.Id
            || hold.Until != work.DueTime
        )
        {
            throw new InvalidOperationException("Order wake lacks its exact active HoldUntil order.");
        }

        SimulationState candidate = state.ReplaceShip(ship.InstanceId, ship with { ActiveOrder = null });
        return (
            candidate,
            Trace(
                state,
                work,
                hold,
                ScheduledConsequenceRule.HoldUntilWake,
                ScheduledConsequenceAction.CompleteHold,
                true
            )
        );
    }

    private static ScheduledConsequenceTrace Trace(
        SimulationState state,
        ScheduledWork work,
        ShipOrder? order,
        ScheduledConsequenceRule rule,
        ScheduledConsequenceAction action,
        bool completed
    ) => new(work.Id, work.TargetShipId, work.Kind, state.Time, order?.Id, order?.Kind, rule, action, completed, false);

    private static ReadOnlyValueList<ScheduledWorkKind> PlayerVisibleKinds(
        IReadOnlyList<ScheduledConsequenceTrace> traces,
        ShipInstanceId playerShipId
    ) =>
        new ReadOnlyValueList<ScheduledWorkKind>(
            traces.Where(trace => trace.TargetShipId == playerShipId).Select(trace => trace.WorkKind)
        );

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
