using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// Pins the V7.2 universal grab &amp; throws in the deterministic simulation:
/// the Block+BasicAttack chord, grab-beats-block (a blocking victim is seized
/// and the throw pays no shieldstun), the punishing whiff, the up throw's
/// vertical launch, and the anti-loop throw immunity. Numbers come from
/// <see cref="BasicComboRules"/> and <c>FighterGrabRules</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterGrabTests {

    [TestCase]
    public void GrabBeatsBlockAndTheThrowIsUnblockable() {
        var simulation = NewSimulation(seed: 971);
        int tick = WalkIntoGrabRange(simulation);

        // The victim turtles the whole way; the chord seizes them regardless.
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            FrameHeld(tick - 1, GameplayButtons.Block));
        for (int step = 0; step < BasicComboRules.GrabStartupFrames + 2; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            tick++;
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent held)).IsTrue();
        AssertThat(held.BeingHeld)
            .OverrideFailureMessage("The grab must seize a blocking victim — grab beats block.")
            .IsEqual(1);
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victimHeld)).IsTrue();
        int chargesBefore = victimHeld.BlockCharges;

        // Ride the decision window and throw animation out: the throw's
        // 1.0x BasicDamage lands as real HP (no charge is spent — unblockable)
        // and the victim leaves with throw immunity.
        for (int step = 0; step < BasicComboRules.ThrowDecisionFrames + BasicComboRules.ThrowAnimationFrames + 4; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent thrown)).IsTrue();
        AssertThat(thrown.CurrentHP < thrown.MaxHP)
            .OverrideFailureMessage("The throw's damage must land through the block.")
            .IsTrue();
        AssertThat(thrown.BlockCharges)
            .OverrideFailureMessage("An unblockable throw spends no shield charge.")
            .IsEqual(chargesBefore);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent released)).IsTrue();
        AssertThat(released.BeingHeld).IsEqual(0);
        AssertThat(released.ThrowImmunityFrames > 0)
            .OverrideFailureMessage("The thrown victim leaves with the anti-regrab window.")
            .IsTrue();
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent grabber)).IsTrue();
        AssertThat(grabber.GrabPhase).IsEqual(0);
    }

    [TestCase]
    public void AGrabWhiffsAgainstAnAirborneVictimIntoTheLongRecovery() {
        var simulation = NewSimulation(seed: 972);
        int tick = WalkIntoGrabRange(simulation);

        // The victim jumps as the grab comes out: airborne targets whiff it.
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            Frame(tick - 1, 0, GameplayButtons.Jump));
        for (int step = 0; step < BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames + 1; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent untouched)).IsTrue();
        AssertThat(untouched.BeingHeld).IsEqual(0);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent whiffed)).IsTrue();
        AssertThat(whiffed.GrabPhase)
            .OverrideFailureMessage("The empty active window must fall into whiff recovery.")
            .IsEqual(3);
        AssertThat(whiffed.GrabPhaseFrames > BasicComboRules.GrabClashBounceFrames)
            .OverrideFailureMessage("The whiff eats the long 24-frame recovery, not the clash bounce.")
            .IsTrue();
    }

    [TestCase]
    public void TheUpThrowLaunchesVerticallyAndTheForwardThrowHorizontally() {
        // Up throw: hold Up through the decision window.
        var upSim = NewSimulation(seed: 973);
        FighterStateComponent upVictim = RunThrow(upSim, moveX: 0, moveY: -127);
        AssertThat(upVictim.Velocity.y > FP64.Zero).IsTrue();
        AssertThat(FP64.Abs(upVictim.Velocity.y) > FP64.Abs(upVictim.Velocity.x))
            .OverrideFailureMessage(
                $"The up throw is the launcher — vertical must dominate "
                + $"(v=({upVictim.Velocity.x.ToFloat()}, {upVictim.Velocity.y.ToFloat()})).")
            .IsTrue();

        // Forward throw: neutral input resolves forward.
        var forwardSim = NewSimulation(seed: 973);
        FighterStateComponent forwardVictim = RunThrow(forwardSim, moveX: 0, moveY: 0);
        AssertThat(FP64.Abs(forwardVictim.Velocity.x) > FP64.Abs(forwardVictim.Velocity.y))
            .OverrideFailureMessage("The forward throw is the spacing tool — horizontal must dominate.")
            .IsTrue();
    }

    [TestCase]
    public void AnAttackBeatsAGrabInStartup() {
        // V7.3 grab-triangle fix: the grab chord holds Block, but a grabbing
        // fighter has NO functioning shield — a swing landing during the grab
        // startup deals full damage and breaks the attempt.
        var simulation = NewSimulation(seed: 974);
        int tick = WalkIntoGrabRange(simulation);

        // P1 swings the same tick P2 chords a grab; the opener's startup is
        // shorter than the grab's 10 frames, so the hit lands mid-startup.
        simulation.Advance(
            Frame(tick, 0, GameplayButtons.BasicAttack),
            FrameChord(tick, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack));
        tick++;
        for (int step = 0; step < BasicComboRules.GrabStartupFrames + 4; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), FrameHeld(tick, GameplayButtons.Block));
            tick++;
        }

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.CurrentHP < struck.MaxHP)
            .OverrideFailureMessage("The swing must land through the grab chord's held Block — attack beats grab.")
            .IsTrue();
        AssertThat(struck.BlockCharges)
            .OverrideFailureMessage("No charge may be spent: the grabbing fighter had no shield to absorb with.")
            .IsEqual(3);
        AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent broken)).IsTrue();
        AssertThat(broken.GrabPhase)
            .OverrideFailureMessage("The landed hit must break the grab attempt.")
            .IsEqual(0);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent attacker)).IsTrue();
        AssertThat(attacker.BeingHeld).IsEqual(0);
    }

    [TestCase]
    public void AGrabWhiffRecoveryHasNoShield() {
        // V7.3 grab-triangle fix, the rules pin: no grab phase — startup,
        // active, the 24f whiff recovery — and no held state leaves the shield
        // functioning, even with Block held and charges available.
        var fighter = new FighterStateComponent {
            PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100,
            BlockCharges = 3, IsGrounded = 1
        };
        var runtime = new FighterRuntimeComponent { HeldButtons = (int)GameplayButtons.Block };
        var verb = new FighterVerbComponent();
        AssertThat(FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in verb))
            .OverrideFailureMessage("The baseline blocker must be in the stance.")
            .IsTrue();

        foreach (int phase in new[] {
                     FighterGrabRules.PhaseStartup, FighterGrabRules.PhaseActive,
                     FighterGrabRules.PhaseRecovery, FighterGrabRules.PhaseHolding,
                     FighterGrabRules.PhaseThrowAnimation }) {
            FighterVerbComponent grabbing = verb;
            grabbing.GrabPhase = phase;
            AssertThat(FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in grabbing))
                .OverrideFailureMessage($"Grab phase {phase} must have no functioning shield.")
                .IsFalse();
        }

        FighterVerbComponent held = verb;
        held.BeingHeld = 1;
        AssertThat(FighterBasicAttackRules.IsBlockStance(in fighter, in runtime, in held))
            .OverrideFailureMessage("A held victim has no shield.")
            .IsFalse();
    }

    [TestCase]
    public void AGrabWhiffsAgainstAVictimInShieldstun() {
        // V7.3 amendment: grab beats the *stance*, never the *stun*. The direct
        // rules pin — in 1v1 the grabber is also the only shieldstun source and
        // the 10f grab startup outlasts the 8f stun, so the rule bites under
        // construct-assisted pressure; every grab resolves through this window.
        var grabber = new FighterStateComponent {
            PlayerID = 0, Stocks = 3, MaxHP = 100, CurrentHP = 100,
            IsGrounded = 1, FacingRight = 1
        };
        var target = new FighterStateComponent {
            PlayerID = 1, Stocks = 3, MaxHP = 100, CurrentHP = 100,
            IsGrounded = 1, BlockCharges = 2,
            Position = new FPVector2(FP64.FromDouble(0.5), FP64.Zero)
        };
        var targetRuntime = new FighterRuntimeComponent { HeldButtons = (int)GameplayButtons.Block };
        var targetVerb = new FighterVerbComponent();

        AssertThat(FighterGrabRules.ActiveWindowSeizes(in grabber, in target, in targetRuntime, in targetVerb))
            .OverrideFailureMessage("The baseline blocking victim must be seizable — grab beats block.")
            .IsTrue();

        targetVerb.ShieldStunFrames = 1;
        AssertThat(FighterGrabRules.ActiveWindowSeizes(in grabber, in target, in targetRuntime, in targetVerb))
            .OverrideFailureMessage("A victim locked in shieldstun must whiff the grab.")
            .IsFalse();
    }

    [TestCase]
    public void AThrowCollectsTheRallyEcho() {
        // V7.3 ruling #10: "any direct hit, including throws" collects the
        // Rally echo. Throws route through ApplyFighterHit, so the reclaim
        // follows the new capped rule: min(pool, throwDamage x 2), with the
        // remainder persisting. P2's heavy opener stashes a large pool on P1;
        // P1's forward throw (10 damage, cap 20) then reclaims exactly 20.
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(BuildGrappler(maxHP: 400)),
            // Zero knockback so the wounded grabber-to-be stays planted in
            // grab range instead of being launched across the stage.
            FighterLoadoutFactory.FromCharacterData(BuildGrappler(maxHP: 400, damage: 300f, knockback: 0f)),
            seed: 975,
            spawnDistance: 1,
            rules: FighterMatchRules.Disabled);
        int tick = WalkIntoGrabRange(simulation);

        // P2's opener lands: 240 damage on P1, who stashes the echo.
        simulation.Advance(
            Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.BasicAttack));
        tick++;
        for (int step = 0; step < 50; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
        }
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent wounded)).IsTrue();
        AssertThat(wounded.CurrentHP)
            .OverrideFailureMessage("The heavy opener must have landed on the future grabber.")
            .IsEqual(160);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent pool)).IsTrue();
        float capDamage = 10f * BasicComboRules.RallyReclaimDamageMultiplier;
        AssertThat(pool.EchoPool.ToFloat() > capDamage)
            .OverrideFailureMessage("The stashed pool must exceed the throw's reclaim cap for the pin to bind.")
            .IsTrue();

        // P1 grabs and holds neutral: forward throw at the decision window's
        // end, resolving through ApplyFighterHit.
        simulation.Advance(
            FrameChord(tick, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            Frame(tick, 0, GameplayButtons.None));
        tick++;
        bool thrown = false;
        for (int step = 0; step < 90 && !thrown; step++) {
            simulation.Advance(Frame(tick, 0, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
            tick++;
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent victimVerb)).IsTrue();
            thrown = victimVerb.ThrowImmunityFrames > 0;
        }
        AssertThat(thrown).OverrideFailureMessage("The throw never resolved.").IsTrue();

        AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
        AssertThat(victim.CurrentHP)
            .OverrideFailureMessage("The throw's 10 damage must have landed.")
            .IsEqual(390);
        AssertThat(simulation.TryGetFighter(0, out FighterStateComponent healed)).IsTrue();
        AssertThat(healed.CurrentHP)
            .OverrideFailureMessage("The throw must reclaim exactly throwDamage x 2 from the echo pool.")
            .IsEqual(160 + (int)capDamage);
        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent remainder)).IsTrue();
        AssertThat(remainder.EchoPool > FP64.Zero)
            .OverrideFailureMessage("The unreclaimed remainder must persist after the throw.")
            .IsTrue();
    }

    // ---- Harness -------------------------------------------------------------

    /// <summary>Grabs, holds with the given decision input, and returns the
    /// victim's state on the first tick after the launch.</summary>
    private static FighterStateComponent RunThrow(FighterSimulation simulation, sbyte moveX, sbyte moveY) {
        int tick = WalkIntoGrabRange(simulation);
        simulation.Advance(
            FrameChord(tick++, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            Frame(tick - 1, 0, GameplayButtons.None));
        int total = BasicComboRules.GrabStartupFrames + BasicComboRules.GrabActiveFrames
            + BasicComboRules.ThrowDecisionFrames + BasicComboRules.ThrowAnimationFrames + 2;
        for (int step = 0; step < total; step++) {
            simulation.Advance(
                new PlayerInputFrame {
                    Tick = (uint)tick, MoveX = moveX, MoveY = moveY,
                    Held = GameplayButtons.None, Pressed = GameplayButtons.None
                },
                Frame(tick, 0, GameplayButtons.None));
            tick++;
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent verb)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent victim)).IsTrue();
            if (verb.ThrowImmunityFrames > 0) return victim;
        }
        AssertThat(false).OverrideFailureMessage("The throw never resolved.").IsTrue();
        return default;
    }

    /// <summary>Walks player one toward the victim until the pushboxes hold
    /// them at contact range (inside the 0.8-unit grab reach).</summary>
    // === C01c direct bind / preset chord, and F23 (Package 11 A1c) ===

    /// <summary>
    /// C01c: the direct <c>gameplay_grab</c> bind and the Block+BasicAttack preset
    /// chord request the <b>same verb once</b>. A frame carrying both routes starts
    /// exactly one grab — no double-fire — and the direct route grants no extra
    /// priority, leniency or resource bypass.
    /// </summary>
    [TestCase]
    public void TheDirectBindAndTheChordProduceOneGrabRequest() {
        // The direct bit alone, with no chord present at all.
        var direct = NewSimulation(seed: 4101);
        int tick = WalkIntoGrabRange(direct);
        direct.Advance(
            FrameChord(tick, GameplayButtons.Grab, GameplayButtons.Grab),
            FrameHeld(tick, GameplayButtons.None));
        AssertThat(direct.TryGetFighterVerb(0, out FighterVerbComponent fromBind)).IsTrue();
        AssertThat(fromBind.GrabPhase)
            .OverrideFailureMessage("The direct gameplay_grab bind must start a grab on its own.")
            .IsEqual(1);

        // The chord alone, exactly as it shipped.
        var chord = NewSimulation(seed: 4101);
        tick = WalkIntoGrabRange(chord);
        chord.Advance(
            FrameChord(tick, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            FrameHeld(tick, GameplayButtons.None));
        AssertThat(chord.TryGetFighterVerb(0, out FighterVerbComponent fromChord)).IsTrue();
        AssertThat(fromChord.GrabPhase).IsEqual(1);

        // Both at once: one request, one grab, at the same phase frame count as
        // either route alone — nothing double-fires and nothing is skipped.
        var both = NewSimulation(seed: 4101);
        tick = WalkIntoGrabRange(both);
        both.Advance(
            FrameChord(
                tick,
                GameplayButtons.Block | GameplayButtons.BasicAttack | GameplayButtons.Grab,
                GameplayButtons.BasicAttack | GameplayButtons.Grab),
            FrameHeld(tick, GameplayButtons.None));
        AssertThat(both.TryGetFighterVerb(0, out FighterVerbComponent fromBoth)).IsTrue();
        AssertThat(fromBoth.GrabPhase).IsEqual(1);
        AssertThat(fromBoth.GrabPhaseFrames)
            .OverrideFailureMessage("Two routes must not advance the grab twice on one tick.")
            .IsEqual(fromChord.GrabPhaseFrames);
    }

    /// <summary>
    /// C01c's normalized origin flag: when the originating machine has its preset
    /// chords switched <b>off</b> it sets <see cref="GameplayButtons.DirectOrigin"/>,
    /// and the simulation must not re-recognize the component bits as a chord.
    /// Block+BasicAttack then means what it plainly says — block, plus an attack
    /// input the stance ignores.
    ///
    /// <para>This is what stops a peer resolving a remote frame with its own
    /// shortcut settings, which is the whole reason protocol v3 exists.</para>
    /// </summary>
    [TestCase]
    public void DirectOriginSuppressesChordRecognition() {
        var simulation = NewSimulation(seed: 4102);
        int tick = WalkIntoGrabRange(simulation);
        simulation.Advance(
            FrameChord(
                tick,
                GameplayButtons.Block | GameplayButtons.BasicAttack | GameplayButtons.DirectOrigin,
                GameplayButtons.BasicAttack),
            FrameHeld(tick, GameplayButtons.None));

        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent verb)).IsTrue();
        AssertThat(verb.GrabPhase)
            .OverrideFailureMessage("A chord must not be re-recognized when the sender disabled it.")
            .IsEqual(0);

        // The direct bit still works from that same machine, which is the point of
        // disabling the chord rather than the verb.
        var withBind = NewSimulation(seed: 4102);
        tick = WalkIntoGrabRange(withBind);
        withBind.Advance(
            FrameChord(tick, GameplayButtons.Grab | GameplayButtons.DirectOrigin, GameplayButtons.Grab),
            FrameHeld(tick, GameplayButtons.None));
        AssertThat(withBind.TryGetFighterVerb(0, out FighterVerbComponent bound)).IsTrue();
        AssertThat(bound.GrabPhase).IsEqual(1);
    }

    /// <summary>
    /// F23: the grab attachment is explicit snapshot state. The partner used to be
    /// resolved positionally by the two-fighter systems — something no restored
    /// snapshot could describe — and the throw carried no once-only damage guard
    /// beyond the phase machine.
    ///
    /// <para>Also pins the block-stance rule for the whole grab: a grabbing fighter
    /// is never in the block stance, including through whiff recovery, so the
    /// attack-beats-grab triangle cannot be short-circuited by a phantom shield.</para>
    /// </summary>
    [TestCase]
    public void TheGrabAttachmentIsExplicitAndTheStanceIsNeverUpDuringIt() {
        var simulation = NewSimulation(seed: 4103);
        int tick = WalkIntoGrabRange(simulation);

        AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent idle)).IsTrue();
        AssertThat(idle.GrabPartnerPlayerID)
            .OverrideFailureMessage("An unattached fighter must read -1, not player 0.")
            .IsEqual(-1);

        simulation.Advance(
            FrameChord(tick, GameplayButtons.Block | GameplayButtons.BasicAttack, GameplayButtons.BasicAttack),
            FrameHeld(tick, GameplayButtons.None));
        tick++;

        bool sawHold = false;
        for (int step = 0; step < 40; step++) {
            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent grabber)).IsTrue();
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent grabberState)).IsTrue();
            AssertThat(simulation.TryGetFighterRuntime(0, out FighterRuntimeComponent grabberRuntime)).IsTrue();
            if (grabber.GrabPhase != 0) {
                // Block is held on every one of these frames, and the stance is
                // still never up for any part of the grab.
                AssertThat(FighterBasicAttackRules.IsBlockStance(
                        in grabberState, in grabberRuntime, in grabber))
                    .OverrideFailureMessage(
                        $"The block stance was up during grab phase {grabber.GrabPhase}.")
                    .IsFalse();
            }
            if (grabber.GrabPhase == 4) {
                sawHold = true;
                AssertThat(grabber.GrabPartnerPlayerID)
                    .OverrideFailureMessage("A holding grabber must record its victim.")
                    .IsEqual(1);
                AssertThat(grabber.ThrowDamageApplied)
                    .OverrideFailureMessage("The once-only throw guard must start down.")
                    .IsFalse();
                AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent held)).IsTrue();
                AssertThat(held.GrabPartnerPlayerID).IsEqual(0);
                break;
            }
            simulation.Advance(
                FrameHeld(tick, GameplayButtons.Block),
                FrameHeld(tick, GameplayButtons.None));
            tick++;
        }
        AssertThat(sawHold)
            .OverrideFailureMessage("Harness: the grab never reached its hold phase.")
            .IsTrue();

        // Ride the throw out; the attachment is released on both sides afterwards.
        for (int step = 0; step < 90; step++) {
            simulation.Advance(
                FrameHeld(tick, GameplayButtons.None),
                FrameHeld(tick, GameplayButtons.None));
            tick++;
            AssertThat(simulation.TryGetFighterVerb(0, out FighterVerbComponent grabber)).IsTrue();
            if (grabber.GrabPhase == 0) {
                AssertThat(grabber.GrabPartnerPlayerID)
                    .OverrideFailureMessage("A finished grab must release its attachment.")
                    .IsEqual(-1);
                return;
            }
        }
        AssertThat(false).OverrideFailureMessage("Harness: the grab never resolved.").IsTrue();
    }

    private static int WalkIntoGrabRange(FighterSimulation simulation) {
        int tick = 0;
        for (; tick < 90; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent one)).IsTrue();
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent two)).IsTrue();
            if (FP64.Abs(two.Position.x - one.Position.x) <= FP64.FromDouble(BasicComboRules.GrabReachUnits)) {
                return tick;
            }
            simulation.Advance(Frame(tick, 127, GameplayButtons.None), Frame(tick, 0, GameplayButtons.None));
        }
        AssertThat(false).OverrideFailureMessage("Never reached grab range.").IsTrue();
        return tick;
    }

    private static FighterSimulation NewSimulation(int seed) => new(
        FighterLoadoutFactory.FromCharacterData(BuildGrappler()),
        FighterLoadoutFactory.FromCharacterData(BuildGrappler()),
        seed: seed,
        spawnDistance: 1,
        rules: FighterMatchRules.Disabled);

    private static CharacterData BuildGrappler(int maxHP = 100, float damage = 10f, float knockback = 3f) => new() {
        CharacterID = "tesla",
        MaxHP = maxHP,
        Weight = 1f,
        MaxBlockCharges = 3,
        MaxJumpCount = 1,
        MaxMoveSpeed = 8f,
        MaxJumpForce = 13f,
        BasicAttackDamage = damage,
        BasicAttackKnockback = knockback,
        SpecialAttackOne = new AbilityData(),
        SpecialAttackTwo = new AbilityData(),
        MovementAbility = new MovementAbilityData(),
        UltimateAttack = new AbilityData { BaseDamage = 20f }
    };

    private static PlayerInputFrame Frame(int tick, sbyte moveX, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = moveX,
        MoveY = 0,
        Held = pressed,
        Pressed = pressed
    };

    private static PlayerInputFrame FrameHeld(int tick, GameplayButtons held) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = held,
        Pressed = GameplayButtons.None
    };

    private static PlayerInputFrame FrameChord(int tick, GameplayButtons held, GameplayButtons pressed) => new() {
        Tick = (uint)tick,
        MoveX = 0,
        MoveY = 0,
        Held = held,
        Pressed = pressed
    };
}
