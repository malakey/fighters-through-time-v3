using System.Collections.Generic;
using System.Threading.Tasks;
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
/// Ruling 7d, plan section 1.13: what a hazard impact pays. A kill pays ledger dust as any kill and
/// survives a rewind; an impact credits nobody and adds no stun or hitstop.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class HazardMobCreditTests {
    private const ulong Source = 33UL;
    private static readonly Vector2 Spot = new(HazardMobTestKit.OriginX, 0f);

    [TestCase]
    public async Task AHazardKillPaysOneLedgerSourceAndCountsAsADeath() {
        StoryManager story = StoryManager.Instance;
        CampaignLevel originalLevel = story.CurrentLevel;
        SessionData originalSession = GameManager.Instance.CurrentSession;
        var host = new Node2D { Name = "HazardDustHost" };
        HazardMobTestKit.Tree.Root.AddChild(host);
        StaticBody2D floor = HazardMobTestKit.CreateFloor();
        try {
            story.PrepareDirectLevel(CampaignLevel.Orleans, "einstein", Difficulty.Normal);
            LevelRewardDirectory.ResetAttempt();
            LevelRewardLedger ledger = LevelRewardDirectory.EnsureCompiled();
            AssertThat(ledger.EnemyQueues.TryGetValue(HazardMobTestKit.Slasher, out IReadOnlyList<string> queue)).IsTrue();
            host.AddChild(new StoryDropSystem { Name = "StoryDropSystem", RandomSeed = 777UL });

            EnemyController mob = EnemyFactory.Spawn(HazardMobTestKit.Slasher, host, Spot);
            AssertObject(mob).IsNotNull();
            int deaths = 0;
            mob.Died += _ => deaths++;
            await HazardMobTestKit.Frames(2);
            // A slasher over 30 HP on Normal: two sources finish it.
            HazardMobTestKit.Throw(mob);
            mob.ApplyHazardImpact(Source);
            mob.ApplyHazardImpact(Source + 1);
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(mob.KilledByHazard).IsTrue();
            AssertThat(deaths).IsEqual(1);
            await HazardMobTestKit.Frames(2);

            List<ChronalDustPickup> pickups = VisiblePickups(host);
            AssertThat(pickups.Count)
                .OverrideFailureMessage("A hazard kill pays through the ledger as any kill does.")
                .IsEqual(1);
            AssertThat(pickups[0].SourceID).IsEqual(queue[0]);
            AssertThat(pickups[0].DustAmount).IsEqual(ledger.Award(queue[0]));
        } finally {
            PoolManager.Instance?.ReleaseActiveUnder(host);
            host.GetParent()?.RemoveChild(host);
            host.Free();
            floor.Free();
            story.PrepareDirectLevel(originalLevel, "einstein", originalSession.Difficulty);
            GameManager.Instance.CurrentSession = originalSession;
            story.ClearLevelAttemptState();
            LevelRewardDirectory.ResetAttempt();
        }
    }

    [TestCase]
    public async Task AHazardKillSurvivesARewindAndThePoolCycleClearsItsLatches() {
        StaticBody2D floor = HazardMobTestKit.CreateFloor();
        EnemyController mob = HazardMobTestKit.CreateMob(HazardMobTestKit.Slasher, Spot, maxHP: 20);
        try {
            await HazardMobTestKit.Frames(2);
            HazardMobTestKit.Throw(mob);
            mob.ApplyHazardImpact(Source);
            AssertThat(mob.CurrentState).IsEqual(EnemyState.Dead);
            AssertThat(mob.KilledByHazard).IsTrue();

            mob.ApplyStoryRewind();
            AssertThat(mob.CurrentState)
                .OverrideFailureMessage("A hazard kill stays a kill through a rewind, as any kill does.")
                .IsEqual(EnemyState.Dead);
            AssertThat(mob.ApplyHazardImpact(Source + 1)).IsEqual(0);

            mob.OnDespawn();
            mob.OnSpawn();
            AssertThat(mob.KilledByHazard).IsFalse();
            AssertThat(mob.IsHazardVulnerable).IsFalse();
            HazardMobTestKit.Throw(mob);
            AssertThat(mob.ApplyHazardImpact(Source) > 0)
                .OverrideFailureMessage("A pool cycle must clear the sources a launch spent.")
                .IsTrue();
        } finally {
            mob.Free();
            floor.Free();
        }
    }

    [TestCase]
    public async Task AnImpactCreditsNobodyAndAddsNoStunOrHitstop() {
        StaticBody2D floor = HazardMobTestKit.CreateFloor();
        EnemyController mob = HazardMobTestKit.CreateMob(HazardMobTestKit.Slasher, Spot);
        EnemyController twin = HazardMobTestKit.CreateMob(HazardMobTestKit.Slasher, Spot + new Vector2(400f, 0f));
        PlayerController hero = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false);
        HazardMobTestKit.Tree.Root.AddChild(hero);
        hero.GlobalPosition = Spot - new Vector2(600f, 0f);
        int confirms = 0;
        void OnConfirm(HitConfirmPayload payload) => confirms++;
        EventBus.Instance.OnHitConfirm += OnConfirm;
        try {
            await HazardMobTestKit.Frames(3);
            hero.RestoreMomentumPips(1f);
            float meter = hero.CurrentUltimateMeter;
            float pips = hero.MomentumPips;
            HazardMobTestKit.Throw(mob);
            HazardMobTestKit.Throw(twin);
            AssertThat(mob.ApplyHazardImpact(Source)).IsEqual(HazardMobRules.StandardImpactDamage);
            AssertThat(mob.HitstopFramesRemaining).IsEqual(0);
            AssertThat(mob.IsThrownFlight)
                .OverrideFailureMessage("The impact does not end the flight.")
                .IsTrue();
            AssertThat(hero.CurrentUltimateMeter).IsEqual(meter);
            AssertThat(hero.MomentumPips).IsEqual(pips);
            AssertThat(hero.EchoPool).IsEqual(0f);
            AssertThat(confirms).IsEqual(0);

            // The impacted mob's throw stun runs out on the same frame as its untouched twin's.
            for (int frame = 0; frame < 150 && twin.CurrentState == EnemyState.Stunned; frame++) {
                AssertThat(mob.CurrentState).IsEqual(EnemyState.Stunned);
                await HazardMobTestKit.Frames(1);
            }
            AssertThat(twin.CurrentState).IsNotEqual(EnemyState.Stunned);
            AssertThat(mob.CurrentState)
                .OverrideFailureMessage("An impact adds no stun.")
                .IsNotEqual(EnemyState.Stunned);
        } finally {
            EventBus.Instance.OnHitConfirm -= OnConfirm;
            hero.Free();
            twin.Free();
            mob.Free();
            floor.Free();
        }
    }

    private static List<ChronalDustPickup> VisiblePickups(Node root) {
        var found = new List<ChronalDustPickup>();
        Godot.Collections.Array<Node> children = root.GetChildren();
        using var lifetime = children.AsDisposable();
        foreach (Node child in children) {
            if (child is ChronalDustPickup pickup && pickup.Visible) found.Add(pickup);
        }
        return found;
    }
}
