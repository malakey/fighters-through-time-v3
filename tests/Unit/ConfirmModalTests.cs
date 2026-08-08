using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 A1. The shared confirmation modal.
///
/// The behaviour worth pinning is the focus trap. The ad-hoc confirmations this
/// replaced left focus wherever it was, so a controller player could move off the
/// modal onto the menu behind it and trigger the very action the modal existed to
/// guard. Cancel taking initial focus is also deliberate and tested: these guard
/// destructive actions, so a mashed confirm button must land on the safe option.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConfirmModalTests {

    [TestCase]
    public void OpeningShowsTheModalAndPutsFocusOnTheSafeOption() {
        var host = MakeHost();
        try {
            ConfirmModal modal = AddModal(host);

            AssertThat(modal.IsOpen).IsFalse();
            AssertThat(modal.Visible).IsFalse();

            modal.Open();

            AssertThat(modal.IsOpen).IsTrue();
            AssertThat(modal.Visible).IsTrue();
            AssertObject(host.GetViewport().GuiGetFocusOwner())
                .OverrideFailureMessage("Cancel must take initial focus on a destructive confirmation.")
                .IsSame(modal.CancelButton);
        } finally {
            host.QueueFree();
        }
    }

    [TestCase]
    public void ConfirmingRaisesConfirmedExactlyOnceAndCloses() {
        var host = MakeHost();
        try {
            ConfirmModal modal = AddModal(host);
            int confirmed = 0;
            int cancelled = 0;
            modal.Confirmed += () => confirmed++;
            modal.Cancelled += () => cancelled++;

            modal.Open();
            modal.Confirm();

            AssertThat(confirmed).IsEqual(1);
            AssertThat(cancelled).IsEqual(0);
            AssertThat(modal.IsOpen).IsFalse();
            AssertThat(modal.Visible).IsFalse();

            // A second call on a closed modal must not fire the action again.
            modal.Confirm();
            AssertThat(confirmed).IsEqual(1);
        } finally {
            host.QueueFree();
        }
    }

    [TestCase]
    public void CancellingRaisesCancelledAndLeavesTheActionUntaken() {
        var host = MakeHost();
        try {
            ConfirmModal modal = AddModal(host);
            int confirmed = 0;
            int cancelled = 0;
            modal.Confirmed += () => confirmed++;
            modal.Cancelled += () => cancelled++;

            modal.Open();
            modal.Cancel();

            AssertThat(cancelled).IsEqual(1);
            AssertThat(confirmed).IsEqual(0);
            AssertThat(modal.IsOpen).IsFalse();
        } finally {
            host.QueueFree();
        }
    }

    [TestCase]
    public void ClosingHandsFocusBackToWhateverHadItBefore() {
        var host = MakeHost();
        try {
            var opener = new Button { Name = "Opener", Text = "open" };
            host.AddChild(opener);
            opener.GrabFocus();
            AssertObject(host.GetViewport().GuiGetFocusOwner()).IsSame(opener);

            ConfirmModal modal = AddModal(host);
            modal.Open();
            AssertObject(host.GetViewport().GuiGetFocusOwner()).IsSame(modal.CancelButton);

            modal.Cancel();

            // Dismissing a confirmation must return the player exactly where they
            // were, not drop focus onto nothing.
            AssertObject(host.GetViewport().GuiGetFocusOwner()).IsSame(opener);
        } finally {
            host.QueueFree();
        }
    }

    [TestCase]
    public void ThePromptCopyIsStoredAsATranslationKey() {
        var host = MakeHost();
        try {
            ConfirmModal modal = AddModal(host);
            var prompt = modal.FindChild("Prompt", recursive: true, owned: false) as Label;
            AssertObject(prompt).IsNotNull();

            // Raw keys are stored and resolved by Godot's control translation, so a
            // language change follows without rebuilding the modal.
            AssertThat(prompt.Text).IsEqual("pause_quit_confirm");

            modal.SetPromptKey("fighter_pause_exit_confirm");
            AssertThat(prompt.Text).IsEqual("fighter_pause_exit_confirm");
        } finally {
            host.QueueFree();
        }
    }

    private static ConfirmModal AddModal(Control host) {
        ConfirmModal modal = ConfirmModal.Create("pause_quit_confirm");
        host.AddChild(modal);
        return modal;
    }

    private static Control MakeHost() {
        var host = new Control { Name = "ModalHost" };
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(host);
        return host;
    }
}
