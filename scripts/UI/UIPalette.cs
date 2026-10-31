using Godot;

namespace FTT.UI {

    /// <summary>
    /// Package 8 A1. The single source of truth for UI colour, type scale, and
    /// layout metrics.
    ///
    /// Before this existed the same six colours were retyped as literals across
    /// roughly fifteen UI scripts and scenes, so a palette change meant a
    /// repo-wide search. The values here are the ones those literals already
    /// used — this class names them, it does not re-tune them. The authored
    /// theme at <see cref="ThemePath"/> is built from exactly these numbers and
    /// <c>UIThemeTests</c> pins the two together, so changing a colour here
    /// requires changing the matching entry in the theme resource.
    ///
    /// Screens adopt the theme by setting <c>Theme</c> on their root
    /// <see cref="Control"/>; Godot propagates it down the subtree. Reach for a
    /// constant here only for the things a Theme cannot express — a
    /// <see cref="ColorRect"/> fill, a runtime tint, a drawn overlay.
    /// </summary>
    public static partial class UIPalette {

        /// <summary>The shared Theme every UI root should adopt.</summary>
        public const string ThemePath = "res://resources/UI/ftt_theme.tres";

        // ---- Brand colours (the pre-existing ad-hoc palette, named) ----------

        /// <summary>Primary accent: titles, focus, Temporal Resonance highlights.</summary>
        public static readonly Color Cyan = new(0f, 0.9f, 0.9f);

        /// <summary>Dimmed accent for borders and inactive accents.</summary>
        public static readonly Color CyanDim = new(0f, 0.55f, 0.6f);

        /// <summary>Chronal Dust and reward emphasis.</summary>
        public static readonly Color Gold = new(0.95f, 0.8f, 0.3f);

        /// <summary>Player-two and future-echo accent in the Temporal Glass menu system.</summary>
        public static readonly Color TemporalViolet = new(0.75f, 0.42f, 1f);

        /// <summary>Brighter gold used by the Dust tiers and hyper-armor shell.</summary>
        public static readonly Color GoldBright = new(1f, 0.92f, 0.35f);

        /// <summary>Boss bars, danger states, destructive confirmations.</summary>
        public static readonly Color BossRed = new(0.95f, 0.25f, 0.25f);

        /// <summary>Default body-copy colour.</summary>
        public static readonly Color Slate = new(0.7f, 0.75f, 0.85f);

        /// <summary>Secondary/subdued body copy.</summary>
        public static readonly Color SlateDim = new(0.45f, 0.5f, 0.6f);

        /// <summary>
        /// CM-STORY (decision 8.2, 2026-10-16): a Story damage number whose hit the
        /// chain proration reduced (a taxed second Special, a decayed chain hit). A
        /// dimmed near-white kept distinct from <see cref="Slate"/>, the warded
        /// boss's refused grey "0"; always paired with the smaller
        /// <c>StoryEnemyCombatRules.ProratedDamageNumberScale</c>, so colour is never
        /// the only channel.
        /// </summary>
        public static readonly Color DamageNumberReduced = new(0.66f, 0.68f, 0.74f);

        /// <summary>Panel and menu background.</summary>
        public static readonly Color Navy = new(0.06f, 0.06f, 0.12f);

        /// <summary>Full-screen background (loading, letterbox fills).</summary>
        public static readonly Color NavyDeep = new(0.05f, 0.05f, 0.1f);

        /// <summary>Translucent scrim behind a modal or results panel.</summary>
        public static readonly Color Shade = new(0.02f, 0.03f, 0.08f, 0.85f);

        /// <summary>Warning/attention copy (controller disconnect, conflicts).</summary>
        public static readonly Color Warning = new(1f, 0.75f, 0.3f);

        // ---- The two-colour visual grammar (V7.6, Package 11 A6b) -----------
        //
        // design-godot.md §2, "The Visual Grammar of Resonance", is an authored
        // visual language that holds in every mode and every mirror:
        //
        //   COLD SYNTHETIC LIGHT = THE UNBOUND.  Extraction beams, siphons,
        //   neural visors, plasma. Jagged and desaturating — what their machines
        //   touch drains grey.
        //
        //   WARM GOLD = HISTORY'S RESONANCE.  The hero's ignition and persistent
        //   aura, Chronal Dust, restoration vignettes, the ending release. Gold
        //   always flows FROM the era INTO its people.
        //
        // The pigments below are not new: the repo already painted extractors
        // cold and Dust gold. What was missing was the CONTRACT — nothing said
        // the two families were load-bearing, so every new siphon or beam site
        // invented its own literal and the language quietly eroded (Level 0's
        // own fracture had drifted to violet). These are the named constants
        // every such site must use, and ResonanceGrammarTests fails the build
        // when one defines its own cold literal instead.
        //
        // Deliberately expressed as the pigments the game already paints rather
        // than as new numbers: the grammar names what exists, it does not
        // re-tune it. They add no theme entries, so UIThemeTests is unaffected.

        /// <summary>
        /// The Unbound's cold synthetic light: extraction beams, siphon tethers,
        /// Unbound rift tears, neural visors, plasma. Never used for anything the
        /// hero or an era generates.
        /// </summary>
        public static readonly Color UnboundCold = new(0.2f, 0.95f, 1f);

        /// <summary>
        /// The same cold light at the alpha every discharge and beam site draws
        /// it at. <c>ChronalExtractor.DischargeColor</c> is exactly this value —
        /// the extractor was already correct, merely mislabelled "Apex Archive
        /// cyan" before the V7.5 faction rename.
        /// </summary>
        public static readonly Color UnboundColdDischarge = new(0.2f, 0.95f, 1f, 0.85f);

        /// <summary>
        /// Dimmed cold, for a siphon's resting state and the halo around a cold
        /// beam — the light before it bites.
        /// </summary>
        public static readonly Color UnboundColdDim = new(0.12f, 0.55f, 0.68f, 0.28f);

        /// <summary>
        /// History's resonance: the hero's ignition and persistent aura, Chronal
        /// Dust, restoration vignettes. The warm half of the grammar. Same value
        /// as <see cref="Gold"/>, named so a grammar site reads as grammar.
        /// </summary>
        public static readonly Color ResonanceGold = new(0.95f, 0.8f, 0.3f);

        /// <summary>
        /// Resonance at full ignition — the brighter gold the Dust tiers and the
        /// hyper-armor shell already use (<see cref="GoldBright"/>).
        /// </summary>
        public static readonly Color ResonanceGoldBright = new(1f, 0.92f, 0.35f);

        /// <summary>
        /// The persistent hero aura's tint. Warm gold biased toward white so it
        /// reads as a glow the hero carries rather than a repaint of the sprite.
        /// Matches <c>ChronalDustPickup.GlowColor</c>'s hue: the recurrence
        /// contract says every Dust pickup restates the hero's origin.
        /// </summary>
        public static readonly Color ResonanceAura = new(1f, 0.86f, 0.42f);

        /// <summary>
        /// A Warden portal's clean steady geometry. Warm, because the Wardens are
        /// history's side of the grammar — the contrast with an Unbound rift is
        /// temperature AND shape, and Level 0's final beat establishes both by
        /// standing the two doors side by side.
        /// </summary>
        public static readonly Color WardenPortal = new(0.98f, 0.84f, 0.38f, 0.85f);

        // ---- Pulp tokens (PAL-B "Resonance Gold", UI redesign 2026-10-26) ------
        //
        // UI_SPEC section 1 resolved to constants. The chrome accent is gold;
        // cyan stays the Player-1 ownership colour and the Severed's cold light
        // (UnboundCold, above) and is no longer the chrome. There is no
        // "pulp red" token (orchestrator ruling Q1): functional damage cues use
        // Danger. Colours are written as byte triples so they read as the
        // spec's hex values; UIThemeTests pins the authored theme to them.

        private static Color Hex(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

        /// <summary>Screen backdrop under art (#05071A).</summary>
        public static readonly Color Backdrop = Hex(0x05, 0x07, 0x1A);
        /// <summary>Text 1 (#F3F0E6).</summary>
        public static readonly Color Ink = Hex(0xF3, 0xF0, 0xE6);
        /// <summary>Text 2 (#C0BAA6): captions.</summary>
        public static readonly Color Ink2 = Hex(0xC0, 0xBA, 0xA6);
        /// <summary>Text 3 (#8A8374): secondary and dormant, never the only cue.</summary>
        public static readonly Color Ink3 = Hex(0x8A, 0x83, 0x74);
        /// <summary>The chrome accent (#F3C95B): focus fill, tags, banners.</summary>
        public static readonly Color Accent = Hex(0xF3, 0xC9, 0x5B);
        /// <summary>Accent light (#FFE39A): pressed fill, rewind and Time Freeze tint.</summary>
        public static readonly Color Accent2 = Hex(0xFF, 0xE3, 0x9A);
        /// <summary>Resonance gold (#F3C95B): dust, Ultimate ring start.</summary>
        public static readonly Color Res = Hex(0xF3, 0xC9, 0x5B);
        /// <summary>Resonance bright (#FFF0B0): Defy ready, objective, full meter.</summary>
        public static readonly Color Res2 = Hex(0xFF, 0xF0, 0xB0);
        /// <summary>Ultimate meter start (#F3C95B).</summary>
        public static readonly Color Meter = Hex(0xF3, 0xC9, 0x5B);
        /// <summary>Ultimate meter end and the Fighter meter fill (#FFB347).</summary>
        public static readonly Color Meter2 = Hex(0xFF, 0xB3, 0x47);
        /// <summary>HP fill (#8CF0B8).</summary>
        public static readonly Color Hp = Hex(0x8C, 0xF0, 0xB8);
        /// <summary>HP damage tracker (#FF5A5A), the delayed underfill.</summary>
        public static readonly Color HpLag = Hex(0xFF, 0x5A, 0x5A);
        /// <summary>Rally echo band (#FFF7DA).</summary>
        public static readonly Color Echo = Hex(0xFF, 0xF7, 0xDA);
        /// <summary>Block pip (#DFE8FF).</summary>
        public static readonly Color Shield = Hex(0xDF, 0xE8, 0xFF);
        /// <summary>Low HP and warnings (#FF5A5A).</summary>
        public static readonly Color Danger = Hex(0xFF, 0x5A, 0x5A);
        /// <summary>Boss red (#FF5A5A).</summary>
        public static readonly Color Boss = Hex(0xFF, 0x5A, 0x5A);

        // ---- Glass tokens (Temporal Glass rebuild, TG-THEME 2026-10-27) ----------
        //
        // UI_SPEC_GLASS sections 1.1, 1.3 and 1.4 resolved to constants, each named for its role (1.6). The
        // generator (tools/ui_theme/build_ftt_theme.py) paints every theme colour from a table of the same names,
        // writes that table into UIPalette.Roles.cs (GeneratorPalette), and UIThemeTests pins every entry to the
        // constant here and the glass ones to the spec's values. The fills are the build fills of 1.4 (one level
        // everywhere, darker than the mock's, because gl_compatibility has no backdrop blur).

        /// <summary>Focus (#FFD45C): the focused edge, the focused marker and its glow.</summary>
        public static readonly Color Focus = Hex(0xFF, 0xD4, 0x5C);
        /// <summary>edge: the standard hairline (#F3C95B at 0.55): rings, the tube edge, the panel border.</summary>
        public static readonly Color Edge = Hex(0xF3, 0xC9, 0x5B, 0.55f);
        /// <summary>edge-hi: the lit hairline (#FFD45C): the tag bar, a ready lens, the bright end of a flourish rule.</summary>
        public static readonly Color EdgeHi = Hex(0xFF, 0xD4, 0x5C);
        /// <summary>edge-dim: the quiet hairline (#F3C95B at 0.25): a normal chevron, a tile, an empty status ring.</summary>
        public static readonly Color EdgeDim = Hex(0xF3, 0xC9, 0x5B, 0.25f);
        /// <summary>ink-inv (#1A1204): text on a gold fill.</summary>
        public static readonly Color InkInv = Hex(0x1A, 0x12, 0x04);
        /// <summary>hp2 (#4FCF8A): the HP fill's anchored end (<see cref="Hp"/> is the moving end).</summary>
        public static readonly Color Hp2 = Hex(0x4F, 0xCF, 0x8A);
        /// <summary>boss2 (#A82A2A): the boss fill's start and the low-HP fill's start.</summary>
        public static readonly Color Boss2 = Hex(0xA8, 0x2A, 0x2A);
        /// <summary>shadow (#00020C at 0.60): the soft drop shadow of panels and portraits.</summary>
        public static readonly Color PanelShadow = Hex(0x00, 0x02, 0x0C, 0.60f);
        /// <summary>The panel fill's 160 degree start, as built (rgba(15, 24, 52, .92); drawn .80 with a blur).</summary>
        public static readonly Color GlassFillTop = Hex(0x0F, 0x18, 0x34, 0.92f);
        /// <summary>The panel fill's 160 degree end, as built (rgba(6, 10, 26, .95)).</summary>
        public static readonly Color GlassFillBottom = Hex(0x06, 0x0A, 0x1A, 0.95f);
        /// <summary>The single-colour fallback of the panel fill (6.2: rgba(15, 24, 52, .93)), what a flat slab paints.</summary>
        public static readonly Color GlassFillFlat = Hex(0x0F, 0x18, 0x34, 0.93f);
        /// <summary>The chevron and field fill, normal and selected (rgba(8, 13, 32, .90)).</summary>
        public static readonly Color ButtonFill = Hex(0x08, 0x0D, 0x20, 0.90f);
        /// <summary>The chevron fill under the focus and pressed washes (rgba(8, 16, 38, .92)).</summary>
        public static readonly Color ButtonFillFocus = Hex(0x08, 0x10, 0x26, 0.92f);
        /// <summary>The pressed wash (rgba(glow, .50) to .16, mean .33) composited over <see cref="ButtonFillFocus"/>: a flat pressed fill.</summary>
        public static readonly Color ButtonFillPressed = Hex(0x5A, 0x51, 0x38, 0.946f);
        /// <summary>The disabled chevron fill (rgba(8, 13, 32, .50)).</summary>
        public static readonly Color ButtonFillDisabled = Hex(0x08, 0x0D, 0x20, 0.50f);
        /// <summary>The select tile fill, 160 degree start (rgba(26, 38, 74, .90)).</summary>
        public static readonly Color TileFillTop = Hex(0x1A, 0x26, 0x4A, 0.90f);
        /// <summary>The select tile fill, 160 degree end (rgba(6, 10, 26, .94)).</summary>
        public static readonly Color TileFillBottom = Hex(0x06, 0x0A, 0x1A, 0.94f);
        /// <summary>The lens radial's outer stop (rgba(6, 10, 26, .92)).</summary>
        public static readonly Color LensFill = Hex(0x06, 0x0A, 0x1A, 0.92f);
        /// <summary>The badge radial's outer stop (rgba(8, 12, 30, .86)).</summary>
        public static readonly Color BadgeFill = Hex(0x08, 0x0C, 0x1E, 0.86f);
        /// <summary>The Integrity dial radial's outer stop (rgba(5, 9, 24, .90)).</summary>
        public static readonly Color DialFill = Hex(0x05, 0x09, 0x18, 0.90f);
        /// <summary>The portrait frame radial's outer stop (rgba(6, 10, 26, .95)).</summary>
        public static readonly Color PortraitFill = Hex(0x06, 0x0A, 0x1A, 0.95f);
        /// <summary>The glass tube's inner track, top (rgba(2, 5, 14, .95)).</summary>
        public static readonly Color TubeTrackTop = Hex(0x02, 0x05, 0x0E, 0.95f);
        /// <summary>The glass tube's inner track, bottom (rgba(10, 16, 36, .82)).</summary>
        public static readonly Color TubeTrackBottom = Hex(0x0A, 0x10, 0x24, 0.82f);
        /// <summary>The boss tube's opaque track, top (#05060F).</summary>
        public static readonly Color BossTrackTop = Hex(0x05, 0x06, 0x0F);
        /// <summary>The boss tube's opaque track, bottom (#120A18).</summary>
        public static readonly Color BossTrackBottom = Hex(0x12, 0x0A, 0x18);
        /// <summary>The cooldown sweep (rgba(2, 5, 14, .76)).</summary>
        public static readonly Color SweepShade = Hex(0x02, 0x05, 0x0E, 0.76f);
        /// <summary>The Integrity wedge's remainder (rgba(2, 5, 14, .55)).</summary>
        public static readonly Color WedgeRemainder = Hex(0x02, 0x05, 0x0E, 0.55f);
        /// <summary>The toggle track fill while off (rgba(6, 10, 26, .90)).</summary>
        public static readonly Color ToggleTrackFill = Hex(0x06, 0x0A, 0x1A, 0.90f);
        /// <summary>The TabContainer's quiet inner panel (agent choice, rgba(6, 10, 26, .60)).</summary>
        public static readonly Color TabPanelFill = Hex(0x06, 0x0A, 0x1A, 0.60f);
        /// <summary>The slider track (rgba(255, 255, 255, .12)).</summary>
        public static readonly Color SliderTrack = Hex(0xFF, 0xFF, 0xFF, 0.12f);
        /// <summary>The scroll bar track (rgba(255, 255, 255, .06)).</summary>
        public static readonly Color ScrollTrack = Hex(0xFF, 0xFF, 0xFF, 0.06f);
        /// <summary>The glass tube's decile ticks (rgba(255, 255, 255, .13)).</summary>
        public static readonly Color TickLine = Hex(0xFF, 0xFF, 0xFF, 0.13f);
        /// <summary>A readiness or status ring's remainder and the tube's glow (rgba(glow, .16)).</summary>
        public static readonly Color RingTrack = Hex(0xF3, 0xC9, 0x5B, 0.16f);
        /// <summary>The 14 px focus glow (rgba(glow, .55)).</summary>
        public static readonly Color FocusGlow = Hex(0xF3, 0xC9, 0x5B, 0.55f);
        /// <summary>A popup row's focus wash (rgba(glow, .22), the flat mean of the button wash).</summary>
        public static readonly Color FocusWash = Hex(0xF3, 0xC9, 0x5B, 0.22f);
        /// <summary>The panel tag's wash (rgba(glow, .21), the flat mean of its .34 to .08 gradient).</summary>
        public static readonly Color TagWash = Hex(0xF3, 0xC9, 0x5B, 0.21f);
        /// <summary>An unselected tab (rgba(glow, .08)).</summary>
        public static readonly Color TabFill = Hex(0xF3, 0xC9, 0x5B, 0.08f);
        /// <summary>A hovered tab (agent choice, rgba(glow, .16)).</summary>
        public static readonly Color TabHover = Hex(0xF3, 0xC9, 0x5B, 0.16f);
        /// <summary>The READY badge fill (rgba(glow, .14)).</summary>
        public static readonly Color ReadyBadgeFill = Hex(0xF3, 0xC9, 0x5B, 0.14f);
        /// <summary>The READY badge glow (rgba(glow, .30)).</summary>
        public static readonly Color ReadyBadgeGlow = Hex(0xF3, 0xC9, 0x5B, 0.30f);
        /// <summary>The idle (choosing) badge border (rgba(glow, .25)).</summary>
        public static readonly Color ReadyBadgeIdleEdge = Hex(0xF3, 0xC9, 0x5B, 0.25f);
        /// <summary>The idle (choosing) badge fill (rgba(0, 0, 0, .25)).</summary>
        public static readonly Color ReadyBadgeIdleFill = Hex(0x00, 0x00, 0x00, 0.25f);
        /// <summary>Text selection in a field (rgba(glow, .40)).</summary>
        public static readonly Color Selection = Hex(0xF3, 0xC9, 0x5B, 0.40f);
        /// <summary>The block pip glow (rgba(143, 216, 255, .6)).</summary>
        public static readonly Color PipGlow = Hex(0x8F, 0xD8, 0xFF, 0.60f);
        /// <summary>The boss tube's edge (#FF4D62, PAL-A's boss red, which the mock hard-codes).</summary>
        public static readonly Color BossEdge = Hex(0xFF, 0x4D, 0x62);
        /// <summary>The boss tube's glow (rgba(255, 77, 98, .30)).</summary>
        public static readonly Color BossGlow = Hex(0xFF, 0x4D, 0x62, 0.30f);
        /// <summary>The boss damage tracker (rgba(255, 255, 255, .55)).</summary>
        public static readonly Color BossTracker = Hex(0xFF, 0xFF, 0xFF, 0.55f);
        /// <summary>The selected tab's text and the select lock glyph (#04141A, AC24).</summary>
        public static readonly Color TabSelectedInk = Hex(0x04, 0x14, 0x1A);
        /// <summary>What the <c>ScrimPanel</c> variation paints, opaque; every user multiplies it by SelfModulate alpha 0.70.</summary>
        public static readonly Color ScrimFill = Hex(0x03, 0x05, 0x0E);
        /// <summary>The build pause dim (rgba(3, 5, 14, .70), 1.4): <see cref="ScrimFill"/> at the users' 0.70.</summary>
        public static readonly Color PauseDim = Hex(0x03, 0x05, 0x0E, 0.70f);
        /// <summary>The Smothered Game Over dim (rgba(3, 5, 14, .82), 9.2).</summary>
        public static readonly Color GameOverDim = Hex(0x03, 0x05, 0x0E, 0.82f);
        /// <summary>The menu scrim's strongest stop (90 degree: .96 at 0, .84 at 26 %, .35 at 48 %, 0 at 66 %; 1.3).</summary>
        public static readonly Color ScrimMenu = Hex(0x04, 0x07, 0x14, 0.96f);
        /// <summary>The select vignette's centre stop (rgba(3, 5, 16, .55), transparent at 80 %).</summary>
        public static readonly Color SelectVignette = Hex(0x03, 0x05, 0x10, 0.55f);
        /// <summary>The Fighter top shade's top stop (180 degree: .80, .64 at 42 %, .30 at 78 %, 0 at 100 %).</summary>
        public static readonly Color TopShade = Hex(0x06, 0x04, 0x0C, 0.80f);
        /// <summary>The main menu title gradient's top (#FFFFFF).</summary>
        public static readonly Color TitleTop = Hex(0xFF, 0xFF, 0xFF);
        /// <summary>The main menu title gradient at 42 % and its flat fallback (#DFE8FF).</summary>
        public static readonly Color TitleMid = Hex(0xDF, 0xE8, 0xFF);
        /// <summary>The main menu title gradient's bottom (#8D9BC6).</summary>
        public static readonly Color TitleBottom = Hex(0x8D, 0x9B, 0xC6);
        /// <summary>The focused, hovered and pressed label (#FFFFFF, T10).</summary>
        public static readonly Color TextFocus = Hex(0xFF, 0xFF, 0xFF);
        /// <summary>The modulate a disabled glass field or tab texture draws with (#FFFFFF at 0.50, agent choice).</summary>
        public static readonly Color DimModulate = Hex(0xFF, 0xFF, 0xFF, 0.50f);
        /// <summary>
        /// The panel fill at the slab's centre, midway along its 160 degree build gradient (<see cref="GlassFillTop"/> to
        /// <see cref="GlassFillBottom"/>). The glass_panel nine-slice bakes this fill over the slab's own soft drop shadow.
        /// </summary>
        public static readonly Color GlassFillMid = GlassFillTop.Lerp(GlassFillBottom, 0.5f);
        /// <summary>The scene halo S rim (#000000 at 0.85, 2.4).</summary>
        public static readonly Color HaloOutlineS = Hex(0x00, 0x00, 0x00, 0.85f);
        /// <summary>The scene halo S shadow (#000000 at 0.60).</summary>
        public static readonly Color HaloShadowS = Hex(0x00, 0x00, 0x00, 0.60f);
        /// <summary>The scene halo L rim (#000000 at 0.70).</summary>
        public static readonly Color HaloOutlineL = Hex(0x00, 0x00, 0x00, 0.70f);
        /// <summary>The scene halo L shadow (#000000 at 0.50).</summary>
        public static readonly Color HaloShadowL = Hex(0x00, 0x00, 0x00, 0.50f);

        /// <summary>The scene halo tiers as Label theme values (2.4): rim px, shadow offset y, shadow outline px.</summary>
        public const int HaloSOutline = 2, HaloSShadowOffsetY = 1, HaloSShadowOutline = 4;
        public const int HaloLOutline = 3, HaloLShadowOffsetY = 3, HaloLShadowOutline = 8;
        /// <summary>T29's halo S variant: the same rim and shadow with a 6 px shadow outline.</summary>
        public const int HaloS6ShadowOutline = 6;

        /// <summary>
        /// Player 1 ownership colour, read live from the player's chosen slot palette
        /// (<see cref="FTT.Combat.PlayerSlotPalettes.ActiveSlotColor"/>: Default cyan, Blue/Orange or High-Contrast,
        /// Settings &gt; Gameplay), so no ownership colour in the interface can drift from the accessibility choice.
        /// With the Default palette it is <see cref="FTT.Combat.GlowPalette.SlotColor"/>.
        /// </summary>
        public static Color P1 => FTT.Combat.PlayerSlotPalettes.ActiveSlotColor(0);

        /// <summary>Player 2 ownership colour, read live from the active slot palette.</summary>
        public static Color P2 => FTT.Combat.PlayerSlotPalettes.ActiveSlotColor(1);

        /// <summary>The slot palette's contrasting edge for player 1 (<see cref="FTT.Combat.PlayerSlotPalettes.ActiveSlotEdgeColor"/>).</summary>
        public static Color P1Edge => FTT.Combat.PlayerSlotPalettes.ActiveSlotEdgeColor(0);

        /// <summary>The slot palette's contrasting edge for player 2.</summary>
        public static Color P2Edge => FTT.Combat.PlayerSlotPalettes.ActiveSlotEdgeColor(1);

        /// <summary>Below this relative luminance a slot colour does not read on the interface's dark plates.</summary>
        public const float OwnerDarkLuminance = 0.2f;

        /// <summary>
        /// The ownership colour to paint on dark interface chrome (a star, a ring, a badge glyph): the slot colour,
        /// or its contrasting edge when the slot colour is too dark to read there (the High-Contrast palette's
        /// black player 2). Shape (triangle or circle, the P1 / P2 text) still tells the players apart.
        /// </summary>
        public static Color OwnerOnDark(int playerIndex) {
            Color fill = playerIndex == 1 ? P2 : P1;
            float luminance = 0.2126f * fill.R + 0.7152f * fill.G + 0.0722f * fill.B;
            return luminance < OwnerDarkLuminance ? (playerIndex == 1 ? P2Edge : P1Edge) : fill;
        }

        // Literals the components hard-code (the pulp kit's legacy ones were retired by the glass closeout, TG-CLOSE).
        /// <summary>#000000: black (the scene halos are black at their own alphas).</summary>
        public static readonly Color InkBlack = Hex(0, 0, 0);
        /// <summary>#FF7A5C: the damage status glyph and ring (Venom, Radiant Burn).</summary>
        public static readonly Color StatusDamage = Hex(0xFF, 0x7A, 0x5C);
        /// <summary>#8FC7FF: the control status glyph and ring (Time Dilation, Static Charge, Root).</summary>
        public static readonly Color StatusControl = Hex(0x8F, 0xC7, 0xFF);

        // ---- Derived control colours (what the Theme actually paints) --------
        //
        // TG-THEME: re-pointed at the glass values. UIThemeTests pins each against the generated theme.

        /// <summary>A glass slab's fill at its centre (the glass_panel nine-slice bakes it over its drop shadow).</summary>
        public static readonly Color PanelBackground = GlassFillMid;
        /// <summary>A glass slab's edge (the standard hairline).</summary>
        public static readonly Color PanelBorder = Edge;

        public static readonly Color ButtonNormal = ButtonFill;
        /// <summary>A chevron's hover box is its normal box (orchestrator ruling: the decor takes focus on mouse-enter).</summary>
        public static readonly Color ButtonHover = ButtonFill;
        public static readonly Color ButtonPressed = ButtonFillPressed;
        public static readonly Color ButtonDisabled = ButtonFillDisabled;

        /// <summary>
        /// The lit edge every focus box draws (chevrons, fields, line edits, check boxes): the focused
        /// row's edge plus a 14 px <see cref="FocusGlow"/>. Controller and keyboard players navigate entirely by
        /// these, so every focusable control in the theme draws one.
        /// </summary>
        public static readonly Color FocusOutline = Focus;

        /// <summary>The fill a focused chevron sits on: its normal box, under the focus overlay (which has no dark base).</summary>
        public static readonly Color FocusFill = ButtonFill;

        /// <summary>Text on a gold fill: the selected tab (#04141A, AC24).</summary>
        public static readonly Color TextOnAccent = TabSelectedInk;

        public static readonly Color TextPrimary = Ink;
        public static readonly Color TextAccent = Accent;
        /// <summary>The disabled label (ink3).</summary>
        public static readonly Color TextDisabled = Ink3;
        /// <summary>The normal chevron label (ink2, T10).</summary>
        public static readonly Color ButtonLabel = Ink2;

        /// <summary>
        /// The glass tube's track (<see cref="TubeTrackTop"/>; the default <c>ProgressBar</c> background paints it). The
        /// radial rings keep their own default (<c>RadialProgress.DefaultTrackColor</c>), so the world-space channel rings
        /// never follow a palette change.
        /// </summary>
        public static readonly Color TrackBackground = TubeTrackTop;

        // ---- Type scale -----------------------------------------------------
        //
        // V7.3: every non-HUD screen expresses these roles through the theme's
        // Label type variations (below) rather than per-label
        // AddThemeFontSizeOverride calls, because UIPalette.ApplyUiScale rescales
        // the theme's font-size entries in place — an override is invisible to it
        // and freezes that label out of the accessibility UI scale. The constants
        // stay as the C# mirror UIThemeTests pins against the authored theme.

        public const int SmallFontSize = 18;
        public const int BodyFontSize = 20;
        public const int HeadingFontSize = 26;
        public const int EmphasisFontSize = 30;
        /// <summary>The panel title inside a slab (UI_SPEC_GLASS 9.1: Cinzel 900, 36 px; the pulp title was 40).</summary>
        public const int TitleFontSize = 36;

        /// <summary>No UI label renders below this at 1080p (plan item 14).</summary>
        public const int MinLabelFontSize = 18;

        /// <summary>Label type variation names authored in the shared theme
        /// (ftt_theme.tres), one per type-scale role. The body role is the
        /// Label default (no variation).</summary>
        public const string SmallLabelVariation = "SmallLabel";
        public const string HeadingLabelVariation = "HeadingLabel";
        public const string EmphasisLabelVariation = "EmphasisLabel";
        public const string TitleLabelVariation = "TitleLabel";

        // ---- Layout metrics -------------------------------------------------

        public const int ButtonMinWidth = 320;
        public const int ButtonMinHeight = 44;
        public const int PanelSeparation = 14;
        public const int CornerRadius = 4;
        public const int BorderWidth = 2;

        /// <summary>
        /// Loads the shared theme, or returns null when it is missing. Callers
        /// fall back to unthemed defaults rather than failing: a missing theme
        /// must never take a screen down.
        /// </summary>
        public static Theme LoadTheme() {
            if (!ResourceLoader.Exists(ThemePath)) return null;
            return ResourceLoader.Load<Theme>(ThemePath);
        }

        /// <summary>Applies the shared theme to a control root when available.</summary>
        public static void ApplyTheme(Control root) {
            if (root == null || root.Theme != null) return;
            Theme theme = LoadTheme();
            if (theme != null) root.Theme = theme;
        }

        // ---- Accessibility UI scale ----------------------------------------
        //
        // Design "Committed Accessibility Additions": a 90%–140% UI scale
        // applied to the shared theme's font sizes and the HUD layout. Eleven
        // scenes attach the theme resource directly and the rest adopt it via
        // ApplyTheme, so the scale is applied by mutating the one cached theme
        // instance in place: every screen — scene-attached or code-applied,
        // already open or not — picks the change up through Godot's theme
        // change propagation, with no per-screen wiring. The authored sizes are
        // captured on the first apply so repeated applies never compound.
        // The HUDs are the exception: they scale their whole layout through
        // UiScaleBinder and therefore use NewUnscaledTheme to avoid scaling
        // their fonts twice.

        /// <summary>The saved UI scale, clamped; 1 when no save is loaded.</summary>
        public static float CurrentUiScale => Mathf.Clamp(
            FTT.Core.SaveManager.Instance?.GlobalData?.UiScale ?? 1f,
            FTT.Core.GlobalSaveData.MinUiScale, FTT.Core.GlobalSaveData.MaxUiScale);

        /// <summary>The factor most recently applied to the shared theme.</summary>
        public static float AppliedUiScale { get; private set; } = 1f;

        private static System.Collections.Generic.Dictionary<(string Type, string Name), int> _authoredFontSizes;
        private static int _authoredDefaultFontSize = -1;

        /// <summary>Applies the persisted setting to the shared theme (boot and settings-change entry point).</summary>
        public static void ApplySavedUiScale() => ApplyUiScale(CurrentUiScale);

        /// <summary>
        /// Rescales the shared theme's default font size and every per-type
        /// font-size entry to <paramref name="factor"/> times the authored
        /// value. Idempotent: always scales from the captured authored sizes,
        /// never from the current ones.
        /// </summary>
        public static void ApplyUiScale(float factor) {
            factor = Mathf.Clamp(factor,
                FTT.Core.GlobalSaveData.MinUiScale, FTT.Core.GlobalSaveData.MaxUiScale);
            Theme theme = LoadTheme();
            if (theme == null) return;
            CaptureAuthoredSizes(theme);
            if (_authoredDefaultFontSize > 0) {
                theme.DefaultFontSize = ScaleFontSize(_authoredDefaultFontSize, factor);
            }
            foreach (var entry in _authoredFontSizes) {
                theme.SetFontSize(entry.Key.Name, entry.Key.Type, ScaleFontSize(entry.Value, factor));
            }
            AppliedUiScale = factor;
        }

        /// <summary>
        /// A duplicate of the shared theme carrying the authored (unscaled)
        /// font sizes, for surfaces that scale their whole layout through
        /// <see cref="UiScaleBinder"/> instead. Null when the theme is missing.
        /// </summary>
        public static Theme NewUnscaledTheme() {
            Theme theme = LoadTheme();
            if (theme == null) return null;
            var copy = (Theme)theme.Duplicate(true);
            if (_authoredFontSizes == null) return copy;
            if (_authoredDefaultFontSize > 0) copy.DefaultFontSize = _authoredDefaultFontSize;
            foreach (var entry in _authoredFontSizes) {
                copy.SetFontSize(entry.Key.Name, entry.Key.Type, entry.Value);
            }
            return copy;
        }

        private static void CaptureAuthoredSizes(Theme theme) {
            if (_authoredFontSizes != null) return;
            _authoredFontSizes = new System.Collections.Generic.Dictionary<(string, string), int>();
            _authoredDefaultFontSize = theme.HasDefaultFontSize() ? theme.DefaultFontSize : -1;
            foreach (string themeType in theme.GetFontSizeTypeList()) {
                foreach (string sizeName in theme.GetFontSizeList(themeType)) {
                    _authoredFontSizes[(themeType, sizeName)] = theme.GetFontSize(sizeName, themeType);
                }
            }
        }

        /// <summary>Rounds a scaled font size, never below 1 px.</summary>
        public static int ScaleFontSize(int size, float factor) =>
            Mathf.Max(1, Mathf.RoundToInt(size * factor));
    }
}
