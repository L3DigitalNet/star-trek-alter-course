using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Tests.Quantities;

/// <summary>Verifies physical-quantity validation and canonical units.</summary>
public sealed class PhysicalQuantityTests
{
    /// <summary>Confirms distance accepts finite nonnegative kilometers.</summary>
    [Fact]
    public void DistanceAcceptsFiniteNonnegativeKilometers()
    {
        Assert.Equal(0, new DistanceKilometers(0).Value);
        Assert.Equal(12.5, new DistanceKilometers(12.5).Value);
    }

    /// <summary>Confirms distance rejects values outside its domain.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DistanceRejectsNegativeOrNonfiniteValues(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DistanceKilometers(value));
    }

    /// <summary>Confirms speed accepts finite nonnegative kilometers per second.</summary>
    [Fact]
    public void SpeedAcceptsFiniteNonnegativeKilometersPerSecond()
    {
        Assert.Equal(0, new SpeedKilometersPerSecond(0).Value);
        Assert.Equal(3.25, new SpeedKilometersPerSecond(3.25).Value);
    }

    /// <summary>Confirms speed rejects values outside its domain.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void SpeedRejectsNegativeOrNonfiniteValues(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpeedKilometersPerSecond(value));
    }

    /// <summary>Confirms headings normalize into the canonical degree interval.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    [InlineData(-90, 270)]
    [InlineData(-720, 0)]
    public void HeadingNormalizesToCanonicalRange(double input, double expected)
    {
        Assert.Equal(expected, new HeadingDegrees(input).Value);
    }

    /// <summary>Confirms headings reject nonfinite degree values.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void HeadingRejectsNonfiniteValues(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeadingDegrees(value));
    }
}
