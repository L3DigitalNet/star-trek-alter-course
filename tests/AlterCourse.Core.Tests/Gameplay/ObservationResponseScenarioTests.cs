using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Proves both production factions act only after legitimate delayed NPC observations.</summary>
public sealed class ObservationResponseScenarioTests
{
    private readonly ObservationResponseProofFixture _fixture = new();

    /// <summary>Confirms each faction completes the NPC observer-to-responder production chain.</summary>
    [Fact]
    public void ProductionWorldCompletesBothIndependentObservationResponseChains()
    {
        SimulationState initial = _fixture.CreateProduction().CaptureState();
        AssertProductionAuthority(initial);

        SimulationState beforeArrival = _fixture.AdvanceTo(initial, 13_900).State;
        Assert.All(beforeArrival.Factions, faction => Assert.Null(faction.Observation!.ActiveInvestigation));

        SimulationState observed = _fixture.AdvanceTo(beforeArrival, 14_000).State;
        AssertReportInFlight(
            observed,
            ObservationResponseProofFixture.FactionA,
            ObservationResponseProofFixture.AObserver,
            ObservationResponseProofFixture.BObserver
        );
        AssertReportInFlight(
            observed,
            ObservationResponseProofFixture.FactionB,
            ObservationResponseProofFixture.BObserver,
            ObservationResponseProofFixture.AObserver
        );

        SimulationState beforeDelivery = _fixture.AdvanceTo(observed, 15_900).State;
        AssertNoResponseWithoutDelivery(beforeDelivery);

        SimulationAdvanceTraceResult delivered = _fixture.AdvanceTo(beforeDelivery, 16_000);
        AssertInvestigation(
            delivered.State,
            ObservationResponseProofFixture.FactionA,
            ObservationResponseProofFixture.AObserver,
            ObservationResponseProofFixture.AResponder
        );
        AssertInvestigation(
            delivered.State,
            ObservationResponseProofFixture.FactionB,
            ObservationResponseProofFixture.BObserver,
            ObservationResponseProofFixture.BResponder
        );
        Assert.Equal(4, delivered.Traces.Count(trace => trace.WorkKind == ScheduledWorkKind.ObservationReportDelivery));

        SimulationState completed = _fixture.AdvanceTo(delivered.State, 30_000).State;
        AssertCompletedWithActualSensing(
            completed,
            ObservationResponseProofFixture.FactionA,
            ObservationResponseProofFixture.AResponder,
            ObservationResponseProofFixture.BResponder
        );
        AssertCompletedWithActualSensing(
            completed,
            ObservationResponseProofFixture.FactionB,
            ObservationResponseProofFixture.BResponder,
            ObservationResponseProofFixture.AObserver
        );
        long reportCounter = completed.ObservationReportIdAllocator.NextId;
        SimulationState unchangedContacts = _fixture.AdvanceTo(completed, 30_100).State;
        Assert.Equal(reportCounter, unchangedContacts.ObservationReportIdAllocator.NextId);
    }

    /// <summary>Confirms a committed preferred responder changes selection or produces explicit no action.</summary>
    [Fact]
    public void CommittingPreferredResponderChangesSelectionOrProducesExplicitNoAction()
    {
        SimulationAdvanceTraceResult delivered = _fixture.AdvanceTo(_fixture.CreateProduction().CaptureState(), 16_000);

        FactionInvestigationDecisionExplanation[] decisions =
        [
            .. delivered
                .Traces.Select(trace => trace.FactionInvestigationDecision)
                .OfType<FactionInvestigationDecisionExplanation>()
                .Where(decision =>
                    decision.Outcome == FactionInvestigationDecisionOutcome.InvestigationProposed
                    && decision.ActorKnownFacts.DecisionTime == new SimulationTime(16_000)
                ),
        ];
        Assert.Equal(2, decisions.Length);

        foreach (FactionInvestigationDecisionExplanation decision in decisions)
        {
            AssertChangedCommitment(decision);
        }
    }

    /// <summary>Confirms committed destinations and report payloads ignore later hidden target movement.</summary>
    [Fact]
    public void HiddenTargetMovementCannotRetargetCommittedInvestigationsOrInventArrivalKnowledge()
    {
        SimulationState committed = _fixture.AdvanceTo(_fixture.CreateProduction().CaptureState(), 16_000).State;
        ActiveFactionInvestigation aBefore = Investigation(committed, ObservationResponseProofFixture.FactionA);
        ActiveFactionInvestigation bBefore = Investigation(committed, ObservationResponseProofFixture.FactionB);

        SimulationState movingA = _fixture.MoveNpcTo(
            committed,
            ObservationResponseProofFixture.AObserver,
            ObservationResponseProofFixture.Meridian
        );
        SimulationState movingBoth = _fixture.MoveNpcTo(
            movingA,
            ObservationResponseProofFixture.BObserver,
            ObservationResponseProofFixture.Meridian
        );
        Assert.Equal(aBefore, Investigation(movingBoth, ObservationResponseProofFixture.FactionA));
        Assert.Equal(bBefore, Investigation(movingBoth, ObservationResponseProofFixture.FactionB));

        SimulationState completed = _fixture.AdvanceTo(movingBoth, 30_200).State;
        Assert.Equal(
            new AtLocationState(ObservationResponseProofFixture.Meridian),
            completed.GetRequiredShip(ObservationResponseProofFixture.AObserver).StrategicState
        );
        Assert.Equal(
            new AtLocationState(ObservationResponseProofFixture.Meridian),
            completed.GetRequiredShip(ObservationResponseProofFixture.BObserver).StrategicState
        );
        AssertCompletedWithoutOriginalTarget(
            completed,
            ObservationResponseProofFixture.FactionA,
            ObservationResponseProofFixture.AResponder,
            ObservationResponseProofFixture.BObserver
        );
        AssertCompletedWithoutOriginalTarget(
            completed,
            ObservationResponseProofFixture.FactionB,
            ObservationResponseProofFixture.BResponder,
            ObservationResponseProofFixture.AObserver
        );
    }

    private static void AssertProductionAuthority(SimulationState state)
    {
        Assert.Equal(6, state.Ships.Length);
        Assert.Equal(2, state.Factions.Length);
        AssertFactionParticipants(
            state,
            ObservationResponseProofFixture.FactionA,
            ObservationResponseProofFixture.AObserver,
            ObservationResponseProofFixture.AResponder
        );
        AssertFactionParticipants(
            state,
            ObservationResponseProofFixture.FactionB,
            ObservationResponseProofFixture.BObserver,
            ObservationResponseProofFixture.BResponder
        );
    }

    private static void AssertChangedCommitment(FactionInvestigationDecisionExplanation decision)
    {
        FactionInvestigationProposal selected = decision.Proposal!;
        FactionInvestigationDecisionInput facts = decision.ActorKnownFacts;
        FactionShipAssignmentSnapshot[] committedAssets =
        [
            .. facts.Assets.Select(asset =>
                asset.ShipId == selected.ResponderShipId
                    ? new FactionShipAssignmentSnapshot(
                        asset.ShipId,
                        asset.IsPlayerShip,
                        asset.StrategicStatus,
                        asset.CurrentLocationId,
                        hasActiveOrder: true
                    )
                    : asset
            ),
        ];
        var counterfactual = new FactionInvestigationDecisionInput(
            facts.FactionId,
            facts.DecisionTime,
            facts.Posture,
            facts.ReceivedReports,
            facts.CompletionWatermarks,
            hasActiveInvestigation: false,
            committedAssets,
            facts.KnownRoutes
        );
        FactionInvestigationDecisionExplanation changed = FactionInvestigationPolicy.Evaluate(counterfactual);

        Assert.Contains(
            changed.Candidates,
            candidate =>
                candidate.ShipId == selected.ResponderShipId
                && candidate.Reason == FactionInvestigationCandidateReason.AlreadyCommitted
        );
        if (changed.Outcome == FactionInvestigationDecisionOutcome.InvestigationProposed)
        {
            Assert.NotEqual(selected.ResponderShipId, changed.Proposal!.ResponderShipId);
        }
        else
        {
            Assert.Equal(FactionInvestigationDecisionOutcome.NoEligibleResponder, changed.Outcome);
            Assert.Null(changed.Proposal);
        }
    }

    private static void AssertFactionParticipants(
        SimulationState state,
        FactionId factionId,
        ShipInstanceId observerId,
        ShipInstanceId responderId
    )
    {
        Assert.NotEqual(observerId, responderId);
        Assert.NotEqual(state.PlayerShipId, observerId);
        Assert.NotEqual(state.PlayerShipId, responderId);
        Assert.Equal(factionId, state.GetRequiredShip(observerId).DirectControllerFactionId);
        Assert.Equal(factionId, state.GetRequiredShip(responderId).DirectControllerFactionId);
        Assert.Equal(ObservationResponsePosture.Enabled, state.GetRequiredFaction(factionId).Observation!.Posture);
    }

    private static void AssertReportInFlight(
        SimulationState state,
        FactionId factionId,
        ShipInstanceId observerId,
        ShipInstanceId foreignTargetId
    )
    {
        ObservationReportInFlight report = Assert.Single(
            state.GetRequiredFaction(factionId).Observation!.InFlightReports,
            item =>
                item.Report.ObserverShipId == observerId
                && item.Report.ObservedAtLocationId == ObservationResponseProofFixture.Vesper
                && item.Report.ObservedAt == new SimulationTime(14_000)
        );
        Assert.Equal(new SimulationTime(16_000), report.DueTime);
        SensorContactTrack sourceContact = Assert.Single(
            state.GetRequiredShip(observerId).SensorKnowledge.Contacts,
            contact => contact.Id == report.Report.ObserverContactId
        );
        Assert.Equal(foreignTargetId, sourceContact.TargetShipId);
        Assert.NotEqual(factionId, state.GetRequiredShip(foreignTargetId).DirectControllerFactionId);
    }

    private static void AssertNoResponseWithoutDelivery(SimulationState state)
    {
        foreach (FactionState faction in state.Factions)
        {
            Assert.Null(faction.Observation!.ActiveInvestigation);
            Assert.DoesNotContain(
                faction.Observation.ReceivedReports,
                report => report.ReceivedAt.Milliseconds >= 14_000
            );
            Assert.Equal(
                FactionInvestigationDecisionOutcome.NoActionableReport,
                GameSimulation.DecideFactionInvestigation(state, faction).Outcome
            );
        }
    }

    private static void AssertInvestigation(
        SimulationState state,
        FactionId factionId,
        ShipInstanceId observerId,
        ShipInstanceId responderId
    )
    {
        ActiveFactionInvestigation active = Investigation(state, factionId);
        Assert.Equal(observerId, active.SourceReport.ObserverShipId);
        Assert.Equal(responderId, active.ResponderShipId);
        Assert.Equal(ObservationResponseProofFixture.Meridian, active.OriginLocationId);
        Assert.Equal(ObservationResponseProofFixture.Vesper, active.DestinationLocationId);
        Assert.Equal(new SimulationTime(14_000), active.SourceReport.ObservedAt);
        Assert.Equal(new SimulationTime(16_000), active.SourceReceivedAt);
        Assert.Equal(new SimulationTime(16_000), active.AssignedAt);
        TravelToOrder order = Assert.IsType<TravelToOrder>(state.GetRequiredShip(responderId).ActiveOrder);
        Assert.Equal(active.OrderId, order.Id);
        Assert.Equal(active.DestinationLocationId, order.Destination);
    }

    private static void AssertCompletedWithActualSensing(
        SimulationState state,
        FactionId factionId,
        ShipInstanceId responderId,
        ShipInstanceId expectedForeignTarget
    )
    {
        FactionObservationState observation = state.GetRequiredFaction(factionId).Observation!;
        Assert.Contains(
            observation.CompletionWatermarks,
            watermark => watermark.LocationId == ObservationResponseProofFixture.Vesper
        );
        Assert.Contains(
            observation.ReceivedReports,
            report =>
                report.Report.ObserverShipId
                    == (
                        factionId == ObservationResponseProofFixture.FactionA
                            ? ObservationResponseProofFixture.AObserver
                            : ObservationResponseProofFixture.BObserver
                    )
                && report.Report.ObservedAt == new SimulationTime(14_000)
                && report.Handling == ObservationReportHandling.Handled
        );
        Assert.Contains(
            state.GetRequiredShip(responderId).SensorKnowledge.Contacts,
            contact =>
                contact.TargetShipId == expectedForeignTarget && contact.LastObservedAt == new SimulationTime(30_000)
        );
    }

    private static void AssertCompletedWithoutOriginalTarget(
        SimulationState state,
        FactionId factionId,
        ShipInstanceId responderId,
        ShipInstanceId originalTargetId
    )
    {
        FactionObservationState observation = state.GetRequiredFaction(factionId).Observation!;
        Assert.Null(observation.ActiveInvestigation);
        Assert.Contains(
            observation.CompletionWatermarks,
            watermark => watermark.LocationId == ObservationResponseProofFixture.Vesper
        );
        Assert.DoesNotContain(
            state.GetRequiredShip(responderId).SensorKnowledge.Contacts,
            contact => contact.TargetShipId == originalTargetId && contact.Status == SensorContactStatus.Current
        );
        Assert.Contains(
            state.GetRequiredShip(responderId).SensorKnowledge.Contacts,
            contact => contact.Status == SensorContactStatus.Current
        );
    }

    private static ActiveFactionInvestigation Investigation(SimulationState state, FactionId factionId) =>
        state.GetRequiredFaction(factionId).Observation!.ActiveInvestigation!;
}
