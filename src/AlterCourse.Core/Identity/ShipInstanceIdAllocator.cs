namespace AlterCourse.Core.Identity;

/// <summary>Allocates deterministic ship identities while returning explicit next state.</summary>
public sealed record ShipInstanceIdAllocator
{
    private ShipInstanceIdAllocator(long nextId)
    {
        NextId = nextId;
    }

    /// <summary>Gets the next persisted identity value.</summary>
    public long NextId { get; }

    /// <summary>Creates an allocator whose first identity is one.</summary>
    /// <returns>A new deterministic allocator.</returns>
    public static ShipInstanceIdAllocator Create() => new(1);

    /// <summary>Restores an allocator from its explicit next identity value.</summary>
    /// <param name="nextId">The positive next identity value.</param>
    /// <returns>The restored allocator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextId"/> is not positive.</exception>
    public static ShipInstanceIdAllocator Restore(long nextId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextId);
        return new ShipInstanceIdAllocator(nextId);
    }

    /// <summary>Allocates the next identity and returns the allocator state that follows it.</summary>
    /// <returns>The following allocator state and allocated identity.</returns>
    /// <exception cref="OverflowException">No following identity value can be represented.</exception>
    public (ShipInstanceIdAllocator Allocator, ShipInstanceId Id) Allocate()
    {
        ShipInstanceId id = new(NextId);
        long followingId = checked(NextId + 1);
        return (new ShipInstanceIdAllocator(followingId), id);
    }
}
