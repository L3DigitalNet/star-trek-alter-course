using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AlterCourse.Core.Tests;

/// <summary>Enforces durable namespace boundaries inside the Core project using ArchUnitNET's IL dependency model.</summary>
/// <remarks>Requires the pinned test-only ArchUnitNET package; actor-safe reachability and migration-call rules remain specialized tests.</remarks>
public sealed class DependencyArchitectureTests
{
    private static readonly Architecture ProductionArchitecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CoreAssemblyMarker).Assembly,
            typeof(Microsoft.Extensions.Logging.ILogger).Assembly,
            typeof(Serilog.Log).Assembly
        )
        .Build();

    private static readonly Architecture ProbeArchitecture = new ArchLoader()
        .LoadAssemblies(
            typeof(DependencyArchitectureTests).Assembly,
            typeof(Microsoft.Extensions.Logging.ILogger).Assembly,
            typeof(Serilog.Log).Assembly
        )
        .Build();

    /// <summary>Core never depends on presentation, test, tool, or concrete logging implementations.</summary>
    [Theory]
    [InlineData("AlterCourse.Godot")]
    [InlineData("Godot")]
    [InlineData("AlterCourse.Core.Tests")]
    [InlineData("AlterCourse.AssetCtl")]
    [InlineData("Serilog")]
    public void CoreHasNoForbiddenDependencies(string forbiddenNamespace) =>
        AssertConforms(NoDependency("AlterCourse.Core", forbiddenNamespace), ProductionArchitecture);

    /// <summary>Domain state and scheduled work remain independent from save transport and persistence adapters.</summary>
    [Theory]
    [InlineData("AlterCourse.Core.Ships")]
    [InlineData("AlterCourse.Core.Simulation")]
    [InlineData("AlterCourse.Core.AI")]
    public void DomainNamespacesDoNotDependOnPersistence(string domainNamespace)
    {
        Assert.Contains(
            typeof(CoreAssemblyMarker).Assembly.GetTypes(),
            type => type.Namespace?.StartsWith(domainNamespace, StringComparison.Ordinal) == true
        );
        AssertConforms(NoDependency(domainNamespace, "AlterCourse.Core.Persistence"), ProductionArchitecture);
    }

    /// <summary>Logging abstractions remain at orchestration boundaries rather than pure domain rules.</summary>
    [Theory]
    [InlineData("AlterCourse.Core.Ships")]
    [InlineData("AlterCourse.Core.Simulation")]
    [InlineData("AlterCourse.Core.AI")]
    [InlineData("AlterCourse.Core.Quantities")]
    [InlineData("AlterCourse.Core.Tactical")]
    public void PureDomainNamespacesDoNotDependOnLogging(string domainNamespace) =>
        AssertConforms(NoDependency(domainNamespace, "Microsoft.Extensions.Logging"), ProductionArchitecture);

    /// <summary>Actual logging package types are visible to enforcement across assembly boundaries.</summary>
    [Theory]
    [InlineData("Microsoft.Extensions.Logging")]
    [InlineData("Serilog")]
    public void RuleDetectsActualLoggingPackageDependencies(string loggingNamespace)
    {
        AssertConforms(NoDependency("AlterCourse.ArchitectureProbes.Valid", loggingNamespace), ProbeArchitecture);
        Xunit.Sdk.XunitException failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertConforms(
                NoDependency("AlterCourse.ArchitectureProbes.Origins.Logging", loggingNamespace),
                ProbeArchitecture
            )
        );
        Assert.Contains("Violation", failure.Message, StringComparison.Ordinal);
        Assert.Contains(loggingNamespace, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Each boundary rejects an isolated dependency and accepts an independent valid class.</summary>
    [Theory]
    [InlineData("Presentation")]
    [InlineData("TestOnly")]
    [InlineData("Tooling")]
    [InlineData("Logging")]
    [InlineData("Persistence")]
    public void NamespaceRuleDetectsAnIntentionalViolation(string boundary)
    {
        string target = "AlterCourse.ArchitectureProbes.Targets." + boundary;
        AssertConforms(NoDependency("AlterCourse.ArchitectureProbes.Valid", target), ProbeArchitecture);
        Xunit.Sdk.XunitException failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertConforms(
                NoDependency("AlterCourse.ArchitectureProbes.Origins." + boundary, target),
                ProbeArchitecture
            )
        );
        Assert.Contains("Violation", failure.Message, StringComparison.Ordinal);
        Assert.Contains(target, failure.Message, StringComparison.Ordinal);
    }

    private static void AssertConforms(TypesShouldConjunction rule, Architecture architecture) =>
        Assert.True(
            rule.HasNoViolations(architecture),
            rule.Description + Environment.NewLine + string.Join(Environment.NewLine, rule.Evaluate(architecture))
        );

    private static TypesShouldConjunction NoDependency(string originNamespace, string targetNamespace) =>
        Types()
            .That()
            .ResideInNamespaceMatching("^" + System.Text.RegularExpressions.Regex.Escape(originNamespace) + "($|\\.)")
            .Should()
            .NotDependOnAny(
                Types()
                    .That()
                    .ResideInNamespaceMatching(
                        "^" + System.Text.RegularExpressions.Regex.Escape(targetNamespace) + "($|\\.)"
                    )
            );
}
