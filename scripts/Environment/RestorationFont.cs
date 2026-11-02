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
    /// exactly like every other interactable; <see cref="Create"/> builds the
    /// whole wellspring (D13, 2026-10-11: one is placed in every campaign level,
    /// design :2131-2132, ledger <c>PROVISIONAL-FONT-PLACEMENT</c>).
    ///
    /// F10 (STORY_PERSISTENCE :65-66, :77): a completed channel commits its use
    /// and its pending heal together through
    /// <see cref="StoryManager.CommitFontChannelCompletion"/>, every paid tick
    /// credits it, and the channeler's death cancels the remainder without
    /// refunding the use.
    /// </summary>
    public partial class RestorationFont : Node2D, IInteractable, IStoryTimeFreezable {
        /// <summary>Every placed font joins this group (content tests, Collapse Tremor safe zones).</summary>
        public const string Group = "restoration_font";

        [Export] public string FontID = "font";

        /// <summary>
        /// V7.3: the channel is anchored to the wellspring — a channeler who
        /// moves (or is knocked) beyond this range interrupts, refunding the
        /// use exactly like taking damage does. Without it a player could start
        /// the channel and walk toward the boss with the heal in flight.
        /// </summary>
        [Export] public float ChannelRangePixels = 90f;

        /// <summary>Channel hold before the restore begins.</summary>
        public const float ChannelSeconds = 1.5f;
        /// <summary>The completed channel restores HP over this window.</summary>
        public const float RestoreSeconds = 2.0f;

        /// <summary>
        /// Scene wiring: true once the level is complete (D13,
        /// <c>PROVISIONAL-FONT-PLACEMENT</c>) — completion re-keys the use
        /// registry to the next level, so a spent font would otherwise read
        /// fresh and commit into the next attempt. A channel running when it
        /// turns true is interrupted with a refund; a paid restore completes.
        /// Null = never refuses.
        /// </summary>
        public System.Func<bool> RefusesWhile { get; set; }

        private static readonly Color StoneColor = new(0.56f, 0.55f, 0.58f);
        private static readonly Color CrackColor = new(0.24f, 0.23f, 0.26f);

        private bool _channeling;
        private float _channelElapsed;
        private PlayerController _channeler;
        private int _hpBaseline;

        private float _restoreRemainingSeconds;
        private float _restorePerSecond;
        private float _restoreAccumulator;
        private int _restoreTotalRemaining;

        private Node2D _glow;
        private InteractionArea _interaction;
        private FTT.UI.RadialProgress _channelRadial;

        /// <summary>True while a channel is running. Test seam.</summary>
        public bool IsChanneling => _channeling;

        /// <summary>The channel's progress, 0..1; 0 when no channel runs. Test seam.</summary>
        public float ChannelProgress =>
            _channeling ? Mathf.Clamp(_channelElapsed / ChannelSeconds, 0f, 1f) : 0f;

        /// <summary>The channel display (the <see cref="ResonanceHoldNode"/> precedent), or null when not built. Test seam.</summary>
        public FTT.UI.RadialProgress ChannelRadial => _channelRadial;

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

        /// <summary>
        /// D13 (<c>PROVISIONAL-FONT-PLACEMENT</c>): refuses while its own restore
        /// runs (a second Easy use waits for the first to land instead of
        /// overwriting it) and while <see cref="RefusesWhile"/> says so. There
        /// is deliberately no boss-fight refusal.
        /// </summary>
        public bool CanInteract(PlayerController player) =>
            !_channeling && _restoreRemainingSeconds <= 0f && !(RefusesWhile?.Invoke() ?? false)
            && UsesRemaining > 0 && player != null
            && player.CurrentState != CharacterState.Dead;

        public void Interact(PlayerController player) {
            if (!CanInteract(player)) return;
            _channeling = true;
            _channelElapsed = 0f;
            _channeler = player;
            _hpBaseline = player.CurrentHP;
        }

        /// <summary>
        /// Builds a complete wellspring at <paramref name="position"/> (the
        /// floor top under it): a placeholder cracked-hourglass body, the gold
        /// <c>Glow</c> that dims per use, the <c>ChannelRadial</c>, and an
        /// <see cref="InteractionArea"/> with its prompt label (the
        /// <see cref="WardenBeacon"/> pattern).
        /// </summary>
        public static RestorationFont Create(string fontID, Vector2 position) {
            var font = new RestorationFont {
                Name = $"RestorationFont_{(fontID ?? "font").Replace('.', '_')}",
                FontID = fontID ?? "font",
                Position = position
            };
            font.AddChild(new Polygon2D {
                Name = "Visual",
                Polygon = new[] {
                    new Vector2(-20f, 0f), new Vector2(20f, 0f), new Vector2(20f, -8f),
                    new Vector2(6f, -45f), new Vector2(20f, -82f), new Vector2(20f, -90f),
                    new Vector2(-20f, -90f), new Vector2(-20f, -82f), new Vector2(-6f, -45f),
                    new Vector2(-20f, -8f)
                },
                Color = StoneColor
            });
            font.AddChild(new Line2D {
                Name = "Crack",
                Points = new[] {
                    new Vector2(12f, -86f), new Vector2(8f, -74f), new Vector2(13f, -66f),
                    new Vector2(9f, -56f)
                },
                Width = 2f,
                DefaultColor = CrackColor
            });
            font.AddChild(new Polygon2D {
                Name = "Glow",
                Polygon = new[] {
                    new Vector2(-13f, -8f), new Vector2(13f, -8f), new Vector2(0f, -36f)
                },
                Color = FTT.UI.UIPalette.ResonanceAura
            });
            Color fill = FTT.UI.UIPalette.ResonanceAura;
            fill.A = 1f;
            font.AddChild(new FTT.UI.RadialProgress {
                Name = "ChannelRadial",
                Size = new Vector2(36f, 36f),
                Position = new Vector2(-18f, -164f),
                Thickness = 4f,
                Fraction = 0f,
                FillColor = fill,
                Visible = false
            });
            var area = new InteractionArea {
                Name = "InteractionArea",
                TargetPath = "..",
                PromptLabelPath = "Prompt"
            };
            area.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(110f, 150f) },
                Position = new Vector2(0f, -70f)
            });
            area.AddChild(new Label {
                Name = "Prompt",
                Text = "",
                Position = new Vector2(-110f, -122f),
                CustomMinimumSize = new Vector2(220f, 26f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = false
            });
            font.AddChild(area);
            return font;
        }

        public override void _Ready() {
            // The font has no natural simulation group, so it joins the generic
            // one the Time Freeze sweep also walks: an in-progress channel must
            // stop (no healing during a freeze).
            AddToGroup(TimeFreezeController.FreezableGroup);
            AddToGroup(Group);
            _glow = GetNodeOrNull<Node2D>("Glow");
            _interaction = GetNodeOrNull<InteractionArea>("InteractionArea");
            _channelRadial = GetNodeOrNull<FTT.UI.RadialProgress>("ChannelRadial");
            if (EventBus.Instance != null) EventBus.Instance.OnPlayerDied += OnPlayerDied;
            UpdateGlow();
            ApplyChannelPresentation();
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) EventBus.Instance.OnPlayerDied -= OnPlayerDied;
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            float dt = (float)delta;
            AdvanceChannel(dt);
            AdvanceRestore(dt);
            ApplyChannelPresentation();
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
            // D13: the level completed mid-channel — the registry now reads the
            // next level, so the channel is interrupted (and, as with every
            // interrupt, the use is refunded by never being spent).
            if (RefusesWhile?.Invoke() == true) {
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
            // V7.3 range anchor: leaving the wellspring interrupts (and, as
            // with every interrupt, the use is refunded by never being spent).
            if (player.GlobalPosition.DistanceTo(GlobalPosition) > ChannelRangePixels) {
                InterruptChannel();
                return;
            }
            if (!player.CurrentInputFrame.IsHeld(GameplayButtons.Interact)) {
                InterruptChannel();
                return;
            }

            _channelElapsed += dt;
            if (_channelElapsed < ChannelSeconds) return;

            // Channel complete: commit the use and the pending heal together
            // (F10), then start the 2-second restore.
            _channeling = false;
            _channelElapsed = 0f;
            float fraction = StoryDifficultyTuning.GetRestorationFontFraction(
                StoryDifficultyTuning.CurrentStoryDifficulty);
            int total = StoryDifficultyTuning.ScaleHeal(player.MaximumHP, fraction);
            StoryManager.Instance?.CommitFontChannelCompletion(RegistryKey, total, RestoreSeconds);
            _restoreRemainingSeconds = RestoreSeconds;
            _restorePerSecond = total / RestoreSeconds;
            _restoreAccumulator = 0f;
            _restoreTotalRemaining = total;
            UpdateGlow();
            _interaction?.RefreshPrompt();
        }

        private void AdvanceRestore(float dt) {
            if (_restoreRemainingSeconds <= 0f) return;
            PlayerController player = _channeler;
            if (player == null || !IsInstanceValid(player) || player.CurrentState == CharacterState.Dead) {
                CancelRestore();
                return;
            }
            // Lingering-kit pass (plan 1.8, provisional): Venom blocks healing, so a completed channel's
            // pending heal waits (neither the trickle clock nor the budget advance) and applies once Venom ends.
            if (player.IsHealingBlocked) return;
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
                StoryManager.Instance?.CreditPendingHealing(whole);
            }
            if (_restoreRemainingSeconds <= 0f) {
                _channeler = null;
                _interaction?.RefreshPrompt();
            }
        }

        /// <summary>
        /// A death recovery replacing HP cancels the pending heal <b>without</b>
        /// refunding the use (F10, STORY_PERSISTENCE :77).
        /// </summary>
        private void CancelRestore() {
            _restoreRemainingSeconds = 0f;
            _restoreTotalRemaining = 0;
            _restoreAccumulator = 0f;
            _channeler = null;
            StoryManager.Instance?.CancelPendingHealingForRecovery();
            _interaction?.RefreshPrompt();
        }

        private void InterruptChannel() {
            _channeling = false;
            _channelElapsed = 0f;
            _channeler = null;
            _interaction?.RefreshPrompt();
            ApplyChannelPresentation();
        }

        /// <summary>
        /// T01a (design :1507): the channeler's death interrupts a channel (a
        /// refund) or cancels a running restore (no refund). Event delivery is
        /// not stopped by the freeze latch, so this runs even while a death
        /// rewind holds the font — whose own tick would never see <c>Dead</c>.
        /// </summary>
        private void OnPlayerDied(int playerIndex) {
            if (_channeler == null || !IsInstanceValid(_channeler)
                || _channeler.PlayerIndex != playerIndex) return;
            if (_channeling) {
                InterruptChannel();
                return;
            }
            if (_restoreRemainingSeconds > 0f) CancelRestore();
        }

        /// <summary>The channel radial is written by this font only.</summary>
        private void ApplyChannelPresentation() {
            if (_channelRadial == null || !IsInstanceValid(_channelRadial)) return;
            _channelRadial.Visible = _channeling;
            _channelRadial.Fraction = ChannelProgress;
        }

        /// <summary>Remaining uses read at a glance: dims per use, dark when spent.</summary>
        private void UpdateGlow() {
            if (_glow == null) return;
            int authored = StoryDifficultyTuning.GetRestorationFontUses(
                StoryDifficultyTuning.CurrentStoryDifficulty);
            float share = authored > 0 ? UsesRemaining / (float)authored : 0f;
            _glow.Modulate = new Color(1f, 1f, 1f, 0.15f + 0.85f * share);
        }

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
