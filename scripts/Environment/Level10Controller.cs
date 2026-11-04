using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 10 - The Globe Theatre, London 1599. Opening night of <i>Hamlet</i>.
    /// The Severed have seeded neural-manipulators through the galleries and
    /// rigged collapse charges under the stage, meaning to turn the house into a
    /// fatal stampede. Four rooms across 10,240 px:
    /// <list type="number">
    /// <item><b>The Yard</b> (0-2560) - the groundling pit in front of the stage.
    ///       Flat, safe, and where the audience teaches itself: stand still in
    ///       front of a heckling crowd and something comes out of the galleries.</item>
    /// <item><b>The Stage</b> (2560-5760) - the signature mechanic. Three
    ///       <see cref="TrapdoorPlatform"/> sections are the stage boards
    ///       themselves; each shakes, then drops out from under whoever is on it.
    ///       Falling lands in the under-stage cellar, which is a real place with a
    ///       Chronal Extractor in it and machinery hazards between you and the
    ///       climb back out.</item>
    /// <item><b>The Galleries</b> (5760-8320) - the vertical section. Four
    ///       <see cref="PendulumAnchor"/> rigging lines ladder up three balcony
    ///       tiers. Deliberately unlike Nassau's long open-air crossings: tight
    ///       arcs, fast periods, staggered phases, short horizontal gaps, and a
    ///       capped release assist. A Globe rope lifts you a tier; it does not
    ///       fling you across a bay.</item>
    /// <item><b>The Tiring-House Stage</b> (8320-10240) - the boss arena. Flat
    ///       open boards with two tiered gallery balconies at the sides, plus two
    ///       arena trapdoors on a slow, heavily telegraphed cycle.</item>
    /// </list>
    ///
    /// <para>
    /// Era roster: <c>holo_page</c> (the London standard - a spectral prompt-boy
    /// hurling holographic daggers that apply TimeDilation) mixed with
    /// <c>chrono_slasher</c> (Severed shock-trooper standard).
    /// </para>
    ///
    /// <para>
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 10 standards,
    /// 0 elites, 1 boss, 3 extractors. The authored tables below are the single
    /// source of those counts and Level10ContentTests asserts them.
    /// </para>
    ///
    /// <para>
    /// <b>Encounter locks</b> (gameplay update 2026-10-12, GU-L8; the Level 2
    /// pattern): the Yard (300-2540); the Stage with its cellar (2560-5740), whose
    /// walls already close the cellar's ends below the boards, so the fight runs over
    /// the cycling trapdoors; and the Galleries (6080-8330), whose exit is the bridge
    /// into the tiring-house. The galleries' middle-tier and upper-tier actors moved
    /// down onto the floor and the lower tier, the Middle anchor moved out of the
    /// stage fight to the galleries' floor (6040) and the PreBoss anchor east out of
    /// swing reach of the yard wall (8460). While a lock is engaged the audience's
    /// stillness clock pauses: waiting one's turn and blocking are normal in a lock.
    /// </para>
    ///
    /// <para>
    /// <b>The stage front</b> (GR-LV4, rulings 7a, 7b, 7c, 7c+): the Yard's lock now ends
    /// at 2000 and the Stage's starts at 2900, and the span between is a traversal stretch
    /// with the secret at the end of its Gallery Rail route. The stage's cellar, its traps,
    /// the rigging and the arena recess did not move; the cellar's west wall followed the
    /// lock to 2880. The Galleries' floor takes the swarm row, away from the Yard, where the
    /// audience teaches itself, and the stillness clock pauses through that lock as through
    /// the others.
    /// </para>
    /// </summary>
    public partial class Level10Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_10_globe";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/tragedy_king.tres";

        /// <summary>
        /// P1 (2026-10-04): the Globe's stage. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 9900 - 900), keeping the pre-boss
        /// anchor (8420) and the tiring-house absence beat (8520) outside; east
        /// edge is the stage wall.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, 10220f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 9900f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;
        public const string DialogueResourcePath = "res://resources/Dialogue/level_10_dialogue.tres";

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.London;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);
        public override string LevelTitleKey => "globe_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(240, EnemyGroundY);

        // GR-LV4: the secret cache sits at the end of the Gallery Rail route, over the
        // stage front's first step (it stood on the one-way 2280/740, which the carve
        // removed). Package 12 W8 placed it off the floor route, and it still is.
        protected override Vector2? SecretCachePosition => new(GalleryRailEndX, 505f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (8420) on the boards, 240 px short of the arena (9000) and 210 px west of the
        // recess.
        protected override Vector2? RestorationFontPosition => new(8760f, GroundY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "globe_objective_reach_stage";
        protected override string BossObjectiveKey => "globe_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "globe_objective_complete";

        // === Layout ===

        public const float LevelWidth = 10240f;
        /// <summary>
        /// Taller than Florence's 1080: the Globe is a vertical building. The
        /// under-stage cellar sits at 1500 and the upper gallery at 420, so a
        /// 1080-tall level cannot express the stage-to-galleries climb at all.
        /// </summary>
        public const float LevelHeight = 1600f;

        /// <summary>Top surface of the stage boards and of the yard.</summary>
        public const float GroundY = 1120f;
        /// <summary>Standing height for actors on the boards.</summary>
        public const float EnemyGroundY = 1070f;
        /// <summary>Under-stage cellar floor ("hell", in the period's own stage jargon).</summary>
        public const float CellarY = 1500f;

        public const float Room1StartX = 0f;
        public const float Room2StartX = 2560f;
        public const float Room3StartX = 5760f;
        public const float Room4StartX = 8320f;

        /// <summary>
        /// The Yard is the one flat room, so it confines to a bottom-anchored
        /// 1080 window rather than the full 1600 - otherwise the camera drifts up
        /// into empty gallery air with nothing in frame. Every other room is a
        /// climb and uses the full height.
        /// </summary>
        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, LevelHeight - 1080f, StageFrontWestDeckEndX, 1080);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 3200, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX, 0, 1920, LevelHeight);

        // === Trapdoor geometry ===

        /// <summary>
        /// The template's collision shape is a fixed 220x28 sub-resource, and
        /// overriding a sub-resource on an instanced scene risks mutating the one
        /// shared between every instance (L05 precedent). So the stage-board gaps
        /// are authored at exactly this width and the trapdoors sit flush in them.
        /// </summary>
        public const float TrapdoorWidth = 220f;

        /// <summary>Board-surface y for a trapdoor node, so its top lines up with <see cref="GroundY"/>.</summary>
        public const float TrapdoorY = GroundY + 14f;

        /// <summary>Centre x of every trapdoor in the level, west to east.</summary>
        public static readonly float[] TrapdoorCentresX = { 3160f, 3900f, 4640f, 9080f, 9560f };

        // === Rigging geometry ===

        /// <summary>
        /// Rope length from an anchor pivot to its hang marker, fixed by
        /// PendulumAnchorTemplate (a 320 px rope plus the grab point's 40 px hang
        /// marker). Used to reason about where a swing actually reaches.
        /// </summary>
        public const float RiggingRopeLength = 360f;

        /// <summary>
        /// Nassau's rope arcs are wide, slow, open-air crossings between ship
        /// hulls. The Globe's rigging is the opposite discipline and every number
        /// here says so: no arc wider than this, so a swing is a tight indoor
        /// pendulum rather than a sweep across a bay.
        /// </summary>
        public const float MaxRiggingAmplitudeDegrees = 34f;

        /// <summary>Fast: a Globe swing completes in well under a second and a half.</summary>
        public const float MaxRiggingPeriodSeconds = 1.4f;

        /// <summary>Capped release assist: the rigging lifts a tier, it does not launch across a gulf.</summary>
        public const float RiggingMaxLaunchSpeed = 520f;

        // === The yard wall (room 3) ===

        /// <summary>
        /// The bridging span that ends the mandatory gallery run (centre x and
        /// centre y, as <c>BuildPlatform</c> takes them): it carries the route east
        /// over the yard wall into the tiring-house.
        /// </summary>
        public const float GalleryBridgeX = 8240f;
        public const float GalleryBridgeY = 620f;
        public const float GalleryBridgeWidth = 180f;

        /// <summary>The yard wall's west face; the yard floor dead-ends against it.</summary>
        public const float YardWallX = 8280f;

        /// <summary>
        /// The yard wall rises to meet the bridging span's underside, so the only
        /// way east from the yard is over the span — which is the climb. It used to
        /// stop at y 700: 420 px above the yard, with 72 px under the span, room
        /// for the 64 px hero to slip across the wall top. The 2026-10-08 apex hang
        /// put that inside Cleopatra's double jump (about 427 px), so the wall no
        /// longer leaves the gap (<c>Level10ContentTests</c> pins it).
        /// </summary>
        public const float YardWallTopY = GalleryBridgeY + DefaultPlatformThickness / 2f;

        // === The arena trap recess (room 4) ===

        /// <summary>The underside of the stage boards: <see cref="GroundY"/> plus a floor's thickness.</summary>
        public const float BoardUndersideY = GroundY + DefaultFloorThickness;

        /// <summary>
        /// West end of the prompt recess under the two arena trapdoors: the west
        /// board's east edge, and the west edge of the west trap gap.
        /// </summary>
        public const float ArenaRecessWestX = 8970f;

        /// <summary>
        /// East end of the prompt recess: the east trap gap's east edge, and the
        /// east board's west edge.
        /// </summary>
        public const float ArenaRecessEastX = 9670f;

        /// <summary>
        /// Top of the recess floor, 100 px under <see cref="BoardUndersideY"/>: a
        /// step down, never a pit.
        /// </summary>
        public const float ArenaRecessFloorY = 1260f;

        /// <summary>
        /// The walls that close the recess at both ends run from the boards'
        /// underside down to the recess floor's underside, so no gap is left above
        /// or below the floor's ends.
        /// </summary>
        public const float ArenaRecessWallHeight = ArenaRecessFloorY + DefaultFloorThickness - BoardUndersideY;

        // === Audience idle-punish tuning ===

        /// <summary>Seconds of standing still before the galleries take an interest.</summary>
        public const float AudienceIdleArmSeconds = 3f;

        /// <summary>Telegraph between the crowd winding up and the object landing.</summary>
        public const float AudienceWarningSeconds = 1.1f;

        /// <summary>Below this speed (px/s) the player counts as idle.</summary>
        public const float AudienceIdleSpeedThreshold = 40f;

        /// <summary>
        /// The single honest number: leaving this radius from the marked spot
        /// disarms the throw, and standing inside it when the telegraph expires is
        /// what gets hit. One constant for both so the hazard can never reach
        /// further than the escape it advertises.
        /// </summary>
        public const float AudienceEscapeRadius = 90f;

        /// <summary>How long the landed prop stays live once it hits the boards.</summary>
        public const float AudienceActiveSeconds = 0.25f;

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>The Yard: groundlings the Severed have already turned, standing in the pit.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room1Spawns = {
            ("holo_page", new Vector2(1180, EnemyGroundY), new Vector2(1020, EnemyGroundY), new Vector2(1340, EnemyGroundY)),
            ("chrono_slasher", new Vector2(1720, EnemyGroundY), new Vector2(1560, EnemyGroundY), new Vector2(1880, EnemyGroundY)),
            // GR-LV4: 2280 -> 1880, inside the carved lock.
            ("holo_page", new Vector2(1880, EnemyGroundY), new Vector2(1760, EnemyGroundY), new Vector2(1960, EnemyGroundY))
        };

        /// <summary>
        /// The Stage: the cast the Archive replaced. Deliberately posted on the
        /// solid board segments and never straddling a trapdoor gap, so the floor
        /// dropping is a threat to the player and not a free enemy delete.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room2Spawns = {
            // GR-LV4: 2830 -> 3530, the board segment between the west and centre traps,
            // because the carved lock starts at 2900.
            ("chrono_slasher", new Vector2(3530, EnemyGroundY), new Vector2(3380, EnemyGroundY), new Vector2(3680, EnemyGroundY)),
            ("holo_page", new Vector2(4270, EnemyGroundY), new Vector2(4120, EnemyGroundY), new Vector2(4420, EnemyGroundY)),
            ("chrono_slasher", new Vector2(5160, EnemyGroundY), new Vector2(5010, EnemyGroundY), new Vector2(5310, EnemyGroundY))
        };

        /// <summary>
        /// The Galleries: three on the yard floor behind the stage and one posted on
        /// the lower balcony tier, so the climb's foot is contested. Four is the level's
        /// concurrency peak, well inside level_10_pool_config's standard_enemy warm
        /// count of 12 and its enemy_projectile warm count of 20.
        /// <para>GU-L8 (2026-10-12): the Galleries are an encounter lock, and a ground
        /// mob never leaves its tier, so every actor stands where the slowest hero
        /// fights it from the floor: the first prompt-boy moved 5960 -> 6300 into the
        /// lock's area, the middle-tier prompt-boy (7000, 610), 468 px up, and the
        /// upper-tier slasher (7940, 370) on the optional branch both moved down onto
        /// the yard floor (6900 and 7400).</para>
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room3Spawns = {
            ("holo_page", new Vector2(6300, EnemyGroundY), new Vector2(6200, EnemyGroundY), new Vector2(6420, EnemyGroundY)),
            ("chrono_slasher", new Vector2(6620, 830), new Vector2(6530, 830), new Vector2(6710, 830)),
            ("holo_page", new Vector2(6900, EnemyGroundY), new Vector2(6800, EnemyGroundY), new Vector2(7000, EnemyGroundY)),
            ("chrono_slasher", new Vector2(7400, EnemyGroundY), new Vector2(7300, EnemyGroundY), new Vector2(7520, EnemyGroundY))
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
            ("level_10.extractor_yard", new Vector2(960, 800)),
            ("level_10.extractor_understage", new Vector2(3900, 1300)),
            ("level_10.extractor_gallery", new Vector2(7940, 360))
        };

        public static int AuthoredStandardCount =>
            Room1Spawns.Length + Room2Spawns.Length + Room3Spawns.Length;

        public static int AuthoredEliteCount => 0;

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === GU-L8 (2026-10-12): the three encounter locks ====================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockYard = ID + "_lock_yard";
        public const string LockStage = ID + "_lock_stage";
        public const string LockGalleries = ID + "_lock_galleries";

        /// <summary>The manifest rows the three lock waves are (level_10_rewards.tres).</summary>
        public const string Room1RowID = "level_10.room1";
        public const string Room2RowID = "level_10.room2";
        public const string Room3RowID = "level_10.room3";
        /// <summary>
        /// GR-LV4 (ruling 1b++): the swarm row, on the Galleries' floor: flat, no trap, and away
        /// from the Yard where the audience teaches itself. Its lock pauses the stillness clock.
        /// </summary>
        public const string SwarmRowID = "level_10.swarm1";

        /// <summary>Eight along the Galleries' floor (<see cref="SwarmWaveRules.RowMobsForLockWidth"/>).</summary>
        public static readonly float[] SwarmSpawnXs = { 6500f, 6720f, 6980f, 7180f, 7450f, 7650f, 7880f, 8100f };

        private const float BarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>The Yard: the groundling pit past the entry anchor (240), carved by GR-LV4 from 2540 to 2000.</summary>
        public const float YardLockMinX = 300f;
        public const float YardLockMaxX = 2000f;

        /// <summary>
        /// The Stage, cellar included: the cellar's walls (2540 and 5740) already close
        /// its ends below the boards. Its exit barrier stands on the east cellar wall
        /// and runs down to that wall's top, so climbing out of the cellar by the
        /// tiring-house stair (5560-5750) stays inside it. GR-LV4 moved its west end 2560 ->
        /// 2900 and the cellar's west wall with it. The west trap gap starts at 3050, so a hero
        /// entering still stands on 150 px of solid boards past the engage line's approach.
        /// The three traps fix the lock's width, so it stays wider than the carve's usual
        /// 1,700-2,000 px.
        /// </summary>
        public const float StageLockMinX = 2900f;
        public const float StageLockMaxX = 5740f;
        /// <summary>Top of the east cellar wall: the stage lock's exit barrier runs down to it.</summary>
        private const float CellarWallTopY = 1180f;
        /// <summary>The cellar's west wall, under the stage lock's entry barrier.</summary>
        public const float CellarWestWallX = StageLockMinX - BarrierWidth;

        /// <summary>
        /// The Middle anchor, on the galleries' floor, under the lower tier and east of
        /// the ground-level approach step: its strike surface (6010-6070) is beyond the
        /// longest basic swing from inside the stage, and its respawn point clears the
        /// step overhead. It stood at 5400, in the stage fight.
        /// </summary>
        public const float MiddleCheckpointX = 6040f;

        /// <summary>
        /// The Galleries: the yard floor, the tiers and the rigging, to the bridging
        /// span. Its exit barrier stands just east of the span, from the sky to the
        /// tiring-house boards: the bridge into room 4 is the area's exit.
        /// </summary>
        public const float GalleriesLockMinX = MiddleCheckpointX + 40f;
        public const float GalleriesLockMaxX = GalleryBridgeX + GalleryBridgeWidth / 2f;

        /// <summary>
        /// The PreBoss anchor, moved 8420 -> 8460 so its strike surface (8430-8490) is
        /// beyond the longest basic swing of a hero pressed against the yard wall's west
        /// face inside the Galleries (8260).
        /// </summary>
        public const float PreBossCheckpointX = 8460f;

        private EncounterLock _yardLock;
        private EncounterLock _stageLock;
        private EncounterLock _galleriesLock;

        public EncounterLock YardLock => _yardLock;
        public EncounterLock StageLock => _stageLock;
        public EncounterLock GalleriesLock => _galleriesLock;

        // === Palette: oak boards, limewash plaster, candle amber ===

        protected override Color FloorColor => new(0.30f, 0.21f, 0.12f);
        protected override Color FloorEdgeColor => new(0.55f, 0.41f, 0.22f);
        protected override Color PlatformColor => new(0.38f, 0.27f, 0.16f);
        protected override Color WallColor => new(0.17f, 0.14f, 0.11f);
        protected override Color HazardColor => new(0.72f, 0.30f, 0.16f);

        private static readonly Color CellarColor = new(0.13f, 0.11f, 0.10f);
        private static readonly Color GalleryColor = new(0.44f, 0.33f, 0.20f);

        // === Scene-authored toolkit instances ===

        private readonly List<TrapdoorPlatform> _stageTrapdoors = new();
        private readonly List<TrapdoorPlatform> _arenaTrapdoors = new();
        private readonly List<PendulumAnchor> _rigging = new();
        private StoryCyclicHazard _audienceThrow;

        public IReadOnlyList<TrapdoorPlatform> StageTrapdoors => _stageTrapdoors;
        public IReadOnlyList<TrapdoorPlatform> ArenaTrapdoors => _arenaTrapdoors;
        public IReadOnlyList<PendulumAnchor> Rigging => _rigging;

        /// <summary>The prop the galleries throw at an idle player. Driven entirely from here.</summary>
        public StoryCyclicHazard AudienceThrow => _audienceThrow;

        /// <summary>Every trapdoor in the level, stage boards then arena boards.</summary>
        public IEnumerable<TrapdoorPlatform> AllTrapdoors {
            get {
                foreach (TrapdoorPlatform door in _stageTrapdoors) yield return door;
                foreach (TrapdoorPlatform door in _arenaTrapdoors) yield return door;
            }
        }

        // === Audience runtime state ===

        /// <summary>How long the player has been effectively motionless.</summary>
        public float AudienceIdleSeconds { get; private set; }

        /// <summary>True between the crowd winding up and the prop landing (or the player escaping).</summary>
        public bool AudienceArmed { get; private set; }

        /// <summary>Props that actually connected. Presentation and tests read this.</summary>
        public int AudienceThrowsLanded { get; private set; }

        /// <summary>Where the throw is aimed. Only meaningful while <see cref="AudienceArmed"/>.</summary>
        public Vector2 AudienceMarkedSpot { get; private set; }

        /// <summary>Set false to silence the galleries (resume restore, teardown).</summary>
        public bool AudienceWatchEnabled { get; set; } = true;

        private float _audienceWarningTimer;
        private float _audienceActiveTimer;
        private Vector2 _lastPlayerPosition;
        private bool _lastPlayerPositionValid;

        private bool _stageReached;
        private bool _galleriesReached;
        private bool _rewindBound;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildYard();
            BuildStageFront();
            BuildStage();
            BuildGalleries();
            BuildTiringHouseStage();
            BuildExtractors(ExtractorPlacements);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L8: the three fights as encounter locks, one wave per manifest row.
        /// Built in <see cref="BuildLevel"/> so a resume past a lock builds it Cleared
        /// with no member ever spawned.
        /// </summary>
        private void BuildEncounterLocks() {
            _yardLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockYard,
                Area = EncounterLockSpec.Span(YardLockMinX, YardLockMaxX, LevelHeight),
                EntryBarrierX = YardLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = YardLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = GroundY,
                Waves = new[] { WaveFrom(Room1RowID, Room1Spawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The cast takes the boards once the hero is on stage, over the cycling
            // trapdoors; the cellar below is part of the fight.
            _stageLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockStage,
                Area = EncounterLockSpec.Span(StageLockMinX, StageLockMaxX, LevelHeight),
                EntryBarrierX = StageLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = StageLockMaxX + BarrierWidth * 0.5f,
                ExitBarrierYRange = new Vector2(StoryGateRules.ColumnTopY, CellarWallTopY),
                BarrierFloorY = GroundY,
                Waves = new[] { WaveFrom(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnEngage) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _galleriesLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockGalleries,
                Area = EncounterLockSpec.Span(GalleriesLockMinX, GalleriesLockMaxX, LevelHeight),
                EntryBarrierX = GalleriesLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = GalleriesLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = GroundY,
                Waves = new[] {
                    WaveFrom(Room3RowID, Room3Spawns, EncounterWaveTrigger.OnBuild),
                    SwarmWave(SwarmRowID, EnemyGroundY, SwarmSpawnXs)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>A lock wave from one of this level's spawn tables (their patrols are not nullable).</summary>
        private static EncounterLockWave WaveFrom(string rowID,
            (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] table, EncounterWaveTrigger trigger) {
            var spawns = new EncounterLockSpawn[table.Length];
            for (int index = 0; index < table.Length; index++) {
                (string enemyID, Vector2 position, Vector2 patrolA, Vector2 patrolB) = table[index];
                spawns[index] = new EncounterLockSpawn(enemyID, position, patrolA, patrolB);
            }
            return new EncounterLockWave { RowID = rowID, Spawns = spawns, Trigger = trigger };
        }

        /// <summary>
        /// Godot readies children before their parent, so every authored template
        /// instance in Level_10_Globe.tscn is already live by the time BuildLevel runs.
        /// </summary>
        private void CollectAuthoredNodes() {
            CollectTrapdoor(_stageTrapdoors, "TrapdoorStageWest");
            CollectTrapdoor(_stageTrapdoors, "TrapdoorStageCentre");
            CollectTrapdoor(_stageTrapdoors, "TrapdoorStageEast");
            CollectTrapdoor(_arenaTrapdoors, "TrapdoorArenaWest");
            CollectTrapdoor(_arenaTrapdoors, "TrapdoorArenaEast");

            CollectRigging("RiggingYardLine");
            CollectRigging("RiggingLowerToMiddle");
            CollectRigging("RiggingMiddleTraverse");
            CollectRigging("RiggingMiddleToUpper");

            _audienceThrow = GetNodeOrNull<StoryCyclicHazard>("AudienceThrow");
            // The prop is driven entirely by the idle watch below; leaving the
            // component's own cycle running would turn a punish into a metronome.
            if (_audienceThrow != null) _audienceThrow.Enabled = false;
        }

        private void CollectTrapdoor(List<TrapdoorPlatform> into, string nodeName) {
            if (GetNodeOrNull<TrapdoorPlatform>(nodeName) is TrapdoorPlatform door) into.Add(door);
        }

        private void CollectRigging(string nodeName) {
            if (GetNodeOrNull<PendulumAnchor>(nodeName) is PendulumAnchor anchor) _rigging.Add(anchor);
        }

        /// <summary>
        /// Room 1: the Yard. Deliberately flat and safe. This is where the
        /// audience mechanic gets taught, and a lesson delivered over a pit is not
        /// a lesson. Stacked barrels and the tiring-house steps give the room its
        /// only elevation, and one of them holds the first extractor.
        /// </summary>
        private void BuildYard() {
            BuildRoomBackground("BG_Room1_Yard", Room1StartX, 2560, LevelHeight,
                new Color(0.14f, 0.11f, 0.08f, 0.40f));

            BuildFloor(Room1StartX, GroundY, StageFrontWestDeckEndX);
            BuildWall(-20, 0, LevelHeight);

            // Groundling clutter: stacked barrels and the gallery stair.
            BuildPlatform(520, 980, 220);
            BuildPlatform(960, 860, 200);    // holds extractor 1
            BuildPlatform(1420, 960, 220);
            // GR-LV4: 1980 -> 1860, west of the carved lock's exit barrier.
            BuildPlatform(1860, 900, 240);

            // The lowest gallery rail overhanging the pit - decor that also gives
            // the player a way to break line of sight from a heckling crowd.
            BuildOneWayPlatform(1180, 760, 240, GalleryColor);

            BuildCheckpoint(240, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "globe_room_yard", new Color(0.95f, 0.80f, 0.48f));
            BuildRoomTransition("globe_room_yard", new Vector2(340, 800), Room1CameraBounds,
                triggerSize: new Vector2(80, LevelHeight));
        }

        // === GR-LV4 (rulings 7a, 7b, 7c, 7c+): the stage front ==================

        public const string StageFrontStretchID = "level_10.stage_front";
        public const string GalleryRailRouteID = "level_10.gallery_rail";

        /// <summary>The stretch's span: the Yard's exit barrier to the Stage's entry barrier.</summary>
        public const float StageFrontMinX = YardLockMaxX + BarrierWidth;
        public const float StageFrontMaxX = StageLockMinX - BarrierWidth;
        /// <summary>Where the yard's floor ends, the west ramp's high joint.</summary>
        public const float StageFrontWestDeckEndX = 2060f;
        /// <summary>Where the boards begin, the east ramp's high joint.</summary>
        public const float StageFrontEastDeckStartX = 2840f;
        /// <summary>The Gallery Rail's last platform, which holds the secret.</summary>
        public const float GalleryRailEndX = 2140f;

        /// <summary>
        /// The stretch between the Yard and the Stage. There is no lock here, so the audience
        /// is watching: standing still on a step gets a prop thrown, which is the level's
        /// mechanic doing its job. A hero who falls in walks out by either ramp. Every number
        /// is provisional (<c>PROVISIONAL-TRAVERSAL-RULES</c>).
        /// </summary>
        private void BuildStageFront() {
            TraversalStretch stageFront = BeginStretch(StageFrontStretchID, "globe_room_stage_front",
                StageFrontMinX, StageFrontMaxX, GroundY, StageFrontWestDeckEndX, StageFrontEastDeckStartX, Room1StartX);
            BuildRoomDecoration(StageFrontMinX, "globe_room_stage_front", new Color(0.98f, 0.76f, 0.42f));
            BuildStretchTrench(stageFront);

            TraversalSurface firstStep = BuildStretchStep(stageFront, 2170f, 1010f, 200f);
            BuildStretchStep(stageFront, 2430f, 940f, 180f, StretchStepKind.Crumbling, "level_10.crumbling_stage_front");
            TraversalSurface railStep = BuildStretchStep(stageFront, 2650f, 940f, 200f, StretchStepKind.OneWay);

            // A winch lane to cross down in the trench, on the cellar's numbers.
            BuildCyclicHazardLane("level_10.stage_front_winch", 2450f, stageFront.TrenchFloorY, 1.2f, 0.6f, 2.8f, 14);

            OptionalRoute rail = BeginOptionalRoute(GalleryRailRouteID, SecretSourceID,
                SecretCachePosition.Value, railStep);
            BuildRouteStep(rail, 2710f, 820f, 120f);
            TraversalSurface burstFrom = BuildRouteStep(rail, 2780f, 710f, 160f);
            BuildRouteStep(rail, 2570f, 610f, 180f);
            TraversalSurface end = BuildRouteStep(rail, GalleryRailEndX, 610f, 220f);
            rail.MarkBurstShortcut(burstFrom, end);
            rail.MarkRiseShortcut(firstStep, end);
        }

        /// <summary>
        /// Room 2: the stage itself, and the level's signature.
        ///
        /// <para>
        /// Three sections of the boards are <see cref="TrapdoorPlatform"/>s on
        /// staggered auto-cycles. Each shakes for its authored warning before its
        /// collision drops, so the floor never disappears without telling you
        /// first. Falling is not a punishment so much as a detour: the under-stage
        /// cellar holds the second extractor, and one-way scaffolding under every
        /// trap climbs back to board level - through the trap the next time it
        /// opens, or up the tiring-house stair at the east end, which is a one-way
        /// platform at board height so the eastward route is never a hole.
        /// </para>
        /// </summary>
        private void BuildStage() {
            BuildRoomBackground("BG_Room2_Stage", Room2StartX, 3200, LevelHeight,
                new Color(0.16f, 0.11f, 0.07f, 0.45f));

            // Board segments, with exactly TrapdoorWidth gaps where the traps sit.
            BuildFloor(StageFrontEastDeckStartX, GroundY, 3050f - StageFrontEastDeckStartX);   // 2840..3050
            BuildFloor(3270, GroundY, 520);   // 3270..3790
            BuildFloor(4010, GroundY, 520);   // 4010..4530
            BuildFloor(4750, GroundY, 810);   // 4750..5560

            // Tiring-house stair head: completes the board surface over the cellar
            // stairwell without turning the eastward walk into a fall.
            BuildOneWayPlatform(5660, GroundY, 200);

            // Under-stage cellar. GR-LV4: its west end follows the lock, and the wall reaches
            // the boards' underside, because the stage front has no cellar under it.
            BuildFloor(CellarWestWallX, CellarY, 5760f - CellarWestWallX, CellarColor);
            BuildWall(CellarWestWallX, BoardUndersideY, CellarY + 40f - BoardUndersideY, CellarColor);
            BuildWall(5740, 1180, 360, CellarColor);

            // Scaffolding under each trap: two drop-through lifts back to the
            // boards. The traps auto-cycle, so this route always reopens.
            foreach (float centreX in new[] { 3160f, 3900f, 4640f }) {
                BuildOneWayPlatform(centreX, 1360, 200, CellarColor);
                BuildOneWayPlatform(centreX, 1240, 200, CellarColor);
            }

            // The tiring-house stair, cellar side.
            BuildPlatform(5560, 1380, 200, CellarColor);
            BuildPlatform(5660, 1250, 180, CellarColor);

            // Side galleries flanking the stage: the balconies the design calls for.
            // GR-LV4: the first moved 2900 -> 3160, wholly inside the carved lock.
            BuildPlatform(3160, 880, 240, GalleryColor);
            BuildPlatform(3600, 760, 220, GalleryColor);
            BuildPlatform(4400, 880, 240, GalleryColor);
            BuildPlatform(5100, 760, 220, GalleryColor);

            BuildRoomDecoration(Room2StartX, "globe_room_stage", new Color(0.98f, 0.72f, 0.36f));

            // GU-L8: just inside the stage lock's area (it was centred on the 2560
            // seam), so a hero pressed against the yard's exit barrier does not reach
            // it. The stage's cast is its lock's wave (it takes the boards on engage).
            BuildRoomTransition("globe_room_stage", new Vector2(StageLockMinX + 40f, 800), Room2CameraBounds,
                triggerSize: new Vector2(80, LevelHeight),
                onEntered: _ => {
                    _stageReached = true;
                    RefreshObjective();
                });
        }

        /// <summary>
        /// Room 3: the galleries, and the level's vertical section.
        ///
        /// <para>
        /// Four rigging lines ladder three balcony tiers (880 / 660 / 420). The
        /// mandatory route climbs to the middle tier and crosses east over a wall
        /// that seals the ground-level yard, so the climb cannot be walked past;
        /// the fourth line up to the upper gallery is the optional branch that
        /// guards the third extractor.
        /// </para>
        ///
        /// <para>
        /// The contrast with Nassau (which uses the same component) is
        /// deliberate and is expressed entirely in the anchors' authored numbers:
        /// arcs no wider than <see cref="MaxRiggingAmplitudeDegrees"/>, periods no
        /// slower than <see cref="MaxRiggingPeriodSeconds"/>, phases staggered a
        /// quarter apart so a chain of them has to be timed, horizontal gaps of
        /// roughly a rope-width rather than a ship-length, and a release assist
        /// capped at <see cref="RiggingMaxLaunchSpeed"/>. A Globe rope buys you a
        /// balcony; a Nassau rope buys you a hull.
        /// </para>
        /// </summary>
        private void BuildGalleries() {
            BuildRoomBackground("BG_Room3_Galleries", Room3StartX, 2560, LevelHeight,
                new Color(0.12f, 0.10f, 0.09f, 0.45f));

            BuildFloor(Room3StartX, GroundY, 2560);

            // Ground-level approach to the lowest tier.
            BuildPlatform(5900, 1000, 200);

            // Lower tier (880).
            BuildPlatform(6060, 880, 240, GalleryColor);
            BuildPlatform(6620, 880, 220, GalleryColor);

            // Middle tier (660).
            BuildPlatform(7000, 660, 240, GalleryColor);
            BuildPlatform(7500, 660, 220, GalleryColor);

            // The step that puts the fourth rope in reach; the upper tier is optional.
            BuildPlatform(7560, 540, 140, GalleryColor);

            // Upper tier (420) - the optional branch, holding extractor 3.
            BuildPlatform(7940, 420, 300, GalleryColor);

            // Mandatory eastward run at middle-tier height, ending on a span that
            // bridges the wall below into the tiring-house.
            BuildPlatform(7800, 640, 180, GalleryColor);
            BuildPlatform(8100, 600, 200, GalleryColor);
            BuildPlatform(GalleryBridgeX, GalleryBridgeY, GalleryBridgeWidth, GalleryColor);

            // The wall that makes the climb mandatory: the yard floor dead-ends and
            // the only way through to the tiring-house is over the middle tier. It
            // rises to the bridging span's underside, so no gap is left beneath it.
            BuildWall(YardWallX, YardWallTopY, GroundY + DefaultFloorThickness - YardWallTopY);

            // GU-L8: the Middle anchor, past the stage lock's exit (it stood at 5400,
            // in the stage fight).
            BuildCheckpoint(MiddleCheckpointX, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room3StartX, "globe_room_galleries", new Color(0.86f, 0.66f, 0.94f));

            BuildRoomTransition("globe_room_galleries", new Vector2(Room3StartX + 30, 800), Room3CameraBounds,
                triggerSize: new Vector2(80, LevelHeight),
                onEntered: _ => {
                    _galleriesReached = true;
                    RefreshObjective();
                });
        }

        /// <summary>
        /// Room 4: the tiring-house stage, where the Tragedy King is performing the
        /// collapse. Flat open boards with two tiered gallery balconies at the
        /// sides, and two more trapdoors on a much slower, much longer-telegraphed
        /// cycle than the ones in room 2 - a boss arena that eats you for standing
        /// on the wrong plank would not be fair.
        ///
        /// <para>
        /// Falling through an arena trap lands in a shallow prompt recess, not a
        /// pit, and the recess's middle section is a one-way platform, so getting
        /// back on stage never depends on a trapdoor's timing.
        /// </para>
        ///
        /// <para>
        /// The recess is closed at both ends (2026-10-09): a wall under each board
        /// edge, flush with the outer edge of its trap gap, runs from the boards'
        /// underside down to the recess floor's underside, so the only way out is
        /// back up onto the stage. Until then both ends were open. The floor sat
        /// 100 px under the boards with nothing beyond its edges, so the 64 px hero
        /// could walk or roll out under either board, or be carried out by a
        /// movement ability, a Special or an Ultimate, into a bottomless drop (three
        /// Level 10 bot runs fell out of the world there, VERIFY-L10-ARENA-RECESS-EXIT).
        /// Level10ContentTests floods the space under every trap and pins that
        /// nothing below the boards reaches the void.
        /// </para>
        ///
        /// <para>
        /// 1,920 px of arena against the Tragedy King's authored 8.0-unit ranged
        /// band, which is 480 px at BossController's 60 px per unit. Level10ContentTests
        /// asserts that against the resource, never against a literal.
        /// </para>
        /// </summary>
        private void BuildTiringHouseStage() {
            BuildRoomBackground("BG_Room4_TiringHouse", Room4StartX, 1920, LevelHeight,
                new Color(0.19f, 0.09f, 0.13f, 0.50f));

            // Board segments with the two arena trap gaps at 8970..9190 and 9450..9670.
            BuildFloor(Room4StartX, GroundY, ArenaRecessWestX - Room4StartX);       // 8320..8970
            BuildFloor(ArenaRecessEastX, GroundY, LevelWidth - ArenaRecessEastX);   // 9670..10240

            // Prompt recess under the traps, and the drop-through island that always
            // returns the player to the boards regardless of trapdoor state.
            BuildFloor(ArenaRecessWestX, ArenaRecessFloorY, ArenaRecessEastX - ArenaRecessWestX, CellarColor);
            BuildOneWayPlatform(9320, GroundY, 260);

            // ...closed at both ends under the boards, so nothing leaves it sideways:
            // 8950..8970 and 9670..9690, y 1160..1300. Each wall stands wholly under
            // its board, so neither trap gap nor the island is narrowed.
            BuildWall(ArenaRecessWestX - DefaultWallThickness, BoardUndersideY, ArenaRecessWallHeight, CellarColor);
            BuildWall(ArenaRecessEastX, BoardUndersideY, ArenaRecessWallHeight, CellarColor);

            // Two tiered gallery balconies, one each side.
            BuildPlatform(8580, 860, 240, GalleryColor);
            BuildPlatform(8580, 620, 200, GalleryColor);
            BuildPlatform(9980, 860, 240, GalleryColor);
            BuildPlatform(9980, 620, 200, GalleryColor);

            BuildWall(10220, 0, LevelHeight);

            BuildCheckpoint(PreBossCheckpointX, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "globe_room_tiring_house", new Color(0.98f, 0.40f, 0.52f));
            // GU-L8: past the galleries' exit barrier (8330-8350), so a hero pressed
            // against it on the bridge does not reach the trigger (it stood at 8350).
            BuildRoomTransition("globe_room_tiring_house", new Vector2(Room4StartX + 70, 800), Room4CameraBounds,
                triggerSize: new Vector2(80, LevelHeight));

            // Package 13 W4 (S35): backstage, where the prompt-book stops mid-scene.
            // Before the boss reveal (BossArenaWestX), so the two sequences never collide.
            BuildAbsenceTrigger(new Vector2(Room4StartX + 200, GroundY - 200));

            _kingEncounter = BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, EnemyGroundY),
                "TragedyKingEncounter", BossArenaBounds);
            if (_kingEncounter != null) _kingEncounter.PhaseEntered += OnKingPhaseEntered;
            // GAP-07: the arena boards hold still until the King's second act.
            SetArenaTrapdoorsCycling(false);
        }

        // === Package 12 W9 (GAP-07) — the Tragedy King's Phase 2 stage ===
        //
        // Design §6: "P2: summons two spectral actors who perform scripted attack
        // 'scenes' while the stage trapdoors cycle; the King is invulnerable
        // mid-soliloquy until both actors take their bow." The soliloquy — the
        // phase-entry summon, the invulnerability and the bow — is authored on
        // tragedy_king.tres (GuardedSummonMinPhase / GuardedSummonSceneSeconds) and
        // enforced by BossController. The stage half lives here: the two arena
        // trapdoors stay shut through Phase 1 and start their slow, heavily
        // telegraphed cycle the moment Phase 2 begins.

        private BossEncounterController _kingEncounter;

        /// <summary>The boss encounter (test seam).</summary>
        public BossEncounterController KingEncounter => _kingEncounter;

        /// <summary>True once Phase 2 has set the arena trapdoors cycling.</summary>
        public bool ArenaTrapdoorsCycling { get; private set; }

        private void OnKingPhaseEntered(int phase) {
            if (phase >= 1) SetArenaTrapdoorsCycling(true);
        }

        /// <summary>Starts or holds the arena boards (closed and re-seeded either way).</summary>
        public void SetArenaTrapdoorsCycling(bool cycling) {
            ArenaTrapdoorsCycling = cycling;
            foreach (TrapdoorPlatform door in _arenaTrapdoors) {
                if (!IsInstanceValid(door)) continue;
                door.Close();
                door.Enabled = cycling;
            }
        }

        // === Encounters ===
        //
        // GU-L8 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles, never
        // more than the table. Concurrency peaks at 4 live standards, inside the
        // level's warm pool counts (standard_enemy 12, enemy_projectile 20).

        // === Era mechanic: the audience ===

        /// <summary>
        /// GU-L8 (2026-10-12): true while one of this level's encounter locks is
        /// engaged. Turn-taking (the engagement director) makes standing still and
        /// blocking normal in a lock fight, so the audience's stillness clock pauses
        /// there: it neither builds nor resets, and resumes from where it stood once
        /// the lock clears or reverts. A throw already telegraphed still resolves.
        /// </summary>
        public bool AudienceHeldByEncounterLock => ActiveEncounterLock != null;

        public override void _PhysicsProcess(double delta) => TickAudience((float)delta);

        /// <summary>
        /// The design's unique Globe beat: an audience that throws things at
        /// anyone who stops moving.
        ///
        /// <para>
        /// Standing still for <see cref="AudienceIdleArmSeconds"/> arms a throw
        /// aimed at exactly where the player is standing. The prop is telegraphed
        /// for <see cref="AudienceWarningSeconds"/> before it lands, and walking
        /// <see cref="AudienceEscapeRadius"/> px away at any point during that
        /// telegraph disarms it outright. That one radius is both the escape
        /// distance and the strike radius, so the throw can never reach further
        /// than the escape it advertises - this is a nudge to keep moving, never a
        /// cheap shot.
        /// </para>
        ///
        /// <para>
        /// Public and dt-driven so it can be stepped deterministically from a test
        /// rather than only from <see cref="_PhysicsProcess"/>.
        /// </para>
        /// </summary>
        public void TickAudience(float dt) {
            if (dt <= 0f) return;
            if (_audienceThrow == null || !AudienceWatchEnabled) return;
            if (Player == null || !IsInstanceValid(Player)) return;

            // Package 12 W1 (GAP-11): the galleries are part of the world. While
            // a Time Freeze, the Post-Landing Hold or a boss suspension holds it,
            // nothing counts — no idle build-up, no telegraph countdown, no
            // throw — and nothing catches up at the thaw. The position baseline
            // follows the hero so the thaw tick does not read the freeze's travel
            // as one enormous step.
            if (Player.TimeFrozen || Player.IsRecoveryWorldHeld) {
                _lastPlayerPosition = Player.GlobalPosition;
                _lastPlayerPositionValid = true;
                return;
            }

            // The landed prop is only live for a beat; the component's own cycle is
            // switched off, so the level owns putting it away again.
            if (_audienceActiveTimer > 0f) {
                _audienceActiveTimer -= dt;
                if (_audienceActiveTimer <= 0f) {
                    _audienceActiveTimer = 0f;
                    _audienceThrow.ForcePhase(HazardPhase.Cooldown, 0f);
                }
            }

            Vector2 position = Player.GlobalPosition;
            if (!_lastPlayerPositionValid) {
                _lastPlayerPosition = position;
                _lastPlayerPositionValid = true;
            }
            float speed = position.DistanceTo(_lastPlayerPosition) / dt;
            _lastPlayerPosition = position;

            if (AudienceArmed) {
                if (position.DistanceTo(AudienceMarkedSpot) > AudienceEscapeRadius) {
                    DisarmAudience();
                    return;
                }
                _audienceWarningTimer -= dt;
                if (_audienceWarningTimer <= 0f) LandAudienceThrow();
                return;
            }

            // GU-L8: the stillness clock pauses while a lock is engaged (the
            // position baseline above still follows the hero, so the first tick
            // after the clear reads no travel as an enormous step).
            if (AudienceHeldByEncounterLock) return;

            AudienceIdleSeconds = speed < AudienceIdleSpeedThreshold ? AudienceIdleSeconds + dt : 0f;
            if (AudienceIdleSeconds >= AudienceIdleArmSeconds) ArmAudience(position);
        }

        /// <summary>Winds the galleries up at the spot the player is standing on.</summary>
        private void ArmAudience(Vector2 position) {
            AudienceArmed = true;
            AudienceIdleSeconds = 0f;
            AudienceMarkedSpot = position;
            _audienceWarningTimer = AudienceWarningSeconds;
            // The prop lands on the boards under the player, so the hazard's
            // 220 px column reads as something falling past them onto the stage.
            _audienceThrow.GlobalPosition = new Vector2(position.X, position.Y + 56f);
            _audienceThrow.ForcePhase(HazardPhase.Warning, AudienceWarningSeconds);
            SetObjective("globe_objective_heckled");
        }

        /// <summary>The player moved. Nothing is thrown and the watch starts over.</summary>
        private void DisarmAudience() {
            if (!AudienceArmed) return;
            AudienceArmed = false;
            AudienceIdleSeconds = 0f;
            _audienceWarningTimer = 0f;
            _audienceThrow?.ForcePhase(HazardPhase.Cooldown, 0f);
            RefreshObjective();
        }

        private void LandAudienceThrow() {
            _audienceThrow.ForcePhase(HazardPhase.Active, AudienceActiveSeconds);
            _audienceActiveTimer = AudienceActiveSeconds;
            // Redundant with the escape check above by construction, and kept
            // anyway: it is the line that makes "the throw cannot reach further
            // than the escape radius" true no matter how the caller steps time.
            if (Player != null && IsInstanceValid(Player) &&
                Player.GlobalPosition.DistanceTo(AudienceMarkedSpot) <= AudienceEscapeRadius &&
                _audienceThrow.ApplyToPlayer(Player)) {
                AudienceThrowsLanded++;
            }
            AudienceArmed = false;
            _audienceWarningTimer = 0f;
            AudienceIdleSeconds = 0f;
            RefreshObjective();
        }

        /// <summary>
        /// Puts the galleries back to sleep. Called on a checkpoint resume and on
        /// every Chronal Rewind: a run must never resume or rewind into a throw
        /// that was already halfway through its telegraph.
        /// </summary>
        public void ResetAudienceWatch() {
            AudienceArmed = false;
            AudienceIdleSeconds = 0f;
            _audienceWarningTimer = 0f;
            _audienceActiveTimer = 0f;
            _lastPlayerPositionValid = false;
            _audienceThrow?.ForcePhase(HazardPhase.Cooldown, 0f);
        }

        // === Era mechanic: the stage boards ===

        /// <summary>
        /// Shuts and re-seeds every trapdoor in the level. A resumed or rewound
        /// run must find solid boards, not a hole that happened to be open when
        /// the save was written.
        /// </summary>
        public void CloseAllTrapdoors() {
            foreach (TrapdoorPlatform door in AllTrapdoors) {
                if (IsInstanceValid(door)) door.Close();
            }
        }

        /// <summary>True when <paramref name="x"/> stands over any authored trapdoor gap.</summary>
        public static bool IsOverATrapdoor(float x) {
            foreach (float centre in TrapdoorCentresX) {
                if (Mathf.Abs(x - centre) <= TrapdoorWidth / 2f) return true;
            }
            return false;
        }

        // === Resume, lifecycle, and objectives ===

        // Package 12 W2 (GAP-13): explicit encounter baseline. GU-L8: the encounters
        // are the three locks; every anchor lists every lock before it, and a resume
        // builds those Cleared (the yard used to respawn behind the hero on every
        // resume).
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockYard, LockStage },
                [Checkpoint2] = new[] { LockYard, LockStage, LockGalleries }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>GU-L8: a cleared or reverted lock hands the objective back to the level's own.</summary>
        protected override void RestoreLevelObjective() => RefreshObjective();

        /// <summary>The anchor's non-encounter world state; the locks come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            // FIX-LEVEL (2026-10-13): since GU-L8 moved it to 6040 the Middle
            // anchor stands in the galleries, east of their room trigger
            // (5750-5830), which a resume there never crosses — so Checkpoint1
            // also means the galleries were reached, or the objective read "cross
            // the stage" in the galleries and again after their lock's clear.
            if (checkpointID == Checkpoint1 || checkpointID == Checkpoint2) {
                _stageReached = true;
                _galleriesReached = true;
            } else {
                return;
            }
            // Both mid-level checkpoints stand on solid boards, but the traps and
            // the galleries are stateful, so neither is left mid-cycle.
            CloseAllTrapdoors();
            ResetAudienceWatch();
        }

        protected override void OnLevelReady() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnRewindTriggered += OnStoryRewind;
                _rewindBound = true;
            }
            if (ResumedMidLevel) {
                CloseAllTrapdoors();
                ResetAudienceWatch();
            }
            RefreshObjective();
        }

        /// <summary>
        /// A rewind restores the player to a safe grounded frame; the trapdoors
        /// restore themselves through their own <see cref="IStoryRewindable"/>
        /// policy. The one thing nothing else owns is the audience, which would
        /// otherwise finish a telegraph the player has already been rewound out of.
        /// </summary>
        private void OnStoryRewind(Vector2 targetPosition) => ResetAudienceWatch();

        public override void _ExitTree() {
            if (_rewindBound && EventBus.Instance != null) {
                EventBus.Instance.OnRewindTriggered -= OnStoryRewind;
            }
            _rewindBound = false;
            base._ExitTree();
        }

        private void RefreshObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (_galleriesReached) SetObjective("globe_objective_climb");
            else if (_stageReached) SetObjective("globe_objective_cross_stage");
            else SetObjective(InitialObjectiveKey);
        }
    }
}
