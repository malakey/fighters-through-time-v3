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
/// UR-HUDF, restyled by TG-HUDF (Temporal Glass, UI_SPEC_GLASS 3.4 and 4.5): the HUD-F2 top band as built. The glass
/// widgets exist at their spec sizes and follow the component values; a hit holds the tracker and drains it with no hit
/// flash (MOT-A has none); player 2's tubes mirror; the timer plate is a glass slab that widens for Sudden Death over the
/// kit's top shade; and at UI scale 1.0 and 1.4 the clusters hang from the screen's top corners, the HP tubes keep 24 px
/// from the plate, and nothing leaves the screen, the band or overlaps.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FighterHudTopBandTests {

    private const int TickRate = 60;

    [TestCase]
    public async Task TheGlassWidgetsExistAtTheirSpecSizesAndFollowTheComponents() {
        await AtScale(1f, async hud => {
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);
            hud.ConfigurePlayer(1, null, 3, 3);
            var issues = new List<string>();

            foreach (string player in new[] { "P1", "P2" }) {
                bool two = player == "P2";
                string column = $"SafeArea/{player}_Status/Body/Column";
                var roundel = hud.GetNodeOrNull<PulpPortraitRoundel>($"SafeArea/{player}_Status/Body/PortraitRoundel");
                if (roundel == null) issues.Add(player + " portrait frame missing");
                else if (roundel.Diameter != 110f) issues.Add(player + " portrait frame " + roundel.Diameter);
                var hp = hud.GetNode<ProgressBar>($"{column}/HPBar");
                if (hp.CustomMinimumSize != new Vector2(510f, 44f)) issues.Add(player + " HP tube " + hp.CustomMinimumSize);
                if (hp.GetThemeStylebox("fill") is not StyleBoxFlat) issues.Add(player + " HP fill is not a StyleBoxFlat");
                PulpBarDecor hpTube = PulpBarDecor.Of(hp);
                if (hpTube == null || hpTube.Style != PulpBarStyle.Hp || hpTube.Mirrored != two) {
                    issues.Add(player + " HP tube decor " + hpTube?.Style + " mirrored " + hpTube?.Mirrored);
                }
                var meter = hud.GetNode<ProgressBar>($"{column}/MeterBar");
                if (meter.CustomMinimumSize != new Vector2(390f, 16f)) issues.Add(player + " Ultimate tube " + meter.CustomMinimumSize);
                PulpBarDecor meterTube = PulpBarDecor.Of(meter);
                if (meterTube == null || meterTube.Style != PulpBarStyle.Meter || meterTube.Mirrored != two) {
                    issues.Add(player + " Ultimate tube decor " + meterTube?.Style + " mirrored " + meterTube?.Mirrored);
                }
                if (hud.GetNodeOrNull($"{column}/MeterBar/FullGradient") != null) issues.Add(player + " keeps the pulp full-meter gradient");
                var name = hud.GetNode<Label>($"{column}/TopRow/Name");
                if (name.ThemeTypeVariation != UIPalette.LabelRoles.HeroNameScene) issues.Add(player + " name role " + name.ThemeTypeVariation);
                var tag = hud.GetNode<Label>($"{column}/TopRow/PTag");
                if (tag.ThemeTypeVariation != UIPalette.LabelRoles.SceneCaption) issues.Add(player + " P tag role " + tag.ThemeTypeVariation);
                var shape = hud.GetNodeOrNull<Control>($"{column}/TopRow/SlotShape");
                if (shape?.GetChild(0) is not PulpIcon glyph || glyph.Tint != UIPalette.OwnerOnDark(two ? 1 : 0)) {
                    issues.Add(player + " slot shape not in the owner colour");
                }
                var slots = hud.GetNode<Control>($"SafeArea/{player}_Slots");
                int slotCount = 0, chipCount = 0;
                foreach (Node child in slots.GetChildren()) {
                    if (child is PulpAbilitySlot slot) {
                        slotCount++;
                        if (slot.SlotSize != 54f) issues.Add(player + " lens " + slot.SlotSize);
                        if (slot.KeyText.Length == 0) issues.Add(player + " lens has no key label");
                        if (!slot.KeyOverScene) issues.Add(player + " lens key label is not the scene role");
                    } else if (child is PulpStatusChip chip) {
                        chipCount++;
                        if (chip.BadgeSize != 40f) issues.Add(player + " badge " + chip.BadgeSize);
                    }
                }
                if (slotCount != 3 || chipCount != 2) issues.Add($"{player} has {slotCount} lenses and {chipCount} badges");
                var defy = hud.GetNodeOrNull<PulpIcon>($"{column}/PipRow/DefyIcon");
                if (defy == null || defy.CustomMinimumSize.X != 32f) issues.Add(player + " Defy icon " + defy?.CustomMinimumSize);
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

            // Stocks: a held stock is the filled star in the owner colour, a lost one the outline at 0.22 (3.4.4).
            hud.ApplyPlayerState(0, State(stocks: 2, block: 1), Runtime(), TickRate);
            var stars = hud.GetNode<Control>("SafeArea/P1_Status/Body/Column/PipRow/Stocks");
            AssertThat(stars.GetChildCount()).IsEqual(3);
            var held = (PulpIcon)stars.GetChild(1);
            var lost = (PulpIcon)stars.GetChild(2);
            AssertThat(held.IconName).IsEqual(FighterHUD.StockLitIcon);
            AssertThat(held.Tint).IsEqual(UIPalette.OwnerOnDark(0));
            AssertThat(held.Modulate.A).IsEqual(1f);
            AssertThat(held.CustomMinimumSize.X).IsEqual(28f);
            AssertThat(lost.IconName).IsEqual(FighterHUD.StockOffIcon);
            AssertFloat(lost.Modulate.A).IsEqualApprox(0.22f, 0.001f);
            AssertThat(hud.LitStockPips(0)).IsEqual(2);
            // Block pips: charged = the glowing shield, spent = the glowless grey shield at 0.28.
            var shields = hud.GetNode<Control>("SafeArea/P1_Status/Body/Column/PipRow/Shields");
            var charged = (PulpIcon)shields.GetChild(0);
            var spent = (PulpIcon)shields.GetChild(2);
            AssertThat(charged.IconName).IsEqual(FighterHUD.ShieldLitIcon);
            AssertThat(charged.Tint).IsEqual(UIPalette.Shield);
            AssertThat(charged.CustomMinimumSize.X).IsEqual(24f);
            AssertThat(spent.IconName).IsEqual(FighterHUD.ShieldOffIcon);
            AssertThat(spent.Tint).IsEqual(FighterHUD.ShieldOffTint);
            AssertThat(spent.Tint.R == spent.Tint.G && spent.Tint.G == spent.Tint.B).IsTrue();
            AssertFloat(spent.Modulate.A).IsEqualApprox(0.28f, 0.001f);
            AssertThat(hud.LitShieldPips(0)).IsEqual(1);

            // The Ultimate tube: meter -> meter2 while charging, res -> res2 (with its glow) when full; the Defy icon
            // changes shape with the state.
            PulpBarDecor ultimate = PulpBarDecor.Of(hud.GetNode<ProgressBar>("SafeArea/P1_Status/Body/Column/MeterBar"));
            var defyIcon = hud.GetNode<PulpIcon>("SafeArea/P1_Status/Body/Column/PipRow/DefyIcon");
            hud.ApplyPlayerState(0, State(influence: 40f), Runtime(), TickRate);
            AssertThat(ultimate.FillGradient.Anchored).IsEqual(UIPalette.Meter);
            AssertThat(ultimate.FillGradient.Moving).IsEqual(UIPalette.Meter2);
            string building = defyIcon.IconName;
            hud.ApplyPlayerState(0, State(influence: 100f), Runtime(), TickRate);
            AssertThat(ultimate.FillGradient.Anchored).IsEqual(UIPalette.Res);
            AssertThat(ultimate.FillGradient.Moving).IsEqual(UIPalette.Res2);
            AssertThat(defyIcon.IconName).IsNotEqual(building);

            // HP numerals "86/110" 14 px inside the tube's inner end, the "/max" at 0.78; the Rally echo rides the tube.
            hud.ApplyPlayerState(0, State(hp: 86, maxHp: 110), Runtime(), TickRate, echoPool: 11f);
            var value = hud.GetNode<HBoxContainer>("SafeArea/P1_Status/Body/Column/HPBar/HpValue");
            AssertThat(value.GetNode<Label>("Cur").Text).IsEqual("86");
            var max = value.GetNode<Label>("Max");
            AssertThat(max.Text).IsEqual("/110");
            AssertFloat(max.Modulate.A).IsEqualApprox(0.78f, 0.001f);
            AssertThat(value.OffsetRight).IsEqual(-14f);
            AssertThat(hud.GetNode<HBoxContainer>("SafeArea/P2_Status/Body/Column/HPBar/HpValue").OffsetLeft).IsEqual(14f);
            AssertFloat(hud.EchoBandWidth(0)).IsEqualApprox(0.1f, 0.001f);

            // A cooling lens shows its sweep: the lens follows the authoritative cooldown.
            FighterRuntimeComponent cooling = Runtime();
            cooling.SpecialOneReuseFrames = 120;
            hud.ApplyPlayerState(0, State(), cooling, TickRate);
            var s1 = (PulpAbilitySlot)hud.GetNode<Control>("SafeArea/P1_Slots").GetChild(0);
            AssertThat(s1.State).IsEqual(PulpSlotState.Cooling);
            AssertFloat(s1.SecondsRemaining).IsEqualApprox(2f, 0.001f);

            // A refused press plays the glass circle round the lens's ring, the lens as the tint target.
            hud.FlashLockoutRefusal(0, FighterLockoutRefusal.SpecialOne);
            Control circle = NotYetFlash.CircleOf(s1.Ring);
            AssertObject(circle).IsNotNull();
            AssertThat(circle.Visible).IsTrue();
            AssertThat(s1.SelfModulate).IsEqual(NotYetFlash.Tint);

            // A status fills its badge; clearing empties it again.
            FighterRuntimeComponent burning = Runtime();
            burning.DamageStatusType = (int)StatusType.RadiantBurn;
            burning.DamageStatusFrames = 2 * TickRate;
            hud.ApplyPlayerState(0, State(), burning, TickRate);
            var chip0 = hud.GetNode<PulpStatusChip>("SafeArea/P1_Slots/DamageChip");
            AssertThat(chip0.Empty).IsFalse();
            AssertThat(chip0.GlyphName).IsEqual("burn");
            hud.ApplyPlayerState(0, State(), Runtime(), TickRate);
            AssertThat(chip0.Empty).IsTrue();
            await Task.CompletedTask;
        });
    }

    [TestCase]
    public async Task AHitHoldsTheTrackerThenDrainsItAndPlaysNoFlash() {
        bool reducedBefore = ComfortSettings.ReducedTemporalEffects;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        try {
            ComfortSettings.Apply(false);
            await AtScale(1f, async hud => {
                hud.ConfigurePlayer(0, null, 3, 3);
                var bar = hud.GetNode<ProgressBar>("SafeArea/P1_Status/Body/Column/HPBar");
                var current = hud.GetNode<Label>("SafeArea/P1_Status/Body/Column/HPBar/HpValue/Cur");
                PulpBarDecor tube = PulpBarDecor.Of(bar);
                hud.ApplyPlayerState(0, State(hp: 100), Runtime(), TickRate);
                AssertFloat(tube.LagValue).IsEqual(100f);

                // MOT-A has no hit flash (plan 0.4 P7): the bar, whose SelfModulate the tube follows, stays white on the
                // hit and the frames after it. The fill and the numerals jump; the tracker holds at the HP before the hit.
                hud.ApplyPlayerState(0, State(hp: 90), Runtime(), TickRate);
                AssertThat(bar.SelfModulate).IsEqual(Colors.White);
                AssertFloat((float)bar.Value).IsEqual(90f);
                AssertThat(current.Text).IsEqual("90");
                AssertFloat(tube.LagValue).IsEqual(100f);
                await Frames(tree, 3);
                AssertThat(bar.SelfModulate).IsEqual(Colors.White);
                AssertThat(tube.SelfModulate).IsEqual(Colors.White);
                AssertFloat(tube.LagValue)
                    .OverrideFailureMessage("The tracker must hold for half a second before it drains.")
                    .IsEqual(100f);
                // Then it drains to the fill (0.5 s hold, 0.6 s drain).
                await Frames(tree, 80);
                AssertFloat(tube.LagValue).IsEqualApprox(90f, 0.01f);

                // Healing raises the fill over 0.2 s with the tracker riding it (no red on a heal); the numerals jump.
                hud.ApplyPlayerState(0, State(hp: 96), Runtime(), TickRate);
                AssertThat(current.Text).IsEqual("96");
                AssertFloat((float)bar.Value).IsLess(96f);
                AssertThat(tube.LagValue <= (float)bar.Value + 0.001f)
                    .OverrideFailureMessage("A heal must not show the damage tracker above the rising fill.")
                    .IsTrue();
                await Frames(tree, 24);
                AssertFloat((float)bar.Value).IsEqual(96f);
                AssertFloat(tube.LagValue).IsEqualApprox(96f, 0.01f);

                // Reduced Temporal Effects: still no flash; the tracker waits 0.2 s, then sits on the fill (no drain).
                ComfortSettings.Apply(true);
                hud.ApplyPlayerState(0, State(hp: 80), Runtime(), TickRate);
                AssertThat(bar.SelfModulate).IsEqual(Colors.White);
                AssertFloat(tube.LagValue).IsEqual(96f);
                await Frames(tree, 24);
                AssertFloat(tube.LagValue).IsEqualApprox(80f, 0.01f);
                // A heal under Reduced lands at once.
                hud.ApplyPlayerState(0, State(hp: 85), Runtime(), TickRate);
                AssertFloat((float)bar.Value).IsEqual(85f);
            });
        } finally {
            ComfortSettings.Apply(reducedBefore);
        }
    }

    /// <summary>
    /// REVIEW-UI-GLASS-CODEX-2 (Codex review 2 of Phase B, major): a heal that lands while the damage tracker holds, or
    /// while it drains, stops the tracker the hit started. Its drain writes through <c>LagValue</c> from its own start, so
    /// a heal that only assigned the value let the old drain bring a damage band back above the healed fill, with no new
    /// hit. Checked frame by frame past the end of the old hold and drain.
    /// </summary>
    [TestCase]
    public async Task AHealDuringTheTrackersHoldOrDrainStopsItSoNoDamageBandReturns() {
        bool reducedBefore = ComfortSettings.ReducedTemporalEffects;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        try {
            ComfortSettings.Apply(false);
            await AtScale(1f, async hud => {
                hud.ConfigurePlayer(0, null, 3, 3);
                var bar = hud.GetNode<ProgressBar>("SafeArea/P1_Status/Body/Column/HPBar");
                PulpBarDecor tube = PulpBarDecor.Of(bar);
                var problems = new List<string>();
                // The heal lands 0.1 s into the 0.5 s hold, then 0.75 s after the hit (inside the 0.6 s drain).
                foreach ((string phase, int healAfterFrames) in new[] { ("hold", 6), ("drain", 45) }) {
                    hud.ApplyPlayerState(0, State(hp: 100), Runtime(), TickRate);
                    await Frames(tree, 20);
                    if (Mathf.Abs(tube.LagValue - 100f) > 0.01f) problems.Add($"{phase}: the tracker starts at {tube.LagValue:0.##}, not 100");
                    hud.ApplyPlayerState(0, State(hp: 50), Runtime(), TickRate);
                    await Frames(tree, healAfterFrames);
                    hud.ApplyPlayerState(0, State(hp: 60), Runtime(), TickRate);
                    // 90 frames: past the end of the hit's hold and drain (1.1 s) whichever phase the heal landed in.
                    bool reported = false;
                    for (int frame = 0; frame < 90; frame++) {
                        await Frames(tree, 1);
                        if (!reported && tube.LagValue > (float)bar.Value + 0.01f) {
                            reported = true;
                            problems.Add($"{phase}: {frame + 1} frames after the heal the tracker is at {tube.LagValue:0.##}, "
                                + $"above the fill at {bar.Value:0.##}");
                        }
                    }
                    if (!Mathf.IsEqualApprox((float)bar.Value, 60f) || Mathf.Abs(tube.LagValue - 60f) > 0.01f) {
                        problems.Add($"{phase}: the heal settles the fill at {bar.Value:0.##} and the tracker at {tube.LagValue:0.##}, not 60");
                    }
                }
                if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
            });
        } finally {
            ComfortSettings.Apply(reducedBefore);
        }
    }

    [TestCase]
    public async Task ThePlayerTwoBarsMirrorLeanAnchorGradientAndCap() {
        await AtScale(1f, async hud => {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            hud.ConfigurePlayer(0, null, 3, 3);
            hud.ConfigurePlayer(1, null, 3, 3);
            hud.ApplyPlayerState(0, State(hp: 60, influence: 50f), Runtime(), TickRate);
            hud.ApplyPlayerState(1, State(hp: 60, influence: 50f), Runtime(), TickRate);
            await Frames(tree, 3);
            var issues = new List<string>();
            foreach (string bar in new[] { "HPBar", "MeterBar" }) {
                PulpBarDecor one = PulpBarDecor.Of(hud.GetNode<ProgressBar>($"SafeArea/P1_Status/Body/Column/{bar}"));
                PulpBarDecor two = PulpBarDecor.Of(hud.GetNode<ProgressBar>($"SafeArea/P2_Status/Body/Column/{bar}"));
                if (one.Mirrored || !two.Mirrored) issues.Add($"{bar}: P1 mirrored {one.Mirrored}, P2 mirrored {two.Mirrored}");
                float w = two.Size.X, h = two.Size.Y, run = two.Run;
                // The lean: P1 "/" (its top edge starts run px right of its bottom edge), P2 "\" (R6).
                if (!Has(one.OuterPolygon, new Vector2(run, 0f)) || !Has(one.OuterPolygon, new Vector2(0f, h))) {
                    issues.Add($"{bar}: P1 does not lean /: {Join(one.OuterPolygon)}");
                }
                if (!Has(two.OuterPolygon, new Vector2(0f, 0f)) || !Has(two.OuterPolygon, new Vector2(run, h))
                    || !Has(two.OuterPolygon, new Vector2(w - run, 0f)) || !Has(two.OuterPolygon, new Vector2(w, h))) {
                    issues.Add($"{bar}: P2 does not lean \\: {Join(two.OuterPolygon)}");
                }
                // The anchor: P1's fill grows from the inner left end, P2's from the inner right end, each to a vertical
                // moving end at 60 % / 50 % of the inner width.
                Rect2 inner = two.TubeInnerBox;
                float share = bar == "HPBar" ? 0.6f : 0.5f;
                float oneMoving = inner.Position.X + share * inner.Size.X, twoMoving = inner.End.X - share * inner.Size.X;
                if (Mathf.Abs(MaxX(one.FillPolygon) - oneMoving) > 0.6f || Count(one.FillPolygon, oneMoving) < 2) {
                    issues.Add($"{bar}: P1's moving end is not vertical at {oneMoving}: {Join(one.FillPolygon)}");
                }
                if (Mathf.Abs(MinX(two.FillPolygon) - twoMoving) > 0.6f || Count(two.FillPolygon, twoMoving) < 2) {
                    issues.Add($"{bar}: P2's moving end is not vertical at {twoMoving}: {Join(two.FillPolygon)}");
                }
                if (MaxX(two.FillPolygon) < inner.End.X - run - 1f) issues.Add($"{bar}: P2's fill is not anchored at the right");
                // The gradient runs from the anchored end to the moving end: P2's anchored colour is at its right.
                (Color anchored, Color moving) = two.FillGradient;
                Color twoAtRight = ColorAt(two.FillPolygon, two.FillColors, MaxX(two.FillPolygon));
                Color twoAtLeft = ColorAt(two.FillPolygon, two.FillColors, MinX(two.FillPolygon));
                if (!twoAtRight.IsEqualApprox(anchored) || !twoAtLeft.IsEqualApprox(moving)) {
                    issues.Add($"{bar}: P2's gradient is not anchored at the right ({twoAtLeft} .. {twoAtRight})");
                }
                Color oneAtLeft = ColorAt(one.FillPolygon, one.FillColors, MinX(one.FillPolygon));
                if (!oneAtLeft.IsEqualApprox(one.FillGradient.Anchored)) issues.Add($"{bar}: P1's gradient is not anchored at the left");
                // The cap sits on the moving edge.
                if (Mathf.Abs(MinX(two.CapPolygon) - twoMoving) > 0.6f) issues.Add($"{bar}: P2's cap is not on its moving (left) edge: {Join(two.CapPolygon)}");
                if (Mathf.Abs(MaxX(one.CapPolygon) - oneMoving) > 0.6f) issues.Add($"{bar}: P1's cap is not on its moving (right) edge");
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        });
    }

    [TestCase]
    public async Task TheTimerPlateWidensForSuddenDeathAndTheTopShadeSitsBehindTheBand() {
        await AtScale(1f, async hud => {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            TranslationServer.SetLocale("en");
            hud.ConfigurePlayer(0, null, 3, 3);
            hud.ConfigurePlayer(1, null, 3, 3);
            var plate = hud.GetNode<PanelContainer>("SafeArea/TimerPlate");
            var clock = hud.GetNode<Label>("SafeArea/MatchClock");
            var mode = hud.GetNode<Label>("SafeArea/ModeLabel");
            AssertThat(plate.ThemeTypeVariation).IsEqual(UIPalette.Variations.GlassPanelTimer);
            AssertObject(plate.GetNodeOrNull<GlassSheen>(GlassSheen.NodeName)).IsNotNull();

            // Regulation: the 234 x 111.6 plate, the digits on their 60 px line, the mode caption under them.
            hud.ApplyMatchState(true, 136 * TickRate, TickRate, (int)MatchMode.Stock, false);
            await Frames(tree, 3);
            AssertFloat(plate.Size.X).IsEqualApprox(234f, 0.5f);
            AssertFloat(plate.Size.Y).IsEqualApprox(111.6f, 0.5f);
            AssertThat(mode.Visible).IsTrue();
            AssertThat(clock.ThemeTypeVariation).IsEqual(UIPalette.LabelRoles.TimerDigits);
            AssertThat(hud.TimerText).IsEqual("02:16");
            float digitsLine = plate.Position.Y + 12f + 30f, captionLine = plate.Position.Y + 12f + 60f + 6f + 10.8f;
            AssertFloat(clock.Position.Y + clock.Size.Y / 2f).IsEqualApprox(digitsLine, 0.5f);
            AssertFloat(mode.Position.Y + mode.Size.Y / 2f).IsEqualApprox(captionLine, 0.5f);

            // Sudden Death: its line takes the label slot (T14, upper case, Danger), the digits and caption leave, and the
            // plate widens to the line plus its 32 px sides, here on one line (at 1.0 the room is 520).
            hud.ApplyMatchState(true, 0, TickRate, (int)MatchMode.Stock, true);
            await Frames(tree, 3);
            AssertThat(mode.Visible).IsFalse();
            AssertThat(clock.ThemeTypeVariation).IsEqual(UIPalette.LabelRoles.CaptionLabel);
            AssertThat(clock.Uppercase).IsTrue();
            AssertThat(clock.GetThemeColor("font_color")).IsEqual(UIPalette.Danger);
            AssertThat(clock.AutowrapMode).IsEqual(TextServer.AutowrapMode.Off);
            float line = clock.GetCombinedMinimumSize().X;
            AssertThat(line).IsGreater(FighterHudModel.TimerPlateWidth - 64f);
            AssertFloat(plate.Size.X).IsEqualApprox(line + 64f, 1f);
            AssertThat(plate.GetRect().Encloses(clock.GetRect())).IsTrue();
            AssertFloat(plate.Size.Y).IsEqualApprox(12f + clock.Size.Y + 12f, 0.5f);

            // Regulation again (a rematch): the plate, the digits and the caption come back.
            hud.ApplyMatchState(true, 30 * TickRate, TickRate, (int)MatchMode.TimeLimit, false);
            await Frames(tree, 3);
            AssertFloat(plate.Size.X).IsEqualApprox(234f, 0.5f);
            AssertThat(mode.Visible).IsTrue();
            AssertThat(mode.Text).IsEqual("fighter_hud_mode_time");
            AssertThat(clock.ThemeTypeVariation).IsEqual(UIPalette.LabelRoles.TimerDigits);
            AssertThat(clock.Uppercase).IsFalse();
            AssertThat(hud.TimerText).IsEqual("00:30");

            // The kit's top shade is the band's first layer, full width and 300 tall, behind every part of the band.
            var shade = hud.GetNode<TextureRect>("SafeArea/TopScrim");
            AssertThat(shade.GetIndex()).IsEqual(0);
            AssertThat(shade.Texture?.ResourcePath ?? "").IsEqual("res://assets/sprites/ui/glass/fighter_top_shade.tres");
            Rect2 shadeRect = shade.GetGlobalRect();
            AssertFloat(shadeRect.Position.Y).IsEqual(0f);
            AssertFloat(shadeRect.Size.X).IsEqualApprox(1920f, 1f);
            AssertFloat(shadeRect.Size.Y).IsEqualApprox(300f, 0.5f);
            foreach (string part in new[] { "P1_Status", "P2_Status", "P1_Slots", "P2_Slots", "TimerPlate", "MatchClock" }) {
                var node = hud.GetNode<Control>("SafeArea/" + part);
                Rect2 r = node.GetGlobalRect();
                if (node.GetIndex() <= shade.GetIndex() || r.Position.Y < shadeRect.Position.Y || r.End.Y > shadeRect.End.Y) {
                    AssertThat($"{part} ({r}, index {node.GetIndex()}) is not in front of the shade {shadeRect}").IsEqual("");
                }
            }
        });
    }

    [TestCase]
    public async Task TheTopBandStaysOnScreenAndClearOfItselfAtBothUiScales() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var issues = new List<string>();
        foreach (float scale in new[] { 1f, 1.4f }) {
            foreach (bool sudden in new[] { false, true }) {
                await AtScale(scale, async hud => {
                    hud.ConfigurePlayer(0, null, 3, 3);
                    hud.ConfigurePlayer(1, null, 3, 3);
                    // Both fighters carry both statuses: the widest the lens rows ever get.
                    FighterRuntimeComponent busy = Runtime();
                    busy.DamageStatusType = (int)StatusType.RadiantBurn;
                    busy.DamageStatusFrames = 4 * TickRate;
                    busy.StatusType = (int)StatusType.TimeDilation;
                    busy.StatusFrames = 4 * TickRate;
                    hud.ApplyPlayerState(0, State(), busy, TickRate);
                    hud.ApplyPlayerState(1, State(), busy, TickRate);
                    hud.ApplyMatchState(true, 135 * TickRate, TickRate, (int)MatchMode.Stock, sudden);
                    hud._Process(0.016);
                    await Frames(tree, 4);
                    string at = $"@{scale}{(sudden ? " SD" : "")}";

                    var canvas = new Rect2(0f, 0f, 1920f, 1080f);
                    var rects = new Dictionary<string, Rect2> {
                        ["P1_Status"] = hud.GetNode<Control>("SafeArea/P1_Status").GetGlobalRect(),
                        ["P2_Status"] = hud.GetNode<Control>("SafeArea/P2_Status").GetGlobalRect(),
                        ["P1_Slots"] = hud.GetNode<Control>("SafeArea/P1_Slots").GetGlobalRect(),
                        ["P2_Slots"] = hud.GetNode<Control>("SafeArea/P2_Slots").GetGlobalRect(),
                        ["TimerPlate"] = hud.GetNode<Control>("SafeArea/TimerPlate").GetGlobalRect()
                    };
                    // 4.5: each cluster hangs from its screen corner (36, 22) at every scale; the lens rows keep 46 px.
                    Near(issues, at + " P1 corner", rects["P1_Status"].Position, new Vector2(36f, 22f));
                    Near(issues, at + " P2 corner", new Vector2(rects["P2_Status"].End.X, rects["P2_Status"].Position.Y), new Vector2(1884f, 22f));
                    Near(issues, at + " P1 lens row", rects["P1_Slots"].Position.X, 46f);
                    Near(issues, at + " P2 lens row", rects["P2_Slots"].End.X, 1874f);
                    Near(issues, at + " lens rows' top", rects["P1_Slots"].Position.Y, FighterHudModel.SlotRowTop * scale);
                    Near(issues, at + " plate top", rects["TimerPlate"].Position.Y, FighterHudModel.TimerPlateTop);

                    // The HP tubes take the clamp's width and keep 24 px from the plate on both sides.
                    Rect2 hp1 = hud.GetNode<Control>("SafeArea/P1_Status/Body/Column/HPBar").GetGlobalRect();
                    Rect2 hp2 = hud.GetNode<Control>("SafeArea/P2_Status/Body/Column/HPBar").GetGlobalRect();
                    Near(issues, at + " HP tube width", hp1.Size.X, FighterHudModel.HpBarWidth(scale) * scale);
                    if (hp1.End.X + FighterHudModel.TimerClearance > rects["TimerPlate"].Position.X + 0.5f) {
                        issues.Add($"{at} P1's HP tube ends at {hp1.End.X}, within 24 px of the plate at {rects["TimerPlate"].Position.X}");
                    }
                    if (rects["TimerPlate"].End.X + FighterHudModel.TimerClearance > hp2.Position.X + 0.5f) {
                        issues.Add($"{at} P2's HP tube starts at {hp2.Position.X}, within 24 px of the plate at {rects["TimerPlate"].End.X}");
                    }

                    // The lens row's rhythm: lenses at pitch 74, 50 px to the damage badge, the badges 18 apart (x scale).
                    var row = hud.GetNode<Control>("SafeArea/P1_Slots");
                    var lenses = new List<Control>();
                    var chips = new List<PulpStatusChip>();
                    foreach (Node child in row.GetChildren()) {
                        if (child is PulpAbilitySlot lens) lenses.Add(lens);
                        else if (child is PulpStatusChip chip) chips.Add(chip);
                    }
                    Near(issues, at + " lens pitch", lenses[1].GetGlobalRect().Position.X - lenses[0].GetGlobalRect().Position.X, 74f * scale);
                    Near(issues, at + " lens to badge", chips[0].GetGlobalRect().Position.X - lenses[2].GetGlobalRect().End.X, 50f * scale);
                    Near(issues, at + " badge gap", chips[1].GetGlobalRect().Position.X - chips[0].GetGlobalRect().End.X, 18f * scale);
                    // The badge is drawn 40 tall, 7 below the row's top (4L: the damage badge at y 199). TG-CLOSE: the chip fills
                    // the row's height and centres its badge itself (BadgeTop), so the drawn badge is measured, not the chip.
                    Near(issues, at + " badge top", chips[0].GetGlobalRect().Position.Y + chips[0].BadgeTop * scale, lenses[0].GetGlobalRect().Position.Y + 7f * scale);
                    Near(issues, at + " badge height", chips[0].BadgeSize * scale, 40f * scale);
                    Near(issues, at + " badge row", chips[0].GetGlobalRect().Size.Y, row.GetGlobalRect().Size.Y);

                    var names = new List<string>(rects.Keys);
                    foreach (string name in names) {
                        Rect2 r = rects[name];
                        if (!canvas.Grow(1.5f).Encloses(r)) issues.Add($"{name}{at} leaves the screen: {r}");
                        if (r.End.Y > 250f * scale + 1.5f) issues.Add($"{name}{at} leaves the top band: {r}");
                    }
                    // Every drawn part of the lens rows (the key labels' baselines, the badges' text) stays in the band:
                    // 4.5 puts the key labels' line at 255 to 273.
                    foreach (string player in new[] { "P1", "P2" }) {
                        foreach (Node child in hud.GetNode<Control>($"SafeArea/{player}_Slots").GetChildren()) {
                            if (child is PulpAbilitySlot lens && lens.KeyTag is Label key && key.Visible) {
                                float baseline = key.GetGlobalRect().Position.Y + key.GetThemeFont("font").GetAscent(key.GetThemeFontSize("font_size")) * scale;
                                if (baseline > FighterHudModel.TopBandHeight * scale + 2f) issues.Add($"{player}{at} key label's baseline {baseline} leaves the band");
                            } else if (child is PulpStatusChip chip && chip.TextColumn.Visible
                                       && chip.TextColumn.GetGlobalRect().End.Y > FighterHudModel.TopBandHeight * scale + 2f) {
                                issues.Add($"{player}{at} badge text {chip.TextColumn.GetGlobalRect()} leaves the band");
                            }
                        }
                    }
                    for (int i = 0; i < names.Count; i++) {
                        for (int j = i + 1; j < names.Count; j++) {
                            // A cluster and its own lens row stack vertically by design.
                            if (names[i][..2] == names[j][..2] && names[j].EndsWith("Slots")) continue;
                            if (rects[names[i]].Intersects(rects[names[j]], false))
                                issues.Add($"{names[i]} overlaps {names[j]} {at}: {rects[names[i]]} / {rects[names[j]]}");
                        }
                    }
                });
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // ---- helpers --------------------------------------------------------

    /// <summary>Mounts a HUD at <paramref name="scale"/> (the saved UI scale, restored after) and frees it after <paramref name="body"/>.</summary>
    private static async Task AtScale(float scale, System.Func<FighterHUD, Task> body) {
        GlobalSaveData global = SaveManager.Instance?.GlobalData;
        float savedScale = global?.UiScale ?? 1f;
        if (global != null) global.UiScale = scale;
        FighterHUD hud = null;
        try {
            hud = Mount();
            await body(hud);
        } finally {
            if (hud != null) {
                hud.GetParent()?.RemoveChild(hud);
                hud.Free();
            }
            if (global != null) global.UiScale = savedScale;
        }
    }

    private static FighterHUD Mount() {
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        var hud = packed.Instantiate<FighterHUD>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hud);
        return hud;
    }

    private static async Task Frames(SceneTree tree, int count) {
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Near(List<string> issues, string what, Vector2 actual, Vector2 expected) {
        if (actual.DistanceTo(expected) > 1.5f) issues.Add($"{what} is {actual}, not {expected}");
    }

    private static void Near(List<string> issues, string what, float actual, float expected) {
        if (Mathf.Abs(actual - expected) > 1.5f) issues.Add($"{what} is {actual}, not {expected}");
    }

    private static bool Has(Vector2[] polygon, Vector2 point) {
        foreach (Vector2 p in polygon) if (p.DistanceTo(point) < 0.6f) return true;
        return false;
    }

    private static int Count(Vector2[] polygon, float x) {
        int n = 0;
        foreach (Vector2 p in polygon) if (Mathf.Abs(p.X - x) < 0.6f) n++;
        return n;
    }

    private static float MinX(Vector2[] polygon) {
        float x = float.MaxValue;
        foreach (Vector2 p in polygon) x = Mathf.Min(x, p.X);
        return x;
    }

    private static float MaxX(Vector2[] polygon) {
        float x = float.MinValue;
        foreach (Vector2 p in polygon) x = Mathf.Max(x, p.X);
        return x;
    }

    private static Color ColorAt(Vector2[] polygon, Color[] colors, float x) {
        for (int i = 0; i < polygon.Length; i++) if (Mathf.Abs(polygon[i].X - x) < 0.01f) return colors[i];
        return default;
    }

    private static string Join(Vector2[] polygon) => string.Join(" ", polygon);

    private static FighterStateComponent State(int hp = 100, int maxHp = 100, int stocks = 3, int block = 3,
        float influence = 0f) => new() {
            CurrentHP = hp, MaxHP = maxHp, Stocks = stocks, BlockCharges = block,
            Influence = FP64.FromFloat(influence)
        };

    private static FighterRuntimeComponent Runtime() => new() { StatusType = (int)StatusType.None };
}
