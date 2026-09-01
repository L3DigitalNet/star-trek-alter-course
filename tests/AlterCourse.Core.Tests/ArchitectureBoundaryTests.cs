namespace AlterCourse.Core.Tests;

/// <summary>Verifies compile-time architecture constraints that span project boundaries.</summary>
public sealed class ArchitectureBoundaryTests
{
    /// <summary>Confirms the pure simulation assembly does not load Godot.</summary>
    [Fact]
    public void CoreAssemblyDoesNotReferenceGodot()
    {
        System.Reflection.AssemblyName[] references = typeof(CoreAssemblyMarker).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => reference.Name?.StartsWith("Godot", StringComparison.Ordinal) == true
        );
    }
}
