using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FU-BLOCK (feel and UI update 2026-10-25): the Story half of the block-success
/// effect. An ordinary block, a perfect block, a guard break and a hazard block
/// each spawn exactly one <see cref="BlockSparkEffect"/> of the right kind at
/// the blocker's front under the hero's parent; an unblocked hit spawns none.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryBlockSparkTests {
    private const float Step = 1f / 60f;
    private static readonly Vector2 Origin = new(-54000f, -9000f);

    [TestCase]
    public void AnOrdinaryBlockSpawnsOneOrdinarySparkAtTheFront() {
        Node host = Host("BlockSparkOrdinaryHost");
        try {
            PlayerController hero = RaiseBlock(host, extraLiveTicks: 8);
            Vector2 at = hero.GlobalPosition;
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(hero, 6f));
            BlockSparkEffect spark = OnlyEffect(host, BlockSparkKind.Ordinary);
            AssertThat(spark.GlobalPosition).IsEqual(at + new Vector2(PlayerController.BlockSparkForwardPixels, -PlayerController.BlockSparkRisePixels));
            AssertThat(spark.Facing).IsEqual(1f);
        } finally {
            Free(host);
        }
    }

    [TestCase]
    public void APerfectBlockSpawnsOnePerfectSpark() {
        Node host = Host("BlockSparkPerfectHost");
        try {
            PlayerController hero = RaiseBlock(host, extraLiveTicks: 0);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(hero, 6f));
            AssertThat(hero.GetNode<BlockSystem>("BlockSystem").LastBlockWasPerfect).IsTrue();
            OnlyEffect(host, BlockSparkKind.Perfect);
        } finally {
            Free(host);
        }
    }

    [TestCase]
    public void AGuardBreakSpawnsOneShatterSparkAndNoOrdinaryOne() {
        Node host = Host("BlockSparkShatterHost");
        try {
            PlayerController hero = RaiseBlock(host, extraLiveTicks: 8);
            HitPayload crush = MobHit(hero, 6f);
            crush.BlockChargeCost = 99; // spends every charge
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(crush);
            OnlyEffect(host, BlockSparkKind.Shatter);
        } finally {
            Free(host);
        }
    }

    [TestCase]
    public void AHazardBlockSpawnsOneOrdinarySpark() {
        Node host = Host("BlockSparkHazardHost");
        try {
            PlayerController hero = RaiseBlock(host, extraLiveTicks: 8);
            PlayerController.EnvironmentalHitResult result = hero.ApplyEnvironmentalHit(
                5, hero.GlobalPosition + new Vector2(40f, -30f), blockable: true);
            AssertThat(result).IsEqual(PlayerController.EnvironmentalHitResult.Blocked);
            OnlyEffect(host, BlockSparkKind.Ordinary);
        } finally {
            Free(host);
        }
    }

    [TestCase]
    public void AnUnblockedHitSpawnsNoSpark() {
        Node host = Host("BlockSparkNoBlockHost");
        try {
            PlayerController hero = SpawnGroundedHero(host);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(hero, 6f));
            AssertThat(Effects(host).Count).IsEqual(0);
        } finally {
            Free(host);
        }
    }

    // === Harness ==============================================================

    private static PlayerController RaiseBlock(Node host, int extraLiveTicks) {
        PlayerController hero = SpawnGroundedHero(host);
        Send(hero, GameplayButtons.Block, GameplayButtons.None);
        Send(hero, GameplayButtons.Block, GameplayButtons.Block);
        for (int tick = 0; tick < extraLiveTicks; tick++) Send(hero, GameplayButtons.Block, GameplayButtons.Block);
        AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
        return hero;
    }

    private static List<BlockSparkEffect> Effects(Node host) {
        var found = new List<BlockSparkEffect>();
        foreach (Node child in host.GetChildren()) {
            if (child is BlockSparkEffect effect && effect.IsActive) found.Add(effect);
        }
        return found;
    }

    private static BlockSparkEffect OnlyEffect(Node host, BlockSparkKind kind) {
        List<BlockSparkEffect> effects = Effects(host);
        AssertThat(effects.Count).OverrideFailureMessage("Exactly one spark must appear.").IsEqual(1);
        AssertThat(effects[0].Kind).IsEqual(kind);
        return effects[0];
    }

    private static void Free(Node host) {
        InputManager.Instance?.ClearInputSource(0);
        host.Free();
    }

    private static HitPayload MobHit(PlayerController hero, float damage) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.block_spark.mob_strike",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0.2f,
        HitOrigin = hero.GlobalPosition + new Vector2(40f, -30f),
        AttackerFacingRight = false,
        SourceIsStandardMob = true,
        ContactId = HitClassification.NextContactId()
    };

    private static void Send(PlayerController hero, GameplayButtons held, GameplayButtons previousHeld) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held, previousHeld));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        hero._PhysicsProcess(Step);
    }

    private static Node Host(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        var floor = new StaticBody2D {
            Name = "BlockSparkFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        return host;
    }

    private static PlayerController SpawnGroundedHero(Node host) {
        PlayerController hero = CharacterFactory.CreateCharacter(
            "einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        hero.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(hero);
        hero.GlobalPosition = Origin + new Vector2(0f, -8f);
        hero.IsFacingRight = true;
        for (int frame = 0; frame < 120 && !hero.IsOnFloor(); frame++) {
            hero.Velocity = new Vector2(0f, 400f);
            Send(hero, GameplayButtons.None, GameplayButtons.None);
        }
        AssertThat(hero.IsOnFloor()).OverrideFailureMessage("Harness: the hero must stand on the floor.").IsTrue();
        for (int frame = 0; frame < 10 && hero.CurrentState != CharacterState.Idle; frame++) {
            Send(hero, GameplayButtons.None, GameplayButtons.None);
        }
        hero.IsFacingRight = true;
        return hero;
    }
}
