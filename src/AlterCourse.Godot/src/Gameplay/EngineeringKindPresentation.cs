using System.Collections.Immutable;
using AlterCourse.Core.Player;
using AlterCourse.Core.Ships;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// The single Godot table of kind-keyed Engineering display text: labels, message nouns, and inspector field layout
/// for the five production kinds, reproducing the pre-substrate UI strings byte for byte.
/// </summary>
/// <remarks>
/// <para>
/// SCOPE: presentation text only. The table decides no availability, ordering, membership, payload, or rule — rows
/// and buttons iterate Core's <see cref="EngineeringProjection.Systems"/> and <see cref="EngineeringProjection.Actions"/>
/// in the order given, and legality comes from Core. That is why it is not a common mechanism under ADR 0014.
/// </para>
/// <para>
/// Fallback: a kind without an entry uses the installation's authored <see cref="InstalledSystemProjection.ComponentLabel"/>
/// and the common field layout, so a new definition of an existing kind, or a later kind, renders without editing
/// this file. The gdUnit label-pinning test fixes every production string and the button order.
/// </para>
/// </remarks>
public static class EngineeringKindPresentation
{
    private static readonly ImmutableDictionary<string, Entry> Entries = new Dictionary<string, Entry>(
        StringComparer.Ordinal
    )
    {
        [ShipSystemKind.PowerGeneration.Value] = new(
            "POWER",
            null,
            null,
            null,
            null,
            null,
            "Power generation",
            "Power generation",
            null,
            FieldLayout.Generation
        ),
        [ShipSystemKind.Sensors.Value] = new(
            "SENSORS",
            "SENSORS",
            "SENSORS",
            "Prioritize sensors",
            "Sensor-priority allocation",
            "Begin sensor repair",
            "Sensors",
            "Sensor",
            "Allocation unavailable: sensor demand would be exceeded.",
            FieldLayout.Sensors
        ),
        [ShipSystemKind.ImpulsePropulsion.Value] = new(
            "PROPULSION",
            "IMPULSE PROPULSION",
            "PROPULSION",
            "Prioritize propulsion",
            "Propulsion-priority allocation",
            "Begin impulse repair",
            "Impulse propulsion",
            "Impulse propulsion",
            "Allocation unavailable: propulsion demand would be exceeded.",
            FieldLayout.ImpulsePropulsion
        ),
        [ShipSystemKind.Shields.Value] = new(
            "SHIELDS",
            "SHIELDS",
            "SHIELDS",
            "Prioritize shields",
            "Shield-priority allocation",
            "Begin shield repair",
            "Shields",
            "Shield",
            null,
            FieldLayout.Common
        ),
        [ShipSystemKind.DirectedEnergyWeapons.Value] = new(
            "DIRECTED-ENERGY WEAPONS",
            "DIRECTED-ENERGY WEAPONS",
            "DIRECTED-ENERGY WEAPONS",
            "Prioritize weapons",
            "Weapon-priority allocation",
            "Begin weapon repair",
            "Directed-energy weapons",
            "Directed-energy weapon",
            null,
            FieldLayout.Common
        ),
    }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Identifies which inspector field set an installation's section shows.</summary>
    public enum FieldLayout
    {
        /// <summary>CONDITION / ALLOCATION / DEMAND / CAPABILITY, omitting consumer fields for nonconsumers.</summary>
        Common = 0,

        /// <summary>NOMINAL / AVAILABLE / CONDITION / RESERVE from ship-level supply totals.</summary>
        Generation = 1,

        /// <summary>CONDITION / ALLOCATION / CAPABILITY / PASSIVE RANGE.</summary>
        Sensors = 2,

        /// <summary>CONDITION / ALLOCATION / CAPABILITY / MAX TACTICAL SPEED.</summary>
        ImpulsePropulsion = 3,
    }

    /// <summary>Gets the Engineering hierarchy row label.</summary>
    public static string HierarchyLabel(InstalledSystemProjection row) =>
        Find(row.Kind)?.HierarchyLabel ?? Upper(row.ComponentLabel);

    /// <summary>Gets the CONNECTED LOADS field label for a consumer.</summary>
    public static string ConnectedLoadLabel(InstalledSystemProjection row) =>
        Find(row.Kind)?.ConnectedLoadLabel ?? Upper(row.ComponentLabel);

    /// <summary>Gets the POWER ALLOCATION SUMMARY field label for a consumer.</summary>
    public static string AllocationSummaryLabel(InstalledSystemProjection row) =>
        Find(row.Kind)?.AllocationSummaryLabel ?? Upper(row.ComponentLabel);

    /// <summary>Gets the Prioritize button label.</summary>
    public static string PrioritizeLabel(InstalledSystemProjection row) =>
        Find(row.Kind)?.PrioritizeLabel ?? $"Prioritize {row.ComponentLabel}";

    /// <summary>Gets the noun phrase used in "… applied." after an accepted priority allocation.</summary>
    public static string PriorityMessageLabel(InstalledSystemProjection row) =>
        Find(row.Kind)?.PriorityMessageLabel ?? $"{row.ComponentLabel}-priority allocation";

    /// <summary>Gets the Begin Repair button label.</summary>
    public static string RepairLabel(InstalledSystemProjection row) =>
        Find(row.Kind)?.RepairLabel ?? $"Begin {row.ComponentLabel} repair";

    /// <summary>Gets the system label used by the aim selector, repair target, and activity log.</summary>
    public static string TargetLabel(ShipSystemKind? kind, string? componentLabel = null) =>
        (kind is { } known ? Find(known)?.TargetLabel : null) ?? componentLabel ?? "System";

    /// <summary>Gets the message noun used in "… repair started." and repair-completion notices.</summary>
    public static string MessageNoun(ShipSystemKind? kind, string? componentLabel = null) =>
        (kind is { } known ? Find(known)?.MessageNoun : null) ?? componentLabel ?? "System";

    /// <summary>
    /// Gets the refusal text for a consumer-demand rejection naming the offending consumer's kind. Kinds that never
    /// had a dedicated message keep the pre-substrate generic refusal.
    /// </summary>
    public static string DemandExceededMessage(ShipSystemKind? kind) =>
        (kind is { } known ? Find(known)?.DemandExceededMessage : null) ?? "Power allocation was not accepted.";

    /// <summary>Gets the inspector field layout for an installation.</summary>
    public static FieldLayout Layout(InstalledSystemProjection row) => Find(row.Kind)?.Layout ?? FieldLayout.Common;

    private static Entry? Find(ShipSystemKind kind) => Entries.GetValueOrDefault(kind.Value);

    private static string Upper(string label) => label.ToUpperInvariant();

    private sealed record Entry(
        string HierarchyLabel,
        string? ConnectedLoadLabel,
        string? AllocationSummaryLabel,
        string? PrioritizeLabel,
        string? PriorityMessageLabel,
        string? RepairLabel,
        string TargetLabel,
        string MessageNoun,
        string? DemandExceededMessage,
        FieldLayout Layout
    );
}
