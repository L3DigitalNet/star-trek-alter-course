using AlterCourse.Core.Ships;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies installed-system storage: bounded, identity-keyed, and never collapsing duplicate kinds.</summary>
public sealed class InstalledSystemCollectionTests
{
    /// <summary>Two installations of one kind are both stored and both visible; nothing picks a first match.</summary>
    [Fact]
    public void StoresTwoSensorsWithoutCollapse()
    {
        var systems = InstalledSystemCollection.Create([
            TestSystems.Install(17, TestSystems.Consumer("long", 300, 40), 10),
            TestSystems.Install(2, TestSystems.Consumer("short", 200, 30), 5),
            TestSystems.Install(900, TestSystems.Generator("gen", 120)),
        ]);

        Assert.Equal(3, systems.Count);
        Assert.Equal([2L, 17L], systems.OfKind(ShipSystemKind.Sensors).Select(system => system.Id.Value));
        Assert.Equal([2L, 17L, 900L], systems.ByIdentity.Select(system => system.Id.Value));
        Assert.Equal([900L, 2L, 17L], systems.InCommonOrder.Select(system => system.Id.Value));
        Assert.Equal([2L, 17L], systems.Consumers.Select(system => system.Id.Value));
        Assert.True(systems.TryGet(new InstalledSystemId(17), out InstalledSystem? found));
        Assert.Equal("long", found.Definition.Id.Value);
        Assert.False(systems.TryGet(new InstalledSystemId(3), out _));
    }

    /// <summary>Equal common orders fall back to installed identity, independent of input order.</summary>
    [Fact]
    public void CommonOrderTiesBreakByIdentityRegardlessOfInputOrder()
    {
        InstalledSystem a = TestSystems.Install(9, TestSystems.Consumer("a", 200, 10));
        InstalledSystem b = TestSystems.Install(4, TestSystems.Consumer("b", 200, 10));
        var forward = InstalledSystemCollection.Create([a, b]);
        var reversed = InstalledSystemCollection.Create([b, a]);

        Assert.Equal(forward, reversed);
        Assert.Equal([4L, 9L], forward.InCommonOrder.Select(system => system.Id.Value));
    }

    /// <summary>Storage is bounded at sixteen and rejects repeated identities.</summary>
    [Fact]
    public void RejectsOverflowAndDuplicateIdentity()
    {
        InstalledSystem[] sixteen =
        [
            .. Enumerable
                .Range(1, InstalledSystemCollection.MaximumCount)
                .Select(index => TestSystems.Install(index, TestSystems.Consumer($"c{index}", index, 1))),
        ];
        Assert.Equal(16, InstalledSystemCollection.Create(sixteen).Count);
        Assert.Throws<ArgumentException>(() =>
            InstalledSystemCollection.Create([.. sixteen, TestSystems.Install(17, TestSystems.Consumer("x", 1, 1))])
        );
        Assert.Throws<ArgumentException>(() =>
            InstalledSystemCollection.Create([
                TestSystems.Install(3, TestSystems.Consumer("a", 1, 1)),
                TestSystems.Install(3, TestSystems.Consumer("b", 2, 1)),
            ])
        );
    }

    /// <summary>
    /// Cardinality is a separate admission rule: storage accepts two sensors, but the supported-single lookup and
    /// the admission check both refuse them, naming every duplicate identity.
    /// </summary>
    [Fact]
    public void AdmissionRefusesDuplicateKindsThatStorageAccepts()
    {
        var systems = InstalledSystemCollection.Create([
            TestSystems.Install(2, TestSystems.Consumer("a", 200, 10)),
            TestSystems.Install(17, TestSystems.Consumer("b", 300, 10)),
        ]);

        InvalidOperationException admission = Assert.Throws<InvalidOperationException>(() =>
            ShipSystemAdmission.ValidateSupportedCardinality(systems)
        );
        Assert.Contains("ids 2, 17", admission.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            ShipSystemAdmission.SupportedSingle(systems, ShipSystemKind.Sensors)
        );
        Assert.Null(ShipSystemAdmission.SupportedSingle(systems, ShipSystemKind.Shields));
    }
}
