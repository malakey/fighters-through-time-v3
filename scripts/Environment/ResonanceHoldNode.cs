using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Which Act III level a <see cref="ResonanceHoldNode"/> belongs to. The
    /// three variants differ only in presentation and localization; their drain
    /// weight and dust allocation are an Extractor's, exactly.
    /// </summary>
    public enum ResonanceHoldVariant {
        /// <summary>Level 13 — a severed conduit still bleeding the hero's charge into the Void.</summary>
        SeveredConduit = 0,
        /// <summary>Level 14 — a cradle intake valve. The reveal: the clock was the Forge's intake.</summary>
        CradleIntakeValve = 1,
        /// <summary>Level 15 — a firing-channel anchor pylon holding the Prime Anchor's beam open.</summary>
        AnchorPylon = 2
    }

    /// <summary>
    /// V7.6 <b>Resonance Hold</b> drain stand-in (Package 11 A3b), reworked into
    /// a <b>held-Interact channel</b> by Package 12 W8 (M16, 2026-09-26).
    ///
    /// <para>Act III has no era to drain and no Chronal Extractors, but the player
    /// has read the top-right clock as pressure for eleven levels and the Collapse
    /// Tremor is its payoff — so the gauntlet keeps the clock and changes what it
    /// measures. It is now <b>the hero's own resonance</b>: the Wardens' readout of
    /// how long the hero can hold their charge against the Unbound's field.</para>
    ///
    /// <para><b>The candle rule.</b> Resonance is binary, held in trust. The hero
    /// has their <b>full kit at 1% exactly as at 100%</b>. This class therefore
    /// changes nothing about the player's abilities, stats or damage.</para>
    ///
    /// <para><b>M16: a stand-in is not a machine to smash.</b> It has <b>no HP and
    /// no discharge</b>: attacks, kit arcs and hazards do nothing to it, and the
    /// Extractor's idle discharge cycle never runs. The player stands beside it
    /// and <b>holds Interact for <see cref="ChannelFrames"/> (180) frames</b>, with
    /// the Restoration Font's range-anchored channel as the precedent. The
    /// channel resets — it does not pause — on taking damage, leaving
    /// <see cref="ChannelRangePixels"/>, releasing Interact, being stunned, dazed
    /// or killed, and whenever Time Freeze, the Post-Landing Hold or a boss's T01b
    /// suspension holds the world; it cannot start in any of those states. The
    /// Integrity clock keeps draining throughout: a channel is paid for in
    /// seconds.</para>
    ///
    /// <para><b>Completion is an Extractor destruction in every bookkeeping
    /// sense</b>, through the base path exactly once: the <c>+0.2</c> drain
    /// contribution goes, the F05 Extractor allocation for this node's stable
    /// <c>ObjectID</c> spawns once as a physical pickup, and the destroyed-
    /// extractor registry records it so a mid-level resume rebuilds the node
    /// already severed. The <c>chronal_extractor</c> group and the F01 starting
    /// denominator are untouched.</para>
    /// </summary>
    public partial class ResonanceHoldNode : ChronalExtractor, IInteractable {

        /// <summary>Every live stand-in joins this group, on top of the Extractor group.</summary>
        public const string HoldGroupName = "resonance_hold_node";

        /// <summary>M16: the held-Interact channel length, in physics frames (3.0 s at 60 Hz).</summary>
        public const int ChannelFrames = 180;

        [Export] public ResonanceHoldVariant Variant = ResonanceHoldVariant.SeveredConduit;

        /// <summary>
        /// The channel is anchored to the node: a channeller who moves (or is
        /// knocked) beyond this range resets it. Wider than the Font's 90 px
        /// because the stand-in's solid body is 112 px wide.
        /// </summary>
        [Export] public float ChannelRangePixels = 130f;

        /// <summary>The localized display name for this variant.</summary>
        public string DisplayNameKey => DisplayNameKeyFor(Variant);

        /// <summary>The E01 "drain slowed" notice this variant posts when it completes.</summary>
        public string DrainSlowedNoticeKey => DrainSlowedNoticeKeyFor(Variant);

        protected override string DrainSlowedNoticeKeyBase => DrainSlowedNoticeKeyFor(Variant);

        public static string DisplayNameKeyFor(ResonanceHoldVariant variant) => variant switch {
            ResonanceHoldVariant.CradleIntakeValve => "hold_node_cradle_valve_name",
            ResonanceHoldVariant.AnchorPylon => "hold_node_anchor_pylon_name",
            _ => "hold_node_severed_conduit_name"
        };

        public static string DrainSlowedNoticeKeyFor(ResonanceHoldVariant variant) => variant switch {
            ResonanceHoldVariant.CradleIntakeValve => "hold_drain_slowed_valve",
            ResonanceHoldVariant.AnchorPylon => "hold_drain_slowed_pylon",
            _ => "hold_drain_slowed_conduit"
        };

        /// <summary>The held-Interact prompt for this variant (Package 12 W8).</summary>
        public static string ChannelPromptKeyFor(ResonanceHoldVariant variant) => variant switch {
            ResonanceHoldVariant.CradleIntakeValve => "hold_channel_prompt_valve",
            ResonanceHoldVariant.AnchorPylon => "hold_channel_prompt_pylon",
            _ => "hold_channel_prompt_conduit"
        };

        /// <summary>
        /// Placeholder core colours, one per era read: the Void's violet, the
        /// Forge's cold cyan, Alexandria's firing-channel amber.
        /// </summary>
        public static Color CoreColorFor(ResonanceHoldVariant variant) => variant switch {
            ResonanceHoldVariant.CradleIntakeValve => new Color(0.24f, 0.86f, 0.94f, 0.6f),
            ResonanceHoldVariant.AnchorPylon => new Color(0.95f, 0.62f, 0.24f, 0.6f),
            _ => new Color(0.62f, 0.45f, 0.95f, 0.6f)
        };

        // === IInteractable ====================================================

        public string InteractionID => $"resonance_hold_{ObjectID}";
        public string PromptKey => ChannelPromptKeyFor(Variant);

        private PlayerController _channeler;
        private int _channelFrames;
        private int _hpBaseline;
        private FTT.UI.RadialProgress _channelRadial;

        /// <summary>True while a channel is running. Test seam.</summary>
        public bool IsChanneling => _channeler != null;

        /// <summary>Frames of the current channel held so far. Test seam.</summary>
        public int ChannelFramesElapsed => _channelFrames;

        /// <summary>The live channel fraction, 0..1 (drives the radial).</summary>
        public float ChannelProgress => Mathf.Clamp(_channelFrames / (float)ChannelFrames, 0f, 1f);

        /// <summary>The channel's radial, written only by this node. Test seam.</summary>
        public FTT.UI.RadialProgress ChannelRadial => _channelRadial;

        /// <summary>M16: no HP — nothing damages a stand-in.</summary>
        protected override bool AcceptsEnvironmentDamage => false;

        public bool CanInteract(PlayerController player) =>
            !IsDestroyed && !IsSealedShutdown && !IsChanneling
            && IsChannelEligible(player) && InChannelRange(player);

        public void Interact(PlayerController player) {
            if (!CanInteract(player)) return;
            _channeler = player;
            _channelFrames = 0;
            _hpBaseline = player.CurrentHP;
            ApplyChannelPresentation();
        }

        /// <summary>
        /// The single eligibility statement, shared by the start and every tick:
        /// alive and actionable, the world not held by Time Freeze, the
        /// Post-Landing Hold or a T01b suspension, and the node itself not held
        /// by a rewind freeze.
        /// </summary>
        private bool IsChannelEligible(PlayerController player) =>
            player != null && IsInstanceValid(player)
            && player.CurrentState is not (CharacterState.Dead or CharacterState.Stunned or CharacterState.Dazed)
            && !player.TimeFrozen
            && !player.IsRecoveryWorldHeld
            && !IsRewindFrozen
            && !(IsInsideTree() && ChronalRewindManager.IsWorldHeld(GetTree()));

        private bool InChannelRange(PlayerController player) =>
            player.GlobalPosition.DistanceTo(GlobalPosition) <= ChannelRangePixels;

        public override void _Ready() {
            // A completed channel is a persistent attempt fact (the destroyed
            // registry holds it); a death rewind never re-opens the node.
            RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            EnsureChannelRadial();
            base._Ready();
            AddToGroup(HoldGroupName);
            EnsureInteractionArea();
            ApplyStatePresentation();
        }

        /// <summary>
        /// M16: replaces the Extractor's idle discharge cycle outright — a
        /// stand-in never telegraphs, never bursts and never drains the meter.
        /// Only the channel runs here.
        /// </summary>
        public override void _PhysicsProcess(double delta) => AdvanceChannel();

        /// <summary>One channel tick. Public so tests can pump it in lockstep with the player.</summary>
        public void AdvanceChannel() {
            if (_channeler == null) return;
            PlayerController player = _channeler;
            if (IsDestroyed || IsSealedShutdown || !IsChannelEligible(player)) {
                ResetChannel();
                return;
            }
            // Any damage resets the channel; a heal mid-channel raises the baseline.
            if (player.CurrentHP < _hpBaseline) {
                ResetChannel();
                return;
            }
            _hpBaseline = Mathf.Max(_hpBaseline, player.CurrentHP);
            if (!InChannelRange(player) || !player.CurrentInputFrame.IsHeld(GameplayButtons.Interact)) {
                ResetChannel();
                return;
            }
            _channelFrames++;
            if (_channelFrames < ChannelFrames) {
                ApplyChannelPresentation();
                return;
            }
            _channeler = null;
            _channelFrames = 0;
            // The ordinary destruction path, exactly once: presentation, the F05
            // pickup against this ObjectID, the drain-factor drop, the
            // destroyed-extractor registry and the variant notice.
            CompleteWithoutDamage();
            ApplyChannelPresentation();
        }

        private void ResetChannel() {
            _channeler = null;
            _channelFrames = 0;
            ApplyChannelPresentation();
        }

        protected override void ApplyStatePresentation() {
            base.ApplyStatePresentation();
            // No HP: the hurtbox never listens, whatever the base decided.
            if (Hurtbox != null) {
                Hurtbox.SetMonitorableSafe(false);
                Hurtbox.SetMonitoringSafe(false);
            }
            if (GetNodeOrNull<CanvasItem>("DamagedVisual") is CanvasItem damaged) damaged.Visible = false;
            if (!IsDestroyed && GetNodeOrNull<CanvasItem>("CoreGlow") is CanvasItem core) {
                core.Modulate = CoreColorFor(Variant);
            }
            ApplyChannelPresentation();
        }

        private void ApplyChannelPresentation() {
            if (_channelRadial == null || !IsInstanceValid(_channelRadial)) return;
            _channelRadial.Visible = IsChanneling && !IsDestroyed;
            _channelRadial.Fraction = ChannelProgress;
        }

        /// <summary>
        /// The shared <see cref="InteractionArea"/> path: its press starts the
        /// channel (and already refuses during Time Freeze and the hold), and its
        /// prompt shows the variant string. Built in code so the existing
        /// template keeps loading; a template that authors its own
        /// <c>InteractionArea</c> child wins.
        /// </summary>
        private void EnsureInteractionArea() {
            if (GetNodeOrNull<InteractionArea>("InteractionArea") != null) return;
            var area = new InteractionArea { Name = "InteractionArea" };
            area.AddChild(new CollisionShape2D {
                Shape = new CircleShape2D { Radius = Mathf.Max(40f, ChannelRangePixels - 20f) }
            });
            var prompt = new Label {
                Name = "Prompt",
                Position = new Vector2(-110f, -150f),
                CustomMinimumSize = new Vector2(220f, 20f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            prompt.AddThemeFontSizeOverride("font_size", 12);
            area.AddChild(prompt);
            AddChild(area);
        }

        /// <summary>The channel's <see cref="FTT.UI.RadialProgress"/>, written by this node only.</summary>
        private void EnsureChannelRadial() {
            _channelRadial = GetNodeOrNull<FTT.UI.RadialProgress>("ChannelRadial");
            if (_channelRadial != null) return;
            _channelRadial = new FTT.UI.RadialProgress {
                Name = "ChannelRadial",
                Size = new Vector2(36f, 36f),
                Position = new Vector2(-18f, -122f),
                Thickness = 4f,
                Fraction = 0f,
                Visible = false
            };
            Color fill = CoreColorFor(Variant);
            fill.A = 1f;
            _channelRadial.FillColor = fill;
            AddChild(_channelRadial);
        }
    }
}
