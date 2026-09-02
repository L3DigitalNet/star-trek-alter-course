using AlterCourse.Core.Gameplay;

namespace AlterCourse.Core.Persistence;

/// <summary>Returns validated save metadata and a newly reconstructed simulation aggregate.</summary>
public sealed record LoadedGameSave(GameSaveMetadata Metadata, GameSimulation Simulation);
