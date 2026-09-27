// Isolated fixture distinguishes pure domain dependencies from permitted orchestration adapters.
namespace AlterCourse.ArchitectureProbes.CoreBoundary.Valid.Gameplay;

internal sealed class PermittedLogger
{
    internal static Microsoft.Extensions.Logging.ILogger? Logger() => null;
}
