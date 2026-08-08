using System.Collections.Generic;
using System.IO;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 4 workstream B2 content contracts: the Act II west era roster
/// (Pompeii, Nassau, Egypt, Berlin). Stat bands come from
/// docs/PACKAGE4_ROSTER_PLAN.md Section 4.1 and the dust tiers from
/// docs/DUST_ECONOMY.md Section 1 (standards 1-2, elites exactly 10).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EnemyRosterActIIWestTests {
    private static readonly string[] StandardIDs = {
        "shock_shield_legionnaire", "laser_pistol_deckhand",
        "plasma_spear_ward", "infrared_border_sentry"
    };

    private static readonly string[] EliteIDs = {
        "cyber_centurion", "overcharged_cannon_master",
        "chrono_chariot_raider", "neural_mech_walker"
    };

    private const int EliteDustReward = 10;

    private static EnemyData Load(string enemyID) {
        EnemyData data = FTT.Core.AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        AssertObject(data).IsNotNull();
        // EnemyFactory caches EnemyData by ID; never mutate the shared instance.
        return data;
    }

    private static IEnumerable<string> AllIDs() {
        foreach (string id in StandardIDs) yield return id;
        foreach (string id in EliteIDs) yield return id;
    }

    [TestCase]
    public void EveryActIIWestEnemyLoadsWithAnIDMatchingItsFilename() {
        var ids = new HashSet<string>();
        foreach (string enemyID in AllIDs()) {
            EnemyData data = Load(enemyID);
            AssertThat(data.EnemyID).IsEqual(enemyID);
            AssertThat(ids.Add(data.EnemyID)).IsTrue();
            AssertThat(data.SchemaVersion).IsEqual(1);
            AssertThat(string.IsNullOrWhiteSpace(data.DisplayName)).IsFalse();
            AssertThat(data.MaxHP > 0).IsTrue();
            AssertThat(data.Weight > 0).IsTrue();
            AssertThat(data.MoveSpeed > 0).IsTrue();
            AssertThat(data.AttackDamage > 0).IsTrue();
            AssertThat(data.DeAggroRadius > data.AggroRadius).IsTrue();
            AssertThat(data.ReactionDelayMinFrames <= data.ReactionDelayMaxFrames).IsTrue();
            AssertThat(data.ItemDropChance).IsEqual(1.0f);
        }
        AssertThat(ids.Count).IsEqual(8);
    }

    [TestCase]
    public void StandardsSitInTheActIIStatBandAndDropOneOrTwoDust() {
        foreach (string enemyID in StandardIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.Tier).IsEqual(EnemyTier.Standard);
            // Plan Section 4.1: Act II standards 40-60 HP, slightly above the Act I anchors.
            AssertThat(data.MaxHP >= 40 && data.MaxHP <= 60).IsTrue();
            AssertThat(data.ChronalDustDrop >= 1 && data.ChronalDustDrop <= 2).IsTrue();
            AssertThat(data.HasEliteAbilities).IsFalse();
        }
    }

    [TestCase]
    public void ElitesSitInTheActIIStatBandWithExactlyTenDustAndStunResistance() {
        foreach (string enemyID in EliteIDs) {
            EnemyData data = Load(enemyID);
            AssertThat(data.Tier).IsEqual(EnemyTier.Elite);
            AssertThat(data.MaxHP >= 160 && data.MaxHP <= 220).IsTrue();
            // docs/DUST_ECONOMY.md Section 1: elite reward is flat 10.
            AssertThat(data.ChronalDustDrop).IsEqual(EliteDustReward);
            AssertThat(data.StunResistance >= 0.4f).IsTrue();
            AssertThat(data.HasEliteAbilities).IsTrue();
            AssertThat(data.EliteAbilities.Length >= 1).IsTrue();
            AssertThat(data.EliteAbilityCooldown > 0f).IsTrue();
        }
    }

    [TestCase]
    public void EveryAuthoredAbilityHasValidArchetypeFieldsAndAUniqueID() {
        var abilityIDs = new HashSet<string>();
        int authored = 0;
        foreach (string enemyID in AllIDs()) {
            EnemyData data = Load(enemyID);
            foreach (EnemyAbilityData ability in AbilitiesOf(data)) {
                authored++;
                AssertThat(ability.HasValidIdentity()).IsTrue();
                AssertThat(ability.HasValidArchetypeFields()).IsTrue();
                AssertThat(ability.AbilityID.StartsWith($"enemy.{enemyID}.")).IsTrue();
                AssertThat(abilityIDs.Add(ability.AbilityID)).IsTrue();
                AssertThat(string.IsNullOrWhiteSpace(ability.PresentationEventID)).IsFalse();
                AssertThat(ability.TotalFrames > 0).IsTrue();
                AssertThat(ability.CooldownSeconds > 0f).IsTrue();
            }
        }
        // 4 authored ranged primaries + 4 elite abilities across the eight entries;
        // the four melee primaries stay synthesized from the scalar EnemyData fields.
        AssertThat(authored).IsEqual(8);
    }

    [TestCase]
    public void EveryDisplayNameKeyResolvesInTheEnglishTable() {
        var keys = new HashSet<string>();
        foreach (string line in File.ReadAllLines("localization/en.csv")) {
            int comma = line.IndexOf(',');
            if (comma > 0) keys.Add(line[..comma]);
        }

        foreach (string enemyID in AllIDs()) {
            EnemyData data = Load(enemyID);
            AssertThat(data.DisplayNameKey).IsEqual($"enemy_{enemyID}_name");
            AssertThat(keys.Contains(data.DisplayNameKey)).IsTrue();
            foreach (EnemyAbilityData ability in AbilitiesOf(data)) {
                AssertThat(string.IsNullOrWhiteSpace(ability.DisplayNameKey)).IsFalse();
                AssertThat(keys.Contains(ability.DisplayNameKey)).IsTrue();
            }
        }
    }

    [TestCase]
    public void AuthoredArchetypesMatchTheRosterPlanIdentities() {
        // Plan Section 4.1: only the ranged/status primaries are authored; melee
        // primaries stay synthesized from the scalar EnemyData fields.
        AssertObject(Load("shock_shield_legionnaire").PrimaryAttack).IsNull();
        AssertObject(Load("cyber_centurion").PrimaryAttack).IsNull();
        AssertObject(Load("chrono_chariot_raider").PrimaryAttack).IsNull();
        AssertObject(Load("neural_mech_walker").PrimaryAttack).IsNull();

        // Pompeii: shield standard reduces frontal damage; centurion lobs a 3-shot volley.
        AssertThat(Load("shock_shield_legionnaire").FrontalDamageReduction).IsEqual(0.5f);
        EnemyAbilityData flares = Load("cyber_centurion").EliteAbilities[0];
        AssertThat(flares.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(flares.ProjectileCount).IsEqual(3);
        AssertThat(flares.ProjectileGravity > 0f).IsTrue();

        // Nassau: rapid low-damage pistol vs lobbed mortar + AreaPulse shell burst.
        EnemyData deckhand = Load("laser_pistol_deckhand");
        AssertThat(deckhand.PrimaryAttack.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(deckhand.PrimaryAttack.Damage < 10f).IsTrue();
        AssertThat(deckhand.PrimaryAttack.CooldownSeconds < 2f).IsTrue();
        AssertThat(deckhand.PrimaryAttack.RecoveryFrames <= 12).IsTrue();

        EnemyData cannonMaster = Load("overcharged_cannon_master");
        AssertThat(cannonMaster.PrimaryAttack.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(cannonMaster.PrimaryAttack.ProjectileGravity > 0f).IsTrue();
        EnemyAbilityData burst = cannonMaster.EliteAbilities[0];
        AssertThat(burst.Archetype).IsEqual(EnemyAbilityArchetype.AreaPulse);
        AssertThat(burst.PulseRadius > 0f).IsTrue();

        // Egypt: javelin standard; chariot charges fast and leaves RadiantBurn.
        AssertThat(Load("plasma_spear_ward").PrimaryAttack.Archetype)
            .IsEqual(EnemyAbilityArchetype.Projectile);
        EnemyData raider = Load("chrono_chariot_raider");
        EnemyAbilityData charge = raider.EliteAbilities[0];
        AssertThat(charge.Archetype).IsEqual(EnemyAbilityArchetype.ChargeDash);
        AssertThat(charge.AppliedStatus).IsEqual(FTT.Core.StatusType.RadiantBurn);
        AssertThat(charge.StatusDuration > 0f).IsTrue();
        AssertThat(charge.DashSpeed > 0f).IsTrue();
        // Fastest entry in the subset, per the plan's "high MoveSpeed" note.
        foreach (string enemyID in AllIDs()) {
            if (enemyID == "chrono_chariot_raider") continue;
            AssertThat(raider.MoveSpeed > Load(enemyID).MoveSpeed).IsTrue();
        }

        // Berlin: sniper standard telegraphs longest and hits hardest; walker volleys.
        EnemyData sentry = Load("infrared_border_sentry");
        AssertThat(sentry.PrimaryAttack.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(sentry.PrimaryAttack.TelegraphFrames >= 40).IsTrue();
        foreach (string enemyID in StandardIDs) {
            if (enemyID == "infrared_border_sentry") continue;
            EnemyData other = Load(enemyID);
            AssertThat(sentry.AttackDamage > other.AttackDamage).IsTrue();
            AssertThat(sentry.AggroRadius > other.AggroRadius).IsTrue();
        }
        EnemyAbilityData volley = Load("neural_mech_walker").EliteAbilities[0];
        AssertThat(volley.Archetype).IsEqual(EnemyAbilityArchetype.Projectile);
        AssertThat(volley.ProjectileCount > 1).IsTrue();
    }

    [TestCase]
    public void PlaceholderTintsAreDistinctAcrossTheSubset() {
        var tints = new HashSet<string>();
        foreach (string enemyID in AllIDs()) {
            Color tint = Load(enemyID).PlaceholderTint;
            AssertThat(tints.Add(tint.ToHtml())).IsTrue();
            // A pure-white tint reads as "unauthored" against the placeholder sprite.
            AssertThat(tint == Colors.White).IsFalse();
        }
    }

    private static IEnumerable<EnemyAbilityData> AbilitiesOf(EnemyData data) {
        if (data.PrimaryAttack != null) yield return data.PrimaryAttack;
        if (data.EliteAbilities == null) yield break;
        foreach (EnemyAbilityData ability in data.EliteAbilities) {
            if (ability != null) yield return ability;
        }
    }
}
