using System.Diagnostics.CodeAnalysis;

namespace AlterCourse.Core.Ships;

/// <summary>
/// Issues ship-local installed-system identities monotonically from an explicit, persisted continuation value.
/// </summary>
/// <remarks>
/// <para>
/// The continuation is always carried explicitly (content, explicit starts, runtime, saves) and never recomputed
/// as the highest retained identity plus one: removed installations leave gaps, and reusing a removed identity
/// would let stale references silently bind to a different installation.
/// </para>
/// <para>
/// Unlike the ship and order allocators, exhaustion is a legal, restorable state (<see cref="NextId"/> equal to
/// <see cref="long.MaxValue"/>) rather than an overflow, so a ship that has consumed every identity can still be
/// saved and restored, and a refused allocation is observable without any partial mutation.
/// </para>
/// </remarks>
public sealed record InstalledSystemIdAllocator
{
    private InstalledSystemIdAllocator(long nextId)
    {
        NextId = nextId;
    }

    /// <summary>Gets the next identity value to issue, or <see cref="long.MaxValue"/> when exhausted.</summary>
    public long NextId { get; }

    /// <summary>Gets whether every identity through <see cref="InstalledSystemId.MaximumValue"/> has been issued.</summary>
    public bool IsExhausted => NextId == long.MaxValue;

    /// <summary>Creates an allocator whose first identity is one.</summary>
    public static InstalledSystemIdAllocator Create() => new(1);

    /// <summary>Restores an allocator from its explicit continuation value, including the exhausted state.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextId"/> is not positive.</exception>
    public static InstalledSystemIdAllocator Restore(long nextId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextId);
        return new InstalledSystemIdAllocator(nextId);
    }

    /// <summary>
    /// Issues the next identity and returns the allocator state that follows it. Returns <see langword="false"/>
    /// with default outputs when exhausted; this allocator is immutable, so a refusal changes nothing.
    /// </summary>
    public bool TryAllocate([MaybeNullWhen(false)] out InstalledSystemIdAllocator following, out InstalledSystemId id)
    {
        if (IsExhausted)
        {
            following = null;
            id = default;
            return false;
        }

        // NextId is at most MaximumValue here, so both the identity and its successor (at most long.MaxValue)
        // are representable; checked arithmetic guards the invariant rather than an expected path.
        id = new InstalledSystemId(NextId);
        following = new InstalledSystemIdAllocator(checked(NextId + 1));
        return true;
    }
}
