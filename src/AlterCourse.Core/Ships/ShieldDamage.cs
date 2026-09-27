namespace AlterCourse.Core.Ships;

/// <summary>Resolves normalized damage against powered shield condition without mutating ship state.</summary>
public static class ShieldDamage
{
    /// <summary>Absorbs damage using current condition and power satisfaction, preserving penetration.</summary>
    public static ShieldDamageResult Resolve(
        double normalizedDamage,
        SystemCondition shieldCondition,
        PowerSatisfactionRatio powerSatisfaction
    )
    {
        ValidateNormalized(normalizedDamage, nameof(normalizedDamage));
        double absorbed = Math.Min(normalizedDamage, shieldCondition.Value * powerSatisfaction.Value);
        return new ShieldDamageResult(
            new SystemCondition(Math.Clamp(shieldCondition.Value - absorbed, 0, 1)),
            absorbed,
            normalizedDamage - absorbed
        );
    }

    private static void ValidateNormalized(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and normalized.");
    }
}
