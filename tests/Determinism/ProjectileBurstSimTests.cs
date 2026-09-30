using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7a — the sim half of the burst-on-terrain projectile primitive
/// (<see cref="FighterProjectileBurstRules"/>), driven through the AUTHORED
/// Einstein and Shakespeare loadouts so the pins are the shipped numbers:
/// E=mc² (E05) is 7 contact + 20 burst in 1.2 units, straight at 12 u/s with no
/// range limit; Yorick's Lament (S03) is 5 contact + 16 wave in 1.5 units,
/// straight at 10 u/s up to 8 units, bursting at max range.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ProjectileBurstSimTests {

    [TestCase]
    public void EmcSquaredDirectHitDealsSevenContactPlusTwentyBurst() {
        var simulation = new FighterSimulation(
            Authored("einstein"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7101, spawnDistance: 3, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent shot)).IsTrue();
        // 720 px/s = 12 units/s, straight (no arc).
        AssertThat(shot.Velocity.x).IsEqual(FP64.FromFloat(720f / 60f));
        AssertThat(shot.GravityPerSecond).IsEqual(FP64.Zero);
        Run(simulation, 1, 60);
        AssertThat(simulation.ProjectileCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(100 - 7 - 20);
    }

    [TestCase]
    public void EmcSquaredCrossesTheStageAndBurstsOnTheWall() {
        // Einstein is player two: he turns to face the empty right side and fires.
        var simulation = new FighterSimulation(
            FighterLoadout.Default(FighterCharacterID.Joan), Authored("einstein"),
            seed: 7102, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.None), Frame(0, GameplayButtons.None, 127));
        simulation.Advance(Frame(1, GameplayButtons.None), Frame(1, GameplayButtons.Special1));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent shot)).IsTrue();
        AssertThat(shot.Velocity.x > FP64.Zero).IsTrue();
        FP64 lastX = shot.Position.x;
        int tick = 2;
        for (; tick < 200 && simulation.ProjectileCount > 0; tick++) {
            simulation.TryGetFirstProjectile(out FighterProjectileComponent flying);
            lastX = flying.Position.x;
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.ProjectileCount).IsEqual(0);
        // It flew all the way to the right wall (x = 10) — no 5-unit range cap —
        // and burst there, far from the opponent, who is untouched.
        AssertThat(lastX > FP64.FromDouble(9.0)).IsTrue();
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent bystander)).IsTrue();
        AssertThat(bystander.CurrentHP).IsEqual(100);
    }

    [TestCase]
    public void YoricksLamentBurstsIntoItsWaveAtMaxRange() {
        // 10 units apart: the skull stops 8 units out, short of a contact, and its
        // 1.5-unit wave reaches the opponent — 16 wave damage and the slow, once.
        var simulation = new FighterSimulation(
            Authored("shakespeare"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7103, spawnDistance: 5, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special1), Frame(0, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent skull)).IsTrue();
        AssertThat(skull.Velocity.x).IsEqual(FP64.FromFloat(600f / 60f));
        // 0.8 s = 48 frames authored; the spawn tick already flew one.
        AssertThat(skull.LifetimeFrames).IsEqual(47);
        bool slowed = false;
        for (int tick = 1; tick < 80; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            if (simulation.TryGetFighterRuntime(1, out FighterRuntimeComponent runtime)
                && runtime.StatusType == (int)StatusType.TimeDilation) slowed = true;
        }
        AssertThat(simulation.ProjectileCount).IsEqual(0);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent target)).IsTrue();
        AssertThat(target.CurrentHP).IsEqual(100 - 16);
        AssertThat(slowed).IsTrue();
    }

    [TestCase]
    public void TheAuthoredContractsProjectTheBurstPrimitive() {
        FighterLoadout einstein = Authored("einstein");
        FighterAbilityHitData emc = einstein.AbilityModes.SpecialOneHit;
        AssertThat(emc.Bursts).IsTrue();
        AssertThat(emc.BurstsOnTerrain).IsTrue();
        AssertThat(emc.ContactDamage).IsEqual(7);
        AssertThat(einstein.SpecialOneDamage).IsEqual(20);
        AssertThat(emc.BurstRadius).IsEqual(FP64.FromFloat(72f / 60f));

        FighterLoadout shakespeare = Authored("shakespeare");
        FighterAbilityHitData yorick = shakespeare.AbilityModes.SpecialOneHit;
        AssertThat(yorick.Bursts).IsTrue();
        AssertThat(yorick.ContactDamage).IsEqual(5);
        AssertThat(shakespeare.SpecialOneDamage).IsEqual(16);
        AssertThat(yorick.BurstRadius).IsEqual(FP64.FromFloat(90f / 60f));

        // Nobody else authors a bursting Special 1/2 in this workstream's kits.
        AssertThat(Authored("tesla").AbilityModes.SpecialOneHit.Bursts).IsFalse();
        AssertThat(Authored("leonardo").AbilityModes.SpecialTwoHit.Bursts).IsFalse();
    }

    internal static FighterLoadout Authored(string characterID) =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres"));

    private static void Run(FighterSimulation simulation, int from, int count) {
        for (int tick = from; tick < from + count; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed, sbyte moveX = 0) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        Held = pressed,
        Pressed = pressed
    };
}
