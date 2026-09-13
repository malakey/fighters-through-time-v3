namespace FTT.Environment {

    /// <summary>
    /// Ordered calibration steps for the Level 0 tutorial's Part 2, matching the
    /// designed sequence (design-godot.md, "Part 2: The Calibration"): basic
    /// attacks, then blocking, then the specials, then the forced-100%-meter
    /// Ultimate, then the scripted Chronal Rewind demonstration, and finally the
    /// V7.6 Time Freeze escape drill. The two time lessons stay last and in that
    /// order: the scripted demonstration path
    /// (<see cref="ChronalRewindManager.TriggerScriptedRewind"/>) teaches the
    /// finite death-save pool, then the drill teaches the escape verb.
    /// </summary>
    public enum TutorialCalibrationStep {
        BasicHits,
        /// <summary>V7.1: the dummy lands one scripted hit, then the player
        /// reclaims the Rally echo by striking back before it fades.</summary>
        RallyReclaim,
        Block,
        UseSpecial,
        UseUltimate,
        UseRewind,
        /// <summary>V7.6: the five-second Time Freeze escape drill — invulnerable
        /// enemies, a safe destination, and a freeze that is ready on every
        /// retry. Replaced the retired V7.2 manual-scrub lesson in place.</summary>
        UseTimeFreeze,
        Done
    }

    /// <summary>
    /// Pure calibration-step machine for <see cref="Level00Controller"/> (audit
    /// M-3). Deliberately engine-free so the step transitions are unit-testable
    /// without a scene: the controller feeds it observed combat events and reads
    /// back whether the step advanced. Each Register method is phase-gated — an
    /// event from the wrong step never advances anything, so a player who blocks
    /// during the basic-hits step or fires a special during the block step cannot
    /// skip a lesson.
    /// </summary>
    public sealed class TutorialCalibrationScript {

        /// <summary>Basic hits required on the dummy, per the existing lesson.</summary>
        public const int RequiredBasicHits = 3;

        /// <summary>
        /// Telegraphed dummy hits the player must absorb with the block stance.
        /// Two absorbs show the charge count falling without forcing the guard
        /// break daze on every playthrough; an actual guard break (all charges
        /// spent) completes the lesson immediately since it demonstrates the
        /// full depletion arc.
        /// </summary>
        public const int RequiredBlockedHits = 2;

        public TutorialCalibrationStep Step { get; private set; } = TutorialCalibrationStep.BasicHits;
        public int BasicHitsLanded { get; private set; }
        public int HitsBlocked { get; private set; }

        /// <summary>Counts a basic hit on the dummy. True when this advanced the step.</summary>
        public bool RegisterBasicHit() {
            if (Step != TutorialCalibrationStep.BasicHits) return false;
            BasicHitsLanded++;
            if (BasicHitsLanded < RequiredBasicHits) return false;
            Step = TutorialCalibrationStep.RallyReclaim;
            return true;
        }

        /// <summary>
        /// V7.1 Rally beat: the dummy landed its scripted hit and the player
        /// struck back (reclaiming the echo). True when this advanced the step.
        /// </summary>
        public bool RegisterRallyReclaimHit() {
            if (Step != TutorialCalibrationStep.RallyReclaim) return false;
            Step = TutorialCalibrationStep.Block;
            return true;
        }

        /// <summary>Counts a successfully blocked dummy hit. True when this advanced the step.</summary>
        public bool RegisterBlockedHit() {
            if (Step != TutorialCalibrationStep.Block) return false;
            HitsBlocked++;
            if (HitsBlocked < RequiredBlockedHits) return false;
            Step = TutorialCalibrationStep.UseSpecial;
            return true;
        }

        /// <summary>
        /// A guard break (last shield charge spent) completes the block lesson
        /// outright — the player has seen the entire depletion arc the design
        /// asks the step to teach.
        /// </summary>
        public bool RegisterGuardBreak() {
            if (Step != TutorialCalibrationStep.Block) return false;
            HitsBlocked = RequiredBlockedHits;
            Step = TutorialCalibrationStep.UseSpecial;
            return true;
        }

        /// <summary>
        /// A special-ability cooldown began. Only the two special slots satisfy
        /// the lesson — the movement ability and the ultimate must not skip it
        /// (the old controller accepted any cooldown event; audit M-3).
        /// </summary>
        public bool RegisterSpecialUsed(FTT.Core.AbilitySlot slot) {
            if (Step != TutorialCalibrationStep.UseSpecial) return false;
            if (slot != FTT.Core.AbilitySlot.Special1 && slot != FTT.Core.AbilitySlot.Special2) return false;
            Step = TutorialCalibrationStep.UseUltimate;
            return true;
        }

        /// <summary>The granted-meter ultimate was triggered.</summary>
        public bool RegisterUltimateUsed() {
            if (Step != TutorialCalibrationStep.UseUltimate) return false;
            Step = TutorialCalibrationStep.UseRewind;
            return true;
        }

        /// <summary>The scripted death-rewind demonstration completed; the Time
        /// Freeze escape drill follows.</summary>
        public bool RegisterRewindComplete() {
            if (Step != TutorialCalibrationStep.UseRewind) return false;
            Step = TutorialCalibrationStep.UseTimeFreeze;
            return true;
        }

        /// <summary>V7.6: the player escaped the drill under their own Time Freeze.</summary>
        public bool RegisterTimeFreezeComplete() {
            if (Step != TutorialCalibrationStep.UseTimeFreeze) return false;
            Step = TutorialCalibrationStep.Done;
            return true;
        }

        /// <summary>
        /// Never-strand fallback: the rewind demonstration could not run (no
        /// manager, or repeated refusals), so the calibration finishes without
        /// either time lesson.
        /// </summary>
        public bool SkipRewindDemonstration() {
            if (Step != TutorialCalibrationStep.UseRewind) return false;
            Step = TutorialCalibrationStep.Done;
            return true;
        }

        /// <summary>Never-strand fallback for the Time Freeze drill alone.</summary>
        public bool SkipTimeFreezeLesson() {
            if (Step != TutorialCalibrationStep.UseTimeFreeze) return false;
            Step = TutorialCalibrationStep.Done;
            return true;
        }
    }

    /// <summary>
    /// Part 3 movement-ability gate rule (audit M-3). The gate is satisfied by
    /// crossing the marked zone while the movement ability is active, or within a
    /// short freshness window after it fired — the window is what lets instant
    /// teleports (Einstein's Warp ends before the next physics poll can observe
    /// <c>UsingMovementAbility</c>) clear the same gate as travel abilities, so
    /// no gate needs character-specific geometry.
    /// </summary>
    public static class TutorialMobilityRules {

        /// <summary>Frames after a movement ability fires during which a zone crossing counts.</summary>
        public const int MovementAbilityFreshnessFrames = 60;

        public static bool MovementGateSatisfied(
            bool playerInZone, bool usingMovementAbility, int framesSinceMovementAbility) {
            if (!playerInZone) return false;
            if (usingMovementAbility) return true;
            return framesSinceMovementAbility >= 0
                && framesSinceMovementAbility <= MovementAbilityFreshnessFrames;
        }
    }
}
