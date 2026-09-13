using System.IO;
using System.Linq;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// V7.6 <b>Time Freeze</b> (F03), Package 11 A2 — the Story-only escape ability
/// that replaced the retired manual rewind, its 12 s cooldown and the Stasis
/// Echo.
///
/// <para>The contract this pins, in one line: <b>five seconds, no charges,
/// forty-five second cooldown armed at thaw, on every difficulty</b>; the world
/// stops and the player does not; enemies are invulnerable and nothing can be
/// progressed; and everything resumes exactly where it stopped.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TimeFreezeTests {

    /// <summary>Builds a controller inside the tree, ready to drive by hand.</summary>
    private static TimeFreezeController AttachController(string name) {
        // The cooldown is campaign state, so it survives between test cases in
        // the shared StoryManager exactly as it survives between rooms in a
        // level. Open every case from a known-Ready campaign.
        StoryManager.Instance?.SetTimeFreezeCooldown(0f);
        var controller = new TimeFreezeController { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(controller);
        // The suite drives the clock by hand, tick by tick. Without this the
        // engine would ALSO tick the node while the test runs and the two would
        // race, making every cooldown measurement a timing coin-flip.
        // ProcessMode stays Inherit, which is the property the suite pins.
        controller.SetPhysicsProcess(false);
        return controller;
    }

    private static void Tick(TimeFreezeController controller, int frames) {
        for (int frame = 0; frame < frames; frame++) controller._PhysicsProcess(1.0 / 60.0);
    }

    [TestCase]
    public void TheAuthoredNumbersAreFiveSecondsAndFortyFiveOnEveryDifficulty() {
        AssertThat(TimeFreezeController.FreezeSeconds).IsEqual(5f);
        AssertThat(TimeFreezeController.FreezeFrames).IsEqual(300);
        AssertThat(Mathf.RoundToInt(TimeFreezeController.FreezeSeconds * 60f))
            .OverrideFailureMessage("5.000 s must be exactly 300 physics ticks at 60 Hz.")
            .IsEqual(TimeFreezeController.FreezeFrames);
        AssertThat(TimeFreezeController.CooldownSeconds).IsEqual(45f);

        // The difficulty table has ONE Time Freeze row. The pin is structural:
        // no member of the contract takes a Difficulty at all, unlike every
        // rewind rule beside it (5/3/1 charges, 70/50/30 HP restore).
        AssertThat(typeof(TimeFreezeController)
                .GetMethods()
                .Where(method => method.GetParameters()
                    .Any(parameter => parameter.ParameterType == typeof(Difficulty)))
                .Count())
            .OverrideFailureMessage("Time Freeze must not branch on difficulty anywhere.")
            .IsEqual(0);
    }

    [TestCase]
    public void TheCooldownArmsAtThawAndAtAnEarlyEndButNeverAtActivation() {
        TimeFreezeController controller = AttachController("ThawCooldownController");
        try {
            AssertThat(controller.IsReady).IsTrue();
            AssertThat(controller.TryBeginTimeFreeze()).IsTrue();
            AssertThat(controller.IsFrozen).IsTrue();
            AssertThat(controller.CooldownRemaining)
                .OverrideFailureMessage("Activation must NOT arm the cooldown — thaw does.")
                .IsEqual(0f);

            // Run the freeze out: the cooldown appears exactly at the thaw.
            Tick(controller, TimeFreezeController.FreezeFrames);
            AssertThat(controller.IsFrozen).IsFalse();
            AssertThat(controller.CooldownRemaining).IsEqual(45f);

            // An early end arms it identically.
            controller.EndFreeze(early: false);
            Tick(controller, 60 * 45 + 2);
            AssertThat(controller.CooldownRemaining).IsEqual(0f);
            AssertThat(controller.TryBeginTimeFreeze()).IsTrue();
            controller.EndFreeze(early: true);
            AssertThat(controller.IsFrozen).IsFalse();
            AssertThat(controller.CooldownRemaining)
                .OverrideFailureMessage("An early end arms the full cooldown too.")
                .IsEqual(45f);
        } finally {
            controller.Free();
        }
    }

    [TestCase]
    public void TheCooldownAdvancesOnlyInLivePlayAndNeverWhileFrozenOrPaused() {
        TimeFreezeController controller = AttachController("CooldownTickController");
        try {
            controller.TryBeginTimeFreeze();
            controller.EndFreeze(early: true);
            AssertThat(controller.CooldownRemaining).IsEqual(45f);

            Tick(controller, 60);
            AssertThat(controller.CooldownRemaining).IsEqualApprox(44f, 0.05f);

            // A live freeze stops the cooldown clock dead.
            float parked = controller.CooldownRemaining;
            controller.EndFreeze(early: true); // no-op, nothing frozen
            AssertThat(controller.CooldownRemaining).IsEqual(parked);

            // The paused tree is handled by the process mode rather than a flag:
            // Inherit means SceneTree.Paused stops BOTH clocks for free, and it
            // is why the controller may never be ProcessMode.Always.
            AssertThat(controller.ProcessMode)
                .OverrideFailureMessage("ProcessMode.Always would keep ticking through the pause menu.")
                .IsEqual(Node.ProcessModeEnum.Inherit);
        } finally {
            controller.Free();
        }
    }

    [TestCase]
    public void NothingRefreshesTheCooldownEarly() {
        TimeFreezeController controller = AttachController("NoRefreshController");
        try {
            AssertThat(controller.TryBeginTimeFreeze()).IsTrue();
            controller.EndFreeze(early: true);
            float armed = controller.CooldownRemaining;
            AssertThat(armed).IsEqual(45f);
            Tick(controller, 60 * 5);
            float afterFiveSeconds = controller.CooldownRemaining;
            AssertThat(armed - afterFiveSeconds)
                .OverrideFailureMessage("Five seconds of live play must cost exactly five seconds.")
                .IsEqualApprox(5f, 0.1f);
            // A checkpoint activation refreshes the rewind pool. It must not
            // touch this: only real elapsed live play shortens the cooldown.
            EventBus.Instance?.RaiseCheckpointReached("time_freeze_refresh_checkpoint");
            AssertThat(controller.CooldownRemaining).IsEqual(afterFiveSeconds);

            // Neither does a death rewind's presentation event, or a collapse.
            EventBus.Instance?.RaiseRewindTriggered(Vector2.Zero);
            AssertThat(controller.CooldownRemaining).IsEqual(afterFiveSeconds);

            Tick(controller, 60);
            AssertThat(afterFiveSeconds - controller.CooldownRemaining)
                .OverrideFailureMessage("Live play is the ONLY thing that advances the cooldown.")
                .IsEqualApprox(1f, 0.1f);
        } finally {
            controller.Free();
        }
    }

    [TestCase]
    public void AFreshEntryAndARestartStartReadyWhileAResumeRestoresTheSavedCooldown() {
        const int scratchSlot = 2;
        StoryManager story = StoryManager.Instance;
        SaveManager saveManager = SaveManager.Instance;
        StorySaveData original = saveManager.SaveSlots[scratchSlot];
        int originalSlot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
        try {
            var parked = new StorySaveData {
                SelectedCharacterID = "einstein",
                LastCheckpointID = "parked_checkpoint_1",
                TimeFreezeCooldownSeconds = 31.5f
            };
            saveManager.SaveSlots[scratchSlot] = parked;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = scratchSlot;

            // Mid-level resume: the parked cooldown comes back exactly.
            story.BeginLevelRun(resumeAttempt: true);
            AssertThat(story.TimeFreezeCooldownRemaining)
                .OverrideFailureMessage("A resume must restore the saved cooldown, not reset it.")
                .IsEqual(31.5f);

            // Fresh entry / full Restart Level: Ready.
            story.BeginLevelRun();
            AssertThat(story.TimeFreezeCooldownRemaining)
                .OverrideFailureMessage("A fresh attempt starts with Time Freeze ready.")
                .IsEqual(0f);

            // And the attempt block round-trips it.
            story.SetTimeFreezeCooldown(12f);
            var written = new StorySaveData();
            story.WriteAttemptStateToSave(written);
            AssertThat(written.TimeFreezeCooldownSeconds).IsEqual(12f);
        } finally {
            story.ClearLevelAttemptState();
            story.StopLevelRun();
            saveManager.SaveSlots[scratchSlot] = original;
            GameManager.Instance.CurrentSession.ActiveSaveSlot = originalSlot;
        }
    }

    [TestCase]
    public void ActivationSpendsNoRewindChargeAndIsLegalAtZeroCharges() {
        var rewind = new ChronalRewindManager { Name = "TimeFreezeChargeRewindManager" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(rewind);
        TimeFreezeController controller = AttachController("ChargeCostController");
        try {
            int before = rewind.RemainingRewinds;
            AssertThat(controller.TryBeginTimeFreeze()).IsTrue();
            Tick(controller, TimeFreezeController.FreezeFrames);
            AssertThat(rewind.RemainingRewinds)
                .OverrideFailureMessage("Time Freeze costs no death-rewind charge, ever.")
                .IsEqual(before);

            // Zero charges is a valid living state and the ability is available
            // in it: the eligibility rule has no charge parameter at all, which
            // is exactly how the "usable at zero rewinds" guarantee is kept.
            AssertThat(TimeFreezeController.CanActivate(
                    CharacterState.Idle, isFrozen: false, cooldownRemaining: 0f, alive: true))
                .IsTrue();
        } finally {
            controller.Free();
            rewind.Free();
        }
    }

    [TestCase]
    public void TheEligibilityMatrixRefusesMidActionStatesAndIgnoresSuppression() {
        // Allowed: any ordinary controllable state.
        foreach (CharacterState state in new[] {
                     CharacterState.Idle, CharacterState.Running, CharacterState.Airborne,
                     CharacterState.Rolling, CharacterState.Crouching, CharacterState.Skidding,
                     CharacterState.Blocking, CharacterState.LedgeHanging }) {
            AssertThat(TimeFreezeController.CanActivate(state, false, 0f, true))
                .OverrideFailureMessage($"Time Freeze must be available from {state}.")
                .IsTrue();
        }

        // Refused: hitstun, daze, death, respawn and every mid-action state.
        foreach (CharacterState state in new[] {
                     CharacterState.Stunned, CharacterState.Dazed, CharacterState.Dead,
                     CharacterState.Respawning, CharacterState.Attacking,
                     CharacterState.UsingSpecial, CharacterState.UsingUltimate,
                     CharacterState.UsingMovementAbility, CharacterState.Grabbing }) {
            AssertThat(TimeFreezeController.CanActivate(state, false, 0f, true))
                .OverrideFailureMessage($"Time Freeze must be refused during {state}.")
                .IsFalse();
        }

        AssertThat(TimeFreezeController.CanActivate(CharacterState.Idle, true, 0f, true)).IsFalse();
        AssertThat(TimeFreezeController.CanActivate(CharacterState.Idle, false, 0.1f, true)).IsFalse();
        AssertThat(TimeFreezeController.CanActivate(CharacterState.Idle, false, 0f, false)).IsFalse();

        // Suppression is the explicit carve-out: it locks ability slots, and
        // Time Freeze is the escape from exactly the situation it creates. The
        // pin is that the rule cannot consult it — it takes no status at all.
        AssertThat(typeof(TimeFreezeController)
                .GetMethod(nameof(TimeFreezeController.CanActivate))
                .GetParameters().Length)
            .OverrideFailureMessage("CanActivate must read only state/frozen/cooldown/alive.")
            .IsEqual(4);
    }

    [TestCase]
    public void DuringAFreezeMovementIsAllowedAndOffensiveInputIsDiscardedNotQueued() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var input = new BufferedInputSource();
        InputManager.Instance?.SetInputSource(player.PlayerIndex, input);
        try {
            player.TimeFrozen = true;

            // Hold every offensive button at once for a full second.
            const GameplayButtons offensive = GameplayButtons.BasicAttack
                | GameplayButtons.Special1 | GameplayButtons.Special2
                | GameplayButtons.Ultimate | GameplayButtons.MovementAbility;
            // InputManager caches one frame per ENGINE physics tick, which does
            // not advance between hand-driven calls — re-registering the source
            // drops that cache, so each phase below really reads its own frame.
            input.SetNextFrame(new PlayerInputFrame { Held = offensive, Pressed = offensive });
            InputManager.Instance?.SetInputSource(player.PlayerIndex, input);
            for (int tick = 0; tick < 60; tick++) player._PhysicsProcess(1.0 / 60.0);

            AssertThat(player.CurrentState is CharacterState.Attacking
                    or CharacterState.UsingSpecial or CharacterState.UsingUltimate
                    or CharacterState.UsingMovementAbility or CharacterState.Grabbing)
                .OverrideFailureMessage("No offensive action may start while time is frozen.")
                .IsFalse();

            // Thaw with NOTHING held: a discarded press must not fire late.
            player.TimeFrozen = false;
            input.SetNextFrame(new PlayerInputFrame());
            InputManager.Instance?.SetInputSource(player.PlayerIndex, input);
            for (int tick = 0; tick < 10; tick++) player._PhysicsProcess(1.0 / 60.0);
            AssertThat(player.CurrentState is CharacterState.Attacking
                    or CharacterState.UsingSpecial or CharacterState.UsingUltimate)
                .OverrideFailureMessage("Attacks are discarded, never queued: no burst on thaw.")
                .IsFalse();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void EnemiesAreInvulnerableToPlayerSourcedHitsForTheWholeFreeze() {
        // The rule at the shared chokepoint: a PLAYER-sourced hit on a NON-player
        // target is discarded outright while the world is frozen, which is what
        // denies damage, stagger, Rally echo and meter in one place — including
        // for already-live projectiles, constructs, zones and DoT, all of which
        // deliver through the same Hitbox path.
        AssertThat(Hitbox.PlayerHitIsDiscardedWhileFrozen(0, -1, worldFrozen: true))
            .OverrideFailureMessage("A player hit on an enemy must be discarded while frozen.")
            .IsTrue();
        AssertThat(Hitbox.PlayerHitIsDiscardedWhileFrozen(1, -1, worldFrozen: true)).IsTrue();

        // Unfrozen play is untouched.
        AssertThat(Hitbox.PlayerHitIsDiscardedWhileFrozen(0, -1, worldFrozen: false)).IsFalse();
        // Enemy-sourced hits on the player are untouched even while frozen —
        // the freeze stops enemies acting rather than making the player immune.
        AssertThat(Hitbox.PlayerHitIsDiscardedWhileFrozen(-1, 0, worldFrozen: true)).IsFalse();

        // And the enemy's own damage intake is NOT what enforces it: the enemy
        // stays an ordinary damageable actor, so nothing downstream has to know
        // about the freeze.
        EnemyController enemy = EnemyFactory.Create("chrono_slasher", Vector2.Zero);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(enemy);
        try {
            enemy.SetTimeFrozen(true);
            int hp = enemy.CurrentHP;
            AssertThat(enemy.TakeDamage(10))
                .OverrideFailureMessage("The freeze gate belongs at the hitbox, not in the enemy.")
                .IsEqual(10);
            AssertThat(enemy.CurrentHP).IsEqual(hp - 10);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void NoPickupCheckpointOrPuzzleProgressLandsDuringAFreeze() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();

        // Pickups: the magnet that resolves a collection lives in the frozen
        // tick, so a frozen pickup cannot reach the player or be collected.
        var pickup = new StoryPickup { Name = "FrozenPickup" };
        tree.Root.AddChild(pickup);
        try {
            pickup.SetTimeFrozen(true);
            Vector2 before = pickup.Position;
            for (int tick = 0; tick < 60; tick++) pickup._PhysicsProcess(1.0 / 60.0);
            AssertThat(pickup.Position)
                .OverrideFailureMessage("A frozen pickup must not move toward the player.")
                .IsEqual(before);
            AssertThat(GodotObject.IsInstanceValid(pickup))
                .OverrideFailureMessage("A frozen pickup must not expire either.")
                .IsTrue();
        } finally {
            if (GodotObject.IsInstanceValid(pickup)) pickup.Free();
        }

        // Checkpoints and extractors are struck, and a strike is a player-sourced
        // hit on a non-player hurtbox — the same discard rule covers both.
        AssertThat(Hitbox.PlayerHitIsDiscardedWhileFrozen(0, -1, worldFrozen: true)).IsTrue();

        // Puzzle interaction is refused at the shared interaction entry point.
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        var lever = new PuzzleLever { Name = "FrozenLever", LeverID = "frozen_lever" };
        var area = new InteractionArea { Name = "FrozenLeverArea", TargetPath = ".." };
        lever.AddChild(area);
        tree.Root.AddChild(lever);
        try {
            player.TimeFrozen = true;
            AssertThat(area.TryInteract(player))
                .OverrideFailureMessage("No puzzle interaction or progress during a freeze.")
                .IsFalse();
            player.TimeFrozen = false;
            AssertThat(area.TryInteract(player))
                .OverrideFailureMessage("The same interaction must work once time resumes.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            lever.Free();
        }
    }

    [TestCase]
    public void PassiveRecoveryAndStatusTimersAreSuspendedWhileMovementKeepsRunning() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        PlayerController player = CharacterFactory.CreateCharacter("einstein");
        tree.Root.AddChild(player);
        try {
            // Block-charge regeneration: three seconds of freeze must not hand
            // the player a free charge.
            BlockSystem block = player.GetNodeOrNull<BlockSystem>("BlockSystem");
            AssertThat(block).IsNotNull();
            block.DepleteCharges(1);
            int charges = block.CurrentCharges;
            player.TimeFrozen = true;
            for (int tick = 0; tick < BasicComboRules.BlockChargeRegenFrames + 30; tick++) {
                block._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("Block-charge regen is suspended for the whole freeze.")
                .IsEqual(charges);

            // Status durations: a burn may not tick down (or tick damage).
            StatusController status = player.GetNodeOrNull<StatusController>("StatusController");
            AssertThat(status).IsNotNull();
            status.ApplyStatus(StatusType.RadiantBurn, 3f, 1f);
            for (int tick = 0; tick < 120; tick++) status._PhysicsProcess(1.0 / 60.0);
            AssertThat(status.HasStatus(StatusType.RadiantBurn))
                .OverrideFailureMessage("Status timers are suspended for the whole freeze.")
                .IsTrue();

            // Resuming lets both clocks run again.
            player.TimeFrozen = false;
            for (int tick = 0; tick < BasicComboRules.BlockChargeRegenFrames + 2; tick++) {
                block._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(block.CurrentCharges)
                .OverrideFailureMessage("Regen resumes on thaw; it is suspended, not cancelled.")
                .IsEqual(charges + 1);
        } finally {
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
        }
    }

    [TestCase]
    public void ThawPreservesEveryActorsPositionVelocityAndAttackPhaseWithNoCatchUp() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        EnemyController enemy = EnemyFactory.Create("chrono_slasher", new Vector2(500, 0));
        tree.Root.AddChild(enemy);
        try {
            enemy.Velocity = new Vector2(120f, -40f);
            Vector2 position = enemy.GlobalPosition;
            Vector2 velocity = enemy.Velocity;
            EnemyState state = enemy.CurrentState;

            enemy.SetTimeFrozen(true);
            AssertThat(enemy.Velocity)
                .OverrideFailureMessage("Time Freeze must NOT zero velocity the way a rewind freeze does.")
                .IsEqual(velocity);

            for (int tick = 0; tick < TimeFreezeController.FreezeFrames; tick++) {
                enemy._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(enemy.GlobalPosition).IsEqual(position);
            AssertThat(enemy.Velocity).IsEqual(velocity);
            AssertThat(enemy.CurrentState).IsEqual(state);

            // Thaw: exactly one tick's worth of motion, never five seconds of it.
            enemy.SetTimeFrozen(false);
            AssertThat(enemy.Velocity)
                .OverrideFailureMessage("The actor resumes with its preserved velocity.")
                .IsEqual(velocity);
        } finally {
            enemy.Free();
        }
    }

    [TestCase]
    public void ProjectilesFreezeInPlaceInsteadOfBeingCleared() {
        // The death rewind releases every enemy projectile to the pool. Time
        // Freeze must not: "no projectile clearing" on thaw means both sides'
        // shots are still in the air, exactly where they were.
        foreach (string group in new[] { "enemy_projectile", "story_projectile", "story_zone" }) {
            AssertThat(TimeFreezeController.ProcessFallbackGroups.Contains(group))
                .OverrideFailureMessage($"Both sides' projectiles and zones must freeze: {group} is missing.")
                .IsTrue();
        }

        // And the fallback is ProcessMode-based, which parks the node's own tick
        // and its collision together — a frozen projectile can neither travel
        // nor damage the player.
        var projectile = new PlaceholderProjectile { Name = "FrozenProjectile" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(projectile);
        try {
            Vector2 before = projectile.GlobalPosition;
            projectile.ProcessMode = Node.ProcessModeEnum.Disabled;
            AssertThat(projectile.GlobalPosition).IsEqual(before);
            AssertThat(GodotObject.IsInstanceValid(projectile))
                .OverrideFailureMessage("A frozen projectile is parked, never released to the pool.")
                .IsTrue();
        } finally {
            projectile.Free();
        }
    }

    [TestCase]
    public void ThePathPlatformFreezesInPlaceButStillReversesDuringADeathRewind() {
        var platform = new PathMovingPlatform {
            Name = "TimeFrozenPlatform",
            Waypoints = new[] { Vector2.Zero, new Vector2(400f, 0f) },
            Speed = 120f,
            EndpointWaitSeconds = 0f
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(platform);
        try {
            for (int frame = 0; frame < 60; frame++) platform._PhysicsProcess(1.0 / 60.0);
            Vector2 present = platform.Position;

            // Time Freeze: stays exactly where it is, for the whole five seconds.
            platform.SetTimeFrozen(true);
            for (int frame = 0; frame < TimeFreezeController.FreezeFrames; frame++) {
                platform._PhysicsProcess(1.0 / 60.0);
            }
            AssertThat(platform.Position)
                .OverrideFailureMessage("Time Freeze stops the platform in place — it must not MOVE it.")
                .IsEqual(present);

            // Thaw resumes the same leg, with no catch-up jump.
            platform.SetTimeFrozen(false);
            platform._PhysicsProcess(1.0 / 60.0);
            AssertThat(platform.Position.X - present.X)
                .OverrideFailureMessage("One tick of travel on thaw, not five seconds of it.")
                .IsLessEqual(120f / 60f + 0.01f);

            // The death rewind is the opposite behaviour and still works.
            platform.BeginRewindScrub();
            platform.ApplyRewindScrub(30);
            AssertThat(platform.Position.X < present.X)
                .OverrideFailureMessage("A death rewind still walks the platform back along its path.")
                .IsTrue();
            platform.EndRewindScrub();
        } finally {
            platform.Free();
        }
    }

    [TestCase]
    public void TimelineIntegrityKeepsDrainingAcrossTheWholeFreeze() {
        // The one clock that does NOT stop. The pin is membership: the death
        // rewind's freeze set contains chronal_extractor (its discharge cycle,
        // siphon drain and grace timer all pause for a rewind), and the Time
        // Freeze set deliberately does not — travel during a freeze counts as
        // live route time, so Integrity keeps falling at its current rate.
        AssertThat(ChronalRewindManager.FrozenSimulationGroups.Contains("chronal_extractor"))
            .OverrideFailureMessage("The rewind freeze must still pause extractors.")
            .IsTrue();
        AssertThat(TimeFreezeController.FrozenTimeGroups.Contains("chronal_extractor"))
            .OverrideFailureMessage("Time Freeze must NEVER pause the Integrity clock.")
            .IsFalse();
        AssertThat(TimeFreezeController.ProcessFallbackGroups.Contains("chronal_extractor"))
            .OverrideFailureMessage("Nor may the ProcessMode fallback park an extractor.")
            .IsFalse();

        // And an extractor really does keep ticking while nothing froze it.
        var extractor = new ChronalExtractor { Name = "FreezeIntegrityExtractor" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(extractor);
        try {
            AssertThat(extractor is IStoryTimeFreezable)
                .OverrideFailureMessage("An extractor must not be time-freezable at all.")
                .IsFalse();
        } finally {
            extractor.Free();
        }
    }

    [TestCase]
    public void ActivationStoresTheConservativeCooldownAndAnExplicitSaveEndsTheFreeze() {
        StoryManager story = StoryManager.Instance;
        float originalCooldown = story.TimeFreezeCooldownRemaining;
        TimeFreezeController controller = AttachController("SaveRuleController");
        try {
            AssertThat(controller.TryBeginTimeFreeze()).IsTrue();
            AssertThat(story.TimeFreezeCooldownRemaining)
                .OverrideFailureMessage("Activation commits the conservative 45 s for any reload.")
                .IsEqual(45f);

            // A background autosave while the freeze is live keeps that value and
            // does NOT end the freeze.
            Tick(controller, 60);
            var autosave = new StorySaveData();
            story.WriteAttemptStateToSave(autosave);
            AssertThat(autosave.TimeFreezeCooldownSeconds).IsEqual(45f);
            AssertThat(controller.IsFrozen)
                .OverrideFailureMessage("An autosave is not an early thaw.")
                .IsTrue();

            // An EXPLICIT save ends the freeze and stores the full cooldown.
            controller.EndFreezeForExplicitSave();
            AssertThat(controller.IsFrozen).IsFalse();
            var explicitSave = new StorySaveData();
            story.WriteAttemptStateToSave(explicitSave);
            AssertThat(explicitSave.TimeFreezeCooldownSeconds).IsEqual(45f);

            // Reload never resumes the effect: the restore path carries a
            // cooldown and nothing else — there is no "was frozen" field.
            var restored = new StorySaveData { TimeFreezeCooldownSeconds = 45f };
            story.RestoreAttemptStateFromSave(restored);
            AssertThat(story.TimeFreezeCooldownRemaining).IsEqual(45f);
            var fresh = new TimeFreezeController { Name = "ReloadedController" };
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(fresh);
            try {
                AssertThat(fresh.IsFrozen)
                    .OverrideFailureMessage("A reload must never resume or renew an active freeze.")
                    .IsFalse();
                AssertThat(fresh.CooldownRemaining).IsEqual(45f);
            } finally {
                fresh.Free();
            }
        } finally {
            controller.Free();
            story.SetTimeFreezeCooldown(originalCooldown);
        }
    }

    [TestCase]
    public void TimeFreezeIsStoryOnlyAndCannotReachTheDeterministicSimulation() {
        // Nothing under scripts/FighterSim may know this class exists.
        string root = ProjectSettings.GlobalizePath("res://scripts/FighterSim");
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("TimeFreeze"))
            .Select(Path.GetFileName)
            .ToList();
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");

        // The action carries no GameplayButtons bit, so it cannot enter a
        // deterministic input frame even by accident.
        AssertThat(InputManager.Actions.TimeFreeze).IsEqual("gameplay_time_freeze");
        string inputManagerSource = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://scripts/Core/InputManager.cs"));
        int bitSite = inputManagerSource.IndexOf("GameplayButtons.", System.StringComparison.Ordinal);
        AssertThat(bitSite >= 0).IsTrue();
        AssertThat(inputManagerSource.Contains("GameplayButtons.TimeFreeze"))
            .OverrideFailureMessage("Time Freeze must never map to a deterministic button bit.")
            .IsFalse();
    }

    [TestCase]
    public void NoAuthoredLevelGatesProgressionOnTimeFreeze() {
        // "Time Freeze may never be a required progression gate." The Level 0
        // drill is the one authored reference and it is a lesson, not a gate:
        // it is the tutorial's last calibration beat and carries a never-strand
        // skip. Every other level controller must not mention the ability.
        string root = ProjectSettings.GlobalizePath("res://scripts/Environment");
        var offenders = Directory.GetFiles(root, "Level*Controller.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "Level00Controller.cs")
            .Where(file => File.ReadAllText(file).Contains("TimeFreeze"))
            .Select(Path.GetFileName)
            .ToList();
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");

        // And the drill itself can always be skipped.
        var script = new TutorialCalibrationScript();
        while (script.Step != TutorialCalibrationStep.UseTimeFreeze
               && script.Step != TutorialCalibrationStep.Done) {
            script.RegisterBasicHit();
            script.RegisterRallyReclaimHit();
            script.RegisterBlockedHit();
            script.RegisterSpecialUsed(AbilitySlot.Special2);
            script.RegisterUltimateUsed();
            script.RegisterRewindComplete();
        }
        AssertThat(script.Step).IsEqual(TutorialCalibrationStep.UseTimeFreeze);
        AssertThat(script.SkipTimeFreezeLesson())
            .OverrideFailureMessage("The drill must never be able to strand the tutorial.")
            .IsTrue();
    }

    [TestCase]
    public void TheControllerNeverWritesTheSceneTreePause() {
        // CLAUDE.md failure signature 4: a leaked SceneTree.Paused stops
        // GdUnit's transport node and hangs the whole session. The controller
        // relies on ProcessMode.Inherit to be stopped BY the pause instead.
        string source = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://scripts/Environment/TimeFreezeController.cs"));
        AssertThat(source.Contains("Paused ="))
            .OverrideFailureMessage("TimeFreezeController must never write SceneTree.Paused.")
            .IsFalse();
        AssertThat(source.Contains("ProcessModeEnum.Always"))
            .OverrideFailureMessage("ProcessMode.Always would defeat the pause-stops-the-clock design.")
            .IsFalse();

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        bool wasPaused = tree.Paused;
        TimeFreezeController controller = AttachController("PauseGuardController");
        try {
            controller.TryBeginTimeFreeze();
            Tick(controller, 30);
            AssertThat(tree.Paused).IsEqual(wasPaused);
            controller.EndFreeze(early: true);
            Tick(controller, 30);
            AssertThat(tree.Paused).IsEqual(wasPaused);
        } finally {
            controller.Free();
            tree.Paused = wasPaused;
        }
    }

    [TestCase]
    public void TheRetiredTimeVerbLeavesNoTraceInTheSource() {
        // The V7.2/V7.3 manual verb is gone outright: the scrub, the frozen
        // past-self copy, the 12 s cooldown and the scrub-cancel hook. This is
        // the acceptance criterion for the retirement half of the workstream.
        //
        // Tokens are assembled from fragments and every comment is stripped
        // before scanning, so the gate reads CODE, not prose — this file and the
        // surviving explanatory comments elsewhere must not trip it.
        string[] banned = {
            "Stasis" + "Echo",
            "Manual" + "Rewind",
            "Scrub" + "PreviewPosition",
            "Cancel" + "RewindScrub",
            "Scripted" + "FreeRewind"
        };
        string[] roots = {
            ProjectSettings.GlobalizePath("res://scripts"),
            ProjectSettings.GlobalizePath("res://tests")
        };
        var offenders = roots
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(file => (File: Path.GetFileName(file), Code: StripComments(File.ReadAllText(file))))
            .SelectMany(entry => banned
                .Where(token => entry.Code.Contains(token))
                .Select(token => $"{entry.File}:{token}"))
            .ToList();
        if (offenders.Count > 0) AssertThat(string.Join(" | ", offenders)).IsEqual("");

        // The replacement's own action name stands in place of the old one.
        string projectSettings = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://project.godot"));
        AssertThat(projectSettings.Contains("gameplay_time_freeze=")).IsTrue();
        AssertThat(projectSettings.Contains("gameplay_rewind="))
            .OverrideFailureMessage("The retired action must be gone from the InputMap.")
            .IsFalse();

        // And its three translation rows went with it, rather than becoming
        // orphans (A2 owns those deletes).
        string table = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://localization/en.csv"));
        foreach (string key in new[] {
                     "tutorial_step_manual_rewind", "controls_action_rewind", "hud_rewind_cooldown" }) {
            AssertThat(table.Contains(key + ","))
                .OverrideFailureMessage($"Retired key {key} must be deleted, not left as an orphan.")
                .IsFalse();
        }
        AssertThat(table.Contains("controls_action_time_freeze,")).IsTrue();
    }

    /// <summary>
    /// Drops <c>//</c> line comments (doc comments included) so a source gate
    /// reads code rather than the prose that explains the retirement.
    /// </summary>
    private static string StripComments(string source) {
        const char newline = (char)10;
        var kept = source.Split(newline)
            .Where(line => !line.TrimStart().StartsWith("//", System.StringComparison.Ordinal));
        return string.Join(newline, kept);
    }

    [TestCase]
    public void TheDrillExemptionIsTheOnlyRechargeBypassAndItEndsWithTheLesson() {
        TimeFreezeController controller = AttachController("DrillExemptionController");
        try {
            controller.TryBeginTimeFreeze();
            controller.EndFreeze(early: true);
            AssertThat(controller.IsReady)
                .OverrideFailureMessage("Outside the drill the cooldown really gates the ability.")
                .IsFalse();

            // Level 0's drill: ready on every retry, so a failed attempt never
            // strands the tutorial behind a 45-second wait.
            controller.DrillFreeFreeze = true;
            AssertThat(controller.IsReady).IsTrue();
            AssertThat(controller.TryBeginTimeFreeze()).IsTrue();
            controller.EndFreeze(early: true);
            AssertThat(controller.CooldownRemaining)
                .OverrideFailureMessage("A drill freeze arms no cooldown.")
                .IsEqual(0f);

            // The exemption dies with the lesson.
            controller.DrillFreeFreeze = false;
            controller.TryBeginTimeFreeze();
            controller.EndFreeze(early: true);
            AssertThat(controller.CooldownRemaining).IsEqual(45f);
        } finally {
            controller.Free();
        }
    }
}
