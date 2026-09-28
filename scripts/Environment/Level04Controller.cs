using Godot;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 4 - Paris, 1789 (The Storming of the Bastille). Four rooms across
    /// ~10,240 px:
    /// <list type="number">
    /// <item><b>Gatehouse Yard</b> (0-3200) - the mob at the gates; opening wave.</item>
    /// <item><b>Security Corridors</b> (3200-6080) - three <see cref="SearchlightZone"/>
    ///       beams in <see cref="SearchlightMode.UltimateDrain"/> mode gate the
    ///       approach; the vertical tower climb ends on a swept upper walkway.</item>
    /// <item><b>Cell Block</b> (6080-8640) - two <see cref="RescuableNPC"/> prisoners
    ///       sealed behind <see cref="DestructibleBlock"/> lock mechanisms. Freeing
    ///       both satisfies the <see cref="PuzzleManager"/> and the freed prisoners
    ///       haul the inner courtyard gate open.</item>
    /// <item><b>Inner Courtyard</b> (8640-10240) - two drawbridge walkways over a
    ///       central lower pit, with a flat pit floor wide enough for the
    ///       Revolutionary Tribunal's summoned adds to land and path.</item>
    /// </list>
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 10 standards,
    /// 0 elites, 1 boss, 3 extractors. Boss-summoned rioters are boss-driven and
    /// deliberately sit outside that budget.
    /// </summary>
    public partial class Level04Controller : StoryLevelControllerBase {

        // === Layout constants ===

        public const float LevelWidth = 10240f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 3200f;
        private const float Room3StartX = 6080f;
        private const float Room4StartX = 8640f;

        /// <summary>Boss-arena pit floor; 100 px below the approach so adds stay reachable.</summary>
        private const float PitFloorY = 1000f;

        private const string PrisonerPuzzleID = "level_04.free_prisoners";
        private const string PrisonerAConditionID = "prisoner_a_freed";
        private const string PrisonerBConditionID = "prisoner_b_freed";
        private const string BossResourcePath = "res://resources/Bosses/revolutionary_tribunal.tres";

        // === Authored encounter table (asserted by Level04ContentTests) ===

        /// <summary>Gatehouse yard: the crowd already fighting when the player arrives.</summary>
        public static readonly (string EnemyID, Vector2 Position)[] Room1Spawns = {
            ("chrono_rioter", new Vector2(1100, 850)),
            ("chrono_slasher", new Vector2(1900, 850)),
            ("chrono_rioter", new Vector2(2700, 850))
        };

        /// <summary>Security corridors: two on the floor, one holding the upper walkway.</summary>
        public static readonly (string EnemyID, Vector2 Position)[] Room2Spawns = {
            ("chrono_rioter", new Vector2(3900, 850)),
            ("chrono_slasher", new Vector2(4600, 850)),
            ("chrono_rioter", new Vector2(5600, 250))
        };

        /// <summary>Cell block: the heaviest concurrent group, still inside the warm-12 cap.</summary>
        public static readonly (string EnemyID, Vector2 Position)[] Room3Spawns = {
            ("chrono_rioter", new Vector2(6400, 850)),
            ("chrono_slasher", new Vector2(7100, 850)),
            ("chrono_rioter", new Vector2(7900, 850)),
            ("chrono_slasher", new Vector2(8350, 850))
        };

        /// <summary>Three extractors, each guarding a climb the critical path can skip.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_04.extractor_barricade", new Vector2(2500, 628)),
            ("level_04.extractor_tower", new Vector2(5700, 228)),
            ("level_04.extractor_cellroof", new Vector2(7820, 524))
        };

        /// <summary>Every authored standard spawn in room order. Must total exactly 10.</summary>
        public static IEnumerable<(string EnemyID, Vector2 Position)> AllStandardSpawns {
            get {
                foreach (var spawn in Room1Spawns) yield return spawn;
                foreach (var spawn in Room2Spawns) yield return spawn;
                foreach (var spawn in Room3Spawns) yield return spawn;
            }
        }

        // === Base contract ===

        public override string LevelID => "level_04_paris";
        public override CampaignLevel Level => CampaignLevel.Paris;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "paris_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_04_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240, 850);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the barricade crest (Platform 3000/560, top 552) one hop past the barricade
        // extractor — a dead-end perch over the gatehouse exit, off the floor route.
        protected override Vector2? SecretCachePosition => new(3000f, 457f);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "paris_objective_breach";
        protected override string BossObjectiveKey => "paris_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "paris_objective_complete";

        protected override Color FloorColor => new(0.20f, 0.18f, 0.17f);
        protected override Color FloorEdgeColor => new(0.44f, 0.38f, 0.30f);
        protected override Color PlatformColor => new(0.30f, 0.27f, 0.24f);
        protected override Color WallColor => new(0.14f, 0.13f, 0.13f);

        // === Scene-authored toolkit instances ===

        private readonly List<SearchlightZone> _searchlights = new();
        private readonly List<RescuableNPC> _prisoners = new();
        private readonly List<DestructibleBlock> _cellLocks = new();
        private PuzzleManager _prisonerPuzzle;
        private StaticBody2D _courtyardGate;

        public IReadOnlyList<SearchlightZone> Searchlights => _searchlights;
        public IReadOnlyList<RescuableNPC> Prisoners => _prisoners;
        public IReadOnlyList<DestructibleBlock> CellLocks => _cellLocks;
        public PuzzleManager PrisonerPuzzle => _prisonerPuzzle;

        /// <summary>False until the prisoner puzzle clears the inner-courtyard gate.</summary>
        public bool CourtyardGateOpen { get; private set; }

        /// <summary>How many prisoners have been freed (0-2). Drives the HUD counter.</summary>
        public int PrisonersFreed { get; private set; }

        /// <summary>Times a beam has caught the player; used for the alarm objective.</summary>
        public int SearchlightAlarmCount { get; private set; }

        private bool _room2WaveSpawned;
        private bool _room3WaveSpawned;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildGatehouseYard();
            BuildSecurityCorridors();
            BuildCellBlock();
            BuildInnerCourtyard();
            BuildExtractors(ExtractorPlacements);
        }

        /// <summary>
        /// Godot readies children before their parent, so every authored template
        /// instance in Level_04_Paris.tscn is already live by the time BuildLevel runs.
        /// </summary>
        private void CollectAuthoredNodes() {
            _prisonerPuzzle = GetNodeOrNull<PuzzleManager>("PrisonerPuzzle");
            if (_prisonerPuzzle != null) _prisonerPuzzle.PuzzleCompleted += _ => OpenCourtyardGate();

            AddSearchlight("SearchlightGatehouse");
            AddSearchlight("SearchlightCorridor");
            AddSearchlight("SearchlightTower");

            AddPrisoner("PrisonerPuzzle/PrisonerA");
            AddPrisoner("PrisonerPuzzle/PrisonerB");

            AddCellLock("PrisonerPuzzle/CellLockA");
            AddCellLock("PrisonerPuzzle/CellLockB");
        }

        private void AddSearchlight(string nodeName) {
            if (GetNodeOrNull<SearchlightZone>(nodeName) is not SearchlightZone light) return;
            _searchlights.Add(light);
            light.PlayerDetected += _ => OnSearchlightAlarm();
        }

        private void AddPrisoner(string nodePath) {
            if (GetNodeOrNull<RescuableNPC>(nodePath) is not RescuableNPC prisoner) return;
            _prisoners.Add(prisoner);
            prisoner.Rescued += _ => OnPrisonerFreed();
        }

        private void AddCellLock(string nodePath) {
            if (GetNodeOrNull<DestructibleBlock>(nodePath) is not DestructibleBlock lockBlock) return;
            _cellLocks.Add(lockBlock);
        }

        /// <summary>Room 1: the crowd at the gates. Wide, flat, barricade platforms.</summary>
        private void BuildGatehouseYard() {
            BuildRoomBackground("BG_Room1_Gatehouse", Room1StartX, 3200, LevelHeight,
                new Color(0.11f, 0.09f, 0.10f, 0.4f));

            BuildFloor(Room1StartX, GroundY, 3200);
            BuildPlatform(700, 740, 280);
            BuildPlatform(1250, 620, 240);
            BuildPlatform(1800, 730, 260);
            BuildPlatform(2500, 700, 300);   // barricade holding extractor 1
            BuildPlatform(3000, 560, 220);

            BuildHazardSpikes(1550, 890, 130);
            BuildHazardSpikes(2200, 890, 110);

            BuildCheckpoint(240, GroundY - 50, $"{LevelID}_checkpoint_0", CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "paris_room_gatehouse", new Color(0.85f, 0.55f, 0.25f));

            BuildRoomTransition("paris_room_gatehouse", new Vector2(560, 600),
                new Rect2(Room1StartX, 0, 3200, LevelHeight));
        }

        /// <summary>
        /// Room 2: the era beat. Two ground-level beams gate the corridor, then a
        /// vertical tower climb on drop-through platforms ends on an upper walkway
        /// under the third beam. The searchlights themselves are authored in the
        /// scene; this only builds the geometry that makes timing them matter.
        /// </summary>
        private void BuildSecurityCorridors() {
            BuildRoomBackground("BG_Room2_Corridors", Room2StartX, 2880, LevelHeight,
                new Color(0.08f, 0.08f, 0.12f, 0.45f));

            BuildFloor(Room2StartX, GroundY, 2880);

            // Low cover between the two ground beams: the safe pockets to wait in.
            BuildPlatform(3400, 780, 200);
            BuildPlatform(4020, 760, 220);
            BuildPlatform(4660, 780, 200);

            // Bastille tower: the level's vertical section, drop-through on the way down.
            BuildOneWayPlatform(4900, 760, 220);
            BuildOneWayPlatform(5150, 620, 220);
            BuildOneWayPlatform(4950, 480, 220);
            BuildOneWayPlatform(5200, 380, 200);

            // Upper walkway, swept end to end by the tower beam.
            BuildPlatform(5540, 300, 1080);

            BuildCheckpoint(5000, GroundY - 50, $"{LevelID}_checkpoint_1", CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "paris_room_searchlights", new Color(0.95f, 0.88f, 0.45f));

            BuildRoomTransition("paris_room_searchlights", new Vector2(Room2StartX, 600),
                new Rect2(Room2StartX, 0, 2880, LevelHeight));

            BuildWaveTrigger("Room2WaveTrigger", new Vector2(Room2StartX + 120, 800), () => {
                if (_room2WaveSpawned) return;
                _room2WaveSpawned = true;
                SpawnWave(Room2Spawns);
            });
        }

        /// <summary>
        /// Room 3: two sealed cells. Each cell is a walled alcove whose only opening
        /// is filled floor-to-ceiling by a destructible lock mechanism, so the
        /// prisoner's interaction area is physically unreachable until the lock is
        /// blown. Freeing both satisfies the puzzle and opens the courtyard gate.
        /// </summary>
        private void BuildCellBlock() {
            BuildRoomBackground("BG_Room3_Cells", Room3StartX, 2560, LevelHeight,
                new Color(0.10f, 0.08f, 0.09f, 0.5f));

            BuildFloor(Room3StartX, GroundY, 2560);
            BuildPlatform(6300, 700, 200);
            BuildPlatform(7300, 660, 220);
            BuildPlatform(8300, 700, 200);

            BuildCellShell(6820);
            BuildCellShell(7820);

            BuildCheckpoint(8480, GroundY - 50, $"{LevelID}_checkpoint_2", CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "paris_room_cells", new Color(0.7f, 0.45f, 0.5f));

            _courtyardGate = BuildDoor("CourtyardGate", new Vector2(8560, GroundY),
                "paris_gate_locked", new Vector2(48, 760), new Color(0.38f, 0.30f, 0.22f));

            BuildRoomTransition("paris_room_cells", new Vector2(Room3StartX, 600),
                new Rect2(Room3StartX, 0, 2560, LevelHeight));

            BuildWaveTrigger("Room3WaveTrigger", new Vector2(Room3StartX + 140, 800), () => {
                if (_room3WaveSpawned) return;
                _room3WaveSpawned = true;
                SpawnWave(Room3Spawns);
            });
        }

        /// <summary>
        /// Cell walls for one prisoner alcove. The ceiling slab spans the cell and
        /// the far wall closes it, leaving a single 96 px doorway on the left that
        /// the authored <see cref="DestructibleBlock"/> fills from floor to ceiling.
        /// </summary>
        private void BuildCellShell(float centerX) {
            BuildPlatform(centerX, 604, 440);              // ceiling slab, 596-612
            BuildWall(centerX + 200, 604, 296);            // far wall, down to the floor
        }

        /// <summary>
        /// Room 4: the boss arena. Two drawbridge walkways ride above a single flat
        /// pit floor - the Tribunal's summoned rioters need a continuous surface to
        /// land on and path along, so the pit is deliberately not a hazard.
        /// </summary>
        private void BuildInnerCourtyard() {
            BuildRoomBackground("BG_Room4_Courtyard", Room4StartX, 1600, LevelHeight,
                new Color(0.16f, 0.06f, 0.07f, 0.5f));

            BuildFloor(Room4StartX, PitFloorY, 1600);

            BuildPlatform(9000, 780, 480);   // drawbridge walkway west
            BuildPlatform(9760, 780, 480);   // drawbridge walkway east

            // Back wall only above the entry drop, so the arena reads closed
            // without sealing the player out of room three.
            BuildWall(Room4StartX - 20, 240, 460);
            BuildWall(10220, 240, 760);

            BuildRoomDecoration(Room4StartX, "paris_room_courtyard", new Color(0.95f, 0.25f, 0.25f));

            BuildRoomTransition("paris_room_courtyard", new Vector2(Room4StartX + 60, 600),
                new Rect2(Room4StartX, 0, 1600, LevelHeight));

            BuildBossEncounter(BossResourcePath, new Vector2(9400, 950),
                encounterName: "RevolutionaryTribunalEncounter", revealDistance: 800f);
        }

        // === Encounters ===

        protected override void SpawnInitialEnemies() => SpawnWave(Room1Spawns);

        private void SpawnWave((string EnemyID, Vector2 Position)[] authored) {
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                authored.Length, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < authored.Length; index++) {
                (string enemyID, Vector2 position) = authored[index];
                EnemyFactory.Spawn(enemyID, this, position,
                    waypointA: position + new Vector2(-200, 0),
                    waypointB: position + new Vector2(200, 0));
            }
        }

        // Package 12 W2 (GAP-13): explicit encounter baseline. Paris has no
        // non-encounter anchor state, so MarkWavesClearedThrough is no longer
        // overridden.

        private const string ParisLevelID = "level_04_paris";
        public const string Room2WaveEncounter = ParisLevelID + "_room2_wave";
        public const string Room3WaveEncounter = ParisLevelID + "_room3_wave";

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [ParisLevelID + "_checkpoint_0"] = System.Array.Empty<string>(),
                [ParisLevelID + "_checkpoint_1"] = new[] { Room2WaveEncounter },
                [ParisLevelID + "_checkpoint_2"] = new[] { Room2WaveEncounter, Room3WaveEncounter }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            if (encounterID == Room2WaveEncounter) _room2WaveSpawned = true;
            else if (encounterID == Room3WaveEncounter) _room3WaveSpawned = true;
        }

        // === Era mechanics ===

        protected override void OnLevelReady() {
            // The base already confines the camera on a resume
            // (StoryLevelControllerBase.ApplyResumeCameraBounds). Paris additionally
            // marks the starting room's trigger as crossed so it cannot re-fire, and
            // enables its encounter root - hence the full ActivateRoom, on every
            // entry rather than only on a resume.
            FindRoomTriggerContaining(Player?.Position.X ?? PlayerSpawnPosition.X)?.ActivateRoom(Player);

            // Redundant since PuzzleManager re-emits a save-restored completion
            // (Wave A integration), but kept: it is also what sets PrisonersFreed.
            if (_prisonerPuzzle is { IsCompleted: true }) {
                PrisonersFreed = _prisoners.Count;
                OpenCourtyardGate();
            } else {
                PostPrisonerObjective();
            }
        }

        /// <summary>A beam catching the player is a warning beat, not an extra wave.</summary>
        private void OnSearchlightAlarm() {
            SearchlightAlarmCount++;
            SetObjective("paris_objective_searchlight_alarm");
        }

        private void OnPrisonerFreed() {
            PrisonersFreed++;
            if (_prisonerPuzzle == null || !_prisonerPuzzle.IsCompleted) PostPrisonerObjective();
        }

        private void PostPrisonerObjective() =>
            SetObjective("paris_objective_free_prisoners", PrisonersFreed, _prisoners.Count);

        /// <summary>The freed prisoners haul the inner-courtyard gate open. Idempotent.</summary>
        private void OpenCourtyardGate() {
            if (CourtyardGateOpen) return;
            CourtyardGateOpen = true;
            OpenDoor(ref _courtyardGate);
            SetObjective("paris_objective_reach_courtyard");
        }
    }
}
