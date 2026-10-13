using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay update 2026-10-12, request 6 (GU-F): the pure Story engagement
/// rulebook (<see cref="StoryEngagementRules"/>) — the slot table, the attack
/// classes, the priority order, the grant rule, the wait ring, the waiter speed
/// cap, the ranged hold cap, and the stale, rebalancing and ranged-fairness
/// rules. Engine-free on purpose — no <c>[RequireGodotRuntime]</c>, so nothing
/// here may touch a Godot API (the difficulty is always a parameter). The
/// behaviour these numbers drive is pinned in real frames by
/// <c>StoryEngagementDirectorTests</c>.
/// </summary>
[TestSuite]
public class StoryEngagementRulesTests {

    private static readonly Difficulty[] Difficulties = { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };

    [TestCase]
    public void TheSlotTotalsAreTheUsersRulingAndTheSideAndPermitCountsFollow() {
        // The user's 2026-10-12 ruling: Easy 1 / Normal 2 / Hard 3 at once.
        AssertThat(StoryEngagementRules.MeleeSlotsTotal(Difficulty.Easy)).IsEqual(1);
        AssertThat(StoryEngagementRules.MeleeSlotsTotal(Difficulty.Normal)).IsEqual(2);
        AssertThat(StoryEngagementRules.MeleeSlotsTotal(Difficulty.Hard)).IsEqual(3);
        // Provisional: Normal's two come one from each side.
        AssertThat(StoryEngagementRules.MeleeSlotsPerSide(Difficulty.Easy)).IsEqual(1);
        AssertThat(StoryEngagementRules.MeleeSlotsPerSide(Difficulty.Normal)).IsEqual(1);
        AssertThat(StoryEngagementRules.MeleeSlotsPerSide(Difficulty.Hard)).IsEqual(2);
        AssertThat(StoryEngagementRules.RangedPermits(Difficulty.Easy)).IsEqual(1);
        AssertThat(StoryEngagementRules.RangedPermits(Difficulty.Normal)).IsEqual(2);
        AssertThat(StoryEngagementRules.RangedPermits(Difficulty.Hard)).IsEqual(3);
        AssertThat(StoryEngagementRules.RestFramesAfterHandoff(Difficulty.Easy)).IsEqual(90);
        AssertThat(StoryEngagementRules.RestFramesAfterHandoff(Difficulty.Normal)).IsEqual(45);
        AssertThat(StoryEngagementRules.RestFramesAfterHandoff(Difficulty.Hard)).IsEqual(20);
        foreach (Difficulty difficulty in Difficulties) {
            // Two sides can always carry the total between them.
            AssertThat(2 * StoryEngagementRules.MeleeSlotsPerSide(difficulty)
                >= StoryEngagementRules.MeleeSlotsTotal(difficulty)).IsTrue();
        }
    }

    [TestCase]
    public void TheWaitRingSitsOutsideThePhaseSkipRadiusEveryCommitLineAndTheHerosReach() {
        AssertThat(StoryEngagementRules.WaitRingRankZeroGapPixels(Difficulty.Easy)).IsEqual(200f);
        AssertThat(StoryEngagementRules.WaitRingRankZeroGapPixels(Difficulty.Normal)).IsEqual(170f);
        AssertThat(StoryEngagementRules.WaitRingRankZeroGapPixels(Difficulty.Hard)).IsEqual(150f);
        AssertThat(StoryEngagementRules.WaitGapForRank(2, Difficulty.Normal)).IsEqual(170f + 2f * 64f);
        AssertThat(StoryEngagementRules.WaitGapForRank(-3, Difficulty.Normal)).IsEqual(170f);
        // The ranks never share an x: the spacing clears the widest mob pushbox
        // (the elite's 48 px) by 16 px, more than both waiters' tolerance.
        AssertThat(StoryEngagementRules.WaitRingRankSpacingPixels >= 48f + 16f).IsTrue();
        AssertThat(StoryEngagementRules.WaitRingRankSpacingPixels > 2f * StoryEngagementRules.WaitRingTolerancePixels).IsTrue();
        // Elite hurtbox half-width (EliteEnemy.tscn), the widest a hero reaches to.
        const float eliteHurtboxHalfWidth = 29f;
        foreach (Difficulty difficulty in Difficulties) {
            float rankZero = StoryEngagementRules.WaitGapForRank(0, difficulty);
            // Above EnemyController.ReactiveTeleportRangePixels (125 px)...
            AssertThat(rankZero > 125f).IsTrue();
            // ...above the farthest D6c melee commit line (77 px, the roster pin)...
            AssertThat(rankZero > 77f).IsTrue();
            // ...and above the widest hero's hit-1 pivot reach, so the hero steps in.
            AssertThat(rankZero > StoryEnemyCombatRules.WidestHit1PivotReachPixels(eliteHurtboxHalfWidth))
                .OverrideFailureMessage($"{difficulty}: rank 0 at {rankZero} px is inside the widest hit 1.")
                .IsTrue();
        }
    }

    [TestCase]
    public void EveryArchetypeNeedsTheRightThingFromTheDirector() {
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.MeleeStrike, false))
            .IsEqual(StoryEngagementRules.EngagementClass.MeleeSlot);
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.ChargeDash, false))
            .IsEqual(StoryEngagementRules.EngagementClass.MeleeSlot);
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.AreaPulse, false))
            .IsEqual(StoryEngagementRules.EngagementClass.MeleeSlot);
        // The standard engage blink is a melee commit; the reactive Phase Skip is free.
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.Teleport, isEngageBlink: true))
            .IsEqual(StoryEngagementRules.EngagementClass.MeleeSlot);
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.Teleport, isEngageBlink: false))
            .IsEqual(StoryEngagementRules.EngagementClass.Free);
        foreach (EnemyAbilityArchetype ranged in new[] {
                     EnemyAbilityArchetype.Projectile, EnemyAbilityArchetype.Shockwave,
                     EnemyAbilityArchetype.PersistentFieldAtTarget, EnemyAbilityArchetype.SiphonTether }) {
            AssertThat(StoryEngagementRules.ClassOf(ranged, false)).IsEqual(StoryEngagementRules.EngagementClass.RangedPermit);
        }
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.ShieldBubble, false))
            .IsEqual(StoryEngagementRules.EngagementClass.Free);
        AssertThat(StoryEngagementRules.ClassOf(EnemyAbilityArchetype.SummonMinions, false))
            .IsEqual(StoryEngagementRules.EngagementClass.Free);
        // Every authored archetype is classified (none falls through unnoticed).
        foreach (EnemyAbilityArchetype archetype in System.Enum.GetValues<EnemyAbilityArchetype>()) {
            bool known = archetype is EnemyAbilityArchetype.MeleeStrike or EnemyAbilityArchetype.ChargeDash
                or EnemyAbilityArchetype.AreaPulse or EnemyAbilityArchetype.Teleport
                or EnemyAbilityArchetype.Projectile or EnemyAbilityArchetype.Shockwave
                or EnemyAbilityArchetype.PersistentFieldAtTarget or EnemyAbilityArchetype.SiphonTether
                or EnemyAbilityArchetype.ShieldBubble or EnemyAbilityArchetype.SummonMinions;
            AssertThat(known).OverrideFailureMessage($"{archetype} has no engagement ruling.").IsTrue();
        }
    }

    [TestCase]
    public void PriorityRanksRestThenElitesThenSideBalanceThenRoundRobinThenGapThenId() {
        var baseline = new StoryEngagementRules.Candidate(10, isElite: false, rested: true, side: 1, waitedFrames: 30, gapPixels: 170f);
        // 1. A rested waiter beats a resting one, whatever its later keys...
        var resting = new StoryEngagementRules.Candidate(1, false, false, 1, 500, 100f);
        AssertThat(StoryEngagementRules.ComparePriority(in baseline, in resting, 0, 0) < 0).IsTrue();
        // ...an elite included (review fix 2026-10-13): a resting elite is the
        // holder that has just handed off, and it must not take the slot back
        // from a rested standard.
        var restingElite = new StoryEngagementRules.Candidate(99, true, false, 1, 900, 60f);
        AssertThat(StoryEngagementRules.ComparePriority(in baseline, in restingElite, 0, 0) < 0).IsTrue();
        AssertThat(StoryEngagementRules.ComparePriority(in restingElite, in baseline, 0, 0) > 0).IsTrue();
        // 2. Within one rest state an elite beats a standard that wins every later key.
        var elite = new StoryEngagementRules.Candidate(99, true, true, 1, 0, 400f);
        AssertThat(StoryEngagementRules.ComparePriority(in elite, in baseline, 0, 0) < 0).IsTrue();
        AssertThat(StoryEngagementRules.ComparePriority(in baseline, in elite, 0, 0) > 0).IsTrue();
        AssertThat(StoryEngagementRules.ComparePriority(in restingElite, in resting, 0, 0) < 0).IsTrue();
        // 3. The side with fewer holders goes first.
        var leftSide = new StoryEngagementRules.Candidate(50, false, true, -1, 0, 300f);
        AssertThat(StoryEngagementRules.ComparePriority(in leftSide, in baseline, holdersLeft: 0, holdersRight: 1) < 0).IsTrue();
        AssertThat(StoryEngagementRules.ComparePriority(in leftSide, in baseline, holdersLeft: 1, holdersRight: 0) > 0).IsTrue();
        // 4. Round-robin: more waited frames first.
        var waitedLonger = new StoryEngagementRules.Candidate(60, false, true, 1, 31, 300f);
        AssertThat(StoryEngagementRules.ComparePriority(in waitedLonger, in baseline, 0, 0) < 0).IsTrue();
        // 5. The nearer 32 px gap bucket, but not inside one bucket...
        var nearer = new StoryEngagementRules.Candidate(70, false, true, 1, 30, 120f);
        AssertThat(StoryEngagementRules.ComparePriority(in nearer, in baseline, 0, 0) < 0).IsTrue();
        var sameBucket = new StoryEngagementRules.Candidate(5, false, true, 1, 30, 165f);
        // ...where 6. the lower instance id decides (165 and 170 share bucket 5).
        AssertThat(StoryEngagementRules.ComparePriority(in sameBucket, in baseline, 0, 0) < 0).IsTrue();
        AssertThat(StoryEngagementRules.ComparePriority(in baseline, in baseline, 0, 0)).IsEqual(0);
    }

    [TestCase]
    public void GrantsRespectTheTotalAndPerSideCapsInPriorityOrder() {
        var granted = new List<ulong>();
        List<StoryEngagementRules.Candidate> ThreeRightOneLeft() => new() {
            new StoryEngagementRules.Candidate(1, false, true, 1, 10, 170f),
            new StoryEngagementRules.Candidate(2, false, true, 1, 20, 234f),
            new StoryEngagementRules.Candidate(3, false, true, 1, 30, 298f),
            new StoryEngagementRules.Candidate(4, false, true, -1, 5, 170f)
        };

        // Easy: one slot, to the best candidate (the longest wait).
        AssertThat(StoryEngagementRules.SelectGrants(ThreeRightOneLeft(), 0, 0, Difficulty.Easy, granted)).IsEqual(1);
        AssertThat(granted[0]).IsEqual(3UL);
        // Normal: one per side.
        AssertThat(StoryEngagementRules.SelectGrants(ThreeRightOneLeft(), 0, 0, Difficulty.Normal, granted)).IsEqual(2);
        AssertThat(granted.Contains(3UL) && granted.Contains(4UL)).IsTrue();
        // Normal with the right side already held: only the left slot is free.
        AssertThat(StoryEngagementRules.SelectGrants(ThreeRightOneLeft(), 0, 1, Difficulty.Normal, granted)).IsEqual(1);
        AssertThat(granted[0]).IsEqual(4UL);
        // Hard: two on the right and one on the left make three.
        AssertThat(StoryEngagementRules.SelectGrants(ThreeRightOneLeft(), 0, 0, Difficulty.Hard, granted)).IsEqual(3);
        AssertThat(granted.Contains(1UL)).IsFalse();
        // Full: nothing, and the output list is cleared.
        AssertThat(StoryEngagementRules.SelectGrants(ThreeRightOneLeft(), 1, 1, Difficulty.Normal, granted)).IsEqual(0);
        AssertThat(granted.Count).IsEqual(0);

        // A resting waiter takes a free slot only when no rested waiter wants it.
        var restingAndRested = new List<StoryEngagementRules.Candidate> {
            new(1, false, false, 1, 900, 60f),
            new(2, false, true, 1, 0, 300f)
        };
        StoryEngagementRules.SelectGrants(restingAndRested, 0, 0, Difficulty.Easy, granted);
        AssertThat(granted[0]).IsEqual(2UL);
        var restingAlone = new List<StoryEngagementRules.Candidate> { new(1, false, false, 1, 0, 60f) };
        AssertThat(StoryEngagementRules.SelectGrants(restingAlone, 0, 0, Difficulty.Easy, granted)).IsEqual(1);
        // An elite that has just handed off (resting) does not take Easy's one
        // slot back from a rested standard...
        var restingEliteAndRestedStandard = new List<StoryEngagementRules.Candidate> {
            new(1, true, false, 1, 0, 60f),
            new(2, false, true, 1, 40, 200f)
        };
        StoryEngagementRules.SelectGrants(restingEliteAndRestedStandard, 0, 0, Difficulty.Easy, granted);
        AssertThat(granted[0]).IsEqual(2UL);
        // ...but still takes a free slot on a side the rested standard cannot use.
        var restedStandardOnAFullSide = new List<StoryEngagementRules.Candidate> {
            new(1, true, false, -1, 0, 60f),
            new(2, false, true, 1, 40, 200f)
        };
        AssertThat(StoryEngagementRules.SelectGrants(restedStandardOnAFullSide, 0, 1, Difficulty.Normal, granted)).IsEqual(1);
        AssertThat(granted[0]).IsEqual(1UL);
    }

    [TestCase]
    public void TheHandOffTestFreesTheHoldersSideBeforeApplyingTheWaitersSideCap() {
        // Normal, holder on the right: a right waiter can take it...
        AssertThat(StoryEngagementRules.CanTakeFreedSlot(1, 1, 0, 1, Difficulty.Normal)).IsTrue();
        // ...a left waiter cannot while the left slot is held (one per side)...
        AssertThat(StoryEngagementRules.CanTakeFreedSlot(-1, 1, 1, 1, Difficulty.Normal)).IsFalse();
        // ...but can when the left side is empty.
        AssertThat(StoryEngagementRules.CanTakeFreedSlot(-1, 1, 0, 1, Difficulty.Normal)).IsTrue();
        // Easy: any side, one slot.
        AssertThat(StoryEngagementRules.CanTakeFreedSlot(-1, 1, 0, 1, Difficulty.Easy)).IsTrue();
        // Hard: two right holders, the waiter on the right takes the freed one.
        AssertThat(StoryEngagementRules.CanTakeFreedSlot(1, 1, 1, 2, Difficulty.Hard)).IsTrue();
        AssertThat(StoryEngagementRules.SideHasFreeSlot(1, 2, Difficulty.Normal)).IsFalse();
        AssertThat(StoryEngagementRules.SideHasFreeSlot(0, 1, Difficulty.Normal)).IsTrue();
        AssertThat(StoryEngagementRules.SideHasFreeSlot(0, 1, Difficulty.Easy)).IsFalse();
    }

    [TestCase]
    public void WaiterSpeedIsItsOwnFractionCappedAbsolutelyByTheHerosRunSpeed() {
        // A 300 px/s slasher against Lincoln after the 2026-10-12 cut (3.8 u/s = 228 px/s).
        AssertThat(StoryEngagementRules.WaiterSpeedPixels(300f, 228f, towardTarget: true))
            .IsEqualApprox(0.9f * 228f, 0.001f);
        AssertThat(StoryEngagementRules.WaiterSpeedPixels(300f, 228f, towardTarget: false))
            .IsEqualApprox(0.6f * 300f, 0.001f);
        // A slow mob keeps its own fractions.
        AssertThat(StoryEngagementRules.WaiterSpeedPixels(156f, 465f, true)).IsEqualApprox(0.8f * 156f, 0.001f);
        AssertThat(StoryEngagementRules.WaiterSpeedPixels(156f, 465f, false)).IsEqualApprox(0.6f * 156f, 0.001f);
        // The chariot raider (390 px/s) never outruns any hero.
        foreach (float hero in new[] { 228f, 312f, 372f }) {
            AssertThat(StoryEngagementRules.WaiterSpeedPixels(390f, hero, true) < hero).IsTrue();
            AssertThat(StoryEngagementRules.WaiterSpeedPixels(390f, hero, false) < hero).IsTrue();
        }
        // A rooted mob does not move.
        AssertThat(StoryEngagementRules.WaiterSpeedPixels(0f, 465f, true)).IsEqual(0f);
    }

    [TestCase]
    public void TheRangedHoldCapKeepsAShooterInsideTheZoomedHalfView() {
        // The half view is the camera rulebook's zoomed view, halved (consts
        // only: StoryCameraRules is never loaded in this engine-free host).
        float halfView = StoryCameraRules.ReferenceViewportWidth / StoryCameraRules.ViewZoom / 2f;
        AssertThat(StoryEngagementRules.ZoomedHalfViewPixels).IsEqual(halfView);
        AssertThat(StoryEngagementRules.RangedHoldMaxPixels)
            .IsEqual(halfView - StoryEngagementRules.RangedHoldViewMarginPixels);
        AssertThat(StoryEngagementRules.RangedHoldReleaseMaxPixels)
            .IsEqual(halfView - StoryEngagementRules.RangedHoldReleaseViewMarginPixels);
        // Both lines sit inside the half view, and the release keeps a dead band
        // above the hold line (hysteresis).
        AssertThat(StoryEngagementRules.RangedHoldViewMarginPixels > 0f).IsTrue();
        AssertThat(StoryEngagementRules.RangedHoldReleaseViewMarginPixels > 0f).IsTrue();
        AssertThat(StoryEngagementRules.RangedHoldReleaseMaxPixels > StoryEngagementRules.RangedHoldMaxPixels).IsTrue();
        AssertThat(StoryEngagementRules.RangedHoldReleaseMaxPixels < halfView).IsTrue();

        float sentryEngage = 0.85f * 810f;
        // The infrared sentry's projectile-widened line (0.85 x 810) is capped...
        AssertThat(StoryEngagementRules.CappedHoldEngagePixels(sentryEngage))
            .IsEqual(StoryEngagementRules.RangedHoldMaxPixels);
        // ...a closer line is untouched.
        AssertThat(StoryEngagementRules.CappedHoldEngagePixels(0.85f * 540f)).IsEqualApprox(459f, 0.001f);
        // A capped band's own hysteresis release (560 x 1.10 / 0.85 = 724.7)
        // would stand it past the half view; the release is capped too...
        float capped = StoryEngagementRules.CappedHoldEngagePixels(sentryEngage);
        AssertThat(capped * 1.10f / 0.85f > halfView).IsTrue();
        AssertThat(StoryEngagementRules.CappedHoldResumePixels(capped * 1.10f / 0.85f))
            .IsEqual(StoryEngagementRules.RangedHoldReleaseMaxPixels);
        // ...and a release already inside it passes through.
        AssertThat(StoryEngagementRules.CappedHoldResumePixels(500f)).IsEqual(500f);
    }

    [TestCase]
    public void TheStaleCrossingAndRangedFairnessRules() {
        AssertThat(StoryEngagementRules.IsStale(StoryEngagementRules.StaleSlotFrames - 1)).IsFalse();
        AssertThat(StoryEngagementRules.IsStale(StoryEngagementRules.StaleSlotFrames)).IsTrue();
        AssertThat(StoryEngagementRules.StaleSlotFrames).IsEqual(120);

        // Crossing: rank >= 1, cut off, and the other side less occupied.
        AssertThat(StoryEngagementRules.ShouldCrossToOtherSide(1, true, 3, 0)).IsTrue();
        AssertThat(StoryEngagementRules.ShouldCrossToOtherSide(0, true, 3, 0)).IsFalse();
        AssertThat(StoryEngagementRules.ShouldCrossToOtherSide(2, false, 3, 0)).IsFalse();
        AssertThat(StoryEngagementRules.ShouldCrossToOtherSide(1, true, 2, 2)).IsFalse();

        // Ranged fairness: the longest-refused shooter outranks, ties to the lower id.
        AssertThat(StoryEngagementRules.RangedWaiterOutranks(40, 7, 3, 2)).IsTrue();
        AssertThat(StoryEngagementRules.RangedWaiterOutranks(3, 7, 40, 2)).IsFalse();
        AssertThat(StoryEngagementRules.RangedWaiterOutranks(5, 2, 5, 7)).IsTrue();
        AssertThat(StoryEngagementRules.RangedWaiterOutranks(5, 7, 5, 2)).IsFalse();
        // A shooter that was never refused outranks nobody.
        AssertThat(StoryEngagementRules.RangedWaiterOutranks(0, 1, 0, 9)).IsFalse();
    }
}
