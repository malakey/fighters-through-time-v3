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
    /// Calibration (every pre-v8 slot included) shows none of the skipped lessons.
    /// <see cref="SpecialMomentum"/> alone shows on every slot, once: no calibration
    /// teaches Specials, because the hero has none until Level 0 is done.</para>
    ///
    /// <para>Pure C#: no engine call, so the rules are provable in the plain test
    /// host. The in-scene presenter is <c>FTT.UI.FirstUseTooltipPresenter</c>.</para>
    /// </summary>
    public static class FirstUseTooltips {

        public const string RallyEcho = "rally_echo";
        public const string ShieldBreak = "shield_break";
        public const string Grab = "grab";
        public const string Launch = "launch";
        public const string DefyButton = "defy_button";
        public const string Ledge = "ledge";
        public const string OneWayPlatform = "one_way";
        public const string DeathRewind = "death_rewind";
        public const string TimeFreeze = "time_freeze";
        public const string SpecialMomentum = "special_momentum";

        /// <summary>
        /// Every tooltip the design lists, in the order the design lists them
        /// (first Rally echo, first shield break, first grab opportunity, first
        /// launch, first time the meter reaches the Defy cost, first ledge, first one-way
        /// platform, first death rewind, first Time Freeze availability), then the
        /// 2026-11-03 schedule shift's one addition: Special 1 on Level 1.
        /// </summary>
        public static readonly IReadOnlyList<string> All = new[] {
            RallyEcho, ShieldBreak, Grab, Launch, DefyButton, Ledge, OneWayPlatform, DeathRewind, TimeFreeze,
            SpecialMomentum
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
            DefyButton => "tooltip_first_use_defy_button",
            Ledge => "tooltip_first_use_ledge",
            OneWayPlatform => "tooltip_first_use_one_way",
            DeathRewind => "tooltip_first_use_death_rewind",
            TimeFreeze => "tooltip_first_use_time_freeze",
            SpecialMomentum => "tooltip_first_use_special_momentum",
            _ => ""
        };

        /// <summary>
        /// MM-TEXT (P27): the Defy button tooltip is due on the first tick the hero's Influence meter reaches the Defy
        /// cost (<see cref="FTT.Combat.BasicComboRules.DefyInfluenceCost"/>). The old seal no longer raises it.
        /// </summary>
        public static bool DefyButtonDue(float meter) => meter >= FTT.Combat.BasicComboRules.DefyInfluenceCost;

        /// <summary>
        /// GR-SCHED (ruling 3b): Special 1 is restored by completing Level 0 and is
        /// first played on Level 1 (Florence), where nothing else teaches it. The
        /// tooltip is due on Level 1 once the hero's Special 1 is unlocked, on every
        /// slot (<see cref="ShowsOnEverySlot"/>).
        /// </summary>
        public static bool SpecialMomentumDue(int levelNumber, bool special1Unlocked) =>
            levelNumber == 1 && special1Unlocked;

        /// <summary>
        /// True for a tooltip no calibration teaches, so a Full-Calibration slot needs it too.
        /// Every other tooltip stands in for a skipped lesson.
        /// </summary>
        public static bool ShowsOnEverySlot(string tooltipID) => tooltipID == SpecialMomentum;

        /// <summary>True when <paramref name="save"/> should show this tooltip now.</summary>
        public static bool ShouldShow(StorySaveData save, string tooltipID) {
            if (save == null) return false;
            if (!save.SkippedCalibration && !ShowsOnEverySlot(tooltipID)) return false;
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
