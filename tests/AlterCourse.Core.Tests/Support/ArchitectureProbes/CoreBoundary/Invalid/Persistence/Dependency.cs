// Isolated fixture distinguishes pure domain dependencies from permitted orchestration adapters.
namespace AlterCourse.ArchitectureProbes.CoreBoundary.Invalid.Persistence;

internal sealed class Dependency
{
    internal static Microsoft.Extensions.Logging.ILogger? Logger() => null;
}
