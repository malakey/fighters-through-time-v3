using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6. The main menu's new surfaces: G15e Extras (a Credits replay
/// that never completes a campaign, and the Third-Party Licenses list), G15d's
/// version corner, the G06 boot sequence (photosensitivity notice + first-run
/// setup) and the G14 slot-card line.
///
/// <para>Save discipline: global flags touched by the boot overlay are
/// snapshotted and restored (and re-persisted) in finally blocks; story slots are
/// swapped in memory only.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MainMenuExtrasTests {

    private const string ScenePath = "res://scenes/menus/MainMenu.tscn";
    private const string RootLayout = "RootScreen/Center/Panel/Layout/";
    private const string ExtrasLayout = "ExtrasScreen/Center/Panel/Layout/";

    [TestCase]
    public void ExtrasSitsBetweenSettingsAndQuitAndLeadsToCreditsAndLicenses() {
        MainMenu menu = Open(out Node host);
        try {
            var layout = menu.GetNode<VBoxContainer>("RootScreen/Center/Panel/Layout");
            int settings = layout.GetNode<Button>("SettingsButton").GetIndex();
            int extras = layout.GetNode<Button>("ExtrasButton").GetIndex();
            int quit = layout.GetNode<Button>("QuitButton").GetIndex();
            AssertThat(extras > settings && extras < quit).IsTrue();

            Press(menu, RootLayout + "ExtrasButton");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Extras);
            Press(menu, ExtrasLayout + "LicensesButton");
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Licenses);
            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Extras);
            menu.GoBack();
            AssertThat(menu.CurrentScreen).IsEqual(MainMenuScreen.Root);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheLicensesScreenListsEveryBundledComponentWithItsLicense() {
        TranslationServer.SetLocale("en");
        MainMenu menu = Open(out Node host);
        try {
            var rows = menu.GetNode<VBoxContainer>("LicensesScreen/Center/Panel/Layout/Scroll/Rows");
            AssertThat(rows.GetChildCount()).IsEqual(ThirdPartyLicenses.Notices.Length);
            string all = string.Join("\n", rows.GetChildren().OfType<Button>().Select(button => button.Text));
            foreach (string product in new[] {
                         "Godot Engine", "Klotho", "K4os.Compression.LZ4", "Newtonsoft.Json", "LiteNetLib", "GdUnit4" }) {
                AssertThat(all.Contains(product)).OverrideFailureMessage($"{product} missing").IsTrue();
            }
            AssertThat(all.Contains(TranslationServer.Translate("license_name_apache2").ToString())).IsTrue();
            AssertThat(all.Contains(TranslationServer.Translate("license_name_mit").ToString())).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ReplayingTheCreditsNeverMarksACampaignCompleted() {
        const int scratch = 2;
        SaveManager saves = SaveManager.Instance;
        StorySaveData original = saves.SaveSlots[scratch];
        SessionData session = GameManager.Instance.CurrentSession;
        MainMenu menu = Open(out Node host);
        try {
            var save = new StorySaveData { SelectedCharacterID = "joan", IsCompleted = false };
            saves.SaveSlots[scratch] = save;
            SessionData active = session;
            active.ActiveSaveSlot = scratch;
            GameManager.Instance.CurrentSession = active;

            CreditsController credits = menu.ReplayCredits();
            AssertObject(credits).IsNotNull();
            credits.Skip();
            AssertThat(credits.IsFinished).IsTrue();
            AssertThat(save.IsCompleted)
                .OverrideFailureMessage("An Extras replay must never complete the campaign.").IsFalse();
        } finally {
            saves.SaveSlots[scratch] = original;
            GameManager.Instance.CurrentSession = session;
            Teardown(host);
        }
    }

    [TestCase]
    public void TheVersionCornerShowsTheCanonicalBuildVersion() {
        MainMenu menu = Open(out Node host);
        try {
            var label = menu.GetNode<Label>("VersionLabel");
            AssertThat(label.Text.Contains(BuildInfo.Version)).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheSlotCardRecordsTheLowestDifficultyUsed() {
        TranslationServer.SetLocale("en");
        MainMenu menu = Open(out Node host);
        try {
            string easy = TranslationServer.Translate("difficulty_easy").ToString();
            string line = menu.LowestDifficultyLine(new StorySaveData {
                Difficulty = Difficulty.Easy, LowestDifficultyUsed = Difficulty.Easy
            });
            AssertThat(line.Contains(easy)).IsTrue();
            // A pre-field payload (initializer Hard) reads back as its own tier.
            string normal = TranslationServer.Translate("difficulty_normal").ToString();
            AssertThat(menu.LowestDifficultyLine(new StorySaveData { Difficulty = Difficulty.Normal }).Contains(normal))
                .IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void HostedMenusNeverRaiseTheBootSequence() {
        MainMenu menu = Open(out Node host);
        try {
            AssertObject(menu.BootOverlay).IsNull();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheFirstPhotosensitivityNoticeCannotBeSkippedAndAdvancesAfterThreeSeconds() {
        GlobalSaveData data = SaveManager.Instance.GlobalData;
        bool seen = data.PhotosensitivityNoticeSeen;
        bool setup = data.FirstRunSetupCompleted;
        MainMenu.BootSequenceOverrideForTesting = true;
        CrashReportService.ConsumePrompt();
        Node host = null;
        try {
            data.PhotosensitivityNoticeSeen = false;
            data.FirstRunSetupCompleted = true;
            MainMenu menu = Open(out host);
            BootSequenceOverlay overlay = menu.BootOverlay;
            AssertObject(overlay).IsNotNull();
            AssertThat(overlay.CurrentStep).IsEqual(BootStep.PhotosensitivityNotice);
            AssertThat(overlay.NoticeSkippable).IsFalse();
            AssertThat(overlay.ContinueButton.Disabled).IsTrue();
            AssertThat(overlay.AdvanceFromNotice(force: false)).IsFalse();

            overlay.AdvanceTime(1.5f);
            AssertThat(overlay.CurrentStep).IsEqual(BootStep.PhotosensitivityNotice);
            overlay.AdvanceTime(1.6f);
            AssertThat(overlay.CurrentStep).IsEqual(BootStep.Done);
            AssertThat(data.PhotosensitivityNoticeSeen).IsTrue();
        } finally {
            MainMenu.BootSequenceOverrideForTesting = null;
            data.PhotosensitivityNoticeSeen = seen;
            data.FirstRunSetupCompleted = setup;
            SaveManager.Instance.SaveGlobalData();
            if (host != null) Teardown(host);
        }
    }

    [TestCase]
    public void ASeenNoticeIsSkippableAndAFirstRunShowsTheSetupWhoseSkipKeepsDefaults() {
        GlobalSaveData data = SaveManager.Instance.GlobalData;
        bool seen = data.PhotosensitivityNoticeSeen;
        bool setup = data.FirstRunSetupCompleted;
        bool comfort = data.ReducedTemporalEffects;
        float scale = data.UiScale;
        MainMenu.BootSequenceOverrideForTesting = true;
        CrashReportService.ConsumePrompt();
        Node host = null;
        try {
            data.PhotosensitivityNoticeSeen = true;
            data.FirstRunSetupCompleted = false;
            data.ReducedTemporalEffects = false;
            MainMenu menu = Open(out host);
            BootSequenceOverlay overlay = menu.BootOverlay;
            AssertThat(overlay.NoticeSkippable).IsTrue();
            AssertThat(overlay.AdvanceFromNotice(force: false)).IsTrue();
            AssertThat(overlay.CurrentStep).IsEqual(BootStep.FirstRunSetup);
            AssertObject(overlay.ReducedEffectsToggle).IsNotNull();
            AssertObject(overlay.UiScaleSlider).IsNotNull();
            AssertObject(overlay.ControllerLabel).IsNotNull();

            // The comfort card applies live, before anything is saved.
            overlay.ReducedEffectsToggle.ButtonPressed = true;
            AssertThat(ComfortSettings.ReducedTemporalEffects).IsTrue();

            overlay.SkipFirstRunSetup();
            AssertThat(ComfortSettings.ReducedTemporalEffects).IsFalse();
            AssertThat(data.ReducedTemporalEffects).IsFalse();
            AssertThat(data.FirstRunSetupCompleted).IsTrue();
            AssertThat(overlay.CurrentStep).IsEqual(BootStep.Done);
        } finally {
            MainMenu.BootSequenceOverrideForTesting = null;
            data.PhotosensitivityNoticeSeen = seen;
            data.FirstRunSetupCompleted = setup;
            data.ReducedTemporalEffects = comfort;
            data.UiScale = scale;
            data.ApplyComfortSettings();
            UIPalette.ApplyUiScale(scale);
            SaveManager.Instance.SaveGlobalData();
            if (host != null) Teardown(host);
        }
    }

    [TestCase]
    public void TheBootPlanOrdersNoticeSetupAndTheAskOnlyCrashPrompt() {
        AssertThat(BootSequencePlan.Steps(true, false).ToArray())
            .IsEqual(new[] { BootStep.PhotosensitivityNotice, BootStep.Done });
        AssertThat(BootSequencePlan.Steps(false, true).ToArray()).IsEqual(new[] {
            BootStep.PhotosensitivityNotice, BootStep.FirstRunSetup, BootStep.CrashPrompt, BootStep.Done
        });
        AssertThat(BootSequencePlan.NoticeSeconds).IsEqual(3f);
    }

    private static void Press(MainMenu menu, string path) =>
        menu.GetNode<Button>(path).EmitSignal(BaseButton.SignalName.Pressed);

    private static MainMenu Open(out Node host) {
        host = new Node { Name = "MainMenuExtrasHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var menu = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<MainMenu>();
        host.AddChild(menu);
        return menu;
    }

    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
