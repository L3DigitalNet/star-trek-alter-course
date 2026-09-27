using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Simulation;
using Xunit.Abstractions;

namespace AlterCourse.Core.Tests.Support;

/// <summary>Retains a bounded deterministic-fixture checkpoint and writes it once when a long-run test scope ends.</summary>
/// <remarks>xUnit output injection preserves context on assertion failure without wrapping assertions or emitting every step.</remarks>
internal sealed class LongHorizonTestContext(ITestOutputHelper output, string scenario, string replay) : IDisposable
{
    private const int TailCount = 4;
    private SimulationState? _state;
    private string _phase = "fixture construction";
    private SimulationTime? _requestedTime;
    private string _lastTraces = "none";
    private string _lastEvents = "none";

    internal void Checkpoint(SimulationState state, string phase)
    {
        _state = state;
        _phase = phase;
        _requestedTime = null;
    }

    internal void BeforeAdvance(SimulationState state, SimulationTime target)
    {
        _state = state;
        _requestedTime = target;
    }

    internal void Record(SimulationAdvanceTraceResult advanced)
    {
        _state = advanced.State;
        if (advanced.Traces.Count > 0)
        {
            _lastTraces = string.Join(
                " | ",
                advanced
                    .Traces.TakeLast(TailCount)
                    .Select(trace =>
                        $"{trace.WorkKind}#{trace.WorkId.Value}@{trace.ResolutionTime.Milliseconds}:target={trace.Target};rule={trace.Rule};action={trace.Action};rngUsed={trace.RandomnessUsed}"
                    )
            );
        }
        if (advanced.PlayerEvents.Count > 0)
        {
            _lastEvents = string.Join(
                " | ",
                advanced
                    .PlayerEvents.TakeLast(TailCount)
                    .Select(item =>
                        $"{item.Kind}@{item.OccurredAt.Milliseconds}:system={item.InstalledSystemId?.Value}"
                    )
            );
        }
    }

    public void Dispose()
    {
        output.WriteLine(
            $"scenario={scenario}; replay={replay}; phase={_phase}; requestedMs={_requestedTime?.Milliseconds}"
        );
        if (_state is not null)
        {
            output.WriteLine(
                $"fixtureWorldTimeMs={_state.Time.Milliseconds}; player={_state.PlayerShipId.Value}; ships={_state.Ships.Length}; factions={_state.Factions.Length}; "
                    + $"nextWork={_state.Scheduler.NextWorkId}; nextSequence={_state.Scheduler.NextSequence}; outstanding={_state.Scheduler.OutstandingWork.Length}; "
                    + "upcomingWork=["
                    + string.Join(
                        " | ",
                        _state
                            .Scheduler.OutstandingWork.Take(TailCount)
                            .Select(work =>
                                $"{work.Kind}#{work.Id.Value}@{work.DueTime.Milliseconds}:target={work.Target}"
                            )
                    )
                    + "]"
            );
        }
        output.WriteLine("lastResolvedWork=[" + _lastTraces + "]; lastPlayerEvents=[" + _lastEvents + "]");
    }
}
