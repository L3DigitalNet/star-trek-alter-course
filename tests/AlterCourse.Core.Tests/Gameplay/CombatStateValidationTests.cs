using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Rejects malformed readiness and exact defensive-wake correlations without weakening ordinary state validation.</summary>
public sealed class CombatStateValidationTests
{
    /// <summary>Rejects every malformed timing, owner, contact, or scheduler correlation.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void InvalidCombatStateCannotRestore(int invalidCase)
    {
        var fixture = new Milestone3ProofFixture();
        SimulationState state = fixture.CreateDefault().CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(4));
        SensorContactId contact = npc.SensorKnowledge.Contacts.Single().Id;
        SimulationTime due = new(100);
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            due,
            npc.InstanceId,
            ScheduledWorkKind.ShipCombatDecisionWake
        );
        var stimulus = new CombatStimulus(contact, default, due, work.Id);
        ShipCombatState combat = new([new DirectedEnergyReadiness(TestShipContent.Weapons, default)], stimulus);
        if (invalidCase == 10)
        {
            ShipState player = state.GetRequiredShip(state.PlayerShipId);
            state = state.ReplaceShip(player.InstanceId, player with { Combat = combat });
        }
        combat = InvalidCombat(invalidCase, combat, stimulus);
        state = state.ReplaceShip(npc.InstanceId, npc with { Combat = combat }) with { Scheduler = scheduler };
        Assert.Throws<InvalidOperationException>(() => GameSimulation.RestoreState(state, fixture.Catalog));
    }

    private static ShipCombatState InvalidCombat(int invalidCase, ShipCombatState combat, CombatStimulus stimulus) =>
        invalidCase switch
        {
            1 => Ready(combat, 1),
            2 => Ready(combat, 2100),
            3 => Ready(combat, long.MaxValue / 100 * 100),
            4 => null!,
            5 => combat with { PendingStimulus = stimulus with { ContactId = new SensorContactId(99) } },
            6 => combat with { PendingStimulus = stimulus with { ObservedAt = new SimulationTime(100) } },
            7 => combat with { PendingStimulus = stimulus with { DueTime = new SimulationTime(200) } },
            8 => combat with { PendingStimulus = stimulus with { ScheduledWorkId = new ScheduledWorkId(999) } },
            _ => combat with { PendingStimulus = null },
        };

    private static ShipCombatState Ready(ShipCombatState combat, long readyAt) =>
        combat.WithReadiness(new DirectedEnergyReadiness(TestShipContent.Weapons, new SimulationTime(readyAt)));

    /// <summary>
    /// Readiness is keyed by installed weapon: a missing weapon entry, an entry for a non-weapon installation, or an
    /// entry for an identity the ship does not have all fail restore.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(2L)]
    [InlineData(900L)]
    public void ReadinessKeySetMustEqualInstalledWeapons(long extraReadinessId)
    {
        var fixture = new Milestone3ProofFixture();
        SimulationState state = fixture.CreateDefault().CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(4));
        ShipCombatState combat =
            extraReadinessId == 0
                ? ShipCombatState.Empty
                : npc.Combat.WithReadiness(
                    new DirectedEnergyReadiness(new InstalledSystemId(extraReadinessId), default)
                );
        state = state.ReplaceShip(npc.InstanceId, npc with { Combat = combat });
        Assert.Throws<InvalidOperationException>(() => GameSimulation.RestoreState(state, fixture.Catalog));
    }

    /// <summary>Weapon readiness remains separate from the player's invariant-empty autonomous state.</summary>
    [Fact]
    public void PlayerReadinessDoesNotRequireAutonomousState()
    {
        var fixture = new Milestone3ProofFixture();
        SimulationState state = fixture.CreateDefault().CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        player = player with
        {
            Combat = new ShipCombatState([
                new DirectedEnergyReadiness(TestShipContent.Weapons, new SimulationTime(2000)),
            ]),
        };
        var game = GameSimulation.RestoreState(state.ReplaceShip(player.InstanceId, player), fixture.Catalog);
        Assert.Equal(ShipAutonomousState.Empty, game.CaptureState().GetRequiredShip(player.InstanceId).AutonomousState);
        Assert.Equal(2000, game.GetPlayerProjection().Ship.Combat.RemainingCooldown.Milliseconds);
    }
}
