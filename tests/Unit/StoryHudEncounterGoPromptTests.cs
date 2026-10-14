using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FIX-LEVEL (2026-10-13, Phase B review item 18): the Story HUD's GO prompt after
/// an encounter lock's clear. A lock with somewhere to go shows the arrow and
/// "GO"; a lock authored with <see cref="EncounterGoDirection.None"/> — Level 0's
/// Combat Trial, whose clear ends the tutorial — shows no prompt at all. It used
/// to flash a bare "GO" with no arrow over the tutorial's completion line and
/// results panel. Driven through <see cref="EventBus.OnEncounterLockChanged"/>,
/// the HUD's only input for the widget.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudEncounterGoPromptTests {

    [TestCase]
    public void ALockClearedWithNowhereToGoShowsNoPromptAndRemovesAStaleOne() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = "StoryHudGoPromptHost" };
        tree.Root.AddChild(host);
        try {
            StoryHUD hud = StoryHUD.CreateDefault();
            host.AddChild(hud);
            EventBus bus = EventBus.Instance;

            // A lock with a way on: the arrow and "GO" (the control).
            bus.RaiseEncounterLockChanged(Payload("fixlevel_lock_a", EncounterLockState.Engaged, 1, EncounterGoDirection.Right));
            bus.RaiseEncounterLockChanged(Payload("fixlevel_lock_a", EncounterLockState.Cleared, 0, EncounterGoDirection.Right));
            AssertThat(hud.EncounterGoPromptVisible).IsTrue();
            AssertThat(hud.EncounterGoArrow).IsEqual("→");

            // A lock with nowhere to go clears while that prompt is still up: no
            // bare "GO", and the stale prompt goes with it.
            bus.RaiseEncounterLockChanged(Payload("fixlevel_lock_b", EncounterLockState.Cleared, 0, EncounterGoDirection.None));
            AssertThat(hud.EncounterGoPromptVisible)
                .OverrideFailureMessage("A lock with no direction left the GO prompt up.").IsFalse();

            // ...and from a fight of its own: the counter goes and no prompt comes.
            bus.RaiseEncounterLockChanged(Payload("fixlevel_lock_b", EncounterLockState.Engaged, 2, EncounterGoDirection.None));
            AssertThat(hud.EncounterCounterVisible).IsTrue();
            bus.RaiseEncounterLockChanged(Payload("fixlevel_lock_b", EncounterLockState.Cleared, 0, EncounterGoDirection.None));
            AssertThat(hud.EncounterCounterVisible).IsFalse();
            AssertThat(hud.EncounterGoPromptVisible)
                .OverrideFailureMessage("A lock with no direction showed a bare GO prompt on its clear.").IsFalse();
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    private static EncounterLockPayload Payload(string lockID, EncounterLockState state, int remaining,
        EncounterGoDirection direction) => new() {
        LockID = lockID,
        State = state,
        Remaining = remaining,
        Total = 2,
        GoDirection = direction
    };
}
