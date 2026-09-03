using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies aggregate invariants for ship-owned sensor knowledge and inert correlations.</summary>
public sealed class SensorKnowledgeValidationTests
{
    private static readonly ShipInstanceId PlayerId = new(1);
    private static readonly ShipInstanceId NpcId = new(2);
    private static readonly ShipDefinitionId DefinitionId = new("test-ship");
    private static readonly LocationId Location = new("test-location");

    /// <summary>Confirms canonical valid current and identified contacts restore without behavior.</summary>
    [Fact]
    public void AcceptsValidBoundedKnowledgeWithoutScheduledWork()
    {
        var knowledge = new SensorKnowledge(
            2,
            [Contact(1, NpcId, identification: SensorContactIdentification.Identified)]
        );

        SimulationState state = CreateState(knowledge);
        state.Validate(CreateCatalog());

        Assert.Equal([1L], state.GetRequiredShip(PlayerId).SensorKnowledge.Contacts.Select(c => c.Id.Value));
    }

    /// <summary>Confirms duplicate local identities and target correlations are rejected independently.</summary>
    [Fact]
    public void RejectsDuplicateContactIdentitiesAndTargets()
    {
        AssertInvalid(new SensorKnowledge(3, [Contact(1, NpcId), Contact(1, new ShipInstanceId(3))]));
        AssertInvalid(new SensorKnowledge(3, [Contact(1, NpcId), Contact(2, NpcId)]));
    }

    /// <summary>Confirms contacts cannot correlate to their observer or a ship outside the aggregate.</summary>
    [Fact]
    public void RejectsSelfAndUnknownContactTargets()
    {
        AssertInvalid(new SensorKnowledge(2, [Contact(1, PlayerId)]));
        AssertInvalid(new SensorKnowledge(2, [Contact(1, new ShipInstanceId(99))]));
    }

    /// <summary>Confirms the monotonic allocator is positive, in range, and follows every retained identity.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void RejectsContactAllocatorRegression(long nextContactId)
    {
        AssertInvalid(new SensorKnowledge(nextContactId, [Contact(1, NpcId)]));
    }

    /// <summary>Confirms last-observed facts cannot claim a future simulation time.</summary>
    [Fact]
    public void RejectsFutureContactObservation()
    {
        AssertInvalid(new SensorKnowledge(2, [Contact(1, NpcId) with { LastObservedAt = new SimulationTime(101) }]));
    }

    /// <summary>Confirms identification gates complete, bounded, target-accurate learned display names.</summary>
    [Fact]
    public void RejectsInconsistentContactIdentificationAndNames()
    {
        AssertInvalid(new SensorKnowledge(2, [Contact(1, NpcId) with { KnownVesselDisplayName = "Unexpected" }]));
        AssertInvalid(
            new SensorKnowledge(
                2,
                [
                    Contact(1, NpcId, identification: SensorContactIdentification.Identified) with
                    {
                        KnownDesignDisplayName = null,
                    },
                ]
            )
        );
        AssertInvalid(
            new SensorKnowledge(2, [Contact(1, NpcId) with { Identification = (SensorContactIdentification)99 }])
        );
        AssertInvalid(
            new SensorKnowledge(
                2,
                [
                    Contact(1, NpcId, identification: SensorContactIdentification.Identified) with
                    {
                        KnownVesselDisplayName = new string('v', ShipState.MaximumVesselDisplayNameLength + 1),
                    },
                ]
            )
        );
        AssertInvalid(
            new SensorKnowledge(
                2,
                [
                    Contact(1, NpcId, identification: SensorContactIdentification.Identified) with
                    {
                        KnownDesignDisplayName = new string('d', ShipDefinition.MaximumDesignDisplayNameLength + 1),
                    },
                ]
            )
        );
    }

    /// <summary>Confirms stale loss state requires one exact future scheduled correlation and no other status carries it.</summary>
    [Fact]
    public void ValidatesExactContactLossCorrelation()
    {
        (SimulationScheduler scheduler, ScheduledWork loss) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(500), PlayerId, ScheduledWorkKind.SensorContactLoss);
        SensorContactTrack stale = Contact(1, NpcId) with
        {
            Status = SensorContactStatus.Stale,
            LossWorkId = loss.Id,
            LossDueTime = loss.DueTime,
        };

        CreateState(new SensorKnowledge(2, [stale]), scheduler).Validate(CreateCatalog());
        AssertInvalid(new SensorKnowledge(2, [stale with { LossWorkId = default(ScheduledWorkId) }]), scheduler);
        AssertInvalid(new SensorKnowledge(2, [stale with { LossWorkId = new ScheduledWorkId(99) }]), scheduler);
        AssertInvalid(new SensorKnowledge(2, [stale with { LossDueTime = new SimulationTime(600) }]), scheduler);
        AssertInvalid(new SensorKnowledge(2, [stale with { Status = SensorContactStatus.Current }]), scheduler);
        AssertInvalid(new SensorKnowledge(2, [Contact(1, NpcId) with { Status = (SensorContactStatus)99 }]));
        AssertInvalid(new SensorKnowledge(2, [Contact(1, NpcId)]), scheduler);
    }

    /// <summary>Confirms active scans target retained local identities and pair to one exact completion.</summary>
    [Fact]
    public void ValidatesExactActiveScanCorrelation()
    {
        (SimulationScheduler scheduler, ScheduledWork completion) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(2100), PlayerId, ScheduledWorkKind.ActiveSensorScanCompletion);
        var scan = new ActiveSensorScanState(
            new SensorContactId(1),
            new SimulationTime(100),
            completion.DueTime,
            completion.Id
        );
        var knowledge = new SensorKnowledge(2, [Contact(1, NpcId)], scan);

        CreateState(knowledge, scheduler).Validate(CreateCatalog());
        AssertInvalid(
            knowledge with
            {
                ActiveScan = scan with { TargetContactId = new SensorContactId(9) },
            },
            scheduler
        );
        AssertInvalid(
            new SensorKnowledge(2, [Contact(1, NpcId) with { Status = SensorContactStatus.Lost }], scan),
            scheduler
        );
        AssertInvalid(
            knowledge with
            {
                ActiveScan = scan with { ScheduledCompletionId = new ScheduledWorkId(99) },
            },
            scheduler
        );
        AssertInvalid(knowledge with { ActiveScan = scan with { ScheduledCompletionId = default } }, scheduler);
        AssertInvalid(knowledge with { ActiveScan = scan with { StartedAt = new SimulationTime(101) } }, scheduler);
        AssertInvalid(knowledge with { ActiveScan = scan with { StartedAt = new SimulationTime(0) } }, scheduler);
        AssertInvalid(new SensorKnowledge(2, [Contact(1, NpcId)]), scheduler);
    }

    /// <summary>Confirms cautious posture may be idle but a pending wake is future and exactly scheduled.</summary>
    [Fact]
    public void ValidatesExactCautiousDecisionWakeCorrelation()
    {
        (SimulationScheduler scheduler, ScheduledWork scheduled) = SimulationScheduler
            .Create()
            .Schedule(new SimulationTime(500), NpcId, ScheduledWorkKind.ShipContactDecisionWake);
        var wake = new ShipContactDecisionWake(scheduled.Id, scheduled.DueTime);
        var valid = new ShipAutonomousState(ShipContactPosture.CautiousContact, wake);

        CreateState(SensorKnowledge.Empty, scheduler, valid).Validate(CreateCatalog());
        AssertInvalidAutonomy(new ShipAutonomousState(null, wake), scheduler);
        AssertInvalidAutonomy(
            valid with
            {
                PendingContactDecisionWake = wake with { ScheduledWorkId = new ScheduledWorkId(99) },
            },
            scheduler
        );
        AssertInvalidAutonomy(
            valid with
            {
                PendingContactDecisionWake = wake with { ScheduledWorkId = default },
            },
            scheduler
        );
        AssertInvalidAutonomy(new ShipAutonomousState(ShipContactPosture.CautiousContact), scheduler);
    }

    private static SensorContactTrack Contact(
        long id,
        ShipInstanceId target,
        SensorContactStatus status = SensorContactStatus.Current,
        SensorContactIdentification identification = SensorContactIdentification.Detected
    ) =>
        new(
            new SensorContactId(id),
            target,
            new TacticalPosition(id, id),
            new SimulationTime(100),
            status,
            identification,
            identification == SensorContactIdentification.Identified ? "Ship 2" : null,
            identification == SensorContactIdentification.Identified ? "Test design" : null
        );

    private static void AssertInvalid(SensorKnowledge knowledge, SimulationScheduler? scheduler = null) =>
        Assert.Throws<InvalidOperationException>(() =>
            CreateState(knowledge, scheduler ?? SimulationScheduler.Create()).Validate(CreateCatalog())
        );

    private static void AssertInvalidAutonomy(ShipAutonomousState autonomy, SimulationScheduler scheduler) =>
        Assert.Throws<InvalidOperationException>(() =>
            CreateState(SensorKnowledge.Empty, scheduler, autonomy).Validate(CreateCatalog())
        );

    private static SimulationState CreateState(
        SensorKnowledge knowledge,
        SimulationScheduler? scheduler = null,
        ShipAutonomousState? npcAutonomy = null
    )
    {
        var location = new StrategicLocation(Location, "Test", default);
        ShipState player = CreateShip(PlayerId) with { SensorKnowledge = knowledge };
        ShipState npc = CreateShip(NpcId) with { AutonomousState = npcAutonomy ?? ShipAutonomousState.Empty };
        return new SimulationState(
            new SimulationTime(100),
            scheduler ?? SimulationScheduler.Create(),
            ShipInstanceIdAllocator.Restore(3),
            new StrategicMap([location], []),
            PlayerId,
            [player, npc]
        );
    }

    private static ShipState CreateShip(ShipInstanceId id) =>
        new(
            id,
            DefinitionId,
            $"Ship {id.Value}",
            default,
            default,
            new SensorIntegrity(1),
            null,
            new AtLocationState(Location)
        );

    private static ShipDefinitionCatalog CreateCatalog()
    {
        var definition = new ShipDefinition(
            DefinitionId,
            "Test design",
            new SpeedKilometersPerSecond(10),
            new DistanceKilometers(30),
            new SimulationDuration(2000),
            new SimulationDuration(1000)
        );
        return new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [DefinitionId] = definition }
        );
    }
}
