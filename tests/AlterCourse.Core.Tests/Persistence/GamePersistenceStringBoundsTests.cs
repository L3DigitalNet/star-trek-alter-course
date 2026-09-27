using System.Text;
using System.Text.Json;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Gameplay;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Pins declared string ceilings before JSON strings are materialized.</summary>
public sealed class GamePersistenceStringBoundsTests
{
    /// <summary>Recognizes the existing safe ceiling of every uniformly bounded field.</summary>
    [Theory]
    [InlineData("saveId", 128)]
    [InlineData("displayName", 128)]
    [InlineData("definitionId", 128)]
    [InlineData("id", 128)]
    [InlineData("locationId", 128)]
    [InlineData("observedAtLocationId", 128)]
    [InlineData("targetLocationId", 128)]
    [InlineData("origin", 128)]
    [InlineData("destination", 128)]
    [InlineData("originLocationId", 128)]
    [InlineData("destinationLocationId", 128)]
    [InlineData("knownVesselDisplayName", 64)]
    [InlineData("knownDesignDisplayName", 64)]
    [InlineData("semantics", 256)]
    [InlineData("aimVocabulary", 1024)]
    public void RejectsTheFirstCharacterBeyondTheDeclaredCeiling(string member, int maximum)
    {
        GamePersistence.ValidateInputBounds(Document(member, new string('a', maximum)), "limit.json");
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.ValidateInputBounds(Document(member, new string('a', maximum + 1)), "limit.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
    }

    /// <summary>Uses decoded UTF-16 length, matching the domain's string.Length contract.</summary>
    [Theory]
    [InlineData("é", 64, false)]
    [InlineData("é", 64, true)]
    [InlineData("😀", 32, false)]
    [InlineData("😀", 32, true)]
    public void CountsEscapesAndMultibyteCharactersByDecodedLength(string character, int count, bool escaped)
    {
        string text = string.Concat(Enumerable.Repeat(character, count));
        byte[] Encode(string value) =>
            escaped
                ? Document("knownVesselDisplayName", value)
                : Encoding.UTF8.GetBytes("{\"knownVesselDisplayName\":\"" + value + "\"}");
        GamePersistence.ValidateInputBounds(Encode(text), "unicode.json");
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.ValidateInputBounds(Encode(text + "x"), "unicode.json")
        );
    }

    /// <summary>The waypoint collection's strings obey the existing location identity ceiling.</summary>
    [Fact]
    public void BoundsWaypointElements()
    {
        byte[] legal = JsonSerializer.SerializeToUtf8Bytes(new { waypoints = new[] { new string('a', 128) } });
        byte[] excess = JsonSerializer.SerializeToUtf8Bytes(new { waypoints = new[] { new string('a', 129) } });
        GamePersistence.ValidateInputBounds(legal, "waypoints.json");
        Assert.Throws<GamePersistenceException>(() => GamePersistence.ValidateInputBounds(excess, "waypoints.json"));
    }

    /// <summary>Oversized text fails before a malformed later member can force document materialization.</summary>
    [Fact]
    public void RejectsOversizedTextBeforeReadingTheRemainingDocument()
    {
        var fixture = new Milestone3ProofFixture();
        string json = "{\"metadata\":{\"saveId\":\"" + new string('a', 129) + "\",invalid";
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(json), fixture.Catalog, "large-text.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Contains("128", failure.Message, StringComparison.Ordinal);
        Assert.Contains("saveId", failure.Message, StringComparison.Ordinal);
    }

    private static byte[] Document(string member, string value) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string>(StringComparer.Ordinal) { [member] = value }
        );
}
