using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Player;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Support;

/// <summary>
/// Resolves player installations the way the Engineering presentation does — from the projected rows — so
/// scenario tests can address "the sensors" of the production loadout without per-kind Core entry points.
/// </summary>
internal static class PlayerEngineeringTestExtensions
{
    /// <summary>Returns the single projected row of a kind; fails when the loadout has none or several.</summary>
    internal static InstalledSystemProjection System(this EngineeringProjection engineering, ShipSystemKind kind) =>
        engineering.Systems.Single(row => row.Kind == kind);

    /// <summary>Returns the player's single installed identity of a kind from the current projection.</summary>
    internal static InstalledSystemId PlayerInstallation(this GameSimulation game, ShipSystemKind kind) =>
        game.GetPlayerProjection().Ship.Engineering.System(kind).Id;

    /// <summary>Applies the priority allocation for the player's single installation of a kind.</summary>
    internal static PowerAllocationResult PrioritizePlayer(this GameSimulation game, ShipSystemKind kind) =>
        game.ApplyPriorityAllocation(game.PlayerInstallation(kind));
}
