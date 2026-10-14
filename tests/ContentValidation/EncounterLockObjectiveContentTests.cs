using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using FTT.UI;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.ContentValidation;

/// <summary>
/// FIX-OBJECTIVE (2026-10-13): no encounter lock is ever held by an objective
/// the hero already met. The Phase B review found it four times — Florence's
/// gears, Chicago's beam, Hakata Bay's signal lines and Gettysburg's array Alpha:
/// a death rewind restores puzzles and breakables to the last checkpoint
/// snapshot, the moved anchors made that snapshot predate the solve, and the
/// lock read the objective live.
///
/// <list type="bullet">
/// <item>The content rule, over every lock in every campaign scene: a required
///       puzzle or breakable, once met, stays met through the death rewind's
///       response, and the lock latches it, so even an objective undone some
///       other way cannot hold the lock (it clears once its members are down).</item>
/// <item>One real-frames case per reviewed level: meet the objective inside the
///       engaged lock, kill the hero there, let the death rewind land inside,
///       kill the members — the lock clears and the world still shows the
///       objective met (the gears aligned, the coils aimed, the line cut, the
///       array broken), with the right objective posted.</item>
/// </list>
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterLockObjectiveContentTests {

    private const int ScratchSlot = 2;
    private const double Step = 1.0 / 60.0;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    // === The content rule, every campaign scene ===============================

    [TestCase(Timeout = 300000)]
    public void EveryLockRequiredObjectiveStaysMetOnceItsLockHasSeenItMet() {
        var issues = new List<string>();
        int checkedObjectives = 0;
        for (int index = 0; index <= 15; index++) {
            checkedObjectives += CheckScene(index, issues);
        }
        if (issues.Count > 0) AssertThat(string.Join(" | ", issues)).IsEqual("");
        AssertThat(checkedObjectives)
            .OverrideFailureMessage("No lock requires a puzzle or a breakable: the sweep is vacuous.")
            .IsGreater(0);
    }

    /// <summary>
    /// Builds one campaign scene (no save slot, Normal, Einstein) and, for every
    /// lock that requires something: meets each objective, lets the Armed lock
    /// read it once, answers a death rewind (each objective's own rewind
    /// response), undoes each objective outright (a reset, the old restore), and
    /// then drives the lock — hero inside, members handed back to the pool —
    /// until it clears. Synchronous: no physics frame runs.
    /// </summary>
    private static int CheckScene(int index, List<string> issues) {
        string label = $"Level{index:00}";
        int checkedObjectives = 0;
        bool originalPaused = Tree.Paused;
        SessionData originalSession = GameManager.Instance.CurrentSession;
        Node level = null;
        try {
            SessionData session = originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;

            string scenePath = StoryManager.GetLevelScenePath((CampaignLevel)index);
            PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath);
            if (scene == null) { issues.Add($"{label}: scene '{scenePath}' did not load"); return 0; }
            level = scene.Instantiate();
            level.Name = $"{label}_ObjectiveLatchContent";
            Tree.Root.AddChild(level);
            Tree.Paused = false;

            foreach (EncounterLock encounterLock in FindAll<EncounterLock>(level)) {
                string[] puzzleIDs = encounterLock.Spec?.RequiredPuzzleIDs ?? Array.Empty<string>();
                DamageableEnvironmentObject[] breakables = encounterLock.Spec?.RequiredBreakables
                    ?? Array.Empty<DamageableEnvironmentObject>();
                if (puzzleIDs.Length == 0 && breakables.Length == 0) continue;
                checkedObjectives += puzzleIDs.Length + breakables.Length;
                CheckLock($"{label} '{encounterLock.LockID}'", level, encounterLock, puzzleIDs, breakables, issues);
            }
        } catch (Exception exception) {
            issues.Add($"{label}: the check threw {exception.GetType().Name}: {exception.Message}");
        } finally {
            StoryEngagementDirector.ResetForTest();
            if (level != null && GodotObject.IsInstanceValid(level)) {
                PoolManager.Instance?.ReleaseActiveUnder(level);
                level.GetParent()?.RemoveChild(level);
                level.Free();
            }
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            GameManager.Instance.CurrentSession = originalSession;
            Tree.Paused = originalPaused;
        }
        return checkedObjectives;
    }

    private static void CheckLock(string label, Node level, EncounterLock encounterLock, string[] puzzleIDs,
        DamageableEnvironmentObject[] breakables, List<string> issues) {
        var puzzles = new List<PuzzleManager>();
        foreach (string puzzleID in puzzleIDs) {
            PuzzleManager puzzle = FindPuzzle(level, puzzleID);
            if (puzzle == null) { issues.Add($"{label} requires '{puzzleID}', but no live PuzzleManager carries it"); continue; }
            foreach (string conditionID in puzzle.RequiredConditionIDs) puzzle.SetCondition(conditionID, true);
            if (!puzzle.IsCompleted) puzzle.TryComplete("content_check");
            if (!puzzle.IsSolvedOrPersisted) { issues.Add($"{label}: '{puzzleID}' cannot be solved"); continue; }
            puzzles.Add(puzzle);
        }
        var broken = new List<DamageableEnvironmentObject>();
        foreach (DamageableEnvironmentObject breakable in breakables) {
            if (breakable == null || !GodotObject.IsInstanceValid(breakable)) { issues.Add($"{label} requires a freed breakable"); continue; }
            for (int guard = 0; guard < 8 && !breakable.IsDestroyed; guard++) breakable.TakeEnvironmentDamage(breakable.MaxHP);
            if (!breakable.IsDestroyed) { issues.Add($"{label}: '{breakable.ObjectID}' cannot be broken"); continue; }
            broken.Add(breakable);
        }

        // The Armed lock reads them once (the hero is still at its spawn).
        encounterLock.Tick(Step);

        // A death rewind: each objective's own response must keep it met.
        foreach (PuzzleManager puzzle in puzzles) {
            puzzle.ApplyStoryRewind();
            if (!puzzle.IsSolvedOrPersisted) issues.Add($"{label}: a death rewind un-solves '{puzzle.PuzzleID}' after the lock saw it solved");
        }
        foreach (DamageableEnvironmentObject breakable in broken) {
            breakable.ApplyStoryRewind();
            if (!breakable.IsDestroyed) issues.Add($"{label}: a death rewind rebuilds '{breakable.ObjectID}' after the lock saw it broken");
        }

        // Undone some other way, it must still not hold the lock (the latch).
        foreach (PuzzleManager puzzle in puzzles) puzzle.ResetPuzzle("content_check");
        foreach (DamageableEnvironmentObject breakable in broken) {
            breakable.RewindPolicy = StoryRewindPolicy.RestoreCheckpointState;
            breakable.ApplyStoryRewind();
        }
        if (!encounterLock.RequiredObjectivesMet) {
            issues.Add($"{label} did not latch its objectives: undoing them would hold it again");
        }

        // The hero walks in and the members go down: the lock clears.
        PlayerController hero = encounterLock.Hero ?? Tree.GetFirstNodeInGroup("StoryPlayer") as PlayerController;
        if (hero == null) { issues.Add($"{label}: no hero to engage it"); return; }
        hero.GlobalPosition = new Vector2(encounterLock.Area.GetCenter().X, encounterLock.Area.End.Y - 200f);
        hero.Velocity = Vector2.Zero;
        Tree.Paused = false;
        for (int round = 0; round < 40 && encounterLock.State != EncounterLockState.Cleared; round++) {
            encounterLock.Tick(Step);
            foreach (EnemyController member in encounterLock.LivingMembers) {
                if (PoolManager.Instance != null) PoolManager.Instance.Release(member);
                else member.TakeDamage(99999);
            }
        }
        if (encounterLock.State != EncounterLockState.Cleared) {
            issues.Add($"{label} is {encounterLock.State} with its objectives met and every member gone "
                + $"(engaged {encounterLock.EngageCount}x, waves all spawned: {encounterLock.AllWavesSpawned})");
        }
    }

    // === Real frames, one case per reviewed level ============================

    /// <summary>
    /// Review #0 (Florence): the gears aligned inside the engaged print shop, the
    /// hero killed there with a guard alive, the rewind landed inside — the gears
    /// stay aligned, the puzzle solved, the "clear" objective posted, and the lock
    /// clears once the guards are down.
    /// </summary>
    [TestCase(Timeout = 120000)]
    public async Task FlorencesPrintShopKeepsItsGearsThroughADeathRewindAndClears() {
        using var fixture = new LevelFixture(StoryManager.GetLevelScenePath(CampaignLevel.Florence), saveSlot: -1, resumeCheckpointID: null);
        var level = (Level01Controller)fixture.Level;
        PlayerController hero = level.GetNode<PlayerController>("Player");
        await fixture.SettleAsync();
        level.StreetsLock.RestoreCleared();
        level.PiazzaLock.RestoreCleared();

        EncounterLock printShop = level.PrintShopLock;
        hero.GlobalPosition = new Vector2(6200f, 850f);
        hero.Velocity = Vector2.Zero;
        await Frames(4);
        AssertThat(printShop.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(printShop.LivingMemberCount).IsGreater(0);

        var puzzle = level.GetNode<PuzzleManager>("GearPuzzle");
        var gearA = level.GetNode<RotatingGear>("GearA");
        var gearB = level.GetNode<RotatingGear>("GearB");
        gearA.SetQuarterTurns(gearA.TargetQuarterTurns);
        gearB.SetQuarterTurns(gearB.TargetQuarterTurns);
        AssertThat(puzzle.IsCompleted).IsTrue();
        await Frames(2);
        AssertObject(level.GetNodeOrNull<Node>("WorkshopDoor")).IsNull();
        AssertString(ObjectiveText(level)).IsEqual(TranslationServer.Translate("encounter_lock_objective_clear").ToString());

        await fixture.DieInsideAndLandAsync(hero, printShop);
        AssertThat(gearA.IsAligned && gearB.IsAligned)
            .OverrideFailureMessage("The rewind spun the solved gears back.").IsTrue();
        AssertThat(puzzle.IsCompleted).OverrideFailureMessage("The rewind un-solved the gear puzzle.").IsTrue();
        AssertThat(printShop.State).IsEqual(EncounterLockState.Engaged);
        AssertString(ObjectiveText(level)).IsEqual(TranslationServer.Translate("encounter_lock_objective_clear").ToString());

        await KillEveryMemberAsync(printShop);
        AssertThat(printShop.State)
            .OverrideFailureMessage("The print shop must clear once its guards are down: the gears were aligned.")
            .IsEqual(EncounterLockState.Cleared);
        AssertThat(printShop.ExitBarrier.IsClosed).IsFalse();
        AssertString(ObjectiveText(level)).IsEqual(TranslationServer.Translate("florence_objective_reach_boss").ToString());
    }

    /// <summary>
    /// Review #1 (Chicago): the beam routed inside the engaged routing hall, the
    /// hero killed there, the rewind landed inside — the coils stay aimed, crowd
    /// tap A stays dark, the puzzle solved, and the hall clears once its guards
    /// are down, handing the objective on to the Court.
    /// </summary>
    [TestCase(Timeout = 120000)]
    public async Task ChicagosRoutingHallKeepsItsBeamThroughADeathRewindAndClears() {
        using var fixture = new LevelFixture(StoryManager.GetLevelScenePath(CampaignLevel.Chicago), ScratchSlot,
            Level03Controller.CheckpointEntry, CampaignLevel.Chicago);
        var level = (Level03Controller)fixture.Level;
        await fixture.SettleAsync();
        level.MidwayLock.RestoreCleared();
        level.ElectricityLock.RestoreCleared();

        EncounterLock routing = level.RoutingHallLock;
        level.Player.GlobalPosition = new Vector2(6700f, Level03Controller.GroundY - 50f);
        level.Player.Velocity = Vector2.Zero;
        await Frames(4);
        AssertThat(routing.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(routing.LivingMemberCount).IsGreater(0);

        AssertThat(level.SolveBeamPuzzleForTest()).IsTrue();
        await Frames(2);
        AssertThat(level.BeamPuzzle.IsCompleted).IsTrue();
        AssertThat(level.CourtDoorOpen).IsTrue();
        var coilA = level.GetNode<ConductiveCoil>("BeamRoutingPuzzle/CoilA");
        var coilB = level.GetNode<ConductiveCoil>("BeamRoutingPuzzle/CoilB");
        var coilC = level.GetNode<ConductiveCoil>("BeamRoutingPuzzle/CoilC");
        var tapA = level.GetNode<PowerRoutingNode>("BeamRoutingPuzzle/CrowdTapA");
        AssertThat(tapA.IsPowered).IsFalse();

        await fixture.DieInsideAndLandAsync(level.Player, routing);
        AssertThat(coilA.QuarterTurns == 3 && coilB.QuarterTurns == 0 && coilC.QuarterTurns == 1)
            .OverrideFailureMessage("The rewind re-aimed the solved coils.").IsTrue();
        AssertThat(tapA.IsPowered).OverrideFailureMessage("The rewind re-armed crowd tap A.").IsFalse();
        AssertThat(level.BeamPuzzle.IsCompleted).OverrideFailureMessage("The rewind un-solved the beam puzzle.").IsTrue();
        AssertThat(routing.State).IsEqual(EncounterLockState.Engaged);
        AssertString(ObjectiveText(level)).IsEqual(TranslationServer.Translate("encounter_lock_objective_clear").ToString());

        await KillEveryMemberAsync(routing);
        AssertThat(routing.State)
            .OverrideFailureMessage("The routing hall must clear once its guards are down: the beam was routed.")
            .IsEqual(EncounterLockState.Cleared);
        AssertString(ObjectiveText(level)).IsEqual(TranslationServer.Translate("chicago_objective_reach_boss").ToString());
    }

    /// <summary>
    /// Review #6 (Hakata Bay): one signal line cut inside the engaged Lantern
    /// Watch, the hero killed there, the rewind landed inside — the line stays cut
    /// and counted; the other two cut afterwards complete the puzzle (re-cutting a
    /// re-built relay used to leave its condition unset for good), and the watch
    /// clears once its members are down.
    /// </summary>
    [TestCase(Timeout = 120000)]
    public async Task TheLanternWatchKeepsItsCutLinesThroughADeathRewindAndClears() {
        using var fixture = new LevelFixture(StoryManager.GetLevelScenePath(CampaignLevel.Berlin), ScratchSlot,
            "level_09_berlin_checkpoint_0", CampaignLevel.Berlin);
        var level = (Level09Controller)fixture.Level;
        await fixture.SettleAsync();
        level.SeawallLock.RestoreCleared();

        EncounterLock watch = level.LanternWatchLock;
        level.Player.GlobalPosition = new Vector2(4120f, 1110f);
        level.Player.Velocity = Vector2.Zero;
        await Frames(4);
        AssertThat(watch.State).IsEqual(EncounterLockState.Engaged);

        DestructibleBlock west = level.FeedRelays[0];
        for (int guard = 0; guard < 8 && !west.IsDestroyed; guard++) west.TakeEnvironmentDamage(west.MaxHP);
        AssertThat(level.FeedsCut).IsEqual(1);
        string westCondition = Level09Controller.FeedLinks[0].ConditionID;
        AssertThat(level.FeedPuzzle.IsConditionSatisfied(westCondition)).IsTrue();

        await fixture.DieInsideAndLandAsync(level.Player, watch);
        AssertThat(west.IsDestroyed).OverrideFailureMessage("The rewind re-built a cut relay.").IsTrue();
        AssertThat(level.FeedPuzzle.IsConditionSatisfied(westCondition))
            .OverrideFailureMessage("The rewind un-cut a signal line under its dark lantern.").IsTrue();
        AssertThat(level.FeedsCut).IsEqual(1);
        AssertThat(watch.State).IsEqual(EncounterLockState.Engaged);

        foreach (DestructibleBlock relay in level.FeedRelays) {
            for (int guard = 0; guard < 8 && !relay.IsDestroyed; guard++) relay.TakeEnvironmentDamage(relay.MaxHP);
        }
        AssertThat(level.FeedsCut).IsEqual(Level09Controller.FeedLinks.Length);
        AssertThat(level.FeedPuzzle.IsCompleted).IsTrue();
        AssertThat(level.RadarGateOpen).IsTrue();

        await KillEveryMemberAsync(watch);
        AssertThat(watch.State)
            .OverrideFailureMessage("The Lantern Watch must clear once every line is cut and its watch is down.")
            .IsEqual(EncounterLockState.Cleared);
    }

    /// <summary>
    /// Reviews #5 and #13 (Gettysburg), on Hard, where the Middle anchor is inert
    /// in Act II so the last checkpoint snapshot is always the entry's, taken
    /// with array Alpha at full HP (the same holds on Normal when the Middle is
    /// never struck): Alpha falls in the Wheatfield, the hero engages the Angle and
    /// dies there; the rewind lands inside and Alpha, behind the Angle's closed
    /// entry, stays broken, so the Angle clears once Bravo and every member are
    /// down.
    /// </summary>
    [TestCase(Timeout = 120000)]
    public async Task TheAngleClearsAfterADeathRewindWithAlphaStillBrokenBehindItsEntry() {
        using var fixture = new LevelFixture(StoryManager.GetLevelScenePath(CampaignLevel.Gettysburg), ScratchSlot,
            Level11Controller.Checkpoint0, CampaignLevel.Gettysburg, Difficulty.Hard);
        var level = (Level11Controller)fixture.Level;
        await fixture.SettleAsync();
        level.SeminaryRidgeLock.RestoreCleared();
        ShieldGeneratorTower alpha = level.ShieldArrayAlpha;
        for (int guard = 0; guard < 8 && !alpha.IsDestroyed; guard++) alpha.TakeEnvironmentDamage(alpha.MaxHP);
        AssertThat(alpha.IsDestroyed).IsTrue();
        // The Wheatfield is behind the hero, who walks on past the inert Middle.
        level.WheatfieldLock.RestoreCleared();

        EncounterLock angle = level.AngleLock;
        level.Player.GlobalPosition = new Vector2(6500f, Level11Controller.EnemyGroundY);
        level.Player.Velocity = Vector2.Zero;
        await Frames(4);
        AssertThat(angle.State).IsEqual(EncounterLockState.Engaged);

        await fixture.DieInsideAndLandAsync(level.Player, angle);
        AssertThat(alpha.IsDestroyed)
            .OverrideFailureMessage("The rewind stood array Alpha back up behind the Angle's closed entry.").IsTrue();
        AssertThat(angle.State).IsEqual(EncounterLockState.Engaged);

        level.ShieldArrayBravo.TakeEnvironmentDamage(level.ShieldArrayBravo.MaxHP);
        await KillEveryMemberAsync(angle);
        AssertThat(angle.State)
            .OverrideFailureMessage("The Angle must clear once Bravo and every member are down.")
            .IsEqual(EncounterLockState.Cleared);
        AssertThat(level.BreastworkOpen).IsTrue();
    }

    // === Helpers =============================================================

    private static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>Kills members until every wave has come and gone, a few frames apart.</summary>
    private static async Task KillEveryMemberAsync(EncounterLock encounterLock) {
        for (int round = 0; round < 16 && encounterLock.State != EncounterLockState.Cleared; round++) {
            foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
            await Frames(3);
        }
    }

    private static string ObjectiveText(Node level) =>
        FindAll<StoryHUD>(level) is { Count: > 0 } huds
            ? huds[0].GetNode<Label>("SafeArea/TopLeft/Objective").Text
            : "";

    private static PuzzleManager FindPuzzle(Node level, string puzzleID) {
        foreach (PuzzleManager puzzle in FindAll<PuzzleManager>(level)) {
            if (puzzle.PuzzleID == puzzleID) return puzzle;
        }
        return null;
    }

    private static List<T> FindAll<T>(Node root) where T : Node {
        var found = new List<T>();
        Walk(root, found);
        return found;
    }

    private static void Walk<T>(Node node, List<T> found) where T : Node {
        if (node is T match) found.Add(match);
        Godot.Collections.Array<Node> children = node.GetChildren();
        using var childrenLifetime = children.AsDisposable();
        foreach (Node child in children) Walk(child, found);
    }

    /// <summary>
    /// One real campaign scene in the runner tree. With a save slot it resumes
    /// at <c>resumeCheckpointID</c> against a scratch save (a fresh entry defers a
    /// gameplay-pausing entrance dialogue); without one it is a slotless load
    /// whose dialogue <see cref="SettleAsync"/> plays through. Hands back the
    /// session, the scratch slot, the rewind pool, the pause and every pooled body.
    /// </summary>
    private sealed class LevelFixture : IDisposable {
        public readonly Node2D Level;
        private readonly SessionData _originalSession;
        private readonly StorySaveData _originalSave;
        private readonly int _originalRewinds;
        private readonly bool _originalPaused;

        public LevelFixture(string scenePath, int saveSlot, string resumeCheckpointID,
            CampaignLevel campaignLevel = CampaignLevel.Florence, Difficulty difficulty = Difficulty.Normal) {
            _originalPaused = Tree.Paused;
            _originalSession = GameManager.Instance.CurrentSession;
            _originalSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            _originalRewinds = StoryManager.Instance.ChronalRewindsRemaining;

            SessionData session = _originalSession;
            session.ActiveSaveSlot = saveSlot;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = difficulty;
            GameManager.Instance.CurrentSession = session;
            if (saveSlot == ScratchSlot) {
                SaveManager.Instance.SaveSlots[ScratchSlot] = new StorySaveData {
                    SelectedCharacterID = "einstein",
                    Difficulty = difficulty,
                    CurrentLevelID = StoryManager.GetLevelScenePath(campaignLevel),
                    LastCheckpointID = resumeCheckpointID,
                    CurrentHP = 100,
                    CurrentUltimateMeter = 0f
                };
            }
            StoryManager.Instance.SetRewinds(ChronalRewindManager.GetMaximumRewinds(difficulty));
            Level = ResourceLoader.Load<PackedScene>(scenePath).Instantiate<Node2D>();
            Level.Name = "EncounterLockObjectiveFixture";
            Tree.Root.AddChild(Level);
        }

        /// <summary>A couple of frames, any entrance dialogue played through, the tree unpaused.</summary>
        public async Task SettleAsync() {
            await Frames(2);
            DialogueManager dialogue = DialogueManager.Instance;
            for (int advance = 0; advance < 200 && dialogue != null && dialogue.IsSequenceActive; advance++) dialogue.AdvanceLine();
            await Frames(2);
            AssertThat(Tree.Paused).OverrideFailureMessage("The fixture level left the tree paused.").IsFalse();
        }

        /// <summary>
        /// Holds the hero alive inside the engaged lock for the whole rewind
        /// buffer (so the rewind lands inside it), kills it there, and waits out
        /// the rewind and the Post-Landing Hold. The lock must stay Engaged
        /// throughout (a dead hero is never judged; the landing is inside).
        /// </summary>
        public async Task DieInsideAndLandAsync(PlayerController hero, EncounterLock encounterLock) {
            ChronalRewindManager rewind = FindAll<ChronalRewindManager>(Level) is { Count: > 0 } managers ? managers[0] : null;
            AssertObject(rewind).OverrideFailureMessage("The level has no ChronalRewindManager.").IsNotNull();
            if (rewind.RemainingRewinds <= 0) rewind.RefundRewind();
            AssertThat(rewind.RemainingRewinds > 0)
                .OverrideFailureMessage("No rewind charge: the death would collapse the timeline.").IsTrue();

            hero.ScriptedHPFloor = 1;
            try {
                await Frames(ChronalRewindBuffer.DefaultCapacity + 30);
            } finally {
                hero.ScriptedHPFloor = 0;
            }
            AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
            hero.KillPlayerNonHit();
            AssertThat(hero.CurrentState).IsEqual(CharacterState.Dead);
            for (int wait = 0; wait < 60 && (rewind.IsRewinding || rewind.IsPostLandingHoldActive); wait++) await Frames(10);
            await Frames(2);
            AssertThat(rewind.IsRewinding || rewind.IsPostLandingHoldActive)
                .OverrideFailureMessage("The death rewind never finished.").IsFalse();
            AssertThat(hero.CurrentState != CharacterState.Dead).OverrideFailureMessage("The hero stayed dead.").IsTrue();
            AssertThat(encounterLock.HeroStandsInside)
                .OverrideFailureMessage($"The rewind landed outside the lock at {hero.GlobalPosition}.").IsTrue();
        }

        public void Dispose() {
            StoryEngagementDirector.ResetForTest();
            PoolManager.Instance?.ReleaseActiveUnder(Level);
            PoolManager.Instance?.ReleaseActiveInGroup("Enemies");
            if (GodotObject.IsInstanceValid(Level)) {
                Level.GetParent()?.RemoveChild(Level);
                Level.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalSave;
            StoryManager.Instance.SetRewinds(_originalRewinds);
            GameManager.Instance.CurrentSession = _originalSession;
            Tree.Paused = _originalPaused;
        }
    }
}
