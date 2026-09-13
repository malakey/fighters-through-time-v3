namespace FTT.Environment {

    /// <summary>
    /// Ordered calibration steps for the Level 0 tutorial's Part 2.
    ///
    /// <para><b>V7.6 rebuild (Package 11 A5).</b> The Special and Ultimate
    /// calibrations are <b>deleted</b>: under the V7.5 Legacy Unlock Schedule
    /// neither ability exists yet at Level 0, so a lesson that demanded one was
    /// an unclearable step. What the tutorial teaches instead is the defensive
    /// and agency layer the player <i>does</i> own from the first minute — the
    /// grab/block/strike triangle, DI and the landing tech, and the Ultimate
    /// Meter as the fuel for Defy History rather than for a cast.</para>
    ///
    /// <para>Order: basics → Rally → Block → <b>Grab</b> → <b>Hitstun DI</b> →
    /// <b>Landing tech</b> → <b>Meter &amp; Defy</b> → the scripted death-rewind
    /// demonstration → the player's own rewind lesson. The two rewind steps keep
    /// their V7.2 names because A2's Time Freeze workstream owns that region and
    /// renames <c>UseManualRewind</c> itself; A5 only re-orders around them.</para>
    /// </summary>
    public enum TutorialCalibrationStep {
        BasicHits,
        /// <summary>V7.1: the dummy lands one scripted hit, then the player
        /// reclaims the Rally echo by striking back before it fades.</summary>
        RallyReclaim,
        Block,
        /// <summary>V7.6: the dummy holds its own shield; the player must grab
        /// and throw it. "Grabs beat blocks. Strikes beat grabs."</summary>
        Grab,
        /// <summary>V7.6: one scripted launch, with the hitstop held a beat
        /// longer than normal so the player can read the DI prompt.</summary>
        HitstunDI,
        /// <summary>V7.6: the landing tech, repeating until one lands.</summary>
        LandingTech,
        /// <summary>V7.6: the meter fills to 100%, then a scripted lethal hit is
        /// refused by Defy History — the F13 seal goes lit, then broken.</summary>
        MeterAndDefy,
        UseRewind,
        /// <summary>V7.2: one manual scrubbed rewind (free). A2's Time Freeze
        /// workstream replaces this step's body with the Time Freeze drill.</summary>
        UseManualRewind,
        Done
    }

    /// <summary>
    /// Pure calibration-step machine for <see cref="Level00Controller"/> (audit
    /// M-3). Deliberately engine-free so the step transitions are unit-testable
    /// without a scene: the controller feeds it observed combat events and reads
    /// back whether the step advanced. Each Register method is phase-gated — an
    /// event from the wrong step never advances anything, so a player who blocks
    /// during the basic-hits step or grabs during the block step cannot skip a
    /// lesson.
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

        /// <summary>
        /// V7.6: one grab and throw closes the grab lesson. The dummy's shield
        /// persists until it lands, so swinging at it teaches the other half of
        /// the triangle for free.
        /// </summary>
        public const int RequiredGrabThrows = 1;

        /// <summary>
        /// V7.6: one successful landing tech closes the agency lesson. There is
        /// no attempt cap — the scripted launch repeats until a tech lands, and
        /// every repeat is free.
        /// </summary>
        public const int RequiredLandingTechs = 1;

        public TutorialCalibrationStep Step { get; private set; } = TutorialCalibrationStep.BasicHits;
        public int BasicHitsLanded { get; private set; }
        public int HitsBlocked { get; private set; }

        /// <summary>Swings the shielded grab dummy has absorbed. Drives the re-prompt.</summary>
        public int GrabLessonSwingsAbsorbed { get; private set; }

        /// <summary>Scripted launches delivered in the hitstun-agency lesson.</summary>
        public int ScriptedLaunchesDelivered { get; private set; }

        /// <summary>Landing techs the player has missed. Never strands anything.</summary>
        public int LandingTechsMissed { get; private set; }

        /// <summary>True once the DI beat has been shown (it proceeds regardless of input).</summary>
        public bool DirectionalInfluenceBeatSeen { get; private set; }

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
            Step = TutorialCalibrationStep.Grab;
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
            Step = TutorialCalibrationStep.Grab;
            return true;
        }

        /// <summary>
        /// V7.6 Grab Calibration: a swing the shielded dummy absorbed. It never
        /// advances anything — the whole point is that the player sees the
        /// shield eat the blade and is re-prompted toward the grab.
        /// </summary>
        public bool RegisterGrabLessonSwingAbsorbed() {
            if (Step != TutorialCalibrationStep.Grab) return false;
            GrabLessonSwingsAbsorbed++;
            return true;
        }

        /// <summary>
        /// V7.6 Grab Calibration: the player grabbed the shielded dummy and
        /// completed a throw. True when this advanced the step.
        /// </summary>
        public bool RegisterGrabThrow() {
            if (Step != TutorialCalibrationStep.Grab) return false;
            Step = TutorialCalibrationStep.HitstunDI;
            return true;
        }

        /// <summary>
        /// V7.6 Hitstun Agency: the scripted launch's extended hitstop has
        /// elapsed and the DI read resolved. The beat proceeds whether or not a
        /// direction was held — the lesson is that the option exists.
        /// </summary>
        public bool RegisterDirectionalInfluenceBeat() {
            if (Step != TutorialCalibrationStep.HitstunDI) return false;
            DirectionalInfluenceBeatSeen = true;
            ScriptedLaunchesDelivered++;
            Step = TutorialCalibrationStep.LandingTech;
            return true;
        }

        /// <summary>
        /// V7.6 Hitstun Agency: the player held Block through ground contact and
        /// recovered on their feet. True when this advanced the step.
        /// </summary>
        public bool RegisterLandingTech() {
            if (Step != TutorialCalibrationStep.LandingTech) return false;
            Step = TutorialCalibrationStep.MeterAndDefy;
            return true;
        }

        /// <summary>
        /// V7.6 Hitstun Agency: the knockdown played out untechnical. The lesson
        /// repeats — the launch is free, so there is nothing to lose by missing.
        /// </summary>
        public bool RegisterLandingTechMissed() {
            if (Step != TutorialCalibrationStep.LandingTech) return false;
            LandingTechsMissed++;
            ScriptedLaunchesDelivered++;
            return true;
        }

        /// <summary>
        /// V7.6 Meter &amp; Defy: the scripted lethal-tagged hit was refused by
        /// Defy History, spending the meter and breaking the F13 seal. True when
        /// this advanced the step.
        /// </summary>
        public bool RegisterDefyProc() {
            if (Step != TutorialCalibrationStep.MeterAndDefy) return false;
            Step = TutorialCalibrationStep.UseRewind;
            return true;
        }

        /// <summary>The scripted rewind demonstration completed; the player's own
        /// rewind lesson follows.</summary>
        public bool RegisterRewindComplete() {
            if (Step != TutorialCalibrationStep.UseRewind) return false;
            Step = TutorialCalibrationStep.UseManualRewind;
            return true;
        }

        /// <summary>V7.2: the player committed their own manual scrubbed rewind.</summary>
        public bool RegisterManualRewindComplete() {
            if (Step != TutorialCalibrationStep.UseManualRewind) return false;
            Step = TutorialCalibrationStep.Done;
            return true;
        }

        /// <summary>
        /// Never-strand fallback: the rewind demonstration could not run (no
        /// manager, or repeated refusals), so the calibration finishes without
        /// either rewind lesson.
        /// </summary>
        public bool SkipRewindDemonstration() {
            if (Step != TutorialCalibrationStep.UseRewind) return false;
            Step = TutorialCalibrationStep.Done;
            return true;
        }

        /// <summary>Never-strand fallback for the manual lesson alone.</summary>
        public bool SkipManualRewindLesson() {
            if (Step != TutorialCalibrationStep.UseManualRewind) return false;
            Step = TutorialCalibrationStep.Done;
            return true;
        }

        /// <summary>
        /// Never-strand fallback for the V7.6 Defy beat: a save that already
        /// spent Defy History this level (a mid-level resume) cannot show the
        /// proc, so the lesson closes on its coaching text alone.
        /// </summary>
        public bool SkipDefyLesson() {
            if (Step != TutorialCalibrationStep.MeterAndDefy) return false;
            Step = TutorialCalibrationStep.UseRewind;
            return true;
        }
    }

    /// <summary>
    /// Part 3 movement-ability gate rule (audit M-3).
    ///
    /// <para><b>V7.6:</b> Level 0's Part 3 no longer <i>contains</i> a movement
    /// gate — the Movement Ability unlocks after Level 1, so the traversal is
    /// authored for base jump reach and the gate was removed from
    /// <see cref="Level00Controller"/>. This rule is retained because it is the
    /// shape any later Wren calibration drill will reuse: the gate is satisfied
    /// by crossing the marked zone while the movement ability is active, or
    /// within a short freshness window after it fired — the window is what lets
    /// instant teleports (Einstein's Warp ends before the next physics poll can
    /// observe <c>UsingMovementAbility</c>) clear the same gate as travel
    /// abilities, so no gate needs character-specific geometry.</para>
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
