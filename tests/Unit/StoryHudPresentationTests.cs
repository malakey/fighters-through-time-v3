using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B1. The authored Story HUD: theme adoption, the boss bar's phase
/// notches, the ability/status indicator row, and live <c>HudOpacity</c>.
///
/// The opacity case is the reason this suite exists in the tree rather than as a
/// pure model. Before Package 8 exactly one surface in the whole project honoured
/// the setting and it read it once, in <c>_Ready</c> — so moving the slider did
/// nothing until a scene change. Plan §2.10 makes that a bug, not a precedent,
/// and a one-shot read passes any test that only checks the value at construction.
/// Every case here therefore restores whatever it changed in a finally block:
/// <c>GlobalData</c> is a live autoload payload shared with the rest of the run.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudPresentationTests {

    private static StoryHUD AddHud(Node host) {
        StoryHUD hud = StoryHUD.CreateDefault();
        host.AddChild(hud);
        return hud;
    }

    private static Node CreateHost(string name) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node { Name = name };
        tree.Root.AddChild(host);
        return host;
    }

    [TestCase]
    public void TheAuthoredSceneSuppliesEveryWidgetTheControllersDriveAndAdoptsTheTheme() {
        // CreateDefault must resolve the authored scene: Package 8 B1 deleted the
        // code-built duplicate, so a missing scene is now a silent empty HUD.
        AssertThat(ResourceLoader.Exists(StoryHUD.SceneResourcePath)).IsTrue();

        Node host = CreateHost("StoryHudAuthoredHost");
        try {
            StoryHUD hud = AddHud(host);
            AssertThat(hud.Name.ToString()).IsEqual("StoryHUD");

            var root = hud.GetNodeOrNull<Control>("Root");
            AssertObject(root).IsNotNull();
            AssertObject(root.Theme).IsNotNull();

            AssertObject(hud.GetNodeOrNull<Label>("Root/TopLeft/LevelTitle")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<Label>("Root/TopLeft/Objective")).IsNotNull();
            // M-27: the designed portrait and block-charge row exist in the scene.
            AssertObject(hud.GetNodeOrNull<TextureRect>("Root/Portrait")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<HBoxContainer>("Root/Vitals/BlockCharges")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<ProgressBar>("Root/Vitals/HPBar")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<ProgressBar>("Root/Vitals/MeterBar")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<Label>("Root/Vitals/RewindLabel")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<Label>("Root/Vitals/DustLabel")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<Label>("Root/Vitals/StatusIndicator")).IsNotNull();
            AssertObject(hud.GetNodeOrNull<ProgressBar>("Root/BossPanel/BossBar")).IsNotNull();
            AssertObject(hud.BossNotches).IsNotNull();

            foreach (string slot in new[] { "Special1", "Special2", "Movement", "Ultimate" }) {
                AssertObject(hud.GetNodeOrNull<Label>($"Root/Vitals/Abilities/{slot}/SlotName")).IsNotNull();
                AssertObject(hud.GetNodeOrNull<ProgressBar>($"Root/Vitals/Abilities/{slot}/SlotBar")).IsNotNull();
            }
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void RevealingABossNotchesTheBarAndHidingItClearsTheNotches() {
        Node host = CreateHost("StoryHudBossBarHost");
        try {
            StoryHUD hud = AddHud(host);
            var panel = hud.GetNodeOrNull<Control>("Root/BossPanel");
            AssertThat(panel.Visible).IsFalse();

            hud.ShowBossBar("boss_archive_prime_name", 900, 1200, new[] { 0.66f, 0.33f });

            AssertThat(panel.Visible).IsTrue();
            AssertThat(hud.BossNotches.Positions.Count).IsEqual(2);
            AssertFloat(hud.BossNotches.Positions[0]).IsEqualApprox(0.66f, 0.0001f);
            AssertFloat(hud.BossNotches.Positions[1]).IsEqualApprox(0.33f, 0.0001f);
            AssertThat(hud.BossNotches.CrossedPhases).IsEqual(0);

            // A phase change flashes the bar and dims the notch just crossed.
            EventBus.Instance?.RaiseBossPhaseChanged(1);
            AssertThat(hud.BossNotches.CrossedPhases).IsEqual(1);
            AssertThat(hud.BossNotches.IsFlashing).IsTrue();

            hud.HideBossBar();
            AssertThat(panel.Visible).IsFalse();
            AssertThat(hud.BossNotches.Positions.Count).IsEqual(0);
            AssertThat(hud.BossNotches.IsFlashing).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void ASinglePhaseBossGetsAPlainBarWithNoInventedNotches() {
        Node host = CreateHost("StoryHudSinglePhaseHost");
        try {
            StoryHUD hud = AddHud(host);

            hud.ShowBossBar("boss_mirror_paradox_name", 1000, 1000, System.Array.Empty<float>());
            AssertThat(hud.BossNotches.Positions.Count).IsEqual(0);

            // The legacy three-argument overload every pre-Package-8 caller used
            // must still work, and must also draw nothing rather than guessing.
            hud.ShowBossBar("boss_mirror_paradox_name", 1000, 1000);
            AssertThat(hud.BossNotches.Positions.Count).IsEqual(0);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void CooldownAndMeterEventsDriveTheIndicatorRow() {
        Node host = CreateHost("StoryHudIndicatorHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            AssertObject(bus).IsNotNull();

            bus.RaiseCooldownStarted(new CooldownPayload {
                PlayerIndex = 0, Slot = AbilitySlot.Special1, Duration = 10f
            });
            AssertThat(hud.Indicators.IsReady(AbilitySlot.Special1)).IsFalse();

            // Player two's cooldowns must never darken the Story player's icons.
            bus.RaiseCooldownStarted(new CooldownPayload {
                PlayerIndex = 1, Slot = AbilitySlot.Special2, Duration = 10f
            });
            AssertThat(hud.Indicators.IsReady(AbilitySlot.Special2)).IsTrue();

            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload {
                PlayerIndex = 0, CurrentValue = 100f, NormalizedValue = 1f, IsFull = true
            });
            AssertThat(hud.Indicators.IsReady(AbilitySlot.Ultimate)).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheStatusPipFollowsTheRisingAndFallingEdgesOnTheBus() {
        Node host = CreateHost("StoryHudStatusHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            var pip = hud.GetNodeOrNull<Label>("Root/Vitals/StatusIndicator");
            AssertThat(pip.Visible).IsFalse();

            bus.RaiseStatusEffectApplied(new StatusEffectPayload {
                TargetIndex = 0, Type = StatusType.Venom, Duration = 5f, Intensity = 1f
            });
            AssertThat(hud.Indicators.ActiveStatus).IsEqual(StatusType.Venom);

            // A3 added the falling edge specifically so a pushed status can clear.
            bus.RaiseStatusEffectCleared(new StatusEffectPayload {
                TargetIndex = 0, Type = StatusType.None, Duration = 0f, Intensity = 0f
            });
            AssertThat(hud.Indicators.ActiveStatus).IsEqual(StatusType.None);

            // An enemy's status must not appear on the player's HUD.
            bus.RaiseStatusEffectApplied(new StatusEffectPayload {
                TargetIndex = 1, Type = StatusType.Root, Duration = 5f, Intensity = 1f
            });
            AssertThat(hud.Indicators.ActiveStatus).IsEqual(StatusType.None);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void BlockChargePipsAreDataDrivenAndTrackTheBus() {
        // M-27 (audit 2026-08-08; design :2611/:2721): Story is the mode where
        // Resonance can raise charge capacity, so the pip count is data-driven and
        // the lit count follows BlockSystem's published charge state.
        Node host = CreateHost("StoryHudBlockChargesHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            AssertObject(bus).IsNotNull();

            bus.RaiseBlockChargesChanged(new BlockChargesPayload {
                PlayerIndex = 0, CurrentCharges = 3, MaxCharges = 3
            });
            AssertThat(hud.BlockPipCapacity).IsEqual(3);
            AssertThat(hud.LitBlockPips).IsEqual(3);

            // A spent pip dims rather than disappearing, so capacity stays readable.
            bus.RaiseBlockChargesChanged(new BlockChargesPayload {
                PlayerIndex = 0, CurrentCharges = 1, MaxCharges = 3
            });
            AssertThat(hud.BlockPipCapacity).IsEqual(3);
            AssertThat(hud.LitBlockPips).IsEqual(1);

            // A Resonance capacity raise rebuilds the row rather than clamping.
            bus.RaiseBlockChargesChanged(new BlockChargesPayload {
                PlayerIndex = 0, CurrentCharges = 4, MaxCharges = 4
            });
            AssertThat(hud.BlockPipCapacity).IsEqual(4);
            AssertThat(hud.LitBlockPips).IsEqual(4);

            // Another combatant's charges never touch the Story player's readout.
            bus.RaiseBlockChargesChanged(new BlockChargesPayload {
                PlayerIndex = 1, CurrentCharges = 0, MaxCharges = 3
            });
            AssertThat(hud.BlockPipCapacity).IsEqual(4);
            AssertThat(hud.LitBlockPips).IsEqual(4);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void HudOpacityIsAppliedAtReadyAndAgainWheneverTheSettingChanges() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        AssertObject(data).IsNotNull();
        float original = data.HudOpacity;
        Node host = CreateHost("StoryHudOpacityHost");
        try {
            data.HudOpacity = 0.5f;
            StoryHUD hud = AddHud(host);
            var root = hud.GetNodeOrNull<Control>("Root");

            AssertFloat(hud.AppliedHudOpacity).IsEqualApprox(0.5f, 0.0001f);
            AssertFloat(root.Modulate.A).IsEqualApprox(0.5f, 0.0001f);

            // The live half: a slider moved mid-level must reach the HUD without a
            // scene reload. _Process is the poll; drive it directly.
            data.HudOpacity = 1f;
            hud._Process(1.0 / 60.0);
            AssertFloat(root.Modulate.A).IsEqualApprox(1f, 0.0001f);

            data.HudOpacity = 0.25f;
            hud._Process(1.0 / 60.0);
            AssertFloat(root.Modulate.A).IsEqualApprox(0.25f, 0.0001f);

            // Only alpha moves; a surface that tints itself keeps its colour.
            AssertFloat(root.Modulate.R).IsEqual(1f);
            AssertFloat(root.Modulate.G).IsEqual(1f);
            AssertFloat(root.Modulate.B).IsEqual(1f);
        } finally {
            data.HudOpacity = original;
            host.Free();
        }
    }
}
