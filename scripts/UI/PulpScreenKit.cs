using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-PAUSE. The few helpers the pause, overlay and dialogue surfaces share when they take the pulp look: the
    /// scrim, the 560 x 64 banner buttons of the PAUSE-A list, the bottom-centre prompt bar and the "first segment of
    /// a level title" rule the pause tag uses. Everything here composes the UR-THEME roles and the UR-WIDGETS parts; it
    /// never sets a font size and never builds a scripted StyleBox.
    /// </summary>
    public static class PulpScreenKit {

        /// <summary>UI_SPEC 4.3: pause rows are 560 wide and 64 tall (the 64 px plate of <c>TemporalGlassButton</c>).</summary>
        public const float PauseButtonWidth = 560f;
        public const float PauseButtonHeight = 64f;

        /// <summary>The scrim's own alpha: the theme's 0.85 scrim x 0.7 reads as the mock's 0.58 dim over the world.</summary>
        public const float ScrimAlpha = 0.7f;

        /// <summary>Prompt bar bottom margin (UI_SPEC 4.3: y 1012 of 1080, a 34 px glyph row).</summary>
        public const float PromptBottomMargin = 34f;

        /// <summary>A full-rect translucent scrim that swallows the mouse.</summary>
        public static PanelContainer MakeScrim(string name = "Shade") {
            var scrim = new PanelContainer {
                Name = name,
                ThemeTypeVariation = UIPalette.Variations.ScrimPanel,
                MouseFilter = Control.MouseFilterEnum.Stop,
                SelfModulate = new Color(1f, 1f, 1f, ScrimAlpha)
            };
            scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            return scrim;
        }

        /// <summary>A pause row: the banner plate at 560 x 64 with the marker, the focus pose and the prompt badge.</summary>
        public static Button StylePauseButton(Button button, float width = PauseButtonWidth) {
            button.ThemeTypeVariation = UIPalette.Variations.TemporalGlassButton;
            button.CustomMinimumSize = new Vector2(width, PauseButtonHeight);
            PadLabel(button);
            PulpButtonDecor.Apply(button);
            return button;
        }

        /// <summary>The spec starts the row label at x + 64, clear of the marker; the 64 px plate's own margin is 54.</summary>
        public const float RowLabelInset = 64f;

        private static readonly string[] ButtonBoxes = { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" };

        private static void PadLabel(Button button) {
            Theme theme = UIPalette.LoadTheme();
            string variation = button.ThemeTypeVariation.ToString();
            foreach (string box in ButtonBoxes) {
                if (!theme.HasStylebox(box, variation) || theme.GetStylebox(box, variation).Duplicate() is not StyleBoxTexture own) continue;
                own.ContentMarginLeft = RowLabelInset;
                button.AddThemeStyleboxOverride(box, own);
            }
        }

        /// <summary>Adds the "(A) SELECT  (B) &lt;caption&gt;" bar at the bottom centre of <paramref name="host"/>.</summary>
        public static PulpPromptBar AddBottomPromptBar(Control host, string cancelCaptionKey) {
            var bar = new PulpPromptBar { Name = "PromptBar", Alignment = BoxContainer.AlignmentMode.Center };
            bar.AddPrompt("ui_accept", "ui_prompt_select");
            bar.AddPrompt("ui_cancel", cancelCaptionKey);
            bar.AnchorLeft = 0.5f;
            bar.AnchorRight = 0.5f;
            bar.AnchorTop = 1f;
            bar.AnchorBottom = 1f;
            bar.GrowHorizontal = Control.GrowDirection.Both;
            bar.GrowVertical = Control.GrowDirection.Begin;
            bar.OffsetBottom = -PromptBottomMargin;
            host.AddChild(bar);
            return bar;
        }

        /// <summary>"Pompeii 79 AD" from "Pompeii 79 AD — Eruption of Vesuvius": the text before the first dash, trimmed.</summary>
        public static string ShortTitle(string title) {
            if (string.IsNullOrWhiteSpace(title)) return "";
            int cut = title.IndexOf(" — ", System.StringComparison.Ordinal);
            return (cut > 0 ? title.Substring(0, cut) : title).Trim();
        }

        /// <summary>"2:05" or "1:02:05": a level clock.</summary>
        public static string FormatClock(float seconds) {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            int secs = total % 60;
            return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes}:{secs:00}";
        }
    }
}
