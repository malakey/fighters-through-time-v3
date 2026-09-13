using System;
using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Why an armed Nexus authorization was cleared. Presentation and tests read it;
    /// the rules are identical for every reason — the source simply becomes available
    /// again once the player is back in the safe setup.
    /// </summary>
    public enum NexusArmingClearReason {
        None = 0,
        LeftCastArea = 1,
        PlayerDown = 2,
        CastInterrupted = 3,
        Reloaded = 4,
        WorldSuspended = 5,
        Resolved = 6
    }

    /// <summary>
    /// F04 Nexus Resonance Source (V7.6, Package 11 A12) — the puzzle-only Ultimate
    /// authorization at Level 4A's required Ultimate set-piece. The hero draws power
    /// from their own nexus moment, so the normal Ultimate input casts the character's
    /// real Ultimate against the designated puzzle target <b>at any meter value,
    /// including zero</b>.
    ///
    /// <para>The rules this class owns, verbatim from the design:</para>
    /// <list type="bullet">
    ///   <item>Armed only by Interact, only in a safe cleared area — the preceding
    ///     encounter cleared, no live enemies, no meter-drain hazards.</item>
    ///   <item>While armed it takes priority over ordinary meter spending and
    ///     <b>neither fills nor consumes the meter</b> — the meter reads exactly the
    ///     same before and after the cast.</item>
    ///   <item>Not a carried charge: leaving the bounded cast area, death, reload, or
    ///     an interrupted cast clears the arming. The source re-arms on return, with
    ///     unlimited free retries.</item>
    ///   <item>Successful resolution latches the gate, disables the source, and
    ///     records completion.</item>
    ///   <item><b>Never saved armed</b> (F10): an unsolved gate restores an available
    ///     source; a solved gate keeps it disabled.</item>
    ///   <item>Time Freeze can neither arm the source nor cast through it.</item>
    ///   <item>It never lights the F13 Defy seal — the meter it leaves behind is the
    ///     meter the player already had.</item>
    /// </list>
    ///
    /// <para>Story-only by construction. Nothing here is referenced from
    /// <c>scripts/FighterSim/</c>, and the interception it drives lives in
    /// <see cref="PlayerController"/>'s Story ultimate-input path alone.</para>
    ///
    /// <para>Pair with an <see cref="InteractionArea"/> child pointing at this node,
    /// exactly like every other interactable.</para>
    /// </summary>
    public partial class NexusResonanceSource : Node2D, IInteractable {

        [Export] public string SourceID = "nexus_source";

        /// <summary>
        /// The bounded cast area. Leaving it clears the arming; the design calls this
        /// out explicitly so the authorization can never be carried to the boss.
        /// </summary>
        [Export] public float CastAreaRadius = 260f;

        /// <summary>The hero Ultimate this source authorizes (<c>einstein_cosmological_constant</c>).</summary>
        [Export] public string RequiredUltimateAbilityID = "";

        /// <summary>
        /// The world-suspension probe. A2's Time Freeze does not exist in this
        /// worktree, so the gate is expressed as a hook rather than a hard reference:
        /// at merge, <c>TimeFreezeController</c> wires its active flag here and the
        /// "Time Freeze cannot arm the source or cast the Ultimate" rule is live with
        /// no change to this class. Default: the world is never suspended.
        /// </summary>
        public static Func<bool> WorldTimeSuspendedProbe { get; set; } = static () => false;

        /// <summary>The gate this source resolves. Set by the level controller.</summary>
        public LegacyKitGate Gate { get; set; }

        /// <summary>
        /// Set by the level while the approach encounter is live or a meter-drain
        /// hazard is running in the cast area. The source refuses to arm while true.
        /// </summary>
        public bool CastAreaContested { get; set; }

        public bool IsArmed { get; private set; }
        public bool IsSolved { get; private set; }

        /// <summary>Free retries are unlimited; this is a diagnostic, never a limit.</summary>
        public int ArmCount { get; private set; }

        public NexusArmingClearReason LastClearReason { get; private set; } = NexusArmingClearReason.None;

        /// <summary>Raised when the designated puzzle target resolves.</summary>
        public event Action<NexusResonanceSource> Solved;

        private PlayerController _armedPlayer;
        private ColorRect _visual;
        private Label _prompt;

        public string InteractionID => $"nexus_resonance_source_{SourceID}";
        public string PromptKey => "nexus_source_prompt";

        /// <summary>
        /// True when the source can be armed right now: unsolved, not already armed,
        /// the area is safe and uncontested, the world is running, and the player is
        /// alive and inside the cast area.
        /// </summary>
        public bool CanInteract(PlayerController player) =>
            IsAvailable
            && !IsArmed
            && player != null
            && IsInstanceValid(player)
            && player.CurrentState is not (CharacterState.Dead or CharacterState.Respawning)
            && IsInsideCastArea(player);

        /// <summary>Unsolved, uncontested, and the world is not suspended.</summary>
        public bool IsAvailable => !IsSolved && !CastAreaContested && !WorldTimeSuspended;

        private static bool WorldTimeSuspended {
            get {
                try { return WorldTimeSuspendedProbe?.Invoke() == true; }
                catch { return false; }
            }
        }

        public void Interact(PlayerController player) {
            if (!CanInteract(player)) return;
            IsArmed = true;
            ArmCount++;
            LastClearReason = NexusArmingClearReason.None;
            _armedPlayer = player;
            player.NexusUltimateSource = this;
            UpdatePresentation();
        }

        public override void _Ready() {
            BuildPresentation();
            // F10: never saved armed. A reconstructed scene starts disarmed by
            // construction; a solved gate additionally starts disabled.
            IsArmed = false;
            UpdatePresentation();
        }

        public override void _ExitTree() {
            // The player outlives the level scene in a room reload, so the
            // authorization must never survive this node.
            if (_armedPlayer != null && IsInstanceValid(_armedPlayer)
                && _armedPlayer.NexusUltimateSource == this) {
                _armedPlayer.NexusUltimateSource = null;
            }
            _armedPlayer = null;
            IsArmed = false;
        }

        public override void _PhysicsProcess(double delta) {
            if (!IsArmed) return;
            if (_armedPlayer == null || !IsInstanceValid(_armedPlayer)) {
                ClearArming(NexusArmingClearReason.PlayerDown);
                return;
            }
            if (_armedPlayer.CurrentState is CharacterState.Dead or CharacterState.Respawning) {
                ClearArming(NexusArmingClearReason.PlayerDown);
                return;
            }
            if (WorldTimeSuspended) {
                ClearArming(NexusArmingClearReason.WorldSuspended);
                return;
            }
            if (!IsInsideCastArea(_armedPlayer)) {
                ClearArming(NexusArmingClearReason.LeftCastArea);
            }
        }

        public bool IsInsideCastArea(Node2D body) =>
            body != null && IsInstanceValid(body)
            && body.GlobalPosition.DistanceTo(GlobalPosition) <= CastAreaRadius;

        /// <summary>
        /// The authorization check <see cref="PlayerController"/> consults on the
        /// Ultimate input. Armed, available, and the cast is the authored Ultimate.
        /// </summary>
        public bool AuthorizesUltimate(string abilityID) =>
            IsArmed
            && IsAvailable
            && (string.IsNullOrWhiteSpace(RequiredUltimateAbilityID)
                || string.Equals(abilityID, RequiredUltimateAbilityID, StringComparison.Ordinal));

        /// <summary>
        /// The cast landed on the designated puzzle target: latch the gate, disable
        /// the source, record completion. Awards nothing — no dust, no meter, no
        /// healing, no Defy/Echo Step supply — which is what keeps a free unlimited
        /// retry loop from being a resource faucet.
        /// </summary>
        public bool ResolvePuzzleTarget() {
            if (!IsArmed || IsSolved) return false;
            IsSolved = true;
            ClearArming(NexusArmingClearReason.Resolved);
            Gate?.TryResolve(RequiredUltimateAbilityID);
            Solved?.Invoke(this);
            UpdatePresentation();
            return true;
        }

        /// <summary>An interrupted cast clears the arming and refunds nothing (there is nothing to refund).</summary>
        public void NotifyCastInterrupted() {
            if (IsArmed) ClearArming(NexusArmingClearReason.CastInterrupted);
        }

        /// <summary>
        /// F10 reconstruction: a checkpoint reload restores an <b>available</b> source
        /// for an unsolved gate and a <b>disabled</b> one for a solved gate — never an
        /// armed one.
        /// </summary>
        public void RestoreFromCheckpoint(bool gateSolved) {
            IsSolved = gateSolved;
            ClearArming(NexusArmingClearReason.Reloaded);
            if (gateSolved) Gate?.ForceResolve();
            UpdatePresentation();
        }

        public void ClearArming(NexusArmingClearReason reason) {
            if (_armedPlayer != null && IsInstanceValid(_armedPlayer)
                && _armedPlayer.NexusUltimateSource == this) {
                _armedPlayer.NexusUltimateSource = null;
            }
            _armedPlayer = null;
            if (IsArmed) LastClearReason = reason;
            IsArmed = false;
            UpdatePresentation();
        }

        // === Presentation (graybox; Package 10 replaces it) ===

        private void BuildPresentation() {
            _visual = new ColorRect {
                Name = "SourceVisual",
                Size = new Vector2(56, 120),
                Position = new Vector2(-28, -120),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_visual);

            _prompt = new Label {
                Name = "Prompt",
                Text = Tr("nexus_source_armed"),
                Position = new Vector2(-130, -160),
                CustomMinimumSize = new Vector2(260, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            };
            _prompt.AddThemeFontSizeOverride("font_size", 10);
            _prompt.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
            AddChild(_prompt);
        }

        private void UpdatePresentation() {
            if (_visual != null) {
                _visual.Color = IsSolved
                    ? new Color(0.25f, 0.35f, 0.4f, 0.4f)
                    : IsArmed
                        ? new Color(1f, 0.82f, 0.35f, 0.85f)
                        : new Color(0.35f, 0.6f, 0.85f, 0.6f);
            }
            // "Nexus-powered Ultimate" is a LOCAL prompt; the meter, its ready ring
            // and the Defy indicator are deliberately untouched.
            if (_prompt != null) _prompt.Visible = IsArmed;
        }
    }
}
