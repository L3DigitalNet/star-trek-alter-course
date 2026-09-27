using System.Text;
using AlterCourse.Core.Persistence;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies resource admission independently of DTO construction and semantic validation.</summary>
public sealed class GamePersistenceCollectionBoundsTests
{
    /// <summary>Accepts the exact contract ceiling and rejects the first excess element for every bounded array.</summary>
    [Theory]
    [InlineData("ships", 256)]
    [InlineData("factions", 256)]
    [InlineData("locations", 256)]
    [InlineData("routes", 1024)]
    [InlineData("outstandingWork", 69120)]
    [InlineData("contacts", 255)]
    [InlineData("waypoints", 16)]
    [InlineData("inFlightReports", 8)]
    [InlineData("receivedReports", 16)]
    [InlineData("completionWatermarks", 24)]
    [InlineData("systemDefinitions", 256)]
    [InlineData("installedSystems", 16)]
    [InlineData("directedEnergyReadiness", 16)]
    public void BoundsEveryKnownCollectionAtItsExactCeiling(string member, int maximum)
    {
        GamePersistence.ValidateCollectionBounds(Document(member, maximum), "boundary.json");
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.ValidateCollectionBounds(Document(member, maximum + 1), "boundary.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Contains(
            maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
            failure.Message,
            StringComparison.Ordinal
        );
    }

    /// <summary>Counts nested objects as single entries and recognizes JSON-escaped property names.</summary>
    [Fact]
    public void CountsArrayEntriesRatherThanTheirNestedTokens()
    {
        string entries = string.Join(',', Enumerable.Repeat("{\"nested\":[1,2,3]}", 16));
        byte[] legal = Encoding.UTF8.GetBytes("{\"installedSyste\\u006ds\":[" + entries + "]}");
        GamePersistence.ValidateCollectionBounds(legal, "escaped.json");
        byte[] excess = Encoding.UTF8.GetBytes("{\"installedSyste\\u006ds\":[" + entries + ",{}]}");
        Assert.Throws<GamePersistenceException>(() => GamePersistence.ValidateCollectionBounds(excess, "escaped.json"));
    }

    private static byte[] Document(string member, int count) =>
        Encoding.UTF8.GetBytes("{\"" + member + "\":[" + string.Join(',', Enumerable.Repeat("null", count)) + "]}");
}
