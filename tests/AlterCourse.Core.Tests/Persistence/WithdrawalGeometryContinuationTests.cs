using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies observed withdrawal decisions, applied courses, and same-build save continuation.</summary>
public sealed class WithdrawalGeometryContinuationTests
{
    private static readonly ShipInstanceId NpcId = new(2);

    /// <summary>Retains a real shot's pending defensive response and its corrected course across a save boundary.</summary>
    [Theory]
    [InlineData(double.Epsilon, 0, 90)]
    [InlineData(-double.Epsilon, 0, 270)]
    [InlineData(0, double.Epsilon, 0)]
    [InlineData(0, -double.Epsilon, 180)]
    [InlineData(double.Epsilon, double.Epsilon, 45)]
    [InlineData(3, 4, 36.86989764584402)]
    public void DefensiveShotResponseAppliesAndContinuesAfterLoad(double ownX, double ownY, double expectedHeading)
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation game = ObservedPair(fixture, new TacticalPosition(ownX, ownY));
        SensorContactId target = game.GetPlayerProjection().Ship.Sensors.Contacts.Single().Id;
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(target).Outcome);
        game.AdvanceFixedSteps(20);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(target, ShipSystemKind.DirectedEnergyWeapons)).Outcome
        );

        ShipState npc = game.CaptureState().GetRequiredShip(NpcId);
        CombatStimulus stimulus = Assert.IsType<CombatStimulus>(npc.Combat.PendingStimulus);
        SensorContactTrack observed = Assert.Single(npc.SensorKnowledge.Contacts);
        Assert.Equal(observed.Id, stimulus.ContactId);
        Assert.Equal(SensorContactStatus.Current, observed.Status);
        Assert.Equal(default, observed.LastObservedPosition);
        Assert.Equal(new TacticalPosition(ownX, ownY), npc.TacticalPosition);
        GameSimulation resumed = fixture.RoundTrip(game, "defensive-geometry.json");
        Assert.Equal(Bytes(game), Bytes(resumed));

        game.AdvanceFixedSteps(1);
        resumed.AdvanceFixedSteps(1);

        DefensiveCombatDecisionExplanation decision = Assert.IsType<DefensiveCombatDecisionExplanation>(
            game.LastDefensiveCombatDecisionExplanation
        );
        DefensiveCombatDecisionExplanation restoredDecision = Assert.IsType<DefensiveCombatDecisionExplanation>(
            resumed.LastDefensiveCombatDecisionExplanation
        );
        Assert.Equal(decision.Input, restoredDecision.Input);
        Assert.Equal(decision, restoredDecision);
        Assert.Equal(new TacticalPosition(ownX, ownY), decision.Input.Own.Position);
        Assert.Equal(default, decision.Input.Contact!.LastObservedPosition);
        Assert.Equal(FireDirectedEnergyOutcome.ContactNotIdentified, decision.Candidates[0].FireRejection);
        Assert.Equal(DefensiveCombatDecisionAction.Withdraw, decision.SelectedAction);
        Assert.All(decision.Candidates[1].Constraints, constraint => Assert.True(constraint.Satisfied));
        Assert.Equal(SetTacticalCourseOutcome.Accepted, decision.CourseOutcome);
        Assert.Equal(expectedHeading, decision.ResultingCourse!.Value.Heading.Value, 10);
        AssertAppliedCourseAndContinuation(game, resumed, fixture, expectedHeading);
    }

    /// <summary>Retains a cautious wake over genuine observed facts and applies the same course after load.</summary>
    [Theory]
    [InlineData(double.Epsilon, 0, 90)]
    [InlineData(-double.Epsilon, 0, 270)]
    [InlineData(0, double.Epsilon, 0)]
    [InlineData(0, -double.Epsilon, 180)]
    [InlineData(double.Epsilon, double.Epsilon, 45)]
    [InlineData(3, 4, 36.86989764584402)]
    public void CautiousObservedWakeAppliesAndContinuesAfterLoad(double ownX, double ownY, double expectedHeading)
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation game = ObservedPair(fixture, new TacticalPosition(ownX, ownY));
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(NpcId);
        Assert.Equal(SensorContactStatus.Current, Assert.Single(npc.SensorKnowledge.Contacts).Status);
        // A correlated same-time wake is the state produced when cautious posture observes a new contact.
        // The prior passive observation keeps the contact legitimate without inventing actor knowledge.
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            state.Time,
            npc.InstanceId,
            ScheduledWorkKind.ShipContactDecisionWake
        );
        state = state.ReplaceShip(
            npc.InstanceId,
            npc with
            {
                AutonomousState = new ShipAutonomousState(
                    ShipContactPosture.CautiousContact,
                    new ShipContactDecisionWake(work.Id, work.DueTime)
                ),
            }
        ) with { Scheduler = scheduler };
        game = GameSimulation.RestoreState(state, fixture.Catalog);
        GameSimulation resumed = fixture.RoundTrip(game, "cautious-geometry.json");
        Assert.Equal(Bytes(game), Bytes(resumed));

        game.AdvanceFixedSteps(1);
        resumed.AdvanceFixedSteps(1);

        ShipContactDecisionExplanation decision = Assert.IsType<ShipContactDecisionExplanation>(
            game.LastContactDecisionExplanation
        );
        ShipContactDecisionExplanation restoredDecision = Assert.IsType<ShipContactDecisionExplanation>(
            resumed.LastContactDecisionExplanation
        );
        Assert.Equal(decision.ActorKnownFacts, restoredDecision.ActorKnownFacts);
        Assert.Equal(decision, restoredDecision);
        Assert.Equal(new TacticalPosition(ownX, ownY), decision.ActorKnownFacts.OwnPosition);
        Assert.Equal(default, Assert.Single(decision.ActorKnownFacts.Contacts).LastObservedPosition);
        Assert.Equal(ShipContactDecisionAction.Withdraw, decision.SelectedAction);
        Assert.True(decision.Candidates[2].HardConstraintsSatisfied);
        Assert.Equal(expectedHeading, decision.ResultingCourse!.Value.Heading.Value, 10);
        AssertAppliedCourseAndContinuation(game, resumed, fixture, expectedHeading);
    }

    private static void AssertAppliedCourseAndContinuation(
        GameSimulation game,
        GameSimulation resumed,
        Milestone3ProofFixture fixture,
        double expectedHeading
    )
    {
        ShipState applied = game.CaptureState().GetRequiredShip(NpcId);
        Assert.Equal(expectedHeading, applied.TacticalMotion.Heading.Value, 10);
        Assert.True(applied.TacticalMotion.Speed.Value > 0);
        Assert.Equal(applied.TacticalMotion, resumed.CaptureState().GetRequiredShip(NpcId).TacticalMotion);
        Assert.Equal(Bytes(game), Bytes(resumed));
        GameSimulation savedAfterDecision = fixture.RoundTrip(game, "applied-geometry.json");
        foreach (int steps in new[] { 1, 3, 17 })
        {
            game.AdvanceFixedSteps(steps);
            resumed.AdvanceFixedSteps(steps);
            savedAfterDecision.AdvanceFixedSteps(steps);
            Assert.Equal(Bytes(game), Bytes(resumed));
            Assert.Equal(Bytes(game), Bytes(savedAfterDecision));
        }
        Assert.NotEqual(applied.TacticalPosition, game.CaptureState().GetRequiredShip(NpcId).TacticalPosition);
    }

    private static byte[] Bytes(GameSimulation game) => GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);

    private static GameSimulation ObservedPair(Milestone3ProofFixture fixture, TacticalPosition npcPosition)
    {
        var location = new LocationId("geometry-pair");
        var map = new StrategicMap([new StrategicLocation(location, "Geometry Pair", default)], []);
        ShipStart Start(long id, TacticalPosition position, int impulsePower, int weaponPower) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Ship " + id,
                position,
                default,
                new AtLocationStart(location),
                TestShipStarts.Pathfinder(impulsePower: impulsePower, weaponPower: weaponPower, weapons: 1)
            );
        GameSimulation game = new GameBootstrap(
            default,
            map,
            new ShipInstanceId(1),
            [Start(1, default, 20, 30), Start(2, npcPosition, 50, 0)]
        ).CreateSimulation(fixture.Catalog);
        game.AdvanceFixedSteps(1);
        return game;
    }
}
