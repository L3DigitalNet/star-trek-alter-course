using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.AI;

/// <summary>Exercises withdrawal geometry through legal actor-safe decisions over represented finite coordinates.</summary>
public sealed class WithdrawalGeometryTests
{
    /// <summary>Verifies legal defensive withdrawal retains the smallest represented eastward displacement.</summary>
    [Fact]
    public void DefensiveWithdrawalFromSmallestPositiveXHeadsEast()
    {
        DefensiveCombatDecisionInput input = DefensiveInput(new TacticalPosition(double.Epsilon, 0), default);

        DefensiveCombatDecisionExplanation decision = DefensiveCombatDecisionPolicy.Evaluate(input);

        Assert.Equal(FireDirectedEnergyOutcome.WeaponUnpowered, decision.Candidates[0].FireRejection);
        Assert.Equal(DefensiveCombatDecisionAction.Withdraw, decision.SelectedAction);
        Assert.All(decision.Candidates[1].Constraints, constraint => Assert.True(constraint.Satisfied));
        Assert.Equal(90, decision.ResultingCourse!.Value.Heading.Value);
        Assert.Equal(input.Own.MaximumSpeed, decision.ResultingCourse.Value.Speed);
    }

    /// <summary>Verifies both policies against analytic headings over finite coordinate scales.</summary>
    [Theory]
    [MemberData(nameof(FiniteGeometryCases))]
    public void WithdrawalRetainsIndependentGeometricHeading(
        bool defensive,
        double ownX,
        double ownY,
        double contactX,
        double contactY,
        double expectedHeading
    )
    {
        SetTacticalCourseIntent course = Withdraw(
            defensive,
            new TacticalPosition(ownX, ownY),
            new TacticalPosition(contactX, contactY)
        );

        Assert.Equal(expectedHeading, course.Heading.Value, 10);
        Assert.Equal(defensive ? 2 : 0.5, course.Speed.Value);
    }

    /// <summary>Supplies signed axes, quadrants, overflow differences, and independent mixed-scale components.</summary>
    public static IEnumerable<object[]> FiniteGeometryCases()
    {
        foreach (bool defensive in new[] { false, true })
        {
            // Powers of two retain their represented displacement exactly, including the smallest subnormal.
            foreach (int exponent in new[] { -1074, -1073, -1022, -500, 0, 500, 1023 })
            {
                double scale = Math.ScaleB(1, exponent);
                foreach ((int x, int y, double heading) in new[]
                {
                    (1, 0, 90.0),
                    (-1, 0, 270.0),
                    (0, 1, 0.0),
                    (0, -1, 180.0),
                    (1, 1, 45.0),
                    (1, -1, 135.0),
                    (-1, -1, 225.0),
                    (-1, 1, 315.0),
                })
                {
                    yield return [defensive, x * scale, y * scale, 0.0, 0.0, heading];
                }
            }

            yield return [defensive, 3.0, 4.0, 0.0, 0.0, 36.86989764584402];
            yield return [defensive, 8.0, 12.0, 5.0, 8.0, 36.86989764584402];
            yield return [defensive, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 45.0];
            yield return [defensive, double.MaxValue, double.MaxValue / 2, -double.MaxValue, 0.0, 75.96375653207353];
            yield return [defensive, double.MaxValue / 2, double.MaxValue, 0.0, -double.MaxValue, 14.036243467926479];
            yield return [defensive, double.MaxValue, double.Epsilon, 0.0, 0.0, 90.0];
            yield return [defensive, double.Epsilon, -double.MaxValue, 0.0, 0.0, 180.0];
            yield return [defensive, double.MaxValue, double.Epsilon, double.MaxValue, 0.0, 0.0];
            yield return [defensive, double.Epsilon, double.MaxValue, 0.0, double.MaxValue, 90.0];
        }
    }

    /// <summary>Verifies exact binary coordinate rescaling preserves the represented 3:4 direction.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactPowerOfTwoRescalingPreservesWithdrawal(bool defensive)
    {
        // The integer endpoints and their 3:4 difference remain exact throughout this finite exponent range.
        foreach (int exponent in new[] { -1074, -1022, -500, 0, 500, 1019 })
        {
            SetTacticalCourseIntent course = Withdraw(
                defensive,
                new TacticalPosition(Math.ScaleB(8, exponent), Math.ScaleB(12, exponent)),
                new TacticalPosition(Math.ScaleB(5, exponent), Math.ScaleB(8, exponent))
            );

            Assert.Equal(36.86989764584402, course.Heading.Value, 10);
        }
    }

    /// <summary>Verifies coincident positions keep the policy's existing nonmovement behavior.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoincidentCoordinatesRetainHoldFallback(bool defensive)
    {
        var position = new TacticalPosition(double.Epsilon, double.MaxValue);
        if (defensive)
        {
            DefensiveCombatDecisionExplanation decision = DefensiveCombatDecisionPolicy.Evaluate(
                DefensiveInput(position, position)
            );
            Assert.Equal(DefensiveCombatDecisionAction.Hold, decision.SelectedAction);
            Assert.Null(decision.ResultingCourse);
            Assert.False(decision.Candidates[1].Constraints.Single(
                constraint => constraint.Constraint == DefensiveCombatConstraint.KnownDisplacement
            ).Satisfied);
        }
        else
        {
            ShipContactDecisionExplanation decision = CautiousContactDecisionPolicy.Evaluate(
                CautiousInput(position, position)
            );
            Assert.Equal(ShipContactDecisionAction.Hold, decision.SelectedAction);
            Assert.Equal(new HeadingDegrees(27), decision.ResultingCourse!.Value.Heading);
            Assert.Equal(0, decision.ResultingCourse.Value.Speed.Value);
            Assert.All(decision.Candidates.Skip(1), candidate => Assert.False(candidate.HardConstraintsSatisfied));
        }
    }

    private static SetTacticalCourseIntent Withdraw(bool defensive, TacticalPosition own, TacticalPosition contact)
    {
        if (defensive)
        {
            DefensiveCombatDecisionExplanation decision = DefensiveCombatDecisionPolicy.Evaluate(
                DefensiveInput(own, contact)
            );
            Assert.Equal(DefensiveCombatDecisionAction.Withdraw, decision.SelectedAction);
            Assert.All(decision.Candidates[1].Constraints, constraint => Assert.True(constraint.Satisfied));
            Assert.NotEqual(FireDirectedEnergyOutcome.Accepted, decision.Candidates[0].FireRejection);
            return decision.ResultingCourse!.Value;
        }

        ShipContactDecisionExplanation cautious = CautiousContactDecisionPolicy.Evaluate(CautiousInput(own, contact));
        Assert.Equal(ShipContactDecisionAction.Withdraw, cautious.SelectedAction);
        Assert.True(cautious.Candidates[2].HardConstraintsSatisfied);
        return cautious.ResultingCourse!.Value;
    }

    private static DefensiveCombatDecisionInput DefensiveInput(TacticalPosition own, TacticalPosition contact) =>
        new(
            new CombatOwnFacts(
                new SimulationTime(1200),
                true,
                own,
                new TacticalMotion(new HeadingDegrees(27), new SpeedKilometersPerSecond(1)),
                new SpeedKilometersPerSecond(2),
                new InstalledSystemId(5),
                new DirectedEnergyWeaponDefinition(new DistanceKilometers(20), 0.25, new SimulationDuration(2000)),
                new SystemCondition(1),
                default,
                default
            ),
            Contact(contact, SensorContactIdentification.Identified),
            true,
            new CombatStimulus(new SensorContactId(1), new SimulationTime(1000), new SimulationTime(1200), new ScheduledWorkId(1))
        );

    private static ShipContactDecisionInput CautiousInput(TacticalPosition own, TacticalPosition contact) =>
        new(
            new ShipInstanceId(7),
            new SimulationTime(1200),
            ShipContactDecisionGoal.RespondCautiously,
            ShipContactPosture.CautiousContact,
            new ShipContactDecisionFacts(
                own,
                new TacticalMotion(new HeadingDegrees(27), new SpeedKilometersPerSecond(1)),
                true,
                new SpeedKilometersPerSecond(2),
                [Contact(contact, SensorContactIdentification.Detected)],
                null
            )
        );

    private static SensorContactSnapshot Contact(TacticalPosition position, SensorContactIdentification identification) =>
        new(
            new SensorContactId(1),
            position,
            new SimulationTime(1000),
            SensorContactStatus.Current,
            identification,
            identification == SensorContactIdentification.Identified ? "Known vessel" : null,
            identification == SensorContactIdentification.Identified ? "Known design" : null
        );
}
