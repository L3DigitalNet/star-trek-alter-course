using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.AI;

/// <summary>Verifies the pure faction-assignment policy and its actor-safe explanation contract.</summary>
public sealed class FactionAssignmentPolicyTests
{
    private static readonly FactionId Faction = new(7);
    private static readonly LocationId Alpha = new("alpha");
    private static readonly LocationId Beta = new("beta");
    private static readonly LocationId Gamma = new("gamma");

    /// <summary>Confirms two eligible ships produce one proposal using the declared stable tie rule.</summary>
    [Fact]
    public void TwoEligibleShipsSelectLowestIdentityOnEqualRoutes()
    {
        FactionAssignmentDecisionExplanation result = FactionAssignmentPolicy.Evaluate(
            Input([AtLocation(9, Alpha), AtLocation(3, Alpha)], [Route(Alpha, Beta, 400)])
        );

        Assert.Equal(FactionAssignmentDecisionOutcome.AssignmentProposed, result.Outcome);
        Assert.Equal(new FactionAssignmentProposal(Faction, new ShipInstanceId(3), Beta), result.Proposal);
        Assert.Equal(FactionAssignmentDecisionTieRule.ShortestRouteThenLowestShipId, result.TieRule);
        Assert.Equal(new long[] { 3, 9 }, result.Candidates.Select(candidate => candidate.ShipId.Value));
        Assert.All(
            result.Candidates,
            candidate => Assert.Equal(FactionAssignmentCandidateReason.Eligible, candidate.Reason)
        );
        Assert.False(result.RandomnessUsed);
    }

    /// <summary>Confirms a commitment rejects the otherwise preferred ship and changes the proposal.</summary>
    [Fact]
    public void CommittedPreferredShipIsDiagnosedAndAlternateSelected()
    {
        FactionAssignmentDecisionExplanation result = FactionAssignmentPolicy.Evaluate(
            Input([AtLocation(3, Alpha, hasActiveOrder: true), AtLocation(9, Alpha)], [Route(Alpha, Beta, 400)])
        );

        Assert.Equal(new ShipInstanceId(9), result.Proposal!.ShipId);
        Assert.Equal(FactionAssignmentCandidateReason.AlreadyCommitted, result.Candidates[0].Reason);
        Assert.Equal(FactionAssignmentCandidateReason.Eligible, result.Candidates[1].Reason);
    }

    /// <summary>Confirms an empty roster and wholly invalid roster both yield deliberate no-action outcomes.</summary>
    [Fact]
    public void NoEligibleCandidateProducesNoProposal()
    {
        FactionAssignmentDecisionExplanation empty = FactionAssignmentPolicy.Evaluate(Input([], []));
        FactionAssignmentDecisionExplanation invalid = FactionAssignmentPolicy.Evaluate(
            Input(
                [AtLocation(1, Alpha, isPlayerShip: true), Traveling(2), AtLocation(3, Gamma)],
                [Route(Alpha, Beta, 400)]
            )
        );

        Assert.Equal(FactionAssignmentDecisionOutcome.NoEligibleCandidate, empty.Outcome);
        Assert.Empty(empty.Candidates);
        Assert.Null(empty.Proposal);
        Assert.Equal(FactionAssignmentDecisionOutcome.NoEligibleCandidate, invalid.Outcome);
        Assert.Null(invalid.Proposal);
        Assert.Equal(
            [
                FactionAssignmentCandidateReason.PlayerShipRejected,
                FactionAssignmentCandidateReason.NotAtLocation,
                FactionAssignmentCandidateReason.DirectRouteUnavailable,
            ],
            invalid.Candidates.Select(candidate => candidate.Reason)
        );
    }

    /// <summary>Confirms decision collections are copied, immutable to callers, and canonically ordered.</summary>
    [Fact]
    public void InputDefensivelyCopiesAndCanonicalizesCollections()
    {
        var assets = new List<FactionShipAssignmentSnapshot> { AtLocation(9, Alpha), AtLocation(3, Gamma) };
        var routes = new List<FactionKnownRouteSnapshot> { Route(Gamma, Beta, 800), Route(Beta, Alpha, 400) };
        FactionAssignmentDecisionInput input = Input(assets, routes);

        assets.Clear();
        routes.Clear();

        Assert.Equal(new long[] { 3, 9 }, input.Assets.Select(asset => asset.ShipId.Value));
        Assert.Equal(new[] { Alpha, Beta }, input.KnownRoutes.Select(route => route.A));
        Assert.False(input.Assets is ICollection<FactionShipAssignmentSnapshot>);
        Assert.False(input.KnownRoutes is ICollection<FactionKnownRouteSnapshot>);
    }

    /// <summary>Confirms equivalent reversed inputs and route endpoints produce identical decisions and explanations.</summary>
    [Fact]
    public void ReorderedEquivalentInputsProduceStructurallyEqualExplanation()
    {
        FactionShipAssignmentSnapshot[] assets = [AtLocation(8, Gamma), AtLocation(2, Alpha)];
        FactionKnownRouteSnapshot[] routes = [Route(Alpha, Beta, 600), Route(Gamma, Beta, 300)];
        FactionAssignmentDecisionExplanation forward = FactionAssignmentPolicy.Evaluate(Input(assets, routes));
        FactionAssignmentDecisionExplanation reversed = FactionAssignmentPolicy.Evaluate(
            Input(
                assets.Reverse(),
                routes.Reverse().Select(route => new FactionKnownRouteSnapshot(route.B, route.A, route.Duration))
            )
        );

        Assert.Equal(new ShipInstanceId(8), forward.Proposal!.ShipId);
        Assert.Equal(forward, reversed);
    }

    /// <summary>Confirms route duration outranks ship identity while equal durations use the lowest identity.</summary>
    [Fact]
    public void RouteDurationThenShipIdentityDeterminePriority()
    {
        FactionAssignmentDecisionExplanation unequal = FactionAssignmentPolicy.Evaluate(
            Input([AtLocation(1, Alpha), AtLocation(9, Gamma)], [Route(Alpha, Beta, 800), Route(Gamma, Beta, 300)])
        );
        FactionAssignmentDecisionExplanation equal = FactionAssignmentPolicy.Evaluate(
            Input([AtLocation(9, Gamma), AtLocation(1, Alpha)], [Route(Alpha, Beta, 300), Route(Gamma, Beta, 300)])
        );

        Assert.Equal(new ShipInstanceId(9), unequal.Proposal!.ShipId);
        Assert.Equal(new ShipInstanceId(1), equal.Proposal!.ShipId);
    }

    /// <summary>Confirms repeated evaluation returns the same complete diagnostics without consuming randomness.</summary>
    [Fact]
    public void IdenticalInputIsReproducible()
    {
        FactionAssignmentDecisionInput input = Input([AtLocation(1, Alpha)], [Route(Alpha, Beta, 400)]);

        FactionAssignmentDecisionExplanation first = FactionAssignmentPolicy.Evaluate(input);
        FactionAssignmentDecisionExplanation second = FactionAssignmentPolicy.Evaluate(input);

        Assert.Equal(first, second);
        Assert.False(first.RandomnessUsed);
    }

    /// <summary>Confirms existing nonplayer presence satisfies the objective even when that ship is committed.</summary>
    [Fact]
    public void ExistingCommittedPresenceSatisfiesObjectiveWithoutAnotherAssignment()
    {
        FactionAssignmentDecisionExplanation result = FactionAssignmentPolicy.Evaluate(
            Input([AtLocation(1, Beta, hasActiveOrder: true), AtLocation(2, Alpha)], [Route(Alpha, Beta, 400)])
        );

        Assert.Equal(FactionAssignmentDecisionOutcome.ObjectiveAlreadySatisfied, result.Outcome);
        Assert.Null(result.Proposal);
        Assert.Equal(FactionAssignmentCandidateReason.AlreadyCommitted, result.Candidates[0].Reason);
        Assert.Equal(FactionAssignmentCandidateReason.Eligible, result.Candidates[1].Reason);
    }

    /// <summary>Confirms snapshot value objects reject invalid identity, status, location, and route combinations.</summary>
    [Fact]
    public void SnapshotValueObjectsRejectInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => new EstablishPresenceObjectiveSnapshot(default));
        Assert.Throws<ArgumentException>(() =>
            new FactionShipAssignmentSnapshot(default, false, FactionShipStrategicStatus.Traveling, null, false)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FactionShipAssignmentSnapshot(new ShipInstanceId(1), false, (FactionShipStrategicStatus)99, null, false)
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionShipAssignmentSnapshot(
                new ShipInstanceId(1),
                false,
                FactionShipStrategicStatus.AtLocation,
                null,
                false
            )
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionShipAssignmentSnapshot(
                new ShipInstanceId(1),
                false,
                FactionShipStrategicStatus.Traveling,
                Alpha,
                false
            )
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionKnownRouteSnapshot(default, Beta, new SimulationDuration(100))
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionKnownRouteSnapshot(Alpha, default, new SimulationDuration(100))
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionKnownRouteSnapshot(Alpha, Alpha, new SimulationDuration(100))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FactionKnownRouteSnapshot(Alpha, Beta, new SimulationDuration(0))
        );
        Assert.Throws<ArgumentException>(() => new FactionKnownRouteSnapshot(Alpha, Beta, new SimulationDuration(101)));
    }

    /// <summary>Confirms decision input rejects invalid identity, null entries, duplicates, and excessive collections.</summary>
    [Fact]
    public void InputRejectsInvalidAndDuplicateValues()
    {
        FactionShipAssignmentSnapshot asset = AtLocation(1, Alpha);
        FactionKnownRouteSnapshot route = Route(Alpha, Beta, 100);

        Assert.Throws<ArgumentException>(() =>
            new FactionAssignmentDecisionInput(
                default,
                new SimulationTime(0),
                new EstablishPresenceObjectiveSnapshot(Beta),
                [],
                []
            )
        );
        Assert.Throws<ArgumentException>(() => Input([asset, asset], []));
        Assert.Throws<ArgumentException>(() => Input([null!], []));
        Assert.Throws<ArgumentException>(() => Input([], [route, Route(Beta, Alpha, 100)]));
        Assert.Throws<ArgumentException>(() => Input([], [null!]));
        Assert.Throws<ArgumentException>(() => Input(Enumerable.Repeat(asset, SimulationState.MaximumShips + 1), []));
        Assert.Throws<ArgumentException>(() => Input([], Enumerable.Repeat(route, StrategicMap.MaximumRoutes + 1)));
    }

    /// <summary>Confirms the public policy input graph cannot traverse world, ship, engineering, sensor, or callback state.</summary>
    [Fact]
    public void InputGraphExcludesHiddenWorldAndCallbackTypes()
    {
        HashSet<Type> exposedTypes = InputGraphTypes(typeof(FactionAssignmentDecisionInput));

        Assert.DoesNotContain(typeof(SimulationState), exposedTypes);
        Assert.DoesNotContain(typeof(ShipState), exposedTypes);
        Assert.DoesNotContain(
            exposedTypes,
            type => string.Equals(type.Namespace, "AlterCourse.Core.Sensors", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(exposedTypes, type => type.Name.Contains("Engineering", StringComparison.Ordinal));
        Assert.DoesNotContain(exposedTypes, typeof(Delegate).IsAssignableFrom);
    }

    private static FactionAssignmentDecisionInput Input(
        IEnumerable<FactionShipAssignmentSnapshot> assets,
        IEnumerable<FactionKnownRouteSnapshot> routes
    ) => new(Faction, new SimulationTime(1200), new EstablishPresenceObjectiveSnapshot(Beta), assets, routes);

    private static FactionShipAssignmentSnapshot AtLocation(
        long shipId,
        LocationId location,
        bool isPlayerShip = false,
        bool hasActiveOrder = false
    ) => new(new ShipInstanceId(shipId), isPlayerShip, FactionShipStrategicStatus.AtLocation, location, hasActiveOrder);

    private static FactionShipAssignmentSnapshot Traveling(long shipId) =>
        new(new ShipInstanceId(shipId), false, FactionShipStrategicStatus.Traveling, null, false);

    private static FactionKnownRouteSnapshot Route(LocationId a, LocationId b, long durationMilliseconds) =>
        new(a, b, new SimulationDuration(durationMilliseconds));

    private static HashSet<Type> InputGraphTypes(Type root)
    {
        var discovered = new HashSet<Type>();
        var pending = new Queue<Type>();
        pending.Enqueue(root);
        while (pending.TryDequeue(out Type? current))
        {
            if (!discovered.Add(current))
            {
                continue;
            }

            foreach (Type argument in current.GetGenericArguments())
            {
                pending.Enqueue(argument);
            }

            if (string.Equals(current.Namespace, "AlterCourse.Core.AI", StringComparison.Ordinal))
            {
                foreach (Type propertyType in current.GetProperties().Select(property => property.PropertyType))
                {
                    pending.Enqueue(propertyType);
                }
            }
        }

        return discovered;
    }
}
