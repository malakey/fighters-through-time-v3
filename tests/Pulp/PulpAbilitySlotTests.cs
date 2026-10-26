using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS step 2. The ability slot: the pure state table, the art inset and the built controls.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpAbilitySlotTests {

    [TestCase]
    public void TheStateTableMatchesTheSpec() {
        PulpSlotLook ready = PulpAbilitySlot.Derive(PulpSlotState.Ready, false, 1f, 0f);
        AssertThat(ready.ReadyFrame).IsTrue();
        AssertThat(ready.SweepElapsed).IsEqual(-1f);
        AssertThat(ready.Ring).IsEqual(PulpRingMode.None);

        PulpSlotLook cooling = PulpAbilitySlot.Derive(PulpSlotState.Cooling, false, 0.4f, 2.4f);
        AssertThat(cooling.Saturation).IsEqual(0.5f);
        AssertThat(cooling.Brightness).IsEqual(0.72f);
        AssertThat(cooling.SweepElapsed).IsEqual(0.4f);
        AssertThat(cooling.CenterText).IsEqual("2.4");

        PulpSlotLook charging = PulpAbilitySlot.Derive(PulpSlotState.Charging, true, 0.64f, 0f);
        AssertThat(charging.Saturation).IsEqual(0.55f);
        AssertThat(charging.Brightness).IsEqual(0.62f);
        AssertThat(charging.CenterText).IsEqual("64%");
        AssertThat(charging.Ring).IsEqual(PulpRingMode.Meter);

        PulpSlotLook dormant = PulpAbilitySlot.Derive(PulpSlotState.Dormant, false, 0f, 0f);
        AssertThat(dormant.ArtVisible).IsFalse();
        AssertThat(dormant.GlyphName).IsEqual("dormant");
        AssertThat(dormant.KeyDim).IsTrue();

        PulpSlotLook suppressed = PulpAbilitySlot.Derive(PulpSlotState.Suppressed, true, 0.64f, 0f);
        AssertThat(suppressed.Brightness).IsEqual(0.5f);
        AssertThat(suppressed.Saturation).IsEqual(0f);
        AssertThat(suppressed.GlyphName).IsEqual("cross");
        AssertThat(suppressed.Ring).IsEqual(PulpRingMode.Cold);
        AssertThat(suppressed.CenterText).IsEqual("");
        AssertThat(suppressed.SweepElapsed).IsEqual(-1f);

        PulpSlotLook readyUltimate = PulpAbilitySlot.Derive(PulpSlotState.Ready, true, 1f, 0f);
        AssertThat(readyUltimate.Ring).IsEqual(PulpRingMode.Ready);
        AssertThat(readyUltimate.ReadyTag).IsTrue();
        // An Ultimate never "cools" and a square slot never "charges".
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
        // 74 -> 54 px at +10; 54 -> 38; 92 -> 69.
        Rect2 story = PulpAbilitySlot.ArtRect(74f);
        AssertThat(Mathf.Round(story.Size.X)).IsEqual(54f);
        AssertThat(Mathf.Round(story.Position.X)).IsEqual(10f);
        AssertThat(Mathf.Round(PulpAbilitySlot.ArtRect(54f).Size.X)).IsEqual(38f);
        AssertThat(Mathf.Round(PulpAbilitySlot.ArtRect(92f).Size.X)).IsEqual(69f);
    }

    [TestCase]
    public void TheBuiltSlotShowsTheRightPartsPerState() {
        var slot = new PulpAbilitySlot { Ultimate = true, KeyText = "hud_slot_ultimate" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(slot);
        try {
            AssertThat(slot.CustomMinimumSize).IsEqual(new Vector2(116f, 116f));
            AssertThat(slot.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(slot.KeyTag.Visible).IsTrue();
            slot.State = PulpSlotState.Ready;
            AssertThat(slot.ReadyTag.Visible).IsTrue();
            slot.State = PulpSlotState.Charging;
            slot.Progress = 0.64f;
            AssertThat(slot.CenterLabel.Text).IsEqual("64%");
            AssertThat(slot.ReadyTag.Visible).IsFalse();
            slot.State = PulpSlotState.Suppressed;
            AssertThat(slot.CenterLabel.Visible).IsFalse();
            AssertThat(slot.GlyphIcon.IconName).IsEqual("cross");
            AssertThat(slot.GlyphIcon.Flat).IsTrue();
            slot.State = PulpSlotState.Dormant;
            AssertThat(slot.ArtNode.Visible).IsFalse();
            AssertThat(slot.GlyphIcon.IconName).IsEqual("dormant");
        } finally {
            slot.Free();
        }
    }
}
