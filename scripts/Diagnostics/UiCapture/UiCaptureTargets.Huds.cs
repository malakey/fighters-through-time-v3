using FTT.Core;
using FTT.UI;
using Godot;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-HUDS' capture targets: the Story HUD (HUD-S1) with a fake hero and fake state.
    /// <c>huds_calm</c>, <c>huds_stress</c>, <c>huds_boss</c>, <c>huds_boss_140</c> (the longest boss name at UI scale 1.4) and <c>huds_140</c> (the stress frame at UI scale 1.4).
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
            return page;
        }

        private static StoryHUD AddCaptureHud(Control page) {
            StoryHUD hud = StoryHUD.CreateDefault();
            page.AddChild(hud);
            return hud;
        }

        private static void CalmState(StoryHUD hud) {
            EventBus bus = EventBus.Instance;
            hud.SetLevelTitle("campaign_level_orleans");
            hud.SetObjective("florence_objective_reach_boss");
            StoryManager story = StoryManager.Instance;
            if (story != null) {
                story.ChronalRewindsRemaining = 3;
                story.CollectDust(1250);
            }
            hud.InitializePlayerVitals(72, 100, 64f);
            hud.HpDecor.LagValue = 72f;
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 64f, NormalizedValue = 0.64f });
            bus.RaiseBlockChargesChanged(new BlockChargesPayload { PlayerIndex = 0, CurrentCharges = 2, MaxCharges = 3 });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4f, Intensity = 1f });
            bus.RaiseCooldownStarted(new CooldownPayload { PlayerIndex = 0, Slot = AbilitySlot.Special2, Duration = 8f });
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2 });
            bus.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Building });
        }

        private static void StressState(StoryHUD hud) {
            EventBus bus = EventBus.Instance;
            hud.SetLevelTitle("campaign_level_orleans");
            hud.SetObjective("florence_objective_reach_boss");
            StoryManager story = StoryManager.Instance;
            if (story != null) {
                story.ChronalRewindsRemaining = 1;
                story.CollectDust(1250);
            }
            hud.InitializePlayerVitals(18, 100, 40f);
            hud.HpDecor.LagValue = 30f;
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 40f, NormalizedValue = 0.4f });
            bus.RaiseBlockChargesChanged(new BlockChargesPayload { PlayerIndex = 0, CurrentCharges = 1, MaxCharges = 3 });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 2f, Intensity = 1f });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Suppression, Duration = 1.4f, Intensity = 1f });
            bus.RaiseCooldownStarted(new CooldownPayload { PlayerIndex = 0, Slot = AbilitySlot.Special1, Duration = 10f });
            bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = AbilitySlot.Special2, State = AbilitySlotLockState.Dormant });
            bus.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload { Slot = AbilitySlot.MovementAbility, State = AbilitySlotLockState.Suppressed });
            bus.RaiseTimeFreezeStateChanged(new TimeFreezePayload { State = TimeFreezeState.Active, SecondsRemaining = 3.2f });
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 18f, Tier = IntegrityTier.Fractured, LivingExtractors = 3 });
            bus.RaiseDefySealChanged(new DefySealPayload { PlayerIndex = 0, State = DefySealState.Ready });
        }

        [UiCaptureTarget("huds_calm", SettleFrames = 14)]
        public static Node HudsCalm(UiCaptureContext context) {
            Control page = HudStage("einstein");
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
            SaveManager save = SaveManager.Instance;
            if (save?.GlobalData != null) save.GlobalData.UiScale = 1.4f;
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                StressState(hud);
                hud.ShowBossBar("boss_archive_prime_name", 640, 1000, new[] { 0.5f });
            };
            return page;
        }

        [UiCaptureTarget("huds_boss_140", SettleFrames = 14)]
        public static Node HudsBoss140(UiCaptureContext context) {
            Control page = HudStage("einstein");
            SaveManager save = SaveManager.Instance;
            if (save?.GlobalData != null) save.GlobalData.UiScale = 1.4f;
            StoryHUD hud = AddCaptureHud(page);
            context.AfterReady = _ => {
                CalmState(hud);
                hud.ShowBossBar("boss_iron_chancellor_name", 640, 1000, new[] { 0.5f });
                EventBus.Instance.RaiseBossPhaseChanged(1);
                hud.UpdateBossHP(520);
            };
            return page;
        }
    }
}
