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
        BossData data = ResourceLoader.Load<BossData>(MirrorResourcePath);
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
        BossData data = ResourceLoader.Load<BossData>(MirrorResourcePath);
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

            CharacterData canonical = ResourceLoader.Load<CharacterData>(
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
            CharacterData canonical = ResourceLoader.Load<CharacterData>(
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
    public void CloneNeverInheritsStoryResonancePerksEvenWhenTheActiveSaveHasThem() {
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        string originalCharacter = GameManager.Instance.CurrentSession.SelectedCharacterID;
        StorySaveData originalSave = SaveManager.Instance.SaveSlots[0];

        GameManager.Instance.CurrentSession.ActiveSaveSlot = 0;
        GameManager.Instance.CurrentSession.SelectedCharacterID = MirroredCharacter;
        SaveManager.Instance.SaveSlots[0] = new StorySaveData {
            SelectedCharacterID = MirroredCharacter,
            DepositedChronalDust = new Dictionary<string, int>(),
            GridProgress = new Dictionary<string, List<string>> {
                [MirroredCharacter] = new() { "einstein_u1", "einstein_u2", "einstein_u3", "einstein_o1", "einstein_d1" }
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
            progressed?.Free();
            SaveManager.Instance.SaveSlots[0] = originalSave;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
            GameManager.Instance.CurrentSession.SelectedCharacterID = originalCharacter;
        }
    }

    // === Decision engine reuse ===

    [TestCase]
    public void DecisionsComeFromTheSharedHardFighterCpuTableAndNothingElse() {
        MirrorParadoxController mirror = CreateMirror(seed: 20260807);
        try {
            MirrorParadoxDecisionAdapter adapter = mirror.Decisions;
            AssertObject(adapter).IsNotNull();
            AssertThat(adapter.ReactionDelayMinFrames).IsEqual(4);
            AssertThat(adapter.ReactionDelayMaxFrames).IsEqual(8);

            // A bare FighterCpuController on the same seed, fed the adapter's own
            // observation each tick, must emit byte-identical frames. That is the
            // proof the Story side reuses the engine rather than porting it.
            var reference = new FighterCpuController(FTT.Core.CpuDifficulty.Hard, 20260807);
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
        }
    }

    [TestCase]
    public void TheDecisionObservationProjectionIsLosslessForTheDeterministicPath() {
        // Extraction guard: Observe() must copy the deterministic components
        // verbatim so the Fighter path's behaviour is unchanged by the refactor.
        var self = new FighterStateComponent {
            Stocks = 3, HitstunFrames = 2, DazeFrames = 1, IsGrounded = 1,
            Influence = xpTURN.Klotho.Deterministic.Math.FP64.FromInt(60),
            Position = new xpTURN.Klotho.Deterministic.Math.FPVector2(
                xpTURN.Klotho.Deterministic.Math.FP64.FromInt(3),
                xpTURN.Klotho.Deterministic.Math.FP64.Zero)
        };
        var target = new FighterStateComponent {
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
        AssertThat(observation.TargetPositionXRaw).IsEqual(target.Position.x.RawValue);
        AssertThat(observation.Stocks).IsEqual(3);
        AssertThat(observation.HitstunFrames).IsEqual(2);
        AssertThat(observation.DazeFrames).IsEqual(1);
        AssertThat(observation.IsGrounded).IsEqual(1);
        AssertThat(observation.InfluenceRaw).IsEqual(self.Influence.RawValue);
        AssertThat(observation.SpecialOneCooldownFrames).IsEqual(11);
        AssertThat(observation.SpecialTwoCooldownFrames).IsEqual(22);
        AssertThat(observation.MovementCooldownFrames).IsEqual(33);
        AssertThat(observation.TargetPressedButtons).IsEqual((int)GameplayButtons.Special1);
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
            encounter.Free();
        }
    }

    [TestCase]
    public void RewindFreezeStopsTheCloneLikeAnyOtherStoryEnemy() {
        MirrorParadoxController mirror = CreateMirror();
        try {
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

    // === Helpers ===

    private static MirrorParadoxController CreateMirror(ulong seed = 4242) {
        var mirror = new MirrorParadoxController {
            Name = "MirrorParadoxUnderTest",
            Data = ResourceLoader.Load<BossData>(MirrorResourcePath),
            CharacterIDOverride = MirroredCharacter,
            DecisionSeed = seed
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(mirror);
        return mirror;
    }

    private static void FreeMirror(MirrorParadoxController mirror) => mirror.Free();
}
