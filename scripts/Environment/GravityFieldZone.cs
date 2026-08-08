using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Scales Story gravity for players inside it (Level 12 Lunar low gravity,
    /// Level 13 Chronal Void shifts, Level 14 containment pockets). Leaving the
    /// zone — or freeing it — restores the player's remaining stack, which is 1.0
    /// when this was the only field.
    ///
    /// With two or more <see cref="CycleScales"/> the field cycles on
    /// <see cref="CycleSeconds"/>, tinting its visual during the final
    /// <see cref="TelegraphSeconds"/> before each shift. Fighter Mode is untouched.
    /// </summary>
    public partial class GravityFieldZone : Area2D, IStoryRewindable {
        [Signal] public delegate void GravityShiftedEventHandler(float scale);
        [Signal] public delegate void TelegraphStartedEventHandler(float nextScale);

        [Export] public string FieldID = "";
        /// <summary>Static scale used when <see cref="CycleScales"/> has fewer than two entries.</summary>
        [Export(PropertyHint.Range, "0,4,0.01")] public float GravityScale = 0.35f;
        /// <summary>Two or more entries turn the field into a telegraphed cycling field.</summary>
        [Export] public float[] CycleScales = Array.Empty<float>();
        [Export(PropertyHint.Range, "0.1,600,0.1")] public float CycleSeconds = 6f;
        [Export(PropertyHint.Range, "0,30,0.05")] public float TelegraphSeconds = 1f;
        [Export] public Color TelegraphTint = new(1f, 0.86f, 0.35f, 0.55f);
        [Export] public Color IdleTint = new(0.45f, 0.55f, 1f, 0.25f);
        [Export] public NodePath FieldVisualPath = "Visual";
        [Export] public bool Enabled = true;

        /// <summary>Translation key for the template's placeholder sign; blank hides it.</summary>
        [Export] public string LabelKey = "toolkit_gravity_field";
        [Export] public NodePath LabelPath = ToolkitLabel.DefaultLabelPath;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private readonly HashSet<PlayerController> _inside = new();
        private float _cycleTimer;
        private int _checkpointIndex;
        private float _checkpointTimer;

        public bool IsCycling => CycleScales != null && CycleScales.Length >= 2;
        public int CycleIndex { get; private set; }
        public float CurrentScale { get; private set; }
        public bool IsTelegraphing { get; private set; }
        public int TrackedPlayerCount => _inside.Count;
        public float NextScale => IsCycling
            ? CycleScales[(CycleIndex + 1) % CycleScales.Length]
            : CurrentScale;

        public override void _Ready() {
            AddToGroup("story_hazard");
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            CurrentScale = IsCycling ? CycleScales[0] : GravityScale;
            _cycleTimer = CycleSeconds;
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyTint();
            ToolkitLabel.Apply(this, LabelPath, LabelKey);
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEntered;
            BodyExited -= OnBodyExited;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
            EnvironmentPlayerModifiers.ClearSource(this);
            _inside.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            if (!Enabled || !IsCycling) return;
            _cycleTimer -= (float)delta;
            bool shouldTelegraph = _cycleTimer <= TelegraphSeconds;
            if (shouldTelegraph != IsTelegraphing) {
                IsTelegraphing = shouldTelegraph;
                ApplyTint();
                if (IsTelegraphing) EmitSignal(SignalName.TelegraphStarted, NextScale);
            }
            if (_cycleTimer <= 0f) AdvanceCycle();
        }

        public void AdvanceCycle() {
            if (!IsCycling) return;
            CycleIndex = (CycleIndex + 1) % CycleScales.Length;
            _cycleTimer = CycleSeconds;
            IsTelegraphing = false;
            SetScale(CycleScales[CycleIndex]);
            ApplyTint();
        }

        public void SetScale(float scale) {
            CurrentScale = Mathf.Max(0f, scale);
            foreach (PlayerController player in _inside) {
                EnvironmentPlayerModifiers.SetGravityScale(player, this, CurrentScale);
            }
            EmitSignal(SignalName.GravityShifted, CurrentScale);
        }

        public bool AddPlayer(PlayerController player) {
            if (player == null || !_inside.Add(player)) return false;
            EnvironmentPlayerModifiers.SetGravityScale(player, this, CurrentScale);
            return true;
        }

        public bool RemovePlayer(PlayerController player) {
            if (player == null || !_inside.Remove(player)) return false;
            EnvironmentPlayerModifiers.ClearGravityScale(player, this);
            return true;
        }

        // === Frame-zero / post-teleport registration ===

        /// <summary>
        /// True when <paramref name="globalPoint"/> falls inside any of this field's
        /// own <see cref="CollisionShape2D"/> children. Rectangles and circles only —
        /// every authored field is one of the two, and an exotic shape reads as "not
        /// containing" rather than guessing.
        /// </summary>
        public bool ContainsPoint(Vector2 globalPoint) {
            Godot.Collections.Array<Node> children = GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is not CollisionShape2D collision || collision.Disabled) continue;
                Vector2 local = collision.ToLocal(globalPoint);
                switch (collision.Shape) {
                    case RectangleShape2D rectangle:
                        Vector2 half = rectangle.Size / 2f;
                        if (Mathf.Abs(local.X) <= half.X && Mathf.Abs(local.Y) <= half.Y) return true;
                        break;
                    case CircleShape2D circle:
                        if (local.LengthSquared() <= circle.Radius * circle.Radius) return true;
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// Puts <paramref name="player"/> into exactly <paramref name="target"/> and
        /// out of every other field in <paramref name="fields"/>, then returns the
        /// target. Pass null to release the player from all of them.
        ///
        /// <para><b>Why this exists.</b> An <see cref="Area2D"/> only reports its
        /// authored initial overlaps on the first physics frame, and a checkpoint
        /// resume, a Chronal Rewind, or a scripted teleport moves the player without
        /// one ever running — so a resumed player stands in a low-gravity level at
        /// Earth-normal gravity, or walks out of a pocket still carrying its scale.
        /// Call this from <c>OnLevelReady</c> and again on
        /// <c>EventBus.OnRewindTriggered</c>. It is idempotent with the physics
        /// callbacks that follow: <see cref="AddPlayer"/>/<see cref="RemovePlayer"/>
        /// are set operations. (Level 12 found this; levels 13 and 14 inherit it.)</para>
        /// </summary>
        public static GravityFieldZone SyncPlayerToField(
            IEnumerable<GravityFieldZone> fields, PlayerController player, GravityFieldZone target) {
            if (fields == null || player == null || !IsInstanceValid(player)) return null;
            foreach (GravityFieldZone field in fields) {
                if (field == null || !IsInstanceValid(field)) continue;
                if (field == target) field.AddPlayer(player);
                else field.RemovePlayer(player);
            }
            return target != null && IsInstanceValid(target) ? target : null;
        }

        /// <summary>
        /// <see cref="SyncPlayerToField"/> against the first field whose own
        /// collision geometry contains the player. Use this when the fields are
        /// scene-authored and there is no span table to consult; a level that owns an
        /// authored span table should pass its own lookup to
        /// <see cref="SyncPlayerToField"/> so the boundary rule stays in one place.
        /// </summary>
        public static GravityFieldZone SyncPlayerToContainingField(
            IEnumerable<GravityFieldZone> fields, PlayerController player) {
            if (fields == null || player == null || !IsInstanceValid(player)) return null;
            GravityFieldZone target = null;
            foreach (GravityFieldZone field in fields) {
                if (field == null || !IsInstanceValid(field)) continue;
                if (field.ContainsPoint(player.GlobalPosition)) { target = field; break; }
            }
            return SyncPlayerToField(fields, player, target);
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointIndex = CycleIndex;
            _checkpointTimer = _cycleTimer;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool reset = RewindPolicy == StoryRewindPolicy.ResetToInitialState;
            CycleIndex = reset ? 0 : _checkpointIndex;
            _cycleTimer = reset ? CycleSeconds : _checkpointTimer;
            IsTelegraphing = false;
            SetScale(IsCycling ? CycleScales[CycleIndex] : GravityScale);
            ApplyTint();
        }

        private void ApplyTint() {
            if (GetNodeOrNull<CanvasItem>(FieldVisualPath) is CanvasItem visual) {
                visual.Modulate = IsTelegraphing ? TelegraphTint : IdleTint;
            }
        }

        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) AddPlayer(player); }
        private void OnBodyExited(Node2D body) { if (body is PlayerController player) RemovePlayer(player); }
        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();
    }
}
