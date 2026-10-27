using Godot;

namespace FTT.UI.Pulp {

    /// <summary>
    /// TG-SCREENS, a kit widget since TG-CLOSE. A results row (UI_SPEC_GLASS 9.2, level results: "label Outfit 500 20 .10em upper case ink2 left, value
    /// Outfit 600 26 ink right (tabular)") drawn over an existing line <see cref="Label"/>. The line keeps its whole
    /// formatted text (the results tests and screen readers read it) but does not draw it; its two children do: the
    /// caption (<c>StatLabel</c>, the nearest role, upper-case) on the left and the value (<c>SettingsRowValue</c>, Outfit
    /// 600 26 tabular) on the right. The line takes the value's role so it is as tall as the value. Idempotent per line.
    /// </summary>
    public partial class ResultRow : Control {
        public const string NodeName = "ResultRow";

        private Label _line;
        private readonly Label _caption;
        private readonly Label _value;

        public Label Caption => _caption;
        public Label Value => _value;

        public ResultRow() {
            Name = NodeName;
            MouseFilter = MouseFilterEnum.Ignore;
            _caption = new Label {
                Name = "Caption", ThemeTypeVariation = UIPalette.LabelRoles.StatLabel, Uppercase = true,
                VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore,
                AutoTranslateMode = AutoTranslateModeEnum.Disabled, ClipText = true
            };
            _value = new Label {
                Name = "Value", ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowValue,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled
            };
            AddChild(_caption);
            AddChild(_value);
        }

        /// <summary>
        /// Splits a "Caption: {0}" style format into a results row: the caption is the text before the last ": " that a
        /// placeholder follows (formatted, so "Player {0} stocks lost: {1}" reads "Player 2 stocks lost" | "1"), else the text
        /// before the first placeholder ("Secrets {0}/{1}" reads "Secrets" | "0/1"); a format with neither is all caption.
        /// Placeholder numbers keep their meaning in either half. Pure; a malformed format falls back to the whole line.
        /// </summary>
        public static (string Caption, string Value) SplitRow(string format, params object[] args) {
            format ??= "";
            try {
                int colon = format.LastIndexOf(": ", System.StringComparison.Ordinal);
                if (colon > 0 && format.IndexOf('{', colon) >= 0) {
                    return (string.Format(format.Substring(0, colon), args).TrimEnd(), string.Format(format.Substring(colon + 2), args).Trim());
                }
                int brace = format.IndexOf('{');
                if (brace > 0) {
                    return (format.Substring(0, brace).TrimEnd(' ', ':', ' '), string.Format(format.Substring(brace), args).Trim());
                }
                return (string.Format(format, args), "");
            } catch (System.FormatException) {
                return (format, "");
            }
        }

        /// <summary>Dresses <paramref name="line"/> as a row (a second call returns the first).</summary>
        public static ResultRow Attach(Label line) {
            if (line == null) return null;
            if (line.GetNodeOrNull<ResultRow>(NodeName) is { } existing) return existing;
            var row = new ResultRow { _line = line };
            line.ThemeTypeVariation = UIPalette.LabelRoles.SettingsRowValue;
            line.SelfModulate = new Color(1f, 1f, 1f, 0f);
            line.AddChild(row);
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row._caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row._value.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            return row;
        }

        /// <summary>
        /// Sets the line's whole text (<c>string.Format(format, args)</c>) and the row's caption and value from it
        /// (<see cref="SplitRow"/>); the caption is clipped, never the value.
        /// </summary>
        public void SetText(string format, params object[] args) {
            if (_line != null) _line.Text = string.Format(format ?? "", args);
            (string caption, string value) = SplitRow(format, args);
            _caption.Text = caption;
            _value.Text = value;
            InsetCaption();
        }

        /// <summary>The gap kept between the caption's end and the value.</summary>
        public const float CaptionGap = 24f;

        // Keep the caption clear of the value: it may run to the value's left edge less a gap. Re-run after a theme
        // (UI-scale) change, once the value's font size has settled.
        private void InsetCaption() => _caption.OffsetRight = -(_value.GetMinimumSize().X + CaptionGap);

        private bool _insetQueued;

        public override void _Notification(int what) {
            if (what != NotificationThemeChanged || _insetQueued) return;
            _insetQueued = true;
            Callable.From(() => {
                _insetQueued = false;
                if (IsInstanceValid(this)) InsetCaption();
            }).CallDeferred();
        }

        /// <summary>Colours the value (the dust total reads in <c>res</c>).</summary>
        public void SetValueColor(Color color) => _value.AddThemeColorOverride("font_color", color);
    }
}
