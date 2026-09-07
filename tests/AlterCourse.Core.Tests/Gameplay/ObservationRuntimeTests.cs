using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies bounded observation publication, response, lifecycle, and aggregate validation.</summary>
public sealed class ObservationRuntimeTests
{
    /// <summary>Confirms initial Current episodes publish immutable local facts only once.</summary>
    [Fact]
    public void FirstCurrentPublishesActorSafeReportsAndRefreshDoesNotRepublish()
    {
        GameSimulation game = CreateGame(ObservationResponsePosture.Enabled);

        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        SimulationState published = game.CaptureState();
        FactionObservationState observation = published.Factions[0].Observation!;

        Assert.Equal(FactionTestWorld.FactionA, published.Factions[0].Id);
        Assert.Equal(2, observation.InFlightReports.Length);
        Assert.All(
            observation.InFlightReports,
            item =>
            {
                SensorContactTrack source = published
                    .GetRequiredShip(item.Report.ObserverShipId)
                    .SensorKnowledge.Contacts.Single(contact => contact.Id == item.Report.ObserverContactId);
                Assert.Equal(new SimulationTime(0), item.Report.ObservedAt);
                Assert.Equal(new SimulationTime(2000), item.DueTime);
                Assert.Equal(FactionTestWorld.Alpha, item.Report.ObservedAtLocationId);
                Assert.Equal(source.LastObservedPosition, item.Report.ObservedPosition);
                Assert.Equal(source.LastObservedAt, item.Report.ObservedAt);
                Assert.Equal(SensorContactIdentification.Detected, item.Report.Identification);
                Assert.Null(item.Report.KnownVesselDisplayName);
                Assert.Null(item.Report.KnownDesignDisplayName);
            }
        );
        Assert.Equal(
            [new ShipInstanceId(2), new ShipInstanceId(3)],
            observation.InFlightReports.Select(item => item.Report.ObserverShipId).OrderBy(id => id.Value)
        );
        Assert.All(
            observation.InFlightReports,
            item => Assert.Equal(new SensorContactId(1), item.Report.ObserverContactId)
        );

        SimulationState refreshed = GameSimulation
            .AdvanceTo(
                published,
                new SimulationTime(100),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Equal(2, refreshed.Factions[0].Observation!.InFlightReports.Length);
        Assert.Equal(3, refreshed.ObservationReportIdAllocator.NextId);
    }

    /// <summary>Confirms queued historical facts ignore later hidden target truth.</summary>
    [Fact]
    public void QueuedReportDoesNotChangeAfterHiddenTargetMovementOrControllerChange()
    {
        GameSimulation game = CreateTransitionGame();
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        SimulationState published = game.CaptureState();
        ObservationReportSnapshot queued = Assert.Single(published.Factions[0].Observation!.InFlightReports).Report;
        ShipState hiddenTarget = published.GetRequiredShip(new ShipInstanceId(3));
        SimulationState changedTruth = published.ReplaceShip(
            hiddenTarget.InstanceId,
            hiddenTarget with
            {
                TacticalPosition = new TacticalPosition(9, -4),
                DirectControllerFactionId = FactionTestWorld.FactionA,
            }
        );

        SimulationState refreshed = GameSimulation
            .AdvanceTo(
                changedTruth,
                new SimulationTime(100),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        Assert.Equal(
            queued,
            refreshed
                .Factions[0]
                .Observation!.InFlightReports.Single(item => item.Report.ReportId == queued.ReportId)
                .Report
        );
    }

    /// <summary>Confirms player observations never publish and faction control is rejected.</summary>
    [Fact]
    public void PlayerObservationAndScanCompletionNeverPublishFactionReports()
    {
        AssertPlayerControllerCannotEnterFactionPublicationPath();
        GameSimulation game = CreatePlayerControlledObservationGame();
        game.AdvanceFixedSteps(1);
        SensorContactId contactId = Assert.Single(game.GetPlayerProjection().Ship.Sensors.Contacts).Id;
        Assert.Empty(game.CaptureState().Factions[0].Observation!.InFlightReports);
        long reportCounter = game.CaptureState().ObservationReportIdAllocator.NextId;

        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contactId).Outcome);
        AdvanceUntilResult completed = game.AdvanceUntilNextPlayerRelevantEvent();

        Assert.Equal(AdvanceUntilOutcome.PlayerEventResolved, completed.Outcome);
        Assert.Empty(game.CaptureState().Factions[0].Observation!.InFlightReports);
        SimulationState final = game.CaptureState();
        Assert.Equal(reportCounter, final.ObservationReportIdAllocator.NextId);
        Assert.Equal(
            SensorContactIdentification.Identified,
            Assert.Single(final.GetRequiredShip(final.PlayerShipId).SensorKnowledge.Contacts).Identification
        );
    }

    /// <summary>Confirms disabled typed posture prevents otherwise legitimate publication.</summary>
    [Fact]
    public void DisabledPostureDoesNotPublishFromLegitimateNpcObservation()
    {
        GameSimulation game = CreateGame(ObservationResponsePosture.Disabled);

        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));

        Assert.Empty(game.CaptureState().Factions[0].Observation!.InFlightReports);
    }

    /// <summary>Confirms exact delayed receipt and one coalesced same-time response pass.</summary>
    [Fact]
    public void DeliveryIsNotEarlyAndCoalescesIntoOneAlreadyThereInvestigation()
    {
        GameSimulation game = CreateGame(ObservationResponsePosture.Enabled);
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));

        SimulationState early = GameSimulation
            .AdvanceTo(
                game.CaptureState(),
                new SimulationTime(1900),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Empty(early.Factions[0].Observation!.ReceivedReports);

        SimulationAdvanceTraceResult delivered = GameSimulation.AdvanceTo(
            early,
            new SimulationTime(2000),
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog
        );
        FactionObservationState observation = delivered.State.Factions[0].Observation!;
        Assert.Empty(observation.InFlightReports);
        Assert.Null(observation.ActiveInvestigation);
        Assert.Single(observation.ReceivedReports, item => item.Handling == ObservationReportHandling.Handled);
        Assert.All(observation.ReceivedReports, item => Assert.Equal(new SimulationTime(2000), item.ReceivedAt));
        Assert.Equal(new SimulationTime(2000), Assert.Single(observation.CompletionWatermarks).ObservedThrough);
        Assert.Equal(2, delivered.Traces.Count(trace => trace.WorkKind == ScheduledWorkKind.ObservationReportDelivery));
        Assert.Single(delivered.Traces, trace => trace.FactionInvestigationDecision is not null);
        Assert.DoesNotContain(delivered.Traces, trace => trace.WorkKind == ScheduledWorkKind.TravelArrival);
        Assert.DoesNotContain(
            delivered.State.Scheduler.OutstandingWork,
            work => work.DueTime.Milliseconds <= delivered.State.Time.Milliseconds
        );
    }

    /// <summary>Confirms a duplicate delivery without extant authority is an idempotent no-op.</summary>
    [Fact]
    public void DuplicateDeliveryWithoutInFlightAuthorityIsIgnored()
    {
        SimulationState state = CreateGame(ObservationResponsePosture.Disabled).CaptureState();
        FactionState faction = state.Factions[0];
        (SimulationScheduler scheduler, ScheduledWork duplicate) = state.Scheduler.Schedule(
            state.Time,
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.ObservationReportDelivery
        );

        SimulationAdvanceTraceResult advanced = GameSimulation.AdvanceTo(
            state with
            {
                Scheduler = scheduler,
            },
            state.Time,
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog
        );

        Assert.Empty(advanced.State.Factions[0].Observation!.ReceivedReports);
        ScheduledConsequenceTrace trace = Assert.Single(advanced.Traces, item => item.WorkId == duplicate.Id);
        Assert.Equal(ScheduledConsequenceAction.IgnoreInvalidatedObservationReportDelivery, trace.Action);
    }

    /// <summary>Confirms two reports cannot share one delivery-work authority.</summary>
    [Fact]
    public void GraphValidationRejectsDuplicateDeliveryWorkCorrelation()
    {
        GameSimulation game = CreateTransitionGame();
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        SimulationState state = game.CaptureState();
        FactionState faction = state.Factions[0];
        ObservationReportInFlight first = faction.Observation!.InFlightReports[0];
        var secondReport = new ObservationReportSnapshot(
            new ObservationReportId(2),
            first.Report.ObserverShipId,
            first.Report.ObserverContactId,
            first.Report.ObservedAtLocationId,
            first.Report.ObservedPosition,
            first.Report.ObservedAt,
            first.Report.Identification
        );
        var second = new ObservationReportInFlight(secondReport, first.DeliveryWorkId, first.DueTime);
        SimulationState invalid = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    inFlightReports: [first, second]
                ),
            }
        ) with
        {
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(3),
        };

        Assert.Throws<InvalidOperationException>(() =>
            invalid.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    /// <summary>Confirms one completion does not leave a retry wake when another report is actionable now.</summary>
    [Fact]
    public void AlreadyThereCompletionLeavesDifferentActionableReportDormantUntilAnotherBoundary()
    {
        SimulationState state = CreateTwoLocationResponseState();

        SimulationState resolved = GameSimulation
            .AdvanceTo(state, state.Time, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;

        FactionObservationState observation = resolved.Factions[0].Observation!;
        Assert.Null(resolved.Factions[0].PendingDecisionWake);
        Assert.Null(observation.ActiveInvestigation);
        Assert.Single(
            observation.ReceivedReports,
            report =>
                report.Report.ReportId == new ObservationReportId(1)
                && report.Handling == ObservationReportHandling.Handled
        );
        Assert.Single(
            observation.ReceivedReports,
            report =>
                report.Report.ReportId == new ObservationReportId(2)
                && report.Handling == ObservationReportHandling.Unhandled
        );
    }

    /// <summary>Confirms changed report handling rejects application without mutation.</summary>
    [Fact]
    public void InvestigationApplicationRevalidatesHandlingAtomically()
    {
        SimulationState received = CreateSingleReceivedState(FactionTestWorld.Beta);
        FactionState faction = received.Factions[0];
        FactionInvestigationProposal proposal = GameSimulation.DecideFactionInvestigation(received, faction).Proposal!;
        ReceivedObservationReport source = faction.Observation!.ReceivedReports[0];
        SimulationState handled = received.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports:
                    [
                        new ReceivedObservationReport(
                            source.Report,
                            source.ReceivedAt,
                            ObservationReportHandling.Handled
                        ),
                    ]
                ),
            }
        );

        FactionInvestigationApplicationResult result = GameSimulation.ApplyFactionInvestigation(
            handled,
            proposal,
            FactionTestWorld.ShipCatalog
        );

        Assert.Equal(FactionInvestigationApplicationOutcome.SourceReportIneligible, result.Outcome);
        Assert.Same(handled, result.CandidateState);
    }

    /// <summary>Confirms accepted response reuses ordinary travel and one correlated faction wake.</summary>
    [Fact]
    public void AcceptedInvestigationUsesOrdinaryTravelAndExactSharedWake()
    {
        SimulationState state = CreateSingleReceivedState(FactionTestWorld.Beta);
        ShipState responder = state.GetRequiredShip(new ShipInstanceId(3));
        ShipState observedTarget = state.GetRequiredShip(new ShipInstanceId(2));
        var current = new SensorContactTrack(
            new SensorContactId(1),
            observedTarget.InstanceId,
            observedTarget.TacticalPosition,
            state.Time,
            FactionTestWorld.Alpha,
            SensorContactStatus.Current,
            SensorContactIdentification.Detected
        );
        state = state.ReplaceShip(
            responder.InstanceId,
            responder with
            {
                SensorKnowledge = new SensorKnowledge(2, [current]),
            }
        );
        FactionInvestigationProposal proposal = GameSimulation
            .DecideFactionInvestigation(state, state.Factions[0])
            .Proposal!;
        ShipState responderBefore = state.GetRequiredShip(proposal.ResponderShipId);
        SensorContactTrack currentContact = Assert.Single(
            responderBefore.SensorKnowledge.Contacts,
            contact => contact.Status == SensorContactStatus.Current
        );

        FactionInvestigationApplicationResult result = GameSimulation.ApplyFactionInvestigation(
            state,
            proposal,
            FactionTestWorld.ShipCatalog
        );

        Assert.Equal(FactionInvestigationApplicationOutcome.Accepted, result.Outcome);
        ActiveFactionInvestigation active = result.CandidateState.Factions[0].Observation!.ActiveInvestigation!;
        Assert.Equal(proposal.SourceReport, active.SourceReport);
        Assert.Equal(proposal.ResponderShipId, active.ResponderShipId);
        Assert.Equal(new SimulationTime(3000), result.CandidateState.Factions[0].PendingDecisionWake!.DueTime);
        Assert.Contains(
            result.CandidateState.GetRequiredShip(proposal.ResponderShipId).SensorKnowledge.Contacts,
            contact => contact.Id == currentContact.Id && contact.Status == SensorContactStatus.Stale
        );
        result.CandidateState.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    /// <summary>Confirms aggregate validation enforces the exact delivery delay.</summary>
    [Fact]
    public void GraphValidationRejectsReceiptBeforeExactDeliveryDelay()
    {
        SimulationState state = CreateSingleReceivedState(FactionTestWorld.Beta);
        FactionState faction = state.Factions[0];
        ReceivedObservationReport source = faction.Observation!.ReceivedReports[0];
        SimulationState invalid = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports: [new ReceivedObservationReport(source.Report, new SimulationTime(1000))]
                ),
            }
        );

        Assert.Throws<InvalidOperationException>(() =>
            invalid.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );

        ObservationReportSnapshot nearMaximum = CreateReport(
            1,
            2,
            FactionTestWorld.Beta,
            new SimulationTime(long.MaxValue - 1000)
        );
        SimulationState overflowHistory = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports: [new ReceivedObservationReport(nearMaximum, new SimulationTime(long.MaxValue))]
                ),
            }
        ) with
        {
            Time = new SimulationTime(long.MaxValue),
        };
        Assert.Throws<InvalidOperationException>(() =>
            overflowHistory.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    /// <summary>Confirms report identities cannot alias across recipient factions.</summary>
    [Fact]
    public void GraphValidationRejectsReportIdentityReuseAcrossFactions()
    {
        SimulationState state = CreateSingleReceivedState(FactionTestWorld.Beta);
        FactionState second = state.Factions[1];
        var conflicting = new ObservationReportSnapshot(
            new ObservationReportId(1),
            new ShipInstanceId(4),
            new SensorContactId(1),
            FactionTestWorld.Alpha,
            default,
            new SimulationTime(0),
            SensorContactIdentification.Detected
        );
        SimulationState invalid = EnsureObserverAllocator(state, 4, 2)
            .ReplaceFaction(
                second.Id,
                second with
                {
                    Observation = new FactionObservationState(
                        ObservationResponsePosture.Enabled,
                        receivedReports: [new ReceivedObservationReport(conflicting, new SimulationTime(2000))]
                    ),
                }
            );

        Assert.Throws<InvalidOperationException>(() =>
            invalid.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    /// <summary>Confirms local allocator provenance and handled-report completion evidence.</summary>
    [Fact]
    public void GraphValidationRejectsUnallocatedLocalContactAndHandledReportWithoutWatermark()
    {
        SimulationState state = CreateSingleReceivedState(FactionTestWorld.Beta);
        ShipState sourceShip = state.GetRequiredShip(new ShipInstanceId(2));
        SimulationState unallocated = state.ReplaceShip(
            sourceShip.InstanceId,
            sourceShip with
            {
                SensorKnowledge = new SensorKnowledge(1, []),
            }
        );
        Assert.Throws<InvalidOperationException>(() =>
            unallocated.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );

        FactionState faction = state.Factions[0];
        ReceivedObservationReport source = faction.Observation!.ReceivedReports[0];
        SimulationState unmarked = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports:
                    [
                        new ReceivedObservationReport(
                            source.Report,
                            source.ReceivedAt,
                            ObservationReportHandling.Handled
                        ),
                    ]
                ),
            }
        );
        Assert.Throws<InvalidOperationException>(() =>
            unmarked.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );

        AssertPrematureInFlightWatermarkIsInvalid();
    }

    /// <summary>Confirms active work retains reachable receipt, assignment, and source-handling history.</summary>
    [Fact]
    public void GraphValidationRejectsForgedActiveInvestigationHistory()
    {
        SimulationState state = CreateLongActiveInvestigation();
        FactionState faction = state.Factions[0];
        FactionObservationState observation = faction.Observation!;
        ActiveFactionInvestigation active = observation.ActiveInvestigation!;
        AssertForgedActiveTimingIsInvalid(state, faction, observation, active);

        ReceivedObservationReport retained = observation.ReceivedReports[0];
        var handled = new ReceivedObservationReport(
            retained.Report,
            retained.ReceivedAt,
            ObservationReportHandling.Handled
        );
        AssertInvalidObservationState(
            state,
            faction,
            observation,
            active,
            [handled],
            [new ObservationLocationCompletionWatermark(active.DestinationLocationId, state.Time)]
        );

        AssertInvalidObservationState(
            state,
            faction,
            observation,
            active,
            observation.ReceivedReports,
            [new ObservationLocationCompletionWatermark(active.DestinationLocationId, state.Time)]
        );

        SimulationState staleAssignment = CreateStaleAssignedInvestigation();
        Assert.Throws<InvalidOperationException>(() =>
            staleAssignment.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    private static void AssertForgedActiveTimingIsInvalid(
        SimulationState state,
        FactionState faction,
        FactionObservationState observation,
        ActiveFactionInvestigation active
    )
    {
        var earlyReceipt = new ActiveFactionInvestigation(
            active.SourceReport,
            active.ResponderShipId,
            active.OriginLocationId,
            active.DestinationLocationId,
            new SimulationTime(1000),
            active.AssignedAt,
            active.OrderId
        );
        AssertInvalidObservationState(state, faction, observation, earlyReceipt, []);

        var wrongAssignment = new ActiveFactionInvestigation(
            active.SourceReport,
            active.ResponderShipId,
            active.OriginLocationId,
            active.DestinationLocationId,
            active.SourceReceivedAt,
            new SimulationTime(3000),
            active.OrderId
        );
        AssertInvalidObservationState(
            state with
            {
                Time = new SimulationTime(3000),
            },
            faction,
            observation,
            wrongAssignment,
            observation.ReceivedReports
        );
    }

    /// <summary>Confirms an own-asset release after report expiry does not justify a wake.</summary>
    [Fact]
    public void ResponseWakeIsNotScheduledAfterItsOnlyReportWouldExpire()
    {
        FactionStart[] factions =
        [
            new FactionStart(
                FactionTestWorld.FactionA,
                FactionTestWorld.DefinitionA,
                ObservationResponsePosture: ObservationResponsePosture.Enabled
            ),
        ];
        SimulationState state = FactionTestWorld
            .CreateBootstrap(
                factions,
                controlledShips: true,
                firstNpcOrder: new HoldUntilOrderStart(new SimulationTime(70_000))
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
        var report = new ObservationReportSnapshot(
            new ObservationReportId(1),
            new ShipInstanceId(3),
            new SensorContactId(1),
            FactionTestWorld.Beta,
            default,
            new SimulationTime(0),
            SensorContactIdentification.Detected
        );
        FactionState faction = state.Factions[0];
        state = EnsureObserverAllocator(state, 3, 2)
            .ReplaceFaction(
                faction.Id,
                faction with
                {
                    Observation = new FactionObservationState(
                        ObservationResponsePosture.Enabled,
                        receivedReports: [new ReceivedObservationReport(report, new SimulationTime(2000))]
                    ),
                }
            ) with
        {
            Time = new SimulationTime(2000),
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(2),
        };

        state.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        SimulationState expired = GameSimulation
            .AdvanceTo(state, new SimulationTime(60_000), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;
        Assert.Null(expired.Factions[0].PendingDecisionWake);
    }

    /// <summary>Confirms overflow admits eight stable candidates and never retries suppressed episodes.</summary>
    [Fact]
    public void SimultaneousOverflowAdmitsLowestEightAndNeverRetriesSuppressedEpisodes()
    {
        GameSimulation game = CreateOverflowGame(reverseDeclarations: true);

        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        SimulationState published = game.CaptureState();
        ObservationReportInFlight[] reports = [.. published.Factions[0].Observation!.InFlightReports];

        Assert.Equal(8, reports.Length);
        Assert.Equal(
            Enumerable.Range(1, 8).Select(value => (long)value),
            reports.Select(item => item.Report.ObserverContactId.Value)
        );

        SimulationState afterDelivery = GameSimulation
            .AdvanceTo(
                published,
                new SimulationTime(2100),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Empty(afterDelivery.Factions[0].Observation!.InFlightReports);
        Assert.Equal(9, afterDelivery.ObservationReportIdAllocator.NextId);
    }

    /// <summary>Confirms partial report capacity is filled independently of declaration order.</summary>
    [Fact]
    public void PartialCapacityAdmissionIsStableAcrossDeclarationOrder()
    {
        SimulationState forward = ObservePartiallyOccupiedOverflow(reverseDeclarations: false);
        SimulationState reversed = ObservePartiallyOccupiedOverflow(reverseDeclarations: true);

        long[] expectedContacts = [1, 2, 3, 4, 5, 6, 7, 8];
        Assert.Equal(
            expectedContacts,
            forward.Factions[0].Observation!.InFlightReports.Select(item => item.Report.ObserverContactId.Value)
        );
        Assert.Equal(
            expectedContacts,
            reversed.Factions[0].Observation!.InFlightReports.Select(item => item.Report.ObserverContactId.Value)
        );
    }

    /// <summary>Confirms Stale-to-Current remains one observation episode.</summary>
    [Fact]
    public void StaleToCurrentRefreshDoesNotPublishAnotherEpisode()
    {
        GameSimulation game = CreateTransitionGame();
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        SimulationState initial = game.CaptureState();
        ShipTravelApplicationResult departure = GameSimulation.ApplyShipTravel(
            initial,
            new ShipTravelCommand(new ShipInstanceId(3), FactionTestWorld.Beta)
        );
        SimulationState stale = GameSimulation
            .AdvanceTo(
                departure.CandidateState,
                new SimulationTime(1000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        ShipTravelApplicationResult returning = GameSimulation.ApplyShipTravel(
            stale,
            new ShipTravelCommand(new ShipInstanceId(3), FactionTestWorld.Alpha)
        );

        SimulationState refreshed = GameSimulation
            .AdvanceTo(
                returning.CandidateState,
                new SimulationTime(2000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        Assert.Equal(SensorContactStatus.Current, ContactFor(refreshed, 2, 3).Status);
        Assert.Equal(2, refreshed.ObservationReportIdAllocator.NextId);
    }

    /// <summary>Confirms Lost-to-Current begins exactly one later publication episode.</summary>
    [Fact]
    public void LostToCurrentReacquisitionPublishesOneNewEpisode()
    {
        GameSimulation game = CreateTransitionGame();
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        ShipTravelApplicationResult departure = GameSimulation.ApplyShipTravel(
            game.CaptureState(),
            new ShipTravelCommand(new ShipInstanceId(3), FactionTestWorld.Beta)
        );
        SimulationState lost = GameSimulation
            .AdvanceTo(
                departure.CandidateState,
                new SimulationTime(6000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Equal(SensorContactStatus.Lost, ContactFor(lost, 2, 3).Status);
        ShipTravelApplicationResult returning = GameSimulation.ApplyShipTravel(
            lost,
            new ShipTravelCommand(new ShipInstanceId(3), FactionTestWorld.Alpha)
        );

        SimulationState reacquired = GameSimulation
            .AdvanceTo(
                returning.CandidateState,
                new SimulationTime(7000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        Assert.Equal(SensorContactStatus.Current, ContactFor(reacquired, 2, 3).Status);
        Assert.Equal(3, reacquired.ObservationReportIdAllocator.NextId);
        Assert.Single(
            reacquired.Factions[0].Observation!.InFlightReports,
            item =>
                item.Report.ObserverShipId == new ShipInstanceId(2)
                && item.Report.ObserverContactId == ContactFor(reacquired, 2, 3).Id
        );
    }

    /// <summary>Confirms shared coordination applies presence before report response.</summary>
    [Fact]
    public void PresenceAssignmentAndReportResponseShareOnePresenceFirstBoundary()
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(4, FactionTestWorld.Beta, FactionTestWorld.FactionB),
        ];
        GameSimulation game = new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    FactionTestWorld.Beta,
                    ObservationResponsePosture.Enabled
                ),
                new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
            ]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));

        SimulationState resolved = GameSimulation
            .AdvanceTo(
                game.CaptureState(),
                new SimulationTime(2000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        Assert.Equal(FactionObjectiveStatus.Satisfied, resolved.Factions[0].PresenceObjective!.Status);
        Assert.Contains(
            resolved.Factions[0].Observation!.ReceivedReports,
            report => report.Handling == ObservationReportHandling.Handled
        );
        Assert.Null(resolved.Factions[0].Observation!.ActiveInvestigation);
    }

    /// <summary>Confirms receipt retention keeps the newest sixteen observation times.</summary>
    [Fact]
    public void ReceiptRetentionKeepsNewestSixteenByObservationTime()
    {
        SimulationState state = EnsureObserverAllocator(
            CreateTransitionGame().CaptureState() with
            {
                Time = new SimulationTime(20_000),
            },
            2,
            18
        );
        FactionState faction = state.Factions[0];
        ReceivedObservationReport[] retained =
        [
            .. Enumerable
                .Range(1, 16)
                .Select(id =>
                {
                    SimulationTime observedAt = new(id * 100L);
                    return new ReceivedObservationReport(
                        CreateReport(id, 2, FactionTestWorld.Alpha, observedAt),
                        observedAt.AdvanceBy(new SimulationDuration(2000))
                    );
                }),
        ];
        ObservationReportSnapshot arriving = CreateReport(17, 2, FactionTestWorld.Alpha, new SimulationTime(19_000));
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            new SimulationTime(21_000),
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.ObservationReportDelivery
        );
        state = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    [new ObservationReportInFlight(arriving, work.Id, work.DueTime)],
                    retained
                ),
            }
        ) with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(18),
        };

        SimulationState delivered = GameSimulation
            .AdvanceTo(state, new SimulationTime(21_000), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;

        Assert.Equal(16, delivered.Factions[0].Observation!.ReceivedReports.Length);
        Assert.DoesNotContain(
            delivered.Factions[0].Observation!.ReceivedReports,
            report => report.Report.ReportId == new ObservationReportId(1)
        );
        Assert.Contains(
            delivered.Factions[0].Observation!.ReceivedReports,
            report => report.Report.ReportId == arriving.ReportId
        );
    }

    /// <summary>Confirms equal observation times retain lower report IDs and discarded deliveries cannot resurrect.</summary>
    [Fact]
    public void ReceiptRetentionBreaksEqualObservationTimesByReportIdWithoutResurrection()
    {
        SimulationState state = CreateEqualObservationRetentionState();
        SimulationState delivered = GameSimulation
            .AdvanceTo(state, state.Time, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;

        Assert.Equal(
            Enumerable.Range(1, 16).Select(id => new ObservationReportId(id)),
            delivered.Factions[0].Observation!.ReceivedReports.Select(report => report.Report.ReportId)
        );
        (SimulationScheduler scheduler, _) = delivered.Scheduler.Schedule(
            delivered.Time,
            ScheduledWorkTarget.ForFaction(delivered.Factions[0].Id),
            ScheduledWorkKind.ObservationReportDelivery
        );
        SimulationState replayed = GameSimulation
            .AdvanceTo(
                delivered with
                {
                    Scheduler = scheduler,
                },
                delivered.Time,
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        Assert.DoesNotContain(
            replayed.Factions[0].Observation!.ReceivedReports,
            report => report.Report.ReportId == new ObservationReportId(17)
        );
    }

    /// <summary>Confirms stale controller, order, origin, and route proposals reject atomically.</summary>
    [Fact]
    public void InvestigationApplicationRejectsChangedControllerOrderOriginAndRouteAtomically()
    {
        SimulationState state = CreateSingleReceivedState(FactionTestWorld.Beta);
        FactionInvestigationProposal proposal = GameSimulation
            .DecideFactionInvestigation(state, state.Factions[0])
            .Proposal!;
        ShipState responder = state.GetRequiredShip(proposal.ResponderShipId);
        AssertRejected(
            state.ReplaceShip(responder.InstanceId, responder with { DirectControllerFactionId = null }),
            proposal,
            FactionInvestigationApplicationOutcome.ControllerMismatch
        );
        AssertRejected(
            state.ReplaceShip(
                responder.InstanceId,
                responder with
                {
                    ActiveOrder = new TravelToOrder(new ShipOrderId(90), FactionTestWorld.Beta),
                }
            ),
            proposal,
            FactionInvestigationApplicationOutcome.ShipCommitted
        );
        AssertRejected(
            state,
            new FactionInvestigationProposal(
                proposal.FactionId,
                proposal.SourceReport,
                proposal.ReceivedAt,
                proposal.ResponderShipId,
                FactionTestWorld.Gamma,
                proposal.DestinationLocationId,
                proposal.DecisionTime
            ),
            FactionInvestigationApplicationOutcome.OriginMismatch
        );

        SimulationState unreachable = CreateSingleReceivedState(FactionTestWorld.Gamma);
        ReceivedObservationReport source = unreachable.Factions[0].Observation!.ReceivedReports[0];
        AssertRejected(
            unreachable,
            new FactionInvestigationProposal(
                FactionTestWorld.FactionA,
                source.Report,
                source.ReceivedAt,
                new ShipInstanceId(3),
                FactionTestWorld.Alpha,
                FactionTestWorld.Gamma,
                unreachable.Time
            ),
            FactionInvestigationApplicationOutcome.RouteUnavailable
        );
    }

    /// <summary>Confirms every stale authority branch rejects without changing its candidate aggregate.</summary>
    [Fact]
    public void InvestigationApplicationRejectsCompleteAuthorityMatrixAtomically()
    {
        SimulationState state = CreateSingleReceivedState(FactionTestWorld.Beta);
        FactionState faction = state.Factions[0];
        FactionInvestigationProposal proposal = GameSimulation.DecideFactionInvestigation(state, faction).Proposal!;
        AssertFactionAuthorityRejections(state, faction, proposal);
        AssertSourceAuthorityRejections(state, proposal);
        AssertResponderAuthorityRejections(state, proposal);
    }

    private static void AssertFactionAuthorityRejections(
        SimulationState state,
        FactionState faction,
        FactionInvestigationProposal proposal
    )
    {
        AssertRejected(
            state,
            CopyProposal(proposal, factionId: new FactionId(99)),
            FactionInvestigationApplicationOutcome.FactionMissing
        );
        SimulationState disabled = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(ObservationResponsePosture.Disabled),
            }
        );
        AssertRejected(disabled, proposal, FactionInvestigationApplicationOutcome.PostureDisabled);
        AssertRejected(
            CreateStateWithActiveInvestigation(state, proposal),
            proposal,
            FactionInvestigationApplicationOutcome.ActiveInvestigationExists
        );
    }

    private static void AssertSourceAuthorityRejections(SimulationState state, FactionInvestigationProposal proposal)
    {
        AssertRejected(
            state,
            CopyProposal(proposal, sourceReport: CopyReport(proposal.SourceReport, new TacticalPosition(99, 99))),
            FactionInvestigationApplicationOutcome.SourceReportUnavailable
        );
        AssertRejected(
            state,
            CopyProposal(proposal, receivedAt: new SimulationTime(2100), decisionTime: new SimulationTime(2100)),
            FactionInvestigationApplicationOutcome.SourceReportUnavailable
        );
        AssertRejected(
            state with
            {
                Time = new SimulationTime(60_000),
            },
            proposal,
            FactionInvestigationApplicationOutcome.SourceReportIneligible
        );
        AssertRejected(
            state,
            CopyProposal(proposal, decisionTime: new SimulationTime(2001)),
            FactionInvestigationApplicationOutcome.DecisionStale
        );
    }

    /// <summary>Confirms fixed active work survives expiry and suppresses arrival feedback.</summary>
    [Fact]
    public void ActiveInvestigationSurvivesSourceExpiryAndSuppressesArrivalFeedback()
    {
        SimulationState active = CreateLongActiveInvestigation();
        SimulationState expired = GameSimulation
            .AdvanceTo(
                active,
                new SimulationTime(62_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.NotNull(expired.Factions[0].Observation!.ActiveInvestigation);
        expired = ReplaceEvictedSourceWithNewerReport(expired);
        Assert.Equal(
            new ObservationReportId(1),
            expired.Factions[0].Observation!.ActiveInvestigation!.SourceReport.ReportId
        );

        SimulationState arrived = GameSimulation
            .AdvanceTo(
                expired,
                new SimulationTime(72_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        FactionObservationState observation = arrived.Factions[0].Observation!;
        Assert.Null(observation.ActiveInvestigation);
        Assert.Single(
            observation.ReceivedReports,
            report =>
                report.Report.ReportId == new ObservationReportId(2)
                && report.Handling == ObservationReportHandling.Unhandled
        );
        Assert.Equal(new SimulationTime(72_000), Assert.Single(observation.CompletionWatermarks).ObservedThrough);
        Assert.NotEmpty(observation.InFlightReports);
        Assert.All(
            observation.InFlightReports,
            inFlight =>
                Assert.True(
                    inFlight.Report.ObservedAt.Milliseconds
                        <= observation.CompletionWatermarks[0].ObservedThrough.Milliseconds
                )
        );

        AssertArrivalReportsRetainedWithoutResponse(arrived);
    }

    private static void AssertArrivalReportsRetainedWithoutResponse(SimulationState arrived)
    {
        SimulationState retained = GameSimulation
            .AdvanceTo(
                arrived,
                new SimulationTime(74_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Null(retained.Factions[0].Observation!.ActiveInvestigation);
        Assert.Contains(
            retained.Factions[0].Observation!.ReceivedReports,
            report =>
                report.Report.ObservedAt == new SimulationTime(72_000)
                && report.Handling == ObservationReportHandling.Unhandled
        );
        Assert.Null(retained.Factions[0].PendingDecisionWake);
    }

    /// <summary>Confirms a genuine post-watermark Lost-to-Current episode can drive a later response.</summary>
    [Fact]
    public void LaterLostToCurrentEpisodeAfterWatermarkCanRespond()
    {
        SimulationState state = CreateArrivalReportsRetainedState();
        long reportCounter = state.ObservationReportIdAllocator.NextId;
        ShipInstanceId observerId = new(2);
        ShipTravelApplicationResult outbound = GameSimulation.ApplyShipTravel(
            state,
            new ShipTravelCommand(observerId, FactionTestWorld.Alpha)
        );
        Assert.Equal(TravelOutcome.Accepted, outbound.Outcome);
        SimulationState away = GameSimulation
            .AdvanceTo(
                outbound.CandidateState,
                new SimulationTime(144_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        ShipTravelApplicationResult returning = GameSimulation.ApplyShipTravel(
            away,
            new ShipTravelCommand(observerId, FactionTestWorld.Beta)
        );
        Assert.Equal(TravelOutcome.Accepted, returning.Outcome);
        SimulationState reacquired = GameSimulation
            .AdvanceTo(
                returning.CandidateState,
                new SimulationTime(214_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.True(reacquired.ObservationReportIdAllocator.NextId > reportCounter);

        SimulationState responded = GameSimulation
            .AdvanceTo(
                reacquired,
                new SimulationTime(216_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Contains(
            responded.Factions[0].Observation!.ReceivedReports,
            report =>
                report.Report.ObservedAt == new SimulationTime(214_000)
                && report.Handling == ObservationReportHandling.Handled
        );
    }

    private static SimulationState CreateArrivalReportsRetainedState()
    {
        SimulationState active = CreateLongActiveInvestigation();
        SimulationState expired = GameSimulation
            .AdvanceTo(
                active,
                new SimulationTime(62_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        SimulationState arrived = GameSimulation
            .AdvanceTo(
                ReplaceEvictedSourceWithNewerReport(expired),
                new SimulationTime(72_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        return GameSimulation
            .AdvanceTo(
                arrived,
                new SimulationTime(74_000),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
    }

    /// <summary>Confirms completion can replace an unwitnessed watermark without a transient twenty-fifth entry.</summary>
    [Fact]
    public void FullWatermarkSetSettlesAfterArrivalAdmissionWithoutOverflow()
    {
        SimulationState state = CreateFullWatermarkArrivalState();

        SimulationState arrived = GameSimulation
            .AdvanceTo(state, new SimulationTime(3000), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;

        FactionObservationState observation = arrived.Factions[0].Observation!;
        Assert.Equal(FactionObservationState.MaximumCompletionWatermarks, observation.CompletionWatermarks.Length);
        Assert.Contains(observation.CompletionWatermarks, item => item.LocationId == new LocationId("location-25"));
        Assert.DoesNotContain(
            observation.CompletionWatermarks,
            item => item.LocationId == new LocationId("location-16")
        );
    }

    private static SimulationState CreateFullWatermarkArrivalState()
    {
        LocationId origin = new("location-0");
        LocationId destination = new("location-25");
        StrategicMap map = CreateWatermarkMap(origin, destination);
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, new LocationId("location-26"), null),
            FactionTestWorld.CreateShip(2, destination, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(
                3,
                origin,
                FactionTestWorld.FactionA,
                new TravelToOrderStart(destination),
                new TravelingStart(origin, destination, new SimulationTime(2000))
            ),
        ];
        SimulationState state = new GameBootstrap(
            new SimulationTime(2000),
            map,
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
        return AddFullObservationGraph(state, destination);
    }

    private static SimulationState AddFullObservationGraph(SimulationState state, LocationId destination)
    {
        FactionState faction = state.Factions[0];
        ReceivedObservationReport[] received =
        [
            .. Enumerable
                .Range(1, 16)
                .Select(id => new ReceivedObservationReport(
                    CreateReport(id, 2, new LocationId($"location-{id}"), new SimulationTime(0)),
                    new SimulationTime(2000)
                )),
        ];
        List<ObservationReportInFlight> inFlight = [];
        SimulationScheduler scheduler = state.Scheduler;
        for (int id = 17; id <= 24; id++)
        {
            SimulationTime observedAt = new(id == 17 ? 1000 : 2000);
            SimulationTime dueTime = observedAt.AdvanceBy(new SimulationDuration(2000));
            ObservationReportSnapshot report = CreateReport(id, 2, new LocationId($"location-{id}"), observedAt);
            (scheduler, ScheduledWork delivery) = scheduler.Schedule(
                dueTime,
                ScheduledWorkTarget.ForFaction(faction.Id),
                ScheduledWorkKind.ObservationReportDelivery
            );
            inFlight.Add(new ObservationReportInFlight(report, delivery.Id, delivery.DueTime));
        }
        (scheduler, ScheduledWork wake) = scheduler.Schedule(
            new SimulationTime(3000),
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.FactionDecisionWake
        );
        return CompleteFullObservationGraph(state, faction, scheduler, wake, received, inFlight, destination);
    }

    private static SimulationState CompleteFullObservationGraph(
        SimulationState state,
        FactionState faction,
        SimulationScheduler scheduler,
        ScheduledWork wake,
        ReceivedObservationReport[] received,
        List<ObservationReportInFlight> inFlight,
        LocationId destination
    )
    {
        ObservationLocationCompletionWatermark[] watermarks =
        [
            .. Enumerable
                .Range(1, 24)
                .Select(id => new ObservationLocationCompletionWatermark(
                    new LocationId($"location-{id}"),
                    new SimulationTime(2000)
                )),
        ];
        ObservationReportSnapshot activeSource = CreateReport(25, 2, destination, new SimulationTime(0));
        var active = new ActiveFactionInvestigation(
            activeSource,
            new ShipInstanceId(3),
            new LocationId("location-0"),
            destination,
            new SimulationTime(2000),
            new SimulationTime(2000),
            new ShipOrderId(1)
        );
        state = EnsureObserverAllocator(state, 2, 26);
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                PendingDecisionWake = new PendingFactionDecisionWake(wake.Id, wake.DueTime),
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    inFlight,
                    received,
                    active,
                    watermarks
                ),
            }
        ) with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(26),
        };
    }

    private static StrategicMap CreateWatermarkMap(LocationId origin, LocationId destination) =>
        new(
            [
                .. Enumerable
                    .Range(0, 27)
                    .Select(id => new StrategicLocation(new LocationId($"location-{id}"), $"Location {id}", default)),
            ],
            [new StrategicRoute(origin, destination, new SimulationDuration(1000))]
        );

    private static SimulationState CreateLongActiveInvestigation()
    {
        StrategicMap map = new(
            [
                new StrategicLocation(FactionTestWorld.Alpha, "Alpha", default),
                new StrategicLocation(FactionTestWorld.Beta, "Beta", default),
                new StrategicLocation(FactionTestWorld.Gamma, "Gamma", default),
            ],
            [new StrategicRoute(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationDuration(70_000))]
        );
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Beta, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
        ];
        SimulationState state = new GameBootstrap(
            new SimulationTime(0),
            map,
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
        ObservationReportSnapshot report = CreateReport(1, 2, FactionTestWorld.Beta, new SimulationTime(0));
        FactionState faction = state.Factions[0];
        state = EnsureObserverAllocator(state, 2, 2)
            .ReplaceFaction(
                faction.Id,
                faction with
                {
                    Observation = new FactionObservationState(
                        ObservationResponsePosture.Enabled,
                        receivedReports: [new ReceivedObservationReport(report, new SimulationTime(2000))]
                    ),
                }
            ) with
        {
            Time = new SimulationTime(2000),
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(2),
        };
        FactionInvestigationProposal proposal = GameSimulation
            .DecideFactionInvestigation(state, state.Factions[0])
            .Proposal!;
        return GameSimulation.ApplyFactionInvestigation(state, proposal, FactionTestWorld.ShipCatalog).CandidateState;
    }

    private static SimulationState CreateStaleAssignedInvestigation()
    {
        SimulationState state = CreateLongActiveInvestigation();
        FactionState faction = state.Factions[0];
        FactionObservationState observation = faction.Observation!;
        ActiveFactionInvestigation active = observation.ActiveInvestigation!;
        ShipState responder = state.GetRequiredShip(active.ResponderShipId);
        TravelingState traveling = Assert.IsType<TravelingState>(responder.StrategicState);
        (SimulationScheduler scheduler, _) = state.Scheduler.Cancel(traveling.Travel.ScheduledArrivalId);
        (scheduler, _) = scheduler.Cancel(faction.PendingDecisionWake!.WorkId);
        SimulationTime assignedAt = new(60_000);
        SimulationTime arrivalAt = new(130_000);
        (scheduler, ScheduledWork arrival) = scheduler.Schedule(
            arrivalAt,
            responder.InstanceId,
            ScheduledWorkKind.TravelArrival
        );
        (scheduler, ScheduledWork wake) = scheduler.Schedule(
            arrivalAt,
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.FactionDecisionWake
        );
        var staleActive = new ActiveFactionInvestigation(
            active.SourceReport,
            active.ResponderShipId,
            active.OriginLocationId,
            active.DestinationLocationId,
            active.SourceReceivedAt,
            assignedAt,
            active.OrderId
        );
        SimulationState rebuilt = state.ReplaceShip(
            responder.InstanceId,
            responder with
            {
                StrategicState = ReassignedTravel(active, assignedAt, arrivalAt, arrival.Id),
            }
        );
        return rebuilt.ReplaceFaction(
            faction.Id,
            faction with
            {
                PendingDecisionWake = new PendingFactionDecisionWake(wake.Id, wake.DueTime),
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    observation.InFlightReports,
                    observation.ReceivedReports,
                    staleActive,
                    observation.CompletionWatermarks
                ),
            }
        ) with
        {
            Time = assignedAt,
            Scheduler = scheduler,
        };
    }

    private static TravelingState ReassignedTravel(
        ActiveFactionInvestigation active,
        SimulationTime assignedAt,
        SimulationTime arrivalAt,
        ScheduledWorkId arrivalId
    ) => new(new TravelState(active.OriginLocationId, active.DestinationLocationId, assignedAt, arrivalAt, arrivalId));

    private static SimulationState ReplaceEvictedSourceWithNewerReport(SimulationState state)
    {
        FactionState activeFaction = state.Factions[0];
        ObservationReportSnapshot newer = CreateReport(2, 2, FactionTestWorld.Beta, new SimulationTime(60_000));
        SimulationState replaced = EnsureObserverAllocator(state, 2, 3)
            .ReplaceFaction(
                activeFaction.Id,
                activeFaction with
                {
                    Observation = new FactionObservationState(
                        ObservationResponsePosture.Enabled,
                        activeFaction.Observation!.InFlightReports,
                        [new ReceivedObservationReport(newer, new SimulationTime(62_000))],
                        activeFaction.Observation.ActiveInvestigation,
                        activeFaction.Observation.CompletionWatermarks
                    ),
                }
            ) with
        {
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(3),
        };
        replaced.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        return replaced;
    }

    private static void AssertInvalidObservationState(
        SimulationState state,
        FactionState faction,
        FactionObservationState observation,
        ActiveFactionInvestigation active,
        IEnumerable<ReceivedObservationReport> received,
        IEnumerable<ObservationLocationCompletionWatermark>? watermarks = null
    )
    {
        SimulationState invalid = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    observation.InFlightReports,
                    received,
                    active,
                    watermarks ?? observation.CompletionWatermarks
                ),
            }
        );
        Assert.Throws<InvalidOperationException>(() =>
            invalid.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    private static void AssertPrematureInFlightWatermarkIsInvalid()
    {
        GameSimulation pendingDelivery = CreateTransitionGame();
        pendingDelivery.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        SimulationState state = pendingDelivery.CaptureState() with { Time = new SimulationTime(1999) };
        FactionState faction = state.Factions[0];
        ObservationReportSnapshot report = faction.Observation!.InFlightReports[0].Report;
        SimulationState invalid = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    inFlightReports: faction.Observation.InFlightReports,
                    completionWatermarks:
                    [
                        new ObservationLocationCompletionWatermark(report.ObservedAtLocationId, state.Time),
                    ]
                ),
            }
        );
        Assert.Throws<InvalidOperationException>(() =>
            invalid.Validate(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
        );
    }

    private static GameSimulation CreateGame(ObservationResponsePosture posture)
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(4, FactionTestWorld.Beta, FactionTestWorld.FactionB),
        ];
        return new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: posture
                ),
                new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
            ]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static GameSimulation CreatePlayerControlledObservationGame()
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Alpha, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, null),
        ];
        return new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static void AssertPlayerControllerCannotEnterFactionPublicationPath()
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, null),
        ];
        Assert.Throws<ArgumentException>(() =>
            new GameBootstrap(
                new SimulationTime(0),
                FactionTestWorld.CreateMap(),
                ships[0].InstanceId,
                ships,
                [
                    new FactionStart(
                        FactionTestWorld.FactionA,
                        FactionTestWorld.DefinitionA,
                        ObservationResponsePosture: ObservationResponsePosture.Enabled
                    ),
                ]
            )
        );
    }

    private static SimulationState CreateTwoLocationResponseState()
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Beta, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(
                4,
                FactionTestWorld.Alpha,
                FactionTestWorld.FactionA,
                new HoldUntilOrderStart(new SimulationTime(5000))
            ),
            FactionTestWorld.CreateShip(5, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
        ];
        SimulationState state = CreateResponseBootstrap(ships);
        FactionState faction = state.Factions[0];
        (SimulationScheduler scheduler, ScheduledWork wake) = state.Scheduler.Schedule(
            state.Time,
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.FactionDecisionWake
        );
        state = EnsureObserverAllocator(state, 2, 3);
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                PendingDecisionWake = new PendingFactionDecisionWake(wake.Id, wake.DueTime),
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports:
                    [
                        new ReceivedObservationReport(
                            CreateReport(1, 2, FactionTestWorld.Beta, new SimulationTime(0)),
                            state.Time
                        ),
                        new ReceivedObservationReport(
                            CreateReport(2, 2, FactionTestWorld.Alpha, new SimulationTime(0)),
                            state.Time
                        ),
                    ]
                ),
            }
        ) with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(3),
        };
    }

    private static SimulationState CreateResponseBootstrap(ShipStart[] ships) =>
        new GameBootstrap(
            new SimulationTime(2000),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();

    private static GameSimulation CreateTransitionGame()
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Alpha, null),
        ];
        return new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static GameSimulation CreateOverflowGame(bool reverseDeclarations)
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            .. Enumerable.Range(3, 9).Select(id => FactionTestWorld.CreateShip(id, FactionTestWorld.Alpha, null)),
        ];
        return new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            reverseDeclarations ? ships.Reverse() : ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static SimulationState ObservePartiallyOccupiedOverflow(bool reverseDeclarations)
    {
        SimulationState state = CreateOverflowGame(reverseDeclarations).CaptureState();
        state = EnsureObserverAllocator(state, 2, 3);
        FactionState faction = state.Factions[0];
        List<ObservationReportInFlight> inFlight = [];
        SimulationScheduler scheduler = state.Scheduler;
        for (int id = 1; id <= 2; id++)
        {
            ObservationReportSnapshot report = CreateReport(id, 2, FactionTestWorld.Alpha, new SimulationTime(0));
            (scheduler, ScheduledWork work) = scheduler.Schedule(
                new SimulationTime(2000),
                ScheduledWorkTarget.ForFaction(faction.Id),
                ScheduledWorkKind.ObservationReportDelivery
            );
            inFlight.Add(new ObservationReportInFlight(report, work.Id, work.DueTime));
        }
        state = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(ObservationResponsePosture.Enabled, inFlight),
            }
        ) with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(3),
        };
        return GameSimulation
            .AdvanceTo(state, new SimulationTime(100), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;
    }

    private static SimulationState CreateEqualObservationRetentionState()
    {
        SimulationState state = EnsureObserverAllocator(
            CreateTransitionGame().CaptureState() with
            {
                Time = new SimulationTime(10_000),
            },
            2,
            18
        );
        FactionState faction = state.Factions[0];
        ReceivedObservationReport[] retained =
        [
            .. Enumerable
                .Range(1, 16)
                .Select(id => new ReceivedObservationReport(
                    CreateReport(id, 2, FactionTestWorld.Gamma, new SimulationTime(0)),
                    new SimulationTime(2000)
                )),
        ];
        ObservationReportSnapshot incoming = CreateReport(17, 2, FactionTestWorld.Gamma, new SimulationTime(0));
        (SimulationScheduler scheduler, ScheduledWork delivery) = state.Scheduler.Schedule(
            new SimulationTime(2000),
            ScheduledWorkTarget.ForFaction(faction.Id),
            ScheduledWorkKind.ObservationReportDelivery
        );
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    [new ObservationReportInFlight(incoming, delivery.Id, delivery.DueTime)],
                    retained
                ),
            }
        ) with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(18),
        };
    }

    private static SensorContactTrack ContactFor(SimulationState state, long observerId, long targetId) =>
        state
            .GetRequiredShip(new ShipInstanceId(observerId))
            .SensorKnowledge.Contacts.Single(contact => contact.TargetShipId == new ShipInstanceId(targetId));

    private static ObservationReportSnapshot CreateReport(
        long reportId,
        long observerId,
        LocationId locationId,
        SimulationTime observedAt
    ) =>
        new(
            new ObservationReportId(reportId),
            new ShipInstanceId(observerId),
            new SensorContactId(reportId),
            locationId,
            default,
            observedAt,
            SensorContactIdentification.Detected
        );

    private static void AssertRejected(
        SimulationState state,
        FactionInvestigationProposal proposal,
        FactionInvestigationApplicationOutcome expected
    )
    {
        FactionInvestigationApplicationResult result = GameSimulation.ApplyFactionInvestigation(
            state,
            proposal,
            FactionTestWorld.ShipCatalog
        );
        Assert.Equal(expected, result.Outcome);
        Assert.Same(state, result.CandidateState);
        Assert.Equal(state.OrderIdAllocator, result.CandidateState.OrderIdAllocator);
        Assert.Equal(state.ObservationReportIdAllocator, result.CandidateState.ObservationReportIdAllocator);
        Assert.Same(state.Scheduler, result.CandidateState.Scheduler);
    }

    private static void AssertResponderAuthorityRejections(SimulationState state, FactionInvestigationProposal proposal)
    {
        AssertRejected(
            state,
            CopyProposal(proposal, responderShipId: new ShipInstanceId(99)),
            FactionInvestigationApplicationOutcome.ShipMissing
        );
        AssertRejected(
            state,
            CopyProposal(proposal, responderShipId: state.PlayerShipId, originLocationId: FactionTestWorld.Gamma),
            FactionInvestigationApplicationOutcome.PlayerShipRejected
        );
        AssertRejected(
            state,
            CopyProposal(proposal, responderShipId: new ShipInstanceId(4), originLocationId: FactionTestWorld.Beta),
            FactionInvestigationApplicationOutcome.ControllerMismatch
        );
        ShipTravelApplicationResult travel = GameSimulation.ApplyShipTravel(
            state,
            new ShipTravelCommand(proposal.ResponderShipId, proposal.DestinationLocationId)
        );
        Assert.Equal(TravelOutcome.Accepted, travel.Outcome);
        AssertRejected(travel.CandidateState, proposal, FactionInvestigationApplicationOutcome.ShipTraveling);
    }

    private static SimulationState CreateStateWithActiveInvestigation(
        SimulationState state,
        FactionInvestigationProposal proposal
    )
    {
        FactionState faction = state.GetRequiredFaction(proposal.FactionId);
        var active = new ActiveFactionInvestigation(
            proposal.SourceReport,
            proposal.ResponderShipId,
            proposal.OriginLocationId,
            proposal.DestinationLocationId,
            proposal.ReceivedAt,
            proposal.DecisionTime,
            new ShipOrderId(1)
        );
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports: faction.Observation!.ReceivedReports,
                    activeInvestigation: active
                ),
            }
        );
    }

    private static FactionInvestigationProposal CopyProposal(
        FactionInvestigationProposal source,
        FactionId? factionId = null,
        ObservationReportSnapshot? sourceReport = null,
        SimulationTime? receivedAt = null,
        ShipInstanceId? responderShipId = null,
        LocationId? originLocationId = null,
        SimulationTime? decisionTime = null
    ) =>
        new(
            factionId ?? source.FactionId,
            sourceReport ?? source.SourceReport,
            receivedAt ?? source.ReceivedAt,
            responderShipId ?? source.ResponderShipId,
            originLocationId ?? source.OriginLocationId,
            source.DestinationLocationId,
            decisionTime ?? source.DecisionTime
        );

    private static ObservationReportSnapshot CopyReport(
        ObservationReportSnapshot source,
        TacticalPosition observedPosition
    ) =>
        new(
            source.ReportId,
            source.ObserverShipId,
            source.ObserverContactId,
            source.ObservedAtLocationId,
            observedPosition,
            source.ObservedAt,
            source.Identification,
            source.KnownVesselDisplayName,
            source.KnownDesignDisplayName
        );

    private static SimulationState CreateSingleReceivedState(LocationId reportedLocation)
    {
        SimulationState state = GameSimulation
            .AdvanceTo(
                CreateGame(ObservationResponsePosture.Enabled).CaptureState(),
                new SimulationTime(0),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        var report = new ObservationReportSnapshot(
            new ObservationReportId(1),
            new ShipInstanceId(2),
            new SensorContactId(1),
            reportedLocation,
            new TacticalPosition(2, 3),
            new SimulationTime(0),
            SensorContactIdentification.Detected
        );
        FactionState faction = state.Factions[0];
        state = EnsureObserverAllocator(state, 2, 2);
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports: [new ReceivedObservationReport(report, new SimulationTime(2000))]
                ),
            }
        ) with
        {
            Time = new SimulationTime(2000),
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(2),
        };
    }

    private static SimulationState EnsureObserverAllocator(SimulationState state, long observerId, long nextContactId)
    {
        ShipState observer = state.GetRequiredShip(new ShipInstanceId(observerId));
        return state.ReplaceShip(
            observer.InstanceId,
            observer with
            {
                SensorKnowledge = new SensorKnowledge(
                    nextContactId,
                    observer.SensorKnowledge.Contacts,
                    observer.SensorKnowledge.ActiveScan
                ),
            }
        );
    }
}
