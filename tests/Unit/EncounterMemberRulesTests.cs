using FTT.Combat;
using FTT.Diagnostics;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FIX-ENEMY (Phase C fix round of the 2026-10-12 gameplay update): the pure
/// rules that keep an engaged lock member within the hero's reach — the flyer
/// hover cap and its settle, the flyer's band top, the ground member's way down
/// off a ledge, the stall watchdog's destination — and the crowd telemetry's
/// split of retaliation (the V7.4 pressure exit and the Armored Recovery answer,
/// never gated by the engagement director) from gated melee attacks. Plain
/// values only, so the suite runs with no Godot runtime (CLAUDE.md failure
/// signature 7); the engine half is <c>EncounterMemberBehaviourTests</c>.
/// </summary>
[TestSuite]
public class EncounterMemberRulesTests {

    private const float HeroFeet = 900f;
    private const float Step = 1f / 60f;

    [TestCase]
    public void AFlyerAboveTheHoverLineDescendsToItAndOneAtOrBelowItKeepsItsOwnVerticalSpeed() {
        float line = EncounterLockRules.FlyerHoverLineY(HeroFeet);
        AssertThat(line).IsEqual(HeroFeet - EncounterLockRules.FlyerHoverCapPixels);

        // 292 px over the hero's feet (the Level 14 drone): full chase speed down.
        AssertThat(EncounterLockRules.FlyerSettleVelocityY(HeroFeet - 292f, HeroFeet, 0f, 240f, Step)).IsEqual(240f);
        // A step short of the line it lands on it exactly, never past it.
        float near = line - 2f;
        float landing = EncounterLockRules.FlyerSettleVelocityY(near, HeroFeet, 0f, 240f, Step);
        AssertThat(near + landing * Step).IsEqualApprox(line, 0.001f);
        // A faster descent of its own (a chase toward the hero's feet) is never slowed.
        AssertThat(EncounterLockRules.FlyerSettleVelocityY(HeroFeet - 292f, HeroFeet, 300f, 240f, Step)).IsEqual(300f);
        // At or below the line the cap never lifts it and never stops it.
        AssertThat(EncounterLockRules.FlyerSettleVelocityY(line, HeroFeet, 0f, 240f, Step)).IsEqual(0f);
        AssertThat(EncounterLockRules.FlyerSettleVelocityY(HeroFeet - 30f, HeroFeet, -120f, 240f, Step)).IsEqual(-120f);
        // A rooted flyer (speed 0) is not moved; a zero tick changes nothing.
        AssertThat(EncounterLockRules.FlyerSettleVelocityY(HeroFeet - 292f, HeroFeet, 0f, 0f, Step)).IsEqual(0f);
        AssertThat(EncounterLockRules.FlyerSettleVelocityY(HeroFeet - 292f, HeroFeet, 0f, 240f, 0f)).IsEqual(0f);
    }

    [TestCase]
    public void AFlyersBodyNeverRisesAboveTheTopOfItsLocksCameraRect() {
        // A 72 px body under a camera rect whose top is y 0: its feet stay at 72 or below.
        AssertThat(EncounterLockRules.ClampFlyerFeetY(-3455f, 0f, 72f)).IsEqual(72f);
        AssertThat(EncounterLockRules.ClampFlyerFeetY(72f, 0f, 72f)).IsEqual(72f);
        AssertThat(EncounterLockRules.ClampFlyerFeetY(1408f, 0f, 72f)).IsEqual(1408f);
        // No lock to read (negative infinity) clamps nothing.
        AssertThat(EncounterLockRules.ClampFlyerFeetY(-3455f, float.NegativeInfinity, 72f)).IsEqual(-3455f);
    }

    [TestCase]
    public void AMeleeGroundMemberSeeksAWayDownOnlyToATargetBelowTheChasesLedgeProbe() {
        float depth = StoryEnemyCombatRules.LedgeProbeDepthPixels;
        // Level 7's quay platform (88 px) and Level 10's gallery tier (248 px).
        AssertThat(EncounterLockRules.SeeksWayDown(88f)).IsTrue();
        AssertThat(EncounterLockRules.SeeksWayDown(248f)).IsTrue();
        // A step the chase already walks down, the same floor, or a target above.
        AssertThat(EncounterLockRules.SeeksWayDown(depth)).IsFalse();
        AssertThat(EncounterLockRules.SeeksWayDown(0f)).IsFalse();
        AssertThat(EncounterLockRules.SeeksWayDown(-200f)).IsFalse();

        // The step's landing keeps the body inside the area.
        AssertThat(EncounterLockRules.StepOffLandsInArea(1500f, 1000f, 2400f, 23f)).IsTrue();
        AssertThat(EncounterLockRules.StepOffLandsInArea(1022f, 1000f, 2400f, 23f)).IsFalse();
        AssertThat(EncounterLockRules.StepOffLandsInArea(2378f, 1000f, 2400f, 23f)).IsFalse();
    }

    [TestCase]
    public void TheWatchdogNeverSendsAMemberBackToThePostItIsStalledOn() {
        float gap = EncounterLockRules.StallVerticalGapPixels;
        float tolerance = EncounterLockRules.StallAnchorTolerancePixels;
        // Stalled away from a reachable anchor: back to the anchor, as before.
        AssertThat(EncounterLockRules.RelocatesBesideHero(300f, 0f)).IsFalse();
        AssertThat(EncounterLockRules.RelocatesBesideHero(300f, -gap)).IsFalse();
        // Stalled on its own anchor: beside the hero (the Level 10 gallery loop).
        AssertThat(EncounterLockRules.RelocatesBesideHero(0f, 0f)).IsTrue();
        AssertThat(EncounterLockRules.RelocatesBesideHero(tolerance, 0f)).IsTrue();
        // Its anchor is itself a post over the hero: beside the hero.
        AssertThat(EncounterLockRules.RelocatesBesideHero(300f, -(gap + 1f))).IsTrue();
        // An anchor below a hero up on a ledge keeps the old return: the hero can come down.
        AssertThat(EncounterLockRules.RelocatesBesideHero(300f, gap + 1f)).IsFalse();
    }

    [TestCase]
    public void AFlyerHeldAboveTheHoverLineThatCannotMoveIsStalledEvenWhenItShoots() {
        float line = EncounterLockRules.FlyerHoverLineY(HeroFeet);
        // Level 3's drone resting on the lamp gantry 308 px over the floor.
        AssertThat(EncounterLockRules.IsFlyerStalledOutOfReach(true, false, 0f, HeroFeet - 308f, HeroFeet)).IsTrue();
        // Still descending, at the line, knocked about, or not a flyer: not stalled.
        AssertThat(EncounterLockRules.IsFlyerStalledOutOfReach(true, false, 4f, HeroFeet - 308f, HeroFeet)).IsFalse();
        AssertThat(EncounterLockRules.IsFlyerStalledOutOfReach(true, false, 0f, line, HeroFeet)).IsFalse();
        AssertThat(EncounterLockRules.IsFlyerStalledOutOfReach(true, true, 0f, HeroFeet - 308f, HeroFeet)).IsFalse();
        AssertThat(EncounterLockRules.IsFlyerStalledOutOfReach(false, false, 0f, HeroFeet - 308f, HeroFeet)).IsFalse();
        // The old sample alone exempts a ranged flyer holding still: shooting from a post.
        AssertThat(EncounterLockRules.IsStalledSample(true, false, false, true, true, 0f, -308f)).IsFalse();
    }

    [TestCase]
    public void TheCrowdTrackerCountsRetaliationApartFromGatedMeleeAttacks() {
        var tracker = new MobEngagementTracker { SlotTotal = 1, PermitTotal = 1 };
        // Easy: the slot holder swings while a struck waiter's pressure exit
        // answers (Level 13's chrono_guard_elite) — two melee attackers, one gated.
        tracker.SampleFrame(new EngagementFrameSample {
            Engaged = 2, AttackingNear = 2, MeleeAttackingNear = 2, MeleeRetaliatingNear = 1, Holders = 1
        });
        tracker.SampleFrame(new EngagementFrameSample {
            Engaged = 2, AttackingNear = 1, MeleeAttackingNear = 1, Holders = 1
        });
        AssertThat(tracker.FramesMeleeOverSlots).IsEqual(1);
        AssertThat(tracker.MaxMeleeRetaliatingNear).IsEqual(1);
        AssertThat(tracker.FramesGatedMeleeOverSlots)
            .OverrideFailureMessage("A retaliating waiter is never gated, so it never puts the gated count over the slots.")
            .IsEqual(0);

        // Two gated melee attackers on Easy's one slot would be the director's fault.
        tracker.SampleFrame(new EngagementFrameSample {
            Engaged = 2, AttackingNear = 2, MeleeAttackingNear = 2, Holders = 2
        });
        AssertThat(tracker.FramesGatedMeleeOverSlots).IsEqual(1);
        AssertThat(tracker.FramesMeleeOverSlots).IsEqual(2);
    }
}
