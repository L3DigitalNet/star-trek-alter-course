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
    [InlineData("CsCheck")]
    [InlineData("ArchUnitNET")]
    [InlineData("Xunit")]
    public void CoreHasNoForbiddenDependencies(string forbiddenNamespace) =>
        AssertConforms(NoDependency("AlterCourse.Core", forbiddenNamespace), ProductionArchitecture);

    /// <summary>Only persistence adapters may depend on save transport types.</summary>
    [Fact]
    public void CoreOutsidePersistenceDoesNotDependOnPersistence() =>
        AssertConforms(OutsideAdapters("AlterCourse.Core", "Persistence"), ProductionArchitecture);

    /// <summary>Only Gameplay and Persistence orchestration may depend on logging abstractions.</summary>
    [Fact]
    public void PureCoreDoesNotDependOnLogging() =>
        AssertConforms(OutsideAdapters("AlterCourse.Core", "Microsoft.Extensions.Logging"), ProductionArchitecture);

    /// <summary>New domain namespaces are covered while explicit orchestration adapters remain valid.</summary>
    [Theory]
    [InlineData("Persistence")]
    [InlineData("Microsoft.Extensions.Logging")]
    public void AdapterExclusionsRejectDomainViolations(string target)
    {
        const string root = "AlterCourse.ArchitectureProbes.CoreBoundary";
        Xunit.Sdk.XunitException failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertConforms(OutsideAdapters(root + ".Invalid", target), ProbeArchitecture)
        );
        Assert.Contains("Factions.Violation", failure.Message, StringComparison.Ordinal);
        AssertConforms(OutsideAdapters(root + ".Valid", target), ProbeArchitecture);
    }

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

    /// <summary>Referenced target types remain enforceable when their defining assembly is not loaded.</summary>
    [Fact]
    public void RuleDetectsDependenciesOnAnUnloadedAssembly()
    {
        Architecture unloaded = new ArchLoader().LoadAssemblies(typeof(DependencyArchitectureTests).Assembly).Build();
        AssertConforms(NoDependency("AlterCourse.ArchitectureProbes.Valid", "CsCheck"), unloaded);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertConforms(NoDependency("AlterCourse.Core.Tests", "CsCheck"), unloaded)
        );
    }

    private static TypesShouldConjunction OutsideAdapters(string root, string target)
    {
        bool persistence = string.Equals(target, "Persistence", StringComparison.Ordinal);
        string excluded =
            "^"
            + System.Text.RegularExpressions.Regex.Escape(root)
            + (persistence ? @"\.Persistence($|\.)" : @"\.(Gameplay|Persistence)($|\.)");
        return Types()
            .That()
            .ResideInNamespaceMatching("^" + System.Text.RegularExpressions.Regex.Escape(root) + @"($|\.)")
            .And()
            .DoNotResideInNamespaceMatching(excluded)
            .Should()
            .NotDependOnAny(
                Types(true)
                    .That()
                    .ResideInNamespaceMatching(
                        "^"
                            + System.Text.RegularExpressions.Regex.Escape(persistence ? root + ".Persistence" : target)
                            + @"($|\.)"
                    )
            );
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
                Types(true)
                    .That()
                    .ResideInNamespaceMatching(
                        "^" + System.Text.RegularExpressions.Regex.Escape(targetNamespace) + "($|\\.)"
                    )
            );
}
