using System.Globalization;
using System.Text;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Content;

/// <summary>
/// Produces the canonical compatibility descriptor of a system definition: every simulation-relevant field in a
/// fixed order, so a save can prove that the definition it references still means what it meant when saved.
/// </summary>
/// <remarks>
/// <para>
/// Contract (persisted in saves; changing any rendering is a descriptor-version change from <c>sd1</c>): the
/// prefix <c>sd1</c>, then <c>kind</c>, <c>condition</c>, <c>order</c>, <c>power</c> (demand or <c>none</c>),
/// <c>repair</c> (full-repair milliseconds or <c>none</c>), then the kind's specialized fields, joined by
/// <c>;</c>. Doubles use the invariant round-trip format (<c>30</c>, <c>0.25</c>, <c>1E+20</c>), so the text is
/// culture-independent and distinguishes every representable value.
/// </para>
/// <para>
/// Excluded by design: the definition identity (it is the lookup key), <see cref="SystemDefinition.ComponentLabel"/>
/// and <see cref="SystemRepairCapability.ActionOrder"/> (presentation only). Renaming a label or reordering repair
/// buttons therefore never invalidates a save, while any tuning or ordering change that alters outcomes does.
/// </para>
/// </remarks>
public static class SystemDefinitionSemantics
{
    /// <summary>Gets the descriptor-format version prefix.</summary>
    public const string DescriptorVersion = "sd1";

    /// <summary>Gets the maximum descriptor length; persisted descriptors are bounded by it.</summary>
    public const int MaximumLength = 256;

    /// <summary>Returns the canonical descriptor for a definition.</summary>
    public static string Describe(SystemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var builder = new StringBuilder(DescriptorVersion);
        Append(builder, "kind", definition.Kind.Value);
        Append(builder, "condition", definition.ConditionParticipation ? "true" : "false");
        Append(builder, "order", Integer(definition.CommonOrder));
        Append(builder, "power", definition.Power is null ? "none" : Integer(definition.Power.NominalDemand.Value));
        Append(
            builder,
            "repair",
            definition.Repair is null ? "none" : Integer(definition.Repair.FullRepairDuration.Milliseconds)
        );

        // The one legitimate per-kind branch: each closed subclass contributes its own specialized tuning.
        switch (definition)
        {
            case PowerGenerationSystemDefinition generation:
                Append(builder, "outputPu", Integer(generation.NominalOutput.Value));
                break;
            case SensorSystemDefinition sensors:
                Append(builder, "passiveRangeKm", Real(sensors.PassiveRange.Value));
                Append(builder, "scanMs", Integer(sensors.ActiveScanDuration.Milliseconds));
                break;
            case ImpulsePropulsionSystemDefinition impulse:
                Append(builder, "maxSpeedKmS", Real(impulse.MaximumTacticalSpeed.Value));
                break;
            case ShieldSystemDefinition:
                break;
            case DirectedEnergyWeaponSystemDefinition weapon:
                Append(builder, "rangeKm", Real(weapon.Weapon.Range.Value));
                Append(builder, "baseDamage", Real(weapon.Weapon.BaseNormalizedDamage));
                Append(builder, "cooldownMs", Integer(weapon.Weapon.Cooldown.Milliseconds));
                break;
            default:
                throw new InvalidOperationException(
                    $"No compatibility descriptor is defined for {definition.GetType().Name}."
                );
        }

        string descriptor = builder.ToString();
        if (descriptor.Length > MaximumLength || !descriptor.All(IsDescriptorCharacter))
        {
            throw new InvalidOperationException($"Compatibility descriptor '{descriptor}' violates its format.");
        }

        return descriptor;
    }

    private static void Append(StringBuilder builder, string name, string value) =>
        builder.Append(';').Append(name).Append('=').Append(value);

    private static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Real(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static bool IsDescriptorCharacter(char character) =>
        character
            is >= 'a'
                and <= 'z'
                or >= 'A'
                and <= 'Z'
                or >= '0'
                and <= '9'
                or '.'
                or '='
                or ';'
                or '_'
                or '+'
                or '-';
}
