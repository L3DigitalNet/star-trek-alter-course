using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Persistence;

/// <summary>
/// Produces and checks the persisted descriptor of the catalog-derived aim-kind vocabulary (the kinds a remote shot
/// may aim at, in selector order).
/// </summary>
/// <remarks>
/// <para>
/// Why a separate descriptor: V10's definition table covers only definitions some installation references, but the
/// aim vocabulary (<c>SystemDefinitionCatalog.DamageTargetKinds</c>) is derived from the whole catalog. A catalog-only
/// change — adding or removing an uninstalled damage-participating definition, or changing an uninstalled
/// definition's order — would otherwise silently change which aim kinds are legal, and their order, for a loaded
/// world. Recomputing on load was rejected for that reason, and persisting only the set was rejected because the
/// selector order is observable.
/// </para>
/// <para>
/// Contract (persisted; changing the rendering requires a new descriptor version): <c>av1;</c> followed by the kind
/// strings in list order joined by <c>,</c>; <c>av1;</c> alone for an empty vocabulary. At most
/// <see cref="ShipSystemLimits.MaximumInstalledSystemsPerShip"/> distinct valid kinds and
/// <see cref="MaximumLength"/> characters from <c>[a-z0-9;,-]</c>.
/// </para>
/// <para>
/// Lives in Persistence rather than Content because saves are its only consumer.
/// </para>
/// </remarks>
internal static class AimVocabularySemantics
{
    internal const string DescriptorVersion = "av1";

    internal const int MaximumLength = 1_024;

    internal const int MaximumEntries = ShipSystemLimits.MaximumInstalledSystemsPerShip;

    /// <summary>
    /// The vocabulary every V1–V9 save was played under: the pre-substrate static aim list, in its order. Frozen —
    /// it describes history, so it must never be derived from current content.
    /// </summary>
    internal const string HistoricalV9 =
        "av1;power-generation,sensors,impulse-propulsion,shields,directed-energy-weapons";

    private const string Prefix = DescriptorVersion + ";";

    /// <summary>Returns the canonical descriptor of an ordered aim-kind vocabulary.</summary>
    internal static string Describe(IReadOnlyList<ShipSystemKind> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        string descriptor = Prefix + string.Join(',', kinds.Select(kind => kind.Value));
        ValidateFormat(descriptor);
        return descriptor;
    }

    /// <summary>
    /// Validates an untrusted descriptor's format: prefix, bounds, charset, and distinct parsable kinds.
    /// </summary>
    /// <exception cref="InvalidOperationException">The descriptor is malformed (invalid data, not incompatibility).</exception>
    internal static void ValidateFormat(string? descriptor)
    {
        if (descriptor is null)
            throw new InvalidOperationException("The aim-vocabulary descriptor is required.");
        if (descriptor.Length > MaximumLength)
            throw new InvalidOperationException($"The aim-vocabulary descriptor exceeds {MaximumLength} characters.");
        if (!descriptor.StartsWith(Prefix, StringComparison.Ordinal) || !descriptor.All(IsDescriptorCharacter))
            throw new InvalidOperationException("The aim-vocabulary descriptor has an unsupported format.");

        string body = descriptor[Prefix.Length..];
        if (body.Length == 0)
            return;
        string[] entries = body.Split(',');
        if (entries.Length > MaximumEntries)
            throw new InvalidOperationException($"The aim vocabulary exceeds {MaximumEntries} kinds.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string entry in entries)
        {
            try
            {
                _ = ShipSystemKind.Parse(entry);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException($"The aim vocabulary names unknown kind '{entry}'.", exception);
            }

            if (!seen.Add(entry))
                throw new InvalidOperationException($"The aim vocabulary lists '{entry}' twice.");
        }
    }

    private static bool IsDescriptorCharacter(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9' or ';' or ',' or '-';
}
