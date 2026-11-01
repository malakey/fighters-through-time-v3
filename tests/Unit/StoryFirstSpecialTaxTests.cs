using System.Collections.Generic;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D4 (user direction 2026-10-11, ledger <c>USER-FIRST-SPECIAL-UNTAXED</c> and
/// <c>PROVISIONAL-FIRST-SPECIAL-TAX-RULES</c>): the first Special in a Story mob's
/// stun chain lands untaxed (no S2 x0.6 and no chain decay on its hits) and
/// charges S3 standard poise x1.0; a second or later Special keeps the x0.6 and
/// the x1.5 poise cost. The re-derived standard budget (2.325 s) fits one full
/// string plus the largest first Special, so Joan's Righteous Smite -> hit 1 ->
/// hit 2 -> hit 3 never trips, while a second Special trips through the stun
/// arithmetic (there is no count rule). The pure rule is pinned in
/// <c>StoryEnemyCombatRulesTests</c>; these cases drive the real
/// <see cref="EnemyController"/> intake and the authored ability data.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryFirstSpecialTaxTests {
    private const float Step = 1f / 60f;
    private static readonly Vector2 WorldOrigin = new(-52000f, -4000f);

    // === Damage (S2 as amended) ===

    [TestCase]
    public void TheFirstSpecialInAChainLandsInFullAndASecondSpecialIsProrated() {
        // A standard and an elite: D4's damage rule covers both tiers.
        foreach (string enemyID in new[] { "chrono_slasher", "steam_automaton" }) {
            EnemyController enemy = CreateEnemy(enemyID, WorldOrigin);
            try {
                var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
                int hp = enemy.CurrentHP;
                int Dealt(HitPayload hit) {
                    hurtbox.TakeHit(hit);
                    int dealt = hp - enemy.CurrentHP;
                    hp = enemy.CurrentHP;
                    return dealt;
                }

                for (int step = 0; step < BasicComboRules.ComboHits; step++) Dealt(StringHit(step, damage: 1f));
                AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits);
                AssertThat(enemy.ChainFirstSpecialID).IsNull();

                // Chain hit 4: the chain's first Special lands at full value (the
                // pre-D4 rule cut it to ScaleIntakeDamage(8, 0.85 x 0.6) = 4).
                AssertThat(Dealt(Special(8f, 12, "test.special_a")))
                    .OverrideFailureMessage(enemyID + ": the chain's first Special must land untaxed.")
                    .IsEqual(8);
                AssertThat(enemy.ChainFirstSpecialID).IsEqual("test.special_a");

                // Chain hit 5: a second Special keeps the confirm cut and the decay.
                AssertThat(Dealt(Special(8f, 12, "test.special_b")))
                    .OverrideFailureMessage(enemyID + ": a second Special must stay prorated.")
                    .IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(
                        8, StoryEnemyCombatRules.ConfirmProrationScale(5, specialOnHitstun: true)));
                AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits + 2);
                AssertThat(enemy.ChainFirstSpecialID).IsEqual("test.special_a");
            } finally {
                enemy.Free();
            }
        }
    }

    [TestCase]
    public void TheFirstSpecialsHitsNeverDecayButStillCountTowardTheChain() {
        // 6-damage Specials (the fixture mob has headroom since CM-STORY 8.1).
        EnemyController enemy = CreateEnemy("chrono_slasher", WorldOrigin);
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            int hp = enemy.CurrentHP;
            int Dealt(HitPayload hit) {
                hurtbox.TakeHit(hit);
                int dealt = hp - enemy.CurrentHP;
                hp = enemy.CurrentHP;
                return dealt;
            }

            for (int step = 0; step < BasicComboRules.ComboHits; step++) Dealt(StringHit(step, damage: 1f));
            // A three-hit first Special at chain hits 4, 5 and 6: no x0.6, no decay.
            for (int hit = 0; hit < 3; hit++) {
                AssertThat(Dealt(Special(6f, 12, "test.special_a")))
                    .OverrideFailureMessage($"The first Special's hit {hit + 1} must land in full.")
                    .IsEqual(6);
            }
            // Its hits still advance the chain (the minimal reading of D4).
            AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits + 3);
            AssertThat(enemy.IsArmoredRecovery).IsFalse();

            // So the next non-Special is chain hit 7 and decays as before.
            AssertThat(Dealt(Hit(AttackClass.Basic, "turret_shot", 6f, hitstunFrames: 0, attackID: "test.turret")))
                .IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(
                    6, StoryEnemyCombatRules.ConfirmProrationScale(7, specialOnHitstun: false)));
            AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits + 4);
        } finally {
            enemy.Free();
        }
    }

    // === Standard poise (S3 as amended) ===

    [TestCase]
    public void RighteousSmiteIntoAFullStringFitsAndASecondSpecialThenTrips() {
        AbilityData smite = AuthoredResources.Load<AbilityData>("res://resources/Abilities/joan/special_1.tres");
        AssertThat(smite.AbilityID).IsEqual("joan_righteous_smite");
        EnemyController enemy = CreateEnemy("chrono_slasher", WorldOrigin);
        try {
            AssertThat(enemy.Data.StunResistance).IsEqual(0f);
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");

            // Joan's design route (design :2522): Smite -> hit 1 -> hit 2 -> hit 3.
            hurtbox.TakeHit(Special(1f, smite.HitstunFrames, smite.AbilityID));
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                hurtbox.TakeHit(StringHit(step, damage: 1f));
                AssertThat(enemy.IsArmoredRecovery)
                    .OverrideFailureMessage("Smite into a full string tripped poise at hit " + (step + 1))
                    .IsFalse();
                AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
                for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            }
            int routeFrames = smite.HitstunFrames + StringFrames();
            AssertThat(routeFrames).IsEqual(132);
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(routeFrames / 60f, 0.001f);

            // A second Special in the same chain trips through the arithmetic:
            // 12 frames, V7.4-diminished to 6, taxed x1.5 = 9; 132 + 9 > 139.5.
            hurtbox.TakeHit(Special(1f, 12, "test.second_special"));
            AssertThat(enemy.IsArmoredRecovery)
                .OverrideFailureMessage("A second Special after the ruled route must trip the standard's poise.")
                .IsTrue();
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Attacking);
            AssertThat(enemy.ActiveAbility).IsNotNull();
            AssertThat(enemy.ConfirmChainHits).IsEqual(BasicComboRules.ComboHits + 2);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void AStringThenSmiteFitsButASecondStringInTheSameChainTrips() {
        EnemyController enemy = CreateEnemy("chrono_slasher", WorldOrigin);
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            // hit 1 -> hit 2 -> a 30-frame first Special: 34 + 44 + 30 = 108 fits
            // (the pre-D4 x1.5 charged 123 frames and tripped a 114-frame budget).
            for (int step = 0; step < 2; step++) {
                hurtbox.TakeHit(StringHit(step, damage: 1f));
                for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            }
            hurtbox.TakeHit(Special(1f, 30, "joan_righteous_smite"));
            AssertThat(enemy.IsArmoredRecovery)
                .OverrideFailureMessage("hit 1 -> hit 2 -> a first Special must fit a standard's poise.")
                .IsFalse();
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(108f / 60f, 0.001f);
            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);

            // A restarted string in the same chain (the D3 recovery cancel's hit 1)
            // trips at 108 + 34 = 142 frames: poise allows one string plus one
            // Special per chain (PROVISIONAL-FIRST-SPECIAL-TAX-RULES, accepted).
            hurtbox.TakeHit(StringHit(0, damage: 1f));
            AssertThat(enemy.IsArmoredRecovery)
                .OverrideFailureMessage("A second string inside the same chain must trip the standard's poise.")
                .IsTrue();
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void AMovementAbilityHitNeverTakesTheFirstSpecialSlot() {
        AbilityData railCharge = AuthoredResources.Load<AbilityData>("res://resources/Abilities/lincoln/movement.tres");
        AbilityData smite = AuthoredResources.Load<AbilityData>("res://resources/Abilities/joan/special_1.tres");
        AssertThat(railCharge.Slot).IsEqual(AbilitySlot.MovementAbility);
        AssertThat(railCharge.Origin).IsEqual(HitOrigin.Special);

        // Both stamping paths mark a movement ability's hit, and only that.
        AssertThat(BaseSpecial.WithAbilityContract(new HitPayload { AttackClass = AttackClass.Special },
            railCharge, null).SourceIsMovementAbility).IsTrue();
        AssertThat(BaseSpecial.WithAbilityContract(new HitPayload { AttackClass = AttackClass.Special },
            smite, null).SourceIsMovementAbility).IsFalse();
        var hitbox = new Hitbox { Name = "FirstSpecialTaxHitbox" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(hitbox);
        EnemyController enemy = null;
        try {
            hitbox.ApplyAbilityHitContract(railCharge);
            AssertThat(hitbox.CreatePayload(1).SourceIsMovementAbility).IsTrue();
            hitbox.ApplyAbilityHitContract(smite);
            AssertThat(hitbox.CreatePayload(1).SourceIsMovementAbility).IsFalse();

            enemy = CreateEnemy("chrono_slasher", WorldOrigin);
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            int hp = enemy.CurrentHP;

            // A Rail Charge hit opens the chain: full damage (it is chain hit 1,
            // as before D4), but the pre-D4 x1.5 poise cost, and no slot claimed.
            HitPayload rail = Special(6f, 12, railCharge.AbilityID);
            rail.SourceIsMovementAbility = true;
            hurtbox.TakeHit(rail);
            AssertThat(hp - enemy.CurrentHP).IsEqual(6);
            hp = enemy.CurrentHP;
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.ChainFirstSpecialID).IsNull();
            float railCharge12 = 12 / 60f * StoryEnemyCombatRules.StandardSpecialStunBudgetCostMultiplier;
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(railCharge12, 0.001f);

            // The real Special after it claims the slot and lands in full (it is a
            // confirm on the Rail Charge's hitstun, which the pre-D4 rule cut x0.6).
            hurtbox.TakeHit(Special(8f, 12, "test.special_a"));
            AssertThat(hp - enemy.CurrentHP)
                .OverrideFailureMessage("The Special after a movement hit must be the chain's untaxed first Special.")
                .IsEqual(8);
            AssertThat(enemy.ChainFirstSpecialID).IsEqual("test.special_a");
            // Its stun is V7.4-diminished (6 frames) and charged x1.0.
            AssertThat(enemy.StaggerBudgetSeconds)
                .IsEqualApprox(railCharge12 + 12 * EnemyStaggerRules.SpecialStunDiminishFactor / 60f, 0.001f);
        } finally {
            hitbox.Free();
            enemy?.Free();
        }
    }

    [TestCase]
    public void ASpecialsLaunchingTickCanBeTheChainsFirstSpecial() {
        EnemyController enemy = CreateEnemy("chrono_slasher", WorldOrigin);
        try {
            // The Sandstorm Vortex's final tick: a Special-class Tick with the
            // authored 12 frames of hitstun. It is proration-exempt (no scale, no
            // chain step) but it is classified, so it claims the slot and charges
            // poise x1.0 (the pre-D4 rule charged it x1.5).
            HitPayload tick = Special(4f, 12, "cleopatra_sandstorm_vortex");
            tick.Delivery = HitDelivery.Tick;
            int hp = enemy.CurrentHP;
            enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(tick);
            AssertThat(hp - enemy.CurrentHP).IsEqual(4);
            AssertThat(enemy.CurrentState).IsEqual(EnemyState.Stunned);
            AssertThat(enemy.ConfirmChainHits).IsEqual(0);
            AssertThat(enemy.ChainFirstSpecialID).IsEqual("cleopatra_sandstorm_vortex");
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(12 / 60f, 0.001f);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void TheGetupDrainClearsAFullFirstSpecialChainSoTheNextChainStartsEmpty() {
        EnemyController enemy = CreateEnemy("chrono_slasher", WorldOrigin);
        try {
            var hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            // Chain one: Smite -> hit 1 -> hit 2 -> hit 3 (132 frames, no trip).
            hurtbox.TakeHit(Special(1f, 30, "joan_righteous_smite"));
            for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                hurtbox.TakeHit(StringHit(step, damage: 1f));
                AssertThat(enemy.IsArmoredRecovery).IsFalse();
                for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            }
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(132f / 60f, 0.001f);

            // Natural expiry ends the chain, and the V7.4 getup armor's 0.6 s at
            // the standard's 4.0 s/s drain clears the whole credit.
            int guard = 0;
            while (enemy.CurrentState == EnemyState.Stunned && guard++ < 240) enemy._PhysicsProcess(Step);
            AssertThat(enemy.CurrentState).IsNotEqual(EnemyState.Stunned);
            AssertThat(enemy.ChainFirstSpecialID).IsNull();
            AssertThat(enemy.ConfirmChainHits).IsEqual(0);
            guard = 0;
            while (enemy.IsGetupArmored && guard++ < 120) enemy._PhysicsProcess(Step);
            AssertThat(enemy.StaggerBudgetSeconds).IsEqualApprox(0f, 0.0001f);

            // Chain two starts empty: a string plus a new first Special fits.
            for (int step = 0; step < BasicComboRules.ComboHits; step++) {
                hurtbox.TakeHit(StringHit(step, damage: 1f));
                AssertThat(enemy.IsArmoredRecovery)
                    .OverrideFailureMessage("The second chain's string tripped poise at hit " + (step + 1))
                    .IsFalse();
                for (int frame = 0; frame < 6; frame++) enemy._PhysicsProcess(Step);
            }
            hurtbox.TakeHit(Special(1f, 30, "joan_divine_piercing"));
            AssertThat(enemy.IsArmoredRecovery)
                .OverrideFailureMessage("The second chain's first Special must fit its poise.")
                .IsFalse();
            AssertThat(enemy.ChainFirstSpecialID).IsEqual("joan_divine_piercing");
        } finally {
            enemy.Free();
        }
    }

    // === The budget's design bound, recomputed from the authored kits ===

    /// <summary>
    /// For every authored Special (both slots of every roster character), the
    /// worst-case standard-poise charge it puts on a chain as the chain's
    /// <b>first</b> Special, and the smallest it puts there as a <b>second</b>
    /// one, from a documented per-kit hit model that reads only authored data
    /// and named kit constants.
    ///
    /// <para>The model: every Special-class stunning hit one execution can land
    /// on one mob, in landing order. The first hit charges its full hitstun;
    /// every later one is V7.4-diminished (x0.5, another Special-sourced stun
    /// landed inside 4 s). A non-Special stun the Special causes (Kinetic
    /// Splitting's ground bounce, through the public <c>ApplyStun</c>) charges
    /// x1.0, undiminished. As a second Special every Special-class hit is
    /// diminished and taxed x1.5. Constructs (nest, coil, turret) deliver
    /// Basic-class hits, so they are no Special in a chain at all.</para>
    /// </summary>
    [TestCase]
    public void NoFirstSpecialChargesMoreStandardPoiseThanTheAllowance() {
        var issues = new List<string>();
        AbilityData teslaCoil = AuthoredResources.Load<AbilityData>("res://resources/Abilities/tesla/special_1.tres");
        AbilityData smite = AuthoredResources.Load<AbilityData>("res://resources/Abilities/joan/special_1.tres");
        AssertThat(teslaCoil.AbilityID).IsEqual("tesla_tesla_coil");
        AssertThat(smite.AbilityID).IsEqual("joan_righteous_smite");
        float diminish = EnemyStaggerRules.SpecialStunDiminishFactor;
        float tax = StoryEnemyCombatRules.StandardSpecialStunBudgetCostMultiplier;
        // The smallest stunning second Special: one 12-frame hit, diminished and taxed.
        float smallestSecondFrames = 12 * diminish * tax;
        AssertThat(smallestSecondFrames).IsEqualApprox(9f, 0.0001f);

        int examined = 0;
        float worstFirst = 0f;
        foreach (string characterID in CharacterRoster.IDs) {
            CharacterData character = AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            foreach (AbilityData special in new[] { character.SpecialAttackOne, character.SpecialAttackTwo }) {
                examined++;
                if (special == null) {
                    issues.Add($"{characterID}: missing Special");
                    continue;
                }
                if (!TryModel(special, teslaCoil, out int[] specialHits, out int extraFrames, out bool construct)) {
                    issues.Add($"{special.AbilityID}: no first-Special poise model row");
                    continue;
                }
                if (construct) {
                    if (special.Delivery != HitDelivery.Construct) {
                        issues.Add($"{special.AbilityID}: modelled as a construct but authors {special.Delivery}");
                    }
                    continue;
                }
                float first = extraFrames;
                float second = extraFrames;
                for (int hit = 0; hit < specialHits.Length; hit++) {
                    first += hit == 0 ? specialHits[hit] : specialHits[hit] * diminish;
                    second += specialHits[hit] * diminish * tax;
                }
                if (first > worstFirst) worstFirst = first;
                if (first > StoryEnemyCombatRules.FirstSpecialStunAllowanceFrames + 0.0001f) {
                    issues.Add($"{special.AbilityID}: first-Special charge {first} f exceeds the allowance "
                        + $"{StoryEnemyCombatRules.FirstSpecialStunAllowanceFrames} f");
                }
                bool stuns = specialHits.Length > 0 || extraFrames > 0;
                if (stuns && second < smallestSecondFrames - 0.0001f) {
                    issues.Add($"{special.AbilityID}: second-Special charge {second} f is under {smallestSecondFrames} f");
                }
            }
        }
        AssertThat(examined).IsEqual(CharacterRoster.Count * 2);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(worstFirst > 0f).IsTrue();

        float budgetFrames = StoryEnemyCombatRules.StandardStaggerBudgetSeconds * 60f;
        int stringFrames = StringFrames();
        AssertThat(stringFrames + StoryEnemyCombatRules.FirstSpecialStunAllowanceFrames + 1 <= budgetFrames)
            .OverrideFailureMessage("A string plus the largest first Special must fit with a frame to spare.")
            .IsTrue();
        AssertThat(stringFrames + smite.HitstunFrames + smallestSecondFrames > budgetFrames + 1)
            .OverrideFailureMessage("Smite -> string plus any second Special must trip with a frame to spare.")
            .IsTrue();
    }

    /// <summary>
    /// The per-kit hit model (<see cref="NoFirstSpecialChargesMoreStandardPoiseThanTheAllowance"/>).
    /// False for an ability with no row, so a new Special cannot skip the guard.
    /// </summary>
    private static bool TryModel(AbilityData data, AbilityData teslaCoil,
        out int[] specialHits, out int extraFrames, out bool construct) {
        int hs = data.HitstunFrames;
        extraFrames = 0;
        construct = false;
        var hits = new List<int>();
        switch (data.AbilityID) {
            case "einstein_mass_energy_conversion":
                // The shot's contact, the burst, and the Rift collapse when the
                // burst lands inside his own rift (it rides this execution, E01).
                if (data.ProjectileContactDamage > 0f) hits.Add(hs);
                hits.Add(hs);
                hits.Add(EinsteinRelativityRift.CollapseLaunchHitstunFrames);
                break;
            case "einstein_relativity_rift":
                // Its ticks reach mobs through TakeDamage(int) with no payload; its
                // collapse belongs to E=mc2. It never stuns as its own Special.
                break;
            case "joan_righteous_smite":
            case "lincoln_emancipator":
            case "mozart_fortissimo_wave":
            case "tubman_conductors_call":
            case "tubman_foresight": // the lantern answer
                hits.Add(hs);
                break;
            case "joan_divine_piercing":
                for (int hit = 0; hit < data.HitCount; hit++) hits.Add(hs);
                break;
            case "leonardo_golden_ratio":
                for (int hit = 0; hit < data.HitCount - 1; hit++) hits.Add(LeonardoGoldenRatio.ChurnTickHitstunFrames);
                hits.Add(hs);
                break;
            case "lincoln_splitting_strike":
                // The strike, plus Kinetic Splitting's ground bounce (Story perk).
                hits.Add(hs);
                extraFrames = LincolnSplittingStrike.GroundBounceWindowFrames;
                break;
            case "mozart_requiem_chord":
                // The chord's contact, its pulses, and the Requiem Crescendo pulse.
                hits.Add(hs);
                for (int pulse = 0; pulse < data.HitCount; pulse++) hits.Add(hs);
                hits.Add(hs);
                break;
            case "shakespeare_yoricks_lament":
                if (data.ProjectileContactDamage > 0f) hits.Add(hs);
                hits.Add(hs);
                break;
            case "shakespeare_the_tempest":
                // A positional windbox: it builds no hit payload at all.
                break;
            case "tesla_lorentz_pulse":
                // The pulse. Its coil discharge bursts (lingering-kit pass) are construct strikes,
                // exempt from standard poise, so they add nothing here.
                hits.Add(hs);
                break;
            case "cleopatra_sandstorm_vortex":
                // Only the final tick carries hitstun.
                hits.Add(hs);
                break;
            case "cleopatra_serpent_nest":
            case "tesla_tesla_coil":
            case "leonardo_clockwork_turret":
                construct = true;
                break;
            default:
                specialHits = System.Array.Empty<int>();
                return false;
        }
        specialHits = hits.ToArray();
        return true;
    }

    // === Helpers ===

    private static int StringFrames() =>
        StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_1")
        + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_2")
        + StoryEnemyCombatRules.BasicStringStunFloorFrames("combo_3");

    private static HitPayload Special(float damage, int hitstunFrames, string attackID) =>
        Hit(AttackClass.Special, "special_test", damage, hitstunFrames, attackID);

    private static HitPayload Hit(AttackClass attackClass, string hitboxID, float damage, int hitstunFrames,
        string attackID) => new() {
            AttackerIndex = 0,
            AttackID = attackID,
            HitboxID = hitboxID,
            AttackClass = attackClass,
            Origin = attackClass == AttackClass.Special ? HitOrigin.Special : HitOrigin.Basic,
            Delivery = HitDelivery.DirectHit,
            Damage = damage,
            HitstunDuration = hitstunFrames / 60f,
            // Behind a right-facing mob, so no frontal reduction can apply.
            HitOrigin = new Vector2(-100000f, 0f),
            AttackerFacingRight = true,
            ContactId = HitClassification.NextContactId()
        };

    private static HitPayload StringHit(int step, float damage) => new() {
        AttackerIndex = 0,
        AttackID = "joan.basic",
        HitboxID = $"combo_{step + 1}",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = BasicComboRules.HitstunFrames[step] / 60f,
        Launches = BasicComboRules.StringHitLaunches[step],
        HitOrigin = new Vector2(-100000f, 0f),
        AttackerFacingRight = true,
        ContactId = HitClassification.NextContactId()
    };

    private static EnemyController CreateEnemy(string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        // CM-STORY (8.1, 2026-10-16): the standard basic floor lifts each
        // 1-damage string fixture hit to 6, which took several cases to within a
        // hit of a Normal 40 HP and past an Easy 28. No case here is about a kill,
        // so every fixture mob gets headroom on any difficulty.
        enemy.CurrentHP = 999;
        return enemy;
    }
}
