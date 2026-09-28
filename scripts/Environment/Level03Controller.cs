using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 3 - Chicago, 1893 (World's Columbian Exposition). Four rooms across
    /// ~10,240 px: the fairground Midway, a vertical climb through the Electricity
    /// Building, the coil-routing hall that gates the Court of Honor, and the
    /// Chronal Inventor's arena.
    ///
    /// Era identity is the design's logic/routing puzzle (design-godot.md 2448-2451):
    /// a fixed <see cref="BeamEmitter"/> feeds a chain of three rotatable
    /// <see cref="ConductiveCoil"/>s that must each be re-aimed before the beam
    /// reaches the <see cref="BeamReceiver"/>. Every wrong orientation dumps the
    /// siphoned current into a crowd tap instead, arming the coil-discharge hazard
    /// over the spectator stands - the "route it away from the crowds" fantasy made
    /// mechanical, and the read-plan-execute tell that shows the player the goal.
    ///
    /// Locked encounter economy: 8 standards, 0 elites, 1 boss, 3 extractors.
    /// </summary>
    public partial class Level03Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ChicagoLevelID = "level_03_chicago";
        public const string BeamPuzzleID = "level_03.beam_routing";
        public const string BossResourcePath = "res://resources/Bosses/chronal_inventor.tres";

        public override string LevelID => ChicagoLevelID;
        public override CampaignLevel Level => CampaignLevel.Chicago;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 6 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 540f;
        public override string LevelTitleKey => "chicago_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_03_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240, GroundY - 50);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "chicago_objective_fairgrounds";
        protected override string BossObjectiveKey => "chicago_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "chicago_objective_complete";

        // Fair-at-night palette: dark blue steel and arc-lamp white.
        protected override Color FloorColor => new(0.17f, 0.19f, 0.26f);
        protected override Color FloorEdgeColor => new(0.55f, 0.72f, 0.86f);
        protected override Color PlatformColor => new(0.24f, 0.27f, 0.35f);
        protected override Color WallColor => new(0.12f, 0.14f, 0.2f);

        // === Layout ===

        public const float LevelWidth = 10240f;
        public const float LevelHeight = 1600f;
        public const float GroundY = 1400f;

        public const float MidwayStartX = 0f;
        public const float ElectricityStartX = 3840f;
        public const float RoutingStartX = 6400f;
        public const float BossStartX = 8320f;

        /// <summary>Bottom-anchored 1080-tall camera window for the ground-level rooms.</summary>
        private const float GroundRoomCameraTop = LevelHeight - 1080f;

        public const string CheckpointEntry = ChicagoLevelID + "_checkpoint_0";
        public const string CheckpointMid = ChicagoLevelID + "_checkpoint_1";
        public const string CheckpointPreBoss = ChicagoLevelID + "_checkpoint_2";

        /// <summary>The three checkpoint IDs in traversal order.</summary>
        public static readonly string[] CheckpointIDs = {
            CheckpointEntry, CheckpointMid, CheckpointPreBoss
        };

        // === Authored encounter table (locked: 8 standards, 0 elites) ===

        /// <summary>
        /// One authored spawn. <paramref name="PatrolRadius"/> above zero gives the
        /// enemy a horizontal patrol of that half-width around its spawn.
        /// </summary>
        public readonly record struct EnemySpawn(string EnemyID, Vector2 Position, float PatrolRadius);

        /// <summary>Room 1 - Midway. Drones strafe the arc lamps, a Unbound trooper works the crowd.</summary>
        public static readonly EnemySpawn[] MidwayWave = {
            new("voltaic_shock_drone", new Vector2(1100, 980), 260f),
            new("chrono_slasher", new Vector2(1900, GroundY - 50), 300f),
            new("voltaic_shock_drone", new Vector2(2900, 900), 300f)
        };

        /// <summary>Room 2 - Electricity Building climb.</summary>
        public static readonly EnemySpawn[] ElectricityWave = {
            new("chrono_slasher", new Vector2(4300, GroundY - 50), 320f),
            new("voltaic_shock_drone", new Vector2(4600, 640), 240f),
            new("chrono_slasher", new Vector2(5240, 340), 0f)
        };

        /// <summary>Room 3 - Coil routing hall. Deliberately light: the puzzle is the room.</summary>
        public static readonly EnemySpawn[] RoutingHallWave = {
            new("voltaic_shock_drone", new Vector2(7200, 700), 320f),
            new("chrono_slasher", new Vector2(7900, GroundY - 50), 280f)
        };

        /// <summary>Every authored standard-enemy spawn in the level, in wave order.</summary>
        public static IEnumerable<EnemySpawn> AllAuthoredSpawns {
            get {
                foreach (EnemySpawn spawn in MidwayWave) yield return spawn;
                foreach (EnemySpawn spawn in ElectricityWave) yield return spawn;
                foreach (EnemySpawn spawn in RoutingHallWave) yield return spawn;
            }
        }

        /// <summary>Authored extractor placements; each one guards a side path.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            // Midway rooftop ledge, reachable only off the one-way awning.
            ("level_03_extractor_0", new Vector2(2100, 998)),
            // Crown of the Electricity Building climb.
            ("level_03_extractor_1", new Vector2(5240, 300)),
            // Gallery catwalk between the two upper coils.
            ("level_03_extractor_2", new Vector2(7360, 880))
        };

        // === Runtime ===

        private PuzzleManager _beamPuzzle;
        private StaticBody2D _courtDoor;
        private bool _midwayWaveSpawned;
        private bool _electricityWaveSpawned;
        private bool _routingWaveSpawned;

        private readonly List<(PowerRoutingNode Tap, StoryCyclicHazard Hazard)> _crowdTaps = new();

        /// <summary>The beam-routing puzzle authored in the scene. Null outside the scene.</summary>
        public PuzzleManager BeamPuzzle => _beamPuzzle;

        /// <summary>True once the routing puzzle has opened the Court of Honor door.</summary>
        public bool CourtDoorOpen => _courtDoor == null || !IsInstanceValid(_courtDoor);

        // === Construction ===

        protected override void BuildLevel() {
            BuildMidway();
            BuildElectricityBuilding();
            BuildRoutingHall();
            BuildCourtOfHonor();
            BuildExtractors(ExtractorPlacements);
        }

        private void BuildMidway() {
            float width = ElectricityStartX - MidwayStartX;
            BuildRoomBackground("BG_Midway", MidwayStartX, width, LevelHeight, new Color(0.06f, 0.08f, 0.14f, 0.55f));
            BuildFloor(MidwayStartX, GroundY, width);
            BuildWall(MidwayStartX, 0, LevelHeight);

            BuildPlatform(500, 1240, 300);
            BuildPlatform(1000, 1100, 260);
            BuildPlatform(1500, 1240, 280);
            BuildPlatform(2650, 1220, 300);
            BuildPlatform(3200, 1060, 240);
            // Awning route up to the extractor ledge.
            BuildOneWayPlatform(1750, 1080, 260);
            BuildOneWayPlatform(2100, 1080, 300);

            BuildCheckpoint(240, GroundY - 50, CheckpointEntry, CheckpointRole.Entry);
            BuildRoomDecoration(MidwayStartX, "chicago_room_midway", new Color(0.6f, 0.82f, 1f));

            BuildWaveTrigger("MidwayWaveTrigger", new Vector2(760, GroundY - 200), () => {
                if (_midwayWaveSpawned) return;
                _midwayWaveSpawned = true;
                SpawnWave(MidwayWave);
            }, new Vector2(60, 600));
        }

        /// <summary>Room 2 - the level's vertical section, a climb up the dynamo hall.</summary>
        private void BuildElectricityBuilding() {
            float width = RoutingStartX - ElectricityStartX;
            BuildRoomBackground("BG_Electricity", ElectricityStartX, width, LevelHeight,
                new Color(0.05f, 0.1f, 0.16f, 0.6f));
            BuildFloor(ElectricityStartX, GroundY, width);

            // Ascent.
            BuildOneWayPlatform(4100, 1240, 240);
            BuildOneWayPlatform(4520, 1080, 220);
            BuildOneWayPlatform(4160, 920, 220);
            BuildOneWayPlatform(4600, 760, 240);
            BuildOneWayPlatform(4220, 600, 220);
            BuildOneWayPlatform(4720, 440, 260);
            BuildPlatform(5240, 380, 340);
            // Descent back to the fairground floor on the east side.
            BuildPlatform(5620, 560, 240);
            BuildPlatform(5960, 820, 240);
            BuildPlatform(6240, 1080, 220);

            BuildCheckpoint(6180, GroundY - 50, CheckpointMid, CheckpointRole.Middle);
            BuildRoomDecoration(ElectricityStartX, "chicago_room_electricity", new Color(0.5f, 0.9f, 1f));

            BuildRoomTransitionPair(
                ElectricityStartX, "chicago_room_electricity", "chicago_room_midway",
                new Rect2(ElectricityStartX, 0, width, LevelHeight),
                new Rect2(MidwayStartX, GroundRoomCameraTop, ElectricityStartX - MidwayStartX, 1080));

            BuildWaveTrigger("ElectricityWaveTrigger", new Vector2(3990, GroundY - 200), () => {
                if (_electricityWaveSpawned) return;
                _electricityWaveSpawned = true;
                SpawnWave(ElectricityWave);
            }, new Vector2(60, 600));
        }

        /// <summary>
        /// Room 3 - the coil routing hall. The beam chain itself is authored in the
        /// scene (see Level_03_Chicago.tscn); this builds the traversal geometry that
        /// reaches each coil, plus the Court of Honor door the puzzle opens.
        /// </summary>
        private void BuildRoutingHall() {
            float width = BossStartX - RoutingStartX;
            BuildRoomBackground("BG_RoutingHall", RoutingStartX, width, LevelHeight,
                new Color(0.07f, 0.07f, 0.13f, 0.6f));
            BuildFloor(RoutingStartX, GroundY, width);

            // Catwalk up to the two upper coils, then the gallery bridge between them.
            BuildOneWayPlatform(6800, 1240, 200);
            BuildOneWayPlatform(6950, 1100, 180);
            BuildPlatform(7060, 960, 280);
            BuildPlatform(7360, 960, 400);
            BuildPlatform(7660, 960, 280);

            BuildRoomDecoration(RoutingStartX, "chicago_room_routing", new Color(0.9f, 0.75f, 0.35f));

            // Tall enough that the gallery catwalk at y=960 cannot be used to hop
            // the gate: the door top sits at y=680, well above the coil platforms.
            _courtDoor = BuildDoor("CourtOfHonorDoor", new Vector2(BossStartX - 20, GroundY),
                "chicago_door_sealed", new Vector2(40, 720), new Color(0.3f, 0.36f, 0.46f));

            BuildRoomTransitionPair(
                RoutingStartX, "chicago_room_routing", "chicago_room_electricity",
                new Rect2(RoutingStartX, GroundRoomCameraTop, width, 1080),
                new Rect2(ElectricityStartX, 0, RoutingStartX - ElectricityStartX, LevelHeight),
                onForwardEntered: _ => {
                    if (_beamPuzzle == null || !_beamPuzzle.IsCompleted) {
                        SetObjective("chicago_objective_reroute");
                    }
                });

            BuildWaveTrigger("RoutingWaveTrigger", new Vector2(6560, GroundY - 200), () => {
                if (_routingWaveSpawned) return;
                _routingWaveSpawned = true;
                SpawnWave(RoutingHallWave);
            }, new Vector2(60, 600));
        }

        /// <summary>
        /// Room 4 - the Court of Honor. Flat metallic stage between two high
        /// conductor coils, sized so the Inventor's 10-unit (600 px) ranged band
        /// and 2.5-unit melee band both have room.
        /// </summary>
        private void BuildCourtOfHonor() {
            float width = LevelWidth - BossStartX;
            BuildRoomBackground("BG_CourtOfHonor", BossStartX, width, LevelHeight,
                new Color(0.12f, 0.06f, 0.09f, 0.6f));
            BuildFloor(BossStartX, GroundY, width, new Color(0.26f, 0.28f, 0.33f));
            BuildWall(LevelWidth - DefaultWallThickness, 0, LevelHeight);

            // The two conductor-coil side platforms from the design's arena note.
            BuildPlatform(8720, 1060, 300, new Color(0.42f, 0.34f, 0.18f));
            BuildPlatform(9840, 1060, 300, new Color(0.42f, 0.34f, 0.18f));

            BuildCheckpoint(8420, GroundY - 50, CheckpointPreBoss, CheckpointRole.PreBoss);
            BuildRoomDecoration(BossStartX, "chicago_room_boss", new Color(1f, 0.45f, 0.35f));

            BuildRoomTransition("chicago_room_boss", new Vector2(BossStartX + 80, GroundY - 400),
                new Rect2(BossStartX, GroundRoomCameraTop, width, 1080));

            BuildBossEncounter(BossResourcePath, new Vector2(9800, GroundY - 50),
                "ChronalInventorEncounter", revealDistance: 800f);
        }

        /// <summary>
        /// Two facing camera triggers on one room boundary so backtracking restores
        /// the previous room's confinement instead of leaving the camera clamped.
        /// Both are repeatable; their 80 px bodies sit either side of the seam and
        /// never overlap.
        /// </summary>
        private void BuildRoomTransitionPair(
            float boundaryX,
            string forwardRoomID,
            string backRoomID,
            Rect2 forwardBounds,
            Rect2 backBounds,
            Action<string> onForwardEntered = null) {
            RoomTransitionTrigger forward = BuildRoomTransition(
                forwardRoomID, new Vector2(boundaryX + 80, GroundY - 500), forwardBounds,
                new Vector2(80, 1400), onForwardEntered);
            forward.ActivateOnce = false;

            RoomTransitionTrigger back = BuildRoomTransition(
                backRoomID, new Vector2(boundaryX - 80, GroundY - 500), backBounds,
                new Vector2(80, 1400));
            back.Name = $"RoomTransition_{backRoomID}_return";
            back.ActivateOnce = false;
        }

        // === Beam routing puzzle ===

        protected override void OnLevelReady() {
            _beamPuzzle = GetNodeOrNull<PuzzleManager>("BeamRoutingPuzzle");
            if (_beamPuzzle == null) {
                GD.PushError("Level 3: the authored 'BeamRoutingPuzzle' node is missing from the scene.");
                return;
            }
            _beamPuzzle.PuzzleCompleted += OnBeamPuzzleCompleted;

            BindCrowdTap("CrowdTapA", "CrowdArcA");
            BindCrowdTap("CrowdTapB", "CrowdArcB");

            // The save may already carry the completion (PersistCompletionToSave),
            // and a run resumed at the pre-boss checkpoint stands PAST the door.
            // Either way the gate has to be open or the player is walled in.
            if (_beamPuzzle.IsCompleted || ResumedCheckpointID == CheckpointPreBoss) {
                OpenCourtDoor(announce: false);
            }
        }

        /// <summary>
        /// A crowd tap is a dead-end routing node standing in for the spectator
        /// stands: while the beam feeds it, the discharge hazard beneath it is live.
        /// The initial mis-aim energizes one, which is how the player reads the room.
        /// </summary>
        private void BindCrowdTap(string tapName, string hazardName) {
            var tap = GetNodeOrNull<PowerRoutingNode>($"BeamRoutingPuzzle/{tapName}");
            var hazard = GetNodeOrNull<StoryCyclicHazard>(hazardName);
            if (tap == null || hazard == null) return;
            _crowdTaps.Add((tap, hazard));
            tap.PowerChanged += powered => ApplyCrowdTapState(hazard, powered);
            // The taps settle during their own _Ready, before this wiring exists.
            ApplyCrowdTapState(hazard, tap.IsPowered);
        }

        private static void ApplyCrowdTapState(StoryCyclicHazard hazard, bool powered) {
            if (!IsInstanceValid(hazard)) return;
            hazard.Enabled = powered;
            if (!powered) hazard.ForcePhase(HazardPhase.Cooldown, hazard.CooldownDuration);
        }

        private void OnBeamPuzzleCompleted(string puzzleID) => OpenCourtDoor(announce: true);

        private void OpenCourtDoor(bool announce) {
            bool opened = OpenDoor(ref _courtDoor);
            if (announce && opened) SetObjective("chicago_objective_reach_boss");
        }

        /// <summary>
        /// Test/diagnostic entry point: drives the authored coils to the solution
        /// exactly as three player interactions would.
        /// </summary>
        public bool SolveBeamPuzzleForTest() {
            var coilA = GetNodeOrNull<ConductiveCoil>("BeamRoutingPuzzle/CoilA");
            var coilB = GetNodeOrNull<ConductiveCoil>("BeamRoutingPuzzle/CoilB");
            var coilC = GetNodeOrNull<ConductiveCoil>("BeamRoutingPuzzle/CoilC");
            if (coilA == null || coilB == null || coilC == null) return false;
            coilA.SetQuarterTurns(3); // north, up the riser to coil B
            coilB.SetQuarterTurns(0); // east, along the gallery to coil C
            coilC.SetQuarterTurns(1); // south, down into the receiver
            return true;
        }

        // === Encounters ===

        // Package 12 W2 (GAP-13): the explicit encounter baseline replaces the
        // old checkpoint switch. Chicago has no non-encounter anchor state, so
        // MarkWavesClearedThrough is no longer overridden.

        public const string MidwayWaveEncounter = ChicagoLevelID + "_midway_wave";
        public const string ElectricityWaveEncounter = ChicagoLevelID + "_electricity_wave";
        public const string RoutingWaveEncounter = ChicagoLevelID + "_routing_wave";

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [CheckpointEntry] = Array.Empty<string>(),
                [CheckpointMid] = new[] { MidwayWaveEncounter },
                [CheckpointPreBoss] = new[] { MidwayWaveEncounter, ElectricityWaveEncounter, RoutingWaveEncounter }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            switch (encounterID) {
                case MidwayWaveEncounter: _midwayWaveSpawned = true; break;
                case ElectricityWaveEncounter: _electricityWaveSpawned = true; break;
                case RoutingWaveEncounter: _routingWaveSpawned = true; break;
            }
        }

        /// <summary>
        /// Authored counts are the Normal-difficulty budget; Hard can ask for more
        /// than the table holds, so the table length is the hard ceiling.
        /// </summary>
        private void SpawnWave(EnemySpawn[] wave) {
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                wave.Length, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < wave.Length; index++) {
                EnemySpawn spawn = wave[index];
                Vector2? patrolA = null;
                Vector2? patrolB = null;
                if (spawn.PatrolRadius > 0f) {
                    patrolA = spawn.Position - new Vector2(spawn.PatrolRadius, 0);
                    patrolB = spawn.Position + new Vector2(spawn.PatrolRadius, 0);
                }
                EnemyFactory.Spawn(spawn.EnemyID, this, spawn.Position, patrolA, patrolB);
            }
        }
    }
}
