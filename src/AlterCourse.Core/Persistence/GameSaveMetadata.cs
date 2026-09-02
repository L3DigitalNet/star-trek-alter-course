namespace AlterCourse.Core.Persistence;

/// <summary>Supplies player-facing save identity and caller-owned wall-clock organization data.</summary>
public sealed record GameSaveMetadata(
    string SaveId,
    string DisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset SavedAtUtc
);
