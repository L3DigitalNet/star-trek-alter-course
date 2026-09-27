namespace AlterCourse.Core.Persistence;

/// <summary>
/// Raised inside the load pipeline when a structurally valid save cannot be interpreted under the supplied content:
/// a referenced system definition is absent or means something else, the aim vocabulary differs, or a historical ship
/// definition has no frozen mapping. <see cref="GamePersistence"/> converts it to
/// <see cref="GamePersistenceFailure.IncompatibleContent"/>; it never escapes the public API.
/// </summary>
/// <remarks>
/// A dedicated type rather than a message convention on <see cref="InvalidOperationException"/>: the load pipeline
/// maps every ordinary semantic exception to invalid data, and incompatibility must stay distinguishable so the
/// player is told to use matching content or start a new game rather than that the file is corrupt.
/// </remarks>
internal sealed class SaveContentIncompatibleException : Exception
{
    public SaveContentIncompatibleException(string message)
        : base(message) { }

    public SaveContentIncompatibleException() { }

    public SaveContentIncompatibleException(string message, Exception innerException)
        : base(message, innerException) { }
}
