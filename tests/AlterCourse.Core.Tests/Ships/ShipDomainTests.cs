using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies ship definition and system-condition value boundaries.</summary>
public sealed class ShipDomainTests
{
    /// <summary>Confirms system condition accepts the complete inclusive unit interval.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    [InlineData(1)]
    public void SystemConditionAcceptsInclusiveUnitInterval(double value)
    {
        Assert.Equal(value, new SystemCondition(value).Value);
    }

    /// <summary>Confirms system condition rejects values outside its finite bounds.</summary>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SystemConditionRejectsValuesOutsideUnitInterval(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemCondition(value));
    }

    /// <summary>
    /// Confirms ship definitions require stable identity and name, and that sensor timing — now owned by the sensor
    /// and repair system definitions a design installs — must be positive and fixed-step aligned.
    /// </summary>
    [Fact]
    public void ShipDefinitionRejectsInvalidIdentityAndUnalignedRepairDuration()
    {
        var loadout = new ShipLoadoutDefinition(1, []);
        Assert.Throws<ArgumentException>(() => new ShipDefinitionId(""));
        Assert.Throws<ArgumentException>(() => new ShipDefinition(new ShipDefinitionId("ship"), " ", loadout));
        Assert.Throws<ArgumentException>(() => Sensor(0));
        Assert.Throws<ArgumentException>(() => Sensor(2050));
        Assert.Throws<ArgumentException>(() => new SystemRepairCapability(new SimulationDuration(8050), 0));
        Assert.Equal(2000, Sensor(2000).ActiveScanDuration.Milliseconds);
    }

    private static SensorSystemDefinition Sensor(long scanMilliseconds) =>
        new(
            new SystemDefinitionId("test.sensors"),
            "Sensors",
            200,
            true,
            new SystemRepairCapability(new SimulationDuration(8000), 0),
            new SystemPowerDemand(new PowerUnits(70)),
            new DistanceKilometers(30),
            new SimulationDuration(scanMilliseconds)
        );

    /// <summary>Confirms durable ship-definition identities use the compact ASCII wire alphabet.</summary>
    [Theory]
    [InlineData("non ascii")]
    [InlineData("non/ascii")]
    [InlineData("non:ascii")]
    [InlineData("nonéascii")]
    [InlineData("non\u0001ascii")]
    public void ShipDefinitionIdentityRejectsCharactersOutsideDurableAlphabet(string identity)
    {
        Assert.Throws<ArgumentException>(() => new ShipDefinitionId(identity));
        Assert.Equal("AZaz09-_.", new ShipDefinitionId("AZaz09-_.").Value);
    }
}
