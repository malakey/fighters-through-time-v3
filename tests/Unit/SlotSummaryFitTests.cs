using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// TG-CLOSE polish. The save slot summary's fit order (<see cref="SlotSummaryFit"/>) as plain C#: the role size when the line
/// fits; else the largest size down to the 18 px floor; else two lines at the role size when the slab holds them; else the
/// level's short title, shrunk if need be; and at worst the short form at the floor. The real menu, all sixteen titles at
/// UI scale 1.0 and 1.4, is <c>MainMenuSceneTests.EverySlotSummaryReadsWholeForEveryLevelTitleAndTheWidestHeroAtBothUiScales</c>.
/// Pure: no Godot type is touched, so this suite needs no runtime (CLAUDE.md failure signature 7).
/// </summary>
[TestSuite]
public class SlotSummaryFitTests {

    // A fake measure: every character is half the font size wide.
    private static float Measure(string text, int size) => text.Length * size * 0.5f;

    [TestCase]
    public void TheLineShrinksThenWrapsThenTakesTheShortTitleInThatOrder() {
        string full = new string('x', 40), shortForm = new string('x', 20);
        // 40 characters at 22 px are 440 px: they fit a 500 px line at the role size.
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 500f, Measure, _ => 1, false))
            .IsEqual(new SlotSummaryFit.Choice(full, 22, false));
        // In 400 px they shrink to 20 px (400), not to the floor.
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 400f, Measure, _ => 2, true))
            .IsEqual(new SlotSummaryFit.Choice(full, 20, false));
        // In 300 px even 18 px (360) does not fit: two lines at the role size when the slab holds them...
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 300f, Measure, _ => 2, true))
            .IsEqual(new SlotSummaryFit.Choice(full, 22, true));
        // ...but not when the text needs three lines, nor when the slab has no room: then the short title, at its own
        // largest fitting size (20 characters at 22 px are 220 px).
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 300f, Measure, _ => 3, true))
            .IsEqual(new SlotSummaryFit.Choice(shortForm, 22, false));
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 300f, Measure, _ => 2, false))
            .IsEqual(new SlotSummaryFit.Choice(shortForm, 22, false));
        // A short form that itself needs shrinking shrinks; one too long even at the floor keeps the floor (the ellipsis
        // is the last resort).
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 190f, Measure, _ => 9, false))
            .IsEqual(new SlotSummaryFit.Choice(shortForm, 19, false));
        AssertThat(SlotSummaryFit.Choose(full, shortForm, 22, 18, 100f, Measure, _ => 9, false))
            .IsEqual(new SlotSummaryFit.Choice(shortForm, 18, false));
        // No short form (a title with no dash): the full line stands in for it.
        AssertThat(SlotSummaryFit.Choose(full, "", 22, 18, 100f, Measure, _ => 9, false))
            .IsEqual(new SlotSummaryFit.Choice(full, 18, false));
        // A role size under the floor is never raised to it.
        AssertThat(SlotSummaryFit.Choose("abc", "", 16, 18, 100f, Measure, _ => 1, false))
            .IsEqual(new SlotSummaryFit.Choice("abc", 16, false));
    }
}
