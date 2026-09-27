// Positive subject prevents an empty pure-domain selection from masquerading as a valid adapter fixture.
namespace AlterCourse.ArchitectureProbes.CoreBoundary.Valid.Factions;

internal sealed class ValidDomain
{
    internal static string Value() => string.Empty;
}
