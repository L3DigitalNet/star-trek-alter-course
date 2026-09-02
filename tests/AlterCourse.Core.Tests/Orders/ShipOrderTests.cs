using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Orders;

/// <summary>Verifies the finite ship-order model and its persisted invariants.</summary>
public sealed class ShipOrderTests
{
    /// <summary>Confirms travel orders retain stable identity and one initialized destination.</summary>
    [Fact]
    public void TravelToOrderCarriesIdentityAndDestination()
    {
        var order = new TravelToOrder(new ShipOrderId(7), Location("destination"));

        Assert.Equal(new ShipOrderId(7), order.Id);
        Assert.Equal(ShipOrderKind.TravelTo, order.Kind);
        Assert.Equal(Location("destination"), order.Destination);
        Assert.Throws<ArgumentException>(() => new TravelToOrder(default, Location("destination")));
        Assert.Throws<ArgumentException>(() => new TravelToOrder(new ShipOrderId(1), default));
    }

    /// <summary>Confirms patrol orders preserve explicit cyclic progress in immutable copied storage.</summary>
    [Fact]
    public void PatrolRouteOrderCopiesImmutableWaypointState()
    {
        LocationId[] source = [Location("alpha"), Location("beta"), Location("gamma")];

        var order = new PatrolRouteOrder(new ShipOrderId(8), source, 2);
        source[2] = Location("changed");

        Assert.Equal(new ShipOrderId(8), order.Id);
        Assert.Equal(ShipOrderKind.PatrolRoute, order.Kind);
        Assert.Equal([Location("alpha"), Location("beta"), Location("gamma")], order.Waypoints.ToArray());
        Assert.Equal(2, order.NextWaypointIndex);
        Assert.Throws<NotSupportedException>(() => ((IList<LocationId>)order.Waypoints).Add(Location("delta")));
    }

    /// <summary>Confirms patrol construction rejects malformed bounded cycles and invalid progress.</summary>
    [Fact]
    public void PatrolRouteOrderRejectsInvalidWaypointState()
    {
        ShipOrderId id = new(1);
        LocationId alpha = Location("alpha");
        LocationId beta = Location("beta");

        Assert.Throws<ArgumentNullException>(() => new PatrolRouteOrder(id, null!, 0));
        Assert.Throws<ArgumentException>(() => new PatrolRouteOrder(default, [alpha, beta], 0));
        Assert.Throws<ArgumentException>(() => new PatrolRouteOrder(id, [alpha], 0));
        Assert.Throws<ArgumentException>(() => new PatrolRouteOrder(id, [alpha, default], 0));
        Assert.Throws<ArgumentException>(() => new PatrolRouteOrder(id, [alpha, alpha], 0));
        Assert.Throws<ArgumentException>(() => new PatrolRouteOrder(id, [alpha, beta, alpha], 0));
        Assert.Throws<ArgumentException>(() =>
            new PatrolRouteOrder(
                id,
                Enumerable.Range(0, PatrolRouteOrder.MaximumWaypointCount + 1).Select(index => Location($"p{index}")),
                0
            )
        );
        Assert.Throws<ArgumentOutOfRangeException>(() => new PatrolRouteOrder(id, [alpha, beta], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PatrolRouteOrder(id, [alpha, beta], 2));
    }

    /// <summary>Confirms hold orders retain the exact time and scheduled-work wake correlation.</summary>
    [Fact]
    public void HoldUntilOrderCarriesExactWakeCorrelation()
    {
        var until = new SimulationTime(12_345);
        var wakeId = new ScheduledWorkId(91);
        var order = new HoldUntilOrder(new ShipOrderId(9), until, wakeId);

        Assert.Equal(new ShipOrderId(9), order.Id);
        Assert.Equal(ShipOrderKind.HoldUntil, order.Kind);
        Assert.Equal(until, order.Until);
        Assert.Equal(wakeId, order.ScheduledWakeId);
        Assert.Throws<ArgumentException>(() => new HoldUntilOrder(default, until, wakeId));
        Assert.Throws<ArgumentException>(() => new HoldUntilOrder(new ShipOrderId(1), until, default));
    }

    private static LocationId Location(string value) => new(value);
}
