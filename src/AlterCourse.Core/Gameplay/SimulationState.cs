using System.Collections.Immutable;
using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

internal sealed record SimulationState
{
    internal const int MaximumShips = 256;

    internal SimulationState(
        SimulationTime time,
        SimulationScheduler scheduler,
        ShipInstanceIdAllocator shipIdAllocator,
        StrategicMap strategicMap,
        ShipInstanceId playerShipId,
        IEnumerable<ShipState> ships,
        ShipOrderIdAllocator? orderIdAllocator = null
    )
    {
        ArgumentNullException.ThrowIfNull(ships);
        ShipState[] materialized = ships.Take(MaximumShips + 1).ToArray();
        if (materialized.Length > MaximumShips)
        {
            throw new ArgumentException($"Simulation state supports at most {MaximumShips} ships.", nameof(ships));
        }

        if (materialized.Length == 0 || materialized.Any(ship => ship is null))
        {
            throw new ArgumentException("Simulation state requires at least one nonnull ship.", nameof(ships));
        }

        if (materialized.Any(ship => ship.InstanceId.Value <= 0))
        {
            throw new ArgumentException("Simulation ships require initialized identities.", nameof(ships));
        }

        if (materialized.Select(ship => ship.InstanceId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Simulation ships require unique identities.", nameof(ships));
        }

        Time = time;
        Scheduler = scheduler;
        ShipIdAllocator = shipIdAllocator;
        StrategicMap = strategicMap;
        PlayerShipId = playerShipId;
        OrderIdAllocator = orderIdAllocator ?? ShipOrderIdAllocator.Create();
        // Canonical order makes every per-ship pass independent of caller enumeration order.
        Ships = [.. materialized.OrderBy(ship => ship.InstanceId.Value)];
    }

    internal SimulationTime Time { get; init; }
    internal SimulationScheduler Scheduler { get; init; }
    internal ShipInstanceIdAllocator ShipIdAllocator { get; init; }
    internal ShipOrderIdAllocator OrderIdAllocator { get; init; }
    internal StrategicMap StrategicMap { get; init; }
    internal ShipInstanceId PlayerShipId { get; init; }
    internal ImmutableArray<ShipState> Ships { get; private init; }

    internal ShipState GetRequiredShip(ShipInstanceId shipId)
    {
        if (shipId.Value <= 0)
        {
            throw new ArgumentException("Ship lookup requires an initialized identity.", nameof(shipId));
        }

        foreach (ShipState ship in Ships)
        {
            if (ship.InstanceId == shipId)
            {
                return ship;
            }
        }

        throw new KeyNotFoundException($"No ship exists with identity '{shipId.Value}'.");
    }

    internal SimulationState ReplaceShip(ShipInstanceId shipId, ShipState replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacement.InstanceId != shipId)
        {
            throw new InvalidOperationException("Ship replacement cannot change instance identity.");
        }

        int index = -1;
        for (int candidateIndex = 0; candidateIndex < Ships.Length; candidateIndex++)
        {
            if (Ships[candidateIndex].InstanceId == shipId)
            {
                index = candidateIndex;
                break;
            }
        }
        if (index < 0)
        {
            throw new KeyNotFoundException($"No ship exists with identity '{shipId.Value}'.");
        }

        return this with
        {
            Ships = Ships.SetItem(index, replacement),
        };
    }

    internal SimulationState ReplaceShips(ShipState[] replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (replacements.Length != Ships.Length)
        {
            throw new InvalidOperationException("Bulk ship replacement must preserve aggregate cardinality.");
        }

        for (int index = 0; index < replacements.Length; index++)
        {
            if (replacements[index] is null || replacements[index].InstanceId != Ships[index].InstanceId)
            {
                throw new InvalidOperationException("Bulk ship replacement must preserve canonical ship identities.");
            }
        }

        return this with
        {
            Ships = [.. replacements],
        };
    }

    internal void Validate(ShipDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidateAggregateMembers();

        foreach (ShipState ship in Ships)
        {
            ShipDefinition definition = catalog.GetRequired(ship.DefinitionId);
            if (ship.TacticalMotion.Speed.Value > definition.MaximumTacticalSpeed.Value)
            {
                throw new InvalidOperationException(
                    $"Ship '{ship.InstanceId.Value}' tactical speed exceeds its definition maximum."
                );
            }

            ValidateShip(ship, definition);
            ValidateSensorKnowledge(ship, catalog);
            ValidateAutonomousState(ship);
        }

        ValidateOrders();

        foreach (ScheduledWork work in Scheduler.OutstandingWork)
        {
            ValidateScheduledWork(work);
        }
    }

    private void ValidateAggregateMembers()
    {
        if (Scheduler is null || ShipIdAllocator is null || OrderIdAllocator is null || StrategicMap is null)
        {
            throw new InvalidOperationException("Simulation state contains a null aggregate member.");
        }

        if (PlayerShipId.Value <= 0 || Ships.Count(ship => ship.InstanceId == PlayerShipId) != 1)
        {
            throw new InvalidOperationException("Player ship identity must resolve exactly once.");
        }

        if (ShipIdAllocator.NextId <= Ships[^1].InstanceId.Value)
        {
            throw new InvalidOperationException("Ship allocator must follow every allocated ship identity.");
        }

        if (
            ShipIdAllocator.NextId == long.MaxValue
            || OrderIdAllocator.NextId == long.MaxValue
            || !SimulationScheduler.AreCountersWithinPersistedRange(Scheduler.NextWorkId, Scheduler.NextSequence)
        )
        {
            throw new InvalidOperationException(
                "Simulation state contains an identity counter outside its persisted range."
            );
        }

        if (
            Time.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
            || Scheduler.OutstandingWork.Any(work =>
                work.DueTime.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
            )
        )
        {
            throw new InvalidOperationException("Simulation state lacks fixed-step time continuation headroom.");
        }

        if (
            Time.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0
            || Scheduler.OutstandingWork.Any(work =>
                work.DueTime.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0
            )
        )
        {
            throw new InvalidOperationException("Simulation time and scheduled work must be fixed-step aligned.");
        }

        if (Scheduler.OutstandingWork.Any(work => work.DueTime.Milliseconds < Time.Milliseconds))
        {
            throw new InvalidOperationException("Scheduled work cannot be overdue in a restorable state.");
        }
    }

    private void ValidateScheduledWork(ScheduledWork work)
    {
        ShipState target = GetRequiredShip(work.TargetShipId);
        bool correlated = work.Kind switch
        {
            ScheduledWorkKind.SensorRepairCompletion => target.SensorRepair is SensorRepairState repair
                && repair.ScheduledCompletionId == work.Id
                && repair.ExpectedCompletion == work.DueTime,
            ScheduledWorkKind.TravelArrival => target.StrategicState is TravelingState traveling
                && traveling.Travel.ScheduledArrivalId == work.Id
                && traveling.Travel.ExpectedArrival == work.DueTime,
            ScheduledWorkKind.OrderWake => target.ActiveOrder is HoldUntilOrder hold
                && hold.ScheduledWakeId == work.Id
                && hold.Until == work.DueTime,
            ScheduledWorkKind.SensorContactLoss => target.SensorKnowledge.Contacts.Any(contact =>
                contact.Status == SensorContactStatus.Stale
                && contact.LossWorkId == work.Id
                && contact.LossDueTime == work.DueTime
            ),
            ScheduledWorkKind.ActiveSensorScanCompletion => target.SensorKnowledge.ActiveScan is { } scan
                && scan.ScheduledCompletionId == work.Id
                && scan.ExpectedCompletion == work.DueTime,
            ScheduledWorkKind.ShipContactDecisionWake => target.AutonomousState.PendingContactDecisionWake is
                { } wake
                && wake.ScheduledWorkId == work.Id
                && wake.DueTime == work.DueTime,
            _ => false,
        };
        if (!correlated)
        {
            throw new InvalidOperationException("Scheduled work has no exactly correlated target ship state.");
        }
    }

    private void ValidateShip(ShipState ship, ShipDefinition definition)
    {
        switch (ship.StrategicState)
        {
            case AtLocationState atLocation:
                StrategicMap.GetLocation(atLocation.LocationId);
                break;
            case TravelingState traveling:
                ValidateTravel(ship, traveling);
                break;
            default:
                throw new InvalidOperationException("Ship strategic state kind is unsupported.");
        }

        ValidateRepair(ship, definition);
    }

    private void ValidateSensorKnowledge(ShipState observer, ShipDefinitionCatalog catalog)
    {
        SensorKnowledge knowledge =
            observer.SensorKnowledge
            ?? throw new InvalidOperationException("Ship sensor knowledge cannot be null.");
        if (knowledge.Contacts.Length > SensorKnowledge.MaximumContactsPerObserver)
        {
            throw new InvalidOperationException(
                $"Ship sensor knowledge exceeds the {SensorKnowledge.MaximumContactsPerObserver}-contact limit."
            );
        }

        if (knowledge.NextContactId <= 0 || knowledge.NextContactId == long.MaxValue)
        {
            throw new InvalidOperationException("Contact allocator is outside its persisted range.");
        }

        long previousId = 0;
        HashSet<ShipInstanceId> targets = [];
        foreach (SensorContactTrack contact in knowledge.Contacts)
        {
            if (contact is null || contact.Id.Value <= previousId)
            {
                throw new InvalidOperationException("Sensor contacts require unique identities in canonical order.");
            }

            previousId = contact.Id.Value;
            if (contact.TargetShipId.Value <= 0 || contact.TargetShipId == observer.InstanceId)
            {
                throw new InvalidOperationException("A sensor contact requires a distinct initialized target ship.");
            }

            if (!targets.Add(contact.TargetShipId))
            {
                throw new InvalidOperationException("Sensor knowledge cannot retain multiple contacts for one target.");
            }

            ShipState target;
            try
            {
                target = GetRequiredShip(contact.TargetShipId);
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidOperationException("A sensor contact target must exist in the simulation.", exception);
            }

            ValidateObservedFacts(contact, target, catalog);
            ValidateContactLoss(observer, contact);
        }

        if (knowledge.NextContactId <= previousId)
        {
            throw new InvalidOperationException("Contact allocator must follow every retained contact identity.");
        }

        if (knowledge.ActiveScan is { } activeScan)
        {
            ValidateActiveScan(observer, activeScan, catalog.GetRequired(observer.DefinitionId));
        }
    }

    private void ValidateObservedFacts(
        SensorContactTrack contact,
        ShipState target,
        ShipDefinitionCatalog catalog
    )
    {
        if (
            !double.IsFinite(contact.LastObservedPosition.XKilometers)
            || !double.IsFinite(contact.LastObservedPosition.YKilometers)
        )
        {
            throw new InvalidOperationException("A sensor contact's last observed position must be finite.");
        }

        if (contact.LastObservedAt.Milliseconds > Time.Milliseconds)
        {
            throw new InvalidOperationException("A sensor contact cannot have been observed in the future.");
        }

        switch (contact.Identification)
        {
            case SensorContactIdentification.Detected
                when contact.KnownVesselDisplayName is null && contact.KnownDesignDisplayName is null:
                break;
            case SensorContactIdentification.Identified
                when !string.IsNullOrWhiteSpace(contact.KnownVesselDisplayName)
                    && !string.IsNullOrWhiteSpace(contact.KnownDesignDisplayName)
                    && contact.KnownVesselDisplayName.Length <= ShipState.MaximumVesselDisplayNameLength
                    && contact.KnownDesignDisplayName.Length <= ShipDefinition.MaximumDesignDisplayNameLength:
                ShipDefinition targetDefinition = catalog.GetRequired(target.DefinitionId);
                if (
                    !string.Equals(
                        contact.KnownVesselDisplayName,
                        target.VesselDisplayName,
                        StringComparison.Ordinal
                    )
                    || !string.Equals(
                        contact.KnownDesignDisplayName,
                        targetDefinition.DesignDisplayName,
                        StringComparison.Ordinal
                    )
                )
                {
                    throw new InvalidOperationException("Identified contact display facts must match their target.");
                }

                break;
            default:
                throw new InvalidOperationException("Sensor contact identification and learned names are inconsistent.");
        }
    }

    private void ValidateContactLoss(ShipState observer, SensorContactTrack contact)
    {
        switch (contact.Status)
        {
            case SensorContactStatus.Current or SensorContactStatus.Lost
                when contact.LossWorkId is null && contact.LossDueTime is null:
                return;
            case SensorContactStatus.Stale
                when contact.LossWorkId is { Value: > 0 } lossWorkId
                    && contact.LossDueTime is { } lossDueTime
                    && lossDueTime.Milliseconds > Time.Milliseconds
                    && lossDueTime.Milliseconds > contact.LastObservedAt.Milliseconds:
                EnsureExactlyCorrelated(
                    observer.InstanceId,
                    lossWorkId,
                    lossDueTime,
                    ScheduledWorkKind.SensorContactLoss
                );
                return;
            default:
                throw new InvalidOperationException("Sensor contact status and loss-work correlation are inconsistent.");
        }
    }

    private void ValidateActiveScan(
        ShipState observer,
        ActiveSensorScanState scan,
        ShipDefinition observerDefinition
    )
    {
        if (scan.TargetContactId.Value <= 0 || scan.ScheduledCompletionId.Value <= 0)
        {
            throw new InvalidOperationException("An active scan requires initialized contact and work identities.");
        }

        SensorContactTrack? targetContact = observer.SensorKnowledge.Contacts.FirstOrDefault(contact =>
            contact.Id == scan.TargetContactId
        );
        if (
            targetContact is null
            || targetContact.Status != SensorContactStatus.Current
            || targetContact.Identification != SensorContactIdentification.Detected
        )
        {
            throw new InvalidOperationException("An active scan requires a current unidentified local contact.");
        }

        if (
            scan.StartedAt.Milliseconds > Time.Milliseconds
            || scan.ExpectedCompletion.Milliseconds <= Time.Milliseconds
            || scan.ExpectedCompletion.Milliseconds <= scan.StartedAt.Milliseconds
            || scan.ExpectedCompletion.Milliseconds - scan.StartedAt.Milliseconds
                != observerDefinition.ActiveScanDuration.Milliseconds
        )
        {
            throw new InvalidOperationException("An active scan must contain the current simulation time.");
        }

        EnsureExactlyCorrelated(
            observer.InstanceId,
            scan.ScheduledCompletionId,
            scan.ExpectedCompletion,
            ScheduledWorkKind.ActiveSensorScanCompletion
        );
    }

    private void ValidateAutonomousState(ShipState ship)
    {
        ShipAutonomousState autonomous =
            ship.AutonomousState
            ?? throw new InvalidOperationException("Ship autonomous state cannot be null.");
        if (ship.InstanceId == PlayerShipId && autonomous != ShipAutonomousState.Empty)
        {
            throw new InvalidOperationException("The player ship cannot have an autonomous contact posture or wake.");
        }

        if (autonomous.ContactPosture is not null and not ShipContactPosture.CautiousContact)
        {
            throw new InvalidOperationException("Ship contact posture is unsupported.");
        }

        if (autonomous.PendingContactDecisionWake is not { } wake)
        {
            return;
        }

        if (
            autonomous.ContactPosture != ShipContactPosture.CautiousContact
            || wake.ScheduledWorkId.Value <= 0
            || wake.DueTime.Milliseconds <= Time.Milliseconds
        )
        {
            throw new InvalidOperationException("A pending contact decision wake requires an active cautious posture.");
        }

        EnsureExactlyCorrelated(
            ship.InstanceId,
            wake.ScheduledWorkId,
            wake.DueTime,
            ScheduledWorkKind.ShipContactDecisionWake
        );
    }

    private void ValidateOrders()
    {
        ShipOrder[] activeOrders = [.. Ships.Select(ship => ship.ActiveOrder).OfType<ShipOrder>()];
        if (Ships.Single(ship => ship.InstanceId == PlayerShipId).ActiveOrder is not null)
        {
            throw new InvalidOperationException("The player ship cannot have an autonomous order.");
        }

        if (activeOrders.Select(order => order.Id).Distinct().Count() != activeOrders.Length)
        {
            throw new InvalidOperationException("Active ship orders require unique identities.");
        }

        long greatestOrderId = activeOrders.Length == 0 ? 0 : activeOrders.Max(order => order.Id.Value);
        if (OrderIdAllocator.NextId <= greatestOrderId)
        {
            throw new InvalidOperationException("Order allocator must follow every active order identity.");
        }

        foreach (ShipState ship in Ships)
        {
            switch (ship.ActiveOrder)
            {
                case null:
                    break;
                case TravelToOrder travelTo:
                    ValidateTravelToOrder(ship, travelTo);
                    break;
                case PatrolRouteOrder patrol:
                    ValidatePatrolOrder(ship, patrol);
                    break;
                case HoldUntilOrder hold:
                    ValidateHoldOrder(ship, hold);
                    break;
                default:
                    throw new InvalidOperationException("Active ship order kind is unsupported.");
            }
        }
    }

    private static void ValidateTravelToOrder(ShipState ship, TravelToOrder order)
    {
        if (ship.StrategicState is not TravelingState traveling || traveling.Travel.Destination != order.Destination)
        {
            throw new InvalidOperationException("A TravelTo order must match the ship's active travel destination.");
        }
    }

    private void ValidatePatrolOrder(ShipState ship, PatrolRouteOrder order)
    {
        foreach (LocationId waypoint in order.Waypoints)
        {
            StrategicMap.GetLocation(waypoint);
        }

        for (int index = 0; index < order.Waypoints.Length; index++)
        {
            LocationId origin = order.Waypoints[index];
            LocationId destination = order.Waypoints[(index + 1) % order.Waypoints.Length];
            if (StrategicMap.FindRoute(origin, destination) is null)
            {
                throw new InvalidOperationException(
                    "Every adjacent patrol waypoint, including wraparound, requires a route."
                );
            }
        }

        int previousIndex = (order.NextWaypointIndex - 1 + order.Waypoints.Length) % order.Waypoints.Length;
        if (
            ship.StrategicState is not TravelingState traveling
            || traveling.Travel.Origin != order.Waypoints[previousIndex]
            || traveling.Travel.Destination != order.Waypoints[order.NextWaypointIndex]
        )
        {
            throw new InvalidOperationException("A patrol order must match its declared current leg.");
        }
    }

    private void ValidateHoldOrder(ShipState ship, HoldUntilOrder order)
    {
        if (ship.StrategicState is not AtLocationState || order.Until.Milliseconds <= Time.Milliseconds)
        {
            throw new InvalidOperationException(
                "A HoldUntil order requires an at-location ship and a future wake time."
            );
        }

        EnsureExactlyCorrelated(ship.InstanceId, order.ScheduledWakeId, order.Until, ScheduledWorkKind.OrderWake);
    }

    private void ValidateTravel(ShipState ship, TravelingState traveling)
    {
        StrategicRoute route =
            StrategicMap.FindRoute(traveling.Travel.Origin, traveling.Travel.Destination)
            ?? throw new InvalidOperationException("Active travel must follow a map route.");
        if (
            traveling.Travel.ExpectedArrival.Milliseconds - traveling.Travel.Departure.Milliseconds
            != route.Duration.Milliseconds
        )
        {
            throw new InvalidOperationException("Active travel duration must match its map route.");
        }

        if (
            Time.Milliseconds < traveling.Travel.Departure.Milliseconds
            || Time.Milliseconds >= traveling.Travel.ExpectedArrival.Milliseconds
        )
        {
            throw new InvalidOperationException("Active travel must contain the current simulation time.");
        }

        if (ship.TacticalMotion != default)
        {
            throw new InvalidOperationException("Active strategic travel requires zero tactical motion.");
        }

        EnsureExactlyCorrelated(
            ship.InstanceId,
            traveling.Travel.ScheduledArrivalId,
            traveling.Travel.ExpectedArrival,
            ScheduledWorkKind.TravelArrival
        );
    }

    private void ValidateRepair(ShipState ship, ShipDefinition definition)
    {
        if (ship.SensorRepair is not SensorRepairState repair)
        {
            return;
        }

        if (
            Time.Milliseconds < repair.StartedAt.Milliseconds
            || Time.Milliseconds >= repair.ExpectedCompletion.Milliseconds
        )
        {
            throw new InvalidOperationException("Active sensor repair must contain the current simulation time.");
        }

        if (
            repair.ExpectedCompletion.Milliseconds - repair.StartedAt.Milliseconds
            != definition.SensorRepairDuration.Milliseconds
        )
        {
            throw new InvalidOperationException("Active sensor repair duration must match its ship definition.");
        }

        if (ship.SensorIntegrity != repair.IntegrityAt(Time))
        {
            throw new InvalidOperationException("Sensor integrity must match its active repair at the current time.");
        }

        EnsureExactlyCorrelated(
            ship.InstanceId,
            repair.ScheduledCompletionId,
            repair.ExpectedCompletion,
            ScheduledWorkKind.SensorRepairCompletion
        );
    }

    private void EnsureExactlyCorrelated(
        ShipInstanceId targetShipId,
        ScheduledWorkId id,
        SimulationTime dueTime,
        ScheduledWorkKind kind
    )
    {
        int count = Scheduler.OutstandingWork.Count(work =>
            work.TargetShipId == targetShipId && work.Id == id && work.DueTime == dueTime && work.Kind == kind
        );
        if (count != 1)
        {
            throw new InvalidOperationException("Runtime ship state lacks exactly one correlated scheduled work item.");
        }
    }
}
