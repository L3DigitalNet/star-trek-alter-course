using System.Reflection;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Sensors;

/// <summary>Verifies observer-local contact value and collection boundaries.</summary>
public sealed class SensorDomainTests
{
    /// <summary>Confirms contact identities are positive values independent from world ship identities.</summary>
    [Fact]
    public void ContactIdentityHasPositiveObserverLocalValueSemantics()
    {
        var firstObserverContact = new SensorContactId(7);
        var secondObserverContact = new SensorContactId(7);

        Assert.Equal(firstObserverContact, secondObserverContact);
        Assert.NotEqual(typeof(SensorContactId), typeof(ShipInstanceId));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SensorContactId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SensorContactId(-1));
    }

    /// <summary>Confirms retained contacts normalize by local identity regardless of input order.</summary>
    [Fact]
    public void SensorKnowledgeUsesCanonicalContactOrder()
    {
        SensorContactTrack later = Track(3, 30);
        SensorContactTrack earlier = Track(1, 10);

        var knowledge = new SensorKnowledge(4, [later, earlier]);

        Assert.Equal(new[] { earlier, later }, knowledge.Contacts.ToArray());
    }

    /// <summary>Confirms every retained lifecycle status counts toward the hard per-observer bound.</summary>
    [Fact]
    public void SensorKnowledgeBoundsAllRetainedContacts()
    {
        SensorContactTrack[] maximum =
        [
            .. Enumerable
                .Range(1, SensorKnowledge.MaximumContactsPerObserver)
                .Select(index => Track(index, index + 100, (SensorContactStatus)(((index - 1) % 3) + 1))),
        ];

        var accepted = new SensorKnowledge(SensorKnowledge.MaximumContactsPerObserver + 1, maximum);

        Assert.Equal(SensorKnowledge.MaximumContactsPerObserver, accepted.Contacts.Length);
        Assert.Throws<ArgumentException>(() =>
            new SensorKnowledge(SensorKnowledge.MaximumContactsPerObserver + 2, [.. maximum, Track(13, 113)])
        );
    }

    /// <summary>Confirms actor-safe contact snapshots cannot expose authoritative target identity types.</summary>
    [Fact]
    public void ActorSafeSnapshotContainsOnlyObservedAndLearnedFacts()
    {
        SensorContactSnapshot snapshot = Track(
                4,
                20,
                SensorContactStatus.Current,
                SensorContactIdentification.Identified,
                "Vessel",
                "Design"
            )
            .ToActorSafeSnapshot();

        Assert.Equal(new SensorContactId(4), snapshot.Id);
        Assert.Equal("Vessel", snapshot.KnownVesselDisplayName);
        Assert.Equal("Design", snapshot.KnownDesignDisplayName);
        Assert.DoesNotContain(
            typeof(SensorContactSnapshot).GetProperties(),
            property =>
                property.PropertyType == typeof(ShipInstanceId)
                || property.PropertyType == typeof(ShipDefinitionId)
                || property.Name.Contains("Target", StringComparison.Ordinal)
                || property.Name.Contains("Definition", StringComparison.Ordinal)
        );
        Assert.Contains(
            typeof(SensorContactTrack).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.PropertyType == typeof(ShipInstanceId)
        );
    }

    /// <summary>Confirms nonfinite observed positions cannot be represented.</summary>
    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void ContactObservationRejectsNonfinitePosition(double x, double y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TacticalPosition(x, y));
    }

    private static SensorContactTrack Track(
        long id,
        long targetId,
        SensorContactStatus status = SensorContactStatus.Current,
        SensorContactIdentification identification = SensorContactIdentification.Detected,
        string? vesselName = null,
        string? designName = null
    ) =>
        new(
            new SensorContactId(id),
            new ShipInstanceId(targetId),
            new TacticalPosition(id, targetId),
            new SimulationTime(0),
            status,
            identification,
            vesselName,
            designName,
            status == SensorContactStatus.Stale ? new ScheduledWorkId(id) : null,
            status == SensorContactStatus.Stale ? new SimulationTime(100) : null
        );
}
