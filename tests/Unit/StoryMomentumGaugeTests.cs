using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The Story Momentum gauge (meter and Defy update 2026-10-31, plan 2.1-2.3): four pips shared by
/// Special 1 and Special 2, one spent per accepted Special, a 2 s reuse delay per slot, a pip back
/// every 12 s that pauses under the cooldowns' own pauses, the not-yet flash on a pip-less press,
/// and the movement ability keeping its own cooldown. Landed-swing gains are pinned in
/// <c>StoryHitConfirmTests</c>, persistence in <c>StoryResourceTimerPersistenceTests</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryMomentumGaugeTests {
    private const double Step = 1.0 / 60.0;
    private static readonly int Lockout = BasicComboRules.AbilityLockoutFrames;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(61000f, 0f);

    [TestCase]
    public void TheGaugeStartsAtTwoPipsAndCapsAtFour() {
        Run((player, events) => {
            AssertThat(BasicComboRules.MomentumStartPips).IsEqual(2);
            AssertThat(BasicComboRules.MomentumMaxPips).IsEqual(4);
            AssertFloat(player.MomentumPips).IsEqualApprox(2f, 0.05f);
            player.RestoreMomentumPips(9f);
            AssertThat(player.MomentumPips).OverrideFailureMessage("The gauge caps at four pips.").IsEqual(4f);
            player.RestoreMomentumPips(-3f);
            AssertThat(player.MomentumPips).OverrideFailureMessage("The gauge never goes below zero.").IsEqual(0f);
        });
    }

    [TestCase]
    public void AnAcceptedSpecialSpendsOnePipAndArmsOnlyItsOwnTwoSecondReuseDelay() {
        Run((player, events) => {
            player.RestoreMomentumPips(2f);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertFloat(player.MomentumPips).OverrideFailureMessage("Special 1 costs exactly one pip.")
                .IsEqualApprox(1f + 1f / 720f, 0.002f);
            AssertFloat(player.SpecialOneCooldownTimer)
                .OverrideFailureMessage("The slot's reuse delay is 2 s, not the authored cooldown.")
                .IsEqualApprox(BasicComboRules.SpecialReuseDelayFrames / 60f, 0.0001f);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
            AssertThat(player.IsSpecialReady(AbilitySlot.Special1)).IsFalse();
            AssertThat(player.IsSpecialReady(AbilitySlot.Special2)).IsTrue();

            // The other Special is free once the 30-frame lockout (unchanged) has run.
            EndTheCast(player, "Special1");
            for (int tick = 1; tick < Lockout; tick++) Tick(player, GameplayButtons.None);
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(player.SpecialTwoCooldownTimer > 0f).IsTrue();
            AssertThat(player.MomentumPips < 0.5f).OverrideFailureMessage("Two Specials cost two pips.").IsTrue();
        });
    }

    [TestCase]
    public void APipLessPressIsRefusedSpendsNothingAndFlashesNotYet() {
        Run((player, events) => {
            player.RestoreMomentumPips(0.5f);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.SpecialOneCooldownTimer).OverrideFailureMessage("A refused press arms nothing.").IsEqual(0f);
            AssertFloat(player.MomentumPips).IsEqualApprox(0.5f, 0.002f);
            AssertThat(events.Count).OverrideFailureMessage("P14: a 0-pip press plays the not-yet flash.").IsEqual(1);
            AssertThat(events[0].Slot).IsEqual(AbilitySlot.Special1);
            AssertThat(events[0].PlayerIndex).IsEqual(player.PlayerIndex);

            events.Clear();
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(events.Count).IsEqual(1);
            AssertThat(events[0].Slot).IsEqual(AbilitySlot.Special2);

            // A whole pip in hand and the same press casts.
            events.Clear();
            player.RestoreMomentumPips(1f);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            AssertThat(events.Count).IsEqual(0);
        });
    }

    [TestCase]
    public void TheReuseDelayRefusesTheSlotWithoutAFlashAndEndsAfterTwoSeconds() {
        Run((player, events) => {
            player.RestoreMomentumPips(3f);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            EndTheCast(player, "Special1");
            for (int tick = 0; tick < Lockout; tick++) Tick(player, GameplayButtons.None);
            events.Clear();

            // Past the 30-frame lockout, inside the 120-frame delay: refused, no flash (its sweep shows).
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsNotEqual(CharacterState.UsingSpecial);
            AssertThat(events.Count).OverrideFailureMessage("A reuse-delay refusal does not flash.").IsEqual(0);
            AssertThat(player.SpecialReuseRemainingSeconds(AbilitySlot.Special1) > 0f).IsTrue();

            for (int tick = 0; tick < BasicComboRules.SpecialReuseDelayFrames + 2; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.SpecialReuseRemainingSeconds(AbilitySlot.Special1)).IsEqual(0f);
            AssertThat(player.IsSpecialReady(AbilitySlot.Special1)).IsTrue();
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).OverrideFailureMessage("Ready again after the delay.")
                .IsEqual(CharacterState.UsingSpecial);
        });
    }

    [TestCase]
    public void TheGaugeRegeneratesOnePipEveryTwelveSecondsTimesTheRegenMultiplierAndCaps() {
        Run((player, events) => {
            player.RestoreMomentumPips(0f);
            for (int tick = 0; tick < BasicComboRules.MomentumRegenFramesPerPip; tick++) Tick(player, GameplayButtons.None);
            AssertFloat(player.MomentumPips).OverrideFailureMessage("720 ticks (12 s) refill exactly one pip.")
                .IsEqualApprox(1f, 0.002f);

            player.RestoreMomentumPips(0f);
            player.MomentumRegenMultiplier = 1.1f;
            for (int tick = 0; tick < BasicComboRules.MomentumRegenFramesPerPip; tick++) Tick(player, GameplayButtons.None);
            AssertFloat(player.MomentumPips).OverrideFailureMessage("A MomentumRegen node refills 10% faster.")
                .IsEqualApprox(1.1f, 0.002f);

            player.RestoreMomentumPips(3.999f);
            for (int tick = 0; tick < 30; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.MomentumPips).OverrideFailureMessage("Regen stops at four pips.").IsEqual(4f);
        });
    }

    [TestCase]
    public void TheGaugeRegenPausesInTheHeroesOwnHitstopAndUnderTimeFreeze() {
        Run((player, events) => {
            player.RestoreMomentumPips(1f);
            player.ApplyHitstop(30);
            for (int tick = 0; tick < 20; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.MomentumPips).OverrideFailureMessage("Hitstop pauses the regen.").IsEqual(1f);
            for (int tick = 0; tick < 20; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.MomentumPips > 1f).OverrideFailureMessage("It resumes after the freeze.").IsTrue();

            player.RestoreMomentumPips(1f);
            player.TimeFrozen = true;
            try {
                for (int tick = 0; tick < 60; tick++) Tick(player, GameplayButtons.None);
                AssertThat(player.MomentumPips).OverrideFailureMessage("Time Freeze pauses the regen.").IsEqual(1f);
            } finally {
                player.TimeFrozen = false;
            }
        });
    }

    [TestCase]
    public void TheMovementAbilityKeepsItsOwnCooldownAndSpendsNoPip() {
        Run((player, events) => {
            player.RestoreMomentumPips(2f);
            Tick(player, GameplayButtons.MovementAbility);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingMovementAbility);
            AssertThat(player.MovementAbilityCooldownTimer > BasicComboRules.SpecialReuseDelayFrames / 60f)
                .OverrideFailureMessage("The movement ability keeps its authored cooldown (G1.5), not the 2 s delay.")
                .IsTrue();
            AssertThat(player.MomentumPips >= 2f).OverrideFailureMessage("It spends no pip.").IsTrue();
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
        });
    }

    [TestCase]
    public void ADormantSlotNeverSpendsAndTheGaugeStillFills() {
        Run((player, events) => {
            player.ApplyLegacyUnlockLocks(Array.Empty<AbilitySlot>());
            player.RestoreMomentumPips(2f);
            events.Clear();
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
            AssertThat(player.MomentumPips >= 2f).OverrideFailureMessage("A Dormant slot spends no pip.").IsTrue();
            AssertThat(events.Count).OverrideFailureMessage("A Dormant slot is refused outright, with no flash.").IsEqual(0);
            float before = player.MomentumPips;
            for (int tick = 0; tick < 120; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.MomentumPips > before).OverrideFailureMessage("G4.1: the gauge fills while Dormant.").IsTrue();
        });
    }

    [TestCase]
    public void ARefusedSpecialToSpecialPressInsideTheThirtyFrameLockoutSpendsNoPip() {
        Run((player, events) => {
            player.RestoreMomentumPips(3f);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            float afterFirst = player.MomentumPips;
            EndTheCast(player, "Special1");
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.SpecialTwoCooldownTimer).OverrideFailureMessage("S locks Special 2 for 30 frames.").IsEqual(0f);
            AssertThat(player.MomentumPips >= afterFirst).OverrideFailureMessage("A locked press spends no pip.").IsTrue();
            AssertThat(events.Count).OverrideFailureMessage("The lockout refusal flashes, as before.").IsEqual(1);
            AssertThat(events[0].Slot).IsEqual(AbilitySlot.Special2);
        });
    }

    [TestCase]
    public void TheReuseDelayAndTheRegenTickInPlainSecondsNotTheCooldownMultiplier() {
        Run((player, events) => {
            // A Story cooldown perk shortens the movement ability's cooldown only: the reuse delay
            // and the regen read plain seconds (P3).
            player.StoryCooldownMultiplier = 0.5f;
            player.RestoreMomentumPips(0f);
            player.SpecialOneCooldownTimer = 2f;
            player.MovementAbilityCooldownTimer = 2f;
            for (int tick = 0; tick < 60; tick++) Tick(player, GameplayButtons.None);
            AssertFloat(player.SpecialOneCooldownTimer).IsEqualApprox(1f, 0.02f);
            AssertThat(player.MovementAbilityCooldownTimer < 0.2f).OverrideFailureMessage("The movement timer keeps the multiplier.").IsTrue();
            AssertFloat(player.MomentumPips).IsEqualApprox(60f / 720f, 0.002f);
        });
    }

    // === helpers ==============================================================

    /// <summary>
    /// The ability node ticks itself in the scene, which these direct-drive cases do not run: end the
    /// cast the way an interruption does and let the controller leave <c>UsingSpecial</c>.
    /// </summary>
    private static void EndTheCast(PlayerController player, string specialNode) {
        player.GetNode<BaseSpecial>(specialNode).Interrupt();
        Tick(player, GameplayButtons.None);
        AssertThat(player.CurrentState).OverrideFailureMessage("Harness: the cast did not end.")
            .IsNotEqual(CharacterState.UsingSpecial);
    }

    private static void Run(Action<PlayerController, List<AbilityLockoutRefusedPayload>> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        EventBus bus = EventBus.Instance;
        AssertObject(bus).IsNotNull();
        var host = new Node2D { Name = "MomentumGaugeHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "MomentumGaugePools" };
        tree.Root.AddChild(pools);
        var floor = new StaticBody2D {
            Name = "MomentumGaugeFloor",
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
                .OverrideFailureMessage($"setup: {player.CurrentState} onFloor={player.IsOnFloor()}")
                .IsEqual(CharacterState.Idle);
            events.Clear();
            body(player, events);
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
