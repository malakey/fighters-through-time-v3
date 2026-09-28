using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// The N01 <b>sealing anchor</b>: the deliberate single-Interact "Seal
    /// Timeline — Complete Level" action that ends a boss level.
    ///
    /// <para>Born as Level 15's Alexandria Prime Anchor (the Temporal Core
    /// insertion that gates the ending) and generalised by Package 12 W8
    /// (GAP-05, D12(a)) into the reusable anchor every boss level and every
    /// Level 4A places beside its boss pickup. Level 15 keeps its scene-authored
    /// Prime Anchor and its own keys; every other level builds one through
    /// <see cref="CreateSealingAnchor"/> from <see cref="StoryLevelControllerBase"/>.</para>
    ///
    /// <para><b>Armed state is now persisted, not derived.</b> The anchor itself
    /// still holds no save state — it is inert until <see cref="Arm"/> — but
    /// the level commits AwaitingSeal into the F10 attempt record
    /// (<c>attemptState.sealReadiness</c>) at the boss defeat, and a load before
    /// the seal rebuilds the boss as defeated and arms the anchor again. The
    /// Package 11 rule "a quit between the boss and the seal repeats the fight"
    /// is retired.</para>
    ///
    /// <para>A single press: no hold, channel, meter cost or confirmation. It
    /// refuses while the hero is dead, Time Freeze holds the world, or the
    /// Post-Landing Hold / a T01b suspension does — nothing queues. It grants
    /// no invulnerability, healing, checkpoint Mending, rewind refill or
    /// Integrity benefit.</para>
    /// </summary>
    public partial class TemporalCoreAnchor : Node2D, IInteractable, IStoryRewindable {

        /// <summary>Raised once, when the seal is accepted (the Core goes in).</summary>
        [Signal] public delegate void CoreInsertedEventHandler(string anchorID);

        /// <summary>Raised once, when the anchor becomes usable.</summary>
        [Signal] public delegate void AnchorArmedEventHandler(string anchorID);

        [Export] public string AnchorID = "";
        /// <summary>Prompt shown once the anchor is armed.</summary>
        [Export] public string InteractionPromptKey = "alexandria_interaction_insert_core";
        /// <summary>Signage while the anchor is still sealed behind the boss.</summary>
        [Export] public string DormantLabelKey = "alexandria_anchor_dormant";
        /// <summary>Signage once the boss is down and the seal can be made.</summary>
        [Export] public string ArmedLabelKey = "alexandria_anchor_ready";
        [Export] public NodePath VisualPath = "Visual";
        [Export] public NodePath LabelPath = "StateLabel";
        [Export] public NodePath InteractionAreaPath = "InteractionArea";
        [Export] public Color DormantColor = new(0.30f, 0.32f, 0.44f, 1f);
        [Export] public Color ArmedColor = new(0.35f, 0.85f, 0.95f, 1f);
        [Export] public Color RestoredColor = new(1f, 0.92f, 0.62f, 1f);

        /// <summary>
        /// The seal is terminal, so it deliberately survives a Chronal Rewind:
        /// rewinding after it must not undo the completion already committed.
        /// </summary>
        [Export] public StoryRewindPolicy RewindPolicy { get; set; } = StoryRewindPolicy.PreserveCurrentState;

        public bool IsArmed { get; private set; }
        public bool IsInserted { get; private set; }

        public string InteractionID => AnchorID;
        public string PromptKey => InteractionPromptKey;

        /// <summary>
        /// Builds the generic sealing anchor a campaign level places beside its
        /// boss pickup: a marked column, its signage, and an interaction box
        /// tall enough to reach a hero standing on the arena floor under it.
        /// </summary>
        public static TemporalCoreAnchor CreateSealingAnchor(string anchorID, Vector2 position) {
            var anchor = new TemporalCoreAnchor {
                Name = "SealingAnchor",
                AnchorID = anchorID ?? "",
                Position = position,
                InteractionPromptKey = StoryLevelControllerBase.SealPromptKey,
                DormantLabelKey = StoryLevelControllerBase.SealAnchorDormantKey,
                ArmedLabelKey = StoryLevelControllerBase.SealAnchorReadyKey
            };
            anchor.AddChild(new Polygon2D {
                Name = "Visual",
                Polygon = new[] {
                    new Vector2(-22f, 50f), new Vector2(22f, 50f),
                    new Vector2(14f, -90f), new Vector2(0f, -120f), new Vector2(-14f, -90f)
                },
                Color = Colors.White
            });
            var label = new Label {
                Name = "StateLabel",
                Position = new Vector2(-110f, -160f),
                CustomMinimumSize = new Vector2(220f, 18f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            anchor.AddChild(label);
            var area = new InteractionArea { Name = "InteractionArea", TargetPath = "..", PromptLabelPath = "Prompt" };
            area.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(220f, 320f) },
                Position = new Vector2(0f, -40f)
            });
            var prompt = new Label {
                Name = "Prompt",
                Position = new Vector2(-130f, -190f),
                CustomMinimumSize = new Vector2(260f, 18f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            prompt.AddThemeFontSizeOverride("font_size", 12);
            area.AddChild(prompt);
            anchor.AddChild(area);
            return anchor;
        }

        public override void _Ready() {
            AddToGroup("puzzle_object");
            if (EventBus.Instance != null) EventBus.Instance.OnRewindTriggered += OnRewind;
            ApplyPresentation();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) EventBus.Instance.OnRewindTriggered -= OnRewind;
        }

        /// <summary>
        /// N01 availability: armed, not yet sealed, and a hero who is alive and
        /// not held by Time Freeze, the Post-Landing Hold or a T01b suspension.
        /// </summary>
        public bool CanInteract(PlayerController player) =>
            player != null && IsArmed && !IsInserted
            && player.CurrentState != CharacterState.Dead
            && !player.TimeFrozen
            && !player.IsRecoveryWorldHeld
            && !(IsInsideTree() && ChronalRewindManager.IsWorldHeld(GetTree()));

        public void Interact(PlayerController player) {
            if (!CanInteract(player)) return;
            InsertCore();
        }

        /// <summary>Opens the anchor. Idempotent.</summary>
        public bool Arm() {
            if (IsArmed) return false;
            IsArmed = true;
            ApplyPresentation();
            EmitSignal(SignalName.AnchorArmed, AnchorID);
            return true;
        }

        /// <summary>
        /// Accepts the seal. Refuses while the anchor is dormant, so completion
        /// cannot fire before the boss is down. Idempotent.
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
        /// the boss is dead and only ever sealed once, and undoing either on a
        /// rewind would strand a player who has already committed the level.
        /// </summary>
        public void CaptureCheckpointState(string checkpointID) { }

        /// <summary>Presentation refresh only; the armed/sealed state is preserved.</summary>
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
                area.SetMonitoringSafe(IsArmed && !IsInserted);
                area.SetMonitorableSafe(IsArmed && !IsInserted);
            }
        }
    }
}
