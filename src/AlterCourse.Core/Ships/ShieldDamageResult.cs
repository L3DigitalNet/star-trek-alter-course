namespace AlterCourse.Core.Ships;

/// <summary>Carries shield depletion and the remaining normalized subsystem damage.</summary>
public sealed record ShieldDamageResult
{
    internal ShieldDamageResult(SystemCondition shieldCondition, double absorbedDamage, double penetratingDamage) =>
        (ShieldCondition, AbsorbedDamage, PenetratingDamage) = (shieldCondition, absorbedDamage, penetratingDamage);

    /// <summary>Gets shield condition after absorption.</summary>
    public SystemCondition ShieldCondition { get; }

    /// <summary>Gets damage absorbed by shields.</summary>
    public double AbsorbedDamage { get; }

    /// <summary>Gets damage that reaches the selected subsystem.</summary>
    public double PenetratingDamage { get; }

    /// <summary>Applies penetration to a concrete condition with final bounded clamping.</summary>
    public SystemCondition ApplyTo(SystemCondition condition) =>
        new(Math.Clamp(condition.Value - PenetratingDamage, 0, 1));
}
