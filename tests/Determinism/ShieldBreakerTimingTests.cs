using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Combat and movement update 2026-10-16, decision 1.3 (plan §2.1.7): the
/// three Shield-Breakers — Divine Piercing, The Emancipator and Splitting
/// Strike — keep the full shatter and gain their authored startup from 1.2.
/// A held block shatters only on or after T+S; a roll, a jump or an air dodge
/// during the startup escapes with all three charges; and after a blocked
/// shatter the attacker stays locked through its whole recovery (its clock
/// paused through the 16-frame shatter freeze), so its first Ultimate comes on
/// the cast's free tick. Production loadouts against a default-kit blocker.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShieldBreakerTimingTests {

    private static readonly (string Name, string Hero, int Slot, int SpawnDistance)[] Breakers = {
        ("Divine Piercing", "joan", 2, 2),
        ("The Emancipator", "lincoln", 1, 2),
        ("Splitting Strike", "lincoln", 2, 1)
    };

    [TestCase]
    public void AHeldBlockShattersOnlyOnOrAfterTheActiveEntry() {
        foreach ((string name, string hero, int slot, int spawnDistance) in Breakers) {
            AbilityData ability = Ability(hero, slot);
            AssertThat(ability.BlockClass).IsEqual(BlockClass.ShieldBreaker);
            FighterSimulation simulation = NewMatch(hero, spawnDistance, seed: 11001);
            int shatterOn = PressAndFindShatter(simulation, slot, ability, blockerFrame: _ => Held(GameplayButtons.Block));
            AssertThat(shatterOn >= ability.StartupFrames)
                .OverrideFailureMessage($"{name}: shattered on T+{shatterOn}, inside its {ability.StartupFrames}-frame startup.")
                .IsTrue();
        }
    }

    [TestCase]
    public void AnEscapeDuringTheStartupKeepsAllThreeCharges() {
        foreach ((string name, string hero, int slot, int spawnDistance) in Breakers) {
            AbilityData ability = Ability(hero, slot);
            int s = ability.StartupFrames;
            int free = s + ability.ActiveFrames + ability.RecoveryFrames;
            var escapes = new List<(string Verb, System.Func<int, PlayerInputFrame> Frame)>();
            if (hero == "lincoln" && slot == 1) {
                // The Emancipator is a grounded-only wave: a jump late in the
                // startup clears it.
                escapes.Add(("a jump", offset => offset == s - 4 ? In(GameplayButtons.Jump) : Neutral()));
            } else {
                // A roll away from the attacker, one tick into the startup, out
                // of the lunge's and the arc's reach; then the stance again.
                escapes.Add(("a roll away", offset => offset == 1 ? In(GameplayButtons.Roll, moveX: 127) : Held(GameplayButtons.Block)));
            }
            // Splitting Strike catches the airborne: an air dodge whose eight
            // invulnerable frames cover its six active ones escapes it.
            if (hero == "lincoln" && slot == 2) {
                escapes.Add(("an air dodge", offset => offset == 1 ? In(GameplayButtons.Jump)
                    : offset == s - 5 ? In(GameplayButtons.Roll) : Neutral()));
            }
            foreach ((string verb, System.Func<int, PlayerInputFrame> frame) in escapes) {
                FighterSimulation simulation = NewMatch(hero, spawnDistance, seed: 11002);
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
                simulation.Advance(Neutral(), Held(GameplayButtons.Block));
                simulation.Advance(In(SlotButton(slot)), Held(GameplayButtons.Block));
                for (int offset = 1; offset <= free + 20; offset++) simulation.Advance(Neutral(), frame(offset));
                AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
                AssertThat(after.BlockCharges)
                    .OverrideFailureMessage($"{name}: {verb} during the startup lost a charge.")
                    .IsEqual(before.BlockCharges);
                AssertThat(after.CurrentHP)
                    .OverrideFailureMessage($"{name}: {verb} during the startup was still hit.")
                    .IsEqual(before.CurrentHP);
            }
        }
    }

    [TestCase]
    public void AfterABlockedShatterTheAttackerIsLockedThroughItsWholeRecovery() {
        foreach ((string name, string hero, int slot, int spawnDistance) in Breakers) {
            AbilityData ability = Ability(hero, slot);
            int free = ability.StartupFrames + ability.ActiveFrames + ability.RecoveryFrames;
            FighterSimulation simulation = NewMatch(hero, spawnDistance, seed: 11003);
            AssertThat(simulation.SeedFighterInfluenceForTest(0, 100)).IsTrue();
            int shatterOn = PressAndFindShatter(simulation, slot, ability, blockerFrame: _ => Held(GameplayButtons.Block));

            // The attacker mashes the Ultimate (meter full) from the shatter on:
            // every press while the cast is busy is discarded — a blocked
            // Shield-Breaker is a Blocked outcome and arms no cancel — and the
            // first press after the cast ends, never before T+S+A+R, is accepted.
            int ultimateOn = -1;
            int freedOn = -1;
            for (int offset = shatterOn + 1; offset <= shatterOn + 200 && ultimateOn < 0; offset++) {
                simulation.Advance(In(GameplayButtons.Ultimate), Neutral());
                bool busy = FighterSpecialCastRules.IsBusy(Cast(simulation));
                if (!busy && freedOn < 0) freedOn = offset;
                if (UltimateActivationTestKit.Phase(simulation, 0) != FighterUltimateActivationRules.PhaseNone) {
                    ultimateOn = offset;
                    AssertThat(busy)
                        .OverrideFailureMessage($"{name}: the Ultimate started inside the busy cast on T+{offset}.")
                        .IsFalse();
                }
            }
            AssertThat(ultimateOn)
                .OverrideFailureMessage($"{name}: the first Ultimate after the shatter on T+{shatterOn} came late.")
                .IsEqual(freedOn);
            AssertThat(ultimateOn >= free)
                .OverrideFailureMessage($"{name}: the attacker was free on T+{ultimateOn}, before T+S+A+R = T+{free}.")
                .IsTrue();

            // Fix round A (CM-FIXA-SIM, ruling (d)): the recovery Block cancel
            // is the attacker's one exit, and its tick raises the stance and
            // nothing else — Block+Roll or Block+Jump on recovery frame 0
            // neither rolls nor jumps (Story's Block cancel drops both). Divine
            // Piercing and Splitting Strike shatter the held stance inside their
            // active window, so their frame 0 comes after the shatter freeze;
            // The Emancipator's wave reaches the stance after its caster is
            // already in recovery, so its frame 0 comes before the shatter.
            var exits = new (string Verb, GameplayButtons Press, System.Func<FighterSimulation, bool> Started)[] {
                ("a roll", GameplayButtons.Roll, sim => sim.TryGetFighterRuntime(0, out FighterRuntimeComponent r) && r.UniversalMovementState != 0),
                ("a jump", GameplayButtons.Jump, sim => sim.TryGetFighter(0, out FighterStateComponent f) && f.IsGrounded == 0)
            };
            foreach ((string verbName, GameplayButtons press, System.Func<FighterSimulation, bool> started) in exits) {
                FighterSimulation cancel = NewMatch(hero, spawnDistance, seed: 11003);
                // The blocker raises the stance before the press and holds it.
                cancel.Advance(Neutral(), Held(GameplayButtons.Block));
                cancel.Advance(In(SlotButton(slot)), Held(GameplayButtons.Block));
                bool pressed = false;
                for (int step = 0; step < 200 && !pressed; step++) {
                    FighterActionStateComponent cast = Cast(cancel);
                    AssertThat(cancel.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
                    // The next unfrozen tick moves the cast from Active to recovery frame 0.
                    pressed = FighterSpecialCastRules.IsActive(in cast) && cast.SpecialPhaseFrames == 1 && verb.HitstopFrames == 0;
                    cancel.Advance(
                        pressed ? new PlayerInputFrame { Pressed = press, Held = press | GameplayButtons.Block } : Neutral(),
                        Held(GameplayButtons.Block));
                }
                AssertThat(pressed).OverrideFailureMessage($"Harness ({name}): the cast never reached its recovery.").IsTrue();
                AssertThat(cancel.TryGetFighter(0, out FighterStateComponent attacker)).IsTrue();
                AssertThat(cancel.TryGetFighterRuntime(0, out FighterRuntimeComponent attackerRuntime)).IsTrue();
                AssertThat(cancel.TryGetFighterVerb(0, out FighterVerbComponent attackerVerb)).IsTrue();
                AssertThat(FighterBasicAttackRules.IsBlockStance(in attacker, in attackerRuntime, in attackerVerb))
                    .OverrideFailureMessage($"{name}: the Block cancel must raise the stance on recovery frame 0.")
                    .IsTrue();
                AssertThat(started(cancel))
                    .OverrideFailureMessage($"{name}: {verbName} started on the Block-cancel tick after a blocked shatter.")
                    .IsFalse();
            }
        }
    }

    /// <summary>
    /// Presses the attacker's Shield-Breaker at T with the blocker sending
    /// <paramref name="blockerFrame"/> from T on; returns the offset of the tick
    /// the shield shattered. Asserts no charge is lost before it.
    /// </summary>
    private static int PressAndFindShatter(
        FighterSimulation simulation, int slot, AbilityData ability, System.Func<int, PlayerInputFrame> blockerFrame) {
        // The blocker raises the stance before the press.
        simulation.Advance(Neutral(), blockerFrame(-1));
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent blocker)).IsTrue();
        int charges = blocker.BlockCharges;
        AssertThat(charges).IsEqual(3);
        simulation.Advance(In(SlotButton(slot)), blockerFrame(0));
        int free = ability.StartupFrames + ability.ActiveFrames + ability.RecoveryFrames;
        for (int offset = 1; offset <= free; offset++) {
            simulation.Advance(Neutral(), blockerFrame(offset));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent now)).IsTrue();
            if (now.BlockCharges == 0) {
                AssertThat(now.DazeFrames > 0).OverrideFailureMessage("A shatter dazes the blocker.").IsTrue();
                return offset;
            }
            AssertThat(now.BlockCharges).IsEqual(charges);
        }
        AssertThat(false).OverrideFailureMessage("Harness: the Shield-Breaker never reached the stance.").IsTrue();
        return -1;
    }

    private static FighterSimulation NewMatch(string hero, int spawnDistance, int seed) {
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(Data(hero)), FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: spawnDistance, rules: FighterMatchRules.Disabled);
        for (int tick = 0; tick < 2; tick++) simulation.Advance(Neutral(), Neutral());
        return simulation;
    }

    private static CharacterData Data(string hero) =>
        AuthoredResources.Load<CharacterData>($"res://resources/Characters/{hero}_data.tres");

    private static AbilityData Ability(string hero, int slot) =>
        slot == 1 ? Data(hero).SpecialAttackOne : Data(hero).SpecialAttackTwo;

    private static GameplayButtons SlotButton(int slot) => slot == 1 ? GameplayButtons.Special1 : GameplayButtons.Special2;

    private static FighterActionStateComponent Cast(FighterSimulation simulation) {
        AssertThat(simulation.TryGetFighterActionState(0, out FighterActionStateComponent cast)).IsTrue();
        return cast;
    }

    private static PlayerInputFrame Neutral() => new();

    private static PlayerInputFrame In(GameplayButtons pressed, sbyte moveX = 0) => new() {
        MoveX = moveX,
        Pressed = pressed,
        Held = pressed
    };

    private static PlayerInputFrame Held(GameplayButtons held) => new() { Held = held };
}
