using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D10 (user direction 2026-10-11, ledger <c>USER-NOT-YET-FLASH</c>), Fighter
/// side: <see cref="FighterLockoutRefusalRules.Evaluate"/>, the pure classifier
/// <c>FighterSimulationDriver</c> runs after each tick to decide which HUD slot
/// flashes "not yet". A slot flashes only when the ability lockout was the sole
/// reason its press was refused: the slot off cooldown at the moment the press
/// was read (the Ultimate: the meter full before the tick), the fighter in a
/// state the ability system reads presses in, nothing accepted that tick. The
/// press is the post-tick <see cref="FighterRuntimeComponent.PressedButtons"/>,
/// which every input-discard path zeroes — never the submitted frame.
///
/// <para>Pure C#: hand-built component values taken from a real fighter, plus
/// real <see cref="FighterSimulation"/> runs on the engine-free loadout of
/// <c>FighterAbilityLockoutTests</c> (slow projectile Specials, 1-frame slot
/// cooldowns).</para>
/// </summary>
[TestSuite]
public class FighterLockoutRefusalRulesTests {

    private const int Lockout = BasicComboRules.AbilityLockoutFrames;
    private const int SpecialOneBit = (int)GameplayButtons.Special1;
    private const int SpecialTwoBit = (int)GameplayButtons.Special2;
    private const int UltimateBit = (int)GameplayButtons.Ultimate;
    private const int MovementBit = (int)GameplayButtons.MovementAbility;

    [TestCase]
    public void ASpecialPressFlashesOnlyWhenTheLockoutAloneRefusedIt() {
        Components locked = LockedIdle(SpecialOneBit);
        LockoutRefusalBaseline ready = Ready();
        AssertThat(Evaluate(ready, locked)).IsEqual(FighterLockoutRefusal.SpecialOne);

        // Cooling at the read: 5 before the tick is 4 when the system reads it.
        LockoutRefusalBaseline cooling = ready;
        cooling.SpecialOneCooldownFrames = 5;
        Components coolingAfter = locked;
        coolingAfter.Runtime.SpecialOneCooldownFrames = 4;
        AssertThat(Evaluate(cooling, coolingAfter))
            .OverrideFailureMessage("A slot still cooling was refused by its cooldown, not the lockout.")
            .IsEqual(FighterLockoutRefusal.None);

        // The gate open: no lockout at all, or a landed swing's D1 confirm.
        Components open = locked;
        open.Runtime.AbilityLockoutFrames = 0;
        AssertThat(Evaluate(ready, open)).IsEqual(FighterLockoutRefusal.None);
        Components confirmed = locked;
        confirmed.Runtime.SwingLanded = 1;
        confirmed.Runtime.SpecialLockoutFrames = 0;
        AssertThat(Evaluate(ready, confirmed))
            .OverrideFailureMessage("A landed swing opens the Specials (D1): nothing to flash.")
            .IsEqual(FighterLockoutRefusal.None);

        // Frozen at tick start (not the freeze's last tick): no press was read.
        LockoutRefusalBaseline frozen = ready;
        frozen.HitstopFrames = 2;
        AssertThat(Evaluate(frozen, locked)).IsEqual(FighterLockoutRefusal.None);

        // Post-tick states the ability system never reads presses in.
        Components stunned = locked;
        stunned.Fighter.HitstunFrames = 12;
        AssertThat(Evaluate(ready, stunned)).IsEqual(FighterLockoutRefusal.None);
        Components blocking = locked;
        blocking.Verb.ShieldStunFrames = 4;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in blocking.Fighter, in blocking.Runtime, in blocking.Verb))
            .OverrideFailureMessage("Harness: shieldstun holds the block stance up.")
            .IsTrue();
        AssertThat(Evaluate(ready, blocking)).IsEqual(FighterLockoutRefusal.None);
        Components captured = locked;
        captured.Activation.CapturedFrames = 30;
        AssertThat(Evaluate(ready, captured)).IsEqual(FighterLockoutRefusal.None);

        // A Special accepted this tick flashes nothing, not even a same-tick
        // Special 2 the acceptance's own lockout refused.
        Components acceptedOne = LockedIdle(SpecialOneBit | SpecialTwoBit);
        acceptedOne.Runtime.SpecialOneCooldownFrames = 60;
        AssertThat(Evaluate(ready, acceptedOne)).IsEqual(FighterLockoutRefusal.None);

        // And in a real match: a whiffed swing's lockout refuses Special 1 on
        // T+5 and the classifier names it; on T+30 the cast is accepted and
        // nothing flashes.
        FighterSimulation simulation = NewSimulation(seed: 301);
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, 4);
        AssertThat(StepAndClassify(simulation, GameplayButtons.Special1))
            .IsEqual(FighterLockoutRefusal.SpecialOne);
        AssertThat(Shots(simulation, 1)).IsEqual(0);
        Idle(simulation, Lockout - 6);
        AssertThat(Runtime(simulation).AbilityLockoutFrames).IsEqual(1);
        AssertThat(StepAndClassify(simulation, GameplayButtons.Special1))
            .OverrideFailureMessage("The press on T+30 is accepted: no flash.")
            .IsEqual(FighterLockoutRefusal.None);
        AssertThat(Shots(simulation, 1)).IsEqual(1);
    }

    [TestCase]
    public void AHeldBackUltimateFlashesAloneAndAnAcceptedAbilityFlashesNothing() {
        // §1.7: a meter-paid Ultimate the lockout alone refused claims the tick
        // from Special 2 (the LB+RB chord), so only the Ultimate flashes.
        Components chord = LockedIdle(SpecialTwoBit | UltimateBit);
        LockoutRefusalBaseline full = Ready();
        full.Influence = FighterMeterRules.Cap;
        chord.Fighter.Influence = FighterMeterRules.Cap;
        AssertThat(Evaluate(full, chord)).IsEqual(FighterLockoutRefusal.Ultimate);

        // A meter that fills only during the tick could not pay at the read:
        // Special 2 flashes, the Ultimate does not.
        LockoutRefusalBaseline almost = Ready();
        almost.Influence = FighterMeterRules.Cap - FP64.One;
        AssertThat(Evaluate(almost, chord)).IsEqual(FighterLockoutRefusal.SpecialTwo);

        // An Ultimate accepted this tick flashes nothing.
        Components accepted = chord;
        accepted.Runtime.PressedButtons = UltimateBit;
        accepted.Activation.Phase = FighterUltimateActivationRules.PhaseWindup;
        AssertThat(Evaluate(full, accepted)).IsEqual(FighterLockoutRefusal.None);

        // The movement ability is outside the lockout family: its acceptance
        // (the movement cooldown rising) neither suppresses nor causes a flash.
        Components movement = LockedIdle(SpecialTwoBit | MovementBit);
        movement.Runtime.MovementCooldownFrames = 120;
        AssertThat(Evaluate(Ready(), movement)).IsEqual(FighterLockoutRefusal.SpecialTwo);
        Components movementOnly = LockedIdle(MovementBit);
        movementOnly.Runtime.MovementCooldownFrames = 120;
        AssertThat(Evaluate(Ready(), movementOnly)).IsEqual(FighterLockoutRefusal.None);

        // In a real match: the chord at a full meter inside a whiffed swing's
        // lockout leaves the meter untouched and names the Ultimate alone.
        FighterSimulation simulation = NewSimulation(seed: 302);
        AssertThat(simulation.SeedFighterInfluenceForTest(0, FighterMeterRules.CapPoints)).IsTrue();
        Step(simulation, GameplayButtons.BasicAttack);
        Idle(simulation, 4);
        AssertThat(StepAndClassify(simulation, GameplayButtons.Special2 | GameplayButtons.Ultimate))
            .IsEqual(FighterLockoutRefusal.Ultimate);
        AssertThat(Fighter(simulation).Influence).IsEqual(FighterMeterRules.Cap);
        AssertThat(Shots(simulation, 2)).IsEqual(0);
    }

    [TestCase]
    public void ADiscardedPressNeverFlashes() {
        // The classifier reads the post-tick presses: a zeroed word flashes
        // nothing whatever the gate says.
        Components discarded = LockedIdle(0);
        AssertThat(Evaluate(Ready(), discarded)).IsEqual(FighterLockoutRefusal.None);
        Components pressed = LockedIdle(SpecialOneBit);
        AssertThat(Evaluate(Ready(), pressed)).IsEqual(FighterLockoutRefusal.SpecialOne);

        // A real Foresight stance: Tubman's Special 2 is accepted, and Special 1
        // pressed during the stance is discarded by the movement system before
        // any ability reads it. The submitted frame pressed it; the post-tick
        // word does not, and nothing flashes.
        FighterSimulation simulation = NewSimulation(seed: 303, character: FighterCharacterID.Tubman);
        AssertThat(StepAndClassify(simulation, GameplayButtons.Special2))
            .OverrideFailureMessage("Foresight's own acceptance flashes nothing.")
            .IsEqual(FighterLockoutRefusal.None);
        AssertThat(simulation.TryGetFighterCounter(0, out FighterCounterComponent counter)).IsTrue();
        AssertThat(FighterForesightRules.IsBusy(in counter)).OverrideFailureMessage("Harness: the stance runs.").IsTrue();
        Idle(simulation, 2);

        LockoutRefusalBaseline before = Baseline(simulation);
        Step(simulation, GameplayButtons.Special1);
        Components after = Read(simulation);
        AssertThat(simulation.TryGetFighterCounter(0, out counter)).IsTrue();
        AssertThat(FighterForesightRules.IsBusy(in counter)).IsTrue();
        AssertThat(after.Runtime.AbilityLockoutFrames > 0).OverrideFailureMessage("Harness: the lockout runs.").IsTrue();
        AssertThat(after.Runtime.SpecialOneCooldownFrames).OverrideFailureMessage("Harness: Special 1 is ready.").IsEqual(0);
        AssertThat(after.Runtime.PressedButtons & SpecialOneBit)
            .OverrideFailureMessage("The stance discards the press before the ability system reads it.")
            .IsEqual(0);
        AssertThat(Evaluate(before, after)).IsEqual(FighterLockoutRefusal.None);
        // The discard is the only thing standing between that press and a flash.
        Components undiscarded = after;
        undiscarded.Runtime.PressedButtons |= SpecialOneBit;
        AssertThat(Evaluate(before, undiscarded)).IsEqual(FighterLockoutRefusal.SpecialOne);
    }

    [TestCase]
    public void TheLastFreezeTickAndSameTickRefundsAreClassifiedAtReadTime() {
        Components locked = LockedIdle(SpecialOneBit);

        // The freeze's last tick (1 → 0): the ability system reads it, and
        // TickCounters did not run, so a cooldown of 0 before is 0 at the read.
        // A freeze-buffered press released there is such a press.
        LockoutRefusalBaseline lastFreezeTick = Ready();
        lastFreezeTick.HitstopFrames = 1;
        AssertThat(Evaluate(lastFreezeTick, locked)).IsEqual(FighterLockoutRefusal.SpecialOne);
        // ...but a cooldown of 1 before is still 1 at that read.
        lastFreezeTick.SpecialOneCooldownFrames = 1;
        AssertThat(Evaluate(lastFreezeTick, locked)).IsEqual(FighterLockoutRefusal.None);
        // On an unfrozen tick the same 1 was ticked to 0 before the read.
        LockoutRefusalBaseline unfrozen = Ready();
        unfrozen.SpecialOneCooldownFrames = 1;
        AssertThat(Evaluate(unfrozen, locked)).IsEqual(FighterLockoutRefusal.SpecialOne);

        // Any earlier freeze tick read nothing.
        LockoutRefusalBaseline deeper = Ready();
        deeper.HitstopFrames = 2;
        AssertThat(Evaluate(deeper, locked)).IsEqual(FighterLockoutRefusal.None);

        // A cooldown of 2 before reads 1: refused by the cooldown, even though
        // a same-tick Momentum refund has it at 0 after the tick.
        LockoutRefusalBaseline refunded = Ready();
        refunded.SpecialOneCooldownFrames = 2;
        AssertThat(locked.Runtime.SpecialOneCooldownFrames).IsEqual(0);
        AssertThat(Evaluate(refunded, locked))
            .OverrideFailureMessage("A refund after the read cannot make a cooldown refusal flash.")
            .IsEqual(FighterLockoutRefusal.None);
    }

    // === helpers ==============================================================

    /// <summary>The post-tick components of player one.</summary>
    private struct Components {
        public FighterStateComponent Fighter;
        public FighterRuntimeComponent Runtime;
        public FighterVerbComponent Verb;
        public FighterUltimateActivationComponent Activation;
    }

    private static FighterLockoutRefusal Evaluate(in LockoutRefusalBaseline before, in Components after) =>
        FighterLockoutRefusalRules.Evaluate(in before, in after.Fighter, in after.Runtime, in after.Verb, in after.Activation);

    /// <summary>A ready fighter before an unfrozen tick: no hitstop, both Specials off cooldown, an empty meter.</summary>
    private static LockoutRefusalBaseline Ready() => new() {
        HitstopFrames = 0,
        SpecialOneCooldownFrames = 0,
        SpecialTwoCooldownFrames = 0,
        Influence = FP64.Zero,
        UltimateBusy = false
    };

    /// <summary>
    /// A real idle fighter's components, with <paramref name="pressed"/> as the
    /// post-tick presses and a whiffed swing's lockout running (A &gt; 0, C = 0).
    /// </summary>
    private static Components LockedIdle(int pressed) {
        FighterSimulation simulation = NewSimulation(seed: 300);
        Idle(simulation, 2);
        Components components = Read(simulation);
        AssertThat(FighterAbilityLockoutRules.CanReadAbilityPress(
                in components.Fighter, in components.Runtime, in components.Verb))
            .OverrideFailureMessage("Harness: an idle fighter reads ability presses.")
            .IsTrue();
        components.Runtime.PressedButtons = pressed;
        components.Runtime.AbilityLockoutFrames = Lockout - 5;
        components.Runtime.SpecialLockoutFrames = 0;
        components.Runtime.SwingLanded = 0;
        components.Runtime.SpecialOneCooldownFrames = 0;
        components.Runtime.SpecialTwoCooldownFrames = 0;
        return components;
    }

    private static Components Read(FighterSimulation simulation) {
        var components = new Components();
        AssertThat(simulation.TryGetFighter(0, out components.Fighter)).IsTrue();
        AssertThat(simulation.TryGetFighterRuntime(0, out components.Runtime)).IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out components.Verb)).IsTrue();
        AssertThat(simulation.TryGetFighterUltimateActivation(0, out components.Activation)).IsTrue();
        return components;
    }

    private static LockoutRefusalBaseline Baseline(FighterSimulation simulation) {
        Components components = Read(simulation);
        return LockoutRefusalBaseline.Capture(
            in components.Runtime, in components.Verb, in components.Fighter, in components.Activation);
    }

    /// <summary>One tick classified exactly as the driver does: baseline, advance, evaluate.</summary>
    private static FighterLockoutRefusal StepAndClassify(FighterSimulation simulation, GameplayButtons buttons) {
        LockoutRefusalBaseline before = Baseline(simulation);
        Step(simulation, buttons);
        return Evaluate(before, Read(simulation));
    }

    private static FighterStateComponent Fighter(FighterSimulation simulation) => Read(simulation).Fighter;

    private static FighterRuntimeComponent Runtime(FighterSimulation simulation) => Read(simulation).Runtime;

    /// <summary>Slow (0.5 u/s), long-lived shots: in flight for the whole of every case.</summary>
    private static FighterAbilityLoadout ProjectileModes() => FighterAbilityLoadout.Default with {
        SpecialOneExecutionType = 1,
        SpecialTwoExecutionType = 1,
        SpecialOneProjectileSpeed = FP64.FromDouble(0.5),
        SpecialTwoProjectileSpeed = FP64.FromDouble(0.5),
        SpecialOneProjectileLifetimeFrames = 600,
        SpecialTwoProjectileLifetimeFrames = 600
    };

    private static FighterSimulation NewSimulation(
        int seed, FighterCharacterID character = FighterCharacterID.Einstein) =>
        new(
            Loadout(character, ProjectileModes()),
            Loadout(FighterCharacterID.Einstein, ProjectileModes()),
            seed: seed, spawnDistance: 4, rules: FighterMatchRules.Disabled);

    /// <summary>The Default kit's numbers with zero authored Special cooldowns (1 frame after PositiveCooldown).</summary>
    private static FighterLoadout Loadout(FighterCharacterID character, FighterAbilityLoadout modes) => new(
        (int)character,
        100, 3, 2,
        10, 14, 12, 20,
        0, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        (int)StatusType.None, 0,
        FP64.One,
        FP64.FromInt(7),
        FP64.FromDouble(11.5),
        FP64.FromInt(3),
        FP64.FromInt(4),
        FP64.FromInt(4),
        FP64.FromInt(5),
        FP64.One, FP64.One, FP64.One,
        modes);

    private static int Shots(FighterSimulation simulation, int slot) {
        int typeID = Fighter(simulation).CharacterID * 10 + slot;
        var projectiles = new System.Collections.Generic.List<FighterProjectileComponent>();
        simulation.CopyProjectilesTo(projectiles);
        int count = 0;
        foreach (FighterProjectileComponent projectile in projectiles) {
            if (projectile.OwnerPlayerID == 0 && projectile.ProjectileTypeID == typeID) count++;
        }
        return count;
    }

    private static void Step(FighterSimulation simulation, GameplayButtons playerOne) =>
        simulation.Advance(Input(simulation.CurrentTick, playerOne), Input(simulation.CurrentTick, GameplayButtons.None));

    private static void Idle(FighterSimulation simulation, int frames) {
        for (int frame = 0; frame < frames; frame++) Step(simulation, GameplayButtons.None);
    }

    private static PlayerInputFrame Input(int tick, GameplayButtons buttons) => new() {
        Tick = (uint)tick,
        Held = buttons,
        Pressed = buttons
    };
}
