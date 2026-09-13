using System.Collections.Generic;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;
using GdUnit4;
using Godot;
using xpTURN.Klotho.Deterministic.Math;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.6 Suppression (Package 11 A1) — the Eraser line's Null Lance. A control-slot,
/// <b>Story-only</b> status whose whole effect is an ability-CAST lock: Special 1,
/// Special 2, the Movement Ability and the Ultimate cast are refused without
/// consuming a cooldown or meter, while cooldowns keep ticking. Basics, block,
/// grab, Rally, DI, landing tech, Defy History, death rewinds and Time Freeze are
/// explicitly untouched, and no path in scripts/FighterSim may ever apply it.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SuppressionTests {
    private const float Step = 1f / 60f;

    [TestCase]
    public void SuppressionRoutesToTheControlSlotAndDrivesTheCastLock() {
        (PlayerController player, StatusController status) = CreateSubject();
        try {
            AssertThat(StatusRouting.SlotOf(StatusType.Suppression))
                .OverrideFailureMessage("Suppression is a control-slot status.")
                .IsEqual(StatusSlot.Control);

            status.ApplyStatus(StatusType.Suppression, SuppressionStrategy.StandardDurationSeconds);
            AssertThat(status.ControlStatusType).IsEqual(StatusType.Suppression);
            AssertThat(player.IsAbilityCastSuppressed)
                .OverrideFailureMessage("Applying Suppression must raise the cast lock.")
                .IsTrue();

            // It obeys the two-slot rule: a Venom in the damage slot is untouched,
            // and Suppression replaces a Root in its own slot.
            status.ApplyStatus(StatusType.Venom, 3f);
            AssertThat(status.DamageStatusType).IsEqual(StatusType.Venom);
            AssertThat(player.IsAbilityCastSuppressed).IsTrue();

            // Expiry releases the lock.
            for (int frame = 0; frame < 130; frame++) status._PhysicsProcess(Step);
            AssertThat(status.ControlStatusType).IsEqual(StatusType.None);
            AssertThat(player.IsAbilityCastSuppressed)
                .OverrideFailureMessage("Suppression expiring must release the cast lock.")
                .IsFalse();
        } finally {
            player.Free();
        }
    }

    [TestCase]
    public void AllFourCastSitesAreRefusedWithoutConsumingCooldownOrMeter() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            player.CurrentUltimateMeter = 100f;
            player.SetAbilityCastSuppressed(true);

            foreach (GameplayButtons button in new[] {
                GameplayButtons.Special1, GameplayButtons.Special2,
                GameplayButtons.MovementAbility, GameplayButtons.Ultimate
            }) {
                player.SpecialOneCooldownTimer = 0f;
                player.SpecialTwoCooldownTimer = 0f;
                player.MovementAbilityCooldownTimer = 0f;
                SendInput(player, button);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"A suppressed {button} press must not start a cast.")
                    .IsEqual(CharacterState.Idle);
                AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
                AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
                AssertThat(player.MovementAbilityCooldownTimer).IsEqual(0f);
                AssertThat(player.CurrentUltimateMeter)
                    .OverrideFailureMessage("A refused ultimate must not spend the meter.")
                    .IsEqual(100f);
            }
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void CooldownsKeepTickingWhileSuppressed() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            player.SetAbilityCastSuppressed(true);
            player.SpecialOneCooldownTimer = 1f;
            player.SpecialTwoCooldownTimer = 1f;
            player.MovementAbilityCooldownTimer = 1f;

            for (int frame = 0; frame < 30; frame++) SendInput(player, GameplayButtons.None);

            AssertThat(player.SpecialOneCooldownTimer < 0.6f)
                .OverrideFailureMessage("Special 1's cooldown must keep running under Suppression.")
                .IsTrue();
            AssertThat(player.SpecialTwoCooldownTimer < 0.6f)
                .OverrideFailureMessage("Special 2's cooldown must keep running under Suppression.")
                .IsTrue();
            AssertThat(player.MovementAbilityCooldownTimer < 0.6f)
                .OverrideFailureMessage("The movement cooldown must keep running under Suppression.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void BasicsBlockAndTheUniversalVerbsAreUntouchedBySuppression() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        try {
            player.SetAbilityCastSuppressed(true);

            // Basics.
            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Suppression must never lock the basic string.")
                .IsEqual(CharacterState.Attacking);
            for (int frame = 0; frame < 60; frame++) SendInput(player, GameplayButtons.None);

            // Block.
            SendInput(player, GameplayButtons.Block);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Suppression must never lock the block stance.")
                .IsEqual(CharacterState.Blocking);
            SendInput(player, GameplayButtons.None);

            // Roll (the universal evasive verb).
            SendInput(player, GameplayButtons.Roll);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Suppression must never lock the roll.")
                .IsEqual(CharacterState.Rolling);
            for (int frame = 0; frame < 40; frame++) SendInput(player, GameplayButtons.None);

            // Rally's echo pool and Defy History are untouched state, not casts:
            // Suppression cannot consume or bar them.
            AssertThat(player.StoryDefyHistoryUsed)
                .OverrideFailureMessage("Suppression must not spend Defy History.")
                .IsFalse();
            AssertThat(player.IsAbilityCastSuppressed).IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    [TestCase]
    public void EveryTransitionPublishesAllFourSlotsOnTheBusAndSmothersTheAura() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        var seen = new List<AbilitySlotLockPayload>();
        void Record(AbilitySlotLockPayload payload) => seen.Add(payload);
        EventBus.Instance.OnAbilitySlotLockChanged += Record;
        try {
            player.SetAbilityCastSuppressed(true);
            AssertThat(seen.Count)
                .OverrideFailureMessage("Every suppressible slot must be published.")
                .IsEqual(4);
            foreach (AbilitySlotLockPayload payload in seen) {
                AssertThat(payload.State).IsEqual(AbilitySlotLockState.Suppressed);
            }
            AssertThat(player.Glow?.IsAuraSmothered)
                .OverrideFailureMessage("Suppression desaturates the persistent aura.")
                .IsTrue();

            // A repeated set is idempotent: no duplicate events.
            seen.Clear();
            player.SetAbilityCastSuppressed(true);
            AssertThat(seen.Count).IsEqual(0);

            player.SetAbilityCastSuppressed(false);
            AssertThat(seen.Count).IsEqual(4);
            foreach (AbilitySlotLockPayload payload in seen) {
                AssertThat(payload.State).IsEqual(AbilitySlotLockState.Clear);
            }
            AssertThat(player.Glow?.IsAuraSmothered).IsFalse();
        } finally {
            EventBus.Instance.OnAbilitySlotLockChanged -= Record;
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    /// <summary>
    /// The design is explicit: "Fighter Mode is untouched — no Suppression source
    /// exists in the sim, and none may be added without a separate ruling." An
    /// authored resource carrying it must be refused at the simulation's single
    /// status chokepoint, and a blocked Null Lance suppresses nothing anywhere.
    /// </summary>
    [TestCase]
    public void TheDeterministicSimulationRefusesSuppressionOutright() {
        var runtime = new FighterRuntimeComponent {
            StatusType = (int)StatusType.None,
            StatusFrames = 0,
            StatusIntensity = FP64.Zero
        };
        var state = new FighterStateComponent { HitstunFrames = 0 };

        // A control status the sim DOES accept, to prove the harness is live.
        FighterDamageRules.ApplyStatus(
            ref state, ref runtime, (int)StatusType.Root, 60, FP64.One);
        AssertThat(runtime.StatusType).IsEqual((int)StatusType.Root);

        FighterDamageRules.ApplyStatus(
            ref state, ref runtime, (int)StatusType.Suppression, 120, FP64.One);
        AssertThat(runtime.StatusType)
            .OverrideFailureMessage("No path in scripts/FighterSim may apply Suppression.")
            .IsEqual((int)StatusType.Root);
        AssertThat(runtime.StatusFrames)
            .OverrideFailureMessage("A refused Suppression must not touch the slot's duration.")
            .IsEqual(60);

        // And onto an EMPTY control slot it still lands nowhere.
        var empty = new FighterRuntimeComponent { StatusType = (int)StatusType.None };
        FighterDamageRules.ApplyStatus(
            ref state, ref empty, (int)StatusType.Suppression, 120, FP64.One);
        AssertThat(empty.StatusType).IsEqual((int)StatusType.None);
    }

    // === Harness ===

    private static (PlayerController player, StatusController status) CreateSubject() {
        var player = new PlayerController { CurrentHP = 100, PlayerIndex = 0 };
        var status = new StatusController { Name = "StatusController" };
        player.AddChild(status);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        return (player, status);
    }

    private static PlayerController CreateGroundedPlayer() {
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = new Vector2(0f, -8f);
        var neutral = new BufferedInputSource();
        neutral.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
        InputManager.Instance.SetInputSource(player.PlayerIndex, neutral);
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            player._PhysicsProcess(1.0 / 60.0);
        }
        for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
            player._PhysicsProcess(1.0 / 60.0);
        }
        AssertThat(player.CurrentState)
            .OverrideFailureMessage("The harness player must settle to Idle before a test scripts input.")
            .IsEqual(CharacterState.Idle);
        return player;
    }

    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "SuppressionTestFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(floor);
        return floor;
    }

    private static void SendInput(PlayerController player, GameplayButtons buttons) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(1.0 / 60.0);
    }
}
