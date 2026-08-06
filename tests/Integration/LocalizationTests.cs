using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Integration;

[TestSuite]
[RequireGodotRuntime]
public class LocalizationTests {
    [TestCase]
    public void EnglishMenuKeysResolveToDisplayText() {
        TranslationServer.SetLocale("en");

        AssertString(TranslationServer.Translate("game_title")).IsEqual("FIGHTERS THROUGH TIME");
        AssertString(TranslationServer.Translate("menu_story_mode")).IsEqual("Story Mode");
        AssertString(TranslationServer.Translate("menu_fighter_mode")).IsEqual("Fighter Mode");
        AssertString(TranslationServer.Translate("menu_settings")).IsEqual("Settings");
        AssertString(TranslationServer.Translate("menu_quit")).IsEqual("Quit Game");
    }
}
