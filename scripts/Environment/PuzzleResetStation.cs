using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>Outcome of one <see cref="PuzzleResetStation.TryReset"/> attempt.</summary>
    public enum PuzzleResetResult {
        /// <summary>The unsolved puzzle's props were returned to their authored homes.</summary>
        Reset,
        /// <summary>The station is not bound to a puzzle.</summary>
        NoPuzzle,
        /// <summary>The puzzle is solved (live or persisted): the station is inert.</summary>
        Solved,
        /// <summary>No living, in-control hero activated it.</summary>
        InvalidActor,
        /// <summary>Time Freeze, the Post-Landing Hold or a boss suspension holds the world.</summary>
        WorldStopped,
        /// <summary>An actor or foreign body occupies a restoration volume; nothing moved.</summary>
        Obstructed,
        /// <summary>A second activation in the same physics tick (a duplicated callback).</summary>
        DuplicateActivation,
        /// <summary>Called from inside a physics in/out flush, where bodies cannot be moved safely.</summary>
        Busy
    }

    /// <summary>
    /// Package 12 W8 (GAP-08 / V01b): the local <b>Reset Puzzle</b> interaction.
    ///
    /// <para>A plain <see cref="IInteractable"/> with an <see cref="InteractionArea"/>
    /// child (the <see cref="RestorationFont"/> / <see cref="TemporalCoreAnchor"/>
    /// pattern), placed on stable ground beside a puzzle whose props can be lost or
    /// jammed. One deliberate Interact press restores the bound <b>unsolved</b>
    /// puzzle's registered <see cref="WeightedObject"/> props to their authored homes,
    /// clears their motion and stale plate contacts, and lets the plates recompute
    /// from the restored arrangement. No dust, meter, cooldown, HP, encounter, reward
    /// or clock state is touched, and no completion signal is raised.</para>
    ///
    /// <para><b>Solved facts win.</b> Once the puzzle is solved — live, or already
    /// committed to the active save — the station refuses, hides its prompt and
    /// stops monitoring. It rechecks immediately before committing, so a completion
    /// that lands in the same tick is never undone.</para>
    ///
    /// <para><b>Geometry rule.</b> Every prop's restoration volume (its own collision
    /// shapes at its authored home) is validated <i>before</i> anything moves, against
    /// the collision shapes of the player(s), enemies/bosses, persistent constructs
    /// and any puzzle weight that is not one of this puzzle's props. If any volume is
    /// obstructed the <b>whole</b> reset is refused with a brief notice and the room is
    /// left exactly as it was — the reset never moves, pushes, damages or encloses an
    /// actor to make room, and never leaves a partially reset room. Stepping clear and
    /// pressing Interact again retries. Static level geometry is not an obstruction:
    /// authored homes are clear by construction.</para>
    ///
    /// <para><b>Lost props.</b> The station restores a prop from wherever it is,
    /// including out of the room or out of bounds. A prop instance that no longer
    /// exists is replaced from its recorded scene (or, for a code-built prop, a
    /// shape-only stand-in) under the same <see cref="WeightedObject.PropID"/>;
    /// surviving instances are never duplicated.</para>
    ///
    /// <para><b>Refusals.</b> Time Freeze, the Post-Landing Hold / boss suspension,
    /// a dead or respawning hero, and a physics flush all refuse without queuing.
    /// <see cref="InteractionArea.TryInteract"/> already refuses the first two;
    /// <see cref="TryReset"/> re-checks so a direct call obeys the same rule.</para>
    /// </summary>
    public partial class PuzzleResetStation : Node2D, IInteractable {

        /// <summary>Every station joins this group; the placement sweep reads it.</summary>
        public const string StationGroup = "puzzle_reset_station";

        public const string DefaultPromptKey = "interaction_reset_puzzle";
        public const string DefaultObstructedNoticeKey = "puzzle_reset_obstructed";
        public const string DefaultResetNoticeKey = "puzzle_reset_done";

        /// <summary>Groups whose collision shapes obstruct a restoration volume.</summary>
        public static readonly string[] ObstructionGroups = {
            "Players", "StoryPlayer", "Enemies", "persistent_construct", WeightedObject.MovableWeightGroup
        };

        [Export] public string StationID = "";
        [Export] public NodePath PuzzlePath;
        [Export] public string InteractionPromptKey = DefaultPromptKey;
        [Export] public string ObstructedNoticeKey = DefaultObstructedNoticeKey;
        [Export] public string ResetNoticeKey = DefaultResetNoticeKey;
        [Export] public NodePath InteractionAreaPath = "Interaction";
        [Export] public NodePath VisualPath = "Visual";

        private sealed class PropRecord {
            public WeightedObject Instance;
            public string PropID;
            public Transform2D Home;
            public string ScenePath;
            public NodePath ParentPath;
            public float WeightUnits;
            public readonly List<(Shape2D Shape, Transform2D Local)> Shapes = new();
        }

        private readonly Dictionary<string, PropRecord> _props = new();
        private readonly List<string> _propOrder = new();
        private PuzzleManager _puzzle;
        private ulong _lastResetPhysicsFrame = ulong.MaxValue;
        private bool _completionBound;

        public string InteractionID => StationID;
        public string PromptKey => InteractionPromptKey;

        /// <summary>The bound puzzle, resolved from <see cref="PuzzlePath"/>.</summary>
        public PuzzleManager Puzzle {
            get {
                if (_puzzle != null && IsInstanceValid(_puzzle)) return _puzzle;
                _puzzle = PuzzlePath == null || PuzzlePath.IsEmpty || !IsInsideTree()
                    ? null
                    : GetNodeOrNull<PuzzleManager>(PuzzlePath);
                return _puzzle;
            }
        }

        public string PuzzleID => Puzzle?.PuzzleID ?? "";

        /// <summary>Successful resets committed by this station.</summary>
        public int ResetCount { get; private set; }

        /// <summary>The last refusal or success, for presentation and tests.</summary>
        public PuzzleResetResult LastResult { get; private set; } = PuzzleResetResult.NoPuzzle;

        /// <summary>The prop IDs this station currently knows, in registration order.</summary>
        public IReadOnlyList<string> RegisteredPropIDs => _propOrder;

        public override void _Ready() {
            AddToGroup(StationGroup);
            // Props and the puzzle may be later siblings; bind once the scene is ready.
            Callable.From(BindDeferred).CallDeferred();
        }

        public override void _ExitTree() {
            if (_completionBound && _puzzle != null && IsInstanceValid(_puzzle)) {
                _puzzle.PuzzleCompleted -= OnPuzzleCompleted;
            }
            _completionBound = false;
        }

        private void BindDeferred() {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            BindPuzzleSignals();
            RefreshPropRegistry();
            ApplyPresentation();
        }

        private void BindPuzzleSignals() {
            if (_completionBound || Puzzle == null) return;
            Puzzle.PuzzleCompleted += OnPuzzleCompleted;
            _completionBound = true;
        }

        private void OnPuzzleCompleted(string puzzleID) => ApplyPresentation();

        /// <summary>True once the bound puzzle is solved live or in the save.</summary>
        public bool IsPuzzleSolved => Puzzle?.IsSolvedOrPersisted == true;

        public bool CanInteract(PlayerController player) =>
            IsActorEligible(player) && Puzzle != null && !IsPuzzleSolved;

        public void Interact(PlayerController player) => TryReset(player);

        /// <summary>
        /// Scans the tree for this puzzle's props and records their identity, home
        /// and shapes. Records survive their instance, so a lost prop can be
        /// replaced; a surviving instance is refreshed, never duplicated.
        /// </summary>
        public void RefreshPropRegistry() {
            string puzzleID = PuzzleID;
            if (string.IsNullOrWhiteSpace(puzzleID) || !IsInsideTree()) return;
            Godot.Collections.Array<Node> weights = GetTree().GetNodesInGroup(WeightedObject.MovableWeightGroup);
            using var lifetime = weights.AsDisposable();
            foreach (Node node in weights) {
                if (node is not WeightedObject prop || !prop.BelongsTo(puzzleID)) continue;
                prop.CaptureAuthoredHome();
                if (!prop.HasAuthoredHome) continue;
                string propID = string.IsNullOrWhiteSpace(prop.PropID) ? prop.Name.ToString() : prop.PropID;
                if (_props.TryGetValue(propID, out PropRecord existing)) {
                    if (existing.Instance != null && IsInstanceValid(existing.Instance) && existing.Instance != prop) {
                        GD.PushError($"PuzzleResetStation '{StationID}': two live props share PropID '{propID}'.");
                        continue;
                    }
                    existing.Instance = prop;
                    continue;
                }
                var record = new PropRecord {
                    Instance = prop,
                    PropID = propID,
                    Home = prop.AuthoredHome,
                    ScenePath = prop.SceneFilePath,
                    ParentPath = prop.GetParent()?.GetPath(),
                    WeightUnits = prop.WeightUnits
                };
                foreach (Node child in prop.GetChildren()) {
                    if (child is CollisionShape2D shape && shape.Shape != null) {
                        record.Shapes.Add((shape.Shape, shape.Transform));
                    }
                }
                _props[propID] = record;
                _propOrder.Add(propID);
            }
        }

        /// <summary>The live prop registered under <paramref name="propID"/>, or null.</summary>
        public WeightedObject GetProp(string propID) =>
            _props.TryGetValue(propID ?? "", out PropRecord record)
            && record.Instance != null && IsInstanceValid(record.Instance)
                ? record.Instance
                : null;

        /// <summary>
        /// The whole V01b transaction. Validates actor, world state and solved state,
        /// then every restoration volume, and only then moves anything.
        /// </summary>
        public PuzzleResetResult TryReset(PlayerController player) {
            LastResult = Evaluate(player);
            return LastResult;
        }

        private PuzzleResetResult Evaluate(PlayerController player) {
            PuzzleManager puzzle = Puzzle;
            if (puzzle == null) return PuzzleResetResult.NoPuzzle;
            if (puzzle.IsSolvedOrPersisted) {
                ApplyPresentation();
                return PuzzleResetResult.Solved;
            }
            if (!IsActorEligible(player)) return PuzzleResetResult.InvalidActor;
            if (IsWorldStopped(player)) return PuzzleResetResult.WorldStopped;
            if (PhysicsCallbackGuard.IsInPhysicsCallback) return PuzzleResetResult.Busy;
            ulong frame = Engine.GetPhysicsFrames();
            if (frame == _lastResetPhysicsFrame) return PuzzleResetResult.DuplicateActivation;

            RefreshPropRegistry();
            if (IsAnyRestorationVolumeObstructed()) {
                EnvironmentNotice.Post(ObstructedNoticeKey, this);
                return PuzzleResetResult.Obstructed;
            }

            // Recheck at commit: a completion committed in the same tick wins.
            if (!puzzle.TryBeginArrangementReset(out int generation)) {
                ApplyPresentation();
                return PuzzleResetResult.Solved;
            }

            _lastResetPhysicsFrame = frame;
            var restored = new List<WeightedObject>(_propOrder.Count);
            foreach (string propID in _propOrder) {
                WeightedObject prop = EnsureInstance(_props[propID]);
                if (prop == null) continue;
                prop.RestoreToAuthoredHome();
                restored.Add(prop);
            }
            RecomputePlates(puzzle, restored);
            ResetCount++;
            puzzle.AnnounceArrangementReset(generation);
            EnvironmentNotice.Post(ResetNoticeKey, this);
            return PuzzleResetResult.Reset;
        }

        /// <summary>
        /// A hero may reset only while alive and in control: not dead, not in the
        /// death-rewind respawn, not suspended by a rewind.
        /// </summary>
        public static bool IsActorEligible(PlayerController player) =>
            player != null && IsInstanceValid(player)
            && player.CurrentState != CharacterState.Dead
            && player.CurrentState != CharacterState.Respawning;

        /// <summary>
        /// Time Freeze and the W1 world hold (Post-Landing Hold, boss suspension)
        /// stop puzzle interaction; checked from both the hero and the scene so a
        /// direct call cannot slip past either.
        /// </summary>
        public bool IsWorldStopped(PlayerController player) {
            if (player != null && (player.TimeFrozen || player.IsRecoveryWorldHeld)) return true;
            SceneTree tree = IsInsideTree() ? GetTree() : null;
            if (tree == null) return false;
            if (tree.GetFirstNodeInGroup(TimeFreezeController.ControllerGroup) is TimeFreezeController freeze
                && IsInstanceValid(freeze) && freeze.IsFrozen) {
                return true;
            }
            return ChronalRewindManager.IsWorldHeld(tree);
        }

        /// <summary>
        /// True when any registered prop's restoration volume overlaps an actor, a
        /// construct or a foreign puzzle weight. Side-effect free; pure shape math
        /// (<see cref="Shape2D.Collide"/>), so it does not depend on a physics step.
        /// </summary>
        public bool IsAnyRestorationVolumeObstructed() {
            if (!IsInsideTree()) return false;
            var ownProps = new HashSet<ulong>();
            foreach (PropRecord record in _props.Values) {
                if (record.Instance != null && IsInstanceValid(record.Instance)) {
                    ownProps.Add(record.Instance.GetInstanceId());
                }
            }
            var obstacles = new List<CollisionObject2D>();
            var seen = new HashSet<ulong>();
            SceneTree tree = GetTree();
            foreach (string group in ObstructionGroups) {
                Godot.Collections.Array<Node> members = tree.GetNodesInGroup(group);
                using var lifetime = members.AsDisposable();
                foreach (Node node in members) {
                    if (node is not CollisionObject2D body || !IsInstanceValid(body) || !body.IsInsideTree()) continue;
                    ulong id = body.GetInstanceId();
                    if (ownProps.Contains(id) || !seen.Add(id)) continue;
                    obstacles.Add(body);
                }
            }
            foreach (PropRecord record in _props.Values) {
                foreach ((Shape2D shape, Transform2D local) in record.Shapes) {
                    Transform2D volume = record.Home * local;
                    foreach (CollisionObject2D body in obstacles) {
                        if (Overlaps(shape, volume, body)) return true;
                    }
                }
            }
            return false;
        }

        private static bool Overlaps(Shape2D shape, Transform2D volume, CollisionObject2D body) {
            foreach (Node child in body.GetChildren()) {
                if (child is not CollisionShape2D other || other.Shape == null || other.Disabled) continue;
                if (shape.Collide(volume, other.Shape, other.GlobalTransform)) return true;
            }
            return false;
        }

        private WeightedObject EnsureInstance(PropRecord record) {
            if (record.Instance != null && IsInstanceValid(record.Instance) && record.Instance.IsInsideTree()) {
                return record.Instance;
            }
            Node parent = record.ParentPath != null && !record.ParentPath.IsEmpty
                ? GetNodeOrNull(record.ParentPath)
                : null;
            parent ??= GetParent();
            if (parent == null) return null;

            WeightedObject replacement = null;
            if (!string.IsNullOrEmpty(record.ScenePath)
                && ResourceLoader.Load<PackedScene>(record.ScenePath) is PackedScene packed) {
                replacement = packed.Instantiate() as WeightedObject;
            }
            if (replacement == null) {
                replacement = new WeightedObject();
                foreach ((Shape2D shape, Transform2D local) in record.Shapes) {
                    replacement.AddChild(new CollisionShape2D { Shape = shape, Transform = local });
                }
            }
            replacement.Name = $"{record.PropID}_restored";
            replacement.PropID = record.PropID;
            replacement.PuzzleOwnerID = PuzzleID;
            replacement.WeightUnits = record.WeightUnits;
            replacement.SetAuthoredHome(record.Home);
            parent.AddChild(replacement);
            replacement.GlobalTransform = record.Home;
            record.Instance = replacement;
            return replacement;
        }

        private void RecomputePlates(PuzzleManager puzzle, List<WeightedObject> restored) {
            Godot.Collections.Array<Node> objects = GetTree().GetNodesInGroup("puzzle_object");
            using var lifetime = objects.AsDisposable();
            foreach (Node node in objects) {
                if (node is PressurePlate plate && plate.ResolvePuzzleManager() == puzzle) {
                    plate.RecomputeAfterReset(restored);
                }
            }
        }

        private void ApplyPresentation() {
            bool live = Puzzle != null && !IsPuzzleSolved;
            if (GetNodeOrNull<CanvasItem>(VisualPath) is CanvasItem visual) visual.Visible = live;
            if (GetNodeOrNull<Label>("Sign") is Label sign) sign.Visible = live;
            if (GetNodeOrNull<Area2D>(InteractionAreaPath) is Area2D area) {
                area.SetMonitoringSafe(live);
                area.SetMonitorableSafe(live);
            }
        }

        /// <summary>Test seam: lets a test issue a second deliberate activation in the same tick.</summary>
        internal void ClearActivationLatchForTest() => _lastResetPhysicsFrame = ulong.MaxValue;

        /// <summary>
        /// Builds a station with its pedestal, sign and a real-shaped
        /// <see cref="InteractionArea"/> (an Area2D with no shape never fires).
        /// <paramref name="position"/> is the feet point on the floor.
        /// </summary>
        public static PuzzleResetStation Create(string stationID, NodePath puzzlePath, Vector2 position) {
            var station = new PuzzleResetStation {
                Name = "PuzzleResetStation",
                StationID = stationID,
                PuzzlePath = puzzlePath,
                Position = position
            };
            station.AddChild(new Polygon2D {
                Name = "Visual",
                Color = new Color(0.55f, 0.85f, 0.95f, 1f),
                // A squat pedestal with a notched top: shape, not colour alone.
                Polygon = new[] {
                    new Vector2(-26, 0), new Vector2(26, 0), new Vector2(20, -46),
                    new Vector2(8, -46), new Vector2(0, -56), new Vector2(-8, -46), new Vector2(-20, -46)
                }
            });
            var sign = new Label {
                Name = "Sign",
                Text = DefaultPromptKey,
                Position = new Vector2(-70, -84),
                CustomMinimumSize = new Vector2(140, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            sign.AddThemeFontSizeOverride("font_size", 12);
            station.AddChild(sign);

            var area = new InteractionArea {
                Name = "Interaction",
                TargetPath = "..",
                PromptLabelPath = "Prompt"
            };
            var size = new Vector2(140f, 160f);
            area.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = size },
                Position = new Vector2(0f, -size.Y / 2f)
            });
            var prompt = new Label {
                Name = "Prompt",
                Visible = false,
                Position = new Vector2(-80, -112),
                CustomMinimumSize = new Vector2(160, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            prompt.AddThemeFontSizeOverride("font_size", 11);
            area.AddChild(prompt);
            station.AddChild(area);
            return station;
        }
    }
}
