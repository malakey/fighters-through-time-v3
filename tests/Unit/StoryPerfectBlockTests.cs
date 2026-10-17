using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// CM-BLOCK (combat and movement update 2026-10-16, decision 3.4 with the Q3
/// ruling; ledger <c>USER-PERFECT-BLOCK</c> / <c>PROVISIONAL-PERFECT-BLOCK-RULES</c>):
/// the Story half of the perfect block. A block raised within five ticks before
/// a hit — contact ages 0–4 counted from the first LIVE stance tick, which in
/// Story is the tick after the press (<c>ProcessBlocking</c> re-asserts
/// <c>BlockSystem.StartBlock</c>) — spends no charge and sets no shieldstun,
/// keeping the 2-frame blocked hitstop. It arms only when Blocking is entered
/// from another state on a tick Block is pressed, after the stance was down at
/// least 20 ticks (or never raised). It is still a block: the D8 Punish window
/// opens and Henry's Bastion grants; Shield of Orléans does not pay (no charge
/// was consumed); a Guard-Crush keeps its two-charge cost; a Siphon Snare cast
/// is perfect-blocked like any other block. Every case drives the hero's real
/// <c>_PhysicsProcess</c> with <see cref="PlayerInputFrame.Create"/>-built
/// edges (plan §1.8); the Fighter half is <c>FighterPerfectBlockTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryPerfectBlockTests {
    private const float Step = 1f / 60f;
    private static readonly Vector2 Origin = new(-52000f, -9000f);

    // === 1. The window ========================================================

    [TestCase]
    public void ARaiseZeroToFourLiveTicksBeforeAMobHitIsPerfectAndTheFifthIsOrdinary() {
        for (int lead = 0; lead <= BasicComboRules.PerfectBlockWindowFrames; lead++) {
            Node host = Host($"PerfectBlockLeadHost{lead}");
            try {
                PlayerController hero = SpawnGroundedHero(host, "einstein");
                BlockSystem block = hero.GetNode<BlockSystem>("BlockSystem");
                int charges = block.CurrentCharges;
                int hp = hero.CurrentHP;

                // The press enters Blocking; the window is owed, not yet open.
                Send(hero, GameplayButtons.Block, GameplayButtons.None);
                AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
                AssertThat(hero.PerfectBlockFramesRemaining)
                    .OverrideFailureMessage("The window opens on the first live stance tick, not the press tick.")
                    .IsEqual(0);
                // The first live stance tick opens it: contact here is age 0.
                Send(hero, GameplayButtons.Block, GameplayButtons.Block);
                AssertThat(block.IsBlocking).IsTrue();
                AssertThat(hero.PerfectBlockFramesRemaining).IsEqual(BasicComboRules.PerfectBlockWindowFrames);
                for (int tick = 0; tick < lead; tick++) Send(hero, GameplayButtons.Block, GameplayButtons.Block);

                HitPayload hit = MobHit(hero, damage: 6f);
                hero.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
                AssertThat(hero.CurrentHP)
                    .OverrideFailureMessage($"Lead {lead}: the hit must be blocked, perfect or not.")
                    .IsEqual(hp);
                AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
                AssertThat(hero.GetNode<Hurtbox>("Hurtbox").DidLand(hit.ContactId))
                    .OverrideFailureMessage($"Lead {lead}: a block is never a D1a landing.")
                    .IsFalse();
                if (lead < BasicComboRules.PerfectBlockWindowFrames) {
                    AssertThat(block.CurrentCharges)
                        .OverrideFailureMessage($"A contact {lead} live tick(s) after the raise is perfect: no charge.")
                        .IsEqual(charges);
                    AssertThat(block.IsInShieldStun)
                        .OverrideFailureMessage($"A perfect block (lead {lead}) sets no shieldstun.")
                        .IsFalse();
                    AssertThat(block.LastBlockWasPerfect).IsTrue();
                    AssertThat(hero.PerfectBlockCount).IsEqual(1);
                    AssertThat(hero.HitstopFramesRemaining)
                        .OverrideFailureMessage("A perfect block keeps the 2-frame blocked hitstop.")
                        .IsEqual(BasicComboRules.BlockedHitstopFrames);
                } else {
                    AssertThat(block.CurrentCharges)
                        .OverrideFailureMessage($"A contact {lead} live ticks after the raise is an ordinary block.")
                        .IsEqual(charges - 1);
                    AssertThat(block.IsInShieldStun).IsTrue();
                    AssertThat(block.LastBlockWasPerfect).IsFalse();
                    AssertThat(hero.PerfectBlockCount).IsEqual(0);
                }
            } finally {
                Teardown(host);
            }
        }
    }

    // === 2. Raises that never arm, and the Toggle raise ========================

    [TestCase]
    public void AHeldRaiseAnEscapeRaisedStanceAndAToggleReRaiseInsideTwentyFramesNeverArm() {
        // A stance that rises while Block is merely held: pressed in the swing's
        // startup (ignored there), held into its recovery, whose Block cancel
        // enters Blocking with no press on that tick.
        Node held = Host("PerfectBlockHeldHost");
        try {
            PlayerController hero = SpawnGroundedHero(held, "einstein");
            Send(hero, GameplayButtons.BasicAttack, GameplayButtons.None);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Attacking);
            Send(hero, GameplayButtons.Block, GameplayButtons.BasicAttack);
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage("Harness: Block is ignored in the swing's startup.")
                .IsEqual(CharacterState.Attacking);
            bool rose = false;
            for (int tick = 0; tick < 90 && !rose; tick++) {
                Send(hero, GameplayButtons.Block, GameplayButtons.Block);
                rose = hero.CurrentState == CharacterState.Blocking;
            }
            AssertThat(rose).OverrideFailureMessage("Harness: the held Block never raised the stance.").IsTrue();
            for (int tick = 0; tick < 3; tick++) {
                AssertThat(hero.IsPerfectBlockWindowOpen)
                    .OverrideFailureMessage("A stance raised with Block only held must not arm the window.")
                    .IsFalse();
                Send(hero, GameplayButtons.Block, GameplayButtons.Block);
            }
        } finally {
            Teardown(held);
        }

        // The hit-2 Block escape, even with Block pressed fresh on the escape
        // tick and a stance never raised before (an eligible lowered age).
        Node escape = Host("PerfectBlockEscapeHost");
        try {
            PlayerController hero = SpawnGroundedHero(escape, "einstein");
            HitPayload stun = MobHit(hero, damage: 6f);
            stun.HitboxID = "combo_2";
            stun.HitstunDuration = 0.5f;
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(stun);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Stunned);
            for (int tick = 0; tick < 20 && hero.IsInHitstop; tick++) Send(hero, GameplayButtons.None, GameplayButtons.None);
            AssertThat(hero.IsInHitstop).IsFalse();
            Send(hero, GameplayButtons.Block, GameplayButtons.None);
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage("Harness: a fresh Block press must escape grounded hitstun into the stance.")
                .IsEqual(CharacterState.Blocking);
            for (int tick = 0; tick < 3; tick++) {
                Send(hero, GameplayButtons.Block, GameplayButtons.Block);
                AssertThat(hero.IsPerfectBlockWindowOpen)
                    .OverrideFailureMessage("A stance raised by the hit-2 escape never arms, even with a fresh press.")
                    .IsFalse();
            }
        } finally {
            Teardown(escape);
        }

        // Toggle Block: the latch's toggle-on is a press edge (PlayerInputFrame
        // computes Pressed from the latched Held), so a tap arms; the anti-mash
        // rearm holds a re-raise 19 ticks after the drop and lets one at 20 arm.
        Node toggle = Host("PerfectBlockToggleHost");
        try {
            PlayerController hero = SpawnGroundedHero(toggle, "einstein");
            var latch = new BlockToggleLatch();
            GameplayButtons previous = GameplayButtons.None;
            void Tap(bool raw) => previous = SendLatched(hero, ref latch, raw, previous);

            Tap(true);   // toggle on: enters Blocking with a press
            Tap(false);  // first live stance tick: the window opens
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
            AssertThat(hero.PerfectBlockFramesRemaining)
                .OverrideFailureMessage("A Toggle Block tap is a press: the window arms.")
                .IsEqual(BasicComboRules.PerfectBlockWindowFrames);
            for (int tick = 0; tick < 10; tick++) Tap(false);
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage("The latch holds the stance with the button up.")
                .IsEqual(CharacterState.Blocking);
            AssertThat(hero.IsPerfectBlockWindowOpen).IsFalse();

            foreach (int lowered in new[] { BasicComboRules.PerfectBlockRearmFrames - 1, BasicComboRules.PerfectBlockRearmFrames }) {
                Tap(true);   // toggle off: the stance drops on this tick
                AssertThat(hero.CurrentState).IsNotEqual(CharacterState.Blocking);
                Tap(false);
                for (int tick = 2; tick < lowered; tick++) Tap(false);
                Tap(true);   // toggle on, `lowered` ticks after the drop
                Tap(false);
                AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
                bool armed = lowered >= BasicComboRules.PerfectBlockRearmFrames;
                AssertThat(hero.IsPerfectBlockWindowOpen)
                    .OverrideFailureMessage($"A re-raise {lowered} ticks after the drop must {(armed ? "" : "not ")}arm.")
                    .IsEqual(armed);
                for (int tick = 0; tick < 6; tick++) Tap(false);
            }
        } finally {
            Teardown(toggle);
        }
    }

    // === 3. Still a block: the Punish window and the block perks ===============

    [TestCase]
    public void APerfectBlockOpensThePunishWindowAndGrantsHenrysBastionButShieldOfOrleansDoesNotPay() {
        Node host = Host("PerfectBlockPerksHost");
        try {
            PlayerController hero = SpawnGroundedHero(host, "joan");
            hero.StoryAbilityPerks.Add(BlockSystem.HenrysBastionPerkKey);
            hero.StoryAbilityPerks.Add(PlayerController.ShieldOfOrleansPerkKey);
            EnemyController mob = CreateEnemy(host, "chrono_slasher", hero.GlobalPosition + new Vector2(90f, 0f));
            BlockSystem block = hero.GetNode<BlockSystem>("BlockSystem");
            int charges = block.CurrentCharges;
            RaiseThroughInput(hero);
            AssertThat(hero.IsPerfectBlockWindowOpen).IsTrue();

            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(hero, damage: 6f, source: mob));
            AssertThat(block.LastBlockWasPerfect).IsTrue();
            AssertThat(block.CurrentCharges).IsEqual(charges);
            AssertThat(mob.PunishFramesRemaining)
                .OverrideFailureMessage("A perfect block is a block: it opens the attacker's Punish window.")
                .IsEqual(StoryEnemyCombatRules.PunishWindowFrames);
            AssertThat(hero.StoryShieldPoints > 0f)
                .OverrideFailureMessage("Henry's Bastion is block-triggered: a perfect block grants it.")
                .IsTrue();
            AssertThat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("Shield of Orleans needs a consumed charge: a perfect block never pays.")
                .IsEqual(0f);
        } finally {
            Teardown(host);
        }

        // Control: the same hero with only Shield of Orleans, blocking ordinarily
        // (the window run out), is paid its flat grant.
        Node control = Host("PerfectBlockOrleansControlHost");
        try {
            PlayerController hero = SpawnGroundedHero(control, "joan");
            hero.StoryAbilityPerks.Add(PlayerController.ShieldOfOrleansPerkKey);
            RaiseThroughInput(hero);
            for (int tick = 0; tick < BasicComboRules.PerfectBlockWindowFrames; tick++) {
                Send(hero, GameplayButtons.Block, GameplayButtons.Block);
            }
            AssertThat(hero.IsPerfectBlockWindowOpen).IsFalse();
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(MobHit(hero, damage: 6f));
            AssertThat(hero.GetNode<BlockSystem>("BlockSystem").LastBlockWasPerfect).IsFalse();
            AssertThat(hero.CurrentUltimateMeter).IsEqual(PlayerController.ShieldOfOrleansMeterPerBlock);
        } finally {
            Teardown(control);
        }
    }

    // === 4. Guard-Crush and Siphon Snare =======================================

    [TestCase]
    public void AGuardCrushInsideTheWindowKeepsItsTwoChargeCostAndASiphonSnareIsPerfectBlocked() {
        // An elite Guard-Crush keeps its effect inside the window.
        Node crush = Host("PerfectBlockGuardCrushHost");
        try {
            PlayerController hero = SpawnGroundedHero(crush, "einstein");
            BlockSystem block = hero.GetNode<BlockSystem>("BlockSystem");
            int charges = block.CurrentCharges;
            RaiseThroughInput(hero);
            AssertThat(hero.IsPerfectBlockWindowOpen).IsTrue();
            HitPayload guardCrush = MobHit(hero, damage: 12f);
            guardCrush.BlockChargeCost = BasicComboRules.GuardCrushBlockChargeCost;
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(guardCrush);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
            AssertThat(block.LastBlockWasPerfect).IsFalse();
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A Guard-Crush inside the window still spends its two charges.")
                .IsEqual(charges - BasicComboRules.GuardCrushBlockChargeCost);
            AssertThat(block.IsInShieldStun).IsTrue();
            AssertThat(hero.PerfectBlockCount).IsEqual(0);
        } finally {
            Teardown(crush);
        }

        // The Eraser's Siphon Snare (a zero-damage cast through the shared
        // non-damaging block entry point) meets a fresh raise: perfect.
        Node siphon = Host("PerfectBlockSiphonHost");
        try {
            PlayerController hero = SpawnGroundedHero(siphon, "einstein");
            hero.SetUltimateMeter(InfluenceMeterRules.MaxValue);
            EnemyController eraser = CreateEnemy(siphon, "unbound_eraser", hero.GlobalPosition + new Vector2(90f, 0f));
            EnemyAbilityData snare = eraser.Data.EliteAbilities[1];
            AssertThat(snare.Archetype).IsEqual(EnemyAbilityArchetype.SiphonTether);
            BlockSystem block = hero.GetNode<BlockSystem>("BlockSystem");
            int charges = block.CurrentCharges;
            RaiseThroughInput(hero);
            AssertThat(hero.IsPerfectBlockWindowOpen).IsTrue();

            // Only the Eraser ticks: the hero's window holds while it channels.
            eraser.BeginAttack(snare);
            for (int frame = 0; frame < snare.TelegraphFrames; frame++) eraser.TickAbilityExecutor(Step);
            AssertThat(eraser.LastSiphonResult).IsEqual(SiphonAttachResult.Blocked);
            AssertThat(block.LastBlockWasPerfect).IsTrue();
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("A perfect-blocked Siphon Snare spends no charge.")
                .IsEqual(charges);
            AssertThat(block.IsInShieldStun).IsFalse();
            AssertThat(hero.CurrentUltimateMeter)
                .OverrideFailureMessage("A blocked snare drains nothing.")
                .IsEqual(InfluenceMeterRules.MaxValue);
            AssertThat(eraser.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames);
        } finally {
            Teardown(siphon);
        }
    }

    // === Harness =================================================================

    /// <summary>One hero tick on a <see cref="PlayerInputFrame.Create"/>-built frame, so the press edge is explicit.</summary>
    private static void Send(PlayerController hero, GameplayButtons held, GameplayButtons previousHeld) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, held, previousHeld));
        InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
        hero._PhysicsProcess(Step);
    }

    /// <summary>One tick through the Toggle Block latch: <paramref name="rawBlock"/> is the physical button.</summary>
    private static GameplayButtons SendLatched(
        PlayerController hero, ref BlockToggleLatch latch, bool rawBlock, GameplayButtons previousHeld) {
        bool latched = latch.Step(BlockMode.Toggle, rawBlock, jumpPressed: false, rollPressed: false);
        GameplayButtons held = latched ? GameplayButtons.Block : GameplayButtons.None;
        Send(hero, held, previousHeld);
        return held;
    }

    /// <summary>A fresh press and the first live stance tick: the window is open after this.</summary>
    private static void RaiseThroughInput(PlayerController hero) {
        Send(hero, GameplayButtons.Block, GameplayButtons.None);
        Send(hero, GameplayButtons.Block, GameplayButtons.Block);
        AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
    }

    /// <summary>A mob strike in front of the (right-facing) hero, as the executor builds them.</summary>
    private static HitPayload MobHit(PlayerController hero, float damage, EnemyController source = null) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.perfect_block.mob_strike",
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
        SourceActorId = source?.GetInstanceId() ?? 0UL,
        ContactId = HitClassification.NextContactId()
    };

    private static Node Host(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        // An Environment floor 4000 px wide whose top surface is at Origin.Y.
        var floor = new StaticBody2D {
            Name = "PerfectBlockFloor",
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

    /// <summary>A right-facing hero standing Idle on the host's floor, with no Story progression.</summary>
    private static PlayerController SpawnGroundedHero(Node host, string characterID) {
        PlayerController hero = CharacterFactory.CreateCharacter(
            characterID, 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
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
        AssertThat(hero.CurrentState).IsEqual(CharacterState.Idle);
        hero.IsFacingRight = true;
        return hero;
    }

    private static EnemyController CreateEnemy(Node parent, string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        // Never aggro on its own: every case decides when this mob acts.
        data.AggroRadius = 0f;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        parent.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        // The mob stands still: only the case's own calls advance it.
        enemy.SetPhysicsProcess(false);
        return enemy;
    }

    private static void Teardown(Node host) {
        InputManager.Instance?.ClearInputSource(0);
        if (!GodotObject.IsInstanceValid(host)) return;
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
