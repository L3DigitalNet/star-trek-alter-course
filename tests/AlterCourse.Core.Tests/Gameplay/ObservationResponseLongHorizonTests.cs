using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Proves recurring observation response remains active, bounded, deterministic, and restorable.</summary>
public sealed class ObservationResponseLongHorizonTests
{
    private const long HourMilliseconds = 3_600_000;
    private const int HorizonHours = 24;
    private const int MaximumHorizonLegs = 20_000;
    private readonly ObservationResponseProofFixture _fixture = new();

    /// <summary>Confirms genuine Lost-to-Current episodes drive bounded responses for a full simulation day.</summary>
    [Fact]
    public void RecurringObservationEpisodesRemainActiveAndBoundedForOneDay()
    {
        SimulationState state = _fixture.CreateRecurringWorld().CaptureState();
        long priorReportId = state.ObservationReportIdAllocator.NextId;
        var lossesByObserver = new Dictionary<ShipInstanceId, int>();
        var proposalsByFaction = new Dictionary<FactionId, int>();
        int deliveries = 0;
        int observedGrowthLegs = 0;
        int maximumCheckpointOutstandingWork = state.Scheduler.OutstandingWork.Length;

        int leg = 0;
        while (state.Time.Milliseconds < HorizonHours * HourMilliseconds)
        {
            SimulationAdvanceTraceResult advanced = AdvanceRecurringLeg(state, leg);
            state = advanced.State;
            deliveries += advanced.Traces.Count(trace =>
                trace.Action == ScheduledConsequenceAction.DeliverObservationReport
            );
            CountLosses(advanced.Traces, lossesByObserver);
            CountProposals(advanced.Traces, proposalsByFaction);
            if (state.ObservationReportIdAllocator.NextId > priorReportId)
            {
                observedGrowthLegs++;
            }
            priorReportId = state.ObservationReportIdAllocator.NextId;
            maximumCheckpointOutstandingWork = Math.Max(
                maximumCheckpointOutstandingWork,
                state.Scheduler.OutstandingWork.Length
            );
            AssertBoundedGraph(state);
            leg++;
            Assert.True(leg < MaximumHorizonLegs, "The recurring proof exceeded its finite leg guard.");
        }

        Assert.Equal(leg, observedGrowthLegs);
        Assert.True(deliveries > 1000);
        Assert.True(lossesByObserver.GetValueOrDefault(new ShipInstanceId(2)) > 1000);
        Assert.True(lossesByObserver.GetValueOrDefault(new ShipInstanceId(4)) > 1000);
        Assert.True(proposalsByFaction.GetValueOrDefault(ObservationResponseProofFixture.FactionA) > 100);
        Assert.True(proposalsByFaction.GetValueOrDefault(ObservationResponseProofFixture.FactionB) > 100);
        Assert.True(
            maximumCheckpointOutstandingWork <= 12,
            $"Observed {maximumCheckpointOutstandingWork} outstanding work items at leg checkpoints."
        );
        Assert.DoesNotContain(
            state.Factions.SelectMany(faction => faction.Observation!.InFlightReports),
            report => report.Report.ObserverShipId == state.PlayerShipId
        );
        Console.WriteLine(
            $"horizon={state.Time.Milliseconds}; legs={leg}; "
                + $"reports={state.ObservationReportIdAllocator.NextId - 1}; deliveries={deliveries}; "
                + $"loss2={lossesByObserver.GetValueOrDefault(new ShipInstanceId(2))}; "
                + $"loss4={lossesByObserver.GetValueOrDefault(new ShipInstanceId(4))}; "
                + $"proposalsA={proposalsByFaction.GetValueOrDefault(ObservationResponseProofFixture.FactionA)}; "
                + $"proposalsB={proposalsByFaction.GetValueOrDefault(ObservationResponseProofFixture.FactionB)}; "
                + $"nextWork={state.Scheduler.NextWorkId}; "
                + $"maxCheckpointOutstanding={maximumCheckpointOutstandingWork}"
        );
    }

    /// <summary>Confirms save/load during recurring activity preserves every consequential continuation result.</summary>
    [Fact]
    public void ActiveRecurringPeriodContinuesIdenticallyAcrossSaveLoad()
    {
        SimulationState activeState = AdvanceRecurringLegs(_fixture.CreateRecurringWorld().CaptureState(), 0, 700);
        GameSimulation uninterrupted = _fixture.Restore(activeState);
        GameSimulation resumed = _fixture.RoundTrip(uninterrupted, "observation-response-active-v8.json");

        for (int hour = 1; hour <= 6; hour++)
        {
            int firstLeg = hour * 700;
            uninterrupted = _fixture.Restore(AdvanceRecurringLegs(uninterrupted.CaptureState(), firstLeg, 700));
            resumed = _fixture.Restore(AdvanceRecurringLegs(resumed.CaptureState(), firstLeg, 700));
            Assert.Equal(
                GamePersistence.Serialize(uninterrupted, ObservationResponseProofFixture.Metadata),
                GamePersistence.Serialize(resumed, ObservationResponseProofFixture.Metadata)
            );
        }
    }

    private SimulationAdvanceTraceResult AdvanceRecurringLeg(SimulationState state, int leg)
    {
        LocationId destination =
            leg % 2 == 0 ? FactionBootstrapTests.FactionTestWorld.Alpha : FactionBootstrapTests.FactionTestWorld.Beta;
        SimulationState departed = _fixture.MoveNpcTo(
            state,
            ObservationResponseProofFixture.RecurringTarget,
            destination
        );
        TravelingState traveling = Assert.IsType<TravelingState>(
            departed.GetRequiredShip(ObservationResponseProofFixture.RecurringTarget).StrategicState
        );
        SimulationTime end = traveling.Travel.ExpectedArrival.AdvanceBy(SimulationFixedStep.Duration);
        long? priorContactLossMilliseconds = departed
            .Scheduler.OutstandingWork.Where(work => work.Kind == ScheduledWorkKind.SensorContactLoss)
            .Select(work => (long?)work.DueTime.Milliseconds)
            .Max();
        if (priorContactLossMilliseconds is { } loss && loss >= end.Milliseconds)
        {
            end = new SimulationTime(loss).AdvanceBy(SimulationFixedStep.Duration);
        }
        return _fixture.AdvanceTo(departed, end.Milliseconds);
    }

    private SimulationState AdvanceRecurringLegs(SimulationState state, int firstLeg, int count)
    {
        SimulationState current = state;
        for (int leg = firstLeg; leg < firstLeg + count; leg++)
        {
            current = AdvanceRecurringLeg(current, leg).State;
        }
        return current;
    }

    /// <summary>Confirms the finite production episode becomes dormant when no new observation episode occurs.</summary>
    [Fact]
    public void ProductionWorldEventuallyQuiescesWithoutNewObservationEpisodes()
    {
        SimulationState settled = _fixture.AdvanceTo(_fixture.CreateProduction().CaptureState(), 120_000).State;
        long reportCounter = settled.ObservationReportIdAllocator.NextId;
        SimulationState later = _fixture.AdvanceTo(settled, 3_720_000).State;

        Assert.Equal(reportCounter, later.ObservationReportIdAllocator.NextId);
        Assert.All(later.Factions, faction => Assert.Null(faction.Observation!.ActiveInvestigation));
        Assert.All(later.Factions, faction => Assert.Empty(faction.Observation!.InFlightReports));
        Assert.DoesNotContain(
            later.Scheduler.OutstandingWork,
            work => work.Target.Kind == ScheduledWorkTargetKind.Faction
        );
        AssertBoundedGraph(later);
    }

    private static void CountLosses(
        IReadOnlyList<ScheduledConsequenceTrace> traces,
        Dictionary<ShipInstanceId, int> totals
    )
    {
        foreach (
            ScheduledConsequenceTrace trace in traces.Where(trace =>
                trace.Action == ScheduledConsequenceAction.LoseSensorContact
            )
        )
        {
            totals[trace.TargetShipId] = totals.GetValueOrDefault(trace.TargetShipId) + 1;
        }
    }

    private static void CountProposals(
        IReadOnlyList<ScheduledConsequenceTrace> traces,
        Dictionary<FactionId, int> totals
    )
    {
        foreach (
            FactionInvestigationDecisionExplanation decision in traces
                .Select(trace => trace.FactionInvestigationDecision)
                .OfType<FactionInvestigationDecisionExplanation>()
                .Where(decision => decision.Outcome == FactionInvestigationDecisionOutcome.InvestigationProposed)
        )
        {
            FactionId factionId = decision.ActorKnownFacts.FactionId;
            totals[factionId] = totals.GetValueOrDefault(factionId) + 1;
        }
    }

    private void AssertBoundedGraph(SimulationState state)
    {
        state.Validate(_fixture.ShipCatalog, _fixture.FactionCatalog);
        Assert.All(
            state.Factions,
            faction =>
            {
                FactionObservationState observation = faction.Observation!;
                Assert.True(observation.InFlightReports.Length <= FactionObservationState.MaximumInFlightReports);
                Assert.True(observation.ReceivedReports.Length <= FactionObservationState.MaximumReceivedReports);
                Assert.True(
                    observation.CompletionWatermarks.Length <= FactionObservationState.MaximumCompletionWatermarks
                );
            }
        );
        Assert.DoesNotContain(
            state.Scheduler.OutstandingWork,
            work => work.DueTime.Milliseconds <= state.Time.Milliseconds
        );
        Assert.DoesNotContain(
            state.Ships,
            ship =>
                ship.ActiveOrder is TravelToOrder order
                && ship.StrategicState is AtLocationState location
                && order.Destination == location.LocationId
        );
    }
}
