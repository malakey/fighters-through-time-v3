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
            // GU-L0 (2026-10-13): never inside an encounter lock (its footprint ±30 px).
            foreach (EncounterLock encounterLock in level.EncounterLocks) {
                if (position.X + 30f > encounterLock.Area.Position.X && position.X - 30f < encounterLock.Area.End.X) {
                    issues.Add($"font x {position.X} stands inside lock '{encounterLock.LockID}'");
                }
            }
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

    // === GU-L0 (2026-10-13): Florence's encounter locks ===

    /// <summary>
    /// Florence is four standalone encounter locks, one wave per manifest row —
    /// the streets and the piazza (the entry room's row split in two, pre-placed
    /// and dormant), the print shop (spawns on engage, needs the gear puzzle, its
    /// exit barrier filling the workshop door under its column) and the workshop
    /// gauntlet (spawns on engage). The west wall closes x 0; the entry anchor
    /// stands at the entrance and the Middle anchor past the workshop door,
    /// outside every lock and out of swing reach from the locks before them; the
    /// baselines reach StoryManager, so the fractures past a lock are held.
    /// </summary>
    [TestCase]
    public void FlorenceIsFourEncounterLocksOneWavePerManifestRow() {
        using var fixture = new FlorenceFixture();
        Level01Controller level = fixture.Level;
        AssertThat(level.EncounterLocks.Count).IsEqual(4);
        EncounterLock streets = level.StreetsLock;
        EncounterLock piazza = level.PiazzaLock;
        EncounterLock printShop = level.PrintShopLock;
        EncounterLock workshop = level.WorkshopLock;
        AssertString(streets.LockID).IsEqual(Level01Controller.LockStreets);
        AssertString(piazza.LockID).IsEqual(Level01Controller.LockPiazza);
        AssertString(printShop.LockID).IsEqual(Level01Controller.LockPrintShop);
        AssertString(workshop.LockID).IsEqual(Level01Controller.LockWorkshop);
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            AssertThat(encounterLock.LockID.StartsWith("level_01_florence", System.StringComparison.Ordinal)).IsTrue();
        }

        AssertThat(streets.Area.Position.X).IsEqual(720f);
        AssertThat(streets.Area.End.X).IsEqual(2330f);
        AssertThat(piazza.Area.Position.X).IsEqual(2350f);
        AssertThat(piazza.Area.End.X).IsEqual(4430f);
        AssertThat(printShop.Area.Position.X).IsEqual(5800f);
        AssertThat(printShop.Area.End.X).IsEqual(8630f);
        AssertThat(workshop.Area.Position.X).IsEqual(8900f);
        AssertThat(workshop.Area.End.X).IsEqual(10460f);

        AssertString(streets.Spec.Waves[0].RowID).IsEqual(Level01Controller.Room1aRowID);
        AssertString(piazza.Spec.Waves[0].RowID).IsEqual(Level01Controller.Room1bRowID);
        AssertString(printShop.Spec.Waves[0].RowID).IsEqual(Level01Controller.Room2RowID);
        AssertString(workshop.Spec.Waves[0].RowID).IsEqual(Level01Controller.Room3RowID);
        AssertThat(streets.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnBuild);
        AssertThat(piazza.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnBuild);
        AssertThat(printShop.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnEngage);
        AssertThat(workshop.Spec.Waves[0].Trigger).IsEqual(EncounterWaveTrigger.OnEngage);
        AssertThat(printShop.Spec.Waves[0].AuthoredCount).IsEqual(3);
        AssertThat(printShop.Spec.Waves[0].Spawns.Length).IsEqual(4);
        AssertThat(workshop.Spec.Waves[0].Spawns.Length).IsEqual(7);
        // GR-LV1: the workshop's swarm row, the 7d showcase.
        AssertThat(workshop.Spec.Waves.Length).IsEqual(2);
        AssertString(workshop.Spec.Waves[1].RowID).IsEqual(Level01Controller.SwarmRowID);
        AssertThat(workshop.Spec.Waves[1].Trigger).IsEqual(EncounterWaveTrigger.WhenAliveAtMost);
        AssertThat(workshop.Spec.Waves[1].AliveAtMost).IsEqual(SwarmWaveRules.TriggerAliveAtMost);

        // Armed at load: room 1 stands dormant, rooms 2 and 3 wait for the hero.
        foreach (EncounterLock encounterLock in level.EncounterLocks) {
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
            AssertThat(encounterLock.ExitBarrier.IsClosed).IsTrue();
            AssertThat(encounterLock.EntryBarrier.IsClosed).IsFalse();
        }
        AssertThat(streets.LivingMemberCount).IsEqual(streets.PlannedTotal);
        AssertThat(piazza.LivingMemberCount).IsEqual(piazza.PlannedTotal);
        AssertThat(streets.PlannedTotal + piazza.PlannedTotal)
            .OverrideFailureMessage("The entry room's split rows must still spawn its five on Normal.")
            .IsEqual(StoryDifficultyTuning.ScaleEncounterCount(5, StoryDifficultyTuning.CurrentStoryDifficulty));
        foreach (FTT.Enemies.EnemyController member in streets.LivingMembers) AssertThat(member.IsEncounterDormant).IsTrue();
        AssertThat(printShop.LivingMemberCount).IsEqual(0);
        AssertThat(workshop.LivingMemberCount).IsEqual(0);

        // The print shop needs the gears, and its exit fills the workshop door below its column.
        AssertThat(string.Join(",", printShop.Spec.RequiredPuzzleIDs)).IsEqual(Level01Controller.GearPuzzleID);
        AssertThat(printShop.ExitBarrier.CenterX).IsEqual(8640f);
        AssertThat(printShop.ExitBarrier.TopY).IsEqual(500f);
        AssertThat(printShop.ExitBarrier.BottomY).IsEqual(900f);
        CollisionShape2D columnShape = FirstShape(level.WorkshopDoorColumn);
        float columnFoot = columnShape.GlobalPosition.Y + ((RectangleShape2D)columnShape.Shape).Size.Y / 2f;
        AssertThat(columnFoot)
            .OverrideFailureMessage("The barrier must start at the door column's foot, never beside the column.")
            .IsEqual(printShop.ExitBarrier.TopY);

        // The west wall: x -20..0, the full height.
        var wall = level.GetNodeOrNull<StaticBody2D>("Wall_-20");
        AssertObject(wall).OverrideFailureMessage("Florence has no west wall at x 0.").IsNotNull();
        CollisionShape2D wallShape = FirstShape(wall);
        Vector2 wallSize = ((RectangleShape2D)wallShape.Shape).Size;
        AssertThat(wallShape.GlobalPosition.X + wallSize.X / 2f).IsEqual(0f);
        AssertThat(wallSize.Y).IsEqual(1080f);

        // The anchors: outside every lock; past ones held until their locks clear.
        var checkpoints = new Dictionary<string, CheckpointTrigger>();
        foreach (Node child in Children(level)) {
            if (child is CheckpointTrigger checkpoint) checkpoints[checkpoint.CheckpointID] = checkpoint;
        }
        AssertThat(checkpoints[Level01Controller.EntryCheckpointID].GlobalPosition.X).IsEqual(Level01Controller.EntryCheckpointX);
        AssertThat(checkpoints[Level01Controller.MiddleCheckpointID].GlobalPosition.X).IsEqual(Level01Controller.MiddleCheckpointX);
        foreach (CheckpointTrigger checkpoint in checkpoints.Values) {
            foreach (EncounterLock encounterLock in level.EncounterLocks) {
                bool inside = checkpoint.GlobalPosition.X + 30f > encounterLock.Area.Position.X
                    && checkpoint.GlobalPosition.X - 30f < encounterLock.Area.End.X;
                AssertThat(inside)
                    .OverrideFailureMessage($"{checkpoint.CheckpointID} stands inside '{encounterLock.LockID}'.").IsFalse();
            }
            string[] baseline = Level01Controller.EncounterBaselines[checkpoint.CheckpointID];
            AssertThat(string.Join(",", StoryManager.Instance.GetEncounterBaseline(checkpoint.CheckpointID)))
                .IsEqual(string.Join(",", baseline));
            AssertThat(checkpoint.IsHeldByEncounterLock).IsEqual(baseline.Length > 0);
        }
        AssertThat(Level01Controller.EncounterBaselines[Level01Controller.MiddleCheckpointID].Length).IsEqual(3);
        AssertThat(Level01Controller.EncounterBaselines[Level01Controller.BossRoomCheckpointID].Length).IsEqual(4);

        // Out of the roster's longest basic swing from a hero pressed against the
        // locks' closed exits.
        float reach = LongestBasicSwingPixels();
        float printShopPivot = printShop.ExitBarrier.Footprint.Position.X - 20f;
        AssertThat(printShopPivot + reach < Level01Controller.MiddleCheckpointX - 30f)
            .OverrideFailureMessage($"A swing from the print shop reaches x {printShopPivot + reach:0}.").IsTrue();
        float workshopPivot = workshop.ExitBarrier.Footprint.Position.X - 20f;
        float bossRoomX = checkpoints[Level01Controller.BossRoomCheckpointID].GlobalPosition.X;
        AssertThat(workshopPivot + reach < bossRoomX - 30f)
            .OverrideFailureMessage($"A swing from the workshop reaches x {workshopPivot + reach:0}.").IsTrue();
    }

    /// <summary>
    /// In real physics frames on the real scene: the print shop's guards come in
    /// with the hero, and its barrier holds — the Middle anchor with it — until
    /// they are down AND the gear train is aligned; the gears open the door, the
    /// clear opens the barriers.
    /// </summary>
    [TestCase]
    public async System.Threading.Tasks.Task ThePrintShopHoldsUntilItsGuardsAreDownAndTheGearsAligned() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        using var fixture = new FlorenceFixture();
        Level01Controller level = fixture.Level;
        FTT.Characters.PlayerController player = level.GetNode<FTT.Characters.PlayerController>("Player");
        for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        FTT.UI.DialogueManager dialogue = FTT.UI.DialogueManager.Instance;
        for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
        AssertThat(tree.Paused).IsFalse();

        // The streets and the piazza are behind the hero (cleared, as a run reaching
        // the print shop has them).
        level.StreetsLock.RestoreCleared();
        level.PiazzaLock.RestoreCleared();
        CheckpointTrigger middle = level.GetNode<CheckpointTrigger>($"Checkpoint_{Level01Controller.MiddleCheckpointID}");

        EncounterLock printShop = level.PrintShopLock;
        player.GlobalPosition = new Vector2(6200f, 850f);
        player.Velocity = Vector2.Zero;
        for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(printShop.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(printShop.EntryBarrier.IsClosed).IsTrue();
        AssertThat(printShop.LivingMemberCount).IsEqual(printShop.PlannedTotal);
        AssertThat(printShop.LivingMemberCount).IsGreater(0);
        AssertThat(middle.IsHeldByEncounterLock).IsTrue();

        foreach (FTT.Enemies.EnemyController member in printShop.LivingMembers) member.TakeDamage(99999);
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertThat(printShop.State)
            .OverrideFailureMessage("The print shop must hold while the gears are still misaligned.")
            .IsEqual(EncounterLockState.Engaged);
        AssertThat(middle.IsHeldByEncounterLock).IsTrue();

        var gears = level.GetNode<PuzzleManager>("GearPuzzle");
        gears.SetCondition("gear_a_aligned", true);
        gears.SetCondition("gear_b_aligned", true);
        AssertThat(gears.IsCompleted).IsTrue();
        for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        AssertObject(level.GetNodeOrNull<Node>("WorkshopDoor")).OverrideFailureMessage("The gears must open the door.").IsNull();
        AssertThat(printShop.State).IsEqual(EncounterLockState.Cleared);
        AssertThat(printShop.EntryBarrier.IsClosed).IsFalse();
        AssertThat(printShop.ExitBarrier.IsClosed).IsFalse();
        AssertThat(middle.IsHeldByEncounterLock).IsFalse();
        // The workshop ahead is untouched by the teleport past the corridor.
        AssertThat(level.WorkshopLock.State).IsEqual(EncounterLockState.Armed);
    }

    // === Helpers ===

    /// <summary>
    /// The farthest a Story basic string hit reaches in front of the pivot over the
    /// whole roster (every AttackRange minor bought) — the FIX-E measure.
    /// </summary>
    private static float LongestBasicSwingPixels() {
        float longest = 0f;
        foreach (string id in CharacterRoster.IDs) {
            float minor = 0f;
            ResonanceGridData grid = ResonanceProgression.LoadGrid(id);
            foreach (ResonanceNodeData node in grid?.Nodes ?? System.Array.Empty<ResonanceNodeData>()) {
                if (node != null && node.StatModifierKey == ResonanceStatKeys.AttackRange) minor += node.StatModifierValue;
            }
            FTT.Combat.BasicStringProfile profile = FTT.Combat.BasicComboRules.StringProfileFor(id);
            for (int step = 0; step < FTT.Combat.BasicComboRules.StringBoxHalfWidthUnits.Length; step++) {
                FTT.Characters.PlayerController.ResolveGroundStringBoxPixels(step, profile, 1f + minor,
                    out Vector2 size, out Vector2 offset);
                longest = Mathf.Max(longest, offset.X + size.X * 0.5f);
            }
        }
        return longest;
    }

    private static CollisionShape2D FirstShape(Node body) {
        foreach (Node child in Children(body)) {
            if (child is CollisionShape2D shape) return shape;
        }
        AssertThat(false).OverrideFailureMessage($"'{body.Name}' has no collision shape.").IsTrue();
        return null;
    }

    private static List<Node> Children(Node parent) {
        var collected = new List<Node>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) collected.Add(child);
        return collected;
    }

    /// <summary>
    /// The real Florence scene with no save slot, handing back the session's slot,
    /// the pause and every pooled body it spawned.
    /// </summary>
    private sealed class FlorenceFixture : System.IDisposable {
        public readonly Level01Controller Level;
        private readonly int _originalSlot;
        private readonly bool _originalPaused;

        public FlorenceFixture() {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            _originalPaused = tree.Paused;
            _originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            Level = ResourceLoader.Load<PackedScene>(FlorenceScenePath).Instantiate<Level01Controller>();
            Level.Name = "FlorenceLockFixture";
            tree.Root.AddChild(Level);
        }

        public void Dispose() {
            PoolManager.Instance?.ReleaseActiveUnder(Level);
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = _originalSlot;
            ((SceneTree)Engine.GetMainLoop()).Paused = _originalPaused;
        }
    }

    private const string FlorenceScenePath = "res://scenes/campaign/Level_01_Florence.tscn";
}
