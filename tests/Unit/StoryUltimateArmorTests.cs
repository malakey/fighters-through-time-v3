using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-STORY (combat and movement update 2026-10-16, decision 2.4 with the Q2
/// ruling "startup only"; findings CMB-01, TWO-06; ledger
/// <c>USER-STORY-ULTIMATE-ARMOR</c> / <c>PROVISIONAL-STORY-ULTIMATE-ARMOR</c>):
/// a Story Ultimate is paid at acceptance, and a mob or boss stun in its startup
/// used to throw the meter away. The hero's Ultimate is now armoured while its
/// own cast clock is in Startup (a Story rule, <see cref="StoryCombatRules.UltimateStartupArmorPreventsInterruption"/>);
/// an Ultimate-class hit still interrupts it, and so does any stun once it is
/// Active. A hit's StaticCharge no longer slips a stun past an armoured cast.
/// The nine <c>ultimate.tres</c> keep <c>GrantsHyperArmor</c> false, so Fighter
/// Mode is untouched.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryUltimateArmorTests {
    private const double Tick = 1.0 / 60.0;

    [TestCase]
    public void EveryRosterHerosUltimateIsArmouredForExactlyItsStartup() {
        var issues = new List<string>();
        foreach (string heroID in CharacterRoster.IDs) {
            (PlayerController hero, Node host) = Spawn(heroID);
            try {
                BaseSpecial ultimate = StartUltimate(hero);
                if (ultimate == null) {
                    issues.Add($"{heroID}: the Ultimate did not start");
                    continue;
                }
                int armouredStartup = 0, startup = 0, active = 0;
                for (int frame = 0; frame < 400 && ultimate.IsExecuting && active < 6; frame++) {
                    AbilityPhase phase = ultimate.CurrentPhase;
                    bool armoured = hero.HasActiveHyperArmorAgainst(AttackClass.Basic);
                    bool armouredSpecial = hero.HasActiveHyperArmorAgainst(AttackClass.Special);
                    if (phase == AbilityPhase.Startup) {
                        startup++;
                        if (armoured && armouredSpecial) armouredStartup++;
                    } else if (phase == AbilityPhase.Active) {
                        active++;
                        if (armoured || armouredSpecial) issues.Add($"{heroID}: armoured on Active frame {active}");
                    }
                    if (hero.HasActiveHyperArmorAgainst(AttackClass.Ultimate)) {
                        issues.Add($"{heroID}: armoured against an Ultimate in {phase}");
                    }
                    ultimate._PhysicsProcess(Tick);
                }
                if (startup != ultimate.Data.StartupFrames) {
                    issues.Add($"{heroID}: {startup} Startup frames, authored {ultimate.Data.StartupFrames}");
                }
                if (armouredStartup != startup) issues.Add($"{heroID}: armoured {armouredStartup} of {startup} Startup frames");
                if (active == 0) issues.Add($"{heroID}: the cast never reached Active");
            } finally {
                Free(hero, host);
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void ABasicStunInStartupDealsDamageButTheSameStunInActiveInterrupts() {
        (PlayerController hero, Node host) = Spawn("einstein");
        try {
            BaseSpecial ultimate = StartUltimate(hero);
            AssertThat(ultimate).IsNotNull();
            for (int frame = 0; frame < 5; frame++) ultimate._PhysicsProcess(Tick);
            AssertThat(ultimate.CurrentPhase).IsEqual(AbilityPhase.Startup);

            int hp = hero.CurrentHP;
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(AttackClass.Basic));
            AssertThat(hero.CurrentHP < hp).OverrideFailureMessage("The armoured hit must still deal its damage.").IsTrue();
            AssertThat(hero.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(ultimate.IsExecuting)
                .OverrideFailureMessage("A Basic stun in the Ultimate's startup must not interrupt it.")
                .IsTrue();

            for (int frame = 0; frame < 60 && ultimate.CurrentPhase == AbilityPhase.Startup; frame++) {
                ultimate._PhysicsProcess(Tick);
            }
            AssertThat(ultimate.CurrentPhase).IsEqual(AbilityPhase.Active);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(AttackClass.Basic));
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(ultimate.IsExecuting)
                .OverrideFailureMessage("Once Active, the same stun interrupts the Ultimate.")
                .IsFalse();
        } finally {
            Free(hero, host);
        }
    }

    [TestCase]
    public void AnUltimateClassHitStillInterruptsTheStartup() {
        (PlayerController hero, Node host) = Spawn("joan");
        try {
            BaseSpecial ultimate = StartUltimate(hero);
            AssertThat(ultimate).IsNotNull();
            for (int frame = 0; frame < 3; frame++) ultimate._PhysicsProcess(Tick);
            AssertThat(ultimate.CurrentPhase).IsEqual(AbilityPhase.Startup);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(AttackClass.Ultimate));
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(ultimate.IsExecuting).IsFalse();
        } finally {
            Free(hero, host);
        }
    }

    /// <summary>
    /// The StaticCharge leak: a hit's charge used to be applied after the armour
    /// gate and stun through it. An armoured body (the Ultimate's startup, and
    /// Lincoln's armoured Rail Charge) now takes the charge into its control slot
    /// without the stun; an unarmoured one is still stunned by it.
    /// </summary>
    [TestCase]
    public void AStaticChargeBearingHitDoesNotInterruptAnArmouredCast() {
        (PlayerController hero, Node host) = Spawn("tesla");
        try {
            BaseSpecial ultimate = StartUltimate(hero);
            AssertThat(ultimate).IsNotNull();
            for (int frame = 0; frame < 4; frame++) ultimate._PhysicsProcess(Tick);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(StaticChargeHit());
            AssertThat(hero.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(ultimate.IsExecuting)
                .OverrideFailureMessage("A StaticCharge hit must not interrupt an armoured Ultimate startup.")
                .IsTrue();
            AssertThat(hero.ActiveStatuses.Has(StatusType.StaticCharge))
                .OverrideFailureMessage("The charge still enters its slot, without the stun half.")
                .IsTrue();
        } finally {
            Free(hero, host);
        }

        (hero, host) = Spawn("lincoln");
        try {
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.MovementAbility));
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            hero._PhysicsProcess(Tick);
            var charge = hero.GetNode<BaseSpecial>("MovementAbility");
            AssertThat(charge.IsExecuting).IsTrue();
            AssertThat(hero.HasActiveHyperArmorAgainst(AttackClass.Basic)).IsTrue();
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(StaticChargeHit());
            AssertThat(charge.IsExecuting)
                .OverrideFailureMessage("A StaticCharge hit must not interrupt Lincoln's armoured Rail Charge.")
                .IsTrue();
            AssertThat(hero.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
        } finally {
            Free(hero, host);
        }

        // Positive control: an unarmoured hero is stunned by the same charge.
        (hero, host) = Spawn("tesla");
        try {
            AssertThat(hero.HasActiveHyperArmorAgainst(AttackClass.Basic)).IsFalse();
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(StaticChargeHit());
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Stunned);
        } finally {
            Free(hero, host);
        }
    }

    /// <summary>
    /// Isolation: the armour is a Story rule. Every roster Ultimate still authors
    /// <c>GrantsHyperArmor = false</c>, and the Fighter loadout's
    /// <c>MovementGrantsHyperArmor</c> (the only hyper-armor flag the sim reads)
    /// is the movement resource's own flag.
    /// </summary>
    [TestCase]
    public void TheArmourNeverReachesTheUltimateResourcesOrTheSimLoadout() {
        var issues = new List<string>();
        foreach (string heroID in CharacterRoster.IDs) {
            CharacterData data = AuthoredResources.Load<CharacterData>($"res://resources/Characters/{heroID}_data.tres");
            if (data?.UltimateAttack == null) {
                issues.Add($"{heroID}: no Ultimate resource");
                continue;
            }
            if (data.UltimateAttack.GrantsHyperArmor) issues.Add($"{heroID}: ultimate.tres grants hyper-armor");
            FighterLoadout loadout = FighterLoadoutFactory.FromCharacterData(data);
            int expected = data.MovementAbility?.GrantsHyperArmor == true ? 1 : 0;
            if (loadout.AbilityModes.MovementGrantsHyperArmor != expected) {
                issues.Add($"{heroID}: sim MovementGrantsHyperArmor {loadout.AbilityModes.MovementGrantsHyperArmor}, resource {expected}");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(StoryCombatRules.UltimateStartupArmorPreventsInterruption(AbilityPhase.Startup, AttackClass.Basic)).IsTrue();
        AssertThat(StoryCombatRules.UltimateStartupArmorPreventsInterruption(AbilityPhase.Startup, AttackClass.Ultimate)).IsFalse();
        AssertThat(StoryCombatRules.UltimateStartupArmorPreventsInterruption(AbilityPhase.Active, AttackClass.Basic)).IsFalse();
        AssertThat(StoryCombatRules.UltimateStartupArmorPreventsInterruption(AbilityPhase.Recovery, AttackClass.Special)).IsFalse();
    }

    // === Harness ===

    private static (PlayerController, Node) Spawn(string heroID) {
        PlayerController hero = CharacterFactory.CreateCharacter(heroID, 0, applyStoryProgression: false);
        var host = new Node { Name = "StoryUltimateArmorHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(hero);
        hero.GlobalPosition = new Vector2(0f, -40000f);
        hero.TransitionTo(CharacterState.Airborne);
        return (hero, host);
    }

    /// <summary>Fills the meter and presses the Ultimate; null when the cast refused.</summary>
    private static BaseSpecial StartUltimate(PlayerController hero) {
        hero.SetUltimateMeter(InfluenceMeterRules.MaxValue);
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Ultimate));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        hero._PhysicsProcess(Tick);
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None, GameplayButtons.Ultimate));
        var ultimate = hero.GetNode<BaseSpecial>("Ultimate");
        return hero.CurrentState == CharacterState.UsingUltimate && ultimate.IsExecuting ? ultimate : null;
    }

    /// <summary>A mob's direct melee stun (no attacker slot), from in front.</summary>
    private static HitPayload MobHit(AttackClass attackClass) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.mob_strike",
        HitboxID = "primary",
        AttackClass = attackClass,
        Delivery = HitDelivery.DirectHit,
        Damage = 6f,
        Knockback = new Vector2(3f, -1f),
        HitstunDuration = 0.5f,
        HitOrigin = new Vector2(-40f, -40000f),
        AttackerFacingRight = true,
        ContactId = HitClassification.NextContactId()
    };

    /// <summary>A Basic-class hit carrying a one-second StaticCharge and no hitstun of its own.</summary>
    private static HitPayload StaticChargeHit() {
        HitPayload hit = MobHit(AttackClass.Basic);
        hit.Knockback = Vector2.Zero;
        hit.HitstunDuration = 0f;
        hit.AppliedStatus = StatusType.StaticCharge;
        hit.StatusDuration = 1f;
        hit.StatusIntensity = 1f;
        return hit;
    }

    private static void Free(PlayerController hero, Node host) {
        if (hero != null && GodotObject.IsInstanceValid(hero)) InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
        if (host == null || !GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
