using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 6 - Pompeii, 79 AD (The Fall of Pompeii). Four rooms across 10,560 px:
    /// <list type="number">
    /// <item><b>The Forum</b> (0-2880) - first ash-fall over the colonnade. Opening
    ///       wave plus the first <see cref="RescuableNPC"/> civilian, which teaches
    ///       the rescue verb somewhere safe.</item>
    /// <item><b>Collapsed Vault</b> (2880-5760) - the weight puzzle. A Roman winch
    ///       with two stone pans (<see cref="PressurePlate"/>), a
    ///       <see cref="Counterweight"/>, and two volcanic boulders
    ///       (<see cref="WeightedObject"/>) hauls a rockfall off the eastern road
    ///       once both pans read the same load. Also the vertical section: the villa
    ///       terraces climb to the wedge gallery.</item>
    /// <item><b>The Ash Road</b> (5760-8960) - the level's signature. An
    ///       <see cref="EscapeSequenceController"/> lava front advances from behind
    ///       and the player runs east; the second civilian stands mid-corridor, so
    ///       the rescue costs seconds the player does not have. Being caught is heavy
    ///       damage and forward knockback, never a kill: Chronal Rewind stays the
    ///       meaningful failure state.</item>
    /// <item><b>Caldera Rim</b> (8960-10560) - the thermal anchor. Slanted rocky
    ///       slopes and two narrow stone ledges for the Vulcan Decimator.</item>
    /// </list>
    ///
    /// <para>
    /// Era roster: <c>shock_shield_legionnaire</c> (Pompeii standard) mixed with
    /// <c>chrono_slasher</c> (Unbound shock-trooper standard). The legionnaire carries
    /// <c>FrontalDamageReduction = 0.5</c>, so every legionnaire post is authored
    /// with a drop-through platform overhead and a patrol that turns its back: the
    /// geometry rewards attacking from above or from behind rather than trading
    /// blows into the shield.
    /// </para>
    ///
    /// <para>
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 10 standards,
    /// 0 elites, 1 boss, 3 extractors. The authored tables below are the single
    /// source of those counts and Level06ContentTests asserts them.
    /// </para>
    /// </summary>
    public partial class Level06Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_06_pompeii";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/vulcan_decimator.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_06_dialogue.tres";
        public const string RockfallPuzzleID = "level_06.clear_rockfall";
        public const string BalanceConditionID = "weights_balanced";
        /// <summary>Package 12 W8 (V01b): the winch puzzle's Reset Puzzle station.</summary>
        public const string RockfallResetStationID = "level_06.clear_rockfall.reset";
        public static readonly Vector2 RockfallResetStationPosition = new(3700f, 900f);

        // === Surface textures (Package 10 pass 1) ===

        private LevelSurfaceTextures _surfaceTextures;

        protected override LevelSurfaceTextures SurfaceTextures => _surfaceTextures ??= new LevelSurfaceTextures {
            Floor = new LevelSurfaceTextures.Entry {
                Texture = GD.Load<Texture2D>("res://assets/environments/pompeii/floor_stone.png"),
                MarginTop = 10, MarginBottom = 10
            },
            Platform = new LevelSurfaceTextures.Entry {
                Texture = GD.Load<Texture2D>("res://assets/environments/pompeii/platform_stone.png")
            },
            OneWay = new LevelSurfaceTextures.Entry {
                Texture = GD.Load<Texture2D>("res://assets/environments/pompeii/oneway_planks.png"),
                MarginLeft = 12, MarginRight = 12, MarginTop = 6, MarginBottom = 6
            },
            Wall = new LevelSurfaceTextures.Entry {
                Texture = GD.Load<Texture2D>("res://assets/environments/pompeii/wall_stone.png"),
                MarginLeft = 8, MarginRight = 8, MarginTop = 12, MarginBottom = 12,
                TileHorizontal = false, TileVertical = true
            }
        };

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.Pompeii;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "pompeii_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(240, 850);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "pompeii_objective_evacuate";
        protected override string BossObjectiveKey => "pompeii_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "pompeii_objective_complete";

        // === Layout ===

        public const float LevelWidth = 10560f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float EnemyGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 2880f;
        private const float Room3StartX = 5760f;
        private const float Room4StartX = 8960f;

        /// <summary>Volcanic rockfall sealing the eastern road out of the vault.</summary>
        public const float RockfallX = 5400f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2880, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2880, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0, 3200, LevelHeight);
        /// <summary>
        /// The caldera arena is 1,600 px of floor but the confiner is floored at the
        /// 1,920 px reference viewport - a window narrower than the screen clamps to
        /// nothing useful (L02 precedent). The 320 px overlap with room three is
        /// deliberate: the base resolves overlapping bounds to the last authored room,
        /// so a resume at checkpoint 2 lands in the arena.
        /// </summary>
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX - 320f, 0, 1920, LevelHeight);

        // === Escape sequence tuning (mirrored by the authored scene node) ===

        /// <summary>Lava front spawn, just west of the Ash Road entrance.</summary>
        public const float EscapeStartX = 5700f;
        /// <summary>Where the front stops - short of the caldera arena, on purpose.</summary>
        public const float EscapeEndX = 8800f;
        /// <summary>Crossing this clears the run.</summary>
        public const float EscapeFinishX = 8880f;

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>
        /// V7.1 design law, first actually placed in V7.6 (Package 11 A7a). The
        /// Chrono-Warden's authored debut is "first appearance Level 6, then
        /// salted through Levels 7-15 alongside the Tech-Enforcer", and until now
        /// it had never been spawned in any level at all (recon G8): the resource,
        /// its three abilities, its manifest row and its tests all existed with
        /// zero call sites.
        ///
        /// <para>This pushes Pompeii's authored elite count from 0 to 1, which the
        /// pre-F05 locked economy row did not budget. That row's flat per-tier
        /// award is retired by F05 in this same wave (A10 owns the reward
        /// manifest); the deviation is recorded in the plan's section 9.</para>
        ///
        /// <para>No Eraser is authored in Pompeii: the two elites may not share a
        /// room until Act III, and Level 6 is the Warden's level.</para>
        /// </summary>
        public const string WardenEnemyID = "chrono_warden";

        /// <summary>Forum: two legionnaires holding the colonnade, one Unbound trooper between them.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room1Spawns = {
            ("shock_shield_legionnaire", new Vector2(1000, EnemyGroundY), new Vector2(860, EnemyGroundY), new Vector2(1160, EnemyGroundY)),
            ("chrono_slasher", new Vector2(1750, EnemyGroundY), new Vector2(1600, EnemyGroundY), new Vector2(1900, EnemyGroundY)),
            ("shock_shield_legionnaire", new Vector2(2450, EnemyGroundY), new Vector2(2300, EnemyGroundY), new Vector2(2620, EnemyGroundY))
        };

        /// <summary>Collapsed vault: the escort on the winch floor.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room2Spawns = {
            ("shock_shield_legionnaire", new Vector2(3400, EnemyGroundY), new Vector2(3260, EnemyGroundY), new Vector2(3560, EnemyGroundY)),
            ("chrono_slasher", new Vector2(4620, EnemyGroundY), new Vector2(4460, EnemyGroundY), new Vector2(4780, EnemyGroundY)),
            ("shock_shield_legionnaire", new Vector2(5150, EnemyGroundY), new Vector2(5010, EnemyGroundY), new Vector2(5300, EnemyGroundY))
        };

        /// <summary>
        /// Ash Road: spread down the corridor so the lava keeps moving while the
        /// player fights. Four is the level's concurrency peak, well inside
        /// level_06_pool_config's standard_enemy warm count of 12.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room3Spawns = {
            ("chrono_slasher", new Vector2(6200, EnemyGroundY), new Vector2(6060, EnemyGroundY), new Vector2(6360, EnemyGroundY)),
            ("shock_shield_legionnaire", new Vector2(6900, EnemyGroundY), new Vector2(6760, EnemyGroundY), new Vector2(7060, EnemyGroundY)),
            ("chrono_slasher", new Vector2(7700, EnemyGroundY), new Vector2(7560, EnemyGroundY), new Vector2(7860, EnemyGroundY)),
            ("shock_shield_legionnaire", new Vector2(8400, EnemyGroundY), new Vector2(8260, EnemyGroundY), new Vector2(8560, EnemyGroundY)),
            // The Warden holds the last stretch of the Ash Road, where a Dilation
            // Field on a lava-timed corridor is the whole point of the enemy.
            (WardenEnemyID, new Vector2(8050, EnemyGroundY), new Vector2(7900, EnemyGroundY), new Vector2(8200, EnemyGroundY))
        };

        /// <summary>Every authored standard spawn in room order. Must total exactly 10.</summary>
        public static IEnumerable<(string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)> AllStandardSpawns {
            get {
                foreach (var spawn in Room1Spawns) yield return spawn;
                foreach (var spawn in Room2Spawns) yield return spawn;
                foreach (var spawn in Room3Spawns) yield return spawn;
            }
        }

        /// <summary>15 dust each and resource-owned; never overridden from level code.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_06.extractor_forum", new Vector2(1720, 500)),
            ("level_06.extractor_villa", new Vector2(4980, 280)),
            ("level_06.extractor_ashroad", new Vector2(7400, 420))
        };

        /// <summary>Authored spawns of standard tier. Elites are counted separately.</summary>
        public static int AuthoredStandardCount {
            get {
                int total = 0;
                foreach (var spawn in AllStandardSpawns) if (spawn.EnemyID != WardenEnemyID) total++;
                return total;
            }
        }

        /// <summary>The Chrono-Warden's authored debut: exactly one, on the Ash Road.</summary>
        public static int AuthoredEliteCount {
            get {
                int total = 0;
                foreach (var spawn in AllStandardSpawns) if (spawn.EnemyID == WardenEnemyID) total++;
                return total;
            }
        }

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === Palette: ash grey, pumice, magma ===

        protected override Color FloorColor => new(0.19f, 0.17f, 0.16f);
        protected override Color FloorEdgeColor => new(0.44f, 0.39f, 0.34f);
        protected override Color PlatformColor => new(0.31f, 0.28f, 0.26f);
        protected override Color WallColor => new(0.13f, 0.12f, 0.12f);
        protected override Color HazardColor => new(0.78f, 0.28f, 0.08f);

        private static readonly Color RockfallColor = new(0.33f, 0.27f, 0.24f);
        private static readonly Color SlopeColor = new(0.24f, 0.20f, 0.18f);

        // === Scene-authored toolkit instances ===

        private PuzzleManager _rockfallPuzzle;
        private WeightComparisonObjective _winch;
        private PressurePlate _leftPan;
        private PressurePlate _rightPan;
        private WeightedObject _basaltBoulder;
        private WeightedObject _pumiceBoulder;
        private DestructibleBlock _wedgeLock;
        private Counterweight _winchWeight;
        private EscapeSequenceController _escape;
        private readonly List<RescuableNPC> _civilians = new();

        private StaticBody2D _rockfall;

        public PuzzleManager RockfallPuzzle => _rockfallPuzzle;
        public WeightComparisonObjective Winch => _winch;
        public PressurePlate LeftPan => _leftPan;
        public PressurePlate RightPan => _rightPan;
        public WeightedObject BasaltBoulder => _basaltBoulder;
        public WeightedObject PumiceBoulder => _pumiceBoulder;
        public DestructibleBlock WedgeLock => _wedgeLock;
        public Counterweight WinchWeight => _winchWeight;
        public EscapeSequenceController Escape => _escape;
        public IReadOnlyList<RescuableNPC> Civilians => _civilians;

        /// <summary>False until the winch balances and the rockfall is hauled clear.</summary>
        public bool RockfallCleared { get; private set; }

        /// <summary>How many civilians have been evacuated (0-2). Drives the HUD counter.</summary>
        public int CiviliansRescued { get; private set; }

        /// <summary>True once the player has entered the Ash Road and the front is live.</summary>
        public bool EscapeTriggered { get; private set; }

        private bool _vaultReached;
        private bool _room2WaveSpawned;
        private bool _room3WaveSpawned;
        private bool _rewindBound;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildForum();
            BuildCollapsedVault();
            BuildAshRoad();
            BuildCalderaRim();
            BuildExtractors(ExtractorPlacements);
        }

        /// <summary>
        /// Godot readies children before their parent, so every authored template
        /// instance in Level_06_Pompeii.tscn is already live by the time BuildLevel runs.
        /// </summary>
        private void CollectAuthoredNodes() {
            _rockfallPuzzle = GetNodeOrNull<PuzzleManager>("RockfallPuzzle");
            // Must be idempotent: PuzzleManager re-emits PuzzleCompleted one deferred
            // frame after _Ready for a save-restored completion (Wave A integration),
            // so a resumed run reaches ClearRockfall twice.
            if (_rockfallPuzzle != null) _rockfallPuzzle.PuzzleCompleted += _ => ClearRockfall();

            _winch = GetNodeOrNull<WeightComparisonObjective>("RockfallPuzzle/WinchBalance");
            _leftPan = GetNodeOrNull<PressurePlate>("RockfallPuzzle/LeftPan");
            _rightPan = GetNodeOrNull<PressurePlate>("RockfallPuzzle/RightPan");
            _basaltBoulder = GetNodeOrNull<WeightedObject>("RockfallPuzzle/BasaltBoulder");
            _pumiceBoulder = GetNodeOrNull<WeightedObject>("RockfallPuzzle/PumiceBoulder");
            _wedgeLock = GetNodeOrNull<DestructibleBlock>("RockfallPuzzle/WedgeLock");
            _winchWeight = GetNodeOrNull<Counterweight>("RockfallPuzzle/WinchCounterweight");

            _escape = GetNodeOrNull<EscapeSequenceController>("EscapeRun");
            WireEscapeFinishLine();

            AddCivilian("CivilianForum");
            AddCivilian("CivilianAshRoad");
        }

        private void AddCivilian(string nodeName) {
            if (GetNodeOrNull<RescuableNPC>(nodeName) is not RescuableNPC civilian) return;
            _civilians.Add(civilian);
            civilian.Rescued += _ => OnCivilianRescued();
        }

        /// <summary>
        /// The template parks its finish trigger at the template's own default X and
        /// wires it to nothing. Repositioning it from code rather than overriding an
        /// instanced scene's child in the .tscn avoids the fragile `index=` override
        /// block (L02 precedent) and keeps the finish line at one authored constant.
        /// </summary>
        private void WireEscapeFinishLine() {
            if (_escape == null) return;
            if (_escape.GetNodeOrNull<Area2D>("FinishTrigger") is not Area2D finish) return;
            finish.Position = new Vector2(EscapeFinishX, -360f);
            finish.BodyEntered += body => {
                if (body is PlayerController player) _escape.NotifyPlayerReachedFinish(player);
            };
        }

        /// <summary>
        /// Room 1: the Forum under first ash-fall. Wide and flat so the rescue verb
        /// and the legionnaire's shield both get taught without time pressure. Each
        /// legionnaire post has a drop-through platform directly overhead - the
        /// frontal shield is halved damage, and the answer is to come down on it.
        /// </summary>
        private void BuildForum() {
            BuildRoomBackground("BG_Room1_Forum", Room1StartX, 2880, LevelHeight,
                new Color(0.13f, 0.10f, 0.09f, 0.40f));

            BuildFloor(Room1StartX, GroundY, 2880);
            BuildWall(-20, 0, LevelHeight);

            // Colonnade stumps.
            BuildPlatform(620, 740, 240);
            BuildPlatform(1120, 620, 220);
            BuildPlatform(1720, 560, 260);   // holds extractor 1
            BuildPlatform(2260, 680, 240);

            // Flanking roofs over the two legionnaire posts.
            BuildOneWayPlatform(1000, 700, 220);
            BuildOneWayPlatform(2450, 690, 220);

            BuildCheckpoint(240, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "pompeii_room_forum", new Color(0.92f, 0.72f, 0.40f));
            BuildRoomTransition("pompeii_room_forum", new Vector2(340, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the collapsed vault and the level's vertical section.
        ///
        /// <para>
        /// A volcanic rockfall seals the eastern road. Beside it stands a Roman
        /// counterweight winch: two stone pans that must carry the same load before
        /// the <see cref="Counterweight"/> drops far enough to haul the rubble clear.
        /// A 3-unit basalt boulder has already fallen onto the west pan. The only
        /// other mass in the vault is a 2-unit pumice block held on the wedge gallery
        /// at the top of the villa terraces - dropping it leaves the east pan one
        /// unit light, and the missing unit is the player, who has to stand on the
        /// pan and be the counterweight. Stepping off afterwards cannot re-seal the
        /// road: the puzzle latches on completion.
        /// </para>
        ///
        /// <para>
        /// The terraces are drop-through platforms, so the climb is the required
        /// vertical section and the descent back to the pan is a single drop.
        /// </para>
        /// </summary>
        private void BuildCollapsedVault() {
            BuildRoomBackground("BG_Room2_Vault", Room2StartX, 2880, LevelHeight,
                new Color(0.11f, 0.09f, 0.09f, 0.45f));

            BuildFloor(Room2StartX, GroundY, 2880);

            // Vault floor cover and the flanking roof over the west legionnaire post.
            BuildPlatform(3120, 720, 220);
            BuildOneWayPlatform(3400, 700, 220);
            BuildPlatform(3660, 620, 200);

            // Villa terraces: the vertical section, climbing east-to-west up to the
            // wedge gallery so the descent lands beside the east pan.
            BuildOneWayPlatform(4560, 780, 200);
            BuildOneWayPlatform(4820, 670, 200);
            BuildOneWayPlatform(4600, 550, 200);
            BuildOneWayPlatform(4860, 440, 200);
            BuildOneWayPlatform(4980, 320, 200);   // side branch: extractor 2

            // Wedge gallery, solid, spanning 4320..4680: the attack platform for the
            // pumice block's wedge lock at x 4260.
            BuildPlatform(4500, 348, 360);

            // Flanking roof over the east legionnaire post.
            BuildOneWayPlatform(5150, 690, 200);

            // Package 12 W8 (GAP-08 / V01b): the winch's Reset Puzzle station, on the
            // open vault floor west of the west pan - reachable from every unsolved
            // arrangement and clear of both boulders' restoration volumes.
            AddChild(PuzzleResetStation.Create(RockfallResetStationID, "../RockfallPuzzle", RockfallResetStationPosition));

            BuildCheckpoint(5600, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "pompeii_room_vault", new Color(0.80f, 0.55f, 0.42f));

            // The rockfall itself, plus the ceiling slab above it - a 620 px slab
            // alone would simply be jumped and the era mechanic would be decorative.
            _rockfall = BuildDoor("Rockfall", new Vector2(RockfallX, GroundY),
                "pompeii_rockfall_blocked", new Vector2(56, 620), RockfallColor);
            BuildWall(RockfallX, 0, 280, RockfallColor);

            BuildRoomTransition("pompeii_room_vault", new Vector2(Room2StartX, 600), Room2CameraBounds,
                onEntered: _ => {
                    _vaultReached = true;
                    RefreshObjective();
                });
            BuildWaveTrigger("Room2WaveTrigger", new Vector2(Room2StartX + 140, 760), TriggerRoom2Wave);
        }

        /// <summary>
        /// Room 3: the Ash Road. Mostly flat running with low hops, because the
        /// pressure is the advancing lava front, not the platforming. The one real
        /// detour is the extractor ledge at 7,400 and the second civilian at 7,050 -
        /// both cost seconds against a front that never stops.
        /// </summary>
        private void BuildAshRoad() {
            BuildRoomBackground("BG_Room3_AshRoad", Room3StartX, 3200, LevelHeight,
                new Color(0.14f, 0.08f, 0.07f, 0.50f));

            BuildFloor(Room3StartX, GroundY, 3200);

            BuildPlatform(6100, 760, 200);
            BuildPlatform(6600, 700, 220);
            BuildPlatform(7120, 600, 200);
            BuildPlatform(7400, 470, 220);   // holds extractor 3
            BuildPlatform(7940, 720, 220);
            BuildPlatform(8460, 660, 200);

            // Flanking roofs over the two Ash Road legionnaire posts.
            BuildOneWayPlatform(6900, 690, 200);
            BuildOneWayPlatform(8400, 700, 200);

            BuildRoomDecoration(Room3StartX, "pompeii_room_ashroad", new Color(0.95f, 0.45f, 0.20f));

            BuildRoomTransition("pompeii_room_ashroad", new Vector2(Room3StartX + 30, 600), Room3CameraBounds,
                onEntered: _ => {
                    BeginEscape();
                    TriggerRoom3Wave();
                });
        }

        /// <summary>
        /// Room 4: the caldera rim and the Unbound's thermal anchor. Slanted rocky
        /// slopes on a flat base floor (the base keeps summoned adds and knockback
        /// from falling out of the world) plus two narrow stone ledges. 1,600 px of
        /// floor against the Vulcan Decimator's authored 9.0-unit ranged band, which
        /// is 540 px at BossController's 60 px per unit.
        /// </summary>
        private void BuildCalderaRim() {
            BuildRoomBackground("BG_Room4_Caldera", Room4StartX, 1600, LevelHeight,
                new Color(0.20f, 0.07f, 0.05f, 0.50f));

            BuildFloor(Room4StartX, GroundY, 1600);

            // Shallow rocky slopes framing the anchor bowl.
            BuildSlope(9000, 862, 420, 8f);
            BuildSlope(10100, 862, 420, -8f);

            // Two narrow stone ledges.
            BuildPlatform(9480, 640, 180);
            BuildPlatform(10040, 640, 180);

            BuildWall(10540, 0, LevelHeight);

            BuildCheckpoint(9060, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "pompeii_room_caldera", new Color(0.98f, 0.32f, 0.20f));
            BuildRoomTransition("pompeii_room_caldera", new Vector2(Room4StartX + 30, 540), Room4CameraBounds);

            BuildBossEncounter(BossResourcePath, new Vector2(10150, EnemyGroundY),
                encounterName: "VulcanDecimatorEncounter", revealDistance: 900f);
        }

        /// <summary>
        /// Graybox slope: a floor slab rotated about its own centre. Rotating a whole
        /// floor plan produces collision seams at the joins (L05 precedent), so the
        /// slopes ride on top of a flat base floor rather than replacing it.
        /// </summary>
        private StaticBody2D BuildSlope(float x, float y, float width, float degrees) {
            StaticBody2D slab = BuildFloor(x, y, width, SlopeColor);
            slab.RotationDegrees = degrees;
            return slab;
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
        /// Concurrency peaks at 4 live standards, inside the level's warm pool counts
        /// (standard_enemy 12, enemy_projectile 20).
        /// </summary>
        private void SpawnWave((string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] wave) {
            if (wave == null) return;
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                wave.Length, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < wave.Length; index++) {
                (string enemyID, Vector2 position, Vector2 patrolA, Vector2 patrolB) = wave[index];
                EnemyFactory.Spawn(enemyID, this, position, patrolA, patrolB);
            }
        }

        // === Era mechanic 1: the advancing lava front ===

        /// <summary>Starts the front the first time the player enters the Ash Road.</summary>
        public bool BeginEscape() {
            if (_escape == null || EscapeTriggered || _escape.IsCompleted) return false;
            EscapeTriggered = true;
            _escape.Begin();
            RefreshObjective();
            return true;
        }

        private void OnEscapeCompleted() => RefreshObjective();

        /// <summary>
        /// A caught player is knocked forward and hurt, never killed, so the objective
        /// only nags. The damage itself lives in <see cref="EscapeSequenceController"/>.
        /// </summary>
        private void OnPlayerCaught(int playerIndex, int damage) => SetObjective("pompeii_objective_caught");

        /// <summary>
        /// Chronal Rewind resets the front to its start (the authored
        /// <see cref="StoryRewindPolicy.ResetToInitialState"/>) and halts it. Without
        /// this the level's signature mechanic would switch itself off the first time
        /// the player died in the corridor and the rest of the run would be a walk.
        /// The escape subscribes in its own <c>_Ready</c>, i.e. before this handler,
        /// so the reset has already landed by the time we restart it.
        /// </summary>
        private void OnStoryRewind(Vector2 targetPosition) {
            if (_escape == null || !EscapeTriggered || _escape.IsCompleted) return;
            _escape.Begin();
        }

        // === Era mechanic 2: the counterweight winch ===

        /// <summary>The winch hauls the rockfall off the road. Idempotent.</summary>
        private void ClearRockfall() {
            if (RockfallCleared) return;
            RockfallCleared = true;
            OpenDoor(ref _rockfall);
            RefreshObjective();
        }

        /// <summary>
        /// Resume helper: a run restored east of the rockfall must not find the road
        /// re-sealed behind it. The puzzle persists to the save and now re-emits its
        /// restored completion, but a save that lost the flag would still soft-lock,
        /// so both checkpoints past the vault force the haul directly.
        /// </summary>
        private void ForceRockfallClear() => ClearRockfall();

        // === Era mechanic 3: the civilians ===

        private void OnCivilianRescued() {
            CiviliansRescued++;
            SetObjective("pompeii_objective_civilians", CiviliansRescued, _civilians.Count);
        }

        // === Resume, lifecycle, and objectives ===

        public const string Room2WaveEncounter = ID + "_room2_wave";
        public const string Room3WaveEncounter = ID + "_room3_wave";

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { Room2WaveEncounter },
                [Checkpoint2] = new[] { Room2WaveEncounter, Room3WaveEncounter }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            if (encounterID == Room2WaveEncounter) _room2WaveSpawned = true;
            else if (encounterID == Room3WaveEncounter) _room3WaveSpawned = true;
        }

        protected override void MarkWavesClearedThrough(string checkpointID) {
            // Package 12 W2 (GAP-13): the room waves come from the explicit
            // encounter baseline (EncounterBaselineMap); this keeps the anchor's
            // world state only.
            if (checkpointID == Checkpoint1) {
                // Checkpoint 1 stands east of the rockfall, before the Ash Road.
                _vaultReached = true;
                ForceRockfallClear();
            } else if (checkpointID == Checkpoint2) {
                // Checkpoint 2 is past the escape run. Resuming must not drop the
                // player back in front of a live lava front they have already
                // outrun - the front is parked at its start and the run is closed.
                _vaultReached = true;
                ForceRockfallClear();
                EscapeTriggered = true;
                if (_escape != null) {
                    _escape.ResetSequence();
                    _escape.CompleteEscape();
                }
            }
        }

        protected override void OnLevelReady() {
            if (_escape != null) {
                _escape.EscapeCompleted += OnEscapeCompleted;
                _escape.PlayerCaught += OnPlayerCaught;
            }
            if (EventBus.Instance != null) {
                EventBus.Instance.OnRewindTriggered += OnStoryRewind;
                _rewindBound = true;
            }

            // The basalt boulder is authored already resting in the west pan. Area2D
            // reports its initial overlaps on the first physics tick, which a headless
            // build or a paused entry may not reach before anything reads the winch,
            // so the load is registered explicitly. PressurePlate keys loads by
            // instance id, so this is idempotent with the physics callback.
            if (_leftPan != null && _basaltBoulder != null) _leftPan.RegisterBody(_basaltBoulder);

            RefreshObjective();
        }

        public override void _ExitTree() {
            if (_rewindBound && EventBus.Instance != null) {
                EventBus.Instance.OnRewindTriggered -= OnStoryRewind;
            }
            _rewindBound = false;
            base._ExitTree();
        }

        private void RefreshObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (EscapeTriggered && _escape is { IsCompleted: false }) SetObjective("pompeii_objective_outrun");
            else if (EscapeTriggered) SetObjective("pompeii_objective_anchor");
            else if (_vaultReached && !RockfallCleared) SetObjective("pompeii_objective_rockfall");
            else if (RockfallCleared && _vaultReached) SetObjective("pompeii_objective_ashroad");
            else SetObjective(InitialObjectiveKey);
        }
    }
}
