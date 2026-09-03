using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Owns authoritative mutation and immutable definition content for the simulation.</summary>
public sealed class GameSimulation
{
    // These budgets bound one candidate advancement operation. A rejected candidate never consumes
    // capacity from a later request or commits partial time, ship, or scheduler state.
    private const int SameBoundaryExecutionBudget = 1024;
    private const int TotalConsequenceExecutionBudget = 10_000;
    private const int ContactMaterializationBoundaryBudget = 10_000;
    private const long ShipStepWorkBudget = 1_000_000;
    private const long SensorContactLossMilliseconds = 5_000;

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

    /// <summary>Gets the latest autonomous contact explanation produced during this live session.</summary>
    public ShipContactDecisionExplanation? LastContactDecisionExplanation { get; private set; }

    /// <summary>Validates and schedules persistent strategic travel for the player ship.</summary>
    public TravelRequestResult RequestTravel(TravelIntent intent)
    {
        var command = new ShipTravelCommand(_state.PlayerShipId, intent.Destination);
        ShipTravelApplicationResult application = ApplyShipTravel(_state, command);
        IReadOnlyList<PlayerAdvanceEvent> resolvedEvents = new ReadOnlyValueList<PlayerAdvanceEvent>([]);
        if (application.Outcome == TravelOutcome.Accepted)
        {
            List<PlayerAdvanceEvent> playerEvents = [];
            SimulationState candidate = ObserveAllShips(application.CandidateState, _shipCatalog, playerEvents);
            Commit(candidate);
            resolvedEvents = new ReadOnlyValueList<PlayerAdvanceEvent>(playerEvents);
        }

        return new TravelRequestResult(application.Outcome, resolvedEvents);
    }

    internal static ShipTravelApplicationResult ApplyShipTravel(SimulationState state, ShipTravelCommand command)
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
        // Same-time batches dequeue all due work before resolving each owner, so other ships may
        // temporarily reference work absent from the scheduler. Aggregate validation belongs after
        // the batch or at the public command commit boundary, never inside this local command path.
        return new ShipTravelApplicationResult(TravelOutcome.Accepted, candidate);
    }

    /// <summary>Validates and changes only the player ship's local tactical motion.</summary>
    public SetTacticalCourseResult SetTacticalCourse(SetTacticalCourseIntent intent)
    {
        TacticalCourseApplicationResult application = ApplyTacticalCourse(
            _state,
            _shipCatalog,
            new TargetableTacticalCourseCommand(_state.PlayerShipId, intent.Heading, intent.Speed)
        );
        if (application.Outcome == SetTacticalCourseOutcome.Accepted)
        {
            Commit(application.CandidateState);
        }

        return new SetTacticalCourseResult(application.Outcome);
    }

    /// <summary>Validates and sends the player's identity to one identified current contact.</summary>
    public HailResult RequestHail(SensorContactId contactId)
    {
        ShipState player = _state.GetRequiredShip(_state.PlayerShipId);
        SensorContactTrack? contact = player.SensorKnowledge.Contacts.FirstOrDefault(candidate =>
            candidate.Id == contactId
        );
        if (contact is null)
        {
            return new HailResult(HailOutcome.ContactNotFound);
        }

        if (contact.Status != SensorContactStatus.Current)
        {
            return new HailResult(HailOutcome.ContactNotCurrent);
        }

        if (contact.Identification != SensorContactIdentification.Identified)
        {
            return new HailResult(HailOutcome.ContactNotIdentified);
        }

        return RespondToHail(player, contact);
    }

    private HailResult RespondToHail(ShipState player, SensorContactTrack contact)
    {
        ShipState target = _state.GetRequiredShip(contact.TargetShipId);
        SensorContactTrack? reciprocal = target.SensorKnowledge.Contacts.FirstOrDefault(candidate =>
            candidate.TargetShipId == player.InstanceId && candidate.Status == SensorContactStatus.Current
        );
        if (target.AutonomousState.ContactPosture != ShipContactPosture.CautiousContact || reciprocal is null)
        {
            return new HailResult(HailOutcome.NoResponse);
        }

        ShipDefinition playerDefinition = _shipCatalog.GetRequired(player.DefinitionId);
        var incomingHail = new IncomingHailFact(
            reciprocal.Id,
            player.VesselDisplayName,
            playerDefinition.DesignDisplayName
        );
        SimulationScheduler scheduler = _state.Scheduler;
        ActiveSensorScanState? activeScan = target.SensorKnowledge.ActiveScan;
        if (activeScan?.TargetContactId == reciprocal.Id)
        {
            (scheduler, bool removed) = scheduler.Cancel(activeScan.ScheduledCompletionId);
            if (!removed)
            {
                throw new InvalidOperationException("A target-side scan lacks its exact scheduled completion.");
            }

            activeScan = null;
        }

        SensorContactTrack identifiedPlayer = reciprocal with
        {
            Identification = SensorContactIdentification.Identified,
            KnownVesselDisplayName = incomingHail.TransmittedVesselDisplayName,
            KnownDesignDisplayName = incomingHail.TransmittedDesignDisplayName,
        };
        SensorKnowledge updatedKnowledge = new(
            target.SensorKnowledge.NextContactId,
            target.SensorKnowledge.Contacts.Replace(reciprocal, identifiedPlayer),
            activeScan
        );
        ShipState informedTarget = target with { SensorKnowledge = updatedKnowledge };
        SimulationState informedState = _state.ReplaceShip(target.InstanceId, informedTarget) with
        {
            Scheduler = scheduler,
        };
        ShipContactDecisionExplanation decision = DecideContact(
            informedState,
            informedTarget,
            _shipCatalog,
            incomingHail
        );
        SimulationState candidate = ApplyContactDecision(informedState, informedTarget, _shipCatalog, decision);
        Commit(candidate);
        LastContactDecisionExplanation = decision;
        return new HailResult(HailOutcome.Acknowledged);
    }

    internal static TacticalCourseApplicationResult ApplyTacticalCourse(
        SimulationState state,
        ShipDefinitionCatalog shipCatalog,
        TargetableTacticalCourseCommand command
    )
    {
        ShipState targetShip = state.GetRequiredShip(command.TargetShipId);
        if (targetShip.StrategicState is TravelingState)
        {
            return new TacticalCourseApplicationResult(SetTacticalCourseOutcome.UnavailableWhileTraveling, state);
        }

        ShipDefinition definition = shipCatalog.GetRequired(targetShip.DefinitionId);
        if (command.Speed.Value > definition.MaximumTacticalSpeed.Value)
        {
            return new TacticalCourseApplicationResult(SetTacticalCourseOutcome.SpeedExceedsMaximum, state);
        }

        SimulationState candidate = state.ReplaceShip(
            targetShip.InstanceId,
            targetShip with
            {
                TacticalMotion = new TacticalMotion(command.Heading, command.Speed),
            }
        );
        return new TacticalCourseApplicationResult(SetTacticalCourseOutcome.Accepted, candidate);
    }

    /// <summary>Validates and schedules an active scan of one player-local contact.</summary>
    public ActiveSensorScanResult RequestActiveSensorScan(SensorContactId contactId)
    {
        ShipState observer = _state.GetRequiredShip(_state.PlayerShipId);
        SensorContactTrack? contact = observer.SensorKnowledge.Contacts.FirstOrDefault(candidate =>
            candidate.Id == contactId
        );
        if (contact is null)
        {
            return new ActiveSensorScanResult(ActiveSensorScanOutcome.ContactNotFound);
        }

        if (contact.Status != SensorContactStatus.Current)
        {
            return new ActiveSensorScanResult(ActiveSensorScanOutcome.ContactNotCurrent);
        }

        if (contact.Identification == SensorContactIdentification.Identified)
        {
            return new ActiveSensorScanResult(ActiveSensorScanOutcome.AlreadyIdentified);
        }

        if (observer.SensorIntegrity.Value == 0 || observer.StrategicState is not AtLocationState)
        {
            return new ActiveSensorScanResult(ActiveSensorScanOutcome.SensorsUnavailable);
        }

        if (observer.SensorKnowledge.ActiveScan is not null)
        {
            return new ActiveSensorScanResult(ActiveSensorScanOutcome.ScanAlreadyActive);
        }

        ShipDefinition definition = _shipCatalog.GetRequired(observer.DefinitionId);
        SimulationTime completion = _state.Time.AdvanceBy(definition.ActiveScanDuration);
        (SimulationScheduler scheduler, ScheduledWork work) = _state.Scheduler.Schedule(
            completion,
            observer.InstanceId,
            ScheduledWorkKind.ActiveSensorScanCompletion
        );
        SensorKnowledge knowledge = observer.SensorKnowledge with
        {
            ActiveScan = new ActiveSensorScanState(contactId, _state.Time, completion, work.Id),
        };
        SimulationState candidate = _state.ReplaceShip(
            observer.InstanceId,
            observer with
            {
                SensorKnowledge = knowledge,
            }
        ) with
        {
            Scheduler = scheduler,
        };
        Commit(candidate);
        return new ActiveSensorScanResult(ActiveSensorScanOutcome.Accepted);
    }

    /// <summary>Advances by explicit one-hundred-millisecond steps and returns resolved consequences.</summary>
    public SimulationAdvanceResult AdvanceFixedSteps(int stepCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stepCount);
        int milliseconds = checked(stepCount * checked((int)SimulationFixedStep.Duration.Milliseconds));
        SimulationTime target = _state.Time.AdvanceBy(new SimulationDuration(milliseconds));
        SimulationAdvanceTraceResult advance = AdvanceTo(_state, target, _shipCatalog);
        Commit(advance.State);
        RememberLatestContactDecision(advance.Traces);
        return new SimulationAdvanceResult(_state.Time, advance.PlayerEvents, Project(_state));
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
                AdvanceUntilOutcome.NoPlayerEvent,
                _state.Time,
                new ReadOnlyValueList<PlayerAdvanceEvent>([]),
                unchanged
            );
        }

        SimulationTime boundary = nextPlayerWork.Value.DueTime;
        SimulationAdvanceTraceResult advance = AdvanceTo(_state, boundary, _shipCatalog, true);
        Commit(advance.State);
        RememberLatestContactDecision(advance.Traces);
        return new AdvanceUntilResult(
            AdvanceUntilOutcome.PlayerEventResolved,
            _state.Time,
            advance.PlayerEvents,
            Project(_state)
        );
    }

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

    internal void BootstrapHiddenCautiousContactObservation(ShipInstanceId observerId)
    {
        ShipState observer = _state.GetRequiredShip(observerId);
        if (observerId == _state.PlayerShipId || observer.StrategicState is not AtLocationState)
        {
            throw new InvalidOperationException("A hidden cautious-contact observer must be a local non-player ship.");
        }

        if (
            _state.Ships.Any(ship =>
                ship.SensorKnowledge.NextContactId != SensorKnowledge.Empty.NextContactId
                || !ship.SensorKnowledge.Contacts.IsEmpty
                || ship.SensorKnowledge.ActiveScan is not null
                || ship.AutonomousState.ContactPosture is not null
                || ship.AutonomousState.PendingContactDecisionWake is not null
            )
        )
        {
            throw new InvalidOperationException(
                "Initial hidden observation requires untouched knowledge and autonomy."
            );
        }

        SimulationState prepared = _state.ReplaceShip(
            observerId,
            observer with
            {
                AutonomousState = new ShipAutonomousState(ShipContactPosture.CautiousContact),
            }
        );
        List<PlayerAdvanceEvent> playerEvents = [];
        SimulationState candidate = ObserveAllShips(prepared, _shipCatalog, playerEvents);
        if (playerEvents.Count != 0)
        {
            throw new InvalidOperationException("Hidden bootstrap observation cannot reveal a player contact.");
        }

        Commit(candidate);
    }

    internal static SimulationAdvanceTraceResult AdvanceTo(
        SimulationState initial,
        SimulationTime target,
        ShipDefinitionCatalog shipCatalog,
        bool stopAfterPlayerEvent = false
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
        List<PlayerAdvanceEvent> playerEvents = [];
        long actualShipSteps = 0;
        int contactMaterializationBoundaries = 0;
        int totalExecutions = 0;

        while (current.Time.Milliseconds < target.Milliseconds)
        {
            bool materializeContacts = RequiresContactMaterialization(current);
            SimulationTime boundary = NextAdvancementBoundary(current, target, materializeContacts);
            if (materializeContacts)
            {
                ChargeContactMaterializationBoundary(ref contactMaterializationBoundaries);
            }

            current = AdvanceSegment(current, boundary, ref actualShipSteps);
            int priorPlayerEventCount = playerEvents.Count;
            current = ResolveCurrentBoundary(current, shipCatalog, traces, playerEvents, ref totalExecutions);
            if (stopAfterPlayerEvent && playerEvents.Count != priorPlayerEventCount)
            {
                break;
            }
        }

        if (current.Time == target)
        {
            current = ResolveCurrentBoundary(
                current,
                shipCatalog,
                traces,
                playerEvents,
                ref totalExecutions,
                observe: false
            );
        }
        current.Validate(shipCatalog);
        return new SimulationAdvanceTraceResult(
            current,
            new ReadOnlyValueList<ScheduledConsequenceTrace>(traces),
            new ReadOnlyValueList<PlayerAdvanceEvent>(playerEvents)
        );
    }

    private static SimulationTime NextAdvancementBoundary(
        SimulationState state,
        SimulationTime target,
        bool materializeContacts
    )
    {
        SimulationTime boundary = target;
        if (
            !state.Scheduler.OutstandingWork.IsDefaultOrEmpty
            && state.Scheduler.OutstandingWork[0].DueTime.Milliseconds <= target.Milliseconds
        )
        {
            boundary = state.Scheduler.OutstandingWork[0].DueTime;
        }

        if (materializeContacts)
        {
            SimulationTime nextStep = state.Time.AdvanceBy(SimulationFixedStep.Duration);
            if (nextStep.Milliseconds < boundary.Milliseconds)
            {
                boundary = nextStep;
            }
        }

        return boundary;
    }

    private static void ChargeContactMaterializationBoundary(ref int materializationBoundaries)
    {
        int attempted = checked(materializationBoundaries + 1);
        if (attempted > ContactMaterializationBoundaryBudget)
        {
            throw new InvalidOperationException(
                $"Advancement exceeds the {ContactMaterializationBoundaryBudget} contact-materialization boundary budget at attempt {attempted}."
            );
        }

        materializationBoundaries = attempted;
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

        int[] movingShipIndexes =
        [
            .. state
                .Ships.Select((ship, index) => (ship, index))
                .Where(pair => pair.ship.StrategicState is AtLocationState && pair.ship.TacticalMotion.Speed.Value != 0)
                .Select(pair => pair.index),
        ];
        if (movingShipIndexes.Length == 0)
        {
            return AdvanceStrategically(state, boundary);
        }

        ShipState[] movingShips = [.. movingShipIndexes.Select(index => state.Ships[index])];
        SimulationTime currentTime = state.Time;
        while (currentTime.Milliseconds < boundary.Milliseconds)
        {
            long attemptedShipSteps = checked(actualShipSteps + movingShips.Length);
            if (attemptedShipSteps > ShipStepWorkBudget)
            {
                throw new InvalidOperationException(
                    $"Advancement exceeds the {ShipStepWorkBudget} actual ship-step work budget at attempted unit {attemptedShipSteps}."
                );
            }

            actualShipSteps = attemptedShipSteps;
            currentTime = currentTime.AdvanceBy(SimulationFixedStep.Duration);
            for (int index = 0; index < movingShips.Length; index++)
            {
                ShipState ship = movingShips[index];
                movingShips[index] = ship with
                {
                    TacticalPosition = ship.TacticalPosition.Advance(
                        ship.TacticalMotion,
                        SimulationFixedStep.Duration.Milliseconds / 1000.0
                    ),
                };
            }
        }

        ShipState[] replacements = [.. state.Ships];
        for (int index = 0; index < movingShipIndexes.Length; index++)
        {
            replacements[movingShipIndexes[index]] = movingShips[index];
        }

        // Repair integrity is an analytical function of time, so one boundary materialization preserves
        // its fixed-step result without processing stationary or strategically traveling ships every tick.
        return AdvanceStrategically(state.ReplaceShips(replacements), boundary);
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

    private static bool RequiresContactMaterialization(SimulationState state)
    {
        foreach (ShipState observer in state.Ships)
        {
            if (observer.StrategicState is not AtLocationState observerLocation)
            {
                continue;
            }

            bool hasLocalTarget = state.Ships.Any(target =>
                target.InstanceId != observer.InstanceId
                && target.StrategicState is AtLocationState targetLocation
                && targetLocation.LocationId == observerLocation.LocationId
            );
            if (hasLocalTarget && (observer.TacticalMotion.Speed.Value != 0 || observer.SensorRepair is not null))
            {
                return true;
            }
        }

        return false;
    }

    private static SimulationState ObserveAllShips(
        SimulationState state,
        ShipDefinitionCatalog shipCatalog,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        ShipState[] truth = [.. state.Ships];
        SimulationState current = state;
        foreach (ShipState truthObserver in truth)
        {
            ShipState observer = current.GetRequiredShip(truthObserver.InstanceId);
            ShipDefinition observerDefinition = shipCatalog.GetRequired(observer.DefinitionId);
            double effectiveRange = observerDefinition.PassiveSensorRange.Value * observer.SensorIntegrity.Value;
            var observableTargets = new HashSet<ShipInstanceId>();
            if (observer.StrategicState is AtLocationState observerLocation)
            {
                IEnumerable<ShipState> targets =
                    observer.SensorKnowledge.Contacts.Length == SensorKnowledge.MaximumContactsPerObserver
                        ? observer.SensorKnowledge.Contacts.Select(contact =>
                            truth[FindShipIndex(truth, contact.TargetShipId)]
                        )
                        : truth;
                foreach (ShipState target in targets)
                {
                    if (
                        effectiveRange > 0
                        && target.InstanceId != observer.InstanceId
                        && target.StrategicState is AtLocationState targetLocation
                        && targetLocation.LocationId == observerLocation.LocationId
                        && IsWithinInclusiveRange(
                            Distance(observer.TacticalPosition, target.TacticalPosition),
                            effectiveRange
                        )
                    )
                    {
                        observableTargets.Add(target.InstanceId);
                    }
                }
            }

            (current, observer) = ReconcileObserverContacts(current, observer, truth, observableTargets, playerEvents);
            current = ScheduleContactDecisionWake(current, observer, truthObserver.SensorKnowledge, observableTargets);
        }

        return current;
    }

    private static int FindShipIndex(IReadOnlyList<ShipState> ships, ShipInstanceId shipId)
    {
        int low = 0;
        int high = ships.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            long value = ships[middle].InstanceId.Value;
            if (value == shipId.Value)
            {
                return middle;
            }

            if (value < shipId.Value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        throw new InvalidOperationException("Sensor knowledge references a missing truth target.");
    }

    private static (SimulationState State, ShipState Observer) ReconcileObserverContacts(
        SimulationState state,
        ShipState observer,
        IReadOnlyList<ShipState> truth,
        HashSet<ShipInstanceId> observableTargets,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        SimulationState current = state;
        SensorKnowledge knowledge = observer.SensorKnowledge;
        (current, List<SensorContactTrack> contacts) = ReconcileRetainedContacts(
            current,
            observer,
            truth,
            observableTargets,
            playerEvents
        );

        long nextContactId = AdmitNewContacts(
            observer,
            truth,
            observableTargets,
            contacts,
            knowledge.NextContactId,
            current.Time,
            current.PlayerShipId,
            playerEvents
        );
        (current, ActiveSensorScanState? activeScan) = InterruptInvalidScan(
            current,
            observer,
            contacts,
            knowledge.ActiveScan,
            playerEvents
        );

        SensorKnowledge updatedKnowledge = new(nextContactId, contacts, activeScan);
        ShipState updatedObserver = observer with { SensorKnowledge = updatedKnowledge };
        return (current.ReplaceShip(observer.InstanceId, updatedObserver), updatedObserver);
    }

    private static (SimulationState State, List<SensorContactTrack> Contacts) ReconcileRetainedContacts(
        SimulationState state,
        ShipState observer,
        IReadOnlyList<ShipState> truth,
        HashSet<ShipInstanceId> observableTargets,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        SimulationState current = state;
        List<SensorContactTrack> contacts = [];
        foreach (
            SensorContactTrack contact in observer.SensorKnowledge.Contacts.OrderBy(contact =>
                contact.TargetShipId.Value
            )
        )
        {
            ShipState target = truth[FindShipIndex(truth, contact.TargetShipId)];
            if (observableTargets.Contains(contact.TargetShipId))
            {
                (current, SensorContactTrack refreshed) = RefreshObservedContact(
                    current,
                    observer.InstanceId,
                    target,
                    contact,
                    playerEvents
                );
                contacts.Add(refreshed);
                continue;
            }

            if (contact.Status == SensorContactStatus.Current)
            {
                (current, SensorContactTrack stale) = MarkContactStale(
                    current,
                    observer.InstanceId,
                    contact,
                    playerEvents
                );
                contacts.Add(stale);
                continue;
            }

            contacts.Add(contact);
        }

        return (current, contacts);
    }

    private static (SimulationState State, SensorContactTrack Contact) RefreshObservedContact(
        SimulationState state,
        ShipInstanceId observerId,
        ShipState target,
        SensorContactTrack contact,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        if (contact.LossWorkId is { } lossWorkId)
        {
            (SimulationScheduler scheduler, _) = state.Scheduler.Cancel(lossWorkId);
            state = state with { Scheduler = scheduler };
        }

        if (contact.Status != SensorContactStatus.Current)
        {
            AddContactEvent(
                observerId,
                state.PlayerShipId,
                PlayerAdvanceEventKind.SensorContactReacquired,
                contact.Id,
                playerEvents
            );
        }

        return (
            state,
            contact with
            {
                LastObservedPosition = target.TacticalPosition,
                LastObservedAt = state.Time,
                Status = SensorContactStatus.Current,
                LossWorkId = null,
                LossDueTime = null,
            }
        );
    }

    private static (SimulationState State, SensorContactTrack Contact) MarkContactStale(
        SimulationState state,
        ShipInstanceId observerId,
        SensorContactTrack contact,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        SimulationTime lossDueTime = state.Time.AdvanceBy(new SimulationDuration(SensorContactLossMilliseconds));
        (SimulationScheduler scheduler, ScheduledWork lossWork) = state.Scheduler.Schedule(
            lossDueTime,
            observerId,
            ScheduledWorkKind.SensorContactLoss
        );
        state = state with { Scheduler = scheduler };
        AddContactEvent(
            observerId,
            state.PlayerShipId,
            PlayerAdvanceEventKind.SensorContactStale,
            contact.Id,
            playerEvents
        );
        return (
            state,
            contact with
            {
                Status = SensorContactStatus.Stale,
                LossWorkId = lossWork.Id,
                LossDueTime = lossDueTime,
            }
        );
    }

    private static long AdmitNewContacts(
        ShipState observer,
        IReadOnlyList<ShipState> truth,
        HashSet<ShipInstanceId> observableTargets,
        List<SensorContactTrack> contacts,
        long nextContactId,
        SimulationTime observationTime,
        ShipInstanceId playerShipId,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        HashSet<ShipInstanceId> retainedTargets = [.. contacts.Select(contact => contact.TargetShipId)];
        foreach (
            ShipState target in truth
                .Where(target =>
                    observableTargets.Contains(target.InstanceId) && !retainedTargets.Contains(target.InstanceId)
                )
                .OrderBy(target => target.InstanceId.Value)
        )
        {
            if (contacts.Count == SensorKnowledge.MaximumContactsPerObserver)
            {
                break;
            }

            var contactId = new SensorContactId(nextContactId);
            nextContactId = checked(nextContactId + 1);
            contacts.Add(
                new SensorContactTrack(
                    contactId,
                    target.InstanceId,
                    target.TacticalPosition,
                    observationTime,
                    SensorContactStatus.Current,
                    SensorContactIdentification.Detected
                )
            );
            AddContactEvent(
                observer.InstanceId,
                playerShipId,
                PlayerAdvanceEventKind.SensorContactDetected,
                contactId,
                playerEvents
            );
        }

        return nextContactId;
    }

    private static (SimulationState State, ActiveSensorScanState? ActiveScan) InterruptInvalidScan(
        SimulationState state,
        ShipState observer,
        List<SensorContactTrack> contacts,
        ActiveSensorScanState? activeScan,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        if (
            activeScan is not null
            && contacts.First(contact => contact.Id == activeScan.TargetContactId).Status != SensorContactStatus.Current
        )
        {
            (SimulationScheduler scheduler, _) = state.Scheduler.Cancel(activeScan.ScheduledCompletionId);
            state = state with { Scheduler = scheduler };
            AddContactEvent(
                observer.InstanceId,
                state.PlayerShipId,
                PlayerAdvanceEventKind.ActiveSensorScanInterrupted,
                activeScan.TargetContactId,
                playerEvents
            );
            activeScan = null;
        }

        return (state, activeScan);
    }

    private static SimulationState ScheduleContactDecisionWake(
        SimulationState state,
        ShipState observer,
        SensorKnowledge priorKnowledge,
        HashSet<ShipInstanceId> observableTargets
    )
    {
        if (
            observer.InstanceId == state.PlayerShipId
            || observer.AutonomousState.ContactPosture != ShipContactPosture.CautiousContact
            || observer.AutonomousState.PendingContactDecisionWake is not null
        )
        {
            return state;
        }

        bool gainedCurrentContact = observer.SensorKnowledge.Contacts.Any(contact =>
            observableTargets.Contains(contact.TargetShipId)
            && priorKnowledge.Contacts.FirstOrDefault(prior => prior.TargetShipId == contact.TargetShipId)?.Status
                != SensorContactStatus.Current
        );
        if (!gainedCurrentContact)
        {
            return state;
        }

        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            state.Time,
            observer.InstanceId,
            ScheduledWorkKind.ShipContactDecisionWake
        );
        ShipState updated = observer with
        {
            AutonomousState = observer.AutonomousState with
            {
                PendingContactDecisionWake = new ShipContactDecisionWake(work.Id, work.DueTime),
            },
        };
        return state.ReplaceShip(observer.InstanceId, updated) with { Scheduler = scheduler };
    }

    private static double Distance(TacticalPosition left, TacticalPosition right)
    {
        double x = Math.Abs(left.XKilometers - right.XKilometers);
        double y = Math.Abs(left.YKilometers - right.YKilometers);
        if (x < y)
        {
            (x, y) = (y, x);
        }

        if (double.IsPositiveInfinity(x) || x == 0)
        {
            return x;
        }

        double ratio = y / x;
        return x * Math.Sqrt(1 + (ratio * ratio));
    }

    private static bool IsWithinInclusiveRange(double distance, double range)
    {
        double tolerance = Math.Max(1, range) * 1e-12;
        return distance <= range + tolerance;
    }

    private static void AddContactEvent(
        ShipInstanceId observerId,
        ShipInstanceId playerShipId,
        PlayerAdvanceEventKind kind,
        SensorContactId contactId,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        if (observerId == playerShipId)
        {
            playerEvents.Add(new PlayerAdvanceEvent(kind, contactId));
        }
    }

    private static SimulationState ResolveCurrentBoundary(
        SimulationState state,
        ShipDefinitionCatalog shipCatalog,
        List<ScheduledConsequenceTrace> traces,
        List<PlayerAdvanceEvent> playerEvents,
        ref int totalExecutions,
        bool observe = true
    )
    {
        SimulationState current = state;
        if (observe)
        {
            current = ObserveAllShips(current, shipCatalog, playerEvents);
        }

        int executions = 0;
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
                ChargeScheduledExecution(ref executions, ref totalExecutions);
                (current, ScheduledConsequenceTrace trace) = ResolveScheduledWork(current, work, shipCatalog);
                traces.Add(trace);
                AddPlayerScheduledEvent(trace, current.PlayerShipId, playerEvents);
                current = ObserveAllShips(current, shipCatalog, playerEvents);
            }
        }
    }

    private static void ChargeScheduledExecution(ref int boundaryExecutions, ref int totalExecutions)
    {
        boundaryExecutions = checked(boundaryExecutions + 1);
        if (boundaryExecutions > SameBoundaryExecutionBudget)
        {
            throw new InvalidOperationException("Scheduled work exceeded the finite same-boundary execution budget.");
        }

        int attemptedTotal = checked(totalExecutions + 1);
        if (attemptedTotal > TotalConsequenceExecutionBudget)
        {
            throw new InvalidOperationException(
                $"Scheduled work exceeds the {TotalConsequenceExecutionBudget} total consequence execution budget at attempt {attemptedTotal}."
            );
        }

        totalExecutions = attemptedTotal;
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) ResolveScheduledWork(
        SimulationState state,
        ScheduledWork work,
        ShipDefinitionCatalog shipCatalog
    ) =>
        work.Kind switch
        {
            ScheduledWorkKind.SensorRepairCompletion => CompleteSensorRepair(state, work),
            ScheduledWorkKind.TravelArrival => CompleteTravel(state, work),
            ScheduledWorkKind.OrderWake => CompleteHold(state, work),
            ScheduledWorkKind.SensorContactLoss => LoseSensorContact(state, work),
            ScheduledWorkKind.ActiveSensorScanCompletion => CompleteActiveSensorScan(state, work, shipCatalog),
            ScheduledWorkKind.ShipContactDecisionWake => CompleteShipContactDecisionWake(state, work, shipCatalog),
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
        ScheduledWork work
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
            return ContinuePatrol(state, arrived, ship, patrol, work);
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
        ScheduledWork work
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
            new ShipTravelCommand(ship.InstanceId, continuedOrder.Waypoints[followingIndex])
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

    private static (SimulationState State, ScheduledConsequenceTrace Trace) LoseSensorContact(
        SimulationState state,
        ScheduledWork work
    )
    {
        ShipState observer = state.GetRequiredShip(work.TargetShipId);
        SensorContactTrack? contact = observer.SensorKnowledge.Contacts.FirstOrDefault(candidate =>
            candidate.Status == SensorContactStatus.Stale
            && candidate.LossWorkId == work.Id
            && candidate.LossDueTime == work.DueTime
        );
        if (contact is null)
        {
            return (state, InvalidatedContactTrace(state, work, ScheduledConsequenceRule.SensorContactLoss));
        }

        SensorKnowledge knowledge = new(
            observer.SensorKnowledge.NextContactId,
            observer.SensorKnowledge.Contacts.Replace(
                contact,
                contact with
                {
                    Status = SensorContactStatus.Lost,
                    LossWorkId = null,
                    LossDueTime = null,
                }
            ),
            observer.SensorKnowledge.ActiveScan
        );
        SimulationState candidate = state.ReplaceShip(
            observer.InstanceId,
            observer with
            {
                SensorKnowledge = knowledge,
            }
        );
        return (
            candidate,
            Trace(
                state,
                work,
                null,
                ScheduledConsequenceRule.SensorContactLoss,
                ScheduledConsequenceAction.LoseSensorContact,
                true,
                contact.Id
            )
        );
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) CompleteActiveSensorScan(
        SimulationState state,
        ScheduledWork work,
        ShipDefinitionCatalog shipCatalog
    )
    {
        ShipState observer = state.GetRequiredShip(work.TargetShipId);
        ActiveSensorScanState? scan = observer.SensorKnowledge.ActiveScan;
        SensorContactTrack? contact = scan is null
            ? null
            : observer.SensorKnowledge.Contacts.FirstOrDefault(candidate =>
                candidate.Id == scan.TargetContactId && candidate.Status == SensorContactStatus.Current
            );
        if (scan is null || scan.ScheduledCompletionId != work.Id || contact is null)
        {
            return (state, InvalidatedContactTrace(state, work, ScheduledConsequenceRule.ActiveSensorScanCompletion));
        }

        ShipState target = state.GetRequiredShip(contact.TargetShipId);
        ShipDefinition targetDefinition = shipCatalog.GetRequired(target.DefinitionId);
        SensorContactTrack identified = contact with
        {
            Identification = SensorContactIdentification.Identified,
            KnownVesselDisplayName = target.VesselDisplayName,
            KnownDesignDisplayName = targetDefinition.DesignDisplayName,
        };
        SensorKnowledge knowledge = new(
            observer.SensorKnowledge.NextContactId,
            observer.SensorKnowledge.Contacts.Replace(contact, identified)
        );
        SimulationState candidate = state.ReplaceShip(
            observer.InstanceId,
            observer with
            {
                SensorKnowledge = knowledge,
            }
        );
        return (
            candidate,
            Trace(
                state,
                work,
                null,
                ScheduledConsequenceRule.ActiveSensorScanCompletion,
                ScheduledConsequenceAction.CompleteActiveSensorScan,
                true,
                contact.Id
            )
        );
    }

    private static (SimulationState State, ScheduledConsequenceTrace Trace) CompleteShipContactDecisionWake(
        SimulationState state,
        ScheduledWork work,
        ShipDefinitionCatalog shipCatalog
    )
    {
        ShipState ship = state.GetRequiredShip(work.TargetShipId);
        ShipContactDecisionWake? wake = ship.AutonomousState.PendingContactDecisionWake;
        if (wake is null || wake.ScheduledWorkId != work.Id || wake.DueTime != work.DueTime)
        {
            return (state, InvalidatedContactTrace(state, work, ScheduledConsequenceRule.ShipContactDecisionWake));
        }

        SimulationState cleared = state.ReplaceShip(
            ship.InstanceId,
            ship with
            {
                AutonomousState = ship.AutonomousState with { PendingContactDecisionWake = null },
            }
        );
        ShipState clearedShip = cleared.GetRequiredShip(ship.InstanceId);
        ShipContactDecisionExplanation decision = DecideContact(cleared, clearedShip, shipCatalog);
        SimulationState candidate = ApplyContactDecision(cleared, clearedShip, shipCatalog, decision);
        return (
            candidate,
            Trace(
                state,
                work,
                null,
                ScheduledConsequenceRule.ShipContactDecisionWake,
                ScheduledConsequenceAction.WakeShipContactDecision,
                true,
                contactDecision: decision
            )
        );
    }

    private static ShipContactDecisionExplanation DecideContact(
        SimulationState state,
        ShipState ship,
        ShipDefinitionCatalog shipCatalog,
        IncomingHailFact? incomingHail = null
    )
    {
        ShipDefinition definition = shipCatalog.GetRequired(ship.DefinitionId);
        var facts = new ShipContactDecisionFacts(
            ship.TacticalPosition,
            ship.TacticalMotion,
            ship.StrategicState is AtLocationState,
            definition.MaximumTacticalSpeed,
            ship.SensorKnowledge.Contacts.Select(contact => contact.ToActorSafeSnapshot()),
            incomingHail
        );
        var input = new ShipContactDecisionInput(
            ship.InstanceId,
            state.Time,
            ShipContactDecisionGoal.RespondCautiously,
            ShipContactPosture.CautiousContact,
            facts
        );
        return CautiousContactDecisionPolicy.Evaluate(input);
    }

    private static SimulationState ApplyContactDecision(
        SimulationState state,
        ShipState ship,
        ShipDefinitionCatalog shipCatalog,
        ShipContactDecisionExplanation decision
    )
    {
        if (decision.ResultingCourse is not { } course)
        {
            return state;
        }

        TacticalCourseApplicationResult application = ApplyTacticalCourse(
            state,
            shipCatalog,
            new TargetableTacticalCourseCommand(ship.InstanceId, course.Heading, course.Speed)
        );
        if (application.Outcome != SetTacticalCourseOutcome.Accepted)
        {
            throw new InvalidOperationException("A validated autonomous course was rejected during application.");
        }

        return application.CandidateState;
    }

    private static ScheduledConsequenceTrace InvalidatedContactTrace(
        SimulationState state,
        ScheduledWork work,
        ScheduledConsequenceRule rule
    ) => Trace(state, work, null, rule, ScheduledConsequenceAction.IgnoreInvalidatedWork, false);

    // Current scheduled rules, including cautious-contact decisions, never consult a random source. The
    // trace keeps later randomized behavior from appearing without changing observable diagnostic data.
    private static ScheduledConsequenceTrace Trace(
        SimulationState state,
        ScheduledWork work,
        ShipOrder? order,
        ScheduledConsequenceRule rule,
        ScheduledConsequenceAction action,
        bool completed,
        SensorContactId? contactId = null,
        ShipContactDecisionExplanation? contactDecision = null
    ) =>
        new(
            work.Id,
            work.TargetShipId,
            work.Kind,
            state.Time,
            order?.Id,
            order?.Kind,
            rule,
            action,
            completed,
            false,
            contactId,
            contactDecision
        );

    private void RememberLatestContactDecision(IReadOnlyList<ScheduledConsequenceTrace> traces)
    {
        ShipContactDecisionExplanation? latest = traces
            .Select(trace => trace.ContactDecision)
            .LastOrDefault(decision => decision is not null);
        if (latest is not null)
        {
            LastContactDecisionExplanation = latest;
        }
    }

    private static void AddPlayerScheduledEvent(
        ScheduledConsequenceTrace trace,
        ShipInstanceId playerShipId,
        List<PlayerAdvanceEvent> playerEvents
    )
    {
        if (trace.TargetShipId != playerShipId || !trace.Completed)
        {
            return;
        }

        PlayerAdvanceEvent? playerEvent = trace.WorkKind switch
        {
            ScheduledWorkKind.TravelArrival => new PlayerAdvanceEvent(PlayerAdvanceEventKind.TravelArrived),
            ScheduledWorkKind.SensorRepairCompletion => new PlayerAdvanceEvent(
                PlayerAdvanceEventKind.SensorRepairCompleted
            ),
            ScheduledWorkKind.SensorContactLoss => new PlayerAdvanceEvent(
                PlayerAdvanceEventKind.SensorContactLost,
                trace.ContactId
            ),
            ScheduledWorkKind.ActiveSensorScanCompletion => new PlayerAdvanceEvent(
                PlayerAdvanceEventKind.ActiveSensorScanCompleted,
                trace.ContactId
            ),
            ScheduledWorkKind.OrderWake or ScheduledWorkKind.ShipContactDecisionWake => null,
            _ => throw new InvalidOperationException("A resolved scheduled consequence has an unknown kind."),
        };
        if (playerEvent is not null)
        {
            playerEvents.Add(playerEvent);
        }
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
                repair is not null,
                new ReadOnlyValueList<SensorContactSnapshot>(
                    playerShip.SensorKnowledge.Contacts.Select(contact => contact.ToActorSafeSnapshot())
                ),
                new ReadOnlyValueList<SensorContactActionProjection>(
                    playerShip.SensorKnowledge.Contacts.Select(contact => new SensorContactActionProjection(
                        contact.Id,
                        new ReadOnlyValueList<SensorContactAction>(GetAvailableContactActions(playerShip, contact))
                    ))
                ),
                playerShip.SensorKnowledge.ActiveScan?.TargetContactId
            )
        );
    }

    private static SensorContactAction[] GetAvailableContactActions(ShipState playerShip, SensorContactTrack contact)
    {
        if (contact.Status != SensorContactStatus.Current)
        {
            return [];
        }

        var actions = new List<SensorContactAction>();
        if (
            contact.Identification == SensorContactIdentification.Detected
            && playerShip.SensorIntegrity.Value > 0
            && playerShip.StrategicState is AtLocationState
            && playerShip.SensorKnowledge.ActiveScan is null
        )
        {
            actions.Add(SensorContactAction.ActiveScan);
        }

        if (contact.Identification == SensorContactIdentification.Identified)
        {
            actions.Add(SensorContactAction.Hail);
        }

        return [.. actions];
    }

    private static PlayerAction[] GetAvailableActions(ShipState playerShip)
    {
        if (playerShip.StrategicState is not AtLocationState)
        {
            return [PlayerAction.AdvanceTime];
        }

        List<PlayerAction> actions = [PlayerAction.Travel, PlayerAction.SetTacticalCourse, PlayerAction.AdvanceTime];
        if (
            playerShip.SensorKnowledge.Contacts.Any(contact =>
                GetAvailableContactActions(playerShip, contact).Contains(SensorContactAction.ActiveScan)
            )
        )
        {
            actions.Add(PlayerAction.ActiveSensorScan);
        }

        return [.. actions];
    }

    private void Commit(SimulationState candidate)
    {
        candidate.Validate(_shipCatalog);
        _state = candidate;
    }
}
