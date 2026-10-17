using System;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// User direction 2026-10-08 (ledger <c>USER-MOVEMENT-FEEL</c>), the Story half,
/// on a real <see cref="PlayerController"/>: the 6-frame run ramp and the
/// 4-frame stop, one ramp step a tick as in the sim (a re-press while sliding,
/// a landing with forward held, a turnaround from a standstill); the apex hang
/// on a jump — a ledge or rope jump-off included — and none on a walk-off, a
/// ledge drop or a relaunch after landing in hitstun; the 2.2 fall and its
/// 720 px/s terminal; the ramped fast-fall (it cuts a rise without snapping,
/// caps at 900 px/s, eases back to 720 on release, and carries through an
/// aerial swing and the air dodge); the launch states' unchanged gravity; and
/// the <see cref="StoryAirPhysics"/> arc helpers agreeing with the controller
/// stepped frame by frame.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryMovementFeelTests {

    private const GameplayButtons Jump = GameplayButtons.Jump;
    private const GameplayButtons Down = GameplayButtons.Down;
    private const GameplayButtons Attack = GameplayButtons.BasicAttack;
    private const GameplayButtons Roll = GameplayButtons.Roll;
    private const GameplayButtons None = GameplayButtons.None;

    /// <summary>
    /// Counted from the stick, as the Fighter sim counts: the push tick is the
    /// ramp's first frame (it turns Idle into Running and moves), full speed lands
    /// on the 6th, not before; the release tick is the stop's first frame (it
    /// turns Running into Idle and slows), and the hero stands still on the 4th.
    /// The 1.13x multiplier is an awkward speed whose steps do not divide evenly.
    /// </summary>
    [TestCase]
    public void TheRunReachesFullSpeedOnItsSixthFrameAndStopsInFour() {
        foreach ((string id, float multiplier) in new[] { ("einstein", 1f), ("lincoln", 1f), ("cleopatra", 1.13f) }) {
            StaticBody2D floor = null;
            PlayerController player = null;
            try {
                floor = CreateFlatFloor();
                player = CreateGroundedPlayer(id);
                player.StoryMoveSpeedMultiplier = multiplier;
                player.IsFacingRight = true;
                float max = player.Data.MaxMoveSpeed * multiplier * 60f;

                for (int frame = 1; frame <= UniversalMovementRules.RunAccelerationFrames; frame++) {
                    Step(player, 1f, None);
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
                    if (frame < UniversalMovementRules.RunAccelerationFrames) {
                        AssertThat(player.Velocity.X > 0f && player.Velocity.X < max - 0.5f)
                            .OverrideFailureMessage($"{id}: ramp frame {frame} is at {player.Velocity.X} of {max} px/s.").IsTrue();
                    }
                }
                AssertThat(player.Velocity.X)
                    .OverrideFailureMessage($"{id}: the run is not at full speed on ramp frame 6.")
                    .IsEqualApprox(max, 0.01f);

                for (int frame = 1; frame <= UniversalMovementRules.RunDecelerationFrames; frame++) {
                    Step(player, 0f, None);
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
                    if (frame < UniversalMovementRules.RunDecelerationFrames) {
                        AssertThat(player.Velocity.X > 0.5f)
                            .OverrideFailureMessage($"{id}: stopped early, on stop frame {frame}.").IsTrue();
                    }
                }
                AssertThat(player.Velocity.X)
                    .OverrideFailureMessage($"{id}: still moving after the 4-frame stop.")
                    .IsEqualApprox(0f, 0.01f);
            } finally {
                Release(player, floor);
            }
        }
    }

    /// <summary>
    /// One ramp step a tick, as in the sim: a stick released for one tick and
    /// pushed again goes full speed → 0.75 → 0.917 → full (the push tick is a
    /// single run step, not the Idle stop step and then a run step, which used
    /// to dip it to 0.667), and a landing with forward held at full air speed
    /// keeps full speed on its first grounded tick.
    /// </summary>
    [TestCase]
    public void APushAfterAOneTickReleaseAndALandingWithForwardHeldTakeOneRampStep() {
        StaticBody2D floor = null;
        PlayerController player = null;
        try {
            floor = CreateFlatFloor();
            player = CreateGroundedPlayer("einstein");
            player.IsFacingRight = true;
            float max = player.Data.MaxMoveSpeed * 60f;
            for (int frame = 0; frame < UniversalMovementRules.RunAccelerationFrames; frame++) Step(player, 1f, None);
            AssertThat(player.Velocity.X).IsEqualApprox(max, 0.01f);

            Step(player, 0f, None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.Velocity.X).IsEqualApprox(max * 0.75f, max * 0.001f);

            Step(player, 1f, None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
            AssertThat(player.Velocity.X)
                .OverrideFailureMessage("The re-press must take one run step from 0.75, not a stop step and a run step.")
                .IsEqualApprox(max * (0.75f + 1f / UniversalMovementRules.RunAccelerationFrames), max * 0.001f);
            Step(player, 1f, None);
            AssertThat(player.Velocity.X).IsEqualApprox(max, 0.01f);

            // Jump at full speed with forward held through the arc and the landing.
            Step(player, 1f, Jump);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            for (int frame = 0; frame < 200 && player.CurrentState != CharacterState.Idle; frame++) {
                Step(player, 1f, Jump, Jump);
            }
            AssertThat(player.CurrentState).OverrideFailureMessage("The jump never landed.").IsEqual(CharacterState.Idle);
            AssertThat(player.Velocity.X).IsEqualApprox(max, 0.01f);
            Step(player, 1f, Jump, Jump);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
            AssertThat(player.Velocity.X)
                .OverrideFailureMessage("A landing with forward held must keep full speed on its first grounded tick.")
                .IsEqualApprox(max, 0.01f);
        } finally {
            Release(player, floor);
        }
    }

    /// <summary>
    /// A push against the facing from a standstill turns on the push tick and
    /// starts the 6-frame ramp — full speed the other way on frame 6, with no
    /// skid — as the sim (which has no skid state) does. It used to cost the push
    /// tick, a Running tick and the 3-frame skid first, about 11 ticks.
    /// </summary>
    [TestCase]
    public void AStandstillTurnaroundFacesThePushAndReachesFullSpeedOnFrameSix() {
        foreach (string id in new[] { "einstein", "lincoln" }) {
            StaticBody2D floor = null;
            PlayerController player = null;
            try {
                floor = CreateFlatFloor();
                player = CreateGroundedPlayer(id);
                player.IsFacingRight = true;
                float max = player.Data.MaxMoveSpeed * 60f;
                for (int frame = 1; frame <= UniversalMovementRules.RunAccelerationFrames; frame++) {
                    Step(player, -1f, None);
                    AssertThat(player.CurrentState)
                        .OverrideFailureMessage($"{id}: frame {frame} of a standstill turn is {player.CurrentState}, not Running.")
                        .IsEqual(CharacterState.Running);
                    AssertThat(player.IsFacingRight)
                        .OverrideFailureMessage($"{id}: the push tick must turn the hero.").IsFalse();
                    if (frame < UniversalMovementRules.RunAccelerationFrames) {
                        AssertThat(player.Velocity.X < 0f && player.Velocity.X > -max + 0.5f)
                            .OverrideFailureMessage($"{id}: turn frame {frame} is at {player.Velocity.X} of {-max} px/s.").IsTrue();
                    }
                }
                AssertThat(player.Velocity.X)
                    .OverrideFailureMessage($"{id}: the turn is not at full speed on frame 6.")
                    .IsEqualApprox(-max, 0.01f);
            } finally {
                Release(player, floor);
            }
        }
    }

    /// <summary>
    /// A genuine reversal at full speed keeps the skid: the reversal tick is the
    /// skid's first stop frame, the skid bleeds the rest over the 4-frame stop
    /// ramp and turns, and the 6-frame run ramp rebuilds — full speed the other
    /// way on frame 10, as the sim's 4 + 6. A push with Down held from a
    /// standstill still only crouches.
    /// </summary>
    [TestCase]
    public void AReversalAtSpeedStillSkidsAndAPushWithDownHeldStillCrouches() {
        StaticBody2D floor = null;
        PlayerController player = null;
        try {
            floor = CreateFlatFloor();
            player = CreateGroundedPlayer("einstein");
            player.IsFacingRight = true;
            float max = player.Data.MaxMoveSpeed * 60f;
            for (int frame = 0; frame < UniversalMovementRules.RunAccelerationFrames; frame++) Step(player, 1f, None);
            AssertThat(player.Velocity.X).IsEqualApprox(max, 0.01f);

            int stop = UniversalMovementRules.RunDecelerationFrames;
            int total = stop + UniversalMovementRules.RunAccelerationFrames;
            for (int frame = 1; frame <= total; frame++) {
                Step(player, -1f, None);
                if (frame == 1) {
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Skidding);
                    AssertThat(player.Velocity.X).IsEqualApprox(max * 0.75f, max * 0.001f);
                }
                if (frame == stop) {
                    AssertThat(player.Velocity.X)
                        .OverrideFailureMessage("The skid must bleed full speed over the 4-frame stop ramp.")
                        .IsEqualApprox(0f, 0.01f);
                    AssertThat(player.IsFacingRight).IsFalse();
                }
                if (frame > stop && frame < total) {
                    AssertThat(player.Velocity.X < 0f && player.Velocity.X > -max + 0.5f)
                        .OverrideFailureMessage($"Reversal frame {frame} is at {player.Velocity.X} of {-max} px/s.").IsTrue();
                }
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
            AssertThat(player.Velocity.X)
                .OverrideFailureMessage("A full-speed reversal is not at full speed the other way on frame 10.")
                .IsEqualApprox(-max, 0.01f);

            for (int frame = 0; frame < UniversalMovementRules.RunDecelerationFrames + 2; frame++) Step(player, 0f, None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.Velocity.X).IsEqualApprox(0f, 0.01f);
            Step(player, 1f, Down);
            Step(player, 1f, Down, Down);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Crouching);
            AssertThat(player.Velocity.X).IsEqualApprox(0f, 0.01f);
        } finally {
            Release(player, floor);
        }
    }

    /// <summary>
    /// A held jump rises at 1x gravity, hangs at half gravity while its vertical
    /// speed is inside the 120 px/s band (either side of the apex), and falls at
    /// 2.2x below the terminal.
    /// </summary>
    [TestCase]
    public void AJumpHangsAtHalfGravityInsideTheBand() {
        StaticBody2D floor = null;
        PlayerController player = null;
        try {
            floor = CreateFlatFloor();
            player = CreateGroundedPlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            float band = StoryAirPhysics.ApexHangSpeedBandPixels;
            Step(player, 0f, Jump);
            AssertThat(player.Velocity.Y).IsEqualApprox(-StoryAirPhysics.JumpLaunchSpeed(player.Data.MaxJumpForce), 0.001f);
            AssertThat(player.JumpedThisAirtime).IsTrue();

            int hangTicks = 0, riseTicks = 0, fallTicks = 0;
            for (int frame = 0; frame < 300; frame++) {
                float before = player.Velocity.Y;
                Step(player, 0f, Jump, Jump);
                if (player.IsOnFloor()) break;
                float delta = player.Velocity.Y - before;
                if (Math.Abs(before) < band) {
                    AssertThat(delta).OverrideFailureMessage($"Hang tick at vy {before}.").IsEqualApprox(g * 0.5f, 0.001f);
                    hangTicks++;
                } else if (before < 0f) {
                    AssertThat(delta).OverrideFailureMessage($"Rise tick at vy {before}.").IsEqualApprox(g, 0.001f);
                    riseTicks++;
                } else if (player.Velocity.Y < StoryAirPhysics.TerminalFallSpeedPixels) {
                    AssertThat(delta).OverrideFailureMessage($"Fall tick at vy {before}.").IsEqualApprox(g * 2.2f, 0.001f);
                    fallTicks++;
                }
            }
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("The jump never landed.").IsTrue();
            // About 22 hang ticks at Einstein's weight: 240 px/s of band at half gravity.
            AssertThat(hangTicks).IsGreater(15);
            AssertThat(riseTicks).IsGreater(15);
            AssertThat(fallTicks).IsGreater(3);
        } finally {
            Release(player, floor);
        }
    }

    /// <summary>
    /// CM-MOVE (decisions 5.1 and 6.1, the Q5 ruling; 2026-10-16): a jump whose
    /// Jump is released on its second tick rises under the ×2.5 short hop and
    /// drops through its peak with no hang tick at all; and a rise that outlives
    /// a stun is not the hero's own jump any more — the stun cleared the latch —
    /// so the same released rise after it takes plain gravity, not the band.
    /// </summary>
    [TestCase]
    public void AReleasedHopHasNoHangAndARiseAfterAStunIsNotCut() {
        StaticBody2D floor = null;
        PlayerController player = null;
        try {
            floor = CreateFlatFloor();
            player = CreateGroundedPlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            float band = StoryAirPhysics.ApexHangSpeedBandPixels;
            Step(player, 0f, Jump);
            Step(player, 0f, Jump, Jump);
            AssertThat(player.RiseFromOwnJump).IsTrue();
            int hangTicks = 0, shortHopTicks = 0;
            GameplayButtons previous = Jump;
            for (int frame = 0; frame < 200; frame++) {
                float before = player.Velocity.Y;
                Step(player, 0f, None, previous);
                previous = None;
                if (player.IsOnFloor()) break;
                float delta = player.Velocity.Y - before;
                if (Math.Abs(before) < band && Math.Abs(delta - g * 0.5f) < 0.001f) hangTicks++;
                // ProcessAirborne steps gravity before CheckJumpInput refreshes
                // the Jump latch, so the release takes effect from the tick after.
                if (before < 0f && frame > 0) {
                    AssertThat(delta).OverrideFailureMessage($"Released rise tick at vy {before}.")
                        .IsEqualApprox(g * StoryAirPhysics.ShortHopGravityMultiplier, 0.001f);
                    shortHopTicks++;
                }
            }
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("The hop never landed.").IsTrue();
            AssertThat(hangTicks).OverrideFailureMessage("A released hop hung at its apex.").IsEqual(0);
            AssertThat(shortHopTicks).IsGreater(3);

            // A fresh jump, then a stun mid-rise: once the stun ends, the hero
            // is put back on a fast rise with Jump released. The stun cleared
            // the own-jump latch, so the band never cuts it.
            for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) Step(player, 0f, None);
            Step(player, 0f, Jump);
            AssertThat(player.RiseFromOwnJump).IsTrue();
            player.ApplyStun(4f / 60f);
            for (int frame = 0; frame < 30 && player.CurrentState == CharacterState.Stunned; frame++) {
                Step(player, 0f, None, frame == 0 ? Jump : None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            AssertThat(player.RiseFromOwnJump).OverrideFailureMessage("The stun left the own-jump latch set.").IsFalse();
            player.Velocity = new Vector2(0f, -500f);
            int freeTicks = 0;
            for (int frame = 0; frame < 30 && player.Velocity.Y < -band; frame++) {
                float before = player.Velocity.Y;
                Step(player, 0f, None);
                AssertThat(player.Velocity.Y - before)
                    .OverrideFailureMessage($"Post-stun rise tick at vy {before}: it must not be cut.")
                    .IsEqualApprox(g, 0.001f);
                freeTicks++;
            }
            AssertThat(freeTicks).IsGreater(5);
        } finally {
            Release(player, floor);
        }
    }

    /// <summary>
    /// A walk-off has spent no jump, so the same slow speeds fall at 2.2x with no
    /// hang, and every normal-control fall stops at the 720 px/s terminal.
    /// </summary>
    [TestCase]
    public void AWalkOffGetsNoHangAndFallsAtTwoPointTwoGravityToSevenTwenty() {
        PlayerController player = null;
        try {
            player = CreateAirbornePlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            AssertThat(player.RemainingJumps).IsEqual(player.Data.MaxJumpCount);
            AssertThat(player.JumpedThisAirtime).IsFalse();
            player.Velocity = Vector2.Zero;
            Step(player, 0f, None);
            AssertThat(player.Velocity.Y).IsEqualApprox(g, 0.001f);
            float before = player.Velocity.Y;
            Step(player, 0f, None);
            AssertThat(player.Velocity.Y - before)
                .OverrideFailureMessage("A walk-off inside the hang band must fall at 2.2x, not hang.")
                .IsEqualApprox(g * 2.2f, 0.001f);
            float fastest = 0f;
            for (int frame = 0; frame < 60; frame++) {
                Step(player, 0f, None);
                fastest = Mathf.Max(fastest, player.Velocity.Y);
            }
            AssertThat(fastest).IsEqual(StoryAirPhysics.TerminalFallSpeedPixels);
            AssertThat(player.Velocity.Y).IsEqual(720f);
        } finally {
            Release(player, null);
        }
    }

    /// <summary>
    /// The hang follows any jump, ledge and rope jump-offs included, and nothing
    /// else, without touching Story's jump budget: a ledge grab ends the airtime
    /// (a drop from it falls at 2.2x even after a jump spent before the catch),
    /// and a jump-off from a walk-off capture hangs at its apex as the sim's
    /// climb jump does. The grab refills no jump and the jump-off spends none.
    /// Swinging ropes reuse this ledge path.
    /// </summary>
    [TestCase]
    public void ALedgeJumpOffHangsAndALedgeDropDoesNotWithTheJumpBudgetUntouched() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        LedgeGrabPoint ledge = null;
        PlayerController player = null;
        try {
            ledge = ResourceLoader.Load<PackedScene>("res://scenes/templates/LedgeGrabPointTemplate.tscn")
                .Instantiate<LedgeGrabPoint>();
            ledge.Position = new Vector2(0f, -40000f);
            tree.Root.AddChild(ledge);
            player = CreateAirbornePlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            float band = StoryAirPhysics.ApexHangSpeedBandPixels;
            int maxJumps = player.Data.MaxJumpCount;

            // A jump spent on the way up to the ledge, then a Down drop.
            player.RemainingJumps = maxJumps - 1;
            player.JumpedThisAirtime = true;
            player.Velocity = new Vector2(0f, 30f);
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            AssertThat(player.JumpedThisAirtime).IsFalse();
            AssertThat(player.RemainingJumps)
                .OverrideFailureMessage("A Story ledge grab refills no jump.").IsEqual(maxJumps - 1);
            Step(player, 0f, Down);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            float before = player.Velocity.Y;
            AssertThat(before > 0f && before < band).IsTrue();
            Step(player, 0f, None);
            AssertThat(player.Velocity.Y - before)
                .OverrideFailureMessage("A ledge drop inside the hang band must fall at 2.2x, not hang.")
                .IsEqualApprox(g * 2.2f, 0.001f);

            // A walk-off capture (no jump spent), then the jump-off.
            player.RemainingJumps = maxJumps;
            AssertThat(player.TryGrabLedge(ledge)).IsTrue();
            Step(player, 0f, Jump);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
            AssertThat(player.Velocity.Y)
                .IsEqualApprox(-StoryAirPhysics.LedgeJumpLaunchSpeed(player.Data.MaxJumpForce), 0.001f);
            AssertThat(player.JumpedThisAirtime).IsTrue();
            AssertThat(player.RemainingJumps)
                .OverrideFailureMessage("A Story ledge jump-off spends no jump.").IsEqual(maxJumps);

            int hangTicks = 0;
            for (int frame = 0; frame < 200; frame++) {
                float vyBefore = player.Velocity.Y;
                if (vyBefore >= band) break;
                Step(player, 0f, Jump, Jump);
                if (Math.Abs(vyBefore) < band) {
                    AssertThat(player.Velocity.Y - vyBefore)
                        .OverrideFailureMessage($"A ledge jump-off must hang at its apex (vy {vyBefore}).")
                        .IsEqualApprox(g * 0.5f, 0.001f);
                    hangTicks++;
                }
            }
            // About 22 ticks at Einstein's weight: 240 px/s of band at half gravity.
            AssertThat(hangTicks).IsGreater(15);
        } finally {
            Release(player, null);
            if (ledge != null && GodotObject.IsInstanceValid(ledge)) ledge.Free();
        }
    }

    /// <summary>
    /// Floor contact in any state ends the airtime: a hero who jumps, is stunned,
    /// lands still in hitstun and is relaunched has not jumped in the new arc, so
    /// a slow fall after the hitstun takes the 2.2 fall, not the hang. (The sim
    /// refills on every ground contact; Story's count only refills on an Idle or
    /// Running tick, so the count alone used to carry the hang over.)
    /// </summary>
    [TestCase]
    public void LandingInHitstunEndsTheAirtimeSoARelaunchGetsNoHang() {
        StaticBody2D floor = null;
        PlayerController player = null;
        try {
            floor = CreateFlatFloor();
            player = CreateGroundedPlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            float band = StoryAirPhysics.ApexHangSpeedBandPixels;
            Step(player, 0f, Jump);
            AssertThat(player.JumpedThisAirtime).IsTrue();
            player.ApplyStun(5f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            for (int frame = 0; frame < 240 && !player.IsOnFloor(); frame++) Step(player, 0f, None);
            AssertThat(player.IsOnFloor()).OverrideFailureMessage("The stunned hero never landed.").IsTrue();
            Step(player, 0f, None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.JumpedThisAirtime)
                .OverrideFailureMessage("Landing in hitstun must end the airtime.").IsFalse();

            // Relaunch from the ground; leave the hitstun falling inside the band.
            player.Velocity = new Vector2(0f, -500f);
            bool inBand = false;
            for (int frame = 0; frame < 120; frame++) {
                Step(player, 0f, None);
                if (!player.IsOnFloor() && player.Velocity.Y > 0f && player.Velocity.Y < band) {
                    inBand = true;
                    break;
                }
            }
            AssertThat(inBand).OverrideFailureMessage("The relaunch never fell through the hang band.").IsTrue();
            player.TransitionTo(CharacterState.Airborne);
            float before = player.Velocity.Y;
            Step(player, 0f, None);
            AssertThat(player.Velocity.Y - before)
                .OverrideFailureMessage("A relaunch after landing in hitstun must fall at 2.2x, not hang.")
                .IsEqualApprox(g * 2.2f, 0.001f);
        } finally {
            Release(player, floor);
        }
    }

    /// <summary>
    /// Down held mid-rise adds 3x gravity per tick: the rise is cut short, never
    /// snapped. The dive caps at 900 px/s, and releasing Down eases it back by one
    /// base-gravity step per tick to the 720 px/s terminal.
    /// </summary>
    [TestCase]
    public void FastFallCutsARiseWithoutSnappingCapsAtNineHundredAndEasesBackOnRelease() {
        PlayerController player = null;
        try {
            player = CreateAirbornePlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            // As after a jump: fast-fall wins over the hang either way.
            player.RemainingJumps = player.Data.MaxJumpCount - 1;
            player.JumpedThisAirtime = true;
            player.Velocity = new Vector2(0f, -500f);
            // Held from the previous frame too, so neither button reads as a press.
            Step(player, 0f, Jump | Down, Jump | Down);
            AssertThat(player.Velocity.Y)
                .OverrideFailureMessage("The first fast-fall tick must add 3x gravity, not snap.")
                .IsEqualApprox(-500f + g * 3f, 0.001f);
            AssertThat(player.Velocity.Y < 0f).IsTrue();

            int riseTicks = 1;
            while (player.Velocity.Y < 0f && riseTicks < 120) {
                Step(player, 0f, Jump | Down, Jump | Down);
                riseTicks++;
            }
            // A third of the unaided rise (about 500 / g ticks plus the hang): every
            // tick of it ran at 3x gravity.
            AssertThat(riseTicks).IsLessEqual((int)Math.Ceiling(500f / (g * 3f)) + 1);

            for (int frame = 0; frame < 90; frame++) Step(player, 0f, Jump | Down, Jump | Down);
            AssertThat(player.Velocity.Y).IsEqual(StoryAirPhysics.FastFallSpeedPixels);
            AssertThat(player.Velocity.Y).IsEqual(900f);

            Step(player, 0f, Jump, Jump | Down);
            AssertThat(player.Velocity.Y)
                .OverrideFailureMessage("Releasing fast-fall must ease the dive, not snap it to the terminal.")
                .IsEqualApprox(900f - g, 0.001f);
            for (int frame = 0; frame < 30; frame++) {
                float previous = player.Velocity.Y;
                Step(player, 0f, Jump, Jump);
                AssertThat(player.Velocity.Y <= previous && player.Velocity.Y >= 720f).IsTrue();
            }
            AssertThat(player.Velocity.Y).IsEqual(StoryAirPhysics.TerminalFallSpeedPixels);
        } finally {
            Release(player, null);
        }
    }

    /// <summary>
    /// Fast-fall is any time in the air under normal control, in both modes: held
    /// Down keeps a carried 900 px/s dive through a down-air (the falling strike
    /// the design asks for) instead of braking it to 720, and ramps a slow fall at
    /// 3x through an aerial swing and through the air dodge, as the sim does.
    /// </summary>
    [TestCase]
    public void HeldDownFastFallsThroughTheDownAirAndTheAirDodge() {
        foreach (bool dive in new[] { true, false }) {
            PlayerController player = null;
            try {
                player = CreateAirbornePlayer("einstein");
                float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
                float start = dive ? StoryAirPhysics.FastFallSpeedPixels : 100f;
                player.Velocity = new Vector2(0f, start);
                Step(player, 0f, Down | Attack, Down);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                for (int frame = 0; frame < 6; frame++) {
                    float before = player.Velocity.Y;
                    Step(player, 0f, Down, Down);
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                    float expected = Math.Min(before + g * 3f, StoryAirPhysics.FastFallSpeedPixels);
                    AssertThat(player.Velocity.Y)
                        .OverrideFailureMessage($"A Down-held down-air at vy {before} must fast-fall, not brake or fall at 2.2x.")
                        .IsEqualApprox(expected, 0.001f);
                }
            } finally {
                Release(player, null);
            }
        }

        PlayerController dodger = null;
        try {
            dodger = CreateAirbornePlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(dodger.Data.Weight);
            dodger.Velocity = new Vector2(0f, 100f);
            Step(dodger, 0f, Down | Roll, Down);
            AssertThat(dodger.IsAirDodging).IsTrue();
            for (int frame = 0; frame < 4; frame++) {
                float before = dodger.Velocity.Y;
                Step(dodger, 0f, Down, Down);
                AssertThat(dodger.IsAirDodging).IsTrue();
                AssertThat(dodger.Velocity.Y - before)
                    .OverrideFailureMessage($"A Down-held air dodge at vy {before} must fast-fall.")
                    .IsEqualApprox(g * 3f, 0.001f);
            }
        } finally {
            Release(dodger, null);
        }
    }

    /// <summary>
    /// Hitstun and the guard-break daze keep the pre-2026-10-08 Story gravity: the
    /// 1.8 fall and a hard 600 px/s clamp, with no hang even after a jump, so
    /// launch arcs, DI and tech timing do not move.
    /// </summary>
    [TestCase]
    public void HitstunAndTheDazeKeepTheOldLaunchGravity() {
        PlayerController player = null;
        try {
            player = CreateAirbornePlayer("einstein");
            float g = StoryAirPhysics.GravityPerFrame(player.Data.Weight);
            player.RemainingJumps = player.Data.MaxJumpCount - 1;
            player.JumpedThisAirtime = true;
            player.ApplyStun(2f);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            player.Velocity = new Vector2(0f, 30f);
            Step(player, 0f, None);
            AssertThat(player.Velocity.Y - 30f)
                .OverrideFailureMessage("Hitstun falls at the old 1.8, not the hang or the 2.2 fall.")
                .IsEqualApprox(g * 1.8f, 0.001f);
            for (int frame = 0; frame < 60; frame++) Step(player, 0f, None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.Velocity.Y).IsEqual(StoryAirPhysics.LaunchTerminalFallSpeedPixels);
            AssertThat(player.Velocity.Y).IsEqual(600f);

            // A launch faster than the terminal clamps at once, as it always has.
            player.Velocity = new Vector2(0f, 1000f);
            Step(player, 0f, None);
            AssertThat(player.Velocity.Y).IsEqual(600f);

            player.TransitionTo(CharacterState.Dazed);
            player.Velocity = new Vector2(0f, 30f);
            Step(player, 0f, None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Dazed);
            AssertThat(player.Velocity.Y - 30f).IsEqualApprox(g * 1.8f, 0.001f);
        } finally {
            Release(player, null);
        }
    }

    /// <summary>
    /// The rulebook's integrated arc is the controller's arc: a held single jump
    /// for every hero, and the best double jump (the second press on the tick its
    /// rise would stop) for the lightest and the heaviest, rise and air time alike.
    /// </summary>
    [TestCase]
    public void TheArcHelpersAgreeWithTheControllerSteppedFrameByFrame() {
        foreach (string id in CharacterRoster.IDs) {
            StaticBody2D floor = null;
            PlayerController player = null;
            try {
                floor = CreateFlatFloor();
                player = CreateGroundedPlayer(id);
                float launch = StoryAirPhysics.JumpLaunchSpeed(player.Data.MaxJumpForce);
                StoryJumpArc arc = StoryAirPhysics.SimulateJump(launch, player.Data.Weight);
                (float rise, int airtime) = JumpAndMeasure(player, doubleJump: false);
                AssertThat(rise).OverrideFailureMessage($"{id}: single-jump rise.").IsEqualApprox(arc.Rise, 0.5f);
                AssertThat(airtime).OverrideFailureMessage($"{id}: single-jump air time (helper {arc.AirtimeFrames}).")
                    .IsBetween(arc.AirtimeFrames - 1, arc.AirtimeFrames + 1);
            } finally {
                Release(player, floor);
            }
        }
        foreach (string id in new[] { "cleopatra", "lincoln" }) {
            StaticBody2D floor = null;
            PlayerController player = null;
            try {
                floor = CreateFlatFloor();
                player = CreateGroundedPlayer(id);
                float launch = StoryAirPhysics.JumpLaunchSpeed(player.Data.MaxJumpForce);
                StoryJumpArc arc = StoryAirPhysics.SimulateJump(launch, player.Data.Weight, 1f, 2);
                (float rise, int airtime) = JumpAndMeasure(player, doubleJump: true);
                AssertThat(rise).OverrideFailureMessage($"{id}: double-jump rise.").IsEqualApprox(arc.Rise, 0.5f);
                AssertThat(airtime).OverrideFailureMessage($"{id}: double-jump air time (helper {arc.AirtimeFrames}).")
                    .IsBetween(arc.AirtimeFrames - 1, arc.AirtimeFrames + 1);
            } finally {
                Release(player, floor);
            }
        }
    }

    /// <summary>
    /// Jumps from the floor with Jump held and returns the rise and the air time
    /// (the jump tick and the landing tick included). For a double jump, Jump is
    /// released on the tick before the rulebook's press tick and pressed on it.
    /// </summary>
    private static (float Rise, int Airtime) JumpAndMeasure(PlayerController player, bool doubleJump) {
        float startY = player.GlobalPosition.Y;
        float minY = startY;
        Step(player, 0f, Jump);
        int ticks = 1;
        minY = Mathf.Min(minY, player.GlobalPosition.Y);
        bool released = false, pressedSecond = false;
        GameplayButtons previous = Jump;
        while (!player.IsOnFloor() && ticks < 400) {
            GameplayButtons held = Jump;
            if (doubleJump && !pressedSecond) {
                if (released) {
                    pressedSecond = true;
                } else {
                    float weight = player.Data.Weight;
                    bool jumped = player.JumpedThisAirtime;
                    float a = StoryAirPhysics.StepVertical(player.Velocity.Y, weight, 1f, 1f / 60f, false, false, true, jumped, false);
                    float b = StoryAirPhysics.StepVertical(a, weight, 1f, 1f / 60f, false, false, true, jumped, false);
                    if (a < 0f && b >= 0f) {
                        released = true;
                        held = None;
                    }
                }
            }
            Step(player, 0f, held, previous);
            previous = held;
            ticks++;
            minY = Mathf.Min(minY, player.GlobalPosition.Y);
        }
        if (doubleJump) AssertThat(pressedSecond).OverrideFailureMessage("The second jump was never pressed.").IsTrue();
        return (startY - minY, ticks);
    }

    private static void Step(PlayerController player, float moveX, GameplayButtons held,
        GameplayButtons previousHeld = GameplayButtons.None) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, moveX, 0f, held, previousHeld));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    /// <summary>Frees whatever a case created; null-safe, so a failed setup still cleans up.</summary>
    private static void Release(PlayerController player, StaticBody2D floor) {
        if (player != null && GodotObject.IsInstanceValid(player)) {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
        if (floor != null && GodotObject.IsInstanceValid(floor)) floor.Free();
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "StoryMovementFeelFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>
    /// A hero standing still on the floor in Idle. The grouped player leaves the
    /// tree again if the setup itself fails, so a failure never leaks it into
    /// later suites (AGENTS.md: grouped nodes are freed in a finally).
    /// </summary>
    private static PlayerController CreateGroundedPlayer(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -8f);
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Step(player, 0f, None);
            }
            for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
                Step(player, 0f, None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            player.Velocity = Vector2.Zero;
            return player;
        } catch {
            Release(player, null);
            throw;
        }
    }

    /// <summary>A hero high in open air that has never touched a floor (no stale floor contact).</summary>
    private static PlayerController CreateAirbornePlayer(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        try {
            player.GlobalPosition = new Vector2(0f, -40000f);
            player.TransitionTo(CharacterState.Airborne);
            return player;
        } catch {
            Release(player, null);
            throw;
        }
    }
}
