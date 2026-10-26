using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// UR-PAUSE's half of <see cref="PulpScreenKit"/> (the class is partial; UR-SCREENS' members are in
    /// <c>PulpScreenKit.cs</c>): the scrim, the 560 x 64 banner rows of the PAUSE-A list, the bottom-centre prompt
    /// bar and the two pure helpers the pause tag and status strip use. Nothing here sets a font size or builds a
    /// scripted StyleBox.
    /// </summary>
    public static partial class PulpScreenKit {

        /// <summary>UI_SPEC 4.3: pause rows are 560 wide and 64 tall (the 64 px plate of <c>TemporalGlassButton</c>).</summary>
        public const float PauseButtonWidth = 560f;
        public const float PauseButtonHeight = 64f;

        /// <summary>The scrim's own alpha: the theme's 0.85 scrim x 0.7 reads as the mock's 0.58 dim over the world.</summary>
        public const float ScrimAlpha = 0.7f;

        /// <summary>Prompt bar bottom margin when centred (UI_SPEC 4.3: y 1012 of 1080, a 34 px glyph row).</summary>
        public const float PromptBottomMargin = 34f;

        /// <summary>The spec starts the row label at x + 64, clear of the marker; the 64 px plate's own margin is 54.</summary>
        public const float RowLabelInset = 64f;

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

        /// <summary>
        /// The "(A) SELECT  (B) &lt;caption&gt;" bar at the bottom centre of <paramref name="host"/>: the shared
        /// <see cref="AddPromptBar"/> (so it is the same <see cref="PulpPromptBar"/> named
        /// <see cref="PromptBarName"/>) moved from the bottom-right corner to the PAUSE-A position.
        /// </summary>
        public static PulpPromptBar AddBottomPromptBar(Control host, string cancelCaptionKey) {
            PulpPromptBar bar = AddPromptBar(host, ("ui_accept", "ui_prompt_select"), ("ui_cancel", cancelCaptionKey));
            if (bar == null) return null;
            bar.Alignment = BoxContainer.AlignmentMode.Center;
            bar.AnchorLeft = 0.5f;
            bar.AnchorRight = 0.5f;
            bar.GrowHorizontal = Control.GrowDirection.Both;
            bar.OffsetRight = 0f;
            bar.OffsetBottom = -PromptBottomMargin;
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
