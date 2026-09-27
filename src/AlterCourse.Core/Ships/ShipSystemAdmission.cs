using System.Globalization;

namespace AlterCourse.Core.Ships;

/// <summary>
/// Typed simulation admission for installed systems: the current rules accept at most one installation per kind.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="InstalledSystemCollection"/> storage validation. Common storage,
/// allocation, repair references, and snapshots may hold several installations of one kind; only typed rules
/// that read "the" sensor, "the" shield, and so on need a singleton, and they obtain it exclusively through
/// <see cref="SupportedSingle"/>. The rule is uniform over whichever kinds are present, so no kind list is
/// maintained. Enforced by bootstrap, the ship content loader, and <c>SimulationState.Validate</c> (every commit
/// and every load).
/// </remarks>
internal static class ShipSystemAdmission
{
    /// <summary>Throws when any kind has more than one installation.</summary>
    internal static void ValidateSupportedCardinality(InstalledSystemCollection systems)
    {
        ArgumentNullException.ThrowIfNull(systems);
        foreach (
            IGrouping<ShipSystemKind, InstalledSystem> group in systems.InCommonOrder.GroupBy(system => system.Kind)
        )
        {
            InstalledSystem[] members = [.. group];
            if (members.Length > 1)
            {
                throw new InvalidOperationException(Message(group.Key, members));
            }
        }
    }

    /// <summary>
    /// Gets the sole installation of a kind, or <see langword="null"/> when absent; several is an admission breach.
    /// </summary>
    /// <remarks>Never a first-match: silently choosing one of several would hide an invalid world.</remarks>
    internal static InstalledSystem? SupportedSingle(InstalledSystemCollection systems, ShipSystemKind kind)
    {
        ArgumentNullException.ThrowIfNull(systems);
        IReadOnlyList<InstalledSystem> members = systems.OfKind(kind);
        return members.Count switch
        {
            0 => null,
            1 => members[0],
            _ => throw new InvalidOperationException(Message(kind, members)),
        };
    }

    private static string Message(ShipSystemKind kind, IReadOnlyList<InstalledSystem> members) =>
        $"The current simulation admits at most one installed '{kind.Value}' system; found {members.Count} "
        + $"(ids {string.Join(", ", members.Select(system => system.Id.Value.ToString(CultureInfo.InvariantCulture)))}).";
}
