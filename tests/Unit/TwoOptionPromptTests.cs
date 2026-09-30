using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 13 W3 (D16): the reusable two-option prompt on the shared
/// <see cref="ConfirmModal"/> idiom. The first option is the left button and
/// takes initial focus; Back selects it; each prompt resolves exactly once; text
/// is stored as raw keys; it never writes the tree's pause. The prompt is freed
/// and the pause restored in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TwoOptionPromptTests {

    [TestCase]
    public void TheFirstOptionIsTheSafeDefaultAndEachPromptResolvesOnce() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        TwoOptionPrompt prompt = null;
        try {
            prompt = TwoOptionPrompt.Create("dlg_l00_wren_offer_1", "level00_choice_full", "level00_choice_skip",
                "speaker_wren");
            int chosen = -1;
            int calls = 0;
            prompt.Chosen += index => { chosen = index; calls++; };
            tree.Root.AddChild(prompt);
            prompt.Open();

            AssertThat(prompt.IsOpen).IsTrue();
            AssertThat(prompt.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
            // Raw keys, resolved by control auto-translation.
            AssertString(prompt.Modal.CancelButton.Text).IsEqual("level00_choice_full");
            AssertString(prompt.Modal.ConfirmButton.Text).IsEqual("level00_choice_skip");
            AssertThat(prompt.Modal.CancelButton.HasFocus())
                .OverrideFailureMessage("The first option takes focus: a mashed confirm loses nothing.").IsTrue();

            // Back (ui_cancel) lands on the first option.
            prompt.Modal.Cancel();
            AssertThat(chosen).IsEqual(0);
            AssertThat(prompt.ChosenIndex).IsEqual(0);
            AssertThat(prompt.IsOpen).IsFalse();

            // Resolved: nothing more fires.
            prompt.ChooseSecond();
            AssertThat(calls).IsEqual(1);
            AssertThat(tree.Paused).IsEqual(originalPaused);
        } finally {
            if (prompt != null && GodotObject.IsInstanceValid(prompt)) {
                prompt.GetParent()?.RemoveChild(prompt);
                prompt.Free();
            }
            tree.Paused = originalPaused;
        }
    }

    [TestCase]
    public void TheSecondOptionResolvesToIndexOne() {
        var tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        TwoOptionPrompt prompt = null;
        try {
            prompt = TwoOptionPrompt.Create("dlg_l00_wren_offer_1", "level00_choice_full", "level00_choice_skip");
            int chosen = -1;
            prompt.Chosen += index => chosen = index;
            tree.Root.AddChild(prompt);
            prompt.Open();
            prompt.ChooseSecond();
            AssertThat(chosen).IsEqual(1);
        } finally {
            if (prompt != null && GodotObject.IsInstanceValid(prompt)) {
                prompt.GetParent()?.RemoveChild(prompt);
                prompt.Free();
            }
            tree.Paused = originalPaused;
        }
    }
}
