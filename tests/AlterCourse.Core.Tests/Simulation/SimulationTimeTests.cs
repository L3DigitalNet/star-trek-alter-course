using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Simulation;

/// <summary>Verifies explicit simulation-time and fixed-step behavior.</summary>
public sealed class SimulationTimeTests
{
    /// <summary>Confirms time and duration cannot represent negative values.</summary>
    [Fact]
    public void TimeAndDurationRejectNegativeMilliseconds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationTime(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationDuration(-1));
    }

    /// <summary>Confirms explicit advancement is monotonic.</summary>
    [Fact]
    public void TimeAdvancesByDurationAndToTarget()
    {
        SimulationTime initial = new(250);

        SimulationTime advanced = initial.AdvanceBy(new SimulationDuration(50));

        Assert.Equal(300, advanced.Milliseconds);
        Assert.Equal(new SimulationTime(450), advanced.AdvanceTo(new SimulationTime(450)));
        Assert.Throws<InvalidOperationException>(() => advanced.AdvanceTo(initial));
    }

    /// <summary>Confirms time and duration arithmetic cannot wrap.</summary>
    [Fact]
    public void TimeAndDurationAdvancementRejectOverflow()
    {
        Assert.Throws<OverflowException>(() =>
            new SimulationTime(long.MaxValue).AdvanceBy(new SimulationDuration(1))
        );
        Assert.Throws<OverflowException>(() =>
            new SimulationDuration(long.MaxValue).Add(new SimulationDuration(1))
        );
    }

    /// <summary>Confirms the authoritative tactical quantum is one hundred milliseconds.</summary>
    [Fact]
    public void FixedStepIsExactlyOneHundredMilliseconds()
    {
        Assert.Equal(100, SimulationFixedStep.Duration.Milliseconds);
    }
}
