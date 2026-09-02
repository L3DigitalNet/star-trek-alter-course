using AlterCourse.Core.Simulation;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Converts presentation elapsed time into bounded whole Core fixed steps.</summary>
public sealed class SimulationRateController
{
    /// <summary>Limits one rendered frame to six authoritative fixed steps.</summary>
    public const int MaximumStepsPerFrame = 6;

    private static readonly double FixedStepSeconds = SimulationFixedStep.Duration.Milliseconds / 1000.0;
    private static readonly double MaximumBacklogSeconds = MaximumStepsPerFrame * FixedStepSeconds;
    private static readonly double[] SupportedRates = [0, 0.5, 1, 2, 4];

    private double _fractionalSimulationSeconds;

    /// <summary>Gets the selected simulation-time multiplier.</summary>
    public double Rate { get; private set; } = 1;

    /// <summary>Selects one of the five supported presentation rates.</summary>
    public void SetRate(double rate)
    {
        if (!SupportedRates.Contains(rate))
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "Unsupported simulation rate.");
        }

        Rate = rate;
    }

    /// <summary>Returns bounded whole steps for a controlled elapsed interval.</summary>
    public int ConsumeElapsed(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                elapsedSeconds,
                "Elapsed presentation time must be finite and nonnegative."
            );
        }

        if (Rate == 0)
        {
            return 0;
        }

        // A stalled presentation may discard excess backlog. Carry is bounded to one frame's
        // budget so reopening a covered window cannot trigger an unbounded simulation burst.
        double accumulated = Math.Min(_fractionalSimulationSeconds + elapsedSeconds * Rate, MaximumBacklogSeconds);
        int steps = Math.Min((int)Math.Floor((accumulated + 1e-12) / FixedStepSeconds), MaximumStepsPerFrame);
        _fractionalSimulationSeconds = accumulated - steps * FixedStepSeconds;
        return steps;
    }
}
