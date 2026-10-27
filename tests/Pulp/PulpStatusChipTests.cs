using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Pulp;

/// <summary>The status chip (TG-WIDGETS step 2: the G7 badge; first written by UR-WIDGETS): colours, the ring, the empty G8 slot and the stable size.</summary>
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
        // 3.4.3: a 3 px duration ring with its outer edge 5 px outside the badge (44 -> 54, 40 -> 50).
        AssertThat(PulpStatusChip.RingWidth).IsEqual(3f);
        AssertThat(PulpStatusChip.RingRadius(44f) + PulpStatusChip.RingWidth / 2f).IsEqual(27f);
        AssertThat(PulpStatusChip.RingRadius(40f) + PulpStatusChip.RingWidth / 2f).IsEqual(25f);
        // G8: the empty slot's ring is edge-dim.
        AssertThat(PulpStatusChip.EmptyRingColor).IsEqual(UIPalette.EdgeDim);
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
            // The glyph at 62 % is a glass icon in the bold weight (under 30 px) and the status colour.
            AssertThat(full.Glyph.GlassName).IsEqual("venom");
            AssertThat(full.Glyph.DrawsBold).IsTrue();
            AssertThat(full.Glyph.Tint).IsEqual(UIPalette.StatusDamage);
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

    /// <summary>
    /// TG-CLOSE (TG-HUDF kit gap 3): a chip given a rect taller than its badge (a 54 px lens row) draws its badge, glyph and
    /// text column centred on that rect; it drew them at its top, so the Fighter HUD had to shrink-centre it.
    /// </summary>
    [TestCase]
    public void ABadgeInATallerRectIsCentredOnIt() {
        var chip = new PulpStatusChip { BadgeSize = 40f, Kind = PulpChipKind.Damage, GlyphName = "venom", NameText = "status_venom", TimeText = "4.0s" };
        UIPalette.ApplyTheme(chip);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(chip);
        try {
            chip.Size = new Vector2(300f, 54f);
            chip.Notification((int)Control.NotificationResized);
            float glyphCentre = chip.Glyph.Position.Y + chip.Glyph.Size.Y / 2f;
            float columnCentre = chip.TextColumn.Position.Y + chip.TextColumn.Size.Y / 2f;
            AssertThat(Mathf.Abs(glyphCentre - 27f) < 0.6f).OverrideFailureMessage($"the glyph is centred at {glyphCentre}, not 27").IsTrue();
            AssertThat(Mathf.Abs(columnCentre - 27f) < 1f).OverrideFailureMessage($"the text column is centred at {columnCentre}, not 27").IsTrue();
            AssertThat(Mathf.Abs(chip.BadgeTop - 7f) < 0.01f).OverrideFailureMessage($"the badge's top is {chip.BadgeTop}, not 7").IsTrue();
        } finally {
            chip.Free();
        }
    }
}
