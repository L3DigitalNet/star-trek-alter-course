namespace AlterCourse.Core.Gameplay;

/// <summary>Where a ship start's installations come from.</summary>
public enum ShipLoadoutSource
{
    /// <summary>The loadout was omitted; installations and continuation come from the design default.</summary>
    DesignDefault = 1,

    /// <summary>The start declares its installations and continuation; the design default is never read.</summary>
    Explicit = 2,
}
