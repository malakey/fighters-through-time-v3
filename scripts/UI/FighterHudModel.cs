using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>The four ability slots the Fighter HUD shows a cooldown for.</summary>
    public enum FighterCooldownSlot {
        SpecialOne = 0,
        SpecialTwo = 1,
        Movement = 2,
        Ultimate = 3
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
        public const float MaxInfluence = 100f;

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
            return UIPalette.Cyan;
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
            FighterCooldownSlot.Movement => "fighter_hud_cooldown_movement",
            _ => "fighter_hud_cooldown_ultimate"
        };

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
    }
}
