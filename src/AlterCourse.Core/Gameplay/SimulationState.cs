using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

internal sealed record SimulationState(
    SimulationTime Time,
    SimulationScheduler Scheduler,
    ShipInstanceIdAllocator ShipIdAllocator,
    StrategicMap StrategicMap,
    PlayerStrategicState StrategicState,
    ShipDefinition PlayerShipDefinition,
    PlayerShipState PlayerShip
)
{
    internal void Validate()
    {
        if (
            Scheduler is null
            || ShipIdAllocator is null
            || StrategicMap is null
            || StrategicState is null
            || PlayerShipDefinition is null
            || PlayerShip is null
        )
        {
            throw new InvalidOperationException("Simulation state contains a null aggregate member.");
        }

        if (PlayerShip.InstanceId.Value <= 0 || PlayerShip.DefinitionId != PlayerShipDefinition.Id)
        {
            throw new InvalidOperationException("Player ship identity or definition correlation is invalid.");
        }

        if (ShipIdAllocator.NextId <= PlayerShip.InstanceId.Value)
        {
            throw new InvalidOperationException("Ship allocator must follow the allocated player identity.");
        }

        if (Scheduler.OutstandingWork.Any(work => work.DueTime.Milliseconds < Time.Milliseconds))
        {
            throw new InvalidOperationException("Scheduled work cannot be overdue in a restorable state.");
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

        ValidateStrategicState();
        if (PlayerShip.SensorRepair is SensorRepairState repair)
        {
            EnsureCorrelated(
                repair.ScheduledCompletionId,
                repair.ExpectedCompletion,
                ScheduledWorkKind.SensorRepairCompletion
            );
        }
        else
        {
            EnsureNoOutstanding(ScheduledWorkKind.SensorRepairCompletion);
        }
    }

    private void ValidateStrategicState()
    {
        switch (StrategicState)
        {
            case AtLocationState atLocation:
                StrategicMap.GetLocation(atLocation.LocationId);
                EnsureNoOutstanding(ScheduledWorkKind.TravelArrival);
                break;
            case TravelingState traveling:
                if (StrategicMap.FindRoute(traveling.Travel.Origin, traveling.Travel.Destination) is null)
                {
                    throw new InvalidOperationException("Active travel must follow a map route.");
                }

                EnsureCorrelated(
                    traveling.Travel.ScheduledArrivalId,
                    traveling.Travel.ExpectedArrival,
                    ScheduledWorkKind.TravelArrival
                );
                break;
            default:
                throw new InvalidOperationException("Strategic state kind is unsupported.");
        }
    }

    private void EnsureCorrelated(ScheduledWorkId id, SimulationTime dueTime, ScheduledWorkKind kind)
    {
        if (!Scheduler.OutstandingWork.Any(work => work.Id == id && work.DueTime == dueTime && work.Kind == kind))
        {
            throw new InvalidOperationException("Runtime state lacks its correlated scheduled work.");
        }
    }

    private void EnsureNoOutstanding(ScheduledWorkKind kind)
    {
        if (Scheduler.OutstandingWork.Any(work => work.Kind == kind))
        {
            throw new InvalidOperationException("Scheduled work has no correlated runtime state.");
        }
    }
}
