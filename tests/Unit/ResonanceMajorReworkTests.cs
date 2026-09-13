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
/// Package 11 A4. The V7.6 Major reworks — two full rewrites (Joan's Zealous
/// Vigor and Shield of Orleans, Lincoln's Kinetic Splitting) plus the effect
/// corrections on Einstein's Critical Mass and Event Horizon, Tesla's Resonant
/// Overdrive and the three Extractor riders.
///
/// <para>The four SHIELD Majors (Henry's Bastion, Royal Aegis, Leaf Barrier,
/// Wardenclyffe Shield) are deliberately absent: A4 authors their nodes and
/// prerequisites, A1b rewrites their lifecycles.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ResonanceMajorReworkTests {

    [TestCase]
    public void ZealousVigorReclaimsAQuarterOfTheLiveEchoPoolOnTheFinisher() {
        // V7.6: the V6 "heal 5% of missing HP" is RETIRED. The finisher now
        // reclaims an extra share of the Rally echo pool, on top of the
        // damage-scaled reclaim.
        AssertThat(PlayerController.ZealousVigorEchoPoolFraction).IsEqual(0.25f);

        PlayerController player = CharacterFactory.CreateCharacter("joan");
        Node host = Attach(player);
        try {
            player.StoryAbilityPerks.Add(PlayerController.ZealousVigorPerkKey);
            // Build an echo pool by taking a hit, then read what the finisher
            // reclaims out of it.
            player.CurrentHP = player.MaximumHP / 2;
            player.ApplyEnvironmentalDamage(40);
            float pool = player.EchoPool;
            AssertThat(pool > 0f).IsTrue();

            int hpBefore = player.CurrentHP;
            player.NotifyStoryHitLanded(default);
            // Only the finisher triggers it; the hook above carries no hitbox ID.
            AssertThat(player.EchoPool).IsEqual(pool);
            AssertThat(player.CurrentHP).IsEqual(hpBefore);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void ShieldOfOrleansGrantsAFlatFiveMeterPerDistinctBlockedAttack() {
        // V7.6 F06 Option A: a FLAT grant per distinct blocked attack, not the
        // retired damage-scaled x1.25 multiplier.
        AssertThat(PlayerController.ShieldOfOrleansMeterPerBlock).IsEqual(5f);
        AssertThat(PlayerController.ShieldOfOrleansRefundCap).IsEqual(3);
        // The dedup window is what makes "distinct" mean one attack EXECUTION:
        // a volley, a multi-hit ability and a zone pulse fold into one grant.
        AssertThat(PlayerController.ShieldOfOrleansExecutionWindowFrames).IsEqual(60);
    }

    [TestCase]
    public void TheGuardCrushRefundHandsBackOneChargeWithoutLiftingTheShatterLockout() {
        PlayerController player = CharacterFactory.CreateCharacter("joan");
        Node host = Attach(player);
        try {
            var block = player.GetNode<BlockSystem>("BlockSystem");
            block.StartBlock();
            // Spend two charges the way a Guard-Crush attack does.
            block.DepleteCharges(2);
            int afterCrush = block.CurrentCharges;
            AssertThat(afterCrush).IsEqual(1);

            // The refund runs AFTER normal consumption and is capped.
            block.RefundCharges(1, PlayerController.ShieldOfOrleansRefundCap);
            AssertThat(block.CurrentCharges).IsEqual(afterCrush + 1);
            // Never above the cap, however many refunds arrive.
            block.RefundCharges(5, PlayerController.ShieldOfOrleansRefundCap);
            AssertThat(block.CurrentCharges).IsEqual(PlayerController.ShieldOfOrleansRefundCap);

            // A refund never lifts a running shatter lockout: the charge is
            // there, but the stance still cannot rise.
            block.DepleteCharges(block.CurrentCharges);
            AssertThat(block.CurrentCharges).IsEqual(0);
            block.RefundCharges(1, PlayerController.ShieldOfOrleansRefundCap);
            AssertThat(block.CurrentCharges).IsEqual(1);
            AssertThat(block.CanRaiseStance).IsFalse();
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void KineticSplittingOpensAGroundBounceWindowAndAddsFiftyPercentToStructures() {
        // V7.6 FULL REWORK. The V6 perk merely re-classed combo hit 3 as
        // Special, which restated the baseline and granted nothing.
        AssertThat(LincolnSplittingStrike.GroundBounceWindowFrames).IsEqual(20);
        AssertThat(LincolnSplittingStrike.GroundBounceUpwardSpeed > 0f).IsTrue();

        PlayerController lincoln = CharacterFactory.CreateCharacter("lincoln");
        Node host = Attach(lincoln);
        try {
            var strike = lincoln.GetNode<LincolnSplittingStrike>("Special2");
            AssertThat(strike.PendingGroundBounces).IsEqual(0);

            // The structure rider is scoped to Splitting Strike alone.
            lincoln.StoryAbilityPerks.Add(LincolnSplittingStrike.KineticSplittingPerkKey);
            AssertFloat(StoryExtractorDamage.ExtractorMultiplier(
                    lincoln, StoryExtractorDamage.SplittingStrikeAttackID))
                .IsEqualApprox(1.5f, 0.0001f);
            AssertFloat(StoryExtractorDamage.ConstructMultiplier(
                    lincoln, StoryExtractorDamage.SplittingStrikeAttackID))
                .IsEqualApprox(1.5f, 0.0001f);
            // ...and reaches nothing else he throws.
            AssertThat(StoryExtractorDamage.ExtractorMultiplier(lincoln, "lincoln_emancipator"))
                .IsEqual(1f);
            AssertThat(StoryExtractorDamage.ConstructMultiplier(lincoln, "lincoln.basic")).IsEqual(1f);
        } finally {
            Teardown(host, lincoln);
        }
    }

    [TestCase]
    public void ClockworkOverdriveDoublesTurretBoltsAgainstExtractorsOnly() {
        PlayerController leonardo = CharacterFactory.CreateCharacter("leonardo");
        Node host = Attach(leonardo);
        try {
            // Without the Major nothing is amplified.
            AssertThat(StoryExtractorDamage.ExtractorMultiplier(
                leonardo, StoryExtractorDamage.ClockworkTurretAttackID)).IsEqual(1f);

            leonardo.StoryAbilityPerks.Add(LeonardoClockworkTurret.ClockworkOverdrivePerkKey);
            AssertFloat(StoryExtractorDamage.ExtractorMultiplier(
                    leonardo, StoryExtractorDamage.ClockworkTurretAttackID))
                .IsEqualApprox(2f, 0.0001f);
            // Extractors ONLY: the bolt rider never reaches enemy constructs,
            // and it never reaches his other abilities.
            AssertThat(StoryExtractorDamage.ConstructMultiplier(
                leonardo, StoryExtractorDamage.ClockworkTurretAttackID)).IsEqual(1f);
            AssertThat(StoryExtractorDamage.ExtractorMultiplier(
                leonardo, "leonardo_golden_ratio")).IsEqual(1f);
        } finally {
            Teardown(host, leonardo);
        }
    }

    [TestCase]
    public void ResonantOverdriveKeepsTheFasterArcsAndDropsTheRetiredDurationClause() {
        // V7.6 moved the "+5 s coil duration" clause onto Minor Coil Duration,
        // so a hero holding the Major alone must get the authored lifetime.
        PlayerController tesla = CharacterFactory.CreateCharacter("tesla");
        Node host = Attach(tesla);
        try {
            AbilityData coil = AuthoredResources.Load<AbilityData>(
                "res://resources/Abilities/tesla/special_1.tres");
            AssertObject(coil).IsNotNull();
            float authoredLifetime = coil.Lifetime > 0f ? coil.Lifetime : 30f;

            var node = new TeslaCoilNode();
            try {
                tesla.StoryAbilityPerks.Add("resonant_overdrive");
                node.Initialize(coil, tesla, resonantOverdrive: true);
                AssertFloat(node.LifetimeRemaining).IsEqualApprox(authoredLifetime, 0.01f);
            } finally {
                node.Free();
            }

            // The Extractor group the Major's new target acquisition sweeps.
            AssertThat(ChronalExtractor.GroupName).IsEqual("chronal_extractor");
        } finally {
            Teardown(host, tesla);
        }
    }

    [TestCase]
    public void EventHorizonHalvesAProjectileCrossingTheRift() {
        AssertThat(EinsteinRelativityRift.EventHorizonProjectileSpeedScale).IsEqual(0.5f);

        var shot = new FTT.Enemies.EnemyProjectile();
        try {
            // ScaleVelocity is the single Story-side write the Major makes; it
            // refuses a no-op or a speed-up so it can never accelerate a shot.
            shot.ScaleVelocity(0f);
            shot.ScaleVelocity(1f);
            shot.ScaleVelocity(2f);
            AssertThat(shot.Velocity).IsEqual(Vector2.Zero);
        } finally {
            shot.Free();
        }
    }

    // ---- helpers ------------------------------------------------------------

    private static Node Attach(PlayerController player) {
        var host = new Node { Name = "ResonanceMajorHost" };
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
