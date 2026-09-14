using System.Collections.Generic;
using System.IO;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 4 workstream B4: content contracts for the Act I boss roster
/// (levels 2-7). Every kit must load, carry a legal phase/ability schema,
/// satisfy the no-deadlock melee+ranged coverage rule, and stay on the locked
/// 50-dust boss payout from docs/DUST_ECONOMY.md.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossRosterActITests {
    /// <summary>Boss IDs in campaign level order, levels 2 through 7.</summary>
    private static readonly string[] ActIBosses = {
        "siegemaster_duke",
        "chronal_inventor",
        "revolutionary_tribunal",
        "tidal_eraser",
        "vulcan_decimator",
        "dread_admiral"
    };

    private const float MeleeDistancePixels = 30f;   // 0.5 units, inside every authored melee band
    private const float RangedDistancePixels = 720f; // 12 units, outside every authored melee band

    [TestCase]
    public void EveryActIBossResourceLoadsWithAnIDMatchingItsFilename() {
        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            AssertObject(boss).IsNotNull();
            AssertThat(boss.BossID).IsEqual(bossID);
            AssertThat(boss.SchemaVersion).IsEqual(1);
            AssertThat(string.IsNullOrWhiteSpace(boss.DisplayName)).IsFalse();
            AssertThat(boss.MoveSpeed > 0f).IsTrue();
            AssertThat(boss.RestCooldown > 0f).IsTrue();
            AssertThat(boss.MeleeRangeThreshold > 0f).IsTrue();
            AssertThat(boss.RangedRangeThreshold > boss.MeleeRangeThreshold).IsTrue();
            AssertThat(boss.ReactionDelayMinFrames > 0).IsTrue();
            AssertThat(boss.ReactionDelayMaxFrames >= boss.ReactionDelayMinFrames).IsTrue();
        }
    }

    [TestCase]
    public void EveryActIBossPaysTheLockedFiftyDustBossReward() {
        foreach (string bossID in ActIBosses) {
            AssertThat(Load(bossID).ChronalDustDrop).IsEqual(50);
        }
    }

    /// <summary>
    /// Package 11 A7b (V7.6 ruling 2.E). The original 500-anchor / 500-700 band
    /// with a strictly ascending sweep across levels 1-7 is <b>retired</b>: the
    /// four authored V7.6 rows put 350 below that floor, 850 above its ceiling,
    /// and 850 &gt; 640 (<c>tidal_eraser</c>, level 5) breaks ascension across the
    /// Act I/II boundary. The four V7.6 values are pinned literally; ascension is
    /// asserted only over the deliberately untouched Act II/III tail. Whether the
    /// whole fifteen-boss curve should be re-tuned to the design table is the open
    /// <c>VERIFY-BOSS-HP</c> entry in
    /// <c>docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md</c>.
    /// </summary>
    [TestCase]
    public void ActIHealthPoolsPinTheAuthoredV76RowsAndAscendAcrossTheUnchangedTail() {
        AssertThat(Load("borgia_inquisitor").MaxHP).IsEqual(350);      // level 1
        AssertThat(Load("siegemaster_duke").MaxHP).IsEqual(520);       // level 2
        AssertThat(Load("chronal_inventor").MaxHP).IsEqual(700);       // level 3
        AssertThat(Load("revolutionary_tribunal").MaxHP).IsEqual(850); // level 4

        // Levels 5-7 keep their shipped pools and still ascend with the campaign.
        string[] unchangedTail = { "tidal_eraser", "vulcan_decimator", "dread_admiral" };
        int previous = 0;
        foreach (string bossID in unchangedTail) {
            int hp = Load(bossID).MaxHP;
            AssertThat(hp > previous).IsTrue();
            previous = hp;
        }

        // The V7.6 rows deliberately overshoot that tail — recorded, not accidental.
        AssertThat(Load("revolutionary_tribunal").MaxHP > Load("tidal_eraser").MaxHP).IsTrue();
    }

    [TestCase]
    public void PhaseThresholdsAreStrictlyDescendingFractionsInsideTheOpenUnitRange() {
        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            float[] thresholds = boss.PhaseThresholds;
            AssertObject(thresholds).IsNotNull();
            AssertThat(thresholds.Length).IsEqual(1); // every Act I boss is 2-phase
            float previous = 1f;
            foreach (float threshold in thresholds) {
                AssertThat(threshold > 0f && threshold < 1f).IsTrue();
                AssertThat(threshold < previous).IsTrue();
                previous = threshold;
            }
            AssertThat(boss.PhaseCount).IsEqual(2);
            AssertThat(boss.PhaseTransitionInvincibilityDuration > 0f).IsTrue();
        }
    }

    [TestCase]
    public void AuthoredPhaseSpeedMultipliersCoverEveryPhaseAndNeverSlowTheBossDown() {
        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            float[] multipliers = boss.PhaseSpeedMultipliers;
            AssertObject(multipliers).IsNotNull();
            AssertThat(multipliers.Length).IsEqual(boss.PhaseCount);
            AssertThat(multipliers[0]).IsEqualApprox(1.0f, 0.0001f);
            for (int phase = 1; phase < multipliers.Length; phase++) {
                AssertThat(multipliers[phase] > multipliers[phase - 1]).IsTrue();
            }
        }
    }

    [TestCase]
    public void AuthoredAbilityMinPhaseRunsParallelToTheAbilityListAndStaysInsideThePhaseCount() {
        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            AssertObject(boss.BossAbilities).IsNotNull();
            AssertThat(boss.BossAbilities.Length >= 3).IsTrue();
            AssertObject(boss.AbilityMinPhase).IsNotNull();
            AssertThat(boss.AbilityMinPhase.Length).IsEqual(boss.BossAbilities.Length);
            for (int index = 0; index < boss.AbilityMinPhase.Length; index++) {
                int minPhase = boss.AbilityMinPhase[index];
                AssertThat(minPhase >= 0 && minPhase < boss.PhaseCount).IsTrue();
            }
            // At least one ability must be usable from the opening phase.
            bool hasOpener = false;
            foreach (int minPhase in boss.AbilityMinPhase) {
                if (minPhase == 0) hasOpener = true;
            }
            AssertThat(hasOpener).IsTrue();
        }
    }

    [TestCase]
    public void EveryBossCarriesBothMeleeAndRangedCoverageSoItCanNeverDeadlock() {
        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            bool hasMelee = false;
            bool hasRanged = false;
            foreach (EnemyAbilityData ability in boss.BossAbilities) {
                AssertObject(ability).IsNotNull();
                if (ability.RangeClass is EnemyAbilityRangeClass.Melee or EnemyAbilityRangeClass.Any) hasMelee = true;
                if (ability.RangeClass is EnemyAbilityRangeClass.Ranged or EnemyAbilityRangeClass.Any) hasRanged = true;
            }
            AssertThat(hasMelee).IsTrue();
            AssertThat(hasRanged).IsTrue();
        }
    }

    [TestCase]
    public void EveryAuthoredAbilityCarriesTheFieldsItsArchetypeExecutorReads() {
        var abilityIDs = new HashSet<string>();
        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            foreach (EnemyAbilityData ability in boss.BossAbilities) {
                AssertObject(ability).IsNotNull();
                AssertThat(ability.HasValidIdentity()).IsTrue();
                AssertThat(ability.HasValidArchetypeFields()).IsTrue();
                string[] parts = ability.AbilityID.Split('.');
                AssertThat(parts.Length).IsEqual(3);
                AssertThat(parts[0]).IsEqual("boss");
                AssertThat(parts[1]).IsEqual(bossID);
                AssertThat(string.IsNullOrWhiteSpace(parts[2])).IsFalse();
                AssertThat(abilityIDs.Add(ability.AbilityID)).IsTrue();
                AssertThat(ability.SelectionWeight > 0f).IsTrue();
                AssertThat(ability.PresentationEventID).IsEqual(ability.AbilityID);
                AssertThat(ability.TelegraphTint.A > 0f).IsTrue();
                // Bosses telegraph harder than mobs so every attack stays readable.
                AssertThat(ability.TelegraphFrames >= 16).IsTrue();
                AssertThat(ability.TotalFrames > 0).IsTrue();
            }
        }
        AssertThat(abilityIDs.Count).IsEqual(19);
    }

    [TestCase]
    public void EveryBossAndAbilityDisplayNameKeyResolvesInTheEnglishTable() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }

        foreach (string bossID in ActIBosses) {
            BossData boss = Load(bossID);
            AssertThat(boss.DisplayNameKey).IsEqual($"boss_{bossID}_name");
            AssertThat(keys.Contains(boss.DisplayNameKey)).IsTrue();
            foreach (EnemyAbilityData ability in boss.BossAbilities) {
                AssertThat(string.IsNullOrWhiteSpace(ability.DisplayNameKey)).IsFalse();
                AssertThat(keys.Contains(ability.DisplayNameKey)).IsTrue();
            }
        }
    }

    [TestCase]
    public void TheParisTribunalSummonsTheEraRosterAndGatesItsHeavySummonBehindPhaseTwo() {
        BossData boss = Load("revolutionary_tribunal");
        EnemyAbilityData baseSummon = FindAbility(boss, "boss.revolutionary_tribunal.mob_call");
        EnemyAbilityData heavySummon = FindAbility(boss, "boss.revolutionary_tribunal.tribunal_levy");

        AssertThat(baseSummon.Archetype).IsEqual(EnemyAbilityArchetype.SummonMinions);
        // Cross-resource existence is a Phase C concern; B4 pins only the ID contract.
        AssertThat(baseSummon.SummonEnemyID).IsEqual("chrono_rioter");
        AssertThat(baseSummon.SummonCount).IsEqual(2);

        AssertThat(heavySummon.Archetype).IsEqual(EnemyAbilityArchetype.SummonMinions);
        AssertThat(heavySummon.SummonEnemyID).IsEqual("plasma_sabre_captain");
        AssertThat(heavySummon.SummonCount >= 1).IsTrue();

        int heavyIndex = System.Array.IndexOf(boss.BossAbilities, heavySummon);
        AssertThat(boss.GetAbilityMinPhase(heavyIndex)).IsEqual(1);
    }

    [TestCase]
    public void TheKitSketchArchetypesAreAuthoredAsSpecified() {
        AssertThat(HasArchetype("siegemaster_duke", EnemyAbilityArchetype.Shockwave)).IsTrue();
        AssertThat(HasArchetype("siegemaster_duke", EnemyAbilityArchetype.ChargeDash)).IsTrue();
        AssertThat(HasArchetype("chronal_inventor", EnemyAbilityArchetype.AreaPulse)).IsTrue();
        AssertThat(HasArchetype("revolutionary_tribunal", EnemyAbilityArchetype.SummonMinions)).IsTrue();
        AssertThat(HasArchetype("tidal_eraser", EnemyAbilityArchetype.Shockwave)).IsTrue();
        AssertThat(HasArchetype("tidal_eraser", EnemyAbilityArchetype.AreaPulse)).IsTrue();
        AssertThat(HasArchetype("vulcan_decimator", EnemyAbilityArchetype.Shockwave)).IsTrue();
        AssertThat(HasArchetype("dread_admiral", EnemyAbilityArchetype.AreaPulse)).IsTrue();

        // Every kit sketch but the Tidal Eraser's names an explicit melee swing.
        foreach (string bossID in ActIBosses) {
            if (bossID == "tidal_eraser") continue;
            AssertThat(HasArchetype(bossID, EnemyAbilityArchetype.MeleeStrike)).IsTrue();
        }

        // The Tidal Eraser has no melee swing in its sketch, so its close-range
        // answer is the Melee-class undertow pulse. Without that the boss would
        // deadlock the no-valid-attack rule at point-blank range.
        BossData tidal = Load("tidal_eraser");
        bool meleeClassPulse = false;
        foreach (EnemyAbilityData ability in tidal.BossAbilities) {
            if (ability.Archetype == EnemyAbilityArchetype.AreaPulse
                && ability.RangeClass == EnemyAbilityRangeClass.Melee) {
                meleeClassPulse = true;
            }
        }
        AssertThat(meleeClassPulse).IsTrue();
    }

    [TestCase]
    public void ChicagoBiasesItsSelectionWeightTowardRangedPressure() {
        BossData boss = Load("chronal_inventor");
        float ranged = 0f;
        float melee = 0f;
        foreach (EnemyAbilityData ability in boss.BossAbilities) {
            if (ability.RangeClass == EnemyAbilityRangeClass.Ranged) ranged += ability.SelectionWeight;
            if (ability.RangeClass == EnemyAbilityRangeClass.Melee) melee += ability.SelectionWeight;
        }
        AssertThat(ranged > melee).IsTrue();
    }

    [TestCase]
    public void PompeiiAndTitanicUseTheirSignatureStatusAndPullPayloads() {
        // Vulcan's lobbed mortar is the RadiantBurn applicator.
        EnemyAbilityData mortar = FindAbility(Load("vulcan_decimator"), "boss.vulcan_decimator.ash_mortar");
        AssertThat(mortar.AppliedStatus).IsEqual(FTT.Core.StatusType.RadiantBurn);
        AssertThat(mortar.StatusDuration > 0f).IsTrue();
        AssertThat(mortar.ProjectileGravity > 0f).IsTrue(); // lobbed arc

        // Chicago's arcs carry StaticCharge.
        EnemyAbilityData arcs = FindAbility(Load("chronal_inventor"), "boss.chronal_inventor.arc_volley");
        AssertThat(arcs.AppliedStatus).IsEqual(FTT.Core.StatusType.StaticCharge);
        AssertThat(arcs.StatusDuration > 0f).IsTrue();

        // The Tidal Eraser's undertow drags the player in rather than out.
        EnemyAbilityData undertow = FindAbility(Load("tidal_eraser"), "boss.tidal_eraser.undertow_pulse");
        AssertThat(undertow.IsPullKnockback).IsTrue();

        // Nassau's gatling is a genuine spread volley.
        EnemyAbilityData gatling = FindAbility(Load("dread_admiral"), "boss.dread_admiral.gatling_volley");
        AssertThat(gatling.ProjectileCount >= 4).IsTrue();
        AssertThat(gatling.ProjectileSpreadDegrees > 0f).IsTrue();
    }

    [TestCase]
    public void SelectionAtEveryDistanceReturnsARangeAppropriateAttackForEveryBoss() {
        foreach (string bossID in ActIBosses) {
            BossData data = Load(bossID);
            BossController boss = CreateBoss(data, seed: 20260807);
            try {
                for (int roll = 0; roll < 60; roll++) {
                    int close = boss.SelectAbilityIndex(MeleeDistancePixels);
                    AssertThat(close >= 0).IsTrue();
                    AssertThat(data.BossAbilities[close].RangeClass
                        is EnemyAbilityRangeClass.Melee or EnemyAbilityRangeClass.Any).IsTrue();

                    int far = boss.SelectAbilityIndex(RangedDistancePixels);
                    AssertThat(far >= 0).IsTrue();
                    AssertThat(data.BossAbilities[far].RangeClass
                        is EnemyAbilityRangeClass.Ranged or EnemyAbilityRangeClass.Any).IsTrue();
                }
            } finally {
                boss.Free();
            }
        }
    }

    [TestCase]
    public void PhaseGatedAbilitiesStayLockedUntilTheirUnlockPhase() {
        BossData data = Load("revolutionary_tribunal");
        int heavyIndex = System.Array.IndexOf(
            data.BossAbilities, FindAbility(data, "boss.revolutionary_tribunal.tribunal_levy"));
        BossController boss = CreateBoss(data, seed: 771);
        try {
            for (int roll = 0; roll < 120; roll++) {
                AssertThat(boss.SelectAbilityIndex(MeleeDistancePixels)).IsNotEqual(heavyIndex);
                AssertThat(boss.SelectAbilityIndex(RangedDistancePixels)).IsNotEqual(heavyIndex);
            }
            boss.CurrentPhase = 1;
            bool sawHeavySummon = false;
            for (int roll = 0; roll < 200; roll++) {
                if (boss.SelectAbilityIndex(MeleeDistancePixels) == heavyIndex) sawHeavySummon = true;
            }
            AssertThat(sawHeavySummon).IsTrue();
        } finally {
            boss.Free();
        }
    }

    // === Helpers ===

    private static BossData Load(string bossID) =>
        FTT.Core.AuthoredResources.Load<BossData>($"res://resources/Bosses/{bossID}.tres");

    private static EnemyAbilityData FindAbility(BossData boss, string abilityID) {
        foreach (EnemyAbilityData ability in boss.BossAbilities) {
            if (ability?.AbilityID == abilityID) return ability;
        }
        AssertObject(null).IsNotNull(); // authored ability is missing from the kit
        return null;
    }

    private static bool HasArchetype(string bossID, EnemyAbilityArchetype archetype) {
        foreach (EnemyAbilityData ability in Load(bossID).BossAbilities) {
            if (ability?.Archetype == archetype) return true;
        }
        return false;
    }

    /// <summary>
    /// Instantiates the authored boss scene against a cached canonical resource.
    /// Unlike the unit-test helper this never disposes <paramref name="data"/>:
    /// these resources are ResourceLoader-cached content, not per-test fixtures.
    /// </summary>
    private static BossController CreateBoss(BossData data, ulong seed) {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = seed;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }
}
