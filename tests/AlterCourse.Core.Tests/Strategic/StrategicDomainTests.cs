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
        var location = new StrategicLocation(id, "Vesper Reach", new StrategicMapPosition(-12.75, 4.125));

        Assert.Equal("vesper-reach", location.Id.Value);
        Assert.Equal(-12.75, location.Position.X);
        Assert.Equal(4.125, location.Position.Y);
        Assert.Throws<ArgumentException>(() => new LocationId(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategicMapPosition(double.NaN, 0));
        Assert.Equal(128, new LocationId(new string('i', 128)).Value.Length);
        Assert.Equal(
            64,
            new StrategicLocation(new LocationId("maximum-name"), new string('n', 64), default).DisplayName.Length
        );
        Assert.Throws<ArgumentException>(() => new LocationId(new string('i', 129)));
        Assert.Throws<ArgumentException>(() =>
            new StrategicLocation(new LocationId("oversized-name"), new string('n', 65), default)
        );
    }

    /// <summary>Confirms durable strategic identities use the compact ASCII wire alphabet.</summary>
    [Theory]
    [InlineData("non location")]
    [InlineData("non/location")]
    [InlineData("non:location")]
    [InlineData("nonélocation")]
    [InlineData("non\u0001location")]
    public void LocationIdentityRejectsCharactersOutsideDurableAlphabet(string identity)
    {
        Assert.Throws<ArgumentException>(() => new LocationId(identity));
        Assert.Equal("AZaz09-_.", new LocationId("AZaz09-_.").Value);
    }

    /// <summary>Confirms map construction rejects ambiguous locations and invalid connections.</summary>
    [Fact]
    public void MapRejectsDuplicateLocationsAndInvalidRouteEndpoints()
    {
        StrategicLocation alpha = Location("alpha", 0, 0);
        StrategicLocation beta = Location("beta", 1, 1);

        Assert.Throws<ArgumentException>(() => new StrategicMap([alpha, alpha], []));
        Assert.Throws<ArgumentException>(() =>
            new StrategicMap(
                [alpha, beta],
                [new StrategicRoute(alpha.Id, new LocationId("missing"), new SimulationDuration(100))]
            )
        );
        Assert.Throws<ArgumentException>(() => new StrategicRoute(alpha.Id, alpha.Id, new SimulationDuration(100)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StrategicRoute(alpha.Id, beta.Id, new SimulationDuration(0))
        );
        Assert.Throws<ArgumentException>(() => new StrategicRoute(alpha.Id, beta.Id, new SimulationDuration(150)));
    }

    /// <summary>Confirms map order remains authored while hostile enumerables stop at finite count bounds.</summary>
    [Fact]
    public void MapPreservesAuthoredOrderAndBoundsEnumerableMaterialization()
    {
        StrategicLocation alpha = Location("alpha", 0, 0);
        StrategicLocation beta = Location("beta", 1, 1);
        StrategicLocation gamma = Location("gamma", 2, 2);
        var first = new StrategicRoute(beta.Id, gamma.Id, new SimulationDuration(100));
        var second = new StrategicRoute(alpha.Id, beta.Id, new SimulationDuration(200));
        var map = new StrategicMap([gamma, alpha, beta], [first, second]);

        Assert.Equal([gamma.Id, alpha.Id, beta.Id], map.Locations.Select(location => location.Id));
        Assert.Equal(
            [(first.Origin, first.Destination), (second.Origin, second.Destination)],
            map.Routes.Select(route => (route.Origin, route.Destination))
        );
        Assert.Throws<ArgumentException>(() => new StrategicMap(OverflowAfter(alpha, 257), []));
        Assert.Throws<ArgumentException>(() => new StrategicMap([alpha, beta], OverflowAfter(second, 1025)));
    }

    private static StrategicLocation Location(string id, double x, double y) =>
        new(new LocationId(id), id, new StrategicMapPosition(x, y));

    private static IEnumerable<T> OverflowAfter<T>(T value, int yieldedCount)
    {
        for (int index = 0; index < yieldedCount; index++)
        {
            yield return value;
        }

        throw new InvalidOperationException("The bounded consumer enumerated past its rejection threshold.");
    }
}
