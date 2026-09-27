// Persistence transport may reference its own models and logging abstractions.
namespace AlterCourse.ArchitectureProbes.CoreBoundary.Valid.Persistence;

internal sealed class Dependency
{
    internal static Microsoft.Extensions.Logging.ILogger? Logger() => null;
}
