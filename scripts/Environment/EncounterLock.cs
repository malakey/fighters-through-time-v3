using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// The Story <b>encounter lock</b> (gameplay update 2026-10-12, GU-E; user
    /// request 3, ledger <c>USER-ENCOUNTER-LOCKS</c>): an authored area the hero
    /// must clear before the way forward opens — "you no longer have the option
    /// of jumping past them". The rules are <see cref="EncounterLockRules"/>; this
    /// node is their engine half.
    ///
    /// <para><b>States</b> (<see cref="EncounterLockState"/>):
    /// <b>Armed</b> — built, the exit barrier closed from level build (so no area
    /// can be skipped even before it engages), pre-placed members dormant;
    /// <b>Engaged</b> — the hero is inside, both barriers closed, members fighting,
    /// waves spawning; <b>Cleared</b> — latched, both barriers open. An Engaged
    /// lock whose living hero ends a tick outside its area (an Echo Step, a
    /// death-rewind landing, a rift snap, a scripted move) reverts to Armed: the
    /// entry opens, the exit stays shut, members keep their HP and go dormant,
    /// spawned waves stay spawned and the rest spawn on the next engage.</para>
    ///
    /// <para><b>Engine interplay.</b> It joins <see cref="Group"/> and
    /// <see cref="TimeFreezeController.FreezableGroup"/> (a pure latch): nothing
    /// engages, spawns or ticks the watchdog while Time Freeze, the R03 hold or
    /// a boss suspension holds the world, though the hero's containment is
    /// polled every tick. Wave spawn-ins refresh the freeze sweep. Members are
    /// registered at the spawn call (never inferred from a parent), carry the
    /// lock's area clamp and fall net (<c>EnemyController.Encounter.cs</c>), and
    /// report their death per instance; a pool-recycled body is counted as
    /// removed. A stall watchdog relocates a member that is stuck where the hero
    /// cannot reach it, and a hard cap force-clears a lock left with nothing to
    /// fight.</para>
    ///
    /// <para><b>Usable standalone</b> (<see cref="Create"/>, then add it to the
    /// level and call <see cref="Arm"/>, or let the first tick arm it) — Florence
    /// and the Tutorial have no base class — or through
    /// <c>StoryLevelControllerBase.BuildEncounterLock</c>, which also wires the
    /// camera, the objective and the F10 baseline.</para>
    /// </summary>
    public partial class EncounterLock : Node2D, IStoryTimeFreezable {
        /// <summary>Every lock joins this group (the test and bot seam).</summary>
        public const string Group = "story_encounter_lock";

        /// <summary>
        /// Test seam (plan §2.3): while set, every lock builds Cleared — no
        /// members, both barriers open. Real-scene suites that walk a hero through
        /// a level and do not test locks set it in try/finally.
        /// </summary>
        internal static bool SuppressForTesting;

        /// <summary>One registered member: the body at the generation it was registered with.</summary>
        private sealed class Member {
            public EnemyController Body;
            public int Generation;
            public Vector2 Anchor;
            public bool Resolved;
            public int StallFrames;
            public Vector2 LastPosition;
            public bool HasLastPosition;
        }

        private readonly List<Member> _members = new();
        private EncounterLockWave[] _waves = Array.Empty<EncounterLockWave>();
        private bool[] _waveSpawned = Array.Empty<bool>();
        private int[] _plannedCounts = Array.Empty<int>();
        private bool _armed;
        private bool _timeFrozen;
        private int _lastPublishedRemaining = -1;
        private float _heroHalfWidth = -1f;
        private PlayerController _heroForHalfWidth;

        // === Spec ============================================================

        public string LockID { get; private set; } = "";

        /// <summary>The area the hero must clear, global (only its x-span confines members).</summary>
        public Rect2 Area { get; private set; }

        /// <summary>The camera rect while Engaged.</summary>
        public Rect2 CameraBounds { get; private set; }

        public EncounterGoDirection GoDirection { get; private set; } = EncounterGoDirection.Right;

        /// <summary>The objective posted while Engaged ("" keeps the level's own).</summary>
        public string ObjectiveKey { get; private set; } = "";

        /// <summary>The spec this lock was built from. Content validation seam.</summary>
        public EncounterLockSpec Spec { get; private set; }

        public EncounterLockBarrier EntryBarrier { get; private set; }
        public EncounterLockBarrier ExitBarrier { get; private set; }

        // === Live state ======================================================

        public EncounterLockState State { get; private set; } = EncounterLockState.Armed;

        /// <summary>
        /// The mover-hold query (plan §2.3): true while the lock is Armed or
        /// Engaged, so a level can hold a moving platform whose path crosses one of
        /// its barriers at its near stop until the lock clears.
        /// </summary>
        public bool IsHolding => State != EncounterLockState.Cleared;

        /// <summary>True once <see cref="Arm"/> has run.</summary>
        public bool IsArmedOnce => _armed;

        /// <summary>True when the lock was restored Cleared from a resume baseline (or the test seam).</summary>
        public bool RestoredCleared { get; private set; }

        /// <summary>True when the hard cap, not the clear predicate, cleared it.</summary>
        public bool ForceCleared { get; private set; }

        /// <summary>Seconds this lock has been Engaged and unheld, over all its engagements.</summary>
        public double EngagedSeconds { get; private set; }

        public int EngageCount { get; private set; }
        public int RevertCount { get; private set; }

        /// <summary>Members the stall watchdog put back on their anchors.</summary>
        public int StallRelocations { get; private set; }

        /// <summary>Members the lock counted as removed because the pool recycled them.</summary>
        public int RecycledMembers { get; private set; }

        /// <summary>Every member this difficulty plans, all waves together.</summary>
        public int PlannedTotal { get; private set; }

        /// <summary>The members registered so far (alive or not).</summary>
        public int RegisteredCount => _members.Count;

        /// <summary>The living registered members right now.</summary>
        public int LivingMemberCount {
            get {
                int living = 0;
                foreach (Member member in _members) if (!member.Resolved && IsLiving(member)) living++;
                return living;
            }
        }

        /// <summary>The living registered members, as a snapshot list. Test and bot seam.</summary>
        public IReadOnlyList<EnemyController> LivingMembers {
            get {
                var bodies = new List<EnemyController>();
                foreach (Member member in _members) if (!member.Resolved && IsLiving(member)) bodies.Add(member.Body);
                return bodies;
            }
        }

        /// <summary>True once every wave has spawned.</summary>
        public bool AllWavesSpawned {
            get {
                foreach (bool spawned in _waveSpawned) if (!spawned) return false;
                return true;
            }
        }

        /// <summary>The HUD's "Enemies left" count: living members plus every member still to spawn.</summary>
        public int RemainingCount => EncounterLockRules.RemainingCount(State, LivingMemberCount, UnspawnedPlanned());

        /// <summary>True while Time Freeze (or a recovery hold's sweep) has latched this lock.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// FIX-E (2026-10-13): true while the lock's living hero stands inside its
        /// area — the same containment test the tick reverts on. The checkpoint
        /// guard (<c>CheckpointTrigger.IsHeldByEncounterLock</c>) reads it.
        /// </summary>
        public bool HeroStandsInside {
            get {
                PlayerController hero = ResolveHero();
                if (!IsAlive(hero)) return false;
                Vector2 feet = hero.GlobalPosition;
                return EncounterLockRules.IsInsideArea(
                    feet.X, feet.Y, Area.Position.X, Area.End.X, Area.Position.Y, Area.End.Y);
            }
        }

        /// <summary>The hero the lock judges; set by the base level, else resolved from <c>StoryPlayer</c>.</summary>
        public PlayerController Hero { get; set; }

        /// <summary>The Story camera whose lock-bounds layer the lock drives; resolved from the hero when unset.</summary>
        public StoryCameraConfiner Camera { get; set; }

        /// <summary>Raised on every state edge: (lock, previous, current). The base level wires the objective here.</summary>
        public event Action<EncounterLock, EncounterLockState, EncounterLockState> StateChanged;

        // === Construction ====================================================

        /// <summary>Builds a configured lock (barriers included, both open). The caller adds it to the level.</summary>
        public static EncounterLock Create(EncounterLockSpec spec) {
            var encounterLock = new EncounterLock { Name = $"EncounterLock_{spec?.LockID ?? "unnamed"}" };
            encounterLock.Configure(spec);
            return encounterLock;
        }

        private void Configure(EncounterLockSpec spec) {
            Spec = spec ?? new EncounterLockSpec();
            LockID = Spec.LockID ?? "";
            Area = Spec.Area.Abs();
            GoDirection = Spec.GoDirection;
            ObjectiveKey = Spec.ObjectiveKey ?? "";
            _waves = Spec.Waves ?? Array.Empty<EncounterLockWave>();
            _waveSpawned = new bool[_waves.Length];
            _plannedCounts = new int[_waves.Length];
            CameraBounds = Spec.CameraBounds?.Abs() ?? DeriveCameraBounds(Area, Spec.LevelBounds);

            float barrierWidth = Spec.BarrierWidth > 0f ? Spec.BarrierWidth : EncounterLockRules.DefaultBarrierWidthPixels;
            float areaCentre = Area.Position.X + Area.Size.X * 0.5f;
            if (Spec.EntryBarrierX is float entryX) {
                Vector2 range = Spec.EntryBarrierYRange ?? new Vector2(StoryGateRules.ColumnTopY, Spec.BarrierFloorY);
                EntryBarrier = EncounterLockBarrier.Create(LockID, isEntry: true,
                    areaSide: areaCentre >= entryX ? 1f : -1f, entryX, range.X, range.Y, barrierWidth);
                AddChild(EntryBarrier);
            }
            if (Spec.ExitBarrierX is float exitX) {
                Vector2 range = Spec.ExitBarrierYRange ?? new Vector2(StoryGateRules.ColumnTopY, Spec.BarrierFloorY);
                ExitBarrier = EncounterLockBarrier.Create(LockID, isEntry: false,
                    areaSide: areaCentre >= exitX ? 1f : -1f, exitX, range.X, range.Y, barrierWidth);
                AddChild(ExitBarrier);
            }
        }

        /// <summary>
        /// The derived camera rect: the area expanded symmetrically to at least
        /// the visible view on each axis and clipped to the level bounds (to the
        /// area itself when no level bounds are known).
        /// </summary>
        public static Rect2 DeriveCameraBounds(Rect2 area, Rect2? levelBounds) {
            Rect2 clip = (levelBounds ?? area).Abs();
            (float minX, float maxX) = EncounterLockRules.ExpandCameraSpan(
                area.Position.X, area.End.X, EncounterLockRules.ViewWorldWidth, clip.Position.X, clip.End.X);
            (float minY, float maxY) = EncounterLockRules.ExpandCameraSpan(
                area.Position.Y, area.End.Y, EncounterLockRules.ViewWorldHeight, clip.Position.Y, clip.End.Y);
            return new Rect2(minX, minY, maxX - minX, maxY - minY);
        }

        public override void _Ready() {
            AddToGroup(Group);
            AddToGroup(TimeFreezeController.FreezableGroup);
            // The spec is authored in level (global) coordinates and the barriers
            // are its children, so the lock itself sits on the global frame
            // whatever its parent's offset (a level's origin is the global one).
            GlobalTransform = Transform2D.Identity;
            if (SuppressForTesting) RestoreCleared();
        }

        public override void _ExitTree() {
            foreach (Member member in _members) Unsubscribe(member);
        }

        /// <summary>Time Freeze and the recovery holds latch the lock (a pure flag).</summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

        // === Arming, restoring ===============================================

        /// <summary>
        /// Arms the lock at level build: closes the exit barrier and spawns every
        /// <see cref="EncounterWaveTrigger.OnBuild"/> wave dormant. Once only; a
        /// lock already restored Cleared spawns nothing. The base level calls it
        /// right after the resume baseline is applied; a standalone lock that
        /// nobody arms arms itself on its first tick.
        /// </summary>
        public void Arm() {
            if (_armed) return;
            _armed = true;
            if (State == EncounterLockState.Cleared) return;
            Difficulty difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            PlannedTotal = 0;
            for (int index = 0; index < _waves.Length; index++) {
                EncounterLockWave wave = _waves[index];
                _plannedCounts[index] = wave == null ? 0 : EncounterLockRules.PlannedSpawnCount(
                    wave.Spawns?.Length ?? 0, wave.AuthoredCount, wave.Scaled, difficulty);
                PlannedTotal += _plannedCounts[index];
            }
            if (ExitBarrier != null) {
                ExitBarrier.SetClosed(true);
                PushHeroOutOf(ExitBarrier);
            }
            for (int index = 0; index < _waves.Length; index++) {
                if (_waves[index]?.Trigger == EncounterWaveTrigger.OnBuild) SpawnWave(index, dormant: true);
            }
        }

        /// <summary>
        /// Builds the lock Cleared: a resume whose F10 baseline lists this lock
        /// (or the test seam). No members, both barriers open, no presentation —
        /// and any member an earlier arm spawned is handed back to the pool.
        /// </summary>
        public void RestoreCleared() {
            if (State == EncounterLockState.Cleared) return;
            EncounterLockState previous = State;
            State = EncounterLockState.Cleared;
            RestoredCleared = true;
            for (int index = 0; index < _waveSpawned.Length; index++) _waveSpawned[index] = true;
            foreach (Member member in _members) {
                Unsubscribe(member);
                if (member.Resolved || !IsLiving(member)) continue;
                member.Resolved = true;
                member.Body.LeaveEncounter();
                if (PoolManager.Instance != null) PoolManager.Instance.Release(member.Body);
                else member.Body.QueueFree();
            }
            EntryBarrier?.SetClosed(false);
            ExitBarrier?.SetClosed(false);
            if (previous == EncounterLockState.Engaged) ResolveCamera()?.ClearLockBounds();
            StateChanged?.Invoke(this, previous, State);
        }

        // === The tick ========================================================

        public override void _PhysicsProcess(double delta) => Tick(delta);

        /// <summary>One physics tick of the lock. Internal so a test can drive it.</summary>
        internal void Tick(double delta) {
            if (!_armed) Arm();
            if (State == EncounterLockState.Cleared) return;
            PollMembers();

            PlayerController hero = ResolveHero();
            bool heroAlive = IsAlive(hero);
            Vector2 feet = heroAlive ? hero.GlobalPosition : Vector2.Zero;
            bool inside = heroAlive && EncounterLockRules.IsInsideArea(
                feet.X, feet.Y, Area.Position.X, Area.End.X, Area.Position.Y, Area.End.Y);

            // Containment is polled every tick, held or not: an Echo Step, a
            // rewind landing or a scripted move outside an engaged area must not
            // leave the hero shut out with live members.
            if (EncounterLockRules.ShouldRevertToArmed(State, heroAlive, inside)) {
                RevertToArmed();
                return;
            }

            bool held = IsHeld();
            if (held) return;

            if (State == EncounterLockState.Armed) {
                if (!heroAlive) return;
                bool paused = IsInsideTree() && GetTree().Paused;
                if (!EncounterLockRules.CanEngage(feet.X, HeroHalfWidth(hero), feet.Y,
                        Area.Position.X, Area.End.X, Area.Position.Y, Area.End.Y,
                        heroAlive, timeFrozen: false, worldHeld: false, gameplayPaused: paused)) return;
                Engage(hero);
            }

            EngagedSeconds += delta;
            ReengageMembers(hero);
            SpawnDueWaves(hero);
            TickWatchdog(hero);
            PollMembers();
            int living = LivingMemberCount;
            bool puzzles = RequiredPuzzlesSolved();
            bool breakables = RequiredBreakablesDestroyed();
            if (EncounterLockRules.IsClearPredicateMet(living, AllWavesSpawned, puzzles, breakables)) {
                Clear(forced: false);
                return;
            }
            if (EncounterLockRules.ShouldForceClear(State, EngagedSeconds, living, puzzles, breakables)) {
                GD.Print($"[encounter-lock] '{LockID}' force-cleared after {EngagedSeconds:0} s engaged "
                    + $"with no living member ({_waveSpawned.Length} waves, all spawned: {AllWavesSpawned}).");
                Clear(forced: true);
                return;
            }
            PublishIfChanged();
        }

        /// <summary>True while anything holds the world: Time Freeze, a recovery hold or a boss suspension.</summary>
        private bool IsHeld() {
            if (_timeFrozen) return true;
            if (!IsInsideTree()) return false;
            SceneTree tree = GetTree();
            if (ChronalRewindManager.IsWorldHeld(tree)) return true;
            return tree.GetFirstNodeInGroup(TimeFreezeController.ControllerGroup) is TimeFreezeController freeze
                && IsInstanceValid(freeze) && freeze.IsFrozen;
        }

        // === Edges ===========================================================

        private void Engage(PlayerController hero) {
            EncounterLockState previous = State;
            State = EncounterLockState.Engaged;
            EngageCount++;
            if (EntryBarrier != null) {
                EntryBarrier.SetClosed(true);
                PushHeroOutOf(EntryBarrier);
            }
            ExitBarrier?.SetClosed(true);
            foreach (Member member in _members) {
                if (!member.Resolved && IsLiving(member)) member.Body.Engage(hero);
            }
            ResolveCamera()?.SetLockBounds(CameraBounds);
            StateChanged?.Invoke(this, previous, State);
            Publish();
        }

        private void RevertToArmed() {
            EncounterLockState previous = State;
            State = EncounterLockState.Armed;
            RevertCount++;
            EntryBarrier?.SetClosed(false);
            foreach (Member member in _members) {
                if (!member.Resolved && IsLiving(member)) member.Body.SetEncounterDormant(true);
                member.StallFrames = 0;
            }
            ResolveCamera()?.ClearLockBounds();
            StateChanged?.Invoke(this, previous, State);
            Publish();
        }

        private void Clear(bool forced) {
            EncounterLockState previous = State;
            State = EncounterLockState.Cleared;
            ForceCleared = forced;
            foreach (Member member in _members) Unsubscribe(member);
            EntryBarrier?.SetClosed(false);
            ExitBarrier?.SetClosed(false);
            ResolveCamera()?.ClearLockBounds();
            StateChanged?.Invoke(this, previous, State);
            Publish();
        }

        // === Members =========================================================

        private void SpawnDueWaves(PlayerController hero) {
            bool spawnedAny = false;
            for (int index = 0; index < _waves.Length; index++) {
                if (_waveSpawned[index]) continue;
                bool earlierSpawned = true;
                for (int earlier = 0; earlier < index; earlier++) earlierSpawned &= _waveSpawned[earlier];
                EncounterLockWave wave = _waves[index];
                if (wave == null || !EncounterLockRules.ShouldSpawnWave(
                        wave.Trigger, wave.AliveAtMost, State, earlierSpawned, LivingMemberCount, held: false)) break;
                SpawnWave(index, dormant: false);
                spawnedAny = true;
            }
            // A wave revealed under a freeze must not take a tick (the sweep is
            // idempotent; nothing spawns while held, but a freeze can begin the
            // same tick).
            if (spawnedAny && IsInsideTree()) TimeFreezeController.FreezeActivatedRoom(GetTree());
        }

        private void SpawnWave(int index, bool dormant) {
            if (index < 0 || index >= _waves.Length || _waveSpawned[index]) return;
            _waveSpawned[index] = true;
            EncounterLockWave wave = _waves[index];
            if (wave?.Spawns == null) return;
            Node parent = GetParent() ?? this;
            PlayerController hero = dormant ? null : ResolveHero();
            int count = Math.Min(_plannedCounts[index], wave.Spawns.Length);
            for (int spawnIndex = 0; spawnIndex < count; spawnIndex++) {
                EncounterLockSpawn spawn = wave.Spawns[spawnIndex];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.EnemyID)) continue;
                EnemyController body = EnemyFactory.Spawn(spawn.EnemyID, parent, spawn.Position, spawn.PatrolA, spawn.PatrolB);
                if (body == null) {
                    GD.PushWarning($"[encounter-lock] '{LockID}' could not spawn '{spawn.EnemyID}' (wave {wave.RowID}).");
                    continue;
                }
                if (!Register(body, spawn.Position, dormant)) continue;
                if (!dormant) {
                    if (IsAlive(hero)) body.Engage(hero);
                    parent.AddChild(EncounterSpawnTear.Create(spawn.Position));
                }
            }
            _lastPublishedRemaining = -1;
        }

        /// <summary>
        /// Registers <paramref name="body"/> as a member at the spawn call. A
        /// dormant member faces the lock's entry. Returns false for a body the
        /// lock may not own (a summon, a drill-invulnerable body).
        /// </summary>
        private bool Register(EnemyController body, Vector2 anchor, bool dormant) {
            float entryX = EntryBarrier?.CenterX ?? Area.Position.X;
            bool facesRight = entryX > anchor.X;
            if (!body.JoinEncounter(GetInstanceId(), LockID, Area, anchor, dormant, facesRight)) {
                GD.PushWarning($"[encounter-lock] '{LockID}' refused '{body.Data?.EnemyID}' as a member.");
                return false;
            }
            var member = new Member { Body = body, Generation = body.SpawnGeneration, Anchor = anchor };
            body.Died += OnMemberDied;
            _members.Add(member);
            return true;
        }

        /// <summary>
        /// A member's death. Runs inside the killing hit's physics flush, so it
        /// only marks the member; the clear and any wave it unlocks run on the
        /// lock's next tick.
        /// </summary>
        private void OnMemberDied(EnemyController body) {
            foreach (Member member in _members) {
                if (member.Resolved || member.Body != body || member.Generation != body.SpawnGeneration) continue;
                member.Resolved = true;
                Unsubscribe(member);
                break;
            }
        }

        /// <summary>Resolves every member that is dead or no longer this lock's body (freed, parked, recycled).</summary>
        private void PollMembers() {
            foreach (Member member in _members) {
                if (member.Resolved) continue;
                if (IsLiving(member)) continue;
                member.Resolved = true;
                if (IsInstanceValid(member.Body) && member.Body.SpawnGeneration != member.Generation) {
                    RecycledMembers++;
                    GD.Print($"[encounter-lock] '{LockID}': a member's body was recycled by the pool "
                        + $"(generation {member.Generation} -> {member.Body.SpawnGeneration}); counted as removed.");
                }
                Unsubscribe(member);
            }
        }

        private bool IsLiving(Member member) {
            EnemyController body = member.Body;
            return body != null && IsInstanceValid(body) && !body.IsQueuedForDeletion()
                && body.SpawnGeneration == member.Generation
                && body.EncounterToken == GetInstanceId()
                && body.CurrentState != EnemyState.Dead;
        }

        private void Unsubscribe(Member member) {
            if (member.Body != null && IsInstanceValid(member.Body)) member.Body.Died -= OnMemberDied;
        }

        /// <summary>An engaged member that lost its target (the hero died, a de-aggro) is woken onto the hero again.</summary>
        private void ReengageMembers(PlayerController hero) {
            if (!IsAlive(hero)) return;
            foreach (Member member in _members) {
                if (!member.Resolved && IsLiving(member) && member.Body.NeedsEncounterEngage) member.Body.Engage(hero);
            }
        }

        /// <summary>
        /// The stall watchdog: a member stuck off any floor, or standing still on
        /// a post the hero cannot reach, for <see cref="EncounterLockRules.StallFrames"/>
        /// ticks is put back on its anchor. Never killed.
        /// </summary>
        private void TickWatchdog(PlayerController hero) {
            foreach (Member member in _members) {
                if (member.Resolved || !IsLiving(member)) continue;
                EnemyController body = member.Body;
                Vector2 position = body.GlobalPosition;
                float moved = member.HasLastPosition ? position.DistanceTo(member.LastPosition) : 0f;
                member.LastPosition = position;
                member.HasLastPosition = true;
                bool flying = body.Data?.Behavior == DefaultBehavior.Flying;
                bool inFlight = body.CurrentState == EnemyState.Stunned || body.IsThrownFlight
                    || body.IsInFinisherFlight || body.IsHeldByPlayer;
                float gap = IsAlive(hero) ? position.Y - hero.GlobalPosition.Y : 0f;
                bool ranged = body.Data?.PrimaryAttack?.SpawnsProjectiles == true;
                bool stalled = EncounterLockRules.IsStalledSample(
                    flying, body.IsOnFloor(), inFlight, body.CurrentState == EnemyState.Chase, ranged, moved, gap);
                member.StallFrames = EncounterLockRules.NextStallFrames(member.StallFrames, stalled);
                if (!EncounterLockRules.ShouldRelocate(member.StallFrames)) continue;
                body.RelocateToEncounterAnchor("stall");
                member.StallFrames = 0;
                member.HasLastPosition = false;
                StallRelocations++;
            }
        }

        private int UnspawnedPlanned() {
            int planned = 0;
            for (int index = 0; index < _waves.Length; index++) {
                if (_waveSpawned[index]) continue;
                // Counts are only planned once armed; before that the table is the estimate.
                planned += _armed ? _plannedCounts[index] : _waves[index]?.Spawns?.Length ?? 0;
            }
            return planned;
        }

        // === Conditions ======================================================

        private bool RequiredPuzzlesSolved() {
            string[] puzzleIDs = Spec?.RequiredPuzzleIDs;
            if (puzzleIDs == null || puzzleIDs.Length == 0) return true;
            foreach (string puzzleID in puzzleIDs) {
                if (string.IsNullOrWhiteSpace(puzzleID)) continue;
                if (!IsPuzzleSolved(puzzleID)) return false;
            }
            return true;
        }

        private bool IsPuzzleSolved(string puzzleID) {
            if (IsInsideTree()) {
                Godot.Collections.Array<Node> managers = GetTree().GetNodesInGroup("puzzle_manager");
                using var managersLifetime = managers.AsDisposable();
                foreach (Node node in managers) {
                    if (node is PuzzleManager manager && manager.PuzzleID == puzzleID) return manager.IsSolvedOrPersisted;
                }
            }
            return SaveManager.Instance?.IsPuzzleCompleted(puzzleID) == true;
        }

        private bool RequiredBreakablesDestroyed() {
            DamageableEnvironmentObject[] breakables = Spec?.RequiredBreakables;
            if (breakables == null) return true;
            foreach (DamageableEnvironmentObject breakable in breakables) {
                // A breakable already freed (an OpenDoor-style removal) is gone.
                if (breakable != null && IsInstanceValid(breakable) && !breakable.IsQueuedForDeletion()
                    && !breakable.IsDestroyed) return false;
            }
            return true;
        }

        // === Hero, camera, presentation ======================================

        private PlayerController ResolveHero() {
            if (Hero != null && IsInstanceValid(Hero)) return Hero;
            if (!IsInsideTree()) return null;
            return GetTree().GetFirstNodeInGroup("StoryPlayer") as PlayerController;
        }

        private static bool IsAlive(PlayerController hero) =>
            hero != null && IsInstanceValid(hero) && hero.IsInsideTree()
            && hero.CurrentState is not (CharacterState.Dead or CharacterState.Respawning);

        /// <summary>The hero's half-width from its own collision shape (40 px wide: 20), read once per hero.</summary>
        private float HeroHalfWidth(PlayerController hero) {
            if (hero != _heroForHalfWidth || _heroHalfWidth < 0f) {
                _heroForHalfWidth = hero;
                _heroHalfWidth = FTT.Combat.SafeRelocation.BodyOf(hero, needsFloor: true, passesWalls: false)?.HalfWidth
                    ?? FTT.Combat.StoryCombatRules.HeroHurtboxWidthPixels * 0.5f;
            }
            return _heroHalfWidth;
        }

        private void PushHeroOutOf(EncounterLockBarrier barrier) {
            PlayerController hero = ResolveHero();
            if (barrier == null || hero == null || !IsInstanceValid(hero)) return;
            barrier.PushOut(hero, HeroHalfWidth(hero));
        }

        private StoryCameraConfiner ResolveCamera() {
            if (Camera != null && IsInstanceValid(Camera)) return Camera;
            PlayerController hero = ResolveHero();
            if (hero == null || !IsInstanceValid(hero)) return null;
            Godot.Collections.Array<Node> children = hero.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is StoryCameraConfiner confiner) return Camera = confiner;
            }
            return null;
        }

        private void PublishIfChanged() {
            if (State != EncounterLockState.Engaged) return;
            if (RemainingCount == _lastPublishedRemaining) return;
            Publish();
        }

        private void Publish() {
            int remaining = RemainingCount;
            _lastPublishedRemaining = remaining;
            EventBus.Instance?.RaiseEncounterLockChanged(new EncounterLockPayload {
                LockID = LockID,
                State = State,
                Remaining = remaining,
                Total = PlannedTotal,
                GoDirection = GoDirection
            });
        }
    }
}
