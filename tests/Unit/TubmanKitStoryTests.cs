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
/// Package 13 W5 — Harriet Tubman's kit in Story (replaces the retired
/// Pocahontas Story suites). Conductor's Call strikes grounded targets once and
/// passes airborne ones; Foresight nullifies an eligible strike, sidesteps and
/// answers a nearby attacker, while Ultimates, throws and ticks are never
/// caught; North Star Leap grants North Star Ward per accepted activation (a
/// Star Guide redirect is not one) and snaps to a ledge from half a unit
/// farther than the normal capture; Safe Passage and Never Lost a Passenger
/// are wired.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TubmanKitStoryTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-42000f, 900f);

    // === Conductor's Call =====================================================

    [TestCase]
    public void ConductorsCallStrikesAGroundedTargetOnceAndPassesAnAirborneOne() {
        Node host = Host("TubmanCallHost");
        PlayerController player = Spawn(host);
        try {
            // A strike surface with no body sits on the ground; an airborne
            // body's hurtbox is passed over.
            int groundHits = 0;
            Hurtbox ground = AddTarget(host, Origin + new Vector2(150f, -30f), () => groundHits++);
            var flyer = new CharacterBody2D { Name = "Flyer", Position = Origin + new Vector2(220f, -30f) };
            host.AddChild(flyer);
            int airHits = 0;
            Hurtbox air = MakeHurtbox(() => airHits++);
            flyer.AddChild(air);

            var call = player.GetNode<TubmanConductorsCall>("Special1");
            AssertThat(call.TryExecute()).IsTrue();
            for (int frame = 0; frame < 90 && call.IsExecuting; frame++) call._PhysicsProcess(Step);

            AssertThat(groundHits).OverrideFailureMessage("The rush strikes a grounded target exactly once.").IsEqual(1);
            AssertThat(airHits).OverrideFailureMessage("The rush passes under an airborne target.").IsEqual(0);
            AssertFloat(call.RushDistancePixels).IsEqualApprox(5f * KitMotionRules.StoryPixelsPerUnit, 0.5f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void SafePassageLeavesATwoSecondTrailThatSpeedsHerUp() {
        Node host = Host("TubmanSafePassageHost");
        PlayerController player = Spawn(host);
        try {
            player.StoryAbilityPerks.Add(TubmanConductorsCall.SafePassagePerkKey);
            var call = player.GetNode<TubmanConductorsCall>("Special1");
            AssertThat(call.TryExecute()).IsTrue();
            int laying = call.Data.StartupFrames + call.Data.ActiveFrames;
            for (int frame = 0; frame < laying; frame++) call._PhysicsProcess(Step);
            AssertThat(call.TrailSecondsRemaining).IsEqual(TubmanConductorsCall.SafePassageTrailSeconds);
            AssertThat(call.IsOnTrail(player.GlobalPosition)).IsTrue();
            call._PhysicsProcess(Step);
            AssertFloat(player.StoryTemporarySpeedMultiplier)
                .IsEqual(TubmanConductorsCall.SafePassageSpeedMultiplier);
            for (int frame = 0; frame < 125; frame++) call._PhysicsProcess(Step);
            AssertThat(call.IsOnTrail(player.GlobalPosition))
                .OverrideFailureMessage("The trail is gone after two seconds.")
                .IsFalse();
        } finally {
            Teardown(host, player);
        }
    }

    // === Foresight ============================================================

    [TestCase]
    public void ForesightNullifiesAStrikeSidestepsAndAnswersTheNearbyAttacker() {
        Node host = Host("TubmanForesightHost");
        PlayerController player = Spawn(host);
        try {
            float answered = 0f;
            AddTarget(host, Origin + new Vector2(90f, -30f), () => { }, payload => answered += payload.Damage);
            var foresight = player.GetNode<TubmanForesight>("Special2");
            OpenWindow(foresight);
            int hp = player.CurrentHP;

            float dealt = player.GetNode<Hurtbox>("Hurtbox").TakeHit(Strike(AttackClass.Basic, 12f));
            AssertFloat(dealt).IsEqual(0f);
            AssertThat(player.CurrentHP).IsEqual(hp);
            AssertThat(foresight.CountersLanded).IsEqual(1);
            AssertThat(foresight.IsSidestepping).IsTrue();
            // The sidestep takes no hit at all.
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Strike(AttackClass.Special, 12f));
            AssertThat(player.CurrentHP).IsEqual(hp);

            foresight._PhysicsProcess(Step); // the answer lands on the next tick
            AssertFloat(answered)
                .OverrideFailureMessage("The attacker within 2.5 units takes the 20-damage answer.")
                .IsEqual(foresight.Data.BaseDamage * player.StorySpecialDamageMultiplier);
            // One trigger per activation.
            AssertThat(foresight.IsWindowOpen).IsFalse();
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void UltimatesThrowsTicksAndWindboxesAreNeverCaught() {
        Node host = Host("TubmanForesightIneligibleHost");
        PlayerController player = Spawn(host);
        try {
            var foresight = player.GetNode<TubmanForesight>("Special2");
            AssertThat(TubmanForesight.IsCounterable(Strike(AttackClass.Basic, 5f))).IsTrue();
            AssertThat(TubmanForesight.IsCounterable(Strike(AttackClass.Special, 5f))).IsTrue();
            AssertThat(TubmanForesight.IsCounterable(Strike(AttackClass.Ultimate, 5f))).IsFalse();
            HitPayload throwHit = Strike(AttackClass.Basic, 5f);
            throwHit.Origin = HitOrigin.Throw;
            AssertThat(TubmanForesight.IsCounterable(throwHit)).IsFalse();
            HitPayload tick = Strike(AttackClass.Special, 5f);
            tick.Delivery = HitDelivery.Tick;
            AssertThat(TubmanForesight.IsCounterable(tick)).IsFalse();
            HitPayload construct = Strike(AttackClass.Basic, 5f);
            construct.Delivery = HitDelivery.Construct;
            AssertThat(TubmanForesight.IsCounterable(construct)).IsFalse();
            AssertThat(TubmanForesight.IsCounterable(Strike(AttackClass.Special, 0f)))
                .OverrideFailureMessage("A zero-damage windbox neither triggers it nor is stopped by it.")
                .IsFalse();
            HitPayload answer = Strike(AttackClass.Special, 20f);
            answer.HitboxID = "foresight_answer";
            AssertThat(TubmanForesight.IsCounterable(answer))
                .OverrideFailureMessage("A counter strike cannot trigger another Foresight.")
                .IsFalse();

            // An Ultimate hit in an open window lands in full.
            OpenWindow(foresight);
            int hp = player.CurrentHP;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Strike(AttackClass.Ultimate, 9f));
            AssertThat(player.CurrentHP).IsEqual(hp - 9);
            AssertThat(foresight.CountersLanded).IsEqual(0);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void NeverLostAPassengerReclaimsHalfTheRemainingEchoPoolOnACounter() {
        Node host = Host("TubmanPassengerHost");
        PlayerController player = Spawn(host);
        try {
            player.StoryAbilityPerks.Add(TubmanForesight.NeverLostAPassengerPerkKey);
            // The answer's target reports a 1-HP hit, so the counter strike's own
            // damage-scaled reclaim takes only 2 and the perk's half is visible.
            AddTarget(host, Origin + new Vector2(90f, -30f), () => { }, _ => { }, reportedDamage: 1f);
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Strike(AttackClass.Special, 60f));
            float pool = player.EchoPool;
            AssertThat(pool > 4f).OverrideFailureMessage("The drill needs a Rally echo pool.").IsTrue();

            var foresight = player.GetNode<TubmanForesight>("Special2");
            OpenWindow(foresight);
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Strike(AttackClass.Basic, 8f));
            foresight._PhysicsProcess(Step);
            AssertFloat(player.EchoPool)
                .OverrideFailureMessage($"Pool {pool} → {player.EchoPool}: the perk reclaims 50% of what is left.")
                .IsEqualApprox((pool - 2f) * (1f - TubmanForesight.NeverLostAPassengerEchoFraction), 0.6f);
        } finally {
            Teardown(host, player);
        }
    }

    // === North Star Leap ======================================================

    [TestCase]
    public void NorthStarWardGrantsTheSharedBarrierPerAcceptedLeapButNotPerRedirect() {
        Node host = Host("TubmanWardHost");
        PlayerController player = Spawn(host);
        try {
            player.StoryAbilityPerks.Add(TubmanNorthStarLeap.NorthStarWardPerkKey);
            player.StoryAbilityPerks.Add(TubmanNorthStarLeap.StarGuidePerkKey);
            var leap = player.GetNode<TubmanNorthStarLeap>("MovementAbility");
            AssertThat(leap.TryExecute()).IsTrue();
            AssertThat(player.StoryShieldEffectId).IsEqual(StoryShieldEffect.ResonanceBarrier);
            AssertThat(player.StoryShieldSource).IsEqual(StoryShieldEffect.NorthStarWard);
            float cap = StoryDefenseRules.GrantedShieldCapacityShare * player.MaximumHP;
            AssertFloat(player.StoryShieldPoints).IsEqualApprox(cap, 0.001f);

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(Strike(AttackClass.Basic, 4f));
            float drained = player.StoryShieldPoints;
            AssertThat(drained < cap).IsTrue();
            leap._PhysicsProcess(Step); // startup → travel
            leap.TryStarGuideRedirect();
            AssertFloat(player.StoryShieldPoints)
                .OverrideFailureMessage("A Star Guide redirect is not a new activation.")
                .IsEqualApprox(drained, 0.001f);

            for (int frame = 0; frame < 60 && leap.IsExecuting; frame++) leap._PhysicsProcess(Step);
            AssertThat(leap.TryExecute()).IsTrue();
            AssertFloat(player.StoryShieldPoints)
                .OverrideFailureMessage("A new leap refills the ward to its cap, never stacking.")
                .IsEqualApprox(cap, 0.001f);
        } finally {
            Teardown(host, player);
        }
    }

    [TestCase]
    public void TheLeapSnapsToALedgeHalfAUnitBeyondTheNormalCapture() {
        Node host = Host("TubmanLedgeHost");
        PlayerController player = Spawn(host);
        try {
            var ledge = GD.Load<PackedScene>("res://scenes/templates/LedgeGrabPointTemplate.tscn")
                .Instantiate<LedgeGrabPoint>();
            // Beyond the ordinary overlap reach, inside the extended one.
            ledge.Position = Origin + new Vector2(50f, -30f);
            host.AddChild(ledge);
            AssertObject(player.FindLedgeWithinSnapReach(0f)).IsNull();
            AssertObject(player.FindLedgeWithinSnapReach(TubmanNorthStarLeap.LedgeSnapBonusPixels)).IsSame(ledge);

            // A leap that ends there catches it. (The drill runs the ability's
            // own clock only, so the hero stays where the ledge was placed.)
            player.TransitionTo(CharacterState.Airborne);
            var leap = player.GetNode<TubmanNorthStarLeap>("MovementAbility");
            AssertThat(leap.TryExecute()).IsTrue();
            player.TransitionTo(CharacterState.UsingMovementAbility);
            for (int frame = 0; frame < leap.Data.StartupFrames + leap.Data.ActiveFrames && leap.IsExecuting; frame++) {
                leap._PhysicsProcess(Step);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.LedgeHanging);
            AssertThat(leap.IsExecuting).IsFalse();
        } finally {
            Teardown(host, player);
        }
    }

    // === helpers ===============================================================

    private static void OpenWindow(TubmanForesight foresight) {
        AssertThat(foresight.TryExecute()).IsTrue();
        for (int frame = 0; frame < foresight.Data.StartupFrames; frame++) foresight._PhysicsProcess(Step);
        AssertThat(foresight.IsWindowOpen).IsTrue();
    }

    private static HitPayload Strike(AttackClass attackClass, float damage) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.strike",
        HitboxID = "primary",
        AttackClass = attackClass,
        Damage = damage,
        HitOrigin = Origin + new Vector2(40f, -30f),
        Delivery = HitDelivery.DirectHit,
        Origin = HitOrigin.Basic
    };

    private static Hurtbox MakeHurtbox(System.Action onHit, System.Action<HitPayload> inspect = null, float reportedDamage = -1f) {
        var hurtbox = new Hurtbox {
            Name = "TargetHurtbox",
            OwnerPlayerIndex = 1,
            CollisionLayer = CollisionLayers.EnemyHurtbox,
            CollisionMask = 0,
            Monitorable = true
        };
        hurtbox.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 60f) } });
        hurtbox.OnHit += payload => {
            onHit();
            inspect?.Invoke(payload);
            return reportedDamage >= 0f ? reportedDamage : payload.Damage;
        };
        return hurtbox;
    }

    private static Hurtbox AddTarget(Node host, Vector2 position, System.Action onHit,
        System.Action<HitPayload> inspect = null, float reportedDamage = -1f) {
        Hurtbox hurtbox = MakeHurtbox(onHit, inspect, reportedDamage);
        hurtbox.Position = position;
        host.AddChild(hurtbox);
        return hurtbox;
    }

    private static Node Host(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static PlayerController Spawn(Node host) {
        PlayerController player = CharacterFactory.CreateCharacter("tubman", 0);
        player.Position = Origin;
        host.AddChild(player);
        return player;
    }

    private static void Teardown(Node host, PlayerController player) {
        if (player != null) InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
