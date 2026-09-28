using System.IO;
using FTT.Combat;
using FTT.Core;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W5 — G12 Color Independence. Player slots carry a shape as well as
/// a colour (P1 ▲ / P2 ●), and a local Settings → Gameplay palette (Default,
/// Blue/Orange, High-Contrast) recolors only the presentation of the ownership
/// outline and HUD slot colours. It never enters the simulation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlayerSlotPaletteTests {

    [TestCase]
    public void TheDefaultPaletteIsTheF24OwnershipTableAndEveryPaletteSeparatesTheSlots() {
        AssertThat(PlayerSlotPalettes.SlotColor(PlayerSlotPalette.Default, 0)).IsEqual(GlowPalette.SlotColor(0));
        AssertThat(PlayerSlotPalettes.SlotColor(PlayerSlotPalette.Default, 1)).IsEqual(GlowPalette.SlotColor(1));
        for (int palette = 0; palette < PlayerSlotPalettes.Count; palette++) {
            Color one = PlayerSlotPalettes.SlotColor((PlayerSlotPalette)palette, 0);
            Color two = PlayerSlotPalettes.SlotColor((PlayerSlotPalette)palette, 1);
            AssertThat(one == two).OverrideFailureMessage($"palette {palette} paints both slots alike").IsFalse();
            AssertThat(TranslationServer.Translate(PlayerSlotPalettes.LabelKey((PlayerSlotPalette)palette)).ToString())
                .IsNotEqual(PlayerSlotPalettes.LabelKey((PlayerSlotPalette)palette));
        }
        AssertThat(PlayerSlotPalettes.ShapeGlyph(0)).IsEqual("▲");
        AssertThat(PlayerSlotPalettes.ShapeGlyph(1)).IsEqual("●");
        AssertThat(PlayerSlotPalettes.Sanitize(99)).IsEqual(PlayerSlotPalette.Default);
    }

    [TestCase]
    public void TheSettingsRowPersistsTheChoiceAndTheOutlineFollowsIt() {
        GlobalSaveData data = SaveManager.Instance?.GlobalData;
        if (data == null) return;
        int saved = data.PlayerSlotPalette;
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/ui/Settings.tscn");
        Node settings = packed.Instantiate();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(settings);
        var owner = new Node2D { Name = "PaletteSubject" };
        var sprite = new Sprite2D { Name = "Body" };
        owner.AddChild(sprite);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(owner);
        GlowPresentationController glow = GlowPresentationController.AttachTo(
            owner, sprite, ownerPlayerIndex: -1, subscribeToStoryEvents: false);
        try {
            var row = settings.GetNodeOrNull<PlayerSlotPaletteRow>(
                "Margin/Panel/Body/Tabs/Gameplay/PlayerSlotPaletteRow");
            AssertObject(row).OverrideFailureMessage("Settings → Gameplay has no player-slot palette row").IsNotNull();
            AssertThat(row.Selector.ItemCount).IsEqual(PlayerSlotPalettes.Count);

            row.Apply((int)PlayerSlotPalette.BlueOrange);
            AssertThat(data.PlayerSlotPalette).IsEqual((int)PlayerSlotPalette.BlueOrange);
            AssertThat(PlayerSlotPalettes.Active).IsEqual(PlayerSlotPalette.BlueOrange);
            glow.SetSlotIndicator(1);
            AssertThat(glow.OwnershipOutlineColor)
                .IsEqual(PlayerSlotPalettes.SlotColor(PlayerSlotPalette.BlueOrange, 1));
        } finally {
            data.PlayerSlotPalette = saved;
            SaveManager.Instance.SaveGlobalData();
            owner.Free();
            settings.GetParent()?.RemoveChild(settings);
            settings.Free();
        }
    }

    [TestCase]
    public void TheDeterministicSimulationNeverReadsThePalette() {
        // Presentation only (plan §3.5): the palette may reach the driver's proxies,
        // never a system, component or rule the snapshot and hash are built from.
        foreach (string file in new[] {
                     "scripts/FighterSim/FighterSimulationSystems.cs",
                     "scripts/FighterSim/FighterEntitySystems.cs",
                     "scripts/FighterSim/FighterSimulationComponents.cs",
                     "scripts/FighterSim/FighterSimulation.cs",
                     "scripts/FighterSim/FighterCpuController.cs" }) {
            string source = File.ReadAllText(ProjectSettings.GlobalizePath("res://" + file));
            AssertThat(source.Contains("PlayerSlotPalette"))
                .OverrideFailureMessage($"{file} reads the local slot palette").IsFalse();
        }
    }
}
