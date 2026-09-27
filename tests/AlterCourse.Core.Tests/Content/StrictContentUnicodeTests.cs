using AlterCourse.Core.Content;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Content;

/// <summary>Malformed Unicode must fail as typed content validation before schema evaluation.</summary>
public sealed class StrictContentUnicodeTests
{
    /// <summary>Both property names and string values share the closed UTF-8/Unicode trust boundary.</summary>
    [Theory]
    [InlineData("{\"\\uD800\":0}")]
    [InlineData("{\"\\uDC00\":0}")]
    [InlineData("{\"id\":\"\\uD800\"}")]
    [InlineData("{\"id\":\"\\uDC00\"}")]
    public void EveryContentFamilyRejectsUnpairedSurrogates(string json)
    {
        ShipDefinitionCatalogLoader ships = TestShipContent.ShipLoader(TestShipContent.ProductionSystems());
        var systems = new SystemDefinitionCatalogLoader(
            TestShipContent.ReadRepositoryFile("src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json")
        );
        var factions = new FactionDefinitionCatalogLoader(
            TestShipContent.ReadRepositoryFile(
                "src/AlterCourse.Godot/content/schemas/faction-definition-v1.schema.json"
            )
        );

        ShipContentValidationException ship = Assert.Throws<ShipContentValidationException>(() =>
            ships.LoadText(json, "malformed-ship.json")
        );
        ShipContentValidationException system = Assert.Throws<ShipContentValidationException>(() =>
            systems.LoadCatalog([SystemDefinitionContent.FromText("malformed-system.json", json)])
        );
        FactionContentValidationException faction = Assert.Throws<FactionContentValidationException>(() =>
            factions.LoadText(json, "malformed-faction.json")
        );
        Assert.Equal("json.invalid", Assert.Single(ship.Diagnostics).Code);
        Assert.Equal("json.invalid", Assert.Single(system.Diagnostics).Code);
        Assert.Equal("json.invalid", Assert.Single(faction.Diagnostics).Code);
    }
}
