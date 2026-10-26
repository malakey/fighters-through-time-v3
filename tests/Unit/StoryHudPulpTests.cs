using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-HUDS (pulp UI redesign). The HUD-S1 widgets of the Story HUD: the tagged panels, the pulp ability slots
/// and status chips on the kept glyph Labels, the HP decor, the time tools, the siphon streams and the BOSS-B
/// nameplate. Values are fed through the same EventBus payloads the game raises.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudPulpTests {

    private const string Abilities = "SafeArea/TopLeft_Panel/AbilitiesPanel/Plate/Pad/Abilities";
    private const string Stack = "SafeArea/TopLeft_Panel/VitalsPanel/Plate/Pad/Stack";
    private const string TemporalRow = "SafeArea/TopLeft_Panel/TimePanel/Pad/TemporalRow";

    [TestCase]
    public void TheCornerClusterBuildsItsPulpWidgetsOnTheKeptGlyphLabels() {
        Node host = CreateHost("StoryHudPulpBuildHost");
        try {
            StoryHUD hud = AddHud(host);
            AssertThat(hud.VitalsPanel.TagLabel.Text).IsEqual("hud_vitals");
            AssertThat(hud.AbilitiesPanel.TagLabel.Text).IsEqual("hud_abilities");
            AssertThat(hud.SlotWidgets.Count).IsEqual(3);
            foreach (PulpAbilitySlot slot in hud.SlotWidgets) {
                AssertObject(slot).IsNotNull();
                AssertThat(slot.SlotSize).IsEqual(74f);
                AssertThat(slot.Ultimate).IsFalse();
            }
            AssertThat(hud.UltimateSlotWidget.Ultimate).IsTrue();
            AssertThat(hud.StatusChips.Damage.BadgeSize).IsEqual(44f);
            AssertObject(hud.HpDecor).IsNotNull();
            AssertThat(hud.HpDecor.Bar == hud.GetNode<ProgressBar>($"{Stack}/Top/Vitals/HealthBar_BG")).IsTrue();

            // The glyph Labels keep their Text and TooltipText but draw nothing; the widget is their child.
            var slotOne = hud.GetNode<Label>($"{Abilities}/CooldownRow/Cooldown1_Icon");
            AssertThat(slotOne.VisibleCharacters).IsEqual(0);
            AssertThat(hud.SlotWidgets[0].GetParent() == slotOne).IsTrue();
            AssertThat(hud.SlotWidgets[0].KeyText).IsEqual("hud_slot_special1");
            // The Rally band node stays for the path pins and never shows (the decor draws the band).
            AssertThat(hud.GetNode<ColorRect>($"{Stack}/Top/Vitals/HealthBar_BG/RallyEcho_Band").Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void SlotsFollowCooldownLockAndMeterThroughTheModel() {
        Node host = CreateHost("StoryHudPulpSlotHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            PulpAbilitySlot s1 = hud.SlotWidgets[0];
            PulpAbilitySlot s2 = hud.SlotWidgets[1];
            PulpAbilitySlot mov = hud.SlotWidgets[2];
            hud._Process(1.0 / 60.0);
            AssertThat(s1.State).IsEqual(PulpSlotState.Ready);

            bus.RaiseCooldownStarted(new CooldownPayload { PlayerIndex = 0, Slot = AbilitySlot.Special1, Duration = 10f });
            hud._Process(1.0 / 60.0);
            AssertThat(s1.State).IsEqual(PulpSlotState.Cooling);
            AssertThat(s1.SecondsRemaining).IsGreater(9f);
            AssertThat(s1.Look.CenterText.Length).IsGreater(0);

            // A locked slot is shown, never hidden: Dormant and Suppressed are their own states.
            bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = AbilitySlot.Special2, State = AbilitySlotLockState.Dormant });
            bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = AbilitySlot.MovementAbility, State = AbilitySlotLockState.Suppressed });
            AssertThat(s2.State).IsEqual(PulpSlotState.Dormant);
            AssertThat(mov.State).IsEqual(PulpSlotState.Suppressed);
            AssertThat(s2.Look.GlyphName).IsEqual("dormant");
            AssertThat(mov.Look.GlyphName).IsEqual("cross");

            // The Ultimate charges with the meter and is Ready (a full ring plus a READY tag) at 100.
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 64f, NormalizedValue = 0.64f });
            hud._Process(1.0 / 60.0);
            PulpAbilitySlot ult = hud.UltimateSlotWidget;
            AssertThat(ult.State).IsEqual(PulpSlotState.Charging);
            AssertThat(ult.Look.CenterText).IsEqual("64%");
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 100f, NormalizedValue = 1f, IsFull = true });
            hud._Process(1.0 / 60.0);
            AssertThat(ult.State).IsEqual(PulpSlotState.Ready);
            AssertThat(ult.Look.ReadyTag).IsTrue();
            AssertThat(ult.Look.Ring).IsEqual(PulpRingMode.Ready);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheNotYetFlashPlaysOverTheSlotWidget() {
        Node host = CreateHost("StoryHudPulpFlashHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus.Instance.RaiseAbilityLockoutRefused(new AbilityLockoutRefusedPayload { PlayerIndex = 0, Slot = AbilitySlot.Special1 });
            var icon = hud.GetNode<Label>($"{Abilities}/CooldownRow/Cooldown1_Icon");
            Panel frame = NotYetFlash.FrameOf(icon);
            AssertObject(frame).IsNotNull();
            // The frame is a later sibling of the slot widget, so it draws over it.
            AssertThat(frame.GetIndex()).IsGreater(hud.SlotWidgets[0].GetIndex());
            AssertThat(hud.SlotWidgets[0].SelfModulate).IsEqual(NotYetFlash.Tint);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void StatusChipsFillAndEmptyWithTheirSlotsAndKeepDistinctShapes() {
        Node host = CreateHost("StoryHudPulpChipHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            var (damage, control) = hud.StatusChips;
            hud._Process(1.0 / 60.0);
            AssertThat(damage.Empty).IsTrue();
            AssertThat(control.Empty).IsTrue();

            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4f, Intensity = 1f });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Suppression, Duration = 2f, Intensity = 1f });
            hud._Process(1.0 / 60.0);
            AssertThat(damage.Empty).IsFalse();
            AssertThat(damage.GlyphName).IsEqual("venom");
            AssertThat(damage.Kind).IsEqual(PulpChipKind.Damage);
            AssertThat(damage.NameText).IsEqual("status_venom");
            AssertThat(control.GlyphName).IsEqual("suppress");
            AssertThat(control.Kind).IsEqual(PulpChipKind.Suppression);

            // Every status has its own icon, and the icons exist in the kit.
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (StatusType type in new[] {
                StatusType.TimeDilation, StatusType.Venom, StatusType.StaticCharge, StatusType.RadiantBurn,
                StatusType.Root, StatusType.Suppression }) {
                string icon = HudAbilityIndicatorModel.StatusIconName(type);
                AssertThat(PulpIcon.Exists(icon)).OverrideFailureMessage("kit icon for " + type).IsTrue();
                AssertThat(names.Add(icon)).OverrideFailureMessage("distinct icon for " + type).IsTrue();
            }

            bus.RaiseStatusEffectCleared(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.None });
            hud._Process(1.0 / 60.0);
            AssertThat(damage.Empty).IsTrue();
            AssertThat(control.Empty).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheDefySealShowsADifferentShapeInEachState() {
        Node host = CreateHost("StoryHudPulpDefyHost");
        try {
            StoryHUD hud = AddHud(host);
            var seal = hud.GetNode<Label>($"{Abilities}/MeterRow/DefyStack/DefySeal");
            var icon = (PulpIcon)seal.GetChild(0);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (DefySealState state in new[] { DefySealState.Building, DefySealState.Ready, DefySealState.Spent, DefySealState.Barred }) {
                EventBus.Instance.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = state });
                AssertThat(icon.IconName).IsEqual(DefySealModel.IconName(state));
                AssertThat(seen.Add(icon.IconName)).IsTrue();
                // The legacy glyph and its localized tooltip stay for the tests and accessibility.
                AssertThat(seal.Text).IsEqual(DefySealModel.Glyph(state));
                AssertThat(seal.TooltipText.Length).IsGreater(0);
            }
            EventBus.Instance.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Ready });
            AssertThat(icon.Tint).IsEqual(UIPalette.Res2);
            EventBus.Instance.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Barred });
            AssertThat(icon.Tint).IsEqual(UIPalette.Danger);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheHpBarFeedsTheTrackerLowStateAndTheEchoToItsDecor() {
        Node host = CreateHost("StoryHudPulpHpHost");
        try {
            StoryHUD hud = AddHud(host);
            hud.InitializePlayerVitals(72, 100, 0f);
            var bar = hud.GetNode<ProgressBar>($"{Stack}/Top/Vitals/HealthBar_BG");
            AssertFloat((float)bar.Value).IsEqual(72f);
            AssertThat(hud.HpDecor.Low).IsFalse();
            AssertFloat(hud.HpDecor.LagValue).IsEqualApprox(72f, 0.01f);
            var text = hud.GetNode<Label>($"{Stack}/Top/Vitals/HealthBar_BG/HPText");
            AssertThat(text.Text).IsEqual("72/100");
            // The numeral is drawn over the decor, not under it.
            AssertThat(text.GetIndex()).IsGreater(hud.HpDecor.GetIndex());

            hud.InitializePlayerVitals(18, 100, 0f);
            AssertThat(hud.HpDecor.Low).OverrideFailureMessage("18 of 100 reads low.").IsTrue();
            // A hit raises the tracker at once and lets it drop later: it still holds the old value.
            AssertThat(hud.HpDecor.LagValue).IsGreater(18f);
            hud.InitializePlayerVitals(0, 100, 0f);
            AssertThat(hud.HpDecor.Low).OverrideFailureMessage("A dead bar draws no low glow.").IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheTimeToolsShowTheRewindCountTheFreezeStateAndTheBeaconPulpArt() {
        Node host = CreateHost("StoryHudPulpTimeHost");
        int rewinds = StoryManager.Instance?.ChronalRewindsRemaining ?? 0;
        try {
            StoryHUD hud = AddHud(host);
            StoryManager story = StoryManager.Instance;
            AssertObject(story).IsNotNull();
            var count = hud.GetNode<Label>($"{TemporalRow}/RewindCounter/RewindCountText");
            story.ChronalRewindsRemaining = 3;
            hud._Process(1.0 / 60.0);
            AssertThat(count.Text.Contains("3")).IsTrue();
            AssertThat(count.HasThemeColorOverride("font_color")).IsFalse();
            story.ChronalRewindsRemaining = 1;
            hud._Process(1.0 / 60.0);
            AssertThat(count.Text.Contains("1")).IsTrue();
            AssertThat(count.GetThemeColor("font_color")).OverrideFailureMessage("one rewind left turns to danger").IsEqual(UIPalette.Danger);

            var freezeText = hud.GetNode<Label>($"{TemporalRow}/TimeFreezeIndicator/FreezeColumn/FreezeRemainingText");
            hud._Process(1.0 / 60.0);
            AssertThat(freezeText.Text).IsEqual(hud.Tr("common_ready"));
            EventBus.Instance.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 3.2f });
            AssertThat(hud.TimeFreeze).IsEqual(TimeFreezeState.Active);
            AssertThat(freezeText.Text.Contains("3.2")).IsTrue();
            var badge = hud.GetNode<Label>($"{TemporalRow}/TimeFreezeIndicator/FreezeIcon").GetChild<HudBadge>(0);
            AssertFloat(badge.Ring).IsEqualApprox(3.2f / FTT.Environment.TimeFreezeController.FreezeSeconds, 0.01f);
            EventBus.Instance.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Cooldown, SecondsRemaining = 31f });
            AssertThat(freezeText.Text.Contains("31")).IsTrue();
            AssertFloat(badge.Ring).IsEqualApprox(31f / FTT.Environment.TimeFreezeController.CooldownSeconds, 0.01f);

            // The Beacon icon and pips are the bigger pulp sizes, and a snap still cracks a pip.
            EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
            AssertThat(hud.AnchorPipCount).IsEqual(3);
            AssertThat(hud.AnchorPips[0].CustomMinimumSize).IsEqual(new Vector2(AnchorPip.PipSize, AnchorPip.PipSize));
            AssertThat(hud.BeaconLastStand).IsFalse();
        } finally {
            if (StoryManager.Instance != null) StoryManager.Instance.ChronalRewindsRemaining = rewinds;
            host.Free();
        }
    }

    [TestCase]
    public void TheIntegrityDialDrawsOneStreamPerLivingExtractorAndNoneWhileFrozen() {
        Node host = CreateHost("StoryHudPulpSiphonHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            AssertThat(hud.SiphonDraw.Count).IsEqual(0);
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 3 });
            AssertThat(hud.SiphonDraw.Count).IsEqual(3);
            AssertThat(hud.SiphonDraw.Visible).IsTrue();
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 3, Frozen = true });
            AssertThat(hud.SiphonDraw.Count).IsEqual(0);

            // The fan runs between the spec's two polylines: stream 0 is path A, the last is path B.
            AssertThat(SiphonStreamsLayer.Stream(0, 3)[0]).IsEqual(new Vector2(100f, 2f));
            AssertThat(SiphonStreamsLayer.Stream(2, 3)[0]).IsEqual(new Vector2(100f, 36f));
            Vector2 mid = SiphonStreamsLayer.Stream(1, 3)[0];
            AssertFloat(mid.Y).IsEqualApprox(19f, 0.01f);
            // One living Extractor draws one stream between the two.
            AssertFloat(SiphonStreamsLayer.Stream(0, 1)[0].Y).IsEqualApprox(19f, 0.01f);
            // The text the tests and the accessibility tree read is kept.
            var label = hud.GetNode<Label>("SafeArea/TopRight_Panel/IntegrityClock/SiphonStreams");
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2 });
            AssertThat(label.Visible).IsTrue();
            AssertThat(label.Text.Contains("2")).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheBossBarIsAPulpNameplateWithAPhaseTabAndTheNotchesOnTheDecor() {
        Node host = CreateHost("StoryHudPulpBossHost");
        try {
            StoryHUD hud = AddHud(host);
            var topLeft = hud.GetNode<Control>("SafeArea/TopLeft");
            hud.ShowBossBar("boss_archive_prime_name", 900, 1200, new[] { 0.66f, 0.33f });
            var tab = hud.GetNode<Label>("SafeArea/BossPanel/BossPhaseTab");
            var value = hud.GetNode<Label>("SafeArea/BossPanel/BossBar/BossHpValue");
            AssertThat(hud.BossPhase).IsEqual(1);
            AssertThat(tab.Text.Contains("1")).IsTrue();
            AssertThat(value.Text).IsEqual("900/1200");
            AssertThat(hud.BossDecor.PhaseNotches.Length).IsEqual(2);
            AssertThat(hud.BossDecor.Style).IsEqual(PulpBarStyle.Boss);
            AssertThat(hud.BossNotches.DrawNotches).OverrideFailureMessage("the decor draws the notches").IsFalse();
            AssertThat(topLeft.Visible).OverrideFailureMessage("the level title gives way to the bar").IsFalse();

            EventBus.Instance.RaiseBossPhaseChanged(2);
            AssertThat(hud.BossPhase).IsEqual(2);
            AssertThat(tab.Text.Contains("2")).IsTrue();
            AssertThat(Mathf.IsEqualApprox(tab.Rotation, Mathf.DegToRad(2f))).IsTrue();
            var name = hud.GetNode<Label>("SafeArea/BossPanel/BossName");
            AssertThat(Mathf.IsEqualApprox(name.Rotation, Mathf.DegToRad(-1f))).IsTrue();
            AssertThat(name.HasThemeStyleboxOverride("normal")).IsTrue();

            hud.UpdateBossHP(500);
            AssertThat(value.Text).IsEqual("500/1200");
            hud.HideBossBar();
            AssertThat(topLeft.Visible).IsTrue();
            AssertThat(hud.BossDecor.PhaseNotches.Length).IsEqual(0);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheObjectiveRowShowsItsFlagOnlyWithAnObjective() {
        Node host = CreateHost("StoryHudPulpObjectiveHost");
        try {
            StoryHUD hud = AddHud(host);
            var objective = hud.GetNode<Label>("SafeArea/TopLeft/Objective");
            var flag = objective.GetChild<PulpIcon>(0);
            AssertThat(flag.IconName).IsEqual("flag");
            AssertThat(flag.Visible).IsFalse();
            hud.SetObjective("florence_objective_reach_boss");
            AssertThat(flag.Visible).IsTrue();
        } finally {
            host.Free();
        }
    }

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
}
