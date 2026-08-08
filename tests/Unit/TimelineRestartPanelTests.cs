using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B1. The Timeline Collapse restart choice: themed, focus-authored,
/// and with its destructive option behind the shared confirmation.
///
/// The panel offers exactly two outcomes and they are one row apart: resume from
/// the Timeline Anchor the player reached, or throw that anchor away and restart
/// the level. It also authored no focus neighbours at all, on a screen that
/// accepts no other input — so a controller player's only route through it was
/// Godot's geometric fallback.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimelineRestartPanelTests {

    private static (Node host, TimelineRestartPanel panel) CreatePanel(string hostName) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = hostName };
        tree.Root.AddChild(host);
        var panel = new TimelineRestartPanel { Name = "TimelineRestartPanel" };
        host.AddChild(panel);
        return (host, panel);
    }

    [TestCase]
    public void ThePanelAdoptsTheSharedThemeAndAuthorsAFocusChain() {
        (Node host, TimelineRestartPanel panel) = CreatePanel("TimelineThemeHost");
        try {
            AssertObject(panel.Theme).IsNotNull();

            var anchor = panel.GetNodeOrNull<Button>("Panel/Layout/AnchorButton");
            var full = panel.GetNodeOrNull<Button>("Panel/Layout/FullRestartButton");
            var cancel = panel.GetNodeOrNull<Button>("Panel/Layout/CancelButton");
            AssertObject(anchor).IsNotNull();
            AssertObject(full).IsNotNull();
            AssertObject(cancel).IsNotNull();

            foreach (Button button in new[] { anchor, full, cancel }) {
                AssertThat(button.FocusMode).IsEqual(Control.FocusModeEnum.All);
            }
            // The chain wraps, so holding down past the last option returns to the
            // first rather than dead-ending on a screen with no other input.
            AssertThat(anchor.FocusNeighborBottom.IsEmpty).IsFalse();
            AssertThat(cancel.FocusNeighborBottom.IsEmpty).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ResumingFromTheAnchorIsImmediateBecauseNothingIsLost() {
        (Node host, TimelineRestartPanel panel) = CreatePanel("TimelineAnchorHost");
        try {
            bool? chosen = null;
            panel.RestartChosen += fromAnchor => chosen = fromAnchor;

            panel.GetNode<Button>("Panel/Layout/AnchorButton")
                .EmitSignal(BaseButton.SignalName.Pressed);

            AssertThat(chosen).IsEqual(true);
            AssertThat(panel.FullRestartConfirmation.IsOpen).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheFullRestartIsConfirmedBeforeTheAnchorIsDiscarded() {
        (Node host, TimelineRestartPanel panel) = CreatePanel("TimelineFullRestartHost");
        try {
            bool? chosen = null;
            panel.RestartChosen += fromAnchor => chosen = fromAnchor;

            panel.GetNode<Button>("Panel/Layout/FullRestartButton")
                .EmitSignal(BaseButton.SignalName.Pressed);

            // Pressing the button must not have acted yet.
            AssertThat(panel.FullRestartConfirmation.IsOpen).IsTrue();
            AssertThat(chosen).IsNull();

            // Cancel returns the player to the choice with nothing changed.
            panel.FullRestartConfirmation.Cancel();
            AssertThat(panel.FullRestartConfirmation.IsOpen).IsFalse();
            AssertThat(chosen).IsNull();

            panel.GetNode<Button>("Panel/Layout/FullRestartButton")
                .EmitSignal(BaseButton.SignalName.Pressed);
            panel.FullRestartConfirmation.Confirm();

            AssertThat(chosen).IsEqual(false);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheConfirmationSitsOutsideTheFocusChainItGuards() {
        (Node host, TimelineRestartPanel panel) = CreatePanel("TimelineConfirmFocusHost");
        try {
            ConfirmModal modal = panel.FullRestartConfirmation;
            AssertObject(modal).IsNotNull();
            AssertThat(modal.Visible).IsFalse();

            // The modal is a sibling of the button column, not a member of it: a
            // hidden confirm button must never become a focus stop on the panel.
            var layout = panel.GetNode<VBoxContainer>("Panel/Layout");
            AssertThat(modal.GetParent() == layout).IsFalse();
            AssertThat(FocusChainBuilder.Collect(layout).Count).IsEqual(3);
        } finally {
            host.Free();
        }
    }
}
