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

    /// <summary>Confirms finite opposite extremes retain their geometric direction despite overflowing displacement.</summary>
    [Fact]
    public void WithdrawalPreservesHeadingWhenBothDisplacementComponentsOverflow()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [Contact(1, new TacticalPosition(double.MaxValue, double.MaxValue / 2))],
                ownPosition: new TacticalPosition(-double.MaxValue, -double.MaxValue)
            )
        );

        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        // The endpoint differences have ratio 2:1.5, independently of their unrepresentable magnitudes.
        double expectedHeading = Math.Atan2(2, 1.5) * 180 / Math.PI + 180;
        Assert.Equal(expectedHeading, result.ResultingCourse!.Value.Heading.Value, 10);
        Assert.Equal(0.5, result.ResultingCourse.Value.Speed.Value);
    }

    /// <summary>Confirms overflow in one component scales the other component by the same factor.</summary>
    [Fact]
    public void WithdrawalPreservesHeadingWhenOneDisplacementComponentOverflows()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [Contact(1, new TacticalPosition(double.MaxValue, double.MaxValue / 2))],
                ownPosition: new TacticalPosition(-double.MaxValue, 0)
            )
        );

        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        double expectedHeading = Math.Atan2(2, 0.5) * 180 / Math.PI + 180;
        Assert.Equal(expectedHeading, result.ResultingCourse!.Value.Heading.Value, 10);
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

    /// <summary>Confirms equal candidate scores use the declared action key rather than input order.</summary>
    [Fact]
    public void CandidateScoreTieUsesStableActionKey()
    {
        ShipContactDecisionCandidate[] candidates =
        [
            Candidate(ShipContactDecisionAction.Withdraw),
            Candidate(ShipContactDecisionAction.Approach),
            Candidate(ShipContactDecisionAction.Hold),
        ];

        ShipContactDecisionCandidate selected = CautiousContactDecisionPolicy.SelectCandidate(candidates);

        Assert.Equal(ShipContactDecisionAction.Hold, selected.Action);
        Assert.Equal(selected, CautiousContactDecisionPolicy.SelectCandidate(candidates.Reverse()));
    }

    /// <summary>Confirms nearest selection remains ordered for finite distances whose squares overflow.</summary>
    [Fact]
    public void PrimarySelectionHandlesExtremeFiniteDistances()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(3, new TacticalPosition(1.1e200, 1.1e200)), Contact(9, new TacticalPosition(1e200, 1e200))])
        );

        Assert.Equal(new SensorContactId(9), result.PrimaryContactId);
    }

    /// <summary>Confirms opposite-sign finite coordinates remain ordered when raw subtraction overflows.</summary>
    [Fact]
    public void PrimarySelectionHandlesOppositeFiniteExtremes()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [
                    Contact(3, new TacticalPosition(double.MaxValue, 0)),
                    Contact(9, new TacticalPosition(double.MaxValue * 0.5, 0)),
                ],
                ownPosition: new TacticalPosition(-double.MaxValue, 0)
            )
        );

        Assert.Equal(new SensorContactId(9), result.PrimaryContactId);
    }

    /// <summary>Confirms a tiny local displacement survives an unrelated huge shared coordinate.</summary>
    [Fact]
    public void PrimarySelectionPreservesMixedScaleDisplacement()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [Contact(3, new TacticalPosition(1e308, 2e-300)), Contact(9, new TacticalPosition(1e308, 1e-300))],
                ownPosition: new TacticalPosition(1e308, 0)
            )
        );

        Assert.Equal(new SensorContactId(9), result.PrimaryContactId);
        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        Assert.NotNull(result.ResultingCourse);
        Assert.Equal(180, result.ResultingCourse.Value.Heading.Value);
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

    /// <summary>Verifies the public evaluator rejects missing input before reading actor facts.</summary>
    [Fact]
    public void NullInputIsRejectedWithArgumentValidation()
    {
        Assert.Throws<ArgumentNullException>("input", () => CautiousContactDecisionPolicy.Evaluate(null!));
    }

    /// <summary>Characterizes every candidate score and reason for the approved cautious policy.</summary>
    [Theory]
    [InlineData(false, false, 10, 50, 100)]
    [InlineData(true, false, 80, 30, 20)]
    [InlineData(true, true, 100, 30, 20)]
    public void CompleteCandidateExplanationReflectsIdentificationAndValidHail(
        bool identified,
        bool hailed,
        int holdScore,
        int approachScore,
        int withdrawScore
    )
    {
        SensorContactSnapshot contact = Contact(
            1,
            new TacticalPosition(3, 4),
            identification: identified ? SensorContactIdentification.Identified : SensorContactIdentification.Detected
        );
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([contact], incomingHail: hailed ? new IncomingHailFact(contact.Id, "Ship 1", "Design 1") : null)
        );

        Assert.Equal(contact.Id, result.PrimaryContactId);
        Assert.Equal(
            identified ? ShipContactDecisionAction.Hold : ShipContactDecisionAction.Withdraw,
            result.SelectedAction
        );
        Assert.Equal(
            new int?[] { holdScore, approachScore, withdrawScore },
            result.Candidates.Select(candidate => candidate.Score)
        );
        Assert.Equal(
            new[]
            {
                hailed ? ShipContactDecisionPolicyReason.IdentifiedHailHold
                : identified ? ShipContactDecisionPolicyReason.IdentifiedContactHold
                : ShipContactDecisionPolicyReason.UnidentifiedContactHoldAlternative,
                identified
                    ? ShipContactDecisionPolicyReason.IdentifiedContactApproachAlternative
                    : ShipContactDecisionPolicyReason.CautiousApproachAlternative,
                identified
                    ? ShipContactDecisionPolicyReason.IdentifiedContactWithdrawAlternative
                    : ShipContactDecisionPolicyReason.UnidentifiedContactWithdraw,
            },
            result.Candidates.Select(candidate => candidate.PolicyReason)
        );
        Assert.All(result.Candidates, candidate => Assert.True(candidate.HardConstraintsSatisfied));
        if (identified)
        {
            Assert.Equal(27, result.ResultingCourse!.Value.Heading.Value);
            Assert.Equal(0, result.ResultingCourse.Value.Speed.Value);
        }
    }

    /// <summary>Verifies invalid hail evidence cannot override nearest current contact selection.</summary>
    [Theory]
    [InlineData(99, "Ship 2", "Design 2", SensorContactStatus.Current, SensorContactIdentification.Identified)]
    [InlineData(2, "ship 2", "Design 2", SensorContactStatus.Current, SensorContactIdentification.Identified)]
    [InlineData(2, "Ship 2", "design 2", SensorContactStatus.Current, SensorContactIdentification.Identified)]
    [InlineData(2, "Ship 2", "Design 2", SensorContactStatus.Stale, SensorContactIdentification.Identified)]
    [InlineData(2, "Ship 2", "Design 2", SensorContactStatus.Lost, SensorContactIdentification.Identified)]
    [InlineData(2, "Ship 2", "Design 2", SensorContactStatus.Current, SensorContactIdentification.Detected)]
    public void InvalidIncomingHailFallsBackToNearestCurrentContact(
        long sourceId,
        string vesselName,
        string designName,
        SensorContactStatus status,
        SensorContactIdentification identification
    )
    {
        SensorContactSnapshot nearest = Contact(1, new TacticalPosition(1, 0));
        SensorContactSnapshot distant = Contact(2, new TacticalPosition(9, 0), status, identification);
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [distant, nearest],
                incomingHail: new IncomingHailFact(new SensorContactId(sourceId), vesselName, designName)
            )
        );

        Assert.Equal(nearest.Id, result.PrimaryContactId);
        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        Assert.Equal(ShipContactDecisionPolicyReason.UnidentifiedContactWithdraw, result.Candidates[2].PolicyReason);
    }

    /// <summary>Verifies overflowing northward displacement retains its finite eastward component ratio.</summary>
    [Fact]
    public void WithdrawalPreservesHeadingWhenOnlyYDisplacementOverflows()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [Contact(1, new TacticalPosition(double.MaxValue / 2, double.MaxValue))],
                ownPosition: new TacticalPosition(0, -double.MaxValue)
            )
        );

        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        Assert.Equal(Math.Atan2(0.5, 2) * 180 / Math.PI + 180, result.ResultingCourse!.Value.Heading.Value, 10);
    }

    /// <summary>Verifies minimum nonzero coordinates retain their diagonal withdrawal direction.</summary>
    [Fact]
    public void WithdrawalPreservesSmallestSubnormalDisplacementDirection()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(double.Epsilon, double.Epsilon))])
        );

        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
        Assert.Equal(225, result.ResultingCourse!.Value.Heading.Value, 10);
    }

    /// <summary>Verifies nearest selection against independently ordered geometric distances.</summary>
    [Theory]
    [InlineData(3, 3, 5, 0)]
    [InlineData(1e200, 1e-200, 2e200, 0)]
    [InlineData(1e-200, 1e200, 0, 2e200)]
    [InlineData(1e-300, 1e-310, 2e-300, 0)]
    [InlineData(1e200, 1e199, 2e200, 2e199)]
    public void NearestSelectionPreservesGeometricOrderAcrossComponentScales(
        double nearX,
        double nearY,
        double farX,
        double farY
    )
    {
        // The diagonal (3,3) is inside radius five. For the other pairs, the near radius is below sqrt(2)
        // times its dominant component, while the far radius is at least twice that component.
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([Contact(1, new TacticalPosition(farX, farY)), Contact(9, new TacticalPosition(nearX, nearY))])
        );

        Assert.Equal(new SensorContactId(9), result.PrimaryContactId);
        Assert.Equal(ShipContactDecisionAction.Withdraw, result.SelectedAction);
    }

    /// <summary>Verifies subnormal diagonal distance remains nearer than twice its axis component.</summary>
    [Fact]
    public void NearestSelectionOrdersSubnormalDistancesByGeometry()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input([
                Contact(1, new TacticalPosition(2 * double.Epsilon, 0)),
                Contact(9, new TacticalPosition(double.Epsilon, double.Epsilon)),
            ])
        );

        Assert.Equal(new SensorContactId(9), result.PrimaryContactId);
        Assert.Equal(225, result.ResultingCourse!.Value.Heading.Value, 10);
    }

    /// <summary>Verifies unrepresentable displacement remains farther than a representable axis distance.</summary>
    [Fact]
    public void RepresentableDistanceIsNearerThanOverflowingDisplacement()
    {
        ShipContactDecisionExplanation result = CautiousContactDecisionPolicy.Evaluate(
            Input(
                [Contact(1, new TacticalPosition(double.MaxValue, 0)), Contact(9, new TacticalPosition(0, 0))],
                ownPosition: new TacticalPosition(-double.MaxValue, 0)
            )
        );

        Assert.Equal(new SensorContactId(9), result.PrimaryContactId);
        Assert.Equal(270, result.ResultingCourse!.Value.Heading.Value);
    }

    private static ShipContactDecisionInput Input(
        IEnumerable<SensorContactSnapshot> contacts,
        double maximumSpeed = 2,
        IncomingHailFact? incomingHail = null,
        bool isAtLocation = true,
        TacticalPosition ownPosition = default
    ) =>
        new(
            DecidingShipId,
            new SimulationTime(1200),
            ShipContactDecisionGoal.RespondCautiously,
            ShipContactPosture.CautiousContact,
            new ShipContactDecisionFacts(
                ownPosition,
                new TacticalMotion(new HeadingDegrees(27), new SpeedKilometersPerSecond(1)),
                isAtLocation,
                new SpeedKilometersPerSecond(maximumSpeed),
                contacts,
                incomingHail
            )
        );

    private static ShipContactDecisionCandidate Candidate(ShipContactDecisionAction action) =>
        new(
            action,
            [new ShipContactConstraintEvaluation(ShipContactDecisionConstraint.HoldAvailable, true)],
            50,
            ShipContactDecisionPolicyReason.NoCurrentContactHold
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
