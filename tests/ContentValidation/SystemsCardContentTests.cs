using System.Collections.Generic;
using System.IO;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// V7.3 Fighter Onboarding: the universal Systems Card — one static localized
/// page, eight sections, one sentence plus one number each. Mirrors
/// <c>CampaignLocalizationTests</c>' idiom: every key must exist in
/// localization/en.csv AND resolve through the compiled
/// localization/en.en.translation (`--headless --import` regenerates it).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SystemsCardContentTests {

    /// <summary>The eight designed sections, title + body key per section.</summary>
    private static readonly string[] SectionStems = {
        "block", "triangle", "di", "tech", "rally", "defy", "echo", "overtime"
    };

    [TestCase]
    public void EverySystemsCardKeyExistsInTheCsvAndResolvesThroughTheCompiledTranslation() {
        TranslationServer.SetLocale("en");
        HashSet<string> csvKeys = EnglishTranslationKeys();

        var missingFromCsv = new List<string>();
        var unresolved = new List<string>();
        var allKeys = new List<string> { "systems_card_title" };
        foreach (string stem in SectionStems) {
            allKeys.Add($"systems_card_{stem}_title");
            allKeys.Add($"systems_card_{stem}_body");
        }

        foreach (string key in allKeys) {
            if (!csvKeys.Contains(key)) missingFromCsv.Add(key);
            if (TranslationServer.Translate(key).ToString() == key) unresolved.Add(key);
        }

        AssertThat(missingFromCsv.Count).OverrideFailureMessage(
            "Systems Card keys missing from localization/en.csv: " + string.Join(", ", missingFromCsv))
            .IsEqual(0);
        AssertThat(unresolved.Count).OverrideFailureMessage(
            "Systems Card keys that do not resolve through localization/en.en.translation " +
            "(run `--headless --import` and commit the regenerated en.en.translation): " +
            string.Join(", ", unresolved))
            .IsEqual(0);
    }

    /// <summary>
    /// The authored scene carries all eight sections as raw-key labels (Godot's
    /// control auto-translation resolves them) and no font-size overrides — the
    /// UI-scale contract for a screen born after the V7.3 type-variation pass.
    /// </summary>
    [TestCase]
    public void TheAuthoredSceneCarriesAllEightSectionsWithNoFontSizeOverrides() {
        var screen = new SystemsCardScreen { Name = "SystemsCardUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(screen);
        try {
            AssertObject(screen.Root).IsNotNull();
            var sections = screen.Root.GetNode<VBoxContainer>("Center/Panel/Layout/Scroll/Sections");
            var problems = new List<string>();
            foreach (string stem in SectionStems) {
                // BlockTitle/BlockBody naming; "di" capitalizes to "Di".
                string pascal = char.ToUpperInvariant(stem[0]) + stem[1..];
                var title = sections.GetNodeOrNull<Label>($"{pascal}Title");
                var body = sections.GetNodeOrNull<Label>($"{pascal}Body");
                if (title == null || title.Text != $"systems_card_{stem}_title") {
                    problems.Add($"{pascal}Title missing or not keyed");
                }
                if (body == null || body.Text != $"systems_card_{stem}_body") {
                    problems.Add($"{pascal}Body missing or not keyed");
                }
                if (title != null && title.HasThemeFontSizeOverride("font_size")) {
                    problems.Add($"{pascal}Title has a font-size override");
                }
                if (body != null && body.HasThemeFontSizeOverride("font_size")) {
                    problems.Add($"{pascal}Body has a font-size override");
                }
            }
            var cardTitle = screen.Root.GetNodeOrNull<Label>("Center/Panel/Layout/Title");
            if (cardTitle == null || cardTitle.Text != "systems_card_title") {
                problems.Add("card Title missing or not keyed");
            }
            if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
        } finally {
            screen.Free();
        }
    }

    private static HashSet<string> EnglishTranslationKeys() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }
        return keys;
    }
}
