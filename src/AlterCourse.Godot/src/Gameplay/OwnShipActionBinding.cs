using AlterCourse.Core.Identity;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// Stamps a live own-ship action with the player ship and simulation generation it was presented for.
/// </summary>
/// <remarks>
/// Installed identities are ship-local, so a stable key such as <c>repair:4</c> can name a different installation —
/// or a different ship's installation — after a load. <see cref="GameScreen"/> increments the generation on every
/// replacement or clearing of its simulation and refuses any submission whose binding no longer matches both the
/// current generation and the current player ship, so a control presented before a load can never act after it.
/// An ordinary projection refresh keeps the generation, so stable keys, selection, and focus survive it.
/// </remarks>
public sealed record OwnShipActionBinding(ShipInstanceId Owner, long SimulationGeneration);
