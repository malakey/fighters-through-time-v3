using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A8 / F13. The Defy History seal's four states.
///
/// <para>The seal answers one question — can this fighter still cheat death once —
/// and the two ways to get it wrong are both about state that looks recoverable
/// and is not. A spent Defy must stay visibly broken however full the meter gets
/// back, and a mode disable (Sudden Death) must not quietly repair the spent flag
/// underneath it.</para>
///
/// <para>Pure resolver, so this needs no scene tree: the state is always
/// recomputable from meter, used, life and mode, which is exactly why it is
/// derived after the gameplay update instead of being persisted as a cosmetic
/// flag that a load could replay.</para>
/// </summary>
[TestSuite]
public class DefySealTests {

    [TestCase]
    public void AnUnusedSealBuildsUntilTheMeterIsFullAndThenReadsReady() {
        AssertThat(DefySealModel.Resolve(0f, used: false, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Building);
        AssertThat(DefySealModel.Resolve(99.9f, used: false, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Building);
        AssertThat(DefySealModel.Resolve(100f, used: false, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Ready);
    }

    /// <summary>
    /// "Spent (visibly broken at any meter value after the proc — refilling never
    /// repairs)". A naive `meter >= 100 ? Ready : Building` gets this exactly
    /// backwards, and the player reads it as a second free life.
    /// </summary>
    [TestCase]
    public void SpentSurvivesAFullRefill() {
        AssertThat(DefySealModel.Resolve(0f, used: true, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Spent);
        AssertThat(DefySealModel.Resolve(100f, used: true, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Spent);
    }

    /// <summary>
    /// F22 disables Defy throughout Sudden Death regardless of meter, and a dead
    /// fighter has no seal to light. Both are Barred — and neither may clear the
    /// spent flag underneath, so leaving the phase restores Spent, not Ready.
    /// </summary>
    [TestCase]
    public void BarredTakesPrecedenceWithoutClearingSpent() {
        AssertThat(DefySealModel.Resolve(100f, used: false, alive: true, modeDisabled: true))
            .IsEqual(DefySealState.Barred);
        AssertThat(DefySealModel.Resolve(100f, used: false, alive: false, modeDisabled: false))
            .IsEqual(DefySealState.Barred);
        AssertThat(DefySealModel.Resolve(100f, used: true, alive: true, modeDisabled: true))
            .IsEqual(DefySealState.Barred);

        // Leaving the phase with the proc already spent must NOT read Ready.
        AssertThat(DefySealModel.Resolve(100f, used: true, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Spent);
    }

    /// <summary>
    /// The seal is derived, never stored, so a rollback that restores an unspent
    /// Defy restores an intact seal for free and a load cannot replay the proc.
    /// Recomputing from the same four inputs must give the same answer every time.
    /// </summary>
    [TestCase]
    public void RecomputingFromRestoredStateIsStable() {
        for (int pass = 0; pass < 3; pass++) {
            AssertThat(DefySealModel.Resolve(100f, used: false, alive: true, modeDisabled: false))
                .IsEqual(DefySealState.Ready);
        }
        // A rollback to before the proc restores used = false, and the seal
        // follows the restored state rather than a latched cosmetic flag.
        AssertThat(DefySealModel.Resolve(60f, used: true, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Spent);
        AssertThat(DefySealModel.Resolve(60f, used: false, alive: true, modeDisabled: false))
            .IsEqual(DefySealState.Building);
    }

    /// <summary>
    /// "Shape and fill must distinguish states, not colour alone." Four distinct
    /// glyphs, four localized labels, and no pulsing anywhere in the model.
    /// </summary>
    [TestCase]
    public void EveryStateHasItsOwnGlyphAndLocalizedLabel() {
        var glyphs = new System.Collections.Generic.HashSet<string>();
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (DefySealState state in System.Enum.GetValues<DefySealState>()) {
            AssertThat(glyphs.Add(DefySealModel.Glyph(state)))
                .OverrideFailureMessage($"{state} shares a glyph with another state.").IsTrue();
            AssertThat(keys.Add(DefySealModel.LabelKey(state)))
                .OverrideFailureMessage($"{state} shares a label key with another state.").IsTrue();
        }
        AssertThat(glyphs.Count).IsEqual(4);
        AssertThat(DefySealModel.SealSize).IsEqual(20);
    }
}
