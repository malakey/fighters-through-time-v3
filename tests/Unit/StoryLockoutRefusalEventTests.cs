using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// D10 (user direction 2026-10-11, ledger <c>USER-NOT-YET-FLASH</c>), Story
/// side: <c>PlayerController</c> raises <see cref="EventBus.OnAbilityLockoutRefused"/>
/// at the exact site the ability lockout refuses a ready Special 1, Special 2 or
/// Ultimate press — the slot unlocked and off cooldown (the Ultimate: its meter
/// full), in a state that polls the press — and never for a press a cooldown,
/// an unpaid meter, Time Freeze or Suppression refused, or one that was
/// accepted. A meter-paid Ultimate held back by the lockout claims the tick and
/// is the only slot reported (§1.7); the movement ability neither suppresses
/// nor causes a report. A press held through a landed swing's freeze and
/// refused at the thaw is reported on the thaw tick (the addendum's "presses
/// refused by the lockout are still dropped and get the D10 flash").
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryLockoutRefusalEventTests {
    private const double Step = 1.0 / 60.0;
    private static readonly int Lockout = BasicComboRules.AbilityLockoutFrames;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(53000f, 0f);

    [TestCase]
    public void ALockoutRefusalRaisesTheNotYetEventOnlyForAReadySlot() {
        // A whiffed swing's lockout refuses a ready Special 1 on T+5: one event.
        Run((player, host, events) => {
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 1; tick < 5; tick++) Tick(player, GameplayButtons.None);
            AssertThat(events.Count).IsEqual(0);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertEvents(events, AbilitySlot.Special1);

            // The same press with the slot still cooling: refused by its cooldown, no event.
            events.Clear();
            player.SpecialOneCooldownTimer = 5f;
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.AbilityLockoutFramesRemaining > 0).IsTrue();
            AssertThat(events.Count).OverrideFailureMessage("A cooling slot never flashes.").IsEqual(0);
        });

        // The Ultimate: an unpaid meter is not a lockout refusal; a full one is.
        Run((player, host, events) => {
            var meter = player.GetNode<UltimateMeter>("UltimateMeter");
            meter.SetValue(UltimateMeter.MaxValue - 1f);
            Tick(player, GameplayButtons.BasicAttack);
            Tick(player, GameplayButtons.Ultimate);
            AssertThat(events.Count).OverrideFailureMessage("An unpaid Ultimate never flashes.").IsEqual(0);
            meter.SetValue(UltimateMeter.MaxValue);
            Tick(player, GameplayButtons.Ultimate);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(meter.CurrentValue).IsEqual(UltimateMeter.MaxValue);
            AssertEvents(events, AbilitySlot.Ultimate);
        });

        // A chain swing starting this tick beats a same-tick Special and re-arms
        // the lockout, as the sim does: that refusal is reported too.
        Run((player, host, events) => {
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 1; tick < Lockout + 4; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
            AssertThat(events.Count).IsEqual(0);
            Tick(player, GameplayButtons.BasicAttack | GameplayButtons.Special1);
            AssertThat(player.ComboCounter).OverrideFailureMessage("Harness: hit 2 starts in the chain hold.").IsEqual(1);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertEvents(events, AbilitySlot.Special1);
        });

        // Time Freeze and Suppression refuse first and flash nothing.
        Run((player, host, events) => {
            player.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);
            Tick(player, GameplayButtons.BasicAttack);
            player.TimeFrozen = true;
            Tick(player, GameplayButtons.Special1);
            Tick(player, GameplayButtons.Ultimate);
            player.TimeFrozen = false;
            player.SetAbilityCastSuppressed(true);
            Tick(player, GameplayButtons.Special2);
            Tick(player, GameplayButtons.Ultimate);
            player.SetAbilityCastSuppressed(false);
            AssertThat(player.AbilityLockoutFramesRemaining > 0).IsTrue();
            AssertThat(events.Count)
                .OverrideFailureMessage("Time Freeze and Suppression refusals are not lockout refusals.")
                .IsEqual(0);
        });

        // A Special pressed inside a landed swing's freeze is held, and the gate
        // refuses it on the thaw tick (a Special accepted 30 frames earlier still
        // locks): no event while frozen, one on the thaw tick.
        Run((player, host, events) => {
            Tick(player, GameplayButtons.Special1);
            player.TransitionTo(CharacterState.Idle);
            events.Clear();
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            box.EmitSignal(Area2D.SignalName.AreaEntered, mob.GetNode<Hurtbox>("Hurtbox"));
            AssertThat(player.SwingLanded).OverrideFailureMessage("Harness: the swing lands.").IsTrue();
            int freeze = player.HitstopFramesRemaining;
            AssertThat(freeze >= 2).OverrideFailureMessage("Harness: the landing freezes the hero.").IsTrue();
            AssertThat(player.SpecialLockoutFramesRemaining > freeze + 2)
                .OverrideFailureMessage("Harness: Special 1's lockout must outlast the freeze.")
                .IsTrue();
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.FreezeBufferedSpecials).IsEqual(GameplayButtons.Special2);
            for (int tick = 1; tick < freeze; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.IsInHitstop).IsFalse();
            AssertThat(events.Count).OverrideFailureMessage("A press held through the freeze is not refused yet.").IsEqual(0);
            Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.UsingSpecial);
            AssertEvents(events, AbilitySlot.Special2);
        });
    }

    [TestCase]
    public void AHeldBackUltimateAndTheChordRaiseOnlyTheirOwnSlots() {
        // After a landed swing (Specials open, the Ultimate still locked), the
        // chord at a full meter is held back and reports the Ultimate alone.
        Run((player, host, events) => {
            player.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue);
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            box.EmitSignal(Area2D.SignalName.AreaEntered, mob.GetNode<Hurtbox>("Hurtbox"));
            for (int tick = 0; tick < 30 && player.IsInHitstop; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.SwingLanded).IsTrue();
            AssertThat(player.SpecialGateFramesRemaining).IsEqual(0);
            AssertThat(player.AbilityLockoutFramesRemaining > 1).IsTrue();
            AssertThat(events.Count).IsEqual(0);

            Tick(player, GameplayButtons.Special2 | GameplayButtons.Ultimate);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertEvents(events, AbilitySlot.Ultimate);

            // Special 2 alone next tick casts: an acceptance reports nothing.
            events.Clear();
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(events.Count).IsEqual(0);
        });

        // Special 1 + Special 2 with the gate open: Special 1 casts and its own
        // lockout refusing the same-tick Special 2 is not reported.
        Run((player, host, events) => {
            Tick(player, GameplayButtons.Special1 | GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(events.Count).IsEqual(0);
        });

        // The chord at a meter one short, in Idle after a whiffed swing whose
        // lockout still runs: Special 2 reports and the movement ability fires.
        Run((player, host, events) => {
            player.GetNode<UltimateMeter>("UltimateMeter").SetValue(UltimateMeter.MaxValue - 1f);
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 0; tick < 40 && player.CurrentState != CharacterState.Blocking; tick++) {
                Tick(player, GameplayButtons.Block);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Harness: Block cancels the swing's recovery.")
                .IsEqual(CharacterState.Blocking);
            for (int tick = 0; tick < 5 && player.CurrentState != CharacterState.Idle; tick++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.AbilityLockoutFramesRemaining > 0)
                .OverrideFailureMessage("Harness: the whiffed swing's lockout still runs in Idle.")
                .IsTrue();
            AssertThat(events.Count).IsEqual(0);

            Tick(player, GameplayButtons.Special2 | GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The movement ability is outside the lockout and fires.")
                .IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
            AssertEvents(events, AbilitySlot.Special2);
        });
    }

    // === helpers ==============================================================

    private static void AssertEvents(List<AbilityLockoutRefusedPayload> events, params AbilitySlot[] slots) {
        var seen = new List<string>();
        foreach (AbilityLockoutRefusedPayload payload in events) seen.Add($"{payload.PlayerIndex}:{payload.Slot}");
        var expected = new List<string>();
        foreach (AbilitySlot slot in slots) expected.Add($"0:{slot}");
        AssertThat(string.Join(", ", seen)).IsEqual(string.Join(", ", expected));
    }

    /// <summary>Starts a swing and ticks until its box opens.</summary>
    private static Hitbox OpenBox(PlayerController player) {
        var box = player.GetNode<Hitbox>("MeleeHitbox");
        Tick(player, GameplayButtons.BasicAttack);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        for (int tick = 0; tick < 30 && !box.IsActive; tick++) Tick(player, GameplayButtons.None);
        AssertThat(box.IsActive).OverrideFailureMessage("Harness: the swing's box never opened.").IsTrue();
        return box;
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

    /// <summary>
    /// The StoryAbilityLockoutTests harness, with the host exposed for targets
    /// and every <see cref="EventBus.OnAbilityLockoutRefused"/> collected (the
    /// subscription is dropped in the finally, before the host is freed).
    /// </summary>
    private static void Run(Action<PlayerController, Node2D, List<AbilityLockoutRefusedPayload>> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        EventBus bus = EventBus.Instance;
        AssertObject(bus).IsNotNull();
        var host = new Node2D { Name = "LockoutRefusalHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "LockoutRefusalPools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "LockoutRefusalFloor",
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
        var events = new List<AbilityLockoutRefusedPayload>();
        void Collect(AbilityLockoutRefusedPayload payload) => events.Add(payload);
        bus.OnAbilityLockoutRefused += Collect;
        try {
            for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                player.Velocity = new Vector2(player.Velocity.X, 400f);
                Tick(player, GameplayButtons.None);
            }
            for (int frame = 0; frame < 60 && player.CurrentState != CharacterState.Idle; frame++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()} pos={player.GlobalPosition}")
                .IsEqual(CharacterState.Idle);
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
            events.Clear();
            body(player, host, events);
        } finally {
            bus.OnAbilityLockoutRefused -= Collect;
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    /// <summary>One controller tick; every held button reads as freshly pressed.</summary>
    private static void Tick(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
