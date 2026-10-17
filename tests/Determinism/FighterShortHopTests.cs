using System;
using System.Collections.Generic;
using System.Globalization;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Determinism;

/// <summary>
/// CM-MOVE (combat and movement update 2026-10-16, decision 5.1 with the Q4 and
/// Q5 rulings; plan §2.5.1; ledger <c>USER-SIM-SHORT-HOP</c>,
/// <c>PROVISIONAL-SIM-SHORT-HOP-LATCH</c>, <c>PROVISIONAL-SIM-SHORT-HOP-HEIGHT</c>,
/// <c>USER-SHORT-HOP-OWN-JUMP-ONLY</c>): the Fighter sim's short hop — gravity
/// ×2.5 while a fighter rises from its own Jump press with Jump released —
/// on the production loadouts of the whole roster.
/// </summary>
/// <remarks>
/// <see cref="RequireGodotRuntimeAttribute"/>: the hero jump speeds come from
/// the <c>CharacterData</c> .tres through <see cref="FighterLoadoutFactory"/>
/// and the roster from <see cref="CharacterRoster"/>, both of which need the
/// engine (CLAUDE.md failure signature 7). Every input that releases Jump is
/// built with <see cref="PlayerInputFrame.Create"/>, which computes the
/// <c>Released</c> edge the latch reads; hand-built frames that never set it
/// stay full hops (plan §1.8). The measured heights are printed to the Godot
/// log under <c>[CM-MOVE]</c> for the ledger.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class FighterShortHopTests {
    private const int MaxTicks = 240;

    /// <summary>
    /// Q4: taps of 1, 2 and 3 ticks (Jump held on the press tick and the next
    /// 0, 1 or 2) rise monotonically — 1 ≤ 2 ≤ 3 &lt; held — and each stays at
    /// or under 55 % of the held apex, for every roster hero. The 45 % floor of
    /// the decided band is not asserted: a hero under it is recorded for
    /// <c>PROVISIONAL-SIM-SHORT-HOP-HEIGHT</c>, never retuned (Q4 ruling).
    /// </summary>
    [TestCase]
    public void TapsOfOneTwoAndThreeTicksRiseMonotonicallyAndStayUnderFiftyFivePercentOfTheHeldApex() {
        var issues = new List<string>();
        foreach (string characterID in CharacterRoster.IDs) {
            FighterLoadout loadout = Loadout(characterID);
            FP64 held = ApexOf(loadout, tapTicks: int.MaxValue);
            FP64[] taps = new FP64[3];
            for (int ticks = 1; ticks <= 3; ticks++) taps[ticks - 1] = ApexOf(loadout, ticks);

            double heldUnits = held.ToDouble();
            string line = string.Format(CultureInfo.InvariantCulture,
                "[CM-MOVE] short hop {0}: held {1:0.000} u; tap1 {2:0.000} u ({3:0.0} %), tap2 {4:0.000} u ({5:0.0} %), tap3 {6:0.000} u ({7:0.0} %)",
                characterID, heldUnits,
                taps[0].ToDouble(), 100.0 * taps[0].ToDouble() / heldUnits,
                taps[1].ToDouble(), 100.0 * taps[1].ToDouble() / heldUnits,
                taps[2].ToDouble(), 100.0 * taps[2].ToDouble() / heldUnits);
            GD.Print(line);

            if (!(taps[0] <= taps[1] && taps[1] <= taps[2] && taps[2] < held)) {
                issues.Add($"{characterID}: taps not monotone below the held apex ({line})");
            }
            FP64 cap = held * FP64.FromDouble(0.55);
            for (int index = 0; index < 3; index++) {
                if (taps[index] > cap) issues.Add($"{characterID}: tap {index + 1} above 55 % ({line})");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// A held jump is unchanged tick for tick: a <see cref="PlayerInputFrame.Create"/>
    /// stream holding Jump from the press to the landing, the same jump built by
    /// hand (Held only, no <c>Released</c>, the way about 33 suites press it),
    /// and a direct integration through the pre-2026-10-16 four-argument
    /// <see cref="FighterAirPhysics.StepMovement(FP64, bool, bool, bool)"/> give
    /// the identical height on every tick, the hang included.
    /// </summary>
    [TestCase]
    public void AHeldJumpIsUnchangedTickForTick() {
        foreach (string characterID in CharacterRoster.IDs) {
            FighterLoadout loadout = Loadout(characterID);
            List<long> created = Trajectory(loadout, tick => tick == 0
                ? PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Jump)
                : PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.Jump, GameplayButtons.Jump));
            List<long> handBuilt = Trajectory(loadout, tick => new PlayerInputFrame {
                Tick = (uint)tick,
                Held = GameplayButtons.Jump,
                Pressed = tick == 0 ? GameplayButtons.Jump : GameplayButtons.None
            });

            // The direct integration: the jump tick sets the launch speed and
            // steps it once (the movement system's order), then the legacy step.
            var integrated = new List<long>();
            FP64 vy = loadout.JumpSpeed;
            FP64 y = FP64.Zero;
            int remaining = loadout.MaxJumpCount - 1;
            for (int tick = 0; tick < MaxTicks; tick++) {
                vy = FighterAirPhysics.StepMovement(vy, false, false,
                    FighterAirPhysics.HasJumpedThisAirtime(remaining, loadout.MaxJumpCount));
                y += vy * FighterAirPhysics.FixedDelta;
                if (y <= FP64.Zero) { integrated.Add(0); break; }
                integrated.Add(y.RawValue);
            }

            AssertThat(created.Count)
                .OverrideFailureMessage($"{characterID}: the Create-built and hand-built held jumps last differently.")
                .IsEqual(handBuilt.Count);
            AssertThat(created.Count)
                .OverrideFailureMessage($"{characterID}: the held jump and the legacy integration land on different ticks.")
                .IsEqual(integrated.Count);
            for (int tick = 0; tick < created.Count; tick++) {
                AssertThat(created[tick])
                    .OverrideFailureMessage($"{characterID} tick {tick}: Create-built differs from hand-built.")
                    .IsEqual(handBuilt[tick]);
                AssertThat(created[tick])
                    .OverrideFailureMessage($"{characterID} tick {tick}: the held jump differs from the legacy step.")
                    .IsEqual(integrated[tick]);
            }
        }
    }

    /// <summary>
    /// The launch states never take the band. A fighter launched by a hit while
    /// its Jump is released — with component 323's own-jump bit and released
    /// latch forced on before every tick — steps every airborne hitstun tick
    /// through <see cref="FighterAirPhysics.StepLaunch"/> exactly: the launch
    /// branch reads neither the latch nor the bit. (The daze shares the branch.)
    /// </summary>
    [TestCase]
    public void TheLaunchStatesNeverTakeTheBand() {
        var simulation = LauncherSimulation(hitstunFrames: 30, out int fireTick, seed: 8101);
        // The victim never holds Jump in its hitstun; the bits say "own jump,
        // released" anyway, before every tick.
        int checkedTicks = 0;
        bool launched = false;
        int tick = fireTick;
        for (int step = 0; step < 120; step++, tick++) {
            ForceReleasedOwnJump(simulation, playerID: 1);
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent beforeVerb)).IsTrue();
            simulation.Advance(SpecialCastTestKit.Neutral(tick),
                PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None, GameplayButtons.Jump));
            AssertThat(simulation.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            AssertThat(simulation.TryGetFighterVerb(1, out FighterVerbComponent afterVerb)).IsTrue();
            if (before.HitstunFrames > 0) launched = true;
            bool steppedInHitstun = before.HitstunFrames > 0 && after.HitstunFrames > 0
                && beforeVerb.HitstopFrames == 0 && afterVerb.HitstopFrames == 0
                && before.IsGrounded == 0 && after.IsGrounded == 0;
            if (!steppedInHitstun) continue;
            AssertThat(after.Velocity.y.RawValue)
                .OverrideFailureMessage($"Hitstun tick {tick}: the launch step was not StepLaunch.")
                .IsEqual(FighterAirPhysics.StepLaunch(before.Velocity.y).RawValue);
            checkedTicks++;
        }
        AssertThat(launched).OverrideFailureMessage("The launcher never put the victim in hitstun.").IsTrue();
        AssertThat(checkedTicks).OverrideFailureMessage("No airborne hitstun tick was stepped.").IsGreater(10);
    }

    /// <summary>
    /// A released air jump short-hops: with Jump released after the second press
    /// (Create-built), every rising tick of the air jump steps the ×2.5 band, and
    /// it peaks lower than the same air jump held.
    /// </summary>
    [TestCase]
    public void AReleasedAirJumpShortHops() {
        FighterLoadout loadout = Loadout("joan");
        AssertThat(loadout.MaxJumpCount).IsEqual(2);
        FP64 releasedPeak = AirJumpPeak(loadout, holdSecond: false, out int bandTicks);
        FP64 heldPeak = AirJumpPeak(loadout, holdSecond: true, out int heldBandTicks);
        AssertThat(releasedPeak < heldPeak)
            .OverrideFailureMessage($"A released air jump ({releasedPeak}) must peak below a held one ({heldPeak}).")
            .IsTrue();
        AssertThat(bandTicks).OverrideFailureMessage("The released air jump never stepped the short-hop band.").IsGreater(3);
        AssertThat(heldBandTicks).IsEqual(0);
    }

    /// <summary>
    /// Q5 ("own jump only, both modes"), Create-built: with Jump released, a
    /// rise that outlives the hitstun of a hit taken on the jump's own tick is
    /// not cut — the hit cleared the own-jump bit — and neither is the rest of
    /// Shakespeare's Tempest owner lift cast out of his own released short hop;
    /// while a released rise from the fighter's own jump is cut ×2.5 tick for
    /// tick.
    /// </summary>
    [TestCase]
    public void APostHitstunRiseAndTheTempestLiftAreNotCutButAReleasedOwnJumpIs() {
        // (a) The fighter's own jump, released after its press tick: ×2.5.
        FighterLoadout joan = Loadout("joan");
        var own = new FighterSimulation(joan, Loadout("einstein"), seed: 8103, rules: FighterMatchRules.Disabled);
        own.Advance(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Jump), SpecialCastTestKit.Neutral(0));
        int cut = 0;
        for (int tick = 1; tick < 60; tick++) {
            AssertThat(own.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
            own.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None,
                tick == 1 ? GameplayButtons.Jump : GameplayButtons.None), SpecialCastTestKit.Neutral(tick));
            AssertThat(own.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
            if (before.IsGrounded != 0 || after.IsGrounded != 0 || before.Velocity.y <= FP64.Zero) continue;
            AssertThat(after.Velocity.y.RawValue)
                .OverrideFailureMessage($"Own jump tick {tick}: a released rise must step the short hop.")
                .IsEqual(ShortHopStep(before.Velocity.y, before.RemainingJumps, joan.MaxJumpCount).RawValue);
            cut++;
        }
        AssertThat(cut).IsGreater(3);

        // (b) A hit on the jump's own tick: the post-hitstun rise is not cut.
        var hit = LauncherSimulation(hitstunFrames: 10, out int fireTick, seed: 8104);
        // The victim presses Jump on the fire tick (its movement runs before the
        // hit lands), so the launch catches its own-jump rise; then it releases.
        hit.Advance(SpecialCastTestKit.Neutral(fireTick), PlayerInputFrame.Create((uint)fireTick, 0f, 0f, GameplayButtons.Jump));
        AssertThat(hit.TryGetFighter(1, out FighterStateComponent struck)).IsTrue();
        AssertThat(struck.HitstunFrames > 0)
            .OverrideFailureMessage("The launcher missed the jumping victim.")
            .IsTrue();
        int freeRise = 0;
        for (int tick = fireTick + 1; tick < fireTick + 120; tick++) {
            AssertThat(hit.TryGetFighter(1, out FighterStateComponent before)).IsTrue();
            AssertThat(hit.TryGetFighterVerb(1, out FighterVerbComponent beforeVerb)).IsTrue();
            hit.Advance(SpecialCastTestKit.Neutral(tick), PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None,
                tick == fireTick + 1 ? GameplayButtons.Jump : GameplayButtons.None));
            AssertThat(hit.TryGetFighter(1, out FighterStateComponent after)).IsTrue();
            bool normalControlRise = before.HitstunFrames <= 0 && beforeVerb.HitstopFrames == 0
                && before.IsGrounded == 0 && after.IsGrounded == 0 && before.Velocity.y > FP64.Zero;
            if (!normalControlRise) continue;
            AssertThat(after.Velocity.y.RawValue)
                .OverrideFailureMessage($"Post-hitstun tick {tick}: a rise after a hit must not be cut.")
                .IsEqual(UncutStep(before.Velocity.y).RawValue);
            freeRise++;
        }
        AssertThat(freeRise).OverrideFailureMessage("The victim was not still rising after its hitstun.").IsGreater(3);

        // (c) Shakespeare's Tempest out of his own released short hop.
        FighterLoadout shakespeare = Loadout("shakespeare");
        var tempest = new FighterSimulation(shakespeare, Loadout("einstein"), seed: 8105, rules: FighterMatchRules.Disabled);
        tempest.Advance(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Jump), SpecialCastTestKit.Neutral(0));
        tempest.Advance(PlayerInputFrame.Create(1, 0f, 0f, GameplayButtons.Special2, GameplayButtons.Jump), SpecialCastTestKit.Neutral(1));
        AssertThat(tempest.TryGetFighterActionState(0, out FighterActionStateComponent cast)).IsTrue();
        AssertThat(FighterSpecialCastRules.IsCasting(in cast))
            .OverrideFailureMessage("The airborne Tempest press was not accepted.")
            .IsTrue();
        int lifted = 0;
        bool fired = false;
        for (int tick = 2; tick < 200; tick++) {
            AssertThat(tempest.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
            AssertThat(tempest.TryGetFighterActionState(0, out FighterActionStateComponent beforeCast)).IsTrue();
            tempest.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, GameplayButtons.None,
                tick == 2 ? GameplayButtons.Special2 : GameplayButtons.None), SpecialCastTestKit.Neutral(tick));
            AssertThat(tempest.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
            if (FighterSpecialCastRules.HasFlag(in beforeCast, FighterSpecialCastRules.FlagEffectFired)) fired = true;
            if (!fired || before.IsGrounded != 0 || after.IsGrounded != 0 || before.Velocity.y <= FP64.Zero) continue;
            bool halved = FighterSpecialCastRules.IsBusy(in beforeCast)
                && FighterSpecialCastRules.HasFlag(in beforeCast, FighterSpecialCastRules.FlagStartedAerial)
                && (FighterSpecialCastRules.IsStartup(in beforeCast) || FighterSpecialCastRules.IsActive(in beforeCast));
            if (halved) continue;
            AssertThat(after.Velocity.y.RawValue)
                .OverrideFailureMessage($"Tempest tick {tick}: the rest of the lift must not be cut.")
                .IsEqual(UncutStep(before.Velocity.y).RawValue);
            lifted++;
        }
        AssertThat(fired).OverrideFailureMessage("The Tempest never fired.").IsTrue();
        AssertThat(lifted).OverrideFailureMessage("The lift never rose under the normal step.").IsGreater(1);
    }

    // === helpers ===

    private static FighterLoadout Loadout(string characterID) =>
        FighterLoadoutFactory.FromCharacterData(
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{characterID}_data.tres"));

    /// <summary>
    /// The apex of a ground jump whose Jump is held for <paramref name="tapTicks"/>
    /// ticks (the press tick counted) and then released, Create-built.
    /// </summary>
    private static FP64 ApexOf(FighterLoadout loadout, int tapTicks) {
        var simulation = new FighterSimulation(loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 8100, rules: FighterMatchRules.Disabled);
        FP64 peak = FP64.Zero;
        GameplayButtons previous = GameplayButtons.None;
        for (int tick = 0; tick < MaxTicks; tick++) {
            GameplayButtons held = tick < tapTicks ? GameplayButtons.Jump : GameplayButtons.None;
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, held, previous), SpecialCastTestKit.Neutral(tick));
            previous = held;
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            if (fighter.Position.y > peak) peak = fighter.Position.y;
            if (tick > 0 && fighter.IsGrounded != 0) return peak;
        }
        AssertThat(false).OverrideFailureMessage("The jump never landed.").IsTrue();
        return peak;
    }

    /// <summary>Player one's height on every tick of a jump, until it lands (0 there).</summary>
    private static List<long> Trajectory(FighterLoadout loadout, Func<int, PlayerInputFrame> input) {
        var simulation = new FighterSimulation(loadout, FighterLoadout.Default(FighterCharacterID.Joan),
            seed: 8102, rules: FighterMatchRules.Disabled);
        var heights = new List<long>();
        for (int tick = 0; tick < MaxTicks; tick++) {
            simulation.Advance(input(tick), SpecialCastTestKit.Neutral(tick));
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent fighter)).IsTrue();
            if (tick > 0 && fighter.IsGrounded != 0) { heights.Add(0); break; }
            heights.Add(fighter.Position.y.RawValue);
        }
        return heights;
    }

    /// <summary>
    /// The peak of an air jump pressed at the first jump's apex: Jump held from
    /// the second press to the landing, or released on the tick after it.
    /// <paramref name="bandTicks"/> counts the second rise's ticks that stepped
    /// the short-hop band.
    /// </summary>
    private static FP64 AirJumpPeak(FighterLoadout loadout, bool holdSecond, out int bandTicks) {
        var simulation = new FighterSimulation(loadout, FighterLoadout.Default(FighterCharacterID.Einstein),
            seed: 8106, rules: FighterMatchRules.Disabled);
        bandTicks = 0;
        FP64 peak = FP64.Zero;
        GameplayButtons previous = GameplayButtons.None;
        int secondPress = -1;
        for (int tick = 0; tick < MaxTicks; tick++) {
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent before)).IsTrue();
            GameplayButtons held;
            if (secondPress < 0) {
                // The first jump is held to its apex, then released for a tick.
                bool falling = tick > 0 && before.Velocity.y <= FP64.Zero;
                held = tick == 0 || !falling ? GameplayButtons.Jump : GameplayButtons.None;
                if (falling && previous == GameplayButtons.None) {
                    held = GameplayButtons.Jump;
                    secondPress = tick;
                }
            } else {
                held = holdSecond ? GameplayButtons.Jump : GameplayButtons.None;
            }
            simulation.Advance(PlayerInputFrame.Create((uint)tick, 0f, 0f, held, previous), SpecialCastTestKit.Neutral(tick));
            previous = held;
            AssertThat(simulation.TryGetFighter(0, out FighterStateComponent after)).IsTrue();
            if (secondPress >= 0 && tick > secondPress && before.Velocity.y > FP64.Zero && after.IsGrounded == 0
                && after.Velocity.y.RawValue == ShortHopStep(before.Velocity.y, before.RemainingJumps, loadout.MaxJumpCount).RawValue) {
                bandTicks++;
            }
            if (secondPress >= 0 && after.Position.y > peak) peak = after.Position.y;
            if (tick > 0 && after.IsGrounded != 0) break;
        }
        AssertThat(secondPress >= 0).OverrideFailureMessage("The air jump was never pressed.").IsTrue();
        return peak;
    }

    /// <summary>The short-hop step: the band's ×2.5 of base gravity.</summary>
    private static FP64 ShortHopStep(FP64 vy, int remainingJumps, int maxJumps) =>
        FighterAirPhysics.StepMovement(vy, false, false,
            FighterAirPhysics.HasJumpedThisAirtime(remainingJumps, maxJumps),
            jumpHeld: false, riseFromOwnJump: true);

    /// <summary>A released rise that is not the fighter's own jump: no band, no hang.</summary>
    private static FP64 UncutStep(FP64 vy) =>
        FighterAirPhysics.StepMovement(vy, false, false, true, jumpHeld: false, riseFromOwnJump: false);

    /// <summary>Forces "own jump, Jump released" into component 323 (the worst case for the launch branch).</summary>
    private static void ForceReleasedOwnJump(FighterSimulation simulation, int playerID) {
        AssertThat(simulation.TryGetFighterActionState(playerID, out FighterActionStateComponent action)).IsTrue();
        action.ActionFlags |= FighterActionStateComponent.ActionFlagRiseFromOwnJump
            | FighterActionStateComponent.ActionFlagJumpReleasedSincePress;
        AssertThat(simulation.SeedFighterActionStateForTest(playerID, in action)).IsTrue();
    }

    /// <summary>
    /// A launcher at 1 unit: player one casts a launching melee Special 1 and is
    /// stepped to the tick before its active entry (<paramref name="fireTick"/>
    /// is that entry's tick, not yet advanced), against a default Joan.
    /// </summary>
    private static FighterSimulation LauncherSimulation(int hitstunFrames, out int fireTick, int seed) {
        var attacker = new CharacterData {
            CharacterID = "tesla",
            MaxHP = 400,
            Weight = 1f,
            MaxBlockCharges = 3,
            MaxJumpCount = 1,
            MaxMoveSpeed = 8f,
            MaxJumpForce = 13f,
            BasicAttackDamage = 6f,
            BasicAttackKnockback = 3f,
            SpecialAttackOne = new AbilityData {
                BaseDamage = 6f,
                KnockbackForce = new Godot.Vector2(4f, -34f),
                HitstunFrames = hitstunFrames,
                Launches = true
            },
            SpecialAttackTwo = new AbilityData(),
            MovementAbility = new MovementAbilityData(),
            UltimateAttack = new AbilityData { BaseDamage = 20f }
        };
        var simulation = new FighterSimulation(
            FighterLoadoutFactory.FromCharacterData(attacker),
            FighterLoadout.Default(FighterCharacterID.Joan),
            seed: seed, spawnDistance: 1, rules: FighterMatchRules.Disabled);
        simulation.Advance(new PlayerInputFrame { Tick = 0, Held = GameplayButtons.Special1, Pressed = GameplayButtons.Special1 },
            SpecialCastTestKit.Neutral(0));
        int tick = 1;
        while (!SpecialCastTestKit.FiresOnNextAdvance(simulation, 0)) {
            AssertThat(tick < 60).OverrideFailureMessage("The launcher never reached its active entry.").IsTrue();
            simulation.Advance(SpecialCastTestKit.Neutral(tick), SpecialCastTestKit.Neutral(tick));
            tick++;
        }
        fireTick = tick;
        return simulation;
    }
}
