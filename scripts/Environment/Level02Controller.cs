using Godot;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 2 - Orléans, 1429 (Siege of Orléans). Four rooms across 10,560 px:
    /// the French vanguard approach, the English siege line under a rolling mortar
    /// barrage, a vertical battlement climb, and the Siegemaster Duke's court.
    ///
    /// Era identity is mechanical, not decorative: the Apex Archive has given the
    /// English two kinetic <see cref="ShieldGeneratorTower"/>s, and each one keeps a
    /// <see cref="ForcefieldBarrier"/> solid across the only route forward. The
    /// player has to break the generators to advance, which is the sabotage the
    /// design asks for. Mortar fire is a lane of telegraphed
    /// <see cref="StoryCyclicHazard"/>s (warning -> active -> cooldown), authored in
    /// the scene rather than in code.
    ///
    /// Locked encounter economy (docs/DUST_ECONOMY.md, DustEconomyTests row 2):
    /// 8 standards / 0 elites / 1 boss / 3 extractors. The authored tables below are
    /// the single source of those counts and Level02ContentTests asserts them.
    /// </summary>
    public partial class Level02Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_02_orleans";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/siegemaster_duke.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_02_dialogue.tres";

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.Orleans;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "orleans_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(200, 850);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "orleans_objective_advance";
        protected override string BossObjectiveKey => "orleans_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "orleans_objective_complete";

        // === Layout ===

        public const float LevelWidth = 10560f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float EnemyGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 3200f;
        private const float Room3StartX = 6400f;
        private const float Room4StartX = 8960f;

        /// <summary>Battlement walkway surface in room three; the only route east.</summary>
        private const float WalkwayY = 560f;

        /// <summary>Shield gate columns: the barrier plane each generator holds up.</summary>
        public const float GateAX = 6300f;
        public const float GateBX = 8700f;

        /// <summary>
        /// ShieldGeneratorTowerTemplate hangs its barrier at a fixed local offset, so
        /// a generator authored in the scene has to stand exactly this far west of the
        /// gate it seals. Level02ContentTests pins the scene positions against it.
        /// </summary>
        public const float BarrierLocalOffsetX = 320f;
        public const float TowerAX = GateAX - BarrierLocalOffsetX;
        public const float TowerBX = GateBX - BarrierLocalOffsetX;
        public const float TowerAY = 790f;
        public const float TowerBY = 440f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 3200, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 3200, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0, 2560, LevelHeight);
        // Narrower than a 1920 viewport otherwise, which makes the confiner useless.
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX - 320f, 0, 1920, LevelHeight);

        public const int TowerCount = 2;

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>
        /// Era roster: `laser_archer` (Orléans standard, ranged) mixed with
        /// `chrono_slasher` (Future Cultist standard, melee). No elites - the locked
        /// row for level 2 is E = 0.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room1Spawns = {
            ("laser_archer", new Vector2(1100, EnemyGroundY), new Vector2(950, EnemyGroundY), new Vector2(1250, EnemyGroundY)),
            ("chrono_slasher", new Vector2(1750, EnemyGroundY), new Vector2(1600, EnemyGroundY), new Vector2(1900, EnemyGroundY)),
            ("laser_archer", new Vector2(2600, EnemyGroundY), null, null)
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room2Spawns = {
            ("chrono_slasher", new Vector2(3900, EnemyGroundY), new Vector2(3750, EnemyGroundY), new Vector2(4050, EnemyGroundY)),
            ("laser_archer", new Vector2(4600, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(5400, EnemyGroundY), new Vector2(5250, EnemyGroundY), new Vector2(5550, EnemyGroundY))
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room3Spawns = {
            ("laser_archer", new Vector2(7100, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(7900, EnemyGroundY), new Vector2(7750, EnemyGroundY), new Vector2(8050, EnemyGroundY))
        };

        /// <summary>15 dust each and resource-owned; never overridden from level code.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_02.extractor_courtyard", new Vector2(2400, 580)),
            ("level_02.extractor_mortar_ledge", new Vector2(4750, 400)),
            ("level_02.extractor_battlement", new Vector2(8560, 260))
        };

        public static int AuthoredStandardCount =>
            Room1Spawns.Length + Room2Spawns.Length + Room3Spawns.Length;

        public static int AuthoredEliteCount => 0;

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === Runtime state ===

        private ShieldGeneratorTower _towerA;
        private ShieldGeneratorTower _towerB;
        private int _towersDown;
        private bool _room2WaveSpawned;
        private bool _room3WaveSpawned;
        private bool _siegeLineReached;

        public int TowersDestroyed => _towersDown;
        public ShieldGeneratorTower ShieldTowerA => _towerA;
        public ShieldGeneratorTower ShieldTowerB => _towerB;

        // === Palette: sunbaked stone, scorched earth, Archive cyan ===

        protected override Color FloorColor => new(0.21f, 0.19f, 0.15f);
        protected override Color FloorEdgeColor => new(0.46f, 0.40f, 0.27f);
        protected override Color PlatformColor => new(0.34f, 0.31f, 0.24f);
        protected override Color WallColor => new(0.15f, 0.14f, 0.11f);
        protected override Color HazardColor => new(0.62f, 0.24f, 0.10f);

        private static readonly Color GateColor = new(0.24f, 0.32f, 0.40f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1VanguardApproach();
            BuildRoom2SiegeLine();
            BuildRoom3BattlementClimb();
            BuildRoom4BossCourt();
            BuildExtractors(ExtractorPlacements);
            WireShieldTowers();
        }

        /// <summary>Room 1: the French vanguard's ruined approach. No gate, no waves past the opener.</summary>
        private void BuildRoom1VanguardApproach() {
            BuildRoomBackground("BG_Room1", Room1StartX, 3200, LevelHeight, new Color(0.11f, 0.09f, 0.06f, 0.35f));
            BuildFloor(Room1StartX, GroundY, 3200);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(500, 720, 260);
            BuildPlatform(900, 570, 220);
            BuildPlatform(1350, 700, 240);
            BuildPlatform(1900, 540, 240);
            BuildPlatform(2400, 660, 260);
            BuildPlatform(2850, 520, 220);

            BuildHazardSpikes(1600, 890, 140);

            BuildCheckpoint(200, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "orleans_room_vanguard", new Color(0.85f, 0.72f, 0.35f));
            BuildRoomTransition("orleans_room_vanguard", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the English siege line. A four-emplacement mortar lane (authored
        /// in the scene) plus generator A, whose barrier seals the eastern gate.
        /// </summary>
        private void BuildRoom2SiegeLine() {
            BuildRoomBackground("BG_Room2", Room2StartX, 3200, LevelHeight, new Color(0.09f, 0.08f, 0.06f, 0.4f));
            BuildFloor(Room2StartX, GroundY, 3200);

            BuildPlatform(3500, 700, 220);
            BuildPlatform(4000, 600, 200);
            BuildPlatform(4500, 700, 220);
            BuildPlatform(4750, 480, 200);
            BuildPlatform(5000, 600, 200);
            BuildPlatform(5500, 700, 240);

            BuildCheckpoint(5300, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "orleans_room_siege_line", new Color(0.9f, 0.55f, 0.25f));

            // The barrier covers y 582..902; this arch seals everything above it, so
            // the gate cannot be jumped while generator A still stands.
            BuildWall(GateAX, 0, 600, GateColor);
            BuildGateLabel("GateALabel", new Vector2(GateAX, 540));

            BuildRoomTransition("orleans_room_siege_line", new Vector2(Room2StartX, 600), Room2CameraBounds,
                onEntered: _ => {
                    _siegeLineReached = true;
                    RefreshObjective();
                });
            BuildWaveTrigger("Room2WaveTrigger", new Vector2(3400, 700), TriggerRoom2Wave);
        }

        /// <summary>
        /// Room 3: the vertical section. The ground route dead-ends in the battlement
        /// footing, so the only way east is a drop-through climb onto the walkway -
        /// and the walkway is sealed by generator B's barrier.
        /// </summary>
        private void BuildRoom3BattlementClimb() {
            BuildRoomBackground("BG_Room3", Room3StartX, 2560, LevelHeight, new Color(0.10f, 0.09f, 0.08f, 0.4f));
            BuildFloor(Room3StartX, GroundY, 2500);

            BuildOneWayPlatform(6900, 790, 200);
            BuildOneWayPlatform(7250, 700, 200);
            BuildOneWayPlatform(7650, 620, 200);
            BuildOneWayPlatform(8050, WalkwayY, 220);

            // Battlement walkway, 8300..8960. Solid: it is the floor of the gate.
            BuildPlatform(8630, WalkwayY, 660);
            // Side branch above the walkway holding the third extractor.
            BuildPlatform(8560, 340, 200);
            // Footing under the east end of the walkway: closes the ground route.
            BuildWall(8880, WalkwayY + DefaultPlatformThickness / 2f, LevelHeight - WalkwayY - DefaultPlatformThickness / 2f);

            // Generator B's barrier covers y 232..552; this seals 0..240 above it.
            BuildWall(GateBX, 0, 240, GateColor);
            BuildGateLabel("GateBLabel", new Vector2(GateBX, 200));

            BuildHazardSpikes(6750, 890, 120);

            BuildRoomDecoration(Room3StartX, "orleans_room_battlement", new Color(0.6f, 0.75f, 0.9f));
            BuildRoomTransition("orleans_room_battlement", new Vector2(Room3StartX + 20, 600), Room3CameraBounds);
            BuildWaveTrigger("Room3WaveTrigger", new Vector2(6600, 700), TriggerRoom3Wave);
        }

        /// <summary>
        /// Room 4: the battlement court. Flat floor plus three stone platforms in a
        /// triangle, 1,600 px wide so the Duke's 9.0 m ranged band (540 px) and
        /// 3.5 m melee band both have room to matter.
        /// </summary>
        private void BuildRoom4BossCourt() {
            BuildRoomBackground("BG_Room4_Boss", Room4StartX, 1600, LevelHeight, new Color(0.16f, 0.07f, 0.06f, 0.45f));
            BuildFloor(Room4StartX, GroundY, 1600);

            BuildPlatform(9500, 690, 240);
            BuildPlatform(10020, 690, 240);
            BuildPlatform(9760, 480, 220);

            BuildWall(10540, 0, LevelHeight);

            BuildCheckpoint(9060, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "orleans_room_boss", new Color(0.95f, 0.3f, 0.25f));
            BuildRoomTransition("orleans_room_boss", new Vector2(Room4StartX + 30, 540), Room4CameraBounds);

            BuildBossEncounter(BossResourcePath, new Vector2(10200, EnemyGroundY),
                "siegemaster_duke_encounter", revealDistance: 900f);
        }

        /// <summary>Placeholder gate signage; replaced with production art in Package 8.</summary>
        private Label BuildGateLabel(string name, Vector2 position) {
            var label = new Label {
                Name = name,
                Text = Tr("orleans_gate_shielded"),
                Position = position + new Vector2(-150, 0),
                CustomMinimumSize = new Vector2(300, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 1f));
            AddChild(label);
            return label;
        }

        // === Shield generators ===

        private void WireShieldTowers() {
            _towerA = GetNodeOrNull<ShieldGeneratorTower>("ShieldTowerA");
            _towerB = GetNodeOrNull<ShieldGeneratorTower>("ShieldTowerB");
            NameBarriers(_towerA);
            NameBarriers(_towerB);
            if (_towerA != null) _towerA.TowerDestroyed += OnShieldTowerDestroyed;
            if (_towerB != null) _towerB.TowerDestroyed += OnShieldTowerDestroyed;
        }

        private static void NameBarriers(ShieldGeneratorTower tower) {
            if (tower == null) return;
            int index = 0;
            foreach (ForcefieldBarrier barrier in tower.Barriers()) {
                barrier.BarrierID = $"{tower.ObjectID}_barrier_{index++}";
            }
        }

        private void OnShieldTowerDestroyed(string towerID) {
            _towersDown++;
            RefreshObjective();
        }

        /// <summary>
        /// Resume helper: a run restored past a gate must not find that gate sealed
        /// again. Destroying the generator is the same code path the player takes, so
        /// the barriers drop and the objective count stays honest.
        /// </summary>
        private static void ForceGateOpen(ShieldGeneratorTower tower) {
            if (tower == null || !IsInstanceValid(tower) || tower.IsDestroyed) return;
            tower.TakeEnvironmentDamage(tower.MaxHP);
        }

        // === Encounters ===

        protected override void SpawnInitialEnemies() => SpawnWave(Room1Spawns);

        private void TriggerRoom2Wave() {
            if (_room2WaveSpawned) return;
            _room2WaveSpawned = true;
            SpawnWave(Room2Spawns);
        }

        private void TriggerRoom3Wave() {
            if (_room3WaveSpawned) return;
            _room3WaveSpawned = true;
            SpawnWave(Room3Spawns);
        }

        /// <summary>
        /// Authored counts are the Normal-difficulty truth; ScaleEncounterCount is the
        /// only thing allowed to move them, and it never exceeds the authored table.
        /// Concurrency stays at 3 or fewer live standards, well inside the level's
        /// warm pool counts (standard_enemy 10, enemy_projectile 18).
        /// </summary>
        private void SpawnWave((string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] wave) {
            if (wave == null) return;
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                wave.Length, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < wave.Length; index++) {
                (string enemyID, Vector2 position, Vector2? patrolA, Vector2? patrolB) = wave[index];
                EnemyFactory.Spawn(enemyID, this, position, patrolA, patrolB);
            }
        }

        // === Resume and objectives ===

        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint1) {
                _siegeLineReached = true;
                _room2WaveSpawned = true;
            } else if (checkpointID == Checkpoint2) {
                _siegeLineReached = true;
                _room2WaveSpawned = true;
                _room3WaveSpawned = true;
                ForceGateOpen(_towerA);
                ForceGateOpen(_towerB);
            }
        }

        // Resume camera confinement is handled by
        // StoryLevelControllerBase.ApplyResumeCameraBounds (Wave A integration).
        protected override void OnLevelReady() => RefreshObjective();

        private void RefreshObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (_towersDown >= TowerCount) SetObjective("orleans_objective_breach");
            else if (_siegeLineReached || _towersDown > 0) SetObjective("orleans_objective_towers", _towersDown, TowerCount);
            else SetObjective(InitialObjectiveKey);
        }
    }
}
