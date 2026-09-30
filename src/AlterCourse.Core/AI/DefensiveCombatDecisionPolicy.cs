using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.AI;

/// <summary>Ranks one finite defensive response using own facts and a legitimately retained local stimulus.</summary>
internal static class DefensiveCombatDecisionPolicy
{
    internal static DefensiveCombatDecisionExplanation Evaluate(DefensiveCombatDecisionInput input)
    {
        FireDirectedEnergyOutcome fire = CombatLegality.EvaluatePrerequisites(
            input.Own,
            input.Contact,
            input.SameContext
        );
        IReadOnlyList<DefensiveCombatConstraintEvaluation> fireConstraints = FireConstraints(input, fire);
        IReadOnlyList<DefensiveCombatConstraintEvaluation> withdrawConstraints = WithdrawConstraints(input);
        bool fireLegal = fireConstraints.All(item => item.Satisfied);
        bool withdrawLegal = withdrawConstraints.All(item => item.Satisfied);
        DefensiveCombatDecisionCandidate[] candidates =
        [
            new(DefensiveCombatDecisionAction.ReturnFire, fireConstraints, fireLegal ? 3 : null, fire),
            new(DefensiveCombatDecisionAction.Withdraw, withdrawConstraints, withdrawLegal ? 2 : null, null),
            new(
                DefensiveCombatDecisionAction.Hold,
                new ReadOnlyDecisionList<DefensiveCombatConstraintEvaluation>([]),
                1,
                null
            ),
        ];
        DefensiveCombatDecisionAction selected = candidates
            .Where(item => item.Rank is not null)
            .OrderByDescending(item => item.Rank)
            .ThenBy(item => item.Action)
            .First()
            .Action;
        SetTacticalCourseIntent? course = null;
        if (selected == DefensiveCombatDecisionAction.Withdraw)
        {
            course = new SetTacticalCourseIntent(
                DecisionGeometry.HeadingBetween(input.Contact!.LastObservedPosition, input.Own.Position),
                input.Own.MaximumSpeed
            );
        }
        return new DefensiveCombatDecisionExplanation(
            input,
            new ReadOnlyDecisionList<DefensiveCombatDecisionCandidate>(candidates),
            selected,
            input.Stimulus.ContactId,
            ShipSystemKind.DirectedEnergyWeapons,
            DefensiveCombatDecisionTieRule.ReturnFireThenWithdrawThenHold,
            course
        );
    }

    private static ReadOnlyDecisionList<DefensiveCombatConstraintEvaluation> WithdrawConstraints(
        DefensiveCombatDecisionInput input
    )
    {
        bool displacement = input.Contact is { } known && known.LastObservedPosition != input.Own.Position;
        return new ReadOnlyDecisionList<DefensiveCombatConstraintEvaluation>(
            new[]
            {
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.LegitimateStimulus,
                    input.Contact?.Id == input.Stimulus.ContactId
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.CurrentContact,
                    input.Contact?.Status == SensorContactStatus.Current
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.LocalContext,
                    input.Own.AtLocation && input.SameContext
                ),
                new DefensiveCombatConstraintEvaluation(DefensiveCombatConstraint.KnownDisplacement, displacement),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.PropulsionAvailable,
                    input.Own.MaximumSpeed.Value > 0
                ),
            }
        );
    }

    private static ReadOnlyDecisionList<DefensiveCombatConstraintEvaluation> FireConstraints(
        DefensiveCombatDecisionInput input,
        FireDirectedEnergyOutcome fire
    )
    {
        return new ReadOnlyDecisionList<DefensiveCombatConstraintEvaluation>(
            new[]
            {
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.LegitimateStimulus,
                    input.Contact?.Id == input.Stimulus.ContactId
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.CurrentContact,
                    input.Contact?.Status == SensorContactStatus.Current
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.IdentifiedContact,
                    input.Contact?.Identification == SensorContactIdentification.Identified
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.LocalContext,
                    input.Own.AtLocation && input.SameContext
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.WeaponPresent,
                    input.Own.Weapon is not null
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.WeaponOperational,
                    input.Own.WeaponCondition.Value > 0
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.InRange,
                    input.Contact is { } target
                        && input.Own.Weapon is { } weapon
                        && CombatLegality.WithinRange(
                            CombatLegality.Distance(input.Own.Position, target.LastObservedPosition),
                            weapon.Range.Value
                        )
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.FutureBoundaryAvailable,
                    input.Own.Weapon is { } authored
                        && long.MaxValue
                            - AlterCourse.Core.Simulation.SimulationFixedStep.Duration.Milliseconds
                            - input.Own.Time.Milliseconds
                            >= CombatLegality.RequiredHeadroom(authored)
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.WeaponPowered,
                    input.Own.WeaponCapability.Value > 0
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.WeaponReady,
                    input.Own.Time.Milliseconds >= input.Own.ReadyAt.Milliseconds
                ),
                new DefensiveCombatConstraintEvaluation(
                    DefensiveCombatConstraint.FireLegal,
                    fire == FireDirectedEnergyOutcome.Accepted
                ),
            }
        );
    }
}
