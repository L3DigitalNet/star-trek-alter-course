using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Proves the approved faction intent-to-offscreen-consequence chain headlessly.</summary>
public sealed class FactionAssignmentScenarioTests
{
    private readonly FactionAssignmentProofFixture _fixture = new();

    /// <summary>Confirms commitment changes the deterministic choice while both paths reach local NPC knowledge.</summary>
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 6)]
    public void FactionChoiceCreatesOrdinaryTravelAndLocalNpcContact(bool preferredCommitted, long selectedId)
    {
        GameSimulation game = _fixture.Create(preferredCommitted);

        AdvanceUntilResult playerBoundary = game.AdvanceUntilNextPlayerRelevantEvent();
        FactionAssignmentDecisionExplanation decision = game.LastFactionDecisionExplanation!;
        ShipInstanceId selected = new(selectedId);

        Assert.Equal(AdvanceUntilOutcome.PlayerEventResolved, playerBoundary.Outcome);
        Assert.Equal(new SimulationTime(7_400), playerBoundary.StoppedAt);
        Assert.Equal(FactionAssignmentDecisionOutcome.AssignmentProposed, decision.Outcome);
        Assert.Equal(selected, decision.Proposal!.ShipId);
        Assert.Equal(FactionAssignmentDecisionTieRule.ShortestRouteThenLowestShipId, decision.TieRule);
        Assert.False(decision.RandomnessUsed);
        Assert.Equal(2, decision.Candidates.Count);
        AssertPreferredCandidateEvidence(decision, preferredCommitted);
        AssertAssignedOrdinaryTravel(game.CaptureState(), selected);

        game.AdvanceFixedSteps(66);

        AssertOffscreenConsequence(game, selected);
    }

    /// <summary>Confirms midflight V7 reload preserves both assignment variants and exact continuation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MidflightSaveLoadContinuesByteIdentically(bool preferredCommitted)
    {
        GameSimulation uninterrupted = _fixture.Assign(_fixture.Create(preferredCommitted));
        byte[] midpoint = GamePersistence.Serialize(uninterrupted, FactionAssignmentProofFixture.Metadata);
        GameSimulation resumed = _fixture.RoundTrip(uninterrupted, "faction-midflight-v7.json");

        Assert.Equal(midpoint, GamePersistence.Serialize(resumed, FactionAssignmentProofFixture.Metadata));
        uninterrupted = _fixture.ContinueTo(uninterrupted, FactionAssignmentProofFixture.ArrivalTime);
        resumed = _fixture.ContinueTo(resumed, FactionAssignmentProofFixture.ArrivalTime);

        Assert.Equal(
            GamePersistence.Serialize(uninterrupted, FactionAssignmentProofFixture.Metadata),
            GamePersistence.Serialize(resumed, FactionAssignmentProofFixture.Metadata)
        );
        Assert.Equal(FactionObjectiveStatus.Satisfied, resumed.CaptureState().Factions[0].PresenceObjective!.Status);
    }

    /// <summary>Confirms declaration order cannot alter work identity, decision trace, or serialized outcome.</summary>
    [Fact]
    public void PermutedDeclarationsProduceTheSameTraceAndState()
    {
        SimulationAdvanceTraceResult canonical = AdvanceInitial(_fixture.Create());
        SimulationAdvanceTraceResult permuted = AdvanceInitial(_fixture.Create(reverseDeclarations: true));

        Assert.Equal(canonical.Traces, permuted.Traces);
        var first = GameSimulation.RestoreState(canonical.State, _fixture.ShipCatalog, _fixture.FactionCatalog);
        var second = GameSimulation.RestoreState(permuted.State, _fixture.ShipCatalog, _fixture.FactionCatalog);
        Assert.Equal(
            GamePersistence.Serialize(first, FactionAssignmentProofFixture.Metadata),
            GamePersistence.Serialize(second, FactionAssignmentProofFixture.Metadata)
        );
    }

    private SimulationAdvanceTraceResult AdvanceInitial(GameSimulation game) =>
        GameSimulation.AdvanceTo(
            game.CaptureState(),
            new SimulationTime(0),
            _fixture.ShipCatalog,
            _fixture.FactionCatalog
        );

    private static void AssertPreferredCandidateEvidence(
        FactionAssignmentDecisionExplanation decision,
        bool preferredCommitted
    )
    {
        FactionAssignmentDecisionCandidate preferred = decision.Candidates.Single(candidate =>
            candidate.ShipId == FactionAssignmentProofFixture.PreferredShipId
        );
        Assert.Equal(
            preferredCommitted
                ? FactionAssignmentCandidateReason.AlreadyCommitted
                : FactionAssignmentCandidateReason.Eligible,
            preferred.Reason
        );
        Assert.Equal(
            FactionAssignmentCandidateReason.Eligible,
            decision
                .Candidates.Single(candidate => candidate.ShipId == FactionAssignmentProofFixture.AlternateShipId)
                .Reason
        );
    }

    private static void AssertAssignedOrdinaryTravel(SimulationState state, ShipInstanceId selected)
    {
        FactionState faction = state.GetRequiredFaction(FactionAssignmentProofFixture.FactionA);
        TravelToOrder order = Assert.IsType<TravelToOrder>(state.GetRequiredShip(selected).ActiveOrder);
        Assert.Equal(FactionAssignmentProofFixture.Vesper, order.Destination);
        Assert.Equal(FactionObjectiveStatus.Assigned, faction.PresenceObjective!.Status);
        Assert.Equal(selected, faction.PresenceObjective.AssignedShipId);
        Assert.Equal(order.Id, faction.PresenceObjective.AssignedOrderId);
    }

    private static void AssertOffscreenConsequence(GameSimulation game, ShipInstanceId selected)
    {
        SimulationState state = game.CaptureState();
        FactionState faction = state.GetRequiredFaction(FactionAssignmentProofFixture.FactionA);
        Assert.Equal(FactionAssignmentProofFixture.ArrivalTime, state.Time);
        Assert.Equal(FactionObjectiveStatus.Satisfied, faction.PresenceObjective!.Status);
        Assert.Null(faction.PendingDecisionWake);
        Assert.Null(state.GetRequiredShip(selected).ActiveOrder);
        Assert.Contains(
            state.GetRequiredShip(selected).SensorKnowledge.Contacts,
            contact =>
                contact.TargetShipId == FactionAssignmentProofFixture.FactionBShipId
                && contact.Status == SensorContactStatus.Current
        );
        PlayerProjection player = game.GetPlayerProjection();
        Assert.DoesNotContain(
            player.Strategic.KnownContactReports,
            report => report.ObservedAtLocationId == FactionAssignmentProofFixture.Vesper
        );
        Assert.Single(player.Ship.Sensors.Contacts);
    }
}
