using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-PILOT (rulings 7a, 7c, 7c+): what the traversal suites share. Not a suite: it
/// captures the levels that author a stretch or a route into plain values once a session,
/// reads a hero's real jump numbers through <see cref="StoryAirPhysics"/>, and flies a
/// scripted hero across a real hop. Needs the Godot runtime, so only
/// <c>[RequireGodotRuntime]</c> suites may call it.
/// </summary>
internal static class TraversalContentKit {
    /// <summary>
    /// Levels that have built their stretch and route. Each level owner adds its level, and the
    /// sweeps then fail if the level authors none. The closeout's list is 1–15.
    /// </summary>
    public static readonly int[] LevelsWithTraversal = { 2 };

    /// <summary>Levels whose route has no ability shortcut: Level 1 has no movement ability yet (dossier C §3).</summary>
    public static readonly HashSet<int> LevelsWithoutShortcuts = new() { 1 };

    /// <summary>A hop must clear with this much reach to spare: nobody lands a 100 % hop on every try.</summary>
    public const float GapMargin = 1.1f;

    /// <summary>A rise must leave this many pixels of the double jump to spare.</summary>
    public const float RiseMarginPixels = 10f;

    public sealed class HeroJump {
        public string ID;
        public float LaunchSpeed, Weight, RunSpeed;
        public float DoubleRise => StoryAirPhysics.DoubleJumpRise(LaunchSpeed, Weight);

        /// <summary>The farthest edge-to-edge gap a double jump clears to a surface <paramref name="rise"/> up, at a full run; 0 when it cannot rise that far.</summary>
        public float ReachAt(float rise) {
            int frames = StoryAirPhysics.AirtimeFrames(LaunchSpeed, Weight, 2, 1f, -rise);
            return frames < 0 ? 0f : frames * RunSpeed / 60f;
        }

        /// <summary>True when this hero clears the hop on the base kit, with the margins.</summary>
        public bool ClearsOnBaseKit(float gap, float rise) =>
            (rise <= 0f || rise + RiseMarginPixels <= DoubleRise) && gap * GapMargin <= ReachAt(Math.Max(rise, 0f));
    }

    public static HeroJump LoadHero(string heroID) {
        CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{heroID}_data.tres");
        return new HeroJump {
            ID = heroID,
            LaunchSpeed = StoryAirPhysics.JumpLaunchSpeed(data.MaxJumpForce),
            Weight = data.Weight,
            RunSpeed = data.MaxMoveSpeed * StoryAirPhysics.PixelsPerUnit
        };
    }

    public sealed class StretchFact {
        public string ID;
        public float MinX, MaxX, DeckY;
        public bool HasRoom, HasTrench;
        public float TrenchFloorY, RampRun;
        public Rect2 CameraBounds;
        public List<TraversalSurface> Line;
    }

    public sealed class RouteFact {
        public string ID, RewardSourceID;
        public Vector2 RewardPosition;
        public List<TraversalSurface> BaseLine;
        public bool HasBurst, HasRise;
        public TraversalSurface BurstFrom, BurstTo, RiseFrom, RiseTo;
    }

    public sealed class LevelFact {
        public int Index;
        public string Label, LoadError;
        public readonly List<StretchFact> Stretches = new();
        public readonly List<RouteFact> Routes = new();
        public readonly List<Rect2> LockAreas = new();
        /// <summary>Standable tops, read from the real collision shapes (a rotated one as its bounding box).</summary>
        public readonly List<(Rect2 Rect, bool OneWay, bool Crumbling)> Surfaces = new();
        public readonly List<float> RopeXs = new();
        public readonly List<Rect2> KillBoundaries = new();
        public readonly HashSet<string> ManifestExtractors = new();
        public string ManifestSecret = "";
        public readonly List<(string ID, Vector2 Position)> PlacedExtractors = new();
        public readonly List<(string ID, Vector2 Position)> PlacedSecrets = new();
    }

    private static List<LevelFact> _levels;

    /// <summary>Every level in <see cref="LevelsWithTraversal"/>, captured once.</summary>
    public static List<LevelFact> Levels() {
        if (_levels != null) return _levels;
        var captured = new List<LevelFact>();
        foreach (int index in LevelsWithTraversal) captured.Add(Capture(index));
        _levels = captured;
        return captured;
    }

    private static LevelFact Capture(int index) {
        var fact = new LevelFact { Index = index, Label = $"Level{index:00}" };
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool originalPaused = tree.Paused;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        Node level = null;
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            GameManager.Instance.CurrentSession.SelectedCharacterID = "einstein";
            LevelRewardManifest manifest = LevelRewardManifest.LoadFor(index);
            if (manifest != null) {
                foreach (string source in manifest.ExtractorSourceIDs) fact.ManifestExtractors.Add(source);
                fact.ManifestSecret = manifest.SecretSourceID ?? "";
            }
            PackedScene scene = ResourceLoader.Load<PackedScene>(StoryManager.GetLevelScenePath((CampaignLevel)index));
            if (scene == null) { fact.LoadError = "the scene did not load"; return fact; }
            level = scene.Instantiate();
            level.Name = $"{fact.Label}_TraversalTest";
            tree.Root.AddChild(level);
            Collect(level, fact);
        } catch (Exception exception) {
            fact.LoadError = $"capture threw {exception.GetType().Name}: {exception.Message}";
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (level != null && GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            tree.Paused = originalPaused;
        }
        return fact;
    }

    private static void Collect(Node node, LevelFact fact) {
        switch (node) {
            case TraversalStretch stretch:
                fact.Stretches.Add(new StretchFact {
                    ID = stretch.StretchID, MinX = stretch.MinX, MaxX = stretch.MaxX, DeckY = stretch.DeckY,
                    HasRoom = stretch.Room != null, CameraBounds = stretch.Room?.CameraBounds ?? default,
                    HasTrench = stretch.HasTrench, TrenchFloorY = stretch.TrenchFloorY, RampRun = stretch.TrenchRampRun,
                    Line = stretch.Line.ToList()
                });
                break;
            case OptionalRoute route:
                fact.Routes.Add(new RouteFact {
                    ID = route.RouteID, RewardSourceID = route.RewardSourceID, RewardPosition = route.RewardPosition,
                    BaseLine = route.BaseLine.ToList(),
                    HasBurst = route.HasBurstShortcut, BurstFrom = route.BurstFrom, BurstTo = route.BurstTo,
                    HasRise = route.HasRiseShortcut, RiseFrom = route.RiseFrom, RiseTo = route.RiseTo
                });
                break;
            case EncounterLock encounterLock:
                fact.LockAreas.Add(encounterLock.Area);
                break;
            case PendulumAnchor rope:
                fact.RopeXs.Add(rope.GlobalPosition.X);
                break;
            case StoryKillBoundary boundary:
                foreach (Rect2 rect in ShapeRects(boundary)) fact.KillBoundaries.Add(rect);
                break;
            case ChronalExtractor extractor:
                fact.PlacedExtractors.Add((extractor.ObjectID, extractor.GlobalPosition));
                break;
            case SecretCache cache:
                fact.PlacedSecrets.Add((cache.SecretID, cache.GlobalPosition));
                break;
        }
        bool oneWay = node is OneWayPlatform;
        bool solid = !oneWay && node is PhysicsBody2D body && (body.CollisionLayer & CollisionLayers.Environment) != 0
            && node is not EncounterLockBarrier;
        if (oneWay || solid) {
            foreach (Rect2 rect in ShapeRects((Node2D)node)) fact.Surfaces.Add((rect, oneWay, node is CrumblingPlatform));
        }
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) Collect(child, fact);
    }

    private static IEnumerable<Rect2> ShapeRects(Node2D body) {
        var rects = new List<Rect2>();
        Godot.Collections.Array<Node> children = body.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is not CollisionShape2D { Disabled: false, Shape: RectangleShape2D rectangle } shape) continue;
            Vector2 half = rectangle.Size / 2f;
            Transform2D transform = shape.GlobalTransform;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (Vector2 corner in new[] { new Vector2(-half.X, -half.Y), new Vector2(half.X, -half.Y), new Vector2(-half.X, half.Y), new Vector2(half.X, half.Y) }) {
                Vector2 world = transform * corner;
                min = new Vector2(Mathf.Min(min.X, world.X), Mathf.Min(min.Y, world.Y));
                max = new Vector2(Mathf.Max(max.X, world.X), Mathf.Max(max.Y, world.Y));
            }
            rects.Add(new Rect2(min, max - min));
        }
        return rects;
    }

    // === A scripted flight in real physics frames ==========================

    /// <summary>How a flight presses the movement ability, once its jumps are spent.</summary>
    public enum FlightAbility { None, Forward, Up }

    /// <summary>
    /// Runs a hero off <paramref name="from"/> and tries to land on <paramref name="to"/>,
    /// in real frames on a copy of the two surfaces: run from the far side (or, for a target
    /// overhead, walk under it, or to <paramref name="takeOffX"/> for a kit that flies forward as it rises), jump at the edge, second jump at the first apex, the ability
    /// (if any) at the second apex, then steer to the target. Returns where the hero came to rest and whether that is on the
    /// target. The caller's world has nothing else in it, so nothing but the hop is measured.
    /// </summary>
    public static async Task<(bool Landed, string Report)> Fly(string heroID, TraversalSurface from, TraversalSurface to,
        FlightAbility ability, float? takeOffX = null) {
        var tree = (SceneTree)Engine.GetMainLoop();
        const float Offset = -30000f; // far from anything else in the runner tree
        var root = new Node2D { Name = "TraversalFlight", Position = new Vector2(Offset, 0f) };
        tree.Root.AddChild(root);
        root.AddChild(MakeSurface("From", from, false));
        root.AddChild(MakeSurface("To", to, true));
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
        tree.Root.AddChild(hero);
        var source = new BufferedInputSource();
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        float direction = to.Left >= from.Right ? 1f : to.Right <= from.Left ? -1f : 0f;
        try {
            float spawnX = direction >= 0f ? from.Left + 4f : from.Right - 4f;
            hero.GlobalPosition = new Vector2(Offset + spawnX, from.Top - 2f);
            hero.IsFacingRight = direction >= 0f;
            for (int frame = 0; frame < 6; frame++) await Step(tree, source, 0f, 0f, GameplayButtons.None);
            float centre = (to.Left + to.Right) / 2f;
            float takeOff = takeOffX ?? centre;
            // 0 run to the take-off, 1 first jump held, 2 one tick off Jump, 3 second jump held,
            // 4 ability pressed, 5 steering home.
            int stage = 0;
            GameplayButtons held = GameplayButtons.None;
            float peak = float.MaxValue;
            for (int frame = 0; frame < 480; frame++) {
                float x = hero.GlobalPosition.X - Offset;
                float y = hero.GlobalPosition.Y;
                peak = Mathf.Min(peak, y);
                bool grounded = hero.IsOnFloor();
                if (stage > 0 && grounded && Mathf.Abs(y - to.Top) <= 2f && x >= to.Left && x <= to.Right) {
                    return (true, $"{heroID} landed on {to} at x {x:0}, peak {from.Top - peak:0} px above the start");
                }
                if (y > Mathf.Max(from.Top, to.Top) + 400f) break;

                float toward = Mathf.Abs(x - centre) < 6f ? 0f : x < centre ? 1f : -1f;
                float stick = toward;
                float vertical = 0f;
                GameplayButtons buttons = GameplayButtons.None;
                switch (stage) {
                    case 0:
                        stick = direction != 0f ? direction : Mathf.Abs(x - takeOff) < 6f ? 0f : x < takeOff ? 1f : -1f;
                        bool ready = direction > 0f ? x >= from.Right - 6f
                            : direction < 0f ? x <= from.Left + 6f
                            : stick == 0f;
                        if (ready && grounded) {
                            buttons = GameplayButtons.Jump;
                            stage = 1;
                        }
                        break;
                    case 1:
                        buttons = GameplayButtons.Jump;
                        if (!grounded && hero.Velocity.Y >= 0f) stage = 2;
                        break;
                    case 2:
                        stage = 3;
                        buttons = GameplayButtons.None; // the release a fresh press needs
                        break;
                    case 3:
                        buttons = GameplayButtons.Jump;
                        if ((held & GameplayButtons.Jump) != 0 && !grounded && hero.Velocity.Y >= 0f) {
                            stage = ability == FlightAbility.None ? 5 : 4;
                        }
                        break;
                    case 4:
                        buttons = GameplayButtons.MovementAbility;
                        if (ability == FlightAbility.Up) { vertical = -1f; stick = 0f; }
                        else stick = direction;
                        stage = 5;
                        break;
                    default:
                        buttons = ability == FlightAbility.None ? GameplayButtons.Jump : GameplayButtons.None;
                        break;
                }
                source.SetNextFrame(PlayerInputFrame.Create(0, stick, vertical, buttons, held));
                held = buttons;
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            float endX = hero.GlobalPosition.X - Offset;
            return (false, $"{heroID} missed {to}: ended at ({endX:0}, {hero.GlobalPosition.Y:0}), peak {from.Top - peak:0} px above the start, state {hero.CurrentState}");
        } finally {
            InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            hero.GetParent()?.RemoveChild(hero);
            hero.Free();
            root.GetParent()?.RemoveChild(root);
            root.Free();
        }
    }

    private static async Task Step(SceneTree tree, BufferedInputSource source, float h, float v, GameplayButtons held) {
        source.SetNextFrame(PlayerInputFrame.Create(0, h, v, held, GameplayButtons.None));
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static Node2D MakeSurface(string name, TraversalSurface surface, bool oneWay) {
        const float Thickness = 16f;
        float width = surface.Right - surface.Left;
        var collision = new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(width, Thickness) },
            OneWayCollision = oneWay
        };
        var position = new Vector2(surface.Left + width / 2f, surface.Top + Thickness / 2f);
        if (oneWay) {
            var platform = new OneWayPlatform { Name = name, Position = position, CollisionLayer = CollisionLayers.OneWayPlatform, CollisionMask = 0 };
            collision.OneWayCollisionMargin = 12f;
            platform.AddChild(collision);
            return platform;
        }
        var body = new StaticBody2D { Name = name, Position = position, CollisionLayer = CollisionLayers.Environment, CollisionMask = 0 };
        body.AddChild(collision);
        return body;
    }
}
