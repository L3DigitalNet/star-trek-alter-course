using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Strategic;

/// <summary>Verifies stable continuous strategic map definitions and route validation.</summary>
public sealed class StrategicDomainTests
{
    /// <summary>Confirms strategic locations are stable identities at arbitrary finite coordinates.</summary>
    [Fact]
    public void StableLocationsSupportArbitraryNonGridPositions()
    {
        var id = new LocationId("vesper-reach");
        var location = new StrategicLocation(
            id,
            "Vesper Reach",
            new StrategicMapPosition(-12.75, 4.125)
        );

        Assert.Equal("vesper-reach", location.Id.Value);
        Assert.Equal(-12.75, location.Position.X);
        Assert.Equal(4.125, location.Position.Y);
        Assert.Throws<ArgumentException>(() => new LocationId(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategicMapPosition(double.NaN, 0));
    }

    /// <summary>Confirms map construction rejects ambiguous locations and invalid connections.</summary>
    [Fact]
    public void MapRejectsDuplicateLocationsAndInvalidRouteEndpoints()
    {
        StrategicLocation alpha = Location("alpha", 0, 0);
        StrategicLocation beta = Location("beta", 1, 1);

        Assert.Throws<ArgumentException>(() => new StrategicMap([alpha, alpha], []));
        Assert.Throws<ArgumentException>(
            () =>
                new StrategicMap(
                    [alpha, beta],
                    [new StrategicRoute(alpha.Id, new LocationId("missing"), new SimulationDuration(100))]
                )
        );
        Assert.Throws<ArgumentException>(
            () =>
                new StrategicRoute(alpha.Id, alpha.Id, new SimulationDuration(100))
        );
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StrategicRoute(alpha.Id, beta.Id, new SimulationDuration(0))
        );
        Assert.Throws<ArgumentException>(
            () => new StrategicRoute(alpha.Id, beta.Id, new SimulationDuration(150))
        );
    }

    private static StrategicLocation Location(string id, double x, double y) =>
        new(new LocationId(id), id, new StrategicMapPosition(x, y));
}
