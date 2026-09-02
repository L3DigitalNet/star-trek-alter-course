using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Ships;

/// <summary>Verifies ship definition and sensor-integrity value boundaries.</summary>
public sealed class ShipDomainTests
{
    /// <summary>Confirms sensor integrity accepts the complete inclusive unit interval.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    [InlineData(1)]
    public void SensorIntegrityAcceptsInclusiveUnitInterval(double value)
    {
        Assert.Equal(value, new SensorIntegrity(value).Value);
    }

    /// <summary>Confirms sensor integrity rejects values outside its finite bounds.</summary>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SensorIntegrityRejectsValuesOutsideUnitInterval(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SensorIntegrity(value));
    }

    /// <summary>Confirms ship definitions require stable identity, name, and aligned repair timing.</summary>
    [Fact]
    public void ShipDefinitionRejectsInvalidIdentityAndUnalignedRepairDuration()
    {
        Assert.Throws<ArgumentException>(() => new ShipDefinitionId(""));
        Assert.Throws<ArgumentException>(() =>
            new ShipDefinition(
                new ShipDefinitionId("ship"),
                " ",
                new SpeedKilometersPerSecond(1),
                new SimulationDuration(8000)
            )
        );
        Assert.Throws<ArgumentException>(() =>
            new ShipDefinition(
                new ShipDefinitionId("ship"),
                "Ship",
                new SpeedKilometersPerSecond(1),
                new SimulationDuration(8050)
            )
        );
    }

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
