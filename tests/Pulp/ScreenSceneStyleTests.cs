using System.Collections.Generic;
using System.IO;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-SCREENS. The restyled scenes carry no per-node font size and no chrome cyan; roles and the palette do that work.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ScreenSceneStyleTests {

    private static readonly string[] Scenes = {
        "scenes/ui/LevelResults.tscn", "scenes/ui/MatchResults.tscn", "scenes/ui/LoadingScreen.tscn",
        "scenes/ui/StoryLoading.tscn", "scenes/ui/FighterLoading.tscn", "scenes/ui/GameOver.tscn",
        "scenes/ui/Credits.tscn", "scenes/ui/ResonanceGrid.tscn", "scenes/ui/CalibrationDrillPicker.tscn",
        "scenes/ui/CalibrationDrillList.tscn", "scenes/ui/HolodeckDrill.tscn", "scenes/menus/FighterPlayOptions.tscn",
        "scenes/menus/NetworkSelect.tscn"
    };

    [TestCase]
    public void NoRestyledSceneOverridesAFontSizeOrPaintsChromeCyan() {
        var offenders = new List<string>();
        foreach (string scene in Scenes) {
            string path = ProjectSettings.GlobalizePath("res://" + scene);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++) {
                if (lines[i].Contains("theme_override_font_sizes/font_size")
                    || lines[i].Contains("Color(0, 0.9, 0.9") || lines[i].Contains("temporal_glass_")) {
                    offenders.Add(scene + ":" + (i + 1));
                }
            }
        }
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }
}
