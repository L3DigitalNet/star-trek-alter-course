// Isolated IL fixture for namespace dependency enforcement; no engine or service is required.
namespace AlterCourse.ArchitectureProbes.Origins.Persistence;

internal sealed class Violation
{
    internal static Targets.Persistence.Dependency Create() => new();
}
