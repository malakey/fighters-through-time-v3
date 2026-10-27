using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// The pause family's half of <see cref="PulpScreenKit"/> (the class is partial; the rest is in
    /// <c>PulpScreenKit.cs</c>): the dim, the 560 x 64 chevron rows of the PAUSE-A list, the bottom-centre prompt bar and the
    /// two pure helpers the pause tag and status strip use. Nothing here sets a font size or builds a scripted StyleBox.
    /// </summary>
    public static partial class PulpScreenKit {

        /// <summary>UI_SPEC_GLASS 3.2, 4.3: pause rows are 560 wide and 64 tall (the <c>TemporalGlassButton</c> chevron).</summary>
        public const float PauseButtonWidth = 560f;
        public const float PauseButtonHeight = 64f;

        /// <summary>
        /// The dim's own alpha: <c>ScrimPanel</c> paints <c>UIPalette.ScrimFill</c> (3, 5, 14) opaque and every user
        /// multiplies it by this, which gives the build's pause dim rgba(3, 5, 14, .70) (UI_SPEC_GLASS 1.4, the theme's
        /// self-alpha contract).
        /// </summary>
        public const float ScrimAlpha = 0.7f;

        /// <summary>Prompt bar bottom margin when centred (UI_SPEC_GLASS 4.3: y 1012 of 1080, a 34 px glyph row).</summary>
        public const float PromptBottomMargin = 34f;

        /// <summary>The chevron's label starts at x + 58 (UI_SPEC_GLASS 3.2), which is the theme's own left padding.</summary>
        public const float RowLabelInset = 58f;

        /// <summary>A full-rect pause dim (the <c>ScrimPanel</c> at <see cref="ScrimAlpha"/>) that swallows the mouse.</summary>
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

        /// <summary>A pause row: the 560 x 64 chevron with the diamond marker, the focus pose and the prompt badge.</summary>
        public static Button StylePauseButton(Button button, float width = PauseButtonWidth) {
            button.ThemeTypeVariation = UIPalette.Variations.TemporalGlassButton;
            button.CustomMinimumSize = new Vector2(width, PauseButtonHeight);
            PadLabel(button);
            PulpButtonDecor.Apply(button);
            return button;
        }

        private static readonly string[] ButtonBoxes = { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled", "selected" };

        // Keeps the label at RowLabelInset. The glass theme already pads 58, so this writes nothing there; when it must, it
        // copies the box the button really draws: its own override, else the shared theme's box for its variation. The
        // theme is read directly, not through the button, because a row styled before its screen is in the tree (the
        // Fighter pause builds its rows so) resolves the engine's default box there, and copying that would freeze it.
        private static void PadLabel(Button button) {
            Theme theme = UIPalette.LoadTheme();
            string variation = button.ThemeTypeVariation.ToString();
            foreach (string box in ButtonBoxes) {
                StyleBox current = button.HasThemeStyleboxOverride(box) ? button.GetThemeStylebox(box)
                    : theme != null && theme.HasStylebox(box, variation) ? theme.GetStylebox(box, variation) : null;
                if (current == null || Mathf.IsEqualApprox(current.GetContentMargin(Side.Left), RowLabelInset)) continue;
                if (current.Duplicate() is not StyleBox own) continue;
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
