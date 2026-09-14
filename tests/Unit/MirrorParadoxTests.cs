using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 4 Section 3.5: the Level 13 Mirror Paradox. The clone must mirror the
/// locked character from normalized base resources only, carry the authored
/// 1000-HP pool, never gain a phase transition, and reach its decisions through
/// the same Hard Fighter CPU table the deterministic simulation uses.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class MirrorParadoxTests {
    private const string MirrorResourcePath = "res://resources/Bosses/mirror_paradox.tres";
    private const string MirroredCharacter = "einstein";

    // === Resource contract ===

    [TestCase]
    public void AuthoredResourceIsASinglePhaseThousandHPBossWithNoAttackPatterns() {
        BossData data = FTT.Core.AuthoredResources.Load<BossData>(MirrorResourcePath);
        AssertObject(data).IsNotNull();
        AssertString(data.BossID).IsEqual("mirror_paradox");
        AssertString(data.DisplayNameKey).IsEqual("boss_mirror_paradox_name");
        AssertThat(data.MaxHP).IsEqual(1000);
        AssertThat(data.ChronalDustDrop).IsEqual(50);

        // No BossData attack-pattern system and no phase transitions: the mirror
        // runs the CPU utility engine instead (design-godot.md Section 6, Level 13).
        AssertThat(data.PhaseThresholds.Length).IsEqual(0);
        AssertThat(data.PhaseCount).IsEqual(1);
        AssertThat(data.BossAbilities == null || data.BossAbilities.Length == 0).IsTrue();
    }

    [TestCase]
    public void AuthoredReactionWindowMatchesTheHardFighterCpuBoundsExactly() {
        BossData data = FTT.Core.AuthoredResources.Load<BossData>(MirrorResourcePath);
        var hardCpu = new FighterCpuController(FTT.Core.CpuDifficulty.Hard, 1);
        int engineMinimum = hardCpu.GetReactionDelayBounds(out int engineMaximum);

        AssertThat(data.ReactionDelayMinFrames).IsEqual(4);
        AssertThat(data.ReactionDelayMaxFrames).IsEqual(8);
        // Parity: the resource must never drift away from the shared engine bounds.
        AssertThat(data.ReactionDelayMinFrames).IsEqual(engineMinimum);
        AssertThat(data.ReactionDelayMaxFrames).IsEqual(engineMaximum);
        AssertThat(FighterCpuController.HardReactionDelayMinFrames).IsEqual(4);
        AssertThat(FighterCpuController.HardReactionDelayMaxFrames).IsEqual(8);
    }

    [TestCase]
    public void DisplayNameKeyResolvesThroughTheEnglishTranslation() {
        TranslationServer.SetLocale("en");
        AssertString(TranslationServer.Translate("boss_mirror_paradox_name")).IsEqual("The Mirror Paradox");
    }

    // === Clone construction ===

    [TestCase]
    public void CloneMirrorsTheLockedCharactersCanonicalResource() {
        MirrorParadoxController mirror = CreateMirror();
        try {
            AssertString(mirror.MirroredCharacterID).IsEqual(MirroredCharacter);
            AssertObject(mirror.Clone).IsNotNull();
            AssertObject(mirror.Clone.Data).IsNotNull();
            AssertString(mirror.Clone.Data.CharacterID).IsEqual(MirroredCharacter);

            CharacterData canonical = FTT.Core.AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{MirroredCharacter}_data.tres");
            AssertString(mirror.Clone.Data.CharacterID).IsEqual(canonical.CharacterID);
            AssertObject(mirror.Clone.Data.SpecialAttackOne).IsEqual(canonical.SpecialAttackOne);
            AssertObject(mirror.Clone.Data.SpecialAttackTwo).IsEqual(canonical.SpecialAttackTwo);
            AssertObject(mirror.Clone.Data.MovementAbility).IsEqual(canonical.MovementAbility);
            AssertObject(mirror.Clone.Data.UltimateAttack).IsEqual(canonical.UltimateAttack);

            // Exact character model: the same four executable ability slots the
            // player fights with, plus the basic string, are present on the clone.
            AssertObject(mirror.Clone.GetNodeOrNull("Special1")).IsNotNull();
            AssertObject(mirror.Clone.GetNodeOrNull("Special2")).IsNotNull();
            AssertObject(mirror.Clone.GetNodeOrNull("MovementAbility")).IsNotNull();
            AssertObject(mirror.Clone.GetNodeOrNull("Ultimate")).IsNotNull();
            AssertObject(mirror.Clone.GetNodeOrNull("MeleeHitbox")).IsNotNull();
        } finally {
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void ClonePoolIsTheAuthoredThousandHPBossPoolNotTheCharacterBaseline() {
        MirrorParadoxController mirror = CreateMirror();
        try {
            CharacterData canonical = FTT.Core.AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{MirroredCharacter}_data.tres");
            AssertThat(canonical.MaxHP < 1000).IsTrue();

            AssertThat(mirror.ScaledMaxHP).IsEqual(1000);
            AssertThat(mirror.Clone.MaximumHP).IsEqual(1000);
            AssertThat(mirror.Clone.CurrentHP).IsEqual(1000);
            // The pool override is not a Resonance bonus.
            AssertThat(mirror.Clone.StoryMaxHPBonus).IsEqual(0);
        } finally {
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void CloneOccupiesTheEnemyCollisionSlotAndLeavesTheStoryPlayerGroup() {
        MirrorParadoxController mirror = CreateMirror();
        try {
            AssertThat(mirror.Clone.PlayerIndex).IsEqual(MirrorParadoxController.DefaultMirrorPlayerIndex);
            AssertThat(mirror.Clone.CollisionLayer).IsEqual(CollisionLayers.Enemy);
            AssertThat(mirror.Clone.IsInGroup("StoryPlayer")).IsFalse();
            AssertThat(mirror.Clone.IsInGroup(MirrorParadoxController.MirrorGroup)).IsTrue();
        } finally {
            FreeMirror(mirror);
        }
    }

    // === Story-progression isolation ===

    [TestCase]
    public void CloneNeverInheritsStoryResonancePerksOnEasyOrNormalEvenWithAPopulatedSave() {
        // Package 11 A7b (F20): perk mirroring is a HARD-only rule, so this
        // isolation case pins Normal explicitly rather than relying on the
        // session default. The Hard half lives in the F20 matrix below.
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];

        GameManager.Instance.CurrentSession.ActiveSaveSlot = 0;
        GameManager.Instance.CurrentSession.SelectedCharacterID = MirroredCharacter;
        SaveManager.Instance.SaveSlots[0] = new StorySaveData {
            SelectedCharacterID = MirroredCharacter,
            DepositedChronalDust = new Dictionary<string, int>(),
            GridProgress = new Dictionary<string, List<string>> {
                [MirroredCharacter] = new() {
                    // Package 11 A4: the V7.6 Einstein mesh node IDs.
                    "einstein_momentum", "einstein_mass", "einstein_chalk_edge",
                    "einstein_rift_range", "einstein_event_horizon"
                }
            }
        };

        PlayerController progressed = null;
        MirrorParadoxController mirror = null;
        try {
            // Control: the campaign avatar DOES pick the perks up, so the active
            // save really is populated and the isolation below is meaningful.
            progressed = CharacterFactory.CreateCharacter(MirroredCharacter, 0, applyStoryProgression: true);
            AssertThat(progressed.StoryAbilityPerks.Count > 0).IsTrue();

            mirror = CreateMirror();
            AssertThat(mirror.Clone.StoryAbilityPerks.Count).IsEqual(0);
            AssertThat(mirror.Clone.HasStoryPerk("event_horizon")).IsFalse();
            // Nor any Resonance stat profile: every multiplier stays neutral.
            AssertThat(mirror.Clone.StoryMaxHPBonus).IsEqual(0);
            AssertThat(mirror.Clone.StoryBlockChargeBonus).IsEqual(0);
            AssertThat(mirror.Clone.StoryMoveSpeedMultiplier).IsEqualApprox(1f, 0.0001f);
            AssertThat(mirror.Clone.StoryBasicDamageMultiplier).IsEqualApprox(1f, 0.0001f);
            AssertThat(mirror.Clone.StorySpecialDamageMultiplier).IsEqualApprox(1f, 0.0001f);
            AssertThat(mirror.Clone.StoryCooldownMultiplier).IsEqualApprox(1f, 0.0001f);
        } finally {
            if (mirror != null) FreeMirror(mirror);
            DetachAndFree(progressed);
            SaveManager.Instance.SaveSlots[0] = originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
        }
    }

    // === Decision engine reuse ===

    [TestCase]
    public void DecisionsComeFromTheSharedFighterCpuTableAtTheStoryTierAndNothingElse() {
        // Package 11 A7b (F20): the tier is now the Story difficulty, and the band
        // is the BOSS override rather than the practice band. The parity proof is
        // unchanged in kind - a bare FighterCpuController built the same way, fed
        // the adapter's own observation each tick, must emit byte-identical frames.
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        GameManager.Instance.CurrentSession.Difficulty = Difficulty.Hard;
        MirrorParadoxController mirror = CreateMirror(seed: 20260807);
        try {
            MirrorParadoxDecisionAdapter adapter = mirror.Decisions;
            AssertObject(adapter).IsNotNull();
            AssertThat(adapter.Difficulty).IsEqual(FTT.Core.CpuDifficulty.Hard);
            AssertThat(adapter.ReactionDelayMinFrames).IsEqual(4);
            AssertThat(adapter.ReactionDelayMaxFrames).IsEqual(8);

            var reference = new FighterCpuController(
                FTT.Core.CpuDifficulty.Hard, 20260807, null, null,
                CpuBandTuning.BossOverride(FTT.Core.CpuDifficulty.Hard));
            PlayerInputFrame adapterPrevious = default;
            PlayerInputFrame referencePrevious = default;
            for (uint tick = 0; tick < 240; tick++) {
                CpuDecisionObservation observation = adapter.Observe();
                PlayerInputFrame adapterFrame = adapter.Sample(tick, in adapterPrevious);
                PlayerInputFrame referenceFrame = reference.Sample(tick, in observation, in referencePrevious);
                AssertThat(adapterFrame.Equals(referenceFrame)).IsTrue();
                adapterPrevious = adapterFrame;
                referencePrevious = referenceFrame;
            }
        } finally {
            FreeMirror(mirror);
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
        }
    }

    [TestCase]
    public void TheDecisionObservationProjectionIsLosslessForTheDeterministicPath() {
        // Extraction guard: Observe() must copy the deterministic components
        // verbatim so the Fighter path's behaviour is unchanged by the refactor.
        var self = new FighterStateComponent {
            Stocks = 3, HitstunFrames = 2, DazeFrames = 1, IsGrounded = 1,
            RemainingJumps = 2, CurrentHP = 64, MaxHP = 100,
            Influence = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(60),
            Position = new xpTURN.Klotho.Deterministic.Math.FPVector2(
                xpTURN.Klotho.Deterministic.Math.FP64.FromInt(3),
                xpTURN.Klotho.Deterministic.Math.FP64.FromDouble(2.4)),
            Velocity = new xpTURN.Klotho.Deterministic.Math.FPVector2(
                xpTURN.Klotho.Deterministic.Math.FP64.FromInt(-2),
                xpTURN.Klotho.Deterministic.Math.FP64.FromInt(-7))
        };
        var target = new FighterStateComponent {
            CurrentHP = 12, MaxHP = 100, HitstunFrames = 9,
            Influence = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(88),
            Position = new xpTURN.Klotho.Deterministic.Math.FPVector2(
                xpTURN.Klotho.Deterministic.Math.FP64.FromInt(-4),
                xpTURN.Klotho.Deterministic.Math.FP64.Zero)
        };
        var selfRuntime = new FighterRuntimeComponent {
            SpecialOneCooldownFrames = 11, SpecialTwoCooldownFrames = 22, MovementCooldownFrames = 33
        };
        var targetRuntime = new FighterRuntimeComponent { PressedButtons = (int)GameplayButtons.Special1 };

        CpuDecisionObservation observation =
            FighterCpuController.Observe(in self, in selfRuntime, in target, in targetRuntime);

        AssertThat(observation.SelfPositionXRaw).IsEqual(self.Position.x.RawValue);
        AssertThat(observation.SelfPositionYRaw).IsEqual(self.Position.y.RawValue);
        AssertThat(observation.SelfVelocityXRaw).IsEqual(self.Velocity.x.RawValue);
        AssertThat(observation.SelfVelocityYRaw).IsEqual(self.Velocity.y.RawValue);
        AssertThat(observation.TargetPositionXRaw).IsEqual(target.Position.x.RawValue);
        AssertThat(observation.TargetPositionYRaw).IsEqual(target.Position.y.RawValue);
        AssertThat(observation.Stocks).IsEqual(3);
        AssertThat(observation.HitstunFrames).IsEqual(2);
        AssertThat(observation.DazeFrames).IsEqual(1);
        AssertThat(observation.IsGrounded).IsEqual(1);
        AssertThat(observation.RemainingJumps).IsEqual(2);
        AssertThat(observation.SelfCurrentHP).IsEqual(64);
        AssertThat(observation.SelfMaxHP).IsEqual(100);
        AssertThat(observation.InfluenceRaw).IsEqual(self.Influence.RawValue);
        AssertThat(observation.SpecialOneCooldownFrames).IsEqual(11);
        AssertThat(observation.SpecialTwoCooldownFrames).IsEqual(22);
        AssertThat(observation.MovementCooldownFrames).IsEqual(33);
        AssertThat(observation.TargetCurrentHP).IsEqual(12);
        AssertThat(observation.TargetMaxHP).IsEqual(100);
        AssertThat(observation.TargetHitstunFrames).IsEqual(9);
        AssertThat(observation.TargetPressedButtons).IsEqual((int)GameplayButtons.Special1);
        AssertThat(observation.TargetInfluenceRaw).IsEqual(target.Influence.RawValue);
        // No geometry and no world observer on this overload: every optional block
        // must read as explicitly absent rather than as a zeroed real value.
        AssertThat(observation.HasStageBounds).IsEqual(0);
        AssertThat(observation.HasOrb).IsEqual(0);
        AssertThat(observation.HasHazard).IsEqual(0);
        AssertThat(observation.HasHostileProjectile).IsEqual(0);
        AssertThat(observation.SuppressGameplayInput).IsEqual(0);
    }

    [TestCase]
    public void TheStoryAdapterProjectsYUpwardAndDeclaresTheFighterOnlyBlocksAbsent() {
        // Package 6 §2.5: both Observe paths carry the same fields. Story has no
        // stage bounds, no Chronal Orbs, and no Fighter stage hazards, so it must
        // say so explicitly — those flags are what stop the shared table's
        // off-stage recovery, orb pursuit, and hazard evasion firing in a level
        // whose floor is nowhere near world y = 0.
        MirrorParadoxController mirror = CreateMirror();
        try {
            mirror.Clone.GlobalPosition = new Vector2(600f, 300f);
            CpuDecisionObservation observation = mirror.Decisions.Observe();

            AssertThat(observation.HasStageBounds).IsEqual(0);
            AssertThat(observation.HasOrb).IsEqual(0);
            AssertThat(observation.HasHazard).IsEqual(0);
            AssertThat(observation.HasHostileProjectile).IsEqual(0);
            AssertThat(observation.SuppressGameplayInput).IsEqual(0);
            AssertThat(observation.PlatformCount).IsEqual(0);

            // Godot screen space is +Y down; the decision table is +Y up.
            AssertThat(observation.SelfPositionXRaw > 0).IsTrue();
            AssertThat(observation.SelfPositionYRaw < 0).IsTrue();
            AssertThat(observation.SelfMaxHP).IsEqual(mirror.ScaledMaxHP);
            AssertThat(observation.SelfCurrentHP).IsEqual(mirror.CurrentHP);
        } finally {
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void TheStoryAdapterProjectsTheNearestHostileProjectileAndTheOpponentMeter() {
        // M-8 parity: the adapter must fill the same projectile and opponent-meter
        // fields the Fighter world observer fills, projected from Story state —
        // pixels to world units, +Y flipped, X sign carrying the travel direction.
        MirrorParadoxController mirror = CreateMirror();
        PlayerController target = null;
        FTT.Combat.PlaceholderProjectile projectile = null;
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        try {
            mirror.Clone.GlobalPosition = new Vector2(600f, 300f);

            target = CharacterFactory.CreateCharacter(
                MirroredCharacter, 0, applyStoryProgression: false);
            root.AddChild(target);
            target.GlobalPosition = new Vector2(300f, 300f);
            target.CurrentUltimateMeter = 42f;
            mirror.Decisions.Bind(mirror.Clone, target);

            CpuDecisionObservation before = mirror.Decisions.Observe();
            AssertThat(before.TargetInfluenceRaw).IsEqual(
                xpTURN.Klotho.Deterministic.Math.FP64.FromFloat(42f).RawValue);
            AssertThat(before.HasHostileProjectile).IsEqual(0);

            // A player-owned shot 120 px left of and 60 px above the clone,
            // travelling right toward it.
            projectile = new FTT.Combat.PlaceholderProjectile { Name = "HostileShotUnderTest" };
            root.AddChild(projectile);
            projectile.Setup(0f, Vector2.Zero, 300f, movingRight: true,
                ownerIndex: 0, color: Colors.White, lifetime: 999f);
            projectile.GlobalPosition = new Vector2(480f, 240f);

            CpuDecisionObservation observation = mirror.Decisions.Observe();
            AssertThat(observation.HasHostileProjectile).IsEqual(1);
            // -120 px => -2 world units; -60 px (screen up) => +1 unit, Y flipped.
            AssertThat(observation.ProjectileRelativeXRaw).IsEqual(
                xpTURN.Klotho.Deterministic.Math.FP64.FromFloat(-2f).RawValue);
            AssertThat(observation.ProjectileRelativeYRaw).IsEqual(
                xpTURN.Klotho.Deterministic.Math.FP64.FromFloat(1f).RawValue);
            // Moving right at 300 px/s => +5 units/s toward the clone, level flight.
            AssertThat(observation.ProjectileVelocityXRaw).IsEqual(
                xpTURN.Klotho.Deterministic.Math.FP64.FromFloat(5f).RawValue);
            AssertThat(observation.ProjectileVelocityYRaw).IsEqual(0L);

            // The clone's own shots are never hostile to it.
            projectile.Setup(0f, Vector2.Zero, 300f, movingRight: true,
                ownerIndex: mirror.Clone.PlayerIndex, color: Colors.White, lifetime: 999f);
            AssertThat(mirror.Decisions.Observe().HasHostileProjectile).IsEqual(0);
        } finally {
            DetachAndFree(projectile);
            DetachAndFree(target);
            FreeMirror(mirror);
        }
    }

    // === Defeat flow ===

    [TestCase]
    public void DefeatRaisesTheBossDefeatedEventOnceWithFiftyDust() {
        MirrorParadoxController mirror = CreateMirror();
        int defeats = 0;
        BossDefeatedPayload captured = default;
        void OnDefeated(BossDefeatedPayload payload) { defeats++; captured = payload; }
        EventBus.Instance.OnBossDefeated += OnDefeated;
        try {
            mirror.Clone.ApplyDamage(400);
            AssertThat(mirror.CurrentHP).IsEqual(600);
            AssertThat(defeats).IsEqual(0);

            mirror.Clone.ApplyDamage(600);
            AssertThat(mirror.IsDefeated).IsTrue();
            AssertThat(defeats).IsEqual(1);
            AssertString(captured.BossID).IsEqual("mirror_paradox");
            AssertThat(captured.ChronalDustDrop).IsEqual(50);

            // Idempotent: further damage cannot re-award the drop.
            mirror.Clone.ApplyDamage(100);
            AssertThat(defeats).IsEqual(1);
        } finally {
            EventBus.Instance.OnBossDefeated -= OnDefeated;
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void HPChangesRepublishAsBossHPEventsAgainstTheThousandHPPool() {
        MirrorParadoxController mirror = CreateMirror();
        var samples = new List<BossHPPayload>();
        void OnHP(BossHPPayload payload) {
            if (payload.BossID == "mirror_paradox") samples.Add(payload);
        }
        EventBus.Instance.OnBossHPChanged += OnHP;
        try {
            samples.Clear();
            mirror.Clone.ApplyDamage(250);
            AssertThat(samples.Count > 0).IsTrue();
            BossHPPayload last = samples[samples.Count - 1];
            AssertThat(last.CurrentHP).IsEqual(750);
            AssertThat(last.MaxHP).IsEqual(1000);
        } finally {
            EventBus.Instance.OnBossHPChanged -= OnHP;
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void TheEncounterWrapperRevealsTheBarAndAwardsDustOnDefeat() {
        var encounter = new MirrorParadoxEncounterController {
            CharacterIDOverride = MirroredCharacter,
            RevealDistance = 0f,
            DecisionSeed = 77
        };
        int dustAwarded = 0;
        BossDefeatedPayload captured = default;
        void OnDust(int amount) => dustAwarded += amount;
        void OnEncounterDefeat(BossDefeatedPayload payload) => captured = payload;
        EventBus.Instance.OnChronalDustCollected += OnDust;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(encounter);
        encounter.MirrorDefeated += OnEncounterDefeat;
        try {
            AssertObject(encounter.Data).IsNotNull();
            AssertObject(encounter.Mirror).IsNotNull();
            AssertThat(encounter.Mirror.ScaledMaxHP).IsEqual(1000);

            encounter.Reveal();
            AssertThat(encounter.IsRevealed).IsTrue();

            encounter.Mirror.Clone.ApplyDamage(1000);
            AssertThat(encounter.IsDefeated).IsTrue();
            AssertThat(dustAwarded).IsEqual(50);
            AssertString(captured.BossID).IsEqual("mirror_paradox");
        } finally {
            EventBus.Instance.OnChronalDustCollected -= OnDust;
            DetachAndFree(encounter);
        }
    }

    [TestCase]
    public void RewindFreezeStopsTheCloneLikeAnyOtherStoryEnemy() {
        MirrorParadoxController mirror = CreateMirror();
        try {
            mirror.BeginEncounter();
            AssertThat(mirror.IsEncounterActive).IsTrue();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsTrue();

            mirror.Clone.Velocity = new Vector2(300f, 0f);
            mirror.SetStoryRewindFrozen(true);
            AssertThat(mirror.IsStoryRewindFrozen).IsTrue();
            AssertThat(mirror.Clone.Velocity).IsEqual(Vector2.Zero);
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsFalse();

            mirror.SetStoryRewindFrozen(false);
            AssertThat(mirror.IsStoryRewindFrozen).IsFalse();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsTrue();
        } finally {
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void AnUnrevealedMirrorStandsInertAndNeverConsumesTheInputSlot() {
        MirrorParadoxController mirror = CreateMirror();
        try {
            // Spawned but not revealed: no simulation, no CPU input source. A boss
            // must not be fighting before the encounter starts.
            AssertThat(mirror.IsEncounterActive).IsFalse();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsFalse();

            mirror.BeginEncounter();
            AssertThat(mirror.IsEncounterActive).IsTrue();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsTrue();

            // Idempotent, and defeat stands the clone back down.
            mirror.BeginEncounter();
            AssertThat(mirror.IsEncounterActive).IsTrue();
            mirror.Clone.ApplyDamage(mirror.ScaledMaxHP);
            AssertThat(mirror.IsDefeated).IsTrue();
            AssertThat(mirror.IsEncounterActive).IsFalse();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsFalse();
        } finally {
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void DespawnDetachesTheCloneAndReleasesTheInputSlot() {
        MirrorParadoxController mirror = CreateMirror();
        try {
            mirror.BeginEncounter();
            PlayerController clone = mirror.Clone;
            AssertObject(clone).IsNotNull();

            mirror.DespawnMirror();
            AssertObject(mirror.Clone).IsNull();
            AssertThat(mirror.IsEncounterActive).IsFalse();
            // Detached from the tree and fully disposed, not left as an orphan.
            AssertThat(GodotObject.IsInstanceValid(clone)).IsFalse();
        } finally {
            FreeMirror(mirror);
        }
    }

    // === Rewind world-freeze wiring (audit H-8) ===

    [TestCase]
    public void MirrorFiredProjectilesJoinTheEnemyProjectileGroup() {
        MirrorParadoxController mirror = CreateMirror();
        var projectile = new FTT.Combat.PlaceholderProjectile { Name = "MirrorShotUnderTest" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(projectile);
        try {
            // The controller marks its clone hostile at spawn...
            AssertThat(mirror.Clone.IsStoryHostile).IsTrue();

            // ...so a shot fired with the clone's non-negative player index still
            // groups as an enemy projectile and a rewind's world clear removes it.
            projectile.Setup(10f, new Vector2(3f, -2f), 400f, movingRight: true,
                ownerIndex: mirror.Clone.PlayerIndex, color: Colors.White,
                sourcePlayer: mirror.Clone);
            AssertThat(projectile.IsInGroup("enemy_projectile")).IsTrue();

            // Control: the campaign avatar's shots keep player-projectile
            // semantics — owner 0 with no hostile source never joins the group.
            projectile.Setup(10f, new Vector2(3f, -2f), 400f, movingRight: true,
                ownerIndex: 0, color: Colors.White);
            AssertThat(projectile.IsInGroup("enemy_projectile")).IsFalse();
        } finally {
            DetachAndFree(projectile);
            FreeMirror(mirror);
        }
    }

    [TestCase]
    public void ARealRewindFreezesTheMirrorAndClearsItsShots() {
        // The wiring, not the mechanism: a rewind through the real
        // ChronalRewindManager must reach the mirror's SetStoryRewindFrozen via
        // the world-freeze group sweep and clear the clone's live shots — the
        // audit found the mechanism tested but nothing calling it (H-8).
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "MirrorRewindWiringHost" };
        tree.Root.AddChild(host);
        MirrorParadoxController mirror = null;
        try {
            StoryManager.Instance?.SetRewinds(3);
            PlayerController player = CharacterFactory.CreateCharacter(MirroredCharacter, 0);
            host.AddChild(player);
            player.GlobalPosition = new Vector2(400f, 850f);
            var manager = new ChronalRewindManager { Name = "MirrorRewindManager" };
            host.AddChild(manager);
            for (int frame = 0; frame < 10; frame++) manager._PhysicsProcess(1.0 / 60.0);

            mirror = CreateMirror();
            mirror.BeginEncounter();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsTrue();

            var shot = new FTT.Combat.PlaceholderProjectile { Name = "LiveMirrorShot" };
            host.AddChild(shot);
            shot.Setup(10f, new Vector2(3f, -2f), 400f, movingRight: true,
                ownerIndex: mirror.Clone.PlayerIndex, color: Colors.White,
                sourcePlayer: mirror.Clone);
            AssertThat(shot.IsInGroup("enemy_projectile")).IsTrue();

            AssertThat(manager.TriggerScriptedRewind()).IsTrue();
            // The freeze reached the mirror through the group sweep...
            AssertThat(mirror.IsStoryRewindFrozen).IsTrue();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsFalse();
            // ...and the projectile clear removed the clone's live shot.
            AssertThat(!GodotObject.IsInstanceValid(shot) || shot.IsQueuedForDeletion()).IsTrue();

            for (int i = 0; i < 600 && manager.IsRewinding; i++) manager._PhysicsProcess(1.0 / 60.0);
            AssertThat(manager.IsRewinding).IsFalse();
            // The resume hands the encounter back exactly as the freeze found it.
            AssertThat(mirror.IsStoryRewindFrozen).IsFalse();
            AssertThat(mirror.Clone.IsPhysicsProcessing()).IsTrue();
        } finally {
            if (mirror != null) FreeMirror(mirror);
            host.Free();
            StoryManager.Instance?.SetRewinds(3);
        }
    }


    // === V7.6 F20 campaign AI profile (Package 11 A7b) ===
    // docs/design-contracts/MIRROR_PARADOX.md. The encounter's tier is the ACTIVE
    // STORY DIFFICULTY, never the saved Holodeck CPU setting, and the boss keeps
    // its full core kit on every tier.

    [TestCase]
    public void TheProfileTiersReactionsAndDefenceRatesFromTheStoryDifficulty() {
        Difficulty original = GameManager.Instance.CurrentSession.Difficulty;
        try {
            AssertProfile(Difficulty.Easy, FTT.Core.CpuDifficulty.Easy,
                reactionMin: 30, reactionMax: 45, block: 10, hitstunDefense: 10, di: 0);
            AssertProfile(Difficulty.Normal, FTT.Core.CpuDifficulty.Normal,
                reactionMin: 15, reactionMax: 20, block: 40, hitstunDefense: 45, di: 40);
            AssertProfile(Difficulty.Hard, FTT.Core.CpuDifficulty.Hard,
                reactionMin: 4, reactionMax: 8, block: 80, hitstunDefense: 85, di: 80);
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = original;
        }
    }

    [TestCase]
    public void TheHPPoolIsSevenHundredOneThousandOrFifteenHundredFromTheAuthoredBase() {
        Difficulty original = GameManager.Instance.CurrentSession.Difficulty;
        try {
            AssertPool(Difficulty.Easy, 700);
            AssertPool(Difficulty.Normal, 1000);
            AssertPool(Difficulty.Hard, 1500);
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = original;
        }
    }

    [TestCase]
    public void TheFullCoreKitIsAvailableOnEveryDifficultyIncludingEasy() {
        // The practice Easy CPU zeroes both Specials, the movement ability and the
        // Ultimate. F20: "do not load a complete practice-CPU preset and
        // accidentally disable the boss's signature abilities."
        CpuBandTuning practiceEasy = CpuBandTuning.For(FTT.Core.CpuDifficulty.Easy);
        AssertThat(practiceEasy.SpecialOneClosePercent).IsEqual(0);
        AssertThat(practiceEasy.UltimatePercent).IsEqual(0);

        Difficulty original = GameManager.Instance.CurrentSession.Difficulty;
        try {
            foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
                GameManager.Instance.CurrentSession.Difficulty = difficulty;
                MirrorParadoxController mirror = CreateMirror();
                try {
                    CpuBandTuning tuning = mirror.Decisions.Tuning;
                    var issues = new List<string>();
                    if (tuning.SpecialOneClosePercent <= 0) issues.Add($"{difficulty}: Special 1 (close) disabled");
                    if (tuning.SpecialOneRangedPercent <= 0) issues.Add($"{difficulty}: Special 1 (ranged) disabled");
                    if (tuning.SpecialTwoPercent <= 0) issues.Add($"{difficulty}: Special 2 disabled");
                    if (tuning.MovementAbilityPercent <= 0) issues.Add($"{difficulty}: movement ability disabled");
                    if (tuning.UltimatePercent <= 0) issues.Add($"{difficulty}: Ultimate disabled");
                    if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");

                    // Easy and Normal fire the Ultimate at the first legal
                    // opportunity at full meter; only Hard seeks a confirm.
                    if (difficulty != Difficulty.Hard) {
                        AssertThat(tuning.UltimatePercent).IsEqual(100);
                        AssertThat(tuning.RequiresUltimateSetup).IsFalse();
                    } else {
                        AssertThat(tuning.RequiresUltimateSetup).IsTrue();
                    }
                } finally {
                    FreeMirror(mirror);
                }
            }
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = original;
        }
    }

    [TestCase]
    public void HardMirrorsPurchasedGridPerksWithoutStackingASecondHPPool() {
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];

        GameManager.Instance.CurrentSession.ActiveSaveSlot = 0;
        GameManager.Instance.CurrentSession.SelectedCharacterID = MirroredCharacter;
        SaveManager.Instance.SaveSlots[0] = new StorySaveData {
            SelectedCharacterID = MirroredCharacter,
            DepositedChronalDust = new Dictionary<string, int>(),
            GridProgress = new Dictionary<string, List<string>> {
                [MirroredCharacter] = new() {
                    "einstein_momentum", "einstein_mass", "einstein_chalk_edge",
                    "einstein_rift_range", "einstein_event_horizon"
                }
            }
        };

        MirrorParadoxController hard = null;
        MirrorParadoxController normal = null;
        try {
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Hard;
            hard = CreateMirror();
            AssertThat(hard.MirrorsPurchasedPerks).IsTrue();
            AssertThat(hard.Clone.StoryAbilityPerks.Count > 0)
                .OverrideFailureMessage("Hard must mirror the player's purchased grid perks.")
                .IsTrue();
            // Single application of stat modifiers: the encounter pool REPLACES the
            // character baseline, so a mirrored MaxHP perk can never stack a second
            // base pool on top of the 1500.
            AssertThat(hard.ScaledMaxHP).IsEqual(1500);
            AssertThat(hard.Clone.MaximumHP)
                .OverrideFailureMessage("The boss pool must not stack with a mirrored MaxHP perk.")
                .IsEqual(1500);
            AssertThat(hard.Clone.CurrentHP).IsEqual(1500);
            // And the V7.5 Legacy ability gate is never installed: core-kit access
            // is independent of Story ability locks at encounter time.
            AssertObject(hard.Clone.GetNodeOrNull("Special1")).IsNotNull();
            AssertObject(hard.Clone.GetNodeOrNull("Ultimate")).IsNotNull();

            // Normal borrows nothing from the player's build.
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Normal;
            normal = CreateMirror();
            AssertThat(normal.MirrorsPurchasedPerks).IsFalse();
            AssertThat(normal.Clone.StoryAbilityPerks.Count).IsEqual(0);
            AssertThat(normal.Clone.StoryMaxHPBonus).IsEqual(0);
        } finally {
            if (hard != null) FreeMirror(hard);
            if (normal != null) FreeMirror(normal);
            SaveManager.Instance.SaveSlots[0] = originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
        }
    }

    [TestCase]
    public void TheSavedHolodeckCpuSettingCannotChangeTheEncounter() {
        Difficulty originalDifficulty = GameManager.Instance.CurrentSession.Difficulty;
        FTT.Core.CpuDifficulty originalCpu = GameManager.Instance.CurrentSession.CpuDifficulty;
        try {
            // A player who practises against an Easy Holodeck CPU still meets the
            // Hard mirror when the campaign is Hard, and the reverse.
            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Hard;
            GameManager.Instance.CurrentSession.CpuDifficulty = FTT.Core.CpuDifficulty.Easy;
            MirrorParadoxController hardStory = CreateMirror();
            try {
                AssertThat(hardStory.EncounterCpuDifficulty).IsEqual(FTT.Core.CpuDifficulty.Hard);
                AssertThat(hardStory.Decisions.ReactionDelayMinFrames).IsEqual(4);
            } finally {
                FreeMirror(hardStory);
            }

            GameManager.Instance.CurrentSession.Difficulty = Difficulty.Easy;
            GameManager.Instance.CurrentSession.CpuDifficulty = FTT.Core.CpuDifficulty.Hard;
            MirrorParadoxController easyStory = CreateMirror();
            try {
                AssertThat(easyStory.EncounterCpuDifficulty).IsEqual(FTT.Core.CpuDifficulty.Easy);
                AssertThat(easyStory.Decisions.ReactionDelayMinFrames).IsEqual(30);
            } finally {
                FreeMirror(easyStory);
            }
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = originalDifficulty;
            GameManager.Instance.CurrentSession.CpuDifficulty = originalCpu;
        }
    }

    [TestCase]
    public void TheOutgoingCampaignDamageMultiplierIsAppliedOnceAtEachTier() {
        Difficulty original = GameManager.Instance.CurrentSession.Difficulty;
        try {
            AssertOutgoingDamage(Difficulty.Easy, 0.5f);
            AssertOutgoingDamage(Difficulty.Normal, 1.0f);
            AssertOutgoingDamage(Difficulty.Hard, 1.5f);
        } finally {
            GameManager.Instance.CurrentSession.Difficulty = original;
        }
    }

    private static void AssertProfile(
        Difficulty story, FTT.Core.CpuDifficulty expectedTier,
        int reactionMin, int reactionMax, int block, int hitstunDefense, int di) {
        GameManager.Instance.CurrentSession.Difficulty = story;
        MirrorParadoxController mirror = CreateMirror();
        try {
            AssertThat(mirror.EncounterDifficulty).IsEqual(story);
            AssertThat(mirror.EncounterCpuDifficulty).IsEqual(expectedTier);
            AssertThat(mirror.Decisions.Difficulty).IsEqual(expectedTier);
            AssertThat(mirror.Decisions.ReactionDelayMinFrames).IsEqual(reactionMin);
            AssertThat(mirror.Decisions.ReactionDelayMaxFrames).IsEqual(reactionMax);

            CpuBandTuning tuning = mirror.Decisions.Tuning;
            AssertThat(tuning.BlockPercent).IsEqual(block);
            AssertThat(tuning.HitstunDefensePercent).IsEqual(hitstunDefense);
            AssertThat(tuning.DiPercent).IsEqual(di);
        } finally {
            FreeMirror(mirror);
        }
    }

    private static void AssertPool(Difficulty story, int expectedHP) {
        GameManager.Instance.CurrentSession.Difficulty = story;
        MirrorParadoxController mirror = CreateMirror();
        try {
            AssertThat(mirror.ScaledMaxHP).IsEqual(expectedHP);
            AssertThat(mirror.Clone.MaximumHP).IsEqual(expectedHP);
            AssertThat(mirror.Clone.CurrentHP).IsEqual(expectedHP);
        } finally {
            FreeMirror(mirror);
        }
    }

    private static void AssertOutgoingDamage(Difficulty story, float expected) {
        GameManager.Instance.CurrentSession.Difficulty = story;
        MirrorParadoxController mirror = CreateMirror();
        try {
            AssertThat(mirror.OutgoingDamageMultiplier).IsEqualApprox(expected, 0.0001f);
            // The seam the clone actually attacks through: both Story damage lanes
            // carry the campaign multiplier, applied exactly once.
            AssertThat(mirror.Clone.StoryBasicDamageMultiplier).IsEqualApprox(expected, 0.0001f);
            AssertThat(mirror.Clone.StorySpecialDamageMultiplier).IsEqualApprox(expected, 0.0001f);
        } finally {
            FreeMirror(mirror);
        }
    }

    // === Helpers ===

    private static MirrorParadoxController CreateMirror(ulong seed = 4242) {
        var mirror = new MirrorParadoxController {
            Name = "MirrorParadoxUnderTest",
            Data = FTT.Core.AuthoredResources.Load<BossData>(MirrorResourcePath),
            CharacterIDOverride = MirroredCharacter,
            DecisionSeed = seed,
            // These tests assert construction and contract, not the fight. An
            // active clone would simulate on every frame the runner yields between
            // tests, spraying AI-driven attacks, VFX, and pooled objects into the
            // shared test tree.
            ActivateOnSpawn = false
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(mirror);
        return mirror;
    }

    /// <summary>
    /// Detach, then free. Calling <c>Free()</c> on a node still inside the tree is
    /// undefined behaviour in Godot, and the clone hierarchy carries eight
    /// monitoring Area2Ds registered with PhysicsServer2D. Removing it first fires
    /// <c>_ExitTree</c> while everything is still alive, so the controller can make
    /// the subtree inert and every area deregisters cleanly.
    /// </summary>
    private static void DetachAndFree(Node node) {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        node.GetParent()?.RemoveChild(node);
        node.Free();
    }

    private static void FreeMirror(MirrorParadoxController mirror) => DetachAndFree(mirror);
}
