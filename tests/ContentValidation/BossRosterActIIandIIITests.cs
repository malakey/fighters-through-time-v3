using System.Collections.Generic;
using System.IO;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 4 workstream B5: content contracts for the Act II/III boss kits
/// (levels 8-15, excluding <c>mirror_paradox</c>, which is CPU-driven and
/// carries no <see cref="BossData"/> abilities).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossRosterActIIandIIITests {
    private const string BossDirectory = "res://resources/Bosses";

    /// <summary>Boss ID -> authored phase count (thresholds + 1).</summary>
    private static readonly (string ID, int PhaseCount)[] Roster = {
        ("jackal_priest", 2),
        ("iron_chancellor", 2),
        ("tragedy_king", 2),
        ("siege_cannon", 2),
        ("gravity_overseer", 3),
        ("archive_prime", 3),
        ("apex_eraser", 3)
    };

    private static BossData Load(string bossID) =>
        FTT.Core.AuthoredResources.Load<BossData>($"{BossDirectory}/{bossID}.tres");

    [TestCase]
    public void EveryActTwoAndThreeBossLoadsWithAnIDMatchingItsFilename() {
        var ids = new HashSet<string>();
        foreach ((string bossID, int _) in Roster) {
            BossData boss = Load(bossID);
            AssertObject(boss).IsNotNull();
            AssertThat(boss.BossID).IsEqual(bossID);
            AssertThat(ids.Add(boss.BossID)).IsTrue();
            AssertThat(boss.DisplayName.Length > 0).IsTrue();
            AssertThat(boss.MaxHP > 0).IsTrue();
            AssertThat(boss.MoveSpeed > 0f).IsTrue();
            AssertThat(boss.ReactionDelayMinFrames <= boss.ReactionDelayMaxFrames).IsTrue();
            AssertThat(boss.MeleeRangeThreshold < boss.RangedRangeThreshold).IsTrue();
            AssertThat(boss.RestCooldown > 0f).IsTrue();
        }
        AssertThat(ids.Count).IsEqual(7);
    }

    [TestCase]
    public void BossDustDropsStayLockedToTheCampaignBossValue() {
        foreach ((string bossID, int _) in Roster) {
            AssertThat(Load(bossID).ChronalDustDrop).IsEqual(25);
        }
    }

    [TestCase]
    public void PhaseThresholdsAreStrictlyDescendingAndInsideTheOpenUnitInterval() {
        foreach ((string bossID, int phaseCount) in Roster) {
            BossData boss = Load(bossID);
            float[] thresholds = boss.PhaseThresholds;
            AssertObject(thresholds).IsNotNull();
            AssertThat(thresholds.Length).IsEqual(phaseCount - 1);
            AssertThat(boss.PhaseCount).IsEqual(phaseCount);
            for (int index = 0; index < thresholds.Length; index++) {
                AssertThat(thresholds[index] > 0f && thresholds[index] < 1f).IsTrue();
                if (index > 0) AssertThat(thresholds[index] < thresholds[index - 1]).IsTrue();
            }
            AssertThat(boss.PhaseTransitionInvincibilityDuration > 0f).IsTrue();
        }
    }

    [TestCase]
    public void ThreePhaseBossesCarryExactlyTwoThresholds() {
        foreach ((string bossID, int phaseCount) in Roster) {
            if (phaseCount != 3) continue;
            BossData boss = Load(bossID);
            AssertThat(boss.PhaseThresholds.Length).IsEqual(2);
            AssertThat(boss.PhaseThresholds[0]).IsEqualApprox(0.66f, 0.0001f);
            AssertThat(boss.PhaseThresholds[1]).IsEqualApprox(0.33f, 0.0001f);
        }
    }

    [TestCase]
    public void PhaseSpeedMultipliersAndAbilityMinPhaseHaveConsistentLengths() {
        foreach ((string bossID, int phaseCount) in Roster) {
            BossData boss = Load(bossID);
            AssertThat(boss.PhaseSpeedMultipliers.Length).IsEqual(phaseCount);
            AssertThat(boss.AbilityMinPhase.Length).IsEqual(boss.BossAbilities.Length);
            foreach (float multiplier in boss.PhaseSpeedMultipliers) {
                AssertThat(multiplier > 0f).IsTrue();
            }
            // A gate beyond the final phase would make an ability unreachable.
            foreach (int minPhase in boss.AbilityMinPhase) {
                AssertThat(minPhase >= 0 && minPhase < phaseCount).IsTrue();
            }
        }
    }

    [TestCase]
    public void EveryBossCoversBothDistanceBandsSoSelectionCanNeverDeadlock() {
        foreach ((string bossID, int _) in Roster) {
            BossData boss = Load(bossID);
            AssertThat(boss.BossAbilities.Length >= 3).IsTrue();
            AssertThat(boss.AttackPattern).IsEqual(BossAttackPattern.DistanceBased);

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
    public void OpeningPhaseAlreadyCoversBothDistanceBands() {
        // Phase gating must not leave phase 0 without a melee or a ranged option.
        foreach ((string bossID, int _) in Roster) {
            BossData boss = Load(bossID);
            bool hasMelee = false;
            bool hasRanged = false;
            for (int index = 0; index < boss.BossAbilities.Length; index++) {
                if (boss.GetAbilityMinPhase(index) != 0) continue;
                EnemyAbilityRangeClass rangeClass = boss.BossAbilities[index].RangeClass;
                if (rangeClass is EnemyAbilityRangeClass.Melee or EnemyAbilityRangeClass.Any) hasMelee = true;
                if (rangeClass is EnemyAbilityRangeClass.Ranged or EnemyAbilityRangeClass.Any) hasRanged = true;
            }
            AssertThat(hasMelee).IsTrue();
            AssertThat(hasRanged).IsTrue();
        }
    }

    [TestCase]
    public void EveryAuthoredAbilityCarriesTheFieldsItsArchetypeExecutes() {
        var abilityIDs = new HashSet<string>();
        foreach ((string bossID, int _) in Roster) {
            BossData boss = Load(bossID);
            foreach (EnemyAbilityData ability in boss.BossAbilities) {
                AssertThat(ability.HasValidIdentity()).IsTrue();
                AssertThat(ability.HasValidArchetypeFields()).IsTrue();
                AssertThat(ability.AbilityID.StartsWith($"boss.{bossID}.")).IsTrue();
                AssertThat(abilityIDs.Add(ability.AbilityID)).IsTrue();
                AssertThat(ability.SelectionWeight > 0f).IsTrue();
                AssertThat(ability.TelegraphFrames > 0).IsTrue();
                AssertThat(ability.PresentationEventID).IsEqual(ability.AbilityID);
                AssertThat(ability.TelegraphTint.A > 0f).IsTrue();
            }
        }
    }

    [TestCase]
    public void SignatureArchetypesAreAuthoredWhereTheRosterPlanCallsForThem() {
        // gravity_overseer: the pull well is a negative-X knockback AreaPulse gated to phase 3.
        BossData overseer = Load("gravity_overseer");
        int wellIndex = IndexOf(overseer, "boss.gravity_overseer.gravity_well");
        AssertThat(wellIndex >= 0).IsTrue();
        EnemyAbilityData well = overseer.BossAbilities[wellIndex];
        AssertThat(well.Archetype).IsEqual(EnemyAbilityArchetype.AreaPulse);
        AssertThat(well.IsPullKnockback).IsTrue();
        AssertThat(overseer.GetAbilityMinPhase(wellIndex)).IsEqual(2);

        // archive_prime: a wide, high-count laser grid plus pooled drone summons.
        BossData prime = Load("archive_prime");
        EnemyAbilityData grid = prime.BossAbilities[IndexOf(prime, "boss.archive_prime.laser_grid")];
        AssertThat(grid.ProjectileCount >= 5).IsTrue();
        AssertThat(grid.ProjectileSpreadDegrees >= 30f).IsTrue();

        // jackal_priest telegraphs are punishable; iron_chancellor and the finale are not.
        AssertThat(Load("jackal_priest").InterruptibleDuringTelegraph).IsTrue();
        AssertThat(Load("iron_chancellor").InterruptibleDuringTelegraph).IsFalse();
        AssertThat(Load("apex_eraser").InterruptibleDuringTelegraph).IsFalse();

        // iron_chancellor and siege_cannon are immobile artillery: heavy, slow, unshovable.
        BossData chancellor = Load("iron_chancellor");
        AssertThat(chancellor.IsKnockbackImmune).IsTrue();
        AssertThat(chancellor.MoveSpeed < 3f).IsTrue();
        EnemyAbilityData shells = chancellor.BossAbilities[IndexOf(chancellor, "boss.iron_chancellor.mortar_volley")];
        AssertThat(shells.ProjectileGravity > 0f).IsTrue();
        AssertThat(Load("siege_cannon").RangedRangeThreshold >= 12f).IsTrue();
    }

    [TestCase]
    public void SummonAbilitiesNameARealRosterEnemyID() {
        // holo_page ships with the London enemy workstream; Phase C validates the
        // cross-reference on disk. Here only the authored ID string is asserted.
        AssertThat(SummonID("tragedy_king", "boss.tragedy_king.curtain_call")).IsEqual("holo_page");
        AssertThat(SummonID("archive_prime", "boss.archive_prime.drone_deployment")).IsEqual("hologram_drone");
        AssertThat(SummonID("apex_eraser", "boss.apex_eraser.archive_remnants")).IsEqual("chrono_slasher");

        foreach (string enemyID in new[] { "hologram_drone", "chrono_slasher" }) {
            AssertThat(ResourceLoader.Exists($"res://resources/Enemies/{enemyID}.tres")).IsTrue();
        }
    }

    [TestCase]
    public void EveryBossAndAbilityDisplayNameKeyResolvesInTheEnglishTable() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }

        foreach ((string bossID, int _) in Roster) {
            BossData boss = Load(bossID);
            AssertThat(boss.DisplayNameKey).IsEqual($"boss_{bossID}_name");
            AssertThat(keys.Contains(boss.DisplayNameKey)).IsTrue();
            foreach (EnemyAbilityData ability in boss.BossAbilities) {
                AssertThat(ability.DisplayNameKey.Length > 0).IsTrue();
                AssertThat(keys.Contains(ability.DisplayNameKey)).IsTrue();
            }
        }
    }

    [TestCase]
    public void SeededSelectionRespectsDistanceBandsAndPhaseGatesOnAuthoredKits() {
        BossController boss = CreateBoss(Load("apex_eraser"), seed: 20260807);
        try {
            const float meleeDistance = 60f;   // 1 unit, inside the 3-unit melee threshold
            const float rangedDistance = 480f; // 8 units, outside it

            // Phase 0: chronal_collapse (min phase 1) and archive_remnants (2) are locked out.
            var seenInPhaseZero = new HashSet<int>();
            for (int roll = 0; roll < 400; roll++) {
                seenInPhaseZero.Add(boss.SelectAbilityIndex(meleeDistance));
                seenInPhaseZero.Add(boss.SelectAbilityIndex(rangedDistance));
            }
            AssertThat(seenInPhaseZero.Contains(2)).IsFalse();
            AssertThat(seenInPhaseZero.Contains(4)).IsFalse();
            AssertThat(seenInPhaseZero.Contains(0)).IsTrue(); // erasure_blade, Melee
            AssertThat(seenInPhaseZero.Contains(1)).IsTrue(); // unwriting_lance, Ranged

            // Melee-class abilities are never selected from ranged distance and vice versa.
            for (int roll = 0; roll < 200; roll++) {
                AssertThat(boss.SelectAbilityIndex(meleeDistance)).IsNotEqual(1);
                AssertThat(boss.SelectAbilityIndex(rangedDistance)).IsNotEqual(0);
            }

            boss.CurrentPhase = 2;
            var seenInFinalPhase = new HashSet<int>();
            for (int roll = 0; roll < 400; roll++) {
                seenInFinalPhase.Add(boss.SelectAbilityIndex(meleeDistance));
            }
            AssertThat(seenInFinalPhase.Contains(2)).IsTrue();
            AssertThat(seenInFinalPhase.Contains(4)).IsTrue();
        } finally {
            boss.Free();
        }
    }

    [TestCase]
    public void EveryAuthoredKitProducesASelectableAttackAtEveryPhaseAndDistance() {
        foreach ((string bossID, int phaseCount) in Roster) {
            BossController boss = CreateBoss(Load(bossID), seed: 4242);
            try {
                for (int phase = 0; phase < phaseCount; phase++) {
                    boss.CurrentPhase = phase;
                    foreach (float distance in new[] { 30f, 60f, 300f, 900f }) {
                        AssertThat(boss.SelectAbilityIndex(distance) >= 0).IsTrue();
                    }
                }
            } finally {
                boss.Free();
            }
        }
    }

    /// <summary>
    /// Package 11 A7b (V7.5 renames, plan §2.3). The two Act III bosses were renamed
    /// in <b>display only</b>: the English values in <c>en.csv</c> (A6) and the
    /// non-localized <c>DisplayName</c> debug field on the resource (A7b). Every
    /// identifier is deliberately retained — <c>BossID</c>, <c>DisplayNameKey</c>,
    /// file names, manifest rows, sprite atlas paths, the five ability
    /// <c>AbilityID</c>/<c>PresentationEventID</c> strings, and the boss-intro
    /// seen-set keys already persisted in players' saves.
    /// </summary>
    [TestCase]
    public void TheActIIIBossesCarryTheirV75DisplayNamesWithEveryIdentifierRetained() {
        TranslationServer.SetLocale("en");

        BossData forgeSentinel = Load("archive_prime");
        AssertThat(forgeSentinel.BossID).IsEqual("archive_prime");
        AssertThat(forgeSentinel.DisplayNameKey).IsEqual("boss_archive_prime_name");
        AssertThat(forgeSentinel.DisplayName).IsEqual("The Forge Sentinel");
        AssertThat(TranslationServer.Translate("boss_archive_prime_name").ToString())
            .IsEqual("The Forge Sentinel");

        BossData firstUnbound = Load("apex_eraser");
        AssertThat(firstUnbound.BossID).IsEqual("apex_eraser");
        AssertThat(firstUnbound.DisplayNameKey).IsEqual("boss_apex_eraser_name");
        AssertThat(firstUnbound.DisplayName).IsEqual("The First Severed");
        AssertThat(TranslationServer.Translate("boss_apex_eraser_name").ToString())
            .IsEqual("The First Severed");

        // Ability identifiers are untouched by the rename.
        foreach (EnemyAbilityData ability in firstUnbound.BossAbilities) {
            AssertThat(ability.AbilityID.StartsWith("boss.apex_eraser.")).IsTrue();
            AssertThat(ability.PresentationEventID).IsEqual(ability.AbilityID);
        }

        // The First Unbound is the one boss authoring the two V7.6 behaviours.
        AssertThat(firstUnbound.HasHistoricalRecovery).IsTrue();
        AssertThat(firstUnbound.BorrowsRosterLegacies).IsTrue();
        AssertThat(forgeSentinel.HasHistoricalRecovery).IsFalse();
        AssertThat(forgeSentinel.BorrowsRosterLegacies).IsFalse();
    }

    // === Helpers ===

    private static int IndexOf(BossData boss, string abilityID) {
        for (int index = 0; index < boss.BossAbilities.Length; index++) {
            if (boss.BossAbilities[index].AbilityID == abilityID) return index;
        }
        return -1;
    }

    private static string SummonID(string bossID, string abilityID) {
        BossData boss = Load(bossID);
        EnemyAbilityData ability = boss.BossAbilities[IndexOf(boss, abilityID)];
        AssertThat(ability.Archetype).IsEqual(EnemyAbilityArchetype.SummonMinions);
        AssertThat(ability.SummonCount).IsEqual(2);
        return ability.SummonEnemyID;
    }

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
