using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.AI;

/// <summary>Records one deterministic actor-safe defensive decision fact.</summary>
internal sealed record DefensiveCombatDecisionExplanation(
    DefensiveCombatDecisionInput Input,
    IReadOnlyList<DefensiveCombatDecisionCandidate> Candidates,
    DefensiveCombatDecisionAction SelectedAction,
    SensorContactId ContactId,
    ShipSystemId TargetSystem,
    DefensiveCombatDecisionTieRule TieRule,
    SetTacticalCourseIntent? ResultingCourse,
    FireDirectedEnergyOutcome? ApplicationOutcome = null,
    SetTacticalCourseOutcome? CourseOutcome = null
);
