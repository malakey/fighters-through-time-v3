using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// 2026-10-04 playtest feel pass, workstream FEEL — the Story half
/// (docs/PLAYTEST_FEEL_2026-10-04_PLAN.md §2.1):
/// <list type="bullet">
/// <item>F1 — attack sheets are POSED from the swing's frame windows (wind-up
/// through startup, strike through active, follow-through through recovery)
/// rather than played at the sheet's fixed fps;</item>
/// <item>F2 — one attack clock: every swing (template-timed or authored-profile)
/// runs on the physics frame clock hitstop suspends;</item>
/// <item>F3 — the chain buffer: active-frame and hitstop presses buffer, a second
/// press queues for the hit after, holding continues the chain, Time Freeze
/// discards;</item>
/// <item>F4 — the chain hold reads as locomotion; hitstun/dazed play their reels
/// and every new hit restarts hitstun;</item>
/// <item>F5 — a landed direct ability hit freezes its caster once per execution
/// and raises its impact feedback, through every delivery path;</item>
/// <item>F6 — the launch bonus on the victim and the attacker's kill freeze;</item>
/// <item>F7 — Standard-tier mob hits never launch the Story player.</item>
/// </list>
/// The same day's fix pass adds: R12 — a late pooled shot claims its own cast's
/// freeze; G2 — the controller's meter field follows the meter node (an
/// Ultimate's spend reaches Defy and the saves); G7 — a double-tap of Down drops
/// through a one-way platform from standing and running.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FeelPassStoryTests {
    private const double Step = 1.0 / 60.0;
    private static readonly Vector2 Origin = new(-52000f, 600f);

    // === F1 / F2 / F4: the swing's poses on one frame clock ===================

    [TestCase]
    public void TheOpenerIsPosedFromItsFrameWindowsAndTheChainHoldReadsAsLocomotion() {
        // Tesla is the template profile — the character whose swings used to run
        // on the combat AnimationPlayer's idle clock.
        PlayerController player = CreateAirborne("tesla");
        try {
            var hitbox = player.GetNode<Hitbox>("MeleeHitbox");
            BasicStringProfile profile = BasicComboRules.StringProfileFor("tesla");
            int startup = profile.AerialStartupFrames[0];
            int active = BasicComboRules.AerialActiveFrames[0];
            int recovery = BasicComboRules.AerialRecoveryFrames[0];

            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertPose(player, "basic_attack_1", AttackPoseRules.WindUpPose, "the press");

            // The press tick runs no attack tick, so the Nth call after it is
            // elapsed frame N-1.
            for (int frame = 0; frame < startup; frame++) {
                SendInput(player, GameplayButtons.None);
                AssertThat(hitbox.IsActive)
                    .OverrideFailureMessage($"The box opened during startup (elapsed {frame}).")
                    .IsFalse();
                AssertPose(player, "basic_attack_1", AttackPoseRules.WindUpPose, $"startup elapsed {frame}");
            }
            for (int frame = 0; frame < active; frame++) {
                SendInput(player, GameplayButtons.None);
                AssertThat(hitbox.IsActive)
                    .OverrideFailureMessage($"The box must be live through the active window (frame {frame}).")
                    .IsTrue();
                AssertPose(player, "basic_attack_1", AttackPoseRules.StrikePose, $"active frame {frame}");
            }
            for (int frame = 0; frame < recovery - 1; frame++) {
                SendInput(player, GameplayButtons.None);
                AssertThat(hitbox.IsActive).IsFalse();
                AssertPose(player, "basic_attack_1", AttackPoseRules.FollowThroughPose, $"recovery frame {frame}");
            }

            // The last recovery frame hands the swing to the chain hold, which is
            // not a swing: the body reads as falling, not frozen mid-follow-through.
            SendInput(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            AssertThat(player.ActiveAnimationName)
                .OverrideFailureMessage("The chain hold must play the body's locomotion reel.")
                .IsEqual("fall");
            AssertThat(player.IsShowingAttackPose).IsFalse();
        } finally {
            Release(player);
        }
    }

    [TestCase]
    public void HitstopExtendsTemplateAndAuthoredProfileSwingsByExactlyTheFreeze() {
        // F2: Tesla's template swing (once AnimationPlayer-timed) and Einstein's
        // authored-profile swing (always frame-clocked) both stretch by exactly
        // the freeze, and hold the strike pose through it.
        const int freeze = 6;
        foreach (string characterID in new[] { "tesla", "einstein" }) {
            int baseline = TicksUntilTheOpenerAndItsHoldEnd(characterID, injectHitstop: 0);
            int frozen = TicksUntilTheOpenerAndItsHoldEnd(characterID, injectHitstop: freeze);
            AssertThat(frozen - baseline)
                .OverrideFailureMessage($"{characterID}: a {freeze}-frame freeze stretched the swing by {frozen - baseline}.")
                .IsEqual(freeze);
        }
    }

    // === F3: the chain buffer =================================================

    [TestCase]
    public void APressDuringTheActiveFramesBuffersTheNextHit() {
        PlayerController player = CreateAirborne("tesla");
        try {
            int startup = BasicComboRules.StringProfileFor("tesla").AerialStartupFrames[0];
            int total = startup + BasicComboRules.AerialActiveFrames[0] + BasicComboRules.AerialRecoveryFrames[0];
            SendInput(player, GameplayButtons.BasicAttack);
            for (int frame = 0; frame < startup; frame++) SendInput(player, GameplayButtons.None);
            // Elapsed frame == startup: the first active frame.
            SendInput(player, GameplayButtons.BasicAttack);
            AssertThat(player.GetNode<Hitbox>("MeleeHitbox").IsActive).IsTrue();
            for (int frame = startup + 1; frame < total; frame++) {
                AssertThat(player.ComboCounter).IsEqual(0);
                SendInput(player, GameplayButtons.None);
            }
            AssertThat(player.ComboCounter)
                .OverrideFailureMessage("An active-frame press must carry hit two straight out of hit one's recovery.")
                .IsEqual(1);
            AssertPose(player, "basic_attack_2", AttackPoseRules.WindUpPose, "hit two's first frame");
        } finally {
            Release(player);
        }
    }

    [TestCase]
    public void APressDuringHitstopBuffersAndTimeFreezeDiscardsIt() {
        foreach (bool timeFrozen in new[] { false, true }) {
            PlayerController player = CreateAirborne("tesla");
            try {
                int startup = BasicComboRules.StringProfileFor("tesla").AerialStartupFrames[0];
                SendInput(player, GameplayButtons.BasicAttack);
                for (int frame = 0; frame <= startup; frame++) SendInput(player, GameplayButtons.None);
                player.ApplyHitstop(5);
                player.TimeFrozen = timeFrozen;
                SendInput(player, GameplayButtons.BasicAttack);
                AssertThat(player.IsInHitstop).IsTrue();
                player.TimeFrozen = false;

                bool sawHold = false;
                for (int frame = 0; frame < 60 && player.ComboCounter == 0; frame++) {
                    SendInput(player, GameplayButtons.None);
                    if (player.ActiveAnimationName == "fall") sawHold = true;
                    if (sawHold) break;
                }
                if (timeFrozen) {
                    AssertThat(sawHold)
                        .OverrideFailureMessage("A press under Time Freeze must be discarded, never buffered.")
                        .IsTrue();
                    AssertThat(player.ComboCounter).IsEqual(0);
                } else {
                    AssertThat(player.ComboCounter)
                        .OverrideFailureMessage("A press made inside the freeze must buffer hit two.")
                        .IsEqual(1);
                    AssertThat(sawHold).IsFalse();
                }
            } finally {
                Release(player);
            }
        }
    }

    [TestCase]
    public void HoldingAttackContinuesTheChainThroughTheFinisher() {
        PlayerController player = CreateAirborne("tesla");
        try {
            SendInput(player, GameplayButtons.BasicAttack);
            int highest = 0;
            for (int frame = 0; frame < 200 && player.CurrentState == CharacterState.Attacking; frame++) {
                // Held, never re-pressed.
                SendFrame(player, PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.BasicAttack,
                    previousHeld: GameplayButtons.BasicAttack));
                if (player.ComboCounter > highest) highest = player.ComboCounter;
            }
            AssertThat(highest)
                .OverrideFailureMessage("Holding the attack button must run the whole string (design-godot.md).")
                .IsEqual(BasicComboRules.ComboHits - 1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The finisher exits straight out; a hold never restarts hit one.")
                .IsNotEqual(CharacterState.Attacking);
        } finally {
            Release(player);
        }
    }

    [TestCase]
    public void ThreePressesOnATwelveFrameRhythmLandThreeSwings() {
        // The playtest probe's "three rhythmic presses -> two hits": the second
        // press lands in hit one's active frames (once excluded) and the third
        // in its recovery (once collapsed into the one-deep buffer).
        PlayerController player = CreateAirborne("tesla");
        try {
            int highest = 0;
            for (int tick = 0; tick < 150; tick++) {
                bool press = tick == 0 || tick == 12 || tick == 24;
                SendInput(player, press ? GameplayButtons.BasicAttack : GameplayButtons.None);
                if (player.ComboCounter > highest) highest = player.ComboCounter;
            }
            AssertThat(highest)
                .OverrideFailureMessage("Three presses must start three swings of the string.")
                .IsEqual(2);
        } finally {
            Release(player);
        }
    }

    // === F4: reactive reels ===================================================

    [TestCase]
    public void HitstunAndDazedPlayTheirReelsAndEveryNewHitRestartsHitstun() {
        PlayerController player = CreateAirborne("einstein");
        try {
            var sprite = player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(PlayerHit(launches: false));
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.ActiveAnimationName).IsEqual("hitstun");
            AssertThat(sprite.Frame).IsEqual(0);

            sprite.Frame = 2;
            player.GetNode<Hurtbox>("Hurtbox").TakeHit(PlayerHit(launches: false));
            AssertThat(sprite.Frame)
                .OverrideFailureMessage("A fresh hit must restart the hitstun reel.")
                .IsEqual(0);

            player.TransitionTo(CharacterState.Dazed);
            AssertThat(player.ActiveAnimationName).IsEqual("dazed");
        } finally {
            Release(player);
        }
    }

    // === F5: the caster half of ability hitstop and the shared feedback =======

    [TestCase]
    public void AShapeQueryBurstFreezesItsCasterOncePerExecutionAndConfirmsEveryContact() {
        Node2D host = CreateHost("FeelBurst");
        var confirms = new List<HitConfirmPayload>();
        void OnConfirm(HitConfirmPayload payload) => confirms.Add(payload);
        EventBus.Instance.OnHitConfirm += OnConfirm;
        try {
            PlayerController einstein = AddEinstein(host);
            Vector2 burstAt = Origin + new Vector2(200f, 0f);
            EnemyController enemy = CreateEnemy("chrono_slasher", host, burstAt + new Vector2(20f, 0f));
            enemy.CurrentHP = 9999;
            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");

            AssertThat(emc.TryExecute()).IsTrue();
            int hpBefore = enemy.CurrentHP;
            emc.DetonateForTest(burstAt);
            int dealt = hpBefore - enemy.CurrentHP;
            AssertThat(dealt > 0).OverrideFailureMessage("The burst never reached the enemy.").IsTrue();
            AssertThat(einstein.HitstopFramesRemaining)
                .OverrideFailureMessage("The burst's caster must freeze for the shared (launching) window.")
                .IsEqual(BasicComboRules.HitstopFrames(dealt, emc.Data.Launches));
            AssertThat(confirms.Count).IsEqual(1);
            AssertThat(confirms[0].AttackID).IsEqual(emc.Data.AbilityID);

            // The caster's next tick hands the freeze to its casts: the phase
            // clock holds, and the node itself keeps processing (its deployed
            // world effects must keep running).
            SendInput(einstein, GameplayButtons.None);
            AssertThat(einstein.CastClocksSuspended)
                .OverrideFailureMessage("A frozen caster's casts freeze with it.")
                .IsTrue();
            AssertThat(emc.CastClockSuspended).IsTrue();
            AssertThat(emc.IsPhysicsProcessing())
                .OverrideFailureMessage("Only the cast clock holds; the ability node keeps processing.")
                .IsTrue();
            AbilityPhase heldPhase = emc.CurrentPhase;
            int heldFrames = emc.PhaseFramesRemaining;
            emc._PhysicsProcess(Step);
            emc._PhysicsProcess(Step);
            AssertThat(emc.CurrentPhase).IsEqual(heldPhase);
            AssertThat(emc.PhaseFramesRemaining).IsEqual(heldFrames);

            // Thaw: the cast clocks come back on the first unfrozen tick.
            for (int frame = 0; frame < 30 && einstein.IsInHitstop; frame++) SendInput(einstein, GameplayButtons.None);
            SendInput(einstein, GameplayButtons.None);
            AssertThat(einstein.IsInHitstop).IsFalse();
            AssertThat(einstein.CastClocksSuspended).IsFalse();
            AssertThat(emc.CastClockSuspended).IsFalse();
            emc._PhysicsProcess(Step);
            AssertThat(emc.PhaseFramesRemaining).IsEqual(heldFrames - 1);

            // A second contact of the SAME execution confirms but does not
            // re-freeze the caster.
            emc.DetonateForTest(burstAt);
            AssertThat(confirms.Count).IsEqual(2);
            AssertThat(einstein.IsInHitstop)
                .OverrideFailureMessage("The caster freezes on its first landed hit per execution only.")
                .IsFalse();

            // A fresh execution re-arms the caster freeze.
            emc.Interrupt();
            AssertThat(emc.TryExecute()).IsTrue();
            emc.DetonateForTest(burstAt);
            AssertThat(einstein.IsInHitstop).IsTrue();
        } finally {
            EventBus.Instance.OnHitConfirm -= OnConfirm;
            InputManager.Instance?.ClearInputSource(0);
            host.Free();
        }
    }

    // === F5 (review fix): the caster freeze holds the CAST, never its world ======

    [TestCase]
    public void AFrozenCastersGroundWaveKeepsTravellingWhileItsCastClockHolds() {
        Node2D host = CreateHost("FeelFrozenWave");
        try {
            PlayerController joan = AddCharacter(host, "joan");
            var smite = joan.GetNode<JoanRighteousSmite>("Special1");
            AssertThat(smite.TryExecute()).IsTrue();
            smite.AdvanceToActive();
            AssertThat(smite.Wave.Active).IsTrue();

            FreezeCaster(joan, smite);
            int heldFrames = smite.PhaseFramesRemaining;
            float start = smite.Wave.Front.X;
            const int ticks = 4;
            for (int tick = 0; tick < ticks; tick++) smite._PhysicsProcess(Step);

            float travelled = Mathf.Abs(smite.Wave.Front.X - start);
            float expected = smite.Wave.Speed * ticks * (float)Step;
            AssertThat(Mathf.Abs(travelled - expected) < 0.5f)
                .OverrideFailureMessage($"The wave must keep travelling through its caster's freeze: moved {travelled} px, expected {expected}.")
                .IsTrue();
            AssertThat(smite.CurrentPhase).IsEqual(AbilityPhase.Active);
            AssertThat(smite.PhaseFramesRemaining)
                .OverrideFailureMessage("The cast's own phase timer holds while its caster is frozen.")
                .IsEqual(heldFrames);

            Thaw(joan, smite);
            smite._PhysicsProcess(Step);
            AssertThat(smite.PhaseFramesRemaining).IsEqual(heldFrames - 1);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            host.Free();
        }
    }

    [TestCase]
    public void AFrozenCastersVortexChurnsOutItsLifetimeWhileItsCastClockHolds() {
        Node2D host = CreateHost("FeelFrozenVortex");
        try {
            PlayerController cleopatra = AddCharacter(host, "cleopatra");
            var vortex = cleopatra.GetNode<CleopatraSandstormVortex>("Special2");
            AssertThat(vortex.TryExecute()).IsTrue();
            vortex.AdvanceToActive();
            AssertThat(vortex.VortexActive).IsTrue();

            FreezeCaster(cleopatra, vortex);
            int heldFrames = vortex.PhaseFramesRemaining;
            float lifetime = vortex.Data.Lifetime > 0f ? vortex.Data.Lifetime : 2f;
            int lifetimeTicks = Mathf.CeilToInt(lifetime * 60f) + 2;
            for (int tick = 0; tick < lifetimeTicks; tick++) vortex._PhysicsProcess(Step);

            AssertThat(vortex.VortexActive)
                .OverrideFailureMessage("A deployed vortex is a world object: it churns out its lifetime through its caster's freeze.")
                .IsFalse();
            AssertThat(vortex.CurrentPhase).IsEqual(AbilityPhase.Active);
            AssertThat(vortex.PhaseFramesRemaining).IsEqual(heldFrames);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            host.Free();
        }
    }

    [TestCase]
    public void TheTempestWindboxShovesOnItsOwnClockWhileTheFrozenCastersLiftHolds() {
        Node2D host = CreateHost("FeelFrozenTempest");
        try {
            PlayerController shakespeare = AddCharacter(host, "shakespeare");
            EnemyController enemy = CreateEnemy("chrono_slasher", host, Origin + new Vector2(80f, 0f));
            float enemyStart = enemy.GlobalPosition.X;
            var tempest = shakespeare.GetNode<ShakespeareTheTempest>("Special2");
            AssertThat(tempest.TryExecute()).IsTrue();
            for (int tick = 0; tick < 40 && tempest.CurrentPhase != AbilityPhase.Active; tick++) {
                tempest._PhysicsProcess(Step);
            }
            AssertThat(tempest.CurrentPhase).IsEqual(AbilityPhase.Active);
            AssertThat(tempest.WindboxFramesRemaining).IsEqual(tempest.Data.ActiveFrames);

            FreezeCaster(shakespeare, tempest);
            int heldFrames = tempest.PhaseFramesRemaining;
            float liftBefore = shakespeare.Velocity.Y;
            for (int tick = 0; tick < KitMotionRules.TempestPushFrames; tick++) tempest._PhysicsProcess(Step);

            float pushed = enemy.GlobalPosition.X - enemyStart;
            AssertThat(Mathf.Abs(pushed - 180f) < 1f)
                .OverrideFailureMessage($"The storm keeps its full ~180 px shove through its caster's freeze, got {pushed}.")
                .IsTrue();
            AssertThat(tempest.WindboxFramesRemaining).IsEqual(0);
            AssertThat(tempest.PhaseFramesRemaining).IsEqual(heldFrames);
            AssertThat(shakespeare.Velocity.Y)
                .OverrideFailureMessage("The lift is the caster's: it holds while he is frozen.")
                .IsEqual(liftBefore);

            // Thawed: the lift resumes for the rest of the active window, and the
            // spent storm shoves no further — the freeze never stretches it.
            Thaw(shakespeare, tempest);
            float enemyAfterStorm = enemy.GlobalPosition.X;
            tempest._PhysicsProcess(Step);
            AssertThat(shakespeare.Velocity.Y < 0f).IsTrue();
            AssertThat(enemy.GlobalPosition.X).IsEqual(enemyAfterStorm);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            host.Free();
        }
    }

    [TestCase]
    public void TheConductorsCallRushFinishesThroughAFreezeWithoutStretching() {
        Node2D host = CreateHost("FeelFrozenRush");
        try {
            PlayerController tubman = AddCharacter(host, "tubman");
            var call = tubman.GetNode<TubmanConductorsCall>("Special1");
            AssertThat(call.TryExecute()).IsTrue();
            for (int tick = 0; tick < 40 && call.CurrentPhase != AbilityPhase.Active; tick++) call._PhysicsProcess(Step);
            AssertThat(call.RushActive).IsTrue();

            FreezeCaster(tubman, call);
            int heldFrames = call.PhaseFramesRemaining;
            for (int tick = 0; tick < call.Data.ActiveFrames; tick++) call._PhysicsProcess(Step);
            AssertFloat(call.RushDistancePixels).IsEqualApprox(call.RushReachPixels, 0.5f);
            AssertThat(call.RushActive).IsFalse();
            AssertThat(call.CurrentPhase).IsEqual(AbilityPhase.Active);
            AssertThat(call.PhaseFramesRemaining).IsEqual(heldFrames);

            Thaw(tubman, call);
            for (int tick = 0; tick < 90 && call.CurrentPhase == AbilityPhase.Active; tick++) call._PhysicsProcess(Step);
            AssertThat(call.CurrentPhase).IsEqual(AbilityPhase.Recovery);
            AssertFloat(call.RushDistancePixels).IsEqualApprox(call.RushReachPixels, 0.5f);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            host.Free();
        }
    }

    [TestCase]
    public void TheHitboxPathConfirmsAbilityHitsOnceAndLeavesTicksAndBasicsAlone() {
        Node2D host = CreateHost("FeelHitboxPath");
        var confirms = new List<HitConfirmPayload>();
        void OnConfirm(HitConfirmPayload payload) => confirms.Add(payload);
        EventBus.Instance.OnHitConfirm += OnConfirm;
        try {
            PlayerController einstein = AddEinstein(host);
            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.TryExecute()).IsTrue();

            // Zone/DoT ticks, construct hits and a basic's ID confirm nothing.
            einstein.ConfirmAbilityHitboxHit(AbilityPayload(emc, HitDelivery.Tick, 1001), 10f, Origin);
            einstein.ConfirmAbilityHitboxHit(AbilityPayload(emc, HitDelivery.Construct, 1002), 10f, Origin);
            HitPayload basic = AbilityPayload(emc, HitDelivery.DirectHit, 1003);
            basic.AttackID = "einstein.basic";
            basic.Origin = HitOrigin.Basic;
            einstein.ConfirmAbilityHitboxHit(basic, 10f, Origin);
            AssertThat(confirms.Count).IsEqual(0);
            AssertThat(einstein.IsInHitstop).IsFalse();

            // A hitstop-exempt direct contact still reads as an impact but
            // freezes nobody.
            HitPayload exempt = AbilityPayload(emc, HitDelivery.DirectHit, 1004);
            exempt.ExemptFromHitstop = true;
            einstein.ConfirmAbilityHitboxHit(exempt, 10f, Origin);
            AssertThat(confirms.Count).IsEqual(1);
            AssertThat(einstein.IsInHitstop).IsFalse();

            // A direct Special contact (a projectile the cast fired) freezes the
            // caster; the same contact reported twice confirms once.
            HitPayload projectile = AbilityPayload(emc, HitDelivery.DirectHit, 1005);
            einstein.ConfirmAbilityHitboxHit(projectile, 12f, Origin);
            emc.ConfirmAbilityHit(projectile, 12f, Origin);
            AssertThat(confirms.Count).IsEqual(2);
            AssertThat(einstein.HitstopFramesRemaining)
                .IsEqual(BasicComboRules.HitstopFrames(12, projectile.Launches));
        } finally {
            EventBus.Instance.OnHitConfirm -= OnConfirm;
            host.Free();
        }
    }

    [TestCase]
    public void AProjectileTheCastFiredFreezesItsCasterAndConfirmsAtTheHurtbox() {
        // The pooled-projectile path never reaches BaseSpecial's child-hitbox
        // handler; the Hitbox itself routes it to the caster's ability.
        Node2D host = CreateHost("FeelProjectile");
        var confirms = new List<HitConfirmPayload>();
        void OnConfirm(HitConfirmPayload payload) => confirms.Add(payload);
        EventBus.Instance.OnHitConfirm += OnConfirm;
        try {
            PlayerController einstein = AddEinstein(host);
            EnemyController enemy = CreateEnemy("chrono_slasher", host, Origin + new Vector2(300f, 0f));
            enemy.CurrentHP = 9999;
            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.TryExecute()).IsTrue();

            var projectile = new PlaceholderProjectile { Name = "FeelProbeShot" };
            host.AddChild(projectile);
            projectile.GlobalPosition = enemy.GlobalPosition + new Vector2(-10f, 0f);
            projectile.Setup(7f, new Vector2(2f, -1f), 0f, true, 0, Colors.White,
                sourcePlayer: einstein, data: emc.Data);
            Hurtbox hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            projectile.GetNode<Hitbox>("Hitbox").EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);

            int dealt = 9999 - enemy.CurrentHP;
            AssertThat(dealt > 0).OverrideFailureMessage("The shot never landed.").IsTrue();
            AssertThat(einstein.HitstopFramesRemaining)
                .OverrideFailureMessage("A landed shot the cast fired must freeze its caster.")
                .IsEqual(BasicComboRules.HitstopFrames(dealt, emc.Data.Launches));
            AssertThat(confirms.Count)
                .OverrideFailureMessage("A projectile hit must raise exactly one HitConfirm.")
                .IsEqual(1);
            AssertThat(confirms[0].Position).IsEqual(hurtbox.GlobalPosition);
        } finally {
            EventBus.Instance.OnHitConfirm -= OnConfirm;
            host.Free();
        }
    }

    // === R12 (fix pass): a late shot claims its own cast's freeze =============

    [TestCase]
    public void ALateShotFromAnEarlierCastNeverSpendsTheCurrentCastsFreeze() {
        // A pooled shot can outlive its cast. It used to claim the once-per-
        // execution caster freeze of whatever cast was current when it landed,
        // so a shot from cast one landing after cast two started spent cast
        // two's freeze and cast two's own first hit froze nobody.
        Node2D host = CreateHost("FeelLateShot");
        // A scoped pool manager takes the singleton for this case, so the shots
        // and flashes it spawns are freed with the case instead of parking in
        // the autoload's shared pool for the rest of the session.
        var pools = new PoolManager { Name = "FeelLateShotPools" };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(pools);
        try {
            PlayerController einstein = AddEinstein(host);
            EnemyController enemy = CreateEnemy("chrono_slasher", host, Origin + new Vector2(300f, 0f));
            enemy.CurrentHP = 9999;
            var emc = einstein.GetNode<EinsteinEmc2Blast>("Special1");

            // Cast one fires its shot: the pooled projectile carries cast one.
            AssertThat(emc.TryExecute()).IsTrue();
            int castOne = emc.ExecutionSerial;
            emc.AdvanceToActive();
            PlaceholderProjectile fired = FirstChildOf<PlaceholderProjectile>(host);
            AssertThat(fired).OverrideFailureMessage("Cast one fired no shot.").IsNotNull();
            AssertThat(fired.SourceExecutionSerial)
                .OverrideFailureMessage("The fired shot must carry the execution that fired it.")
                .IsEqual(castOne);
            pools.ReleaseActiveUnder(host);

            // Cast two starts while a cast-one shot is still in flight.
            emc.Interrupt();
            AssertThat(emc.TryExecute()).IsTrue();
            AssertThat(emc.ExecutionSerial).IsEqual(castOne + 1);
            var late = new PlaceholderProjectile { Name = "FeelLateShotProbe" };
            host.AddChild(late);
            late.GlobalPosition = enemy.GlobalPosition + new Vector2(-10f, 0f);
            late.Setup(7f, new Vector2(2f, -1f), 0f, true, 0, Colors.White,
                sourcePlayer: einstein, data: emc.Data);
            late.SourceExecutionSerial = castOne;
            late.GetNode<Hitbox>("Hitbox").EmitSignal(Area2D.SignalName.AreaEntered, enemy.GetNode<Hurtbox>("Hurtbox"));
            AssertThat(9999 - enemy.CurrentHP > 0).OverrideFailureMessage("The late shot never landed.").IsTrue();
            AssertThat(einstein.IsInHitstop)
                .OverrideFailureMessage("Cast one never froze its caster, so its late shot still may.")
                .IsTrue();

            // Cast one's freeze is spent; cast two's is untouched.
            AssertThat(emc.TryClaimCasterHitstop(castOne)).IsFalse();
            AssertThat(emc.TryClaimCasterHitstop())
                .OverrideFailureMessage("The late cast-one shot spent cast two's freeze.")
                .IsTrue();
            // And once cast two has frozen, a stale serial can never re-arm one.
            AssertThat(emc.TryClaimCasterHitstop(castOne)).IsFalse();
            AssertThat(emc.TryClaimCasterHitstop()).IsFalse();
        } finally {
            pools.ClearAllPools();
            pools.Free();
            host.Free();
        }
    }

    // E=mc²'s burst and Yorick's wave resolve one step after the shot strikes
    // (CallDeferred off the shot's Impacted), so they used to claim whatever cast
    // was current by then. In these two cases the shot bursts on a wall with
    // nobody in its path, so the burst is cast one's only landed hit: it must
    // freeze the caster on cast ONE's account and leave cast two's freeze
    // unspent. Real physics frames fly the shot, strike the wall and run the
    // deferred burst.

    [TestCase]
    public async Task ADeferredEmc2BurstFreezesTheCastThatFiredItNotTheCurrentOne() =>
        await AssertTerrainBurstChargesTheFiringCast<EinsteinEmc2Blast>("einstein");

    [TestCase]
    public async Task ADeferredYorickWaveFreezesTheCastThatThrewItNotTheCurrentOne() =>
        await AssertTerrainBurstChargesTheFiringCast<ShakespeareYoricksLament>("shakespeare");

    [TestCase]
    public async Task ARequiemBurstIsChargedToTheCastThatFiredTheChord() {
        // The chord's contact sets its burst up one step later (CallDeferred) and
        // the burst pulses every six frames after that; its pulses must carry the
        // cast that fired the chord, not the cast current when they land.
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Node2D host = CreateHost("FeelLateChord");
        // Scoped, so the chord and the burst's flashes never park in the autoload's pool.
        var pools = new PoolManager { Name = "FeelLateChordPools" };
        tree.Root.AddChild(pools);
        try {
            PlayerController mozart = AddCharacter(host, "mozart");
            HoldIdleInput(mozart);
            EnemyController enemy = CreateEnemy("chrono_slasher", host, Origin + new Vector2(300f, 0f));
            enemy.CurrentHP = 9999;
            // A passive target that holds its spot through the frames below.
            enemy.SetPhysicsProcess(false);
            var requiem = mozart.GetNode<MozartRequiemChord>("Special1");

            // Cast one fires its chord.
            AssertThat(requiem.TryExecute()).IsTrue();
            int castOne = requiem.ExecutionSerial;
            requiem.AdvanceToActive();
            PlaceholderProjectile chord = FirstChildOf<PlaceholderProjectile>(host);
            AssertThat(chord).OverrideFailureMessage("Cast one fired no chord.").IsNotNull();
            AssertThat(chord.SourceExecutionSerial).IsEqual(castOne);

            // Cast two starts while the chord is in flight. Its clock (and the
            // burst's pulse clock) is held: the pulses are driven by hand below.
            requiem.Interrupt();
            AssertThat(requiem.TryExecute()).IsTrue();
            AssertThat(requiem.ExecutionSerial).IsEqual(castOne + 1);
            requiem.SetPhysicsProcess(false);

            // The chord connects through the real Hitbox: its contact stage lands
            // and the burst is deferred off the shot's Impacted.
            Hurtbox hurtbox = enemy.GetNode<Hurtbox>("Hurtbox");
            chord.GlobalPosition = hurtbox.GlobalPosition + new Vector2(-10f, 0f);
            chord.GetNode<Hitbox>("Hitbox").EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
            AssertThat(9999 - enemy.CurrentHP > 0).OverrideFailureMessage("The chord's contact never landed.").IsTrue();
            AssertThat(requiem.PendingPulses)
                .OverrideFailureMessage("The burst is set up one step after the contact, not inside it.")
                .IsEqual(0);
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(requiem.PendingPulses > 0)
                .OverrideFailureMessage("The deferred burst never started.")
                .IsTrue();

            // One of that burst's pulses lands.
            float dealt = requiem.StrikeWithPulse(hurtbox, requiem.Data.BaseDamage, "shockwave", finalPulse: false);
            AssertThat(dealt > 0f).OverrideFailureMessage("The burst pulse never landed.").IsTrue();
            AssertThat(requiem.TryClaimCasterHitstop(castOne)).IsFalse();
            AssertThat(requiem.TryClaimCasterHitstop())
                .OverrideFailureMessage("Cast one's burst pulse spent cast two's freeze.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    // === F6: launch weight and the kill freeze ================================

    [TestCase]
    public void ALaunchingHitFreezesTheStoryVictimForTheLaunchBonus() {
        foreach (bool launches in new[] { false, true }) {
            PlayerController player = CreateAirborne("einstein");
            try {
                player.GetNode<Hurtbox>("Hurtbox").TakeHit(PlayerHit(launches));
                AssertThat(player.HitstopFramesRemaining)
                    .OverrideFailureMessage($"Launches={launches}: wrong victim freeze.")
                    .IsEqual(BasicComboRules.HitstopFrames(10, launches));
            } finally {
                Release(player);
            }
        }
    }

    [TestCase]
    public void KillingAMobStillFreezesTheAttacker() {
        Node2D host = CreateHost("FeelKillFreeze");
        try {
            // The basic swing: a lethal opener still lands the attacker's freeze.
            PlayerController einstein = AddEinstein(host);
            EnemyController mob = CreateEnemy("chrono_slasher", host, Origin + new Vector2(40f, 0f));
            einstein.TransitionTo(CharacterState.Airborne);
            einstein.Velocity = Vector2.Down;
            SendInput(einstein, GameplayButtons.BasicAttack);
            var hitbox = einstein.GetNode<Hitbox>("MeleeHitbox");
            for (int frame = 0; frame < 20 && !hitbox.IsActive; frame++) SendInput(einstein, GameplayButtons.None);
            AssertThat(hitbox.IsActive).IsTrue();
            mob.CurrentHP = 1;
            hitbox.EmitSignal(Area2D.SignalName.AreaEntered, mob.GetNode<Hurtbox>("Hurtbox"));
            AssertThat(mob.IsAlive).OverrideFailureMessage("The opener must have killed the mob.").IsFalse();
            AssertThat(einstein.HitstopFramesRemaining)
                .OverrideFailureMessage("A killing blow must still freeze the attacker (the kill freeze).")
                .IsEqual(BasicComboRules.HitstopFrames(1, launches: false));

            // The Special: a lethal burst freezes its caster too.
            PlayerController caster = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            caster.Position = Origin + new Vector2(0f, -900f);
            host.AddChild(caster);
            Vector2 burstAt = Origin + new Vector2(600f, 0f);
            EnemyController second = CreateEnemy("chrono_slasher", host, burstAt);
            second.CurrentHP = 1;
            var emc = caster.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.TryExecute()).IsTrue();
            emc.DetonateForTest(burstAt);
            AssertThat(second.IsAlive).IsFalse();
            AssertThat(caster.IsInHitstop)
                .OverrideFailureMessage("A lethal Special burst must still freeze its caster.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            host.Free();
        }
    }

    // === F7: standard-mob hits never launch ==================================

    [TestCase]
    public void OnlyAuthoredOrNonStandardEnemyHitsLaunchTheStoryPlayer() {
        var standard = new HitPayload { AttackerIndex = -1, Knockback = new Vector2(3f, -2f), SourceIsStandardMob = true };
        var elite = new HitPayload { AttackerIndex = -1, Knockback = new Vector2(3f, -2f) };
        var authored = new HitPayload { AttackerIndex = -1, Knockback = new Vector2(3f, -2f), SourceIsStandardMob = true, Launches = true };
        var impulseFree = new HitPayload { AttackerIndex = -1, Knockback = Vector2.Zero };
        var playerBasic = new HitPayload { AttackerIndex = 1, Knockback = new Vector2(3f, -2f), Launches = false };
        AssertThat(PlayerController.ResolveHitLaunches(in standard))
            .OverrideFailureMessage("A Standard-tier mob's poke must not launch.")
            .IsFalse();
        AssertThat(PlayerController.ResolveHitLaunches(in elite)).IsTrue();
        AssertThat(PlayerController.ResolveHitLaunches(in authored)).IsTrue();
        AssertThat(PlayerController.ResolveHitLaunches(in impulseFree)).IsFalse();
        AssertThat(PlayerController.ResolveHitLaunches(in playerBasic)).IsFalse();
    }

    [TestCase]
    public void AStandardMobPokeIsGroundedHitstunAndTheEnemyStampsItsTier() {
        StaticBody2D floor = CreateFlatFloor();
        PlayerController player = CreateGroundedPlayer();
        EnemyController standard = null;
        EnemyController elite = null;
        try {
            Hitbox standardHitbox = null;
            standard = CreateEnemy("chrono_slasher", ((SceneTree)Engine.GetMainLoop()).Root, Origin + new Vector2(3000f, -2000f));
            elite = CreateEnemy("steam_automaton", ((SceneTree)Engine.GetMainLoop()).Root, Origin + new Vector2(3600f, -2000f));
            standardHitbox = standard.GetNode<Hitbox>("Hitbox");
            AssertThat(standardHitbox.SourceIsStandardMob).IsTrue();
            AssertThat(standardHitbox.CreatePayload(0).SourceIsStandardMob).IsTrue();
            AssertThat(elite.GetNode<Hitbox>("Hitbox").SourceIsStandardMob).IsFalse();

            player.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = -1,
                TargetIndex = 0,
                AttackID = "chrono_slasher",
                HitboxID = "primary",
                AttackClass = AttackClass.Basic,
                Damage = 5f,
                Knockback = new Vector2(3f, -2f),
                HitstunDuration = 0.3f,
                HitOrigin = player.GlobalPosition + new Vector2(-30f, 0f),
                AttackerFacingRight = true,
                SourceIsStandardMob = true
            });
            AssertThat(player.CurrentState).IsEqual(CharacterState.Stunned);
            AssertThat(player.IsInTumble)
                .OverrideFailureMessage("A standard-mob poke must not tumble the player.")
                .IsFalse();
            AssertThat(player.Velocity.Y)
                .OverrideFailureMessage("A grounded standard-mob poke is a slide, not a launch.")
                .IsEqual(0f);
            AssertThat(player.Velocity.X > 0f).IsTrue();
            for (int frame = 0; frame < 60 && player.CurrentState == CharacterState.Stunned; frame++) {
                HoldInput(player, GameplayButtons.None);
                AssertThat(player.IsInKnockdownOrGetUp)
                    .OverrideFailureMessage("A standard-mob poke must never knock the player down.")
                    .IsFalse();
            }
        } finally {
            standard?.Free();
            elite?.Free();
            InputManager.Instance?.ClearInputSource(player.PlayerIndex);
            player.Free();
            floor.Free();
        }
    }

    // === G2 (fix pass): the meter field follows the meter node =================

    [TestCase]
    public void AnUltimateCastLeavesTheMeterFieldOnTheNodesValueAndDefyUnarmed() {
        // Joan's Grand Crusade spends the meter inside its own OnStartup
        // (UltimateMeter.Consume), never through PlayerController. The public
        // CurrentUltimateMeter field — which Defy History, the Defy seal and the
        // save writers read — used to stay at 100 until the next meter event, so
        // a lethal hit right after the cast was defied on an empty meter.
        StaticBody2D floor = CreateFlatFloor();
        PlayerController joan = CreateGroundedPlayer("joan");
        try {
            var meter = joan.GetNode<UltimateMeter>("UltimateMeter");
            meter.SetValue(UltimateMeter.MaxValue);
            AssertThat(joan.CurrentUltimateMeter)
                .OverrideFailureMessage("A write to the meter node must reach the controller's field.")
                .IsEqual(UltimateMeter.MaxValue);
            AssertThat(joan.DefySeal).IsEqual(DefySealState.Ready);

            SendInput(joan, GameplayButtons.Ultimate);
            AssertThat(joan.CurrentState).IsEqual(CharacterState.UsingUltimate);
            AssertThat(meter.CurrentValue).IsEqual(0f);
            AssertThat(joan.CurrentUltimateMeter)
                .OverrideFailureMessage("The Ultimate spent the meter node; the field must read the same 0.")
                .IsEqual(meter.CurrentValue);
            AssertThat(joan.DefySeal)
                .OverrideFailureMessage("A spent meter cannot light the Defy seal.")
                .IsEqual(DefySealState.Building);

            // A lethal blow on the spent meter is not defied.
            joan.ApplyDamage(joan.MaximumHP * 2);
            AssertThat(joan.StoryDefyHistoryUsed)
                .OverrideFailureMessage("Defy History fired on a meter the Ultimate had already spent.")
                .IsFalse();
            AssertThat(joan.CurrentHP).IsEqual(0);
        } finally {
            InputManager.Instance?.ClearInputSource(joan.PlayerIndex);
            joan.Free();
            floor.Free();
        }
    }

    // === G7 (fix pass): drop-through is the double-tap of Down from standing ===

    [TestCase]
    public void ADoubleTapOfDownDropsThroughAOneWayPlatformFromStandingAndRunning() {
        // design-godot.md: Story drop-through is a double-tap of Down (the sim's
        // Down+Jump is untouched). Idle and Running turned the first Down into a
        // crouch BEFORE the tap was recorded, so a double-tap from standing or
        // running never dropped; a single Down must still only crouch.
        foreach (bool running in new[] { false, true }) {
            var scene = ResourceLoader.Load<PackedScene>("res://scenes/templates/OneWayPlatformTemplate.tscn");
            var platform = scene.Instantiate<FTT.Environment.OneWayPlatform>();
            Vector2 platformAt = Origin + new Vector2(running ? 2000f : 0f, -3000f);
            platform.Position = platformAt;
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(platform);
            float platformTop = platformAt.Y - 10f;
            PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
            player.GlobalPosition = new Vector2(platformAt.X - 90f, platformTop - 4f);
            string label = running ? "running" : "standing";
            try {
                for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
                    player.Velocity = new Vector2(0f, 400f);
                    SendEdge(player, GameplayButtons.None, GameplayButtons.None);
                }
                for (int frame = 0; frame < 20 && player.CurrentState != CharacterState.Idle; frame++) {
                    SendEdge(player, GameplayButtons.None, GameplayButtons.None);
                }
                AssertThat(player.IsOnFloor()).OverrideFailureMessage($"{label}: never stood on the platform.").IsTrue();
                AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);

                // A single Down only crouches, however long it is held.
                SendEdge(player, GameplayButtons.Down, GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Crouching);
                for (int frame = 0; frame < 30; frame++) SendEdge(player, GameplayButtons.Down, GameplayButtons.Down);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Crouching);
                SendEdge(player, GameplayButtons.None, GameplayButtons.Down);
                for (int frame = 0; frame < 30; frame++) SendEdge(player, GameplayButtons.None, GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
                AssertThat(player.IsOnFloor())
                    .OverrideFailureMessage($"{label}: a single held Down must never drop through.")
                    .IsTrue();

                float moveX = running ? 1f : 0f;
                if (running) {
                    for (int frame = 0; frame < 3; frame++) SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX);
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
                }
                // Tap, release, (running: one frame back into the run), tap.
                SendEdge(player, GameplayButtons.Down, GameplayButtons.None, moveX);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Crouching);
                SendEdge(player, GameplayButtons.None, GameplayButtons.Down, moveX);
                if (running) {
                    SendEdge(player, GameplayButtons.None, GameplayButtons.None, moveX);
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Running);
                }
                SendEdge(player, GameplayButtons.Down, GameplayButtons.None, moveX);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage($"{label}: the second tap of Down must drop through the one-way platform.")
                    .IsEqual(CharacterState.Airborne);
                for (int frame = 0; frame < 20; frame++) SendEdge(player, GameplayButtons.None, GameplayButtons.None);
                AssertThat(player.GlobalPosition.Y > platformTop + 40f)
                    .OverrideFailureMessage($"{label}: the body must fall below the platform, feet at {player.GlobalPosition.Y} vs top {platformTop}.")
                    .IsTrue();
            } finally {
                InputManager.Instance?.ClearInputSource(player.PlayerIndex);
                player.Free();
                platform.Free();
            }
        }
    }

    // === helpers ==============================================================

    private static int TicksUntilTheOpenerAndItsHoldEnd(string characterID, int injectHitstop) {
        PlayerController player = CreateAirborne(characterID);
        try {
            int startup = BasicComboRules.StringProfileFor(characterID).AerialStartupFrames[0];
            SendInput(player, GameplayButtons.BasicAttack);
            int ticks = 0;
            for (; ticks < 300 && player.CurrentState == CharacterState.Attacking; ticks++) {
                SendInput(player, GameplayButtons.None);
                if (injectHitstop > 0 && ticks == startup) {
                    // First active frame: freeze the attacker as a landed hit would.
                    player.ApplyHitstop(injectHitstop);
                    AssertPose(player, "basic_attack_1", AttackPoseRules.StrikePose, "the freeze");
                }
            }
            return ticks;
        } finally {
            Release(player);
        }
    }

    private static void AssertPose(PlayerController player, string animation, int pose, string where) {
        AssertThat(player.ActiveAnimationName)
            .OverrideFailureMessage($"Wrong sheet at {where}.")
            .IsEqual(animation);
        AssertThat(player.ActiveAnimationFrame)
            .OverrideFailureMessage($"Wrong pose at {where}.")
            .IsEqual(pose);
        AssertThat(player.IsShowingAttackPose)
            .OverrideFailureMessage($"The sheet must be held on its pose at {where}, not playing at fps.")
            .IsTrue();
    }

    private static HitPayload AbilityPayload(BaseSpecial ability, HitDelivery delivery, ulong contactId) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = ability.Data.AbilityID,
        HitboxID = "projectile",
        AttackClass = AttackClass.Special,
        Damage = 12f,
        Delivery = delivery,
        Origin = HitOrigin.Special,
        Launches = ability.Data.Launches,
        ContactId = contactId
    };

    private static HitPayload PlayerHit(bool launches) => new() {
        AttackerIndex = 1,
        TargetIndex = 0,
        AttackID = "test.hit",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Damage = 10f,
        Knockback = new Vector2(4f, -2f),
        Launches = launches,
        HitstunDuration = 0.4f,
        HitOrigin = new Vector2(-20f, 0f),
        AttackerFacingRight = true
    };

    private static PlayerController CreateAirborne(string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = Origin + new Vector2(0f, -4000f);
        player.TransitionTo(CharacterState.Airborne);
        player.Velocity = Vector2.Down;
        return player;
    }

    private static void Release(PlayerController player) {
        InputManager.Instance?.ClearInputSource(player.PlayerIndex);
        player.Free();
    }

    private static PlayerController AddEinstein(Node2D host) => AddCharacter(host, "einstein");

    private static PlayerController AddCharacter(Node2D host, string characterID) {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        player.Position = Origin;
        host.AddChild(player);
        player.GlobalPosition = Origin;
        return player;
    }

    /// <summary>
    /// Freezes <paramref name="caster"/> as a landed hit would and ticks it once,
    /// which hands the freeze to its casts — only the cast clock holds; the
    /// ability node keeps processing.
    /// </summary>
    private static void FreezeCaster(PlayerController caster, BaseSpecial cast) {
        caster.ApplyHitstop(8);
        SendInput(caster, GameplayButtons.None);
        AssertThat(caster.IsInHitstop).IsTrue();
        AssertThat(caster.CastClocksSuspended).IsTrue();
        AssertThat(cast.CastClockSuspended).IsTrue();
        AssertThat(cast.IsPhysicsProcessing())
            .OverrideFailureMessage("The ability node must keep processing: it ticks the cast's world effects.")
            .IsTrue();
    }

    /// <summary>Ticks <paramref name="caster"/> out of its freeze and one tick more, handing the cast clocks back.</summary>
    private static void Thaw(PlayerController caster, BaseSpecial cast) {
        for (int frame = 0; frame < 30 && caster.IsInHitstop; frame++) SendInput(caster, GameplayButtons.None);
        SendInput(caster, GameplayButtons.None);
        AssertThat(caster.IsInHitstop).IsFalse();
        AssertThat(cast.CastClockSuspended).IsFalse();
    }

    private static EnemyController CreateEnemy(string enemyID, Node host, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        // Positioned before entering the tree, so the physics server registers
        // its hurtbox where the shape queries will look.
        enemy.Position = position;
        host.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    /// <summary>
    /// R12: <paramref name="characterID"/>'s Special 1 fires a bursting shot (cast
    /// one), starts cast two, and lets cast one's shot fly into a wall over real
    /// physics frames; the wall strike raises the shot's Impacted, whose burst is
    /// deferred. A target just past the wall — out of the shot's path, inside the
    /// burst — takes the burst, the cast's only landed hit.
    /// </summary>
    private static async Task AssertTerrainBurstChargesTheFiringCast<T>(string characterID) where T : BaseSpecial {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Node2D host = CreateHost($"FeelLateBurst_{characterID}");
        // Scoped, so the shot and the burst's flash never park in the autoload's pool.
        var pools = new PoolManager { Name = $"FeelLateBurstPools_{characterID}" };
        tree.Root.AddChild(pools);
        // Away from every other case's geometry; nothing else on Environment here.
        Vector2 at = Origin + new Vector2(-9000f, -9000f);
        try {
            PlayerController caster = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
            caster.Position = at;
            host.AddChild(caster);
            caster.GlobalPosition = at;
            HoldIdleInput(caster);
            AssertThat(caster.IsFacingRight).IsTrue();

            // The wall spans x +85..+105 (the shots spawn at +40/+50 and fly right);
            // the target's body spans +122..+168 — behind the wall, never in the
            // shot's path, and within either burst's radius of the strike point.
            var wall = new StaticBody2D {
                Name = "LateBurstWall",
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0,
                Position = at + new Vector2(95f, 0f)
            };
            wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20f, 600f) } });
            host.AddChild(wall);
            EnemyController enemy = CreateEnemy("chrono_slasher", host, at + new Vector2(145f, 0f));
            enemy.CurrentHP = 9999;
            // A passive target that holds its spot (there is no floor here).
            enemy.SetPhysicsProcess(false);

            var ability = caster.GetNode<T>("Special1");
            AssertThat(ability.TryExecute()).IsTrue();
            int castOne = ability.ExecutionSerial;
            ability.AdvanceToActive();
            PlaceholderProjectile shot = FirstChildOf<PlaceholderProjectile>(host);
            AssertThat(shot).OverrideFailureMessage($"{characterID}: cast one fired no shot.").IsNotNull();
            AssertThat(shot.SourceExecutionSerial).IsEqual(castOne);

            // Cast two starts while the shot is in flight; its clock is held so
            // it never fires a shot of its own here.
            ability.Interrupt();
            AssertThat(ability.TryExecute()).IsTrue();
            AssertThat(ability.ExecutionSerial).IsEqual(castOne + 1);
            ability.SetPhysicsProcess(false);

            for (int frame = 0; frame < 40 && enemy.CurrentHP == 9999; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(enemy.CurrentHP < 9999)
                .OverrideFailureMessage($"{characterID}: the shot never burst on the wall.")
                .IsTrue();
            AssertThat(caster.IsInHitstop)
                .OverrideFailureMessage($"{characterID}: cast one never froze its caster, so its late burst still must.")
                .IsTrue();
            AssertThat(ability.TryClaimCasterHitstop(castOne))
                .OverrideFailureMessage($"{characterID}: the burst's freeze was not charged to cast one.")
                .IsFalse();
            AssertThat(ability.TryClaimCasterHitstop())
                .OverrideFailureMessage($"{characterID}: cast one's late burst spent cast two's freeze.")
                .IsTrue();
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    /// <summary>Points <paramref name="player"/>'s input slot at a source that holds nothing, for real frames.</summary>
    private static void HoldIdleInput(PlayerController player) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
    }

    private static T FirstChildOf<T>(Node parent) where T : Node {
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is T match) return match;
        }
        return null;
    }

    private static Node2D CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    /// <summary>Environment floor with its top surface at y = 0.</summary>
    private static StaticBody2D CreateFlatFloor() {
        var floor = new StaticBody2D {
            Name = "FeelPassFloor",
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

    private static PlayerController CreateGroundedPlayer(string characterID = "einstein") {
        PlayerController player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.GlobalPosition = new Vector2(0f, -8f);
        for (int frame = 0; frame < 120 && !player.IsOnFloor(); frame++) {
            player.Velocity = new Vector2(player.Velocity.X, 400f);
            HoldInput(player, GameplayButtons.None);
        }
        AssertThat(player.IsOnFloor())
            .OverrideFailureMessage("The harness player must be standing on the floor.")
            .IsTrue();
        for (int frame = 0; frame < 10 && player.CurrentState != CharacterState.Idle; frame++) {
            HoldInput(player, GameplayButtons.None);
        }
        return player;
    }

    private static void HoldInput(PlayerController player, GameplayButtons buttons) =>
        SendInput(player, buttons);

    private static void SendInput(PlayerController player, GameplayButtons buttons) =>
        SendFrame(player, PlayerInputFrame.Create(0, 0f, 0f, buttons));

    /// <summary>
    /// One tick holding <paramref name="held"/> against last tick's
    /// <paramref name="previousHeld"/>, so press and release edges are explicit
    /// (<see cref="SendInput"/> reports every held button as freshly pressed).
    /// Down also drives the vertical axis, as the InputManager synthesizes it.
    /// </summary>
    private static void SendEdge(PlayerController player, GameplayButtons held, GameplayButtons previousHeld,
        float moveX = 0f) =>
        SendFrame(player, PlayerInputFrame.Create(0, moveX,
            (held & GameplayButtons.Down) != 0 ? 1f : 0f, held, previousHeld));

    private static void SendFrame(PlayerController player, PlayerInputFrame frame) {
        var source = new BufferedInputSource();
        source.SetNextFrame(frame);
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
