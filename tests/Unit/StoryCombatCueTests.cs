using System;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// GR-AUDIO (gameplay review ruling 13, 2026-11-03): the Story half of the small combat sound set.
/// A landed basic hit plays <c>hit_basic</c> (once per throttle window, however many bodies it
/// cleaves), a swing whose active window closes with no contact plays <c>swing_whiff</c> once, an
/// ordinary block or a guard break plays <c>block</c>, a perfect block plays <c>perfect_block</c>, and a
/// grab that connects plays <c>grab_catch</c>. Contacts are driven by emitting the melee hitbox's
/// AreaEntered on the target's hurtbox (the StoryHitConfirmTests idiom). The cues are read back from the
/// presenter's <c>LastCueID</c> / <c>CuesPlayed</c> test seams.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryCombatCueTests {
    private const double Step = 1.0 / 60.0;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(53000f, 0f);

    [TestCase]
    public void ALandedBasicHitPlaysTheHitThumpOnceAndAnUnlandedSwingPlaysNoWhiff() {
        Run((player, host, cues) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            int before = cues.CuesPlayed;
            Contact(box, mob);
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.HitBasic);

            // A second body cleaved inside the window is thinned by the row's throttle, not re-played.
            EnemyController second = Mob(host, Origin + new Vector2(60f, 0f));
            Contact(box, second);
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
            AssertThat(cues.CuesThrottled > 0).IsTrue();

            // Running the swing out after a contact is no whiff.
            for (int tick = 0; tick < 120 && player.CurrentState == CharacterState.Attacking; tick++) {
                Tick(player, GameplayButtons.None);
                cues.Advance((float)Step);
            }
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.Attacking);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.HitBasic);
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
        });
    }

    [TestCase]
    public void ASwingThatReachesNothingPlaysTheWhiffOnceWhenItsActiveWindowCloses() {
        Run((player, host, cues) => {
            int before = cues.CuesPlayed;
            Hitbox box = OpenBox(player);
            // Nothing has played while the box is open.
            AssertThat(cues.CuesPlayed).IsEqual(before);
            int whiffs = 0;
            for (int tick = 0; tick < 120 && player.CurrentState == CharacterState.Attacking; tick++) {
                int playedBefore = cues.CuesPlayed;
                Tick(player, GameplayButtons.None);
                cues.Advance((float)Step);
                if (cues.CuesPlayed > playedBefore && cues.LastCueID == CoreMechanicCueIDs.SwingWhiff) whiffs++;
            }
            AssertThat(box.IsActive).IsFalse();
            AssertThat(whiffs).IsEqual(1);
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
        });
    }

    [TestCase]
    public void AnOrdinaryBlockPlaysTheBlockPingAndAPerfectBlockTheBrightOne() {
        Run((player, host, cues) => {
            // Perfect: the stance raised inside the five-frame window of contact.
            Send(player, GameplayButtons.Block, GameplayButtons.None);
            Send(player, GameplayButtons.Block, GameplayButtons.Block);
            int before = cues.CuesPlayed;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(player, 6f));
            AssertThat(player.GetNode<BlockSystem>("BlockSystem").LastBlockWasPerfect).IsTrue();
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.PerfectBlock);
        });

        Run((player, host, cues) => {
            Send(player, GameplayButtons.Block, GameplayButtons.None);
            for (int tick = 0; tick < 9; tick++) Send(player, GameplayButtons.Block, GameplayButtons.Block);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Blocking);
            int before = cues.CuesPlayed;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(player, 6f));
            AssertThat(player.GetNode<BlockSystem>("BlockSystem").LastBlockWasPerfect).IsFalse();
            AssertThat(cues.CuesPlayed - before).IsEqual(1);
            AssertThat(cues.LastCueID).IsEqual(CoreMechanicCueIDs.Block);
        });

        Run((player, host, cues) => {
            // A hit the hero does not block plays neither block cue.
            int before = cues.CuesPlayed;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(player, 6f));
            AssertThat(cues.CuesPlayed).IsEqual(before);
        });
    }

    [TestCase]
    public void AMutedPlaceholderSwitchSilencesTheStoryCuesWithoutTouchingTheSwing() {
        Run((player, host, cues) => {
            PlaceholderCueSettings.Apply(true);
            try {
                EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
                Hitbox box = OpenBox(player);
                int before = cues.CuesPlayed;
                int mutedBefore = cues.CuesMuted;
                Contact(box, mob);
                AssertThat(cues.CuesPlayed).IsEqual(before);
                AssertThat(cues.CuesMuted - mutedBefore).IsEqual(1);
                // The hit itself still landed: only the sound was dropped.
                AssertThat(player.SwingLanded).IsTrue();
            } finally {
                PlaceholderCueSettings.Apply(false);
            }
        });
    }

    // === Harness ==============================================================

    private static HitPayload MobHit(PlayerController hero, float damage) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.combat_cue.mob_strike",
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

    private static void Tick(PlayerController player, GameplayButtons buttons) => Send(player, buttons, GameplayButtons.None);

    /// <summary>Starts a swing and ticks until its box opens.</summary>
    private static Hitbox OpenBox(PlayerController player) {
        var box = player.GetNode<Hitbox>("MeleeHitbox");
        Tick(player, GameplayButtons.BasicAttack);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        for (int tick = 0; tick < 30 && !box.IsActive; tick++) Tick(player, GameplayButtons.None);
        AssertThat(box.IsActive).OverrideFailureMessage("Harness: the swing's box never opened.").IsTrue();
        return box;
    }

    private static void Contact(Hitbox box, Node target) {
        Hurtbox hurtbox = target as Hurtbox ?? target.GetNode<Hurtbox>("Hurtbox");
        box.EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
    }

    private static EnemyController Mob(Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>("res://resources/Enemies/chrono_slasher.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        enemy.CurrentHP = 9999;
        return enemy;
    }

    private static void Run(Action<PlayerController, Node2D, CoreMechanicCuePresenter> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        AudioManager audio = AudioManager.Instance;
        CoreMechanicCuePresenter cues = audio.CoreCues;
        var host = new Node2D { Name = "CombatCueHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "CombatCuePools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "CombatCueFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Tick(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState).OverrideFailureMessage("setup: the hero must stand idle").IsEqual(CharacterState.Idle);
            player.IsFacingRight = true;
            cues.ResetTransient();
            body(player, host, cues);
        } finally {
            PlaceholderCueSettings.Apply(false);
            cues.ResetTransient();
            audio.ReleaseAllVoices();
            InputManager.Instance?.ClearInputSource(0);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }
}
