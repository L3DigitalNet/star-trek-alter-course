using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Locks observations to the strategic location they happened in and the reports projected from them.</summary>
public sealed class StrategicContactReportTests
{
    private static readonly LocationId Dawn = new("dawn-anchor");
    private static readonly LocationId Vesper = new("vesper-reach");
    private static readonly LocationId Meridian = new("meridian-drift");

    // Step counts on the locked first-contact timeline: detection at 3500 ms, the final current
    // observation at 24000 ms, stale at 24100 ms, and loss at 29100 ms.
    private const int StepsToAcquisition = 35;
    private const int StepsFromAcquisitionToLastCurrent = 205;
    private const int StepsFromLastCurrentToStale = 1;
    private const int StepsFromStaleToLoss = 50;

    private readonly Milestone3ProofFixture _fixture = new();

    /// <summary>Confirms a fresh observation records the strategic location the observer made it from.</summary>
    [Fact]
    public void NewObservationRecordsTheObserversStrategicLocation()
    {
        GameSimulation game = CreateAcquiredContact();

        SensorContactTrack track = Assert.Single(Milestone3ProofFixture.Player(game).SensorKnowledge.Contacts);
        StrategicContactReportProjection report = Assert.Single(Reports(game));

        Assert.Equal(Dawn, track.ObservedAtLocationId);
        Assert.Equal(track.Id, report.ContactId);
        Assert.Equal(Dawn, report.ObservedAtLocationId);
        Assert.Equal(Milestone3ProofFixture.Kestrel(game).TacticalPosition, report.LastObservedPosition);
        Assert.Equal(new SimulationTime(3500), report.LastObservedAt);
        Assert.Equal(SensorContactStatus.Current, report.Status);
        Assert.Equal(SensorContactIdentification.Detected, report.Identification);
        Assert.Null(report.KnownVesselDisplayName);
        Assert.Null(report.KnownDesignDisplayName);
    }

    /// <summary>Confirms continued observation advances the observed facts while the frame stays the observer's location.</summary>
    [Fact]
    public void ContinuedObservationRefreshesObservedFactsWithinTheSameFrame()
    {
        GameSimulation game = CreateAcquiredContact();
        StrategicContactReportProjection acquisition = Assert.Single(Reports(game));

        game.AdvanceFixedSteps(StepsFromAcquisitionToLastCurrent);
        StrategicContactReportProjection refreshed = Assert.Single(Reports(game));

        Assert.Equal(acquisition.ContactId, refreshed.ContactId);
        Assert.Equal(Dawn, refreshed.ObservedAtLocationId);
        Assert.Equal(new SimulationTime(24000), refreshed.LastObservedAt);
        Assert.Equal(Milestone3ProofFixture.Kestrel(game).TacticalPosition, refreshed.LastObservedPosition);
        Assert.Equal(SensorContactStatus.Current, refreshed.Status);
    }

    /// <summary>Confirms losing contact changes only status, never the observed position, time, or frame.</summary>
    [Fact]
    public void StaleAndLostTransitionsPreserveObservedFactsAndTheirFrame()
    {
        GameSimulation game = CreateAcquiredContact();
        game.AdvanceFixedSteps(StepsFromAcquisitionToLastCurrent);
        StrategicContactReportProjection current = Assert.Single(Reports(game));

        game.AdvanceFixedSteps(StepsFromLastCurrentToStale);
        StrategicContactReportProjection stale = Assert.Single(Reports(game));
        Assert.Equal(SensorContactStatus.Stale, stale.Status);
        AssertSameObservation(current, stale);

        game.AdvanceFixedSteps(StepsFromStaleToLoss);
        StrategicContactReportProjection lost = Assert.Single(Reports(game));
        Assert.Equal(SensorContactStatus.Lost, lost.Status);
        AssertSameObservation(current, lost);

        // The tactical surface drops a lost contact because it is no longer present; the strategic
        // report retains it because it is a record of an observation that did happen.
        Assert.Empty(game.GetPlayerProjection().Ship.Sensors.Contacts);
    }

    /// <summary>Confirms the observer's own travel never requalifies or moves a retained report.</summary>
    [Fact]
    public void ObserverTravelDoesNotRewriteRetainedReports()
    {
        GameSimulation game = CreateLostContact();
        StrategicContactReportProjection beforeTravel = Assert.Single(Reports(game));

        Assert.Equal(TravelOutcome.Accepted, game.RequestTravel(new TravelIntent(Vesper)).Outcome);
        game.AdvanceFixedSteps(120);

        Assert.Equal(Vesper, game.GetPlayerProjection().Strategic.CurrentLocation!.Id);
        StrategicContactReportProjection afterTravel = Assert.Single(
            Reports(game),
            report => report.ContactId == beforeTravel.ContactId
        );
        Assert.Equal(beforeTravel, afterTravel);
    }

    /// <summary>Confirms later authoritative changes to a target cannot mutate the observer's prior report.</summary>
    [Fact]
    public void HiddenTargetChangesDoNotAlterRetainedReports()
    {
        GameSimulation game = CreateLostContact();
        IReadOnlyList<StrategicContactReportProjection> before = Reports(game);

        GameSimulation moved = MoveKestrel(
            game,
            Meridian,
            new TacticalPosition(-97.5, 64.25),
            sensorCondition: new SystemCondition(0.25)
        );

        Assert.Equal(before, Reports(moved));
    }

    /// <summary>Confirms reacquisition elsewhere keeps the observer-local identity and requalifies the report.</summary>
    [Fact]
    public void ReacquisitionAtAnotherLocationKeepsTheContactIdentity()
    {
        GameSimulation game = CreateLostContact();
        StrategicContactReportProjection lost = Assert.Single(Reports(game));

        GameSimulation moved = MoveKestrel(game, Vesper, new TacticalPosition(4.5, 2.25));
        Assert.Equal(TravelOutcome.Accepted, moved.RequestTravel(new TravelIntent(Vesper)).Outcome);
        moved.AdvanceFixedSteps(120);

        StrategicContactReportProjection reacquired = Assert.Single(
            Reports(moved),
            report => report.ContactId == lost.ContactId
        );
        Assert.Equal(SensorContactStatus.Current, reacquired.Status);
        Assert.Equal(Vesper, reacquired.ObservedAtLocationId);
        Assert.Equal(new SimulationTime(41100), reacquired.LastObservedAt);
        Assert.Equal(Milestone3ProofFixture.Kestrel(moved).TacticalPosition, reacquired.LastObservedPosition);
        Assert.NotEqual(lost.LastObservedPosition, reacquired.LastObservedPosition);
    }

    /// <summary>Confirms learned names reach the report only through the existing active scan.</summary>
    [Fact]
    public void LearnedNamesReachTheReportOnlyAfterIdentification()
    {
        GameSimulation game = CreateAcquiredContact();
        StrategicContactReportProjection detected = Assert.Single(Reports(game));
        Assert.Null(detected.KnownVesselDisplayName);

        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(detected.ContactId).Outcome);
        game.AdvanceFixedSteps(20);

        StrategicContactReportProjection identified = Assert.Single(Reports(game));
        Assert.Equal(SensorContactIdentification.Identified, identified.Identification);
        Assert.Equal("Survey Vessel Kestrel", identified.KnownVesselDisplayName);
        Assert.Equal("Pathfinder class", identified.KnownDesignDisplayName);
        Assert.Equal(Dawn, identified.ObservedAtLocationId);
    }

    /// <summary>Confirms two observers of one world hold separate knowledge and separate local identities.</summary>
    [Fact]
    public void DistinctObserversHoldDistinctLocalIdentitiesForOneTrueShip()
    {
        GameSimulation game = CreateChainedObserverWorld();
        game.AdvanceFixedSteps(1);
        SimulationState state = game.CaptureState();

        // Ship 3 is within range of both the player and ship 4, but the player detects ship 2 first,
        // so one true ship carries a different observer-local identity in each observer's knowledge.
        StrategicContactReportProjection playerReportOfShipThree = Reports(game)
            .Single(report => report.LastObservedPosition == new TacticalPosition(20, 0));
        SensorContactTrack distantObserverTrack = Assert.Single(
            state.GetRequiredShip(new ShipInstanceId(4)).SensorKnowledge.Contacts
        );

        Assert.Equal([1L, 2L], Reports(game).Select(report => report.ContactId.Value));
        Assert.Equal(2, playerReportOfShipThree.ContactId.Value);
        Assert.Equal(1, distantObserverTrack.Id.Value);
        Assert.Equal(new ShipInstanceId(3), distantObserverTrack.TargetShipId);
        Assert.Equal(Dawn, playerReportOfShipThree.ObservedAtLocationId);
        Assert.Equal(Dawn, distantObserverTrack.ObservedAtLocationId);

        // Ship 4 is outside the player's passive range, so it is knowledge the player never acquired.
        Assert.DoesNotContain(Reports(game), report => report.LastObservedPosition == new TacticalPosition(45, 0));
    }

    private static void AssertSameObservation(
        StrategicContactReportProjection expected,
        StrategicContactReportProjection actual
    )
    {
        Assert.Equal(expected.ContactId, actual.ContactId);
        Assert.Equal(expected.ObservedAtLocationId, actual.ObservedAtLocationId);
        Assert.Equal(expected.LastObservedPosition, actual.LastObservedPosition);
        Assert.Equal(expected.LastObservedAt, actual.LastObservedAt);
        Assert.Equal(expected.Identification, actual.Identification);
        Assert.Equal(expected.KnownVesselDisplayName, actual.KnownVesselDisplayName);
        Assert.Equal(expected.KnownDesignDisplayName, actual.KnownDesignDisplayName);
    }

    private static IReadOnlyList<StrategicContactReportProjection> Reports(GameSimulation game) =>
        game.GetPlayerProjection().Strategic.KnownContactReports;

    /// <summary>Rewrites hidden world truth about Kestrel, which no player command can reach.</summary>
    private GameSimulation MoveKestrel(
        GameSimulation game,
        LocationId location,
        TacticalPosition position,
        SystemCondition? sensorCondition = null
    )
    {
        SimulationState state = game.CaptureState();
        ShipState kestrel = Milestone3ProofFixture.Kestrel(game);
        ShipState relocated = kestrel with
        {
            StrategicState = new AtLocationState(location),
            TacticalPosition = position,
            Engineering = sensorCondition is null
                ? kestrel.Engineering
                : kestrel.Engineering with
                {
                    SensorCondition = sensorCondition.Value,
                },
        };
        return GameSimulation.RestoreState(state.ReplaceShip(kestrel.InstanceId, relocated), _fixture.Catalog);
    }

    private GameSimulation CreateAcquiredContact()
    {
        GameSimulation game = _fixture.CreateDefault();
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            game.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );
        game.AdvanceFixedSteps(StepsToAcquisition);
        return game;
    }

    private GameSimulation CreateLostContact()
    {
        GameSimulation game = CreateAcquiredContact();
        game.AdvanceFixedSteps(StepsFromAcquisitionToLastCurrent + StepsFromLastCurrentToStale + StepsFromStaleToLoss);
        return game;
    }

    /// <summary>Builds a one-location world whose ships form a chain of overlapping passive sensor ranges.</summary>
    private GameSimulation CreateChainedObserverWorld()
    {
        // 30 km passive range at nominal capability: ship 1 reaches ships 2 and 3, ship 4 reaches
        // only ship 3, and no observer reaches every other ship.
        ShipStart[] starts =
        [
            ChainedShip(1, "Observer One", 0),
            ChainedShip(2, "Near Ship", 5),
            ChainedShip(3, "Shared Ship", 20),
            ChainedShip(4, "Observer Two", 45),
        ];
        var map = new StrategicMap(
            [new StrategicLocation(Dawn, "Dawn Anchor", new StrategicMapPosition(-5.5, 2.25))],
            []
        );
        return new GameBootstrap(new SimulationTime(0), map, starts[0].InstanceId, starts).CreateSimulation(
            _fixture.Catalog
        );
    }

    private static ShipStart ChainedShip(long id, string vesselDisplayName, double xKilometers)
    {
        var nominal = new SystemCondition(1);
        return new ShipStart(
            new ShipInstanceId(id),
            new ShipDefinitionId("pathfinder"),
            vesselDisplayName,
            new TacticalPosition(xKilometers, 0),
            default,
            nominal,
            nominal,
            nominal,
            new PowerAllocation(new PowerUnits(70), new PowerUnits(50)),
            new AtLocationStart(Dawn)
        );
    }
}
