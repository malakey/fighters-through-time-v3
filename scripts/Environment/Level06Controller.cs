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
    /// <item><b>The Ash Road</b> (6660-8960, past the Ashfall) - the level's signature. An
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
    /// <c>chrono_slasher</c> (Severed shock-trooper standard). The legionnaire carries
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
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L4): the
    /// Forum (300–2880), the vault up to the rockfall (2900–5390, which also needs
    /// the winch balanced: the rockfall door still opens on the solve, the lock's
    /// barrier on "solved AND cleared") and the Ash Road (6720–8500 since GR-LV2) are
    /// <see cref="EncounterLock"/>s. The Ash Road crowd is fought <i>before</i> the
    /// lava front: the front starts when the road lock clears, a fixed head start
    /// behind the hero (<see cref="EscapeHeadStartPixels"/>, never west of
    /// <see cref="EscapeStartX"/>), and the road's second civilian waits in the run
    /// that follows (8700), so the rescue still costs seconds the hero does not
    /// have. The Chrono-Warden is a member of the road lock, so its Phase Skip
    /// lands inside the road (the lock's member clamp).</para>
    ///
    /// <para><b>The Ashfall and the Roofline</b> (gameplay review, GR-LV2: rulings 7a, 7b, 7c,
    /// 7c+). The road lock was carved to 6720-8500, and the span it left, 5680-6700, is a
    /// platforming stretch over a collapsed sewer past the Middle anchor. It starts east of the
    /// rockfall and the Middle anchor (5390-5660 is untouched). Above it the Roofline climbs to
    /// the Ash Road's Extractor. The Vulcan Decimator's arena change (ruling 14) is
    /// <c>Level06Controller.Vulcan.cs</c>. The recipe is <c>docs/handoffs/GR_PILOT.md</c>.</para>
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
        /// <summary>Where the PreBoss anchor stands, outside the arena (9250).</summary>
        public const float PreBossCheckpointX = 9060f;

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
        /// 30 s, plus the Ashfall's +30 (§1.14). Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);
        public override string LevelTitleKey => "pompeii_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(240, 850);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the forum colonnade ledge (Platform 1120/620, top 612), reached from the
        // drop-through at 1000 — a detour over the forum floor, well before the ash-road
        // escape run.
        protected override Vector2? SecretCachePosition => new(1120f, 517f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, just before the
        // PreBoss anchor (9060), past the escape finish (8880) and west of the caldera slope;
        // the window after PreBoss is too short.
        protected override Vector2? RestorationFontPosition => new(8920f, GroundY);

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
        private const float RockfallWidth = 56f;
        private const float RockfallHeight = 620f;

        /// <summary>
        /// P1 (2026-10-04): the caldera arena. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 10150 - 900), keeping the pre-boss
        /// anchor at 9060 outside; east edge is the caldera wall's inner face.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, 10540f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10150f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

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

        /// <summary>
        /// GU-L4 (2026-10-13, Provisional): the lava front starts when the Ash Road
        /// lock clears, this far behind the hero, wherever the hero stands. The
        /// front runs 224 px/s against Lincoln's 228, so a running hero keeps
        /// about this lead and a stop costs it (about one second at the front's
        /// speed). FIX-LEVEL: the start is no longer floored at
        /// <see cref="EscapeStartX"/>, which sits inside the road lock (5660): a
        /// hero who landed the last kill against the entry barrier got the front
        /// on top of them and was knocked behind it.
        /// </summary>
        public const float EscapeHeadStartPixels = 240f;

        /// <summary>
        /// The road's second civilian, in the run after the road lock (GU-L4: it
        /// stood at 7050, inside the fight; the scene node is pinned to it).
        /// </summary>
        public static readonly Vector2 AshRoadCivilianPosition = new(8700f, 820f);

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

        /// <summary>Forum: two legionnaires holding the colonnade, one Severed trooper between them.</summary>
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
            // GR-LV2: 6200 -> 7300, inside the carved lock.
            ("chrono_slasher", new Vector2(7300, EnemyGroundY), new Vector2(7160, EnemyGroundY), new Vector2(7460, EnemyGroundY)),
            ("shock_shield_legionnaire", new Vector2(6900, EnemyGroundY), new Vector2(6760, EnemyGroundY), new Vector2(7060, EnemyGroundY)),
            ("chrono_slasher", new Vector2(7700, EnemyGroundY), new Vector2(7560, EnemyGroundY), new Vector2(7860, EnemyGroundY)),
            // GU-L4: its east patrol end moved 8560 -> 8460, inside the road lock (it ends at 8500).
            ("shock_shield_legionnaire", new Vector2(8400, EnemyGroundY), new Vector2(8260, EnemyGroundY), new Vector2(8460, EnemyGroundY)),
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
            // GR-LV2: at the end of the Roofline, on its last platform (top 310).
            (AshRoadExtractorID, new Vector2(RooflineEndX, 238))
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

        /// <summary>
        /// FIX-LEVEL (2026-10-13): latched once the run is won (the escape
        /// completed, or a resume past it). The escape's own rewind reset
        /// (ResetToInitialState) clears <c>IsCompleted</c> before
        /// <see cref="OnStoryRewind"/> runs, so this, not the escape, is what a
        /// death rewind reads.
        /// </summary>
        public bool EscapeSurvived { get; private set; }

        /// <summary>How many civilians have been evacuated (0-2). Drives the HUD counter.</summary>
        public int CiviliansRescued { get; private set; }

        /// <summary>True once the player has entered the Ash Road and the front is live.</summary>
        public bool EscapeTriggered { get; private set; }

        private bool _vaultReached;
        private bool _rewindBound;

        // === GU-L4 (2026-10-12): the three encounter locks ===================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockForum = ID + "_lock_forum";
        public const string LockVault = ID + "_lock_vault";
        public const string LockAshRoad = ID + "_lock_ash_road";

        /// <summary>The manifest rows the three lock waves are (level_06_rewards.tres).</summary>
        public const string Room1RowID = "level_06.room1";
        public const string Room2RowID = "level_06.room2";
        public const string Room3RowID = "level_06.room3";

        /// <summary>The Forum, east of the entry anchor (240) to the room seam.</summary>
        public const float ForumLockMinX = 300f;
        public const float ForumLockMaxX = Room2StartX;
        /// <summary>The vault, up to the rockfall's west face (its exit barrier fills the rockfall under the gate column).</summary>
        public const float VaultLockMinX = Room2StartX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float VaultLockMaxX = RockfallX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
        /// <summary>
        /// The Ash Road to 8500: the road crowd is fought here, then the front starts and
        /// the hero runs 8500 → the finish (8880) past the second civilian. GR-LV2: its
        /// west end moved from 5660 to 6720, so the span past the Middle anchor (5600) can
        /// be the Ashfall.
        /// </summary>
        public const float AshRoadLockMinX = AshfallMaxX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float AshRoadLockMaxX = 8500f;

        // === GR-LV2: the Ashfall and the Roofline ============================

        public const string AshfallStretchID = "level_06.stretch_ashfall";
        public const string RooflineRouteID = "level_06.route_roofline";
        public const string SwarmRowID = "level_06.swarm1";
        public const string AshRoadExtractorID = "level_06.extractor_ashroad";

        /// <summary>East of the Middle anchor's strike surface and of where the road's barrier used to stand (5650).</summary>
        public const float AshfallMinX = 5680f;
        public const float AshfallMaxX = 6700f;
        /// <summary>Where the vault's deck ends and the road's begins: the sewer's two ramps' high joints.</summary>
        public const float AshfallWestDeckEndX = 5720f;
        public const float AshfallEastDeckStartX = 6660f;
        /// <summary>The Roofline's last platform, which holds the Ash Road's Extractor.</summary>
        public const float RooflineEndX = 6580f;

        /// <summary>
        /// The swarm row on the Forum, 2,580 px wide, so eight
        /// (<see cref="SwarmWaveRules.RowMobsForLockWidth"/>; Easy's prefix gives five). The
        /// Forum's two ash geysers stand among them for a launched one.
        /// </summary>
        public static readonly float[] SwarmSpawnXs = { 640f, 900f, 1200f, 1600f, 1900f, 2100f, 2400f, 2750f };

        private EncounterLock _forumLock;
        private EncounterLock _vaultLock;
        private EncounterLock _ashRoadLock;

        public EncounterLock ForumLock => _forumLock;
        public EncounterLock VaultLock => _vaultLock;
        public EncounterLock AshRoadLock => _ashRoadLock;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildForum();
            BuildCollapsedVault();
            BuildAshfall();
            BuildAshRoad();
            BuildCalderaRim();
            BuildExtractors(ExtractorPlacements);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L4: the three rooms with enemies as encounter locks, one wave per
        /// manifest row. Built here, before the resume baseline, so a resume past a
        /// lock builds it Cleared.
        /// </summary>
        private void BuildEncounterLocks() {
            float half = EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
            // The Forum's colonnade guard: pre-placed.
            _forumLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockForum,
                Area = EncounterLockSpec.Span(ForumLockMinX, ForumLockMaxX, LevelHeight),
                EntryBarrierX = ForumLockMinX - half,
                ExitBarrierX = ForumLockMaxX + half,
                BarrierFloorY = GroundY,
                Waves = new[] {
                    LockWave(Room1RowID, Room1Spawns, EncounterWaveTrigger.OnBuild),
                    SwarmWave(SwarmRowID, EnemyGroundY, SwarmSpawnXs)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The winch room: the escort is on the vault floor, and its exit — the
            // rockfall — also needs the winch balanced. It keeps the level's
            // rockfall objective.
            _vaultLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockVault,
                Area = EncounterLockSpec.Span(VaultLockMinX, VaultLockMaxX, LevelHeight),
                EntryBarrierX = VaultLockMinX - half,
                ExitBarrierX = RockfallX,
                ExitBarrierYRange = new Vector2(GroundY - RockfallHeight, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] { LockWave(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnBuild) },
                RequiredPuzzleIDs = new[] { RockfallPuzzleID },
                GoDirection = EncounterGoDirection.Right
            });
            // The Ash Road: the crowd comes out of the ash as the hero steps onto
            // the road; the lava front only starts once it is cleared.
            _ashRoadLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockAshRoad,
                Area = EncounterLockSpec.Span(AshRoadLockMinX, AshRoadLockMaxX, LevelHeight),
                EntryBarrierX = AshRoadLockMinX - half,
                ExitBarrierX = AshRoadLockMaxX + half,
                BarrierFloorY = GroundY,
                Waves = new[] { LockWave(Room3RowID, Room3Spawns, EncounterWaveTrigger.OnEngage) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _ashRoadLock.StateChanged += OnAshRoadLockStateChanged;
        }

        private static EncounterLockWave LockWave(string rowID,
            (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] table, EncounterWaveTrigger trigger) {
            var spawns = new EncounterLockSpawn[table.Length];
            for (int index = 0; index < table.Length; index++) {
                (string enemyID, Vector2 position, Vector2 patrolA, Vector2 patrolB) = table[index];
                spawns[index] = new EncounterLockSpawn(enemyID, position, patrolA, patrolB);
            }
            return new EncounterLockWave { RowID = rowID, Spawns = spawns, Trigger = trigger };
        }

        /// <summary>GU-L4: the road lock's clear (from a live fight) starts the lava front.</summary>
        private void OnAshRoadLockStateChanged(EncounterLock encounterLock, EncounterLockState previous,
            EncounterLockState current) {
            if (current != EncounterLockState.Cleared || previous != EncounterLockState.Engaged) return;
            BeginEscape();
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
        /// FIX-LEVEL: the trigger alone never completed the run for a walking hero
        /// (it meets the hero short of the line); the escape itself now completes
        /// on the first running tick its runner stands past
        /// <see cref="EscapeFinishX"/>, and the trigger stays as the visible line.
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

            // Flanking roofs over the two legionnaire posts. G5 (2026-10-04): the
            // west roof sat at 700 (top 690), 62 px under the 1120 stump's solid
            // underside (628) where the two overlap — less than the 64 px hero,
            // and inside the one-way's 12 px landing margin, so a hero jumping up
            // through it there was pushed onto it and wedged. At 720 (top 710) it
            // leaves 82 px.
            BuildOneWayPlatform(1000, 720, 220);
            BuildOneWayPlatform(2450, 690, 220);

            BuildCheckpoint(240, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "pompeii_room_forum", new Color(0.92f, 0.72f, 0.40f));
            BuildRoomTransition("pompeii_room_forum", new Vector2(340, 600), Room1CameraBounds);
            // Package 13 W4 (S37): Pliny's galleys, waiting on an admiral who never came aboard.
            BuildAbsenceTrigger(new Vector2(1400, GroundY - 200));
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

            // GR-LV2: the vault's deck ends at the Ashfall's sewer.
            BuildFloor(Room2StartX, GroundY, AshfallWestDeckEndX - Room2StartX);

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
            // P5 (2026-10-04): the slab used to stop at y 0 and spanned only
            // x 5400..5420 over a rockfall centred on 5400 (x 5372..5428), leaving
            // a 28 px lip on the rockfall's top at y 280 with open sky; a double
            // jump off that lip or the villa Extractor (top ~208) cleared it. The
            // column is now centred on the rockfall, wider than it, and rises past
            // the level top (StoryGateRules).
            _rockfall = BuildDoor("Rockfall", new Vector2(RockfallX, GroundY),
                "pompeii_rockfall_blocked", new Vector2(RockfallWidth, RockfallHeight), RockfallColor);
            BuildGateColumn(RockfallX, RockfallWidth, GroundY - RockfallHeight, RockfallColor);

            // GU-L4: the trigger stands just inside the vault lock, so a hero pressed
            // against the Forum's exit barrier does not reach it; the old wave
            // trigger is gone (the vault lock places its escort).
            BuildRoomTransition("pompeii_room_vault", new Vector2(VaultLockMinX + 40f, 600), Room2CameraBounds,
                onEntered: _ => {
                    _vaultReached = true;
                    RefreshObjective();
                });
        }

        /// <summary>
        /// GR-LV2: the Ashfall, 5680-6700, and the Roofline above it. The step line crosses a
        /// collapsed sewer past the Middle anchor: a solid step, a crumbling one, a one-way one
        /// and a solid one, then down to the road's deck. The road's first geyser fires in the
        /// sewer now, a one-way climbs back up, and both ramps walk out. Every number is
        /// provisional (<c>PROVISIONAL-TRAVERSAL-RULES</c>).
        /// </summary>
        private void BuildAshfall() {
            TraversalStretch ashfall = BeginStretch(AshfallStretchID, "pompeii_room_ashfall",
                AshfallMinX, AshfallMaxX, GroundY, AshfallWestDeckEndX, AshfallEastDeckStartX, Room2StartX);
            BuildRoomDecoration(AshfallMinX, "pompeii_room_ashfall", new Color(0.9f, 0.5f, 0.3f));
            BuildStretchTrench(ashfall);

            BuildStretchStep(ashfall, 5830, 790, 200);
            BuildStretchStep(ashfall, 6090, 720, 180, StretchStepKind.Crumbling, "level_06.crumbling_ashfall");
            TraversalSurface middleStep = BuildStretchStep(ashfall, 6350, 722, 200, StretchStepKind.OneWay);
            TraversalSurface eastStep = BuildStretchStep(ashfall, RooflineEndX, 720, 180);

            // Off the line: a way back up out of the sewer.
            BuildOneWayPlatform(6120, 880, 200);

            // The Roofline. Its base-kit line climbs west and back east, and ends in a 245 px hop.
            OptionalRoute roofline = BeginOptionalRoute(RooflineRouteID, AshRoadExtractorID,
                new Vector2(RooflineEndX, 238f), middleStep);
            BuildRouteStep(roofline, 6135, 530, 180);
            TraversalSurface burstFrom = BuildRouteStep(roofline, 5885, 420, 160);
            BuildRouteStep(roofline, 6135, 320, 180);
            TraversalSurface end = BuildRouteStep(roofline, RooflineEndX, 320, 220);
            roofline.MarkBurstShortcut(burstFrom, end);
            roofline.MarkRiseShortcut(eastStep, end);
        }

        /// <summary>
        /// Room 3: the Ash Road. Mostly flat running with low hops, because the
        /// pressure is the advancing lava front, not the platforming. The one real
        /// detour is the second civilian at the run's end, which costs seconds against
        /// a front that never stops.
        /// </summary>
        private void BuildAshRoad() {
            BuildRoomBackground("BG_Room3_AshRoad", Room3StartX, 3200, LevelHeight,
                new Color(0.14f, 0.08f, 0.07f, 0.50f));

            // GR-LV2: the road's deck starts past the Ashfall's sewer.
            BuildFloor(AshfallEastDeckStartX, GroundY, Room3StartX + 3200f - AshfallEastDeckStartX);

            BuildPlatform(7120, 600, 200);
            BuildPlatform(7400, 470, 220);
            BuildPlatform(7940, 720, 220);
            BuildPlatform(8460, 660, 200);

            // Flanking roofs over the two Ash Road legionnaire posts.
            BuildOneWayPlatform(6900, 690, 200);
            BuildOneWayPlatform(8400, 700, 200);

            BuildRoomDecoration(Room3StartX, "pompeii_room_ashroad", new Color(0.95f, 0.45f, 0.20f));

            // GU-L4: entering the road no longer starts the front or spawns the
            // crowd: the Ash Road lock spawns it, and its clear starts the front.
            // GR-LV2: the trigger stands just inside the carved lock, and the rect still
            // reaches back over the Ashfall, so entering it does not pan.
            BuildRoomTransition("pompeii_room_ashroad", new Vector2(AshRoadLockMinX + 40f, 600), Room3CameraBounds);
        }

        /// <summary>
        /// Room 4: the caldera rim and the Severed's thermal anchor. Slanted rocky
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

            BuildCheckpoint(PreBossCheckpointX, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "pompeii_room_caldera", new Color(0.98f, 0.32f, 0.20f));
            BuildRoomTransition("pompeii_room_caldera", new Vector2(Room4StartX + 30, 540), Room4CameraBounds);

            BossEncounterController vulcan = BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, EnemyGroundY),
                "VulcanDecimatorEncounter", BossArenaBounds);
            // GR-LV2 (ruling 14): Phase 2 cracks the floor; Level06Controller.Vulcan.cs.
            BuildVulcanVents();
            if (vulcan != null) {
                vulcan.PhaseEntered += OnVulcanPhaseEntered;
                vulcan.BossDefeated += _ => LowerVulcanEruption();
            }
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
        //
        // GU-L4 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles, never more
        // than the table. Concurrency peaks at 5 live (4 standards and the Warden),
        // inside the level's warm pool counts (standard_enemy 12, elite_enemy 3,
        // enemy_projectile 20).

        // === Era mechanic 1: the advancing lava front ===

        /// <summary>
        /// Starts the front. Since GU-L4 that happens when the Ash Road lock clears
        /// (no longer on entering the road), so the front starts
        /// <see cref="EscapeHeadStartPixels"/> behind wherever the hero stands
        /// instead of at the road's west end, which would trail a hero who
        /// finished the fight far down the road by thousands of pixels.
        /// </summary>
        public bool BeginEscape() {
            if (_escape == null || EscapeTriggered || _escape.IsCompleted) return false;
            EscapeTriggered = true;
            float heroX = Player != null && IsInstanceValid(Player) ? Player.GlobalPosition.X : EscapeStartX;
            _escape.StartX = EscapeStartFor(heroX);
            _escape.ResetSequence();
            _escape.Begin();
            RefreshObjective();
            return true;
        }

        /// <summary>
        /// The front's start for a hero at <paramref name="heroX"/>: the head start
        /// behind it, never past the front's end. FIX-LEVEL: no floor at the
        /// authored start — a front west of it runs east like any other, and only
        /// the hero is east of it.
        /// </summary>
        public static float EscapeStartFor(float heroX) =>
            Mathf.Min(heroX - EscapeHeadStartPixels, EscapeEndX);

        private void OnEscapeCompleted() {
            EscapeSurvived = true;
            RefreshObjective();
        }

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
        /// <para>FIX-LEVEL: that reset also clears <c>IsCompleted</c>, so the old
        /// <c>_escape.IsCompleted</c> guard read false after every rewind and a
        /// death after the run was won (in the arena, say) restarted the front.
        /// The level's own <see cref="EscapeSurvived"/> latch decides, and a won
        /// run is completed again.</para>
        /// </summary>
        private void OnStoryRewind(Vector2 targetPosition) {
            if (_escape == null || !EscapeTriggered) return;
            if (EscapeSurvived) {
                _escape.CompleteEscape();
                return;
            }
            // GU-L4: the front's start follows the hero since it starts at the road
            // lock's clear. A rewind that lands the hero behind it moves it back, so
            // the reset front never begins over (or ahead of) the hero.
            float start = Mathf.Min(_escape.StartX, EscapeStartFor(targetPosition.X));
            if (!Mathf.IsEqualApprox(start, _escape.StartX)) {
                _escape.StartX = start;
                _escape.ResetSequence();
            }
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

        // GU-L4: the encounters are the three locks; every anchor lists every lock
        // before it, so a resume builds those Cleared (the Forum used to respawn
        // behind the hero on every resume).
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockForum, LockVault },
                [Checkpoint2] = new[] { LockForum, LockVault, LockAshRoad }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>GU-L4: a cleared or reverted lock hands back the level's derived objective.</summary>
        protected override void RestoreLevelObjective() => RefreshObjective();

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
                EscapeSurvived = true;
                if (_escape != null) {
                    _escape.ResetSequence();
                    _escape.CompleteEscape();
                }
            }
        }

        protected override void OnLevelReady() {
            if (_escape != null) {
                _escape.Runner = Player; // FIX-LEVEL: the hero the finish line is judged against
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
