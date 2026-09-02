namespace AlterCourse.Core.Persistence;

/// <summary>Reports a typed persistence failure with the source that could not be trusted.</summary>
public sealed class GamePersistenceException : Exception
{
    internal GamePersistenceException(
        GamePersistenceFailure failure,
        string sourceIdentity,
        string message,
        Exception? innerException = null
    )
        : base($"Save '{sourceIdentity}' {message}", innerException)
    {
        Failure = failure;
        SourceIdentity = sourceIdentity;
    }

    /// <summary>Gets the persistence failure category.</summary>
    public GamePersistenceFailure Failure { get; }

    /// <summary>Gets the caller-supplied path or diagnostic source identity.</summary>
    public string SourceIdentity { get; }
}
