using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.AI;

/// <summary>Records one deterministic actor-safe defensive decision fact.</summary>
internal sealed record DefensiveCombatDecisionInput(
    CombatOwnFacts Own,
    SensorContactSnapshot? Contact,
    bool SameContext,
    CombatStimulus Stimulus
);
