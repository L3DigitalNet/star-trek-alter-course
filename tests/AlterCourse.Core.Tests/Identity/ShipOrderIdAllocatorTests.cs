using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Tests.Identity;

/// <summary>Verifies deterministic ship-order identity allocation.</summary>
public sealed class ShipOrderIdAllocatorTests
{
    /// <summary>Confirms ship-order identities reject nonpositive persisted values.</summary>
    [Fact]
    public void ShipOrderIdentityRequiresPositiveValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipOrderId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipOrderId(-1));
        Assert.Equal(long.MaxValue, new ShipOrderId(long.MaxValue).Value);
    }

    /// <summary>Confirms allocation is unique, monotonic, restorable, and leaves prior state unchanged.</summary>
    [Fact]
    public void ShipOrderIdentityAllocationIsDeterministicAndRestorable()
    {
        var initial = ShipOrderIdAllocator.Create();

        (ShipOrderIdAllocator afterFirst, ShipOrderId first) = initial.Allocate();
        (ShipOrderIdAllocator afterSecond, ShipOrderId second) = afterFirst.Allocate();
        var restored = ShipOrderIdAllocator.Restore(afterSecond.NextId);

        Assert.Equal(new ShipOrderId(1), first);
        Assert.Equal(new ShipOrderId(2), second);
        Assert.NotEqual(first, second);
        Assert.Equal(3, restored.NextId);
        Assert.Equal(new ShipOrderId(3), restored.Allocate().Id);
        Assert.Equal(1, initial.NextId);
    }

    /// <summary>Confirms invalid state and the final representable allocation boundary fail explicitly.</summary>
    [Fact]
    public void ShipOrderIdentityAllocatorPreservesHeadroomForFollowingState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipOrderIdAllocator.Restore(0));

        var penultimate = ShipOrderIdAllocator.Restore(long.MaxValue - 1);
        (ShipOrderIdAllocator final, ShipOrderId id) = penultimate.Allocate();

        Assert.Equal(new ShipOrderId(long.MaxValue - 1), id);
        Assert.Equal(long.MaxValue, final.NextId);
        Assert.Throws<OverflowException>(() => final.Allocate());
        Assert.Equal(long.MaxValue, final.NextId);
    }
}
