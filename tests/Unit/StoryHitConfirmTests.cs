using System;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// The hit-confirm cancel (D1), the landed-contact definition (D1a), the
/// Momentum gauge's landed-swing gains (user rulings 2026-10-11, D2 replaced by
/// the Momentum gauge on 2026-10-31) and the freeze-only Special buffer,
/// Story side. A swing whose contact LANDS — the receiver applied damage, or a
/// shield absorbed it on a body that was not invulnerable — releases Special 1
/// and Special 2 from its own lockout; the Ultimate keeps the whole 30 frames.
/// A strike surface, a drill-invulnerable mob, a warded boss, a block, an
/// invulnerable hero and a Time Freeze discard do not land. Every landed swing
/// adds to the Momentum gauge once — hits 1-2, the Up-Attack and the Down-Air
/// 0.05 pip, the finisher 0.2 — bosses included. A Special pressed inside a landed swing's freeze
/// fires on the first tick after it, if it is legal then.
///
/// <para>Contacts are driven by emitting the melee hitbox's AreaEntered on the
/// target's hurtbox (the FeelPassStoryTests idiom); timings are measured.</para>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryHitConfirmTests {
    private const double Step = 1.0 / 60.0;
    private static readonly int Lockout = BasicComboRules.AbilityLockoutFrames;
    // Away from every other suite's geometry.
    private static readonly Vector2 Origin = new(51000f, 0f);
    private const string BossScenePath = "res://scenes/enemies/Boss.tscn";

    // === D1: the hit-confirm cancel ===========================================

    [TestCase]
    public void ALandedSwingReleasesSpecialsButNotTheUltimate() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            AssertThat(player.SwingLanded).IsTrue();
            int freeze = player.HitstopFramesRemaining;
            AssertThat(freeze > 0).OverrideFailureMessage("Harness: the landing must freeze the hero.").IsTrue();

            int accepted = -1;
            for (int tick = 1; tick <= freeze + 4 && accepted < 0; tick++) {
                bool frozen = player.IsInHitstop;
                int lockoutBefore = player.AbilityLockoutFramesRemaining;
                Tick(player, GameplayButtons.Special1);
                if (player.CurrentState != CharacterState.UsingSpecial) continue;
                accepted = tick;
                AssertThat(frozen).IsFalse();
                AssertThat(lockoutBefore > 0)
                    .OverrideFailureMessage("Special 1 must be accepted while the swing's lockout still runs.")
                    .IsTrue();
            }
            AssertThat(accepted)
                .OverrideFailureMessage("Special 1 is accepted on the first unfrozen tick after the contact.")
                .IsEqual(freeze + 1);
        });

        Run((player, host) => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            int freeze = player.HitstopFramesRemaining;
            bool refusedAfterTheThaw = false;
            bool accepted = false;
            for (int tick = 1; tick <= 90 && !accepted; tick++) {
                bool frozen = player.IsInHitstop;
                int lockoutBefore = player.AbilityLockoutFramesRemaining;
                Tick(player, GameplayButtons.Ultimate);
                if (player.CurrentState == CharacterState.UsingUltimate) {
                    accepted = true;
                    AssertThat(!frozen && lockoutBefore <= 1)
                        .OverrideFailureMessage("The Ultimate is accepted only once the lockout runs out.")
                        .IsTrue();
                    break;
                }
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
                if (tick > freeze) refusedAfterTheThaw = true;
            }
            AssertThat(accepted).IsTrue();
            AssertThat(refusedAfterTheThaw)
                .OverrideFailureMessage("The Ultimate must still be refused where a Special would confirm.")
                .IsTrue();
        });
    }

    [TestCase]
    public void TheNextSwingRelocksSpecialsAndALandedAerialHitSurvivesTheLandingCancel() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            // One press inside the freeze buffers hit 2 (F3); nothing else is pressed.
            Tick(player, GameplayButtons.BasicAttack);
            for (int tick = 0; tick < 90 && player.ComboCounter == 0; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.ComboCounter).OverrideFailureMessage("Harness: hit 2 never started.").IsEqual(1);
            AssertThat(player.SwingLanded).IsFalse();
            AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(Lockout);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Hit 2's start re-locks the Specials until it lands.")
                .IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
        });

        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            LiftIntoTheAir(player, 120f);
            Hitbox box = OpenBox(player);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
            Contact(box, mob);
            AssertThat(player.SwingLanded).IsTrue();
            for (int tick = 0; tick < 90 && player.CurrentState == CharacterState.Attacking; tick++) {
                Tick(player, GameplayButtons.None);
            }
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Landing cancels the aerial string.")
                .IsEqual(CharacterState.Idle);
            AssertThat(player.SwingLanded).IsTrue();
            AssertThat(player.AbilityLockoutFramesRemaining > 0)
                .OverrideFailureMessage("Harness: the lockout must still run after the landing.")
                .IsTrue();
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("The aerial hit's confirm survives the landing cancel.")
                .IsEqual(CharacterState.UsingSpecial);
        });
    }

    [TestCase]
    public void ASpecialWithinThirtyTicksKeepsSpecialsLockedWhateverLands() {
        Run((player, host) => {
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
            player.TransitionTo(CharacterState.Idle);
            // Spawned after the cast, so Special 1 never reaches it.
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            int mobHP = mob.CurrentHP;
            Hitbox box = player.GetNode<Hitbox>("MeleeHitbox");

            int unfrozen = 0;
            int accepted = -1;
            bool contacted = false;
            int freezeTicks = 0;
            for (int tick = 1; tick <= 120 && accepted < 0; tick++) {
                bool frozen = player.IsInHitstop;
                if (frozen) freezeTicks++;
                else unfrozen++;
                GameplayButtons press = tick == 20 ? GameplayButtons.BasicAttack
                    : tick > 20 ? GameplayButtons.Special2
                    : GameplayButtons.None;
                Tick(player, press);
                if (!contacted && tick > 20 && box.IsActive) {
                    AssertThat(mob.CurrentHP).IsEqual(mobHP);
                    Contact(box, mob);
                    contacted = true;
                    AssertThat(player.SwingLanded).IsTrue();
                }
                if (tick <= 20) continue;
                if (player.CurrentState == CharacterState.UsingSpecial) {
                    accepted = tick;
                    AssertThat(unfrozen)
                        .OverrideFailureMessage("Special 2 is accepted on the 30th unfrozen tick after Special 1.")
                        .IsEqual(Lockout);
                } else {
                    AssertThat(unfrozen < Lockout)
                        .OverrideFailureMessage($"Special 2 refused on the {unfrozen}th unfrozen tick after Special 1.")
                        .IsTrue();
                }
            }
            AssertThat(contacted).OverrideFailureMessage("Harness: the T+20 swing never opened its box.").IsTrue();
            AssertThat(freezeTicks > 0).IsTrue();
            AssertThat(accepted).IsEqual(Lockout + freezeTicks);
        });
    }

    // === D1a: what lands ======================================================

    [TestCase]
    public void PropsDrillInvulnerableAndWardedTargetsNeitherConfirmNorGainButADummyDoes() {
        // A strike surface (a checkpoint fracture, an extractor) takes damage but never lands.
        Run((player, host) => {
            var surface = new EnvironmentHurtboxAdapter {
                Name = "ConfirmStrikeSurface",
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.PersistentObject,
                CollisionMask = CollisionLayers.PlayerHitbox,
                Position = Origin + new Vector2(40f, -40f)
            };
            surface.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 60f) } });
            int strikes = 0;
            surface.OnHit += _ => {
                strikes++;
                return 10f;
            };
            host.AddChild(surface);
            AssertSwingDoesNotLand(player, surface, () => strikes > 0, "a strike surface");
        });

        // A drill-invulnerable mob.
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            mob.DrillInvulnerable = true;
            int hp = mob.CurrentHP;
            AssertSwingDoesNotLand(player, mob, () => true, "a drill-invulnerable mob");
            AssertThat(mob.CurrentHP).IsEqual(hp);
        });

        // A warded boss: engaged, leashed, its hero outside the arena and beyond its reach.
        Run((player, host) => {
            float arenaLeft = Origin.X + 900f;
            BossController boss = CreateBoss(host, ScratchBoss("confirm_ward_boss", shoveable: true));
            boss.ArenaBounds = new Rect2(arenaLeft, Origin.Y - 4000f, 1200f, 5000f);
            boss.GlobalPosition = new Vector2(arenaLeft + 600f, Origin.Y);
            boss.SetTargetForTest(player);
            AssertThat(boss.IsArenaWarded).OverrideFailureMessage("Harness: the boss must be warded.").IsTrue();
            int hp = boss.CurrentHP;
            AssertSwingDoesNotLand(player, boss, () => boss.WardedHitsRefused > 0, "a warded boss");
            AssertThat(boss.CurrentHP).IsEqual(hp);
        });

        // The Level 0 training dummy is the enemy stand-in: it lands.
        Run((player, host) => {
            var dummy = new TrainingDummy { Name = "ConfirmDummy", Position = Origin + new Vector2(40f, 0f) };
            host.AddChild(dummy);
            AssertSwingLandsAndGains(player, dummy, BasicComboRules.MomentumStringHitGainUnits);
        });

        // A live mob lands.
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            AssertSwingLandsAndGains(player, mob, BasicComboRules.MomentumStringHitGainUnits);
        });
    }

    [TestCase]
    public void AShieldAbsorbedHitConfirmsAndGainsButABlockedOrInvulnerableOneDoesNot() {
        // Temporal Aegis absorbs the whole contact: it LANDED (D1a).
        RunDuel((hero, attacker) => {
            hero.GrantTemporalAegis();
            int hp = hero.CurrentHP;
            bool contactLanded = SwingAtHero(attacker, hero, out float pipsBefore);
            AssertThat(hero.HasTemporalAegis).OverrideFailureMessage("Harness: Aegis must take the contact.").IsFalse();
            AssertThat(hero.CurrentHP).IsEqual(hp);
            AssertThat(contactLanded).IsTrue();
            AssertThat(attacker.SwingLanded).IsTrue();
            AssertFloat(attacker.MomentumPips)
                .IsEqualApprox(pipsBefore + BasicComboRules.MomentumStringHitGainUnits / 7200f, 0.0001f);
        });

        // The hero's block stance takes it: no landing.
        RunDuel((hero, attacker) => {
            // The first tick enters the state; the stance rises on the next.
            Tick(hero, GameplayButtons.Block);
            Tick(hero, GameplayButtons.Block);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
            // CM-BLOCK (decision 3.4): hold the stance past its 5-frame
            // perfect-block window, so the swing meets an ordinary block.
            for (int tick = 0; tick < BasicComboRules.PerfectBlockWindowFrames; tick++) Tick(hero, GameplayButtons.Block);
            AssertThat(hero.IsPerfectBlockWindowOpen).IsFalse();
            int hp = hero.CurrentHP;
            bool contactLanded = SwingAtHero(attacker, hero, out float pipsBefore);
            AssertThat(hero.CurrentHP).OverrideFailureMessage("Harness: the swing must be blocked.").IsEqual(hp);
            AssertThat(contactLanded).IsFalse();
            AssertThat(attacker.SwingLanded).IsFalse();
            AssertThat(attacker.MomentumPips).IsEqual(pipsBefore);
        });

        // Aegis on a post-rewind-invulnerable hero: the shield is spent (a
        // recorded D01 divergence) but the contact does not land.
        RunDuel((hero, attacker) => {
            // A death rewind, then its thaw's 2 s protection (the seam the
            // Post-Landing Hold calls at the thaw).
            hero.CompleteStoryRewind(hero.GlobalPosition, hero.CurrentHP, grounded: true);
            hero.ArmRecoveryProtection();
            AssertThat(hero.IsPostRewindInvulnerable).IsTrue();
            hero.GrantTemporalAegis();
            int hp = hero.CurrentHP;
            bool contactLanded = SwingAtHero(attacker, hero, out float pipsBefore);
            AssertThat(hero.CurrentHP).IsEqual(hp);
            AssertThat(contactLanded)
                .OverrideFailureMessage("An absorption on an invulnerable body is not a landed contact.")
                .IsFalse();
            AssertThat(attacker.SwingLanded).IsFalse();
            AssertThat(attacker.MomentumPips).IsEqual(pipsBefore);
        });
    }

    // === The Momentum gauge's gains ===========================================

    [TestCase]
    public void ALandedHitGainsOncePerSwingEvenWhenItCleavesTwoMobs() {
        Run((player, host) => {
            EnemyController first = Mob(host, Origin + new Vector2(40f, 0f));
            EnemyController second = Mob(host, Origin + new Vector2(60f, 0f));
            int firstHP = first.CurrentHP;
            int secondHP = second.CurrentHP;
            Hitbox box = OpenBox(player);
            player.RestoreMomentumPips(0f);
            Contact(box, first);
            Contact(box, second);
            AssertThat(first.CurrentHP < firstHP && second.CurrentHP < secondHP)
                .OverrideFailureMessage("Harness: the opener must cleave both mobs.")
                .IsTrue();
            AssertFloat(player.MomentumPips)
                .OverrideFailureMessage("A swing that cleaves two mobs still pays 0.05 pip once.")
                .IsEqualApprox(BasicComboRules.MomentumStringHitGainUnits / 7200f, 0.0001f);
        });
    }

    [TestCase]
    public void TheFinisherGainsTwoTenthsAndAFullLandedStringThreeTenthsOfAPip() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            player.RestoreMomentumPips(0f);
            int[] gains = LandString(player, mob, hits: 3);
            AssertThat(gains[0]).IsEqual(BasicComboRules.MomentumStringHitGainUnits);
            AssertThat(gains[1]).IsEqual(BasicComboRules.MomentumStringHitGainUnits);
            AssertThat(gains[2]).OverrideFailureMessage("The landed finisher adds a fifth of a pip.")
                .IsEqual(BasicComboRules.MomentumFinisherGainUnits);
            AssertFloat(player.MomentumPips).OverrideFailureMessage("A full landed string is 0.3 pip.")
                .IsEqualApprox(2160f / 7200f, 0.0001f);
        });

        // The landed swing releases Special 1 from its lockout the first tick after the freeze, and
        // the gauge (2 pips at the start) pays for it.
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            int freeze = player.HitstopFramesRemaining;
            for (int tick = 0; tick < freeze; tick++) Tick(player, GameplayButtons.None);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
        });
    }

    [TestCase]
    public void TheUpAttackAndTheDownAirEachGainAFiftiethOfAPip() {
        Run((player, host) => {
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox up = OpenBox(player, GameplayButtons.BasicAttack, vertical: -1f);
            AssertThat(up.HitboxID).IsEqual(BasicComboRules.UpAttackHitboxID);
            player.RestoreMomentumPips(0f);
            Contact(up, mob);
            AssertFloat(player.MomentumPips).IsEqualApprox(BasicComboRules.MomentumDirectionalGainUnits / 7200f, 0.0001f);
            for (int tick = 0; tick < 120 && player.CurrentState != CharacterState.Idle; tick++) Tick(player, GameplayButtons.None);

            LiftIntoTheAir(player, 300f);
            Hitbox down = OpenBox(player, GameplayButtons.BasicAttack | GameplayButtons.Down, vertical: 1f);
            AssertThat(down.HitboxID).IsEqual(BasicComboRules.DownAirHitboxID);
            Contact(down, mob);
            AssertFloat(player.MomentumPips)
                .IsEqualApprox(2f * BasicComboRules.MomentumDirectionalGainUnits / 7200f, 0.0001f);
        });
    }

    [TestCase]
    public void ALandedHitOnABossGainsLikeAnyEnemy() {
        // Addendum ruling (2026-10-11): a landed swing on a boss pays the gauge.
        Run((player, host) => {
            BossController boss = CreateBoss(host, ScratchBoss("confirm_open_boss", shoveable: false));
            boss.GlobalPosition = Origin + new Vector2(60f, 0f);
            AssertThat(boss.IsArenaWarded).IsFalse();
            int hp = boss.CurrentHP;
            AssertSwingLandsAndGains(player, boss, BasicComboRules.MomentumStringHitGainUnits);
            AssertThat(boss.CurrentHP < hp).IsTrue();
        });
    }

    // === §1.7: a meter-paid Ultimate held back by the lockout =================

    [TestCase]
    public void AMeterPaidUltimateHeldBackByTheLockoutTakesTheTickFromTheSpecials() {
        Run((player, host) => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            for (int tick = 0; tick < 30 && player.IsInHitstop; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.SwingLanded).IsTrue();
            AssertThat(player.SpecialGateFramesRemaining).IsEqual(0);
            AssertThat(player.AbilityLockoutFramesRemaining > 1).IsTrue();

            Tick(player, GameplayButtons.Special2 | GameplayButtons.Ultimate);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("A meter-paid Ultimate held back by the lockout claims the tick.")
                .IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.CurrentState).IsEqual(CharacterState.UsingSpecial);
        });
    }

    // === Resets ===============================================================

    [TestCase]
    public void RewindCheckpointAndTimeFreezeHandleTheHitConfirmState() {
        foreach (bool rewind in new[] { true, false }) {
            Run((player, host) => {
                ConfirmIntoSpecialOne(player, host);
                AssertThat(player.SwingLanded).IsTrue();
                AssertThat(player.SpecialLockoutFramesRemaining > 0).IsTrue();
                AssertThat(player.AbilityLockoutFramesRemaining > 0).IsTrue();
                if (rewind) player.CompleteStoryRewind(player.GlobalPosition, player.CurrentHP, grounded: true);
                else player.RestoreStoryCheckpoint(player.GlobalPosition, player.CurrentHP, 0f);
                AssertThat(player.AbilityLockoutFramesRemaining).IsEqual(0);
                AssertThat(player.SpecialLockoutFramesRemaining).IsEqual(0);
                AssertThat(player.SwingLanded).IsFalse();
            });
        }

        // Time Freeze holds the Special-sourced lockout like the lockout itself.
        Run((player, host) => {
            Tick(player, GameplayButtons.Special1);
            player.TransitionTo(CharacterState.Idle);
            Tick(player, GameplayButtons.None);
            int held = player.SpecialLockoutFramesRemaining;
            AssertThat(held).IsEqual(Lockout - 1);
            player.TimeFrozen = true;
            for (int tick = 0; tick < 5; tick++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.SpecialLockoutFramesRemaining).IsEqual(held);
            }
            player.TimeFrozen = false;
            Tick(player, GameplayButtons.None);
            AssertThat(player.SpecialLockoutFramesRemaining).IsEqual(held - 1);
        });

        // A contact on a time-frozen world is discarded before any receiver.
        Run((player, host) => {
            StoryManager.Instance?.SetTimeFreezeCooldown(0f);
            var freeze = new TimeFreezeController { Name = "ConfirmTimeFreeze" };
            host.AddChild(freeze);
            freeze.SetPhysicsProcess(false);
            try {
                EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
                int hp = mob.CurrentHP;
                AssertThat(freeze.TryBeginTimeFreeze()).IsTrue();
                // The hero swings through its own freeze flag; the world stays frozen.
                player.TimeFrozen = false;
                Hitbox box = OpenBox(player);
                Contact(box, mob);
                AssertThat(mob.CurrentHP).IsEqual(hp);
                AssertThat(player.SwingLanded)
                    .OverrideFailureMessage("A Time Freeze discard is not a landed contact.")
                    .IsFalse();
                AssertThat(player.MomentumPips).IsEqual(BasicComboRules.MomentumStartPips);
            } finally {
                if (freeze.IsFrozen) freeze.EndFreeze(early: true);
                StoryManager.Instance?.SetTimeFreezeCooldown(0f);
            }
        });
    }

    // === Special → Special and the Specials' own hits =========================

    [TestCase]
    public void AHitConfirmedSpecialStillLocksTheOtherSpecialForThirtyFrames() {
        Run((player, host) => {
            ConfirmIntoSpecialOne(player, host);
            player.TransitionTo(CharacterState.Idle);
            int unfrozen = 0;
            bool accepted = false;
            for (int tick = 1; tick <= 90 && !accepted; tick++) {
                if (!player.IsInHitstop) unfrozen++;
                Tick(player, GameplayButtons.Special2);
                accepted = player.CurrentState == CharacterState.UsingSpecial;
                if (accepted) {
                    AssertThat(unfrozen)
                        .OverrideFailureMessage("Special 2 waits the 30 unfrozen ticks after the confirmed Special 1.")
                        .IsEqual(Lockout);
                } else {
                    AssertThat(unfrozen < Lockout).IsTrue();
                }
            }
            AssertThat(accepted).IsTrue();
        });
    }

    [TestCase]
    public void ASpecialsOwnLandedHitNeverSetsTheConfirm() {
        Run((player, host) => {
            var emc = player.GetNode<EinsteinEmc2Blast>("Special1");
            AssertThat(emc.TryExecute()).IsTrue();
            player.SpecialOneCooldownTimer = 5f;
            player.SpecialTwoCooldownTimer = 5f;

            // A Hitbox-delivered Special: the cast's pooled shot.
            EnemyController shotTarget = Mob(host, Origin + new Vector2(300f, 0f));
            shotTarget.CurrentHP = 9999;
            var projectile = new PlaceholderProjectile { Name = "ConfirmProbeShot" };
            host.AddChild(projectile);
            projectile.GlobalPosition = shotTarget.GlobalPosition + new Vector2(-10f, 0f);
            projectile.Setup(7f, new Vector2(2f, -1f), 0f, true, 0, Colors.White,
                sourcePlayer: player, data: emc.Data);
            projectile.GetNode<Hitbox>("Hitbox").EmitSignal(
                Area2D.SignalName.AreaEntered, shotTarget.GetNode<Hurtbox>("Hurtbox"));
            AssertThat(shotTarget.CurrentHP < 9999).OverrideFailureMessage("Harness: the shot must land.").IsTrue();

            // A shape-query Special: the E=mc² burst.
            Vector2 burstAt = Origin + new Vector2(700f, 0f);
            EnemyController burstTarget = Mob(host, burstAt);
            burstTarget.CurrentHP = 9999;
            emc.DetonateForTest(burstAt);
            AssertThat(burstTarget.CurrentHP < 9999).OverrideFailureMessage("Harness: the burst must land.").IsTrue();

            AssertThat(player.SwingLanded)
                .OverrideFailureMessage("A Special's own hit never stands in for a landed swing.")
                .IsFalse();
            AssertThat(player.MomentumPips)
                .OverrideFailureMessage("A Special's own hits pay the gauge nothing (G2.4).")
                .IsEqual(BasicComboRules.MomentumStartPips);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(5f);
        });
    }

    // === The freeze-only Special buffer (user ruling 2026-10-11) ==============

    [TestCase]
    public void ASpecialPressedInsideTheLandedSwingsFreezeFiresOnTheFirstTickAfterIt() {
        foreach (bool press in new[] { true, false }) {
            Run((player, host) => {
                EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
                Hitbox box = OpenBox(player);
                Contact(box, mob);
                int freeze = player.HitstopFramesRemaining;
                AssertThat(freeze >= 2).OverrideFailureMessage("Harness: the freeze needs a tick before its last.").IsTrue();

                // Pressed on the freeze's first tick only.
                Tick(player, press ? GameplayButtons.Special1 : GameplayButtons.None);
                AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                AssertThat(player.FreezeBufferedSpecials)
                    .IsEqual(press ? GameplayButtons.Special1 : GameplayButtons.None);
                for (int tick = 1; tick < freeze; tick++) {
                    Tick(player, GameplayButtons.None);
                    AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
                }
                AssertThat(player.IsInHitstop).IsFalse();
                Tick(player, GameplayButtons.None);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage(press
                        ? "The held Special 1 fires on the first tick after the freeze."
                        : "Control: with nothing pressed the swing runs on.")
                    .IsEqual(press ? CharacterState.UsingSpecial : CharacterState.Attacking);
                AssertThat(player.FreezeBufferedSpecials).IsEqual(GameplayButtons.None);
            });
        }
    }

    [TestCase]
    public void TheFreezeBufferHoldsNoUltimateNoLockedPressAndNoOtherFreeze() {
        // The Ultimate chord inside the freeze: nothing is held, nothing fires.
        Run((player, host) => {
            player.AddInfluenceFromDamageDealt(UltimateMeter.MaxValue, collectsEcho: false);
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            int freeze = player.HitstopFramesRemaining;
            Tick(player, GameplayButtons.Special2 | GameplayButtons.MovementAbility | GameplayButtons.Ultimate);
            AssertThat(player.FreezeBufferedSpecials).IsEqual(GameplayButtons.None);
            for (int tick = 0; tick < freeze + 2; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState)
                .OverrideFailureMessage("Neither the Ultimate nor the chord's Special 2 is ever buffered.")
                .IsEqual(CharacterState.Attacking);
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
            AssertThat(player.CurrentUltimateMeter).IsEqual(UltimateMeter.MaxValue);
        });

        // A held press the gate refuses at the thaw is dropped, not kept.
        Run((player, host) => {
            Tick(player, GameplayButtons.Special1);
            player.TransitionTo(CharacterState.Idle);
            EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
            Hitbox box = OpenBox(player);
            Contact(box, mob);
            int freeze = player.HitstopFramesRemaining;
            AssertThat(player.SpecialLockoutFramesRemaining > freeze + 2)
                .OverrideFailureMessage("Harness: Special 1's lockout must outlast the freeze.")
                .IsTrue();
            Tick(player, GameplayButtons.Special2);
            AssertThat(player.FreezeBufferedSpecials).IsEqual(GameplayButtons.Special2);
            for (int tick = 0; tick < 60; tick++) {
                Tick(player, GameplayButtons.None);
                AssertThat(player.CurrentState)
                    .OverrideFailureMessage("A refused held press is never re-tried when the gate opens.")
                    .IsNotEqual(CharacterState.UsingSpecial);
            }
            AssertThat(player.SpecialTwoCooldownTimer).IsEqual(0f);
        });

        // A freeze that is not a landed swing's (the victim's seam) holds nothing.
        Run((player, host) => {
            player.ApplyHitstop(6);
            Tick(player, GameplayButtons.Special1);
            AssertThat(player.FreezeBufferedSpecials).IsEqual(GameplayButtons.None);
            for (int tick = 0; tick < 8; tick++) Tick(player, GameplayButtons.None);
            AssertThat(player.CurrentState).IsEqual(CharacterState.Idle);
            AssertThat(player.SpecialOneCooldownTimer).IsEqual(0f);
        });
    }

    // === helpers ==============================================================

    private static void AssertSwingDoesNotLand(PlayerController player, Node target, Func<bool> reached, string what) {
        Hitbox box = OpenBox(player);
        player.RestoreMomentumPips(1f);
        bool contactLanded = true;
        void OnConfirmed(HitPayload payload, float dealt) => contactLanded = payload.ContactLanded;
        box.HitConfirmed += OnConfirmed;
        try {
            Contact(box, target);
        } finally {
            box.HitConfirmed -= OnConfirmed;
        }
        AssertThat(reached()).OverrideFailureMessage($"Harness: the contact must reach {what}.").IsTrue();
        AssertThat(contactLanded).OverrideFailureMessage($"{what} must not report a landed contact.").IsFalse();
        AssertThat(player.SwingLanded).OverrideFailureMessage($"{what} must not confirm.").IsFalse();
        AssertThat(player.MomentumPips).OverrideFailureMessage($"{what} pays the gauge nothing.").IsEqual(1f);
    }

    private static void AssertSwingLandsAndGains(PlayerController player, Node target, int gainUnits) {
        Hitbox box = OpenBox(player);
        player.RestoreMomentumPips(1f);
        bool contactLanded = false;
        void OnConfirmed(HitPayload payload, float dealt) => contactLanded = payload.ContactLanded;
        box.HitConfirmed += OnConfirmed;
        try {
            Contact(box, target);
        } finally {
            box.HitConfirmed -= OnConfirmed;
        }
        AssertThat(contactLanded).IsTrue();
        AssertThat(player.SwingLanded).IsTrue();
        AssertFloat(player.MomentumPips).IsEqualApprox(1f + gainUnits / 7200f, 0.0001f);
    }

    /// <summary>The opener lands and Special 1 is pressed until it is accepted (the hit-confirm).</summary>
    private static void ConfirmIntoSpecialOne(PlayerController player, Node2D host) {
        EnemyController mob = Mob(host, Origin + new Vector2(40f, 0f));
        Hitbox box = OpenBox(player);
        Contact(box, mob);
        for (int tick = 0; tick < 20 && player.CurrentState != CharacterState.UsingSpecial; tick++) {
            Tick(player, GameplayButtons.Special1);
        }
        AssertThat(player.CurrentState)
            .OverrideFailureMessage("Harness: the hit-confirm Special 1 never fired.")
            .IsEqual(CharacterState.UsingSpecial);
    }

    /// <summary>
    /// Lands <paramref name="hits"/> chained string hits on <paramref name="mob"/>,
    /// returning each landing's gauge gain in units (7200 to a pip).
    /// </summary>
    private static int[] LandString(PlayerController player, EnemyController mob, int hits) {
        var gains = new int[hits];
        Hitbox box = player.GetNode<Hitbox>("MeleeHitbox");
        Tick(player, GameplayButtons.BasicAttack);
        int contacts = 0;
        int contactedHit = -1;
        bool pressNext = false;
        for (int tick = 0; tick < 400 && contacts < hits; tick++) {
            GameplayButtons press = pressNext ? GameplayButtons.BasicAttack : GameplayButtons.None;
            pressNext = false;
            Tick(player, press);
            if (!box.IsActive || player.ComboCounter == contactedHit) continue;
            float pipsBefore = player.MomentumPips;
            Contact(box, mob);
            gains[contacts] = (int)Math.Round((player.MomentumPips - pipsBefore) * 7200f);
            contactedHit = player.ComboCounter;
            contacts++;
            // The next press lands in the freeze and buffers the next hit (F3).
            pressNext = contacts < hits;
        }
        AssertThat(contacts).OverrideFailureMessage("Harness: the string never reached its length.").IsEqual(hits);
        return gains;
    }

    /// <summary>Starts a swing and ticks until its box opens.</summary>
    private static Hitbox OpenBox(PlayerController player, GameplayButtons press = GameplayButtons.BasicAttack, float vertical = 0f) {
        var box = player.GetNode<Hitbox>("MeleeHitbox");
        Tick(player, press, vertical: vertical);
        AssertThat(player.CurrentState).IsEqual(CharacterState.Attacking);
        for (int tick = 0; tick < 30 && !box.IsActive; tick++) Tick(player, GameplayButtons.None);
        AssertThat(box.IsActive).OverrideFailureMessage("Harness: the swing's box never opened.").IsTrue();
        return box;
    }

    private static void Contact(Hitbox box, Node target) {
        Hurtbox hurtbox = target as Hurtbox ?? target.GetNode<Hurtbox>("Hurtbox");
        box.EmitSignal(Area2D.SignalName.AreaEntered, hurtbox);
    }

    /// <summary>
    /// Lifts the grounded hero <paramref name="height"/> px and ticks until the
    /// controller itself reads it as airborne (the first tick still sees the
    /// stale floor contact of the last move, and Idle waits out its coyote time).
    /// </summary>
    private static void LiftIntoTheAir(PlayerController player, float height) {
        player.GlobalPosition += new Vector2(0f, -height);
        player.Velocity = Vector2.Zero;
        player.TransitionTo(CharacterState.Airborne);
        for (int tick = 0; tick < 20 && (player.CurrentState != CharacterState.Airborne || player.IsOnFloor()); tick++) {
            Tick(player, GameplayButtons.None);
        }
        AssertThat(player.IsOnFloor()).IsFalse();
        AssertThat(player.CurrentState).IsEqual(CharacterState.Airborne);
    }

    /// <summary>Points the attacker's swing at the hero and returns the contact's D1a report.</summary>
    private static bool SwingAtHero(PlayerController attacker, PlayerController hero, out float pipsBefore) {
        Hitbox box = OpenBox(attacker);
        attacker.RestoreMomentumPips(1f);
        pipsBefore = attacker.MomentumPips;
        bool contactLanded = false;
        void OnConfirmed(HitPayload payload, float dealt) => contactLanded = payload.ContactLanded;
        box.HitConfirmed += OnConfirmed;
        try {
            box.EmitSignal(Area2D.SignalName.AreaEntered, hero.GetNode<Hurtbox>("Hurtbox"));
        } finally {
            box.HitConfirmed -= OnConfirmed;
        }
        return contactLanded;
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

    /// <summary>A code-built single-phase boss with no kit (the BossArenaLeashTests scratch boss).</summary>
    private static BossData ScratchBoss(string id, bool shoveable) => new() {
        BossID = id,
        DisplayNameKey = "boss",
        MaxHP = 100,
        MoveSpeed = 4f,
        MeleeRangeThreshold = 2f,
        RangedRangeThreshold = 4f,
        ChronalDustDrop = 0,
        IsKnockbackImmune = !shoveable
    };

    private static BossController CreateBoss(Node host, BossData data) {
        var boss = ResourceLoader.Load<PackedScene>(BossScenePath).Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        host.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    /// <summary>The StoryAbilityLockoutTests harness, with the host exposed for targets.</summary>
    private static void Run(Action<PlayerController, Node2D> body) {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var host = new Node2D { Name = "HitConfirmHost" };
        tree.Root.AddChild(host);
        var pools = new PoolManager { Name = "HitConfirmPools" };
        tree.Root.AddChild(pools);
        host.AddChild(Floor());
        PlayerController player = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        player.Position = Origin + new Vector2(0f, -8f);
        host.AddChild(player);
        try {
            Settle(player);
            // The gauge's regen would blur the exact gain these cases pin: hold it still.
            player.MomentumRegenMultiplier = 0f;
            player.RestoreMomentumPips(BasicComboRules.MomentumStartPips);
            body(player, host);
        } finally {
            InputManager.Instance?.ClearInputSource(0);
            InputManager.Instance?.ClearInputSource(1);
            pools.ClearAllPools();
            tree.Root.RemoveChild(pools);
            pools.Free();
            host.Free();
        }
    }

    /// <summary>The hero (index 0) and an Einstein attacker (index 1) to its left, facing each other.</summary>
    private static void RunDuel(Action<PlayerController, PlayerController> body) {
        Run((hero, host) => {
            PlayerController attacker = CharacterFactory.CreateCharacter("einstein", 1, applyStoryProgression: false);
            attacker.Position = Origin + new Vector2(-100f, -8f);
            host.AddChild(attacker);
            Settle(attacker);
            attacker.MomentumRegenMultiplier = 0f;
            attacker.IsFacingRight = true;
            hero.IsFacingRight = false;
            body(hero, attacker);
        });
    }

    private static StaticBody2D Floor() {
        var floor = new StaticBody2D {
            Name = "HitConfirmFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = Origin
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(4000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        return floor;
    }

    private static void Settle(PlayerController player) {
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
        AssertThat(player.SwingLanded).IsFalse();
    }

    /// <summary>One controller tick; every held button reads as freshly pressed.</summary>
    private static void Tick(PlayerController player, GameplayButtons buttons, float vertical = 0f) {
        var source = new BufferedInputSource();
        source.SetNextFrame(PlayerInputFrame.Create(0, 0f, vertical, buttons));
        InputManager.Instance.SetInputSource(player.PlayerIndex, source);
        player._PhysicsProcess(Step);
    }
}
