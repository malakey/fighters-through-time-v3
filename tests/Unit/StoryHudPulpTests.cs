using FTT.Core;
using FTT.Enemies;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The Story HUD's glass corner cluster (HUD-S1) and boss bar (BOSS-A), UI_SPEC_GLASS 3.4, 3.6, 4.4 and 9.2 (TG-HUDS,
/// Temporal Glass; first written by UR-HUDS for the pulp HUD, the suite keeps its name): the glass slabs with light tags,
/// the lenses and status badges on the kept glyph Labels, the HP tube and its value, the time tools, the thin-ring
/// Integrity dial with its streams and sealed note, the boss bar, the checkpoint toast and the boss intro card. Values
/// are fed through the same EventBus payloads the game raises.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudPulpTests {

    private const string Abilities = "SafeArea/TopLeft_Panel/AbilitiesPanel/Plate/Pad/Abilities";
    private const string Stack = "SafeArea/TopLeft_Panel/VitalsPanel/Plate/Pad/Stack";
    private const string TemporalRow = "SafeArea/TopLeft_Panel/TimePanel/Pad/TemporalRow";

    private static Rect2 Bounds(Control control) => control.GetGlobalTransform() * new Rect2(Vector2.Zero, control.Size);

    [TestCase]
    public void TheCornerClusterBuildsItsGlassWidgetsOnTheKeptGlyphLabels() {
        Node host = CreateHost("StoryHudGlassBuildHost");
        try {
            StoryHUD hud = AddHud(host);
            AssertThat(hud.VitalsPanel.TagLabel.Text).IsEqual("hud_vitals");
            AssertThat(hud.AbilitiesPanel.TagLabel.Text).IsEqual("hud_abilities");
            // The three slabs wear their 3.1 padding presets and each carries the sheen and streaks over the slab.
            foreach ((string path, string preset) in new[] {
                ("SafeArea/TopLeft_Panel/VitalsPanel/Plate", UIPalette.Variations.GlassPanelVitals),
                ("SafeArea/TopLeft_Panel/AbilitiesPanel/Plate", UIPalette.Variations.GlassPanelAbilities),
                ("SafeArea/TopLeft_Panel/TimePanel", UIPalette.Variations.GlassPanelTimeTools),
                ("SafeArea/TopRight_Panel/DustPlate", UIPalette.Variations.GlassPanelDust) }) {
                var plate = hud.GetNode<PanelContainer>(path);
                AssertThat(plate.ThemeTypeVariation.ToString()).OverrideFailureMessage($"{path} preset").IsEqual(preset);
                AssertObject(plate.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName)).OverrideFailureMessage($"{path} sheen").IsNotNull();
            }
            AssertThat(hud.SlotWidgets.Count).IsEqual(3);
            foreach (PulpAbilitySlot slot in hud.SlotWidgets) {
                AssertObject(slot).IsNotNull();
                AssertThat(slot.SlotSize).IsEqual(74f);
                AssertThat(slot.Ultimate).IsFalse();
            }
            // The Ultimate's layout box is its 92 px lens; the 110 px ring box hangs 9 px outside it (4.4).
            PulpAbilitySlot ult = hud.UltimateSlotWidget;
            AssertThat(ult.Ultimate).IsTrue();
            var ultHost = hud.GetNode<Control>($"{Abilities}/MeterRow/UltimateMeter_BG");
            AssertThat(ultHost.CustomMinimumSize).IsEqual(new Vector2(PulpAbilitySlot.UltimateFrame, PulpAbilitySlot.UltimateFrame));
            AssertThat(ult.Size).IsEqual(new Vector2(PulpAbilitySlot.RingOuterDiameter, PulpAbilitySlot.RingOuterDiameter));
            AssertThat(ult.Position).IsEqual(-ult.LensOrigin);
            AssertThat(hud.StatusChips.Damage.BadgeSize).IsEqual(44f);
            AssertObject(hud.HpDecor).IsNotNull();
            AssertThat(hud.HpDecor.Bar == hud.GetNode<ProgressBar>($"{Stack}/Top/Vitals/HealthBar_BG")).IsTrue();
            AssertThat(hud.HpDecor.Style).IsEqual(PulpBarStyle.Hp);

            // The glyph Labels keep their Text and TooltipText but draw nothing; the widget is their child.
            var slotOne = hud.GetNode<Label>($"{Abilities}/CooldownRow/Cooldown1_Icon");
            AssertThat(slotOne.VisibleCharacters).IsEqual(0);
            AssertThat(hud.SlotWidgets[0].GetParent() == slotOne).IsTrue();
            AssertThat(hud.SlotWidgets[0].KeyText).IsEqual("hud_slot_special1");
            // The Rally band node stays for the path pins and never shows (the tube draws the band).
            AssertThat(hud.GetNode<ColorRect>($"{Stack}/Top/Vitals/HealthBar_BG/RallyEcho_Band").Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void SlotsFollowCooldownLockAndMeterThroughTheModel() {
        Node host = CreateHost("StoryHudGlassSlotHost");
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

            // A locked slot is shown, never hidden: Dormant and Suppressed are their own states, drawn as the lens's locks.
            bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = AbilitySlot.Special2, State = AbilitySlotLockState.Dormant });
            bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = AbilitySlot.MovementAbility, State = AbilitySlotLockState.Suppressed });
            AssertThat(s2.State).IsEqual(PulpSlotState.Dormant);
            AssertThat(mov.State).IsEqual(PulpSlotState.Suppressed);
            AssertThat(s2.Look.GlyphName).IsEqual("dormant");
            AssertThat(mov.Look.GlyphName).IsEqual("cross");
            AssertThat(s2.GlyphIcon.IconName).IsEqual("lock_dormant");
            AssertThat(mov.GlyphIcon.IconName).IsEqual("lock_suppressed");

            // The Ultimate charges with the meter and is Ready (a full ring plus a READY label) at 100.
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
    public void TheNotYetFlashPlaysTheCircleRoundTheLensRing() {
        Node host = CreateHost("StoryHudGlassFlashHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus.Instance.RaiseAbilityLockoutRefused(new AbilityLockoutRefusedPayload { PlayerIndex = 0, Slot = AbilitySlot.Special1 });
            PulpAbilitySlot slot = hud.SlotWidgets[0];
            // Ruling 3 (TG-WIDGETS step 2): the glass flash is the contracting danger circle round the readiness ring,
            // with the lens as the tint target; no pulp frame anywhere.
            Control circle = NotYetFlash.CircleOf(slot.Ring);
            AssertObject(circle).IsNotNull();
            AssertThat(circle.Visible).IsTrue();
            AssertThat(circle.Scale).IsEqual(new Vector2(NotYetFlash.StartScale, NotYetFlash.StartScale));
            AssertThat(slot.SelfModulate).IsEqual(NotYetFlash.Tint);
            var icon = hud.GetNode<Label>($"{Abilities}/CooldownRow/Cooldown1_Icon");
            AssertObject(NotYetFlash.FrameOf(icon)).IsNull();
            AssertObject(NotYetFlash.CircleOf(hud.SlotWidgets[1].Ring)).IsNull();

            EventBus.Instance.RaiseAbilityLockoutRefused(new AbilityLockoutRefusedPayload { PlayerIndex = 0, Slot = AbilitySlot.Ultimate });
            Control ultCircle = NotYetFlash.CircleOf(hud.UltimateSlotWidget.Ring);
            AssertObject(ultCircle).IsNotNull();
            AssertThat(ultCircle.Visible).IsTrue();
            AssertThat(hud.UltimateSlotWidget.SelfModulate).IsEqual(NotYetFlash.Tint);
            AssertObject(NotYetFlash.FrameOf(hud.GetNode<Control>($"{Abilities}/MeterRow/UltimateMeter_BG"))).IsNull();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task StatusChipsFillAndEmptyWithTheirSlotsAndKeepDistinctShapes() {
        Node host = CreateHost("StoryHudGlassChipHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            var (damage, control) = hud.StatusChips;
            await Frames(2);
            hud._Process(1.0 / 60.0);
            AssertThat(damage.Empty).IsTrue();
            AssertThat(control.Empty).IsTrue();
            float controlX = control.GetGlobalTransform().Origin.X;

            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4f, Intensity = 1f });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Suppression, Duration = 2f, Intensity = 1f });
            hud._Process(1.0 / 60.0);
            AssertThat(damage.Empty).IsFalse();
            AssertThat(damage.GlyphName).IsEqual("venom");
            AssertThat(damage.Kind).IsEqual(PulpChipKind.Damage);
            AssertThat(damage.NameText).IsEqual("status_venom");
            AssertThat(control.GlyphName).IsEqual("suppress");
            AssertThat(control.Kind).IsEqual(PulpChipKind.Suppression);
            // The glyph is the glass icon at 62 % of the 44 px badge, so the bold weight (3.5).
            AssertThat(damage.Glyph.GlassName).IsEqual("venom");
            AssertThat(damage.Glyph.DrawsBold).IsTrue();

            // Fixed positions (HUD_CONTRACT) that keep the control chip 30 px after the widest damage name (3.4.3).
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.RadiantBurn, Duration = 4f, Intensity = 1f });
            hud._Process(1.0 / 60.0);
            AssertThat(damage.GlyphName).IsEqual("burn");
            AssertFloat(control.GetGlobalTransform().Origin.X).IsEqualApprox(controlX, 0.01f);
            float damageRight = damage.GetGlobalTransform().Origin.X + damage.GetCombinedMinimumSize().X;
            AssertThat(controlX - damageRight >= 30f - 0.5f)
                .OverrideFailureMessage($"the control chip starts {controlX - damageRight} px after RADIANT BURN, under 30").IsTrue();
            AssertThat(damage.Position).IsEqual(new Vector2(StoryHUD.ChipInset, 0f));

            // Every status has its own icon, and the icons exist in the glass kit.
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (StatusType type in new[] {
                StatusType.TimeDilation, StatusType.Venom, StatusType.StaticCharge, StatusType.RadiantBurn,
                StatusType.Root, StatusType.Suppression }) {
                string icon = HudAbilityIndicatorModel.StatusIconName(type);
                AssertThat(PulpIcon.ResolveGlass(icon) != null).OverrideFailureMessage("glass icon for " + type).IsTrue();
                AssertThat(names.Add(icon)).OverrideFailureMessage("distinct icon for " + type).IsTrue();
            }

            bus.RaiseStatusEffectCleared(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.None });
            hud._Process(1.0 / 60.0);
            AssertThat(damage.Empty).IsTrue();
            AssertThat(control.Empty).IsTrue();
            AssertFloat(control.GetGlobalTransform().Origin.X).IsEqualApprox(controlX, 0.01f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheDefySealShowsADifferentShapeInEachState() {
        Node host = CreateHost("StoryHudGlassDefyHost");
        try {
            StoryHUD hud = AddHud(host);
            var seal = hud.GetNode<Label>($"{Abilities}/MeterRow/DefyStack/DefySeal");
            var icon = (PulpIcon)seal.GetChild(0);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (DefySealState state in new[] { DefySealState.Building, DefySealState.Ready, DefySealState.Spent, DefySealState.Barred }) {
                EventBus.Instance.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = state });
                AssertThat(icon.IconName).IsEqual(DefySealModel.IconName(state));
                AssertThat(icon.GlassName).OverrideFailureMessage($"glass Defy icon for {state}").IsEqual(DefySealModel.IconName(state));
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
    public void TheHpTubeFeedsTheTrackerLowStateAndTheEchoAndShowsTheNumeralThenItsMax() {
        Node host = CreateHost("StoryHudGlassHpHost");
        try {
            StoryHUD hud = AddHud(host);
            hud.InitializePlayerVitals(72, 100, 0f);
            var bar = hud.GetNode<ProgressBar>($"{Stack}/Top/Vitals/HealthBar_BG");
            AssertFloat((float)bar.Value).IsEqual(72f);
            AssertThat(hud.HpDecor.Low).IsFalse();
            AssertFloat(hud.HpDecor.LagValue).IsEqualApprox(72f, 0.01f);
            // T17 (3.4.1): the numeral, then "/max" at 18 px dimmed to .78, 2 px after it on its baseline, ending 14 px
            // inside the tube; both over the tube.
            var number = hud.GetNode<Label>($"{Stack}/Top/Vitals/HealthBar_BG/HPText");
            var max = hud.GetNode<Label>($"{Stack}/Top/Vitals/HealthBar_BG/HPMax");
            AssertThat(number.Text).IsEqual("72");
            AssertThat(max.Text).IsEqual("/100");
            AssertThat(number.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.HudNumeralSmall);
            AssertThat(max.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.HudNumeralMax);
            AssertFloat(max.Modulate.A).IsEqualApprox(0.78f, 0.001f);
            AssertFloat(max.Position.X + max.Size.X).IsEqualApprox(bar.Size.X - StoryHUD.HpValueRightPad, 0.5f);
            AssertFloat(max.Position.X - (number.Position.X + number.Size.X)).IsEqualApprox(StoryHUD.ValueMaxGap, 0.5f);
            float numberBaseline = number.Position.Y + number.GetThemeFont("font").GetAscent(number.GetThemeFontSize("font_size"));
            float maxBaseline = max.Position.Y + max.GetThemeFont("font").GetAscent(max.GetThemeFontSize("font_size"));
            AssertFloat(maxBaseline).IsEqualApprox(numberBaseline, 0.5f);
            AssertThat(number.GetIndex()).IsGreater(hud.HpDecor.GetIndex());
            AssertThat(max.GetIndex()).IsGreater(hud.HpDecor.GetIndex());

            hud.InitializePlayerVitals(18, 100, 0f);
            AssertThat(hud.HpDecor.Low).OverrideFailureMessage("18 of 100 reads low.").IsTrue();
            AssertThat(hud.HpDecor.FillGradient.Moving).IsEqual(UIPalette.Danger);
            // A hit raises the tracker at once and lets it drop later: it still holds the old value.
            AssertThat(hud.HpDecor.LagValue).IsGreater(18f);
            hud.InitializePlayerVitals(0, 100, 0f);
            AssertThat(hud.HpDecor.Low).OverrideFailureMessage("A dead bar draws no low glow.").IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheFreezeStringsAreRebuiltOnlyWhenTheShownSecondTheStateOrTheLanguageChanges() {
        Node host = CreateHost("StoryHudPulpFreezeCacheHost");
        try {
            StoryHUD hud = AddHud(host);
            var freezeText = hud.GetNode<Label>($"{TemporalRow}/TimeFreezeIndicator/FreezeColumn/FreezeRemainingText");
            EventBus.Instance.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Cooldown, SecondsRemaining = 31f });
            hud._Process(0.001);
            int key = hud.FreezeTextKey;
            AssertThat(freezeText.Text.Contains("31")).IsTrue();
            // Same shown second: nothing is rebuilt, so a sentinel written over the text survives the frame.
            freezeText.Text = "sentinel";
            hud._Process(0.001);
            AssertThat(hud.FreezeTextKey).IsEqual(key);
            AssertThat(freezeText.Text).IsEqual("sentinel");
            // The next second rebuilds.
            hud._Process(1.0);
            AssertThat(hud.FreezeTextKey == key).IsFalse();
            AssertThat(freezeText.Text.Contains("30")).IsTrue();
            // A language change invalidates the key, so the strings are rebuilt in the new language.
            freezeText.Text = "sentinel";
            hud.Notification((int)Node.NotificationTranslationChanged);
            AssertThat(hud.FreezeTextKey).IsEqual(-1);
            hud._Process(0.001);
            AssertThat(freezeText.Text.Contains("30")).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task TheTimeToolsShowTheRewindCountTheFreezeStateAndTheBeaconGlassBadge() {
        Node host = CreateHost("StoryHudGlassTimeHost");
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
            var rewindBadge = hud.GetNode<Label>($"{TemporalRow}/RewindCounter/RewindIcon").GetChild<HudBadge>(0);
            AssertThat(rewindBadge.IconName).IsEqual("hourglass");
            AssertThat(rewindBadge.Tint).IsEqual(UIPalette.Accent2);
            AssertFloat(rewindBadge.Ring).IsEqual(0f);
            story.ChronalRewindsRemaining = 1;
            hud._Process(1.0 / 60.0);
            AssertThat(count.Text.Contains("1")).IsTrue();
            AssertThat(count.GetThemeColor("font_color")).OverrideFailureMessage("one rewind left turns to danger").IsEqual(UIPalette.Danger);
            AssertThat(rewindBadge.Tint).IsEqual(UIPalette.Danger);

            var freezeText = hud.GetNode<Label>($"{TemporalRow}/TimeFreezeIndicator/FreezeColumn/FreezeRemainingText");
            hud._Process(1.0 / 60.0);
            AssertThat(freezeText.Text).IsEqual(hud.Tr("common_ready"));
            EventBus.Instance.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 3.2f });
            AssertThat(hud.TimeFreeze).IsEqual(TimeFreezeState.Active);
            AssertThat(freezeText.Text.Contains("3.2")).IsTrue();
            var badge = hud.GetNode<Label>($"{TemporalRow}/TimeFreezeIndicator/FreezeIcon").GetChild<HudBadge>(0);
            AssertFloat(badge.Ring).IsEqualApprox(3.2f / FTT.Environment.TimeFreezeController.FreezeSeconds, 0.01f);
            // G7 (3.4.4): the duration ring is 3 px with its outer edge 5 px outside the 46 px badge.
            AssertThat(HudBadge.RingWidth).IsEqual(3f);
            AssertFloat(HudBadge.RingRadius(badge.BadgeSize) + HudBadge.RingWidth / 2f).IsEqualApprox(46f / 2f + 5f, 0.001f);
            EventBus.Instance.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Cooldown, SecondsRemaining = 31f });
            AssertThat(freezeText.Text.Contains("31")).IsTrue();
            AssertFloat(badge.Ring).IsEqualApprox(31f / FTT.Environment.TimeFreezeController.CooldownSeconds, 0.01f);

            // 4.4: Time Freeze sits 48 px after the rewind counter; in Act III the Beacon sits 24 px after the counter.
            await Frames(2);
            Rect2 rewind = Bounds(hud.GetNode<Control>($"{TemporalRow}/RewindCounter"));
            Rect2 freeze = Bounds(hud.GetNode<Control>($"{TemporalRow}/TimeFreezeIndicator"));
            AssertFloat(freeze.Position.X - rewind.End.X).IsEqualApprox(48f, 0.5f);
            EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
            await Frames(2);
            Rect2 beacon = Bounds(hud.GetNode<Control>($"{TemporalRow}/BeaconAnchors"));
            AssertFloat(beacon.Position.X - rewind.End.X).IsEqualApprox(24f, 0.5f);
            freeze = Bounds(hud.GetNode<Control>($"{TemporalRow}/TimeFreezeIndicator"));
            AssertFloat(freeze.Position.X - beacon.End.X).IsEqualApprox(48f, 0.5f);

            // The Beacon is a glass badge with the beacon_anchor glyph in accent2; its pips are 18 px drawn diamonds.
            AssertThat(hud.AnchorPipCount).IsEqual(3);
            AssertThat(hud.AnchorPips[0].CustomMinimumSize).IsEqual(new Vector2(AnchorPip.PipSize, AnchorPip.PipSize));
            var icon = hud.GetNode<BeaconAnchorIcon>($"{TemporalRow}/BeaconAnchors/BeaconIcon");
            AssertThat(icon.CurrentGlyph).IsEqual("beacon_anchor");
            AssertThat(icon.Badge.Tint).IsEqual(UIPalette.Accent2);
            AssertThat(icon.Badge.Glyph.GlassName).IsEqual("beacon_anchor");
            AssertThat(hud.BeaconLastStand).IsFalse();
            EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 0, Max = 3 });
            AssertThat(icon.CurrentGlyph).IsEqual("beacon_anchor_hollow");
            AssertThat(icon.Badge.Glyph.GlassName).IsEqual("beacon_anchor_hollow");
            AssertThat(icon.Badge.Tint).IsEqual(BeaconAnchorIcon.LastStandTint);
        } finally {
            if (StoryManager.Instance != null) StoryManager.Instance.ChronalRewindsRemaining = rewinds;
            host.Free();
        }
    }

    [TestCase]
    public void TheIntegrityDialDrawsOneStreamPerLivingExtractorAndNoneWhileFrozen() {
        Node host = CreateHost("StoryHudGlassSiphonHost");
        try {
            StoryHUD hud = AddHud(host);
            EventBus bus = EventBus.Instance;
            AssertThat(hud.SiphonDraw.Count).IsEqual(0);
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 3 });
            AssertThat(hud.SiphonDraw.Count).IsEqual(3);
            AssertThat(hud.SiphonDraw.Visible).IsTrue();
            // Each stream is a cold Line2D (1.92 px, miter joints) over its 6 px glow line; only the living count shows.
            for (int k = 0; k < SiphonStreamsLayer.MaxStreams; k++) {
                Line2D line = hud.SiphonDraw.StreamLines[k], glow = hud.SiphonDraw.GlowLines[k];
                AssertThat(line.Visible).OverrideFailureMessage($"stream {k}").IsEqual(k < 3);
                AssertThat(glow.Visible).OverrideFailureMessage($"glow {k}").IsEqual(k < 3);
                if (k >= 3) continue;
                AssertFloat(line.Width).IsEqualApprox(1.92f, 0.01f);
                AssertThat(line.JointMode).IsEqual(Line2D.LineJointMode.Sharp);
                AssertThat(line.DefaultColor).IsEqual(UIPalette.UnboundCold);
                AssertThat(glow.GetIndex()).IsLess(line.GetIndex());
                AssertThat(line.Points.Length).IsEqual(9);
            }
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 3, Frozen = true });
            AssertThat(hud.SiphonDraw.Count).IsEqual(0);
            AssertThat(hud.SiphonDraw.StreamLines[0].Visible).IsFalse();

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
    public void TheDialWedgeIsGoldAboveTheTremorThresholdAndDangerBelowIt() {
        Node host = CreateHost("StoryHudPulpWedgeHost");
        try {
            StoryHUD hud = AddHud(host);
            var wedge = hud.GetNode<RadialProgress>("SafeArea/TopRight_Panel/IntegrityClock/ClockWedge");
            foreach (float percent in new[] { 100f, 64f, 20f }) {
                EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = percent, Tier = IntegrityTier.Stabilized });
                AssertThat(TimelineIntegrityRules.TremorStage(percent)).IsEqual(0);
                AssertThat(wedge.FillColor).OverrideFailureMessage($"{percent}% reads calm gold").IsEqual(UIPalette.Res);
            }
            foreach (float percent in new[] { 19f, 8f }) {
                EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = percent, Tier = IntegrityTier.Fractured });
                AssertThat(TimelineIntegrityRules.TremorStage(percent)).IsGreater(0);
                AssertThat(wedge.FillColor).OverrideFailureMessage($"{percent}% reads danger").IsEqual(UIPalette.Danger);
            }
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task TheSealedDialGivesWayToTheNoteAndAFrozenWedgeTurnsInkTwo() {
        Node host = CreateHost("StoryHudGlassSealHost");
        StoryManager story = StoryManager.Instance;
        try {
            AssertObject(story).IsNotNull();
            StoryHUD hud = AddHud(host);
            await Frames(2);
            var wedge = hud.GetNode<RadialProgress>("SafeArea/TopRight_Panel/IntegrityClock/ClockWedge");
            var face = hud.GetNode<Control>("SafeArea/TopRight_Panel/IntegrityClock/ClockFace");
            var percent = hud.GetNode<Label>("SafeArea/TopRight_Panel/IntegrityClock/IntegrityText");
            var dust = hud.GetNode<Control>("SafeArea/TopRight_Panel/DustPlate/Pad/CurrencyContainer");
            // G9 (3.4.4): the wedge is the annulus from radius 23.15 to 43.68 drawn at rgba(glow, .85) over the .55 remainder.
            AssertFloat(wedge.Size.X / 2f).IsEqualApprox(IntegrityDialLayer.WedgeOuterRadius, 0.01f);
            AssertFloat(wedge.Thickness).IsEqualApprox(IntegrityDialLayer.WedgeOuterRadius - IntegrityDialLayer.WedgeInnerRadius, 0.01f);
            AssertFloat(IntegrityDialLayer.WedgeInnerRadius).IsEqualApprox(23.15f, 0.01f);
            AssertFloat(wedge.FillColor.A * wedge.SelfModulate.A).IsEqualApprox(0.85f, 0.001f);
            AssertFloat(wedge.TrackColor.A * wedge.SelfModulate.A).IsEqualApprox(0.55f, 0.001f);
            AssertThat(hud.DialTicks.TickPolygons.Length).IsEqual(IntegrityDialLayer.TickCount);
            AssertThat(hud.DialTicks.GetIndex()).IsGreater(wedge.GetIndex());
            AssertThat(hud.DialFace.GetParent() == face).IsTrue();

            // AC17: a frozen clock's wedge turns ink2 at .6 and its streams stop.
            EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2, Frozen = true });
            Color drawn = new(wedge.FillColor, wedge.FillColor.A * wedge.SelfModulate.A);
            AssertThat(drawn.IsEqualApprox(new Color(UIPalette.Ink2, StoryHUD.FrozenWedgeAlpha)))
                .OverrideFailureMessage($"a frozen wedge draws {drawn}, not ink2 at .6").IsTrue();
            AssertThat(hud.DialShown).IsTrue();
            AssertThat(hud.SealedNote.Visible).IsFalse();

            // Sealed at the pre-boss checkpoint: the dial, caption, percent and streams give way to the note (T33),
            // right-aligned under the dust plate; the dust plate stays.
            story.LockIntegrityAtPreBoss();
            hud._Process(1.0 / 60.0);
            AssertThat(hud.DialShown).IsFalse();
            AssertThat(face.Visible).IsFalse();
            AssertThat(wedge.Visible).IsFalse();
            AssertThat(hud.DialTicks.Visible).IsFalse();
            AssertThat(percent.Visible).IsFalse();
            AssertThat(hud.IntegrityCaption.Visible).IsFalse();
            AssertThat(hud.SiphonDraw.Visible).IsFalse();
            AssertThat(hud.SealedNote.Visible).IsTrue();
            AssertThat(hud.SealedNote.Text).IsEqual("hud_integrity_sealed");
            AssertThat(hud.SealedNote.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.NoteLabel);
            AssertThat(hud.SealedNote.HorizontalAlignment).IsEqual(HorizontalAlignment.Right);
            Rect2 note = Bounds(hud.SealedNote);
            Rect2 plate = Bounds(hud.GetNode<Control>("SafeArea/TopRight_Panel/DustPlate"));
            AssertFloat(note.End.X).IsEqualApprox(plate.End.X, 0.5f);
            AssertThat(note.Position.Y).IsGreater(plate.End.Y);
            AssertThat(dust.IsVisibleInTree()).IsTrue();
        } finally {
            story?.ClearLevelAttemptState();
            host.Free();
        }
    }

    private static BossController CreateBossForPhaseTab() {
        var data = AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    [TestCase]
    public void TheBossBarIsBossAWithADiamondLedNameAGlassTubeAndAPlainOneBasedPhaseLabel() {
        Node host = CreateHost("StoryHudGlassBossHost");
        try {
            StoryHUD hud = AddHud(host);
            var topLeft = hud.GetNode<Control>("SafeArea/TopLeft");
            hud.ShowBossBar("boss_archive_prime_name", 900, 1200, new[] { 0.66f, 0.33f });
            var panel = hud.GetNode<Control>("SafeArea/BossPanel");
            var name = hud.GetNode<Label>("SafeArea/BossPanel/BossName");
            var phase = hud.GetNode<Label>("SafeArea/BossPanel/BossPhaseTab");
            var bar = hud.GetNode<ProgressBar>("SafeArea/BossPanel/BossBar");
            var value = hud.GetNode<Label>("SafeArea/BossPanel/BossBar/BossHpValue");
            var max = hud.GetNode<Label>("SafeArea/BossPanel/BossBar/BossHpMax");
            AssertThat(hud.BossPhase).IsEqual(1);
            AssertThat(phase.Text.Contains("1")).IsTrue();
            AssertThat(value.Text).IsEqual("900");
            AssertThat(max.Text).IsEqual("/1200");

            // 3.6: the boss tube (840 x 34 at 100 %), 10 px under the 38 px header; the notches on the tube.
            AssertThat(hud.BossDecor.Style).IsEqual(PulpBarStyle.Boss);
            AssertFloat(panel.Size.X).IsEqualApprox(StoryHUD.BossBarWidthFor(hud.GetNode<Control>("SafeArea").Size.X), 0.5f);
            AssertFloat(StoryHUD.BossBarWidthFor(1920f)).IsEqual(840f);
            AssertFloat(StoryHUD.BossBarWidthFor(1920f / 1.4f)).IsEqualApprox(1920f / 1.4f - 1008f, 0.01f);
            AssertFloat(bar.Size.Y).IsEqual(StoryHUD.BossTubeHeight);
            AssertFloat(bar.Position.Y).IsEqual(StoryHUD.BossHeaderHeight + StoryHUD.BossTubeGap);
            AssertThat(hud.BossDecor.PhaseNotches.Length).IsEqual(2);
            AssertThat(hud.BossNotches.DrawNotches).OverrideFailureMessage("the tube draws the notches").IsFalse();
            AssertThat(topLeft.Visible).OverrideFailureMessage("the level title gives way to the bar").IsFalse();

            // The diamond-led name: the kit's 12 px diamond in boss red before the name, no plate, no tilt; the phase
            // label is a plain T31 label (no plate, no tilt) right-aligned, on the name's baseline.
            AssertObject(hud.BossDiamond).IsNotNull();
            AssertThat(hud.BossDiamond.Texture == PulpKit.Texture("glass_diamond_12")).IsTrue();
            AssertThat(hud.BossDiamond.Modulate).IsEqual(UIPalette.Boss);
            AssertFloat(hud.BossDiamond.Position.X + hud.BossDiamond.Size.X / 2f)
                .IsEqualApprox(StoryHUD.BossDiamondInset + StoryHUD.BossDiamondSize / 2f, 0.01f);
            AssertFloat(name.Position.X).IsEqual(StoryHUD.BossDiamondInset + StoryHUD.BossDiamondSize + StoryHUD.BossNameGap);
            AssertThat(name.HasThemeStyleboxOverride("normal")).IsFalse();
            AssertThat(phase.HasThemeStyleboxOverride("normal")).IsFalse();
            AssertFloat(name.Rotation).IsEqual(0f);
            AssertFloat(phase.Rotation).IsEqual(0f);
            AssertThat(phase.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.BossPhaseTag);
            AssertFloat(phase.Position.X + phase.Size.X).IsEqualApprox(panel.Size.X, 0.5f);
            float nameBaseline = name.Position.Y + name.PivotOffset.Y;
            float phaseBaseline = phase.Position.Y + phase.GetThemeFont("font").GetAscent(phase.GetThemeFontSize("font_size"));
            AssertFloat(phaseBaseline).IsEqualApprox(nameBaseline, 0.5f);
            AssertFloat(hud.BossDiamond.Position.Y + hud.BossDiamond.Size.Y / 2f).IsEqualApprox(nameBaseline - StoryHUD.BossDiamondLift, 0.01f);
            AssertThat(hud.BossPhaseDropped).IsFalse();

            // The real publisher: a BossController crossing its first threshold raises phase index 1, and the
            // label must read "Phase 2" (one-based).
            BossController boss = CreateBossForPhaseTab();
            try {
                boss.TakeDamage(Mathf.RoundToInt(boss.ScaledMaxHP * 0.6f));
                AssertThat(boss.CurrentPhase).IsEqual(1);
                AssertThat(hud.BossPhase).IsEqual(2);
                AssertThat(phase.Text.Contains("2")).IsTrue();
            } finally {
                boss.Free();
            }

            hud.UpdateBossHP(500);
            AssertThat(value.Text).IsEqual("500");
            hud.HideBossBar();
            AssertThat(topLeft.Visible).IsTrue();
            AssertThat(hud.BossDecor.PhaseNotches.Length).IsEqual(0);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheObjectiveRowShowsItsFlagOnlyWithAnObjective() {
        Node host = CreateHost("StoryHudGlassObjectiveHost");
        try {
            StoryHUD hud = AddHud(host);
            var objective = hud.GetNode<Label>("SafeArea/TopLeft/Objective");
            var flag = objective.GetChild<PulpIcon>(0);
            AssertThat(flag.IconName).IsEqual("flag");
            AssertThat(flag.Visible).IsFalse();
            hud.SetObjective("florence_objective_reach_boss");
            AssertThat(flag.Visible).IsTrue();
            // 4.4: the flag is 22 px (the bold weight) in res2, and the text starts 10 px after it.
            AssertThat(flag.Tint).IsEqual(UIPalette.Res2);
            AssertThat(flag.GlassName).IsEqual("flag");
            AssertThat(flag.CustomMinimumSize).IsEqual(new Vector2(22f, 22f));
            AssertThat(flag.DrawsBold).IsTrue();
            AssertFloat(objective.GetThemeStylebox("normal").GetContentMargin(Side.Left)).IsEqual(32f);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task TheCheckpointToastAndTheBossIntroCardWearTheGlassPlateAndFlourish() {
        Node host = CreateHost("StoryHudGlassToastHost");
        try {
            StoryHUD hud = AddHud(host);
            var toast = hud.GetNode<Label>("SafeArea/CheckpointToast");
            AssertThat(toast.Visible).IsFalse();
            EventBus.Instance.RaiseCheckpointReached("story_hud_glass_toast_checkpoint");
            AssertThat(toast.Visible).IsTrue();
            AssertThat(toast.Text).IsEqual(hud.Tr("hud_checkpoint_reached"));
            // 9.2: the glass tag plate (G2) with the bold res2 flag 10 px before the T14 text in ink.
            StyleBox tag = toast.GetThemeStylebox("panel", UIPalette.Variations.PanelTagPlate);
            AssertThat(toast.GetThemeStylebox("normal") is StyleBoxTexture plate && tag is StyleBoxTexture tagPlate
                    && plate.Texture == tagPlate.Texture)
                .OverrideFailureMessage("the toast wears the glass tag plate").IsTrue();
            float left = tag.GetContentMargin(Side.Left);
            AssertFloat(toast.GetThemeStylebox("normal").GetContentMargin(Side.Left))
                .IsEqualApprox(left + StoryHUD.ToastFlagSize + StoryHUD.ToastFlagGap, 0.01f);
            AssertObject(hud.ToastFlag).IsNotNull();
            AssertThat(hud.ToastFlag.GetParent() == toast).IsTrue();
            AssertThat(hud.ToastFlag.IconName).IsEqual("flag");
            AssertThat(hud.ToastFlag.Tint).IsEqual(UIPalette.Res2);
            AssertThat(hud.ToastFlag.DrawsBold).IsTrue();
            AssertThat(toast.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.PanelTag);
            AssertThat(toast.Uppercase).IsTrue();
            // It fades in rising over 220 ms (MOT-A row 4), holds for 2 s, fades over 300 ms, then hides.
            AssertFloat(hud.CheckpointToastSecondsLeft).IsEqualApprox(0.22f + StoryHUD.ToastHoldSeconds + StoryHUD.ToastFadeSeconds, 0.001f);
            AssertFloat(toast.Modulate.A).OverrideFailureMessage("the toast starts its fade-rise transparent").IsLess(0.5f);
            await Frames(20);
            AssertFloat(toast.Modulate.A).OverrideFailureMessage("the fade-rise has finished").IsEqual(1f);
            while (hud.CheckpointToastSecondsLeft > StoryHUD.ToastFadeSeconds + 1.5f / 60f) hud._Process(1.0 / 60.0);
            AssertThat(toast.Visible).IsTrue();
            AssertFloat(toast.Modulate.A).IsEqual(1f);
            for (int frame = 0; frame < 6; frame++) hud._Process(1.0 / 60.0);
            AssertThat(toast.Modulate.A > 0f && toast.Modulate.A < 1f).OverrideFailureMessage("the toast fades over its last 300 ms").IsTrue();
            for (int frame = 0; frame < 20; frame++) hud._Process(1.0 / 60.0);
            AssertThat(toast.Visible).IsFalse();

            // The boss intro card: the name in T04 under a flourish whose diamond is the boss-red 12 px one, rules 240.
            var card = hud.GetNode<Label>("SafeArea/BossIntroCard");
            hud.ShowBossIntroCard("boss_vulcan_decimator_name", 2f);
            AssertThat(card.Visible).IsTrue();
            AssertThat(card.ThemeTypeVariation.ToString()).IsEqual(UIPalette.LabelRoles.PauseTitle);
            AssertThat(card.Uppercase).IsTrue();
            AssertThat(card.HasThemeStyleboxOverride("normal")).IsFalse();
            AssertFloat(card.Rotation).IsEqual(0f);
            GlassFlourish flourish = hud.IntroFlourish;
            AssertObject(flourish).IsNotNull();
            AssertThat(flourish.GetParent() == card).IsTrue();
            AssertThat(flourish.Shape).IsEqual(GlassFlourish.FlourishShape.RuleDiamondRule);
            AssertThat(flourish.DiamondSize).IsEqual(12);
            AssertThat(flourish.DiamondTint).IsEqual(UIPalette.Boss);
            AssertFloat(flourish.Size.X).IsEqualApprox(2f * StoryHUD.IntroRuleLength + 2f * StoryHUD.IntroFlourishGap + 12f, 0.01f);
            // Centred over the text, above it.
            AssertFloat(flourish.Position.X + flourish.Size.X / 2f).IsEqualApprox(card.Size.X / 2f, 0.5f);
            AssertThat(flourish.Position.Y + flourish.Size.Y).IsLess(card.Size.Y / 2f);
            for (int frame = 0; frame < 125; frame++) hud._Process(1.0 / 60.0);
            AssertThat(card.Visible).IsFalse();
        } finally {
            host.Free();
        }
    }

    private static async System.Threading.Tasks.Task Frames(int count) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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
