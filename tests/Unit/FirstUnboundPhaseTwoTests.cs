using System;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A7b — T01b Option A, the First Unbound's Phase 2 capped historical
/// recovery (<c>docs/design-contracts/TEMPORAL_STATE_CONTRACT.md</c>).
///
/// <para>Once per encounter, on the first <b>nonlethal</b> crossing of the authored
/// 66% threshold: rewind the boss's own position 180 simulation frames, recover
/// toward the HP it had then capped at 20% of its difficulty-scaled maximum,
/// validate historical → current → authored anchor, suspend combat for 1.5 s with
/// every remaining timer preserved, and resume with a full attack telegraph.
/// Lethal damage wins; a nonlethal crossing of 33% queues Phase 3 even when the
/// healing lifts HP back above it.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FirstUnboundPhaseTwoTests {
    private const float Step = 1f / 60f;
    private const int BaseHP = 1000;

    // === 1. Once per encounter ===

    [TestCase]
    public void TheRecoveryFiresOnceAndASecondThresholdCrossingTakesTheOrdinaryTransition() {
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                DriveTicks(boss, 200);
                AssertThat(boss.HistoricalRecoveryUsed).IsFalse();

                // 1000 -> 600 (60%): the first crossing of 66%.
                boss.TakeDamage(400);
                AssertThat(boss.HistoricalRecoveryUsed).IsTrue();
                AssertThat(boss.IsHistoricalRecoverySuspended).IsTrue();
                AssertThat(boss.CurrentPhase).IsEqual(1);

                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 1);
                AssertThat(boss.IsHistoricalRecoverySuspended).IsFalse();

                // Down through 33%: a real second transition, but the ordinary
                // invincibility window, never a second rewind.
                boss.TakeDamage(boss.CurrentHP - 200);
                AssertThat(boss.CurrentPhase).IsEqual(2);
                AssertThat(boss.CurrentState).IsEqual(BossState.PhaseTransitioning);
                AssertThat(boss.IsHistoricalRecoverySuspended).IsFalse();
                AssertThat(boss.HistoricalRecoveryUsed).IsTrue();
            } finally {
                boss.Free();
            }
        });
    }

    // === 2. Only a nonlethal crossing, and only for a boss that authors it ===

    [TestCase]
    public void DamageThatNeverReachesTheThresholdDoesNotArmTheRecovery() {
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                DriveTicks(boss, 200);
                // 1000 -> 700 (70%), still above the authored 66% line.
                boss.TakeDamage(300);
                AssertThat(boss.CurrentHP).IsEqual(700);
                AssertThat(boss.CurrentPhase).IsEqual(0);
                AssertThat(boss.HistoricalRecoveryUsed).IsFalse();
                AssertThat(boss.IsHistoricalRecoverySuspended).IsFalse();
            } finally {
                boss.Free();
            }

            // A boss that does not author the behaviour never gets it, however far
            // its HP falls: every other boss in the roster loads the false default.
            BossController plain = CreateBoss(historicalRecovery: false);
            try {
                DriveTicks(plain, 200);
                plain.TakeDamage(400);
                AssertThat(plain.HasHistoricalRecovery).IsFalse();
                AssertThat(plain.CurrentPhase).IsEqual(1);
                AssertThat(plain.HistoricalRecoveryUsed).IsFalse();
                AssertThat(plain.CurrentState).IsEqual(BossState.PhaseTransitioning);
                AssertThat(plain.HistorySampleCount).IsEqual(0);
            } finally {
                plain.Free();
            }
        });
    }

    // === 3. Lethal damage wins ===

    [TestCase]
    public void ALethalHitDefeatsTheBossAndTheRewindCannotResurrectIt() {
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                DriveTicks(boss, 200);
                boss.TakeDamage(BaseHP + 500);

                AssertThat(boss.CurrentState).IsEqual(BossState.Dead);
                AssertThat(boss.CurrentHP).IsEqual(0);
                AssertThat(boss.HistoricalRecoveryUsed)
                    .OverrideFailureMessage("A lethal result must cancel pending phase work outright.")
                    .IsFalse();
                AssertThat(boss.IsHistoricalRecoverySuspended).IsFalse();

                // And it stays dead right through the window the beat would occupy.
                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 10);
                AssertThat(boss.CurrentState).IsEqual(BossState.Dead);
                AssertThat(boss.CurrentHP).IsEqual(0);
            } finally {
                boss.Free();
            }
        });
    }

    // === 4. The exact 180-frame ring, plus the oldest-real-sample fallback ===

    [TestCase]
    public void ThePositionRewindLocksTheSampleOneHundredEightyTicksBackOrTheOldestReal() {
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                // One sample per authoritative tick, each at an identifiable X.
                for (int tick = 1; tick <= 200; tick++) {
                    boss.GlobalPosition = new Vector2(tick * 10f, 0f);
                    boss._PhysicsProcess(Step);
                }
                AssertThat(boss.HistorySampleCount)
                    .OverrideFailureMessage("The ring retains 181 consecutive samples.")
                    .IsEqual(BossData.HistoricalRecoveryHistorySamples);

                boss.TakeDamage(400);
                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 1);

                // Trigger tick 200, lookback 180 => tick 20 => x = 200. Exact, not
                // "roughly three seconds ago".
                AssertThat(boss.HistoricalRecoveryDestination.X).IsEqualApprox(200f, 0.001f);
                AssertThat(boss.GlobalPosition.X).IsEqualApprox(200f, 0.001f);
            } finally {
                boss.Free();
            }

            // A history younger than the lookback uses the oldest REAL sample —
            // the one seeded when combat began — never fabricated pre-fight history.
            BossController young = CreateBoss();
            try {
                young.GlobalPosition = new Vector2(-500f, 0f);
                young.ApplyData(young.Data); // reseed the encounter at a known origin
                for (int tick = 1; tick <= 30; tick++) {
                    young.GlobalPosition = new Vector2(tick * 10f, 0f);
                    young._PhysicsProcess(Step);
                }
                AssertThat(young.HistorySampleCount).IsEqual(31);

                young.TakeDamage(400);
                DriveTicks(young, BossData.HistoricalRecoverySuspendFrames + 1);
                AssertThat(young.HistoricalRecoveryDestination.X).IsEqualApprox(-500f, 0.001f);
            } finally {
                young.Free();
            }
        });
    }

    // === 5. The 20%-of-difficulty-scaled-max heal cap ===

    [TestCase]
    public void TheHealIsCappedAtTwentyPercentOfTheDifficultyScaledMaximum() {
        // The contract's worked example: a 60% trigger with 95% historical HP
        // resolves to 80%, because the 350-point gap exceeds the 200-point cap.
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                AssertThat(boss.ScaledMaxHP).IsEqual(1000);
                DriveTicks(boss, 10);
                boss.TakeDamage(50);    // 1000 -> 950 (95%)
                DriveTicks(boss, 185);  // 185 ticks recorded at 950
                boss.TakeDamage(350);   // 950 -> 600 (60%), crossing 66%
                AssertThat(boss.CurrentHP).IsEqual(600);

                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 1);
                // heal = max(0, min(950 - 600, floor(0.20 * 1000), 1000 - 600)) = 200.
                AssertThat(boss.CurrentHP).IsEqual(800);
                AssertThat(boss.CurrentHP <= boss.ScaledMaxHP).IsTrue();
            } finally {
                boss.Free();
            }
        });

        // The cap follows the SCALED maximum, not the authored base: on Easy the
        // pool is 700, so the ceiling is 140 rather than 200.
        RunWithDifficulty(Difficulty.Easy, () => {
            BossController boss = CreateBoss();
            try {
                AssertThat(boss.ScaledMaxHP).IsEqual(700);
                DriveTicks(boss, 200);
                boss.TakeDamage(280);   // 700 -> 420 (60%)
                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 1);
                // heal = min(700 - 420, floor(0.20 * 700), 700 - 420) = 140.
                AssertThat(boss.CurrentHP).IsEqual(560);
                AssertThat(boss.CurrentHP <= boss.ScaledMaxHP).IsTrue();
            } finally {
                boss.Free();
            }
        });
    }

    // === 6. Historical -> current -> authored anchor -> stay put ===

    [TestCase]
    public void TheDestinationChainFallsBackHistoricalThenCurrentThenAuthoredAnchor() {
        RunWithDifficulty(Difficulty.Normal, () => {
            var historical = new Vector2(120f, 0f);
            var current = new Vector2(900f, 0f);
            var anchor = new Vector2(-300f, 0f);

            // Tier 1: the historical coordinate validates.
            Vector2 tier1 = ResolveDestination(historical, current, anchor, _ => true, out bool fallback1);
            AssertThat(tier1.X).IsEqualApprox(historical.X, 0.001f);
            AssertThat(fallback1).IsFalse();

            // Tier 2: the historical coordinate is blocked; the current one is not.
            Vector2 tier2 = ResolveDestination(historical, current, anchor,
                point => point != historical, out bool fallback2);
            AssertThat(tier2.X).IsEqualApprox(current.X, 0.001f);
            AssertThat(fallback2).IsTrue();

            // Tier 3: only the stable authored safe anchor validates.
            Vector2 tier3 = ResolveDestination(historical, current, anchor,
                point => point == anchor, out bool fallback3);
            AssertThat(tier3.X).IsEqualApprox(anchor.X, 0.001f);
            AssertThat(fallback3).IsTrue();

            // Nothing valid: the boss stays put rather than spawning into danger,
            // and the fallback "changes only destination, never the sampled heal or
            // the once-only event".
            BossController boss = CreateBoss();
            try {
                for (int tick = 1; tick <= 200; tick++) {
                    boss.GlobalPosition = new Vector2(tick * 10f, 0f);
                    boss._PhysicsProcess(Step);
                }
                Vector2 standing = boss.GlobalPosition;
                boss.HistoricalRecoveryDestinationValidator = _ => false;

                boss.TakeDamage(400);
                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 1);

                AssertThat(boss.HistoricalRecoveryDestinationInvalid).IsTrue();
                AssertThat(boss.GlobalPosition.X)
                    .OverrideFailureMessage("An invalid arena must never spawn the boss into danger.")
                    .IsEqualApprox(standing.X, 0.001f);
                // "Fallback changes only destination, never the sampled heal or the
                // once-only event."
                AssertThat(boss.CurrentHP).IsEqual(800);
                AssertThat(boss.HistoricalRecoveryUsed).IsTrue();
            } finally {
                boss.Free();
            }
        });
    }

    // === 7. The 1.5-second suspension preserves every remaining timer ===

    [TestCase]
    public void TheSuspensionRunsNinetyFramesPreservingTimersAndRefusingDamage() {
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                DriveTicks(boss, 200);
                boss.ApplyStatusEffect(StatusType.TimeDilation, 5f, 1f);
                boss.ApplyConductiveMark(0, 240);
                float statusBefore = boss.ActiveStatuses.Control.RemainingSeconds;
                int markBefore = boss.ConductiveFramesRemaining;

                boss.TakeDamage(400);
                AssertThat(boss.IsHistoricalRecoverySuspended).IsTrue();
                AssertThat(boss.HistoricalRecoverySuspendFramesRemaining)
                    .IsEqual(BossData.HistoricalRecoverySuspendFrames);

                // Halfway through: still suspended, still refusing damage, and no
                // clock on this controller has advanced.
                DriveTicks(boss, 45);
                AssertThat(boss.IsHistoricalRecoverySuspended).IsTrue();
                boss.TakeDamage(100);
                AssertThat(boss.CurrentHP)
                    .OverrideFailureMessage("Nothing may deal damage during the suspended beat.")
                    .IsEqual(600);
                AssertThat(boss.ActiveStatuses.Control.RemainingSeconds)
                    .IsEqualApprox(statusBefore, 0.0001f);
                AssertThat(boss.ConductiveFramesRemaining).IsEqual(markBefore);

                // Completion applies the resolved HP once, keeps the surviving
                // status, and hands the boss back with no attack in flight — so its
                // next attack runs a full normal telegraph.
                DriveTicks(boss, 46);
                AssertThat(boss.IsHistoricalRecoverySuspended).IsFalse();
                AssertThat(boss.CurrentHP).IsEqual(800);
                AssertThat(boss.ControlStatusType).IsEqual(StatusType.TimeDilation);
                AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
            } finally {
                boss.Free();
            }
        });
    }

    // === 8. A single hit through both thresholds ===

    [TestCase]
    public void ASingleNonlethalHitThroughBothThresholdsLatchesPhaseThreeDespiteTheHealing() {
        RunWithDifficulty(Difficulty.Normal, () => {
            BossController boss = CreateBoss();
            try {
                DriveTicks(boss, 200);
                // 1000 -> 300 (30%): one resolution crossing 66% and 33% together.
                boss.TakeDamage(700);
                AssertThat(boss.CurrentPhase)
                    .OverrideFailureMessage("Both crossings must latch from one damage resolution.")
                    .IsEqual(2);
                AssertThat(boss.IsHistoricalRecoverySuspended).IsTrue();

                DriveTicks(boss, BossData.HistoricalRecoverySuspendFrames + 1);
                // Healed back to 500 = 50%, comfortably above the 33% line...
                AssertThat(boss.CurrentHP).IsEqual(500);
                // ...and the phase never regresses, so Phase 3 is not re-earned.
                AssertThat(boss.CurrentPhase).IsEqual(2);
            } finally {
                boss.Free();
            }
        });
    }

    // === Helpers ===

    /// <summary>
    /// Drives the destination chain against an injected validator and returns the
    /// tier it settled on. The ring is reseeded at <paramref name="historical"/> and
    /// the boss then stood at <paramref name="current"/>, so the three candidates
    /// are distinguishable by position alone.
    /// </summary>
    private static Vector2 ResolveDestination(
        Vector2 historical, Vector2 current, Vector2 anchorPosition,
        Func<Vector2, bool> validator, out bool usedFallback) {
        BossController boss = CreateBoss();
        var anchor = new Node2D { Name = "HistoricalRecoveryAnchorUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(anchor);
        try {
            anchor.GlobalPosition = anchorPosition;
            boss.HistoricalRecoveryAnchor = anchor;
            boss.HistoricalRecoveryDestinationValidator = validator;

            boss.GlobalPosition = historical;
            boss.ApplyData(boss.Data);
            boss.GlobalPosition = current;

            boss.TakeDamage(400);
            usedFallback = boss.HistoricalRecoveryUsedFallbackDestination;
            return boss.HistoricalRecoveryDestination;
        } finally {
            anchor.GetParent()?.RemoveChild(anchor);
            anchor.Free();
            boss.Free();
        }
    }

    private static void DriveTicks(BossController boss, int ticks) {
        for (int tick = 0; tick < ticks; tick++) boss._PhysicsProcess(Step);
    }

    /// <summary>
    /// The pool and the cap are both difficulty-scaled, so every case pins its tier
    /// and hands the session back exactly as it found it.
    /// </summary>
    private static void RunWithDifficulty(Difficulty difficulty, Action body) {
        GameManager manager = GameManager.Instance;
        Difficulty original = manager != null ? manager.CurrentSession.Difficulty : Difficulty.Normal;
        if (manager != null) manager.CurrentSession.Difficulty = difficulty;
        try {
            body();
        } finally {
            if (manager != null) manager.CurrentSession.Difficulty = original;
        }
    }

    /// <summary>
    /// A fixture boss shaped like the First Unbound — 1000 base HP, the authored
    /// [0.66, 0.33] thresholds — rather than the authored resource itself, so these
    /// cases pin the mechanism and not the (open, VERIFY-BOSS-HP) HP curve.
    /// </summary>
    private static BossController CreateBoss(bool historicalRecovery = true) {
        var data = new BossData {
            BossID = "first_unbound_under_test",
            DisplayNameKey = "boss_apex_eraser_name",
            MaxHP = BaseHP,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = new[] { 0.66f, 0.33f },
            PhaseTransitionInvincibilityDuration = 2f,
            HasHistoricalRecovery = historicalRecovery,
            BossAbilities = new[] {
                new EnemyAbilityData {
                    AbilityID = "boss.first_unbound_under_test.swing",
                    Archetype = EnemyAbilityArchetype.MeleeStrike,
                    RangeClass = EnemyAbilityRangeClass.Any,
                    SelectionWeight = 1f,
                    TelegraphFrames = 20,
                    ActiveFrames = 6,
                    RecoveryFrames = 10,
                    Damage = 10f
                }
            }
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 20260913;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }
}
