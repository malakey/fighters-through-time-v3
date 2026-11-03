using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using Godot;
using xpTURN.Klotho.Deterministic.Math;

namespace FTT.Diagnostics {

    /// <summary>
    /// UR-HUDF's capture targets, re-pointed at the glass HUD by TG-HUDF: the Fighter HUD-F2 top band over a stage plate.
    /// <c>hudf_versus</c> holds rec_06's state (Joan 86/110 with the tracker from 92, a full meter, S2 cooling 3.1 at 40 %;
    /// Tesla 63/90 from 70, meter 38, two stocks, S1 cooling 1.8 at 70 %, Radiant Burn), so it compares 1:1 with the
    /// render and the three hud_fighter crops. Every target sets the player-slot palette it shows and puts the saved one
    /// back when its page leaves the tree.
    /// </summary>
    public static partial class UiCaptureTargets {

        private const int HudfTick = 60;

        /// <summary>The UI scale as the game applies it: the saved setting (the HUD's transform) and the shared theme (what
        /// Settings rescales); the runner resets both around every target.</summary>
        private static void HudfSetScale(float scale) {
            GlobalSaveData global = SaveManager.Instance?.GlobalData;
            if (global != null) global.UiScale = scale;
            UIPalette.ApplyUiScale(scale);
        }

        /// <summary>A page with the Florence stage plate behind a mounted Fighter HUD at the given palette (restored on exit).</summary>
        private static (Control page, FighterHUD hud) HudfPage(PlayerSlotPalette palette = PlayerSlotPalette.Default) {
            var page = new Control { Name = "HudfPage" };
            GlobalSaveData global = SaveManager.Instance?.GlobalData;
            if (global != null) {
                int saved = global.PlayerSlotPalette;
                global.PlayerSlotPalette = (int)palette;
                page.TreeExiting += () => global.PlayerSlotPalette = saved;
            }
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

        private static CharacterData HudfHero(string id) =>
            AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");

        /// <summary>Configures both players once the HUD is in the tree (its panels bind in _Ready), then applies a mid-match state.</summary>
        private static void HudfStart(FighterHUD hud, int matchMode, bool suddenDeath, string one = "joan", string two = "tesla") {
            hud.ConfigurePlayer(0, HudfHero(one), 3, 3);
            hud.ConfigurePlayer(1, HudfHero(two), 3, 3);
            HudfMidMatch(hud, matchMode, suddenDeath);
        }

        private static FighterStateComponent HudfState(int hp, int maxHp, int stocks, int block, float meter) => new() {
            CurrentHP = hp, MaxHP = maxHp, Stocks = stocks, BlockCharges = block, Influence = FP64.FromFloat(meter)
        };

        /// <summary>rec_06's mid-match state. The cooldowns and the trackers need two passes: the HUD latches a cooldown's
        /// start from the first count it sees and holds the tracker at the HP before a drop.</summary>
        private static void HudfMidMatch(FighterHUD hud, int matchMode, bool suddenDeath) {
            FighterRuntimeComponent p1 = new() { StatusType = (int)StatusType.None, SpecialTwoReuseFrames = 100 };
            FighterRuntimeComponent p2 = new() {
                StatusType = (int)StatusType.None,
                DamageStatusType = (int)StatusType.RadiantBurn, DamageStatusFrames = 2 * HudfTick,
                SpecialOneReuseFrames = 110, KnockoutsSuffered = 1
            };
            hud.ApplyMatchState(true, 136 * HudfTick, HudfTick, matchMode, suddenDeath);
            hud.ApplyPlayerState(0, HudfState(92, 110, 3, 3, 100f), p1, HudfTick);
            hud.ApplyPlayerState(1, HudfState(70, 90, 2, 2, 38f), p2, HudfTick);
            p1.SpecialTwoReuseFrames = 72;    // 1.2 s left of the 2 s reuse delay, 40 % elapsed
            p2.SpecialOneReuseFrames = 36;    // 0.6 s left, 70 % elapsed
            hud.ApplyPlayerState(0, HudfState(86, 110, 3, 3, 100f), p1, HudfTick);
            hud.ApplyPlayerState(1, HudfState(63, 90, 2, 2, 38f), p2, HudfTick);
        }

        /// <summary>rec_06: both players mid-match, one full meter, a stock lost on P2, Radiant Burn on P2.</summary>
        [UiCaptureTarget("hudf_versus", SettleFrames = 12)]
        public static Node HudfVersus(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, false);
            return page;
        }

        /// <summary>
        /// The Momentum pips (MM-HUD): P1 with 2.6 pips and both Specials usable, P2 starved at 0.4 pips with the Ultimate tube's
        /// gold Defy notch lit on P1 (its meter at 100) and dim on P2 (its meter at 38).
        /// </summary>
        [UiCaptureTarget("hudf_momentum", SettleFrames = 12)]
        public static Node HudfMomentum(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => {
                HudfStart(hud, 0, false);
                FighterRuntimeComponent idle = new() { StatusType = (int)StatusType.None };
                hud.ApplyPlayerState(0, HudfState(92, 110, 3, 3, 100f), idle, HudfTick, momentumUnits: FTT.Combat.BasicComboRules.MomentumUnitsPerPip * 26 / 10);
                hud.ApplyPlayerState(1, HudfState(70, 90, 2, 2, 38f), idle, HudfTick, momentumUnits: FTT.Combat.BasicComboRules.MomentumUnitsPerPip * 4 / 10);
            };
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

        /// <summary>F22: the phase line takes the plate's label slot and the plate widens to fit it.</summary>
        [UiCaptureTarget("hudf_sudden_death", SettleFrames = 12)]
        public static Node HudfSuddenDeath(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, true);
            return page;
        }

        /// <summary>Sudden Death at UI scale 1.4: the HP tubes leave the plate no wider room, so the line wraps inside it.</summary>
        [UiCaptureTarget("hudf_sudden_death_140", SettleFrames = 12)]
        public static Node HudfSuddenDeath140(UiCaptureContext context) {
            HudfSetScale(1.4f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, true);
            return page;
        }

        /// <summary>The KO stamp over the top band.</summary>
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

        /// <summary>The pre-match countdown's "3" over the top band (TG-CLOSE fix step: the chrome accent, not player one's colour).</summary>
        [UiCaptureTarget("hudf_countdown", SettleFrames = 12)]
        public static Node HudfCountdown(UiCaptureContext context) => HudfCountdownAt(context, 1f);

        /// <summary>The countdown at UI scale 1.4 (the overlay keeps its 190 px numeral on the HUD's own scale).</summary>
        [UiCaptureTarget("hudf_countdown_140", SettleFrames = 12)]
        public static Node HudfCountdown140(UiCaptureContext context) => HudfCountdownAt(context, 1.4f);

        private static Node HudfCountdownAt(UiCaptureContext context, float scale) {
            HudfSetScale(scale);
            (Control page, FighterHUD hud) = HudfPage();
            var overlay = new FighterPresentationOverlay();
            page.AddChild(overlay);
            context.AfterReady = _ => {
                HudfStart(hud, 0, false);
                overlay.Present(new FighterPresentationPayload {
                    Phase = FighterPresentationPhase.Countdown, CountdownValue = 3, WinnerPlayerID = -1, SubjectPlayerID = -1,
                    DurationSeconds = 30f
                });
            };
            return page;
        }

        /// <summary>UI scale 1.4: the clusters keep their corners, the HP tubes shrink before they meet the plate, the badges drop their text.</summary>
        [UiCaptureTarget("hudf_140", SettleFrames = 12)]
        public static Node Hudf140(UiCaptureContext context) {
            HudfSetScale(1.4f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, false);
            return page;
        }

        /// <summary>Low HP: P1 at 20 / 110 (the low fill and its pulse) with the tracker from 52, P2 at 40 / 90 (the caution band) with a Rally echo.</summary>
        [UiCaptureTarget("hudf_low_hp", SettleFrames = 12)]
        public static Node HudfLowHp(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => {
                hud.ConfigurePlayer(0, HudfHero("joan"), 3, 3);
                hud.ConfigurePlayer(1, HudfHero("tesla"), 3, 3);
                FighterRuntimeComponent idle = new() { StatusType = (int)StatusType.None };
                hud.ApplyMatchState(true, 41 * HudfTick, HudfTick, 0, false);
                hud.ApplyPlayerState(0, HudfState(52, 110, 1, 0, 64f), idle, HudfTick);
                hud.ApplyPlayerState(1, HudfState(40, 90, 2, 1, 12f), idle, HudfTick, echoPool: 9f);
                hud.ApplyPlayerState(0, HudfState(20, 110, 1, 0, 64f), idle, HudfTick, echoPool: 6f);
            };
            return page;
        }

        /// <summary>Both fighters with both statuses (damage left, control right on both sides) and a cooling lens each.</summary>
        [UiCaptureTarget("hudf_statuses", SettleFrames = 12)]
        public static Node HudfStatuses(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => {
                hud.ConfigurePlayer(0, HudfHero("cleopatra"), 3, 3);
                hud.ConfigurePlayer(1, HudfHero("lincoln"), 3, 3);
                FighterRuntimeComponent one = new() {
                    DamageStatusType = (int)StatusType.Venom, DamageStatusFrames = 5 * HudfTick,
                    StatusType = (int)StatusType.Root, StatusFrames = 2 * HudfTick, MovementCooldownFrames = 240
                };
                FighterRuntimeComponent two = new() {
                    DamageStatusType = (int)StatusType.RadiantBurn, DamageStatusFrames = 3 * HudfTick,
                    StatusType = (int)StatusType.StaticCharge, StatusFrames = 6 * HudfTick, SpecialTwoReuseFrames = 600
                };
                hud.ApplyMatchState(true, 95 * HudfTick, HudfTick, 0, false);
                hud.ApplyPlayerState(0, HudfState(72, 100, 3, 2, 55f), one, HudfTick);
                hud.ApplyPlayerState(1, HudfState(118, 130, 3, 3, 90f), two, HudfTick, echoPool: 7f);
                one.MovementCooldownFrames = 96;
                two.SpecialTwoReuseFrames = 420;
                hud.ApplyPlayerState(0, HudfState(72, 100, 3, 2, 55f), one, HudfTick);
                hud.ApplyPlayerState(1, HudfState(118, 130, 3, 3, 90f), two, HudfTick, echoPool: 7f);
            };
            return page;
        }

        /// <summary>The Blue / Orange player-slot palette: slot shapes and stock stars follow it (the chrome stays gold).</summary>
        [UiCaptureTarget("hudf_palette_blue_orange", SettleFrames = 12)]
        public static Node HudfPaletteBlueOrange(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage(PlayerSlotPalette.BlueOrange);
            context.AfterReady = _ => HudfStart(hud, 0, false);
            return page;
        }

        /// <summary>The High-Contrast palette: player 2's black reads as its light edge on the dark band (OwnerOnDark); shape tells them apart.</summary>
        [UiCaptureTarget("hudf_palette_high_contrast", SettleFrames = 12)]
        public static Node HudfPaletteHighContrast(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage(PlayerSlotPalette.HighContrast);
            context.AfterReady = _ => HudfStart(hud, 0, false);
            return page;
        }

        /// <summary>The two longest names (Mozart, Shakespeare): each shrinks to its header, never below 18 px on screen.</summary>
        [UiCaptureTarget("hudf_long_names", SettleFrames = 12)]
        public static Node HudfLongNames(UiCaptureContext context) {
            HudfSetScale(1f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, false, "mozart", "shakespeare");
            return page;
        }

        /// <summary>The two longest names at UI scale 1.4.</summary>
        [UiCaptureTarget("hudf_long_names_140", SettleFrames = 12)]
        public static Node HudfLongNames140(UiCaptureContext context) {
            HudfSetScale(1.4f);
            (Control page, FighterHUD hud) = HudfPage();
            context.AfterReady = _ => HudfStart(hud, 0, false, "mozart", "shakespeare");
            return page;
        }
    }
}
