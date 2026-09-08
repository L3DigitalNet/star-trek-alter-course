using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Defines the complete bounded actor-specific input to one faction assignment decision.</summary>
public sealed record FactionAssignmentDecisionInput
{
    private readonly ReadOnlyDecisionList<FactionShipAssignmentSnapshot> _assets;
    private readonly ReadOnlyDecisionList<FactionKnownRouteSnapshot> _knownRoutes;

    /// <summary>Initializes an immutable faction assignment decision request.</summary>
    public FactionAssignmentDecisionInput(
        FactionId factionId,
        SimulationTime decisionTime,
        EstablishPresenceObjectiveSnapshot objective,
        IEnumerable<FactionShipAssignmentSnapshot> assets,
        IEnumerable<FactionKnownRouteSnapshot> knownRoutes
    )
    {
        if (factionId.Value <= 0)
        {
            throw new ArgumentException(
                "A faction decision requires an initialized faction identity.",
                nameof(factionId)
            );
        }

        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(knownRoutes);

        FactionShipAssignmentSnapshot[] materializedAssets = MaterializeAssets(assets);
        FactionKnownRouteSnapshot[] materializedRoutes = MaterializeRoutes(knownRoutes);

        FactionId = factionId;
        DecisionTime = decisionTime;
        Objective = objective;
        _assets = new ReadOnlyDecisionList<FactionShipAssignmentSnapshot>(
            materializedAssets.OrderBy(asset => asset.ShipId.Value)
        );
        _knownRoutes = new ReadOnlyDecisionList<FactionKnownRouteSnapshot>(
            materializedRoutes
                .OrderBy(route => route.A.Value, StringComparer.Ordinal)
                .ThenBy(route => route.B.Value, StringComparer.Ordinal)
        );
    }

    /// <summary>Gets the faction that owns the decision.</summary>
    public FactionId FactionId { get; }

    /// <summary>Gets the authoritative simulation time supplied to the policy.</summary>
    public SimulationTime DecisionTime { get; }

    /// <summary>Gets the active establish-presence objective.</summary>
    public EstablishPresenceObjectiveSnapshot Objective { get; }

    /// <summary>Gets directly controlled assets in stable ship-identity order.</summary>
    public IReadOnlyList<FactionShipAssignmentSnapshot> Assets => _assets;

    /// <summary>Gets actor-known direct routes in stable endpoint order.</summary>
    public IReadOnlyList<FactionKnownRouteSnapshot> KnownRoutes => _knownRoutes;

    private static FactionShipAssignmentSnapshot[] MaterializeAssets(IEnumerable<FactionShipAssignmentSnapshot> assets)
    {
        FactionShipAssignmentSnapshot[] materialized = assets.Take(SimulationState.MaximumShips + 1).ToArray();
        if (materialized.Length > SimulationState.MaximumShips)
        {
            throw new ArgumentException(
                $"A faction decision supports at most {SimulationState.MaximumShips} controlled ships.",
                nameof(assets)
            );
        }

        if (materialized.Any(asset => asset is null))
        {
            throw new ArgumentException("Faction decision assets cannot contain null.", nameof(assets));
        }

        if (materialized.Select(asset => asset.ShipId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Faction decision assets require unique ship identities.", nameof(assets));
        }

        return materialized;
    }

    private static FactionKnownRouteSnapshot[] MaterializeRoutes(IEnumerable<FactionKnownRouteSnapshot> knownRoutes)
    {
        FactionKnownRouteSnapshot[] materialized = knownRoutes.Take(StrategicMap.MaximumRoutes + 1).ToArray();
        if (materialized.Length > StrategicMap.MaximumRoutes)
        {
            throw new ArgumentException(
                $"A faction decision supports at most {StrategicMap.MaximumRoutes} known routes.",
                nameof(knownRoutes)
            );
        }

        if (materialized.Any(route => route is null))
        {
            throw new ArgumentException("Faction decision routes cannot contain null.", nameof(knownRoutes));
        }

        if (materialized.DistinctBy(route => (route.A, route.B)).Count() != materialized.Length)
        {
            throw new ArgumentException("A known direct connection may be supplied only once.", nameof(knownRoutes));
        }

        return materialized;
    }
}
