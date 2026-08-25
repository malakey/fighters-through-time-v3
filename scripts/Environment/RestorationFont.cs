using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// V7.2 Restoration Font — a chronal wellspring (a cracked hourglass
    /// monument leaking golden sand), one per level between the mid checkpoint
    /// and the boss. Hold Interact for 1.5 s to channel; taking any damage
    /// interrupts the channel and refunds the use. Completion restores HP over
    /// 2 seconds. Uses and potency scale by difficulty (Easy 2 × 50% /
    /// Normal 1 × 50% / Hard 1 × 25%).
    ///
    /// The spent state persists through rewinds and Timeline Collapse — the
    /// consumed-use registry lives on <see cref="StoryManager"/>, keyed by
    /// level + FontID — and resets only on a full Restart Level or a fresh
    /// level entry. The glow dims per use, dark when spent.
    ///
    /// Pair with an <see cref="InteractionArea"/> child pointing at this node,
    /// exactly like every other interactable.
    /// </summary>
    public partial class RestorationFont : Node2D, IInteractable {
        [Export] public string FontID = "font";

        /// <summary>Channel hold before the restore begins.</summary>
        public const float ChannelSeconds = 1.5f;
        /// <summary>The completed channel restores HP over this window.</summary>
        public const float RestoreSeconds = 2.0f;

        private bool _channeling;
        private float _channelElapsed;
        private PlayerController _channeler;
        private int _hpBaseline;

        private float _restoreRemainingSeconds;
        private float _restorePerSecond;
        private float _restoreAccumulator;
        private int _restoreTotalRemaining;

        private Node2D _glow;

        /// <summary>True while a channel is running. Test seam.</summary>
        public bool IsChanneling => _channeling;

        /// <summary>Uses left at the current difficulty. Test seam.</summary>
        public int UsesRemaining {
            get {
                int authored = StoryDifficultyTuning.GetRestorationFontUses(
                    StoryDifficultyTuning.CurrentStoryDifficulty);
                int used = StoryManager.Instance?.GetFontUsesConsumed(RegistryKey) ?? 0;
                return Mathf.Max(0, authored - used);
            }
        }

        private string RegistryKey =>
            $"{StoryManager.Instance?.CurrentLevel.ToString() ?? "level"}:{FontID}";

        public string InteractionID => $"restoration_font_{FontID}";
        public string PromptKey => "font_channel_prompt";

        public bool CanInteract(PlayerController player) =>
            !_channeling && UsesRemaining > 0 && player != null
            && player.CurrentState != CharacterState.Dead;

        public void Interact(PlayerController player) {
            if (!CanInteract(player)) return;
            _channeling = true;
            _channelElapsed = 0f;
            _channeler = player;
            _hpBaseline = player.CurrentHP;
        }

        public override void _Ready() {
            _glow = GetNodeOrNull<Node2D>("Glow");
            UpdateGlow();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            AdvanceChannel(dt);
            AdvanceRestore(dt);
        }

        private void AdvanceChannel(float dt) {
            if (!_channeling) return;
            PlayerController player = _channeler;
            if (player == null || !IsInstanceValid(player)
                || player.CurrentState is CharacterState.Dead or CharacterState.Stunned
                    or CharacterState.Dazed) {
                InterruptChannel();
                return;
            }
            // Taking any damage interrupts the channel and refunds the use
            // (the use is only consumed on completion, so "refund" is simply
            // not consuming it). A heal mid-channel raises the baseline.
            if (player.CurrentHP < _hpBaseline) {
                InterruptChannel();
                return;
            }
            _hpBaseline = Mathf.Max(_hpBaseline, player.CurrentHP);
            if (!player.CurrentInputFrame.IsHeld(GameplayButtons.Interact)) {
                InterruptChannel();
                return;
            }

            _channelElapsed += dt;
            if (_channelElapsed < ChannelSeconds) return;

            // Channel complete: consume the use and start the 2-second restore.
            _channeling = false;
            StoryManager.Instance?.RecordFontUse(RegistryKey);
            float fraction = StoryDifficultyTuning.GetRestorationFontFraction(
                StoryDifficultyTuning.CurrentStoryDifficulty);
            int total = StoryDifficultyTuning.ScaleHeal(player.MaximumHP, fraction);
            _restoreRemainingSeconds = RestoreSeconds;
            _restorePerSecond = total / RestoreSeconds;
            _restoreAccumulator = 0f;
            _restoreTotalRemaining = total;
            UpdateGlow();
        }

        private void AdvanceRestore(float dt) {
            if (_restoreRemainingSeconds <= 0f) return;
            PlayerController player = _channeler;
            if (player == null || !IsInstanceValid(player) || player.CurrentState == CharacterState.Dead) {
                _restoreRemainingSeconds = 0f;
                _restoreTotalRemaining = 0;
                _channeler = null;
                return;
            }
            float step = Mathf.Min(dt, _restoreRemainingSeconds);
            _restoreRemainingSeconds -= step;
            _restoreAccumulator += _restorePerSecond * step;
            // The integer budget is authoritative: fractional accumulation
            // paces the trickle, the budget guarantees the exact total.
            int whole = Mathf.FloorToInt(_restoreAccumulator);
            if (_restoreRemainingSeconds <= 0f) whole = _restoreTotalRemaining;
            whole = Mathf.Min(whole, _restoreTotalRemaining);
            if (whole > 0) {
                _restoreAccumulator -= whole;
                _restoreTotalRemaining -= whole;
                player.HealStory(whole);
            }
            if (_restoreRemainingSeconds <= 0f) _channeler = null;
        }

        private void InterruptChannel() {
            _channeling = false;
            _channelElapsed = 0f;
            _channeler = null;
        }

        /// <summary>Remaining uses read at a glance: dims per use, dark when spent.</summary>
        private void UpdateGlow() {
            if (_glow == null) return;
            int authored = StoryDifficultyTuning.GetRestorationFontUses(
                StoryDifficultyTuning.CurrentStoryDifficulty);
            float share = authored > 0 ? UsesRemaining / (float)authored : 0f;
            _glow.Modulate = new Color(1f, 1f, 1f, 0.15f + 0.85f * share);
        }
    }
}
