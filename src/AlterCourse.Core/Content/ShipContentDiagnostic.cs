namespace AlterCourse.Core.Content;

/// <summary>Describes one deterministic authored-content validation failure.</summary>
public sealed record ShipContentDiagnostic(
    string Code,
    string SourceIdentity,
    string InstanceLocation,
    string SchemaLocation,
    string Message
);
