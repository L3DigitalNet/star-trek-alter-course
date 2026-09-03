using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.AI;

/// <summary>Verifies the pure cautious-contact policy and its actor-safe explanation contract.</summary>
public sealed class CautiousContactDecisionPolicyTests
{
    private static readonly ShipInstanceId DecidingShipId = new(7);

    /// <summary>Confirms repeated evaluation produces the same complete explanation without randomness or mutation.</summary>
    [Fact]
    public void SameInputProducesSameExplanationWithoutMutatingActorFacts()
    {
        var source = new List<SensorContactSnapshot> { Contact(2, new TacticalPosition(4, 0)) };
        ShipContactDecisionInput input = Input(source);
        source.Clear();

        ShipContactDecisionExplanation first = CautiousContactDecisionPolicy.Evaluate(input);
        ShipContactDecisionExplanation second = CautiousContactDecisionPolicy.Evaluate(input);

        Assert.Equal(first, second);
        Assert.Equal(first.DecidingShipId, second.DecidingShipId);
        Assert.Equal(first.DecisionTime, second.DecisionTime);
        Assert.Equal(first.Goal, second.Goal);
        Assert.Equal(first.Posture, second.Posture);
        Assert.Equal(first.PrimaryContactId, second.PrimaryContactId);
        Assert.Equal(first.SelectedAction, second.SelectedAction);
        Assert.Equal(first.ResultingCourse, second.ResultingCourse);
        Assert.Single(input.Facts.Contacts);
        Assert.False(first.RandomnessUsed);
    }

    /// <summary>Confirms an unidentified current contact favors withdrawal over a legal approach.</summary>
    [Fact]
    public void UnidentifiedCurrentContactWithdrawsFromObservedPosition()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(3, 4))])
        );

        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        Assert.Equal(216.86989764584402, result.ResultingCourse!.Value.Heading.Value, 10);
        Assert.Equal(0.5, result.ResultingCourse.Value.Speed.Value);
        ShipContactDecisionCandidate approach = result.Candidates[1];
        ShipContactDecisionCandidate withdraw = result.Candidates[2];
        Assert.True(approach.HardConstraintsSatisfied);
        Assert.True(withdraw.HardConstraintsSatisfied);
        Assert.True(approach.Score < withdraw.Score);
        Assert.Equal(ShipContactDecisionPolicyReason.UnidentifiedContactWithdraw, withdraw.PolicyReason);
    }

    /// <summary>Confirms an identified current contact that hailed is selected first and causes a hold.</summary>
    [Fact]
    public void IdentifiedIncomingHailSelectsItsContactAndHolds()
    {
        SensorContactSnapshot nearer = Contact(1, new TacticalPosition(1, 0));
        SensorContactSnapshot hailed = Contact(
            2,
            new TacticalPosition(9, 0),
            SensorContactStatus.Current,
            SensorContactIdentification.Identified
        );
        var incoming = new IncomingHailFact(hailed.Id, "Ship 2", "Design 2");

        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([nearer, hailed], incomingHail: incoming)
        );

        Assert.Equal(hailed.Id, result.PrimaryContactId);
        Assert.Equal(ShipContactDecisionAction.Hold, result.SelectedAction);
        Assert.Equal(0, result.ResultingCourse!.Value.Speed.Value);
        Assert.Equal(ShipContactDecisionPolicyReason.IdentifiedHailHold, result.Candidates[0].PolicyReason);
        Assert.Equal(incoming, result.ActorKnownFacts.IncomingHail);
    }

    /// <summary>Confirms absent current knowledge produces a deliberate hold and rejects blind movement.</summary>
    [Fact]
    public void NoCurrentContactHoldsWithoutPursuit()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(3, 0), SensorContactStatus.Lost)])
        );

        Assert.Null(result.PrimaryContactId);
        Assert.Equal(ShipContactDecisionAction.Hold, result.SelectedAction);
        Assert.Equal(0, result.ResultingCourse!.Value.Speed.Value);
        Assert.Equal(ShipContactDecisionPolicyReason.NoCurrentContactHold, result.Candidates[0].PolicyReason);
        Assert.All(
            result.Candidates.Skip(1),
            candidate =>
                Assert.False(
                    candidate
                        .Constraints.Single(evaluation =>
                            evaluation.Constraint == ShipContactDecisionConstraint.CurrentPrimaryContact
                        )
                        .Satisfied
                )
        );
    }

    /// <summary>Confirms coincident observed positions reject both movement candidates.</summary>
    [Fact]
    public void ZeroObservedDisplacementRejectsMovement()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(0, 0))])
        );

        Assert.Equal(ShipContactDecisionAction.Hold, result.SelectedAction);
        Assert.All(
            result.Candidates.Skip(1),
            candidate =>
            {
                Assert.False(candidate.HardConstraintsSatisfied);
                Assert.Null(candidate.Score);
                Assert.False(
                    candidate
                        .Constraints.Single(evaluation =>
                            evaluation.Constraint == ShipContactDecisionConstraint.NonzeroObservedDisplacement
                        )
                        .Satisfied
                );
            }
        );
    }

    /// <summary>Confirms proof movement speed is clamped to the deciding ship's own capability.</summary>
    [Fact]
    public void ProofSpeedClampsToOwnMaximum()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(0, 5))], maximumSpeed: 0.2)
        );

        Assert.Equal(0.2, result.ResultingCourse!.Value.Speed.Value);
        Assert.All(
            result.Candidates.Skip(1),
            candidate =>
                Assert.True(
                    candidate
                        .Constraints.Single(evaluation =>
                            evaluation.Constraint == ShipContactDecisionConstraint.LegalMovementSpeed
                        )
                        .Satisfied
                )
        );
    }

    /// <summary>Confirms location and speed capability are independently visible hard constraints.</summary>
    [Fact]
    public void MovementReportsLocationAndSpeedConstraintFailures()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(0, 5))], maximumSpeed: 0, isAtLocation: false)
        );

        Assert.Equal(ShipContactDecisionAction.Hold, result.SelectedAction);
        Assert.Null(result.ResultingCourse);
        Assert.All(
            result.Candidates.Skip(1),
            candidate =>
            {
                Assert.False(
                    candidate
                        .Constraints.Single(evaluation =>
                            evaluation.Constraint == ShipContactDecisionConstraint.AtLocation
                        )
                        .Satisfied
                );
                Assert.False(
                    candidate
                        .Constraints.Single(evaluation =>
                            evaluation.Constraint == ShipContactDecisionConstraint.LegalMovementSpeed
                        )
                        .Satisfied
                );
            }
        );
    }

    /// <summary>Confirms nearest-distance ties use local contact identity and all candidates remain visible.</summary>
    [Fact]
    public void PrimaryTieUsesLocalIdentityAndCandidateOrderIsStable()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(9, new TacticalPosition(-2, 0)), Contact(3, new TacticalPosition(2, 0))])
        );

        Assert.Equal(new SensorContactId(3), result.PrimaryContactId);
        Assert.Equal(ShipContactDecisionTieRule.HoldApproachWithdraw, result.TieRule);
        Assert.Equal(
            [ShipContactDecisionAction.Hold, ShipContactDecisionAction.Approach, ShipContactDecisionAction.Withdraw],
            result.Candidates.Select(candidate => candidate.Action)
        );
        Assert.Single(result.Candidates[0].Constraints);
        Assert.Equal(4, result.Candidates[1].Constraints.Count);
        Assert.Equal(4, result.Candidates[2].Constraints.Count);
        Assert.Equal(
            [
                ShipContactDecisionConstraint.AtLocation,
                ShipContactDecisionConstraint.CurrentPrimaryContact,
                ShipContactDecisionConstraint.NonzeroObservedDisplacement,
                ShipContactDecisionConstraint.LegalMovementSpeed,
            ],
            result.Candidates[1].Constraints.Select(evaluation => evaluation.Constraint)
        );
    }

    /// <summary>Confirms hidden world and definition types cannot enter the public policy input graph.</summary>
    [Fact]
    public void PolicyInputExcludesHiddenTruthTypes()
    {
        Type[] exposedTypes =
        [
            .. typeof(ShipContactDecisionInput).GetProperties().Select(property => property.PropertyType),
            .. typeof(ShipContactDecisionFacts).GetProperties().Select(property => property.PropertyType),
            .. typeof(SensorContactSnapshot).GetProperties().Select(property => property.PropertyType),
        ];

        Assert.DoesNotContain(typeof(ShipState), exposedTypes);
        Assert.DoesNotContain(typeof(ShipDefinition), exposedTypes);
        Assert.DoesNotContain(typeof(SimulationState), exposedTypes);
        Assert.DoesNotContain(
            typeof(SensorContactSnapshot).GetProperties(),
            property => property.Name.Contains("Target", StringComparison.Ordinal)
        );
    }

    private static ShipContactDecisionInput Input(
        IEnumerable<SensorContactSnapshot> contacts,
        double maximumSpeed = 2,
        IncomingHailFact? incomingHail = null,
        bool isAtLocation = true
    ) =>
        new(
            DecidingShipId,
            new SimulationTime(1200),
            ShipContactDecisionGoal.RespondCautiously,
            ShipContactPosture.CautiousContact,
            new ShipContactDecisionFacts(
                default,
                new TacticalMotion(new HeadingDegrees(27), new SpeedKilometersPerSecond(1)),
                isAtLocation,
                new SpeedKilometersPerSecond(maximumSpeed),
                contacts,
                incomingHail
            )
        );

    private static SensorContactSnapshot Contact(
        long id,
        TacticalPosition observedPosition,
        SensorContactStatus status = SensorContactStatus.Current,
        SensorContactIdentification identification = SensorContactIdentification.Detected
    ) =>
        new(
            new SensorContactId(id),
            observedPosition,
            new SimulationTime(1000),
            status,
            identification,
            identification == SensorContactIdentification.Identified ? $"Ship {id}" : null,
            identification == SensorContactIdentification.Identified ? $"Design {id}" : null
        );
}
