using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 8 B2. The authored production Fighter HUD scene: that it instantiates
/// at the path the driver loads, that every widget tracks the deterministic
/// component values it is given, and that <c>HudOpacity</c> is read live rather
/// than once at load.
///
/// The HUD is driven through <see cref="FighterHUD.ApplyPlayerState"/> and
/// <see cref="FighterHUD.ApplyMatchState"/> with hand-built component structs
/// rather than a real simulation. That is the same path
/// <c>FighterSimulationDriver</c> uses — it pulls the components and hands the
/// values over — so this exercises the production code without standing up a
/// match, and it keeps the HUD unable to reach into <c>scripts/FighterSim/</c>
/// on its own.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHudSceneTests {

    private const int TickRate = 60;

    [TestCase]
    public void TheAuthoredSceneInstantiatesAtThePathTheDriverLoads() {
        AssertThat(ResourceLoader.Exists(FighterHUD.ScenePath)).OverrideFailureMessage(
            $"{FighterHUD.ScenePath} is the path FighterSimulationDriver instantiates "
            + "for every stage.").IsTrue();
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        AssertObject(packed).IsNotNull();
        Node root = AutoFree(packed.Instantiate());
        AssertThat(root is FighterHUD).OverrideFailureMessage(
            $"{FighterHUD.ScenePath} root is {root.GetType().Name}, not FighterHUD.").IsTrue();
    }

    [TestCase]
    public void BarsAndPipsTrackTheComponentValuesTheyAreGiven() {
        FighterHUD hud = Mount();
        try {
            hud.ConfigurePlayer(0, null, maxStocks: 3, maxShieldCharges: 3);
            hud.ConfigurePlayer(1, null, maxStocks: 3, maxShieldCharges: 3);

            AssertThat(hud.StockPipCapacity(0)).IsEqual(3);
            AssertThat(hud.ShieldPipCapacity(0)).IsEqual(3);

            hud.ApplyPlayerState(0, State(hp: 40, maxHp: 100, stocks: 2, block: 1, influence: 75f),
                Runtime(), TickRate);
            AssertThat(hud.HPFraction(0)).IsEqual(0.4f);
            AssertThat(hud.MeterFraction(0)).IsEqual(0.75f);
            AssertThat(hud.LitStockPips(0)).IsEqual(2);
            AssertThat(hud.LitShieldPips(0)).IsEqual(1);

            // The second panel is independent of the first.
            hud.ApplyPlayerState(1, State(hp: 100, maxHp: 100, stocks: 3, block: 3, influence: 0f),
                Runtime(), TickRate);
            AssertThat(hud.HPFraction(1)).IsEqual(1f);
            AssertThat(hud.LitStockPips(1)).IsEqual(3);
            AssertThat(hud.HPFraction(0)).IsEqual(0.4f);

            // A spent pip dims rather than disappearing, so the panel never
            // reflows mid-match and the starting capacity stays readable.
            hud.ApplyPlayerState(0, State(hp: 0, maxHp: 100, stocks: 0, block: 0, influence: 0f),
                Runtime(), TickRate);
            AssertThat(hud.LitStockPips(0)).IsEqual(0);
            AssertThat(hud.StockPipCapacity(0)).IsEqual(3);
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void PipCapacityFollowsTheAuthoredShieldChargesAndMatchStockRule() {
        FighterHUD hud = Mount();
        try {
            hud.ConfigurePlayer(0, null, maxStocks: 5, maxShieldCharges: 4);
            AssertThat(hud.StockPipCapacity(0)).IsEqual(5);
            AssertThat(hud.ShieldPipCapacity(0)).IsEqual(4);

            // Reconfiguring rebuilds rather than appending — a rematch with
            // different stock rules must not leave stale pips behind.
            hud.ConfigurePlayer(0, null, maxStocks: 2, maxShieldCharges: 3);
            AssertThat(hud.StockPipCapacity(0)).IsEqual(2);
            AssertThat(hud.ShieldPipCapacity(0)).IsEqual(3);

            // A degenerate rule still leaves something on screen.
            hud.ConfigurePlayer(0, null, maxStocks: 0, maxShieldCharges: 0);
            AssertThat(hud.StockPipCapacity(0)).IsEqual(1);
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void ThreeRadialCooldownsTrackAuthoritativeState() {
        // M25 (Package 12 W5, HUD_CONTRACT): S1 / S2 / Movement radials replace the
        // Ultimate plate (the Echo Step radial left on 2026-10-25).
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);

            FighterRuntimeComponent cooling = Runtime();
            cooling.SpecialOneReuseFrames = 120;
            cooling.SpecialTwoReuseFrames = 0;
            cooling.MovementCooldownFrames = 30;
            hud.ApplyPlayerState(0, State(influence: 40f), cooling, TickRate);

            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.SpecialOne)).IsFalse();
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.SpecialTwo)).IsTrue();
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.Movement)).IsFalse();
            // A cooldown that just started shows an empty arc; a ready one a full arc.
            AssertThat(hud.CooldownFraction(0, FighterCooldownSlot.SpecialOne)).IsEqual(0f);
            AssertThat(hud.CooldownFraction(0, FighterCooldownSlot.SpecialTwo)).IsEqual(1f);

            // Half-way through the cooldown the radial is half full: the arc reads
            // the authoritative remaining count against the count it started from.
            cooling.SpecialOneReuseFrames = 60;
            hud.ApplyPlayerState(0, State(influence: 40f), cooling, TickRate);
            AssertFloat(hud.CooldownFraction(0, FighterCooldownSlot.SpecialOne)).IsEqualApprox(0.5f, 0.001f);

            // Meter never gates a radial any more: a finished cooldown is ready.
            hud.ApplyPlayerState(0, State(influence: 20f), Runtime(), TickRate);
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.SpecialOne)).IsTrue();
            // Distinct glyphs: readiness never rides on colour alone.
            AssertThat(hud.CooldownText(0, FighterCooldownSlot.SpecialOne))
                .IsEqual(TranslationServer.Translate("fighter_hud_cooldown_special1").ToString());
            AssertThat(hud.CooldownText(0, FighterCooldownSlot.Movement))
                .IsEqual(TranslationServer.Translate("fighter_hud_cooldown_movement").ToString());
        } finally {
            hud.Free();
        }
    }

    /// <summary>
    /// D10 (2026-10-11, ledger <c>USER-NOT-YET-FLASH</c>): the driver's
    /// classifier hands the HUD the slots the ability lockout alone refused, and
    /// the HUD flashes that player's Special icon or meter bar — a red
    /// <c>SelfModulate</c> tint plus a contracting frame. The readiness alpha and
    /// the M25 ready flash live on <c>Modulate</c>, which stays untouched.
    /// </summary>
    [TestCase]
    public void ANotYetRefusalFlashesThatPlayersIconOrMeter() {
        FighterHUD hud = Mount();
        try {
            hud.ConfigurePlayer(0, null, 3, 3);
            hud.ConfigurePlayer(1, null, 3, 3);
            FighterRuntimeComponent cooling = Runtime();
            cooling.SpecialOneReuseFrames = 90;
            hud.ApplyPlayerState(0, State(influence: 40f), Runtime(), TickRate);
            hud.ApplyPlayerState(1, State(influence: 100f), cooling, TickRate);

            const string Two = "SafeArea/P2_Status/Body/Column";
            const string One = "SafeArea/P1_Status/Body/Column";
            var twoSpecialTwo = hud.GetNode<Label>($"{Two}/PipRow/Cooldowns/S2_Cooldown");
            var twoSpecialOne = hud.GetNode<Label>($"{Two}/PipRow/Cooldowns/S1_Cooldown");
            var twoMeter = hud.GetNode<ProgressBar>($"{Two}/MeterBar");
            var oneSpecialTwo = hud.GetNode<Label>($"{One}/PipRow/Cooldowns/S2_Cooldown");
            var oneMeter = hud.GetNode<ProgressBar>($"{One}/MeterBar");
            Color readinessTwo = twoSpecialTwo.Modulate;
            Color readinessOne = twoSpecialOne.Modulate;

            hud.FlashLockoutRefusal(1, FighterLockoutRefusal.SpecialTwo | FighterLockoutRefusal.Ultimate);

            AssertThat(twoSpecialTwo.SelfModulate).IsEqual(NotYetFlash.Tint);
            AssertThat(twoMeter.SelfModulate).IsEqual(NotYetFlash.Tint);
            AssertObject(NotYetFlash.FrameOf(twoSpecialTwo)).IsNotNull();
            AssertThat(NotYetFlash.FrameOf(twoSpecialTwo).Visible).IsTrue();
            // TG-CLOSE (TG-HUDF kit gap 4): the Ultimate tube's refusal draws a frame that leans with the tube (P2's
            // mirrored, "\"), not the rectangle round it.
            AssertObject(NotYetFlash.FrameOf(twoMeter)).IsNull();
            AssertObject(NotYetFlash.TubeOf(twoMeter)).IsNotNull();
            AssertThat(NotYetFlash.TubeOf(twoMeter).Visible).IsTrue();
            AssertThat(NotYetFlash.TubeLeansMirrored(twoMeter)).IsTrue();
            AssertThat(twoSpecialTwo.Modulate)
                .OverrideFailureMessage("The readiness alpha (Modulate) is not the flash's to touch.")
                .IsEqual(readinessTwo);

            // Only the named slots, only that player.
            AssertThat(twoSpecialOne.SelfModulate).IsEqual(Colors.White);
            AssertThat(twoSpecialOne.Modulate).IsEqual(readinessOne);
            AssertObject(NotYetFlash.FrameOf(twoSpecialOne)).IsNull();
            AssertThat(oneSpecialTwo.SelfModulate).IsEqual(Colors.White);
            AssertThat(oneMeter.SelfModulate).IsEqual(Colors.White);
            AssertObject(NotYetFlash.FrameOf(oneSpecialTwo)).IsNull();
            AssertObject(NotYetFlash.FrameOf(oneMeter)).IsNull();

            // A repeat flash restarts on the same frame instead of stacking a second.
            int children = twoSpecialTwo.GetChildCount();
            Panel frame = NotYetFlash.FrameOf(twoSpecialTwo);
            hud.FlashLockoutRefusal(1, FighterLockoutRefusal.SpecialTwo);
            AssertThat(twoSpecialTwo.GetChildCount()).IsEqual(children);
            AssertThat(NotYetFlash.FrameOf(twoSpecialTwo) == frame).IsTrue();
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void EachNameTagCarriesItsSlotShape() {
        // G12: ownership never depends on colour alone — P1 ▲ / P2 ●.
        FighterHUD hud = Mount();
        try {
            hud.ConfigurePlayer(0, null, 3, 3);
            hud.ConfigurePlayer(1, null, 3, 3);
            AssertThat(hud.SlotBadgeText(0)).IsEqual(FTT.Combat.PlayerSlotPalettes.PlayerOneGlyph);
            AssertThat(hud.SlotBadgeText(1)).IsEqual(FTT.Combat.PlayerSlotPalettes.PlayerTwoGlyph);
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void PlayerPanelsAnchorToTheTopAndTheClockStaysTopCenter() {
        // UR-HUDF (HUD-F2 "top versus bars", UI_SPEC 4.5): both clusters hang from the TOP
        // corners (margin 36 / 22), replacing M-29's bottom anchors (design :2617); the match
        // timer stays top-centre. TG-HUDF (UI_SPEC_GLASS 4.5): the margins are screen pixels at
        // every UI scale, so the offsets are the margins divided by the HUD's scale (36 / 22 at
        // 1.0). The fill-mode and clock checks are unchanged.
        FighterHUD hud = Mount();
        try {
            var one = hud.GetNode<Control>("SafeArea/P1_Status");
            var two = hud.GetNode<Control>("SafeArea/P2_Status");
            float scale = UIPalette.CurrentUiScale;

            AssertFloat(one.AnchorTop).IsEqual(0f);
            AssertFloat(one.AnchorBottom).IsEqual(0f);
            AssertFloat(one.AnchorLeft).IsEqual(0f);
            AssertFloat(one.OffsetTop * scale).IsEqualApprox(22f, 0.01f);
            AssertFloat(one.OffsetLeft * scale).IsEqualApprox(36f, 0.01f);

            AssertFloat(two.AnchorTop).IsEqual(0f);
            AssertFloat(two.AnchorBottom).IsEqual(0f);
            AssertFloat(two.AnchorLeft).IsEqual(1f);
            AssertFloat(two.AnchorRight).IsEqual(1f);
            AssertFloat(two.OffsetRight * scale).IsEqualApprox(-36f, 0.01f);

            // HP fills stay anchored at the outer edges (plan item 14, Q2): P2's bar still
            // fills end-to-begin (fill_mode = 1), as does its meter.
            AssertThat(hud.GetNode<ProgressBar>("SafeArea/P2_Status/Body/Column/HPBar")
                .Get("fill_mode").AsInt32()).IsEqual(1);
            AssertThat(hud.GetNode<ProgressBar>("SafeArea/P1_Status/Body/Column/HPBar")
                .Get("fill_mode").AsInt32()).IsEqual(0);

            // Match timer stays top-center (design :2622).
            var clock = hud.GetNode<Control>("SafeArea/MatchClock");
            AssertFloat(clock.AnchorTop).IsEqual(0f);
            AssertFloat(clock.AnchorLeft).IsEqual(0.5f);
            AssertFloat(clock.AnchorRight).IsEqual(0.5f);
        } finally {
            hud.Free();
        }
    }

    /// <summary>
    /// F24: both status slots render at once, at fixed positions. The old single
    /// pip chose one winner and hid the other, which is exactly what the contract
    /// forbids — and the data for both was always present on the component.
    /// </summary>
    [TestCase]
    public void BothStatusSlotsRenderIndependentlyAndKeepTheirFixedPositions() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);

            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: true)).IsFalse();
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: false)).IsFalse();
            // An empty slot keeps its reserved space rather than collapsing.
            AssertThat(hud.StatusSlotReserved(0, damageSlot: true)).IsTrue();
            AssertThat(hud.StatusSlotReserved(0, damageSlot: false)).IsTrue();

            // Root alone: the CONTROL slot fills, the damage slot stays reserved.
            FighterRuntimeComponent rooted = Runtime();
            rooted.StatusType = (int)StatusType.Root;
            rooted.StatusFrames = 5 * TickRate;
            hud.ApplyPlayerState(0, State(), rooted, TickRate);
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: false)).IsTrue();
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: true)).IsFalse();
            AssertThat(hud.StatusText(0, damageSlot: false))
                .IsEqual(TranslationServer.Translate("status_root").ToString());

            // Venom lands in the DAMAGE slot without evicting Root.
            FighterRuntimeComponent both = rooted;
            both.DamageStatusType = (int)StatusType.Venom;
            both.DamageStatusFrames = 3 * TickRate;
            hud.ApplyPlayerState(0, State(), both, TickRate);
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: true)).IsTrue();
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: false)).IsTrue();
            AssertThat(hud.StatusText(0, damageSlot: true))
                .IsEqual(TranslationServer.Translate("status_venom").ToString());

            // Hiding one indicator must not shift the other: the panel is
            // fixed-position and both slots keep their reserved space and order.
            AssertThat(hud.StatusSlotIndex(0, damageSlot: true))
                .IsLess(hud.StatusSlotIndex(0, damageSlot: false));
            AssertThat(hud.StatusSlotReserved(0, damageSlot: true)).IsTrue();
            AssertThat(hud.StatusSlotReserved(0, damageSlot: false)).IsTrue();

            // Clearing both empties the slots again.
            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: true)).IsFalse();
            AssertThat(hud.StatusSlotOccupied(0, damageSlot: false)).IsFalse();
        } finally {
            hud.Free();
        }
    }

    /// <summary>
    /// F24 is explicit that P2's slots keep their semantic order rather than
    /// mirroring it: the damage slot is the left one on both sides, so one icon
    /// never means two different things.
    /// </summary>
    [TestCase]
    public void PlayerTwoSlotsAreNotReversed() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(1, null, 3, 3);

            FighterRuntimeComponent burning = Runtime();
            burning.DamageStatusType = (int)StatusType.RadiantBurn;
            burning.DamageStatusFrames = 4 * TickRate;
            burning.StatusType = (int)StatusType.TimeDilation;
            burning.StatusFrames = 4 * TickRate;
            hud.ApplyPlayerState(1, State(), burning, TickRate);

            AssertThat(hud.StatusText(1, damageSlot: true))
                .IsEqual(TranslationServer.Translate("status_radiantburn").ToString());
            AssertThat(hud.StatusText(1, damageSlot: false))
                .IsEqual(TranslationServer.Translate("status_timedilation").ToString());
            AssertThat(hud.StatusSlotIndex(1, damageSlot: true)
                    < hud.StatusSlotIndex(1, damageSlot: false))
                .OverrideFailureMessage("P2's damage slot must stay to the LEFT of its control slot.")
                .IsTrue();
            // Same order as P1: one icon never means two different things.
            AssertThat(hud.StatusSlotIndex(1, damageSlot: true))
                .IsEqual(hud.StatusSlotIndex(0, damageSlot: true));
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void TheMatchClockFollowsTheTimerFlagAndFormatsTimedModes() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");

            // V7: a disabled timer hides the clock in any mode; an enabled one
            // shows it — including Stock, which now defaults to 8:00.
            hud.ApplyMatchState(timerEnabled: false, 480 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsFalse();

            hud.ApplyMatchState(timerEnabled: true, 480 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsTrue();
            AssertThat(hud.TimerText).IsEqual("08:00");

            hud.ApplyMatchState(timerEnabled: true, 65 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsTrue();
            // Seconds are zero-padded, so 01:05 never renders as "1:5", and minutes are padded too (mm:ss, UR-HUDF).
            AssertThat(hud.TimerText).IsEqual("01:05");

            // Timer Off takes the clock away again.
            hud.ApplyMatchState(timerEnabled: false, 65 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsFalse();
        } finally {
            hud.Free();
        }
    }

    /// <summary>
    /// F22: Sudden Death has no timer left to count, so the clock area carries
    /// the phase instead of a running time. The existing SUDDEN DEATH banner on
    /// <c>FighterOverlayModel</c> is untouched — this is the clock, not the stamp.
    /// </summary>
    [TestCase]
    public void SuddenDeathReplacesTheRunningClockWithItsPhaseText() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");

            hud.ApplyMatchState(true, 12 * TickRate, TickRate, (int)MatchMode.Stock, suddenDeath: false);
            AssertThat(hud.TimerText).IsEqual("00:12");

            hud.ApplyMatchState(true, 0, TickRate, (int)MatchMode.Stock, suddenDeath: true);
            AssertThat(hud.TimerVisible).IsTrue();
            AssertThat(hud.TimerText)
                .IsEqual(TranslationServer.Translate("fighter_hud_sudden_death_clock").ToString());
            // TG-CLOSE polish: "Sudden Death — next death loses", the spaced em dash HUD_CONTRACT writes (it was a hyphen).
            AssertThat(hud.TimerText.Contains(" — ") && !hud.TimerText.Contains(" - "))
                .OverrideFailureMessage($"The Sudden Death line '{hud.TimerText}' is not joined by ' — '")
                .IsTrue();
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void HudOpacityIsReadEveryFrameRatherThanOnceAtLoad() {
        // §2.10: the accessibility settings are live. A player who lowers HUD
        // opacity from the in-match pause menu must see it without restarting.
        GlobalSaveData global = SaveManager.Instance?.GlobalData;
        if (global == null) return;
        float original = global.HudOpacity;
        FighterHUD hud = Mount();
        try {
            global.HudOpacity = 1f;
            hud._Process(0.016);
            AssertThat(hud.AppliedOpacity).IsEqual(1f);

            global.HudOpacity = 0.4f;
            hud._Process(0.016);
            AssertThat(hud.AppliedOpacity).IsEqual(0.4f);

            // The clamp keeps a corrupt payload from hiding the HUD entirely.
            global.HudOpacity = 0f;
            hud._Process(0.016);
            AssertThat(hud.AppliedOpacity).IsEqual(0.2f);
        } finally {
            global.HudOpacity = original;
            hud.Free();
        }
    }

    // ---- helpers --------------------------------------------------------

    private static FighterHUD Mount() {
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        var hud = packed.Instantiate<FighterHUD>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hud);
        return hud;
    }

    private static FighterStateComponent State(
        int hp = 100,
        int maxHp = 100,
        int stocks = 3,
        int block = 3,
        float influence = 0f) => new() {
            CurrentHP = hp,
            MaxHP = maxHp,
            Stocks = stocks,
            BlockCharges = block,
            Influence = FP64.FromFloat(influence)
        };

    private static FighterRuntimeComponent Runtime() => new() {
        StatusType = (int)StatusType.None
    };
}
