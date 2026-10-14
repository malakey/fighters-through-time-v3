using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// FIX-LEVEL (2026-10-13, Phase B review item 2): every campaign scene's Middle
/// and PreBoss anchors are a checkpoint for the fight after them. Each must stand
/// outside every encounter lock's area, and its strike surface must be reachable
/// without first crossing the <b>next</b> lock's engage line — the line past which
/// the lock engages, shuts its entry barrier and holds every fracture behind it
/// until it clears (FIX-E). Level 7's Middle anchor stood on the Bonnet's deck
/// under the solid receiving yard, where rope C lands the hero: the only way down
/// was the yard's east end, past the boarding lock's engage line, so the boarding
/// fight had no checkpoint at all.
///
/// <para><b>How "reachable" is read.</b> From the anchor, the search walks its
/// standing surface (the deck it is authored on) both ways until a wall, the
/// surface's end or the next lock's engage line, then asks whether the hero could
/// have arrived on that stretch: from above, where some column of it is open (no
/// solid anywhere over the hero's head — one-way platforms, moving platforms,
/// barriers, doors and breakables never count as a ceiling), or from the stretch's
/// own ends — onward at the same height, by dropping off a higher surface whose
/// edge is over it, or by jumping up from a lower surface within the roster's
/// weakest double-jump rise — or by walking back to the previous lock's area (or
/// the hero's spawn), and so on across surfaces. It is a coarse platform graph in
/// rectangles: no ropes, no swings, no movement abilities, no Down-Air through a
/// floor and no double jump in the drift sense, which is the point (a checkpoint
/// must be reachable by ordinary means). Rect collision shapes only; a rotated
/// one is read as its bounding box.</para>
///
/// <para>Not a duplicate of <c>EncounterLockContentTests</c>' anchor rule (a
/// footprint outside every area): an anchor can stand outside every area and
/// still be reachable only through one, which is what Level 7 did.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AnchorApproachContentTests {

    /// <summary>The 40 × 64 px Story hero body (CharacterFactory, feet on the origin).</summary>
    private const float HeroHalfWidthPixels = 20f;
    private const float HeroHeightPixels = 64f;
    /// <summary>A checkpoint's strike surface reaches this far either side of its x (CheckpointTrigger's 60 px box).</summary>
    private const float AnchorHalfWidthPixels = 30f;
    /// <summary>An anchor is authored 10–20 px above the deck it stands on; its deck lies within this band below it.</summary>
    private const float StandingBandPixels = 60f;
    /// <summary>Two surfaces whose tops differ by at most this are one walk.</summary>
    private const float SameHeightPixels = 2f;
    /// <summary>A walk that reaches this close to the previous lock's area (its exit barrier is 20 px, the hero 20) came from it.</summary>
    private const float SourceReachPixels = 40f;
    private const int SearchLimit = 4000;

    private static List<LevelGeometry> _levels;

    /// <summary>
    /// Every live Middle and PreBoss anchor in Levels 0–15 stands outside every
    /// lock area and is reached before the next lock's engage line.
    /// </summary>
    [TestCase]
    public void EveryMiddleAndPreBossAnchorIsReachedBeforeTheNextLockEngages() {
        var issues = new List<string>();
        int checkedAnchors = 0;
        int anchorsBeforeALock = 0;
        foreach (LevelGeometry level in Levels()) {
            if (level.LoadError != null) { issues.Add($"{level.Label}: {level.LoadError}"); continue; }
            foreach (AnchorFact anchor in level.Anchors) {
                if (anchor.Role == CheckpointRole.Entry || anchor.Inert) continue;
                checkedAnchors++;
                if (NextLock(level, anchor.Position.X) != null) anchorsBeforeALock++;
                string issue = ApproachIssue(level, anchor.ID, anchor.Position);
                if (issue != null) issues.Add($"{level.Label}: {issue}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(checkedAnchors).OverrideFailureMessage("No Middle or PreBoss anchor was found: the sweep is vacuous.").IsGreater(0);
        AssertThat(anchorsBeforeALock).OverrideFailureMessage("No anchor stands before a lock: the engage-line half is vacuous.").IsGreater(0);
    }

    /// <summary>
    /// The rule catches the defect it was written for: Level 7's GU-L4 Middle
    /// anchor (5700, on the Bonnet's deck under the solid receiving yard) is
    /// refused, and the shipped one on the yard passes.
    /// </summary>
    [TestCase]
    public void TheRuleRefusesLevelSevensOldMiddleAnchorUnderTheReceivingYard() {
        LevelGeometry nassau = Levels()[7];
        AssertThat(nassau.LoadError == null).OverrideFailureMessage($"Level 7 did not load: {nassau.LoadError}").IsTrue();
        string old = ApproachIssue(nassau, "the GU-L4 Middle anchor", new Vector2(5700f, Level07Controller.DeckY - 20f));
        AssertThat(old != null)
            .OverrideFailureMessage("The approach rule passed the anchor under the yard, reachable only past the boarding lock's engage line.")
            .IsTrue();
        string shipped = ApproachIssue(nassau, "the Middle anchor", Level07Controller.CheckpointPlacements[1].Position);
        AssertThat(shipped == null).OverrideFailureMessage($"The shipped Middle anchor: {shipped}").IsTrue();
    }

    // === The rule =============================================================

    private static LockFact NextLock(LevelGeometry level, float x) {
        LockFact next = null;
        foreach (LockFact encounterLock in level.Locks) {
            if (encounterLock.Area.Position.X < x + AnchorHalfWidthPixels) continue;
            if (next == null || encounterLock.Area.Position.X < next.Area.Position.X) next = encounterLock;
        }
        return next;
    }

    private static LockFact PreviousLock(LevelGeometry level, float x) {
        LockFact previous = null;
        foreach (LockFact encounterLock in level.Locks) {
            if (encounterLock.Area.End.X > x - AnchorHalfWidthPixels) continue;
            if (previous == null || encounterLock.Area.End.X > previous.Area.End.X) previous = encounterLock;
        }
        return previous;
    }

    /// <summary>Null when the anchor at <paramref name="position"/> passes, else why not.</summary>
    private static string ApproachIssue(LevelGeometry level, string id, Vector2 position) {
        foreach (LockFact encounterLock in level.Locks) {
            if (position.X + AnchorHalfWidthPixels > encounterLock.Area.Position.X
                && position.X - AnchorHalfWidthPixels < encounterLock.Area.End.X) {
                return $"{id} at x {position.X:0} stands inside '{encounterLock.ID}' (x {encounterLock.Area.Position.X:0}..{encounterLock.Area.End.X:0})";
            }
        }
        LockFact next = NextLock(level, position.X);
        float engageX = next != null
            ? next.Area.Position.X + HeroHalfWidthPixels + EncounterLockRules.EngageInsetPixels
            : float.PositiveInfinity;
        LockFact previous = PreviousLock(level, position.X);
        float sourceX = previous != null ? previous.Area.End.X : level.Spawn.X;

        int standing = StandingSurface(level, position);
        if (standing < 0) return $"{id} at {position} stands on no surface";

        var queue = new Queue<(int Surface, float EntryX)>();
        var visited = new HashSet<(int, int)>();
        queue.Enqueue((standing, position.X));
        int steps = 0;
        bool first = true;
        while (queue.Count > 0 && steps++ < SearchLimit) {
            (int surfaceIndex, float entryX) = queue.Dequeue();
            Rect2 surface = level.Rects[surfaceIndex].Rect;
            if (!WalkInterval(level, surfaceIndex, entryX, engageX, out float a, out float b)) {
                if (first) return $"{id} at {position}: a wall stands where the hero would strike it";
                continue;
            }
            first = false;
            if (!visited.Add((surfaceIndex, Mathf.RoundToInt(a)))) continue;
            if (HasOpenColumn(level, surface.Position.Y, a, b)) return null;
            if (a <= sourceX + SourceReachPixels) return null;
            EnqueueNeighbours(level, surfaceIndex, a, b, engageX, queue);
        }
        string bound = next != null ? $"'{next.ID}''s engage line (x {engageX:0})" : "the level's end";
        return $"{id} at {position} is reached only past {bound}: its deck is covered and walled in short of it";
    }

    /// <summary>The surface the anchor stands on: the nearest standable top within the band below it.</summary>
    private static int StandingSurface(LevelGeometry level, Vector2 position) {
        int best = -1;
        for (int index = 0; index < level.Rects.Count; index++) {
            SurfaceRect rect = level.Rects[index];
            if (!rect.Standable) continue;
            if (position.X < rect.Rect.Position.X || position.X > rect.Rect.End.X) continue;
            float top = rect.Rect.Position.Y;
            if (top < position.Y - 1f || top > position.Y + StandingBandPixels) continue;
            if (best < 0 || top < level.Rects[best].Rect.Position.Y) best = index;
        }
        return best;
    }

    /// <summary>
    /// The pivot interval a hero standing on <paramref name="surfaceIndex"/> at
    /// <paramref name="entryX"/> can walk: the surface's ends, the walls in the
    /// hero's body band, and the next lock's engage line.
    /// </summary>
    private static bool WalkInterval(LevelGeometry level, int surfaceIndex, float entryX, float engageX,
        out float a, out float b) {
        Rect2 surface = level.Rects[surfaceIndex].Rect;
        float top = surface.Position.Y;
        a = surface.Position.X;
        b = Mathf.Min(surface.End.X, engageX - 1f);
        if (entryX < a - 0.5f || entryX > b + 0.5f) return false;
        for (int index = 0; index < level.Rects.Count; index++) {
            if (index == surfaceIndex) continue;
            SurfaceRect rect = level.Rects[index];
            if (!rect.Blocks) continue;
            Rect2 wall = rect.Rect;
            if (wall.Position.Y >= top - 1f || wall.End.Y <= top - HeroHeightPixels) continue; // not in the body band
            if (wall.End.X <= entryX - HeroHalfWidthPixels) a = Mathf.Max(a, wall.End.X + HeroHalfWidthPixels);
            else if (wall.Position.X >= entryX + HeroHalfWidthPixels) b = Mathf.Min(b, wall.Position.X - HeroHalfWidthPixels);
            else return false; // the body at entryX is inside it
        }
        return a <= b;
    }

    /// <summary>True when some pivot in [<paramref name="a"/>, <paramref name="b"/>] has no solid anywhere over the hero's head.</summary>
    private static bool HasOpenColumn(LevelGeometry level, float top, float a, float b) {
        var covered = new List<(float From, float To)>();
        foreach (SurfaceRect rect in level.Rects) {
            if (!rect.Ceiling) continue;
            if (rect.Rect.End.Y > top - HeroHeightPixels + 0.5f) continue; // not over the head
            covered.Add((rect.Rect.Position.X - HeroHalfWidthPixels, rect.Rect.End.X + HeroHalfWidthPixels));
        }
        covered.Sort((left, right) => left.From.CompareTo(right.From));
        float x = a;
        foreach ((float from, float to) in covered) {
            if (to <= x) continue;
            if (from > x) return true; // a gap at x
            x = to;
            if (x > b) return false;
        }
        return x <= b;
    }

    private static void EnqueueNeighbours(LevelGeometry level, int surfaceIndex, float a, float b, float engageX,
        Queue<(int, float)> queue) {
        Rect2 surface = level.Rects[surfaceIndex].Rect;
        float top = surface.Position.Y;
        bool atWestEnd = a <= surface.Position.X + 0.5f;
        bool atEastEnd = b >= surface.End.X - 0.5f;
        bool oneWay = level.Rects[surfaceIndex].OneWay;
        for (int index = 0; index < level.Rects.Count; index++) {
            if (index == surfaceIndex) continue;
            SurfaceRect other = level.Rects[index];
            if (!other.Standable) continue;
            Rect2 next = other.Rect;
            float otherTop = next.Position.Y;
            if (Mathf.Abs(otherTop - top) <= SameHeightPixels) {
                // Onward at the same height, across a seam.
                if (atWestEnd && next.End.X >= surface.Position.X - SameHeightPixels && next.Position.X < surface.Position.X) {
                    Enqueue(queue, index, surface.Position.X - 1f, engageX);
                }
                if (atEastEnd && next.Position.X <= surface.End.X + SameHeightPixels && next.End.X > surface.End.X) {
                    Enqueue(queue, index, surface.End.X + 1f, engageX);
                }
            } else if (otherTop < top) {
                // Down from a higher surface: off its east edge, off its west edge, or through it (one-way).
                float offEast = next.End.X + HeroHalfWidthPixels + 1f;
                float offWest = next.Position.X - HeroHalfWidthPixels - 1f;
                if (offEast >= a && offEast <= b) Enqueue(queue, index, next.End.X - 1f, engageX);
                if (offWest >= a && offWest <= b) Enqueue(queue, index, next.Position.X + 1f, engageX);
                if (other.OneWay && next.Position.X < b && next.End.X > a) {
                    Enqueue(queue, index, Mathf.Clamp((a + b) / 2f, next.Position.X, next.End.X), engageX);
                }
            } else if (otherTop - top <= level.JumpRisePixels) {
                // Up from a lower surface: onto either end, or through this one if it is one-way.
                if (atWestEnd && next.Position.X < surface.Position.X - HeroHalfWidthPixels
                    && next.End.X >= surface.Position.X - HeroHalfWidthPixels) {
                    Enqueue(queue, index, Mathf.Min(next.End.X, surface.Position.X - HeroHalfWidthPixels - 1f), engageX);
                }
                if (atEastEnd && next.End.X > surface.End.X + HeroHalfWidthPixels
                    && next.Position.X <= surface.End.X + HeroHalfWidthPixels) {
                    Enqueue(queue, index, Mathf.Max(next.Position.X, surface.End.X + HeroHalfWidthPixels + 1f), engageX);
                }
                if (oneWay && next.Position.X < b && next.End.X > a) {
                    Enqueue(queue, index, Mathf.Clamp((a + b) / 2f, next.Position.X, next.End.X), engageX);
                }
            }
        }
    }

    private static void Enqueue(Queue<(int, float)> queue, int index, float entryX, float engageX) {
        if (entryX < engageX) queue.Enqueue((index, entryX));
    }

    // === Geometry snapshots ==================================================

    private sealed class LockFact {
        public string ID;
        public Rect2 Area;
    }

    private sealed class AnchorFact {
        public string ID;
        public Vector2 Position;
        public CheckpointRole Role;
        public bool Inert;
    }

    /// <summary>One rectangle of collision: what it is to a walking hero.</summary>
    private readonly record struct SurfaceRect(Rect2 Rect, bool Standable, bool Blocks, bool Ceiling, bool OneWay);

    private sealed class LevelGeometry {
        public string Label;
        public string LoadError;
        public Vector2 Spawn;
        public bool SpawnFound;
        public float JumpRisePixels;
        public readonly List<LockFact> Locks = new();
        public readonly List<AnchorFact> Anchors = new();
        public readonly List<SurfaceRect> Rects = new();
    }

    private static List<LevelGeometry> Levels() {
        if (_levels != null) return _levels;
        float rise = SlowestDoubleJumpRise();
        var levels = new List<LevelGeometry>();
        for (int index = 0; index <= 15; index++) {
            LevelGeometry level = Capture($"Level{index:00}", StoryManager.GetLevelScenePath((CampaignLevel)index));
            level.JumpRisePixels = rise;
            levels.Add(level);
        }
        _levels = levels;
        return levels;
    }

    /// <summary>
    /// Builds one campaign scene in the runner tree (no save slot, Normal,
    /// Einstein), copies its locks, anchors and collision rectangles into plain
    /// values and hands every shared singleton back.
    /// </summary>
    private static LevelGeometry Capture(string label, string scenePath) {
        var geometry = new LevelGeometry { Label = label };
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
            if (scene == null) { geometry.LoadError = $"scene '{scenePath}' did not load"; return geometry; }
            level = scene.Instantiate();
            level.Name = $"{label}_AnchorApproach";
            tree.Root.AddChild(level);
            Collect(level, geometry, passable: false);
            if (!geometry.SpawnFound) geometry.LoadError = "no hero was spawned";
        } catch (Exception exception) {
            geometry.LoadError = $"capture threw {exception.GetType().Name}: {exception.Message}";
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession = originalSession;
            tree.Paused = originalPaused;
        }
        return geometry;
    }

    private static void Collect(Node node, LevelGeometry geometry, bool passable) {
        switch (node) {
            case EncounterLock encounterLock:
                geometry.Locks.Add(new LockFact { ID = encounterLock.LockID, Area = encounterLock.Area });
                break;
            case CheckpointTrigger checkpoint:
                geometry.Anchors.Add(new AnchorFact {
                    ID = checkpoint.CheckpointID,
                    Position = checkpoint.GlobalPosition,
                    Role = checkpoint.Role,
                    Inert = checkpoint.Inert
                });
                break;
            case PlayerController hero when !geometry.SpawnFound && hero.IsInGroup("StoryPlayer"):
                geometry.Spawn = hero.GlobalPosition;
                geometry.SpawnFound = true;
                break;
        }
        // Barriers, doors and breakables open: never a wall, a ceiling or a floor here.
        bool opens = passable || node is EncounterLockBarrier or ForcefieldBarrier or DamageableEnvironmentObject
            || node.IsInGroup(StoryGateRules.DoorGroup);
        if (!opens && node is PhysicsBody2D body && node is not CharacterBody2D
            && (body.CollisionLayer & CollisionLayers.Environment) != 0) {
            bool oneWay = node is OneWayPlatform;
            bool moving = node is AnimatableBody2D;
            AddRects(body, geometry.Rects, oneWay, moving);
        } else if (!opens && node is OneWayPlatform platform) {
            AddRects(platform, geometry.Rects, oneWay: true, moving: false);
        }

        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, geometry, opens);
    }

    private static void AddRects(CollisionObject2D body, List<SurfaceRect> into, bool oneWay, bool moving) {
        Godot.Collections.Array<Node> shapes = body.GetChildren();
        using var shapesLifetime = shapes.AsDisposable();
        foreach (Node child in shapes) {
            if (child is not CollisionShape2D { Shape: RectangleShape2D rect } shape || shape.Disabled) continue;
            bool shapeOneWay = oneWay || shape.OneWayCollision;
            Transform2D transform = shape.GlobalTransform;
            Vector2 half = rect.Size / 2f;
            Vector2 a = transform * new Vector2(-half.X, -half.Y);
            Vector2 b = transform * new Vector2(half.X, -half.Y);
            Vector2 c = transform * new Vector2(half.X, half.Y);
            Vector2 d = transform * new Vector2(-half.X, half.Y);
            float minX = Mathf.Min(Mathf.Min(a.X, b.X), Mathf.Min(c.X, d.X));
            float maxX = Mathf.Max(Mathf.Max(a.X, b.X), Mathf.Max(c.X, d.X));
            float minY = Mathf.Min(Mathf.Min(a.Y, b.Y), Mathf.Min(c.Y, d.Y));
            float maxY = Mathf.Max(Mathf.Max(a.Y, b.Y), Mathf.Max(c.Y, d.Y));
            var bounds = new Rect2(minX, minY, maxX - minX, maxY - minY);
            // A slab turned off the axes is a walkable ramp (Pompeii's caldera
            // slopes, Nassau's listing deck): its bounding box is a floor, never a
            // wall or a ceiling.
            bool ramp = Mathf.Abs(Mathf.Sin(2f * transform.Rotation)) > 0.01f;
            bool solid = !shapeOneWay && !moving && !ramp;
            into.Add(new SurfaceRect(bounds, Standable: true, Blocks: solid, Ceiling: solid, OneWay: shapeOneWay));
        }
    }

    /// <summary>The roster's weakest double-jump rise, from the shipped data (the EncounterLockContentTests measure).</summary>
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
}
