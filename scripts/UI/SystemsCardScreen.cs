using System;
using System.Collections.Generic;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// V7.3 Fighter Onboarding: the universal Systems Card — one static,
    /// localized reference page with eight short sections (block &amp; shatter,
    /// the attack/block/grab triangle, DI, landing tech, Rally &amp;
    /// Desperation, Defy History, Resonance Momentum, Overtime).
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
        private PausePanel _panel;
        private Control _shade;

        /// <summary>
        /// One dim (UI_SPEC_GLASS 9.2): opened over a host that already dims (a pause menu, the Move List) the card sits on
        /// the host's dim and draws none of its own. Default false: the card brings its 0.70 dim.
        /// </summary>
        public bool UsesHostDim { get; set; }

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
            _shade = _root.GetNodeOrNull<Control>("Shade");
            // TG-PAUSE (UI_SPEC_GLASS 9.2): a 1240 x 900 glass slab, each of the eight sections a section heading over a
            // fading rule and its body copy, and a compact Back that stays clear of the scroll area.
            _panel = _root.GetNodeOrNull<PausePanel>("Center/Panel");
            if (_panel != null) {
                PulpScreenKit.DressPanel(_panel, sheen: true, buttonInset: true);
                MenuSafeAreaFit.AttachClearOfPromptBar(_panel, _root);
            }
            AddSectionRules(_root.GetNodeOrNull<Container>("Center/Panel/Layout/Scroll/Sections"));
            var close = _root.GetNodeOrNull<Button>("Center/Panel/Layout/FooterRow/CloseButton");
            if (close != null) {
                close.Pressed += Close;
                Pulp.PulpButtonDecor.Apply(close, compact: true).ShowPrompt = false;
                Pulp.PulpButtonDecor.ApplyStackWidth(new[] { close }, MoveListScreen.CompactFooterMinWidth, reservePrompt: false);
            }
            PulpScreenKit.AddBottomPromptBar(_root, "common_back");
        }

        /// <summary>
        /// 9.1 item 2: a 1 px rule (<c>edge</c> to transparent) under every section heading, and a little air above each
        /// heading after the first. Inserted beside the authored labels, whose names and order stay as authored.
        /// </summary>
        private static void AddSectionRules(Container sections) {
            if (sections == null) return;
            var titles = new List<Label>();
            for (int i = 0, n = sections.GetChildCount(); i < n; i++) {
                if (sections.GetChild(i) is Label label && label.Name.ToString().EndsWith("Title")) titles.Add(label);
            }
            for (int i = 0; i < titles.Count; i++) {
                Label title = titles[i];
                title.Uppercase = true;
                Pulp.GlassFlourish rule = Pulp.GlassFlourish.Create(Pulp.GlassFlourish.FlourishShape.Rule);
                rule.Name = title.Name + "Rule";
                rule.RuleColor = UIPalette.Edge;
                rule.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                sections.AddChild(rule);
                sections.MoveChild(rule, title.GetIndex() + 1);
                if (i == 0) continue;
                var gap = new Control {
                    Name = title.Name + "Gap", CustomMinimumSize = new Vector2(0f, MoveListScreen.SectionGap),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                sections.AddChild(gap);
                sections.MoveChild(gap, title.GetIndex());
            }
        }

        /// <summary>Shows the card and takes focus onto its close button.</summary>
        public void Open() {
            Visible = true;
            if (_shade != null) _shade.Visible = !UsesHostDim;
            _focusChain = FocusChainBuilder.Apply(
                _root?.GetNodeOrNull<Control>("Center/Panel/Layout/FooterRow"));
            // MOT-A row 4: the slab fades and rises (its own dim fading in with it unless it sits on a host's).
            _panel?.PlayAppear(UsesHostDim ? null : _shade);
        }

        /// <summary>Hides the card and hands control back to the opener.</summary>
        public void Close() {
            if (!Visible) return;
            Visible = false;
            _panel?.StopAppear(UsesHostDim ? null : _shade);
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
