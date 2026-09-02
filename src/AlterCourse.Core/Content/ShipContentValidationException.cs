namespace AlterCourse.Core.Content;

/// <summary>Reports failures that prevent authored ship content from entering the domain catalog.</summary>
public sealed class ShipContentValidationException : Exception
{
    /// <summary>Initializes an exception from one or more deterministic diagnostics.</summary>
    public ShipContentValidationException(IEnumerable<ShipContentDiagnostic> diagnostics)
        : this((diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).ToArray()) { }

    private ShipContentValidationException(ShipContentDiagnostic[] diagnostics)
        : base(FormatMessage(diagnostics))
    {
        Diagnostics = Array.AsReadOnly(diagnostics);
    }

    /// <summary>Gets the ordered validation failures.</summary>
    public IReadOnlyList<ShipContentDiagnostic> Diagnostics { get; }

    private static string FormatMessage(ShipContentDiagnostic[] diagnostics)
    {
        if (diagnostics.Length == 0)
        {
            throw new ArgumentException("Content validation requires at least one diagnostic.", nameof(diagnostics));
        }

        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"{diagnostic.SourceIdentity} {diagnostic.InstanceLocation}: {diagnostic.Message} "
                + $"[{diagnostic.Code}; schema {diagnostic.SchemaLocation}]"
            )
        );
    }
}
