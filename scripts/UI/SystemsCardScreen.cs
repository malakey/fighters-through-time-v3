using System;
using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// V7.3 Fighter Onboarding: the universal Systems Card — one static,
    /// localized reference page with eight short sections (block &amp; shatter,
    /// the attack/block/grab triangle, DI, landing tech, Rally &amp;
    /// Desperation, Defy History, Echo Step &amp; Resonance Momentum, Overtime).
    /// One sentence plus one number each; a reference, not a lesson
    /// (design-godot-v7.md Section 7, "Fighter Onboarding").
    ///
    /// <para>Follows the <see cref="SettingsMenu"/> overlay pattern: the scene
    /// is a script-less authored Control tree this CanvasLayer instantiates, so
    /// every opener constructs the class directly. It runs
    /// <see cref="Node.ProcessModeEnum.Always"/> (usable from a pause menu) and
    /// never writes <see cref="SceneTree.Paused"/> — closing hands control back
    /// to whoever opened it via <see cref="Closed"/>.</para>
    /// </summary>
    public partial class SystemsCardScreen : CanvasLayer {
        public const string ScenePath = "res://scenes/ui/SystemsCard.tscn";

        /// <summary>Raised after the card hides, so the opener can restore focus.</summary>
        public event Action Closed;

        private Control _root;
        private List<Control> _focusChain = new();

        /// <summary>The instantiated authored scene root. Test seam.</summary>
        internal Control Root => _root;

        public override void _Ready() {
            Layer = 101;
            ProcessMode = ProcessModeEnum.Always;
            Visible = false;
            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            _root = packed?.Instantiate<Control>();
            if (_root == null) {
                GD.PushError($"SystemsCardScreen could not instantiate {ScenePath}.");
                return;
            }
            AddChild(_root);
            UIPalette.ApplyTheme(_root);
            var close = _root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/CloseButton");
            if (close != null) close.Pressed += Close;
        }

        /// <summary>Shows the card and takes focus onto its close button.</summary>
        public void Open() {
            Visible = true;
            _focusChain = FocusChainBuilder.Apply(
                _root?.GetNodeOrNull<Control>("Center/Panel/Layout/FooterRow"));
        }

        /// <summary>Hides the card and hands control back to the opener.</summary>
        public void Close() {
            if (!Visible) return;
            Visible = false;
            Closed?.Invoke();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (!Visible || @event == null) return;
            if (@event.IsActionPressed("ui_cancel")
                || @event.IsActionPressed(FTT.Core.InputManager.Actions.Pause)) {
                Close();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }
}
