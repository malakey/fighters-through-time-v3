using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7 committed accessibility addition: the 90%–140% UI scale applied to the
/// shared theme's font sizes and the HUD layout (design "Committed
/// Accessibility Additions"). The theme is rescaled by mutating the one cached
/// resource in place, so every test here restores scale 1 before returning —
/// the UIThemeTests contract pins the authored sizes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UiScaleTests {

    [TestCase]
    public void TheSaveClampsTheScaleToTheDesignedRange() {
        var data = new GlobalSaveData { UiScale = 5f };
        data.Normalize();
        AssertThat(data.UiScale).IsEqual(GlobalSaveData.MaxUiScale);

        data.UiScale = 0.1f;
        data.Normalize();
        AssertThat(data.UiScale).IsEqual(GlobalSaveData.MinUiScale);

        data.UiScale = 1f;
        data.Normalize();
        AssertThat(data.UiScale).IsEqual(1f);
    }

    [TestCase]
    public void FontSizesRoundAndNeverReachZero() {
        AssertThat(UIPalette.ScaleFontSize(18, 1.4f)).IsEqual(25);
        AssertThat(UIPalette.ScaleFontSize(14, 0.9f)).IsEqual(13);
        AssertThat(UIPalette.ScaleFontSize(1, 0.9f)).IsEqual(1);
    }

    [TestCase]
    public void ApplyUiScaleRescalesTheSharedThemeInPlaceAndNeverCompounds() {
        Theme theme = UIPalette.LoadTheme();
        AssertObject(theme).IsNotNull();
        int authored = theme.DefaultFontSize;
        try {
            UIPalette.ApplyUiScale(1.4f);
            int scaled = UIPalette.ScaleFontSize(authored, 1.4f);
            AssertThat(theme.DefaultFontSize).IsEqual(scaled);
            AssertThat(UIPalette.AppliedUiScale).IsEqual(1.4f);

            // Applying the same factor again must scale from the authored
            // sizes, not the current ones.
            UIPalette.ApplyUiScale(1.4f);
            AssertThat(theme.DefaultFontSize).IsEqual(scaled);
        } finally {
            UIPalette.ApplyUiScale(1f);
        }
        AssertThat(theme.DefaultFontSize).IsEqual(authored);
    }

    /// <summary>
    /// V7.3: the non-HUD screens express their type-scale roles through the
    /// theme's Label type variations instead of per-label font-size overrides,
    /// because ApplyUiScale iterates GetFontSizeTypeList() — variations scale
    /// with the theme for free while an override freezes its label at 100%.
    /// </summary>
    [TestCase]
    public void LabelTypeVariationsScaleWithTheTheme() {
        Theme theme = UIPalette.LoadTheme();
        AssertObject(theme).IsNotNull();
        try {
            UIPalette.ApplyUiScale(1.4f);
            AssertThat(theme.GetFontSize("font_size", UIPalette.TitleLabelVariation))
                .IsEqual(UIPalette.ScaleFontSize(UIPalette.TitleFontSize, 1.4f));
            AssertThat(theme.GetFontSize("font_size", UIPalette.EmphasisLabelVariation))
                .IsEqual(UIPalette.ScaleFontSize(UIPalette.EmphasisFontSize, 1.4f));
            AssertThat(theme.GetFontSize("font_size", UIPalette.HeadingLabelVariation))
                .IsEqual(UIPalette.ScaleFontSize(UIPalette.HeadingFontSize, 1.4f));
            AssertThat(theme.GetFontSize("font_size", UIPalette.SmallLabelVariation))
                .IsEqual(UIPalette.ScaleFontSize(UIPalette.SmallFontSize, 1.4f));
        } finally {
            UIPalette.ApplyUiScale(1f);
        }
        AssertThat(theme.GetFontSize("font_size", UIPalette.TitleLabelVariation))
            .IsEqual(UIPalette.TitleFontSize);
    }

    [TestCase]
    public void TheUnscaledCopyKeepsAuthoredSizesWhileTheSharedThemeIsScaled() {
        // The HUDs scale their whole layout through UiScaleBinder, so they use
        // an authored-size theme copy to avoid scaling their text twice.
        Theme theme = UIPalette.LoadTheme();
        AssertObject(theme).IsNotNull();
        int authored = theme.DefaultFontSize;
        try {
            UIPalette.ApplyUiScale(1.4f);
            Theme unscaled = UIPalette.NewUnscaledTheme();
            AssertObject(unscaled).IsNotNull();
            AssertThat(unscaled.DefaultFontSize).IsEqual(authored);
            AssertThat(theme.DefaultFontSize).IsNotEqual(authored);
        } finally {
            UIPalette.ApplyUiScale(1f);
        }
    }

    [TestCase]
    public void TheBinderMagnifiesTheHudRootAndShrinksItsAnchorBox() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        AssertObject(data).IsNotNull();
        float savedScale = data.UiScale;
        var root = new Control();
        try {
            data.UiScale = 1.25f;
            var binder = new UiScaleBinder();
            AssertThat(binder.Apply(root, force: true)).IsTrue();

            // Magnified render transform over a 1/scale anchor box: children
            // lay out on a smaller canvas that scales back up to exactly the
            // viewport, so corner-anchored clusters stay pinned.
            AssertThat(Mathf.IsEqualApprox(root.Scale.X, 1.25f)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(root.AnchorRight, 0.8f)).IsTrue();
            AssertThat(Mathf.IsEqualApprox(root.AnchorBottom, 0.8f)).IsTrue();

            // An unchanged setting is a no-op poll.
            AssertThat(binder.Apply(root)).IsFalse();
        } finally {
            data.UiScale = savedScale;
            root.Free();
        }
    }

    [TestCase]
    public void TheSettingsSceneAuthorsTheUiScaleSliderOverTheDesignedRange() {
        var packed = ResourceLoader.Load<PackedScene>(SettingsMenu.ScenePath);
        AssertObject(packed).IsNotNull();
        Control root = packed.Instantiate<Control>();
        try {
            var slider = root.GetNodeOrNull<HSlider>("Margin/Panel/Body/Tabs/Gameplay/UiScaleSlider");
            AssertObject(slider).IsNotNull();
            AssertThat(Mathf.IsEqualApprox((float)slider.MinValue, GlobalSaveData.MinUiScale)).IsTrue();
            AssertThat(Mathf.IsEqualApprox((float)slider.MaxValue, GlobalSaveData.MaxUiScale)).IsTrue();
            var label = root.GetNodeOrNull<Label>("Margin/Panel/Body/Tabs/Gameplay/UiScaleLabel");
            AssertObject(label).IsNotNull();
        } finally {
            root.Free();
        }
    }
}
