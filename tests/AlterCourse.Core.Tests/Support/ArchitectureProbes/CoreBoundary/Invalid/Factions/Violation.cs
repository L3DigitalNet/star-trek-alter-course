// Isolated fixture distinguishes pure domain dependencies from permitted orchestration adapters.
namespace AlterCourse.ArchitectureProbes.CoreBoundary.Invalid.Factions;

internal sealed class Violation
{
    internal static Microsoft.Extensions.Logging.ILogger? Logger() => null;

    internal static Persistence.Dependency? Save() => null;
}
