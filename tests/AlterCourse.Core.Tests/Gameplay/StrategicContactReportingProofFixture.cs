using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>
/// Drives the four-ship proof world for the headless Strategic Contact Reporting scenario, including the
/// authoritative NPC movement the player has no command for.
/// </summary>
/// <remarks>
/// Composes <see cref="Milestone3ProofFixture"/> rather than rebuilding the proof world, because a second
/// hand-built copy of the world would silently drift from <c>FirstGameSetup</c> and quietly change the locked
/// first-contact timeline (detection at 3500 ms, stale at 24100 ms, loss at 29100 ms) this scenario stands on.
/// </remarks>
internal sealed class StrategicContactReportingProofFixture
{
    private readonly Milestone3ProofFixture _proofWorld = new();

    internal ShipDefinitionCatalog Catalog => _proofWorld.Catalog;

    internal GameSimulation CreateDefault() => _proofWorld.CreateDefault();

    internal GameSimulation CreateWithKestrelOrder(ShipOrderStart? kestrelOrder) =>
        _proofWorld.CreateWithKestrelOrder(kestrelOrder);

    internal GameSimulation RoundTrip(GameSimulation simulation, string sourceName) =>
        _proofWorld.RoundTrip(simulation, sourceName);

    /// <summary>Puts Kestrel under an authoritative travel order toward <paramref name="destination"/>.</summary>
    /// <remarks>
    /// There is deliberately no public command that orders an NPC, so the scenario reaches hidden strategic
    /// movement through the same internal seam a patrol order uses on arrival: <c>ApplyShipTravel</c> schedules
    /// the real <c>TravelArrival</c> work, and the accompanying <see cref="TravelToOrder"/> makes the scheduler
    /// resolve the arrival through the order path rather than as an orderless drift. Nothing here writes the
    /// destination state directly — the move is scheduler work the simulation performs, which is the point: the
    /// player's retained report must survive world truth that genuinely changed underneath it.
    ///
    /// Issuing the order costs one fixed step: a departing ship's own current contacts stop being observable
    /// the moment it leaves, and only the simulation's observation pass may stale them, so the state between
    /// the departure and that pass is deliberately not a state this fixture hands back.
    /// </remarks>
    internal GameSimulation OrderKestrelTo(GameSimulation simulation, LocationId destination)
    {
        ShipInstanceId kestrelId = Milestone3ProofFixture.Kestrel(simulation).InstanceId;
        ShipTravelApplicationResult application = GameSimulation.ApplyShipTravel(
            simulation.CaptureState(),
            new ShipTravelCommand(kestrelId, destination)
        );
        Assert.Equal(TravelOutcome.Accepted, application.Outcome);

        (ShipOrderIdAllocator allocator, ShipOrderId orderId) = application.CandidateState.OrderIdAllocator.Allocate();
        SimulationState ordered = application.CandidateState.ReplaceShip(
            kestrelId,
            application.CandidateState.GetRequiredShip(kestrelId) with
            {
                ActiveOrder = new TravelToOrder(orderId, destination),
            }
        ) with
        {
            OrderIdAllocator = allocator,
        };
        SimulationAdvanceTraceResult observed = GameSimulation.AdvanceTo(
            ordered,
            ordered.Time.AdvanceBy(SimulationFixedStep.Duration),
            Catalog
        );
        return GameSimulation.RestoreState(observed.State, Catalog);
    }
}
