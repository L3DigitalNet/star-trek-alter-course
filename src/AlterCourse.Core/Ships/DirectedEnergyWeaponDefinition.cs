using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Defines authored directed-energy range, normalized output, and fixed-step cooldown.</summary>
public sealed record DirectedEnergyWeaponDefinition
{
    /// <summary>Initializes positive bounded weapon tuning without gameplay defaults.</summary>
    public DirectedEnergyWeaponDefinition(
        DistanceKilometers range,
        double baseNormalizedDamage,
        SimulationDuration cooldown
    )
    {
        if (range.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(range));
        if (!double.IsFinite(baseNormalizedDamage) || baseNormalizedDamage <= 0 || baseNormalizedDamage > 1)
            throw new ArgumentOutOfRangeException(nameof(baseNormalizedDamage));
        if (cooldown.Milliseconds <= 0 || cooldown.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
            throw new ArgumentException("Cooldown must be positive and fixed-step aligned.", nameof(cooldown));
        Range = range;
        BaseNormalizedDamage = baseNormalizedDamage;
        Cooldown = cooldown;
    }

    /// <summary>Gets inclusive tactical range in kilometers.</summary>
    public DistanceKilometers Range { get; }

    /// <summary>Gets normalized shot output at nominal powered capability.</summary>
    public double BaseNormalizedDamage { get; }

    /// <summary>Gets the interval before another accepted shot.</summary>
    public SimulationDuration Cooldown { get; }
}
