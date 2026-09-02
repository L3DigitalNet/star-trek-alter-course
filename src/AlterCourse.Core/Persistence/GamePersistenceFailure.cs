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
}
