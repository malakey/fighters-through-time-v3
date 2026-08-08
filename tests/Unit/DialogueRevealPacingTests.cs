using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B3. The typewriter reveal model: punctuation pacing, chirp cadence,
/// and hitch containment.
///
/// <para>Pure C# on purpose — the model has no Godot dependency, so a case can
/// step it by exact deltas instead of waiting on engine frames, and the
/// behaviour that actually needs pinning (how long a full stop holds, how often
/// a chirp fires) is arithmetic rather than presentation.</para>
///
/// <para>Deltas in these cases carry deliberate slack around the 1/30 s
/// character cost. Asking for exactly three characters' worth of time is a
/// coin flip on float rounding; every case here asks for a little more and
/// asserts the character count it must have reached.</para>
/// </summary>
[TestSuite]
public class DialogueRevealPacingTests {

    private static DialogueRevealModel Line(string text) {
        var model = new DialogueRevealModel();
        model.Begin(text);
        return model;
    }

    [TestCase]
    public void TheRevealHoldsTheDesignLockedThirtyCharactersPerSecond() {
        AssertThat(DialogueRevealModel.CharactersPerSecond).IsEqual(30f);
        AssertThat(DialogueManager.CharactersPerSecond)
            .OverrideFailureMessage("DialogueManager must publish the model's rate, not a second copy of it.")
            .IsEqual(DialogueRevealModel.CharactersPerSecond);

        DialogueRevealModel model = Line("abcdefghij");
        // 0.25 s at 30 cps is 7.5 characters; a partial character never appears.
        AssertThat(model.Advance(0.25f)).IsEqual(2);
        AssertThat(model.VisibleCharacters).IsEqual(7);
        AssertThat(model.IsComplete).IsFalse();
    }

    [TestCase]
    public void AnEmptyLineBeginsCompleteAndNeverChirps() {
        DialogueRevealModel model = Line("");
        AssertThat(model.IsComplete).IsTrue();
        AssertThat(model.TotalCharacters).IsEqual(0);
        AssertThat(model.Advance(1f)).IsEqual(0);
        AssertThat(model.IsPausedOnPunctuation).IsFalse();

        DialogueRevealModel nullLine = Line(null);
        AssertThat(nullLine.IsComplete).IsTrue();
        AssertThat(nullLine.Advance(1f)).IsEqual(0);
    }

    [TestCase]
    public void PunctuationHoldsTheRevealAndASentenceHoldsLongerThanAClause() {
        AssertThat(DialogueRevealModel.PauseAfter('.'))
            .IsEqual(DialogueRevealModel.SentencePauseSeconds);
        AssertThat(DialogueRevealModel.PauseAfter('!'))
            .IsEqual(DialogueRevealModel.SentencePauseSeconds);
        AssertThat(DialogueRevealModel.PauseAfter('?'))
            .IsEqual(DialogueRevealModel.SentencePauseSeconds);
        AssertThat(DialogueRevealModel.PauseAfter(','))
            .IsEqual(DialogueRevealModel.ClausePauseSeconds);
        AssertThat(DialogueRevealModel.PauseAfter(';'))
            .IsEqual(DialogueRevealModel.ClausePauseSeconds);
        AssertThat(DialogueRevealModel.PauseAfter('a')).IsEqual(0f);
        AssertThat(DialogueRevealModel.SentencePauseSeconds > DialogueRevealModel.ClausePauseSeconds)
            .IsTrue();

        DialogueRevealModel sentence = Line("ab.cd");
        DialogueRevealModel clause = Line("ab,cd");
        sentence.Advance(0.11f);
        clause.Advance(0.11f);
        AssertThat(sentence.VisibleCharacters).IsEqual(3);
        AssertThat(clause.VisibleCharacters).IsEqual(3);
        AssertThat(sentence.IsPausedOnPunctuation)
            .OverrideFailureMessage("The reveal must hold on the full stop before the next clause.")
            .IsTrue();

        // The same amount of time afterwards: the clause resumes, the sentence
        // is still holding.
        sentence.Advance(0.2f);
        clause.Advance(0.2f);
        AssertThat(clause.VisibleCharacters > sentence.VisibleCharacters)
            .OverrideFailureMessage(
                $"A comma must resume sooner than a full stop (clause {clause.VisibleCharacters}, " +
                $"sentence {sentence.VisibleCharacters}).")
            .IsTrue();
        AssertThat(sentence.VisibleCharacters).IsEqual(3);
    }

    [TestCase]
    public void ARunOfMarksHoldsOnceAtItsEndRatherThanOncePerMark() {
        // Twenty-two authored lines use "..."; three stacked sentence holds
        // there would read as a stall rather than as a beat.
        DialogueRevealModel ellipsis = Line("a...b");
        ellipsis.Advance(0.15f);
        AssertThat(ellipsis.VisibleCharacters)
            .OverrideFailureMessage("An ellipsis must reveal as one mark, not hold between its dots.")
            .IsEqual(4);
        AssertThat(ellipsis.IsPausedOnPunctuation)
            .OverrideFailureMessage("The hold belongs after the last dot of the run.")
            .IsTrue();
    }

    [TestCase]
    public void ChirpsFireEveryThirdNonWhitespaceCharacterAndStopWhileHolding() {
        AssertThat(DialogueRevealModel.ChirpEveryNCharacters).IsEqual(3);

        // Whitespace is silent: eleven characters, six of them letters, two chirps.
        DialogueRevealModel spaced = Line("a b c d e f");
        while (!spaced.IsComplete) spaced.Advance(DialogueRevealModel.MaxAdvanceSeconds);
        AssertThat(spaced.TotalChirps)
            .OverrideFailureMessage($"Six letters must produce two chirps, not {spaced.TotalChirps}.")
            .IsEqual(2);

        // A punctuation hold is silence, not a slower ticker.
        DialogueRevealModel holding = Line("ab.cdefgh");
        holding.Advance(0.11f);
        AssertThat(holding.IsPausedOnPunctuation).IsTrue();
        AssertThat(holding.Advance(0.05f))
            .OverrideFailureMessage("No chirp may fire while the reveal is holding on punctuation.")
            .IsEqual(0);
    }

    [TestCase]
    public void AFrameHitchCannotFlushTheWholeLineOrBurstItsChirps() {
        // A scene load or pool warm-up used to dump an entire line in one frame.
        // With chirps attached that is also a burst of voices in one frame.
        DialogueRevealModel model = Line("abcdefghijklmnopqrst");
        int chirps = model.Advance(10f);
        AssertThat(model.VisibleCharacters)
            .OverrideFailureMessage(
                $"A 10 s frame must advance at most {DialogueRevealModel.MaxAdvanceSeconds} s of reveal.")
            .IsEqual(7);
        AssertThat(chirps).IsEqual(2);
        AssertThat(model.IsComplete).IsFalse();
    }

    [TestCase]
    public void ConfirmToCompleteLandsTheWholeLineSilently() {
        DialogueRevealModel model = Line("Hello, traveler. The sky tore apart!");
        model.Advance(0.1f);
        int chirpsBefore = model.TotalChirps;

        model.CompleteImmediately();

        AssertThat(model.IsComplete).IsTrue();
        AssertThat(model.VisibleCharacters).IsEqual(model.TotalCharacters);
        AssertThat(model.IsPausedOnPunctuation).IsFalse();
        AssertThat(model.TotalChirps)
            .OverrideFailureMessage("Skipping the reveal must not fire the chirps it skipped.")
            .IsEqual(chirpsBefore);
        AssertThat(model.Advance(1f)).IsEqual(0);
    }

    [TestCase]
    public void BeginningANewLineResetsEveryPieceOfRevealState() {
        DialogueRevealModel model = Line("ab.cdef");
        model.Advance(0.11f);
        AssertThat(model.IsPausedOnPunctuation).IsTrue();
        AssertThat(model.TotalChirps).IsEqual(1);

        model.Begin("xyz");
        AssertThat(model.VisibleCharacters).IsEqual(0);
        AssertThat(model.TotalChirps).IsEqual(0);
        AssertThat(model.IsPausedOnPunctuation)
            .OverrideFailureMessage("A held pause must not survive into the next line.")
            .IsFalse();
        AssertThat(model.TotalCharacters).IsEqual(3);
    }
}
