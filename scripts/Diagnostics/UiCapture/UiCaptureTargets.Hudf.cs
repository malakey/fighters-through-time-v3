using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using Godot;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Diagnostics {

    /// <summary>UR-HUDF's capture targets: the Fighter HUD-F2 top band over a stage plate.</summary>
    public static partial class UiCaptureTargets {

        private const int HudfTick = 60;

        private static void HudfSetScale(float scale) {
            GlobalSaveData global = SaveManager.Instance?.GlobalData;
            if (global != null) global.UiScale = scale;
        }

        /// <summary>A page with the Florence stage plate behind a mounted Fighter HUD; both players configured.</summary>
        private static (Control page, FighterHUD hud) HudfPage() {
            var page = new Control { Name = "HudfPage" };
            var back = new TextureRect {
                Texture = ResourceLoader.Load<Texture2D>("res://assets/placeholders/stages/florence_preview.svg"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            page.AddChild(back);
            var hud = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath).Instantiate<FighterHUD>();
            page.AddChild(hud);
            return (page, hud);
        }

        /// <summary>Configures both players once the HUD is in the tree (its panels bind in _Ready), then applies a mid-match state.</summary>
        private static void HudfStart(FighterHUD hud, int matchMode, bool suddenDeath) {
            hud.ConfigurePlayer(0, AuthoredResources.Load<CharacterData>("res://resources/Characters/joan_data.tres"), 3, 3);
            hud.ConfigurePlayer(1, AuthoredResources.Load<CharacterData>("res://resources/Characters/tesla_data.tres"), 3, 3);
            HudfMidMatch(hud, matchMode, suddenDeath);
        }

        private static FighterStateComponent HudfState(int hp, int maxHp, int stocks, int block, float meter) => new() {
            CurrentHP = hp, MaxHP = maxHp, Stocks = stocks, BlockCharges = block, Influence = FP64.FromFloat(meter)
        };

        private static void HudfMidMatch(FighterHUD hud, int matchMode, bool suddenDeath) {
            FighterRuntimeComponent p1 = new() { StatusType = (int)StatusType.None, SpecialOneCooldownFrames = 3 * HudfTick, MovementCooldownFrames = 90 };
            FighterRuntimeComponent p2 = new() {
                StatusType = (int)StatusType.TimeDilation, StatusFrames = 4 * HudfTick,
                DamageStatusType = (int)StatusType.RadiantBurn, DamageStatusFrames = 2 * HudfTick,
                SpecialTwoCooldownFrames = 40 * HudfTick, KnockoutsSuffered = 1
            };
            hud.ApplyMatchState(true, 136 * HudfTick, HudfTick, matchMode, suddenDeath);
            hud.ApplyPlayerState(0, HudfState(86, 110, 3, 3, 100f), p1, HudfTick);
            hud.ApplyPlayerState(1, HudfState(63, 90, 2, 2, 40f), p2, HudfTick, echoPool: 8f);
        }

        /// <summary>Both players mid-match: statuses, cooling slots, one full meter, a stock lost on P2.</summary>
        [UiCaptureTarget("hudf_versus", SettleFrames = 12)]
        public static Node HudfVersus(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, false);
            return page;
        }

        /// <summary>Time mode: stock pips become the "Stocks lost" counter.</summary>
        [UiCaptureTarget("hudf_time_mode", SettleFrames = 12)]
        public static Node HudfTimeMode(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 1, false);
            return page;
        }

        /// <summary>F22: the phase text replaces the running clock.</summary>
        [UiCaptureTarget("hudf_sudden_death", SettleFrames = 12)]
        public static Node HudfSuddenDeath(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, true);
            return page;
        }

        /// <summary>The KO stamp over the new top band.</summary>
        [UiCaptureTarget("hudf_ko", SettleFrames = 12)]
        public static Node HudfKo(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            var overlay = new FighterPresentationOverlay();
            page.AddChild(overlay);
            context.AfterReady = _ => {
                HudfStart(hud, 0, false);
                overlay.Present(new FighterPresentationPayload {
                    Phase = FighterPresentationPhase.KOStamp, WinnerPlayerID = 0, SubjectPlayerID = 1, DurationSeconds = 30f
                });
            };
            return page;
        }

        /// <summary>UI scale 1.4: the bars shrink before they meet the timer, the chips drop their text.</summary>
        [UiCaptureTarget("hudf_140", SettleFrames = 12)]
        public static Node Hudf140(UiCaptureContext context) {
            HudfSetScale(1.4f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, false);
            return page;
        }
    }
}
