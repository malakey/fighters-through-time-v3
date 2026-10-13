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
/// Package 12 W8: the base-owned content hooks every boss level carries (three
/// since D13, 2026-10-11, added the Restoration Font),
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
/// stays out of every boss fight: outside the arena rect of an arena encounter
/// (the arena is its reveal), outside the reveal radius of a radius encounter,
/// and outside both the Mirror's reveal radius and its arena.</para>
///
/// <para><b>GAP-05 — the N01 sealing anchor.</b> Levels 2–14 carry the generic <see cref="TemporalCoreAnchor"/>: dormant at load,
/// ID <c>{DialoguePrefix}.sealing_anchor</c>, over a standable surface within
/// 200 px below it (its interaction area is 220×320 centred 40 px above it),
/// not buried in a solid body, and within 700 px of where the boss (or the
/// Mirror) stands. Level 15 uses its scene Prime Anchor instead.</para>
///
/// <para><b>D13 — the Restoration Font</b> (<c>PROVISIONAL-FONT-PLACEMENT</c>).
/// Exactly one <see cref="RestorationFont"/> per level, ID
/// <c>{DialoguePrefix}.restoration_font</c>, standing on a surface whose top is in
/// [y - 2, y + 16] under it; at least 110 px past the Middle-role checkpoint;
/// its whole channel circle (range + 30 px) outside the boss reveal (west of
/// the arena rect, or beyond the reveal radius for the Mirror); 110 px from
/// every checkpoint, 320 px from every Extractor or Resonance Hold node, and
/// 200 px from every other Interact target (Warden Beacons, the secret cache,
/// sealing anchors, Puzzle Reset Stations). The hero's body anywhere in the
/// channel circle stays 20 px clear of every damaging hazard's shape — cyclic
/// hazards armed or dormant at load (a phase-armed laser lane included), rifts
/// and spike strips (<see cref="FontHazardClearance"/>). It carries its
/// interaction area, glow and channel radial, and its completion refusal is
/// wired and false at load. The low-gravity, escape, flood, searchlight and
/// spawn-proximity preferences are authoring guidance, not encoded here.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SecretCachePlacementTests {

    private const float MinimumClearance = 300f;
    private const float StandBand = 160f;
    private const float AnchorStandBand = 200f;
    private const float AnchorBossReach = 700f;
    private const float MaxHorizontalGap = 250f;

    // D13 font placement rule (PROVISIONAL-FONT-PLACEMENT).
    private const float FontStandAbove = 2f;
    private const float FontStandBelow = 16f;
    private const float FontMiddleClearance = 110f;
    private const float FontArenaMargin = 30f;
    private const float FontCheckpointClearance = 110f;
    private const float FontExtractorClearance = 320f;
    private const float FontInteractClearance = 200f;

    private static List<LevelSnapshot> _snapshots;

    [TestCase]
    public void EverySecretLevelAuthorsOneReachableOffPathCacheKeyedToItsManifest() {
        var issues = new List<string>();
        float doubleRise = WorstJumpBudgetRise();
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

    [TestCase]
    public void EveryBossLevelPlacesOneRestorationFontBetweenItsMiddleCheckpointAndTheArena() {
        var issues = new List<string>();
        List<LevelSnapshot> levels = Snapshots();
        AssertThat(levels.Count).IsEqual(14);
        foreach (LevelSnapshot level in levels) CheckRestorationFont(level, issues);
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
        foreach ((string checkpointID, Vector2 checkpoint, CheckpointRole _) in level.Checkpoints) {
            if (position.DistanceTo(checkpoint) < MinimumClearance) {
                issues.Add($"{label}: cache {position} is within {MinimumClearance} px of {checkpointID}");
            }
        }
        if (level.Encounters.Count == 0) issues.Add($"{label}: no boss encounter found to fence the arena");
        foreach (Encounter encounter in level.Encounters) {
            // The fight's own zone: the arena rect where one is authored (it is
            // the reveal and the leash — the encounter ignores its radius then,
            // A4), and the reveal radius where that is what reveals the fight
            // (a radius encounter, and the Mirror, whose arena is its leash).
            if (encounter.HasArena && encounter.Arena.HasPoint(position)) {
                issues.Add($"{label}: cache {position} is inside the boss arena {encounter.Arena}");
            }
            if (encounter.Reveal > 0f && position.DistanceTo(encounter.Position) <= encounter.Reveal) {
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

    private static void CheckRestorationFont(LevelSnapshot level, List<string> issues) {
        string label = level.Label;
        if (level.LoadError != null) { issues.Add($"{label}: {level.LoadError}"); return; }
        if (level.Fonts.Count != 1) { issues.Add($"{label}: {level.Fonts.Count} Restoration Fonts (expected 1)"); return; }
        FontSnapshot font = level.Fonts[0];
        Vector2 position = font.Position;
        string expectedID = $"{level.DialoguePrefix}.restoration_font";
        if (font.ID != expectedID) issues.Add($"{label}: font ID '{font.ID}' != '{expectedID}'");

        // Wiring.
        if (!font.HasInteraction) issues.Add($"{label}: the font has no InteractionArea");
        if (!font.HasGlow) issues.Add($"{label}: the font has no Glow");
        if (!font.HasRadial) issues.Add($"{label}: the font has no ChannelRadial");
        if (!font.HasRefusal) issues.Add($"{label}: the font's completion refusal is not wired");
        else if (font.RefusedAtLoad) issues.Add($"{label}: the font refuses at load");

        // Standing on a surface.
        bool standing = false;
        foreach (Surface surface in level.Surfaces) {
            if (position.X < surface.Left || position.X > surface.Right) continue;
            if (surface.Top >= position.Y - FontStandAbove && surface.Top <= position.Y + FontStandBelow) {
                standing = true;
                break;
            }
        }
        if (!standing) issues.Add($"{label}: no surface top within [{position.Y - FontStandAbove}, {position.Y + FontStandBelow}] under the font at {position}");

        // Past the Middle anchor.
        bool hasMiddle = false;
        foreach ((string checkpointID, Vector2 checkpoint, CheckpointRole role) in level.Checkpoints) {
            if (role != CheckpointRole.Middle) continue;
            hasMiddle = true;
            if (position.X < checkpoint.X + FontMiddleClearance) {
                issues.Add($"{label}: font x {position.X} is not {FontMiddleClearance} px past the Middle checkpoint {checkpointID} ({checkpoint.X})");
            }
        }
        if (!hasMiddle) issues.Add($"{label}: no Middle-role checkpoint to measure the font against");

        // The whole channel circle stays outside the boss reveal.
        float arenaClearance = font.ChannelRange + FontArenaMargin;
        if (level.Encounters.Count == 0) issues.Add($"{label}: no boss encounter to fence the font");
        foreach (Encounter encounter in level.Encounters) {
            if (encounter.HasArena && position.X > encounter.Arena.Position.X - arenaClearance) {
                issues.Add($"{label}: font x {position.X} is within {arenaClearance} px of the arena's west edge ({encounter.Arena.Position.X})");
            }
            if (encounter.Reveal > 0f && position.DistanceTo(encounter.Position) < encounter.Reveal + arenaClearance) {
                issues.Add($"{label}: font {position} is within the reveal radius ({encounter.Reveal}) + {arenaClearance} of {encounter.Position}");
            }
        }

        // Clearances.
        foreach ((string checkpointID, Vector2 checkpoint, CheckpointRole _) in level.Checkpoints) {
            if (position.DistanceTo(checkpoint) < FontCheckpointClearance) {
                issues.Add($"{label}: font {position} is within {FontCheckpointClearance} px of {checkpointID} {checkpoint}");
            }
        }
        foreach ((string extractorID, Vector2 extractor) in level.Extractors) {
            if (position.DistanceTo(extractor) < FontExtractorClearance) {
                issues.Add($"{label}: font {position} is within {FontExtractorClearance} px of the Extractor/hold node {extractorID} {extractor}");
            }
        }
        foreach ((string targetID, Vector2 target) in level.InteractTargets) {
            if (position.DistanceTo(target) < FontInteractClearance) {
                issues.Add($"{label}: font {position} is within {FontInteractClearance} px of the Interact target {targetID} {target}");
            }
        }

        // No damaging hazard reaches a hero channelling anywhere in the circle.
        FontHazardClearance.Check(label, position, font.ChannelRange, level.Hazards, issues);
    }

    // === The single capture pass ===

    private sealed class Surface {
        public string Name;
        public float Left, Right, Top;
        public bool IsFloor;
    }

    /// <summary>
    /// One boss (or Mirror) encounter. <see cref="Reveal"/> is the radius that
    /// reveals it (0 when an arena rect does instead); <see cref="Arena"/> is its
    /// arena rect, default when it has none.
    /// </summary>
    private readonly record struct Encounter(Vector2 Position, float Reveal, Vector2 BossSpawn, Rect2 Arena) {
        public bool HasArena => Arena.Size.X > 0f && Arena.Size.Y > 0f;
    }

    private sealed class FontSnapshot {
        public string ID;
        public Vector2 Position;
        public float ChannelRange;
        public bool HasInteraction, HasGlow, HasRadial, HasRefusal, RefusedAtLoad;
    }

    private sealed class LevelSnapshot {
        public string Label;
        public int Index;
        public string LoadError;
        public Vector2 Spawn;
        public string DialoguePrefix;
        public string ManifestSecretID;
        public readonly List<(string ID, Vector2 Position)> Caches = new();
        public readonly List<(string ID, Vector2 Position, CheckpointRole Role)> Checkpoints = new();
        public readonly List<FontSnapshot> Fonts = new();
        public readonly List<(string ID, Vector2 Position)> Extractors = new();
        /// <summary>Every other Interact target the font keeps clear of: Warden
        /// Beacons, secret caches, sealing anchors and Puzzle Reset Stations.</summary>
        public readonly List<(string ID, Vector2 Position)> InteractTargets = new();
        public readonly List<Encounter> Encounters = new();
        /// <summary>Every damaging hazard shape the font keeps clear of (D13).</summary>
        public readonly List<FontHazardClearance.Hazard> Hazards = new();
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
        FontHazardClearance.TryCollect(node, snapshot.Hazards);
        switch (node) {
            case SecretCache cache:
                snapshot.Caches.Add((cache.SecretID, cache.GlobalPosition));
                snapshot.InteractTargets.Add((cache.SecretID, cache.GlobalPosition));
                break;
            case CheckpointTrigger checkpoint:
                snapshot.Checkpoints.Add((checkpoint.CheckpointID, checkpoint.GlobalPosition, checkpoint.Role));
                break;
            case RestorationFont font:
                snapshot.Fonts.Add(new FontSnapshot {
                    ID = font.FontID,
                    Position = font.GlobalPosition,
                    ChannelRange = font.ChannelRangePixels,
                    HasInteraction = font.GetNodeOrNull<InteractionArea>("InteractionArea") != null,
                    HasGlow = font.GetNodeOrNull<Node2D>("Glow") != null,
                    HasRadial = font.ChannelRadial != null,
                    HasRefusal = font.RefusesWhile != null,
                    RefusedAtLoad = font.RefusesWhile?.Invoke() ?? false
                });
                break;
            case ChronalExtractor extractor:
                // Resonance Hold nodes are Extractors too.
                snapshot.Extractors.Add((extractor.ObjectID, extractor.GlobalPosition));
                break;
            case WardenBeacon beacon: snapshot.InteractTargets.Add((beacon.Name.ToString(), beacon.GlobalPosition)); break;
            case PuzzleResetStation station: snapshot.InteractTargets.Add((station.Name.ToString(), station.GlobalPosition)); break;
            case TemporalCoreAnchor sealingAnchor:
                snapshot.InteractTargets.Add((sealingAnchor.Name.ToString(), sealingAnchor.GlobalPosition));
                break;
            case BossEncounterController boss:
                // A4: an arena encounter reveals on its rect and its RevealDistance
                // is the unused export default — fence the rect, not that number.
                snapshot.Encounters.Add(new Encounter(boss.GlobalPosition,
                    boss.HasArena ? 0f : boss.RevealDistance,
                    boss.GlobalPosition + boss.SpawnOffset,
                    boss.HasArena ? boss.ArenaBounds : default));
                break;
            case MirrorParadoxEncounterController mirror:
                snapshot.Encounters.Add(new Encounter(mirror.GlobalPosition, mirror.RevealDistance, mirror.GlobalPosition,
                    mirror.ArenaBounds));
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
    /// The whole jump budget's rise at gravity scale 1 (<see cref="StoryAirPhysics.MultiJumpRise"/>:
    /// the controller's own per-tick step integrated, apex hang included, the second
    /// press at the first apex), minimised over the manifest roster so the heaviest,
    /// lowest jumper sets the bar. Until 2026-10-08 this was twice the continuous
    /// closed-form single rise (v² / 2a), which ran ~7% under the real arc.
    /// </summary>
    private static float WorstJumpBudgetRise() {
        float worst = float.MaxValue;
        foreach (string id in CharacterRoster.IDs) {
            var data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            worst = Mathf.Min(worst, StoryAirPhysics.MultiJumpRise(
                StoryAirPhysics.JumpLaunchSpeed(data.MaxJumpForce), data.Weight, Math.Max(1, data.MaxJumpCount)));
        }
        return worst;
    }
}
