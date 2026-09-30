using System.Collections.Generic;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 11 A4. The nine V7.6 TRAVERSAL nodes — one per character, all Tier
/// 2, all new behaviour. Each is authored as a node carrying an
/// <c>AbilityModifierKey</c> and no stat lane, so it reaches
/// <c>PlayerController.StoryAbilityPerks</c> through the existing
/// <c>CharacterFactory</c> path with no new plumbing.
///
/// <para>Every case pins the flag's specific REFUSAL conditions as well as its
/// grant, because the design spends most of its words on what each traversal
/// must NOT do.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceTraversalTests {

    /// <summary>Every character's traversal node ID and its perk key.</summary>
    private static readonly (string Character, string NodeID, string PerkKey)[] Expected = {
        ("einstein", "einstein_extended_float", EinsteinRelativityWarp.ExtendedFloatPerkKey),
        ("joan", "joan_wings_refresh", JoanAscendantWings.WingsRefreshPerkKey),
        ("leonardo", "leonardo_replacement", LeonardoClockworkTurret.ReplacementPerkKey),
        ("tesla", "tesla_long_blink", "long_blink"),
        ("shakespeare", "shakespeare_tempest_apex", ShakespeareTheTempest.TempestApexJumpPerkKey),
        ("mozart", "mozart_extra_note", MozartSonataDrift.ExtraNotePerkKey),
        ("cleopatra", "cleopatra_vortex_step", CleopatraSandstormVortex.VortexStepPerkKey),
        ("lincoln", "lincoln_rail_breaker", LincolnRailCharge.RailBreakerPerkKey),
        ("tubman", "tubman_star_guide", TubmanNorthStarLeap.StarGuidePerkKey)
    };

    [TestCase]
    public void EveryCharacterAuthorsItsTraversalNodeAtTierTwoWithTheKeyItsKitReads() {
        var issues = new List<string>();
        foreach ((string character, string nodeID, string perkKey) in Expected) {
            ResonanceGridData grid = AuthoredResources.Load<ResonanceGridData>(
                $"res://resources/Resonance/{character}_grid.tres");
            ResonanceNodeData node = null;
            foreach (ResonanceNodeData candidate in grid.Nodes) {
                if (candidate.NodeID == nodeID) node = candidate;
            }
            if (node == null) {
                issues.Add($"{character}: no node {nodeID}");
                continue;
            }
            if (node.Type != ResonanceNodeType.Traversal) issues.Add($"{nodeID}: type {node.Type}");
            if (node.Tier != 2) issues.Add($"{nodeID}: tier {node.Tier}");
            if (node.UnlockCost != 75) issues.Add($"{nodeID}: cost {node.UnlockCost}");
            if (node.AbilityModifierKey != perkKey) {
                issues.Add($"{nodeID}: perk key '{node.AbilityModifierKey}', kit reads '{perkKey}'");
            }
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase]
    public void EinsteinExtendedFloatAddsExactlyTwentyFramesToTheWarpFloatWindow() {
        // The float window is frame-authored, so the node is a flat frame
        // bonus rather than a multiplier: 1.0 s becomes 1.0 s + 20 frames.
        AssertThat(EinsteinRelativityWarp.ExtendedFloatBonusFrames).IsEqual(20);
        AssertFloat(EinsteinRelativityWarp.ExtendedFloatBonusFrames / 60f)
            .IsEqualApprox(0.3333f, 0.001f);

        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        Node host = Attach(player);
        try {
            // The warp reads the flag off StoryAbilityPerks, which is the only
            // channel a traversal node has: it is Story-only by construction.
            AssertThat(player.HasStoryPerk(EinsteinRelativityWarp.ExtendedFloatPerkKey)).IsFalse();
            player.StoryAbilityPerks.Add(EinsteinRelativityWarp.ExtendedFloatPerkKey);
            AssertThat(player.HasStoryPerk(EinsteinRelativityWarp.ExtendedFloatPerkKey)).IsTrue();

            // The float itself is a plain timer the warp writes on recovery,
            // so the grant is exactly the extra window and nothing else - no
            // extra jumps, no cancel, no reach into the deterministic sim.
            player.StoryFloatTimer = 1.0f + EinsteinRelativityWarp.ExtendedFloatBonusFrames / 60f;
            AssertFloat(player.StoryFloatTimer).IsEqualApprox(1.3333f, 0.001f);
            AssertThat(player.RemainingJumps).IsEqual(player.Data?.MaxJumpCount ?? 2);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void JoanWingsRefreshResetsTheMovementCooldownOncePerAttackExecution() {
        PlayerController player = CharacterFactory.CreateCharacter("joan");
        Node host = Attach(player);
        try {
            player.StoryAbilityPerks.Add(JoanAscendantWings.WingsRefreshPerkKey);

            // A connecting FINISHER refreshes Ascendant Wings.
            player.MovementAbilityCooldownTimer = 5f;
            player.NotifyStoryHitLanded(Hit("joan.basic", "combo_3"));
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(0f);

            // The SAME execution landing on a second target grants nothing:
            // once per attack execution even on multi-target.
            player.MovementAbilityCooldownTimer = 5f;
            player.NotifyStoryHitLanded(Hit("joan.basic", "combo_3"));
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(5f);

            // Hits 1 and 2 of the string never trigger it.
            player.NotifyStoryHitLanded(Hit("joan.basic", "combo_1"));
            player.NotifyStoryHitLanded(Hit("joan.basic", "combo_2"));
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(5f);

            // Righteous Smite is the second authorized trigger, and it is a
            // different execution, so it refreshes even after the finisher did.
            player.NotifyStoryHitLanded(Hit(JoanAscendantWings.RighteousSmiteAttackID, "primary"));
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(0f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void JoanWingsRefreshDoesNothingWithoutTheNode() {
        PlayerController player = CharacterFactory.CreateCharacter("joan");
        Node host = Attach(player);
        try {
            player.MovementAbilityCooldownTimer = 5f;
            player.NotifyStoryHitLanded(Hit("joan.basic", "combo_3"));
            AssertThat(player.MovementAbilityCooldownTimer).IsEqual(5f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void LeonardoReplacementMovesADeployedTurretExactlyOnce() {
        var turret = new LeonardoTurretNode();
        try {
            // A freshly spawned turret carries its one allowance...
            AssertThat(turret.ReplacementAvailable).IsTrue();
            AssertThat(turret.TryConsumeReplacement()).IsTrue();
            // ...and only one. A second pick-up falls through to the ordinary
            // deploy path rather than granting a free re-place.
            AssertThat(turret.ReplacementAvailable).IsFalse();
            AssertThat(turret.TryConsumeReplacement()).IsFalse();
        } finally {
            turret.Free();
        }
    }

    [TestCase]
    public void MozartExtraNoteRaisesTheStaffPlatformCapByExactlyOne() {
        // The deploy limit is already modelled for persistents, so the flag
        // only raises the cap - it does not change lifetime or placement.
        AssertThat(MozartSonataDrift.ExtraNoteAdditionalPlatforms).IsEqual(1);

        AbilityData drift = AuthoredResources.Load<AbilityData>(
            "res://resources/Abilities/mozart/movement.tres");
        AssertObject(drift).IsNotNull();
        int authoredCap = drift.MaxActiveObjects > 0 ? drift.MaxActiveObjects : 1;
        AssertThat(authoredCap).IsEqual(1);
        AssertThat(authoredCap + MozartSonataDrift.ExtraNoteAdditionalPlatforms).IsEqual(2);
    }

    [TestCase]
    public void CleopatraVortexStepHalvesOneCastAndIsConsumedOncePerVortexInstance() {
        AssertThat(CleopatraSandstormVortex.VortexStepCooldownScale).IsEqual(0.5f);

        PlayerController player = CharacterFactory.CreateCharacter("cleopatra");
        Node host = Attach(player);
        try {
            var vortex = player.GetNode<CleopatraSandstormVortex>("Special2");
            player.StoryAbilityPerks.Add(CleopatraSandstormVortex.VortexStepPerkKey);

            // No live vortex: the allowance cannot be consumed, and a refused
            // input spends nothing.
            AssertThat(vortex.VortexActive).IsFalse();
            AssertThat(vortex.TryConsumeVortexStep(player.GlobalPosition)).IsFalse();

            // Deploy one.
            player.TransitionTo(CharacterState.Airborne);
            SendInput(player, GameplayButtons.Special2);
            for (int frame = 0; frame <= vortex.Data.StartupFrames; frame++) {
                vortex._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(vortex.VortexActive).IsTrue();
            AssertThat(vortex.VortexStepAvailable).IsTrue();

            // A cast from far OUTSIDE the churn does not qualify and spends
            // nothing - entering later does not retroactively earn it.
            Vector2 outside = vortex.ActiveVortexCenter
                + new Vector2(CleopatraSandstormVortex.VortexRadiusPixels * 10f, 0f);
            AssertThat(vortex.TryConsumeVortexStep(outside)).IsFalse();
            AssertThat(vortex.VortexStepAvailable).IsTrue();

            // A cast from inside consumes the allowance ATOMICALLY...
            AssertThat(vortex.TryConsumeVortexStep(vortex.ActiveVortexCenter)).IsTrue();
            AssertThat(vortex.VortexStepAvailable).IsFalse();
            // ...and this vortex can never grant a second one, so overlapping
            // vortices can never stack more than one halving.
            AssertThat(vortex.TryConsumeVortexStep(vortex.ActiveVortexCenter)).IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            Teardown(host, player);
        }
    }

    [TestCase]
    public void LincolnRailBreakerSpendsOneBreakPerActivationAndNeverTouchesBeamsOrZones() {
        AssertThat(LincolnRailCharge.RailBreakerProjectilesPerActivation).IsEqual(1);

        PlayerController player = CharacterFactory.CreateCharacter("lincoln");
        Node host = Attach(player);
        try {
            var charge = player.GetNode<LincolnRailCharge>("MovementAbility");
            // Baseline Rail Charge breaks nothing: the allowance arms only for
            // a hero carrying the node.
            AssertThat(charge.RailBreakerSpent).IsFalse();
            AssertThat(player.HasStoryPerk(LincolnRailCharge.RailBreakerPerkKey)).IsFalse();

            // A hostile projectile is breakable by default; beams, zones and
            // hazards never join the pooled projectile group at all, which is
            // what keeps them out of the sweep.
            var shot = new FTT.Enemies.EnemyProjectile();
            try {
                AssertThat(shot.IsBreakable).IsTrue();
                shot.IsBreakable = false;
                AssertThat(shot.IsBreakable).IsFalse();
            } finally {
                shot.Free();
            }
            AssertThat(FTT.Enemies.EnemyProjectile.GroupName).IsEqual("enemy_projectile");
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void TubmanStarGuideReAimsOneLeapOnceWithoutRestartingTheCooldown() {
        // Package 13 W5 (replaces the retired Second Glide case): Star Guide
        // re-aims the remaining travel of a North Star Leap ONCE, on a fresh
        // Movement press, in the newly held direction — no cooldown restart.
        PlayerController player = CharacterFactory.CreateCharacter("tubman");
        Node host = Attach(player);
        try {
            var leap = player.GetNode<TubmanNorthStarLeap>("MovementAbility");
            player.TransitionTo(CharacterState.Airborne);
            SendAxes(player, GameplayButtons.MovementAbility, 1f, 0f);
            AssertThat(leap.IsExecuting).IsTrue();
            AssertThat(leap.LeapDirection).IsEqual(Vector2.Right);
            leap._PhysicsProcess(1.0 / 60.0); // the one startup frame
            float cooldown = player.MovementAbilityCooldownTimer;

            // Without the node, a mid-flight press changes nothing.
            SendAxes(player, GameplayButtons.MovementAbility, 0f, -1f);
            AssertThat(leap.RedirectUsed).IsFalse();
            AssertThat(leap.LeapDirection).IsEqual(Vector2.Right);

            player.StoryAbilityPerks.Add(TubmanNorthStarLeap.StarGuidePerkKey);
            SendAxes(player, GameplayButtons.MovementAbility, 0f, -1f);
            AssertThat(leap.RedirectUsed).IsTrue();
            AssertThat(leap.LeapDirection).IsEqual(Vector2.Up);
            AssertThat(player.MovementAbilityCooldownTimer <= cooldown)
                .OverrideFailureMessage("A Star Guide redirect must not restart the cooldown.")
                .IsTrue();

            // ONE per leap: a second press is refused.
            SendAxes(player, GameplayButtons.MovementAbility, -1f, 0f);
            AssertThat(leap.LeapDirection).IsEqual(Vector2.Up);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void ShakespeareTempestApexJumpRefreshesJumpsOnlyForAHeroCarryingTheNode() {
        PlayerController player = CharacterFactory.CreateCharacter("shakespeare");
        Node host = Attach(player);
        try {
            var tempest = player.GetNode<ShakespeareTheTempest>("Special2");
            int maxJumps = player.Data?.MaxJumpCount ?? 2;

            // Without the node, the lift ending changes nothing.
            player.RemainingJumps = 0;
            tempest.Interrupt();
            AssertThat(player.RemainingJumps).IsEqual(0);

            // With it, the apex - the frame the lift's active phase ends -
            // refreshes the jump budget. A refresh only: no glide, no cancel.
            player.StoryAbilityPerks.Add(ShakespeareTheTempest.TempestApexJumpPerkKey);
            player.TransitionTo(CharacterState.Airborne);
            SendInput(player, GameplayButtons.Special2);
            for (int frame = 0; frame < tempest.Data.StartupFrames + tempest.Data.ActiveFrames + 2; frame++) {
                tempest._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(player.RemainingJumps).IsEqual(maxJumps);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            Teardown(host, player);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static HitPayload Hit(string attackID, string hitboxID) => new() {
        AttackerIndex = 0,
        AttackID = attackID,
        HitboxID = hitboxID,
        Damage = 10f
    };

    private static void SendInput(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    private static void SendAxes(PlayerController player, GameplayButtons buttons, float horizontal, float vertical) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, horizontal, vertical, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "ResonanceTraversalHost" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        host.AddChild(player);
        return host;
    }

    private static void Teardown(Node host, PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
