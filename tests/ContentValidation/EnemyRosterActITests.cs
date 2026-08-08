using System.Collections.Generic;
using System.IO;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 4 workstream B1: the Act I era roster (Orleans, Chicago, Paris).
/// Locks the authored contracts from docs/PACKAGE4_ROSTER_PLAN.md Section 4.1 and
/// the dust bands from docs/DUST_ECONOMY.md — retune the resources and this suite
/// together.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyRosterActITests {
    private const float Step = 1f / 60f;

    private static readonly string[] StandardIDs = {
        "laser_archer", "voltaic_shock_drone", "chrono_rioter"
    };

    private static readonly string[] EliteIDs = {
        "neural_linked_knight", "tesla_exo_baron", "plasma_sabre_captain"
    };

    private static readonly string[] AllActOneIDs = {
        "laser_archer", "neural_linked_knight", "voltaic_shock_drone",
        "tesla_exo_baron", "chrono_rioter", "plasma_sabre_captain"
    };

    // The five Phase A entries, so tint distinctness is checked across the whole
    // on-disk roster rather than just this workstream's slice.
    private static readonly string[] PhaseAIDs = {
        "chrono_slasher", "tech_enforcer", "cyber_guard", "steam_automaton", "hologram_drone"
    };

    // docs/DUST_ECONOMY.md Section 1 reward tiers.
    private const int EliteDustReward = 10;

    [TestCase]
    public void ActOneResourcesLoadWithInternalIDsMatchingTheirFilenames() {
        var ids = new HashSet<string>();
        foreach (string enemyID in AllActOneIDs) {
            EnemyData data = Load(enemyID);
            AssertObject(data).IsNotNull();
            AssertThat(data.EnemyID == enemyID).IsTrue();
            AssertThat(ids.Add(data.EnemyID)).IsTrue();
            AssertThat(data.SchemaVersion).IsEqual(1);
            AssertThat(string.IsNullOrWhiteSpace(data.DisplayName)).IsFalse();
            AssertThat(data.DisplayNameKey == $"enemy_{enemyID}_name").IsTrue();
            AssertThat(data.MaxHP > 0).IsTrue();
            AssertThat(data.Weight > 0f).IsTrue();
            AssertThat(data.MoveSpeed > 0f).IsTrue();
            AssertThat(data.AttackDamage > 0f).IsTrue();
            AssertThat(data.DeAggroRadius > data.AggroRadius).IsTrue();
            AssertThat(data.ReactionDelayMinFrames <= data.ReactionDelayMaxFrames).IsTrue();
            AssertThat(data.ItemDropChance).IsEqual(1.0f);
            // Authored 60 Hz attack envelope: the controller synthesizes the legacy
            // melee from these when PrimaryAttack is null.
            AssertThat(data.AttackTelegraphFrames > 0).IsTrue();
            AssertThat(data.AttackActiveFrames >= 1).IsTrue();
            AssertThat(data.AttackRecoveryFrames > 0).IsTrue();
        }
    }

    [TestCase]
    public void ActOneTierBandsAndDustDropsConformToTheEconomy() {
        // Plan Section 4.1 stat bands + docs/DUST_ECONOMY.md Section 1 reward tiers.
        foreach (string enemyID in StandardIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.Tier).IsEqual(EnemyTier.Standard);
            AssertThat(data.MaxHP >= 30 && data.MaxHP <= 60).IsTrue();
            AssertThat(data.ChronalDustDrop >= 1 && data.ChronalDustDrop <= 2).IsTrue();
        }

        foreach (string enemyID in EliteIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.Tier).IsEqual(EnemyTier.Elite);
            AssertThat(data.MaxHP >= 140 && data.MaxHP <= 220).IsTrue();
            AssertThat(data.ChronalDustDrop).IsEqual(EliteDustReward);
            AssertThat(data.StunResistance >= 0.4f).IsTrue();
            AssertThat(data.EliteAbilityCooldown > 0f).IsTrue();
        }
    }

    [TestCase]
    public void OnlyActOneElitesCarryEliteAbilities() {
        foreach (string enemyID in EliteIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.HasEliteAbilities).IsTrue();
            AssertThat(data.EliteAbilities.Length >= 1).IsTrue();
            foreach (EnemyAbilityData ability in data.EliteAbilities) {
                AssertObject(ability).IsNotNull();
            }
        }

        foreach (string enemyID in StandardIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.HasEliteAbilities).IsFalse();
            AssertThat(data.EliteAbilities == null || data.EliteAbilities.Length == 0).IsTrue();
        }
    }

    [TestCase]
    public void EveryAuthoredActOneAbilityPassesArchetypeValidation() {
        var abilityIDs = new HashSet<string>();
        foreach (string enemyID in AllActOneIDs) {
            foreach (EnemyAbilityData ability in AbilitiesOf(Load(enemyID))) {
                AssertThat(ability.HasValidIdentity()).IsTrue();
                AssertThat(ability.HasValidArchetypeFields()).IsTrue();
                AssertThat(ability.AbilityID.StartsWith($"enemy.{enemyID}.")).IsTrue();
                AssertThat(abilityIDs.Add(ability.AbilityID)).IsTrue();
                AssertThat(string.IsNullOrWhiteSpace(ability.PresentationEventID)).IsFalse();
                AssertThat(ability.PresentationEventID == ability.AbilityID).IsTrue();
                AssertThat(ability.TelegraphTint.A > 0f).IsTrue();
                AssertThat(ability.TelegraphFrames > 0).IsTrue();
                AssertThat(ability.TotalFrames > 0).IsTrue();
                AssertThat(ability.CooldownSeconds > 0f).IsTrue();
            }
        }
        // 1 primary (laser_archer) + 1 primary (voltaic) + 3 elite abilities.
        AssertThat(abilityIDs.Count).IsEqual(5);
    }

    [TestCase]
    public void AuthoredArchetypesMatchThePlannedActOneRosterRows() {
        // Plan Section 4.1: the per-row Primary / Elite ability commitments.
        EnemyData archer = Load("laser_archer");
        AssertObject(archer.PrimaryAttack).IsNotNull();
        AssertThat(archer.PrimaryAttack.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(archer.PrimaryAttack.RangeClass).IsEqual(EnemyAbilityRangeClass.Ranged);
        AssertThat(archer.PrimaryAttack.ProjectileSpeed > 0f).IsTrue();
        AssertThat(archer.Behavior).IsEqual(DefaultBehavior.Ground);

        EnemyData knight = Load("neural_linked_knight");
        AssertObject(knight.PrimaryAttack).IsNull(); // synthesized MeleeStrike broadsword
        AssertThat(knight.EliteAbilities[0].Archetype).IsEqual(EnemyAbilityArchetype.ChargeDash);
        AssertThat(knight.EliteAbilities[0].DashSpeed > 0f).IsTrue();
        // "high StunResistance": the stiffest of the Act I elites.
        AssertThat(knight.StunResistance > Load("tesla_exo_baron").StunResistance).IsTrue();
        AssertThat(knight.StunResistance > Load("plasma_sabre_captain").StunResistance).IsTrue();

        EnemyData drone = Load("voltaic_shock_drone");
        AssertThat(drone.Behavior).IsEqual(DefaultBehavior.Flying);
        AssertObject(drone.PrimaryAttack).IsNotNull();
        AssertThat(drone.PrimaryAttack.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(drone.PrimaryAttack.AppliedStatus).IsEqual(StatusType.StaticCharge);
        AssertThat(drone.PrimaryAttack.StatusDuration > 0f).IsTrue();

        EnemyData baron = Load("tesla_exo_baron");
        AssertObject(baron.PrimaryAttack).IsNull(); // synthesized MeleeStrike
        EnemyAbilityData nova = baron.EliteAbilities[0];
        AssertThat(nova.Archetype).IsEqual(EnemyAbilityArchetype.AreaPulse);
        AssertThat(nova.PulseRadius > 0f).IsTrue();
        AssertThat(nova.AppliedStatus).IsEqual(StatusType.StaticCharge);
        AssertThat(nova.StatusDuration > 0f).IsTrue();

        EnemyData rioter = Load("chrono_rioter");
        AssertObject(rioter.PrimaryAttack).IsNull(); // synthesized MeleeStrike laser-scythe
        AssertThat(rioter.HasEliteAbilities).IsFalse();

        EnemyData captain = Load("plasma_sabre_captain");
        AssertObject(captain.PrimaryAttack).IsNull(); // synthesized MeleeStrike
        EnemyAbilityData rush = captain.EliteAbilities[0];
        AssertThat(rush.Archetype).IsEqual(EnemyAbilityArchetype.ChargeDash);
        AssertThat(rush.DashSpeed > 0f).IsTrue();
        AssertThat(rush.DashDurationFrames >= 1).IsTrue();
    }

    [TestCase]
    public void RangedPrimariesMirrorTheirScalarFallbackFields() {
        // Convention from the Phase A migration: when an EnemyData authors a
        // PrimaryAttack, the legacy scalar fields mirror it so HUD/tuning reads
        // agree with what the executor actually runs.
        foreach (string enemyID in AllActOneIDs) {
            EnemyData data = Load(enemyID);
            EnemyAbilityData primary = data.PrimaryAttack;
            if (primary == null) continue;
            AssertThat(data.AttackDamage).IsEqual(primary.Damage);
            AssertThat(data.AttackKnockback).IsEqual(primary.KnockbackForce.X);
            AssertThat(data.AttackCooldown).IsEqual(primary.CooldownSeconds);
            AssertThat(data.AttackTelegraphFrames).IsEqual(primary.TelegraphFrames);
            AssertThat(data.AttackActiveFrames).IsEqual(primary.ActiveFrames);
            AssertThat(data.AttackRecoveryFrames).IsEqual(primary.RecoveryFrames);
        }
    }

    [TestCase]
    public void EveryActOneTranslationKeyExistsInTheEnglishTable() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }

        foreach (string enemyID in AllActOneIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(keys.Contains(data.DisplayNameKey)).IsTrue();
            foreach (EnemyAbilityData ability in AbilitiesOf(data)) {
                AssertThat(string.IsNullOrWhiteSpace(ability.DisplayNameKey)).IsFalse();
                AssertThat(keys.Contains(ability.DisplayNameKey)).IsTrue();
            }
        }
    }

    [TestCase]
    public void PlaceholderTintsStayDistinctAcrossTheAuthoredRoster() {
        // Plan Section 3.7: every roster entry reads distinctly without new art.
        var seen = new List<Color>();
        foreach (string enemyID in PhaseAIDs) seen.Add(Load(enemyID).PlaceholderTint);

        foreach (string enemyID in AllActOneIDs) {
            Color tint = Load(enemyID).PlaceholderTint;
            AssertThat(tint.A > 0f).IsTrue();
            foreach (Color other in seen) {
                float distance = Mathf.Abs(tint.R - other.R)
                    + Mathf.Abs(tint.G - other.G)
                    + Mathf.Abs(tint.B - other.B);
                AssertThat(distance > 0.25f).IsTrue();
            }
            seen.Add(tint);
        }
    }

    [TestCase]
    public void ActOneElitesAlternateStandardAndEliteAttacks() {
        // Design Section 6 / plan Section 3.2: elites cycle standard <-> elite once
        // the elite cooldown is up, so an elite never chains its signature move.
        foreach (string enemyID in EliteIDs) {
            EnemyController elite = CreateEnemy(enemyID);
            try {
                EnemyAbilityData first = elite.SelectNextAttack();
                AssertThat(elite.LastAttackWasElite).IsTrue();
                AssertThat(first.AbilityID == elite.Data.EliteAbilities[0].AbilityID).IsTrue();

                EnemyAbilityData second = elite.SelectNextAttack();
                AssertThat(elite.LastAttackWasElite).IsFalse();
                AssertThat(second.Archetype).IsEqual(EnemyAbilityArchetype.MeleeStrike);
                AssertThat(second.Damage).IsEqual(elite.Data.AttackDamage);

                // Still inside EliteAbilityCooldown a frame later: standard again.
                elite._PhysicsProcess(Step);
                EnemyAbilityData third = elite.SelectNextAttack();
                AssertThat(third.AbilityID == second.AbilityID).IsTrue();
            } finally {
                elite.Free();
            }
        }
    }

    private static EnemyData Load(string enemyID) =>
        FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");

    private static IEnumerable<EnemyAbilityData> AbilitiesOf(EnemyData data) {
        if (data.PrimaryAttack != null) yield return data.PrimaryAttack;
        if (data.EliteAbilities == null) yield break;
        foreach (EnemyAbilityData ability in data.EliteAbilities) {
            if (ability != null) yield return ability;
        }
    }

    /// <summary>
    /// EnemyFactory caches EnemyData by ID, so the canonical resource is duplicated
    /// before it reaches a live controller.
    /// </summary>
    private static EnemyController CreateEnemy(string enemyID) {
        var data = (EnemyData)Load(enemyID).Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        return enemy;
    }
}
