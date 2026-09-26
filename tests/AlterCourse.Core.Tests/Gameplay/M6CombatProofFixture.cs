using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Composes V5 content and real contact transitions for combat acceptance proofs.</summary>
internal sealed class M6CombatProofFixture
{
    private readonly Milestone3ProofFixture _production = new();
    internal ShipDefinitionCatalog Catalog => _production.Catalog;
    internal static readonly ShipInstanceId Defender = new(2);
    internal static readonly ShipInstanceId ScanTarget = new(3);

    internal GameSimulation Production() => _production.CreateDefault();

    internal GameSimulation Restore(SimulationState state) => GameSimulation.RestoreState(state, Catalog);

    internal GameSimulation AdvanceTo(GameSimulation game, SimulationTime time) =>
        Restore(GameSimulation.AdvanceTo(game.CaptureState(), time, Catalog).State);

    internal GameSimulation Pair()
    {
        var location = new LocationId("combat-proof");
        ShipStart Start(long id, double x, PowerAllocation allocation) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Proof ship " + id,
                new TacticalPosition(x, 0),
                default,
                new SystemCondition(1),
                new SystemCondition(1),
                new SystemCondition(1),
                allocation,
                new AtLocationStart(location)
            )
            {
                ShieldCondition = new SystemCondition(1),
                DirectedEnergyCondition = new SystemCondition(1),
            };
        var map = new StrategicMap([new StrategicLocation(location, "Combat proof", default)], []);
        GameSimulation game = new GameBootstrap(
            default,
            map,
            new ShipInstanceId(1),
            [
                Start(1, 0, Allocation(70, 20, 0, 30)),
                Start(2, 10, Allocation(70, 5, 15, 30)),
                Start(3, 28, Allocation(70, 0, 0, 0)),
            ]
        ).CreateSimulation(Catalog);
        SimulationState initial = game.CaptureState();
        ShipState defender = initial.GetRequiredShip(Defender);
        // Only posture is authored here; the subsequent observation pass creates all contact knowledge.
        game = Restore(
            initial.ReplaceShip(
                Defender,
                defender with
                {
                    AutonomousState = defender.AutonomousState with
                    {
                        ContactPosture = ShipContactPosture.CautiousContact,
                    },
                }
            )
        );
        game.AdvanceFixedSteps(1);
        IdentifyAndHail(game, Contact(game, Defender));
        return game;
    }

    internal void IdentifyAndHail(GameSimulation game, SensorContactId contact)
    {
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contact).Outcome);
        long duration = Catalog.GetRequired(Player(game).DefinitionId).ActiveScanDuration.Milliseconds;
        game.AdvanceFixedSteps(checked((int)(duration / SimulationFixedStep.Duration.Milliseconds)));
        Assert.Equal(HailOutcome.Acknowledged, game.RequestHail(contact).Outcome);
    }

    internal static PowerAllocation Allocation(int sensors, int impulse, int shields, int weapons) =>
        new(new PowerUnits(sensors), new PowerUnits(impulse), new PowerUnits(shields), new PowerUnits(weapons));

    internal static ShipState Player(GameSimulation game) =>
        game.CaptureState().GetRequiredShip(game.CaptureState().PlayerShipId);

    internal static SensorContactId Contact(GameSimulation game, ShipInstanceId target) =>
        Player(game).SensorKnowledge.Contacts.Single(contact => contact.TargetShipId == target).Id;

    internal static double Separation(GameSimulation game, ShipInstanceId target)
    {
        TacticalPosition own = Player(game).TacticalPosition;
        TacticalPosition other = game.CaptureState().GetRequiredShip(target).TacticalPosition;
        return Math.Sqrt(
            Math.Pow(own.XKilometers - other.XKilometers, 2) + Math.Pow(own.YKilometers - other.YKilometers, 2)
        );
    }

    /// <summary>Submits a trusted NPC intent to the shared Core transition, preserving local fire legality.</summary>
    internal ShipDirectedEnergyApplicationResult Incoming(GameSimulation game, ShipSystemId system)
    {
        SimulationState state = game.CaptureState();
        SensorContactId contact = state
            .GetRequiredShip(Defender)
            .SensorKnowledge.Contacts.Single(item => item.TargetShipId == state.PlayerShipId)
            .Id;
        ShipDirectedEnergyApplicationResult result = GameSimulation.ApplyShipDirectedEnergy(
            state,
            Catalog,
            Defender,
            new(contact, system)
        );
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, result.Outcome);
        result.CandidateState.Validate(Catalog);
        return result;
    }
}
