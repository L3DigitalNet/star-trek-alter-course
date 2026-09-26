namespace AlterCourse.Core.Ships;

/// <summary>One authored installation in a ship design's default initial loadout.</summary>
public sealed record InitialInstalledSystem
{
    /// <summary>Initializes an authored installation from initialized identities.</summary>
    /// <exception cref="ArgumentException">Either identity is uninitialized (default).</exception>
    public InitialInstalledSystem(InstalledSystemId id, SystemDefinitionId definitionId)
    {
        if (id.Value == 0)
        {
            throw new ArgumentException("Initial installation requires an initialized installed identity.", nameof(id));
        }

        if (definitionId.Value is null)
        {
            throw new ArgumentException(
                "Initial installation requires an initialized definition identity.",
                nameof(definitionId)
            );
        }

        Id = id;
        DefinitionId = definitionId;
    }

    /// <summary>Gets the explicit ship-local identity the installation receives at bootstrap.</summary>
    public InstalledSystemId Id { get; }

    /// <summary>Gets the reusable definition the installation is built from.</summary>
    public SystemDefinitionId DefinitionId { get; }
}
