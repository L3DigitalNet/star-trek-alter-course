using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Ships;
using static AlterCourse.Core.Tests.Characterization.M6ABaselineRecords;
using Expected = AlterCourse.Core.Tests.Characterization.M6ABaselineExpectations;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// Pins M6A ship-system outcomes as representation-neutral records so issue #121 can prove equivalence.
/// </summary>
/// <remarks>
/// Each test compares a whole scenario record with <see cref="M6ABaselineExpectations"/>; the named assertions
/// before it single out the authored proof values so a failure names the broken contract directly. These tests
/// deliberately assert nothing about save-file shape — historical wire formats stay in the Persistence tests.
/// </remarks>
public sealed class M6ABaselineCharacterizationTests
{
    private readonly M6ABaselineScenarios _scenarios = new();

    /// <summary>Pins every authored Pathfinder proof value the M6A behaviors derive from.</summary>
    [Fact]
    public void ProductionContentCarriesAuthoredProofValues()
    {
        DefinitionFacts facts = _scenarios.ProductionContent();
        Assert.Equal(120, facts.NominalGeneration);
        Assert.Equal(new AllocationTuple(70, 50, 40, 30), facts.NominalDemands);
        Assert.Equal(30, facts.PassiveRangeKm);
        Assert.Equal(10, facts.MaximumTacticalSpeed);
        Assert.Equal(2000, facts.ActiveScanMs);
        Assert.Equal(2000, facts.WeaponCooldownMs);
        Assert.Equal(20, facts.WeaponRangeKm);
        Assert.Equal(0.25, facts.WeaponDamage);
        Assert.Equal(new RepairDurations(8000, 6000, 8000, 6000), facts.Repairs);
        Assert.Equal(Expected.PathfinderContent, facts);
    }

    /// <summary>Pins the constrained player, the Kestrel combat allocation, and nominal combat systems.</summary>
    [Fact]
    public void NewGameStartsConstrainedPlayerAndNominalCombatSystems()
    {
        NewGameOutcome outcome = _scenarios.NewGame();
        EngineeringOutcome player = outcome.FourShip.Items[0];
        Assert.Equal(0.625, player.Conditions.Generation);
        Assert.Equal(75, player.AvailablePower);
        Assert.Equal(new AllocationTuple(44, 31, 0, 0), player.Allocation);
        Assert.Equal(new AllocationTuple(70, 5, 15, 30), outcome.FourShip.Items[3].Allocation);
        Assert.All(
            outcome.SixShip.Items,
            ship =>
            {
                Assert.Equal(1, ship.Conditions.Shields);
                Assert.Equal(1, ship.Conditions.DirectedEnergy);
            }
        );
        Assert.Equal(Expected.NewGame, outcome);
    }

    /// <summary>Pins four-consumer Balanced at 75 available power as 28/20/16/11.</summary>
    [Fact]
    public void BalancedPresetOnConstrainedPlayerSplitsAllFourConsumers()
    {
        AllocationChange change = _scenarios.Preset(PresetChoice.Balanced);
        Assert.Equal(new AllocationTuple(28, 20, 16, 11), change.View.Allocation);
        Assert.Equal(Expected.Preset("balanced"), change);
    }

    /// <summary>Pins each priority preset: its consumer first, the rest in canonical order.</summary>
    [Theory]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void PriorityPresetServesItsConsumerFirst(string system) =>
        Assert.Equal(Expected.Preset(system), _scenarios.Preset(new PresetChoice(ShipSystemId.Parse(system))));

    /// <summary>Pins exact-allocation acceptance and atomic rejection, including the current-speed guard.</summary>
    [Fact]
    public void ExactAllocationAcceptsOrRejectsAtomically()
    {
        (OutcomeSequence<AllocationAttempt> attempts, SetTacticalCourseOutcome speed) = _scenarios.ExactAllocation();
        Assert.Equal(SetTacticalCourseOutcome.Accepted, speed);
        Assert.Equal(Expected.ExactAllocationAttempts, attempts);
    }

    /// <summary>Pins generation loss absorbed by reserve versus exact four-consumer brownout.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenerationDamageUsesReserveThenExactBrownout(bool brownout)
    {
        DamageOutcome outcome = _scenarios.Brownout(brownout);
        Assert.Equal(90, outcome.Engineering.AvailablePower);
        Assert.Equal(brownout ? Expected.Brownout : Expected.ReserveAbsorbsGenerationLoss, outcome);
    }

    /// <summary>Pins one unabsorbed 0.25 hit on each system and its derived consequences.</summary>
    [Theory]
    [InlineData("power-generation")]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void SubsystemDamageReducesOnlyTheSelectedSystem(string system) =>
        Assert.Equal(Expected.SubsystemDamage(system), _scenarios.SubsystemDamage(ShipSystemId.Parse(system)));

    /// <summary>Pins passive range from capability, acquisition time, and the exact 2000 ms identifying scan.</summary>
    [Fact]
    public void PassiveSensingAndActiveScanFollowCapabilityAndExactDuration()
    {
        SensingOutcome outcome = _scenarios.SensingAndScanning();
        Assert.Equal(12, outcome.AfterPreset.EffectivePassiveRangeKm);
        Assert.Equal(2000, outcome.ScanInFlight!.CompletesAtMs - outcome.ScanInFlight.StartedAtMs);
        Assert.Equal(Expected.SensingAndScanning, outcome);
    }

    /// <summary>Pins voluntary speed limits and heading-preserving forced deceleration.</summary>
    [Fact]
    public void TacticalMotionIsBoundedByPropulsionCapability() =>
        Assert.Equal(Expected.TacticalMotion, _scenarios.TacticalMotion());

    /// <summary>Pins full absorption by powered shields against an attacker losing weapon condition.</summary>
    [Fact]
    public void ShieldsAbsorbDamageFromADegradingAttacker() =>
        Assert.Equal(Expected.ShieldAbsorption, _scenarios.ShieldAbsorption());

    /// <summary>Pins the production engagement: two absorbed shots, then partial absorption and penetration.</summary>
    [Fact]
    public void FirstGameEngagementPenetratesAfterShieldDepletion() =>
        Assert.Equal(Expected.FirstGameEngagement, _scenarios.FirstGameEngagement());

    /// <summary>Pins partial absorption: shield capacity 0.1 absorbs, 0.15 penetrates to the selected system.</summary>
    [Fact]
    public void WeakShieldsAbsorbPartiallyAndPenetrateTheRemainder() =>
        Assert.Equal(Expected.PartialAbsorption, _scenarios.PartialAbsorption());

    /// <summary>Pins repair begin, linear midpoint, and completion at each consumer's authored duration.</summary>
    [Theory]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void RepairInterpolatesAndCompletesAtAuthoredDuration(string system) =>
        Assert.Equal(Expected.RepairLifecycle(system), _scenarios.RepairLifecycle(ShipSystemId.Parse(system)));

    /// <summary>Pins refusal of a non-improving target, of generation, and of a second concurrent repair.</summary>
    [Fact]
    public void RepairAdmissionRefusesNominalGenerationAndSecondRepair() =>
        Assert.Equal(Expected.RepairAdmission, _scenarios.RepairAdmission());

    /// <summary>Pins that any shield absorption cancels a running shield repair.</summary>
    [Fact]
    public void ShieldAbsorptionInterruptsShieldRepair() =>
        Assert.Equal(
            Expected.ShieldRepairInterruptedByAbsorption,
            _scenarios.RepairHit(ShipSystemId.Shields, ShipSystemId.ImpulsePropulsion, powerShields: true)
        );

    /// <summary>Pins that penetration to the repaired system cancels its repair and keeps the damaged value.</summary>
    [Fact]
    public void PenetrationInterruptsRepairOfTheHitSystem() =>
        Assert.Equal(
            Expected.SensorRepairInterruptedByPenetration,
            _scenarios.RepairHit(ShipSystemId.Sensors, ShipSystemId.Sensors, powerShields: false)
        );

    /// <summary>Pins that damage to another system leaves the repair running to its original completion.</summary>
    [Fact]
    public void DamageElsewhereLeavesRepairRunning() =>
        Assert.Equal(
            Expected.SensorRepairSurvivesImpulseHit,
            _scenarios.RepairHit(ShipSystemId.Sensors, ShipSystemId.ImpulsePropulsion, powerShields: false)
        );

    /// <summary>Pins one delayed, explained return fire exactly one fixed step after the public attack.</summary>
    [Fact]
    public void PublicAttackTriggersOneDelayedReturnFire() =>
        Assert.Equal(Expected.DelayedDefense, _scenarios.DelayedDefense());

    /// <summary>Pins withdrawal selection and its propulsion-limited tactical result.</summary>
    [Fact]
    public void UnpoweredDefenderWithdrawsAtPropulsionLimitedSpeed() =>
        Assert.Equal(Expected.Withdrawal, _scenarios.Withdrawal());
}
