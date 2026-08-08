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
    public void CooldownSlotsReadReadyOrRemainingTimeAndTheUltimateIsMeterGated() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);

            FighterRuntimeComponent cooling = Runtime();
            cooling.SpecialOneCooldownFrames = 120;
            cooling.SpecialTwoCooldownFrames = 0;
            cooling.MovementCooldownFrames = 30;
            hud.ApplyPlayerState(0, State(influence: 40f), cooling, TickRate);

            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.SpecialOne)).IsFalse();
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.SpecialTwo)).IsTrue();
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.Movement)).IsFalse();
            // Meter-gated, not cooldown-gated.
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.Ultimate)).IsFalse();

            hud.ApplyPlayerState(0, State(influence: FighterHudModel.MaxInfluence), Runtime(), TickRate);
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.Ultimate)).IsTrue();
            AssertThat(hud.CooldownIsReady(0, FighterCooldownSlot.SpecialOne)).IsTrue();
            AssertThat(hud.CooldownText(0, FighterCooldownSlot.SpecialOne))
                .IsEqual(TranslationServer.Translate("fighter_hud_cooldown_special1").ToString());
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void TheStatusPipAppearsOnlyWhileAStatusIsActive() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);

            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(hud.StatusText(0)).IsEqual("");

            FighterRuntimeComponent venom = Runtime();
            venom.StatusType = (int)StatusType.Venom;
            hud.ApplyPlayerState(0, State(), venom, TickRate);
            AssertThat(hud.StatusText(0))
                .IsEqual(TranslationServer.Translate("status_venom").ToString());

            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(hud.StatusText(0)).IsEqual("");
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public void TheMatchClockIsHiddenInStockModeAndFormattedInTimedModes() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");

            hud.ApplyMatchState(MatchMode.Stock, 480 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsFalse();

            hud.ApplyMatchState(MatchMode.TimeLimit, 480 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsTrue();
            AssertThat(hud.TimerText).IsEqual("8:00");

            hud.ApplyMatchState(MatchMode.Hybrid, 65 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsTrue();
            // Seconds are zero-padded, so 1:05 never renders as "1:5".
            AssertThat(hud.TimerText).IsEqual("1:05");

            // Switching back to a stock match takes the clock away again.
            hud.ApplyMatchState(MatchMode.Stock, 65 * TickRate, TickRate);
            AssertThat(hud.TimerVisible).IsFalse();
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
