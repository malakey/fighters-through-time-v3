using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>UR-WIDGETS step 2. The status chip: colours, ring share, the empty slot and the stable size.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PulpStatusChipTests {

    [TestCase]
    public void TheGlyphColourAndTheRingShareFollowTheSpec() {
        AssertThat(PulpStatusChip.GlyphColor(PulpChipKind.Damage)).IsEqual(UIPalette.StatusDamage);
        AssertThat(PulpStatusChip.GlyphColor(PulpChipKind.Control)).IsEqual(UIPalette.StatusControl);
        AssertThat(PulpStatusChip.GlyphColor(PulpChipKind.Suppression)).IsEqual(UIPalette.UnboundCold);
        AssertThat(PulpStatusChip.RingFraction(false, true, 0.3f)).IsEqual(0.3f);
        AssertThat(PulpStatusChip.RingFraction(false, true, 4f)).IsEqual(1f);
        AssertThat(PulpStatusChip.RingFraction(true, true, 0.5f)).IsEqual(0f);
        AssertThat(PulpStatusChip.RingFraction(false, false, 0.5f)).IsEqual(0f);
    }

    [TestCase]
    public void AnEmptySlotKeepsItsSizeHidesTheGlyphAndFadesTheText() {
        var full = new PulpStatusChip { BadgeSize = 44f, GlyphName = "venom", NameText = "Venom", TimeText = "4s" };
        var empty = new PulpStatusChip { BadgeSize = 44f, GlyphName = "venom", NameText = "Venom", TimeText = "4s", Empty = true };
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        root.AddChild(full);
        root.AddChild(empty);
        try {
            AssertThat(full.GetCombinedMinimumSize().Y).IsEqual(44f);
            AssertThat(empty.GetCombinedMinimumSize().Y).IsEqual(44f);
            AssertThat(empty.Glyph.Visible).IsFalse();
            AssertThat(full.Glyph.Visible).IsTrue();
            AssertThat(full.Glyph.Size.X.ToString("0.00") + "|" + full.Glyph.Position.X.ToString("0.00")).IsEqual("27.28|8.36");
            AssertThat(empty.TextColumn.Modulate.A).IsEqual(0.35f);
            AssertThat(full.TextColumn.Modulate.A).IsEqual(1f);
            AssertThat(full.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(full.GetCombinedMinimumSize().X > 44f + PulpStatusChip.TextGap).IsTrue();
            full.ShowText = false;
            AssertThat(full.GetCombinedMinimumSize().X).IsEqual(44f);
        } finally {
            full.Free();
            empty.Free();
        }
    }
}
