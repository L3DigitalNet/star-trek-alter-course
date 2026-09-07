namespace AlterCourse.Core.Content;

/// <summary>Reports failures that prevent authored faction content from entering the domain catalog.</summary>
public sealed class FactionContentValidationException : Exception
{
    /// <summary>Initializes an exception from one or more deterministic diagnostics.</summary>
    public FactionContentValidationException(IEnumerable<FactionContentDiagnostic> diagnostics)
        : this((diagnostics ?? throw new ArgumentNullException(nameof(diagnostics))).ToArray()) { }

    private FactionContentValidationException(FactionContentDiagnostic[] diagnostics)
        : base(FormatMessage(diagnostics))
    {
        Diagnostics = Array.AsReadOnly(diagnostics);
    }

    /// <summary>Gets the ordered validation failures.</summary>
    public IReadOnlyList<FactionContentDiagnostic> Diagnostics { get; }

    private static string FormatMessage(FactionContentDiagnostic[] diagnostics)
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
