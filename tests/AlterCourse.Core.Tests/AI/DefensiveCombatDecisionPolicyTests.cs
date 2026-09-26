using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.AI;

/// <summary>Verifies defensive decisions from actor-safe snapshots without aggregate or political context.</summary>
public sealed class DefensiveCombatDecisionPolicyTests
{
    /// <summary>Evaluates every hard fire rejection and the permitted ordinary fallback.</summary>
    [Theory]
    [InlineData(FireDirectedEnergyOutcome.Accepted, (int)DefensiveCombatDecisionAction.ReturnFire)]
    [InlineData(FireDirectedEnergyOutcome.ContactNotFound, (int)DefensiveCombatDecisionAction.Hold)]
    [InlineData(FireDirectedEnergyOutcome.ContactNotCurrent, (int)DefensiveCombatDecisionAction.Hold)]
    [InlineData(FireDirectedEnergyOutcome.ContactNotIdentified, (int)DefensiveCombatDecisionAction.Withdraw)]
    [InlineData(FireDirectedEnergyOutcome.NotAtSameLocation, (int)DefensiveCombatDecisionAction.Hold)]
    [InlineData(FireDirectedEnergyOutcome.OutOfRange, (int)DefensiveCombatDecisionAction.Withdraw)]
    [InlineData(FireDirectedEnergyOutcome.WeaponUnpowered, (int)DefensiveCombatDecisionAction.Withdraw)]
    [InlineData(FireDirectedEnergyOutcome.WeaponOffline, (int)DefensiveCombatDecisionAction.Withdraw)]
    [InlineData(FireDirectedEnergyOutcome.CooldownActive, (int)DefensiveCombatDecisionAction.Withdraw)]
    [InlineData(FireDirectedEnergyOutcome.TimeLimitExceeded, (int)DefensiveCombatDecisionAction.Withdraw)]
    public void ConstraintsRankOneExplainedAction(FireDirectedEnergyOutcome rejection, int actionValue)
    {
        var action = (DefensiveCombatDecisionAction)actionValue;
        DefensiveCombatDecisionInput input = Input();
        input = rejection switch
        {
            FireDirectedEnergyOutcome.ContactNotFound => input with { Contact = null },
            FireDirectedEnergyOutcome.ContactNotCurrent => input with { Contact = Snapshot(SensorContactStatus.Stale) },
            FireDirectedEnergyOutcome.ContactNotIdentified => input with
            {
                Contact = Snapshot(identification: SensorContactIdentification.Detected),
            },
            FireDirectedEnergyOutcome.NotAtSameLocation => input with { SameContext = false },
            FireDirectedEnergyOutcome.OutOfRange => input with
            {
                Own = input.Own with { Position = new TacticalPosition(100, 0) },
            },
            FireDirectedEnergyOutcome.WeaponUnpowered => input with { Own = input.Own with { WeaponCapability = 0 } },
            FireDirectedEnergyOutcome.WeaponOffline => input with
            {
                Own = input.Own with { WeaponCondition = default },
            },
            FireDirectedEnergyOutcome.CooldownActive => input with
            {
                Own = input.Own with { ReadyAt = new SimulationTime(12000) },
            },
            FireDirectedEnergyOutcome.TimeLimitExceeded => input with
            {
                Own = input.Own with { Time = new SimulationTime((long.MaxValue - 3000) / 100 * 100) },
            },
            _ => input,
        };
        DefensiveCombatDecisionExplanation result = DefensiveCombatDecisionPolicy.Evaluate(input);
        Assert.Equal(action, result.SelectedAction);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(rejection, result.Candidates[0].FireRejection);
        Assert.Equal(ShipSystemKind.DirectedEnergyWeapons, result.TargetSystem);
        Assert.Equal(DefensiveCombatDecisionTieRule.ReturnFireThenWithdrawThenHold, result.TieRule);
        if (action == DefensiveCombatDecisionAction.ReturnFire)
            Assert.All(result.Candidates[0].Constraints, constraint => Assert.True(constraint.Satisfied));
        else
            Assert.Contains(result.Candidates[0].Constraints, constraint => !constraint.Satisfied);
        if (action == DefensiveCombatDecisionAction.Withdraw)
        {
            Assert.NotNull(result.ResultingCourse);
            Assert.Equal(input.Own.MaximumSpeed, result.ResultingCourse.Value.Speed);
        }
        else
            Assert.Null(result.ResultingCourse);
    }

    /// <summary>Hold issues no implicit stop, and zero displacement or propulsion rejects withdrawal.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HoldPreservesExistingCourse(bool coincident)
    {
        DefensiveCombatDecisionInput input = Input();
        input = input with
        {
            Own = input.Own with
            {
                WeaponCapability = 0,
                Position = coincident ? input.Contact!.LastObservedPosition : input.Own.Position,
                MaximumSpeed = coincident ? input.Own.MaximumSpeed : default,
            },
        };
        DefensiveCombatDecisionExplanation result = DefensiveCombatDecisionPolicy.Evaluate(input);
        Assert.Equal(DefensiveCombatDecisionAction.Hold, result.SelectedAction);
        Assert.Null(result.ResultingCourse);
    }

    /// <summary>Own readiness and observed finite coordinates produce bounded deterministic facts.</summary>
    private static DefensiveCombatDecisionInput Input()
    {
        var weapon = new DirectedEnergyWeaponDefinition(new DistanceKilometers(20), 0.25, new SimulationDuration(2000));
        var own = new CombatOwnFacts(
            new SimulationTime(10100),
            true,
            default,
            new TacticalMotion(new HeadingDegrees(90), new SpeedKilometersPerSecond(1)),
            new SpeedKilometersPerSecond(10),
            weapon,
            new SystemCondition(1),
            1,
            default
        );
        return new DefensiveCombatDecisionInput(
            own,
            Snapshot(),
            true,
            new CombatStimulus(
                new SensorContactId(1),
                new SimulationTime(10000),
                new SimulationTime(10100),
                new ScheduledWorkId(1)
            )
        );
    }

    private static SensorContactSnapshot Snapshot(
        SensorContactStatus status = SensorContactStatus.Current,
        SensorContactIdentification identification = SensorContactIdentification.Identified
    ) =>
        new(
            new SensorContactId(1),
            new TacticalPosition(10, 0),
            new SimulationTime(10000),
            status,
            identification,
            identification == SensorContactIdentification.Identified ? "Known vessel" : null,
            identification == SensorContactIdentification.Identified ? "Known design" : null
        );
}
