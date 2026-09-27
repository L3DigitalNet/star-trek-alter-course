// Isolated IL fixture for namespace dependency enforcement; no engine or service is required.
namespace AlterCourse.ArchitectureProbes.Origins.Tooling;

internal sealed class Violation
{
    internal static Targets.Tooling.Dependency Create() => new();
}
