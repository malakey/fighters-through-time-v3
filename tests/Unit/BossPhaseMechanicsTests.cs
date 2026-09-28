using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// Package 12 W9: the boss runtime behind M18, M19 and GAP-07 —
/// the Tribunal's two-body squad (shared bar, member-defeat phase trigger,
/// ability hand-off, one defeat payload, both bodies frozen together), the
/// Tragedy King's guarded soliloquy, the Jackal Priest's sand decoys, arena
/// guardians, and the Borgia Inquisitor's decorative after-images.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BossPhaseMechanicsTests {
    private const float Step = 1f / 60f;

    private static BossData Load(string bossID) =>
        AuthoredResources.Load<BossData>($"res://resources/Bosses/{bossID}.tres");

    // === M19: the Revolutionary Tribunal squad ===========================

    [TestCase]
    public void TheTribunalFieldsTwoBodiesOnEvenSharesOfOneBarAndCarriesItsDesignTableRow() {
        BossData data = Load("revolutionary_tribunal");
        AssertThat(data.PhaseTrigger).IsEqual(BossPhaseTrigger.MemberDefeat);
        AssertThat(data.SquadMemberCount).IsEqual(2);
        AssertThat(data.MemberMaxHP).IsEqual(425);
        AssertThat(data.MemberMaxHP * data.SquadMemberCount).IsEqual(data.MaxHP);
        // The design table's drift fixes (M19): not knockback immune, 1.0 s rest.
        AssertThat(data.IsKnockbackImmune).IsFalse();
        AssertFloat(data.RestCooldown).IsEqualApprox(1.0f, 0.0001f);

        Node host = CreateHost("TribunalBarHost");
        try {
            StoryHUD hud = StoryHUD.CreateDefault();
            host.AddChild(hud);
            BossEncounterController encounter = CreateEncounter(host, data, hud);
            AssertThat(encounter.Members.Count).IsEqual(2);
            int share = StoryDifficultyTuning.ScaleEnemyHP(data.MemberMaxHP, StoryDifficultyTuning.CurrentStoryDifficulty);
            foreach (BossController member in encounter.Members) {
                AssertThat(member.IsSquadMember).IsTrue();
                AssertThat(member.ScaledMaxHP).IsEqual(share);
            }
            AssertThat(encounter.Members[0].GlobalPosition.X < encounter.Members[1].GlobalPosition.X).IsTrue();

            // RevealDistance 0: the reveal fires at once (after the name-card beat
            // on a first viewing), and the bar holds the SUM of both bodies.
            encounter._Process(Step);
            if (encounter.IsIntroBeatRunning) encounter._Process(BossEncounterController.IntroBeatSeconds + 0.1f);
            var bar = hud.GetNodeOrNull<ProgressBar>("SafeArea/BossPanel/BossBar");
            AssertObject(bar).IsNotNull();
            AssertThat((int)bar.MaxValue).IsEqual(share * 2);
            AssertThat((int)bar.Value).IsEqual(share * 2);

            encounter.Members[1].TakeDamage(40);
            AssertThat((int)bar.Value).IsEqual(share * 2 - 40);
            AssertThat(encounter.CombinedCurrentHP).IsEqual(share * 2 - 40);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void HpNeverAdvancesTheSquadButTheFirstFallHandsItsKitToTheSurvivorAndEntersPhaseTwo() {
        BossData data = Load("revolutionary_tribunal");
        Node host = CreateHost("TribunalPhaseHost");
        try {
            BossEncounterController encounter = CreateEncounter(host, data, hud: null);
            BossController prosecutor = encounter.Members[0];
            BossController executioner = encounter.Members[1];
            var entered = new List<int>();
            encounter.PhaseEntered += entered.Add;

            // Each body fields its own abilities plus the shared ones.
            AssertThat(HasAbility(prosecutor, "boss.revolutionary_tribunal.verdict_bolt")).IsTrue();
            AssertThat(HasAbility(prosecutor, "boss.revolutionary_tribunal.guillotine_drop")).IsFalse();
            AssertThat(HasAbility(executioner, "boss.revolutionary_tribunal.guillotine_drop")).IsTrue();
            AssertThat(HasAbility(executioner, "boss.revolutionary_tribunal.verdict_bolt")).IsFalse();
            AssertThat(HasAbility(executioner, "boss.revolutionary_tribunal.mob_call")).IsTrue();

            // MemberDefeat ignores the authored 0.5 threshold entirely.
            prosecutor.TakeDamage(prosecutor.ScaledMaxHP - 5);
            AssertThat(prosecutor.CurrentPhase).IsEqual(0);
            AssertThat(executioner.CurrentPhase).IsEqual(0);
            AssertThat(entered.Count).IsEqual(0);

            prosecutor.TakeDamage(5);
            AssertThat(prosecutor.CurrentState).IsEqual(BossState.Dead);
            AssertThat(encounter.IsDefeated).IsFalse();
            AssertThat(encounter.FallenMemberCount).IsEqual(1);
            // The survivor absorbs the fallen's set and enters Phase 2 with the
            // ordinary transition window.
            AssertThat(executioner.CurrentPhase).IsEqual(1);
            AssertThat(executioner.CurrentState).IsEqual(BossState.PhaseTransitioning);
            AssertThat(HasAbility(executioner, "boss.revolutionary_tribunal.verdict_bolt")).IsTrue();
            AssertThat(HasAbility(executioner, "boss.revolutionary_tribunal.guillotine_drop")).IsTrue();
            AssertThat(executioner.AbsorbedSquadMembers.Contains(0)).IsTrue();
            AssertThat(encounter.CurrentPhase).IsEqual(1);
            AssertThat(entered.Count).IsEqual(1);
            AssertThat(entered[0]).IsEqual(1);
        } finally {
            host.Free();
        }
    }

    [TestCase]
    public void OnlyTheLastMemberFallingRaisesTheOneDefeatPayloadAndTheOneBossAward() {
        BossData data = Load("revolutionary_tribunal");
        Node host = CreateHost("TribunalDefeatHost");
        int payloads = 0;
        void OnDefeated(BossDefeatedPayload payload) {
            if (payload.BossID == data.BossID) payloads++;
        }
        EventBus.Instance.OnBossDefeated += OnDefeated;
        // No level ledger here: the award falls back to the authored 25, so the
        // count below measures the encounter, not another suite's claimed ledger.
        bool originalSuppress = LevelRewardDirectory.SuppressLedgerForTest;
        LevelRewardDirectory.SuppressLedgerForTest = true;
        try {
            BossEncounterController encounter = CreateEncounter(host, data, hud: null, awardDust: true);
            int encounterDefeats = 0;
            encounter.BossDefeated += _ => encounterDefeats++;

            encounter.Members[0].TakeDamage(9999);
            AssertThat(payloads).IsEqual(0);
            AssertThat(CountDustAwards(host)).IsEqual(0);

            // The survivor's transition window refuses damage; wait it out.
            BossController survivor = encounter.Members[1];
            AssertThat(Hit(survivor, 9999)).IsEqual(0);
            for (int frame = 0; frame < 200 && survivor.CurrentState == BossState.PhaseTransitioning; frame++) {
                survivor._PhysicsProcess(Step);
            }
            survivor.TakeDamage(9999);

            AssertThat(payloads).IsEqual(1);
            AssertThat(encounterDefeats).IsEqual(1);
            AssertThat(encounter.IsDefeated).IsTrue();
            AssertThat(CountDustAwards(host)).IsEqual(1);
        } finally {
            EventBus.Instance.OnBossDefeated -= OnDefeated;
            LevelRewardDirectory.SuppressLedgerForTest = originalSuppress;
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
        }
    }

    [TestCase]
    public void TheRewindFreezeAndTheTimeFreezeHoldBothMembers() {
        BossData data = Load("revolutionary_tribunal");
        Node host = CreateHost("TribunalFreezeHost");
        try {
            BossEncounterController encounter = CreateEncounter(host, data, hud: null);
            encounter.SetStoryRewindFrozen(true);
            foreach (BossController member in encounter.Members) AssertThat(member.IsStoryRewindFrozen).IsTrue();
            encounter.SetStoryRewindFrozen(false);
            foreach (BossController member in encounter.Members) AssertThat(member.IsStoryRewindFrozen).IsFalse();

            // Time Freeze / the Post-Landing Hold sweep the "Enemies" group; both
            // bodies are in it, so neither can keep acting while the other waits.
            foreach (BossController member in encounter.Members) {
                AssertThat(member.IsInGroup("Enemies")).IsTrue();
                ((IStoryTimeFreezable)member).SetTimeFrozen(true);
            }
            Vector2 before = encounter.Members[1].GlobalPosition;
            int hp = encounter.Members[1].CurrentHP;
            for (int frame = 0; frame < 30; frame++) encounter.Members[1]._PhysicsProcess(Step);
            AssertThat(encounter.Members[1].GlobalPosition).IsEqual(before);
            AssertThat(encounter.Members[1].CurrentHP).IsEqual(hp);
        } finally {
            host.Free();
        }
    }

    // === GAP-07: the Tragedy King's soliloquy ===========================

    [TestCase]
    public void TheTragedyKingIsInvulnerableMidSoliloquyUntilBothActorsBow() {
        BossData data = Load("tragedy_king");
        AssertThat(data.GuardedSummonMinPhase).IsEqual(1);
        Node host = CreateHost("TragedyKingHost");
        BossController king = CreateBoss(host, data);
        try {
            // Phase 1 summons are ordinary adds: no soliloquy.
            AssertThat(king.IsSoliloquy).IsFalse();

            king.TakeDamage(king.ScaledMaxHP - king.ScaledMaxHP / 2 + 1);
            AssertThat(king.CurrentPhase).IsEqual(1);
            // The phase entry opens a scene: two actors, then the King holds.
            king.BeginPhaseEntrySummonForTest();
            for (int frame = 0; frame < 120 && !king.IsSoliloquy; frame++) king.TickAbility(Step);
            AssertThat(king.IsSoliloquy).IsTrue();
            AssertThat(king.SceneActorCount).IsEqual(2);

            int hp = king.CurrentHP;
            // Leave the transition window so only the soliloquy can refuse damage.
            ForceOutOfTransition(king);
            AssertThat(Hit(king, 50)).IsEqual(0);
            AssertThat(king.CurrentHP).IsEqual(hp);
            AssertThat(king.IsMechanicInvulnerable).IsTrue();

            // Both actors taking their bow (here: falling) ends it.
            foreach (EnemyController actor in SceneActors(host)) actor.TakeDamage(9999);
            king.AdvanceGuardedSceneForTest(Step);
            AssertThat(king.IsSoliloquy).IsFalse();
            AssertThat(Hit(king, 50) > 0).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
        }
    }

    [TestCase]
    public void AnUnfinishedSceneEndsWhenItsTimeIsUpAndTheSurvivingActorsLeaveTheStage() {
        BossData data = Load("tragedy_king");
        Node host = CreateHost("TragedyKingBowHost");
        BossController king = CreateBoss(host, data);
        try {
            king.TakeDamage(king.ScaledMaxHP - king.ScaledMaxHP / 2 + 1);
            king.BeginPhaseEntrySummonForTest();
            for (int frame = 0; frame < 120 && !king.IsSoliloquy; frame++) king.TickAbility(Step);
            AssertThat(king.IsSoliloquy).IsTrue();
            List<EnemyController> actors = SceneActors(host);
            AssertThat(actors.Count).IsEqual(2);

            king.AdvanceGuardedSceneForTest(data.GuardedSummonSceneSeconds + 0.1f);
            AssertThat(king.IsSoliloquy).IsFalse();
            AssertThat(king.SceneActorCount).IsEqual(0);
            // The bow: survivors are taken off the stage, not left as loose adds.
            foreach (EnemyController actor in actors) {
                AssertThat(GodotObject.IsInstanceValid(actor) && actor.GetParent() == host).IsFalse();
            }
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.Free();
        }
    }

    // === GAP-07: the Jackal Priest's sand decoys ========================

    [TestCase]
    public async Task EachPhaseTwoTeleportLeavesASandDecoyWhoseStrikeBurstsOnThePlayer() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            BossData data = Load("jackal_priest");
            AssertThat(data.TeleportDecoyMinPhase).IsEqual(1);
            EnemyAbilityData step = FindAbility(data, "boss.jackal_priest.jackal_step");
            Node host = CreateHost("JackalDecoyHost");
            PlayerController player = null;
            try {
                BossController priest = CreateBoss(host, data);
                priest.GlobalPosition = new Vector2(0f, 0f);

                // Phase 1: a teleport is just a teleport.
                RunAbility(priest, step, new Vector2(400f, 0f));
                AssertObject(priest.LastDecoy).IsNull();

                // Phase 2: the vacated spot keeps a decoy.
                priest.CurrentPhase = 1;
                Vector2 vacated = priest.GlobalPosition;
                RunAbility(priest, step, new Vector2(vacated.X + 400f, 0f));
                BossDecoy decoy = priest.LastDecoy;
                AssertObject(decoy).IsNotNull();
                AssertThat(decoy.GlobalPosition.DistanceTo(vacated) < 0.5f).IsTrue();
                AssertThat(decoy.IsInGroup(BossDecoy.GroupName)).IsTrue();
                AssertThat(decoy.IsInGroup(TimeFreezeController.FreezableGroup)).IsTrue();
                AssertFloat(decoy.BurstRadius).IsEqualApprox(data.DecoyBurstRadius, 0.001f);

                // A freeze holds its lifetime in place.
                float remaining = decoy.RemainingSeconds;
                decoy.SetTimeFrozen(true);
                for (int frame = 0; frame < 30; frame++) decoy._PhysicsProcess(Step);
                AssertFloat(decoy.RemainingSeconds).IsEqualApprox(remaining, 0.0001f);
                decoy.SetTimeFrozen(false);

                // An enemy hitbox (index -1) never sets it off; the player's does.
                var hurtbox = decoy.GetNode<Hurtbox>("Hurtbox");
                hurtbox.TakeHit(new HitPayload { AttackerIndex = -1, Damage = 10f });
                AssertThat(decoy.IsSpent).IsFalse();

                player = CharacterFactory.CreateCharacter("einstein", 0);
                host.AddChild(player);
                player.GlobalPosition = decoy.GlobalPosition + new Vector2(60f, 0f);
                float playerHP = player.CurrentHP;
                float dealt = hurtbox.TakeHit(new HitPayload { AttackerIndex = 0, Damage = 10f });
                AssertThat(dealt).IsEqual(0f);
                AssertThat(decoy.IsSpent).IsTrue();
                AssertThat(decoy.LastBurstVictims).IsEqual(1);
                AssertThat(player.CurrentHP < playerHP).IsTrue();
            } finally {
                player?.Free();
                host.Free();
            }
        });
    }

    // === GAP-07: arena guardians (the Chronal Inventor's coils) ==========

    [TestCase]
    public void ABossRefusesDamageWhileAnyRegisteredGuardianStands() {
        BossData data = Load("chronal_inventor");
        AssertThat(data.ArenaGuardianMinPhase).IsEqual(1);
        Node host = CreateHost("GuardianHost");
        try {
            BossController inventor = CreateBoss(host, data);
            var west = new ArenaGuardian { Name = "West", MaxHP = data.ArenaGuardianHP };
            var east = new ArenaGuardian { Name = "East", MaxHP = data.ArenaGuardianHP };
            host.AddChild(west);
            host.AddChild(east);
            inventor.RegisterGuardian(west);
            inventor.RegisterGuardian(east);
            AssertThat(inventor.LiveGuardianCount).IsEqual(2);

            int hp = inventor.CurrentHP;
            AssertThat(Hit(inventor, 40)).IsEqual(0);
            AssertThat(inventor.CurrentHP).IsEqual(hp);

            // A guardian is struck like any enemy; only player hits count, and it
            // credits nothing back to the hit pipeline.
            var hurtbox = west.GetNode<Hurtbox>("Hurtbox");
            AssertThat(hurtbox.TakeHit(new HitPayload { AttackerIndex = -1, Damage = 999f })).IsEqual(0f);
            AssertThat(west.IsAlive).IsTrue();
            AssertThat(hurtbox.TakeHit(new HitPayload { AttackerIndex = 0, Damage = 999f })).IsEqual(0f);
            AssertThat(west.IsAlive).IsFalse();
            AssertThat(inventor.IsGuarded).IsTrue();
            AssertThat(Hit(inventor, 40)).IsEqual(0);

            east.ApplyDamage(data.ArenaGuardianHP);
            AssertThat(inventor.IsGuarded).IsFalse();
            AssertThat(Hit(inventor, 40) > 0).IsTrue();
        } finally {
            host.Free();
        }
    }

    // === M18: the Borgia after-image dash ================================

    [TestCase]
    public async Task TheAfterImageDashShedsGhostsUnlessReducedTemporalEffectsIsOn() {
        await OutOfBandPhysicsStep.RunInPhysicsFrameAsync(() => {
            BossData data = Load("borgia_inquisitor");
            EnemyAbilityData dash = FindAbility(data, "boss.borgia_inquisitor.after_image_dash");
            bool originalComfort = ComfortSettings.ReducedTemporalEffects;
            Node host = CreateHost("AfterImageHost");
            try {
                ComfortSettings.Apply(false);
                BossController borgia = CreateBoss(host, data);
                borgia.CurrentPhase = 1;
                RunDash(borgia, dash);
                AssertThat(borgia.AfterImagesSpawned > 0).IsTrue();
                AssertThat(host.GetTree().GetNodesInGroup(AfterImageGhost.GroupName).Count > 0).IsTrue();

                ComfortSettings.Apply(true);
                BossController calm = CreateBoss(host, data);
                calm.CurrentPhase = 1;
                RunDash(calm, dash);
                AssertThat(calm.AfterImagesSpawned).IsEqual(0);
            } finally {
                ComfortSettings.Apply(originalComfort);
                host.Free();
            }
        });
    }

    // === Helpers =========================================================

    private static Node CreateHost(string name) {
        var host = new Node2D { Name = name };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(host);
        return host;
    }

    private static BossEncounterController CreateEncounter(
        Node host, BossData data, StoryHUD hud, bool awardDust = false) {
        var encounter = new BossEncounterController {
            Name = "SquadEncounter",
            Data = data,
            RevealDistance = 0f,
            AwardDustOnDefeat = awardDust,
            HUD = hud
        };
        host.AddChild(encounter);
        return encounter;
    }

    private static BossController CreateBoss(Node host, BossData data) {
        PackedScene scene = ResourceLoader.Load<PackedScene>("res://scenes/enemies/Boss.tscn");
        var boss = scene.Instantiate<BossController>();
        boss.SelectionSeed = 2026;
        boss.Data = data;
        host.AddChild(boss);
        boss.ApplyData(data);
        return boss;
    }

    private static bool HasAbility(BossController boss, string abilityID) {
        foreach (EnemyAbilityData ability in boss.ActiveAbilities) {
            if (ability?.AbilityID == abilityID) return true;
        }
        return false;
    }

    private static EnemyAbilityData FindAbility(BossData data, string abilityID) {
        foreach (EnemyAbilityData ability in data.BossAbilities) {
            if (ability?.AbilityID == abilityID) return ability;
        }
        AssertObject(null).OverrideFailureMessage($"{abilityID} is not authored").IsNotNull();
        return null;
    }

    private static void RunAbility(BossController boss, EnemyAbilityData ability, Vector2 target) {
        boss.BeginAbility(ability, target);
        for (int frame = 0; frame < 240 && boss.AbilityPhase != EnemyAbilityPhase.Idle; frame++) {
            boss.TickAbility(Step);
        }
    }

    private static void RunDash(BossController boss, EnemyAbilityData dash) {
        boss.BeginAbility(dash, boss.GlobalPosition + new Vector2(400f, 0f));
        for (int frame = 0; frame < 240 && boss.AbilityPhase != EnemyAbilityPhase.Idle; frame++) {
            boss._PhysicsProcess(Step);
        }
    }

    private static List<EnemyController> SceneActors(Node host) {
        var actors = new List<EnemyController>();
        foreach (Node child in host.GetChildren()) {
            if (child is EnemyController enemy && enemy.CurrentState != EnemyState.Dead) actors.Add(enemy);
        }
        return actors;
    }

    private static int CountDustAwards(Node host) {
        int count = 0;
        foreach (Node child in host.GetChildren()) {
            if (child is ChronalDustPickup pickup && pickup.Visible) count++;
        }
        return count;
    }

    /// <summary>HP a direct hit actually removed (BossController.TakeDamage(int) returns void).</summary>
    private static int Hit(BossController boss, int damage) {
        int before = boss.CurrentHP;
        boss.TakeDamage(damage);
        return before - boss.CurrentHP;
    }

    private static void ForceOutOfTransition(BossController boss) {
        for (int frame = 0; frame < 300 && boss.CurrentState == BossState.PhaseTransitioning; frame++) {
            boss._PhysicsProcess(Step);
        }
    }
}
