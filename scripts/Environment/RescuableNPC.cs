using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Interactable civilian/prisoner placeholder (Level 4 Paris cells, Level 6
    /// Pompeii evacuation). Interacting flashes the freed tint, despawns the NPC
    /// after <see cref="FreedFlashSeconds"/>, optionally sets a
    /// <see cref="PuzzleManager"/> condition, and raises
    /// <see cref="RescuedEventHandler"/> for the level's objective counter.
    /// </summary>
    public partial class RescuableNPC : Node2D, IInteractable, IStoryRewindable, IStoryTimeFreezable {
        [Signal] public delegate void RescuedEventHandler(string npcID);
        [Signal] public delegate void DespawnedEventHandler(string npcID);

        [Export] public string NpcID = "";
        [Export] public string InteractionPromptKey = "interaction_rescue";
        [Export] public NodePath PuzzleManagerPath;
        /// <summary>Optional; leave blank when the NPC only feeds an objective counter.</summary>
        [Export] public string ConditionID = "";
        [Export] public NodePath VisualPath = "Visual";
        [Export] public NodePath InteractionAreaPath = "InteractionArea";
        [Export] public Color FreedFlashColor = new(1f, 0.98f, 0.72f, 1f);
        [Export] public Color CaptiveColor = new(0.62f, 0.6f, 0.7f, 1f);
        [Export(PropertyHint.Range, "0,10,0.05")] public float FreedFlashSeconds = 0.35f;
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.RestoreCheckpointState;

        private float _flashTimer;
        private bool _checkpointRescued;

        public bool IsRescued { get; private set; }
        public bool IsDespawned { get; private set; }
        public string InteractionID => NpcID;
        public string PromptKey => InteractionPromptKey;

        public override void _Ready() {
            AddToGroup("rescuable_npc");
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached += CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered += OnRewind;
            }
            ApplyPresentation();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointReached -= CaptureCheckpointState;
                EventBus.Instance.OnRewindTriggered -= OnRewind;
            }
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            if (!IsRescued || IsDespawned) return;
            _flashTimer -= (float)delta;
            if (_flashTimer <= 0f) CompleteDespawn();
        }

        public bool CanInteract(PlayerController player) => player != null && !IsRescued;
        public void Interact(PlayerController player) => Rescue();

        public bool Rescue() {
            if (IsRescued) return false;
            IsRescued = true;
            _flashTimer = FreedFlashSeconds;
            ApplyPresentation();
            if (!string.IsNullOrWhiteSpace(ConditionID)) {
                GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, true);
            }
            EmitSignal(SignalName.Rescued, NpcID);
            if (FreedFlashSeconds <= 0f) CompleteDespawn();
            return true;
        }

        /// <summary>Ends the freed flash and hides the placeholder actor.</summary>
        public void CompleteDespawn() {
            if (IsDespawned) return;
            IsDespawned = true;
            ApplyPresentation();
            EmitSignal(SignalName.Despawned, NpcID);
        }

        public void CaptureCheckpointState(string checkpointID) => _checkpointRescued = IsRescued;

        public void ApplyStoryRewind() {
            if (RewindPolicy == StoryRewindPolicy.PreserveCurrentState) return;
            IsRescued = RewindPolicy == StoryRewindPolicy.RestoreCheckpointState && _checkpointRescued;
            IsDespawned = IsRescued;
            _flashTimer = 0f;
            ApplyPresentation();
        }

        private void ApplyPresentation() {
            Visible = !IsDespawned;
            if (GetNodeOrNull<CanvasItem>(VisualPath) is CanvasItem visual) {
                visual.Modulate = IsRescued ? FreedFlashColor : CaptiveColor;
            }
            if (GetNodeOrNull<Area2D>(InteractionAreaPath) is Area2D area) {
                area.Monitoring = !IsRescued;
                area.Monitorable = !IsRescued;
            }
        }

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();

        // === IStoryTimeFreezable (V7.6 Time Freeze) ===========================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the world. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Stops simulating in place, preserving every timer. For a pickup this
        /// is also what enforces "no pickup collection during a freeze": the
        /// magnet that resolves a collection lives in the frozen tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

    }
}
