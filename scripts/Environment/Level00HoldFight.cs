using System;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>The steps of the Level 0 Part 1 hold fight (S02, design §3 beat 5).</summary>
    public enum HoldFightStep {
        /// <summary>The voice's first line is up; nothing has spawned yet.</summary>
        Opening,
        /// <summary>The locals are live; the voice taught the 3-hit string.</summary>
        FightOff,
        /// <summary>The last local winds up a telegraphed swing until one is blocked.</summary>
        Guard,
        /// <summary>The swing was blocked; the last local can be finished.</summary>
        Finish,
        /// <summary>The last local fell: "It's catching — hold on!"</summary>
        Done
    }

    /// <summary>
    /// Package 13 W3 (S02): the rules of Level 0's Part 1 hold fight, kept pure so
    /// the order is provable without a scene.
    ///
    /// <para>The beam's pull stalls — the Wardens have caught it — and visored
    /// locals of the hero's own era walk out of the drained world to drag the
    /// hero into the column. An unnamed voice (speaker "???", no portrait; it is
    /// Sarah, recognized in Part 2) coaches the fight: the 3-hit string first,
    /// then Block against one clearly telegraphed swing that repeats until it is
    /// blocked, then "It's catching — hold on!" as the last local falls.</para>
    ///
    /// <para><b>It cannot be lost.</b> The hero's HP cannot fall below
    /// <see cref="HeroHPFloor"/>, and nothing here uses or grants rewind charges,
    /// Rally, meter, dust or Integrity — <c>Level00Controller</c> enforces each of
    /// those; this class owns the step order and the counts.</para>
    /// </summary>
    public sealed class Level00HoldFight {

        /// <summary>The hold's HP floor: the fight is a story beat, never a death.</summary>
        public const int HeroHPFloor = 1;

        /// <summary>How many locals walk out of the tear.</summary>
        public const int LocalCount = 3;

        /// <summary>
        /// Locals that must fall to the ordinary fight before the Guard lesson:
        /// all but the last, which winds up the telegraphed swing.
        /// </summary>
        public const int LocalsBeforeGuard = LocalCount - 1;

        /// <summary>Frames of the telegraph before the scripted swing lands.</summary>
        public const int GuardTelegraphFrames = 50;

        /// <summary>Frames between a missed guard and the next telegraph.</summary>
        public const int GuardRepeatFrames = 70;

        /// <summary>
        /// The era's altered local mob each hero's hold reuses (design §6 era
        /// roster). Heroes with no shared level need a small Linked roster
        /// authored for Level 0; until one exists they reuse the nearest-era
        /// standard, recorded in the P13 W3 handoff. The fallback is the Paris
        /// rioter — a harnessed local, the most generic "people of this place"
        /// silhouette in the roster.
        /// </summary>
        private static readonly Dictionary<string, string> LocalMobByHero = new(StringComparer.Ordinal) {
            ["leonardo"] = "cyber_guard",
            ["joan"] = "laser_archer",
            ["tesla"] = "voltaic_shock_drone",
            ["cleopatra"] = "plasma_spear_ward",
            ["shakespeare"] = "holo_page",
            ["lincoln"] = "laser_rifle_infantry",
            // No shared level (S04): the nearest-era placeholder until a Level 0
            // Linked roster is authored.
            ["mozart"] = "chrono_rioter",
            ["einstein"] = "laser_rifle_infantry",
        };

        public const string FallbackLocalMob = "chrono_rioter";

        /// <summary>The era local the hold spawns for <paramref name="heroID"/>. Never null.</summary>
        public static string LocalMobFor(string heroID) =>
            !string.IsNullOrWhiteSpace(heroID) && LocalMobByHero.TryGetValue(heroID, out string mob)
                ? mob
                : FallbackLocalMob;

        public HoldFightStep Step { get; private set; } = HoldFightStep.Opening;
        public int LocalsDefeated { get; private set; }
        public int GuardAttempts { get; private set; }

        /// <summary>The voice's opening line finished: the locals come.</summary>
        public bool BeginFight() {
            if (Step != HoldFightStep.Opening) return false;
            Step = HoldFightStep.FightOff;
            return true;
        }

        /// <summary>
        /// A local fell. Returns the step the fight moved to (unchanged when it did
        /// not move). The last local cannot fall before its swing is blocked — the
        /// controller keeps it standing through the Guard step — so a kill during
        /// Guard is refused here and the lesson is not skipped.
        /// </summary>
        public HoldFightStep RegisterLocalDefeated() {
            switch (Step) {
                case HoldFightStep.FightOff:
                    LocalsDefeated++;
                    if (LocalsDefeated >= LocalsBeforeGuard) Step = HoldFightStep.Guard;
                    break;
                case HoldFightStep.Finish:
                    LocalsDefeated++;
                    if (LocalsDefeated >= LocalCount) Step = HoldFightStep.Done;
                    break;
            }
            return Step;
        }

        /// <summary>A telegraphed swing resolved. Only a blocked one advances the lesson.</summary>
        public bool RegisterGuardSwing(bool blocked) {
            if (Step != HoldFightStep.Guard) return false;
            GuardAttempts++;
            if (!blocked) return false;
            Step = HoldFightStep.Finish;
            return true;
        }

        /// <summary>
        /// Never strand the story beat: if the last local is lost to anything but
        /// the scripted path (freed, pooled away), the lesson closes and the beam
        /// breaks anyway.
        /// </summary>
        public bool ForceComplete() {
            if (Step == HoldFightStep.Done) return false;
            Step = HoldFightStep.Done;
            return true;
        }
    }
}
