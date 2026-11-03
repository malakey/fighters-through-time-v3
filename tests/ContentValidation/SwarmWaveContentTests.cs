using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.Diagnostics;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// GR-CROWD (rulings 1b, 1b+, 1b++; plan §1.11): the fodder mob and the rules a
/// level's swarm row obeys. GR-CROWD places no row, so the two sweeps have nothing
/// to read today; each rule is pinned on a bad fixture so the sweeps bite when the
/// level owners add their rows.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SwarmWaveContentTests {
    private const string FodderID = "chrono_swarmling";
    private const string EnemyFolder = "res://resources/Enemies";
    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private static bool IsFodder(string enemyID) {
        try {
            return EnemyFactory.LoadData(enemyID)?.DustWeightZero == true;
        } catch (InvalidOperationException) {
            return false;
        }
    }

    private static List<(string WaveID, string[] EnemyIDs)> SwarmRows(LevelRewardManifest manifest) {
        var rows = new List<(string, string[])>();
        foreach ((string waveID, string[] enemyIDs) in manifest.ParseWaves()) {
            if (enemyIDs.Any(IsFodder)) rows.Add((waveID, enemyIDs));
        }
        return rows;
    }

    private static string RowIdOf(string waveID) {
        int at = waveID.IndexOf('@');
        return at < 0 ? waveID : waveID[..at];
    }

    // === The mob ===========================================================

    [TestCase]
    public void TheFodderIsTheProvisionalStatBlockOnAStandardBody() {
        EnemyData data = EnemyFactory.LoadData(FodderID);
        AssertObject(data).IsNotNull();
        AssertThat(data.EnemyID).IsEqual(FodderID);
        AssertThat(data.Tier == EnemyTier.Standard).IsTrue();
        AssertThat(data.MaxHP).IsEqual(10);
        AssertThat(data.MoveSpeed).IsEqual(5.2f);
        AssertThat(data.Weight).IsEqual(0.6f);
        AssertThat(data.AttackDamage).IsEqual(5f);
        AssertThat(data.AttackCooldown).IsEqual(2.5f);
        AssertThat(data.AttackTelegraphFrames).IsEqual(12);
        AssertThat(data.AttackActiveFrames).IsEqual(6);
        AssertThat(data.AttackRecoveryFrames).IsEqual(14);
        // A scalar melee mob: no ability resource, and a melee-slot candidate.
        AssertObject(data.PrimaryAttack).IsNull();
        AssertThat(data.ChronalDustDrop).IsEqual(1);
        AssertThat(data.DustWeightZero).IsTrue();
    }

    [TestCase]
    public void TheFodderHasItsManifestRowAndDisplayName() {
        string manifest = FileAccess.GetFileAsString("res://resources/Content/content_manifest.csv");
        AssertThat(manifest.Contains(
                "Enemy,chrono_swarmling,6,res://resources/Enemies/chrono_swarmling.tres,EnemyFactory,Implemented,ReadyForReplacement,Valid,true"))
            .OverrideFailureMessage("content_manifest.csv has no chrono_swarmling row.").IsTrue();
        EnemyData data = EnemyFactory.LoadData(FodderID);
        AssertThat(data.DisplayName).IsEqual("Chrono Swarmling");
        AssertThat(data.DisplayNameKey).IsEqual("enemy_chrono_swarmling_name");
        AssertThat(FileAccess.GetFileAsString("res://localization/en.csv")
                .Contains("enemy_chrono_swarmling_name,Chrono Swarmling"))
            .OverrideFailureMessage("en.csv has no display name row.").IsTrue();
    }

    [TestCase]
    public void OnlyTheFodderCarriesDustWeightZero() {
        using DirAccess folder = DirAccess.Open(EnemyFolder);
        var carriers = new List<string>();
        foreach (string file in folder.GetFiles()) {
            if (!file.EndsWith(".tres")) continue;
            EnemyData data = AuthoredResources.Load<EnemyData>($"{EnemyFolder}/{file}");
            if (data.DustWeightZero) carriers.Add(data.EnemyID);
        }
        AssertThat(string.Join(",", carriers)).IsEqual(FodderID);
    }

    [TestCase]
    public void AFodderRowAddsNoDustAndLeavesEveryOtherAwardAlone() {
        foreach (Difficulty difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }) {
            LevelRewardLedger without = LevelRewardDirectory.Compile(Manifest(withSwarm: false), difficulty);
            LevelRewardLedger with = LevelRewardDirectory.Compile(Manifest(withSwarm: true), difficulty);
            AssertThat(with.RequiredEncounterTotal).IsEqual(without.RequiredEncounterTotal);
            foreach (KeyValuePair<string, int> award in without.Awards) {
                AssertThat(with.Award(award.Key))
                    .OverrideFailureMessage($"{difficulty}: {award.Key} moved from {award.Value}.")
                    .IsEqual(award.Value);
            }
            // The fodder's sources exist, so a kill finds its source and is paid 0.
            IReadOnlyList<string> queue = with.EnemyQueues[FodderID];
            AssertThat(queue.Count >= 5).IsTrue();
            foreach (string source in queue) AssertThat(with.Award(source)).IsEqual(0);
        }

        static LevelRewardManifest Manifest(bool withSwarm) {
            var waves = new List<string> { "test_level.room1:chrono_slasher,chrono_slasher,laser_archer" };
            if (withSwarm) waves.Add("test_level.swarm1:" + string.Join(",", Enumerable.Repeat(FodderID, 8)));
            return new LevelRewardManifest {
                LevelID = "test_level", CampaignLevelIndex = 2, RequiredEncounterPool = 15,
                BossAward = 0, OptionalPool = 0, RequiredWaves = waves.ToArray()
            };
        }
    }

    [TestCase(Timeout = 60000)]
    public async Task TheFodderIsGrabbableSmallerAndTintedAndAPoolCycleUndoesIt() {
        var host = new Node2D { Name = "SwarmLookHost" };
        Tree.Root.AddChild(host);
        try {
            EnemyController slasher = EnemyFactory.Spawn("chrono_slasher", host, new Vector2(0f, 0f));
            EnemyController swarmling = EnemyFactory.Spawn(FodderID, host, new Vector2(200f, 0f));
            await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(swarmling.IsGrabbable).IsTrue();
            AnimatedSprite2D slasherSprite = SpriteOf(slasher);
            AnimatedSprite2D swarmSprite = SpriteOf(swarmling);
            // The slasher's own sheet, smaller, in the fodder's tint.
            AssertThat(swarmSprite.SpriteFrames == slasherSprite.SpriteFrames).IsTrue();
            AssertThat(swarmling.Data.SpriteScaleFactor < 1f).IsTrue();
            AssertThat(Math.Abs(swarmSprite.Scale.Y - slasherSprite.Scale.Y * swarmling.Data.SpriteScaleFactor) < 0.001f)
                .OverrideFailureMessage($"Scale {swarmSprite.Scale.Y} against the slasher's {slasherSprite.Scale.Y}.")
                .IsTrue();
            AssertThat(swarmling.Data.TintAuthoredFrames).IsTrue();
            AssertThat(TintOf(swarmling) == swarmling.Data.PlaceholderTint).IsTrue();
            AssertThat(TintOf(slasher) == Colors.White).IsTrue();
            // The shared pool: the same body handed back as a slasher wears a slasher's look.
            swarmling.ApplyData(EnemyFactory.LoadData("chrono_slasher"));
            await Tree.ToSignal(Tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(Math.Abs(swarmSprite.Scale.Y - slasherSprite.Scale.Y) < 0.001f).IsTrue();
            AssertThat(TintOf(swarmling) == Colors.White).IsTrue();
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            host.GetParent()?.RemoveChild(host);
            host.Free();
        }

        static AnimatedSprite2D SpriteOf(EnemyController body) =>
            body.FindChildren("*", nameof(AnimatedSprite2D), true, false).OfType<AnimatedSprite2D>().First();
        static Color TintOf(EnemyController body) =>
            body.FindChildren("*", "", true, false).OfType<GlowPresentationController>().First().BaseTint;
    }

    // === The rules =========================================================

    [TestCase]
    public void ARowIsSizedByItsLockAndEasysPrefixGivesFiveToSeven() {
        AssertThat(SwarmWaveRules.RowMobsForLockWidth(1700f)).IsEqual(8);
        AssertThat(SwarmWaveRules.RowMobsForLockWidth(2799f)).IsEqual(8);
        AssertThat(SwarmWaveRules.RowMobsForLockWidth(2800f)).IsEqual(10);
        // Authored at the Hard truth: Hard and Normal spawn the table, Easy its prefix.
        AssertThat(EncounterLockRules.PlannedSpawnCount(8, -1, true, Difficulty.Easy)).IsEqual(5);
        AssertThat(EncounterLockRules.PlannedSpawnCount(10, -1, true, Difficulty.Easy)).IsEqual(7);
        AssertThat(EncounterLockRules.PlannedSpawnCount(8, -1, true, Difficulty.Normal)).IsEqual(8);
        AssertThat(EncounterLockRules.PlannedSpawnCount(10, -1, true, Difficulty.Hard)).IsEqual(10);
    }

    [TestCase]
    public void TheValidatorsRefuseEveryBadShape() {
        static bool Fodder(string id) => id == "fodder";
        static string[] Fodders(int n) => Enumerable.Repeat("fodder", n).ToArray();

        AssertThat(SwarmWaveRules.IsSwarmRow(Fodders(8), Fodder)).IsTrue();
        AssertThat(SwarmWaveRules.IsSwarmRow(new[] { "fodder", "slasher" }, Fodder)).IsFalse();
        AssertThat(SwarmWaveRules.IsSwarmRow(Array.Empty<string>(), Fodder)).IsFalse();

        AssertThat(SwarmWaveRules.ValidateRows(2, new[] { ("a", Fodders(8)) }, Fodder).Count).IsEqual(0);
        AssertThat(SwarmWaveRules.ValidateRows(9, new[] { ("a", Fodders(6)), ("b", Fodders(10)) }, Fodder).Count).IsEqual(0);
        // A row with no fodder is not the rulebook's business.
        AssertThat(SwarmWaveRules.ValidateRows(12, new[] { ("a", new[] { "slasher" }) }, Fodder).Count).IsEqual(0);
        foreach (int excluded in new[] { 0, 12, 13, 14 }) {
            AssertThat(SwarmWaveRules.ValidateRows(excluded, new[] { ("a", Fodders(8)) }, Fodder).Count > 0)
                .OverrideFailureMessage($"Level {excluded} takes no swarm row.").IsTrue();
        }
        AssertThat(SwarmWaveRules.ValidateRows(2, new[] { ("a", Fodders(8)), ("b", Fodders(8)), ("c", Fodders(8)) }, Fodder).Count > 0).IsTrue();
        AssertThat(SwarmWaveRules.ValidateRows(2, new[] { ("a", Fodders(5)) }, Fodder).Count > 0).IsTrue();
        AssertThat(SwarmWaveRules.ValidateRows(2, new[] { ("a", Fodders(11)) }, Fodder).Count > 0).IsTrue();
        AssertThat(SwarmWaveRules.ValidateRows(2, new[] { ("a", Fodders(7).Append("slasher").ToArray()) }, Fodder).Count > 0)
            .OverrideFailureMessage("A swarm is all fodder.").IsTrue();

        AssertThat(SwarmWaveRules.ValidateWaveTrigger(EncounterWaveTrigger.WhenAliveAtMost, SwarmWaveRules.TriggerAliveAtMost)).IsNull();
        AssertThat(SwarmWaveRules.ValidateWaveTrigger(EncounterWaveTrigger.WhenAliveAtMost, 3)).IsNotNull();
        AssertThat(SwarmWaveRules.ValidateWaveTrigger(EncounterWaveTrigger.OnEngage, 2)).IsNotNull();

        AssertThat(SwarmWaveRules.ValidateWarmUp(2, 16, 8)).IsNull();
        AssertThat(SwarmWaveRules.ValidateWarmUp(2, 10, 8)).IsNotNull();
        AssertThat(SwarmWaveRules.ValidateWarmUp(2, 16, 17)).IsNotNull();
    }

    [TestCase]
    public void EveryManifestObeysTheSwarmRules() {
        var issues = new List<string>();
        for (int level = 0; level <= 15; level++) {
            LevelRewardManifest manifest = AuthoredResources.Load<LevelRewardManifest>(LevelRewardManifest.PathFor(level));
            AssertObject(manifest).OverrideFailureMessage($"Level {level} has no manifest.").IsNotNull();
            issues.AddRange(SwarmWaveRules.ValidateRows(level, manifest.ParseWaves(), IsFodder));
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    [TestCase(Timeout = 300000)]
    public void EverySwarmLevelWarmsItsPoolAndTriggersItsWavesByTheRule() {
        var issues = new List<string>();
        for (int level = 0; level <= 15; level++) {
            LevelRewardManifest manifest = AuthoredResources.Load<LevelRewardManifest>(LevelRewardManifest.PathFor(level));
            List<(string WaveID, string[] EnemyIDs)> rows = SwarmRows(manifest);
            if (rows.Count == 0) continue;

            string configPath = level == 1
                ? "res://resources/Pools/florence_pool_config.tres"
                : $"res://resources/Pools/level_pool_configs/level_{level:00}_pool_config.tres";
            ScenePoolConfig config = AuthoredResources.Load<ScenePoolConfig>(configPath);
            int warm = config.PoolDefinitions.First(d => d.PoolID == "standard_enemy").WarmUpCount;
            string warmIssue = SwarmWaveRules.ValidateWarmUp(level, warm, rows.Max(r => r.EnemyIDs.Length));
            if (warmIssue != null) issues.Add(warmIssue);

            CheckLockWaves(level, rows, issues);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
    }

    private static void CheckLockWaves(int level, List<(string WaveID, string[] EnemyIDs)> rows, List<string> issues) {
        SessionData originalSession = GameManager.Instance.CurrentSession;
        bool originalPaused = Tree.Paused;
        Node scene = null;
        try {
            SessionData session = originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;
            scene = ResourceLoader.Load<PackedScene>(StoryManager.GetLevelScenePath((CampaignLevel)level)).Instantiate();
            scene.Name = $"Level{level:00}_SwarmContent";
            Tree.Root.AddChild(scene);
            var waves = new Dictionary<string, EncounterLockWave>(StringComparer.Ordinal);
            foreach (Node node in Tree.GetNodesInGroup(EncounterLock.Group)) {
                if (node is not EncounterLock encounterLock) continue;
                foreach (EncounterLockWave wave in encounterLock.Spec.Waves) waves[wave.RowID] = wave;
            }
            foreach ((string waveID, string[] _) in rows) {
                string rowID = RowIdOf(waveID);
                if (!waves.TryGetValue(rowID, out EncounterLockWave wave)) {
                    issues.Add($"Level {level}: swarm row '{rowID}' has no lock wave");
                    continue;
                }
                if (!EncounterLock.IsSwarmWave(wave)) issues.Add($"Level {level}: wave '{rowID}' is not read as a swarm");
                string trigger = SwarmWaveRules.ValidateWaveTrigger(wave.Trigger, wave.AliveAtMost);
                if (trigger != null) issues.Add($"Level {level}: wave '{rowID}': {trigger}");
            }
        } finally {
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (scene != null && GodotObject.IsInstanceValid(scene)) {
                scene.GetParent()?.RemoveChild(scene);
                scene.Free();
            }
            GameManager.Instance.CurrentSession = originalSession;
            Tree.Paused = originalPaused;
        }
    }

    // === The option ========================================================

    [TestCase(Timeout = 120000)]
    public async Task TheSwarmOptionDropsOnlyTheSwarmWaveAndTheLockStillClears() {
        // The runner's argument, the bad value it exits 2 on, and the default.
        AssertThat(PlaytestOptions.Parse(new[] { "--swarm=off" }).SwarmWaves).IsFalse();
        AssertThat(PlaytestOptions.Parse(new[] { "--swarm=on" }).SwarmWaves).IsTrue();
        AssertThat(PlaytestOptions.Parse(Array.Empty<string>()).SwarmWaves).IsTrue();
        AssertThat(PlaytestOptions.Parse(new[] { "--slot-table=legacy" }).LegacySlotTable).IsTrue();
        AssertThat(PlaytestOptions.Parse(new[] { "--slot-table=current" }).LegacySlotTable).IsFalse();
        AssertThrown(() => PlaytestOptions.Parse(new[] { "--swarm=maybe" })).IsInstanceOf<ArgumentException>();
        AssertThrown(() => PlaytestOptions.Parse(new[] { "--slot-table=old" })).IsInstanceOf<ArgumentException>();

        SessionData originalSession = GameManager.Instance.CurrentSession;
        var host = new Node2D { Name = "SwarmOptionHost" };
        Tree.Root.AddChild(host);
        try {
            SessionData session = originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "joan";
            session.Difficulty = Difficulty.Hard;
            GameManager.Instance.CurrentSession = session;
            StoryEngagementDirector.ResetForTest();
            var floor = new StaticBody2D {
                Position = new Vector2(6000f, 920f), CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) } });
            host.AddChild(floor);
            PlayerController hero = CharacterFactory.CreateCharacter("joan", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            host.AddChild(hero);
            hero.GlobalPosition = new Vector2(4500f, 850f);

            // On: the swarm spawns its whole table once the first wave is down.
            EncounterLock on = BuildLock(host, hero, "swarm_on");
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(on.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(on.PlannedTotal).IsEqual(7);
            foreach (EnemyController member in on.LivingMembers) member.TakeDamage(99999);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(on.LivingMemberCount).IsEqual(6);
            AssertThat(on.LivingMembers.All(m => m.Data.EnemyID == FodderID)).IsTrue();
            foreach (EnemyController member in on.LivingMembers) member.TakeDamage(99999);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(on.State).IsEqual(EncounterLockState.Cleared);

            // Off: the row is gone; the first wave stands and clears the lock alone.
            EncounterLock.SwarmWavesOffForDiagnostics = true;
            EncounterLock off = BuildLock(host, hero, "swarm_off");
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(off.State).IsEqual(EncounterLockState.Engaged);
            AssertThat(off.PlannedTotal).IsEqual(1);
            AssertThat(off.AllWavesSpawned).IsTrue();
            foreach (EnemyController member in off.LivingMembers) member.TakeDamage(99999);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            AssertThat(off.LivingMemberCount).IsEqual(0);
            AssertThat(off.State).IsEqual(EncounterLockState.Cleared);
        } finally {
            EncounterLock.SwarmWavesOffForDiagnostics = false;
            StoryEngagementDirector.ResetForTest();
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            host.GetParent()?.RemoveChild(host);
            host.Free();
            GameManager.Instance.CurrentSession = originalSession;
        }

        static EncounterLock BuildLock(Node host, PlayerController hero, string id) {
            var swarm = new EncounterLockSpawn[6];
            for (int i = 0; i < swarm.Length; i++) swarm[i] = new EncounterLockSpawn(FodderID, new Vector2(4800f + i * 60f, 850f));
            EncounterLock encounterLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = id,
                Area = EncounterLockSpec.Span(4000f, 7000f, 1080f),
                LevelBounds = new Rect2(1000f, 0f, 10000f, 1080f),
                BarrierFloorY = 900f,
                Waves = new[] {
                    new EncounterLockWave {
                        RowID = "swarm_test.room", Trigger = EncounterWaveTrigger.OnEngage,
                        Spawns = new[] { new EncounterLockSpawn("chrono_slasher", new Vector2(4700f, 850f)) }
                    },
                    new EncounterLockWave {
                        RowID = "swarm_test.swarm1", Spawns = swarm,
                        Trigger = EncounterWaveTrigger.WhenAliveAtMost, AliveAtMost = 0
                    }
                }
            });
            host.AddChild(encounterLock);
            encounterLock.Hero = hero;
            encounterLock.Arm();
            return encounterLock;
        }
    }
}
