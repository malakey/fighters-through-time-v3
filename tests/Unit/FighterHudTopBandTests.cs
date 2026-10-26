using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// UR-HUDF: the HUD-F2 top band as built. The Pulp widgets exist at their UI_SPEC sizes and follow the
/// component values, the hit flash is rate-limited, and at UI scale 1.0 and 1.4 the two clusters, the two slot
/// rows and the timer plate stay inside the canvas and clear of each other.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHudTopBandTests {

    private const int TickRate = 60;

    [TestCase]
    public void ThePulpWidgetsExistAtTheirSpecSizesAndFollowTheComponents() {
        FighterHUD hud = Mount();
        try {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);
            hud.ConfigurePlayer(1, null, 3, 3);
            var issues = new List<string>();

            foreach (string player in new[] { "P1", "P2" }) {
                var roundel = hud.GetNodeOrNull<PulpPortraitRoundel>($"SafeArea/{player}_Status/Body/PortraitRoundel");
                if (roundel == null) issues.Add(player + " roundel missing");
                else if (roundel.Diameter != 110f) issues.Add(player + " roundel " + roundel.Diameter);
                var hp = hud.GetNode<ProgressBar>($"SafeArea/{player}_Status/Body/Column/HPBar");
                if (hp.CustomMinimumSize.Y != 44f) issues.Add(player + " HP height " + hp.CustomMinimumSize.Y);
                if (hp.GetThemeStylebox("fill") is not StyleBoxFlat) issues.Add(player + " HP fill is not a StyleBoxFlat");
                var meter = hud.GetNode<ProgressBar>($"SafeArea/{player}_Status/Body/Column/MeterBar");
                if (meter.CustomMinimumSize.Y != 16f) issues.Add(player + " meter height " + meter.CustomMinimumSize.Y);
                var slots = hud.GetNode<Control>($"SafeArea/{player}_Slots");
                int slotCount = 0;
                foreach (Node child in slots.GetChildren()) {
                    if (child is PulpAbilitySlot slot) {
                        slotCount++;
                        if (slot.SlotSize != 54f) issues.Add(player + " slot size " + slot.SlotSize);
                        if (slot.KeyText.Length == 0) issues.Add(player + " slot has no key tag");
                    } else if (child is PulpStatusChip chip && chip.BadgeSize != 40f) {
                        issues.Add(player + " chip badge " + chip.BadgeSize);
                    }
                }
                if (slotCount != 3) issues.Add(player + " has " + slotCount + " slots");
                if (hud.GetNodeOrNull<PulpIcon>($"SafeArea/{player}_Status/Body/Column/PipRow/DefyIcon") == null)
                    issues.Add(player + " Defy icon missing");
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

            // Stocks are 28 px stars, block pips 24 px shields; spent ones dim and never vanish.
            var stars = hud.GetNode<Control>("SafeArea/P1_Status/Body/Column/PipRow/Stocks");
            AssertThat(stars.GetChildCount()).IsEqual(3);
            var star = (PulpIcon)stars.GetChild(0);
            AssertThat(star.IconName).IsEqual("star4");
            AssertThat(star.CustomMinimumSize.X).IsEqual(28f);
            var shield = (PulpIcon)hud.GetNode<Control>("SafeArea/P1_Status/Body/Column/PipRow/Shields").GetChild(0);
            AssertThat(shield.IconName).IsEqual("shield");
            AssertThat(shield.CustomMinimumSize.X).IsEqual(24f);

            // The meter's gold gradient shows only on a full meter, and the Defy icon changes shape with the state.
            var full = hud.GetNode<Control>("SafeArea/P1_Status/Body/Column/MeterBar/FullGradient");
            var defy = hud.GetNode<PulpIcon>("SafeArea/P1_Status/Body/Column/PipRow/DefyIcon");
            hud.ApplyPlayerState(0, State(influence: 40f), Runtime(), TickRate);
            AssertThat(full.Visible).IsFalse();
            string building = defy.IconName;
            hud.ApplyPlayerState(0, State(influence: 100f), Runtime(), TickRate);
            AssertThat(full.Visible).IsTrue();
            AssertThat(defy.IconName).IsNotEqual(building);

            // HP numerals read "86/110"; the Rally echo rides the decor, not a second band.
            hud.ApplyPlayerState(0, State(hp: 86, maxHp: 110), Runtime(), TickRate, echoPool: 11f);
            AssertThat(hud.GetNode<Label>("SafeArea/P1_Status/Body/Column/HPBar/HpValue/Cur").Text).IsEqual("86");
            AssertThat(hud.GetNode<Label>("SafeArea/P1_Status/Body/Column/HPBar/HpValue/Max").Text).IsEqual("/110");
            AssertFloat(hud.EchoBandWidth(0)).IsEqualApprox(0.1f, 0.001f);

            // A cooling slot shows its sweep: the slot widget follows the authoritative cooldown.
            FighterRuntimeComponent cooling = Runtime();
            cooling.SpecialOneCooldownFrames = 120;
            hud.ApplyPlayerState(0, State(), cooling, TickRate);
            var s1 = (PulpAbilitySlot)hud.GetNode<Control>("SafeArea/P1_Slots").GetChild(0);
            AssertThat(s1.State).IsEqual(PulpSlotState.Cooling);
            AssertFloat(s1.SecondsRemaining).IsEqualApprox(2f, 0.001f);

            // A status fills its chip; clearing empties it again.
            FighterRuntimeComponent burning = Runtime();
            burning.DamageStatusType = (int)StatusType.RadiantBurn;
            burning.DamageStatusFrames = 2 * TickRate;
            hud.ApplyPlayerState(0, State(), burning, TickRate);
            var chip0 = hud.GetNode<PulpStatusChip>("SafeArea/P1_Slots/DamageChip");
            AssertThat(chip0.Empty).IsFalse();
            AssertThat(chip0.GlyphName).IsEqual("burn");
            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(chip0.Empty).IsTrue();
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public async Task TheHitFlashPlaysOnceASecondPerBar() {
        FighterHUD hud = Mount();
        try {
            hud.ConfigurePlayer(0, null, 3, 3);
            var bar = hud.GetNode<ProgressBar>("SafeArea/P1_Status/Body/Column/HPBar");
            hud.ApplyPlayerState(0, State(hp: 100), Runtime(), TickRate);
            AssertThat(bar.SelfModulate).IsEqual(Colors.White);

            hud.ApplyPlayerState(0, State(hp: 90), Runtime(), TickRate);
            bool reduced = FTT.Core.ComfortSettings.ReducedTemporalEffects;
            if (!reduced) AssertThat(bar.SelfModulate.R).IsEqual(0f);

            // The flash lasts one frame, and a second hit inside the second does not flash again.
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(bar.SelfModulate).IsEqual(Colors.White);
            hud.ApplyPlayerState(0, State(hp: 80), Runtime(), TickRate);
            AssertThat(bar.SelfModulate)
                .OverrideFailureMessage("A second hit inside one second must not flash the bar again.")
                .IsEqual(Colors.White);
        } finally {
            hud.Free();
        }
    }

    [TestCase]
    public async Task TheTopBandStaysOnScreenAndClearOfItselfAtBothUiScales() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        GlobalSaveData global = SaveManager.Instance?.GlobalData;
        if (global == null) return;
        float savedScale = global.UiScale;
        var issues = new List<string>();
        try {
            foreach (float scale in new[] { 1f, 1.4f }) {
                global.UiScale = scale;
                FighterHUD hud = Mount();
                try {
                    hud.ConfigurePlayer(0, null, 3, 3);
                    hud.ConfigurePlayer(1, null, 3, 3);
                    // Both fighters carry both statuses: the widest the slot rows ever get.
                    FighterRuntimeComponent busy = Runtime();
                    busy.DamageStatusType = (int)StatusType.RadiantBurn;
                    busy.DamageStatusFrames = 4 * TickRate;
                    busy.StatusType = (int)StatusType.TimeDilation;
                    busy.StatusFrames = 4 * TickRate;
                    hud.ApplyPlayerState(0, State(), busy, TickRate);
                    hud.ApplyPlayerState(1, State(), busy, TickRate);
                    hud.ApplyMatchState(true, 135 * TickRate, TickRate);
                    hud._Process(0.016);
                    for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

                    var canvas = new Rect2(0f, 0f, 1920f, 1080f);
                    var rects = new Dictionary<string, Rect2> {
                        ["P1_Status"] = hud.GetNode<Control>("SafeArea/P1_Status").GetGlobalRect(),
                        ["P2_Status"] = hud.GetNode<Control>("SafeArea/P2_Status").GetGlobalRect(),
                        ["P1_Slots"] = hud.GetNode<Control>("SafeArea/P1_Slots").GetGlobalRect(),
                        ["P2_Slots"] = hud.GetNode<Control>("SafeArea/P2_Slots").GetGlobalRect(),
                        ["TimerPlate"] = hud.GetNode<Control>("SafeArea/TimerPlate").GetGlobalRect()
                    };
                    var names = new List<string>(rects.Keys);
                    foreach (string name in names) {
                        Rect2 r = rects[name];
                        if (!canvas.Grow(1.5f).Encloses(r)) issues.Add($"{name}@{scale} leaves the screen: {r}");
                        if (r.End.Y > 250f * scale + 1.5f) issues.Add($"{name}@{scale} leaves the top band: {r}");
                    }
                    for (int i = 0; i < names.Count; i++) {
                        for (int j = i + 1; j < names.Count; j++) {
                            // A cluster and its own slot row stack vertically by design.
                            if (names[i][..2] == names[j][..2] && names[j].EndsWith("Slots")) continue;
                            if (rects[names[i]].Intersects(rects[names[j]], false))
                                issues.Add($"{names[i]} overlaps {names[j]} at {scale}: {rects[names[i]]} / {rects[names[j]]}");
                        }
                    }
                } finally {
                    hud.GetParent()?.RemoveChild(hud);
                    hud.Free();
                }
            }
        } finally {
            global.UiScale = savedScale;
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // ---- helpers --------------------------------------------------------

    private static FighterHUD Mount() {
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        var hud = packed.Instantiate<FighterHUD>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hud);
        return hud;
    }

    private static FighterStateComponent State(int hp = 100, int maxHp = 100, int stocks = 3, int block = 3,
        float influence = 0f) => new() {
            CurrentHP = hp, MaxHP = maxHp, Stocks = stocks, BlockCharges = block,
            Influence = FP64.FromFloat(influence)
        };

    private static FighterRuntimeComponent Runtime() => new() { StatusType = (int)StatusType.None };
}
