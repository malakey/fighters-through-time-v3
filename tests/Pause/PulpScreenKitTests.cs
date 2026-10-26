using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pause;

/// <summary>UR-PAUSE: the pure rules the pause tag and status strip use. Pure C# (failure signature 7: no engine).</summary>
[TestSuite]
public class PulpScreenKitTests {

    [TestCase]
    public void TheTagTakesTheLevelNameBeforeItsFirstDash() {
        AssertThat(PulpScreenKit.ShortTitle("Pompeii 79 AD — Eruption of Vesuvius")).IsEqual("Pompeii 79 AD");
        AssertThat(PulpScreenKit.ShortTitle("The Chronal Void")).IsEqual("The Chronal Void");
        AssertThat(PulpScreenKit.ShortTitle("")).IsEqual("");
        AssertThat(PulpScreenKit.ShortTitle(null)).IsEqual("");
    }

    [TestCase]
    public void TheLevelClockReadsMinutesAndSecondsAndOnlyShowsHoursWhenNeeded() {
        AssertThat(PulpScreenKit.FormatClock(0f)).IsEqual("0:00");
        AssertThat(PulpScreenKit.FormatClock(125.9f)).IsEqual("2:05");
        AssertThat(PulpScreenKit.FormatClock(3725f)).IsEqual("1:02:05");
        AssertThat(PulpScreenKit.FormatClock(-4f)).IsEqual("0:00");
    }
}
