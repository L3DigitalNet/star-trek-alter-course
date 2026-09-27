using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Gameplay;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies bounded member-name admission and typed malformed-name failures.</summary>
public sealed class GamePersistenceMemberAdmissionTests
{
    /// <summary>A small unknown-name payload cannot amplify duplicate-walk allocations through array paths.</summary>
    [Fact]
    public void UnknownLongNameFailsWithoutRepeatedLargePathAllocations()
    {
        var fixture = new Milestone3ProofFixture();
        string json = "{\"" + new string('a', 2048) + "\":[" + string.Join(',', Enumerable.Repeat("0", 2048)) + "]}";
        byte[] input = Encoding.UTF8.GetBytes(json);
        long before = GC.GetAllocatedBytesForCurrentThread();
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(input, fixture.Catalog, "amplification.json")
        );
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.InRange(allocated, 0, 1024 * 1024);
    }

    /// <summary>Both public entry points reject malformed names without changing the live world or existing save.</summary>
    [Theory]
    [InlineData("utf8")]
    [InlineData("high-surrogate")]
    [InlineData("low-surrogate")]
    [InlineData("unpaired-surrogate")]
    public void MalformedNamesReturnTypedInvalidDataAndPreserveState(string mutation)
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation live = fixture.CreateDefault();
        byte[] before = GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata);
        byte[] input = MalformedName(mutation);
        GamePersistenceException direct = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(input, fixture.Catalog, "malformed-name.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, direct.Failure);
        string directory = Path.Combine(Path.GetTempPath(), "name-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string slot = Path.Combine(directory, "valid.json");
            string malformed = Path.Combine(directory, "malformed.json");
            File.WriteAllBytes(slot, before);
            File.WriteAllBytes(malformed, input);
            GamePersistenceException loaded = Assert.Throws<GamePersistenceException>(() =>
                GamePersistence.Load(malformed, fixture.Catalog)
            );
            Assert.Equal(GamePersistenceFailure.InvalidData, loaded.Failure);
            Assert.Equal(input, File.ReadAllBytes(malformed));
            Assert.Equal(before, File.ReadAllBytes(slot));
            _ = GamePersistence.Load(slot, fixture.Catalog);
            Assert.Equal(before, GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Every supported DTO wire member fits the name bound, including the longest historical property.</summary>
    [Fact]
    public void MemberNameCeilingMatchesTheCompleteSupportedDtoInventory()
    {
        string[] names = typeof(GamePersistence)
            .Assembly.GetTypes()
            .Where(type => type.DeclaringType?.Name.StartsWith("SaveModelsV", StringComparison.Ordinal) == true)
            .SelectMany(type =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            )
            .Where(property =>
                property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always
            )
            .Select(property =>
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name)
            )
            .ToArray();
        Assert.NotEmpty(names);
        Assert.Equal(names.Max(name => name.Length), GamePersistence.MaximumJsonMemberNameLength);
        string longest = names.MaxBy(name => name.Length)!;
        string escaped = string.Concat(
            longest.Select(character => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture))
        );
        GamePersistence.ValidateInputBounds(Encoding.UTF8.GetBytes("{\"" + escaped + "\":0}"), "escaped-longest.json");
    }

    /// <summary>Duplicate diagnostics retain both object and array coordinates after lazy path rendering.</summary>
    [Theory]
    [InlineData("{\"metadata\":{\"saveId\":\"a\",\"saveId\":\"b\"}}", "$.metadata")]
    [InlineData(
        "{\"simulation\":{\"ships\":[{\"displayName\":\"a\",\"displayName\":\"b\"}]}}",
        "$.simulation.ships[0]"
    )]
    public void DuplicateFailuresKeepTheirNestedPath(string json, string expectedPath)
    {
        var fixture = new Milestone3ProofFixture();
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(json), fixture.Catalog, "duplicate.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Contains(expectedPath, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Many children under a legal-length name do not allocate a full diagnostic path per child.</summary>
    [Fact]
    public void DuplicateWalkDoesNotBuildPathsForEveryArrayItem()
    {
        var fixture = new Milestone3ProofFixture();
        byte[] input = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":10,\"observationReportAllocatorNextId\":["
                + string.Join(',', Enumerable.Repeat("0", 8192))
                + "]}"
        );
        // Warm the DTO metadata cache before measuring only this document's traversal allocations.
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(input, fixture.Catalog, "array-paths.json")
        );
        long before = GC.GetAllocatedBytesForCurrentThread();
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(input, fixture.Catalog, "array-paths.json")
        );
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.InRange(allocated, 0, 512 * 1024);
    }

    private static byte[] MalformedName(string mutation) =>
        mutation switch
        {
            "utf8" => [(byte)'{', (byte)'"', 0xff, (byte)'"', (byte)':', (byte)'0', (byte)'}'],
            "high-surrogate" => Encoding.UTF8.GetBytes("{\"\\uD800\":0}"),
            "low-surrogate" => Encoding.UTF8.GetBytes("{\"\\uDC00\":0}"),
            "unpaired-surrogate" => Encoding.UTF8.GetBytes("{\"\\uD800x\":0}"),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
}
