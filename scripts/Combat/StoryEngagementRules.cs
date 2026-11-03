using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Combat {

    /// <summary>
    /// The Story engagement director's rulebook (gameplay update 2026-10-12,
    /// request 6, workstream GU-F; <c>docs/GAMEPLAY_UPDATE_2026-10-12_PLAN.md</c>
    /// §2.6). The user asked that mobs "maintain more separation, especially on
    /// normal mode" and "wait their turn to come and attack the character", so
    /// only a few mobs may commit to melee on the campaign hero at once; the rest
    /// hold on a spaced wait ring on both sides and rotate in.
    ///
    /// <para>Every number lives here once. <c>FTT.Enemies.StoryEngagementDirector</c>
    /// (the per-target registry) and <c>EnemyController</c>'s engagement partial
    /// read it; nothing else does. The slot totals (Easy 1 / Normal 2 / Hard 3)
    /// are the user's 2026-10-12 ruling; every other value is
    /// <i>Provisional</i>, recorded for the closeout's
    /// <c>PROVISIONAL-ENGAGEMENT-DIRECTOR-RULES</c> ledger row.</para>
    ///
    /// <para><b>Scope guard:</b> Story Mode only. Plain constants and pure
    /// functions with no Godot types, and the difficulty is always a parameter
    /// (never <c>GameManager</c>), so the pure-C# test host can pin every rule.
    /// Nothing in <c>scripts/FighterSim/</c> may name this class.</para>
    /// </summary>
    public static class StoryEngagementRules {

        /// <summary>What the attack a mob will throw next needs from the director.</summary>
        public enum EngagementClass {
            /// <summary>Ungated: a self-buff, a summon, or the reactive Phase Skip.</summary>
            Free,
            /// <summary>Needs one of the target's melee slots before the mob may commit.</summary>
            MeleeSlot,
            /// <summary>Needs one of the target's concurrent ranged permits before the mob may commit.</summary>
            RangedPermit
        }

        /// <summary>
        /// The mob's place in the target's rotation. <c>Waiting</c> covers every
        /// joined mob without a melee slot, whatever its next attack needs.
        /// </summary>
        public enum EngagementRole {
            /// <summary>Not engaged with any target (patrolling, returning, dead, or pooled).</summary>
            None,
            /// <summary>Joined, without a melee slot: a melee mob holds on the wait ring.</summary>
            Waiting,
            /// <summary>Holds one of the target's melee slots and may commit.</summary>
            Slot
        }

        // === The difficulty table ===

        /// <summary>
        /// Diagnostics seam (<c>PlaytestOptions</c>, <c>--slot-table=legacy</c>): a
        /// same-build A/B of the slot counts. Null in the game and in every test
        /// that does not set it, in <c>finally</c>; the table below stays the one
        /// rule. The old numbers live in <c>PlaytestSlotTables</c>, not here.
        /// </summary>
        internal static Func<Difficulty, (int Total, int PerSide)> SlotTableOverride;

        /// <summary>
        /// Melee slots on one target, both sides together. <b>The user's
        /// 2026-11-03 ruling 1a+:</b> Easy 2 / Normal 3 / Hard 4 (it amends
        /// 2026-10-12's 1 / 2 / 3).
        /// </summary>
        public static int MeleeSlotsTotal(Difficulty difficulty) =>
            SlotTableOverride != null ? SlotTableOverride(difficulty).Total : difficulty switch {
                Difficulty.Easy => 2,
                Difficulty.Hard => 4,
                _ => 3
            };

        /// <summary>
        /// Melee slots on one side of the target. <b>Ruling 1a+:</b> Easy 1 /
        /// Normal 2 / Hard 2, so Easy's two need one attacker on each side,
        /// Normal's three are two and one, and Hard's four have no slack.
        /// </summary>
        public static int MeleeSlotsPerSide(Difficulty difficulty) =>
            SlotTableOverride != null ? SlotTableOverride(difficulty).PerSide : difficulty switch {
                Difficulty.Easy => 1,
                _ => 2
            };

        /// <summary>
        /// Ranged attacks on one target at the same time (<i>Provisional</i>):
        /// Easy 1 / Normal 2 / Hard 3. A permit is stateless — the count of the
        /// target's members in <c>Attacking</c> with a ranged-class attack.
        /// </summary>
        public static int RangedPermits(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 1,
            Difficulty.Hard => 3,
            _ => 2
        };

        /// <summary>
        /// The wait ring's rank-0 gap, pivot to pivot, in pixels
        /// (<i>Provisional</i>): Easy 200 / Normal 170 / Hard 150. Every value sits
        /// above the reactive Phase Skip radius (125 px,
        /// <c>EnemyController.ReactiveTeleportRangePixels</c>), above the
        /// farthest melee commit line (77 px, D6c) and above the hero's hit-1
        /// pivot reach, so a waiter stands just outside the string and the hero
        /// has to step in to it.
        /// </summary>
        public static float WaitRingRankZeroGapPixels(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 200f,
            Difficulty.Hard => 150f,
            _ => 170f
        };

        /// <summary>
        /// The mob's own unfrozen ticks after it hands off its slot before it
        /// ranks with the rested waiters again (<i>Provisional</i>): Easy 90 /
        /// Normal 45 / Hard 20. A resting waiter is never handed a slot, and takes
        /// a free one only when no rested waiter wants it.
        /// </summary>
        public static int RestFramesAfterHandoff(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 90,
            Difficulty.Hard => 20,
            _ => 45
        };

        // === Shared values (Provisional) ===

        /// <summary>
        /// Spacing between wait-ring ranks on one side, in pixels: at least the
        /// widest mob pushbox (the elite's 48 px) plus 16, so two waiters never
        /// share an x.
        /// </summary>
        public const float WaitRingRankSpacingPixels = 64f;

        /// <summary>
        /// A waiter within this many pixels of its ring position stops there
        /// (the dead band that keeps it from oscillating on a moving target).
        /// </summary>
        public const float WaitRingTolerancePixels = 12f;

        /// <summary>
        /// A slot holder that has spent this many consecutive Chase ticks without
        /// committing while an eligible waiter exists hands its slot off (it
        /// cannot reach or commit, for example under a hero on a ledge).
        /// </summary>
        public const int StaleSlotFrames = 120;

        /// <summary>
        /// Half the zoomed Story view's world width, in pixels: the reference
        /// viewport width over <c>StoryCameraRules.ViewZoom</c>, halved
        /// (1920 / 1.5 / 2 = 640). Derived at compile time — the camera rulebook
        /// owns the zoom, and const arithmetic keeps this class engine-free.
        /// </summary>
        public const float ZoomedHalfViewPixels =
            FTT.Environment.StoryCameraRules.ReferenceViewportWidth / FTT.Environment.StoryCameraRules.ViewZoom / 2f;

        /// <summary>How far inside the half view the ranged hold line sits, in pixels.</summary>
        public const float RangedHoldViewMarginPixels = 80f;

        /// <summary>
        /// How far inside the half view a capped hold band's release line sits,
        /// in pixels: the band keeps a 40 px dead band above the hold line and
        /// still releases on screen.
        /// </summary>
        public const float RangedHoldReleaseViewMarginPixels = 40f;

        /// <summary>
        /// The farthest a mob's AttackRange-based stand-off band may hold, in
        /// pixels: the zoomed half view less <see cref="RangedHoldViewMarginPixels"/>
        /// (640 − 80 = 560), so a shooter whose projectile-widened range would
        /// hold it off screen stays on screen. Only the hold line is capped; the
        /// commit range is unchanged.
        /// </summary>
        public const float RangedHoldMaxPixels = ZoomedHalfViewPixels - RangedHoldViewMarginPixels;

        /// <summary>
        /// The farthest a capped hold band's release line may sit, in pixels: the
        /// zoomed half view less <see cref="RangedHoldReleaseViewMarginPixels"/>
        /// (640 − 40 = 600). A capped shooter waiting in its band (for its
        /// cooldown or a ranged permit) follows a hero who backs away before the
        /// hero's step takes it off screen (review fix 2026-10-13: the release
        /// used to keep the uncapped hysteresis ratio, 724.7 px for the sentry).
        /// </summary>
        public const float RangedHoldReleaseMaxPixels = ZoomedHalfViewPixels - RangedHoldReleaseViewMarginPixels;

        /// <summary>A waiter's approach toward its ring position, as a fraction of its own chase speed.</summary>
        public const float WaiterApproachSpeedFraction = 0.8f;

        /// <summary>A waiter's back-off away from the target, as a fraction of its own chase speed.</summary>
        public const float WaiterBackOffSpeedFraction = 0.6f;

        /// <summary>
        /// Absolute waiter speed cap, as a fraction of the hero's run speed read
        /// at runtime: a waiter never repositions faster than 0.9 of the hero,
        /// whatever its own speed (17 roster mobs outrun Lincoln at ×0.8).
        /// </summary>
        public const float WaiterHeroSpeedCapFraction = 0.9f;

        /// <summary>The bucket the candidates' gaps are compared in, in pixels (priority key 5).</summary>
        public const float PriorityGapBucketPixels = 32f;

        /// <summary>
        /// Own unfrozen ticks after a crossing before the same waiter may start
        /// another, so a waiter never ping-pongs between two cut-off sides.
        /// </summary>
        public const int CrossingCooldownFrames = 60;

        // === Rules ===

        /// <summary>
        /// The class of the attack a mob will throw next. MeleeStrike, ChargeDash
        /// and AreaPulse need a melee slot, as does a standard's engage blink
        /// (a Teleport that closes the gap, <paramref name="isEngageBlink"/>);
        /// Projectile, Shockwave, PersistentFieldAtTarget and SiphonTether need a
        /// ranged permit; ShieldBubble, SummonMinions and the reactive Phase
        /// Skip are free.
        /// </summary>
        public static EngagementClass ClassOf(EnemyAbilityArchetype archetype, bool isEngageBlink) => archetype switch {
            EnemyAbilityArchetype.MeleeStrike => EngagementClass.MeleeSlot,
            EnemyAbilityArchetype.ChargeDash => EngagementClass.MeleeSlot,
            EnemyAbilityArchetype.AreaPulse => EngagementClass.MeleeSlot,
            EnemyAbilityArchetype.Teleport => isEngageBlink ? EngagementClass.MeleeSlot : EngagementClass.Free,
            EnemyAbilityArchetype.Projectile => EngagementClass.RangedPermit,
            EnemyAbilityArchetype.Shockwave => EngagementClass.RangedPermit,
            EnemyAbilityArchetype.PersistentFieldAtTarget => EngagementClass.RangedPermit,
            EnemyAbilityArchetype.SiphonTether => EngagementClass.RangedPermit,
            _ => EngagementClass.Free
        };

        /// <summary>
        /// The gap, pivot to pivot, at which wait-ring rank <paramref name="rank"/>
        /// (0 = nearest) stands on its side of the target.
        /// </summary>
        public static float WaitGapForRank(int rank, Difficulty difficulty) =>
            WaitRingRankZeroGapPixels(difficulty) + Math.Max(0, rank) * WaitRingRankSpacingPixels;

        /// <summary>
        /// A waiter's horizontal speed in px/s: its own chase speed times the
        /// approach or back-off fraction, never above
        /// <see cref="WaiterHeroSpeedCapFraction"/> of the hero's run speed.
        /// </summary>
        public static float WaiterSpeedPixels(float mobSpeedPixels, float heroRunSpeedPixels, bool towardTarget) {
            float fraction = towardTarget ? WaiterApproachSpeedFraction : WaiterBackOffSpeedFraction;
            float own = Math.Max(0f, mobSpeedPixels) * fraction;
            float cap = Math.Max(0f, heroRunSpeedPixels) * WaiterHeroSpeedCapFraction;
            return Math.Min(own, cap);
        }

        /// <summary>
        /// The ranged hold cap on an AttackRange-based stand-off engage line.
        /// </summary>
        public static float CappedHoldEngagePixels(float engagePixels) => Math.Min(engagePixels, RangedHoldMaxPixels);

        /// <summary>
        /// The release line of a band whose engage line the ranged hold cap
        /// moved: its own hysteresis line, never past
        /// <see cref="RangedHoldReleaseMaxPixels"/>.
        /// </summary>
        public static float CappedHoldResumePixels(float resumePixels) => Math.Min(resumePixels, RangedHoldReleaseMaxPixels);

        /// <summary>True when a holder's stale count has reached <see cref="StaleSlotFrames"/>.</summary>
        public static bool IsStale(int staleFrames) => staleFrames >= StaleSlotFrames;

        /// <summary>
        /// Side rebalancing: a waiter of rank ≥ 1 whose ring position on its own
        /// side is cut off (by a wall, a ledge or a barrier) crosses to the other
        /// side only when that side holds fewer engaged melee mobs (holders and
        /// waiters) than its own. Rank 0 stays: it is next in line.
        /// </summary>
        public static bool ShouldCrossToOtherSide(int rank, bool ownPositionCutOff, int ownPresence, int otherPresence) =>
            rank >= 1 && ownPositionCutOff && otherPresence < ownPresence;

        /// <summary>
        /// Ranged fairness: of the members whose ranged commit is being refused
        /// for want of a permit, the one refused longest (ties: the lower
        /// instance id) takes the next free permit, so tree order never starves a
        /// shooter. True when <paramref name="otherRefusedFrames"/> /
        /// <paramref name="otherInstanceId"/> outranks the asking mob.
        /// </summary>
        public static bool RangedWaiterOutranks(int otherRefusedFrames, ulong otherInstanceId,
            int ownRefusedFrames, ulong ownInstanceId) =>
            otherRefusedFrames > 0
            && (otherRefusedFrames > ownRefusedFrames
                || (otherRefusedFrames == ownRefusedFrames && otherInstanceId < ownInstanceId));

        /// <summary>
        /// True when a side holding <paramref name="sideHolders"/> slots, with
        /// <paramref name="totalHolders"/> held overall, has a free melee slot.
        /// </summary>
        public static bool SideHasFreeSlot(int sideHolders, int totalHolders, Difficulty difficulty) =>
            totalHolders < MeleeSlotsTotal(difficulty) && sideHolders < MeleeSlotsPerSide(difficulty);

        /// <summary>
        /// The hand-off test: once the holder on <paramref name="holderSide"/>
        /// releases its slot, could a waiter on <paramref name="waiterSide"/>
        /// take it? (Its own side's per-side cap still applies.)
        /// </summary>
        public static bool CanTakeFreedSlot(int waiterSide, int holderSide, int holdersLeft, int holdersRight,
            Difficulty difficulty) {
            int left = holdersLeft - (holderSide < 0 ? 1 : 0);
            int right = holdersRight - (holderSide < 0 ? 0 : 1);
            return SideHasFreeSlot(waiterSide < 0 ? left : right, left + right, difficulty);
        }

        /// <summary>One mob that wants a melee slot on a target, as the priority rule sees it.</summary>
        public readonly struct Candidate {
            /// <summary>The mob's stable tie-break identity (its Godot instance id).</summary>
            public readonly ulong InstanceId;
            /// <summary>An elite ranks before any standard in the same rest state.</summary>
            public readonly bool IsElite;
            /// <summary>Its rest after a hand-off has run out (the first priority key).</summary>
            public readonly bool Rested;
            /// <summary>−1: left of the target; +1: right of it.</summary>
            public readonly int Side;
            /// <summary>Its own ticks spent waiting since it last held a slot.</summary>
            public readonly int WaitedFrames;
            /// <summary>Its horizontal gap to the target, pivot to pivot, in pixels.</summary>
            public readonly float GapPixels;

            public Candidate(ulong instanceId, bool isElite, bool rested, int side, int waitedFrames, float gapPixels) {
                InstanceId = instanceId;
                IsElite = isElite;
                Rested = rested;
                Side = side < 0 ? -1 : 1;
                WaitedFrames = waitedFrames;
                GapPixels = gapPixels;
            }
        }

        /// <summary>
        /// The priority order for a free melee slot, best first: a rested waiter
        /// before a resting one; then an elite before a standard; the side with
        /// fewer holders; more waited frames (round-robin, so a mob that just
        /// handed off goes to the back); the nearer
        /// <see cref="PriorityGapBucketPixels"/> bucket; and the lower instance
        /// id. Negative when <paramref name="a"/> ranks first.
        ///
        /// <para>Rest outranks tier (review fix 2026-10-13, a deviation from the
        /// plan's literal "elite → rest" order): a holder that has just handed
        /// off is resting, and an elite ranked ahead of every rested standard
        /// took its slot straight back, so the hand-off and the stale rule never
        /// passed an elite's turn on. Elites still go first among rested
        /// waiters (and among resting ones).</para>
        /// </summary>
        public static int ComparePriority(in Candidate a, in Candidate b, int holdersLeft, int holdersRight) {
            if (a.Rested != b.Rested) return a.Rested ? -1 : 1;
            if (a.IsElite != b.IsElite) return a.IsElite ? -1 : 1;
            int aSideHolders = a.Side < 0 ? holdersLeft : holdersRight;
            int bSideHolders = b.Side < 0 ? holdersLeft : holdersRight;
            if (aSideHolders != bSideHolders) return aSideHolders < bSideHolders ? -1 : 1;
            if (a.WaitedFrames != b.WaitedFrames) return a.WaitedFrames > b.WaitedFrames ? -1 : 1;
            int aBucket = GapBucket(a.GapPixels);
            int bBucket = GapBucket(b.GapPixels);
            if (aBucket != bBucket) return aBucket < bBucket ? -1 : 1;
            return a.InstanceId.CompareTo(b.InstanceId);
        }

        private static int GapBucket(float gapPixels) =>
            (int)MathF.Floor(Math.Max(0f, gapPixels) / PriorityGapBucketPixels);

        /// <summary>
        /// Hands the target's free melee slots to the best candidates: sorts
        /// <paramref name="candidates"/> in place by <see cref="ComparePriority"/>
        /// (an allocation-free insertion sort — a target's candidates are a
        /// handful), then grants down the order while slots remain overall and on
        /// the candidate's side. Writes the granted instance ids, best first, into
        /// <paramref name="granted"/> (cleared first) and returns their count.
        /// </summary>
        public static int SelectGrants(List<Candidate> candidates, int holdersLeft, int holdersRight,
            Difficulty difficulty, List<ulong> granted) {
            granted.Clear();
            int freeTotal = MeleeSlotsTotal(difficulty) - holdersLeft - holdersRight;
            if (freeTotal <= 0 || candidates.Count == 0) return 0;
            int perSide = MeleeSlotsPerSide(difficulty);
            int freeLeft = perSide - holdersLeft;
            int freeRight = perSide - holdersRight;
            for (int index = 1; index < candidates.Count; index++) {
                Candidate moving = candidates[index];
                int slot = index - 1;
                while (slot >= 0 && ComparePriority(in moving, candidates[slot], holdersLeft, holdersRight) < 0) {
                    candidates[slot + 1] = candidates[slot];
                    slot--;
                }
                candidates[slot + 1] = moving;
            }
            foreach (Candidate candidate in candidates) {
                if (freeTotal <= 0) break;
                if (candidate.Side < 0) {
                    if (freeLeft <= 0) continue;
                    freeLeft--;
                } else {
                    if (freeRight <= 0) continue;
                    freeRight--;
                }
                freeTotal--;
                granted.Add(candidate.InstanceId);
            }
            return granted.Count;
        }
    }
}
