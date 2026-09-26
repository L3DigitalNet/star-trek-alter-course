namespace AlterCourse.Core.Ships;

/// <summary>Declares the finite shape limits shared by installed-system content, runtime state, and saves.</summary>
public static class ShipSystemLimits
{
    /// <summary>
    /// Gets the maximum number of installations one ship may carry. The <c>ship-definition-v6</c> schema's
    /// <c>systems.maxItems</c> repeats this value; change both together.
    /// </summary>
    public const int MaximumInstalledSystemsPerShip = 16;
}
