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
/// Gameplay update 2026-10-12 (GU-E, user request 3, plan §2.3): the content
/// rules every encounter lock in every campaign scene (Levels 0–15) obeys. A
/// level with no locks passes trivially; the sweep asserts at least one lock
/// exists somewhere so it can never pass vacuously.
///
/// <list type="bullet">
/// <item>No checkpoint, Restoration Font or sealing anchor inside any area; a
///       Reset Puzzle station only inside a puzzle lock's.</item>
/// <item>No area overlaps a boss arena, and no member spawns inside one.</item>
/// <item>Every member spawn stands inside its area on a floor the slowest hero
///       reaches from the area's ground.</item>
/// <item>Every area is between one visible view and one authored room wide, and
///       no lock's barrier stands inside another lock's area.</item>
/// <item>Every lock wave is exactly its manifest row (ID, enemy IDs in order,
///       the <c>@n</c> prefix).</item>
/// <item>Every barrier is a static Environment wall with floor under it,
///       hugging its area's edge from outside — swept here with its shape enabled
///       or not, because the geometry sweeps of <c>ArenaGeometryContentTests</c>
///       only see enabled shapes.</item>
/// <item>Every lock ID is a stable encounter ID of its level, unique, and listed
///       in the baseline of every checkpoint after its area (and none before).</item>
/// <item>FIX-E (2026-10-13): every checkpoint past a lock refuses activation
///       while that lock holds, and no checkpoint respawns the hero inside a room
///       trigger whose camera leaves the hero off screen (a placement rule for
///       every campaign scene, locks or not).</item>
/// </list>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterLockContentTests {

    /// <summary>The 40 px hero's half-width, for prompt footprints.</summary>
    private const float HeroHalfWidthPixels = 20f;
    /// <summary>The hero body's height (CharacterFactory's 40 × 64 px body, feet on the origin).</summary>
    private const float HeroHeightPixels = 64f;
    /// <summary>A checkpoint's or font's footprint reaches this far either side of its x (CheckpointTrigger's 60 px box).</summary>
    private const float AnchorHalfWidthPixels = 30f;
    /// <summary>A member spawn's floor lies at most this far under its feet (spawns stand 50 px above the floor).</summary>
    private const float SpawnFloorBandPixels = 120f;
    /// <summary>A flyer's spawn has some floor within this far below it.</summary>
    private const float FlyerFloorBandPixels = 700f;

    private static List<LevelSnapshot> _snapshots;

    [TestCase]
    public void NoCheckpointFontOrSealingAnchorStandsInsideALockArea() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (LockSnapshot encounterLock in level.Locks) {
                foreach ((string id, Vector2 position) in level.Anchors) {
                    if (OverlapsX(encounterLock.Area, position.X, AnchorHalfWidthPixels)) {
                        issues.Add($"{level.Label}: {id} at {position} stands inside lock '{encounterLock.ID}' {Span(encounterLock.Area)}");
                    }
                }
                foreach ((string id, Vector2 position) in level.ResetStations) {
                    if (encounterLock.RequiredPuzzleIDs.Length == 0 && OverlapsX(encounterLock.Area, position.X, 0f)) {
                        issues.Add($"{level.Label}: the Reset Puzzle station {id} stands inside '{encounterLock.ID}', which is not a puzzle lock");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(TotalLocks()).OverrideFailureMessage("No level authors an encounter lock: the sweep is vacuous.").IsGreater(0);
    }

    [TestCase]
    public void NoLockAreaOverlapsABossArenaAndNoMemberSpawnsInOne() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (LockSnapshot encounterLock in level.Locks) {
                foreach (Rect2 arena in level.BossArenas) {
                    if (encounterLock.Area.Position.X < arena.End.X && arena.Position.X < encounterLock.Area.End.X) {
                        issues.Add($"{level.Label}: lock '{encounterLock.ID}' {Span(encounterLock.Area)} overlaps the boss arena {Span(arena)}");
                    }
                    foreach (SpawnSnapshot spawn in encounterLock.Spawns) {
                        if (arena.HasPoint(spawn.Position)) {
                            issues.Add($"{level.Label}: '{encounterLock.ID}' spawns {spawn.EnemyID} at {spawn.Position}, inside the boss arena");
                        }
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryMemberSpawnStandsInsideItsAreaOnAFloorTheSlowestHeroReaches() {
        float rise = SlowestDoubleJumpRise();
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (LockSnapshot encounterLock in level.Locks) {
                float ground = AreaGround(level, encounterLock.Area);
                foreach (SpawnSnapshot spawn in encounterLock.Spawns) {
                    string label = $"{level.Label}: '{encounterLock.ID}' {spawn.EnemyID} at {spawn.Position}";
                    if (!OverlapsX(encounterLock.Area, spawn.Position.X, 0f)) {
                        issues.Add($"{label} is outside its area {Span(encounterLock.Area)}");
                        continue;
                    }
                    float band = spawn.Flying ? FlyerFloorBandPixels : SpawnFloorBandPixels;
                    float? floor = FloorUnder(level, spawn.Position, band);
                    if (floor is not float top) {
                        issues.Add($"{label} has no floor within {band} px under it");
                        continue;
                    }
                    if (!spawn.Flying && !float.IsNaN(ground) && ground - top > rise) {
                        issues.Add($"{label} stands on a post {ground - top:0} px above the area's ground; "
                            + $"the slowest hero's double jump rises {rise:0}");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryAreaIsBetweenOneViewAndOneRoomWideAndNoBarrierStandsInsideAnotherLocksArea() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (LockSnapshot encounterLock in level.Locks) {
                if (!ids.Add(encounterLock.ID)) issues.Add($"{level.Label}: lock ID '{encounterLock.ID}' repeats");
                float width = encounterLock.Area.Size.X;
                if (width < EncounterLockRules.MinAreaWidthPixels - 0.5f) {
                    issues.Add($"{level.Label}: '{encounterLock.ID}' is {width:0} px wide, narrower than the {EncounterLockRules.MinAreaWidthPixels:0} px view");
                }
                if (width > EncounterLockRules.MaxAreaWidthPixels + 0.5f) {
                    issues.Add($"{level.Label}: '{encounterLock.ID}' is {width:0} px wide, more than {EncounterLockRules.MaxAreaWidthPixels:0} px");
                }
                if (encounterLock.Camera.Size.X < Mathf.Min(EncounterLockRules.ViewWorldWidth, level.Bounds.Size.X) - 0.5f
                    || encounterLock.Camera.Size.Y < Mathf.Min(EncounterLockRules.ViewWorldHeight, level.Bounds.Size.Y) - 0.5f) {
                    issues.Add($"{level.Label}: '{encounterLock.ID}' camera rect {encounterLock.Camera} is smaller than the view");
                }
                foreach (LockSnapshot other in level.Locks) {
                    if (other == encounterLock) continue;
                    foreach (BarrierSnapshot barrier in other.Barriers) {
                        if (barrier.Rect.Position.X < encounterLock.Area.End.X - 0.5f
                            && barrier.Rect.End.X > encounterLock.Area.Position.X + 0.5f) {
                            issues.Add($"{level.Label}: '{other.ID}''s barrier {Span(barrier.Rect)} stands inside '{encounterLock.ID}' {Span(encounterLock.Area)}");
                        }
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryLockWaveIsExactlyItsManifestRow() {
        var issues = new List<string>();
        int wavesChecked = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            if (level.Locks.Count == 0) continue;
            LevelRewardManifest manifest = AuthoredResources.Load<LevelRewardManifest>(LevelRewardManifest.PathFor(level.Index));
            if (manifest == null) { issues.Add($"{level.Label}: no reward manifest"); continue; }
            var rows = new Dictionary<string, (int Authored, string[] EnemyIDs)>(StringComparer.Ordinal);
            foreach ((string head, string[] enemyIDs) in manifest.ParseWaves()) {
                (string id, int authored) = SplitHead(head, enemyIDs.Length);
                rows[id] = (authored, enemyIDs);
            }
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (LockSnapshot encounterLock in level.Locks) {
                foreach (EncounterLockWave wave in encounterLock.Waves) {
                    wavesChecked++;
                    string label = $"{level.Label}: '{encounterLock.ID}' wave '{wave.RowID}'";
                    if (!claimed.Add(wave.RowID)) issues.Add($"{label} is claimed by more than one lock wave");
                    if (!rows.TryGetValue(wave.RowID, out (int Authored, string[] EnemyIDs) row)) {
                        issues.Add($"{label} names no RequiredWaves row of the manifest");
                        continue;
                    }
                    string[] ids = wave.EnemyIDs;
                    if (string.Join(",", ids) != string.Join(",", row.EnemyIDs)) {
                        issues.Add($"{label} spawns [{string.Join(",", ids)}] but the row lists [{string.Join(",", row.EnemyIDs)}]");
                    }
                    bool rowUnscaled = row.Authored == 0;
                    if (rowUnscaled == wave.Scaled) issues.Add($"{label}: Scaled={wave.Scaled} disagrees with the row's @{row.Authored}");
                    int waveAuthored = wave.AuthoredCount > 0 ? wave.AuthoredCount : ids.Length;
                    if (!rowUnscaled && waveAuthored != row.Authored) {
                        issues.Add($"{label} scales from {waveAuthored} but the row from {row.Authored}");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(wavesChecked).IsGreater(0);
    }

    [TestCase]
    public void EveryBarrierIsAStaticEnvironmentWallWithFloorUnderItHuggingItsAreaFromOutside() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (LockSnapshot encounterLock in level.Locks) {
                if (encounterLock.Barriers.Count == 0) issues.Add($"{level.Label}: '{encounterLock.ID}' has no barrier at all");
                foreach (BarrierSnapshot barrier in encounterLock.Barriers) {
                    string label = $"{level.Label}: '{encounterLock.ID}' {(barrier.IsEntry ? "entry" : "exit")} barrier {Span(barrier.Rect)}";
                    if (!barrier.IsStaticWall) issues.Add($"{label} is not a static Environment wall (StaticBody2D, not AnimatableBody2D)");
                    if (barrier.InDoorGroup) issues.Add($"{label} joined the gate door group");
                    if (barrier.Rect.Size.X >= HeroHalfWidthPixels * 2f) issues.Add($"{label} is as wide as a hero (it would be a standable ledge)");
                    if (FloorUnder(level, new Vector2(barrier.Rect.GetCenter().X, barrier.Rect.End.Y - 1f), 4f) == null) {
                        issues.Add($"{label} has no floor under it at y {barrier.Rect.End.Y}");
                    }
                    // Outside the area, its inner face on (or just off) the area's edge.
                    Rect2 area = encounterLock.Area;
                    float gap = barrier.Rect.End.X <= area.Position.X + 0.5f
                        ? area.Position.X - barrier.Rect.End.X
                        : barrier.Rect.Position.X >= area.End.X - 0.5f
                            ? barrier.Rect.Position.X - area.End.X
                            : float.NaN;
                    if (float.IsNaN(gap)) issues.Add($"{label} stands inside its own area {Span(area)}");
                    else if (gap > EncounterLockRules.BarrierEdgeTolerancePixels + 0.5f) {
                        issues.Add($"{label} stands {gap:0} px off its area's edge (at most {EncounterLockRules.BarrierEdgeTolerancePixels:0})");
                    }
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EveryLockIsListedInTheBaselineOfEveryCheckpointAfterItsAreaAndNoneBefore() {
        var issues = new List<string>();
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            if (level.Locks.Count == 0 || level.Baselines == null) continue;
            foreach (LockSnapshot encounterLock in level.Locks) {
                if (!encounterLock.ID.StartsWith(level.LevelID, StringComparison.Ordinal)) {
                    issues.Add($"{level.Label}: lock ID '{encounterLock.ID}' does not start with the level ID '{level.LevelID}'");
                }
                foreach ((string checkpointID, Vector2 position) in level.Checkpoints) {
                    bool after = position.X >= encounterLock.Area.End.X;
                    bool listed = level.Baselines.TryGetValue(checkpointID, out string[] ids)
                        && ids != null && Array.IndexOf(ids, encounterLock.ID) >= 0;
                    if (after && !listed) issues.Add($"{level.Label}: {checkpointID} (x {position.X}) is past '{encounterLock.ID}' but its baseline omits it");
                    if (!after && listed) issues.Add($"{level.Label}: {checkpointID} (x {position.X}) lists '{encounterLock.ID}' before its area ends");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    /// <summary>
    /// FIX-E (2026-10-13): no fracture past a lock can be stabilized while that
    /// lock holds. At build every lock is Armed and the hero stands at its spawn,
    /// so a checkpoint that lists a lock in its F10 baseline, or stands at or past
    /// a lock's area (the baseline rule above), must answer "held"
    /// (<c>CheckpointTrigger.IsHeldByEncounterLock</c>) — a strike through a
    /// barrier would otherwise commit a baseline a reload builds Cleared — and
    /// every other checkpoint must not. Inert anchors activate nothing and are
    /// skipped. A standalone level must register its baselines with StoryManager
    /// for its fractures to be held.
    /// </summary>
    [TestCase]
    public void EveryCheckpointPastALockRefusesActivationUntilThatLockIsCleared() {
        var issues = new List<string>();
        int held = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (FractureSnapshot fracture in level.Fractures) {
                if (fracture.Inert) continue;
                string listed = null;
                string passed = null;
                foreach (LockSnapshot encounterLock in level.Locks) {
                    if (Array.IndexOf(fracture.Baseline, encounterLock.ID) >= 0) listed ??= encounterLock.ID;
                    if (fracture.Position.X >= encounterLock.Area.End.X) passed ??= encounterLock.ID;
                }
                string holder = listed ?? passed;
                if (holder != null && !fracture.HeldAtLevelStart) {
                    issues.Add($"{level.Label}: {fracture.ID} (x {fracture.Position.X:0}) can be stabilized while '{holder}' is uncleared");
                } else if (holder == null && fracture.HeldAtLevelStart) {
                    issues.Add($"{level.Label}: {fracture.ID} (x {fracture.Position.X:0}) is held at the level start though no lock comes before it");
                }
                if (fracture.HeldAtLevelStart) held++;
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(held).OverrideFailureMessage("No fracture is held anywhere: the sweep is vacuous.").IsGreater(0);
    }

    /// <summary>
    /// FIX-E (2026-10-13): a hero respawned at a checkpoint is on screen. No live
    /// checkpoint's respawn point puts the hero's body (40 × 64 px, feet on the
    /// point) inside a room trigger whose camera rect does not contain that body:
    /// the trigger fires on the first physics frame and confines the camera away
    /// from the hero, and a once-only trigger never gives the room back (Level 2's
    /// Middle anchor at 6370 sat inside the battlement trigger, whose camera starts
    /// at 6400).
    /// </summary>
    [TestCase]
    public void NoCheckpointRespawnsTheHeroInsideARoomTriggerWhoseCameraLeavesItOffScreen() {
        var issues = new List<string>();
        int checkedPoints = 0;
        foreach (LevelSnapshot level in Snapshots()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (FractureSnapshot fracture in level.Fractures) {
                if (fracture.Inert) continue;
                checkedPoints++;
                var body = new Rect2(fracture.Respawn.X - HeroHalfWidthPixels, fracture.Respawn.Y - HeroHeightPixels,
                    HeroHalfWidthPixels * 2f, HeroHeightPixels);
                foreach ((string roomID, Rect2 shape, Rect2 camera) in level.RoomTriggers) {
                    if (!body.Intersects(shape)) continue;
                    if (camera.Encloses(body)) continue;
                    issues.Add($"{level.Label}: {fracture.ID} respawns the hero at {fracture.Respawn} inside room trigger "
                        + $"'{roomID}' {Span(shape)}, whose camera {camera} leaves the hero's body {Span(body)} off screen");
                }
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(checkedPoints).IsGreater(0);
    }

    // === Snapshots ===========================================================

    private sealed class SpawnSnapshot {
        public string EnemyID;
        public Vector2 Position;
        public bool Flying;
    }

    private sealed class BarrierSnapshot {
        public Rect2 Rect;
        public bool IsEntry;
        public bool IsStaticWall;
        public bool InDoorGroup;
    }

    private sealed class LockSnapshot {
        public string ID;
        public Rect2 Area;
        public Rect2 Camera;
        public string[] RequiredPuzzleIDs = Array.Empty<string>();
        public readonly List<EncounterLockWave> Waves = new();
        public readonly List<SpawnSnapshot> Spawns = new();
        public readonly List<BarrierSnapshot> Barriers = new();
    }

    private sealed class LevelSnapshot {
        public string Label;
        public int Index;
        public string LevelID = "";
        public string LoadError;
        public Rect2 Bounds = new(0f, 0f, 1920f, 1080f);
        public IReadOnlyDictionary<string, string[]> Baselines;
        public readonly List<LockSnapshot> Locks = new();
        public readonly List<(string ID, Vector2 Position)> Checkpoints = new();
        /// <summary>Checkpoints, Restoration Fonts and sealing anchors: nothing of these may stand in an area.</summary>
        public readonly List<(string ID, Vector2 Position)> Anchors = new();
        public readonly List<(string ID, Vector2 Position)> ResetStations = new();
        public readonly List<Rect2> BossArenas = new();
        /// <summary>Every standable top (floors, platforms, one-ways), with disabled shapes.</summary>
        public readonly List<Rect2> Standables = new();
        /// <summary>FIX-E: every checkpoint fracture, with its respawn point, its baseline and the lock guard's answer at build.</summary>
        public readonly List<FractureSnapshot> Fractures = new();
        /// <summary>FIX-E: every room trigger's enabled shape and the camera rect it applies.</summary>
        public readonly List<(string RoomID, Rect2 Shape, Rect2 CameraBounds)> RoomTriggers = new();
    }

    private sealed class FractureSnapshot {
        public string ID;
        public Vector2 Position;
        public Vector2 Respawn;
        public CheckpointRole Role;
        public bool Inert;
        public bool HeldAtLevelStart;
        public string[] Baseline = Array.Empty<string>();
    }

    private static int TotalLocks() {
        int total = 0;
        foreach (LevelSnapshot level in Snapshots()) total += level.Locks.Count;
        return total;
    }

    private static List<LevelSnapshot> Snapshots() {
        if (_snapshots != null) return _snapshots;
        var snapshots = new List<LevelSnapshot>();
        for (int index = 0; index <= 15; index++) {
            snapshots.Add(Capture($"Level{index:00}", StoryManager.GetLevelScenePath((CampaignLevel)index), index));
        }
        _snapshots = snapshots;
        return snapshots;
    }

    /// <summary>
    /// Builds one campaign scene in the runner tree (no save slot, Normal,
    /// Einstein), copies the lock and placement facts into plain values and hands
    /// every shared singleton back.
    /// </summary>
    private static LevelSnapshot Capture(string label, string scenePath, int index) {
        var snapshot = new LevelSnapshot { Label = label, Index = index };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        SessionData originalSession = GameManager.Instance.CurrentSession;
        Node level = null;
        try {
            SessionData session = originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;

            PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null) { snapshot.LoadError = $"scene '{scenePath}' did not load"; return snapshot; }
            level = scene.Instantiate();
            level.Name = $"{label}_EncounterLockContent";
            tree.Root.AddChild(level);
            if (level is StoryLevelControllerBase story) {
                snapshot.LevelID = story.LevelID;
                snapshot.Bounds = story.LevelBounds;
                snapshot.Baselines = story.AuthoredEncounterBaselines;
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
            GameManager.Instance.CurrentSession = originalSession;
            tree.Paused = originalPaused;
        }
        return snapshot;
    }

    private static void Collect(Node node, LevelSnapshot snapshot) {
        switch (node) {
            case EncounterLock encounterLock:
                snapshot.Locks.Add(Snapshot(encounterLock));
                break;
            case CheckpointTrigger checkpoint:
                snapshot.Checkpoints.Add((checkpoint.CheckpointID, checkpoint.GlobalPosition));
                snapshot.Anchors.Add(($"checkpoint {checkpoint.CheckpointID}", checkpoint.GlobalPosition));
                snapshot.Fractures.Add(new FractureSnapshot {
                    ID = checkpoint.CheckpointID,
                    Position = checkpoint.GlobalPosition,
                    Respawn = checkpoint.GlobalPosition + checkpoint.RespawnOffset,
                    Role = checkpoint.Role,
                    Inert = checkpoint.Inert,
                    HeldAtLevelStart = checkpoint.IsHeldByEncounterLock,
                    Baseline = new List<string>(StoryManager.Instance?.GetEncounterBaseline(checkpoint.CheckpointID)
                        ?? Array.Empty<string>()).ToArray()
                });
                break;
            case RoomTransitionTrigger room: {
                var shapes = new List<Rect2>();
                AddShapes(room, shapes, includeDisabled: false);
                foreach (Rect2 shape in shapes) snapshot.RoomTriggers.Add((room.RoomID, shape, room.CameraBounds));
                break;
            }
            case RestorationFont font:
                snapshot.Anchors.Add(($"font {font.FontID}", font.GlobalPosition));
                break;
            case TemporalCoreAnchor anchor:
                snapshot.Anchors.Add(($"sealing anchor {anchor.Name}", anchor.GlobalPosition));
                break;
            case PuzzleResetStation station:
                snapshot.ResetStations.Add((station.Name, station.GlobalPosition));
                break;
            case BossEncounterController boss when boss.ArenaBounds.Size.X > 0f:
                snapshot.BossArenas.Add(boss.ArenaBounds);
                break;
            case MirrorParadoxEncounterController mirror when mirror.ArenaBounds.Size.X > 0f:
                snapshot.BossArenas.Add(mirror.ArenaBounds);
                break;
        }
        bool oneWay = node is OneWayPlatform;
        bool solid = !oneWay && node is PhysicsBody2D body && (body.CollisionLayer & CollisionLayers.Environment) != 0
            && node is not EncounterLockBarrier && node.GetParent() is not EncounterLockBarrier;
        if ((oneWay || solid) && node is CollisionObject2D collision) AddShapes(collision, snapshot.Standables);

        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, snapshot);
    }

    private static LockSnapshot Snapshot(EncounterLock encounterLock) {
        var snapshot = new LockSnapshot {
            ID = encounterLock.LockID,
            Area = encounterLock.Area,
            Camera = encounterLock.CameraBounds,
            RequiredPuzzleIDs = encounterLock.Spec?.RequiredPuzzleIDs ?? Array.Empty<string>()
        };
        foreach (EncounterLockWave wave in encounterLock.Spec?.Waves ?? Array.Empty<EncounterLockWave>()) {
            if (wave == null) continue;
            snapshot.Waves.Add(wave);
            foreach (EncounterLockSpawn spawn in wave.Spawns ?? Array.Empty<EncounterLockSpawn>()) {
                if (spawn == null) continue;
                EnemyData data = EnemyFactory.LoadData(spawn.EnemyID);
                snapshot.Spawns.Add(new SpawnSnapshot {
                    EnemyID = spawn.EnemyID,
                    Position = spawn.Position,
                    Flying = data?.Behavior == DefaultBehavior.Flying
                });
            }
        }
        foreach (EncounterLockBarrier barrier in new[] { encounterLock.EntryBarrier, encounterLock.ExitBarrier }) {
            if (barrier == null) continue;
            var shapes = new List<Rect2>();
            AddShapes(barrier, shapes, includeDisabled: true);
            snapshot.Barriers.Add(new BarrierSnapshot {
                Rect = shapes.Count > 0 ? shapes[0] : barrier.Footprint,
                IsEntry = barrier.IsEntry,
                IsStaticWall = barrier.GetType() == typeof(EncounterLockBarrier)
                    && (barrier.CollisionLayer & CollisionLayers.Environment) != 0,
                InDoorGroup = barrier.IsInGroup(StoryGateRules.DoorGroup)
            });
        }
        return snapshot;
    }

    private static void AddShapes(CollisionObject2D body, List<Rect2> into, bool includeDisabled = true) {
        Godot.Collections.Array<Node> shapes = body.GetChildren();
        using var shapesLifetime = shapes.AsDisposable();
        foreach (Node child in shapes) {
            if (child is not CollisionShape2D { Shape: RectangleShape2D rect } shape) continue;
            if (shape.Disabled && !includeDisabled) continue;
            Transform2D transform = shape.GlobalTransform;
            Vector2 half = rect.Size / 2f;
            Vector2 a = transform * new Vector2(-half.X, -half.Y);
            Vector2 c = transform * new Vector2(half.X, half.Y);
            into.Add(new Rect2(Mathf.Min(a.X, c.X), Mathf.Min(a.Y, c.Y), Mathf.Abs(c.X - a.X), Mathf.Abs(c.Y - a.Y)));
        }
    }

    /// <summary>The top of the highest standable surface within <paramref name="band"/> px under <paramref name="point"/>, or null.</summary>
    private static float? FloorUnder(LevelSnapshot level, Vector2 point, float band) {
        float? best = null;
        foreach (Rect2 surface in level.Standables) {
            if (point.X < surface.Position.X || point.X > surface.End.X) continue;
            float top = surface.Position.Y;
            if (top < point.Y - 1f || top > point.Y + band) continue;
            if (best == null || top < best.Value) best = top;
        }
        return best;
    }

    /// <summary>The lowest standable top under the area's centre line: its ground.</summary>
    private static float AreaGround(LevelSnapshot level, Rect2 area) {
        float ground = float.NaN;
        foreach (Rect2 surface in level.Standables) {
            if (surface.End.X <= area.Position.X || surface.Position.X >= area.End.X) continue;
            if (surface.Size.Y >= 200f) continue; // walls and columns are not ground
            if (surface.Position.Y > area.End.Y) continue;
            if (float.IsNaN(ground) || surface.Position.Y > ground) ground = surface.Position.Y;
        }
        return ground;
    }

    /// <summary>The roster's weakest double-jump rise (the slowest hero reaching a post), from the shipped data.</summary>
    private static float SlowestDoubleJumpRise() {
        float weakest = float.MaxValue;
        foreach (string id in CharacterRoster.IDs) {
            CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{id}_data.tres");
            if (data == null) continue;
            float rise = StoryAirPhysics.MultiJumpRise(StoryAirPhysics.JumpLaunchSpeed(data.MaxJumpForce), data.Weight,
                Math.Max(1, data.MaxJumpCount));
            weakest = Math.Min(weakest, rise);
        }
        return weakest == float.MaxValue ? 0f : weakest;
    }

    private static (string ID, int Authored) SplitHead(string head, int enemyCount) {
        int at = head.IndexOf('@');
        if (at <= 0) return (head, enemyCount);
        return int.TryParse(head.Substring(at + 1), out int authored) ? (head.Substring(0, at), authored) : (head.Substring(0, at), enemyCount);
    }

    private static bool OverlapsX(Rect2 area, float x, float halfWidth) =>
        x + halfWidth > area.Position.X && x - halfWidth < area.End.X;

    private static string Span(Rect2 rect) => $"x {rect.Position.X:0}..{rect.End.X:0}";
}
