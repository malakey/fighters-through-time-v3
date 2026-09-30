using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// Package 13 W3 (S02): the skip path's first-use tooltips.
    ///
    /// <para>A player who answers Wren's Level 0 offer with <b>Skip — I'll learn in
    /// the field</b> never runs the Full Calibration. Each skipped lesson is instead
    /// delivered as a one-line tooltip the first time its mechanic becomes relevant
    /// in play. Tooltips pause nothing and show <b>once per save slot</b>.</para>
    ///
    /// <para>The ledger is two additive <see cref="StorySaveData"/> fields —
    /// <see cref="StorySaveData.SkippedCalibration"/> and
    /// <see cref="StorySaveData.ShownFirstUseTooltips"/>. A slot that ran the Full
    /// Calibration (every pre-v8 slot included) never shows one.</para>
    ///
    /// <para>Pure C#: no engine call, so the rules are provable in the plain test
    /// host. The in-scene presenter is <c>FTT.UI.FirstUseTooltipPresenter</c>.</para>
    /// </summary>
    public static class FirstUseTooltips {

        public const string RallyEcho = "rally_echo";
        public const string ShieldBreak = "shield_break";
        public const string Grab = "grab";
        public const string Launch = "launch";
        public const string Defy = "defy";
        public const string Ledge = "ledge";
        public const string OneWayPlatform = "one_way";
        public const string DeathRewind = "death_rewind";
        public const string TimeFreeze = "time_freeze";

        /// <summary>
        /// Every tooltip the design lists, in the order the design lists them
        /// (first Rally echo, first shield break, first grab opportunity, first
        /// launch, first full meter / Defy proc, first ledge, first one-way
        /// platform, first death rewind, first Time Freeze availability).
        /// </summary>
        public static readonly IReadOnlyList<string> All = new[] {
            RallyEcho, ShieldBreak, Grab, Launch, Defy, Ledge, OneWayPlatform, DeathRewind, TimeFreeze
        };

        /// <summary>
        /// The translation key for a tooltip. Spelled out rather than built, so the
        /// unused-key sweep sees every row it references.
        /// </summary>
        public static string KeyFor(string tooltipID) => tooltipID switch {
            RallyEcho => "tooltip_first_use_rally_echo",
            ShieldBreak => "tooltip_first_use_shield_break",
            Grab => "tooltip_first_use_grab",
            Launch => "tooltip_first_use_launch",
            Defy => "tooltip_first_use_defy",
            Ledge => "tooltip_first_use_ledge",
            OneWayPlatform => "tooltip_first_use_one_way",
            DeathRewind => "tooltip_first_use_death_rewind",
            TimeFreeze => "tooltip_first_use_time_freeze",
            _ => ""
        };

        /// <summary>True when <paramref name="save"/> should show this tooltip now.</summary>
        public static bool ShouldShow(StorySaveData save, string tooltipID) {
            if (save == null || !save.SkippedCalibration) return false;
            if (string.IsNullOrEmpty(KeyFor(tooltipID))) return false;
            return save.ShownFirstUseTooltips == null || !save.ShownFirstUseTooltips.Contains(tooltipID);
        }

        /// <summary>
        /// Records the tooltip as shown. Returns true exactly once per slot per
        /// tooltip — the caller shows it only on a true.
        /// </summary>
        public static bool TryConsume(StorySaveData save, string tooltipID) {
            if (!ShouldShow(save, tooltipID)) return false;
            save.ShownFirstUseTooltips ??= new List<string>();
            save.ShownFirstUseTooltips.Add(tooltipID);
            return true;
        }

        /// <summary>
        /// The Skip choice: arms the ledger. Idempotent, and it never un-shows a
        /// tooltip already consumed.
        /// </summary>
        public static void ArmForSkippedCalibration(StorySaveData save) {
            if (save == null) return;
            save.SkippedCalibration = true;
            save.ShownFirstUseTooltips ??= new List<string>();
        }
    }
}
