using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Package 13 W7a (L03, D14) — the Clockwork Turret in the sim, on the AUTHORED
/// Leonardo loadout: it fires real straight bolts at 12 units/s (construct-class
/// projectiles) only while the opponent is in range, so its lifetime is the idle
/// cap and its 4 bolts the ammunition; and the Ornithopter glide's
/// once-per-flight bonus bolt — new to the sim — spends one of the four.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LeonardoTurretSimTests {
    private static readonly int BoltType = (int)FighterCharacterID.Leonardo * 10 + FighterTurretRules.ConstructBoltSlot;

    [TestCase]
    public void TheTurretFiresAStraightTwelveUnitBoltOnlyWithTheOpponentInRange() {
        // In range (4 units): the first bolt leaves after the 2 s cadence.
        var near = new FighterSimulation(
            ProjectileBurstSimTests.Authored("leonardo"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7401, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        near.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        // Decision 1.2 (2026-10-16): the turret deploys on the cast's active entry.
        int nearDeployed = SpecialCastTestKit.StepUntilFired(near, 0, 1);
        AssertThat(near.TryGetFirstPersistentObject(out FighterPersistentObjectComponent turret)).IsTrue();
        AssertThat(turret.RemainingAttacks).IsEqual(4);
        bool sawBolt = false;
        for (int tick = nearDeployed; tick < nearDeployed + 199 && !sawBolt; tick++) {
            near.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            if (near.TryGetFirstProjectile(out FighterProjectileComponent bolt) && bolt.ProjectileTypeID == BoltType) {
                sawBolt = true;
                FP64 speed = FighterKitGeometry.Sqrt(bolt.Velocity.x * bolt.Velocity.x + bolt.Velocity.y * bolt.Velocity.y);
                AssertThat(FP64.Abs(speed - FP64.FromInt(12)) < FP64.FromDouble(0.01)).IsTrue();
                AssertThat(bolt.Velocity.x > FP64.Zero).IsTrue();
                AssertThat(bolt.AttackClass).IsEqual(FighterDamageRules.BasicAttackClass);
            }
        }
        AssertThat(sawBolt).IsTrue();
        AssertThat(near.TryGetFirstPersistentObject(out FighterPersistentObjectComponent spent)).IsTrue();
        AssertThat(spent.RemainingAttacks).IsEqual(3);

        // Out of range (12 units, past the arena-bounded 10): it never fires, and
        // keeps all four bolts until its idle cap.
        var far = new FighterSimulation(
            ProjectileBurstSimTests.Authored("leonardo"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7402, spawnDistance: 6, rules: FighterMatchRules.Disabled);
        far.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        int farDeployed = SpecialCastTestKit.StepUntilFired(far, 0, 1);
        for (int tick = farDeployed; tick < farDeployed + 399; tick++) {
            far.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
            AssertThat(far.ProjectileCount).IsEqual(0);
        }
        AssertThat(far.TryGetFirstPersistentObject(out FighterPersistentObjectComponent idle)).IsTrue();
        AssertThat(idle.RemainingAttacks).IsEqual(4);
    }

    [TestCase]
    public void TheGlideBonusBoltFiresOncePerFlightAndSpendsOneOfTheFour() {
        var simulation = new FighterSimulation(
            ProjectileBurstSimTests.Authored("leonardo"), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 7403, spawnDistance: 2, rules: FighterMatchRules.Disabled);
        simulation.Advance(Frame(0, GameplayButtons.Special2), Frame(0, GameplayButtons.None));
        // Decision 1.2 (2026-10-16): the cast lock holds the glide press until
        // the turret cast's recovery ends.
        int free = SpecialCastTestKit.StepUntilCastEnds(simulation, 0, 1);
        simulation.Advance(Frame(free, GameplayButtons.None), Frame(free, GameplayButtons.None));
        simulation.Advance(Frame(free + 1, GameplayButtons.MovementAbility), Frame(free + 1, GameplayButtons.None));
        for (int tick = free + 2; tick < free + 5; tick++) {
            simulation.Advance(Frame(tick, GameplayButtons.None), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent gliding)).IsTrue();
        AssertThat(gliding.FloatFrames > 0).IsTrue();
        AssertThat(simulation.ProjectileCount).IsEqual(0);

        // Attack mid-glide: the turret fires one bolt right now, well before its
        // 2 s cadence would have.
        simulation.Advance(Frame(free + 5, GameplayButtons.BasicAttack), Frame(free + 5, GameplayButtons.None));
        AssertThat(simulation.TryGetFirstProjectile(out FighterProjectileComponent bolt)).IsTrue();
        AssertThat(bolt.ProjectileTypeID).IsEqual(BoltType);
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent turret)).IsTrue();
        AssertThat(turret.RemainingAttacks).IsEqual(3);
        AssertThat(turret.GlideBoltSpent).IsEqual(1);

        // A second command in the same flight is refused and spends nothing.
        for (int tick = free + 6; tick < free + 19; tick++) {
            GameplayButtons press = tick == free + 11 ? GameplayButtons.BasicAttack : GameplayButtons.None;
            simulation.Advance(Frame(tick, press), Frame(tick, GameplayButtons.None));
        }
        AssertThat(simulation.TryGetFirstPersistentObject(out FighterPersistentObjectComponent after)).IsTrue();
        AssertThat(after.RemainingAttacks).IsEqual(3);
    }

    private static PlayerInputFrame Frame(int tick, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        Held = pressed,
        Pressed = pressed
    };
}
