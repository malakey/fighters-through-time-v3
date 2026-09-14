using Godot;

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
    /// V7.6 <b>Resonance Hold</b> drain stand-in (Package 11 A3b).
    ///
    /// <para>Act III has no era to drain and no Chronal Extractors, but the player
    /// has read the top-right clock as pressure for eleven levels and the Collapse
    /// Tremor is its payoff — so the gauntlet keeps the clock and changes what it
    /// measures. It is now <b>the hero's own resonance</b>: the Wardens' readout of
    /// how long the hero can hold their charge against the Unbound's field.</para>
    ///
    /// <para><b>The candle rule.</b> Resonance is binary, held in trust. The hero
    /// has their <b>full kit at 1% exactly as at 100%</b> — the gauge measures how
    /// long the field needs to <i>smother</i> the flame, not how much flame is
    /// left. A candle in a draft burns whole until it goes out. This class
    /// therefore changes nothing about the player's abilities, stats or damage;
    /// V7.5's "no mid-campaign power loss" rule survives to the letter.</para>
    ///
    /// <para><b>Mechanically identical to an Extractor</b>, and deliberately so:
    /// it derives from <see cref="ChronalExtractor"/>, keeps its group, its
    /// <c>+0.2</c> contribution to the F01 drain factor, its idle discharge cycle,
    /// its destruction registry entry, its F05 optional-dust share and its
    /// rewind-freeze behaviour. Only the fiction and the strings change — which is
    /// the design's explicit instruction ("the same gauge, the same rules, the
    /// same HUD face and Tremor assets; only the fiction and the drain sources
    /// change").</para>
    /// </summary>
    public partial class ResonanceHoldNode : ChronalExtractor {

        /// <summary>Every live stand-in joins this group, on top of the Extractor group.</summary>
        public const string HoldGroupName = "resonance_hold_node";

        [Export] public ResonanceHoldVariant Variant = ResonanceHoldVariant.SeveredConduit;

        /// <summary>The localized display name for this variant.</summary>
        public string DisplayNameKey => DisplayNameKeyFor(Variant);

        /// <summary>The E01 "drain slowed" notice this variant posts when it breaks.</summary>
        public string DrainSlowedNoticeKey => DrainSlowedNoticeKeyFor(Variant);

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

        /// <summary>
        /// Placeholder core colours, one per era read: the Void's violet, the
        /// Forge's cold cyan, Alexandria's firing-channel amber.
        /// </summary>
        public static Color CoreColorFor(ResonanceHoldVariant variant) => variant switch {
            ResonanceHoldVariant.CradleIntakeValve => new Color(0.24f, 0.86f, 0.94f, 0.6f),
            ResonanceHoldVariant.AnchorPylon => new Color(0.95f, 0.62f, 0.24f, 0.6f),
            _ => new Color(0.62f, 0.45f, 0.95f, 0.6f)
        };

        public override void _Ready() {
            base._Ready();
            AddToGroup(HoldGroupName);
            ApplyVariantPresentation();
        }

        protected override void ApplyStatePresentation() {
            base.ApplyStatePresentation();
            ApplyVariantPresentation();
        }

        /// <summary>
        /// Recolours the placeholder body. Runs after the base has decided
        /// idle/damaged/destroyed visibility so a destroyed husk still reads as a
        /// husk; only the live core takes the variant tint.
        /// </summary>
        private void ApplyVariantPresentation() {
            // Idle only: the base owns the telegraph red and the damaged amber,
            // and both are promises the player has learned to read.
            if (IsDestroyed || IsTelegraphing || VisualState != ChronalExtractorVisualState.Idle) return;
            if (GetNodeOrNull<CanvasItem>("CoreGlow") is CanvasItem core) {
                core.Modulate = CoreColorFor(Variant);
            }
        }
    }
}
