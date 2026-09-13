using Godot;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>
    /// Package 11 A3 (V7.6 F12): a checkpoint's authored role. The Integrity
    /// freeze, the Hard "middle inactive" rule and self-activation all read
    /// this — <b>never</b> the checkpoint's numeric ID suffix, its array
    /// position or the level's checkpoint count. F12 is explicit: a saved
    /// <c>_1</c> must never be reinterpreted as PreBoss, which is exactly what
    /// would strand Level 4A (whose PreBoss anchor IS <c>_checkpoint_1</c>)
    /// under the Hard middle rule.
    ///
    /// Append-only: the ordinals are exported into authored scenes.
    /// </summary>
    public enum CheckpointRole {
        /// <summary>The level entrance. Self-activates once on fresh entry; never locks the clock.</summary>
        Entry = 0,
        /// <summary>A mid-route fracture. Struck to activate; inert on Hard in Acts I-II; never locks the clock.</summary>
        Middle = 1,
        /// <summary>The pre-boss fracture. Struck to activate; freezes Timeline Integrity permanently.</summary>
        PreBoss = 2
    }

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

        /// <summary>
        /// Package 11 A3 (V7.6 F12): the authored role, exported ALONGSIDE the
        /// stable ID. Self-activation, the Hard middle rule and the Integrity
        /// freeze all read this and never the ID suffix.
        /// </summary>
        [Export] public CheckpointRole Role = CheckpointRole.Entry;

        /// <summary>
        /// F12: an inert anchor still exists in the world (so the fracture is
        /// visible and the geometry is unchanged) but cannot be activated,
        /// cannot anchor a respawn and saves nothing. Hard's middle checkpoint
        /// in Acts I-II is the only current user; A3b enables Act III's.
        /// </summary>
        [Export] public bool Inert;

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
            // F12: an inert anchor is not a recovery destination. It saves
            // nothing, heals nothing, refreshes nothing and never locks the
            // clock — the earlier real anchor stays the destination.
            if (Inert) return;
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
            // V7.6 F12: the PreBoss fracture — identified by its authored ROLE,
            // never by a `_checkpoint_2` spelling — freezes Timeline Integrity
            // permanently for this attempt and banks the final score. Entry and
            // Middle only bank the F11 recovery allowance. Both happen BEFORE
            // the committed half so the save always records the post-lock gauge.
            if (Role == CheckpointRole.PreBoss) FTT.Core.StoryManager.Instance?.LockIntegrityAtPreBoss();
            else FTT.Core.StoryManager.Instance?.BankCheckpointIntegrity();
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
                // An inert anchor reads dead grey: present in the world, but
                // visibly not a fracture this difficulty can stabilize.
                visual.Modulate = Inert
                    ? new Color(0.4f, 0.42f, 0.45f)
                    : IsActivated ? Colors.White : new Color(1f, 0.55f, 0.3f);
            }
            if (_strikePrompt != null) {
                _strikePrompt.Visible = _playerNearby && !IsActivated && !SelfActivating && !Inert;
            }
        }
    }
}
