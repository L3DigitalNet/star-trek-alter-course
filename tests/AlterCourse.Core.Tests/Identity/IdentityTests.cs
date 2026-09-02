using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Tests.Identity;

/// <summary>Verifies deterministic typed identity allocation.</summary>
public sealed class IdentityTests
{
    /// <summary>Confirms typed identities reject nonpositive persisted values.</summary>
    [Fact]
    public void TypedIdentitiesRejectNonpositiveValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShipInstanceId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScheduledWorkId(-1));
    }

    /// <summary>Confirms ship allocation is deterministic and returns explicit next state.</summary>
    [Fact]
    public void ShipIdentityAllocationIsDeterministicAndRestorable()
    {
        var initial = ShipInstanceIdAllocator.Create();

        (ShipInstanceIdAllocator afterFirst, ShipInstanceId first) = initial.Allocate();
        (ShipInstanceIdAllocator afterSecond, ShipInstanceId second) = afterFirst.Allocate();
        var restored = ShipInstanceIdAllocator.Restore(afterSecond.NextId);

        Assert.Equal(new ShipInstanceId(1), first);
        Assert.Equal(new ShipInstanceId(2), second);
        Assert.Equal(3, restored.NextId);
        Assert.Equal(new ShipInstanceId(3), restored.Allocate().Id);
        Assert.Equal(1, initial.NextId);
    }

    /// <summary>Confirms invalid or exhausted allocator state cannot allocate.</summary>
    [Fact]
    public void ShipIdentityAllocatorRejectsInvalidOrOverflowState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipInstanceIdAllocator.Restore(0));

        var exhausted = ShipInstanceIdAllocator.Restore(long.MaxValue);

        Assert.Throws<OverflowException>(() => exhausted.Allocate());
    }
}
