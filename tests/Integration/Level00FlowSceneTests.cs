using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

/// <summary>
/// Package 13 W3 (S02/S09/S10, D16): Level 0's new Part 1 and Part 2 on the real
/// scene. The hold is unlosable and spawns the hero's era locals when the voice
/// finishes; the beam break shows the ~2 s Eraser watcher and opens the Warden
/// rift; the Translation lands the hero in the bay, healed, for the arrival
/// dialogue; Wren's offer is the two-option prompt, and Full runs the
/// calibration.
///
/// <para>Driven through the controller's public beats and the EventBus, never
/// by stepping frames: a sync test never lets the deferred hold start run, and
/// dialogue completion is raised the way <c>DialogueManager</c> raises it. Every
/// case restores <c>SceneTree.Paused</c> and frees the level (and with it the
/// grouped <c>StoryPlayer</c>) in <c>finally</c>. No save slot is active, so
/// nothing here writes a save or advances the campaign.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level00FlowSceneTests {

    private const string ScenePath = "res://scenes/campaign/Level_00_Tutorial.tscn";

    [TestCase]
    public void TheHoldIsUnlosableSpawnsTheErasLocalsAndBreaksIntoTheWardenRift() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Hold);

            level.StartHoldFight();
            var player = level.GetNode<PlayerController>("Player");
            AssertThat(player.ScriptedHPFloor).IsEqual(Level00HoldFight.HeroHPFloor);

            // The Rally seam the hold uses every physics frame: a landed blow leaves
            // an echo, and the hold takes it back without healing.
            player.ApplyEnvironmentalDamage(10);
            int afterBlow = player.CurrentHP;
            player.ClearRallyEcho();
            AssertThat(player.EchoPool).IsEqual(0f);
            AssertThat(player.CurrentHP).IsEqual(afterBlow);

            // Unlosable: no blow can take the hero below 1 HP, so nothing dies,
            // no rewind charge is spent and Defy never has to fire.
            player.ApplyDamage(player.MaximumHP * 5);
            AssertThat(player.CurrentHP).IsEqual(1);
            AssertThat(player.CurrentState == CharacterState.Dead).IsFalse();

            // The voice finishes: the locals walk out of the tear.
            EventBus.Instance.RaiseDialogueComplete("level_00.hold_start");
            AssertThat(level.HoldStep).IsEqual(HoldFightStep.FightOff);
            int locals = 0;
            foreach (Node child in level.GetChildren()) {
                if (child is EnemyController enemy && enemy.IsInsideTree()) {
                    locals++;
                    AssertThat(enemy.IsSummoned)
                        .OverrideFailureMessage("Hold locals must draw no dust award.").IsTrue();
                }
            }
            AssertThat(locals).IsEqual(Level00HoldFight.LocalCount);

            for (int index = 0; index < Level00HoldFight.LocalsBeforeGuard; index++) {
                EventBus.Instance.RaiseEnemyKilled(new EnemyKilledPayload { EnemyID = "hold_local" });
            }
            AssertThat(level.HoldStep).IsEqual(HoldFightStep.Guard);

            // Beat 6: the beam breaks, the watcher stands at the tear, the rift opens.
            level.BreakTheBeam();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Rift);
            AssertObject(level.GetNodeOrNull<Node2D>("EraserWatcher"))
                .OverrideFailureMessage("S10: the Eraser watcher must appear as the beam breaks.").IsNotNull();
            AssertThat(Level00Controller.WatcherSeconds).IsEqual(2f);
            AssertObject(level.GetNodeOrNull<Area2D>("WardenRiftGate")).IsNotNull();
            AssertThat(player.ScriptedHPFloor)
                .OverrideFailureMessage("The HP floor must end with the hold.").IsEqual(0);
        } finally {
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    [TestCase]
    public void TheArrivalEndsOnWrensOfferAndFullCalibrationRunsTheLessons() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level00Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Level00Controller>();
            tree.Root.AddChild(level);
            var player = level.GetNode<PlayerController>("Player");

            level.BreakTheBeam();
            player.ApplyDamage(10);
            level.TranslateToBay();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Arrival);
            AssertThat(player.GlobalPosition).IsEqual(Level00Controller.BayArrivalPosition);
            AssertThat(player.CurrentHP)
                .OverrideFailureMessage("\"You're whole. Mostly.\" The Translation heals the hero.")
                .IsEqual(player.MaximumHP);
            AssertThat(level.GetNodeOrNull<Node2D>("ChronalFracture").Visible).IsFalse();

            // Arrival -> Wren's line -> the two-option prompt (D16).
            EventBus.Instance.RaiseDialogueComplete("level_00.intro");
            AssertObject(level.CalibrationPrompt)
                .OverrideFailureMessage("The prompt waits for Wren's offer to finish.").IsNull();
            EventBus.Instance.RaiseDialogueComplete("level_00.wren_offer");
            TwoOptionPrompt prompt = level.CalibrationPrompt;
            AssertObject(prompt).IsNotNull();
            AssertThat(prompt.IsOpen).IsTrue();
            AssertString(prompt.Modal.CancelButton.Text).IsEqual("level00_choice_full");
            AssertString(prompt.Modal.ConfirmButton.Text).IsEqual("level00_choice_skip");
            AssertThat(player.ProcessMode).IsEqual(Node.ProcessModeEnum.Disabled);

            prompt.ChooseFirst();
            AssertThat(level.Phase).IsEqual(Level00Controller.TutorialPhase.Calibration);
            AssertThat(player.ProcessMode).IsEqual(Node.ProcessModeEnum.Inherit);
            AssertObject(level.GetNodeOrNull<Node>("CalibrationDummy"))
                .OverrideFailureMessage("Full calibration starts with the dummy.").IsNotNull();
        } finally {
            FreeLevel(level);
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    private static void FreeLevel(Node level) {
        if (level == null || !GodotObject.IsInstanceValid(level)) return;
        level.GetParent()?.RemoveChild(level);
        level.Free();
    }
}
