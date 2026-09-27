// Isolated IL fixture for namespace dependency enforcement; no engine or service is required.
namespace AlterCourse.ArchitectureProbes.Origins.Presentation;

internal sealed class Violation
{
    internal static Targets.Presentation.Dependency Create() => new();
}
