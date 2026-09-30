using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W8: the two base-owned content hooks every boss level carries,
/// checked against each level as it is really built — the authored scene
/// entered into the tree once, so the code-built geometry exists — for
/// Levels 2–15 (Package 13 W2: S27 retired the nine Level 4A variants this
/// suite also swept). Each scene is instantiated ONCE per session: both cases
/// read the same captured snapshot.
///
/// <para><b>GAP-03 — the F05 secret cache.</b> Exactly one
/// <see cref="SecretCache"/>, keyed to the manifest's <c>SecretSourceID</c>,
/// standing on a surface (an Environment-layer StaticBody2D wider than it is
/// tall, or a OneWayPlatform) that spans its X with the top in
/// [cache.y, cache.y + 160] — a player standing there is inside the 200×200
/// trigger. That surface must be reachable: a breadth-first walk from the
/// level's <c>Floor_*</c> surfaces, where one surface leads to another when the
/// horizontal gap is at most <see cref="MaxHorizontalGap"/> and the target's top
/// is no more than one double jump of the WORST roster character above the
/// source (any drop is allowed). Deliberately coarse — it ignores walls and
/// hazards, so it catches a cache stranded in the sky, not a subtle route
/// problem. The cache also keeps 300 px from every checkpoint and the spawn and
/// stays outside every boss (and Mirror) reveal radius.</para>
///
/// <para><b>GAP-05 — the N01 sealing anchor.</b> Levels 2–14 carry the generic <see cref="TemporalCoreAnchor"/>: dormant at load,
/// ID <c>{DialoguePrefix}.sealing_anchor</c>, over a standable surface within
/// 200 px below it (its interaction area is 220×320 centred 40 px above it),
/// not buried in a solid body, and within 700 px of where the boss (or the
/// Mirror) stands. Level 15 uses its scene Prime Anchor instead.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SecretCachePlacementTests {

    private const float MinimumClearance = 300f;
    private const float StandBand = 160f;
    private const float AnchorStandBand = 200f;
    private const float AnchorBossReach = 700f;
    private const float MaxHorizontalGap = 250f;

    private static List<LevelSnapshot> _snapshots;

    [TestCase]
    public void EverySecretLevelAuthorsOneReachableOffPathCacheKeyedToItsManifest() {
        var issues = new List<string>();
        float doubleRise = 2f * WorstSingleJumpRise();
        List<LevelSnapshot> levels = Snapshots();
        // Levels 2–15 (S27 retired the per-hero Level 4A variants).
        AssertThat(levels.Count).IsEqual(14);
        foreach (LevelSnapshot level in levels) CheckSecret(level, doubleRise, issues);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryBossLevelPlacesItsSealingAnchorBesideTheBossOnSolidGround() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) CheckSealingAnchor(level, issues);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    // === The two contracts ===

    private static void CheckSecret(LevelSnapshot level, float doubleRise, List<string> issues) {
        string label = level.Label;
        if (level.LoadError != null) { issues.Add($"{label}: {level.LoadError}"); return; }
        if (level.Caches.Count != 1) { issues.Add($"{label}: {level.Caches.Count} secret caches (expected 1)"); return; }
        (string secretID, Vector2 position) = level.Caches[0];

        if (level.ManifestSecretID == null) issues.Add($"{label}: no reward manifest");
        else if (secretID != level.ManifestSecretID) {
            issues.Add($"{label}: cache '{secretID}' != manifest secret '{level.ManifestSecretID}'");
        }

        HashSet<Surface> reachable = Reachable(level.Surfaces, doubleRise);
        Surface standing = null;
        foreach (Surface surface in level.Surfaces) {
            if (position.X < surface.Left || position.X > surface.Right) continue;
            if (surface.Top < position.Y || surface.Top > position.Y + StandBand) continue;
            if (standing == null || reachable.Contains(surface)) standing = surface;
        }
        if (standing == null) issues.Add($"{label}: no standable surface under the cache at {position}");
        else if (!reachable.Contains(standing)) {
            issues.Add($"{label}: the cache's surface '{standing.Name}' (top {standing.Top}) is not reachable from the floor");
        }

        if (position.DistanceTo(level.Spawn) < MinimumClearance) {
            issues.Add($"{label}: cache {position} is within {MinimumClearance} px of the spawn");
        }
        foreach ((string checkpointID, Vector2 checkpoint) in level.Checkpoints) {
            if (position.DistanceTo(checkpoint) < MinimumClearance) {
                issues.Add($"{label}: cache {position} is within {MinimumClearance} px of {checkpointID}");
            }
        }
        if (level.Encounters.Count == 0) issues.Add($"{label}: no boss encounter found to fence the arena");
        foreach (Encounter encounter in level.Encounters) {
            if (position.DistanceTo(encounter.Position) <= encounter.Reveal) {
                issues.Add($"{label}: cache {position} is inside the boss reveal radius ({encounter.Reveal}) of {encounter.Position}");
            }
        }
    }

    private static void CheckSealingAnchor(LevelSnapshot level, List<string> issues) {
        string label = level.Label;
        if (level.LoadError != null) { issues.Add($"{label}: {level.LoadError}"); return; }
        if (level.Index == (int)CampaignLevel.Alexandria) {
            // Level 15 seals at its scene-authored Prime Anchor, not a generic one.
            if (level.HasSealingAnchor) issues.Add($"{label}: builds a generic sealing anchor");
            if (level.SealingAnchorID != Level15Controller.PrimeAnchorID) {
                issues.Add($"{label}: SealingAnchorID '{level.SealingAnchorID}' is not the Prime Anchor");
            }
            return;
        }
        if (!level.HasSealingAnchor) { issues.Add($"{label}: no sealing anchor"); return; }
        Vector2 anchor = level.AnchorPosition;
        if (level.AnchorArmed) issues.Add($"{label}: the sealing anchor is armed at load");
        string expectedID = $"{level.DialoguePrefix}.sealing_anchor";
        if (level.AnchorID != expectedID) issues.Add($"{label}: anchor ID '{level.AnchorID}' != '{expectedID}'");

        bool standing = false;
        foreach (Surface surface in level.Surfaces) {
            if (anchor.X < surface.Left || anchor.X > surface.Right) continue;
            if (surface.Top >= anchor.Y && surface.Top <= anchor.Y + AnchorStandBand) { standing = true; break; }
        }
        if (!standing) issues.Add($"{label}: no standable surface within {AnchorStandBand} px under the anchor at {anchor}");
        foreach (Rect2 solid in level.Solids) {
            if (anchor.X > solid.Position.X && anchor.X < solid.End.X
                && anchor.Y > solid.Position.Y && anchor.Y < solid.End.Y) {
                issues.Add($"{label}: the anchor at {anchor} is inside a solid body {solid}");
            }
        }
        if (level.Encounters.Count == 0) { issues.Add($"{label}: no boss encounter to measure the anchor against"); return; }
        float nearest = float.MaxValue;
        foreach (Encounter encounter in level.Encounters) nearest = Mathf.Min(nearest, anchor.DistanceTo(encounter.BossSpawn));
        if (nearest > AnchorBossReach) {
            issues.Add($"{label}: the anchor at {anchor} is {nearest:0} px from the boss spawn (max {AnchorBossReach})");
        }
    }

    // === The single capture pass ===

    private sealed class Surface {
        public string Name;
        public float Left, Right, Top;
        public bool IsFloor;
    }

    private readonly record struct Encounter(Vector2 Position, float Reveal, Vector2 BossSpawn);

    private sealed class LevelSnapshot {
        public string Label;
        public int Index;
        public string LoadError;
        public Vector2 Spawn;
        public string DialoguePrefix;
        public string ManifestSecretID;
        public readonly List<(string ID, Vector2 Position)> Caches = new();
        public readonly List<(string ID, Vector2 Position)> Checkpoints = new();
        public readonly List<Encounter> Encounters = new();
        public readonly List<Surface> Surfaces = new();
        public readonly List<Rect2> Solids = new();
        public bool HasSealingAnchor;
        public Vector2 AnchorPosition;
        public bool AnchorArmed;
        public string AnchorID;
        public string SealingAnchorID;
    }

    private static List<LevelSnapshot> Snapshots() {
        if (_snapshots != null) return _snapshots;
        var snapshots = new List<LevelSnapshot>();
        foreach ((string label, string scenePath, int index, string hero) in SecretLevels()) {
            snapshots.Add(Capture(label, scenePath, index, hero));
        }
        _snapshots = snapshots;
        return snapshots;
    }

    private static IEnumerable<(string Label, string ScenePath, int Index, string Hero)> SecretLevels() {
        for (int level = 2; level <= 15; level++) {
            yield return ($"Level{level:00}", StoryManager.GetLevelScenePath((CampaignLevel)level), level, "");
        }
    }

    /// <summary>
    /// Builds one authored scene in the runner tree, copies everything both
    /// cases need into plain values, and hands every shared singleton back:
    /// session slot (-1, so nothing touches a real save), character (the 4A
    /// hero), difficulty, pooled enemies, and the pause flag.
    /// </summary>
    private static LevelSnapshot Capture(string label, string scenePath, int index, string hero) {
        var snapshot = new LevelSnapshot { Label = label, Index = index };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        StoryLevelControllerBase level = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = string.IsNullOrEmpty(hero) ? "einstein" : hero;
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;

            snapshot.ManifestSecretID = LevelRewardManifest.LoadFor(index)?.SecretSourceID;
            PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null) { snapshot.LoadError = $"scene '{scenePath}' did not load"; return snapshot; }
            level = scene.Instantiate<StoryLevelControllerBase>();
            level.Name = $"{label}_PlacementTest";
            tree.Root.AddChild(level);

            snapshot.Spawn = level.PlayerSpawnPosition;
            snapshot.DialoguePrefix = level.DialoguePrefix;
            snapshot.SealingAnchorID = level.SealingAnchorID;
            TemporalCoreAnchor anchor = level.SealingAnchor;
            if (anchor != null && GodotObject.IsInstanceValid(anchor)) {
                snapshot.HasSealingAnchor = true;
                snapshot.AnchorPosition = anchor.GlobalPosition;
                snapshot.AnchorArmed = anchor.IsArmed;
                snapshot.AnchorID = anchor.AnchorID;
            }
            Collect(level, snapshot);
        } catch (Exception exception) {
            snapshot.LoadError = $"capture threw {exception.GetType().Name}: {exception.Message}";
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            tree.Paused = originalPaused;
        }
        return snapshot;
    }

    private static void Collect(Node node, LevelSnapshot snapshot) {
        switch (node) {
            case SecretCache cache: snapshot.Caches.Add((cache.SecretID, cache.GlobalPosition)); break;
            case CheckpointTrigger checkpoint: snapshot.Checkpoints.Add((checkpoint.CheckpointID, checkpoint.GlobalPosition)); break;
            case BossEncounterController boss:
                snapshot.Encounters.Add(new Encounter(boss.GlobalPosition, boss.RevealDistance, boss.GlobalPosition + boss.SpawnOffset));
                break;
            case MirrorParadoxEncounterController mirror:
                snapshot.Encounters.Add(new Encounter(mirror.GlobalPosition, mirror.RevealDistance, mirror.GlobalPosition));
                break;
        }
        bool oneWay = node is OneWayPlatform;
        bool solid = !oneWay && node is StaticBody2D body && (body.CollisionLayer & CollisionLayers.Environment) != 0;
        if (oneWay || solid) {
            Godot.Collections.Array<Node> shapes = node.GetChildren();
            using var shapesLifetime = shapes.AsDisposable();
            foreach (Node child in shapes) {
                if (child is not CollisionShape2D { Disabled: false, Shape: RectangleShape2D rect } shape) continue;
                Vector2 size = rect.Size * shape.GlobalScale.Abs();
                Vector2 centre = shape.GlobalPosition;
                if (solid) snapshot.Solids.Add(new Rect2(centre - size / 2f, size));
                if (size.X < size.Y) continue; // a wall, not a floor
                snapshot.Surfaces.Add(new Surface {
                    Name = node.Name,
                    Left = centre.X - size.X / 2f,
                    Right = centre.X + size.X / 2f,
                    Top = centre.Y - size.Y / 2f,
                    IsFloor = solid && node.Name.ToString().StartsWith("Floor_", StringComparison.Ordinal)
                });
            }
        }
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, snapshot);
    }

    /// <summary>
    /// Breadth-first from every <c>Floor_*</c> surface: a surface leads to another
    /// within <see cref="MaxHorizontalGap"/> whose top is at most one worst-case
    /// double jump higher (any drop is allowed).
    /// </summary>
    private static HashSet<Surface> Reachable(List<Surface> surfaces, float doubleRise) {
        var reached = new HashSet<Surface>();
        var queue = new Queue<Surface>();
        foreach (Surface surface in surfaces) {
            if (surface.IsFloor && reached.Add(surface)) queue.Enqueue(surface);
        }
        while (queue.Count > 0) {
            Surface from = queue.Dequeue();
            foreach (Surface to in surfaces) {
                if (reached.Contains(to)) continue;
                float gap = Mathf.Max(0f, Mathf.Max(to.Left - from.Right, from.Left - to.Right));
                if (gap > MaxHorizontalGap) continue;
                if (from.Top - to.Top > doubleRise) continue;
                reached.Add(to);
                queue.Enqueue(to);
            }
        }
        return reached;
    }

    /// <summary>
    /// The Level 13 closed form (PlayerController's authored physics at gravity
    /// scale 1): launch = MaxJumpForce × 54 px/s, gravity =
    /// 18 × (0.8 + 0.4 × Weight) × 60 px/s², rise = v² / 2a — minimised over the
    /// manifest roster so the heaviest, lowest jumper sets the bar.
    /// </summary>
    private static float WorstSingleJumpRise() {
        float worst = float.MaxValue;
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            float launch = data.MaxJumpForce * 54f;
            float acceleration = 18f * (0.8f + 0.4f * data.Weight);
            acceleration *= 60f;
            worst = Mathf.Min(worst, launch * launch / (2f * acceleration));
        }
        return worst;
    }
}
