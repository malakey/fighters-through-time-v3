using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A5 — the three V7.6 Level 0 beats that replaced the Special,
/// Ultimate and Movement-Ability calibrations: <b>Grab</b> (the dummy holds its
/// shield until the grab lands), <b>Hitstun Agency</b> (an extended tutorial
/// hitstop for the DI read, then a landing tech that repeats until it lands),
/// and <b>Meter &amp; Defy History</b> (the meter fills, then a scripted lethal
/// hit is refused and the F13 seal breaks).
///
/// <para>The defining rule is that the teaching launch is <b>free</b>: no HP,
/// no Rally echo, no meter, no rewind charge. That is why it does not travel
/// the hurtbox pipeline at all — there is nothing to apply — and why a missed
/// tech can repeat forever without punishing the player for learning.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TutorialCalibrationV76Tests {

    // === Grab Calibration ==============================================

    [TestCase]
    public void TheGrabStepAbsorbsSwingsAndOnlyAGrabThrowAdvancesIt() {
        TutorialCalibrationScript script =
            TutorialCalibrationTests.AdvanceToStep(TutorialCalibrationStep.Grab);

        // Swinging at a raised shield is absorbed and re-prompts; it never
        // advances, however long the player keeps hitting it.
        for (int swing = 0; swing < 5; swing++) {
            AssertThat(script.RegisterGrabLessonSwingAbsorbed()).IsTrue();
        }
        AssertThat(script.GrabLessonSwingsAbsorbed).IsEqual(5);
        AssertThat(script.Step)
            .OverrideFailureMessage("The shield persists until the grab lands.")
            .IsEqual(TutorialCalibrationStep.Grab);

        AssertThat(script.RegisterGrabThrow()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.HitstunDI);
        // Once past, further absorbs are inert.
        AssertThat(script.RegisterGrabLessonSwingAbsorbed()).IsFalse();
    }

    // === Hitstun Agency: the machine ====================================

    [TestCase]
    public void TheFreeLaunchRepeatsUntilOneTechLands() {
        TutorialCalibrationScript script =
            TutorialCalibrationTests.AdvanceToStep(TutorialCalibrationStep.LandingTech);
        AssertThat(script.ScriptedLaunchesDelivered).IsEqual(1);

        for (int miss = 0; miss < 4; miss++) {
            AssertThat(script.RegisterLandingTechMissed()).IsTrue();
            AssertThat(script.Step)
                .OverrideFailureMessage("A missed tech repeats the lesson rather than passing it.")
                .IsEqual(TutorialCalibrationStep.LandingTech);
        }
        AssertThat(script.LandingTechsMissed).IsEqual(4);
        AssertThat(script.ScriptedLaunchesDelivered)
            .OverrideFailureMessage("Every retry delivers another free launch.")
            .IsEqual(5);

        AssertThat(script.RegisterLandingTech()).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.MeterAndDefy);
    }

    [TestCase]
    public void TheDiBeatProceedsEvenWhenNoDirectionIsHeld() {
        TutorialCalibrationScript script =
            TutorialCalibrationTests.AdvanceToStep(TutorialCalibrationStep.HitstunDI);
        AssertThat(script.DirectionalInfluenceBeatSeen).IsFalse();

        // No input at all: the launch is unbent, and the beat still advances —
        // the lesson is that the option exists, not that it was taken.
        BasicComboRules.ResolveDirectionalInfluence(
            3.2f, -7.5f, 0f, 0f, out float x, out float y);
        AssertThat(Mathf.IsEqualApprox(x, 3.2f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(y, -7.5f)).IsTrue();

        AssertThat(script.RegisterDirectionalInfluenceBeat()).IsTrue();
        AssertThat(script.DirectionalInfluenceBeatSeen).IsTrue();
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.LandingTech);
    }

    [TestCase]
    public void AHeldDirectionBendsTheLaunchByTheAuthoredFifteenDegreesWithoutChangingItsForce() {
        var launch = new Vector2(
            Level00Controller.ScriptedLaunchKnockback.X,
            Level00Controller.ScriptedLaunchKnockback.Y);

        BasicComboRules.ResolveDirectionalInfluence(
            launch.X, launch.Y, 1f, 0f, out float rightX, out float rightY);
        var right = new Vector2(rightX, rightY);
        BasicComboRules.ResolveDirectionalInfluence(
            launch.X, launch.Y, -1f, 0f, out float leftX, out float leftY);
        var left = new Vector2(leftX, leftY);

        // DI never changes knockback magnitude, only direction. The rotation
        // runs on the quantized sin/cos pair the fixed-point sim shares, so the
        // magnitude is preserved to the table's precision rather than exactly.
        AssertThat(Mathf.Abs(right.Length() - launch.Length()) < 0.001f)
            .OverrideFailureMessage(
                $"DI changed the impulse from {launch.Length()} to {right.Length()}.")
            .IsTrue();
        AssertThat(Mathf.Abs(left.Length() - launch.Length()) < 0.001f).IsTrue();

        float rightBend = Mathf.RadToDeg(launch.AngleTo(right));
        float leftBend = Mathf.RadToDeg(launch.AngleTo(left));
        AssertThat(Mathf.Abs(Mathf.Abs(rightBend) - 15f) < 0.2f)
            .OverrideFailureMessage($"Full DI must bend 15°, measured {rightBend}°.")
            .IsTrue();
        AssertThat(Mathf.Abs(Mathf.Abs(leftBend) - 15f) < 0.2f)
            .OverrideFailureMessage($"Full DI must bend 15°, measured {leftBend}°.")
            .IsTrue();
        // Opposite inputs bend opposite ways.
        AssertThat(rightBend * leftBend < 0f).IsTrue();
    }

    [TestCase]
    public void TheTutorialHitstopIsLongerThanAnyHitstopCombatCanProduce() {
        // "The game holds the freeze a beat longer than normal (tutorial-only)."
        // The shared damage-scaled table tops out at HitstopCeilingFrames (the
        // curve's top plus the 2026-10-04 provisional launch bonus), so the
        // tutorial value has to clear it by a visible margin to give the player
        // time to read the prompt and commit a direction.
        AssertThat(Level00Controller.TutorialLaunchHitstopFrames > BasicComboRules.HitstopCeilingFrames)
            .OverrideFailureMessage(
                $"{Level00Controller.TutorialLaunchHitstopFrames} must exceed the combat maximum "
                + $"{BasicComboRules.HitstopCeilingFrames}.")
            .IsTrue();
        AssertThat(Level00Controller.TutorialLaunchHitstopFrames).IsEqual(36);
        // The first attempt's "slowed approach" is a second, shorter freeze.
        AssertThat(Level00Controller.FirstTechApproachFreezeFrames > 0).IsTrue();
        AssertThat(Level00Controller.FirstTechApproachFreezeFrames
            < Level00Controller.TutorialLaunchHitstopFrames).IsTrue();
    }

    // === Hitstun Agency: the real controller ============================

    [TestCase]
    public void TheScriptedLaunchCostsNoHpNoRallyEchoAndNoMeter() {
        PlayerController player = Spawn();
        try {
            int hpBefore = player.CurrentHP;
            float meterBefore = player.CurrentUltimateMeter;

            player.ApplyTutorialScriptedLaunch(
                Level00Controller.ScriptedLaunchKnockback,
                Level00Controller.ScriptedLaunchHitstunSeconds,
                Level00Controller.TutorialLaunchHitstopFrames);

            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("The teaching launch must never cost HP.")
                .IsEqual(hpBefore);
            AssertThat(player.EchoPool)
                .OverrideFailureMessage("The teaching launch must never enter Rally accounting.")
                .IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("The teaching launch must never earn meter.")
                .IsEqual(meterBefore);
            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("The teaching launch must never spend Defy History.")
                .IsFalse();

            // What it DOES do is the real verb layer.
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.IsInTumble).IsTrue();
            AssertThat(player.HasPendingLaunch)
                .OverrideFailureMessage("The impulse must be stashed so DI can bend it at hitstop end.")
                .IsTrue();
            AssertThat(player.HitstopFramesRemaining)
                .IsEqual(Level00Controller.TutorialLaunchHitstopFrames);
        } finally {
            Free(player);
        }
    }

    [TestCase]
    public void TheTumbleAndItsTechAreIndependentOfTheBlockChargePool() {
        // V7.3 ruling, relied on by the V7.6 lesson: the landing tech reads the
        // RAW Block input, so an exhausted shield — which cannot raise a stance
        // at all — still techs. The lesson must be clearable by a player who
        // just spent every charge in the block calibration.
        PlayerController player = Spawn();
        try {
            var block = player.GetNodeOrNull<BlockSystem>("BlockSystem");
            AssertObject(block)
                .OverrideFailureMessage("The factory must build a BlockSystem for this pin to mean anything.")
                .IsNotNull();
            block.DepleteCharges(block.CurrentCharges);
            AssertThat(block.CurrentCharges).IsEqual(0);
            AssertThat(block.CanRaiseStance)
                .OverrideFailureMessage("An empty shield must not raise the stance.")
                .IsFalse();

            player.ApplyTutorialScriptedLaunch(
                Level00Controller.ScriptedLaunchKnockback,
                Level00Controller.ScriptedLaunchHitstunSeconds,
                Level00Controller.TutorialLaunchHitstopFrames);

            AssertThat(player.IsInTumble)
                .OverrideFailureMessage(
                    "The techable tumble must not depend on shield charges — the tech reads raw input.")
                .IsTrue();
        } finally {
            Free(player);
        }
    }

    // === Meter & Defy History ==========================================

    [TestCase]
    public void TheDefyBeatSpendsExactlyOneUseAndLeavesTheSealBroken() {
        PlayerController player = Spawn();
        try {
            // The calibration's scripted meter grant: never a landed hit, so it
            // can never be mistaken for a Rally reclaim.
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("The seal starts lit, not spent.")
                .IsFalse();

            // The scripted lethal-tagged hit, through the V7.3 environmental
            // chokepoint the calibration uses.
            player.ApplyEnvironmentalDamage(player.CurrentHP);

            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("A full meter must refuse the lethal hit.")
                .IsTrue();
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("Defy History survives at 1 HP.")
                .IsEqual(1);
            AssertThat(player.CurrentUltimateMeter)
                .OverrideFailureMessage("The meter shatters to zero — that is what the broken seal means.")
                .IsEqual(0f);

            // V7.6 D04 (Package 11 A1b): the survivor is invulnerable through
            // the presentation and for 60 resumed control ticks, so the second
            // lethal hit has to wait that window out. Clearing it directly keeps
            // this case about the SEAL rather than about the timer, which
            // DefyProtectedRecoveryTests pins on its own.
            player.ClearDefyProtection();
            AssertThat(player.IsDefyProtected).IsFalse();

            // Refilling the meter does NOT restore the spent Defy: the next
            // lethal hit kills.
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
            player.ApplyEnvironmentalDamage(player.CurrentHP);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("A refilled meter must not resurrect a spent Defy History.")
                .IsEqual(0);
        } finally {
            Free(player);
        }
    }

    // === Helpers =======================================================

    private static PlayerController Spawn() {
        var tree = (SceneTree)Engine.GetMainLoop();
        // applyStoryProgression: false keeps an unrelated campaign save from
        // installing the Legacy Unlock gate under these cases.
        PlayerController player = CharacterFactory.CreateCharacter(
            "einstein", 0, applyStoryProgression: false);
        tree.Root.AddChild(player);
        return player;
    }

    private static void Free(PlayerController player) {
        if (player == null || !GodotObject.IsInstanceValid(player)) return;
        player.GetParent()?.RemoveChild(player);
        player.Free();
    }
}
