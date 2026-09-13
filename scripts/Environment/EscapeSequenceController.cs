using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Advancing-hazard escape run (Level 6 Pompeii lava front, Level 15 collapse).
    /// A child damage-front Area2D marches from <see cref="StartX"/> toward
    /// <see cref="EndX"/> at <see cref="AdvanceSpeed"/>. Being caught costs
    /// <see cref="CatchDamage"/> plus forward knockback — never an instant kill, so
    /// Chronal Rewind stays the meaningful failure state.
    /// </summary>
    public partial class EscapeSequenceController : Node2D, IStoryRewindable, IStoryTimeFreezable {
        [Signal] public delegate void EscapeStartedEventHandler();
        [Signal] public delegate void EscapeCompletedEventHandler();
        [Signal] public delegate void PlayerCaughtEventHandler(int playerIndex, int damage);
        [Signal] public delegate void FrontAdvancedEventHandler(float frontX);

        [Export] public string SequenceID = "";
        /// <summary>Global X where the damage front starts.</summary>
        [Export] public float StartX;
        /// <summary>Global X where the damage front stops advancing.</summary>
        [Export] public float EndX = 6000f;
        /// <summary>Global X the player must cross to clear the sequence.</summary>
        [Export] public float FinishX = 6200f;
        [Export(PropertyHint.Range, "1,4000,1")] public float AdvanceSpeed = 220f;
        [Export(PropertyHint.Range, "0,500,1")] public int CatchDamage = 25;
        [Export] public Vector2 CatchKnockback = new(520f, -260f);
        [Export(PropertyHint.Range, "0,30,0.05")] public float CatchCooldownSeconds = 1f;
        [Export] public NodePath DamageFrontPath = "DamageFront";
        [Export] public bool AutoBegin;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private readonly Dictionary<PlayerController, float> _catchCooldowns = new();
        /// <summary>Reused per-frame key scratch; AdvanceFront must not allocate at 60 Hz.</summary>
        private readonly List<PlayerController> _cooldownScratch = new();
        private float _checkpointFrontX;
        private bool _checkpointRunning;

        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public float FrontX { get; private set; }
        public int CatchCount { get; private set; }
        /// <summary>Seconds until the front reaches <see cref="EndX"/> at the authored speed.</summary>
        public float RemainingSeconds => Mathf.Max(0f, (EndX - FrontX) / Mathf.Max(0.01f, AdvanceSpeed));
        public Area2D DamageFront => GetNodeOrNull<Area2D>(DamageFrontPath);

        public override void _Ready() {
            AddToGroup("story_hazard");
            FrontX = StartX;
            ApplyFrontPosition();
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            if (AutoBegin) Begin();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
            _catchCooldowns.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            AdvanceFront((float)delta);
        }

        public void AdvanceFront(float dt) {
            _cooldownScratch.Clear();
            _cooldownScratch.AddRange(_catchCooldowns.Keys);
            foreach (PlayerController player in _cooldownScratch) {
                float remaining = _catchCooldowns[player] - dt;
                if (remaining <= 0f) _catchCooldowns.Remove(player);
                else _catchCooldowns[player] = remaining;
            }
            if (!IsRunning) return;

            FrontX = Mathf.Min(EndX, FrontX + AdvanceSpeed * dt);
            ApplyFrontPosition();
            EmitSignal(SignalName.FrontAdvanced, FrontX);

            Area2D front = DamageFront;
            if (front == null) return;
            // Audit Low: this was the one undisposed overlap query in the repo,
            // leaking an engine collection wrapper every physics frame of the
            // Pompeii/Level-15 escape beats. See the AGENTS.md disposal rule.
            Godot.Collections.Array<Node2D> bodies = front.GetOverlappingBodies();
            using var lifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body is not PlayerController player) continue;
                if (player.GlobalPosition.X >= FinishX) { CompleteEscape(); return; }
                CatchPlayer(player);
            }
        }

        public void Begin() {
            if (IsRunning) return;
            IsRunning = true;
            IsCompleted = false;
            EmitSignal(SignalName.EscapeStarted);
        }

        public void Halt() {
            if (!IsRunning) return;
            IsRunning = false;
        }

        public void ResetSequence() {
            IsRunning = false;
            IsCompleted = false;
            FrontX = StartX;
            CatchCount = 0;
            _catchCooldowns.Clear();
            ApplyFrontPosition();
        }

        public bool CatchPlayer(PlayerController player) {
            if (player == null || !GodotObject.IsInstanceValid(player)) return false;
            if (_catchCooldowns.ContainsKey(player)) return false;
            _catchCooldowns[player] = CatchCooldownSeconds;
            CatchCount++;
            // V7.3: environmental chokepoint — Defy/echo/meter accounting.
            int applied = player.ApplyEnvironmentalDamage(CatchDamage);
            float direction = player.GlobalPosition.X >= FrontX ? 1f : -1f;
            player.Velocity += new Vector2(CatchKnockback.X * direction, CatchKnockback.Y);
            EmitSignal(SignalName.PlayerCaught, player.PlayerIndex, applied);
            EventBus.Instance?.RaiseHazardStateChanged(new HazardStatePayload {
                HazardID = SequenceID,
                Phase = HazardPhase.Active,
                Duration = 0f
            });
            return true;
        }

        /// <summary>Called by the finish trigger (or a level script) when the player is clear.</summary>
        public bool CompleteEscape() {
            if (IsCompleted) return false;
            IsCompleted = true;
            IsRunning = false;
            EmitSignal(SignalName.EscapeCompleted);
            return true;
        }

        /// <summary>Finish-trigger entry point: completes only once the player is past <see cref="FinishX"/>.</summary>
        public bool NotifyPlayerReachedFinish(PlayerController player) {
            if (player == null || player.GlobalPosition.X < FinishX) return false;
            return CompleteEscape();
        }

        public void CaptureCheckpointState(string checkpointID) {
            _checkpointFrontX = FrontX;
            _checkpointRunning = IsRunning;
        }

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            bool reset = RewindPolicy == StoryRewindPolicy.ResetToInitialState;
            FrontX = reset ? StartX : _checkpointFrontX;
            IsRunning = !reset && _checkpointRunning;
            IsCompleted = false;
            _catchCooldowns.Clear();
            ApplyFrontPosition();
        }

        private void ApplyFrontPosition() {
            if (DamageFront is not Area2D front) return;
            front.GlobalPosition = new Vector2(FrontX, front.GlobalPosition.Y);
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();

        // === IStoryTimeFreezable (V7.6 Time Freeze) ===========================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the world. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Stops simulating in place. Nothing else is mutated, so the phase, the
        /// timer and the position all survive and resume with no catch-up tick —
        /// the collision shape stays live throughout.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

    }
}
