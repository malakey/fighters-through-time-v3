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
/// D8 (user direction 2026-10-11, ledger <c>USER-PUNISH-WINDOW</c> and
/// <c>PROVISIONAL-PUNISH-WINDOW-RULES</c>): a non-shatter block of a mob's
/// attack, a Foresight counter of it, or the mob's melee swing whiffing near
/// the hero opens a 60-frame window on that mob in which the hero's direct
/// hits deal x1.2, shown with a GoldBright, enlarged damage number. Mobs and
/// elites only; bosses never get one (<c>VERIFY-BOSS-HP</c> is held). The
/// window runs on the mob's world clock: it pauses only while the mob is
/// frozen. The pure numbers are pinned in <c>StoryEnemyCombatRulesTests</c>;
/// these cases drive the real hero chokepoint, the executor and the mob intake.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StoryPunishWindowTests {
    private const float Step = 1f / 60f;
    private static readonly Vector2 Origin = new(-64000f, -9000f);

    // === Triggers: the block and the counter =================================

    [TestCase]
    public void ANonShatterBlockOpensASixtyFramePunishOnThatEnemyOnly() {
        Node host = Host("PunishBlockHost");
        try {
            PlayerController hero = SpawnHero(host, "joan", Origin);
            EnemyController a = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(90f, 0f));
            EnemyController b = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(300f, 0f));
            RaiseBlock(hero, facingRight: true);

            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(a, hero, damage: 6f));
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage("A one-charge mob hit on a full shield is a non-shatter block.")
                .IsEqual(CharacterState.Blocking);
            AssertThat(a.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames);
            AssertThat(a.IsPunishWindowOpen).IsTrue();
            AssertThat(b.IsPunishWindowOpen)
                .OverrideFailureMessage("Only the blocked mob's window opens.")
                .IsFalse();

            // Joan's 6-damage hit 1: x1.25 (S4) x1.2 on the punished mob, x1.25 on the other.
            AssertThat(Dealt(a, StringHit(a, 0, 6f)))
                .IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(6,
                    StoryEnemyCombatRules.StandardBasicDamageScale * StoryEnemyCombatRules.PunishDamageScale));
            AssertThat(Dealt(b, StringHit(b, 0, 6f)))
                .IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(6, StoryEnemyCombatRules.StandardBasicDamageScale));
            AssertThat(StoryEnemyCombatRules.ScaleIntakeDamage(6,
                StoryEnemyCombatRules.StandardBasicDamageScale * StoryEnemyCombatRules.PunishDamageScale)).IsEqual(9);

            // 60 of A's own unfrozen ticks close it.
            for (int tick = 0; tick < StoryEnemyCombatRules.PunishWindowFrames - 1; tick++) a._PhysicsProcess(Step);
            AssertThat(a.IsPunishWindowOpen).IsTrue();
            a._PhysicsProcess(Step);
            AssertThat(a.IsPunishWindowOpen).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void AShatteringBlockADamagingHitAndABarrierAbsorptionOpenNoPunishButANonShatterBlockDoes() {
        Node host = Host("PunishNegativesHost");
        try {
            EnemyController a = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(90f, 0f));

            // A Resonance Barrier absorbs the whole hit: it reached the hero, but
            // no block opens a window.
            PlayerController hero = SpawnHero(host, "joan", Origin);
            AssertThat(hero.GrantResonanceBarrier(StoryShieldEffect.HenrysBastion, 4101)).IsTrue();
            int hp = hero.CurrentHP;
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(a, hero, damage: 4f));
            AssertThat(hero.CurrentHP).OverrideFailureMessage("The barrier must absorb the hit.").IsEqual(hp);
            AssertThat(a.IsPunishWindowOpen).IsFalse();

            // An unblocked hit that deals damage opens nothing.
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(a, hero, damage: 30f));
            AssertThat(hero.CurrentHP < hp).OverrideFailureMessage("The hit must reach HP.").IsTrue();
            AssertThat(a.IsPunishWindowOpen).IsFalse();
            FreeHero(hero);

            // A shatter opens nothing: one unowned Guard-Crush leaves one charge,
            // then A's Guard-Crush breaks the guard.
            hero = SpawnHero(host, "joan", Origin);
            RaiseBlock(hero, facingRight: true);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(null, hero, damage: 6f, chargeCost: 2));
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(a, hero, damage: 6f, chargeCost: 2));
            AssertThat(hero.CurrentState)
                .OverrideFailureMessage("A Guard-Crush on the last charge must shatter the guard.")
                .IsEqual(CharacterState.Dazed);
            AssertThat(a.IsPunishWindowOpen)
                .OverrideFailureMessage("A shatter is not a punishable block.")
                .IsFalse();
            FreeHero(hero);

            // Positive control: a fresh non-shatter block of A opens A's window.
            hero = SpawnHero(host, "joan", Origin);
            RaiseBlock(hero, facingRight: true);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(a, hero, damage: 6f));
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
            AssertThat(a.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void AForesightCounterOpensAPunishOnTheCounteredAttacker() {
        Node host = Host("PunishForesightHost");
        try {
            PlayerController hero = SpawnHero(host, "tubman", Origin);
            hero.IsFacingRight = true;
            // Inside the 2.5-unit answer reach, facing away from Tubman (no frontal cut).
            EnemyController a = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(90f, 0f));
            a.CurrentHP = 999;
            var foresight = hero.GetNode<TubmanForesight>("Special2");
            AssertThat(foresight.TryExecute()).IsTrue();
            for (int frame = 0; frame < foresight.Data.StartupFrames; frame++) foresight._PhysicsProcess(Step);
            AssertThat(foresight.IsWindowOpen).IsTrue();

            int heroHP = hero.CurrentHP;
            float taken = hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(a, hero, damage: 6f));
            AssertThat(taken).IsEqual(0f);
            AssertThat(hero.CurrentHP).IsEqual(heroHP);
            AssertThat(foresight.CountersLanded).IsEqual(1);
            AssertThat(a.PunishFramesRemaining)
                .OverrideFailureMessage("The countered attacker's window must open.")
                .IsEqual(StoryEnemyCombatRules.PunishWindowFrames);

            // The lantern answer lands on the ability's next tick, inside the window.
            int before = a.CurrentHP;
            foresight._PhysicsProcess(Step);
            int answerBase = Mathf.RoundToInt(hero.StoryAbilityDamage(foresight.Data.BaseDamage, foresight.Data.AbilityID));
            AssertThat(answerBase).IsEqual(20);
            AssertThat(before - a.CurrentHP)
                .OverrideFailureMessage("The answer must strike the punished attacker at x1.2.")
                .IsEqual(StoryEnemyCombatRules.ScaleIntakeDamage(answerBase, StoryEnemyCombatRules.PunishDamageScale));
            AssertThat(before - a.CurrentHP).IsEqual(24);
        } finally {
            Teardown(host);
        }
    }

    // === Trigger: the whiff (real physics frames) ===========================

    [TestCase]
    public async Task AMeleeWhiffNearTheHeroOpensAPunishButAConnectedOrDistantSwingDoesNot() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Node host = Host("PunishWhiffHost");
        Vector2 heroAt = new(-70000f, -9000f);
        try {
            BuildFloor(host, heroAt);
            PlayerController hero = SpawnHero(host, "joan", heroAt);
            hero.IsFacingRight = true;
            // Each mob faces right toward the hero; the legacy box (40 px at
            // offset 30) reaches 70 px pivot to pivot, near is 110.
            EnemyController near = CreateHeldEnemy(host, hero, heroAt - new Vector2(100f, 0f));
            EnemyController connected = CreateHeldEnemy(host, hero, heroAt - new Vector2(50f, 0f));
            EnemyController distant = CreateHeldEnemy(host, hero, heroAt - new Vector2(120f, 0f));
            EnemyController charger = CreateHeldEnemy(host, hero, heroAt - new Vector2(100f, 0f));
            EnemyController pulser = CreateHeldEnemy(host, hero, heroAt - new Vector2(100f, 0f));
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            float pivotReach = StoryEnemyCombatRules.MeleePivotReachPixels(
                30f, 40f, 0f, StoryCombatRules.HeroHurtboxWidthPixels / 2f);
            AssertThat(pivotReach).IsEqualApprox(70f, 0.001f);

            // Outside the box, inside near: the window opens at the active window's end.
            int heroHP = hero.CurrentHP;
            await SwingThroughActiveWindowAsync(tree, near, Melee("test.punish.near"));
            AssertThat(hero.CurrentHP).IsEqual(heroHP);
            AssertThat(near.IsPunishWindowOpen)
                .OverrideFailureMessage("A melee whiff 100 px from the hero must open the window.")
                .IsTrue();

            // The hero inside the box: the contact reaches, so it is no whiff.
            await SwingThroughActiveWindowAsync(tree, connected, Melee("test.punish.connected"));
            AssertThat(hero.CurrentHP < heroHP)
                .OverrideFailureMessage("The connected swing must reach the hero.")
                .IsTrue();
            AssertThat(connected.IsPunishWindowOpen).IsFalse();

            // Beyond the 40 px margin (pivot reach + 50): no window.
            await SwingThroughActiveWindowAsync(tree, distant, Melee("test.punish.distant"));
            AssertThat(distant.IsPunishWindowOpen).IsFalse();

            // A ChargeDash primary that ends beside the hero without contact opens it.
            EnemyAbilityData charge = Melee("test.punish.charge");
            charge.Archetype = EnemyAbilityArchetype.ChargeDash;
            charge.DashSpeed = 300f;
            await SwingThroughActiveWindowAsync(tree, charger, charge);
            AssertThat(charger.IsPunishWindowOpen).IsTrue();

            // An AreaPulse whiff never opens one.
            EnemyAbilityData pulse = Melee("test.punish.pulse");
            pulse.Archetype = EnemyAbilityArchetype.AreaPulse;
            pulse.PulseRadius = 20f;
            await SwingThroughActiveWindowAsync(tree, pulser, pulse);
            AssertThat(pulser.IsPunishWindowOpen).IsFalse();
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public async Task ARolledThroughSwingIsAWhiffButAWallBlockedSwingIsNot() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        Node host = Host("PunishRollHost");
        Vector2 heroAt = new(-74000f, -9000f);
        Vector2 wallSiteAt = heroAt + new Vector2(3000f, 0f);
        PlayerController hero = null;
        try {
            BuildFloor(host, heroAt);
            BuildFloor(host, wallSiteAt);
            hero = SpawnHero(host, "joan", heroAt);
            hero.IsFacingRight = true;
            // The hero stands inside this mob's box (it spans the hero's -30..+10).
            EnemyController roller = CreateHeldEnemy(host, hero, heroAt - new Vector2(40f, 0f));
            // A mob flush against a wall (its face 5 px ahead) with the hero 60 px
            // away on the far side: near, but the whole swing is behind the wall.
            Vector2 walledMobAt = wallSiteAt;
            BuildWall(host, walledMobAt.X + 5f, walledMobAt.Y - 100f);
            EnemyController walled = CreateHeldEnemy(host, hero, walledMobAt);
            var source = new BufferedInputSource();
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None));
            InputManager.Instance.SetInputSource(hero.PlayerIndex, source);
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            // Roll away from the mob, then swing while the roll is invulnerable.
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.Roll));
            for (int frame = 0; frame < 10 && hero.CurrentState != CharacterState.Rolling; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            source.SetNextFrame(PlayerInputFrame.Create(0, 0f, 0f, GameplayButtons.None, GameplayButtons.Roll));
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Rolling);
            for (int frame = 0; frame < 10 && !hero.IsRollInvulnerable; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            AssertThat(hero.IsRollInvulnerable).IsTrue();
            int heroHP = hero.CurrentHP;
            await SwingThroughActiveWindowAsync(tree, roller, Melee("test.punish.rolled", active: 4));
            AssertThat(hero.CurrentHP)
                .OverrideFailureMessage("The roll's invulnerability must refuse the contact.")
                .IsEqual(heroHP);
            AssertThat(roller.IsPunishWindowOpen)
                .OverrideFailureMessage("Rolling through a swing leaves it a whiff and opens the window.")
                .IsTrue();

            // The wall-blocked swing: the hero stands near it, nothing reaches,
            // and still no window.
            for (int frame = 0; frame < 30 && hero.CurrentState == CharacterState.Rolling; frame++) {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            }
            hero.GlobalPosition = walledMobAt + new Vector2(60f, 0f);
            hero.Velocity = Vector2.Zero;
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(Mathf.Abs(hero.GlobalPosition.X - walledMobAt.X - 60f) < 4f).IsTrue();
            await SwingThroughActiveWindowAsync(tree, walled, Melee("test.punish.walled"));
            AssertThat(walled.LastAttackWallBlocked)
                .OverrideFailureMessage("The swing must be fully behind the wall.")
                .IsTrue();
            AssertThat(walled.IsPunishWindowOpen)
                .OverrideFailureMessage("A swing the wall swallowed is not a whiff.")
                .IsFalse();
        } finally {
            if (hero != null) InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
            Teardown(host);
        }
    }

    // === Effect and presentation ===========================================

    [TestCase]
    public void APunishedHitStacksWithBasicsPacingAndFloatsATintedEnlargedNumber() {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        var punishedParent = new Node2D { Name = "PunishNumberParent" };
        var plainParent = new Node2D { Name = "PlainNumberParent" };
        tree.Root.AddChild(punishedParent);
        tree.Root.AddChild(plainParent);
        GlobalSaveData global = SaveManager.Instance?.GlobalData;
        bool numbersBefore = global?.DamageNumbersVisible ?? true;
        try {
            if (global != null) global.DamageNumbersVisible = true;
            EnemyController punished = CreateEnemy(punishedParent, "chrono_slasher", Origin);
            EnemyController plain = CreateEnemy(plainParent, "chrono_slasher", Origin + new Vector2(400f, 0f));
            punished.OpenPunishWindow();

            AssertThat(Dealt(punished, StringHit(punished, 0, 6f))).IsEqual(9);
            AssertThat(Dealt(plain, StringHit(plain, 0, 6f))).IsEqual(8);

            FTT.UI.FloatingDamageNumber gold = SingleNumber(punishedParent);
            AssertThat(gold).IsNotNull();
            AssertThat(NumberColor(gold).IsEqualApprox(FTT.UI.UIPalette.GoldBright))
                .OverrideFailureMessage($"Punished number colour {NumberColor(gold)}.")
                .IsTrue();
            AssertThat(gold.Scale.IsEqualApprox(Vector2.One * StoryEnemyCombatRules.PunishDamageNumberScale))
                .OverrideFailureMessage($"Punished number scale {gold.Scale}.")
                .IsTrue();
            AssertThat(StoryEnemyCombatRules.PunishDamageNumberScale).IsEqualApprox(1.25f, 0.0001f);

            FTT.UI.FloatingDamageNumber white = SingleNumber(plainParent);
            AssertThat(white).IsNotNull();
            AssertThat(NumberColor(white).IsEqualApprox(Colors.White))
                .OverrideFailureMessage($"Plain number colour {NumberColor(white)}.")
                .IsTrue();
            AssertThat(white.Scale.IsEqualApprox(Vector2.One)).IsTrue();
        } finally {
            if (global != null) global.DamageNumbersVisible = numbersBefore;
            ReturnDamageNumbers(punishedParent);
            ReturnDamageNumbers(plainParent);
            punishedParent.Free();
            plainParent.Free();
        }
    }

    [TestCase]
    public void ThePunishWindowRunsOnTheWorldClockAndPausesOnlyForFreezes() {
        Node host = Host("PunishClockHost");
        try {
            PlayerController hero = SpawnHero(host, "joan", Origin);

            // Victim hitstop does not pause it: a punished hit freezes the mob,
            // and each of its hitstop ticks still spends a frame.
            EnemyController stopped = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(90f, 0f));
            stopped.CurrentHP = 999;
            stopped.OpenPunishWindow();
            AssertThat(Dealt(stopped, StringHit(stopped, 0, 6f))).IsEqual(9);
            int hitstop = stopped.HitstopFramesRemaining;
            AssertThat(hitstop > 3).OverrideFailureMessage($"Victim hitstop {hitstop}.").IsTrue();
            for (int tick = 0; tick < 3; tick++) stopped._PhysicsProcess(Step);
            // Those were hitstop ticks, and each still spent a window frame.
            AssertThat(stopped.HitstopFramesRemaining).IsEqual(hitstop - 3);
            AssertThat(stopped.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames - 3);

            // Time Freeze pauses it, and the thaw resumes the count.
            stopped.SetTimeFrozen(true);
            for (int tick = 0; tick < 10; tick++) stopped._PhysicsProcess(Step);
            AssertThat(stopped.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames - 3);
            stopped.SetTimeFrozen(false);
            stopped._PhysicsProcess(Step);
            AssertThat(stopped.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames - 4);

            // A block-opened window closes after exactly 60 of the mob's ticks.
            EnemyController blocked = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(90f, 0f));
            RaiseBlock(hero, facingRight: true);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(EnemyHit(blocked, hero, damage: 6f));
            AssertThat(blocked.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames);
            for (int tick = 0; tick < StoryEnemyCombatRules.PunishWindowFrames - 1; tick++) blocked._PhysicsProcess(Step);
            AssertThat(blocked.IsPunishWindowOpen).IsTrue();
            blocked._PhysicsProcess(Step);
            AssertThat(blocked.IsPunishWindowOpen).IsFalse();

            // A whiff-opened window: the swing's last active tick opens it and
            // that same tick is the first of its 60. A manually stepped swing can
            // never overlap, so the whiff is guaranteed; the hero is kept 100 px
            // in front of the (falling) mob at each tick.
            EnemyController whiffer = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(-400f, 0f));
            whiffer.SetChaseTargetForTest(hero);
            whiffer.BeginAttack(Melee("test.punish.clock", active: 3));
            int ticks = 0;
            while (!whiffer.IsPunishWindowOpen && ticks < 10) {
                hero.GlobalPosition = whiffer.GlobalPosition + new Vector2(100f, 0f);
                whiffer._PhysicsProcess(Step);
                ticks++;
            }
            AssertThat(ticks).IsEqual(3);
            AssertThat(whiffer.PunishFramesRemaining).IsEqual(StoryEnemyCombatRules.PunishWindowFrames - 1);
            for (int tick = 0; tick < StoryEnemyCombatRules.PunishWindowFrames - 2; tick++) whiffer._PhysicsProcess(Step);
            AssertThat(whiffer.IsPunishWindowOpen).IsTrue();
            whiffer._PhysicsProcess(Step);
            AssertThat(whiffer.IsPunishWindowOpen).IsFalse();

            // Rewind restore and both halves of a pool cycle clear it.
            EnemyController reset = CreateEnemy(host, "chrono_slasher", Origin + new Vector2(600f, 0f));
            reset.RewindPolicy = FTT.Environment.StoryRewindPolicy.ResetToInitialState;
            reset.OpenPunishWindow();
            reset.ApplyStoryRewind();
            AssertThat(reset.PunishFramesRemaining).IsEqual(0);
            reset.OpenPunishWindow();
            reset.OnDespawn();
            AssertThat(reset.PunishFramesRemaining).IsEqual(0);
            reset.OnSpawn();
            reset.OpenPunishWindow();
            AssertThat(reset.IsPunishWindowOpen).IsTrue();
            reset.OnSpawn();
            AssertThat(reset.PunishFramesRemaining).IsEqual(0);
        } finally {
            Teardown(host);
        }
    }

    [TestCase]
    public void ABlockedSiphonSnareOpensAPunishButBossesNeverGetOne() {
        Node host = Host("PunishSiphonHost");
        BossController boss = null;
        try {
            PlayerController hero = SpawnHero(host, "einstein", Origin);
            hero.CurrentUltimateMeter = 100f;
            hero.GetNodeOrNull<UltimateMeter>("UltimateMeter")?.SetValue(100f);
            EnemyController eraser = CreateEnemy(host, "unbound_eraser", Origin + new Vector2(90f, 0f));
            EnemyAbilityData siphon = eraser.Data.EliteAbilities[1];
            AssertThat(siphon.Archetype).IsEqual(EnemyAbilityArchetype.SiphonTether);
            RaiseBlock(hero, facingRight: true);

            eraser.BeginAttack(siphon);
            for (int frame = 0; frame < siphon.TelegraphFrames; frame++) eraser.TickAbilityExecutor(Step);
            AssertThat(eraser.LastSiphonResult).IsEqual(SiphonAttachResult.Blocked);
            AssertThat(eraser.PunishFramesRemaining)
                .OverrideFailureMessage("A blocked Siphon Snare must open the Eraser's window.")
                .IsEqual(StoryEnemyCombatRules.PunishWindowFrames);

            // A blocked boss attack changes nothing and throws nothing...
            boss = CreateBoss();
            HitPayload bossStrike = EnemyHit(null, hero, damage: 6f);
            bossStrike.SourceActorId = boss.GetInstanceId();
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
            hero.GetNode<Hurtbox>("Hurtbox").TakeHit(bossStrike);
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);

            // ...and the hero's hit on that boss is never scaled x1.2.
            int bossHP = boss.CurrentHP;
            boss.GetNode<Hurtbox>("Hurtbox").TakeHit(new HitPayload {
                AttackerIndex = 0,
                TargetIndex = -1,
                AttackID = "joan.basic",
                HitboxID = "combo_1",
                AttackClass = AttackClass.Basic,
                Origin = HitOrigin.Basic,
                Delivery = HitDelivery.DirectHit,
                Damage = 10f,
                HitOrigin = boss.GlobalPosition + new Vector2(-40f, -30f),
                AttackerFacingRight = true,
                ContactId = HitClassification.NextContactId()
            });
            AssertThat(bossHP - boss.CurrentHP)
                .OverrideFailureMessage("Bosses have no Punish window.")
                .IsEqual(10);
        } finally {
            if (boss != null && GodotObject.IsInstanceValid(boss)) boss.Free();
            Teardown(host);
        }
    }

    // === Harness ============================================================

    private static async Task SwingThroughActiveWindowAsync(
        SceneTree tree, EnemyController enemy, EnemyAbilityData ability) {
        enemy.BeginAttack(ability);
        AssertThat(enemy.AbilityPhase).IsEqual(EnemyAbilityPhase.Active);
        for (int frame = 0; frame < 30 && enemy.AbilityPhase == EnemyAbilityPhase.Active; frame++) {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            enemy.TickAbilityExecutor(Step);
        }
        AssertThat(enemy.AbilityPhase).IsNotEqual(EnemyAbilityPhase.Active);
    }

    private static EnemyAbilityData Melee(string id, int active = 6) => new() {
        AbilityID = id,
        Archetype = EnemyAbilityArchetype.MeleeStrike,
        TelegraphFrames = 0,
        ActiveFrames = active,
        RecoveryFrames = 0,
        Damage = 5f,
        KnockbackForce = Vector2.Zero,
        HitstunDuration = 0f,
        // The legacy scalar melee's box: 40 x 40 at offset (30, -25).
        HitboxSize = new Vector2(40f, 40f),
        HitboxOffset = new Vector2(30f, -25f)
    };

    /// <summary>An enemy-sourced hit as the executor builds them, owned by <paramref name="source"/>.</summary>
    private static HitPayload EnemyHit(EnemyController source, PlayerController hero, float damage, int chargeCost = 0) => new() {
        AttackerIndex = -1,
        TargetIndex = 0,
        AttackID = "test.punish.enemy_strike",
        HitboxID = "primary",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        Knockback = Vector2.Zero,
        HitstunDuration = 0.2f,
        // In front of a right-facing hero.
        HitOrigin = hero.GlobalPosition + new Vector2(40f, -30f),
        AttackerFacingRight = false,
        BlockChargeCost = chargeCost,
        SourceActorId = source?.GetInstanceId() ?? 0UL,
        ContactId = HitClassification.NextContactId()
    };

    /// <summary>The hero's string hit, from behind the (right-facing) mob so no frontal cut applies.</summary>
    private static HitPayload StringHit(EnemyController target, int step, float damage) => new() {
        AttackerIndex = 0,
        TargetIndex = -1,
        AttackID = "joan.basic",
        HitboxID = $"combo_{step + 1}",
        AttackClass = AttackClass.Basic,
        Origin = HitOrigin.Basic,
        Delivery = HitDelivery.DirectHit,
        Damage = damage,
        HitstunDuration = BasicComboRules.HitstunFrames[step] / 60f,
        Launches = BasicComboRules.StringHitLaunches[step],
        HitOrigin = target.GlobalPosition + new Vector2(-200f, -30f),
        AttackerFacingRight = true,
        ContactId = HitClassification.NextContactId()
    };

    private static int Dealt(EnemyController enemy, HitPayload hit) {
        int before = enemy.CurrentHP;
        enemy.GetNode<Hurtbox>("Hurtbox").TakeHit(hit);
        return before - enemy.CurrentHP;
    }

    private static void RaiseBlock(PlayerController hero, bool facingRight) {
        hero.IsFacingRight = facingRight;
        hero.GetNode<BlockSystem>("BlockSystem").StartBlock();
        hero.TransitionTo(CharacterState.Blocking);
        AssertThat(hero.CurrentState).IsEqual(CharacterState.Blocking);
    }

    private static Node Host(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static PlayerController SpawnHero(Node host, string characterID, Vector2 position) {
        PlayerController hero = CharacterFactory.CreateCharacter(
            characterID, 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
        hero.Position = position;
        host.AddChild(hero);
        hero.GlobalPosition = position;
        return hero;
    }

    private static void FreeHero(PlayerController hero) {
        InputManager.Instance?.ClearInputSource(hero.PlayerIndex);
        hero.GetParent()?.RemoveChild(hero);
        hero.Free();
    }

    private static EnemyController CreateEnemy(Node parent, string enemyID, Vector2 position) {
        EnemyData canonical = AuthoredResources.Load<EnemyData>($"res://resources/Enemies/{enemyID}.tres");
        var data = (EnemyData)canonical.Duplicate();
        // Never aggro on its own: every case decides when this mob swings.
        data.AggroRadius = 0f;
        PackedScene scene = ResourceLoader.Load<PackedScene>(EnemyFactory.ScenePathForTier(data.Tier));
        var enemy = scene.Instantiate<EnemyController>();
        enemy.Data = data;
        enemy.Position = position;
        parent.AddChild(enemy);
        enemy.OnSpawn();
        enemy.GlobalPosition = position;
        return enemy;
    }

    /// <summary>
    /// A mob held where it stands for a real-physics case: its own tick is off
    /// (no chase, gravity or AI), its hurtbox and attack hitbox stay live, it
    /// faces right, and the hero is its target.
    /// </summary>
    private static EnemyController CreateHeldEnemy(Node parent, PlayerController hero, Vector2 position) {
        EnemyController enemy = CreateEnemy(parent, "chrono_slasher", position);
        enemy.SetPhysicsProcess(false);
        enemy.SetFacingForTest(true);
        enemy.SetChaseTargetForTest(hero);
        return enemy;
    }

    private static BossController CreateBoss() {
        var data = new BossData {
            BossID = "test_punish_boss",
            DisplayNameKey = "boss_borgia_inquisitor_name",
            MaxHP = 500,
            MeleeRangeThreshold = 3f,
            RangedRangeThreshold = 8f,
            RestCooldown = 1f,
            AttackPattern = BossAttackPattern.DistanceBased,
            PhaseThresholds = System.Array.Empty<float>(),
            BossAbilities = System.Array.Empty<EnemyAbilityData>()
        };
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(boss);
        boss.ApplyData(data);
        boss.GlobalPosition = Origin + new Vector2(2000f, 0f);
        return boss;
    }

    /// <summary>An Environment floor 2000 px wide whose top surface is at <paramref name="at"/>.Y.</summary>
    private static void BuildFloor(Node host, Vector2 at) {
        var floor = new StaticBody2D {
            Name = "PunishFloor",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = at
        };
        floor.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(2000f, 40f) },
            Position = new Vector2(0f, 20f)
        });
        host.AddChild(floor);
    }

    private static void BuildWall(Node host, float faceX, float centreY) {
        var wall = new StaticBody2D {
            Name = "PunishWall",
            CollisionLayer = CollisionLayers.Environment,
            CollisionMask = 0,
            Position = new Vector2(faceX, centreY)
        };
        wall.AddChild(new CollisionShape2D {
            Shape = new RectangleShape2D { Size = new Vector2(20f, 600f) },
            Position = new Vector2(10f, 0f)
        });
        host.AddChild(wall);
    }

    private static FTT.UI.FloatingDamageNumber SingleNumber(Node parent) {
        FTT.UI.FloatingDamageNumber found = null;
        int count = 0;
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using (children.AsDisposable()) {
            foreach (Node child in children) {
                if (child is FTT.UI.FloatingDamageNumber number && number.IsInsideTree()) {
                    found = number;
                    count++;
                }
            }
        }
        AssertThat(count).IsEqual(1);
        return found;
    }

    private static Color NumberColor(FTT.UI.FloatingDamageNumber number) {
        Godot.Collections.Array<Node> children = number.GetChildren();
        using (children.AsDisposable()) {
            foreach (Node child in children) {
                if (child is Label label) return label.GetThemeColor("font_color");
            }
        }
        AssertThat(false).OverrideFailureMessage("The damage number has no label.").IsTrue();
        return default;
    }

    private static void ReturnDamageNumbers(Node parent) {
        var numbers = new System.Collections.Generic.List<FTT.UI.FloatingDamageNumber>();
        Godot.Collections.Array<Node> children = parent.GetChildren();
        using (children.AsDisposable()) {
            foreach (Node child in children) {
                if (child is FTT.UI.FloatingDamageNumber number) numbers.Add(number);
            }
        }
        foreach (FTT.UI.FloatingDamageNumber number in numbers) number.ReturnToPool();
    }

    private static void Teardown(Node host) {
        InputManager.Instance?.ClearInputSource(0);
        if (!GodotObject.IsInstanceValid(host)) return;
        // Pooled numbers spawned under the host go back to their pool, never freed with it.
        ReturnDamageNumbers(host);
        host.GetParent()?.RemoveChild(host);
        host.Free();
    }
}
