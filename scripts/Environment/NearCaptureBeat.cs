using System;
using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 13 W4 (S36, design §6 "The near-capture"): Level 9's scripted Eraser
    /// near-capture during the night stealth.
    ///
    /// <para>A single Eraser's Siphon Snare catches the hero: the aura gutters grey
    /// under the Suppression smother presentation
    /// (<see cref="FTT.Combat.GlowPresentationController.SetAuraSmothered"/>, the
    /// same channel the Null Lance drives) and a cradle silhouette begins to close.
    /// Sarah: "It's trying to finish what the beam started — pull away!". The
    /// Level 0 prompt returns — "Hold away from the light" — and the player tears
    /// free by holding away from the Eraser, which withdraws into a rift. Then the
    /// hero and Sarah trade the two after-lines.</para>
    ///
    /// <para><b>It cannot be failed and costs nothing</b>: no HP, no rewind charge,
    /// no meter and no Integrity. The Eraser is a presentation silhouette, never an
    /// <c>EnemyController</c> — it has no hitbox, no dust source and no place in the
    /// locked encounter economy — and the struggle releases on its own after
    /// <see cref="AutoReleaseSeconds"/> if the player never pulls. The Integrity
    /// clock holds for the whole beat through
    /// <see cref="IntegrityClockPause.ScriptedBeat"/>, and the struggle holds the
    /// world with the <c>DialogueManager</c> idiom (take the tree pause only
    /// if nobody else holds it, hand back only what was taken). Both are released
    /// on every exit path, <c>_ExitTree</c> included.</para>
    ///
    /// <para>Phases: <see cref="NearCapturePhase.Idle"/> →
    /// <see cref="NearCapturePhase.Snared"/> (Sarah's line) →
    /// <see cref="NearCapturePhase.Struggle"/> (the prompt) →
    /// <see cref="NearCapturePhase.Released"/> (the after-lines) →
    /// <see cref="NearCapturePhase.Done"/>. The dialogue is started and observed
    /// through two injected delegates so the level keeps its own dialogue
    /// idiom and tests can drive the beat without a dialogue box.</para>
    /// </summary>
    public partial class NearCaptureBeat : Node2D {

        public enum NearCapturePhase { Idle, Snared, Struggle, Released, Done }

        public const string SnaredDialogueID = "level_09.near_capture";
        public const string ReleasedDialogueID = "level_09.near_capture_after";

        /// <summary>The Level 0 prompt, restated for this beat (design §6).</summary>
        public const string PromptKey = "dlg_l09_near_capture_prompt";

        /// <summary>Seconds the player must hold away from the Eraser to tear free.</summary>
        public const float TearFreeHoldSeconds = 0.75f;

        /// <summary>The beat is unfailable: the snare lets go on its own after this long.</summary>
        public const float AutoReleaseSeconds = 4f;

        /// <summary>A held direction counts once its strength clears this.</summary>
        public const float HoldThreshold = 0.5f;

        /// <summary>Where the Eraser stands relative to the hero (to the east, so "away" is west).</summary>
        public static readonly Vector2 EraserOffset = new(260f, 0f);

        public NearCapturePhase Phase { get; private set; } = NearCapturePhase.Idle;

        /// <summary>+1 when the Eraser is east of the hero; the held direction must be the opposite.</summary>
        public int EraserSide { get; private set; } = 1;

        /// <summary>Seconds of away-input accumulated during the struggle.</summary>
        public float HeldAwaySeconds { get; private set; }

        /// <summary>Seconds spent in the struggle.</summary>
        public float StruggleSeconds { get; private set; }

        /// <summary>True when the player pulled free (false when the snare auto-released).</summary>
        public bool ToreFreeByInput { get; private set; }

        /// <summary>Starts a dialogue sequence; returns true when it is on screen.</summary>
        public Func<string, bool> StartDialogue { get; set; }

        public PlayerController Target { get; private set; }

        public Node2D EraserSilhouette { get; private set; }
        public Line2D Tether { get; private set; }
        public Node2D Cradle { get; private set; }
        public Label Prompt { get; private set; }

        private bool _clockHeld;
        private bool _pausedTree;
        private bool _smothered;

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
        }

        /// <summary>
        /// Snares <paramref name="player"/>. Idempotent: returns false unless the
        /// beat is idle. Works with no dialogue service (headless): the snared line
        /// is skipped and the struggle starts at once.
        /// </summary>
        public bool Begin(PlayerController player) {
            if (Phase != NearCapturePhase.Idle) return false;
            Target = player;
            Phase = NearCapturePhase.Snared;
            HoldClock(true);
            SetSmothered(true);
            if (player != null && IsInstanceValid(player)) GlobalPosition = player.GlobalPosition;
            BuildPresentation();
            if (StartDialogue?.Invoke(SnaredDialogueID) != true) EnterStruggle();
            return true;
        }

        /// <summary>Routed from the level's dialogue-complete hook (base IDs).</summary>
        public void OnDialogueComplete(string dialogueID) {
            if (dialogueID == SnaredDialogueID && Phase == NearCapturePhase.Snared) {
                EnterStruggle();
            } else if (dialogueID == ReleasedDialogueID && Phase == NearCapturePhase.Released) {
                Finish();
            }
        }

        private void EnterStruggle() {
            Phase = NearCapturePhase.Struggle;
            HeldAwaySeconds = 0f;
            StruggleSeconds = 0f;
            if (Prompt != null) Prompt.Visible = true;
            SceneTree tree = IsInsideTree() ? GetTree() : null;
            if (tree != null && !tree.Paused) {
                tree.Paused = true;
                _pausedTree = true;
            }
        }

        public override void _Process(double delta) {
            if (Phase != NearCapturePhase.Struggle) return;
            float away = EraserSide > 0
                ? Input.GetActionStrength(InputManager.Actions.MoveLeft)
                : Input.GetActionStrength(InputManager.Actions.MoveRight);
            AdvanceStruggle((float)delta, away);
        }

        /// <summary>
        /// One struggle step. <paramref name="awayStrength"/> is the held strength
        /// in the direction away from the Eraser. Public so the rule is provable
        /// without an input device. Returns true on the step that tears free.
        /// </summary>
        public bool AdvanceStruggle(float delta, float awayStrength) {
            if (Phase != NearCapturePhase.Struggle) return false;
            float step = Mathf.Max(0f, delta);
            StruggleSeconds += step;
            if (awayStrength >= HoldThreshold) HeldAwaySeconds += step;
            if (HeldAwaySeconds >= TearFreeHoldSeconds) {
                ToreFreeByInput = true;
                TearFree();
                return true;
            }
            if (StruggleSeconds >= AutoReleaseSeconds) {
                TearFree();
                return true;
            }
            if (Cradle != null) {
                // The cradle keeps closing until the pull wins.
                float closing = Mathf.Clamp(1f - StruggleSeconds / AutoReleaseSeconds, 0.35f, 1f);
                Cradle.Scale = new Vector2(closing, 1f);
            }
            return false;
        }

        private void TearFree() {
            Phase = NearCapturePhase.Released;
            ReleaseTreePause();
            SetSmothered(false);
            if (Prompt != null) Prompt.Visible = false;
            if (Tether != null) Tether.Visible = false;
            if (Cradle != null) Cradle.Visible = false;
            // The Eraser withdraws into a rift.
            if (EraserSilhouette != null && IsInsideTree()) {
                Tween withdraw = CreateTween();
                withdraw.TweenProperty(EraserSilhouette, "modulate:a", 0f, 0.5);
            } else if (EraserSilhouette != null) {
                EraserSilhouette.Visible = false;
            }
            if (StartDialogue?.Invoke(ReleasedDialogueID) != true) Finish();
        }

        private void Finish() {
            Phase = NearCapturePhase.Done;
            ReleaseTreePause();
            SetSmothered(false);
            HoldClock(false);
        }

        public override void _ExitTree() {
            ReleaseTreePause();
            SetSmothered(false);
            HoldClock(false);
        }

        private void HoldClock(bool held) {
            if (_clockHeld == held) return;
            _clockHeld = held;
            StoryManager.Instance?.SetIntegrityClockPause(IntegrityClockPause.ScriptedBeat, held);
        }

        private void ReleaseTreePause() {
            if (!_pausedTree) return;
            _pausedTree = false;
            SceneTree tree = IsInsideTree() ? GetTree() : null;
            if (tree != null) tree.Paused = false;
        }

        private void SetSmothered(bool smothered) {
            if (_smothered == smothered) return;
            _smothered = smothered;
            if (Target != null && IsInstanceValid(Target)) Target.Glow?.SetAuraSmothered(smothered);
        }

        /// <summary>True while the beat holds the hero's aura smothered. Test seam.</summary>
        public bool IsSmothering => _smothered;

        /// <summary>True while the beat holds the Integrity clock. Test seam.</summary>
        public bool IsHoldingClock => _clockHeld;

        private void BuildPresentation() {
            if (EraserSilhouette != null) return;

            // The Eraser: lean, visored, cold. A silhouette, never an enemy body.
            EraserSilhouette = new Node2D { Name = "EraserSilhouette", Position = EraserOffset * EraserSide };
            AddChild(EraserSilhouette);
            EraserSilhouette.AddChild(new ColorRect {
                Name = "Body",
                Size = new Vector2(30f, 88f),
                Position = new Vector2(-15f, -88f),
                Color = new Color(0.06f, 0.07f, 0.1f, 0.95f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            EraserSilhouette.AddChild(new ColorRect {
                Name = "Visor",
                Size = new Vector2(22f, 5f),
                Position = new Vector2(-11f, -76f),
                Color = FTT.UI.UIPalette.UnboundCold,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            // The Siphon Snare's tether, from the Eraser to the hero.
            Tether = new Line2D {
                Name = "SnareTether",
                Width = 4f,
                DefaultColor = FTT.UI.UIPalette.UnboundColdDischarge,
                Points = new[] { EraserSilhouette.Position + new Vector2(0f, -50f), new Vector2(0f, -50f) }
            };
            AddChild(Tether);

            // The cradle silhouette closing around the hero: jagged cold shards.
            Cradle = new Node2D { Name = "CradleSilhouette" };
            AddChild(Cradle);
            float[] offsets = { -46f, -30f, 24f, 40f };
            for (int index = 0; index < offsets.Length; index++) {
                Cradle.AddChild(new ColorRect {
                    Name = $"CradleShard{index}",
                    Size = new Vector2(8f, 120f),
                    Position = new Vector2(offsets[index], -120f),
                    Color = FTT.UI.UIPalette.UnboundColdDim,
                    RotationDegrees = offsets[index] < 0f ? 8f : -8f,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }

            Prompt = new Label {
                Name = "NearCapturePrompt",
                Text = TranslationServer.Translate(PromptKey),
                Position = new Vector2(-140f, -190f),
                CustomMinimumSize = new Vector2(280f, 20f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            Prompt.AddThemeFontSizeOverride("font_size", 16);
            Prompt.AddThemeColorOverride("font_color", FTT.UI.UIPalette.ResonanceGoldBright);
            AddChild(Prompt);
        }
    }
}
