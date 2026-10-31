using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// The three ability slots the Fighter HUD shows a cooldown for: Special 1,
    /// Special 2 and Movement. The Ultimate plate is gone (the meter bar already
    /// shows the Ultimate's readiness) and the Echo Step slot (ordinal 3, M25)
    /// was removed on 2026-10-25 with Echo Step.
    /// </summary>
    public enum FighterCooldownSlot {
        SpecialOne = 0,
        SpecialTwo = 1,
        Movement = 2
    }

    /// <summary>
    /// Package 8 B2. Every display rule the production Fighter HUD applies,
    /// separated from the nodes that render them.
    ///
    /// The HUD itself is a scene, so its behaviour is only reachable through an
    /// instantiated tree; the decisions worth pinning — when the clock is shown,
    /// how a frame count becomes "0:47", where the HP bar turns amber then red —
    /// are arithmetic and belong somewhere a test can reach them without one.
    /// Nothing here reads a simulation component: the HUD hands over values it
    /// already pulled, so this class cannot become a second route into
    /// <c>scripts/FighterSim/</c>.
    /// </summary>
    public static class FighterHudModel {

        /// <summary>
        /// Meter ceiling. The Influence/Ultimate meter is 0-100 by the design
        /// contract and <c>FighterSimulationSystems</c> clamps to the same number;
        /// this is the display range, not a second tuning value.
        /// </summary>
        public const float MaxInfluence = FTT.Combat.InfluenceMeterRules.MaxValue;

        /// <summary>Below this fraction the HP bar reads amber.</summary>
        public const float HpCautionFraction = 0.5f;

        /// <summary>Below this fraction the HP bar reads red.</summary>
        public const float HpDangerFraction = 0.25f;

        /// <summary>
        /// V7: the clock is visible whenever the match timer is enabled — Stock
        /// mode defaults to the 8:00 timer too (configurable, including Off), so
        /// visibility follows the deterministic match component's TimerEnabled
        /// flag rather than the mode.
        /// </summary>
        public static bool TimerIsVisible(bool timerEnabled) => timerEnabled;

        /// <summary>
        /// Whole minutes remaining, floored, never negative. Split from
        /// <see cref="ClockSeconds"/> so the rendered string can go through a
        /// translation key rather than being assembled with a hardcoded colon.
        /// </summary>
        public static int ClockMinutes(int remainingFrames, int tickRate) =>
            TotalSeconds(remainingFrames, tickRate) / 60;

        /// <summary>Seconds within the current minute, 0-59.</summary>
        public static int ClockSeconds(int remainingFrames, int tickRate) =>
            TotalSeconds(remainingFrames, tickRate) % 60;

        /// <summary>
        /// Rounds *up*, so a match with any frames left never shows 0:00. A clock
        /// that reads zero while the fighters are still playing is the single most
        /// confusing thing a match timer can do.
        /// </summary>
        public static int TotalSeconds(int remainingFrames, int tickRate) {
            if (remainingFrames <= 0 || tickRate <= 0) return 0;
            return (remainingFrames + tickRate - 1) / tickRate;
        }

        /// <summary>
        /// Package 11 A8 / F21. Seconds at which the match clock turns red and
        /// chimes once. The design calls it out as <c>00:10</c>.
        /// </summary>
        public const int TimerDangerSeconds = 10;

        /// <summary>True while the clock is inside its final ten seconds.</summary>
        public static bool IsTimerDanger(int totalSecondsRemaining) =>
            totalSecondsRemaining > 0 && totalSecondsRemaining <= TimerDangerSeconds;

        /// <summary>
        /// Package 11 A8 / F21. True for the mode whose stock display is
        /// "Stocks lost: N" rather than finite pips.
        ///
        /// <para>Takes the ordinal rather than the enum because the deterministic
        /// match component stores the mode as an <c>int</c>;
        /// <c>MatchMode.TimeLimit</c> is explicitly 1. Legacy
        /// <c>Hybrid == 2</c> normalizes to Stock, so anything that is not 1 keeps
        /// the pips — the safe direction, since showing pips for a mode that has
        /// them is merely redundant while hiding them in Stock would hide the
        /// elimination rule itself.</para>
        /// </summary>
        public static bool UsesStocksLostDisplay(int matchMode) =>
            matchMode == (int)MatchMode.TimeLimit;

        /// <summary>
        /// Package 11 A8 / F24. Status duration radial fill from the authoritative
        /// frame count. Ten seconds is the reference sweep: statuses do not publish
        /// their original duration anywhere the HUD can read it, and inventing a
        /// per-type duration here would create a second canonical value.
        /// </summary>
        public static float StatusRadialFraction(int remainingFrames, int tickRate) {
            if (remainingFrames <= 0 || tickRate <= 0) return 0f;
            return Mathf.Clamp(remainingFrames / (float)(tickRate * 10), 0f, 1f);
        }

        /// <summary>Bar fill in 0..1, safe against a zero or negative maximum.</summary>
        public static float BarFraction(int current, int maximum) {
            if (maximum <= 0) return 0f;
            return Mathf.Clamp(current / (float)maximum, 0f, 1f);
        }

        /// <summary>Meter fill in 0..1 against <see cref="MaxInfluence"/>.</summary>
        public static float MeterFraction(float influence) =>
            Mathf.Clamp(influence / MaxInfluence, 0f, 1f);

        /// <summary>The ultimate is available at a full meter, not on a cooldown.</summary>
        public static bool UltimateIsReady(float influence) => influence >= MaxInfluence;

        /// <summary>
        /// HP bar fill colour. Three bands rather than a continuous gradient: a
        /// player reads a state change, not a hue.
        /// </summary>
        public static Color HpFillColor(int current, int maximum) {
            float fraction = BarFraction(current, maximum);
            if (fraction <= HpDangerFraction) return UIPalette.BossRed;
            if (fraction <= HpCautionFraction) return UIPalette.Warning;
            return UIPalette.Hp;
        }

        /// <summary>
        /// Rally echo band (V7.1): the draining "win it back" sliver rendered
        /// directly above the current HP fill — the HP this fighter can reclaim
        /// by landing a direct hit before the 2.5 s drain empties it. Returns
        /// the band width in 0..1 of the bar, clamped so fill + band never
        /// overflows the bar. Zero when dead or when nothing is stashed.
        /// </summary>
        public static float EchoBandFraction(int currentHP, float echoPool, int maximumHP) {
            if (maximumHP <= 0 || echoPool <= 0f || currentHP <= 0) return 0f;
            float fill = BarFraction(currentHP, maximumHP);
            return Mathf.Clamp(echoPool / maximumHP, 0f, 1f - fill);
        }

        /// <summary>Pips lit, clamped into the authored capacity.</summary>
        public static int FilledPips(int current, int capacity) {
            if (capacity <= 0) return 0;
            return Mathf.Clamp(current, 0, capacity);
        }

        /// <summary>Remaining cooldown in seconds, rounded up for display.</summary>
        public static float CooldownSeconds(int frames, int tickRate) {
            if (frames <= 0 || tickRate <= 0) return 0f;
            return frames / (float)tickRate;
        }

        /// <summary>Short glyph key for a cooldown slot.</summary>
        public static string CooldownGlyphKey(FighterCooldownSlot slot) => slot switch {
            FighterCooldownSlot.SpecialOne => "fighter_hud_cooldown_special1",
            FighterCooldownSlot.SpecialTwo => "fighter_hud_cooldown_special2",
            _ => "fighter_hud_cooldown_movement"
        };

        /// <summary>
        /// A cooldown radial's fill: 1 when ready, otherwise the elapsed share of
        /// the cooldown that started at <paramref name="startFrames"/>. The start
        /// is whatever the authoritative remaining count was when it last rose, so
        /// a Resonance Momentum refund or a rollback correction moves the arc
        /// without the HUD holding a second copy of any cooldown length.
        /// </summary>
        public static float CooldownRadialFraction(int remainingFrames, int startFrames) {
            if (remainingFrames <= 0) return 1f;
            if (startFrames <= 0) return 0f;
            return Mathf.Clamp(1f - remainingFrames / (float)startFrames, 0f, 1f);
        }

        /// <summary>
        /// Status label key. Mirrors the family <c>TestArenaHUD</c> already used, so
        /// the production HUD and the debug layer never disagree about a status name.
        /// </summary>
        public static string StatusKey(StatusType status) => status == StatusType.None
            ? "status_none"
            : $"status_{status.ToString().ToLowerInvariant()}";

        /// <summary>
        /// Opacity actually applied to the HUD. Clamped to the same 0.2 floor
        /// <see cref="SaveManager"/> enforces, so a corrupt payload can never make
        /// the HUD invisible and unrecoverable from inside a match.
        /// </summary>
        public static float ResolveOpacity(float saved) => Mathf.Clamp(saved, 0.2f, 1f);

        // ---- TG-HUDF layout (UI_SPEC_GLASS 4.5; every number a spec size, the fit its UI-scale rule) ----
        //
        // The HUD lays out on a canvas of viewport / s design units that UiScaleBinder scales by s (the HUD scale), so a
        // design length L draws L x s screen px. 4.5's anchor model pins some lengths in SCREEN px: each cluster scales
        // about its own top corner (36 / 22 from the screen's corner at every s), the timer plate about its top centre
        // (22 from the top), and the lens rows keep their 46 px side margin while their top (192) scales with the HUD
        // (269 at 1.4). FighterHUD.ApplyLayout writes those pinned lengths as length / s.

        /// <summary>The reference viewport width (the project's 1920 x 1080).</summary>
        public const float ReferenceCanvasWidth = 1920f;
        /// <summary>Screen px from a cluster to its top corner (side, top): pinned at every UI scale.</summary>
        public const float ClusterMargin = 36f, ClusterTop = 22f;
        /// <summary>A cluster's height (header 30, gap 9, HP 44, gap 9, row 2 32, gap 9, Ultimate bar 16).</summary>
        public const float ClusterHeight = 149f;
        /// <summary>The lens rows: a pinned 46 px side margin; their top is a design length (192 at 1.0, 269 at 1.4).</summary>
        public const float SlotRowMargin = 46f, SlotRowTop = 192f;
        /// <summary>
        /// MM-HUD: the Momentum pips sit under the two Special lenses and their key labels (which end at 273): 5 px below them,
        /// spanning the two lenses and the gap between (54 + 20 + 54).
        /// </summary>
        public const float PipRowTop = 278f, PipRowWidth = 128f;
        /// <summary>The band the HUD lives in at 1.0 (the key labels end at 273, the pips at 287); it scales with the HUD.</summary>
        public const float TopBandHeight = 287f;
        /// <summary>The timer plate (GlassPanelTimer): 234 x 111.6, padding 12 / 32 / 12, its top pinned 22 px from the screen's.</summary>
        public const float TimerPlateWidth = 234f, TimerPlateHeight = 111.6f, TimerPlateTop = 22f;
        public const float TimerPlatePaddingX = 32f, TimerPlatePaddingY = 12f;
        /// <summary>The digits' line box (T34 at line-height 1), the gap, and the mode label's line box (T14 at 1.2).</summary>
        public const float TimerDigitsHeight = 60f, TimerLabelGap = 6f, TimerLabelHeight = 21.6f;
        /// <summary>Screen px every HP tube keeps from the timer plate.</summary>
        public const float TimerClearance = 24f;
        /// <summary>The portrait frame (110) plus the 20 px gap before the column.</summary>
        public const float RoundelColumnWidth = 130f;
        public const float HpBarFullWidth = 510f;
        /// <summary>The HP tube never draws narrower than this (screen px; 4.5's clamp floor).</summary>
        public const float HpBarMinScreenWidth = 300f;
        /// <summary>Below this tube width (design) the HP numerals drop their "/max" (the Rally echo band would sit under them).</summary>
        public const float HpMaxTextMinWidth = 460f;
        public const float HpBarHeight = 44f;
        public const float MeterBarFullWidth = 390f;
        public const float MeterBarHeight = 16f;
        /// <summary>Below this canvas width the status badges show their badge and ring only (the text column would collide).</summary>
        public const float ChipTextMinCanvas = 1700f;
        /// <summary>A shrunk hero name never draws smaller than this on screen (px; the 18 px floor of the type scale).</summary>
        public const float NameMinScreenPixels = 18f;

        /// <summary>
        /// The HP tube's width in screen px at HUD scale <paramref name="scale"/> (4.5):
        /// <c>clamp(510 s, 300, (V/2 - 117 s - 24) - (36 + 130 s))</c>, so it shrinks before it meets the timer plate
        /// (510 at 1.0, 554 at 1.4 against 714 unshrunk). The floor wins over the room, as the CSS clamp does.
        /// </summary>
        public static float HpBarScreenWidth(float scale, float viewportWidth = ReferenceCanvasWidth) {
            float s = Mathf.Max(0.01f, scale);
            float room = (viewportWidth / 2f - TimerPlateWidth / 2f * s - TimerClearance) - (ClusterMargin + RoundelColumnWidth * s);
            return Mathf.Max(HpBarMinScreenWidth, Mathf.Min(HpBarFullWidth * s, room));
        }

        /// <summary>The HP tube's width in design units (the screen width / s, floored to a whole pixel so the clearance holds).</summary>
        public static float HpBarWidth(float scale, float viewportWidth = ReferenceCanvasWidth) {
            float s = Mathf.Max(0.01f, scale);
            return Mathf.Floor(HpBarScreenWidth(s, viewportWidth) / s + 0.001f);
        }

        /// <summary>The Ultimate bar keeps 390 / 510 of its HP tube.</summary>
        public static float MeterBarWidth(float hpBarWidth) =>
            Mathf.Round(hpBarWidth * MeterBarFullWidth / HpBarFullWidth);

        /// <summary>A cluster's width: the portrait frame and its gap plus the HP tube.</summary>
        public static float ClusterWidth(float hpBarWidth) => RoundelColumnWidth + hpBarWidth;

        public static bool ChipsShowText(float canvasWidth) => canvasWidth >= ChipTextMinCanvas;

        /// <summary>
        /// The widest timer plate (design units) that keeps the clearance from both HP tubes at HUD scale
        /// <paramref name="scale"/>; never narrower than the normal plate (its width is what the HP rule leaves room for).
        /// </summary>
        public static float TimerPlateMaxWidth(float scale, float viewportWidth = ReferenceCanvasWidth) {
            float s = Mathf.Max(0.01f, scale);
            float tubeEnd = ClusterMargin + (RoundelColumnWidth + HpBarWidth(s, viewportWidth)) * s;
            float half = viewportWidth / 2f - tubeEnd - TimerClearance;
            return Mathf.Max(TimerPlateWidth, 2f * half / s);
        }

        /// <summary>
        /// Sudden Death's plate (4.5: "the plate widens to fit"): its one-line text plus the plate's sides, at least the
        /// normal plate and at most <paramref name="maxWidth"/> (the text wraps inside it beyond that).
        /// </summary>
        public static float SuddenDeathPlateWidth(float textWidth, float maxWidth) =>
            Mathf.Clamp(textWidth + 2f * TimerPlatePaddingX, TimerPlateWidth, Mathf.Max(TimerPlateWidth, maxWidth));

        /// <summary>
        /// The scale a hero name draws at so it fits <paramref name="room"/> (design px) in the header: 1 when it fits,
        /// else the fit, but never below <see cref="NameMinScreenPixels"/> on screen at HUD scale <paramref name="scale"/>
        /// (beyond that the name is ellipsized).
        /// </summary>
        public static float NameScale(float textWidth, float room, float fontSize, float scale) {
            if (textWidth <= 0f || room <= 0f || textWidth <= room) return 1f;
            float floor = Mathf.Min(1f, NameMinScreenPixels / Mathf.Max(1f, fontSize * Mathf.Max(0.01f, scale)));
            return Mathf.Clamp(room / textWidth, floor, 1f);
        }

        /// <summary>Kit icon for a status (UI_SPEC_GLASS 3.4.3).</summary>
        public static string StatusIconName(StatusType status) => status switch {
            StatusType.TimeDilation => "dilation",
            StatusType.Venom => "venom",
            StatusType.StaticCharge => "static",
            StatusType.RadiantBurn => "burn",
            StatusType.Root => "root",
            StatusType.Suppression => "suppress",
            _ => "venom"
        };

        /// <summary>
        /// MM-HUD: the Momentum gauge in pips for the sim's integer units (7200 to a pip, 0 to 4). Pure; the HUD feeds the pip
        /// row and the Special lenses' starved look through it, so the sim stub or the real gauge reads the same way.
        /// </summary>
        public static float MomentumPipFill(int units) =>
            Mathf.Clamp(units, 0, FTT.Combat.BasicComboRules.MomentumMaxUnits) / (float)FTT.Combat.BasicComboRules.MomentumUnitsPerPip;

        /// <summary>The small mode caption under the timer digits (a raw key; the label upper-cases it).</summary>
        public static string ModeLabelKey(int matchMode) =>
            UsesStocksLostDisplay(matchMode) ? "fighter_hud_mode_time" : "fighter_hud_mode_stock";
    }
}
