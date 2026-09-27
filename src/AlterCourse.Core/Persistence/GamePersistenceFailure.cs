namespace AlterCourse.Core.Persistence;

/// <summary>Classifies failures at the untrusted save-load boundary.</summary>
public enum GamePersistenceFailure
{
    /// <summary>The document is malformed or violates the V1 contract.</summary>
    InvalidData = 1,

    /// <summary>The document declares a save version this build cannot interpret.</summary>
    UnsupportedVersion = 2,

    /// <summary>The save path could not be read or replaced.</summary>
    InputOutput = 3,

    /// <summary>
    /// The document is well formed, but the supplied content cannot interpret it: a referenced system definition is
    /// absent or changed meaning, the aim-kind vocabulary differs, or a historical ship definition has no mapping.
    /// Load with the content the save was created with, or start a new game.
    /// </summary>
    IncompatibleContent = 4,
}
