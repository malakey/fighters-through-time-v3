using System.Collections.Generic;
using FTT.Core;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// The prompt row, for example "(A) SELECT  (B) BACK" (UI_SPEC_GLASS 3.4.4, 3.8; TG-WIDGETS restyled the UR-WIDGETS
    /// bar): each pair is a glyph, 10 px, then a caption, and pairs are 30 px apart. The glyph follows player 1's last-used
    /// device: a 34 px circle with a 2 px ink border and the button letter in ink, or a key-cap of the same height
    /// (corner radius 6, a 2 px border, at least 34 wide, 9 px padding) with the key bound to the action. Captions are
    /// Labels in the <c>PromptCaption</c> role (T13, halo S) whose <c>Text</c> is the raw translation key (Godot
    /// auto-translates it; the label upper-cases it).
    /// </summary>
    public partial class PulpPromptBar : HBoxContainer {

        public const float GlyphHeight = 34f;
        public const int PairGap = 30;
        public const int GlyphGap = 10;

        private sealed class Pair {
            public StringName Action;
            public string CaptionKey;
            public HBoxContainer Row;
            public Control Glyph;
            public Label Caption;
        }

        private readonly List<Pair> _pairs = new();
        private PromptDevice _device;
        private bool _subscribed;

        public PulpPromptBar() {
            Name = "PulpPromptBar";
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeConstantOverride("separation", PairGap);
            _device = PulpPromptGlyphs.CurrentDevice();
        }

        /// <summary>The device family the glyphs currently show.</summary>
        public PromptDevice Device => _device;
        public int PairCount => _pairs.Count;

        /// <summary>The glyph control of pair <paramref name="index"/>.</summary>
        public Control GlyphAt(int index) => _pairs[index].Glyph;
        /// <summary>The caption Label of pair <paramref name="index"/> (its <c>Text</c> is the raw key).</summary>
        public Label CaptionAt(int index) => _pairs[index].Caption;

        /// <summary>Adds a glyph + caption pair for <paramref name="action"/> (for example <c>ui_accept</c>) and the caption's translation key.</summary>
        public void AddPrompt(StringName action, string captionKey) {
            var row = new HBoxContainer { Name = "Prompt_" + action, MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", GlyphGap);
            var caption = new Label {
                Name = "Caption",
                Text = captionKey,
                ThemeTypeVariation = UIPalette.LabelRoles.PromptCaption,
                Uppercase = true,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            var pair = new Pair { Action = action, CaptionKey = captionKey, Row = row, Caption = caption };
            pair.Glyph = BuildGlyph(action, _device);
            row.AddChild(pair.Glyph);
            row.AddChild(caption);
            AddChild(row);
            _pairs.Add(pair);
        }

        public override void _EnterTree() {
            if (_subscribed) return;
            _subscribed = true;
            if (InputManager.Instance != null) InputManager.Instance.DeviceAssigned += OnDeviceAssigned;
            Refresh();
        }

        public override void _ExitTree() {
            if (!_subscribed) return;
            _subscribed = false;
            if (InputManager.Instance != null) InputManager.Instance.DeviceAssigned -= OnDeviceAssigned;
        }

        private void OnDeviceAssigned(int slot, int device) => Refresh();

        /// <summary>Rebuilds every glyph when player 1's last-used device family changed.</summary>
        public void Refresh() {
            PromptDevice device = PulpPromptGlyphs.CurrentDevice();
            if (device == _device) return;
            _device = device;
            foreach (Pair pair in _pairs) {
                int slot = pair.Glyph.GetIndex();
                pair.Row.RemoveChild(pair.Glyph);
                pair.Glyph.QueueFree();
                pair.Glyph = BuildGlyph(pair.Action, device);
                pair.Row.AddChild(pair.Glyph);
                pair.Row.MoveChild(pair.Glyph, slot);
            }
        }

        // UI_SPEC_GLASS 3.4.4: in a prompt bar the glyph is 34 px, its 2 px border and its letter in ink; the bar sits on
        // the scene, so the letter carries the halo S like the captions.
        private static Control BuildGlyph(StringName action, PromptDevice device) {
            string text = PulpPromptGlyphs.LabelFor(action, device);
            if (device == PromptDevice.Pad) {
                return PulpPromptGlyphs.CreatePadGlyph(text, UIPalette.LabelRoles.PromptKey, UIPalette.Ink, GlyphHeight, halo: true);
            }
            PanelContainer cap = PulpPromptGlyphs.CreateKeyCap(text, UIPalette.LabelRoles.PromptKey, UIPalette.Ink, GlyphHeight, out Label label);
            PulpPromptGlyphs.ApplyHaloS(label);
            cap.Name = "KeyGlyph";
            return cap;
        }
    }
}
