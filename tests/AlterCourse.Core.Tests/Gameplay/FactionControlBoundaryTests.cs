using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies faction assignment authority is rechecked before any state or counter changes.</summary>
public sealed class FactionControlBoundaryTests
{
    /// <summary>Confirms accepted authority creates one normal TravelTo and its exact objective correlation.</summary>
    [Fact]
    public void AcceptedProposalCreatesOrdinaryTravelAndExactOrderCorrelation()
    {
        SimulationState state = FactionTestWorld.DormantFactionState();
        var proposal = new FactionAssignmentProposal(
            FactionTestWorld.FactionA,
            new ShipInstanceId(2),
            FactionTestWorld.Beta
        );

        FactionAssignmentApplicationResult result = GameSimulation.ApplyFactionAssignment(state, proposal);

        Assert.Equal(FactionAssignmentApplicationOutcome.Accepted, result.Outcome);
        ShipState ship = result.CandidateState.GetRequiredShip(proposal.ShipId);
        TravelToOrder order = Assert.IsType<TravelToOrder>(ship.ActiveOrder);
        TravelingState traveling = Assert.IsType<TravelingState>(ship.StrategicState);
        EstablishPresenceObjectiveState objective = result.CandidateState.Factions[0].PresenceObjective!;
        Assert.Equal(order.Id, objective.AssignedOrderId);
        Assert.Equal(ship.InstanceId, objective.AssignedShipId);
        Assert.Equal(FactionObjectiveStatus.Assigned, objective.Status);
        Assert.Equal(traveling.Travel.ExpectedArrival, result.CandidateState.Factions[0].PendingDecisionWake!.DueTime);
    }

    /// <summary>Confirms identity, objective, controller, and wake mismatches fail without mutation.</summary>
    [Fact]
    public void RejectsWrongFactionObjectiveShipControllerAndPendingWakeWithoutMutation()
    {
        SimulationState state = FactionTestWorld.DormantFactionState();
        var valid = new FactionAssignmentProposal(
            FactionTestWorld.FactionA,
            new ShipInstanceId(2),
            FactionTestWorld.Beta
        );
        AssertRejected(
            state,
            new FactionAssignmentProposal(new FactionId(99), valid.ShipId, valid.Destination),
            FactionAssignmentApplicationOutcome.FactionMissing
        );
        AssertRejected(
            state,
            new FactionAssignmentProposal(valid.FactionId, valid.ShipId, FactionTestWorld.Gamma),
            FactionAssignmentApplicationOutcome.ObjectiveMismatch
        );
        AssertRejected(
            state,
            new FactionAssignmentProposal(valid.FactionId, new ShipInstanceId(99), valid.Destination),
            FactionAssignmentApplicationOutcome.ShipMissing
        );
        AssertRejected(
            state,
            new FactionAssignmentProposal(valid.FactionId, new ShipInstanceId(4), valid.Destination),
            FactionAssignmentApplicationOutcome.ControllerMismatch
        );
        SimulationState foreignControlled = FactionTestWorld.DormantFactionState(includeFactionB: true);
        AssertRejected(
            foreignControlled,
            new FactionAssignmentProposal(valid.FactionId, new ShipInstanceId(4), valid.Destination),
            FactionAssignmentApplicationOutcome.ControllerMismatch
        );

        SimulationState pending = FactionTestWorld
            .CreateBootstrap(
                [new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta)],
                controlledShips: true
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
        AssertRejected(pending, valid, FactionAssignmentApplicationOutcome.DecisionWakePending);
    }

    /// <summary>Confirms player, commitment, movement, and route guards run before allocator changes.</summary>
    [Fact]
    public void RejectsPlayerCommittedTravelingAndRouteInvalidShipsWithoutConsumingCounters()
    {
        SimulationState idle = FactionTestWorld.DormantFactionState();
        ShipState playerControlled = idle.GetRequiredShip(new ShipInstanceId(1)) with
        {
            DirectControllerFactionId = FactionTestWorld.FactionA,
        };
        SimulationState craftedPlayer = idle.ReplaceShip(playerControlled.InstanceId, playerControlled);
        AssertRejected(
            craftedPlayer,
            new FactionAssignmentProposal(
                FactionTestWorld.FactionA,
                playerControlled.InstanceId,
                FactionTestWorld.Beta
            ),
            FactionAssignmentApplicationOutcome.PlayerShipRejected
        );

        SimulationState committed = FactionTestWorld.DormantFactionState(
            new HoldUntilOrderStart(new SimulationTime(500))
        );
        AssertRejected(committed, Proposal(2), FactionAssignmentApplicationOutcome.ShipCommitted);

        SimulationState traveling = FactionTestWorld.DormantFactionState(
            firstNpcStrategic: new TravelingStart(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationTime(0))
        );
        AssertRejected(traveling, Proposal(2), FactionAssignmentApplicationOutcome.ShipTraveling);

        SimulationState noRoute = idle.ReplaceShip(
            new ShipInstanceId(2),
            idle.GetRequiredShip(new ShipInstanceId(2)) with
            {
                StrategicState = new AtLocationState(FactionTestWorld.Gamma),
            }
        );
        AssertRejected(noRoute, Proposal(2), FactionAssignmentApplicationOutcome.RouteUnavailable);
    }

    private static FactionAssignmentProposal Proposal(long shipId) =>
        new(FactionTestWorld.FactionA, new ShipInstanceId(shipId), FactionTestWorld.Beta);

    private static void AssertRejected(
        SimulationState state,
        FactionAssignmentProposal proposal,
        FactionAssignmentApplicationOutcome expected
    )
    {
        long workCounter = state.Scheduler.NextWorkId;
        long sequenceCounter = state.Scheduler.NextSequence;
        long orderCounter = state.OrderIdAllocator.NextId;

        FactionAssignmentApplicationResult result = GameSimulation.ApplyFactionAssignment(state, proposal);

        Assert.Equal(expected, result.Outcome);
        Assert.Same(state, result.CandidateState);
        Assert.Equal(workCounter, result.CandidateState.Scheduler.NextWorkId);
        Assert.Equal(sequenceCounter, result.CandidateState.Scheduler.NextSequence);
        Assert.Equal(orderCounter, result.CandidateState.OrderIdAllocator.NextId);
    }
}
