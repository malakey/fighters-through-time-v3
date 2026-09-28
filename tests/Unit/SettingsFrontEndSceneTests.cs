using System.Linq;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W6. The Settings rows W6 added or moved: M27 per-category mutes and
/// Mute When Unfocused (Audio), G10 Text Speed and G09 About &amp; Privacy
/// (Gameplay), G13 Block Mode and the stick sliders plus M27's relocated Vibration
/// rows (Controls). Settings never touches <see cref="SceneTree.Paused"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SettingsFrontEndSceneTests {

    [TestCase]
    public void TheAudioTabCarriesFourCategoryMutesAndMuteWhenUnfocused() {
        Node host = NewHost("SettingsAudioMutesHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            foreach (string toggle in new[] {
                         "MasterMuteToggle", "MusicMuteToggle", "SfxMuteToggle", "UiMuteToggle", "MuteWhenUnfocusedToggle" }) {
                AssertObject(menu.Tabs.GetNodeOrNull<CheckButton>($"Audio/{toggle}"))
                    .OverrideFailureMessage($"Audio/{toggle} missing").IsNotNull();
            }
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void VibrationMovedFromGameplayToControlsBesideBlockModeAndTheStickSliders() {
        Node host = NewHost("SettingsControlsRowsHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            AssertObject(menu.Tabs.GetNodeOrNull<CheckButton>("Gameplay/HapticToggle")).IsNull();
            AssertObject(menu.Tabs.GetNodeOrNull<HSlider>("Gameplay/HapticSlider")).IsNull();
            foreach (string path in new[] {
                         "Controls/Options/HapticToggle", "Controls/Options/HapticSlider",
                         "Controls/Options/BlockModeDropdown", "Controls/Options/StickDeviceDropdown",
                         "Controls/Options/DeadzoneSlider", "Controls/Options/DownThresholdSlider" }) {
                AssertObject(menu.Tabs.GetNodeOrNull<Control>(path))
                    .OverrideFailureMessage($"{path} missing").IsNotNull();
            }
            var deadzone = menu.Tabs.GetNode<HSlider>("Controls/Options/DeadzoneSlider");
            AssertThat((float)deadzone.MaxValue).IsEqualApprox(StickProfile.MaxDeadzone, 0.0001f);
            AssertThat(menu.Tabs.GetNode<OptionButton>("Controls/Options/BlockModeDropdown").ItemCount).IsEqual(2);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void TheGameplayTabCarriesTextSpeedAndAboutAndPrivacyWithTheBuildVersion() {
        TranslationServer.SetLocale("en");
        Node host = NewHost("SettingsAboutHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            var speed = menu.Tabs.GetNode<OptionButton>("Gameplay/TextSpeedRow/TextSpeedDropdown");
            AssertThat(speed.ItemCount).IsEqual(4);
            var crash = menu.Tabs.GetNode<OptionButton>("Gameplay/CrashReportsRow/CrashReportsDropdown");
            AssertThat(crash.ItemCount).IsEqual(3);
            AssertObject(menu.Tabs.GetNodeOrNull<Button>("Gameplay/CrashReportsRow/OpenLogsButton")).IsNotNull();
            AssertThat(menu.Tabs.GetNode<Label>("Gameplay/PrivacyText").Text).IsEqual("settings_privacy_notice");
            AssertThat(menu.Tabs.GetNode<Label>("Gameplay/VersionLabel").Text.Contains(BuildInfo.Version)).IsTrue();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ClosingPersistsTheNewSettingsIntoTheGlobalPayload() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) return;
        TextSpeed speed = data.DialogueTextSpeed;
        CrashReportMode crash = data.CrashReports;
        BlockMode block = data.BlockInputMode;
        bool unfocused = data.MuteWhenUnfocused;
        bool sfxMuted = data.SFXMuted;
        var profiles = data.StickProfiles.ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
        Node host = NewHost("SettingsPersistHost");
        try {
            SettingsMenu menu = OpenMenu(host);
            menu.Tabs.GetNode<OptionButton>("Gameplay/TextSpeedRow/TextSpeedDropdown").Selected = 2;
            menu.Tabs.GetNode<OptionButton>("Gameplay/CrashReportsRow/CrashReportsDropdown").Selected = 2;
            menu.Tabs.GetNode<OptionButton>("Controls/Options/BlockModeDropdown").Selected = 1;
            menu.Tabs.GetNode<CheckButton>("Audio/MuteWhenUnfocusedToggle").ButtonPressed = false;
            menu.Tabs.GetNode<CheckButton>("Audio/SfxMuteToggle").ButtonPressed = true;
            menu.Tabs.GetNode<HSlider>("Controls/Options/DeadzoneSlider").Value = 0.3;
            menu.Close();

            AssertThat(data.DialogueTextSpeed).IsEqual(TextSpeed.Fast);
            AssertThat(data.CrashReports).IsEqual(CrashReportMode.Never);
            AssertThat(data.BlockInputMode).IsEqual(BlockMode.Toggle);
            AssertThat(data.MuteWhenUnfocused).IsFalse();
            AssertThat(data.SFXMuted).IsTrue();
            AssertThat(StickProfiles.Resolve(data.StickProfiles, "any").Deadzone).IsEqualApprox(0.3f, 0.001f);
            AssertThat(InputManager.Instance?.CurrentBlockMode() ?? BlockMode.Toggle).IsEqual(BlockMode.Toggle);
        } finally {
            data.DialogueTextSpeed = speed;
            data.CrashReports = crash;
            data.BlockInputMode = block;
            data.MuteWhenUnfocused = unfocused;
            data.SFXMuted = sfxMuted;
            data.StickProfiles = profiles;
            SaveManager.Instance.SaveGlobalData();
            AudioManager.Instance?.ApplySavedMutes();
            Teardown(host);
        }
    }

    private static Node NewHost(string name) {
        var host = new Node { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static SettingsMenu OpenMenu(Node host) {
        var menu = new SettingsMenu { Name = "SettingsMenu" };
        host.AddChild(menu);
        menu.Show();
        return menu;
    }

    private static void Teardown(Node host) {
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
