using FTT.Core;
using Godot;

namespace FTT.UI.Pulp {

    /// <summary>Which device family a prompt glyph should show.</summary>
    public enum PromptDevice { Keyboard, Pad }

    /// <summary>
    /// UR-WIDGETS. The one place the pulp prompt widgets (the button badge and the prompt bar) decide which
    /// device to draw and what its glyph says. Player 1's last-used device comes from
    /// <see cref="InputManager"/>; tests force it through <see cref="DeviceOverrideForTesting"/>.
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

        public const string KeyCapPath = "res://assets/sprites/ui/pulp/keycap.png";

        /// <summary>
        /// A key-cap pill (UI_SPEC 3.7, keyboard / mouse): the kit's 9-slice <c>keycap.png</c> as a panel that
        /// grows with its label. <paramref name="label"/> is the live Label in <paramref name="role"/>.
        /// </summary>
        internal static PanelContainer CreateKeyCap(string text, string role, Color textColor, float pillHeight, out Label label) {
            var plate = new StyleBoxTexture {
                Texture = GD.Load<Texture2D>(KeyCapPath),
                TextureMarginLeft = 21, TextureMarginTop = 19, TextureMarginRight = 21, TextureMarginBottom = 19,
                ExpandMarginLeft = 2, ExpandMarginTop = 2, ExpandMarginRight = 2, ExpandMarginBottom = 2,
                ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 0, ContentMarginBottom = 0
            };
            var cap = new PanelContainer {
                Name = "KeyCap",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(44f, pillHeight)
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
    }
}
