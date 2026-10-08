using System;
using System.Collections.Generic;
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
/// A default-priority node that relocates a body every physics tick — the
/// stand-in for anything that moves the Mirror clone during its own step (its
/// MoveAndSlide, an Echo Step, a warp). Used by
/// <see cref="BossArenaLeashTests"/>; not a suite.
/// </summary>
public partial class MirrorLeashShoveProbe : Node {
    public Node2D Target;
    public float ShoveToX;
    public int Shoves { get; private set; }

    public override void _PhysicsProcess(double delta) {
        if (Target == null || !IsInstanceValid(Target)) return;
        Target.GlobalPosition = new Vector2(ShoveToX, Target.GlobalPosition.Y);
        Shoves++;
    }
}

/// <summary>
/// Playtest pass 2026-10-04, workstream ARENA — P1. A campaign boss waits
/// dormant in its arena until the encounter reveals it (the player enters the
/// arena rect), and it never leaves that arena: chase, rest tracking and a
/// ChargeDash stop at the edge, and a body knocked, teleported or dashed past
/// it is clamped back. The bots measured the Level 2 Duke marching 1,250 px out
/// of his court from level load and the Level 5 Overseer fighting on the boat
/// deck (design-godot.md §17 boss introduction, §12 "bosses are bounded to
/// stage platforms").
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossArenaLeashTests {
    private const float Step = 1f / 60f;
    private const string BossScenePath = "res://scenes/enemies/Boss.tscn";

    [TestCase]
    public void ABossBuiltDirectlyKeepsItsLegacyBehaviourEngagedAndUnleashed() {
        Node host = CreateHost("LeashDefaultHost");
        try {
            BossController boss = CreateBoss(host, Duke());
            AssertThat(boss.IsEngaged).IsTrue();
            AssertThat(boss.HasArenaLeash).IsFalse();
            AssertThat(boss.IsInsideArena(-1_000_000f)).IsTrue();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public async Task ADormantBossNeitherAcquiresNorMovesNorAttacks() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = CreateHost("LeashDormantHost");
            PlayerController player = null;
            try {
                BossController boss = CreateBoss(host, Duke());
                boss.GlobalPosition = new Vector2(60000f, -30000f);
                boss.IsEngaged = false;
                player = SpawnPlayer(host, boss.GlobalPosition + new Vector2(150f, 0f));

                for (int frame = 0; frame < 90; frame++) boss._PhysicsProcess(Step);

                AssertThat(boss.CurrentState).IsEqual(BossState.Idle);
                AssertObject(boss.SelectedAbility).IsNull();
                AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
                AssertFloat(boss.GlobalPosition.X).IsEqualApprox(60000f, 0.5f);

                // Engaging wakes it: the very next steps acquire and act.
                boss.IsEngaged = true;
                for (int frame = 0; frame < 30; frame++) boss._PhysicsProcess(Step);
                AssertThat(boss.CurrentState == BossState.Idle).IsFalse();
            } finally {
                FreePlayer(player);
                ReleaseAndFree(host);
            }
        });
    }

    /// <summary>
    /// The leash stops the chase at the edge. A target outside the arena and
    /// beyond the boss's engagement range is neither chased nor attacked — the
    /// boss holds the line (answering it at any distance let the RangeClass.Any
    /// summon kits raise a wave every cooldown while the hero stood outside). One
    /// that steps back within range is answered from the edge. A5: the pinned
    /// boss is warded against the far hero, and a hero who re-approaches from
    /// outside to melee spacing hits it normally.
    /// </summary>
    [TestCase]
    public async Task AnEngagedBossHoldsTheArenaEdgeAgainstAFarTargetAndAnswersOneInRange() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = CreateHost("LeashChaseHost");
            PlayerController player = null;
            try {
                const float arenaLeft = 62000f;
                const float floorY = -30000f;
                BuildFloor(host, arenaLeft, floorY);
                BossController boss = CreateBoss(host, Duke());
                boss.ArenaBounds = new Rect2(arenaLeft, -40000f, 1200f, 20000f);
                boss.GlobalPosition = new Vector2(arenaLeft + 150f, floorY);
                // Far outside the Duke's 9-unit (540 px) band, west of the arena.
                player = SpawnPlayer(host, new Vector2(arenaLeft - 900f, floorY));

                for (int frame = 0; frame < 300; frame++) {
                    boss._PhysicsProcess(Step);
                    // Chase stops at the edge...
                    AssertFloat(boss.GlobalPosition.X).IsGreaterEqual(arenaLeft - 0.01f);
                    // ...and nothing is committed against the far target.
                    AssertThat(boss.CurrentState is BossState.Attacking or BossState.RestWindow)
                        .OverrideFailureMessage($"frame {frame}: a pinned boss committed against a target beyond its range")
                        .IsFalse();
                    AssertObject(boss.SelectedAbility).IsNull();
                    AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
                }
                AssertFloat(boss.GlobalPosition.X).IsEqualApprox(arenaLeft, 1f);
                AssertFloat(boss.Velocity.X).IsEqualApprox(0f, 0.001f);

                // A5: pinned there against the hero it acquired itself, the boss
                // is warded — the far hero's plinks land nothing...
                AssertThat(boss.IsArenaWarded).IsTrue();
                int hpPinned = boss.CurrentHP;
                AssertFloat(boss.TakeDamage(WardProbeHit(5f))).IsEqual(0f);
                AssertThat(boss.CurrentHP).IsEqual(hpPinned);
                // ...and the hero who re-approaches to the play bot's strike
                // spacing (92 px from the pinned boss, still 92 px outside the
                // arena — a rewind landing or a retreat west of the edge) is not:
                // every hit lands, so the re-approach can never stall the fight.
                player.GlobalPosition = new Vector2(boss.GlobalPosition.X - 92f, floorY);
                AssertThat(boss.IsArenaWarded).IsFalse();
                AssertFloat(boss.TakeDamage(WardProbeHit(5f))).IsEqual(5f);
                AssertThat(boss.CurrentHP).IsEqual(hpPinned - 5);

                // Back within the band (still outside the arena): answered from
                // the edge, and never with a summon or a teleport.
                player.GlobalPosition = new Vector2(arenaLeft - 250f, floorY);
                bool answered = false;
                for (int frame = 0; frame < 180 && !answered; frame++) {
                    boss._PhysicsProcess(Step);
                    AssertFloat(boss.GlobalPosition.X).IsGreaterEqual(arenaLeft - 0.01f);
                    if (boss.SelectedAbility != null) answered = true;
                }
                AssertThat(answered)
                    .OverrideFailureMessage("A boss pinned at its leash must answer a target within its range.")
                    .IsTrue();
                AssertThat(boss.SelectedAbility.Archetype is EnemyAbilityArchetype.SummonMinions
                    or EnemyAbilityArchetype.Teleport).IsFalse();
            } finally {
                FreePlayer(player);
                ReleaseAndFree(host);
            }
        });
    }

    /// <summary>
    /// Against a target outside the arena the arena-bound archetypes are never
    /// selected — not even by the no-deadlock fallback — so a summon kit cannot
    /// raise waves at a hero who stepped out. With nothing else ready the boss
    /// rests. Inside the arena (or with no leash) the kit is untouched.
    /// </summary>
    [TestCase]
    public async Task AnOutsideTargetNeverDrawsASummonOrATeleport() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = CreateHost("LeashSummonHost");
            PlayerController player = null;
            try {
                EnemyAbilityData summon = KitAbility("p1.summon", EnemyAbilityArchetype.SummonMinions);
                summon.SummonEnemyID = "chrono_slasher";
                summon.SummonCount = 2;
                EnemyAbilityData teleport = KitAbility("p1.teleport", EnemyAbilityArchetype.Teleport);
                EnemyAbilityData strike = KitAbility("p1.strike", EnemyAbilityArchetype.MeleeStrike);
                strike.RangeClass = EnemyAbilityRangeClass.Melee;

                // The pure rule: summon + teleport only — nothing to use outside.
                BossData arenaBound = ScratchBoss("p1_summon_only_boss");
                arenaBound.BossAbilities = new[] { summon, teleport };
                BossController boss = CreateBoss(host, arenaBound);
                for (int roll = 0; roll < 20; roll++) {
                    AssertThat(boss.SelectAbilityIndex(200f, targetOutsideArena: true)).IsEqual(-1);
                    AssertThat(boss.SelectAbilityIndex(200f, targetOutsideArena: false) >= 0).IsTrue();
                    AssertThat(boss.SelectAbilityIndex(200f) >= 0).IsTrue();
                }

                // With a strike in the kit, the outside answer is always the strike.
                BossData mixed = ScratchBoss("p1_mixed_kit_boss");
                mixed.BossAbilities = new[] { summon, strike, teleport };
                BossController mixedBoss = CreateBoss(host, mixed);
                for (int roll = 0; roll < 20; roll++) {
                    AssertThat(mixedBoss.SelectAbilityIndex(60f, targetOutsideArena: true)).IsEqual(1);
                }

                // In the fight: a summon-only boss at its edge, a hero just
                // outside and well within range — it rests, it never summons.
                const float arenaLeft = 76000f;
                const float floorY = -30000f;
                BuildFloor(host, arenaLeft, floorY);
                boss.ArenaBounds = new Rect2(arenaLeft, -40000f, 1200f, 20000f);
                boss.GlobalPosition = new Vector2(arenaLeft, floorY);
                player = SpawnPlayer(host, new Vector2(arenaLeft - 120f, floorY));
                bool rested = false;
                for (int frame = 0; frame < 240; frame++) {
                    boss._PhysicsProcess(Step);
                    AssertObject(boss.SelectedAbility).IsNull();
                    AssertThat(boss.AbilityPhase).IsEqual(EnemyAbilityPhase.Idle);
                    if (boss.CurrentState == BossState.RestWindow) rested = true;
                }
                AssertThat(rested).IsTrue();
                AssertThat(CountEnemiesUnder(host)).IsEqual(0);
            } finally {
                FreePlayer(player);
                ReleaseAndFree(host);
            }
        });
    }

    /// <summary>
    /// A dormant boss hit from outside its arena wakes into a revealed fight —
    /// bar, BossRevealed (objective, boss-intro dialogue, music) — with the
    /// intro beat skipped, and the intro is not recorded as seen. Stepping into
    /// the arena afterwards never runs the intro freeze on the fighting boss.
    /// </summary>
    [TestCase]
    public void ADormantBossHitFromOutsideItsArenaWakesIntoARevealWithoutTheIntroBeat() {
        Node host = CreateHost("LeashWakeHost");
        PlayerController player = null;
        BossData data = ScratchBoss("p1_wake_test_boss");
        try {
            var encounter = new BossEncounterController {
                Name = "WakeEncounter",
                Data = data,
                ArenaBounds = new Rect2(74000f, -31000f, 1200f, 2000f),
                AwardDustOnDefeat = false,
                Position = new Vector2(74600f, -30000f)
            };
            host.AddChild(encounter);
            int revealed = 0;
            encounter.BossRevealed += () => revealed++;
            BossController boss = encounter.Boss;
            AssertThat(boss.IsEngaged).IsFalse();

            boss.TakeDamage(5);
            AssertThat(boss.IsEngaged).IsTrue();
            AssertThat(encounter.IsRevealed).IsTrue();
            AssertThat(revealed).IsEqual(1);
            AssertThat(encounter.IsIntroBeatRunning).IsFalse();
            AssertThat(boss.IsStoryRewindFrozen).IsFalse();
            if (StoryManager.Instance != null) {
                AssertThat(StoryManager.Instance.HasSeenBossIntro(data.BossID)).IsFalse();
            }

            // The hero steps in mid-fight: no second reveal, no intro freeze.
            player = SpawnPlayer(host, new Vector2(74300f, -30000f));
            encounter._Process(Step);
            AssertThat(revealed).IsEqual(1);
            AssertThat(encounter.IsIntroBeatRunning).IsFalse();
            AssertThat(boss.IsStoryRewindFrozen).IsFalse();
        } finally {
            FreePlayer(player);
            ReleaseAndFree(host);
        }
    }

    /// <summary>
    /// The reveal never runs the intro freeze over a body that is already
    /// engaged, whatever engaged it: the ritual is an opening, not an interrupt.
    /// </summary>
    [TestCase]
    public void TheRevealSkipsTheIntroBeatForABossAlreadyFighting() {
        Node host = CreateHost("LeashEngagedRevealHost");
        PlayerController player = null;
        BossData data = ScratchBoss("p1_engaged_reveal_boss");
        try {
            var encounter = new BossEncounterController {
                Name = "EngagedRevealEncounter",
                Data = data,
                ArenaBounds = new Rect2(78000f, -31000f, 1200f, 2000f),
                AwardDustOnDefeat = false,
                Position = new Vector2(78600f, -30000f)
            };
            host.AddChild(encounter);
            int revealed = 0;
            encounter.BossRevealed += () => revealed++;
            encounter.Boss.IsEngaged = true;

            player = SpawnPlayer(host, new Vector2(78300f, -30000f));
            encounter._Process(Step);
            AssertThat(encounter.IsRevealed).IsTrue();
            AssertThat(revealed).IsEqual(1);
            AssertThat(encounter.IsIntroBeatRunning).IsFalse();
            AssertThat(encounter.Boss.IsStoryRewindFrozen).IsFalse();
        } finally {
            FreePlayer(player);
            ReleaseAndFree(host);
        }
    }

    /// <summary>
    /// BossController is IPoolable: a recycled body must not carry one
    /// encounter's dormancy or arena leash into the next.
    /// </summary>
    [TestCase]
    public void APoolCycleClearsTheDormancyAndTheLeash() {
        Node host = CreateHost("LeashPoolHost");
        try {
            BossController boss = CreateBoss(host, Duke());
            var arena = new Rect2(1000f, 0f, 500f, 1000f);

            boss.IsEngaged = false;
            boss.ArenaBounds = arena;
            boss.OnDespawn();
            AssertThat(boss.IsEngaged).IsTrue();
            AssertThat(boss.HasArenaLeash).IsFalse();

            boss.IsEngaged = false;
            boss.ArenaBounds = arena;
            boss.OnSpawn();
            AssertThat(boss.IsEngaged).IsTrue();
            AssertThat(boss.HasArenaLeash).IsFalse();
            AssertThat(boss.ArenaBounds).IsEqual(default(Rect2));
        } finally {
            ReleaseAndFree(host);
        }
    }

    [TestCase]
    public void AKnockbackOrTeleportPastTheEdgeIsClampedBackAndRecoveryDestinationsOutsideAreRefused() {
        Node host = CreateHost("LeashClampHost");
        try {
            BossController boss = CreateBoss(host, Duke());
            boss.ArenaBounds = new Rect2(1000f, 0f, 500f, 1000f);
            boss.GlobalPosition = new Vector2(1750f, 400f);
            boss.Velocity = new Vector2(300f, -20f);
            boss.ApplyArenaLeashForTest();
            AssertFloat(boss.GlobalPosition.X).IsEqualApprox(1500f, 0.001f);
            AssertFloat(boss.Velocity.X).IsEqualApprox(0f, 0.001f);

            boss.GlobalPosition = new Vector2(400f, 400f);
            boss.ApplyArenaLeashForTest();
            AssertFloat(boss.GlobalPosition.X).IsEqualApprox(1000f, 0.001f);

            // Inside: untouched.
            boss.GlobalPosition = new Vector2(1200f, 400f);
            boss.Velocity = new Vector2(-50f, 0f);
            boss.ApplyArenaLeashForTest();
            AssertFloat(boss.GlobalPosition.X).IsEqualApprox(1200f, 0.001f);
            AssertFloat(boss.Velocity.X).IsEqualApprox(-50f, 0.001f);

            AssertThat(boss.IsInsideArena(1250f)).IsTrue();
            AssertThat(boss.IsInsideArena(999f)).IsFalse();
            AssertThat(boss.IsInsideArena(1501f)).IsFalse();
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void AnArenaEncounterSpawnsDormantAndLeashedAndEngagesWhenThePlayerStepsIn() {
        Node host = CreateHost("LeashEncounterHost");
        PlayerController player = null;
        BossData data = ScratchBoss("p1_arena_test_boss");
        // Skip the name-card beat: this case is about the reveal, not the ritual.
        StoryManager.Instance?.RecordBossIntroSeen(data.BossID);
        try {
            var arena = new Rect2(64000f, -31000f, 1200f, 2000f);
            var encounter = new BossEncounterController {
                Name = "LeashEncounter",
                Data = data,
                RevealDistance = 5000f,
                ArenaBounds = arena,
                AwardDustOnDefeat = false,
                Position = new Vector2(64900f, -30000f)
            };
            host.AddChild(encounter);
            BossController boss = encounter.Boss;
            AssertObject(boss).IsNotNull();
            AssertThat(boss.IsEngaged).IsFalse();
            AssertThat(boss.ArenaBounds).IsEqual(arena);

            // Well inside the legacy radius but outside the arena: still dormant.
            player = SpawnPlayer(host, new Vector2(63900f, -30000f));
            AssertThat(encounter.IsRevealPoint(player.GlobalPosition)).IsFalse();
            encounter._Process(Step);
            AssertThat(encounter.IsRevealed).IsFalse();
            AssertThat(boss.IsEngaged).IsFalse();

            // One step into the arena: revealed and engaged.
            player.GlobalPosition = new Vector2(64010f, -30000f);
            AssertThat(encounter.IsRevealPoint(player.GlobalPosition)).IsTrue();
            encounter._Process(Step);
            AssertThat(encounter.IsRevealed).IsTrue();
            AssertThat(boss.IsEngaged).IsTrue();
        } finally {
            FreePlayer(player);
            host.Free();
        }
    }

    [TestCase]
    public void ARadiusEncounterStillWaitsForItsRevealBeforeTheBossEngages() {
        Node host = CreateHost("LeashRadiusHost");
        PlayerController player = null;
        BossData data = ScratchBoss("p1_radius_test_boss");
        StoryManager.Instance?.RecordBossIntroSeen(data.BossID);
        try {
            var encounter = new BossEncounterController {
                Name = "RadiusEncounter",
                Data = data,
                RevealDistance = 300f,
                AwardDustOnDefeat = false,
                Position = new Vector2(68000f, -30000f)
            };
            host.AddChild(encounter);
            AssertThat(encounter.HasArena).IsFalse();
            AssertThat(encounter.Boss.IsEngaged).IsFalse();
            AssertThat(encounter.Boss.HasArenaLeash).IsFalse();

            player = SpawnPlayer(host, new Vector2(67000f, -30000f));
            encounter._Process(Step);
            AssertThat(encounter.Boss.IsEngaged).IsFalse();

            player.GlobalPosition = new Vector2(67800f, -30000f);
            encounter._Process(Step);
            AssertThat(encounter.IsRevealed).IsTrue();
            AssertThat(encounter.Boss.IsEngaged).IsTrue();
        } finally {
            FreePlayer(player);
            host.Free();
        }
    }

    [TestCase]
    public void EverySquadMemberSharesTheArenaAndTheDormancy() {
        Node host = CreateHost("LeashSquadHost");
        try {
            BossData tribunal = AuthoredResources.Load<BossData>("res://resources/Bosses/revolutionary_tribunal.tres");
            var arena = new Rect2(70000f, -31000f, 1400f, 2000f);
            var encounter = new BossEncounterController {
                Name = "LeashSquadEncounter",
                Data = tribunal,
                ArenaBounds = arena,
                AwardDustOnDefeat = false,
                Position = new Vector2(70700f, -30000f)
            };
            host.AddChild(encounter);
            AssertThat(encounter.Members.Count).IsEqual(tribunal.SquadMemberCount);
            foreach (BossController member in encounter.Members) {
                AssertThat(member.IsEngaged).IsFalse();
                AssertThat(member.ArenaBounds).IsEqual(arena);
                AssertThat(member.IsInsideArena(member.GlobalPosition.X)).IsTrue();
            }
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void TheMirrorCloneIsClampedBackIntoItsArena() {
        var mirror = new MirrorParadoxController {
            Name = "LeashMirror",
            Data = AuthoredResources.Load<BossData>("res://resources/Bosses/mirror_paradox.tres"),
            CharacterIDOverride = "einstein",
            DecisionSeed = 4242,
            // Construction and the clamp only — never an AI-driven fight.
            ActivateOnSpawn = false,
            ArenaBounds = new Rect2(72000f, -31000f, 1200f, 2000f)
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(mirror);
        try {
            PlayerController clone = mirror.Clone;
            AssertObject(clone).IsNotNull();
            clone.GlobalPosition = new Vector2(71500f, -30000f);
            clone.Velocity = new Vector2(-300f, 0f);
            mirror.ApplyArenaLeashForTest();
            AssertFloat(clone.GlobalPosition.X).IsEqualApprox(72000f, 0.001f);
            AssertFloat(clone.Velocity.X).IsEqualApprox(0f, 0.001f);

            // Inside the arena the clone is left alone.
            clone.GlobalPosition = new Vector2(72600f, -30000f);
            clone.Velocity = new Vector2(-300f, 0f);
            mirror.ApplyArenaLeashForTest();
            AssertFloat(clone.GlobalPosition.X).IsEqualApprox(72600f, 0.001f);
            AssertFloat(clone.Velocity.X).IsEqualApprox(-300f, 0.001f);
        } finally {
            if (GodotObject.IsInstanceValid(mirror)) {
                mirror.GetParent()?.RemoveChild(mirror);
                mirror.Free();
            }
        }
    }

    /// <summary>
    /// A2 — the clamp runs after the clone moves. The controller used to tick
    /// first (a parent before its child at the same priority), so whatever moved
    /// the clone during its own step — MoveAndSlide, an Echo Step, a warp —
    /// left it outside the arena until the next frame, rendered and hit-tested
    /// there. Stepped through real physics frames: a default-priority node
    /// shoves the clone 300 px west of the arena every tick, and at the end of
    /// every frame (the process_frame after the physics step) the clone is
    /// back inside.
    /// </summary>
    [TestCase]
    public async Task TheMirrorCloneEndsEveryPhysicsFrameInsideItsArena() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var arena = new Rect2(86000f, -31000f, 1200f, 2000f);
        var mirror = new MirrorParadoxController {
            Name = "LeashMirrorStepped",
            Data = AuthoredResources.Load<BossData>("res://resources/Bosses/mirror_paradox.tres"),
            CharacterIDOverride = "einstein",
            DecisionSeed = 4243,
            ActivateOnSpawn = false,
            ArenaBounds = arena
        };
        tree.Root.AddChild(mirror);
        try {
            PlayerController clone = mirror.Clone;
            AssertObject(clone).IsNotNull();
            AssertThat(mirror.ProcessPhysicsPriority).IsGreater(clone.ProcessPhysicsPriority);
            clone.GlobalPosition = new Vector2(arena.Position.X + 100f, -30000f);
            var shove = new MirrorLeashShoveProbe {
                Name = "MirrorLeashShove",
                Target = clone,
                ShoveToX = arena.Position.X - 300f
            };
            // Under the clone: at the default priority it ticks after the
            // controller would have, exactly like the clone's own step.
            clone.AddChild(shove);

            List<float> endOfFrameX = await SampleAfterPhysicsAsync(clone, 4);

            AssertThat(shove.Shoves).IsGreaterEqual(4);
            foreach (float x in endOfFrameX) {
                AssertFloat(x).OverrideFailureMessage(
                        $"The clone ended a physics frame at x {x}, outside the arena (west edge {arena.Position.X}).")
                    .IsGreaterEqual(arena.Position.X - 0.01f);
            }
        } finally {
            if (GodotObject.IsInstanceValid(mirror)) {
                mirror.GetParent()?.RemoveChild(mirror);
                mirror.Free();
            }
        }
    }

    /// <summary>
    /// A5 — the arena ward (ARENA-LEASH-EDGE-ANSWER). An engaged, leashed boss
    /// whose hero stands more than one hero width outside its arena AND beyond
    /// its engagement range refuses every hit outright — no damage, knockback,
    /// status or hitstop, only the warded read — and every status applied
    /// without a hit (the Relativity Rift's linger), so the hero cannot plink a
    /// boss pinned at its leash edge from where it cannot answer. Inside the
    /// margin, inside the arena or back in, the hits land. The subject is a
    /// knockback-susceptible scratch boss (every authored campaign boss but the
    /// Tribunal is knockback-immune, which would make the shove assertion
    /// vacuous), stood mid-arena so its 240 px engagement range covers neither
    /// edge's margin.
    /// </summary>
    [TestCase]
    public void AnEngagedBossRefusesEveryHitWhileItsHeroStandsOutsideTheArenaBeyondItsReach() {
        Node host = CreateHost("LeashWardHost");
        PlayerController player = null;
        try {
            const float arenaLeft = 88000f;
            const float arenaWidth = 1200f;
            const float floorY = -30000f;
            BossController boss = CreateBoss(host, ShoveableScratchBoss("a5_ward_test_boss"));
            boss.ArenaBounds = new Rect2(arenaLeft, -40000f, arenaWidth, 20000f);
            boss.GlobalPosition = new Vector2(arenaLeft + arenaWidth / 2f, floorY);
            player = SpawnPlayer(host, new Vector2(arenaLeft - 900f, floorY));
            boss.SetTargetForTest(player);
            AssertThat(boss.IsEngaged).IsTrue();
            AssertThat(boss.IsArenaWarded).IsTrue();

            int hp = boss.CurrentHP;
            boss.Velocity = Vector2.Zero;
            AssertFloat(boss.TakeDamage(WardProbeHit(12f, StatusType.Venom))).IsEqual(0f);
            AssertThat(boss.CurrentHP).IsEqual(hp);
            AssertThat(boss.WardedHitsRefused).IsEqual(1);
            AssertThat(boss.DamageStatusType).IsEqual(StatusType.None);
            AssertThat(boss.HitstopFramesRemaining).IsEqual(0);
            AssertFloat(boss.Velocity.X).IsEqualApprox(0f, 0.001f);
            // A status applied without a hit is refused too.
            boss.ApplyStatusEffect(StatusType.TimeDilation, 2f, 1f);
            AssertThat(boss.ControlStatusType).IsEqual(StatusType.None);

            // Just outside, within one hero width of the edge: not warded.
            player.GlobalPosition = new Vector2(arenaLeft - BossController.ArenaWardMarginPixels + 5f, floorY);
            AssertThat(boss.IsArenaWarded).IsFalse();
            AssertFloat(boss.TakeDamage(WardProbeHit(10f))).IsEqual(10f);

            // Past the east edge's margin: warded again.
            player.GlobalPosition = new Vector2(arenaLeft + arenaWidth + BossController.ArenaWardMarginPixels + 5f, floorY);
            AssertThat(boss.IsArenaWarded).IsTrue();
            hp = boss.CurrentHP;
            AssertFloat(boss.TakeDamage(WardProbeHit(10f))).IsEqual(0f);
            AssertThat(boss.CurrentHP).IsEqual(hp);
            AssertThat(boss.WardedHitsRefused).IsEqual(2);

            // Back inside: every hit lands again, shove and status included —
            // which is what gives the refused shove above its teeth.
            player.GlobalPosition = new Vector2(arenaLeft + arenaWidth / 2f + 300f, floorY);
            AssertThat(boss.IsArenaWarded).IsFalse();
            boss.Velocity = Vector2.Zero;
            AssertFloat(boss.TakeDamage(WardProbeHit(10f))).IsEqual(10f);
            AssertFloat(Mathf.Abs(boss.Velocity.X)).IsGreater(1f);
            boss.ApplyStatusEffect(StatusType.TimeDilation, 2f, 1f);
            AssertThat(boss.ControlStatusType).IsEqual(StatusType.TimeDilation);
        } finally {
            FreePlayer(player);
            ReleaseAndFree(host);
        }
    }

    /// <summary>
    /// A5 — the ward never holds where the boss can answer. A boss pinned at its
    /// west edge (an 80 px pushbox) meets a hero whose centre is 58 px outside
    /// at pushbox contact, and the template melee reaches it from about 103 px
    /// out; the playtest bot swings from 92 px. An edge-relative margin alone
    /// warded all of that while the boss kept swinging back. Anywhere inside the
    /// boss's engagement range (the Duke's 9 units, 540 px) the hits land, and
    /// they still land mid-answer; only past that range, outside, is it warded.
    /// </summary>
    [TestCase]
    public async Task ABossPinnedAtItsEdgeTakesEveryHitFromAnOutsideHeroWithinItsReach() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = CreateHost("LeashWardReachHost");
            PlayerController player = null;
            try {
                const float arenaLeft = 92000f;
                const float floorY = -30000f;
                const float dukeRange = 540f;
                BuildFloor(host, arenaLeft, floorY);
                BossController boss = CreateBoss(host, Duke());
                boss.ArenaBounds = new Rect2(arenaLeft, -40000f, 1200f, 20000f);
                boss.GlobalPosition = new Vector2(arenaLeft, floorY);
                player = SpawnPlayer(host, new Vector2(arenaLeft - 90f, floorY));
                boss.SetTargetForTest(player);

                foreach (float outside in new[] { 58f, 90f, 103f, 300f, dukeRange - 20f }) {
                    player.GlobalPosition = new Vector2(arenaLeft - outside, floorY);
                    AssertThat(boss.IsArenaWarded)
                        .OverrideFailureMessage($"A hero {outside} px outside, within the boss's reach, was warded.")
                        .IsFalse();
                    int before = boss.CurrentHP;
                    AssertFloat(boss.TakeDamage(WardProbeHit(5f))).IsEqual(5f);
                    AssertThat(boss.CurrentHP).IsEqual(before - 5);
                }

                // Past the range, outside: warded.
                player.GlobalPosition = new Vector2(arenaLeft - dukeRange - 20f, floorY);
                AssertThat(boss.IsArenaWarded).IsTrue();
                AssertFloat(boss.TakeDamage(WardProbeHit(5f))).IsEqual(0f);
                AssertThat(boss.WardedHitsRefused).IsEqual(1);

                // At the bot's swing distance the pinned boss answers — and every
                // hit it takes while answering lands.
                player.GlobalPosition = new Vector2(arenaLeft - 90f, floorY);
                bool answered = false;
                for (int frame = 0; frame < 180 && !answered; frame++) {
                    boss._PhysicsProcess(Step);
                    AssertThat(boss.IsArenaWarded).IsFalse();
                    if (boss.SelectedAbility != null) answered = true;
                }
                AssertThat(answered)
                    .OverrideFailureMessage("A boss pinned at its leash must answer a hero at melee spacing.")
                    .IsTrue();
                int hp = boss.CurrentHP;
                AssertFloat(boss.TakeDamage(WardProbeHit(5f))).IsEqual(5f);
                AssertThat(boss.CurrentHP).IsEqual(hp - 5);
                AssertThat(boss.WardedHitsRefused).IsEqual(1);
            } finally {
                FreePlayer(player);
                ReleaseAndFree(host);
            }
        });
    }

    /// <summary>
    /// A5 — the ward covers damage over time as well as hits. Venom taken while
    /// the hero was in the arena keeps its clock, but its ticks deal nothing
    /// while the hero stands outside beyond the boss's reach (a finisher's
    /// Venom, then a step out, used to keep chipping 2 HP a second); back in,
    /// the ticks land again.
    /// </summary>
    [TestCase]
    public async Task AVenomTakenBeforeTheHeroStepsOutDealsNothingWhileTheBossIsWarded() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            Node host = CreateHost("LeashWardVenomHost");
            PlayerController player = null;
            try {
                const float arenaLeft = 94000f;
                const float floorY = -30000f;
                BuildFloor(host, arenaLeft, floorY);
                BossController boss = CreateBoss(host, ShoveableScratchBoss("a5_ward_venom_boss"));
                boss.ArenaBounds = new Rect2(arenaLeft, -40000f, 1200f, 20000f);
                boss.GlobalPosition = new Vector2(arenaLeft + 150f, floorY);
                player = SpawnPlayer(host, new Vector2(arenaLeft + 600f, floorY));
                boss.SetTargetForTest(player);
                AssertThat(boss.IsArenaWarded).IsFalse();
                boss.ApplyStatusEffect(StatusType.Venom, 10f, 1f);
                AssertThat(boss.DamageStatusType).IsEqual(StatusType.Venom);

                // Out past the boss's 240 px reach: 2.5 s of ticks deal nothing.
                player.GlobalPosition = new Vector2(arenaLeft - 900f, floorY);
                int hp = boss.CurrentHP;
                for (int frame = 0; frame < 150; frame++) {
                    boss._PhysicsProcess(Step);
                    AssertThat(boss.IsArenaWarded).IsTrue();
                }
                AssertThat(boss.CurrentHP).IsEqual(hp);
                AssertThat(boss.DamageStatusType).IsEqual(StatusType.Venom);

                // Back in the arena: the poison bites again.
                player.GlobalPosition = new Vector2(arenaLeft + 600f, floorY);
                for (int frame = 0; frame < 70; frame++) boss._PhysicsProcess(Step);
                AssertThat(boss.IsArenaWarded).IsFalse();
                AssertThat(boss.CurrentHP).IsLess(hp);
            } finally {
                FreePlayer(player);
                ReleaseAndFree(host);
            }
        });
    }

    /// <summary>
    /// A5 — what the ward is not. An unleashed boss is never warded; a dormant
    /// one is not either, so the hit that wakes it from outside still lands
    /// (ARENA-WAKE-REVEAL); scripted damage (<c>TakeDamage(int)</c>) is not a hit
    /// and ignores it; and a pool cycle leaves nothing of it behind.
    /// </summary>
    [TestCase]
    public void AnUnleashedOrDormantBossIsNeverWardedAndAPoolCycleClearsTheWard() {
        Node host = CreateHost("LeashWardScopeHost");
        PlayerController player = null;
        try {
            const float arenaLeft = 90000f;
            const float floorY = -30000f;
            var arena = new Rect2(arenaLeft, -40000f, 1200f, 20000f);
            BossController boss = CreateBoss(host, Duke());
            boss.GlobalPosition = new Vector2(arenaLeft + 150f, floorY);
            player = SpawnPlayer(host, new Vector2(arenaLeft - 900f, floorY));

            // Unleashed: never warded.
            boss.SetTargetForTest(player);
            AssertThat(boss.IsArenaWarded).IsFalse();

            // Leashed but dormant: the waking hit lands.
            boss.ArenaBounds = arena;
            boss.IsEngaged = false;
            AssertThat(boss.IsArenaWarded).IsFalse();
            AssertFloat(boss.TakeDamage(WardProbeHit(10f))).IsEqual(10f);

            // Engaged with the hero outside: hits are refused, scripted damage is not.
            boss.IsEngaged = true;
            boss.SetTargetForTest(player);
            AssertFloat(boss.TakeDamage(WardProbeHit(10f))).IsEqual(0f);
            AssertThat(boss.WardedHitsRefused).IsEqual(1);
            int hp = boss.CurrentHP;
            boss.TakeDamage(5);
            AssertThat(boss.CurrentHP).IsEqual(hp - 5);

            // A pool cycle: no leash, no target, no count.
            boss.OnDespawn();
            AssertThat(boss.IsArenaWarded).IsFalse();
            AssertThat(boss.WardedHitsRefused).IsEqual(0);
        } finally {
            FreePlayer(player);
            ReleaseAndFree(host);
        }
    }

    /// <summary>
    /// Florence's arena now has a ground-level doorway (its west wall used to seal
    /// it outright). A hero who is west of the arena when Phase 2's barricades
    /// close — hitting the boss from the doorway, or a rewind landing back in the
    /// workshop — is brought inside, never locked out with the boss.
    /// </summary>
    [TestCase]
    public void FlorencesClosingArenaBringsAHeroStandingOutsideItIn() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        bool originalPaused = tree.Paused;
        var level = new Level01Controller { Name = "FlorenceDoorwayFixture" };
        try {
            GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
            tree.Root.AddChild(level);
            PlayerController hero = null;
            Godot.Collections.Array<Node> children = level.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    if (child is PlayerController found) { hero = found; break; }
                }
            }
            AssertObject(hero).IsNotNull();
            AssertThat(level.BossEncounter.ArenaBounds).IsEqual(Level01Controller.BossArenaBounds);

            // In the workshop, west of the doorway, when the arena closes.
            hero.GlobalPosition = new Vector2(Level01Controller.BossArenaBounds.Position.X - 400f, 850f);
            level.BeginArenaShrink();
            level.AdvanceArenaShrink(Level01Controller.ArenaShrinkSeconds);

            AssertFloat(hero.GlobalPosition.X).IsGreaterEqual(level.ArenaInnerLeftX);
            AssertFloat(hero.GlobalPosition.X).IsLessEqual(level.ArenaInnerRightX);
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(level)) {
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            tree.Paused = originalPaused;
        }
    }

    /// <summary>
    /// A5 in Florence, on the real level. The antechamber runs from the doorway
    /// to the arena's west edge (180 px), and the boss-room checkpoint stands in
    /// it 80 px outside the edge. Against an engaged Borgia Inquisitor holding
    /// that edge no spot in it is warded — all of it is inside his 7-unit
    /// (420 px) engagement range — so a hero fighting him from the doorway or
    /// the checkpoint lands every hit. Only while he is still deep in his arena
    /// is a hero at the checkpoint warded, and the engaged boss closes to his
    /// edge on his own, which lifts it.
    /// </summary>
    [TestCase]
    public async Task FlorencesAntechamberIsNeverWardedAgainstABossHoldingItsEdge() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            bool originalPaused = tree.Paused;
            var level = new Level01Controller { Name = "FlorenceWardFixture" };
            try {
                GameManager.Instance.CurrentSession.ActiveSaveSlot = -1;
                tree.Root.AddChild(level);
                PlayerController hero = null;
                CheckpointTrigger bossRoomCheckpoint = null;
                Godot.Collections.Array<Node> children = level.GetChildren();
                using (children.AsDisposable()) {
                    foreach (Node child in children) {
                        if (child is PlayerController found) hero = found;
                        if (child is CheckpointTrigger checkpoint
                            && checkpoint.CheckpointID == Level01Controller.BossRoomCheckpointID) {
                            bossRoomCheckpoint = checkpoint;
                        }
                    }
                }
                AssertObject(hero).IsNotNull();
                AssertObject(bossRoomCheckpoint).IsNotNull();
                BossController boss = level.BossEncounter.Boss;
                AssertObject(boss).IsNotNull();
                float edge = Level01Controller.BossArenaBounds.Position.X;
                float checkpointX = bossRoomCheckpoint.GlobalPosition.X;
                AssertFloat(checkpointX).IsLess(edge - BossController.ArenaWardMarginPixels);
                boss.IsEngaged = true;
                boss.SetTargetForTest(hero);

                // The boss still at his spawn, deep in the arena: a hero at the
                // checkpoint is outside and beyond his reach — warded.
                hero.GlobalPosition = new Vector2(checkpointX, hero.GlobalPosition.Y);
                AssertThat(boss.IsArenaWarded).IsTrue();
                // He closes to his edge on his own, and the ward lifts.
                bool lifted = false;
                for (int frame = 0; frame < 240 && !lifted; frame++) {
                    boss._PhysicsProcess(Step);
                    lifted = !boss.IsArenaWarded;
                }
                AssertThat(lifted)
                    .OverrideFailureMessage("An engaged boss must close on a hero at the boss-room checkpoint.")
                    .IsTrue();
                AssertFloat(boss.GlobalPosition.X).IsGreaterEqual(edge - 0.01f);

                // Holding his edge: nowhere from the doorway's inner face to the
                // edge is warded, and the checkpoint's hits land.
                boss.GlobalPosition = new Vector2(edge, boss.GlobalPosition.Y);
                for (float x = level.ArenaInnerLeftX; x < edge; x += 20f) {
                    hero.GlobalPosition = new Vector2(x, hero.GlobalPosition.Y);
                    AssertThat(boss.IsArenaWarded)
                        .OverrideFailureMessage($"A hero in Florence's antechamber at x {x} was warded.")
                        .IsFalse();
                }
                hero.GlobalPosition = new Vector2(checkpointX, hero.GlobalPosition.Y);
                int hp = boss.CurrentHP;
                AssertFloat(boss.TakeDamage(WardProbeHit(5f))).IsEqual(5f);
                AssertThat(boss.CurrentHP).IsEqual(hp - 5);
                AssertThat(boss.WardedHitsRefused).IsEqual(0);
            } finally {
                PoolManager.Instance?.ReleaseActiveUnder(level);
                PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
                if (GodotObject.IsInstanceValid(level)) {
                    level.GetParent()?.RemoveChild(level);
                    level.Free();
                }
                GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
                tree.Paused = originalPaused;
            }
        });
    }

    // === Helpers ==========================================================

    private static BossData Duke() =>
        AuthoredResources.Load<BossData>("res://resources/Bosses/siegemaster_duke.tres");

    /// <summary>A player basic that would knock back and (optionally) apply a status.</summary>
    private static HitPayload WardProbeHit(float damage, StatusType status = StatusType.None) => new() {
        AttackerIndex = 0,
        AttackID = "einstein.basic",
        HitboxID = "combo_1",
        AttackClass = AttackClass.Basic,
        Damage = damage,
        Knockback = new Vector2(4f, -2f),
        HitstunDuration = 0.4f,
        HitOrigin = Vector2.Zero,
        AttackerFacingRight = true,
        AppliedStatus = status,
        StatusDuration = status == StatusType.None ? 0f : 3f,
        StatusIntensity = 1f
    };

    /// <summary>
    /// <paramref name="body"/>'s X at the end of each of the next
    /// <paramref name="frames"/> frames that ran a physics step, read on the
    /// process_frame emission (after every physics callback and the physics
    /// server's step, before the frame renders).
    /// </summary>
    private static Task<List<float>> SampleAfterPhysicsAsync(Node2D body, int frames) {
        var tree = (SceneTree)Engine.GetMainLoop();
        var completion = new TaskCompletionSource<List<float>>();
        var samples = new List<float>();
        int physicsSteps = 0;
        Action onPhysics = () => physicsSteps++;
        Action onProcess = null;
        onProcess = () => {
            if (physicsSteps == 0) return;
            physicsSteps = 0;
            if (!GodotObject.IsInstanceValid(body)) {
                tree.PhysicsFrame -= onPhysics;
                tree.ProcessFrame -= onProcess;
                completion.TrySetException(new InvalidOperationException("The sampled body was freed."));
                return;
            }
            samples.Add(body.GlobalPosition.X);
            if (samples.Count < frames) return;
            tree.PhysicsFrame -= onPhysics;
            tree.ProcessFrame -= onProcess;
            completion.TrySetResult(samples);
        };
        tree.PhysicsFrame += onPhysics;
        tree.ProcessFrame += onProcess;
        return completion.Task;
    }

    /// <summary>A code-built single-phase boss with no kit, so a reveal touches no authored seen-set.</summary>
    private static BossData ScratchBoss(string id) => new() {
        BossID = id,
        DisplayNameKey = "boss",
        MaxHP = 100,
        MoveSpeed = 4f,
        MeleeRangeThreshold = 2f,
        RangedRangeThreshold = 4f,
        ChronalDustDrop = 0
    };

    /// <summary>
    /// <see cref="ScratchBoss"/> that knockback moves (BossData defaults to
    /// knockback-immune): 2-unit melee and 4-unit ranged bands, so a 240 px
    /// engagement range.
    /// </summary>
    private static BossData ShoveableScratchBoss(string id) {
        BossData data = ScratchBoss(id);
        data.IsKnockbackImmune = false;
        return data;
    }

    private static EnemyAbilityData KitAbility(string id, EnemyAbilityArchetype archetype) => new() {
        AbilityID = id,
        Archetype = archetype,
        RangeClass = EnemyAbilityRangeClass.Any,
        SelectionWeight = 1f,
        TelegraphFrames = 6,
        ActiveFrames = 4,
        RecoveryFrames = 6,
        Damage = 10f,
        HitboxSize = new Vector2(60f, 60f),
        HitboxOffset = new Vector2(40f, -40f)
    };

    /// <summary>An Environment floor whose top surface is at <paramref name="topY"/>, centred on <paramref name="centerX"/>.</summary>
    private static void BuildFloor(Node host, float centerX, float topY) {
        var floor = new StaticBody2D {
            Name = "LeashFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = new Vector2(centerX, topY)
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
    }

    private static int CountEnemiesUnder(Node host) {
        int count = 0;
        Godot.Collections.Array<Node> children = host.GetChildren();
        using (children.AsDisposable()) {
            foreach (Node child in children) {
                if (child is EnemyController) count++;
            }
        }
        return count;
    }

    private static Node CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static BossController CreateBoss(Node host, BossData data) {
        var boss = ResourceLoader.Load<PackedScene>(BossScenePath).Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        host.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    private static PlayerController SpawnPlayer(Node host, Vector2 position) {
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0);
        host.AddChild(player);
        player.GlobalPosition = position;
        return player;
    }

    /// <summary>Hands pooled shots back before the host that parents them is freed.</summary>
    private static void ReleaseAndFree(Node host) {
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        PoolManager.Instance?.ReleaseActiveUnder(host);
        host.Free();
    }

    /// <summary>A grouped StoryPlayer must never outlive its case (AGENTS.md).</summary>
    private static void FreePlayer(PlayerController player) {
        if (player == null || !GodotObject.IsInstanceValid(player)) return;
        player.GetParent()?.RemoveChild(player);
        player.Free();
    }
}
