using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>
/// Proves an attacker cannot learn a victim's hidden loadout: worlds that differ only in whether the victim has the
/// aimed kind look identical to the attacker, while trusted state and the victim's own events show the difference.
/// </summary>
public sealed class HiddenLoadoutTargetingTests
{
    private static readonly ShipDefinitionCatalog Catalog = TestShipContent.Pathfinder();

    /// <summary>Victim loadouts per pair: (present, absent), with equal shield interaction.</summary>
    public static TheoryData<string> Pairs => new() { "directed-energy-weapons", "impulse-propulsion", "shields" };

    /// <summary>
    /// For each pair the attacker's pre-shot projection, outcome, immediate events, and readiness are equal, the
    /// shot actually penetrates, and trusted state shows the receiver damaged only when present.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void AttackerObservationsAreEqualWhetherOrNotVictimHasAimedKind(string aim)
    {
        var aimKind = ShipSystemKind.Parse(aim);
        (Observation present, SimulationState presentBefore, SimulationState presentAfter) = FireAtVictim(
            aimKind,
            VictimLoadout(aimKind, present: true)
        );
        (Observation absent, SimulationState absentBefore, SimulationState absentAfter) = FireAtVictim(
            aimKind,
            VictimLoadout(aimKind, present: false)
        );

        Assert.Equal(present, absent);
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, present.Outcome);
        Assert.Contains(nameof(PlayerAdvanceEventKind.SubsystemPenetration), present.Events, StringComparison.Ordinal);

        ShipState presentVictim = presentAfter.GetRequiredShip(TestPairs.Npc);
        ShipState absentVictim = absentAfter.GetRequiredShip(TestPairs.Npc);
        Assert.Empty(absentVictim.Engineering.Systems.OfKind(aimKind));
        if (aimKind != ShipSystemKind.Shields)
        {
            // Nominal shields at 5/40 power absorb 0.125 of the 0.25 shot, so 0.125 penetrates to the receiver.
            Assert.Equal(
                TestEngineering.ConditionOf(presentBefore.GetRequiredShip(TestPairs.Npc).Engineering, aimKind) - 0.125,
                TestEngineering.ConditionOf(presentVictim.Engineering, aimKind),
                12
            );
        }

        // No redirect and no hull pool: every installation the absent victim has matches its shield-only outcome.
        foreach (InstalledSystem system in absentVictim.Engineering.Systems)
        {
            double before = absentBefore
                .GetRequiredShip(TestPairs.Npc)
                .Engineering.Systems.GetRequired(system.Id)
                .Condition.Value;
            double expected = system.Kind == ShipSystemKind.Shields ? Math.Max(0, before - 0.125) : before;
            Assert.Equal(expected, system.Condition.Value, 12);
        }
    }

    /// <summary>
    /// Mirrored victim-player worlds: the owner's events must differ, because only the receiver-present member has
    /// a receiver leg naming its own kind and installed id.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void VictimOwnerEventsRevealOnlyTheirOwnReceiver(string aim)
    {
        var aimKind = ShipSystemKind.Parse(aim);
        IReadOnlyList<PlayerAdvanceEvent> present = IncomingAt(aimKind, VictimLoadout(aimKind, present: true));
        IReadOnlyList<PlayerAdvanceEvent> absent = IncomingAt(aimKind, VictimLoadout(aimKind, present: false));

        bool ReceiverLeg(PlayerAdvanceEvent item) =>
            item.Kind == PlayerAdvanceEventKind.OwnSystemDamaged && item.SystemKind == aimKind;

        PlayerAdvanceEvent leg = Assert.Single(present, ReceiverLeg);
        Assert.NotNull(leg.InstalledSystemId);
        Assert.DoesNotContain(absent, ReceiverLeg);
        Assert.NotEqual(present, absent);
    }

    /// <summary>
    /// Regression (disposition 2): a non-shield aim that both hits shields and penetrates reports the aim kind on
    /// the fire and penetration events, shields on the impact event, and no installed identity on any of them.
    /// </summary>
    [Fact]
    public void AttackerPayloadsNameAimKindExceptShieldImpact()
    {
        (GameSimulation game, SensorContactId contact) = TestPairs.Create(
            Catalog,
            TestPairs.Loadout(),
            TestPairs.Loadout()
        );
        IReadOnlyList<PlayerAdvanceEvent> events = game.FireDirectedEnergy(
            new(contact, ShipSystemKind.ImpulsePropulsion)
        ).ResolvedEvents;

        PlayerAdvanceEvent fired = Assert.Single(
            events,
            item => item.Kind == PlayerAdvanceEventKind.DirectedEnergyFired
        );
        PlayerAdvanceEvent impact = Assert.Single(events, item => item.Kind == PlayerAdvanceEventKind.ShieldImpact);
        PlayerAdvanceEvent penetration = Assert.Single(
            events,
            item => item.Kind == PlayerAdvanceEventKind.SubsystemPenetration
        );
        Assert.Equal(ShipSystemKind.ImpulsePropulsion, fired.SystemKind);
        Assert.Equal(ShipSystemKind.Shields, impact.SystemKind);
        Assert.Equal(ShipSystemKind.ImpulsePropulsion, penetration.SystemKind);
        Assert.All([fired, impact, penetration], item => Assert.Null(item.InstalledSystemId));
    }

    /// <summary>A shieldless attacker may aim at shields; the aim vocabulary is not the attacker's inventory.</summary>
    [Fact]
    public void AimVocabularyIsIndependentOfAttackerInventory()
    {
        (GameSimulation shieldless, SensorContactId contact) = TestPairs.Create(
            Catalog,
            TestPairs.Loadout(shields: false),
            TestPairs.Loadout()
        );
        (GameSimulation shielded, _) = TestPairs.Create(Catalog, TestPairs.Loadout(), TestPairs.Loadout());

        Assert.Equal(
            shielded.GetPlayerProjection().Ship.Combat.Targets.Single().AimKinds,
            shieldless.GetPlayerProjection().Ship.Combat.Targets.Single().AimKinds
        );
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            shieldless.FireDirectedEnergy(new(contact, ShipSystemKind.Shields)).Outcome
        );
    }

    /// <summary>
    /// Unsupported-kind control: in a valid world whose catalog has no shield definition at all, aiming at shields is
    /// refused as unsupported with no change, and shields never appear among the aim kinds.
    /// </summary>
    [Fact]
    public void CatalogWithoutShieldDefinitionRefusesShieldAim()
    {
        SystemDefinition[] definitions =
        [
            .. TestShipContent.PathfinderSystems().Where(definition => definition.Kind != ShipSystemKind.Shields),
        ];
        ShipDefinitionCatalog catalog = TestShipContent.Catalog(
            TestShipContent.Systems(definitions),
            TestShipContent.Design("pathfinder", "Pathfinder class", definitions)
        );
        (GameSimulation game, SensorContactId contact) = TestPairs.Create(
            catalog,
            TestPairs.Loadout(shields: false),
            TestPairs.Loadout(shields: false)
        );
        SimulationState before = game.CaptureState();

        FireDirectedEnergyResult result = game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields));

        Assert.Equal(FireDirectedEnergyOutcome.UnsupportedSystem, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Same(before, game.CaptureState());
        Assert.DoesNotContain(ShipSystemKind.Shields, catalog.SystemDefinitions.DamageTargetKinds);
        Assert.All(
            game.GetPlayerProjection().Ship.Combat.Targets,
            target => Assert.DoesNotContain(ShipSystemKind.Shields, target.AimKinds)
        );
    }

    private static ShipSystemsStart VictimLoadout(ShipSystemKind aim, bool present) =>
        aim.Value switch
        {
            "directed-energy-weapons" => TestPairs.Loadout(weapons: present),
            // Unpowered impulse keeps the present victim stationary, matching the absent victim's observed motion.
            "impulse-propulsion" => TestPairs.Loadout(impulse: present, impulsePower: 0),
            "shields" => present
                ? TestPairs.Loadout(shieldCondition: 0, shieldPower: 5)
                : TestPairs.Loadout(shields: false),
            _ => throw new ArgumentOutOfRangeException(nameof(aim), aim.Value, "No pair is defined for this kind."),
        };

    private static (Observation Observation, SimulationState Before, SimulationState After) FireAtVictim(
        ShipSystemKind aim,
        ShipSystemsStart victim
    )
    {
        (GameSimulation game, SensorContactId contact) = TestPairs.Create(Catalog, TestPairs.Loadout(), victim);
        SimulationState before = game.CaptureState();
        CombatTargetProjection target = game.GetPlayerProjection().Ship.Combat.Targets.Single();
        FireDirectedEnergyResult result = game.FireDirectedEnergy(new(contact, aim));
        CombatProjection after = game.GetPlayerProjection().Ship.Combat;
        return (
            new Observation(
                target.ContactId,
                target.Range,
                target.Outcome,
                string.Join(',', target.AimKinds.Select(kind => kind.Value)),
                result.Outcome,
                Render(result.ResolvedEvents),
                after.RemainingCooldown,
                after.NextDirectedEnergyReadyAt
            ),
            before,
            game.CaptureState()
        );
    }

    /// <summary>Renders every attacker-visible event field so worlds compare by value.</summary>
    private static string Render(IEnumerable<PlayerAdvanceEvent> events) =>
        string.Join(
            ';',
            events.Select(item =>
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"{item.Kind}@{item.OccurredAt.Milliseconds}:{item.SensorContactId?.Value}:{item.SystemKind?.Value}:{item.InstalledSystemId?.Value}"
                )
            )
        );

    /// <summary>Makes the player the victim; the NPC fires through the trusted per-ship transition.</summary>
    private static IReadOnlyList<PlayerAdvanceEvent> IncomingAt(ShipSystemKind aim, ShipSystemsStart victim)
    {
        (GameSimulation game, _) = TestPairs.Create(Catalog, victim, TestPairs.Loadout());
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(TestPairs.Npc);
        ShipDirectedEnergyApplicationResult shot = GameSimulation.ApplyShipDirectedEnergy(
            state,
            Catalog,
            npc.InstanceId,
            new(npc.SensorKnowledge.Contacts.Single().Id, aim)
        );
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, shot.Outcome);
        return shot.ResolvedEvents;
    }

    /// <summary>Everything the attacking player can observe about one shot, compared by value.</summary>
    private sealed record Observation(
        SensorContactId Contact,
        DistanceKilometers? Range,
        FireDirectedEnergyOutcome ProjectedOutcome,
        string AimKinds,
        FireDirectedEnergyOutcome Outcome,
        string Events,
        SimulationDuration RemainingCooldown,
        SimulationTime? ReadyAt
    );
}
