using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.Diagnostics;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-POLISH. The small defects the final screen captures showed: trimmed upper-case button labels, the credits skip
/// hint over the roll, the dialogue speaker tight against its box, the slider's stray track cap, and the capture tool's
/// scale targets.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UrPolishTests {

    private static async Task Settle(int frames = 6) {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < frames; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static string Wraps(Button button, string tag) {
        PulpButtonDecor decor = button.GetNodeOrNull<PulpButtonDecor>("PulpButtonDecor");
        if (decor == null) return $"{tag}: no decor";
        StyleBox box = button.GetThemeStylebox("normal");
        float chrome = (box?.GetContentMargin(Side.Top) ?? 0f) + (box?.GetContentMargin(Side.Bottom) ?? 0f);
        float lineHeight = button.GetThemeFont("font").GetHeight(button.GetThemeFontSize("font_size"));
        float needed = decor.WrappedLineCount * lineHeight + chrome;
        if (decor.LabelIsTrimmed) return $"{tag}: '{decor.DrawnText}' ends in an ellipsis";
        if (decor.WidestWrappedLine > decor.LabelAvailableWidth + 0.5f) return $"{tag}: a word is wider than its line";
        if (needed > button.Size.Y + 1f) return $"{tag}: {decor.WrappedLineCount} lines need {needed} in {button.Size.Y} ('{decor.DrawnText}')";
        return null;
    }

    [TestCase]
    public async Task TheModalAndResultsButtonLabelsShowWholeAtBothUiScales() {
        float original = UIPalette.AppliedUiScale;
        Theme hold = UIPalette.LoadTheme();
        var problems = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                UIPalette.ApplyUiScale(scale);
                var host = new Node { Name = "UrPolishLabelHost" };
                ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
                try {
                    TwoOptionPrompt prompt = TwoOptionPrompt.Create(
                        "dlg_l00_wren_offer_1", "level00_choice_full", "level00_choice_skip", "speaker_wren");
                    host.AddChild(prompt);
                    prompt.Open();
                    LevelResultsPanel results = LevelResultsPanel.CreateDefault();
                    host.AddChild(results);
                    results.ShowResults("campaign_level_02", 140, 60, 25, 30, 20, 40, 15, 612f, 2);
                    await Settle();
                    foreach (Button button in new[] { prompt.Modal.CancelButton, prompt.Modal.ConfirmButton }) {
                        string problem = Wraps(button, $"{button.Name}@{scale}");
                        if (problem != null) problems.Add(problem);
                    }
                    var returnButton = results.GetNodeOrNull<Button>("Shade/Panel/Layout/ReturnButton");
                    if (returnButton == null) problems.Add("no return button");
                    else {
                        string problem = Wraps(returnButton, $"ReturnButton@{scale}");
                        if (problem != null) problems.Add(problem);
                    }
                } finally {
                    host.GetParent()?.RemoveChild(host);
                    host.Free();
                }
            }
        } finally {
            UIPalette.ApplyUiScale(original);
            System.GC.KeepAlive(hold);
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }

    [TestCase]
    public async Task TheCreditsSkipHintHasItsOwnBandAboveTheRoll() {
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/ui/Credits.tscn");
        var credits = packed.Instantiate<CanvasLayer>();
        var host = new Node { Name = "UrPolishCreditsHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        try {
            host.AddChild(credits);
            await Settle(3);
            var shade = credits.GetNode<Control>("Shade");
            var band = shade.GetNodeOrNull<ColorRect>("HintBand");
            var hint = shade.GetNode<Control>("SkipHint");
            var scroll = shade.GetNode<Control>("Scroll");
            AssertThat(band != null).IsTrue();
            AssertThat(band.GetIndex() < hint.GetIndex()).OverrideFailureMessage("the band sits under the hint").IsTrue();
            AssertThat(band.GetIndex() > scroll.GetIndex()).OverrideFailureMessage("the band sits over the roll").IsTrue();
            AssertThat(band.Color.A).IsEqual(1f);
            AssertThat(band.GetRect().Encloses(hint.GetRect())).OverrideFailureMessage("the hint stands wholly inside its band").IsTrue();
            AssertThat(band.GetRect().End.Y).IsEqual(shade.Size.Y);
        } finally {
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }
    }

    /// <summary>
    /// TG-THEME re-valued this for the glass: the dialogue slab pads 22 / 26 / 22 (UI_SPEC_GLASS 3.1; the pulp plate's
    /// thick frame needed 30 on top), and the slider track, now a leaning <see cref="StyleBoxFlat"/>, still ends under
    /// the grabber's travel limit so its slanted cap never shows past the grabber at a high value.
    /// </summary>
    [TestCase]
    public void TheDialoguePanelGivesTheSpeakerItsPaddingAndTheSliderTrackEndsUnderTheGrabber() {
        Theme theme = UIPalette.LoadTheme();
        StyleBox panel = theme.GetStylebox("panel", UIPalette.Variations.DialogueGlassPanel);
        AssertThat(panel.GetContentMargin(Side.Top) >= 22f).OverrideFailureMessage("UI_SPEC_GLASS 3.1: the dialogue box pads 22 on top").IsTrue();
        AssertThat(panel.GetContentMargin(Side.Left) >= 26f).IsTrue();
        AssertThat(panel.GetContentMargin(Side.Right) >= 26f).IsTrue();
        AssertThat(panel.GetContentMargin(Side.Bottom) >= 22f).IsTrue();
        StyleBox slider = theme.GetStylebox("slider", "HSlider");
        float expandRight = slider switch {
            StyleBoxFlat flat => flat.ExpandMarginRight,
            StyleBoxTexture texture => texture.ExpandMarginRight,
            _ => 0f
        };
        float lean = slider is StyleBoxFlat leaning ? Mathf.Abs(leaning.Skew.X) * slider.GetMinimumSize().Y / 2f : 0f;
        // The cap must end at least its own lean inside the track's rect, under the grabber at the maximum value.
        AssertThat(expandRight + lean < 0f).OverrideFailureMessage("the track's slanted right cap must not stick out past the grabber").IsTrue();
    }

    [TestCase]
    public void AScaleAppliedWhileTheCaptureRunnerHoldsTheThemeSurvivesAndResetScaleRestoresBoth() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) AssertThat("GlobalData is missing").IsEqual("");
        float savedSetting = data.UiScale;
        float savedScale = UIPalette.AppliedUiScale;
        Theme held = UIPalette.LoadTheme();
        try {
            UIPalette.ApplyUiScale(1f);
            int baseline = held.DefaultFontSize;
            UIPalette.ApplyUiScale(1.4f);
            data.UiScale = 1.4f;
            // The same instance comes back from the loader while it is held, so a screen built next wears the scale.
            Theme again = UIPalette.LoadTheme();
            AssertThat(ReferenceEquals(again, held)).IsTrue();
            AssertThat(again.DefaultFontSize > baseline).IsTrue();
            UiCaptureRunner.ResetScale(savedSetting);
            AssertThat(UIPalette.AppliedUiScale).IsEqual(1f);
            AssertThat(data.UiScale).IsEqual(savedSetting);
        } finally {
            data.UiScale = savedSetting;
            UIPalette.ApplyUiScale(savedScale);
        }
    }

    [TestCase]
    public void TheMenuFitKeepsAnExtraVerticalReserveClearWhenAsked() {
        var view = new Vector2(1920f, 1080f);
        var panel = new Vector2(1560f, 980f);
        float plain = MenuSafeAreaFit.ScaleFor(panel, view);
        float reserved = MenuSafeAreaFit.ScaleFor(panel, view, 56f);
        AssertThat(plain).IsEqual(1f);
        AssertThat(reserved < 1f).OverrideFailureMessage("a 980 px panel must shrink once 80 px are kept clear top and bottom").IsTrue();
        AssertThat(Mathf.IsEqualApprox(reserved, (1080f - 160f) / 980f)).IsTrue();
    }
}
