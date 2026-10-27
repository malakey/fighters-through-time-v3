using FTT.Core;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>Which device family a prompt glyph should show.</summary>
    public enum PromptDevice { Keyboard, Pad }

    /// <summary>
    /// The one place the prompt widgets (the button badge and the prompt bar) decide which device to draw and what its
    /// glyph says, and build the glass glyphs (UI_SPEC_GLASS 3.4.4: a ringed letter for a pad, a key-cap for a keyboard).
    /// Player 1's last-used device comes from <see cref="InputManager"/>; tests force it through
    /// <see cref="DeviceOverrideForTesting"/>.
    /// </summary>
    public static class PulpPromptGlyphs {

        /// <summary>Test seam. Every test that sets it restores it in <c>finally</c>.</summary>
        internal static PromptDevice? DeviceOverrideForTesting;

        /// <summary>The device family player 1 last used (keyboard when no input manager exists).</summary>
        public static PromptDevice CurrentDevice() {
            if (DeviceOverrideForTesting.HasValue) return DeviceOverrideForTesting.Value;
            InputManager input = InputManager.Instance;
            return input != null && input.GetDeviceForPlayer(0) >= 0 ? PromptDevice.Pad : PromptDevice.Keyboard;
        }

        /// <summary>The pad letter for a joypad button index: 0-3 A B X Y, 9 LB, 10 RB, else the Controls tab's generic label.</summary>
        public static string PadButtonLabel(int buttonIndex) => buttonIndex switch {
            0 => "A",
            1 => "B",
            2 => "X",
            3 => "Y",
            9 => "LB",
            10 => "RB",
            _ => string.Format(TranslationServer.Translate("controls_binding_joy_button"), buttonIndex)
        };

        /// <summary>The pad glyph letter for <paramref name="action"/> (its first joypad button event); "?" when it has none.</summary>
        public static string PadLabelFor(StringName action) {
            if (InputMap.HasAction(action)) {
                foreach (InputEvent e in InputMap.ActionGetEvents(action)) {
                    if (e is InputEventJoypadButton pad) return PadButtonLabel((int)pad.ButtonIndex);
                }
            }
            return "?";
        }

        /// <summary>The key label for <paramref name="action"/> (its first keyboard event, upper-case); "?" when it has none.</summary>
        public static string KeyLabelFor(StringName action) {
            if (InputMap.HasAction(action)) {
                foreach (InputEvent e in InputMap.ActionGetEvents(action)) {
                    if (e is not InputEventKey key) continue;
                    Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
                    if (code == Key.None) continue;
                    return OS.GetKeycodeString(code).ToUpperInvariant();
                }
            }
            return "?";
        }

        /// <summary>The label for <paramref name="action"/> on <paramref name="device"/>.</summary>
        public static string LabelFor(StringName action, PromptDevice device) =>
            device == PromptDevice.Pad ? PadLabelFor(action) : KeyLabelFor(action);

        /// <summary>The glass key-cap (UI_SPEC_GLASS 3.4.4): corner radius 6, a 2 px border, minimum width 34, padding 9.</summary>
        public const int KeyCapRadius = 6, KeyCapBorder = 2;
        public const float KeyCapMinWidth = 34f, KeyCapPadding = 9f;

        /// <summary>
        /// A key-cap (UI_SPEC_GLASS 3.4.4, keyboard / mouse; AC22): a rounded outline of the glyph's height, corner radius
        /// 6, a 2 px border in <paramref name="textColor"/>, at least 34 wide with 9 px of padding, growing with its label.
        /// <paramref name="label"/> is the live Label in <paramref name="role"/>.
        /// </summary>
        internal static PanelContainer CreateKeyCap(string text, string role, Color textColor, float pillHeight, out Label label) {
            var plate = new StyleBoxFlat {
                DrawCenter = false, BgColor = new Color(0f, 0f, 0f, 0f), BorderColor = textColor, AntiAliasing = true,
                ContentMarginLeft = KeyCapPadding, ContentMarginRight = KeyCapPadding, ContentMarginTop = 0, ContentMarginBottom = 0
            };
            plate.SetBorderWidthAll(KeyCapBorder);
            plate.SetCornerRadiusAll(KeyCapRadius);
            var cap = new PanelContainer {
                Name = "KeyCap",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(KeyCapMinWidth, pillHeight)
            };
            cap.AddThemeStyleboxOverride("panel", plate);
            label = new Label {
                Name = "KeyLabel",
                Text = text,
                ThemeTypeVariation = role,
                Uppercase = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeColorOverride("font_color", textColor);
            cap.AddChild(label);
            return cap;
        }

        /// <summary>A centred live-letter Label over a full-rect parent.</summary>
        internal static Label CreateLetter(string text, string role, Color color) {
            var letter = new Label {
                Name = "Letter",
                Text = text,
                ThemeTypeVariation = role,
                Uppercase = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            letter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            letter.AddThemeColorOverride("font_color", color);
            return letter;
        }

        /// <summary>
        /// The pad glyph (UI_SPEC_GLASS 3.4.4): a <paramref name="size"/> px circle with a 2 px border in
        /// <paramref name="color"/> (<see cref="GlassPromptRing"/>, named <c>Circle</c>) and the live letter
        /// (<see cref="CreateLetter"/>) centred on it. <paramref name="halo"/> gives the letter the scene halo S (a glyph
        /// over art, the prompt bar).
        /// </summary>
        internal static Control CreatePadGlyph(string text, string role, Color color, float size, bool halo) {
            var glyph = new Control {
                Name = "PadGlyph",
                CustomMinimumSize = new Vector2(size, size),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var ring = new GlassPromptRing { Name = "Circle", RingColor = color };
            ring.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            glyph.AddChild(ring);
            Label letter = CreateLetter(text, role, color);
            if (halo) ApplyHaloS(letter);
            glyph.AddChild(letter);
            return glyph;
        }

        /// <summary>The scene halo S on a label (UI_SPEC_GLASS 2.4), for a role the theme draws as plate text.</summary>
        internal static void ApplyHaloS(Label label) {
            label.AddThemeConstantOverride("outline_size", UIPalette.HaloSOutline);
            label.AddThemeColorOverride("font_outline_color", UIPalette.HaloOutlineS);
            label.AddThemeConstantOverride("shadow_offset_x", 0);
            label.AddThemeConstantOverride("shadow_offset_y", UIPalette.HaloSShadowOffsetY);
            label.AddThemeConstantOverride("shadow_outline_size", UIPalette.HaloSShadowOutline);
            label.AddThemeColorOverride("font_shadow_color", UIPalette.HaloShadowS);
        }
    }
}
