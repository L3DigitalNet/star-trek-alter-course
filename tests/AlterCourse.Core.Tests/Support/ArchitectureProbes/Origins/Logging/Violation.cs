// Isolated IL fixture for namespace dependency enforcement; no engine or service is required.
namespace AlterCourse.ArchitectureProbes.Origins.Logging;

internal sealed class Violation
{
    internal static Targets.Logging.Dependency Create() => new();

    internal static Microsoft.Extensions.Logging.ILogger? AbstractLogger() => null;

    internal static Serilog.ILogger? ConcreteLogger() => null;
}
