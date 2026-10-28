using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Core;
using FTT.UI;
using FTT.UI.Pulp;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The compact Story HUD (2026-10-27, the user's request: "combine the 3 sections into one and reduce the size of
/// everything", then "Ultimate on its own and bigger, then the 2 specials, then movement on its own", grouping "A": the
/// Specials share a recessed capsule and Movement has its own). The Fighter HUD is unchanged. Each case finds the nodes by
/// name and by the HUD's long-standing seams only, so it builds against the three-slab HUD it replaced and fails there
/// (docs/handoffs/TG_HUDS_COMPACT.md has the evidence).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHudCompactTests {

    private static Rect2 Bounds(Control control) => control.GetGlobalTransform() * new Rect2(Vector2.Zero, control.Size);

    [TestCase]
    public async Task OneGlassSlabHoldsTheVitalsTheAbilitiesAndTheTimeTools() {
        await AtScale(1f, async hud => {
            var left = hud.GetNode<Control>("SafeArea/TopLeft_Panel");
            var slabs = new List<PanelContainer>();
            Collect(left, slabs, node => node is PanelContainer panel && panel.ThemeTypeVariation.ToString().StartsWith("GlassPanel"));
            AssertThat(slabs.Count).OverrideFailureMessage($"{slabs.Count} glass slabs on the left, not one").IsEqual(1);
            PanelContainer slab = slabs[0];
            var missing = new List<string>();
            foreach (string name in new[] {
                "PlayerPortrait", "HealthBar_BG", "BlockCharges_Panel", "DamageStatus_Indicator", "ControlStatus_Indicator",
                "UltimateMeter_BG", "DefySeal", "Cooldown1_Icon", "Cooldown2_Icon", "Cooldown3_Icon", "RewindCounter",
                "BeaconAnchors", "TimeFreezeIndicator" }) {
                Node node = hud.FindChild(name, true, false);
                if (node == null || !slab.IsAncestorOf(node)) missing.Add(name);
            }
            if (missing.Count > 0) AssertThat("outside the one slab: " + string.Join(", ", missing)).IsEqual("");
            // No panel tags (VITALS / ABILITIES went with the three slabs).
            var tags = new List<Node>();
            Collect(left, tags, node => node is Label label && (label.Text == "hud_vitals" || label.Text == "hud_abilities") && label.IsVisibleInTree());
            AssertThat(tags.Count).IsEqual(0);
            await Task.CompletedTask;
        });
    }

    [TestCase]
    public async Task TheSlabIsAbout484By182AtTheTopLeftAtUiScaleOne() {
        await AtScale(1f, async hud => {
            Control slab = SlabOf(hud);
            AssertObject(slab).IsNotNull();
            Rect2 rect = Bounds(slab);
            AssertFloat(rect.Position.X).IsEqualApprox(24f, 1f);
            AssertFloat(rect.Position.Y).IsEqualApprox(20f, 1f);
            AssertFloat(rect.Size.X).IsEqualApprox(484f, 4f);
            AssertFloat(rect.Size.Y).IsEqualApprox(182f, 4f);
            // Act III's Beacon and anchor pips fit the time tools column without widening the slab.
            EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 3, Max = 3 });
            await Frames(3);
            AssertThat(Bounds(slab).Size.X <= 484f + 0.5f).OverrideFailureMessage($"Act III widens the slab to {Bounds(slab).Size.X}").IsTrue();
            EventBus.Instance.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 0, Max = 0 });
        });
    }

    [TestCase]
    public async Task TheUltimateLensStandsAloneAndIsLargerThanTheThreeAbilityLenses() {
        await AtScale(1f, async hud => {
            PulpAbilitySlot ult = hud.UltimateSlotWidget;
            AssertFloat(ult.LensSize).IsEqual(64f);
            foreach (PulpAbilitySlot slot in hud.SlotWidgets) {
                AssertFloat(slot.LensSize).IsEqual(48f);
                AssertThat(ult.LensSize).IsGreater(slot.LensSize);
            }
            AssertObject(CapsuleOf(ult)).OverrideFailureMessage("the Ultimate stands outside the capsules").IsNull();
            await Task.CompletedTask;
        });
    }

    [TestCase]
    public async Task TheSpecialsShareOneCapsuleAndMovementSitsInAnotherAndTheyDoNotOverlap() {
        await AtScale(1f, async hud => {
            Control s1 = CapsuleOf(hud.SlotWidgets[0]), s2 = CapsuleOf(hud.SlotWidgets[1]), mov = CapsuleOf(hud.SlotWidgets[2]);
            AssertObject(s1).OverrideFailureMessage("Special 1 sits in a capsule").IsNotNull();
            AssertObject(mov).OverrideFailureMessage("Movement sits in a capsule").IsNotNull();
            AssertThat(s1 == s2).OverrideFailureMessage("Special 1 and Special 2 share one capsule").IsTrue();
            AssertThat(s1 == mov).OverrideFailureMessage("Movement has its own capsule").IsFalse();
            Rect2 specials = Bounds(s1), movement = Bounds(mov);
            AssertThat(specials.Intersects(movement)).OverrideFailureMessage($"the capsules overlap: {specials} vs {movement}").IsFalse();
            AssertThat(movement.Position.X).IsGreater(specials.End.X);
            // Each capsule encloses its lenses' readiness rings.
            foreach ((PulpAbilitySlot slot, Rect2 capsule) in new[] { (hud.SlotWidgets[0], specials), (hud.SlotWidgets[1], specials), (hud.SlotWidgets[2], movement) }) {
                AssertThat(capsule.Grow(0.5f).Encloses(Bounds(slot.Ring))).OverrideFailureMessage($"{slot.GetParent().Name}'s ring leaves its capsule").IsTrue();
            }
            await Task.CompletedTask;
        });
    }

    [TestCase]
    public async Task TheTimeToolsSitRightOfTheHairlineAtTheSlabsRightEnd() {
        await AtScale(1f, async hud => {
            var divider = hud.FindChild("TimeDivider", true, false) as Control;
            AssertObject(divider).OverrideFailureMessage("a hairline divides the abilities from the time tools").IsNotNull();
            Rect2 line = Bounds(divider);
            Rect2 movement = Bounds(CapsuleOf(hud.SlotWidgets[2]));
            AssertThat(line.Position.X).IsGreater(movement.End.X);
            Rect2 slab = Bounds(SlabOf(hud));
            foreach (string name in new[] { "RewindCounter", "TimeFreezeIndicator" }) {
                Rect2 tool = Bounds((Control)hud.FindChild(name, true, false));
                AssertThat(tool.Position.X >= line.End.X).OverrideFailureMessage($"{name} at {tool} is not right of the hairline {line}").IsTrue();
                AssertThat(slab.Encloses(tool)).OverrideFailureMessage($"{name} leaves the slab").IsTrue();
            }
            // A column: Time Freeze under the rewind counter.
            Rect2 rewind = Bounds((Control)hud.FindChild("RewindCounter", true, false));
            Rect2 freeze = Bounds((Control)hud.FindChild("TimeFreezeIndicator", true, false));
            AssertThat(freeze.Position.Y >= rewind.End.Y).IsTrue();
            await Task.CompletedTask;
        });
    }

    [TestCase]
    public async Task TheTitleAndObjectiveSitUnderTheSlabAndTheLeftClusterEndsBy260() {
        await AtScale(1f, async hud => {
            hud.SetLevelTitle("campaign_level_orleans");
            hud.SetObjective("florence_objective_reach_boss");
            await Frames(2);
            Rect2 slab = Bounds(SlabOf(hud));
            Rect2 title = Bounds(hud.GetNode<Control>("SafeArea/TopLeft/LevelTitle"));
            Rect2 objective = Bounds(hud.GetNode<Control>("SafeArea/TopLeft/Objective"));
            AssertThat(title.Position.Y >= slab.End.Y + 10f).OverrideFailureMessage($"the title {title} is not under the slab {slab}").IsTrue();
            AssertThat(objective.Position.Y >= title.End.Y - 0.5f).IsTrue();
            AssertFloat(title.Position.X).IsEqualApprox(slab.Position.X + 8f, 1f);
            float bottom = Mathf.Max(slab.End.Y, objective.End.Y);
            AssertThat(bottom <= 262f).OverrideFailureMessage($"the left cluster ends at {bottom}, past about 260").IsTrue();
        });
    }

    [TestCase]
    public async Task StatusNamesAreShownInSentenceCase() {
        await AtScale(1f, async hud => {
            EventBus.Instance.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4f, Intensity = 1f });
            hud._Process(1.0 / 60.0);
            PulpStatusChip chip = hud.StatusChips.Damage;
            var name = chip.FindChild("StatusName", true, false) as Label;
            AssertObject(name).IsNotNull();
            AssertThat(name.Uppercase).OverrideFailureMessage("the status name is drawn in capitals").IsFalse();
            AssertThat(chip.BadgeSize).IsEqual(30f);
            EventBus.Instance.RaiseStatusEffectCleared(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.None });
            await Task.CompletedTask;
        });
    }

    [TestCase]
    public async Task NoStoryHudTextIsDrawnUnder18PixelsAtUiScaleOne() {
        await AtScale(1f, async hud => {
            EventBus bus = EventBus.Instance;
            hud.SetLevelTitle("campaign_level_orleans");
            hud.SetObjective("florence_objective_reach_boss");
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Venom, Duration = 4f, Intensity = 1f });
            bus.RaiseStatusEffectApplied(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.Suppression, Duration = 2f, Intensity = 1f });
            bus.RaiseCooldownStarted(new CooldownPayload { PlayerIndex = 0, Slot = AbilitySlot.Special1, Duration = 6f });
            bus.RaiseUltimateMeterChanged(new UltimateMeterPayload { PlayerIndex = 0, CurrentValue = 64f, NormalizedValue = 0.64f });
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 2, Max = 3 });
            bus.RaiseTimelineIntegrityChanged(new IntegrityPayload { Percent = 64f, Tier = IntegrityTier.Stabilized, LivingExtractors = 2 });
            hud.ShowBossBar("boss_archive_prime_name", 640, 1000, new[] { 0.5f });
            for (int i = 0; i < 3; i++) hud._Process(1.0 / 60.0);
            await Frames(3);
            var labels = new List<Node>();
            Collect(hud, labels, node => node is Label label && label.IsVisibleInTree() && label.Text.Length > 0 && label.VisibleCharacters != 0);
            AssertThat(labels.Count).IsGreater(10);
            var small = new List<string>();
            foreach (Node node in labels) {
                var label = (Label)node;
                if (label.SelfModulate.A <= 0.01f) continue;   // the dust float's layout carrier draws nothing
                float onScreen = label.GetThemeFontSize("font_size") * label.GetGlobalTransform().Scale.Y;
                if (onScreen < 18f - 0.05f) small.Add($"{label.GetPath()} '{label.Text}' {onScreen:0.##} px");
            }
            if (small.Count > 0) AssertThat("under 18 px: " + string.Join(" | ", small)).IsEqual("");
            bus.RaiseStatusEffectCleared(new StatusEffectPayload { TargetIndex = 0, Type = StatusType.None });
            bus.RaiseAnchorChargesChanged(new AnchorChargesPayload { Charges = 0, Max = 0 });
        });
    }

    [TestCase]
    public async Task TheFighterHudKeepsItsLensesAndBadges() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var packed = ResourceLoader.Load<PackedScene>(FighterHUD.ScenePath);
        var hud = packed.Instantiate<FighterHUD>();
        tree.Root.AddChild(hud);
        try {
            await Frames(2);
            var lenses = new List<Node>();
            Collect(hud, lenses, node => node is PulpAbilitySlot);
            var chips = new List<Node>();
            Collect(hud, chips, node => node is PulpStatusChip);
            AssertThat(lenses.Count).IsEqual(6);
            foreach (Node node in lenses) {
                var lens = (PulpAbilitySlot)node;
                AssertFloat(lens.LensSize).IsEqual(54f);
                AssertFloat(lens.RingThickness).IsEqual(PulpAbilitySlot.SmallRingWidth);
                AssertFloat(lens.RingOutset).IsEqual(PulpAbilitySlot.SmallRingOutset);
                AssertThat(lens.ReadyPill).IsFalse();
            }
            AssertThat(chips.Count).IsEqual(4);
            foreach (Node node in chips) {
                var chip = (PulpStatusChip)node;
                AssertFloat(chip.BadgeSize).IsEqual(40f);
                AssertFloat(chip.RingThickness).IsEqual(PulpStatusChip.RingWidth);
                AssertThat(chip.UppercaseName).IsTrue();
                AssertThat(chip.NameRole).IsEqual(UIPalette.LabelRoles.StatusName);
            }
        } finally {
            hud.GetParent()?.RemoveChild(hud);
            hud.Free();
        }
    }

    // ---- helpers --------------------------------------------------------

    /// <summary>The nearest ancestor of <paramref name="node"/> that is a capsule (by type name, so the suite builds against any HUD).</summary>
    private static Control CapsuleOf(Node node) {
        for (Node at = node?.GetParent(); at != null; at = at.GetParent()) {
            if (at.GetType().Name == "HudCapsule") return (Control)at;
        }
        return null;
    }

    private static Control SlabOf(StoryHUD hud) {
        var slabs = new List<PanelContainer>();
        Collect(hud.GetNode("SafeArea/TopLeft_Panel"), slabs, node => node is PanelContainer panel && panel.ThemeTypeVariation.ToString().StartsWith("GlassPanel"));
        return slabs.Count == 1 ? slabs[0] : null;
    }

    private static void Collect<T>(Node root, List<T> into, System.Func<Node, bool> match) where T : class {
        if (match(root) && root is T hit) into.Add(hit);
        Godot.Collections.Array<Node> children = root.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, into, match);
    }

    /// <summary>Mounts a Story HUD in a 1920 x 1080 viewport at <paramref name="scale"/> (restored after) and frees it after <paramref name="body"/>.</summary>
    private static async Task AtScale(float scale, System.Func<StoryHUD, Task> body) {
        SaveManager save = SaveManager.Instance;
        AssertObject(save?.GlobalData).IsNotNull();
        float original = save.GlobalData.UiScale;
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var port = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
        tree.Root.AddChild(port);
        try {
            save.GlobalData.UiScale = scale;
            StoryHUD hud = StoryHUD.CreateDefault();
            port.AddChild(hud);
            await Frames(4);
            await body(hud);
        } finally {
            save.GlobalData.UiScale = original;
            port.Free();
        }
    }

    private static async Task Frames(int count) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < count; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
