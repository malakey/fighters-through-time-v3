using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.1 Tier-2 string riders — the one authored per-character rule
/// each kit brief promised, delivered through the ordinary hit payload:
/// Tesla's finisher primes Static Charge (Lorentz-chain fuel, zero extra
/// lockdown), Cleopatra's finisher marks with a light Venom on the damage
/// status slot, Lincoln's hit 2 launches at double the bridge's vertical
/// scale, and Mozart's finisher shoves at 5.5x. Non-rider hits must stay
/// status-free, so the shared hitbox/payload can never leak a rider.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BasicStringRiderTests {

    private struct HitRecord {
        public int ControlStatusType;
        public int ControlStatusFrames;
        public int DamageStatusType;
        public long DamageStatusIntensityRaw;
        public FP64 VelocityX;
        public FP64 VelocityY;
    }

    [TestCase]
    public void TeslaFinisherPrimesStaticChargeWithoutExtraLockdown() {
        var simulation = BuildSimulation("tesla", knockback: 0f, seed: 141);
        HitRecord[] hits = LandRecordedChain(simulation, hits: 3);

        // Hits 1-2 carry no status; the finisher magnetizes for exactly its
        // own 24-frame hitstun — pure Lorentz-chain priming.
        AssertThat(hits[0].ControlStatusType).IsEqual((int)StatusType.None);
        AssertThat(hits[1].ControlStatusType).IsEqual((int)StatusType.None);
        AssertThat(hits[2].ControlStatusType).IsEqual((int)StatusType.StaticCharge);
        AssertThat(hits[2].ControlStatusFrames).IsEqual(24);
    }

    [TestCase]
    public void CleopatraFinisherMarksWithALightVenomOnTheDamageSlot() {
        var simulation = BuildSimulation("cleopatra", knockback: 0f, seed: 142);
        HitRecord[] hits = LandRecordedChain(simulation, hits: 3);

        AssertThat(hits[0].DamageStatusType).IsEqual((int)StatusType.None);
        AssertThat(hits[1].DamageStatusType).IsEqual((int)StatusType.None);
        AssertThat(hits[2].DamageStatusType).IsEqual((int)StatusType.Venom);
        AssertThat(hits[2].DamageStatusIntensityRaw)
            .IsEqual((FP64.FromInt(500) / FP64.FromInt(1000)).RawValue);

        // The 2 s / 0.5-intensity mark chips exactly twice at the 1-per-tick
        // floor: string total 33 plus 2 venom chip.
        for (int tick = 400; tick < 540; tick++) {
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick },
                new PlayerInputFrame { Tick = (uint)tick });
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent marked)).IsTrue();
        AssertThat(marked.MaxHP - marked.CurrentHP).IsEqual(35);
    }

    [TestCase]
    public void LincolnHitTwoLaunchesAtDoubleTheTemplateVerticalScale() {
        var lincoln = BuildSimulation("lincoln", knockback: 3f, seed: 143);
        var template = BuildSimulation("tesla", knockback: 3f, seed: 143);
        HitRecord[] lincolnHits = LandRecordedChain(lincoln, hits: 2);
        HitRecord[] templateHits = LandRecordedChain(template, hits: 2);

        AssertThat(lincolnHits[1].VelocityY > FP64.Zero).IsTrue();
        AssertThat(lincolnHits[1].VelocityY > templateHits[1].VelocityY * FP64.FromDouble(1.5))
            .OverrideFailureMessage("Lincoln's hit 2 must launch well above the template bridge lift.")
            .IsTrue();
    }

    [TestCase]
    public void MozartFinisherShovesFartherThanTheTemplateFinisher() {
        var mozart = BuildSimulation("mozart", knockback: 3f, seed: 144);
        var template = BuildSimulation("tesla", knockback: 3f, seed: 144);
        HitRecord[] mozartHits = LandRecordedChain(mozart, hits: 3);
        HitRecord[] templateHits = LandRecordedChain(template, hits: 3);

        FP64 mozartShove = FP64.Abs(mozartHits[2].VelocityX);
        FP64 templateShove = FP64.Abs(templateHits[2].VelocityX);
        AssertThat(mozartShove > templateShove)
            .OverrideFailureMessage("Mozart's 5.5x finisher must out-shove the shared 4.5x.")
            .IsTrue();
    }

    /// <summary>
    /// Walks player one inside melee range, then presses through the chain
    /// (PhaseNone/ChainHold, like BasicStringTestDriver) while recording the
    /// victim's runtime state on the tick each hit's damage lands.
    /// </summary>
    private static HitRecord[] LandRecordedChain(FighterSimulation simulation, int hits) {
        var records = new HitRecord[hits];
        int recorded = 0;
        int tick = 0;

        for (; tick < 200; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent walker)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent standing)).IsTrue();
            if (FP64.Abs(standing.Position.x - walker.Position.x) <= FP64.One) break;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, MoveX = 127 },
                new PlayerInputFrame { Tick = (uint)tick });
        }

        int swingsStarted = 0;
        for (int limit = tick + 400; tick < limit && recorded < hits; tick++) {
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent runtime)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            int hpBefore = before.CurrentHP;
            bool press = swingsStarted < hits
                && (swingsStarted == 0
                    ? runtime.AttackPhase == FighterBasicAttackRules.PhaseNone
                    : runtime.AttackPhase == FighterBasicAttackRules.PhaseChainHold);
            GameplayButtons buttons = press ? GameplayButtons.BasicAttack : GameplayButtons.None;
            simulation.Advance(
                new PlayerInputFrame { Tick = (uint)tick, Held = buttons, Pressed = buttons },
                new PlayerInputFrame { Tick = (uint)tick });
            if (press) swingsStarted++;

            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            if (after.CurrentHP >= hpBefore) continue;
            AssertThat(simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent victim)).IsTrue();
            records[recorded++] = new HitRecord {
                ControlStatusType = victim.StatusType,
                ControlStatusFrames = victim.StatusFrames,
                DamageStatusType = victim.DamageStatusType,
                DamageStatusIntensityRaw = victim.DamageStatusIntensity.RawValue,
                VelocityX = after.Velocity.x,
                VelocityY = after.Velocity.y
            };
        }

        AssertThat(recorded)
            .OverrideFailureMessage("Every requested chain hit must connect.")
            .IsEqual(hits);
        return records;
    }

    private static FighterSimulation BuildSimulation(string characterID, float knockback, int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildAttacker(characterID, knockback)),
        FighterLoadoutFactory.FromCharacterData(BuildHeavyVictim()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildAttacker(string characterID, float knockback) => new() {
        CharacterID = characterID,
        MaxHP = 100,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = knockback,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    /// <summary>
    /// Weight 5 shrinks knockback slides to a fraction of a unit, keeping the
    /// victim inside even Lincoln's 1.7-unit reach for the whole chain while
    /// the launch/shove velocities stay measurable.
    /// </summary>
    private static CharacterData BuildHeavyVictim() => new() {
        CharacterID = "joan",
        MaxHP = 1000,
        Weight = 5f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = 10f,
        BasicAttackKnockback = 3f,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };
}
