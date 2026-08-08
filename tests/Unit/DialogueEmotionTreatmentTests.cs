using System.Collections.Generic;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B3. The emotion → portrait-treatment mapping that replaced the
/// literal "(Determined)" label beside the speaker name.
///
/// <para>Two failure modes are worth a test. The first is a missing case: an
/// authored key that falls through the mapping used to print itself; now it
/// would paint nothing, which is a silent regression rather than a visible one.
/// The second is a tint heavy enough to recolour the portrait — the portrait is
/// the character's face, and a mood cue that changes their skin tone is a bug
/// that only shows up once production art lands.</para>
/// </summary>
// The mapping itself is pure, but Color.IsEqualApprox and Colors.White are
// engine calls, and the GdUnit analyzer (GdUnit0501) requires the runtime for
// them. Comparing colours by hand-rolled epsilon to keep the suite pure would
// trade a real contract for a bookkeeping one.
[TestSuite]
[RequireGodotRuntime]
public class DialogueEmotionTreatmentTests {

    /// <summary>Every emotion key the authored dialogue sets actually use.</summary>
    private static readonly (string Key, DialogueEmotion Emotion)[] AuthoredEmotions = {
        ("emotion_neutral", DialogueEmotion.Neutral),
        ("emotion_determined", DialogueEmotion.Determined),
        ("emotion_shocked", DialogueEmotion.Shocked),
        ("emotion_confused", DialogueEmotion.Confused),
        ("emotion_injured", DialogueEmotion.Injured)
    };

    [TestCase]
    public void EveryAuthoredEmotionKeyParsesToItsOwnEmotion() {
        foreach ((string key, DialogueEmotion expected) in AuthoredEmotions) {
            AssertThat(DialogueEmotionTreatment.Parse(key))
                .OverrideFailureMessage($"'{key}' must parse to {expected}.")
                .IsEqual(expected);
        }
        // Case and stray whitespace in an authored .tres must not silently
        // downgrade a line to neutral.
        AssertThat(DialogueEmotionTreatment.Parse("  EMOTION_Injured "))
            .IsEqual(DialogueEmotion.Injured);
    }

    [TestCase]
    public void AnUnknownOrAbsentEmotionPresentsAsCalmRatherThanBlank() {
        foreach (string key in new[] { null, "", "   ", "emotion_smug", "not_an_emotion" }) {
            DialogueEmotionTreatment.Treatment treatment = DialogueEmotionTreatment.Resolve(key);
            AssertThat(treatment.Emotion)
                .OverrideFailureMessage($"'{key ?? "<null>"}' must resolve to Neutral.")
                .IsEqual(DialogueEmotion.Neutral);
            AssertThat(treatment.FrameWidth > 0)
                .OverrideFailureMessage("An unresolved emotion must still draw a frame.")
                .IsTrue();
        }
    }

    [TestCase]
    public void EachEmotionCarriesItsOwnFrameColourDrawnFromTheSharedPalette() {
        var seen = new List<Color>();
        var allowed = new List<Color> {
            UIPalette.Cyan, UIPalette.CyanDim, UIPalette.Gold, UIPalette.GoldBright,
            UIPalette.BossRed, UIPalette.Slate, UIPalette.SlateDim, UIPalette.Warning
        };
        var issues = new List<string>();

        foreach ((string key, DialogueEmotion emotion) in AuthoredEmotions) {
            Color frame = DialogueEmotionTreatment.Resolve(key).FrameColor;
            if (seen.Exists(existing => existing.IsEqualApprox(frame))) {
                issues.Add($"{emotion} reuses a frame colour another emotion already owns");
            }
            if (!allowed.Exists(candidate => candidate.IsEqualApprox(frame))) {
                issues.Add($"{emotion} frame {frame} is not a UIPalette colour");
            }
            seen.Add(frame);
        }

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void HeightenedEmotionsDrawAThickerFrameThanTheRestingOnes() {
        AssertThat(DialogueEmotionTreatment.UrgentFrameWidth
            > DialogueEmotionTreatment.CalmFrameWidth).IsTrue();

        foreach (DialogueEmotion emotion in new[] { DialogueEmotion.Shocked, DialogueEmotion.Injured }) {
            AssertThat(DialogueEmotionTreatment.Resolve(emotion).FrameWidth)
                .OverrideFailureMessage($"{emotion} should read as urgent.")
                .IsEqual(DialogueEmotionTreatment.UrgentFrameWidth);
        }
        foreach (DialogueEmotion emotion in new[] {
            DialogueEmotion.Neutral, DialogueEmotion.Determined, DialogueEmotion.Confused
        }) {
            AssertThat(DialogueEmotionTreatment.Resolve(emotion).FrameWidth)
                .OverrideFailureMessage($"{emotion} should read as calm.")
                .IsEqual(DialogueEmotionTreatment.CalmFrameWidth);
        }
    }

    [TestCase]
    public void EveryTintStaysCloseToWhiteSoAPortraitIsNotRecoloured() {
        var issues = new List<string>();
        foreach ((string key, DialogueEmotion emotion) in AuthoredEmotions) {
            Color tint = DialogueEmotionTreatment.Resolve(key).PortraitTint;
            if (tint.R < 0.8f || tint.G < 0.8f || tint.B < 0.8f) {
                issues.Add($"{emotion} tint {tint} would recolour the portrait rather than shade it");
            }
            if (!Mathf.IsEqualApprox(tint.A, 1f)) {
                issues.Add($"{emotion} tint must stay fully opaque; the panel owns the transparency");
            }
        }
        // Neutral is the reference: no tint at all.
        AssertThat(DialogueEmotionTreatment.Resolve(DialogueEmotion.Neutral).PortraitTint
            .IsEqualApprox(Colors.White))
            .OverrideFailureMessage("Neutral must leave the portrait untinted.")
            .IsTrue();

        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }
}
