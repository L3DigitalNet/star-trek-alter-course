namespace AlterCourse.Core.Content;

/// <summary>Describes one deterministic authored-faction validation failure.</summary>
public sealed record FactionContentDiagnostic(
    string Code,
    string SourceIdentity,
    string InstanceLocation,
    string SchemaLocation,
    string Message
);
