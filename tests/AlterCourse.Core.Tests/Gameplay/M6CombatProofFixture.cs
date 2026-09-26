using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
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

    internal M6CombatProofFixture(bool lowDamage = false)
    {
        Catalog = _production.Catalog;
        if (lowDamage)
        {
            ShipDefinition source = Catalog.GetRequired(new ShipDefinitionId("pathfinder"));
            DirectedEnergyWeaponDefinition weapon = source.DirectedEnergyWeapon!;
            // Small authored output keeps the finite horizon active without recharge or fixture health resets.
            var alternate = new ShipDefinition(
                source.Id,
                source.DesignDisplayName,
                source.MaximumTacticalSpeed,
                source.PassiveSensorRange,
                source.ActiveScanDuration,
                source.Engineering,
                new DirectedEnergyWeaponDefinition(weapon.Range, 0.0001, weapon.Cooldown)
            );
            Catalog = new ShipDefinitionCatalog(
                new Dictionary<ShipDefinitionId, ShipDefinition> { [alternate.Id] = alternate }
            );
        }
    }

    internal ShipDefinitionCatalog Catalog { get; }
    internal FactionDefinitionCatalog ProductionFactions { get; } = new FactionAssignmentProofFixture().FactionCatalog;
    internal static readonly ShipInstanceId Defender = new(2);
    internal static readonly ShipInstanceId ScanTarget = new(3);

    internal GameSimulation FourShipFirstGame() => FirstGameSetup.Create(Catalog);

    internal GameSimulation Production() => FirstGameSetup.Create(Catalog, ProductionFactions);

    internal GameSimulation RoundTrip(GameSimulation game) =>
        GamePersistence
            .Deserialize(
                GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata),
                Catalog,
                game.FactionCatalog,
                "m6-combat-v9.json"
            )
            .Simulation;

    internal GameSimulation Restore(SimulationState state) =>
        GameSimulation.RestoreState(
            state,
            Catalog,
            state.Factions.IsEmpty ? FactionDefinitionCatalog.Empty : ProductionFactions
        );

    internal GameSimulation AdvanceTo(GameSimulation game, SimulationTime time) =>
        Restore(AdvanceTrace(game, time).State);

    internal SimulationAdvanceTraceResult AdvanceTrace(GameSimulation game, SimulationTime time) =>
        GameSimulation.AdvanceTo(game.CaptureState(), time, Catalog, game.FactionCatalog);

    internal SensorContactId EnterCombatRange(GameSimulation game)
    {
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            game.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );
        game.AdvanceUntilNextPlayerRelevantEvent();
        SensorContactId contact = Assert.Single(game.GetPlayerProjection().Ship.Sensors.Contacts).Id;
        IdentifyAndHail(game, contact);
        SimulationTime completion = Player(game).Engineering.ActiveRepair!.ExpectedCompletion;
        game.AdvanceFixedSteps(
            checked(
                (int)(
                    (completion.Milliseconds - game.CaptureState().Time.Milliseconds)
                    / SimulationFixedStep.Duration.Milliseconds
                )
            )
        );
        PowerAllocationResult power = game.SetPowerAllocation(Allocation(20, 5, 20, 30));
        Assert.Equal(PowerAllocationOutcome.Accepted, power.Outcome);
        Assert.Contains(power.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.SensorContactStale);
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new(new HeadingDegrees(90), new SpeedKilometersPerSecond(1))).Outcome
        );
        SimulationAdvanceResult approach = game.AdvanceFixedSteps(140);
        Assert.Contains(approach.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.SensorContactReacquired);
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new(new HeadingDegrees(90), default)).Outcome
        );
        return contact;
    }

    internal void AssertEquivalent(GameSimulation expected, GameSimulation actual)
    {
        SimulationState first = expected.CaptureState();
        SimulationState second = actual.CaptureState();
        first.Validate(Catalog, expected.FactionCatalog);
        second.Validate(Catalog, actual.FactionCatalog);
        Assert.Equal(expected.GetPlayerProjection(), actual.GetPlayerProjection());
        Assert.Equal(first.Time, second.Time);
        Assert.Equal(first.PlayerShipId, second.PlayerShipId);
        Assert.Equal(first.ShipIdAllocator.NextId, second.ShipIdAllocator.NextId);
        Assert.Equal(first.OrderIdAllocator.NextId, second.OrderIdAllocator.NextId);
        Assert.Equal(first.ObservationReportIdAllocator.NextId, second.ObservationReportIdAllocator.NextId);
        Assert.Equal(first.Scheduler.NextWorkId, second.Scheduler.NextWorkId);
        Assert.Equal(first.Scheduler.NextSequence, second.Scheduler.NextSequence);
        Assert.Equal(first.Scheduler.OutstandingWork.ToArray(), second.Scheduler.OutstandingWork.ToArray());
        Assert.Equal(first.Ships.Length, second.Ships.Length);
        foreach (ShipState ship in first.Ships)
        {
            ShipState other = second.GetRequiredShip(ship.InstanceId);
            Assert.Equal(ship.DefinitionId, other.DefinitionId);
            Assert.Equal(ship.VesselDisplayName, other.VesselDisplayName);
            Assert.Equal(ship.Engineering, other.Engineering);
            Assert.Equal(ship.Combat, other.Combat);
            Assert.Equal(ship.TacticalPosition, other.TacticalPosition);
            Assert.Equal(ship.TacticalMotion, other.TacticalMotion);
            Assert.Equal(ship.StrategicState, other.StrategicState);
            Assert.Equal(ship.ActiveOrder, other.ActiveOrder);
            Assert.Equal(ship.AutonomousState, other.AutonomousState);
            Assert.Equal(ship.DirectControllerFactionId, other.DirectControllerFactionId);
            Assert.Equal(ship.SensorKnowledge.NextContactId, other.SensorKnowledge.NextContactId);
            Assert.Equal(ship.SensorKnowledge.Contacts.ToArray(), other.SensorKnowledge.Contacts.ToArray());
            Assert.Equal(ship.SensorKnowledge.ActiveScan, other.SensorKnowledge.ActiveScan);
        }
        Assert.Equal(first.Factions.Length, second.Factions.Length);
        foreach (FactionState faction in first.Factions)
        {
            FactionState other = second.Factions.Single(item => item.Id == faction.Id);
            Assert.Equal(faction.DefinitionId, other.DefinitionId);
            Assert.Equal(faction.PresenceObjective, other.PresenceObjective);
            Assert.Equal(faction.PendingDecisionWake, other.PendingDecisionWake);
            Assert.Equal(faction.Observation!.Posture, other.Observation!.Posture);
            Assert.Equal(faction.Observation.ActiveInvestigation, other.Observation.ActiveInvestigation);
            Assert.Equal(faction.Observation.InFlightReports.ToArray(), other.Observation.InFlightReports.ToArray());
            Assert.Equal(faction.Observation.ReceivedReports.ToArray(), other.Observation.ReceivedReports.ToArray());
            Assert.Equal(
                faction.Observation.CompletionWatermarks.ToArray(),
                other.Observation.CompletionWatermarks.ToArray()
            );
        }
    }

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
