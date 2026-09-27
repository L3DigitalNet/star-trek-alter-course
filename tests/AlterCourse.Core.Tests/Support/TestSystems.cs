using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Support;

/// <summary>Builds free-form system definitions and engineering state for substrate unit tests.</summary>
/// <remarks>
/// Storage admits several installations of one kind; only the typed world boundary refuses them. These helpers stay
/// at the storage layer so power and collection tests can use arbitrary identities, orders, and duplicate kinds.
/// </remarks>
internal static class TestSystems
{
    /// <summary>Builds a generator definition with an arbitrary identity and output.</summary>
    internal static PowerGenerationSystemDefinition Generator(string id, int output, int commonOrder = 100) =>
        new(new SystemDefinitionId(id), "Generator " + id, commonOrder, true, new PowerUnits(output));

    /// <summary>Builds a sensor consumer with an arbitrary identity, common order, and demand.</summary>
    internal static SensorSystemDefinition Consumer(string id, int commonOrder, int demand) =>
        new(
            new SystemDefinitionId(id),
            "Consumer " + id,
            commonOrder,
            true,
            new SystemRepairCapability(new SimulationDuration(1000), 0),
            new SystemPowerDemand(new PowerUnits(demand)),
            new DistanceKilometers(10),
            new SimulationDuration(1000)
        );

    /// <summary>Installs one definition; consumers need an allocation, nonconsumers must pass none.</summary>
    internal static InstalledSystem Install(
        long id,
        SystemDefinition definition,
        int? allocation = null,
        double condition = 1
    ) =>
        new(
            new InstalledSystemId(id),
            definition,
            new SystemCondition(condition),
            definition.Power is null ? null : new PowerUnits(allocation ?? 0)
        );

    /// <summary>Builds engineering state whose continuation follows the largest installed identity.</summary>
    internal static ShipEngineeringState Engineering(params InstalledSystem[] installations) =>
        new(
            InstalledSystemCollection.Create(installations),
            InstalledSystemIdAllocator.Restore(
                installations.Length == 0 ? 1 : installations.Max(system => system.Id.Value) + 1
            )
        );

    /// <summary>Gets the allocation of one installed consumer.</summary>
    internal static int Share(PowerAllocation allocation, long consumer) =>
        allocation.TryGet(new InstalledSystemId(consumer), out PowerUnits share)
            ? share.Value
            : throw new KeyNotFoundException($"Allocation has no entry for installed system {consumer}.");
}
