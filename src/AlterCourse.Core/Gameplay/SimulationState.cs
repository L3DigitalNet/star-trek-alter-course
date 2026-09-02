using System.Collections.Immutable;
using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
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
        IEnumerable<ShipState> ships
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
        // Canonical order makes every per-ship pass independent of caller enumeration order.
        Ships = [.. materialized.OrderBy(ship => ship.InstanceId.Value)];
    }

    internal SimulationTime Time { get; init; }
    internal SimulationScheduler Scheduler { get; init; }
    internal ShipInstanceIdAllocator ShipIdAllocator { get; init; }
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
        }

        foreach (ScheduledWork work in Scheduler.OutstandingWork)
        {
            ValidateScheduledWork(work);
        }
    }

    private void ValidateAggregateMembers()
    {
        if (Scheduler is null || ShipIdAllocator is null || StrategicMap is null)
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
            || !SimulationScheduler.HasContinuationHeadroom(Scheduler.NextWorkId, Scheduler.NextSequence)
            || Time.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
            || Scheduler.OutstandingWork.Any(work =>
                work.DueTime.Milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds
            )
        )
        {
            throw new InvalidOperationException("Simulation state lacks continuation headroom.");
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
