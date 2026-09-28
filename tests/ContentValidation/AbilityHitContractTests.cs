using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// Package 12 W3 — M08, the ability and hit data contract. Pins that all 36
/// ability resources author <c>HitstunFrames</c> / <c>BlockClass</c> /
/// <c>Launches</c> / <c>Delivery</c> / <c>Origin</c> with the values derived
/// from what the build did before the fields existed (slot-derived class and
/// origin, 0.2 s hitstun → 12 frames, knockback ⇒ launch), that the deprecated
/// seconds reader derives from the frames, that hitboxes and the Fighter
/// loadout carry the contract, and that the Story receivers implement
/// <see cref="IDamageable"/>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AbilityHitContractTests {

    private static readonly string[] Roster = {
        "einstein", "joan", "leonardo", "lincoln", "cleopatra",
        "tesla", "shakespeare", "mozart", "pocahontas"
    };
    private static readonly string[] Slots = { "special_1", "special_2", "movement", "ultimate" };

    // Delivery is authored from the kit's actual damage path: the four
    // constructs, and the two zone-tick abilities plus the Vortex (D03g names
    // every Vortex tick, its final launching tick included, as a Tick).
    private static readonly HashSet<string> TickAbilities = new() {
        "einstein_relativity_rift", "shakespeare_the_tempest", "cleopatra_sandstorm_vortex"
    };

    [TestCase]
    public void EveryAbilityAuthorsTheDerivedHitContract() {
        var issues = new List<string>();
        int checkedCount = 0;
        foreach (string character in Roster) {
            foreach (string slot in Slots) {
                string path = $"res://resources/Abilities/{character}/{slot}.tres";
                AbilityData data = AuthoredResources.Load<AbilityData>(path);
                if (data == null) { issues.Add($"{path} did not load"); continue; }
                checkedCount++;
                bool ultimate = data.Slot == AbilitySlot.Ultimate;
                int expectedHitstun = data.AbilityID == "lincoln_union_indestructible" ? 30 : 12;
                if (data.HitstunFrames != expectedHitstun) issues.Add($"{data.AbilityID} HitstunFrames {data.HitstunFrames}");
                if (data.BlockClass != (ultimate ? BlockClass.Unblockable : BlockClass.Special)) issues.Add($"{data.AbilityID} BlockClass {data.BlockClass}");
                if (data.Origin != (ultimate ? HitOrigin.Ultimate : HitOrigin.Special)) issues.Add($"{data.AbilityID} Origin {data.Origin}");
                HitDelivery expectedDelivery = data.ExecutionType == AbilityExecutionType.PersistentObject
                    ? HitDelivery.Construct
                    : TickAbilities.Contains(data.AbilityID) ? HitDelivery.Tick : HitDelivery.DirectHit;
                if (data.Delivery != expectedDelivery) issues.Add($"{data.AbilityID} Delivery {data.Delivery}");
                bool expectedLaunch = data.KnockbackForce != Vector2.Zero;
                if (data.Launches != expectedLaunch) issues.Add($"{data.AbilityID} Launches {data.Launches}");
                // The runtime class is the one mapping of the pair — it must
                // reproduce the old slot-derived class exactly.
                AttackClass expectedClass = ultimate ? AttackClass.Ultimate : AttackClass.Special;
                if (data.ResolvedAttackClass != expectedClass) issues.Add($"{data.AbilityID} class {data.ResolvedAttackClass}");
            }
        }
        AssertThat(checkedCount).IsEqual(36);
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheSecondsReaderDerivesFromTheFrameCount() {
        var data = new AbilityData { HitstunFrames = 30 };
        AssertThat(data.HitstunDuration).IsEqualApprox(0.5f, 0.0001f);
        data.HitstunFrames = 12;
        AssertThat(data.HitstunDuration).IsEqualApprox(0.2f, 0.0001f);
        // The seconds field is no longer an authored export.
        foreach (Godot.Collections.Dictionary property in data.GetPropertyList()) {
            AssertThat((string)property["name"]).IsNotEqual("HitstunDuration");
        }
    }

    [TestCase]
    public void AHitboxCarriesTheAuthoredContractIntoItsPayload() {
        var ultimate = new AbilityData {
            AbilityID = "test.ult", Slot = AbilitySlot.Ultimate, HitstunFrames = 30,
            BlockClass = BlockClass.Unblockable, Launches = true,
            Delivery = HitDelivery.DirectHit, Origin = HitOrigin.Ultimate
        };
        var construct = new AbilityData {
            AbilityID = "test.construct", HitstunFrames = 9, BlockClass = BlockClass.GuardCrush,
            Launches = false, Delivery = HitDelivery.Construct, Origin = HitOrigin.Special
        };
        var hitbox = new Hitbox();
        try {
            hitbox.ApplyAbilityHitContract(ultimate);
            HitPayload payload = hitbox.CreatePayload(-1);
            AssertThat(payload.AttackClass).IsEqual(AttackClass.Ultimate);
            AssertThat(payload.Origin).IsEqual(HitOrigin.Ultimate);
            AssertThat(payload.Launches).IsTrue();
            AssertThat(payload.HitstunDuration).IsEqualApprox(0.5f, 0.0001f);
            AssertThat(payload.Unblockable)
                .OverrideFailureMessage("An Ultimate bypasses block through its class, not the boss-only flag.")
                .IsFalse();

            hitbox.ApplyAbilityHitContract(construct);
            payload = hitbox.CreatePayload(-1);
            AssertThat(payload.AttackClass).IsEqual(AttackClass.Special);
            AssertThat(payload.BlockChargeCost).IsEqual(2);
            AssertThat(payload.Delivery).IsEqual(HitDelivery.Construct);
            AssertThat(payload.Origin).IsEqual(HitOrigin.Special);
            AssertThat(payload.Launches).IsFalse();
        } finally {
            hitbox.Free();
        }
    }

    [TestCase]
    public void ThePureClassificationRulesAreTheOnlyMapping() {
        AssertThat(HitClassification.CollectsEcho(HitDelivery.DirectHit)).IsTrue();
        AssertThat(HitClassification.CollectsEcho(HitDelivery.Tick)).IsFalse();
        AssertThat(HitClassification.CollectsEcho(HitDelivery.Construct)).IsFalse();
        AssertThat(HitClassification.CollectsEcho(HitDelivery.Hazard)).IsFalse();
        AssertThat(HitClassification.IsUltimateOrigin(HitOrigin.Ultimate)).IsTrue();
        AssertThat(HitClassification.IsUltimateOrigin(HitOrigin.Special)).IsFalse();
        AssertThat(HitClassification.AttackClassFor(BlockClass.Basic, HitOrigin.Basic)).IsEqual(AttackClass.Basic);
        AssertThat(HitClassification.AttackClassFor(BlockClass.Special, HitOrigin.Special)).IsEqual(AttackClass.Special);
        AssertThat(HitClassification.AttackClassFor(BlockClass.Unblockable, HitOrigin.Ultimate)).IsEqual(AttackClass.Ultimate);
        AssertThat(HitClassification.BlockChargeCostFor(BlockClass.GuardCrush)).IsEqual(2);
        AssertThat(HitClassification.BlockChargeCostFor(BlockClass.Special)).IsEqual(0);
        ulong first = HitClassification.NextContactId();
        AssertThat(HitClassification.NextContactId() > first).IsTrue();
        // The basic string's launch table: only the finisher launches (M05).
        AssertThat(BasicComboRules.StringHitLaunches[0]).IsFalse();
        AssertThat(BasicComboRules.StringHitLaunches[1]).IsFalse();
        AssertThat(BasicComboRules.StringHitLaunches[2]).IsTrue();
        AssertThat(BasicComboRules.DirectionalAttackLaunches).IsTrue();
        AssertThat(BasicComboRules.ThrowLaunches).IsTrue();
    }

    [TestCase]
    public void TheFighterLoadoutProjectsTheAuthoredContractForTheWholeRoster() {
        var issues = new List<string>();
        foreach (string character in Roster) {
            CharacterData data = AuthoredResources.Load<CharacterData>(
                $"res://resources/Characters/{character}_data.tres");
            FighterAbilityLoadout modes = FighterLoadoutFactory.FromCharacterData(data).AbilityModes;
            Compare(character + ".s1", data.SpecialAttackOne, modes.SpecialOneHit, issues);
            Compare(character + ".s2", data.SpecialAttackTwo, modes.SpecialTwoHit, issues);
            Compare(character + ".ult", data.UltimateAttack, modes.UltimateHit, issues);
            Compare(character + ".move", data.MovementAbility, modes.MovementHit, issues);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void TheStoryReceiversImplementIDamageable() {
        AssertThat(typeof(IDamageable).IsAssignableFrom(typeof(PlayerController))).IsTrue();
        AssertThat(typeof(IDamageable).IsAssignableFrom(typeof(FTT.Enemies.EnemyController))).IsTrue();
        AssertThat(typeof(IDamageable).IsAssignableFrom(typeof(FTT.Enemies.BossController))).IsTrue();
        AssertThat(typeof(IDamageable).IsAssignableFrom(typeof(FTT.Environment.EnvironmentHurtboxAdapter))).IsTrue();
        // IStatusEffectTarget coexists with it; the three controllers carry both.
        AssertThat(typeof(IStatusEffectTarget).IsAssignableFrom(typeof(PlayerController))).IsTrue();

        // The interface routes through the same chokepoint the hurtbox feeds.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            AssertThat(((IDamageable)player).IsAlive).IsTrue();
            int hp = player.CurrentHP;
            float dealt = ((IDamageable)player).TakeDamage(new HitPayload {
                AttackerIndex = 1,
                AttackID = "test.hit",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 7f,
                HitOrigin = player.GlobalPosition + new Vector2(40f, 0f),
                Origin = HitOrigin.Basic
            });
            AssertThat(dealt).IsEqualApprox(7f, 0.001f);
            AssertThat(player.CurrentHP).IsEqual(hp - 7);
        } finally {
            player.Free();
        }
    }

    private static void Compare(string label, AbilityData data, FighterAbilityHitData projected, List<string> issues) {
        if (data == null) { issues.Add(label + " missing"); return; }
        if (projected.HitstunFrames != data.HitstunFrames) issues.Add(label + " hitstun");
        if (projected.BlockClass != (int)data.BlockClass) issues.Add(label + " block");
        if (projected.Launches != data.Launches) issues.Add(label + " launches");
        if (projected.Delivery != (int)data.Delivery) issues.Add(label + " delivery");
        if (projected.Origin != (int)data.Origin) issues.Add(label + " origin");
    }
}
