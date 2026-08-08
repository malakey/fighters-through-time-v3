using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// The Alexandria Prime Anchor (Level 15 only). The campaign does not end when
    /// the Apex Eraser dies — design-godot.md 3389 gates the ending cinematic on
    /// "defeating the Apex Eraser final boss <b>and</b> inserting the Temporal Core
    /// into the Alexandria Anchor". This is that insertion: a deliberate player
    /// action through the shared <see cref="IInteractable"/> path, not a cutscene.
    ///
    /// <para><b>Armed state is derived, never persisted.</b> The anchor is inert
    /// until <see cref="Arm"/> is called (Level 15 calls it the instant the boss is
    /// down), and nothing writes an "anchor is armed" flag to the save. A run that
    /// quits between the boss dying and the Core going in therefore resumes at the
    /// pre-boss checkpoint with the Eraser alive again — the ending is reachable by
    /// replaying the fight, and can never be stranded behind a transient flag that
    /// the save did not keep.</para>
    ///
    /// Bespoke to the finale rather than a shared toolkit component: it exists to
    /// gate exactly one chain and carries no reusable authoring surface.
    /// </summary>
    public partial class TemporalCoreAnchor : Node2D, IInteractable, IStoryRewindable {

        /// <summary>Raised once, when the Core actually goes in.</summary>
        [Signal] public delegate void CoreInsertedEventHandler(string anchorID);

        /// <summary>Raised once, when the anchor becomes usable.</summary>
        [Signal] public delegate void AnchorArmedEventHandler(string anchorID);

        [Export] public string AnchorID = "";
        /// <summary>Prompt shown once the anchor is armed.</summary>
        [Export] public string InteractionPromptKey = "alexandria_interaction_insert_core";
        /// <summary>Signage while the anchor is still sealed behind the Eraser.</summary>
        [Export] public string DormantLabelKey = "alexandria_anchor_dormant";
        /// <summary>Signage once the Eraser is down and the Core can go in.</summary>
        [Export] public string ArmedLabelKey = "alexandria_anchor_ready";
        [Export] public NodePath VisualPath = "Visual";
        [Export] public NodePath LabelPath = "StateLabel";
        [Export] public NodePath InteractionAreaPath = "InteractionArea";
        [Export] public Color DormantColor = new(0.30f, 0.32f, 0.44f, 1f);
        [Export] public Color ArmedColor = new(0.35f, 0.85f, 0.95f, 1f);
        [Export] public Color RestoredColor = new(1f, 0.92f, 0.62f, 1f);

        /// <summary>
        /// The restoration is the campaign's terminal action, so it deliberately
        /// survives a Chronal Rewind: rewinding after the Core is in must not undo
        /// the ending that is already rolling.
        /// </summary>
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public bool IsArmed { get; private set; }
        public bool IsInserted { get; private set; }

        public string InteractionID => AnchorID;
        public string PromptKey => InteractionPromptKey;

        public override void _Ready() {
            AddToGroup("puzzle_object");
            if (EventBus.Instance != null) EventBus.Instance.OnRewindTriggered += OnRewind;
            ApplyPresentation();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) EventBus.Instance.OnRewindTriggered -= OnRewind;
        }

        public bool CanInteract(PlayerController player) => player != null && IsArmed && !IsInserted;

        public void Interact(PlayerController player) => InsertCore();

        /// <summary>Opens the anchor. Idempotent.</summary>
        public bool Arm() {
            if (IsArmed) return false;
            IsArmed = true;
            ApplyPresentation();
            EmitSignal(SignalName.AnchorArmed, AnchorID);
            return true;
        }

        /// <summary>
        /// Deposits the accumulated temporal energy. Refuses while the anchor is
        /// dormant, so the ending cannot fire before the Eraser is down. Idempotent.
        /// </summary>
        public bool InsertCore() {
            if (!IsArmed || IsInserted) return false;
            IsInserted = true;
            ApplyPresentation();
            EmitSignal(SignalName.CoreInserted, AnchorID);
            return true;
        }

        /// <summary>
        /// Terminal state: nothing to capture. The anchor is only ever armed after
        /// the boss is dead and only ever restored once, and undoing either on a
        /// rewind would strand a player who is already watching the ending.
        /// </summary>
        public void CaptureCheckpointState(string checkpointID) { }

        /// <summary>Presentation refresh only; the armed/restored state is preserved.</summary>
        public void ApplyStoryRewind() => ApplyPresentation();

        private void OnRewind(Vector2 targetPosition) => ApplyStoryRewind();

        private void ApplyPresentation() {
            if (GetNodeOrNull<CanvasItem>(VisualPath) is CanvasItem visual) {
                visual.Modulate = IsInserted ? RestoredColor : IsArmed ? ArmedColor : DormantColor;
            }
            if (GetNodeOrNull<Label>(LabelPath) is Label label) {
                label.Text = Tr(IsArmed ? ArmedLabelKey : DormantLabelKey);
                label.Visible = !IsInserted;
            }
            if (GetNodeOrNull<Area2D>(InteractionAreaPath) is Area2D area) {
                area.Monitoring = IsArmed && !IsInserted;
                area.Monitorable = IsArmed && !IsInserted;
            }
        }
    }
}
