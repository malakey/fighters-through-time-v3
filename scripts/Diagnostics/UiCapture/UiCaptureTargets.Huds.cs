using System.Collections.Generic;
using FTT.Core;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// The Story HUD's capture targets (HUD-S1 and BOSS-A in Temporal Glass, TG-HUDS; first written by UR-HUDS), each a
    /// fake hero and fake state over a gradient stage: <c>huds_calm</c> (the <c>rec_04</c> calm frame),
    /// <c>huds_stress</c> (the <c>rec_05</c> stress frame: the boss bar, low HP, both chips, every lens suppressed, the
    /// sealed dial), <c>huds_boss</c>, <c>huds_anchors</c> (Act III, a Snap mid-split), <c>huds_anchors_zero</c> (the
    /// last stand), <c>huds_toasts</c> (the checkpoint toast and the boss intro card), <c>huds_140</c> and
    /// <c>huds_calm_140</c> (UI scale 1.4), <c>huds_boss_140</c> (the longest boss name at 1.4) and
    /// <c>huds_boss_names</c> / <c>huds_boss_names_140</c> (all fifteen boss bars, cropped, at 1.0 and 1.4). Every
    /// target puts the autoload state it touches back when its page leaves the tree.
    /// </summary>
    public static partial class UiCaptureTargets {

        private static Control HudStage(string heroId) {
            var page = new Control { Name = "HudStage" };
            var back = new TextureRect();
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var gradient = new GradientTexture2D {
                Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0.5f, 0f), FillTo = new Vector2(0.5f, 1f),
                Gradient = new Gradient {
                    Colors = new[] { new Color(0.16f, 0.09f, 0.14f), new Color(0.78f, 0.36f, 0.12f), new Color(0.12f, 0.08f, 0.06f) },
                    Offsets = new[] { 0f, 0.62f, 1f }
                }
            };
            back.Texture = gradient;
            back.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            back.StretchMode = TextureRect.StretchModeEnum.Scale;
            page.AddChild(back);
            GameManager manager = GameManager.Instance;
            if (manager != null) {
                SessionData session = manager.CurrentSession;
                session.SelectedCharacterID = heroId;
                manager.CurrentSession = session;
            }
            // The fake state lives on autoloads: start clean, and put it back once the page is freed.
            StoryManager story = StoryManager.Instance;
            int rewinds = story?.ChronalRewindsRemaining ?? 0;
            story?.ClearLevelAttemptState();
            page.TreeExiting += () => {
                if (StoryManager.Instance == null) return;
                StoryManager.Instance.ClearLevelAttemptState();
                StoryManager.Instance.ChronalRewindsRemaining = rewinds;
            };
            return page;
        }

        private static StoryHUD AddCaptureHud(Control page) {
            StoryHUD hud = StoryHUD.CreateDefault();
            page.AddChild(hud);
            return hud;
        }

        /// <summary>The game applies the saved UI scale to the shared theme at boot; a 1.4 capture does the same.</summary>
        private static void HudScale(float scale) {
            SaveManager save = SaveManager.Instance;
            if (save?.GlobalData != null) save.GlobalData.UiScale = scale;
            UIPalette.ApplyUiScale(scale);
        }

        private static void SetDust(int amount) {
            StoryManager story = StoryManager.Instance;
            if (story != null && story.ChronalDustCollected < amount) story.CollectDust(amount - story.ChronalDustCollected);
        }

        /// <summary>The rec_04 calm frame: 72 / 100 with the tracker, Venom, S1 ready, S2 cooling at 2.4, the Ultimate at 64 %.</summary>
        private static void CalmState(StoryHUD hud) {
            EventBus bus = EventBus.Instance;
            hud.SetLevelTitle("campaign_level_pompeii");
            hud.SetObjective("pompeii_objective_evacuate");
            StoryManager story = StoryManager.Instance;
            if (story != null) story.ChronalRewindsRemaining = 3;
            SetDust(1250);
            hud.InitializePlayerVitals(72, 100, 64f);
            hud.HpDecor.LagValue = 78f;
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 64f, NormalizedValue = 0.64f });
            bus.RaiseBlockChargesChanged(new BlockChargesPayload { PlayerIndex = 0, CurrentCharges = 2, MaxCharges = 3 });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4.2f, Intensity = 1f });
            hud.Indicators.StartCooldown(AbilitySlot.Special2, 8f);
            hud.Indicators.SyncCooldownRemaining(AbilitySlot.Special2, 2.6f);
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2 });
            bus.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Building });
            bus.RaiseDustAwardCollected(new DustAwardCollectedPayload { Amount = 25, Source = DustAwardSource.Extractor });
        }

        /// <summary>The rec_05 stress frame: the boss bar, 18 / 100 low, Venom and Suppression, every lens suppressed, the dial sealed.</summary>
        private static void StressState(StoryHUD hud) {
            EventBus bus = EventBus.Instance;
            hud.SetLevelTitle("campaign_level_pompeii");
            hud.SetObjective("pompeii_objective_evacuate");
            StoryManager story = StoryManager.Instance;
            if (story != null) story.ChronalRewindsRemaining = 1;
            SetDust(1250);
            hud.InitializePlayerVitals(30, 100, 40f);
            hud.InitializePlayerVitals(18, 100, 40f);
            hud.HpDecor.LagValue = 24f;
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 40f, NormalizedValue = 0.4f });
            bus.RaiseBlockChargesChanged(new BlockChargesPayload { PlayerIndex = 0, CurrentCharges = 1, MaxCharges = 3 });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 2.2f, Intensity = 1f });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Suppression, Duration = 1.6f, Intensity = 1f });
            foreach (AbilitySlot slot in new[] { AbilitySlot.Special1, AbilitySlot.Special2, AbilitySlot.MovementAbility, AbilitySlot.Ultimate }) {
                bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = slot, State = AbilitySlotLockState.Suppressed });
            }
            bus.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 3.4f });
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 18f, Tier = IntegrityTier.Fractured, LivingExtractors = 2 });
            story?.LockIntegrityAtPreBoss();
            bus.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Ready });
            bus.RaiseDustAwardCollected(new DustAwardCollectedPayload { Amount = 25, Source = DustAwardSource.Extractor });
            // rec_05 draws the tracker at about 470 / 660 above the 412 / 660 fill.
            hud.ShowBossBar("boss_vulcan_decimator_name", 470, 660, new[] { 0.5f });
            bus.RaiseBossPhaseChanged(1);
            hud.UpdateBossHP(412);
        }

        [UiCaptureTarget("huds_calm", SettleFrames = 14)]
        public static Node HudsCalm(UiCaptureContext context) {
            Control page = HudStage("einstein");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => CalmState(hud);
            return page;
        }

        [UiCaptureTarget("huds_calm_140", SettleFrames = 14)]
        public static Node HudsCalm140(UiCaptureContext context) {
            Control page = HudStage("einstein");
            HudScale(1.4f);
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => CalmState(hud);
            return page;
        }

        [UiCaptureTarget("huds_stress", SettleFrames = 14)]
        public static Node HudsStress(UiCaptureContext context) {
            Control page = HudStage("einstein");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => StressState(hud);
            return page;
        }

        [UiCaptureTarget("huds_boss", SettleFrames = 14)]
        public static Node HudsBoss(UiCaptureContext context) {
            Control page = HudStage("einstein");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                hud.ShowBossBar("boss_archive_prime_name", 640, 1000, new[] { 0.5f });
                EventBus.Instance.RaiseBossPhaseChanged(1);
                hud.UpdateBossHP(520);
            };
            return page;
        }

        [UiCaptureTarget("huds_140", SettleFrames = 14)]
        public static Node Huds140(UiCaptureContext context) {
            Control page = HudStage("einstein");
            HudScale(1.4f);
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => StressState(hud);
            return page;
        }

        [UiCaptureTarget("huds_boss_140", SettleFrames = 14)]
        public static Node HudsBoss140(UiCaptureContext context) {
            Control page = HudStage("einstein");
            HudScale(1.4f);
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                hud.ShowBossBar("boss_iron_chancellor_name", 640, 1000, new[] { 0.5f });
                EventBus.Instance.RaiseBossPhaseChanged(1);
                hud.UpdateBossHP(520);
            };
            return page;
        }

        /// <summary>MOT-A row 11 at its start: HP drops to 18 / 100 and the frame is taken two frames later.</summary>
        [UiCaptureTarget("huds_low_first", SettleFrames = 2)]
        public static Node HudsLowFirst(UiCaptureContext context) {
            Control page = HudStage("einstein");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                hud.InitializePlayerVitals(18, 100, 64f);
                hud.HpDecor.LagValue = 24f;
            };
            return page;
        }

        /// <summary>AC17: the calm frame with the Integrity clock paused (the wedge in ink2 at 0.6, no siphon streams).</summary>
        [UiCaptureTarget("huds_frozen_dial", SettleFrames = 14)]
        public static Node HudsFrozenDial(UiCaptureContext context) {
            Control page = HudStage("einstein");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                EventBus.Instance.RaiseTimelineIntegrityChanged(new IntegrityPayload {
                    Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2, Frozen = true
                });
            };
            return page;
        }

        /// <summary>Act III: the Beacon badge with three anchor pips, a Snap splitting the third.</summary>
        [UiCaptureTarget("huds_anchors", SettleFrames = 14)]
        public static Node HudsAnchors(UiCaptureContext context) {
            Control page = HudStage("joan");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 3, Max = 3 });
                EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
            };
            return page;
        }

        /// <summary>
        /// The compact HUD mock's third frame (HUD-COMPACT, 2026-10-27): Act III with two of three anchors, the Ultimate ready
        /// with its READY pill and Defy armed, S1 cooling at 6.1, Time Freeze cooling (38 s), the damage slot empty while Time
        /// Dilation runs.
        /// </summary>
        [UiCaptureTarget("huds_states", SettleFrames = 14)]
        public static Node HudsStates(UiCaptureContext context) {
            Control page = HudStage("einstein");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                EventBus bus = EventBus.Instance;
                StoryManager story = StoryManager.Instance;
                if (story != null) story.ChronalRewindsRemaining = 2;
                hud.InitializePlayerVitals(46, 100, 100f);
                hud.HpDecor.LagValue = 52f;
                bus.RaiseBlockChargesChanged(new BlockChargesPayload { PlayerIndex = 0, CurrentCharges = 3, MaxCharges = 3 });
                bus.RaiseStatusEffectCleared(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.None });
                bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.TimeDilation, Duration = 3.4f, Intensity = 1f });
                hud.Indicators.SyncCooldownRemaining(AbilitySlot.Special2, 0f);
                hud.Indicators.StartCooldown(AbilitySlot.Special1, 8f);
                hud.Indicators.SyncCooldownRemaining(AbilitySlot.Special1, 6.1f);
                bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 100f, NormalizedValue = 1f, IsFull = true });
                bus.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Ready });
                bus.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Cooldown, SecondsRemaining = 38f });
                bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 31f, Tier = IntegrityTier.Fractured, LivingExtractors = 2 });
                bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
            };
            return page;
        }

        /// <summary>Act III at zero anchors: the hollow last-stand Beacon and three spent pips.</summary>
        [UiCaptureTarget("huds_anchors_zero", SettleFrames = 14)]
        public static Node HudsAnchorsZero(UiCaptureContext context) {
            Control page = HudStage("tubman");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 0, Max = 3 });
            };
            return page;
        }

        /// <summary>UI_SPEC_GLASS 9.2: the checkpoint toast (top centre) and the boss intro card (centred) at once.</summary>
        [UiCaptureTarget("huds_toasts", SettleFrames = 20)]
        public static Node HudsToasts(UiCaptureContext context) {
            Control page = HudStage("mozart");
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                EventBus.Instance.RaiseCheckpointReached("capture_toast_checkpoint");
                hud.ShowBossIntroCard("boss_vulcan_decimator_name", 5f);
            };
            return page;
        }

        /// <summary>The toast and the intro card at UI scale 1.4 (the card's name fitted to the canvas less 40 px a side).</summary>
        [UiCaptureTarget("huds_toasts_140", SettleFrames = 20)]
        public static Node HudsToasts140(UiCaptureContext context) {
            Control page = HudStage("mozart");
            HudScale(1.4f);
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                EventBus.Instance.RaiseCheckpointReached("capture_toast_checkpoint");
                hud.ShowBossIntroCard("boss_iron_chancellor_name", 5f);
            };
            return page;
        }

        [UiCaptureTarget("huds_boss_names", SettleFrames = 20)]
        public static Node HudsBossNames(UiCaptureContext context) => BossNamesPage(1f);

        [UiCaptureTarget("huds_boss_names_140", SettleFrames = 20)]
        public static Node HudsBossNames140(UiCaptureContext context) => BossNamesPage(1.4f);

        /// <summary>
        /// Every campaign boss's bar at <paramref name="scale"/>: one 1920 x 1080 HUD per name in its own viewport, the
        /// top band around the bar cropped out at 1:1 and laid in a grid (the D11 fit at a glance): 2 x 8 at both scales; the
        /// compact bar (2026-10-27) is 640 px wide at 1.0 and 834 px on screen at 1.4, where a dropped phase line makes it taller.
        /// </summary>
        private static Control BossNamesPage(float scale) {
            Control page = HudStage("einstein");
            HudScale(scale);
            bool large = scale > 1.2f;
            var grid = new GridContainer {
                Name = "Names", Columns = 2, Position = large ? new Vector2(94f, 24f) : new Vector2(286f, 24f)
            };
            grid.AddThemeConstantOverride("h_separation", 20);
            grid.AddThemeConstantOverride("v_separation", large ? 6 : 8);
            page.AddChild(grid);
            // The band's crop on screen: the compact bar spans 640-1280 at 1.0 (header from y 20, the notch diamond to about
            // y 92) and 745-1578 at 1.4 (from y 28, the diamond to about y 140 when the phase line drops below a long name).
            var crop = large ? new Rect2(735f, 20f, 856f, 124f) : new Rect2(628f, 12f, 664f, 90f);
            List<string> keys = BossKeys();
            foreach (string key in keys) {
                var port = new SubViewport {
                    Size = new Vector2I(1920, 1080), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always
                };
                page.AddChild(port);
                StoryHUD hud = StoryHUD.CreateDefault();
                port.AddChild(hud);
                var view = new TextureRect {
                    Texture = new AtlasTexture { Atlas = port.GetTexture(), Region = crop },
                    CustomMinimumSize = crop.Size, StretchMode = TextureRect.StretchModeEnum.Keep
                };
                grid.AddChild(view);
                Callable.From(() => {
                    if (!GodotObject.IsInstanceValid(hud)) return;
                    hud.ShowBossBar(key, 640, 1000, new[] { 0.5f });
                    EventBus.Instance.RaiseBossPhaseChanged(1);
                }).CallDeferred();
            }
            return page;
        }

        private static List<string> BossKeys() {
            var keys = new List<string>();
            foreach (string file in DirAccess.GetFilesAt("res://resources/Bosses")) {
                if (!file.EndsWith(".tres")) continue;
                using var access = Godot.FileAccess.Open($"res://resources/Bosses/{file}", Godot.FileAccess.ModeFlags.Read);
                var match = System.Text.RegularExpressions.Regex.Match(access.GetAsText(), "DisplayNameKey = \"([^\"]+)\"");
                if (match.Success) keys.Add(match.Groups[1].Value);
            }
            keys.Sort(System.StringComparer.Ordinal);
            return keys;
        }
    }
}
