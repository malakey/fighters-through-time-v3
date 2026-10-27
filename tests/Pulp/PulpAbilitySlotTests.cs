using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>
/// The glass lens (TG-WIDGETS step 2, GAUGE-A, UI_SPEC_GLASS 3.4.2; first written by UR-WIDGETS for the pulp slot): the
/// state table, the countdown text, the art inset and centre size, the built parts, the readiness ring outside the lens,
/// and the single ready bloom with a redraw that rebuilds nothing.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpAbilitySlotTests {

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static async Task Frames(int count) {
        for (int i = 0; i < count; i++) await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void TheStateTableMatchesTheSpec() {
        // Ready: full colour, no sweep, a full accent ring, the lit edge (ReadyFrame).
        PulpSlotLook ready = PulpAbilitySlot.Derive(PulpSlotState.Ready, false, 1f, 0f);
        AssertThat(ready.ReadyFrame).IsTrue();
        AssertThat(ready.SweepElapsed).IsEqual(-1f);
        AssertThat(ready.Ring).IsEqual(PulpRingMode.Accent);
        AssertThat(ready.ReadyTag).IsFalse();

        // Cooling: saturate .5, brightness .72, the sweep, the seconds, the accent arc.
        PulpSlotLook cooling = PulpAbilitySlot.Derive(PulpSlotState.Cooling, false, 0.4f, 2.4f);
        AssertThat(cooling.Saturation).IsEqual(0.5f);
        AssertThat(cooling.Brightness).IsEqual(0.72f);
        AssertThat(cooling.SweepElapsed).IsEqual(0.4f);
        AssertThat(cooling.CenterText).IsEqual("2.4");
        AssertThat(cooling.Ring).IsEqual(PulpRingMode.Accent);
        AssertThat(cooling.ReadyFrame).IsFalse();

        // Ultimate charging: saturate .55, brightness .62, the percent, the gradient arc.
        PulpSlotLook charging = PulpAbilitySlot.Derive(PulpSlotState.Charging, true, 0.64f, 0f);
        AssertThat(charging.Saturation).IsEqual(0.55f);
        AssertThat(charging.Brightness).IsEqual(0.62f);
        AssertThat(charging.CenterText).IsEqual("64%");
        AssertThat(charging.Ring).IsEqual(PulpRingMode.Meter);

        // Dormant: no art, no ring, the lock_dormant overlay; the key label stays ink2 (agent choice, no dimming).
        PulpSlotLook dormant = PulpAbilitySlot.Derive(PulpSlotState.Dormant, false, 0f, 0f);
        AssertThat(dormant.ArtVisible).IsFalse();
        AssertThat(dormant.GlyphName).IsEqual("dormant");
        AssertThat(PulpAbilitySlot.LockIconFor(dormant.GlyphName)).IsEqual("lock_dormant");
        AssertThat(dormant.Ring).IsEqual(PulpRingMode.None);
        AssertThat(dormant.KeyDim).IsFalse();

        // Suppressed: greyscale at .5, the lock_suppressed overlay, the ring in cold, no percent and no sweep.
        PulpSlotLook suppressed = PulpAbilitySlot.Derive(PulpSlotState.Suppressed, true, 0.64f, 0f);
        AssertThat(suppressed.Brightness).IsEqual(0.5f);
        AssertThat(suppressed.Saturation).IsEqual(0f);
        AssertThat(suppressed.GlyphName).IsEqual("cross");
        AssertThat(PulpAbilitySlot.LockIconFor(suppressed.GlyphName)).IsEqual("lock_suppressed");
        AssertThat(suppressed.Ring).IsEqual(PulpRingMode.Cold);
        AssertThat(suppressed.CenterText).IsEqual("");
        AssertThat(suppressed.SweepElapsed).IsEqual(-1f);
        AssertThat(PulpAbilitySlot.Derive(PulpSlotState.Suppressed, false, 0.3f, 0f).Ring).IsEqual(PulpRingMode.Cold);

        // Ultimate ready: the res2 / res / res2 ring and READY.
        PulpSlotLook readyUltimate = PulpAbilitySlot.Derive(PulpSlotState.Ready, true, 1f, 0f);
        AssertThat(readyUltimate.Ring).IsEqual(PulpRingMode.Ready);
        AssertThat(readyUltimate.ReadyTag).IsTrue();
        // An Ultimate never "cools" and a round slot never "charges".
        AssertThat(PulpAbilitySlot.Derive(PulpSlotState.Cooling, true, 0.5f, 3f).Ring).IsEqual(PulpRingMode.Meter);
        AssertThat(PulpAbilitySlot.Derive(PulpSlotState.Charging, false, 0.5f, 3f).SweepElapsed).IsEqual(0.5f);
    }

    [TestCase]
    public void TheCountdownTextDoesNotDependOnWhichValueFilledTheCacheFirst() {
        // Adjacent values either side of a tenth, in both call orders: each must read the same whatever came before.
        float[][] orders = {
            new[] { 1.000001f, 1f, 0.999999f, 1.05f, 1.1f },
            new[] { 1.1f, 1.05f, 0.999999f, 1f, 1.000001f }
        };
        foreach (float[] order in orders) {
            foreach (float v in order) {
                string expected = v switch {
                    <= 1.0001f => "1.0",
                    <= 1.1001f => "1.1",
                    _ => "1.2"
                };
                AssertThat(PulpAbilitySlot.FormatSeconds(v)).OverrideFailureMessage($"FormatSeconds({v}) after order starting {order[0]}").IsEqual(expected);
            }
        }
        // And a 10-second boundary pair.
        AssertThat(PulpAbilitySlot.FormatSeconds(10f)).IsEqual("10");
        AssertThat(PulpAbilitySlot.FormatSeconds(10.2f)).IsEqual("11");
        AssertThat(PulpAbilitySlot.FormatSeconds(9.999f)).IsEqual("10.0");
    }

    [TestCase]
    public void TheCentreNumberAndTheArtInsetFollowTheSpec() {
        AssertThat(PulpAbilitySlot.FormatSeconds(0f)).IsEqual("");
        AssertThat(PulpAbilitySlot.FormatSeconds(23.2f)).IsEqual("24");
        AssertThat(PulpAbilitySlot.FormatSeconds(9.95f)).IsEqual("10.0");
        // 3.4.2: the art inset 9 %: 74 -> 60.7 px square, 92 -> 75.4, 54 -> 44.3, 56 -> 45.9.
        Rect2 story = PulpAbilitySlot.ArtRect(74f);
        AssertThat(Mathf.Abs(story.Size.X - 60.68f) < 0.01f).IsTrue();
        AssertThat(Mathf.Abs(story.Position.X - 6.66f) < 0.01f).IsTrue();
        AssertThat(Mathf.Abs(PulpAbilitySlot.ArtRect(92f).Size.X - 75.44f) < 0.01f).IsTrue();
        AssertThat(Mathf.Abs(PulpAbilitySlot.ArtRect(54f).Size.X - 44.28f) < 0.01f).IsTrue();
        AssertThat(Mathf.Abs(PulpAbilitySlot.ArtRect(56f).Size.X - 45.92f) < 0.01f).IsTrue();
        // T19: 0.36 x the lens at 100 % (19.44, 26.64, 33.12), following the theme's scale.
        AssertThat(PulpAbilitySlot.CenterFontSize(54f, 19)).IsEqual(19);
        AssertThat(PulpAbilitySlot.CenterFontSize(74f, 19)).IsEqual(27);
        AssertThat(PulpAbilitySlot.CenterFontSize(92f, 19)).IsEqual(33);
        AssertThat(PulpAbilitySlot.CenterFontSize(74f, 27) > PulpAbilitySlot.CenterFontSize(74f, 19)).IsTrue();
    }

    [TestCase]
    public async Task TheBuiltSlotShowsTheRightPartsPerState() {
        var slot = new PulpAbilitySlot { Ultimate = true, KeyText = "hud_slot_ultimate" };
        Tree.Root.AddChild(slot);
        try {
            await Frames(2);
            // The Ultimate: a 92 lens in its 110 ring box; the key label under the lens, 9 px down.
            AssertThat(slot.CustomMinimumSize).IsEqual(new Vector2(110f, 110f));
            AssertThat(slot.LensSize).IsEqual(92f);
            AssertThat(slot.LensOrigin).IsEqual(new Vector2(9f, 9f));
            AssertThat(slot.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(slot.KeyTag.Visible).IsTrue();
            AssertThat(slot.KeyTag is Label).IsTrue();
            AssertThat(Mathf.Abs(slot.KeyTag.Position.Y - (9f + 92f + 9f)) < 0.01f).IsTrue();
            slot.State = PulpSlotState.Ready;
            AssertThat(slot.ReadyTag.Visible).IsTrue();
            AssertThat(slot.Ring.Visible).IsTrue();
            AssertThat(slot.Ring.GradientMode).IsEqual(RadialGradientMode.Loop);
            AssertThat(slot.Ring.Fraction).IsEqual(1f);
            slot.State = PulpSlotState.Charging;
            slot.Progress = 0.64f;
            AssertThat(slot.CenterLabel.Text).IsEqual("64%");
            AssertThat(slot.ReadyTag.Visible).IsFalse();
            AssertThat(slot.Ring.GradientMode).IsEqual(RadialGradientMode.Arc);
            AssertThat(Mathf.IsEqualApprox(slot.Ring.Fraction, 0.64f)).IsTrue();
            AssertThat(slot.Ring.FillColor).IsEqual(UIPalette.Meter);
            AssertThat(slot.Ring.FillColorEnd).IsEqual(UIPalette.Meter2);
            slot.State = PulpSlotState.Suppressed;
            AssertThat(slot.CenterLabel.Visible).IsFalse();
            AssertThat(slot.GlyphIcon.IconName).IsEqual("lock_suppressed");
            AssertThat(slot.GlyphIcon.GlassName).IsEqual("lock_suppressed");
            AssertThat(slot.Ring.FillColor).IsEqual(UIPalette.UnboundCold);
            slot.State = PulpSlotState.Dormant;
            AssertThat(slot.ArtNode.Visible).IsFalse();
            AssertThat(slot.GlyphIcon.IconName).IsEqual("lock_dormant");
            AssertThat(slot.Ring.Visible).IsFalse();
            // The lock overlay is inset 16 % of the lens.
            AssertThat(Mathf.Abs(slot.GlyphIcon.Position.X - (9f + 0.16f * 92f)) < 0.01f).IsTrue();
        } finally {
            slot.Free();
        }
    }

    [TestCase]
    public async Task TheReadinessRingSitsOutsideTheLensAtItsSpecWidth() {
        var story = new PulpAbilitySlot { SlotSize = 74f, State = PulpSlotState.Cooling, Progress = 0.3f, SecondsRemaining = 4f };
        var fighter = new PulpAbilitySlot { SlotSize = 54f };
        var select = new PulpAbilitySlot { SlotSize = 56f };
        var ultimate = new PulpAbilitySlot { Ultimate = true, State = PulpSlotState.Charging, Progress = 0.5f };
        foreach (PulpAbilitySlot slot in new[] { story, fighter, select, ultimate }) Tree.Root.AddChild(slot);
        try {
            await Frames(2);
            // 3.4.2: small slots 3 px with the outer edge 6 px outside the lens (74 -> 86, 54 -> 66, 56 -> 68)...
            foreach ((PulpAbilitySlot slot, float outer) in new[] { (story, 86f), (fighter, 66f), (select, 68f) }) {
                AssertThat(slot.Ring.Size).IsEqual(new Vector2(outer, outer));
                AssertThat(slot.Ring.Position).IsEqual(new Vector2(-6f, -6f));
                AssertThat(slot.Ring.Thickness).IsEqual(3f);
                // The ring's inner edge clears the lens and its 1.5 px edge.
                float innerEdge = outer / 2f - slot.Ring.Thickness;
                AssertThat(innerEdge > slot.LensSize / 2f + PulpAbilitySlot.EdgeWidth).IsTrue();
            }
            // ...the Ultimate's 5 px with its outer edge 9 px out (92 -> 110), filling its box.
            AssertThat(ultimate.Ring.Size).IsEqual(new Vector2(110f, 110f));
            AssertThat(ultimate.Ring.Position).IsEqual(Vector2.Zero);
            AssertThat(ultimate.Ring.Thickness).IsEqual(5f);
            AssertThat(110f / 2f - 5f > 92f / 2f + PulpAbilitySlot.EdgeWidth).IsTrue();
            // Cooling draws the elapsed share in accent; the rest is rgba(glow, .16).
            AssertThat(story.Ring.FillColor).IsEqual(UIPalette.Accent);
            AssertThat(Mathf.IsEqualApprox(story.Ring.Fraction, 0.3f)).IsTrue();
            AssertThat(story.Ring.TrackColor).IsEqual(new Color(UIPalette.Accent, 0.16f));
            // The not-yet flash's glass form plays round the ring: a contracting danger circle just outside it, no frame.
            NotYetFlash.Play(story.Ring, story, NotYetShape.Circle);
            Control circle = NotYetFlash.CircleOf(story.Ring);
            AssertObject(circle).IsNotNull();
            AssertThat(circle.Visible).IsTrue();
            AssertThat(circle.Size).IsEqual(story.Ring.Size);
            AssertThat(circle.Scale).IsEqual(new Vector2(NotYetFlash.StartScale, NotYetFlash.StartScale));
            AssertThat(NotYetFlash.CircleOutset).IsEqual(2.5f);
            AssertObject(NotYetFlash.FrameOf(story.Ring)).IsNull();
            AssertThat(story.SelfModulate).IsEqual(NotYetFlash.Tint);
        } finally {
            foreach (PulpAbilitySlot slot in new[] { story, fighter, select, ultimate }) slot.Free();
        }
        // A ring nobody styles keeps the pulp #3A3144 track the world-space channel rings have always drawn.
        var fresh = new RadialProgress();
        try {
            AssertThat(fresh.TrackColor.IsEqualApprox(new Color(0x3A / 255f, 0x31 / 255f, 0x44 / 255f))).IsTrue();
            AssertThat(fresh.GradientMode).IsEqual(RadialGradientMode.None);
        } finally {
            fresh.Free();
        }
    }

    [TestCase]
    public async Task AReadyStateBloomsOnceAndARedrawWithoutAChangeRebuildsNothing() {
        bool before = ComfortSettings.ReducedTemporalEffects;
        var slot = new PulpAbilitySlot { SlotSize = 74f, State = PulpSlotState.Cooling, Progress = 0.5f, SecondsRemaining = 3f };
        var reduced = new PulpAbilitySlot { SlotSize = 74f, State = PulpSlotState.Cooling, Progress = 0.5f };
        Tree.Root.AddChild(slot);
        Tree.Root.AddChild(reduced);
        try {
            ComfortSettings.Apply(false);
            await Frames(2);
            int builds = slot.GeometryBuilds;
            Color rest = slot.Modulate;
            // MOT-A row 10: entering ready blooms once, the glow peaking above its steady value, then settling exactly.
            slot.State = PulpSlotState.Ready;
            AssertThat(slot.Blooms).IsEqual(1);
            float peak = 0f;
            for (int i = 0; i < 24; i++) {
                await Frames(1);
                peak = Mathf.Max(peak, slot.BloomGlow);
            }
            AssertThat(peak > 1.2f).OverrideFailureMessage($"the bloom peaked at {peak}").IsTrue();
            await Frames(20);
            AssertThat(slot.BloomGlow).IsEqual(1f);
            AssertThat(slot.Modulate).IsEqual(rest);
            // Staying ready blooms nothing more; redraws and owner writes rebuild no geometry.
            slot.Progress = 1f;
            slot.State = PulpSlotState.Ready;
            for (int i = 0; i < 3; i++) {
                slot.QueueRedraw();
                await Frames(1);
            }
            AssertThat(slot.Blooms).IsEqual(1);
            AssertThat(slot.GeometryBuilds).IsEqual(builds);
            // Under Reduced Temporal Effects: the steady ready look at once, no bloom.
            ComfortSettings.Apply(true);
            reduced.State = PulpSlotState.Ready;
            AssertThat(reduced.Blooms).IsEqual(0);
            AssertThat(reduced.BloomGlow).IsEqual(1f);
        } finally {
            ComfortSettings.Apply(before);
            slot.Free();
            reduced.Free();
        }
    }

    /// <summary>
    /// TG-WIDGETS fix step (TG-HUDF found it at UI scale 1.4): the centre number is scaled exactly once. A HUD gives its root
    /// an unscaled theme copy and magnifies its whole layout through <see cref="UiScaleBinder"/>, so a lens there must
    /// resolve its size in that copy, not in the shared theme the UI scale has already grown; a lens on a screen that wears
    /// the shared theme takes the theme's scale. The key label and the chip's name, which resolve their roles through their
    /// own context, are checked beside it.
    /// </summary>
    [TestCase]
    public async Task TheCentreNumberIsScaledOnceInAScaledHudAndOnAThemeScaledScreen() {
        const float Scale = 1.4f;
        GlobalSaveData global = SaveManager.Instance?.GlobalData;
        float savedSetting = global?.UiScale ?? 1f;
        float savedTheme = UIPalette.AppliedUiScale;
        Control hud = null, screen = null;
        var problems = new List<string>();
        try {
            if (global != null) global.UiScale = Scale;
            UIPalette.ApplyUiScale(Scale);
            // The HUD as StoryHUD and FighterHUD build it: an unscaled theme copy on a full-rect root the binder magnifies.
            hud = new Control { Name = "HudRoot", Theme = UIPalette.NewUnscaledTheme() };
            Tree.Root.AddChild(hud);
            hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            new UiScaleBinder().Apply(hud, force: true);
            if (global == null) hud.Scale = new Vector2(Scale, Scale);   // no save loaded: the binder's transform by hand
            AssertThat(hud.Scale).IsEqual(new Vector2(Scale, Scale));
            var lens = new PulpAbilitySlot {
                SlotSize = 74f, State = PulpSlotState.Cooling, Progress = 0.5f, SecondsRemaining = 3f, KeyText = "hud_slot_special1"
            };
            var chip = new PulpStatusChip {
                BadgeSize = 44f, Kind = PulpChipKind.Damage, GlyphName = "venom", Remaining = 0.5f, NameText = "status_venom", TimeText = "4s"
            };
            hud.AddChild(lens);
            hud.AddChild(chip);
            // A screen on the shared, scale-mutated theme with no layout scale (the select screen's cards).
            screen = new Control { Name = "ThemeScaledScreen" };
            UIPalette.ApplyTheme(screen);
            Tree.Root.AddChild(screen);
            var card = new PulpAbilitySlot { SlotSize = 56f, State = PulpSlotState.Cooling, Progress = 0.5f, SecondsRemaining = 3f };
            screen.AddChild(card);
            await Frames(3);

            Theme unscaled = hud.Theme;
            // What the eye sees: the label's font size times the canvas scale above it.
            static float Drawn(Label label) => label.GetThemeFontSize("font_size") * label.GetGlobalTransform().Scale.Y;
            void Expect(string what, float drawn, float want) {
                if (Mathf.Abs(drawn - want) > 1.5f) problems.Add($"{what} draws at {drawn:0.0} px, not about {want:0.0}");
            }
            Expect("the HUD lens's number", Drawn(lens.CenterLabel), PulpAbilitySlot.CenterShare * 74f * Scale);
            Expect("the HUD lens's key label", Drawn((Label)lens.KeyTag),
                unscaled.GetFontSize("font_size", UIPalette.LabelRoles.SlotKeyTag) * Scale);
            Expect("the HUD chip's name", Drawn(chip.TextColumn.GetNode<Label>("StatusName")),
                unscaled.GetFontSize("font_size", UIPalette.LabelRoles.StatusName) * Scale);
            Expect("the screen lens's number", Drawn(card.CenterLabel), PulpAbilitySlot.CenterShare * 56f * Scale);
        } finally {
            hud?.Free();
            screen?.Free();
            UIPalette.ApplyUiScale(savedTheme);
            if (global != null) global.UiScale = savedSetting;
        }
        if (problems.Count > 0) AssertThat(string.Join(" | ", problems)).IsEqual("");
    }
}
