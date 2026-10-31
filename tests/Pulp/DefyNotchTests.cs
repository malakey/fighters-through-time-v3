using System.Collections.Generic;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// MM-HUD (meter and Defy update, 2026-10-31; ledger <c>USER-DEFY-HUD-NOTCH</c>): the Defy notch at 50 on the Story Ultimate
/// ring and the Fighter meter tube. Gold only when the meter holds the Defy cost and <c>gameplay_defy</c> is bound for the
/// player's device kind; dim otherwise; and the tube's notch is nothing like the boss bar's white phase notches.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DefyNotchTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    // Runs body with gameplay_defy bound (a keyboard F) or unbound, then puts the InputMap back.
    private static void WithDefy(bool bound, System.Action body) {
        bool existed = InputMap.HasAction(DefyNotchRules.DefyAction);
        var saved = new List<InputEvent>();
        if (existed) foreach (InputEvent e in InputMap.ActionGetEvents(DefyNotchRules.DefyAction)) saved.Add(e);
        else InputMap.AddAction(DefyNotchRules.DefyAction);
        try {
            InputMap.ActionEraseEvents(DefyNotchRules.DefyAction);
            if (bound) InputMap.ActionAddEvent(DefyNotchRules.DefyAction, new InputEventKey { PhysicalKeycode = Key.F });
            body();
        } finally {
            if (!existed) InputMap.EraseAction(DefyNotchRules.DefyAction);
            else {
                InputMap.ActionEraseEvents(DefyNotchRules.DefyAction);
                foreach (InputEvent e in saved) InputMap.ActionAddEvent(DefyNotchRules.DefyAction, e);
            }
        }
    }

    [TestCase]
    public void TheStoryRingsNotchIsAtTheHalfWayAngleAndLightsGoldAtFiftyOnlyWhenDefyIsBound() {
        AssertFloat(DefyNotchRules.NotchFraction).IsEqual(0.5f);
        WithDefy(true, () => {
            Node host = new Node { Name = "DefyNotchStoryHost" };
            Tree.Root.AddChild(host);
            try {
                StoryHUD hud = StoryHUD.CreateDefault();
                host.AddChild(hud);
                hud.InvalidateDefyBinding();
                RadialProgress ring = hud.UltimateRing;
                AssertObject(ring).IsNotNull();
                AssertFloat(ring.NotchFraction).IsEqual(0.5f);
                // 49: below the cost, dim even though bound.
                EventBus.Instance.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 49f, NormalizedValue = 0.49f });
                hud._Process(1.0 / 60.0);
                AssertThat(ring.NotchColor).IsEqual(UIPalette.Ink3);
                // 50 and above, bound: lit gold.
                EventBus.Instance.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 50f, NormalizedValue = 0.5f });
                hud._Process(1.0 / 60.0);
                AssertThat(ring.NotchColor).IsEqual(UIPalette.GoldBright);
                // The ring drops at once on a spend: back under the cost, dim.
                EventBus.Instance.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 0f, NormalizedValue = 0f });
                hud._Process(1.0 / 60.0);
                AssertThat(ring.NotchColor).IsEqual(UIPalette.Ink3);
            } finally {
                host.Free();
            }
        });
    }

    [TestCase]
    public void TheFighterTubesGoldNotchIsDistinctFromTheBossBarsWhitePhaseNotches() {
        WithDefy(true, () => {
            var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
            var hud = packed.Instantiate<FighterHUD>();
            Tree.Root.AddChild(hud);
            try {
                hud.ConfigurePlayer(0, null, 3, 3);
                hud.ConfigurePlayer(1, null, 3, 3);
                PulpBarDecor tube = hud.MeterDecorFor(0);
                AssertObject(tube).IsNotNull();
                AssertFloat(tube.DefyNotchFraction).IsEqual(DefyNotchRules.NotchFraction);
                var state = new FighterStateComponent { CurrentHP = 100, MaxHP = 100, Stocks = 3, BlockCharges = 3, Influence = FP64.FromFloat(64f) };
                var runtime = new FighterRuntimeComponent { StatusType = (int)StatusType.None };
                hud.ApplyPlayerState(0, state, runtime, 60);
                AssertThat(tube.DefyNotchLit).OverrideFailureMessage("lit at 64 with Defy bound").IsTrue();
                state.Influence = FP64.FromFloat(49f);
                hud.ApplyPlayerState(0, state, runtime, 60);
                AssertThat(tube.DefyNotchLit).IsFalse();
                // The boss bar's decor has phase notches and no Defy notch, and the gold is not the white phase-notch colour.
                var bossBar = new ProgressBar { MaxValue = 100, Value = 100 };
                Tree.Root.AddChild(bossBar);
                try {
                    PulpBarDecor boss = PulpBarDecor.Attach(bossBar, PulpBarStyle.Boss);
                    boss.PhaseNotches = new[] { 0.5f };
                    AssertThat(boss.DefyNotchFraction < 0f).OverrideFailureMessage("the boss bar draws no Defy notch").IsTrue();
                    AssertThat(boss.PhaseNotches.Length).IsEqual(1);
                    AssertThat(UIPalette.GoldBright == Colors.White).OverrideFailureMessage("gold is not the white phase-notch colour").IsFalse();
                } finally {
                    bossBar.Free();
                }
            } finally {
                hud.GetParent()?.RemoveChild(hud);
                hud.Free();
            }
        });
    }

    [TestCase]
    public void AnUnboundDefyKeepsTheNotchDimOnBothHudsEvenAtFullMeter() {
        AssertThat(DefyNotchRules.IsLit(100f, false)).IsFalse();
        AssertThat(DefyNotchRules.IsLit(100f, true)).IsTrue();
        AssertThat(DefyNotchRules.IsLit(49.9f, true)).IsFalse();
        WithDefy(false, () => {
            AssertThat(DefyNotchRules.IsBound(false)).IsFalse();
            AssertThat(DefyNotchRules.IsBound(true)).IsFalse();
            Node host = new Node { Name = "DefyNotchUnboundHost" };
            Tree.Root.AddChild(host);
            try {
                StoryHUD hud = StoryHUD.CreateDefault();
                host.AddChild(hud);
                hud.InvalidateDefyBinding();
                EventBus.Instance.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 100f, NormalizedValue = 1f, IsFull = true });
                hud._Process(1.0 / 60.0);
                AssertThat(hud.UltimateRing.NotchColor).IsEqual(UIPalette.Ink3);
            } finally {
                host.Free();
            }
        });
        // A keyboard-only binding is not "bound" for a pad player.
        WithDefy(true, () => {
            AssertThat(DefyNotchRules.IsBound(false)).IsTrue();
            AssertThat(DefyNotchRules.IsBound(true)).IsFalse();
        });
    }
}
