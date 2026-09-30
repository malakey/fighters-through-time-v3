using System;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 13 W3 (D16): a small reusable two-option prompt on the shared
    /// <see cref="ConfirmModal"/> idiom — the same themed glass panel, focus trap,
    /// <see cref="Node.ProcessModeEnum.Always"/> processing and raw-key text — for
    /// a choice that is not a confirmation (Wren's Level 0 offer: <b>Full
    /// calibration</b> / <b>Skip — I'll learn in the field</b>).
    ///
    /// <para>It is deliberately a wrapper, not a new dialogue-schema feature: the
    /// modal's left (cancel) button is the <b>first</b> option and takes initial
    /// focus, its right (confirm) button is the <b>second</b>, and
    /// <c>ui_cancel</c> selects the first option — so the choice that loses
    /// nothing is the one a mashed button or a Back press lands on. The prompt
    /// never writes <c>SceneTree.Paused</c>; the owner decides what else holds
    /// still while it is up.</para>
    /// </summary>
    public partial class TwoOptionPrompt : CanvasLayer {

        /// <summary>Raised once with 0 for the first option or 1 for the second.</summary>
        public event Action<int> Chosen;

        public ConfirmModal Modal { get; private set; }
        public bool IsOpen => Modal != null && Modal.IsOpen;

        /// <summary>The option picked, or -1 while undecided. Test seam.</summary>
        public int ChosenIndex { get; private set; } = -1;

        public static TwoOptionPrompt Create(string promptKey, string firstOptionKey, string secondOptionKey,
                                             string titleKey = "") {
            var prompt = new TwoOptionPrompt { Name = "TwoOptionPrompt" };
            prompt.Modal = ConfirmModal.Create(
                promptKey, confirmKey: secondOptionKey, cancelKey: firstOptionKey, titleKey: titleKey);
            prompt.AddChild(prompt.Modal);
            prompt.Modal.Cancelled += () => prompt.Resolve(0);
            prompt.Modal.Confirmed += () => prompt.Resolve(1);
            return prompt;
        }

        public override void _Ready() {
            Layer = 95;
            ProcessMode = ProcessModeEnum.Always;
        }

        public void Open() => Modal?.Open();

        /// <summary>Picks the first option. Public so the path is testable without input.</summary>
        public void ChooseFirst() => Modal?.Cancel();

        /// <summary>Picks the second option.</summary>
        public void ChooseSecond() => Modal?.Confirm();

        private void Resolve(int index) {
            if (ChosenIndex >= 0) return;
            ChosenIndex = index;
            Chosen?.Invoke(index);
        }
    }
}
