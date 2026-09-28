using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6 (G10). Text Speed replaces the fixed 30 cps reveal: Slow 20 /
/// Normal 30 / Fast 60 / Instant, with punctuation holds and chirp cadence
/// scaling with the rate. Pure model — no Godot runtime.
/// </summary>
[TestSuite]
public class TextSpeedRevealTests {

    private static int CharactersAfterOneSecond(TextSpeed speed) {
        var model = new DialogueRevealModel();
        model.SetSpeed(speed);
        model.Begin(new string('a', 200));
        for (int step = 0; step < 60; step++) model.Advance(1f / 60f);
        return model.VisibleCharacters;
    }

    [TestCase]
    public void EachSpeedRevealsAtItsDesignRate() {
        AssertThat(CharactersAfterOneSecond(TextSpeed.Slow)).IsBetween(19, 21);
        AssertThat(CharactersAfterOneSecond(TextSpeed.Normal)).IsBetween(29, 31);
        AssertThat(CharactersAfterOneSecond(TextSpeed.Fast)).IsBetween(59, 61);
        // Normal remains the published default and the model's default.
        AssertThat(new DialogueRevealModel().Speed).IsEqual(TextSpeed.Normal);
        AssertThat(DialogueRevealModel.CharactersPerSecond).IsEqual(30f);
    }

    [TestCase]
    public void InstantRevealsTheWholeLineAtOnceAndSilently() {
        var model = new DialogueRevealModel();
        model.SetSpeed(TextSpeed.Instant);
        model.Begin("Hello, world. Again!");
        AssertThat(model.IsComplete).IsTrue();
        AssertThat(model.VisibleCharacters).IsEqual(20);
        AssertThat(model.Advance(1f)).IsEqual(0);
        AssertThat(model.TotalChirps).IsEqual(0);
    }

    [TestCase]
    public void PunctuationHoldsAndChirpCadenceScaleWithTheRate() {
        var slow = new DialogueRevealModel();
        slow.SetSpeed(TextSpeed.Slow);
        var fast = new DialogueRevealModel();
        fast.SetSpeed(TextSpeed.Fast);
        var normal = new DialogueRevealModel();

        AssertThat(normal.PauseScale).IsEqualApprox(1f, 0.0001f);
        AssertThat(slow.PauseScale).IsEqualApprox(1.5f, 0.0001f);
        AssertThat(fast.PauseScale).IsEqualApprox(0.5f, 0.0001f);

        AssertThat(normal.ChirpInterval).IsEqual(DialogueRevealModel.ChirpEveryNCharacters);
        AssertThat(slow.ChirpInterval).IsEqual(2);
        AssertThat(fast.ChirpInterval).IsEqual(6);

        // A sentence hold at Fast is half the Normal hold: "Hi. X" reaches X sooner.
        static float SecondsToReveal(DialogueRevealModel model, string text) {
            model.Begin(text);
            float elapsed = 0f;
            while (!model.IsComplete && elapsed < 5f) {
                model.Advance(0.001f);
                elapsed += 0.001f;
            }
            return elapsed;
        }
        float normalTime = SecondsToReveal(normal, "Hi. There");
        float fastTime = SecondsToReveal(fast, "Hi. There");
        AssertThat(fastTime < normalTime * 0.6f)
            .OverrideFailureMessage($"fast {fastTime} vs normal {normalTime}").IsTrue();
    }
}
