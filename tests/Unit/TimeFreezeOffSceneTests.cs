using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay review 2026-11-03 (decision 10b++, plan §1.3), the engine half. With <see cref="TimeFreezeRules.Enabled"/> off the
/// controller node, its input, the HUD indicator and the first-use tooltip are dormant, while the statics, the freeze sweeps
/// and the thaw cue the Post-Landing Hold shares stay live. The engine-free half is <c>TimeFreezeOffRulesTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimeFreezeOffSceneTests {
    private const double Step = 1.0 / 60.0;

    [TestCase]
    public void TheBootstrapperBuildsNoControllerAndOffersNoTooltipWhileTheVerbIsOff() {
        bool? previous = TimeFreezeRules.EnabledOverrideForTesting;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var off = new Node { Name = "TimeFreezeOffBootstrapHost" };
        var on = new Node { Name = "TimeFreezeOnBootstrapHost" };
        tree.Root.AddChild(off);
        tree.Root.AddChild(on);
        try {
            TimeFreezeRules.EnabledOverrideForTesting = false;
            StorySceneServices dormant = StorySceneBootstrapper.Attach(off, "", includeHUD: false, includeRewind: true);
            AssertObject(dormant.TimeFreeze).OverrideFailureMessage("No controller while the verb is off.").IsNull();
            AssertObject(off.FindChild("TimeFreezeController", true, false)).IsNull();
            AssertObject(dormant.RewindManager).OverrideFailureMessage("The rewind stack is untouched.").IsNotNull();
            AssertObject(dormant.Tooltips).IsNotNull();
            AssertThat(dormant.Tooltips.SceneHasTimeFreeze).IsFalse();
            AssertThat(dormant.Tooltips.PendingCount).IsEqual(0);
            AssertThat(dormant.Tooltips.ShowingTooltipID).IsEqual("");

            // The seam turns the legacy stack back on, so the existing suites can still reach it.
            TimeFreezeRules.EnabledOverrideForTesting = true;
            StorySceneServices live = StorySceneBootstrapper.Attach(on, "", includeHUD: false, includeRewind: true);
            AssertObject(live.TimeFreeze).IsNotNull();
            AssertThat(live.Tooltips.SceneHasTimeFreeze).IsTrue();
        } finally {
            TimeFreezeRules.EnabledOverrideForTesting = previous;
            off.Free();
            on.Free();
        }
    }

    [TestCase]
    public void TheActionStaysDefinedWithNoEventsAndLeavesBothBindingLists() {
        AssertThat(InputMap.HasAction(InputManager.Actions.TimeFreeze))
            .OverrideFailureMessage("A lookup by name must never throw, so the action stays in project.godot.")
            .IsTrue();
        var events = InputMap.ActionGetEvents(InputManager.Actions.TimeFreeze);
        using (events.AsDisposable()) AssertThat(events.Count).IsEqual(0);

        AssertThat(InputManager.RemappableActions.Contains(InputManager.Actions.TimeFreeze)).IsFalse();
        AssertThat(InputShortcuts.DirectActions.Contains(InputShortcuts.TimeFreezeAction)).IsFalse();
        AssertThat(InputBindingService.BindableActions().Contains(InputManager.Actions.TimeFreeze))
            .OverrideFailureMessage("It is no Controls row and not in the conflict set.")
            .IsFalse();
        // No new orphan key: the label stays resolvable for a re-enable.
        AssertThat(InputManager.ActionLabelKey(InputManager.Actions.TimeFreeze)).IsEqual("controls_action_time_freeze");
    }

    [TestCase]
    public async Task TheStoryHudHidesTheIndicatorKeepsItsSlabAt484AndShowsItAgainWhenForcedOn() {
        bool? previous = TimeFreezeRules.EnabledOverrideForTesting;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var port = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
        tree.Root.AddChild(port);
        try {
            TimeFreezeRules.EnabledOverrideForTesting = false;
            StoryHUD hud = StoryHUD.CreateDefault();
            port.AddChild(hud);
            for (int i = 0; i < 4; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var indicator = (Control)hud.FindChild("TimeFreezeIndicator", true, false);
            AssertObject(indicator).OverrideFailureMessage("The node stays in the scene.").IsNotNull();
            AssertThat(indicator.Visible).IsFalse();
            // A payload (the UI capture tool still raises them) changes nothing visible.
            EventBus.Instance.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 3f });
            hud._Process(0.016);
            AssertThat(indicator.Visible).IsFalse();

            var slab = (Control)hud.FindChild("HudPanel", true, false);
            AssertObject(slab).IsNotNull();
            Rect2 rect = slab.GetGlobalTransform() * new Rect2(Vector2.Zero, slab.Size);
            AssertFloat(rect.Size.X).OverrideFailureMessage($"The slab is {rect.Size.X} wide, not 484.").IsEqualApprox(484f, 4f);

            TimeFreezeRules.EnabledOverrideForTesting = true;
            hud._Process(0.016);
            AssertThat(indicator.Visible).IsTrue();
        } finally {
            TimeFreezeRules.EnabledOverrideForTesting = previous;
            port.Free();
        }
    }

    [TestCase]
    public void ThePostLandingHoldAndTheFreezeSweepsStayLiveWithNoControllerInTheTree() {
        bool? previous = TimeFreezeRules.EnabledOverrideForTesting;
        TimeFreezeRules.EnabledOverrideForTesting = false;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "TimeFreezeOffHoldHost" };
        tree.Root.AddChild(host);
        try {
            // The shared statics the hold reads are still there.
            AssertThat(TimeFreezeController.FrozenTimeGroups.Count() > 0).IsTrue();
            AssertThat(TimeFreezeController.FreezeSeconds > 0f && TimeFreezeController.CooldownSeconds > 0f).IsTrue();
            AssertThat(string.IsNullOrEmpty(TimeFreezeController.ThawWarningLabelKey)).IsFalse();
            foreach (string group in TimeFreezeController.FrozenTimeGroups) {
                AssertThat(ChronalRewindManager.PostLandingHoldGroups.Contains(group)).IsTrue();
            }

            StoryManager.Instance?.SetRewinds(3);
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
            host.AddChild(player);
            player.GlobalPosition = new Vector2(400f, 850f);
            var manager = new ChronalRewindManager { Name = "OffHoldManager" };
            host.AddChild(manager);
            var hazard = new HoldFreezeProbe { Name = "OffHoldHazard" };
            host.AddChild(hazard);
            hazard.AddToGroup("story_hazard");
            AssertThat(tree.GetFirstNodeInGroup(TimeFreezeController.ControllerGroup) == null)
                .OverrideFailureMessage("No controller is in the tree.").IsTrue();

            for (int frame = 0; frame < 30; frame++) {
                player.GlobalPosition = new Vector2(400f + frame * 4f, 850f);
                manager._PhysicsProcess(Step);
            }
            player.ApplyDamage(player.CurrentHP);
            AssertThat(manager.IsRewinding).IsTrue();
            for (int i = 0; i < 900 && manager.IsRewinding; i++) manager._PhysicsProcess(Step);
            AssertThat(manager.IsPostLandingHoldActive).OverrideFailureMessage("The hold still runs.").IsTrue();
            AssertThat(hazard.IsFrozen).OverrideFailureMessage("The hold still sweeps the Time Freeze groups.").IsTrue();
            for (int i = 0; i < ChronalRewindManager.PostLandingHoldFrames; i++) manager._PhysicsProcess(Step);
            AssertThat(manager.IsPostLandingHoldActive).IsFalse();
            AssertThat(hazard.IsFrozen).OverrideFailureMessage("The thaw releases it.").IsFalse();
        } finally {
            TimeFreezeRules.EnabledOverrideForTesting = previous;
            if (GodotObject.IsInstanceValid(host)) host.Free();
            StoryManager.Instance?.SetRewinds(3);
        }
    }
}
