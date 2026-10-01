namespace AlterCourse.AssetCtl.Cli;

/// <summary>Records an actual command commit separately from its required result reporting.</summary>
internal sealed class CommandCommitState
{
    public bool IsCommitted { get; private set; }

    // Mutation owners call this only after their authoritative rename/publication boundary returns.
    public void MarkCommitted() => IsCommitted = true;
}
