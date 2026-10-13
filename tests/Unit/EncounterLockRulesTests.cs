using FTT.Core;
using FTT.Environment;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay update 2026-10-12 (GU-E, user request 3): the pure encounter-lock
/// rulebook — engage eligibility, containment and the revert to Armed, the
/// clear predicate, wave advance, the per-row difficulty prefix, the camera
/// rect's expansion to the visible view, the member clamp and fall net, the stall
/// watchdog, the hard cap and the HUD count. Plain values only, so the suite runs
/// with no Godot runtime (CLAUDE.md failure signature 7); the engine half is
/// <c>EncounterLockTests</c>.
/// </summary>
[TestSuite]
public class EncounterLockRulesTests {

    // An area 1000..2400 wide, from the sky ceiling to the level bottom.
    private const float MinX = 1000f;
    private const float MaxX = 2400f;
    private const float MinY = -2400f;
    private const float MaxY = 1080f;
    private const float HeroHalf = 20f;

    [TestCase]
    public void TheHeroEngagesOnlyItsHalfWidthPlusTheInsetInsideAndOnlyWhileNothingHoldsTheWorld() {
        float inset = HeroHalf + EncounterLockRules.EngageInsetPixels;
        AssertThat(EncounterLockRules.EngageInsetPixels).IsEqual(24f);
        AssertThat(Engage(MinX + inset, 850f)).IsTrue();
        AssertThat(Engage(MinX + inset - 1f, 850f)).IsFalse();
        AssertThat(Engage(MaxX - inset, 850f)).IsTrue();
        AssertThat(Engage(MaxX - inset + 1f, 850f)).IsFalse();
        // Vertically the hero must be inside the band too.
        AssertThat(Engage(1700f, MaxY + 1f)).IsFalse();
        AssertThat(Engage(1700f, MinY - 1f)).IsFalse();

        // Anything that holds the world, or a dead hero, holds the engage.
        AssertThat(EncounterLockRules.CanEngage(1700f, HeroHalf, 850f, MinX, MaxX, MinY, MaxY,
            heroAlive: false, timeFrozen: false, worldHeld: false, gameplayPaused: false)).IsFalse();
        AssertThat(EncounterLockRules.CanEngage(1700f, HeroHalf, 850f, MinX, MaxX, MinY, MaxY,
            heroAlive: true, timeFrozen: true, worldHeld: false, gameplayPaused: false)).IsFalse();
        AssertThat(EncounterLockRules.CanEngage(1700f, HeroHalf, 850f, MinX, MaxX, MinY, MaxY,
            heroAlive: true, timeFrozen: false, worldHeld: true, gameplayPaused: false)).IsFalse();
        AssertThat(EncounterLockRules.CanEngage(1700f, HeroHalf, 850f, MinX, MaxX, MinY, MaxY,
            heroAlive: true, timeFrozen: false, worldHeld: false, gameplayPaused: true)).IsFalse();

        // An area narrower than both insets can never be engaged.
        AssertThat(EncounterLockRules.CanEngage(1000f, HeroHalf, 850f, 980f, 1020f, MinY, MaxY,
            heroAlive: true, timeFrozen: false, worldHeld: false, gameplayPaused: false)).IsFalse();
    }

    [TestCase]
    public void AnEngagedLockRevertsOnlyWhenItsLivingHeroEndsATickOutsideTheArea() {
        AssertThat(EncounterLockRules.IsInsideArea(MinX, 850f, MinX, MaxX, MinY, MaxY)).IsTrue();
        AssertThat(EncounterLockRules.IsInsideArea(MaxX, 850f, MinX, MaxX, MinY, MaxY)).IsTrue();
        AssertThat(EncounterLockRules.IsInsideArea(MinX - 0.5f, 850f, MinX, MaxX, MinY, MaxY)).IsFalse();
        AssertThat(EncounterLockRules.IsInsideArea(1500f, MaxY + 0.5f, MinX, MaxX, MinY, MaxY)).IsFalse();

        AssertThat(EncounterLockRules.ShouldRevertToArmed(EncounterLockState.Engaged, heroAlive: true, heroInsideArea: false))
            .IsTrue();
        AssertThat(EncounterLockRules.ShouldRevertToArmed(EncounterLockState.Engaged, heroAlive: true, heroInsideArea: true))
            .IsFalse();
        // The rewind playback moves a dead hero along its path: not judged until it lives.
        AssertThat(EncounterLockRules.ShouldRevertToArmed(EncounterLockState.Engaged, heroAlive: false, heroInsideArea: false))
            .IsFalse();
        // Armed and Cleared locks never revert.
        AssertThat(EncounterLockRules.ShouldRevertToArmed(EncounterLockState.Armed, heroAlive: true, heroInsideArea: false))
            .IsFalse();
        AssertThat(EncounterLockRules.ShouldRevertToArmed(EncounterLockState.Cleared, heroAlive: true, heroInsideArea: false))
            .IsFalse();
    }

    [TestCase]
    public void ALockClearsOnlyWithEveryMemberDownEveryWaveSpawnedAndEveryPuzzleAndBreakableDone() {
        AssertThat(EncounterLockRules.IsClearPredicateMet(0, allWavesSpawned: true, puzzlesSolved: true, breakablesDestroyed: true))
            .IsTrue();
        AssertThat(EncounterLockRules.IsClearPredicateMet(1, true, true, true)).IsFalse();
        AssertThat(EncounterLockRules.IsClearPredicateMet(0, false, true, true)).IsFalse();
        AssertThat(EncounterLockRules.IsClearPredicateMet(0, true, false, true)).IsFalse();
        AssertThat(EncounterLockRules.IsClearPredicateMet(0, true, true, false)).IsFalse();
    }

    [TestCase]
    public void WavesAdvanceInOrderOnTheirTriggersOnlyWhileEngagedAndUnheld() {
        EncounterLockState engaged = EncounterLockState.Engaged;
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.OnEngage, 0, engaged, true, 3, held: false)).IsTrue();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.WhenAliveAtMost, 1, engaged, true, 2, false)).IsFalse();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.WhenAliveAtMost, 1, engaged, true, 1, false)).IsTrue();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.AfterPreviousCleared, 0, engaged, true, 1, false)).IsFalse();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.AfterPreviousCleared, 0, engaged, true, 0, false)).IsTrue();
        // A wave never jumps the queue, never spawns while held, never while Armed or Cleared.
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.OnEngage, 0, engaged, earlierWavesSpawned: false, 0, false))
            .IsFalse();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.OnEngage, 0, engaged, true, 0, held: true)).IsFalse();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.OnEngage, 0, EncounterLockState.Armed, true, 0, false))
            .IsFalse();
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.AfterPreviousCleared, 0, EncounterLockState.Cleared, true, 0, false))
            .IsFalse();
        // A negative threshold reads as zero, never as "never".
        AssertThat(EncounterLockRules.ShouldSpawnWave(EncounterWaveTrigger.WhenAliveAtMost, -3, engaged, true, 0, false)).IsTrue();
    }

    [TestCase]
    public void AWaveSpawnsTheManifestRowsPerDifficultyPrefixNeverMoreThanItsTable() {
        // A three-row table: Easy floors 2.1 to 2, Normal 3, Hard rounds 3.75 to 4 -> the table's 3.
        AssertThat(EncounterLockRules.PlannedSpawnCount(3, -1, scaled: true, Difficulty.Easy)).IsEqual(2);
        AssertThat(EncounterLockRules.PlannedSpawnCount(3, -1, scaled: true, Difficulty.Normal)).IsEqual(3);
        AssertThat(EncounterLockRules.PlannedSpawnCount(3, -1, scaled: true, Difficulty.Hard)).IsEqual(3);
        // A table with a Hard-only extra authors @3: Hard takes the fourth row.
        AssertThat(EncounterLockRules.PlannedSpawnCount(4, 3, scaled: true, Difficulty.Normal)).IsEqual(3);
        AssertThat(EncounterLockRules.PlannedSpawnCount(4, 3, scaled: true, Difficulty.Hard)).IsEqual(4);
        // @0: the whole table on every difficulty.
        AssertThat(EncounterLockRules.PlannedSpawnCount(2, 0, scaled: false, Difficulty.Easy)).IsEqual(2);
        AssertThat(EncounterLockRules.PlannedSpawnCount(0, -1, scaled: true, Difficulty.Normal)).IsEqual(0);
        // It is the same prefix the reward manifest compiles.
        AssertThat(EncounterLockRules.PlannedSpawnCount(5, -1, true, Difficulty.Easy))
            .IsEqual(StoryDifficultyTuning.ScaleEncounterCount(5, Difficulty.Easy));
    }

    [TestCase]
    public void ALockCameraRectGrowsToTheViewAndStaysInsideTheLevel() {
        AssertThat(EncounterLockRules.ViewWorldWidth).IsEqual(1280f);
        AssertThat(EncounterLockRules.ViewWorldHeight).IsEqual(720f);
        // Wide enough already: unchanged.
        AssertThat(EncounterLockRules.ExpandCameraSpan(1000f, 2400f, 1280f, 0f, 10000f)).IsEqual((1000f, 2400f));
        // Narrow: grown symmetrically about its centre.
        AssertThat(EncounterLockRules.ExpandCameraSpan(1500f, 2000f, 1280f, 0f, 10000f)).IsEqual((1110f, 2390f));
        // Grown past the level's west edge: shifted back inside.
        AssertThat(EncounterLockRules.ExpandCameraSpan(100f, 500f, 1280f, 0f, 10000f)).IsEqual((0f, 1280f));
        // Past the east edge.
        AssertThat(EncounterLockRules.ExpandCameraSpan(9600f, 9900f, 1280f, 0f, 10000f)).IsEqual((8720f, 10000f));
        // A level narrower than the view: the level itself.
        AssertThat(EncounterLockRules.ExpandCameraSpan(100f, 300f, 1280f, 0f, 1000f)).IsEqual((0f, 1000f));
        // The vertical axis: a sky-to-floor area is clipped to the level's height.
        AssertThat(EncounterLockRules.ExpandCameraSpan(-2400f, 1080f, 720f, 0f, 1080f)).IsEqual((0f, 1080f));
    }

    [TestCase]
    public void AMemberIsClampedItsHalfWidthInsideTheAreaAndCaughtByTheBossFallNetMargin() {
        AssertThat(EncounterLockRules.ClampMemberX(500f, MinX, MaxX, 23f)).IsEqual(MinX + 23f);
        AssertThat(EncounterLockRules.ClampMemberX(9000f, MinX, MaxX, 23f)).IsEqual(MaxX - 23f);
        AssertThat(EncounterLockRules.ClampMemberX(1700f, MinX, MaxX, 23f)).IsEqual(1700f);
        // A body wider than the area sits on its centre.
        AssertThat(EncounterLockRules.ClampMemberX(0f, 100f, 120f, 23f)).IsEqual(110f);

        // The fall net is the boss arena's four-unit margin, stated once there.
        AssertThat(EncounterLockRules.FallNetMarginPixels).IsEqual(240f);
        AssertThat(EncounterLockRules.FallNetLineY(MaxY)).IsEqual(MaxY + 240f);
        AssertThat(EncounterLockRules.IsBelowFallNet(MaxY + 240f, MaxY)).IsFalse();
        AssertThat(EncounterLockRules.IsBelowFallNet(MaxY + 240.5f, MaxY)).IsTrue();
    }

    [TestCase]
    public void TheStallWatchdogRelocatesOnlyAMemberStuckOffTheFloorOrOutOfTheHerosReach() {
        // Off any floor, not in a knockback flight: stalled. Flyers never are.
        AssertThat(EncounterLockRules.IsStalledSample(flying: false, onFloor: false, inFlight: false,
            chasing: false, ranged: false, movedPixels: 0f, verticalGapToHero: 0f)).IsTrue();
        AssertThat(EncounterLockRules.IsStalledSample(false, false, inFlight: true, false, false, 0f, 0f)).IsFalse();
        AssertThat(EncounterLockRules.IsStalledSample(flying: true, false, false, false, false, 0f, 0f)).IsFalse();
        // A melee chaser standing still on a post the hero cannot reach: stalled.
        AssertThat(EncounterLockRules.IsStalledSample(false, true, false, chasing: true, ranged: false,
            movedPixels: 0f, verticalGapToHero: -300f)).IsTrue();
        // A shooter holding still on its post is doing its job.
        AssertThat(EncounterLockRules.IsStalledSample(false, true, false, true, ranged: true, 0f, -300f)).IsFalse();
        // A waiter on the hero's own floor, a mover and an attacker are not.
        AssertThat(EncounterLockRules.IsStalledSample(false, true, false, true, false, 0f, 0f)).IsFalse();
        AssertThat(EncounterLockRules.IsStalledSample(false, true, false, true, false, 6f, -300f)).IsFalse();
        AssertThat(EncounterLockRules.IsStalledSample(false, true, false, chasing: false, false, 0f, -300f)).IsFalse();

        int frames = 0;
        for (int tick = 0; tick < EncounterLockRules.StallFrames - 1; tick++) frames = EncounterLockRules.NextStallFrames(frames, true);
        AssertThat(EncounterLockRules.ShouldRelocate(frames)).IsFalse();
        frames = EncounterLockRules.NextStallFrames(frames, true);
        AssertThat(EncounterLockRules.ShouldRelocate(frames)).IsTrue();
        AssertThat(EncounterLockRules.StallFrames).IsEqual(600);
        // One unstalled sample starts the count again.
        AssertThat(EncounterLockRules.NextStallFrames(frames, false)).IsEqual(0);
    }

    [TestCase]
    public void TheHardCapForceClearsOnlyAnEngagedLockWithNothingLeftThatTheHeroCouldStillFinish() {
        AssertThat(EncounterLockRules.HardCapSeconds).IsEqual(300f);
        AssertThat(EncounterLockRules.ShouldForceClear(EncounterLockState.Engaged, 301.0, 0, true, true)).IsTrue();
        AssertThat(EncounterLockRules.ShouldForceClear(EncounterLockState.Engaged, 299.0, 0, true, true)).IsFalse();
        AssertThat(EncounterLockRules.ShouldForceClear(EncounterLockState.Engaged, 301.0, 1, true, true)).IsFalse();
        AssertThat(EncounterLockRules.ShouldForceClear(EncounterLockState.Engaged, 301.0, 0, puzzlesSolved: false, true)).IsFalse();
        AssertThat(EncounterLockRules.ShouldForceClear(EncounterLockState.Engaged, 301.0, 0, true, breakablesDestroyed: false)).IsFalse();
        AssertThat(EncounterLockRules.ShouldForceClear(EncounterLockState.Armed, 900.0, 0, true, true)).IsFalse();
    }

    [TestCase]
    public void TheHudCountsTheLivingAndTheUnspawnedAndTheGoPromptReadsByItsArrow() {
        AssertThat(EncounterLockRules.RemainingCount(EncounterLockState.Engaged, livingMembers: 2, unspawnedPlanned: 3)).IsEqual(5);
        AssertThat(EncounterLockRules.RemainingCount(EncounterLockState.Armed, 2, 0)).IsEqual(2);
        AssertThat(EncounterLockRules.RemainingCount(EncounterLockState.Cleared, 2, 3)).IsEqual(0);
        AssertThat(EncounterLockRules.GoGlyph(EncounterGoDirection.Right)).IsEqual("→");
        AssertThat(EncounterLockRules.GoGlyph(EncounterGoDirection.Left)).IsEqual("←");
        AssertThat(EncounterLockRules.GoGlyph(EncounterGoDirection.Up)).IsEqual("↑");
        AssertThat(EncounterLockRules.GoGlyph(EncounterGoDirection.Down)).IsEqual("↓");
        AssertThat(EncounterLockRules.GoGlyph(EncounterGoDirection.None)).IsEqual("");
        AssertThat(EncounterLockRules.GoPromptSeconds).IsEqual(3f);
    }

    [TestCase]
    public void TheProvisionalParScaleMultipliesParAndRouteOnceSoF11BudgetsKeepTheirPoints() {
        AssertThat(TimelineIntegrityRules.ProvisionalParScale).IsEqual(1.5f);
        AssertThat(TimelineIntegrityRules.ProvisionallyScaledSeconds(360f)).IsEqual(540f);
        AssertThat(TimelineIntegrityRules.ProvisionallyScaledSeconds(0f)).IsEqual(0f);
        // Scaling par and the remaining route together leaves the recovery minimum unchanged.
        int authored = TimelineIntegrityRules.RequiredRecovery(360f, Difficulty.Normal, 360f * 0.45f);
        int scaled = TimelineIntegrityRules.RequiredRecovery(
            TimelineIntegrityRules.ProvisionallyScaledSeconds(360f), Difficulty.Normal,
            TimelineIntegrityRules.ProvisionallyScaledSeconds(360f * 0.45f));
        AssertThat(scaled).IsEqual(authored);
        // A content area is between one view and one authored room wide.
        AssertThat(EncounterLockRules.MinAreaWidthPixels).IsEqual(1280f);
        AssertThat(EncounterLockRules.MaxAreaWidthPixels).IsEqual(3200f);
    }

    private static bool Engage(float heroX, float feetY) =>
        EncounterLockRules.CanEngage(heroX, HeroHalf, feetY, MinX, MaxX, MinY, MaxY,
            heroAlive: true, timeFrozen: false, worldHeld: false, gameplayPaused: false);
}
