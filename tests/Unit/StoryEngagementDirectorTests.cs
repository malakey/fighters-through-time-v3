using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Gameplay update 2026-10-12, request 6 (GU-F): the Story engagement director
/// in real frames. Every case runs its body inside a physics frame through
/// <see cref="OutOfBandPhysicsStep.RunInPhysicsFrameAsync"/> and steps the mobs
/// by calling <c>_PhysicsProcess</c> directly, so <c>MoveAndSlide</c> reads the
/// fixed step. The hero is a bare, never-ticking <see cref="PlayerController"/>
/// with a pushbox and no hurtbox, so no mob attack moves or hurts it and the
/// only thing being measured is who commits, when, and where the rest stand.
/// Mobs get a fixed reaction delay (no RNG draw), so two runs are comparable.
/// Each case snapshots and restores <c>GameManager.CurrentSession</c> and
/// resets the director's registry in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryEngagementDirectorTests {
    private const float Step = 1f / 60f;
    private const int ReactionFrames = 18;

    // ---- one side, rotation, sides and difficulty --------------------------

    [TestCase]
    public async Task ThreeSlashersOnOneSideCommitOneAtATimeWhileTheRestHoldSpacedRanks() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", 300f), ("chrono_slasher", 380f), ("chrono_slasher", 460f));
            try {
                Trace trace = scenario.Run(1200);
                AssertThat(trace.WaiterCommits)
                    .OverrideFailureMessage("A mob without a melee slot committed to an attack.")
                    .IsEqual(0);
                AssertThat(trace.MaxAttacking)
                    .OverrideFailureMessage($"{trace.MaxAttacking} mobs attacked at once on one side on Normal.")
                    .IsEqual(1);
                AssertThat(trace.Commits.Select(commit => commit.Mob).Distinct().Count())
                    .OverrideFailureMessage("Not every slasher got a turn.")
                    .IsEqual(3);
                // The waiters, once settled, stand on their own ranks 64 px apart.
                AssertThat(trace.SettledWaiterPairs > 0)
                    .OverrideFailureMessage("Two waiters never stood settled on the ring at once.")
                    .IsTrue();
                AssertThat(trace.SettledOffRank)
                    .OverrideFailureMessage("A settled waiter stood off its rank's gap.")
                    .IsEqual(0);
                AssertThat(trace.MinSettledRankGapDifference >= StoryEngagementRules.WaitRingRankSpacingPixels - 0.01f)
                    .OverrideFailureMessage($"Two settled waiters shared a rank ({trace.MinSettledRankGapDifference:F1} px apart in rank).")
                    .IsTrue();
                AssertThat(trace.MinSettledSpacing >= StoryEngagementRules.WaitRingRankSpacingPixels
                        - 2f * StoryEngagementRules.WaitRingTolerancePixels - 4f)
                    .OverrideFailureMessage($"Two settled waiters stood {trace.MinSettledSpacing:F1} px apart.")
                    .IsTrue();
                // Every waiter stays outside the hero's reach once settled.
                AssertThat(trace.MinSettledWaiterGap >= StoryEngagementRules.WaitRingRankZeroGapPixels(Difficulty.Normal)
                        - StoryEngagementRules.WaitRingTolerancePixels - 4f)
                    .IsTrue();
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task TheSlotRotatesRoundRobinAcrossTheWaiters() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", 300f), ("chrono_slasher", 380f), ("chrono_slasher", 460f));
            try {
                Trace trace = scenario.Run(1500);
                int[] order = trace.Commits.Select(commit => commit.Mob).ToArray();
                AssertThat(order.Length >= 7)
                    .OverrideFailureMessage($"Only {order.Length} commits in 1500 frames.")
                    .IsTrue();
                AssertThat(order.Take(3).Distinct().Count()).IsEqual(3);
                for (int index = 3; index < order.Length; index++) {
                    AssertThat(order[index])
                        .OverrideFailureMessage($"Commit {index} went to mob {order[index]}; round-robin owed it to mob {order[index - 3]} ({string.Join(",", order)}).")
                        .IsEqual(order[index - 3]);
                }
                // A hand-off rests the old holder (it ranks behind the rested).
                AssertThat(trace.HandOffRestSeen)
                    .OverrideFailureMessage("No holder ever handed off into a rest.")
                    .IsTrue();
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task OnNormalOneMobAttacksFromEachSideAtOnce() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", -300f), ("chrono_slasher", -380f),
                ("chrono_slasher", 300f), ("chrono_slasher", 380f));
            try {
                Trace trace = scenario.Run(900);
                AssertThat(trace.WaiterCommits).IsEqual(0);
                AssertThat(trace.MaxAttackingPerSide)
                    .OverrideFailureMessage("Two mobs attacked from one side on Normal.")
                    .IsEqual(1);
                AssertThat(trace.MaxAttacking)
                    .OverrideFailureMessage("The two sides never attacked at the same time.")
                    .IsEqual(2);
                AssertThat(trace.MaxHolders).IsEqual(2);
                AssertThat(trace.MaxHoldersPerSide).IsEqual(1);
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task TheDifficultySetsHowManyMobsHoldASlotAtOnce() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
                WithDifficulty(difficulty, () => {
                    Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                        ("chrono_slasher", -300f), ("chrono_slasher", -380f),
                        ("chrono_slasher", 300f), ("chrono_slasher", 380f));
                    try {
                        Trace trace = scenario.Run(400);
                        AssertThat(trace.MaxHolders)
                            .OverrideFailureMessage($"{difficulty}: {trace.MaxHolders} slots held at once.")
                            .IsEqual(StoryEngagementRules.MeleeSlotsTotal(difficulty));
                        AssertThat(trace.MaxHoldersPerSide)
                            .OverrideFailureMessage($"{difficulty}: {trace.MaxHoldersPerSide} slots held on one side.")
                            .IsEqual(StoryEngagementRules.MeleeSlotsPerSide(difficulty));
                        AssertThat(trace.MaxAttacking <= StoryEngagementRules.MeleeSlotsTotal(difficulty)).IsTrue();
                        AssertThat(trace.DirectorHolderMismatches)
                            .OverrideFailureMessage($"{difficulty}: the director's count disagreed with the mobs' own flags.")
                            .IsEqual(0);
                    } finally {
                        scenario.Free();
                    }
                });
            }
        });
    }

    // ---- releasing a slot ---------------------------------------------------

    [TestCase]
    public async Task DeathDespawnFreeAndAResettingRewindEachReleaseTheSlotAStunKeepsIt() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Easy, () => {
            foreach (string release in new[] { "death", "despawn", "free", "rewind" }) {
                Scenario scenario = Scenario.Build(new Vector2(0f, 0f), cooldown: 999f,
                    ("chrono_slasher", 300f), ("chrono_slasher", 380f));
                try {
                    EnemyController holder = scenario.Mobs[0];
                    EnemyController waiter = scenario.Mobs[1];
                    scenario.Advance(12);
                    AssertThat(holder.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Slot);
                    AssertThat(waiter.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Waiting);
                    // A stun keeps the slot: the hero is busy with that mob.
                    holder.ApplyStun(0.1f);
                    scenario.Advance(3);
                    AssertThat(holder.CurrentState).IsEqual(EnemyState.Stunned);
                    AssertThat(holder.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Slot);
                    AssertThat(waiter.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Waiting);

                    switch (release) {
                        case "death":
                            holder.TakeDamage(99999);
                            AssertThat(holder.CurrentState).IsEqual(EnemyState.Dead);
                            break;
                        case "despawn":
                            holder.OnDespawn();
                            // The pool parks the body; it is never stepped again.
                            scenario.Mobs.RemoveAt(0);
                            scenario.Parked.Add(holder);
                            break;
                        case "free":
                            scenario.Mobs.RemoveAt(0);
                            holder.Free();
                            break;
                        case "rewind":
                            holder.RewindPolicy = StoryRewindPolicy.ResetToInitialState;
                            holder.ApplyStoryRewind();
                            AssertThat(holder.CurrentState).IsEqual(EnemyState.Patrol);
                            break;
                    }
                    if (GodotObject.IsInstanceValid(holder)) {
                        AssertThat(holder.EngagementRole)
                            .OverrideFailureMessage($"{release}: the old holder kept its role.")
                            .IsEqual(StoryEngagementRules.EngagementRole.None);
                    }
                    waiter._PhysicsProcess(Step);
                    AssertThat(waiter.EngagementRole)
                        .OverrideFailureMessage($"{release}: the waiter did not take the freed slot.")
                        .IsEqual(StoryEngagementRules.EngagementRole.Slot);
                } finally {
                    scenario.Free();
                }
            }
        }));
    }

    // ---- a lone mob, elites, ranged ------------------------------------------

    [TestCase]
    public async Task ALoneMobIsFrameIdenticalToADirectorlessControl() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            List<string> control;
            Scenario bypassed = Scenario.Build(new Vector2(0f, 0f), ("chrono_slasher", 300f));
            try {
                StoryEngagementDirector.BypassForTest = true;
                control = bypassed.Record(500, includeEngagement: false);
                AssertThat(bypassed.Mobs[0].EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.None);
                AssertThat(control.Count(frame => frame.StartsWith("Attacking")) > 0).IsTrue();
            } finally {
                StoryEngagementDirector.BypassForTest = false;
                bypassed.Free();
            }

            Scenario directed = Scenario.Build(new Vector2(0f, 0f), ("chrono_slasher", 300f));
            try {
                EnemyController mob = directed.Mobs[0];
                // Frame 0 aggroes (Patrol -> Chase); frame 1 is its first Chase tick.
                List<string> directedFrames = directed.RecordFrom(0, 2, includeEngagement: false);
                AssertThat(mob.EngagementRole)
                    .OverrideFailureMessage("A lone mob must take a slot on its first Chase tick.")
                    .IsEqual(StoryEngagementRules.EngagementRole.Slot);
                directedFrames.AddRange(directed.RecordFrom(2, 500, includeEngagement: false));
                for (int frame = 0; frame < 500; frame++) {
                    AssertThat(directedFrames[frame])
                        .OverrideFailureMessage($"Frame {frame} differs from the control.")
                        .IsEqual(control[frame]);
                }
            } finally {
                directed.Free();
            }
        }));
    }

    [TestCase]
    public async Task AnEliteTakesTheNextSlotBeforeALongerWaitingStandard() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Easy, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", 300f), ("chrono_slasher", 380f));
            try {
                scenario.Advance(30);
                EnemyController elite = scenario.Add("chrono_guard_elite", 440f);
                AssertThat(StoryEngagementRules.ClassOf(elite.PreviewNextAttack().Archetype, false))
                    .IsEqual(StoryEngagementRules.EngagementClass.MeleeSlot);
                Trace trace = scenario.Run(900);
                AssertThat(trace.Commits.Count >= 2).IsTrue();
                AssertThat(trace.Commits[0].Mob).IsEqual(0);
                AssertThat(trace.Commits[1].Mob)
                    .OverrideFailureMessage($"The slot went to mob {trace.Commits[1].Mob}, not the elite (mob 2).")
                    .IsEqual(2);
                // The elite's turn ends like any other (review fix 2026-10-13):
                // the standard that waited longest gets the next one.
                int[] order = trace.Commits.Select(commit => commit.Mob).ToArray();
                AssertThat(order.Skip(2).Contains(1))
                    .OverrideFailureMessage($"The longer-waiting standard (mob 1) never got a turn after the elite's ({string.Join(",", order)}).")
                    .IsTrue();
                AssertThat(trace.WaiterCommits).IsEqual(0);
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task AnEliteHandsItsTurnToARestedStandardAndOnlyGetsItBackAfterTheStandardsTurn() {
        // Review fix 2026-10-13: an elite that has just handed off is resting,
        // and it used to outrank every rested standard and take Easy's one slot
        // straight back, so the standard never got a turn.
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Easy, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_guard_elite", 300f), ("chrono_slasher", 380f));
            try {
                Trace trace = scenario.Run(900);
                int[] order = trace.Commits.Select(commit => commit.Mob).ToArray();
                string orderText = string.Join(",", order);
                AssertThat(order.Length >= 3)
                    .OverrideFailureMessage($"Only {order.Length} commits in 900 frames ({orderText}).")
                    .IsTrue();
                AssertThat(order[0]).OverrideFailureMessage($"The elite should open ({orderText}).").IsEqual(0);
                AssertThat(order[1])
                    .OverrideFailureMessage($"The elite took its slot back instead of handing it to the rested standard ({orderText}).")
                    .IsEqual(1);
                AssertThat(order.Skip(2).Contains(0))
                    .OverrideFailureMessage($"The elite never got its turn back after the standard's ({orderText}).")
                    .IsTrue();
                AssertThat(trace.WaiterCommits).IsEqual(0);
                AssertThat(trace.MaxHolders).IsEqual(1);
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task AStaleEliteHolderHandsItsSlotToTheRestedStandardForGood() {
        // Review fix 2026-10-13: the stale hand-off asks for a grant in the same
        // call, and a resting elite used to win it straight back.
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Easy, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f), cooldown: 999f,
                ("chrono_guard_elite", 300f), ("chrono_slasher", 380f));
            try {
                EnemyController elite = scenario.Mobs[0];
                EnemyController slasher = scenario.Mobs[1];
                // The elite takes the slot but never commits: its slot goes stale.
                elite.SetAttackCooldownForTest(999f);
                scenario.Advance(3);
                AssertThat(elite.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Slot);
                AssertThat(slasher.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Waiting);

                int handOffFrame = -1;
                for (int frame = 0; frame < 300 && handOffFrame < 0; frame++) {
                    scenario.Advance(1);
                    if (elite.EngagementRole != StoryEngagementRules.EngagementRole.Slot) handOffFrame = frame;
                }
                AssertThat(handOffFrame >= 0)
                    .OverrideFailureMessage("The stale elite never let its slot go.")
                    .IsTrue();
                AssertThat(handOffFrame + 3 >= StoryEngagementRules.StaleSlotFrames)
                    .OverrideFailureMessage($"The elite handed off after {handOffFrame + 3} frames, before going stale.")
                    .IsTrue();
                AssertThat(elite.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Waiting);
                AssertThat(elite.EngagementRestFramesRemaining > 0).IsTrue();
                AssertThat(slasher.EngagementRole)
                    .OverrideFailureMessage("The stale hand-off did not pass the slot to the rested standard.")
                    .IsEqual(StoryEngagementRules.EngagementRole.Slot);
                // It stays passed: the elite does not take it back while it rests.
                for (int frame = 0; frame < StoryEngagementRules.RestFramesAfterHandoff(Difficulty.Easy); frame++) {
                    scenario.Advance(1);
                    AssertThat(elite.EngagementRole == StoryEngagementRules.EngagementRole.Slot)
                        .OverrideFailureMessage($"The elite took the slot back {frame + 1} frames after handing it off.")
                        .IsFalse();
                }
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task RangedPermitsCapConcurrentShotsAndNoShooterStarves() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal }) {
                WithDifficulty(difficulty, () => {
                    Scenario scenario = Scenario.Build(new Vector2(0f, 0f), cooldown: 0.5f,
                        ("laser_archer", 300f), ("laser_archer", 340f), ("laser_archer", 380f));
                    try {
                        Trace trace = scenario.Run(900);
                        int permits = StoryEngagementRules.RangedPermits(difficulty);
                        AssertThat(trace.MaxAttacking)
                            .OverrideFailureMessage($"{difficulty}: {trace.MaxAttacking} archers shot at once against {permits} permits.")
                            .IsEqual(permits);
                        for (int mob = 0; mob < 3; mob++) {
                            int shots = trace.Commits.Count(commit => commit.Mob == mob);
                            AssertThat(shots >= 2)
                                .OverrideFailureMessage($"{difficulty}: archer {mob} shot {shots} times in 900 frames.")
                                .IsTrue();
                        }
                        // Archers hold no melee slot.
                        AssertThat(trace.MaxHolders).IsEqual(0);
                    } finally {
                        scenario.Free();
                    }
                });
            }
        });
    }

    [TestCase]
    public async Task TheLongestRefusedShooterTakesTheNextRangedPermitWhateverTheTreeOrder() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Easy, () => {
            // Tree order: early (0), patient (1), firing (2). Easy has one permit.
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f), cooldown: 0.5f,
                ("laser_archer", 300f), ("laser_archer", 340f), ("laser_archer", 380f));
            try {
                EnemyController early = scenario.Mobs[0];
                EnemyController patient = scenario.Mobs[1];
                EnemyController firing = scenario.Mobs[2];
                early.SetAttackCooldownForTest(999f);
                patient.SetAttackCooldownForTest(999f);
                scenario.Advance(3);
                AssertThat(firing.CurrentState).IsEqual(EnemyState.Attacking);
                // The patient archer is ready first and refused for 20 frames...
                patient.SetAttackCooldownForTest(0f);
                scenario.Advance(20);
                AssertThat(patient.CurrentState).IsEqual(EnemyState.Chase);
                // ...then the early archer, which ticks first, becomes ready too.
                early.SetAttackCooldownForTest(0f);
                Trace trace = scenario.Run(200);
                AssertThat(trace.Commits.Count >= 1).IsTrue();
                AssertThat(trace.Commits[0].Mob)
                    .OverrideFailureMessage("The permit went to the archer that ticks first, not the one refused longest.")
                    .IsEqual(1);
                AssertThat(trace.MaxAttacking).IsEqual(1);
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task AShooterHoldsInsideTheZoomedViewNotAtItsWidenedRange() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f), cooldown: 999f, ("infrared_border_sentry", 800f));
            try {
                EnemyController sentry = scenario.Mobs[0];
                AssertThat(sentry.AttackRangePixels * EnemyController.StandOffEngageFraction
                    > StoryEngagementRules.RangedHoldMaxPixels).IsTrue();
                AssertThat(sentry.StandOffEngagePixels).IsEqualApprox(StoryEngagementRules.RangedHoldMaxPixels, 0.001f);
                // The capped band's own hysteresis release would sit past the
                // half view; it is capped too (review fix 2026-10-13).
                AssertThat(StoryEngagementRules.RangedHoldMaxPixels * EnemyController.StandOffResumeFraction
                    / EnemyController.StandOffEngageFraction > StoryEngagementRules.ZoomedHalfViewPixels).IsTrue();
                AssertThat(sentry.StandOffResumePixels)
                    .IsEqualApprox(StoryEngagementRules.RangedHoldReleaseMaxPixels, 0.001f);
                int guard = 0;
                while (!sentry.IsStandOffEngaged && guard++ < 1200) scenario.Advance(1);
                scenario.Advance(30);
                float gap = Mathf.Abs(sentry.GlobalPosition.X);
                AssertThat(sentry.IsStandOffEngaged).IsTrue();
                AssertThat(gap <= StoryEngagementRules.RangedHoldMaxPixels + 0.5f && gap > 500f)
                    .OverrideFailureMessage($"The sentry held at {gap:F1} px.")
                    .IsTrue();
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task AWaitingShooterFollowsAHeroWhoBacksAwayBeforeItWouldStandOffScreen() {
        // Review fix 2026-10-13: the capped band used to release only past
        // 724.7 px, so a sentry waiting out its cooldown stood still off the
        // 640 px half view while the hero backed away.
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f), cooldown: 999f, ("infrared_border_sentry", 800f));
            try {
                EnemyController sentry = scenario.Mobs[0];
                int guard = 0;
                while (!sentry.IsStandOffEngaged && guard++ < 1200) scenario.Advance(1);
                scenario.Advance(30);
                AssertThat(sentry.IsStandOffEngaged).OverrideFailureMessage("Harness: the sentry never held.").IsTrue();
                AssertThat(sentry.CurrentState).IsEqual(EnemyState.Chase);

                // Inside the dead band the hold stands (the hysteresis is kept).
                float release = StoryEngagementRules.RangedHoldReleaseMaxPixels;
                float hold = StoryEngagementRules.RangedHoldMaxPixels;
                scenario.Hero.GlobalPosition = new Vector2(sentry.GlobalPosition.X - (hold + release) / 2f, 0f);
                scenario.Advance(5);
                AssertThat(sentry.IsStandOffEngaged)
                    .OverrideFailureMessage("The sentry released inside its dead band.")
                    .IsTrue();

                // The hero backs off past the release line, still inside the half
                // view: the sentry follows instead of standing there.
                float backedOff = (release + StoryEngagementRules.ZoomedHalfViewPixels) / 2f;
                scenario.Hero.GlobalPosition = new Vector2(sentry.GlobalPosition.X - backedOff, 0f);
                scenario.Advance(2);
                AssertThat(sentry.IsStandOffEngaged)
                    .OverrideFailureMessage($"The sentry still held at {backedOff:F1} px, past its {release:F1} px release.")
                    .IsFalse();
                AssertThat(sentry.Velocity.X < 0f)
                    .OverrideFailureMessage("The released sentry did not walk after the hero.")
                    .IsTrue();

                float farthestStandStill = 0f;
                guard = 0;
                while (!sentry.IsStandOffEngaged && guard++ < 600) {
                    scenario.Advance(1);
                    float gap = Mathf.Abs(sentry.GlobalPosition.X - scenario.Hero.GlobalPosition.X);
                    if (Mathf.Abs(sentry.Velocity.X) < 0.5f) farthestStandStill = Mathf.Max(farthestStandStill, gap);
                }
                float settled = Mathf.Abs(sentry.GlobalPosition.X - scenario.Hero.GlobalPosition.X);
                AssertThat(sentry.IsStandOffEngaged).OverrideFailureMessage("The sentry never held again.").IsTrue();
                AssertThat(settled <= hold + 0.5f)
                    .OverrideFailureMessage($"The sentry re-held at {settled:F1} px.")
                    .IsTrue();
                AssertThat(farthestStandStill <= release)
                    .OverrideFailureMessage($"The sentry stood still {farthestStandStill:F1} px out while following.")
                    .IsTrue();
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task TheEngageBlinkIsGrantedTheSlotOnTheSideItLandsOn() {
        // Review fix 2026-10-13: the Rift Phantom's engage blink lands on the
        // far side of the hero, and it used to be granted the slot of the side
        // it stood on, so on Normal two holders ended up on one side.
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            // A slasher holds the far (right) side: the phantom must not blink
            // there; it takes the free near slot and walks in instead.
            Scenario held = Scenario.Build(new Vector2(0f, 0f), ("chrono_slasher", 300f), ("rift_phantom", -300f));
            try {
                EnemyController slasher = held.Mobs[0];
                EnemyController phantom = held.Mobs[1];
                SideTrace trace = RunSides(held, 600);
                AssertThat(trace.PhantomBlinked)
                    .OverrideFailureMessage("The phantom blinked onto the side the slasher holds.")
                    .IsFalse();
                AssertThat(trace.MaxHoldersPerSide)
                    .OverrideFailureMessage("Two mobs held a slot on one side on Normal.")
                    .IsEqual(1);
                AssertThat(trace.MaxAttackingPerSide).IsEqual(1);
                AssertThat(held.Mobs.All(mob => mob.EngagementRole == StoryEngagementRules.EngagementRole.Slot)).IsTrue();
                AssertThat(phantom.GlobalPosition.X < 0f)
                    .OverrideFailureMessage($"The phantom ended at x {phantom.GlobalPosition.X:F1}, on the slasher's side.")
                    .IsTrue();
                AssertThat(trace.PhantomCommits > 0)
                    .OverrideFailureMessage("The phantom never attacked from its own side.")
                    .IsTrue();
                AssertThat(slasher.GlobalPosition.X > 0f).IsTrue();
            } finally {
                held.Free();
            }

            // The near (left) side is held and the far side is free: the
            // phantom is granted the far slot and blinks there at once.
            Scenario free = Scenario.Build(new Vector2(0f, 0f), ("chrono_slasher", -300f), ("rift_phantom", -420f));
            try {
                EnemyController slasher = free.Mobs[0];
                EnemyController phantom = free.Mobs[1];
                SideTrace trace = RunSides(free, 600);
                AssertThat(trace.PhantomBlinked)
                    .OverrideFailureMessage("The phantom never blinked to the free far side.")
                    .IsTrue();
                AssertThat(trace.FirstBlinkFrame <= 3)
                    .OverrideFailureMessage($"The phantom first blinked on frame {trace.FirstBlinkFrame}.")
                    .IsTrue();
                AssertThat(trace.SlasherHeldAtFirstBlink)
                    .OverrideFailureMessage("The phantom blinked only after the near holder let its slot go.")
                    .IsTrue();
                AssertThat(trace.PhantomSideAfterFirstBlink)
                    .OverrideFailureMessage("The phantom did not land on the far side.")
                    .IsEqual(1);
                AssertThat(trace.MaxHoldersPerSide).IsEqual(1);
                AssertThat(trace.MaxAttackingPerSide).IsEqual(1);
                AssertThat(slasher.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Slot);
            } finally {
                free.Free();
            }
        }));
    }

    // ---- targets, freezes, determinism, sides -------------------------------

    [TestCase]
    public async Task NoMobTargetsOrGroupsOnTheMirrorClone() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(-300f, 0f), ("chrono_slasher", 0f));
            PlayerController clone = CreateHero(new Vector2(120f, 0f), storyHostile: true);
            try {
                EnemyController mob = scenario.Mobs[0];
                scenario.Advance(4);
                // The clone is nearer, but the mob chases the hero.
                AssertThat(mob.CurrentState).IsEqual(EnemyState.Chase);
                AssertThat(mob.Velocity.X < 0f)
                    .OverrideFailureMessage("The mob walked toward the Mirror clone.")
                    .IsTrue();
                AssertThat(StoryEngagementDirector.MemberCountFor(scenario.Hero)).IsEqual(1);
                AssertThat(StoryEngagementDirector.MemberCountFor(clone)).IsEqual(0);

                // Pinned on the clone, the director never opens a group for it.
                mob.SetChaseTargetForTest(clone);
                scenario.Advance(3);
                AssertThat(mob.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.None);
                AssertThat(StoryEngagementDirector.MemberCountFor(clone)).IsEqual(0);
                AssertThat(StoryEngagementDirector.MemberCountFor(scenario.Hero)).IsEqual(0);
            } finally {
                clone.Free();
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task TimeFreezeLeavesRolesCountersAndPositionsUntouched() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", 300f), ("chrono_slasher", 380f), ("chrono_slasher", 460f));
            try {
                scenario.Advance(140);
                string before = scenario.Snapshot();
                int holdersBefore = StoryEngagementDirector.HolderCountFor(scenario.Hero);
                AssertThat(holdersBefore <= 1).IsTrue();
                foreach (EnemyController mob in scenario.Mobs) mob.SetTimeFrozen(true);
                scenario.Advance(180);
                AssertThat(scenario.Snapshot())
                    .OverrideFailureMessage("A frozen mob moved, changed role or ran an engagement counter.")
                    .IsEqual(before);
                AssertThat(StoryEngagementDirector.MemberCountFor(scenario.Hero)).IsEqual(3);
                AssertThat(StoryEngagementDirector.HolderCountFor(scenario.Hero)).IsEqual(holdersBefore);
                foreach (EnemyController mob in scenario.Mobs) mob.SetTimeFrozen(false);
                Trace trace = scenario.Run(600);
                AssertThat(trace.MaxAttacking).IsEqual(1);
                AssertThat(trace.Commits.Select(commit => commit.Mob).Distinct().Count() >= 2).IsTrue();
            } finally {
                scenario.Free();
            }
        }));
    }

    [TestCase]
    public async Task TwoIdenticalRunsProduceIdenticalTrajectories() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            List<string> first;
            Scenario one = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", -320f), ("chrono_slasher", 300f), ("chrono_slasher", 380f), ("chrono_slasher", 460f));
            try {
                first = one.Record(700);
            } finally {
                one.Free();
            }
            Scenario two = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", -320f), ("chrono_slasher", 300f), ("chrono_slasher", 380f), ("chrono_slasher", 460f));
            try {
                List<string> second = two.Record(700);
                for (int frame = 0; frame < first.Count; frame++) {
                    AssertThat(second[frame])
                        .OverrideFailureMessage($"Frame {frame} differs between two identical runs.")
                        .IsEqual(first[frame]);
                }
            } finally {
                two.Free();
            }
        }));
    }

    [TestCase]
    public async Task AWaiterCutOffByAWallCrossesToTheEmptierSideAndTakesItsSlot() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => WithDifficulty(Difficulty.Normal, () => {
            // A wall 200 px right of the hero leaves room for rank 0 (170 px) only.
            StaticBody2D wall = CreateWall(220f);
            Scenario scenario = Scenario.Build(new Vector2(0f, 0f),
                ("chrono_slasher", 100f), ("chrono_slasher", 140f), ("chrono_slasher", 175f));
            try {
                Vector2 heroBefore = scenario.Hero.GlobalPosition;
                bool sawCrossing = false;
                bool pushboxDownWhileCrossing = true;
                int crosser = -1;
                int maxHolders = 0;
                for (int frame = 0; frame < 600; frame++) {
                    scenario.Advance(1);
                    maxHolders = Math.Max(maxHolders, StoryEngagementDirector.HolderCountFor(scenario.Hero));
                    for (int index = 0; index < scenario.Mobs.Count; index++) {
                        EnemyController mob = scenario.Mobs[index];
                        if (!mob.IsEngagementCrossing) continue;
                        sawCrossing = true;
                        crosser = index;
                        if (mob.GetNode<CombatantPushbox>("Pushbox").PushEnabled) pushboxDownWhileCrossing = false;
                    }
                }
                AssertThat(sawCrossing)
                    .OverrideFailureMessage("No waiter crossed although rank 1 had no room behind the wall.")
                    .IsTrue();
                AssertThat(pushboxDownWhileCrossing).IsTrue();
                EnemyController moved = scenario.Mobs[crosser];
                AssertThat(moved.GlobalPosition.X < 0f)
                    .OverrideFailureMessage($"The crossing mob ended at x {moved.GlobalPosition.X:F1}.")
                    .IsTrue();
                foreach (EnemyController mob in scenario.Mobs) {
                    AssertThat(mob.IsEngagementCrossing).IsFalse();
                    AssertThat(mob.GetNode<CombatantPushbox>("Pushbox").PushEnabled).IsTrue();
                }
                // Normal: the far side's slot went to the crosser; one per side.
                AssertThat(maxHolders).IsEqual(2);
                AssertThat(moved.EngagementRole).IsEqual(StoryEngagementRules.EngagementRole.Slot);
                AssertThat(scenario.Hero.GlobalPosition.DistanceTo(heroBefore) < 0.5f)
                    .OverrideFailureMessage("The crossing shoved the hero.")
                    .IsTrue();
            } finally {
                scenario.Free();
                wall.Free();
            }
        }));
    }

    [TestCase]
    public void NothingInTheFighterSimNamesTheEngagementDirector() {
        string root = ProjectSettings.GlobalizePath("res://scripts/FighterSim");
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("StoryEngagement"))
            .Select(Path.GetFileName)
            .ToList();
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");
    }

    // ---- harness ---------------------------------------------------------------

    private readonly record struct Commit(int Frame, int Mob, StoryEngagementRules.EngagementRole Role);

    private sealed class Trace {
        public readonly List<Commit> Commits = new();
        public int WaiterCommits;
        public int MaxAttacking;
        public int MaxAttackingPerSide;
        public int MaxHolders;
        public int MaxHoldersPerSide;
        public int DirectorHolderMismatches;
        public int SettledWaiterPairs;
        public int SettledOffRank;
        public float MinSettledSpacing = float.MaxValue;
        public float MinSettledRankGapDifference = float.MaxValue;
        public float MinSettledWaiterGap = float.MaxValue;
        public bool HandOffRestSeen;
    }

    /// <summary>What <see cref="RunSides"/> saw of a slasher and a Rift Phantom.</summary>
    private sealed class SideTrace {
        public bool PhantomBlinked;
        public int FirstBlinkFrame = -1;
        public bool SlasherHeldAtFirstBlink;
        public int PhantomSideAfterFirstBlink;
        public int PhantomCommits;
        public int MaxHoldersPerSide;
        public int MaxAttackingPerSide;
    }

    /// <summary>
    /// Steps a scenario of one slasher and one Rift Phantom, counting holders and
    /// attackers per side by where they stand. Frames with an engage blink in
    /// flight are left out of the per-side counts: the blinker still stands on
    /// the side it leaves until the teleport lands.
    /// </summary>
    private static SideTrace RunSides(Scenario scenario, int frames) {
        EnemyController phantom = scenario.Mobs.Single(mob => mob.Data.EnemyID == "rift_phantom");
        EnemyController slasher = scenario.Mobs.Single(mob => mob != phantom);
        var trace = new SideTrace();
        EnemyState previousPhantomState = phantom.CurrentState;
        for (int frame = 0; frame < frames; frame++) {
            scenario.Advance(1);
            bool blinking = phantom.IsEngageBlinking;
            if (blinking && !trace.PhantomBlinked) {
                trace.PhantomBlinked = true;
                trace.FirstBlinkFrame = frame;
                trace.SlasherHeldAtFirstBlink = slasher.EngagementRole == StoryEngagementRules.EngagementRole.Slot;
            }
            if (trace.PhantomBlinked && !blinking && trace.PhantomSideAfterFirstBlink == 0) {
                trace.PhantomSideAfterFirstBlink = phantom.GlobalPosition.X < scenario.Hero.GlobalPosition.X ? -1 : 1;
            }
            if (!blinking && phantom.CurrentState == EnemyState.Attacking && previousPhantomState != EnemyState.Attacking) {
                trace.PhantomCommits++;
            }
            previousPhantomState = phantom.CurrentState;
            if (blinking) continue;
            int holdersLeft = 0, holdersRight = 0, attackingLeft = 0, attackingRight = 0;
            foreach (EnemyController mob in scenario.Mobs) {
                bool left = mob.GlobalPosition.X < scenario.Hero.GlobalPosition.X;
                if (mob.EngagementRole == StoryEngagementRules.EngagementRole.Slot) {
                    if (left) holdersLeft++; else holdersRight++;
                }
                if (mob.CurrentState == EnemyState.Attacking) {
                    if (left) attackingLeft++; else attackingRight++;
                }
            }
            trace.MaxHoldersPerSide = Math.Max(trace.MaxHoldersPerSide, Math.Max(holdersLeft, holdersRight));
            trace.MaxAttackingPerSide = Math.Max(trace.MaxAttackingPerSide, Math.Max(attackingLeft, attackingRight));
        }
        return trace;
    }

    private sealed class Scenario {
        public PlayerController Hero;
        public StaticBody2D Floor;
        public readonly List<EnemyController> Mobs = new();
        public readonly List<EnemyController> Parked = new();
        private float? _cooldown;

        public static Scenario Build(Vector2 heroPosition, params (string id, float x)[] mobs) =>
            Build(heroPosition, null, mobs);

        public static Scenario Build(Vector2 heroPosition, float? cooldown, params (string id, float x)[] mobs) {
            StoryEngagementDirector.ResetForTest();
            var scenario = new Scenario {
                Floor = CreateFloor(),
                Hero = CreateHero(heroPosition, storyHostile: false),
                _cooldown = cooldown ?? 0.6f
            };
            foreach ((string id, float x) in mobs) scenario.Add(id, x);
            return scenario;
        }

        public EnemyController Add(string enemyID, float x) {
            EnemyController mob = CreateMob(enemyID, new Vector2(x, 0f), _cooldown);
            Mobs.Add(mob);
            return mob;
        }

        public void Advance(int frames) {
            for (int frame = 0; frame < frames; frame++) {
                foreach (EnemyController mob in Mobs) {
                    if (GodotObject.IsInstanceValid(mob)) mob._PhysicsProcess(Step);
                }
            }
        }

        public Trace Run(int frames) {
            var trace = new Trace();
            var previous = Mobs.Select(mob => mob.CurrentState).ToArray();
            var previousRoles = Mobs.Select(mob => mob.EngagementRole).ToArray();
            // A waiter counts as settled only after a whole frame at rest on an
            // unchanged rank: a hand-off re-ranks the ring, and the waiter starts
            // walking to its new rank on its next tick.
            var previousRanks = Mobs.Select(mob => mob.WaitRingRank).ToArray();
            var previousVx = Mobs.Select(mob => mob.Velocity.X).ToArray();
            for (int frame = 0; frame < frames; frame++) {
                Advance(1);
                int attacking = 0, attackingLeft = 0, attackingRight = 0;
                int holders = 0, holdersLeft = 0, holdersRight = 0;
                var settled = new List<EnemyController>();
                for (int index = 0; index < Mobs.Count; index++) {
                    EnemyController mob = Mobs[index];
                    bool left = mob.GlobalPosition.X < Hero.GlobalPosition.X;
                    if (mob.CurrentState == EnemyState.Attacking) {
                        attacking++;
                        if (left) attackingLeft++; else attackingRight++;
                        if (previous[index] != EnemyState.Attacking) {
                            trace.Commits.Add(new Commit(frame, index, mob.EngagementRole));
                            if (mob.EngagementRole != StoryEngagementRules.EngagementRole.Slot
                                && mob.PreviewNextAttack() is { } next
                                && StoryEngagementRules.ClassOf(next.Archetype, false) == StoryEngagementRules.EngagementClass.MeleeSlot) {
                                trace.WaiterCommits++;
                            }
                        }
                    }
                    if (mob.EngagementRole == StoryEngagementRules.EngagementRole.Slot) {
                        holders++;
                        if (left) holdersLeft++; else holdersRight++;
                    }
                    if (previousRoles[index] == StoryEngagementRules.EngagementRole.Slot
                        && mob.EngagementRole == StoryEngagementRules.EngagementRole.Waiting
                        && mob.EngagementRestFramesRemaining > 0) {
                        trace.HandOffRestSeen = true;
                    }
                    int rank = mob.WaitRingRank;
                    if (rank >= 0 && rank == previousRanks[index] && mob.CurrentState == EnemyState.Chase
                        && Mathf.Abs(mob.Velocity.X) < 0.5f && Mathf.Abs(previousVx[index]) < 0.5f) {
                        settled.Add(mob);
                    }
                    previous[index] = mob.CurrentState;
                    previousRoles[index] = mob.EngagementRole;
                    previousRanks[index] = rank;
                    previousVx[index] = mob.Velocity.X;
                }
                trace.MaxAttacking = Math.Max(trace.MaxAttacking, attacking);
                trace.MaxAttackingPerSide = Math.Max(trace.MaxAttackingPerSide, Math.Max(attackingLeft, attackingRight));
                trace.MaxHolders = Math.Max(trace.MaxHolders, holders);
                trace.MaxHoldersPerSide = Math.Max(trace.MaxHoldersPerSide, Math.Max(holdersLeft, holdersRight));
                if (StoryEngagementDirector.HolderCountFor(Hero) != holders) trace.DirectorHolderMismatches++;
                foreach (EnemyController waiter in settled) {
                    float gap = Mathf.Abs(waiter.GlobalPosition.X - Hero.GlobalPosition.X);
                    if (Mathf.Abs(gap - waiter.WaitRingGapPixels) > StoryEngagementRules.WaitRingTolerancePixels + 4f) {
                        trace.SettledOffRank++;
                    }
                    trace.MinSettledWaiterGap = Mathf.Min(trace.MinSettledWaiterGap, gap);
                }
                for (int a = 0; a < settled.Count; a++) {
                    for (int b = a + 1; b < settled.Count; b++) {
                        bool sameSide = (settled[a].GlobalPosition.X < Hero.GlobalPosition.X)
                            == (settled[b].GlobalPosition.X < Hero.GlobalPosition.X);
                        if (!sameSide) continue;
                        trace.SettledWaiterPairs++;
                        trace.MinSettledSpacing = Mathf.Min(trace.MinSettledSpacing,
                            Mathf.Abs(settled[a].GlobalPosition.X - settled[b].GlobalPosition.X));
                        trace.MinSettledRankGapDifference = Mathf.Min(trace.MinSettledRankGapDifference,
                            Mathf.Abs(settled[a].WaitRingGapPixels - settled[b].WaitRingGapPixels));
                    }
                }
            }
            return trace;
        }

        /// <summary>One line per frame: every mob's state, phase, position and velocity (and its engagement fields).</summary>
        public List<string> Record(int frames, bool includeEngagement = true) => RecordFrom(0, frames, includeEngagement);

        public List<string> RecordFrom(int start, int end, bool includeEngagement = true) {
            var frames = new List<string>();
            for (int frame = start; frame < end; frame++) {
                Advance(1);
                frames.Add(Snapshot(includeEngagement));
            }
            return frames;
        }

        public string Snapshot(bool includeEngagement = true) => string.Join(" | ", Mobs.Select(mob =>
            $"{mob.CurrentState}/{mob.AbilityPhase}"
            + $"@{mob.GlobalPosition.X:F3},{mob.GlobalPosition.Y:F3} v{mob.Velocity.X:F3},{mob.Velocity.Y:F3}"
            + (includeEngagement ? $" {mob.EngagementRole} r{mob.EngagementRestFramesRemaining}" : "")));

        public void Free() {
            try {
                foreach (EnemyController mob in Mobs.Concat(Parked)) {
                    if (GodotObject.IsInstanceValid(mob)) mob.Free();
                }
                if (GodotObject.IsInstanceValid(Hero)) Hero.Free();
                if (GodotObject.IsInstanceValid(Floor)) Floor.Free();
            } finally {
                StoryEngagementDirector.ResetForTest();
            }
        }
    }

    private static void WithDifficulty(Difficulty difficulty, Action body) {
        GameManager manager = GameManager.Instance;
        SessionData saved = manager.CurrentSession;
        try {
            manager.CurrentSession.Difficulty = difficulty;
            body();
        } finally {
            manager.CurrentSession = saved;
            StoryEngagementDirector.BypassForTest = false;
            StoryEngagementDirector.ResetForTest();
        }
    }

    private static EnemyController CreateMob(string enemyID, Vector2 position, float? cooldown) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        data.ReactionDelayMinFrames = ReactionFrames;
        data.ReactionDelayMaxFrames = ReactionFrames;
        if (cooldown.HasValue) data.AttackCooldown = cooldown.Value;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var mob = scene.Instantiate<EnemyController>();
        mob.Data = data;
        mob.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(mob);
        mob.OnSpawn();
        return mob;
    }

    /// <summary>
    /// A bare PlayerController (no hurtbox, so mob attacks cannot move it)
    /// carrying the factory-shaped pushbox, in the "Players" group. It never
    /// ticks, so any change in its position was caused by a mob.
    /// </summary>
    private static PlayerController CreateHero(Vector2 position, bool storyHostile) {
        var hero = new PlayerController {
            Name = storyHostile ? "EngagementMirrorClone" : "EngagementHero",
            Position = position,
            IsStoryHostile = storyHostile
        };
        var pushbox = new CombatantPushbox {
            Name = "Pushbox",
            BoxSize = new Vector2(30f, 48f),
            Position = new Vector2(0f, -28f),
            CollisionLayer = CollisionLayers.Player,
            CollisionMask = CollisionLayers.Enemy,
            Monitoring = false,
            Monitorable = false
        };
        pushbox.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = pushbox.BoxSize } });
        hero.AddChild(pushbox);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hero);
        hero.AddToGroup("Players");
        return hero;
    }

    /// <summary>An Environment floor, its top at y = 0, from x −2000 to 2000.</summary>
    private static StaticBody2D CreateFloor() {
        var floor = new StaticBody2D {
            Name = "EngagementFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    /// <summary>An Environment wall 40 px wide and 400 px tall, centred on <paramref name="centreX"/>.</summary>
    private static StaticBody2D CreateWall(float centreX) {
        var wall = new StaticBody2D {
            Name = "EngagementWall",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(40f, 400f) },
            Position = new Vector2(centreX, -200f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(wall);
        return wall;
    }
}
