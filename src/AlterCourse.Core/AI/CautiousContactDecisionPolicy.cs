using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.AI;

/// <summary>Evaluates the project-owned deterministic cautious-contact policy from actor-safe facts.</summary>
public static class CautiousContactDecisionPolicy
{
    private const double ProofSpeedKilometersPerSecond = 0.5;

    /// <summary>Returns a typed command and complete explanation without mutating the supplied snapshot.</summary>
    public static ShipContactDecisionExplanation Evaluate(ShipContactDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        SensorContactSnapshot? primary = SelectPrimaryContact(input.Facts);
        bool incomingHail = primary is not null && IsValidIncomingHail(input.Facts, primary);
        double movementSpeed = Math.Min(ProofSpeedKilometersPerSecond, input.Facts.MaximumTacticalSpeed.Value);
        ShipContactConstraintEvaluation[] movementConstraints = MovementConstraints(
            input.Facts,
            primary,
            movementSpeed
        );
        bool movementLegal = movementConstraints.All(result => result.Satisfied);

        ShipContactDecisionCandidate[] candidates =
        [
            HoldCandidate(primary, incomingHail),
            MovementCandidate(ShipContactDecisionAction.Approach, movementConstraints, primary, incomingHail),
            MovementCandidate(ShipContactDecisionAction.Withdraw, movementConstraints, primary, incomingHail),
        ];
        ShipContactDecisionCandidate selected = SelectCandidate(candidates);

        SetTacticalCourseIntent? course = selected.Action switch
        {
            ShipContactDecisionAction.Hold when input.Facts.IsAtLocation => new SetTacticalCourseIntent(
                input.Facts.OwnMotion.Heading,
                new SpeedKilometersPerSecond(0)
            ),
            ShipContactDecisionAction.Approach when movementLegal => MovementCourse(
                input.Facts,
                primary!,
                movementSpeed
            ),
            ShipContactDecisionAction.Withdraw when movementLegal => WithdrawCourse(
                input.Facts,
                primary!,
                movementSpeed
            ),
            _ => null,
        };

        return new ShipContactDecisionExplanation(
            input.DecidingShipId,
            input.DecisionTime,
            input.Goal,
            input.Posture,
            input.Facts,
            primary?.Id,
            candidates,
            ShipContactDecisionTieRule.HoldApproachWithdraw,
            selected.Action,
            course,
            false
        );
    }

    internal static ShipContactDecisionCandidate SelectCandidate(
        IEnumerable<ShipContactDecisionCandidate> candidates
    ) =>
        candidates
            .Where(candidate => candidate.HardConstraintsSatisfied && candidate.Score is not null)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => TieBreakPriority(candidate.Action))
            .First();

    private static int TieBreakPriority(ShipContactDecisionAction action) =>
        action switch
        {
            ShipContactDecisionAction.Hold => 1,
            ShipContactDecisionAction.Approach => 2,
            ShipContactDecisionAction.Withdraw => 3,
            _ => throw new InvalidOperationException("A cautious-contact candidate has an unsupported action."),
        };

    private static ShipContactConstraintEvaluation[] MovementConstraints(
        ShipContactDecisionFacts facts,
        SensorContactSnapshot? primary,
        double movementSpeed
    ) =>
        [
            new(ShipContactDecisionConstraint.AtLocation, facts.IsAtLocation),
            new(ShipContactDecisionConstraint.CurrentPrimaryContact, primary is not null),
            new(
                ShipContactDecisionConstraint.NonzeroObservedDisplacement,
                primary is not null && HasNonzeroDisplacement(facts, primary)
            ),
            new(ShipContactDecisionConstraint.LegalMovementSpeed, movementSpeed > 0),
        ];

    private static SensorContactSnapshot? SelectPrimaryContact(ShipContactDecisionFacts facts)
    {
        SensorContactSnapshot? hailed = facts.IncomingHail is null
            ? null
            : facts.Contacts.FirstOrDefault(contact => IsValidIncomingHail(facts, contact));
        return hailed
            ?? facts
                .Contacts.Where(contact => contact.Status == SensorContactStatus.Current)
                .OrderBy(contact => DistanceKey(facts.OwnPosition, contact.LastObservedPosition))
                .ThenBy(contact => contact.Id.Value)
                .FirstOrDefault();
    }

    private static bool IsValidIncomingHail(ShipContactDecisionFacts facts, SensorContactSnapshot contact) =>
        facts.IncomingHail is { } hail
        && contact.Id == hail.SourceContactId
        && contact.Status == SensorContactStatus.Current
        && contact.Identification == SensorContactIdentification.Identified
        && string.Equals(contact.KnownVesselDisplayName, hail.TransmittedVesselDisplayName, StringComparison.Ordinal)
        && string.Equals(contact.KnownDesignDisplayName, hail.TransmittedDesignDisplayName, StringComparison.Ordinal);

    private static ShipContactDecisionCandidate HoldCandidate(SensorContactSnapshot? primary, bool incomingHail)
    {
        (int score, ShipContactDecisionPolicyReason reason) = primary switch
        {
            null => (100, ShipContactDecisionPolicyReason.NoCurrentContactHold),
            _ when incomingHail && primary.Identification == SensorContactIdentification.Identified => (
                100,
                ShipContactDecisionPolicyReason.IdentifiedHailHold
            ),
            { Identification: SensorContactIdentification.Identified } => (
                80,
                ShipContactDecisionPolicyReason.IdentifiedContactHold
            ),
            _ => (10, ShipContactDecisionPolicyReason.UnidentifiedContactHoldAlternative),
        };
        return new ShipContactDecisionCandidate(
            ShipContactDecisionAction.Hold,
            [new ShipContactConstraintEvaluation(ShipContactDecisionConstraint.HoldAvailable, true)],
            score,
            reason
        );
    }

    private static ShipContactDecisionCandidate MovementCandidate(
        ShipContactDecisionAction action,
        ShipContactConstraintEvaluation[] constraints,
        SensorContactSnapshot? primary,
        bool incomingHail
    )
    {
        if (constraints.Any(result => !result.Satisfied))
        {
            return new ShipContactDecisionCandidate(
                action,
                constraints,
                null,
                ShipContactDecisionPolicyReason.HardConstraintRejected
            );
        }

        bool identified = primary!.Identification == SensorContactIdentification.Identified;
        return action switch
        {
            ShipContactDecisionAction.Approach => new ShipContactDecisionCandidate(
                action,
                constraints,
                identified || incomingHail ? 30 : 50,
                identified || incomingHail
                    ? ShipContactDecisionPolicyReason.IdentifiedContactApproachAlternative
                    : ShipContactDecisionPolicyReason.CautiousApproachAlternative
            ),
            ShipContactDecisionAction.Withdraw => new ShipContactDecisionCandidate(
                action,
                constraints,
                identified || incomingHail ? 20 : 100,
                identified || incomingHail
                    ? ShipContactDecisionPolicyReason.IdentifiedContactWithdrawAlternative
                    : ShipContactDecisionPolicyReason.UnidentifiedContactWithdraw
            ),
            _ => throw new InvalidOperationException("Movement candidate action is unsupported."),
        };
    }

    private static SetTacticalCourseIntent MovementCourse(
        ShipContactDecisionFacts facts,
        SensorContactSnapshot primary,
        double speed
    ) => new(HeadingTo(facts.OwnPosition, primary.LastObservedPosition), new SpeedKilometersPerSecond(speed));

    private static SetTacticalCourseIntent WithdrawCourse(
        ShipContactDecisionFacts facts,
        SensorContactSnapshot primary,
        double speed
    ) =>
        new(
            new HeadingDegrees(HeadingTo(facts.OwnPosition, primary.LastObservedPosition).Value + 180),
            new SpeedKilometersPerSecond(speed)
        );

    private static HeadingDegrees HeadingTo(TacticalPosition origin, TacticalPosition destination)
    {
        double deltaX = destination.XKilometers - origin.XKilometers;
        double deltaY = destination.YKilometers - origin.YKilometers;
        return new HeadingDegrees(Math.Atan2(deltaX, deltaY) * 180 / Math.PI);
    }

    private static bool HasNonzeroDisplacement(ShipContactDecisionFacts facts, SensorContactSnapshot contact) =>
        DistanceKey(facts.OwnPosition, contact.LastObservedPosition).NonzeroRank != 0;

    private static (int NonzeroRank, int Exponent, double Significand) DistanceKey(
        TacticalPosition left,
        TacticalPosition right
    )
    {
        (int Exponent, double Significand) deltaX = AbsoluteDifference(left.XKilometers, right.XKilometers);
        (int Exponent, double Significand) deltaY = AbsoluteDifference(left.YKilometers, right.YKilometers);
        if (deltaX.Significand == 0 && deltaY.Significand == 0)
        {
            return default;
        }

        int componentExponent =
            deltaX.Significand == 0 ? deltaY.Exponent
            : deltaY.Significand == 0 ? deltaX.Exponent
            : Math.Max(deltaX.Exponent, deltaY.Exponent);
        double scaledX = Math.ScaleB(deltaX.Significand, deltaX.Exponent - componentExponent);
        double scaledY = Math.ScaleB(deltaY.Significand, deltaY.Exponent - componentExponent);
        double normalizedDistance = Math.Sqrt((scaledX * scaledX) + (scaledY * scaledY));
        int distanceExponent = Math.ILogB(normalizedDistance);
        return (1, checked(componentExponent + distanceExponent), Math.ScaleB(normalizedDistance, -distanceExponent));
    }

    private static (int Exponent, double Significand) AbsoluteDifference(double left, double right)
    {
        double difference = left - right;
        int exponentOffset = 0;
        if (!double.IsFinite(difference))
        {
            // Scaling operands before subtraction preserves an overflowing opposite-sign difference. Scaling the
            // coordinates as a group would instead erase a small displacement beside an unrelated huge coordinate.
            difference = Math.ScaleB(left, -1) - Math.ScaleB(right, -1);
            exponentOffset = 1;
        }

        double magnitude = Math.Abs(difference);
        if (magnitude == 0)
        {
            return default;
        }

        int exponent = Math.ILogB(magnitude);
        return (checked(exponent + exponentOffset), Math.ScaleB(magnitude, -exponent));
    }
}
