namespace AlterCourse.Core.Identity;

/// <summary>Allocates deterministic observation-report identities while returning explicit next state.</summary>
public sealed record ObservationReportIdAllocator
{
    private ObservationReportIdAllocator(long nextId) => NextId = nextId;

    /// <summary>Gets the next persisted identity value.</summary>
    public long NextId { get; }

    /// <summary>Creates an allocator whose first identity is one.</summary>
    public static ObservationReportIdAllocator Create() => new(1);

    /// <summary>Restores an allocator from its positive next identity value.</summary>
    public static ObservationReportIdAllocator Restore(long nextId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextId);
        return new ObservationReportIdAllocator(nextId);
    }

    /// <summary>Allocates the next identity and returns the allocator state that follows it.</summary>
    public (ObservationReportIdAllocator Allocator, ObservationReportId Id) Allocate()
    {
        ObservationReportId id = new(NextId);
        long followingId = checked(NextId + 1);
        return (new ObservationReportIdAllocator(followingId), id);
    }
}
