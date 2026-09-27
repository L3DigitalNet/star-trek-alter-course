using System.Globalization;
using System.Text.Json;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Content;

/// <summary>
/// Reads a schema-validated ship-content V6 <c>initialLoadout</c> object and resolves its definition references
/// against a fully loaded <see cref="SystemDefinitionCatalog"/>.
/// </summary>
/// <remarks>
/// <para>
/// Contract with the V6 ship loader: call only after the document passed <c>ship-definition-v6</c> schema
/// validation (which proves the members exist, the id ranges, and the 16-item bound) and after the system catalog
/// is complete, so an unresolved reference means the content is wrong rather than not yet loaded.
/// </para>
/// <para>
/// Storage rules only: duplicate installed identities, a continuation that does not exceed every listed identity,
/// and unresolved references. Per-kind cardinality is a separate typed-admission step owned by the ship loader.
/// Installed identities come from the document, never from array position, and the resulting loadout is canonical
/// (ascending identity), so reordered entries produce an equal loadout.
/// </para>
/// </remarks>
internal static class SystemDefinitionLoadoutReader
{
    /// <summary>
    /// Gets the largest installed identity authored content may use; the V6 schema repeats it. Runtime and saves use
    /// the full <see cref="InstalledSystemId"/> range.
    /// </summary>
    internal const long MaximumAuthoredInstalledSystemId = 1_000_000;

    /// <summary>
    /// Returns the loadout, or <see langword="null"/> after appending at least one diagnostic.
    /// <paramref name="pointer"/> is the JSON pointer of the <c>initialLoadout</c> object, for example
    /// <c>#/initialLoadout</c>.
    /// </summary>
    internal static ShipLoadoutDefinition? Read(
        JsonElement initialLoadout,
        string pointer,
        SystemDefinitionCatalog catalog,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(diagnostics);
        int before = diagnostics.Count;
        string nextPointer = pointer + "/nextInstalledSystemId";
        bool nextRead = TryReadContinuation(initialLoadout, nextPointer, source, diagnostics, out long nextId);

        JsonElement systems = initialLoadout.GetProperty("systems");
        if (systems.GetArrayLength() > ShipSystemLimits.MaximumInstalledSystemsPerShip)
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    pointer + "/systems",
                    $"A loadout supports at most {ShipSystemLimits.MaximumInstalledSystemsPerShip} installed systems."
                )
            );
            return null;
        }

        var installed = new List<InitialInstalledSystem>();
        var seen = new Dictionary<long, int>();
        int index = 0;
        foreach (JsonElement entry in systems.EnumerateArray())
        {
            string entryPointer = pointer + "/systems/" + index.ToString(CultureInfo.InvariantCulture);
            InitialInstalledSystem? system = ReadEntry(entry, entryPointer, index, catalog, source, seen, diagnostics);
            if (system is not null)
            {
                installed.Add(system);
            }

            index++;
        }

        // Every in-range listed identity counts, including entries rejected for another reason, so all failures
        // in one loadout are reported together.
        long largestId = seen.Count > 0 ? seen.Keys.Max() : 0;

        // Validated, never recomputed: a continuation at or below a retained identity would let a later allocation
        // reissue it, and max + 1 would erase the gaps left by removed installations.
        if (nextRead && largestId > 0 && nextId <= largestId)
        {
            diagnostics.Add(
                new ShipContentDiagnostic(
                    "loadout.allocator-behind",
                    source,
                    nextPointer,
                    string.Empty,
                    $"Next installed system identity {nextId.ToString(CultureInfo.InvariantCulture)} must exceed the "
                        + $"largest listed identity {largestId.ToString(CultureInfo.InvariantCulture)}."
                )
            );
        }

        return diagnostics.Count > before ? null : new ShipLoadoutDefinition(nextId, installed);
    }

    private static bool TryReadContinuation(
        JsonElement initialLoadout,
        string nextPointer,
        string source,
        List<ShipContentDiagnostic> diagnostics,
        out long nextId
    )
    {
        if (
            !StrictContentJson.TryReadInt64(
                initialLoadout.GetProperty("nextInstalledSystemId"),
                nextPointer,
                source,
                diagnostics,
                out nextId
            )
        )
        {
            return false;
        }

        if (nextId is < 1 or > MaximumAuthoredInstalledSystemId + 1)
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    nextPointer,
                    $"Next installed system identity must be from 1 through {MaximumAuthoredInstalledSystemId + 1}."
                )
            );
            return false;
        }

        return true;
    }

    private static InitialInstalledSystem? ReadEntry(
        JsonElement entry,
        string entryPointer,
        int index,
        SystemDefinitionCatalog catalog,
        string source,
        Dictionary<long, int> seen,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        int before = diagnostics.Count;
        long id = ReadInstalledId(entry, entryPointer + "/installedSystemId", index, source, seen, diagnostics);
        SystemDefinitionId definitionId = ResolveDefinition(
            entry.GetProperty("definitionId").GetString()!,
            entryPointer + "/definitionId",
            catalog,
            source,
            diagnostics
        );
        return diagnostics.Count > before ? null : new InitialInstalledSystem(new InstalledSystemId(id), definitionId);
    }

    private static long ReadInstalledId(
        JsonElement entry,
        string idPointer,
        int index,
        string source,
        Dictionary<long, int> seen,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (
            !StrictContentJson.TryReadInt64(
                entry.GetProperty("installedSystemId"),
                idPointer,
                source,
                diagnostics,
                out long id
            )
        )
        {
            return 0;
        }

        if (id is < 1 or > MaximumAuthoredInstalledSystemId)
        {
            diagnostics.Add(
                StrictContentJson.Semantic(
                    source,
                    idPointer,
                    $"Installed system identity must be from 1 through {MaximumAuthoredInstalledSystemId}."
                )
            );
        }
        else if (seen.TryGetValue(id, out int firstIndex))
        {
            diagnostics.Add(
                new ShipContentDiagnostic(
                    "loadout.duplicate-installed-id",
                    source,
                    idPointer,
                    string.Empty,
                    $"Installed system identity {id.ToString(CultureInfo.InvariantCulture)} duplicates "
                        + $"systems/{firstIndex.ToString(CultureInfo.InvariantCulture)}."
                )
            );
        }
        else
        {
            seen.Add(id, index);
        }

        return id;
    }

    private static SystemDefinitionId ResolveDefinition(
        string definitionText,
        string definitionPointer,
        SystemDefinitionCatalog catalog,
        string source,
        List<ShipContentDiagnostic> diagnostics
    )
    {
        if (!IsValidDefinitionId(definitionText))
        {
            diagnostics.Add(
                StrictContentJson.Semantic(source, definitionPointer, "System definition identity is malformed.")
            );
            return default;
        }

        var definitionId = new SystemDefinitionId(definitionText);
        if (!catalog.TryGet(definitionId, out _))
        {
            diagnostics.Add(
                new ShipContentDiagnostic(
                    "reference.unresolved-definition",
                    source,
                    definitionPointer,
                    string.Empty,
                    $"System definition '{definitionText}' is not in the loaded system definition catalog."
                )
            );
        }

        return definitionId;
    }

    private static bool IsValidDefinitionId(string text)
    {
        try
        {
            _ = new SystemDefinitionId(text);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
