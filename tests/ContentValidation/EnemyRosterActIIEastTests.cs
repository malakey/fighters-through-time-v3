using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 4 workstream B3 content contract: the Act II east + Alexandria era
/// roster (London, Gettysburg, Lunar Landing, Alexandria) authored per
/// docs/PACKAGE4_ROSTER_PLAN.md Section 4.1. Dust bands come from
/// docs/DUST_ECONOMY.md (standard 1-2, elite exactly 10).
///
/// Everything here loads through <see cref="ResourceLoader"/>, which caches, so
/// nothing in this suite mutates a loaded resource — <c>Duplicate()</c> first if a
/// future case needs to.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyRosterActIIEastTests {
    private static readonly string[] StandardIDs = {
        "holo_page", "laser_rifle_infantry", "vacuum_digger", "rift_phantom"
    };

    private static readonly string[] EliteIDs = {
        "kinetic_royal_guard", "cyber_cavalry_commander", "void_enforcer", "chrono_guard_elite"
    };

    // docs/PACKAGE4_ROSTER_PLAN.md Section 4.1 stat bands for this workstream.
    private const int StandardMinHP = 40;
    private const int StandardMaxHP = 60;
    private const int EliteMinHP = 160;
    private const int EliteMaxHP = 220;
    private const int EliteDustReward = 10;
    private const float EliteMinStunResistance = 0.4f;

    private static IEnumerable<string> AllIDs => StandardIDs.Concat(EliteIDs);

    private static EnemyData Load(string enemyID) =>
        ResourceLoader.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");

    /// <summary>Primary (when authored) plus every elite ability on one resource.</summary>
    private static IEnumerable<EnemyAbilityData> AbilitiesOf(EnemyData data) {
        if (data.PrimaryAttack != null) yield return data.PrimaryAttack;
        foreach (EnemyAbilityData ability in data.EliteAbilities ?? System.Array.Empty<EnemyAbilityData>()) {
            if (ability != null) yield return ability;
        }
    }

    [TestCase]
    public void EveryEraResourceLoadsWithAnIDMatchingItsFilename() {
        var ids = new HashSet<string>();
        foreach (string enemyID in AllIDs) {
            EnemyData data = Load(enemyID);
            AssertObject(data).IsNotNull();
            AssertThat(data.EnemyID).IsEqual(enemyID);
            AssertThat(ids.Add(data.EnemyID)).IsTrue();
            AssertThat(data.MaxHP > 0).IsTrue();
            AssertThat(data.Weight > 0f).IsTrue();
            AssertThat(data.MoveSpeed > 0f).IsTrue();
            AssertThat(data.AttackDamage > 0f).IsTrue();
            AssertThat(data.DeAggroRadius > data.AggroRadius).IsTrue();
            AssertThat(data.ReactionDelayMinFrames <= data.ReactionDelayMaxFrames).IsTrue();
            AssertThat(data.ItemDropChance > 0f).IsTrue();
        }
    }

    [TestCase]
    public void StandardsAndElitesSitInTheirDocumentedStatAndDustBands() {
        foreach (string enemyID in StandardIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.Tier).IsEqual(EnemyTier.Standard);
            AssertThat(data.MaxHP >= StandardMinHP && data.MaxHP <= StandardMaxHP).IsTrue();
            // docs/DUST_ECONOMY.md Section 1: standard mob kill is 1-2 dust, flat.
            AssertThat(data.ChronalDustDrop >= 1 && data.ChronalDustDrop <= 2).IsTrue();
        }

        foreach (string enemyID in EliteIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.Tier).IsEqual(EnemyTier.Elite);
            AssertThat(data.MaxHP >= EliteMinHP && data.MaxHP <= EliteMaxHP).IsTrue();
            // docs/DUST_ECONOMY.md Section 1: elite kill is exactly 10 dust.
            AssertThat(data.ChronalDustDrop).IsEqual(EliteDustReward);
            AssertThat(data.StunResistance >= EliteMinStunResistance).IsTrue();
            AssertThat(data.HasEliteAbilities).IsTrue();
            AssertThat(data.EliteAbilities.Length >= 1).IsTrue();
            AssertThat(data.EliteAbilityCooldown > 0f).IsTrue();
        }
    }

    [TestCase]
    public void EveryAuthoredAbilityCarriesValidArchetypeFields() {
        var abilityIDs = new HashSet<string>();
        foreach (string enemyID in AllIDs) {
            EnemyData data = Load(enemyID);
            foreach (EnemyAbilityData ability in AbilitiesOf(data)) {
                AssertThat(ability.HasValidIdentity()).IsTrue();
                AssertThat(ability.HasValidArchetypeFields()).IsTrue();
                AssertThat(ability.AbilityID.StartsWith($"enemy.{enemyID}.")).IsTrue();
                AssertThat(abilityIDs.Add(ability.AbilityID)).IsTrue();
                AssertThat(string.IsNullOrWhiteSpace(ability.PresentationEventID)).IsFalse();
                AssertThat(ability.TotalFrames > 0).IsTrue();
                // A telegraph is the design's mandatory readable windup.
                AssertThat(ability.TelegraphFrames > 0).IsTrue();
                // A status is meaningless without a duration to run for.
                if (ability.AppliedStatus != FTT.Core.StatusType.None) {
                    AssertThat(ability.StatusDuration > 0f).IsTrue();
                }
            }
        }
    }

    [TestCase]
    public void EveryDisplayNameKeyResolvesInTheEnglishTable() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }

        foreach (string enemyID in AllIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.DisplayNameKey).IsEqual($"enemy_{enemyID}_name");
            AssertThat(keys.Contains(data.DisplayNameKey)).IsTrue();
            foreach (EnemyAbilityData ability in AbilitiesOf(data)) {
                AssertThat(keys.Contains(ability.DisplayNameKey)).IsTrue();
            }
        }
    }

    [TestCase]
    public void MeleeStandardsWithoutAnAuthoredPrimaryStillCarryScalarFallbackTiming() {
        // Section 3.2: a null PrimaryAttack means the controller synthesizes a
        // MeleeStrike from the scalar fields, so those must be authored.
        foreach (string enemyID in AllIDs) {
            EnemyData data = Load(enemyID);
            if (data.PrimaryAttack != null) continue;
            AssertThat(data.AttackTelegraphFrames > 0).IsTrue();
            AssertThat(data.AttackActiveFrames > 0).IsTrue();
            AssertThat(data.AttackRecoveryFrames > 0).IsTrue();
            AssertThat(data.AttackKnockback > 0f).IsTrue();
            AssertThat(data.AttackCooldown > 0f).IsTrue();
        }
    }

    [TestCase]
    public void RiftPhantomIsAFlyingWallPhaser() {
        EnemyData data = Load("rift_phantom");
        AssertThat(data.Behavior).IsEqual(DefaultBehavior.Flying);
        AssertThat(data.PhasesThroughWalls).IsTrue();
        // Wraith: melee only, no authored ranged primary.
        AssertObject(data.PrimaryAttack).IsNull();
    }

    [TestCase]
    public void VacuumDiggerGravBeamPullsTheTargetTowardTheCaster() {
        EnemyData data = Load("vacuum_digger");
        EnemyAbilityData beam = data.PrimaryAttack;
        AssertObject(beam).IsNotNull();
        AssertThat(beam.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        // Negative authored X is the pull contract EnemyProjectile.Setup reads.
        AssertThat(beam.KnockbackForce.X < 0f).IsTrue();
        AssertThat(beam.IsPullKnockback).IsTrue();
    }

    [TestCase]
    public void CyberCavalryCommanderHasTheAuthoredFrontalForcefieldAndChargeDash() {
        EnemyData data = Load("cyber_cavalry_commander");
        AssertThat(Mathf.IsEqualApprox(data.FrontalDamageReduction, 0.4f)).IsTrue();
        AssertThat(data.EliteAbilities[0].Archetype).IsEqual(EnemyAbilityArchetype.ChargeDash);
        AssertThat(data.EliteAbilities[0].DashSpeed > 0f).IsTrue();
        AssertThat(data.EliteAbilities[0].DashDurationFrames >= 1).IsTrue();
    }

    [TestCase]
    public void EliteSecondaryArchetypesMatchTheRosterPlan() {
        // Section 4.1: Shockwave / ChargeDash / AreaPulse / ChargeDash respectively.
        AssertThat(Load("kinetic_royal_guard").EliteAbilities[0].Archetype)
            .IsEqual(EnemyAbilityArchetype.Shockwave);
        AssertThat(Load("cyber_cavalry_commander").EliteAbilities[0].Archetype)
            .IsEqual(EnemyAbilityArchetype.ChargeDash);
        AssertThat(Load("void_enforcer").EliteAbilities[0].Archetype)
            .IsEqual(EnemyAbilityArchetype.AreaPulse);
        AssertThat(Load("chrono_guard_elite").EliteAbilities[0].Archetype)
            .IsEqual(EnemyAbilityArchetype.ChargeDash);
    }

    [TestCase]
    public void TimeDilationCarriersApplyTheirAuthoredSlow() {
        // Holo-Page daggers "slow on hit"; the Void Enforcer's cold vent is a
        // slowing AreaPulse; Chrono-Guard blades are the design's 75% slow.
        EnemyAbilityData dagger = Load("holo_page").PrimaryAttack;
        AssertThat(dagger.AppliedStatus).IsEqual(FTT.Core.StatusType.TimeDilation);
        AssertThat(dagger.StatusDuration > 0f).IsTrue();

        EnemyAbilityData vent = Load("void_enforcer").EliteAbilities[0];
        AssertThat(vent.AppliedStatus).IsEqual(FTT.Core.StatusType.TimeDilation);
        AssertThat(vent.PulseRadius > 0f).IsTrue();

        EnemyAbilityData blades = Load("chrono_guard_elite").PrimaryAttack;
        AssertObject(blades).IsNotNull();
        AssertThat(blades.Archetype).IsEqual(EnemyAbilityArchetype.MeleeStrike);
        AssertThat(blades.AppliedStatus).IsEqual(FTT.Core.StatusType.TimeDilation);
        // TimeDilationStrategy: movement multiplier = 1 - 0.5 * intensity, so the
        // design's 75% slow needs intensity 1.5 and must beat every milder carrier.
        AssertThat(Mathf.IsEqualApprox(1f - 0.5f * blades.StatusIntensity, 0.25f)).IsTrue();
        AssertThat(blades.StatusIntensity > dagger.StatusIntensity).IsTrue();
        AssertThat(blades.StatusIntensity > vent.StatusIntensity).IsTrue();
    }

    [TestCase]
    public void EveryEraEntryReadsAsADistinctPlaceholderSilhouette() {
        // Package 4 Section 3.7: distinct tints across this workstream and the
        // five already-migrated prototype enemies.
        var tints = new List<Color>();
        foreach (string enemyID in AllIDs.Concat(new[] {
            "chrono_slasher", "tech_enforcer", "cyber_guard", "steam_automaton", "hologram_drone"
        })) {
            tints.Add(Load(enemyID).PlaceholderTint);
        }

        for (int i = 0; i < tints.Count; i++) {
            for (int j = i + 1; j < tints.Count; j++) {
                Color a = tints[i];
                Color b = tints[j];
                float separation = Mathf.Abs(a.R - b.R) + Mathf.Abs(a.G - b.G) + Mathf.Abs(a.B - b.B);
                AssertThat(separation > 0.2f).IsTrue();
            }
        }
    }
}
