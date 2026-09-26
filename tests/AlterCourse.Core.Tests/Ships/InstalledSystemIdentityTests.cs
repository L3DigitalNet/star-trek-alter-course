using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies the kind-independent identity types and the explicit installed-system allocator contract.</summary>
public sealed class InstalledSystemIdentityTests
{
    /// <summary>Confirms definition identities accept the full ASCII charset up to the shared 64-character bound.</summary>
    [Theory]
    [InlineData("pathfinder.sensors")]
    [InlineData("A-z_0.9")]
    [InlineData("x")]
    public void SystemDefinitionIdAcceptsBoundedAsciiIdentities(string value)
    {
        Assert.Equal(value, new SystemDefinitionId(value).Value);
        string maximum = new('d', SystemDefinitionId.MaximumLength);
        Assert.Equal(maximum, new SystemDefinitionId(maximum).Value);
    }

    /// <summary>Confirms definition identities reject empty, oversize, and non-ASCII-token text.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has space")]
    [InlineData("slash/separated")]
    [InlineData("colon:separated")]
    [InlineData("café")]
    public void SystemDefinitionIdRejectsInvalidText(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SystemDefinitionId(value));
    }

    /// <summary>Confirms the length bound is exact and a null value is refused.</summary>
    [Fact]
    public void SystemDefinitionIdRejectsOversizeAndNull()
    {
        Assert.Throws<ArgumentException>(() =>
            new SystemDefinitionId(new string('d', SystemDefinitionId.MaximumLength + 1))
        );
        Assert.Throws<ArgumentNullException>(() => new SystemDefinitionId(null!));
    }

    /// <summary>Confirms the installed identity range is 1 through long.MaxValue - 1.</summary>
    [Fact]
    public void InstalledSystemIdEnforcesItsRange()
    {
        Assert.Equal(1, new InstalledSystemId(1).Value);
        Assert.Equal(long.MaxValue - 1, InstalledSystemId.MaximumValue);
        Assert.Equal(InstalledSystemId.MaximumValue, new InstalledSystemId(InstalledSystemId.MaximumValue).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new InstalledSystemId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InstalledSystemId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InstalledSystemId(long.MinValue));
        // long.MaxValue is reserved as the exhausted allocator continuation, never an identity.
        Assert.Throws<ArgumentOutOfRangeException>(() => new InstalledSystemId(long.MaxValue));
    }

    /// <summary>Confirms the same local identity on two ships forms two distinct global addresses.</summary>
    [Fact]
    public void AddressesPairShipAndLocalIdentity()
    {
        var local = new InstalledSystemId(2);
        var onShipOne = new ShipSystemAddress(new ShipInstanceId(1), local);
        var onShipFour = new ShipSystemAddress(new ShipInstanceId(4), local);

        Assert.NotEqual(onShipOne, onShipFour);
        Assert.Equal(onShipOne, new ShipSystemAddress(new ShipInstanceId(1), new InstalledSystemId(2)));
        Assert.NotEqual(onShipOne, new ShipSystemAddress(new ShipInstanceId(1), new InstalledSystemId(3)));
    }

    /// <summary>Confirms creation starts at one and allocation returns explicit successor state without mutation.</summary>
    [Fact]
    public void AllocatorCreatesAtOneAndContinuesExplicitly()
    {
        var allocator = InstalledSystemIdAllocator.Create();
        Assert.Equal(1, allocator.NextId);
        Assert.False(allocator.IsExhausted);

        Assert.True(allocator.TryAllocate(out InstalledSystemIdAllocator? following, out InstalledSystemId first));
        Assert.Equal(new InstalledSystemId(1), first);
        Assert.Equal(2, following.NextId);
        Assert.Equal(1, allocator.NextId);

        Assert.True(following.TryAllocate(out InstalledSystemIdAllocator? third, out InstalledSystemId second));
        Assert.Equal(new InstalledSystemId(2), second);
        Assert.Equal(3, third.NextId);
    }

    /// <summary>
    /// Confirms a restored continuation is honored verbatim: after retained ids 2, 17, 900 and a continuation of
    /// 5000, the next id is 5000 — not 901 (max + 1) and not a reused gap.
    /// </summary>
    [Fact]
    public void AllocatorRestoresContinuationWithoutRecomputingFromRetainedIds()
    {
        var restored = InstalledSystemIdAllocator.Restore(5000);

        Assert.True(restored.TryAllocate(out InstalledSystemIdAllocator? following, out InstalledSystemId id));
        Assert.Equal(5000, id.Value);
        Assert.Equal(5001, following.NextId);
        Assert.Equal(InstalledSystemIdAllocator.Restore(5000), restored);
    }

    /// <summary>Confirms restore bounds: zero and negative fail, while the exhausted value is legal.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void AllocatorRestoreRejectsNonPositiveContinuation(long next)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InstalledSystemIdAllocator.Restore(next));
    }

    /// <summary>
    /// Confirms the last identity can be issued, the resulting exhausted state is representable and restorable, and
    /// an exhausted allocator refuses without mutation instead of overflowing.
    /// </summary>
    [Fact]
    public void AllocatorExhaustsAtTheLastIdentityWithoutOverflow()
    {
        var last = InstalledSystemIdAllocator.Restore(InstalledSystemId.MaximumValue);
        Assert.False(last.IsExhausted);

        Assert.True(last.TryAllocate(out InstalledSystemIdAllocator? exhausted, out InstalledSystemId final));
        Assert.Equal(InstalledSystemId.MaximumValue, final.Value);
        Assert.Equal(long.MaxValue, exhausted.NextId);
        Assert.True(exhausted.IsExhausted);

        var restored = InstalledSystemIdAllocator.Restore(long.MaxValue);
        Assert.Equal(exhausted, restored);
        Assert.True(restored.IsExhausted);

        Assert.False(restored.TryAllocate(out InstalledSystemIdAllocator? refused, out InstalledSystemId none));
        Assert.Null(refused);
        Assert.Equal(default, none);
        Assert.Equal(long.MaxValue, restored.NextId);
        Assert.Equal(InstalledSystemIdAllocator.Restore(long.MaxValue), restored);
    }
}
