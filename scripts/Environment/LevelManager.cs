using Godot;
using System.Collections.Generic;

namespace FTT.Environment {

    public partial class LevelManager : Node {
        [Export] public string LevelID = "";
        [Export] public string LevelDisplayName = "";

        private string _lastCheckpointID;
        public string LastCheckpointID => _lastCheckpointID ?? "";
        private Vector2 _lastCheckpointPosition;
        private List<string> _activatedCheckpoints = new();
        private readonly Dictionary<string, Vector2> _checkpointPositions = new();

        public override void _Ready() {
            FTT.Core.EventBus.Instance.OnCheckpointReached += OnCheckpointActivated;
        }

        public override void _ExitTree() {
            if (FTT.Core.EventBus.Instance != null)
                FTT.Core.EventBus.Instance.OnCheckpointReached -= OnCheckpointActivated;
        }

        private void OnCheckpointActivated(string checkpointID) {
            if (!_activatedCheckpoints.Contains(checkpointID)) {
                _activatedCheckpoints.Add(checkpointID);
            }
            _lastCheckpointID = checkpointID;
        }

        public void SetCheckpointPosition(string id, Vector2 pos) {
            RegisterCheckpoint(id, pos);
            _lastCheckpointID = id;
            _lastCheckpointPosition = pos;
        }

        public void RegisterCheckpoint(string id, Vector2 position) {
            if (!string.IsNullOrWhiteSpace(id)) _checkpointPositions[id] = position;
        }

        public bool TryGetCheckpointPosition(string id, out Vector2 position) {
            position = default;
            return !string.IsNullOrWhiteSpace(id) && _checkpointPositions.TryGetValue(id, out position);
        }

        public Vector2 GetRespawnPosition() {
            return _lastCheckpointPosition;
        }

        public void CompleteLevel() {
            FTT.Core.EventBus.Instance?.RaiseLevelComplete(LevelID);
        }
    }

    /// <summary>
    /// V7.3 strike-to-activate Chronal Fracture (design "Chronal Rift
    /// Checkpoints"): the Area2D is now only a proximity prompt; activation
    /// requires striking the fracture's PersistentObject-layer surface with a
    /// Standard Attack or Special (the EnvironmentHurtboxAdapter pattern the
    /// Extractor uses — the interim walk-through triggers were a recorded
    /// divergence). The entry checkpoint self-activates on walk-through
    /// (<see cref="SelfActivating"/>), since the player just arrived through
    /// it. Once-per-attempt facts (the Mending heal, the rewind refresh) are
    /// gated by <see cref="FTT.Core.StoryManager.TryActivateCheckpoint"/>;
    /// respawn anchor and save update on every activation.
    /// </summary>
    public partial class CheckpointTrigger : Area2D {
        [Export] public string CheckpointID = "";
        [Export] public Vector2 RespawnOffset = new(0, -50);
        /// <summary>Entry checkpoints self-activate — the player just arrived through them.</summary>
        [Export] public bool SelfActivating;

        /// <summary>Local visual state; the once-per-attempt authority is StoryManager's registry.</summary>
        public bool IsActivated { get; private set; }

        private Label _strikePrompt;
        private bool _playerNearby;

        public override void _Ready() {
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
            BuildStrikeSurface();
            BuildStrikePrompt();
            // A mid-level resume rebuilds the scene: fractures the attempt
            // already stabilized come back in their active state.
            if (FTT.Core.StoryManager.Instance?.IsCheckpointActivated(CheckpointID) == true) {
                IsActivated = true;
            }
            UpdatePresentation();
        }

        /// <summary>
        /// The strikeable surface: a PersistentObject-layer hurtbox, which the
        /// player's hitbox mask (EnemyHurtbox | PersistentObject) already
        /// covers. Enemy hitboxes share the -1 owner index and are skipped by
        /// the Hitbox pipeline, and <see cref="HandleStrike"/> additionally
        /// requires a player attacker, so mobs can never stabilize a fracture.
        /// </summary>
        private void BuildStrikeSurface() {
            var surface = new EnvironmentHurtboxAdapter {
                Name = "StrikeSurface",
                OwnerPlayerIndex = -1,
                CollisionLayer = FTT.Core.CollisionLayers.PersistentObject,
                CollisionMask = FTT.Core.CollisionLayers.PlayerHitbox,
                Monitoring = true,
                Monitorable = true
            };
            surface.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(60, 120) },
                Position = new Vector2(0, -60)
            });
            surface.OnHit += HandleStrike;
            AddChild(surface);
        }

        private void BuildStrikePrompt() {
            _strikePrompt = new Label {
                Name = "StrikePrompt",
                Text = Tr("checkpoint_strike_prompt"),
                Position = new Vector2(-90, -150),
                CustomMinimumSize = new Vector2(180, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            _strikePrompt.AddThemeFontSizeOverride("font_size", 10);
            _strikePrompt.AddThemeColorOverride("font_color", new Color(1f, 0.65f, 0.3f));
            AddChild(_strikePrompt);
        }

        /// <summary>Hurtbox receiver for the activating strike. Test seam.</summary>
        internal float HandleStrike(FTT.Combat.HitPayload payload) {
            // Only the player's own attacks discharge chronal energy into the
            // fracture; enemy and unattributed hits pass through.
            if (payload.AttackerIndex >= 0) Activate();
            return 0f;
        }

        private void OnBodyEntered(Node2D body) {
            if (body is not FTT.Characters.PlayerController) return;
            _playerNearby = true;
            if (SelfActivating && !IsActivated) {
                Activate();
                return;
            }
            UpdatePresentation();
        }

        private void OnBodyExited(Node2D body) {
            if (body is not FTT.Characters.PlayerController) return;
            _playerNearby = false;
            UpdatePresentation();
        }

        /// <summary>Test seam mirroring the proximity signal.</summary>
        internal void HandleBodyEntered(Node2D body) => OnBodyEntered(body);

        /// <summary>
        /// Stabilizes the fracture. Respawn anchor and the checkpoint save
        /// update on EVERY activation; the Mending heal and the rewind-pool
        /// refresh fire only on the first activation this attempt (V7.2 rule,
        /// made authoritative by StoryManager's persisted registry in V7.3).
        /// </summary>
        public void Activate() {
            bool firstActivation = FTT.Core.StoryManager.Instance?.TryActivateCheckpoint(CheckpointID) ?? !IsActivated;
            IsActivated = true;
            var levelManager = GetTree()?.CurrentScene?.GetNodeOrNull<LevelManager>("LevelManager");
            levelManager?.SetCheckpointPosition(CheckpointID, GlobalPosition + RespawnOffset);
            if (firstActivation
                && GetTree()?.GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                // V7.2 Checkpoint Mending: activating a Chronal Fracture
                // restores HP once per checkpoint per level attempt.
                int heal = FTT.Core.StoryDifficultyTuning.ScaleHeal(
                    player.MaximumHP,
                    FTT.Core.StoryDifficultyTuning.GetCheckpointMendingFraction(
                        FTT.Core.StoryDifficultyTuning.CurrentStoryDifficulty));
                if (heal > 0) player.HealStory(heal);
            }
            // Gameplay half first (state capture, rewind refresh, HUD)...
            FTT.Core.EventBus.Instance?.RaiseCheckpointReached(CheckpointID, firstActivation);
            // ...then the persistence half, so the save is always post-refresh.
            FTT.Core.EventBus.Instance?.RaiseCheckpointCommitted(CheckpointID);
            UpdatePresentation();
        }

        /// <summary>Inactive fractures read dull orange with a strike prompt in
        /// range; the stabilized rift shows its authored cyan.</summary>
        private void UpdatePresentation() {
            if (GetNodeOrNull<CanvasItem>("CheckpointVisual") is CanvasItem visual) {
                visual.Modulate = IsActivated ? Colors.White : new Color(1f, 0.55f, 0.3f);
            }
            if (_strikePrompt != null) {
                _strikePrompt.Visible = _playerNearby && !IsActivated && !SelfActivating;
            }
        }
    }
}
