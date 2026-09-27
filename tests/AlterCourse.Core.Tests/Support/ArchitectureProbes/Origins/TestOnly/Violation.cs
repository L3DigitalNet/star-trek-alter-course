// Isolated IL fixture for namespace dependency enforcement; no engine or service is required.
namespace AlterCourse.ArchitectureProbes.Origins.TestOnly;

internal sealed class Violation
{
    internal static Targets.TestOnly.Dependency Create() => new();
}
