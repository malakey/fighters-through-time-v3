using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Level 1 (Florence) content contract.
///
/// Florence's three budgeted Chronal Extractors were the one standing content
/// gap <c>docs/DUST_ECONOMY.md</c> §6 left open: Package 5 deliberately did
/// not retrofit the pre-existing Tutorial and Florence controllers, so the
/// ledger budgeted three machines that no scene actually authored. Package 11
/// A3 closes it.
///
/// Level 1 is <b>untimed</b> — it does not extend
/// <c>StoryLevelControllerBase</c>, so no F01 Integrity clock arms and no tier
/// bonus applies. These machines therefore carry presentation and dust only;
/// the per-machine dust allocation is A10's.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Level01ContentTests {

    [TestCase]
    public void FlorenceAuthorsItsThreeBudgetedChronalExtractors() {
        var level = new Level01Controller { Name = "FlorenceContentFixture" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(level);
        try {
            IReadOnlyList<ChronalExtractor> extractors = level.Extractors;
            AssertThat(extractors.Count)
                .OverrideFailureMessage("DUST_ECONOMY budgets Florence exactly three Extractors.")
                .IsEqual(3);

            var issues = new List<string>();
            var seen = new HashSet<string>();
            foreach (ChronalExtractor extractor in extractors) {
                if (extractor == null || !GodotObject.IsInstanceValid(extractor)) {
                    issues.Add("an Extractor failed to instantiate from the shared template");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(extractor.ObjectID)) issues.Add("an Extractor has no stable ID");
                else if (!seen.Add(extractor.ObjectID)) issues.Add($"duplicate Extractor ID {extractor.ObjectID}");
                if (!extractor.ObjectID.StartsWith("florence_extractor_", System.StringComparison.Ordinal)) {
                    issues.Add($"{extractor.ObjectID} does not follow the florence_extractor_N convention");
                }
                // The dust value stays resource-owned: never overridden from
                // level code, per the standing rule in the base controller.
                if (extractor.DustReward != 3) {
                    issues.Add($"{extractor.ObjectID} overrides the resource-owned dust value");
                }
            }
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

            // One machine per traversable room, off the critical path — never
            // stacked in one place, and never in the boss arena.
            var columns = new List<float>();
            foreach (ChronalExtractor extractor in extractors) columns.Add(extractor.Position.X);
            columns.Sort();
            AssertThat(columns[1] - columns[0])
                .OverrideFailureMessage("Florence's Extractors must be spread across its rooms.")
                .IsGreater(1500f);
            AssertThat(columns[2] - columns[1]).IsGreater(1500f);
        } finally {
            level.Free();
        }
    }

    /// <summary>
    /// Package 12 W9 (M18): "Phase 2 (50% HP): the scaffolding burns away,
    /// shrinking the arena". The two scaffolding platforms run the standard
    /// Crumbling Platform beat (0.8 s shake + 1.2 s collapse) and never return,
    /// and burning barricades close the arena to a span that still fits the
    /// Inquisitor's ranged band.
    /// </summary>
    [TestCase]
    public void TheBorgiaPhaseTwoBurnsTheScaffoldingAndClosesTheArena() {
        // The standard Crumbling Platform timings the scaffolding reuses.
        var reference = new CrumblingPlatform();
        AssertFloat(reference.ShakeDuration).IsEqualApprox(0.8f, 0.0001f);
        AssertFloat(reference.CollapseDuration).IsEqualApprox(1.2f, 0.0001f);
        AssertFloat(reference.RespawnDuration).IsEqualApprox(5f, 0.0001f);
        reference.Free();

        var level = new Level01Controller { Name = "FlorenceArenaFixture" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(level);
        try {
            AssertThat(level.Scaffolding.Count).IsEqual(2);
            AssertThat(level.ArenaShrunk).IsFalse();
            FTT.Enemies.BossController borgia = level.BossEncounter.Boss;
            AssertObject(borgia).IsNotNull();

            borgia.TakeDamage(borgia.ScaledMaxHP / 2 + 1);
            AssertThat(borgia.CurrentPhase).IsEqual(1);
            AssertThat(level.ArenaShrunk).IsTrue();
            foreach (CrumblingPlatform scaffold in level.Scaffolding) {
                AssertThat(scaffold.State).IsEqual(CrumblingPlatformState.Shaking);
            }

            level.AdvanceArenaShrink(Level01Controller.ArenaShrinkSeconds);
            AssertFloat(level.BarricadeWest.Position.X).IsEqualApprox(Level01Controller.ArenaShrinkLeftWallX, 0.01f);
            AssertFloat(level.BarricadeEast.Position.X).IsEqualApprox(Level01Controller.ArenaShrinkRightWallX, 0.01f);
            float span = level.ArenaInnerRightX - level.ArenaInnerLeftX;
            AssertThat(span < 920f).OverrideFailureMessage("The arena did not shrink.").IsTrue();
            AssertThat(span > borgia.Data.RangedRangeThreshold * 60f)
                .OverrideFailureMessage($"The shrunk arena ({span} px) no longer fits the ranged band.").IsTrue();

            // 0.8 s shake, 1.2 s collapse, then gone for good (no 5 s respawn).
            foreach (CrumblingPlatform scaffold in level.Scaffolding) {
                scaffold._PhysicsProcess(0.81);
                AssertThat(scaffold.State).IsEqual(CrumblingPlatformState.Collapsing);
                scaffold._PhysicsProcess(1.21);
                AssertThat(scaffold.State).IsEqual(CrumblingPlatformState.Disabled);
                for (int second = 0; second < 30; second++) scaffold._PhysicsProcess(1.0);
                AssertThat(scaffold.State).IsEqual(CrumblingPlatformState.Disabled);
            }
        } finally {
            level.Free();
        }
    }

    /// <summary>
    /// D13 (PROVISIONAL-FONT-PLACEMENT): Florence places its own Restoration Font
    /// (it does not extend the base controller), on the workshop floor past the
    /// Middle anchor, outside the boss reveal and clear of its Extractors and
    /// checkpoints, with its completion refusal wired.
    /// </summary>
    [TestCase]
    public void FlorenceAuthorsOneRestorationFontInTheWorkshopBeforeTheBossRoom() {
        // The real scene, not a bare controller: its SteamGeyser is scene-authored,
        // and the first placement stood inside it.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
        Level01Controller level = null;
        try {
            level = ResourceLoader.Load<PackedScene>(FlorenceScenePath).Instantiate<Level01Controller>();
            level.Name = "FlorenceFontFixture";
            tree.Root.AddChild(level);
            RestorationFont font = level.PlacedRestorationFont;
            AssertObject(font).IsNotNull();
            AssertString(font.FontID).IsEqual(Level01Controller.RestorationFontID);
            AssertString(font.FontID).IsEqual("level_01.restoration_font");
            AssertThat(font.Position).IsEqual(Level01Controller.RestorationFontPosition);
            AssertThat(font.RefusesWhile != null).IsTrue();
            AssertThat(font.RefusesWhile()).IsFalse();
            AssertObject(font.GetNodeOrNull<InteractionArea>("InteractionArea")).IsNotNull();
            AssertObject(font.ChannelRadial).IsNotNull();

            Vector2 position = font.GlobalPosition;
            var issues = new List<string>();
            CheckpointTrigger middle = null;
            var checkpoints = new List<CheckpointTrigger>();
            Godot.Collections.Array<Node> children = level.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    if (child is not CheckpointTrigger checkpoint) continue;
                    checkpoints.Add(checkpoint);
                    if (checkpoint.CheckpointID == "florence_checkpoint_1") middle = checkpoint;
                }
            }
            AssertObject(middle).IsNotNull();
            if (position.X < middle.GlobalPosition.X + 110f) {
                issues.Add($"font x {position.X} is not 110 px past florence_checkpoint_1 ({middle.GlobalPosition.X})");
            }
            float arenaWest = Level01Controller.BossArenaBounds.Position.X;
            if (position.X > arenaWest - 120f) issues.Add($"font x {position.X} is within 120 px of the arena ({arenaWest})");
            foreach (ChronalExtractor extractor in level.Extractors) {
                if (position.DistanceTo(extractor.GlobalPosition) < 320f) {
                    issues.Add($"font is within 320 px of {extractor.ObjectID}");
                }
            }
            foreach (CheckpointTrigger checkpoint in checkpoints) {
                if (position.DistanceTo(checkpoint.GlobalPosition) < 110f) {
                    issues.Add($"font is within 110 px of {checkpoint.CheckpointID}");
                }
            }
            // The workshop floor (top 900) lies in [y - 2, y + 16] under the font.
            if (!(900f >= position.Y - 2f && 900f <= position.Y + 16f)) {
                issues.Add($"the room 3 floor top (900) is not under the font at {position}");
            }
            bool floorUnder = false;
            Godot.Collections.Array<Node> floors = level.FindChildren("Floor_*", nameof(StaticBody2D), true, false);
            using var floorsLifetime = floors.AsDisposable();
            foreach (Node child in floors) {
                if (child is not StaticBody2D body) continue;
                Godot.Collections.Array<Node> shapes = body.GetChildren();
                using var shapesLifetime = shapes.AsDisposable();
                foreach (Node shapeNode in shapes) {
                    if (shapeNode is not CollisionShape2D { Shape: RectangleShape2D rect } shape) continue;
                    float left = shape.GlobalPosition.X - rect.Size.X / 2f;
                    float right = shape.GlobalPosition.X + rect.Size.X / 2f;
                    float top = shape.GlobalPosition.Y - rect.Size.Y / 2f;
                    if (position.X >= left && position.X <= right
                        && top >= position.Y - 2f && top <= position.Y + 16f) floorUnder = true;
                }
            }
            if (!floorUnder) issues.Add($"no Floor_* surface stands under the font at {position}");

            // No damaging hazard reaches a hero channelling anywhere in the circle:
            // the scene's SteamGeyser and the two workshop spike strips among them.
            List<FontHazardClearance.Hazard> hazards = FontHazardClearance.CollectUnder(level);
            bool sawGeyser = false;
            foreach (FontHazardClearance.Hazard hazard in hazards) {
                if (hazard.Label.Contains("level_01.steam_geyser")) sawGeyser = true;
            }
            AssertThat(sawGeyser)
                .OverrideFailureMessage("The scene's SteamGeyser was not collected, so the hazard check proves nothing.")
                .IsTrue();
            FontHazardClearance.Check("Florence", position, font.ChannelRangePixels, hazards, issues);
            if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    private const string FlorenceScenePath = "res://scenes/campaign/Level_01_Florence.tscn";
}
