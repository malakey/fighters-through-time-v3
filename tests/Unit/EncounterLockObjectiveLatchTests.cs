using System;
using System.Threading.Tasks;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.Environment;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

/// <summary>
/// FIX-OBJECTIVE (2026-10-13), the Phase B review's systemic defect: a death
/// rewind restores puzzles and breakables to the last checkpoint snapshot, which
/// the moved anchors made predate the solve, while an encounter lock read them
/// live every tick — so a lock could hold forever over an objective the hero had
/// already met. The framework rule (<see cref="EncounterLock"/>'s conditions
/// region): a required objective <b>latches</b> the first time the lock sees it
/// met (from Armed, held or not, surviving a revert), and the lock switches the
/// objective itself to <see cref="StoryRewindPolicy.PreserveCurrentState"/>.
/// In real physics frames with a stand-in puzzle and breakable, far from the
/// origin; every scene hands back the session, the save slot, the pause and the
/// pooled bodies in <c>finally</c>.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class EncounterLockObjectiveLatchTests {

    private static readonly Vector2 Origin = new(84000f, 0f);
    private const float FloorY = 900f;
    private const float FeetY = 850f;
    private const float AreaMin = 1000f;
    private const float AreaMax = 2400f;
    private const string PuzzleID = "test_lock.latch_puzzle";
    private const string ConditionID = "latch_condition";
    private const int ScratchSlot = 2;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>
    /// The review's Florence/Chicago/Hakata case on a stand-in: the puzzle is
    /// solved inside the engaged lock, and the death rewind's response leaves it
    /// solved (the lock switched it to Preserve); a reset that un-solves it anyway
    /// cannot hold the lock (the latch), which clears once its member is down.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task ASolvedRequiredPuzzleSurvivesADeathRewindAndAResetCannotHoldTheLock() {
        using var scene = new LatchScene();
        PuzzleManager puzzle = scene.BuildPuzzle(persist: false);
        EncounterLock encounterLock = scene.BuildLock(requiredPuzzle: true, breakable: null);
        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(encounterLock.IsRequiredPuzzleMet(PuzzleID)).IsFalse();
        AssertThat(puzzle.RewindPolicy).IsEqual(StoryRewindPolicy.RestoreCheckpointState);

        puzzle.SetCondition(ConditionID, true);
        AssertThat(puzzle.IsCompleted).IsTrue();
        await Frames(2);
        AssertThat(encounterLock.RequiredObjectivesMet).IsTrue();
        AssertThat(puzzle.RewindPolicy)
            .OverrideFailureMessage("The lock must keep a solved required puzzle solved through rewinds.")
            .IsEqual(StoryRewindPolicy.PreserveCurrentState);

        // A death rewind lands inside: its snapshot (taken at _Ready) predates the solve.
        EventBus.Instance.RaiseRewindTriggered(scene.Hero.GlobalPosition);
        AssertThat(puzzle.IsCompleted).OverrideFailureMessage("A rewind un-solved a latched puzzle.").IsTrue();

        // Anything else that un-solves it can no longer hold the lock.
        puzzle.ResetPuzzle("test");
        AssertThat(puzzle.IsCompleted).IsFalse();
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(encounterLock.IsRequiredPuzzleMet(PuzzleID)).IsTrue();
        AssertThat(encounterLock.RequiredObjectivesMet).IsTrue();

        foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
        await Frames(3);
        AssertThat(encounterLock.State)
            .OverrideFailureMessage("A lock whose objective was met must clear once its members are down.")
            .IsEqual(EncounterLockState.Cleared);
        AssertThat(encounterLock.ExitBarrier.IsClosed).IsFalse();
    }

    /// <summary>
    /// The review's Level 2 / Level 11 case on a stand-in: a destroyed required
    /// breakable stays destroyed through the death rewind's response, and one
    /// restored anyway cannot hold the lock.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task ADestroyedRequiredBreakableSurvivesADeathRewindAndARestoreCannotHoldTheLock() {
        using var scene = new LatchScene();
        DestructibleBlock breakable = scene.BuildBreakable();
        EncounterLock encounterLock = scene.BuildLock(requiredPuzzle: false, breakable);
        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);

        breakable.TakeEnvironmentDamage(breakable.MaxHP);
        AssertThat(breakable.IsDestroyed).IsTrue();
        await Frames(2);
        AssertThat(encounterLock.IsRequiredBreakableMet(breakable)).IsTrue();
        AssertThat(breakable.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);

        EventBus.Instance.RaiseRewindTriggered(scene.Hero.GlobalPosition);
        AssertThat(breakable.IsDestroyed).OverrideFailureMessage("A rewind rebuilt a latched breakable.").IsTrue();

        // Restored anyway (the old policy's response, from its full-HP snapshot).
        breakable.RewindPolicy = StoryRewindPolicy.RestoreCheckpointState;
        breakable.ApplyStoryRewind();
        AssertThat(breakable.IsDestroyed).IsFalse();
        await Frames(2);
        AssertThat(encounterLock.RequiredObjectivesMet).IsTrue();

        foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
    }

    /// <summary>
    /// The latch reads from Armed (Level 11's array Alpha, broken in the Wheatfield
    /// before the Angle engages, then restored by a rewind before the engage) and
    /// survives a revert to Armed (the hero landing outside, the objective undone,
    /// the hero walking back in).
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task AnObjectiveMetWhileArmedOrBeforeARevertStaysMet() {
        using var scene = new LatchScene();
        DestructibleBlock breakable = scene.BuildBreakable();
        PuzzleManager puzzle = scene.BuildPuzzle(persist: false);
        EncounterLock encounterLock = scene.BuildLock(requiredPuzzle: true, breakable);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);

        // Met while the hero is still outside, then undone before the engage.
        breakable.TakeEnvironmentDamage(breakable.MaxHP);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
        AssertThat(encounterLock.IsRequiredBreakableMet(breakable)).IsTrue();
        breakable.RewindPolicy = StoryRewindPolicy.RestoreCheckpointState;
        breakable.ApplyStoryRewind();
        AssertThat(breakable.IsDestroyed).IsFalse();

        // Engaged, the puzzle solved, then a landing outside reverts the lock.
        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        puzzle.SetCondition(ConditionID, true);
        await Frames(2);
        scene.PlaceHero(AreaMin - 400f);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
        AssertThat(encounterLock.RevertCount).IsEqual(1);
        puzzle.ResetPuzzle("test");

        // Back in: nothing left to meet but the member.
        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        AssertThat(encounterLock.RequiredObjectivesMet).IsTrue();
        foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
    }

    /// <summary>
    /// A lock rebuilt mid-attempt (a reload resumed before it): the solve the save
    /// carries counts on the lock's first tick, the puzzle then keeps it through a
    /// rewind (the save too), and the lock clears once its member is down.
    /// </summary>
    [TestCase(Timeout = 60000)]
    public async Task ALockRebuiltMidAttemptLatchesASolveTheSaveCarries() {
        using var scene = new LatchScene();
        StorySaveData scratch = scene.UseScratchSave();
        scratch.CompletedPuzzleIDs.Add(PuzzleID);
        PuzzleManager puzzle = scene.BuildPuzzle(persist: true);
        AssertThat(puzzle.IsCompleted).OverrideFailureMessage("The stand-in must restore its solve from the save.").IsTrue();
        EncounterLock encounterLock = scene.BuildLock(requiredPuzzle: true, breakable: null);
        await Frames(2);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Armed);
        AssertThat(encounterLock.RequiredObjectivesMet).IsTrue();
        AssertThat(puzzle.RewindPolicy).IsEqual(StoryRewindPolicy.PreserveCurrentState);

        EventBus.Instance.RaiseRewindTriggered(scene.Hero.GlobalPosition);
        AssertThat(puzzle.IsSolvedOrPersisted).IsTrue();
        AssertThat(scratch.CompletedPuzzleIDs.Contains(PuzzleID))
            .OverrideFailureMessage("A rewind wiped the persisted solve from the save.").IsTrue();

        scene.PlaceHero(AreaMin + 200f);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Engaged);
        foreach (EnemyController member in encounterLock.LivingMembers) member.TakeDamage(99999);
        await Frames(3);
        AssertThat(encounterLock.State).IsEqual(EncounterLockState.Cleared);
    }

    // === Helpers =============================================================

    private static async Task Frames(int count) {
        for (int frame = 0; frame < count; frame++) await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
    }

    /// <summary>
    /// A flat floor far from the origin, an Einstein hero west of the area, and
    /// one lock from 1000 to 2400 (entry at 990, exit at 2410) with one dormant
    /// slasher. Hands back the session, the scratch save slot, the pause and the
    /// pooled bodies.
    /// </summary>
    private sealed class LatchScene : IDisposable {
        public readonly Node2D Host;
        public readonly PlayerController Hero;
        private readonly SessionData _originalSession;
        private readonly bool _originalPaused;
        private readonly StorySaveData _originalScratchSave;
        private PuzzleManager _puzzle;

        public LatchScene() {
            _originalSession = GameManager.Instance.CurrentSession;
            _originalPaused = Tree.Paused;
            _originalScratchSave = SaveManager.Instance.SaveSlots[ScratchSlot];
            SessionData session = _originalSession;
            session.ActiveSaveSlot = -1;
            session.SelectedCharacterID = "einstein";
            session.Difficulty = Difficulty.Normal;
            GameManager.Instance.CurrentSession = session;

            Host = new Node2D { Name = "EncounterLockLatchHost" };
            Tree.Root.AddChild(Host);
            var floor = new StaticBody2D {
                Name = "Floor",
                Position = Origin + new Vector2(1000f, FloorY + 20f),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(8000f, 40f) } });
            Host.AddChild(floor);

            Hero = CharacterFactory.CreateCharacter("einstein", 0, applyStoryProgression: false, applyLegacyUnlockLocks: false);
            Host.AddChild(Hero);
            PlaceHero(AreaMin - 400f);
        }

        public Vector2 At(float x, float y = FeetY) => Origin + new Vector2(x, y);

        public void PlaceHero(float x) {
            Hero.GlobalPosition = At(x);
            Hero.Velocity = Vector2.Zero;
        }

        /// <summary>An active scratch save (slot 2, handed back on dispose) for the persisted-solve case.</summary>
        public StorySaveData UseScratchSave() {
            var save = new StorySaveData { SelectedCharacterID = "einstein" };
            SaveManager.Instance.SaveSlots[ScratchSlot] = save;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = ScratchSlot;
            GameManager.Instance.CurrentSession = session;
            return save;
        }

        /// <summary>A one-condition puzzle with the default rewind policy (restore the checkpoint snapshot).</summary>
        public PuzzleManager BuildPuzzle(bool persist) {
            _puzzle = new PuzzleManager {
                Name = "LatchPuzzle",
                PuzzleID = PuzzleID,
                RequiredConditionIDs = new[] { ConditionID },
                PersistCompletionToSave = persist
            };
            Host.AddChild(_puzzle);
            return _puzzle;
        }

        /// <summary>A breakable with the default rewind policy, off to the side and colliding with nothing.</summary>
        public DestructibleBlock BuildBreakable() {
            var breakable = new DestructibleBlock {
                Name = "LatchBreakable",
                ObjectID = "test_lock.latch_breakable",
                MaxHP = 20,
                Position = At(1200f, 600f),
                CollisionLayer = 0,
                CollisionMask = 0
            };
            breakable.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(40f, 40f) }
            });
            Host.AddChild(breakable);
            return breakable;
        }

        public EncounterLock BuildLock(bool requiredPuzzle, DamageableEnvironmentObject breakable) {
            EncounterLock encounterLock = EncounterLock.Create(new EncounterLockSpec {
                LockID = "test_lock_latch",
                Area = EncounterLockSpec.Span(Origin.X + AreaMin, Origin.X + AreaMax, 1080f),
                LevelBounds = new Rect2(Origin.X - 2000f, 0f, 8000f, 1080f),
                EntryBarrierX = Origin.X + AreaMin - 10f,
                ExitBarrierX = Origin.X + AreaMax + 10f,
                BarrierFloorY = FloorY,
                Waves = new[] {
                    new EncounterLockWave {
                        RowID = "test_lock.latch_room",
                        Spawns = new[] { new EncounterLockSpawn("chrono_slasher", At(2000f)) },
                        Trigger = EncounterWaveTrigger.OnBuild
                    }
                },
                RequiredPuzzleIDs = requiredPuzzle ? new[] { PuzzleID } : Array.Empty<string>(),
                RequiredBreakables = breakable != null
                    ? new[] { breakable } : Array.Empty<DamageableEnvironmentObject>(),
                ObjectiveKey = "encounter_lock_objective_clear"
            });
            Host.AddChild(encounterLock);
            encounterLock.Hero = Hero;
            encounterLock.Arm();
            // A pooled body another case left with its physics stopped would never tick.
            foreach (EnemyController member in encounterLock.LivingMembers) member.SetPhysicsProcess(true);
            return encounterLock;
        }

        public void Dispose() {
            InputManager.Instance?.ClearInputSource(Hero.PlayerIndex);
            StoryEngagementDirector.ResetForTest();
            PoolManager.Instance?.ReleaseActiveUnder(Host);
            if (GodotObject.IsInstanceValid(Host)) {
                Host.GetParent()?.RemoveChild(Host);
                Host.Free();
            }
            SaveManager.Instance.SaveSlots[ScratchSlot] = _originalScratchSave;
            GameManager.Instance.CurrentSession = _originalSession;
            Tree.Paused = _originalPaused;
        }
    }
}
