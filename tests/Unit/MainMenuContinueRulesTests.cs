using FTT.Core;
using FTT.UI;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-MENU. The pure rules behind the Continue sticker: which save is the most recent, the chapter number,
/// and the short place line. <c>LevelOf</c> reads <c>StoryManager</c>'s route table, so the suite runs with
/// the engine.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuContinueRulesTests {

    private static StorySaveData At(string stamp, string scene = "res://scenes/campaign/Level_00_Tutorial.tscn") =>
        new() { LastSavedTimestamp = stamp, CurrentLevelID = scene };

    [TestCase]
    public void TheGreatestTimestampWinsAndATieGoesToTheLowerSlot() {
        var slots = new[] { At("2026-10-01T00:00:00Z"), At("2026-10-03T00:00:00Z"), At("2026-10-02T00:00:00Z") };
        AssertThat(MainMenuContinue.PickMostRecent(slots)).IsEqual(1);
        var tie = new[] { null, At("2026-10-03T00:00:00Z"), At("2026-10-03T00:00:00Z") };
        AssertThat(MainMenuContinue.PickMostRecent(tie)).IsEqual(1);
    }

    [TestCase]
    public void NoSaveReadsAsMinusOneAndAnUnparsableStampStillCountsAsASave() {
        AssertThat(MainMenuContinue.PickMostRecent(new StorySaveData[3])).IsEqual(-1);
        AssertThat(MainMenuContinue.PickMostRecent(null)).IsEqual(-1);
        AssertThat(MainMenuContinue.PickMostRecent(new[] { null, At("not a time"), null })).IsEqual(1);
        // A parsed stamp always beats an unparsable one.
        AssertThat(MainMenuContinue.PickMostRecent(new[] { At(""), At("2026-10-03T00:00:00Z"), null })).IsEqual(1);
    }

    [TestCase]
    public void ChapterAndPlaceComeFromTheSavedLevel() {
        StorySaveData pompeii = At("", "res://scenes/campaign/Level_06_Pompeii.tscn");
        AssertThat(MainMenuContinue.ChapterNumber(pompeii)).IsEqual(6);
        AssertThat(MainMenuContinue.LevelOf(pompeii)).IsEqual(CampaignLevel.Pompeii);
        AssertThat(MainMenuContinue.PlaceKey(pompeii)).IsEqual("campaign_level_pompeii");
        AssertThat(MainMenuContinue.ShortPlace("Pompeii 79 AD — Eruption of Vesuvius")).IsEqual("Pompeii 79 AD");
        AssertThat(MainMenuContinue.ShortPlace("The Chronal Void")).IsEqual("The Chronal Void");
        AssertThat(MainMenuContinue.ShortPlace("")).IsEqual("");
    }
}
