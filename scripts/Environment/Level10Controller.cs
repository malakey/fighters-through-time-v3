using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 10 - The Globe Theatre, London 1599. Opening night of <i>Hamlet</i>.
    /// The Unbound has seeded neural-manipulators through the galleries and
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
    /// <c>chrono_slasher</c> (Unbound shock-trooper standard).
    /// </para>
    ///
    /// <para>
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 10 standards,
    /// 0 elites, 1 boss, 3 extractors. The authored tables below are the single
    /// source of those counts and Level10ContentTests asserts them.
    /// </para>
    /// </summary>
    public partial class Level10Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_10_globe";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/tragedy_king.tres";
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
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "globe_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(240, EnemyGroundY);
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
        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, LevelHeight - 1080f, 2560, 1080);
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

        /// <summary>The Yard: groundlings the Unbound has already turned, standing in the pit.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room1Spawns = {
            ("holo_page", new Vector2(1180, EnemyGroundY), new Vector2(1020, EnemyGroundY), new Vector2(1340, EnemyGroundY)),
            ("chrono_slasher", new Vector2(1720, EnemyGroundY), new Vector2(1560, EnemyGroundY), new Vector2(1880, EnemyGroundY)),
            ("holo_page", new Vector2(2280, EnemyGroundY), new Vector2(2120, EnemyGroundY), new Vector2(2420, EnemyGroundY))
        };

        /// <summary>
        /// The Stage: the cast the Archive replaced. Deliberately posted on the
        /// solid board segments and never straddling a trapdoor gap, so the floor
        /// dropping is a threat to the player and not a free enemy delete.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room2Spawns = {
            ("chrono_slasher", new Vector2(2830, EnemyGroundY), new Vector2(2680, EnemyGroundY), new Vector2(2980, EnemyGroundY)),
            ("holo_page", new Vector2(4270, EnemyGroundY), new Vector2(4120, EnemyGroundY), new Vector2(4420, EnemyGroundY)),
            ("chrono_slasher", new Vector2(5160, EnemyGroundY), new Vector2(5010, EnemyGroundY), new Vector2(5310, EnemyGroundY))
        };

        /// <summary>
        /// The Galleries: two in the yard behind the stage and two posted on the
        /// balcony tiers, so the rigging climb is contested. Four is the level's
        /// concurrency peak, well inside level_10_pool_config's standard_enemy
        /// warm count of 12 and its enemy_projectile warm count of 20.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2 PatrolA, Vector2 PatrolB)[] Room3Spawns = {
            ("holo_page", new Vector2(5960, EnemyGroundY), new Vector2(5840, EnemyGroundY), new Vector2(6120, EnemyGroundY)),
            ("chrono_slasher", new Vector2(6620, 830), new Vector2(6530, 830), new Vector2(6710, 830)),
            ("holo_page", new Vector2(7000, 610), new Vector2(6910, 610), new Vector2(7100, 610)),
            ("chrono_slasher", new Vector2(7940, 370), new Vector2(7830, 370), new Vector2(8060, 370))
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
        private bool _room2WaveSpawned;
        private bool _room3WaveSpawned;
        private bool _rewindBound;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildYard();
            BuildStage();
            BuildGalleries();
            BuildTiringHouseStage();
            BuildExtractors(ExtractorPlacements);
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

            BuildFloor(Room1StartX, GroundY, 2560);
            BuildWall(-20, 0, LevelHeight);

            // Groundling clutter: stacked barrels and the gallery stair.
            BuildPlatform(520, 980, 220);
            BuildPlatform(960, 860, 200);    // holds extractor 1
            BuildPlatform(1420, 960, 220);
            BuildPlatform(1980, 900, 240);

            // The lowest gallery rail overhanging the pit - decor that also gives
            // the player a way to break line of sight from a heckling crowd.
            BuildOneWayPlatform(1180, 760, 240, GalleryColor);
            BuildOneWayPlatform(2280, 740, 240, GalleryColor);

            BuildCheckpoint(240, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "globe_room_yard", new Color(0.95f, 0.80f, 0.48f));
            BuildRoomTransition("globe_room_yard", new Vector2(340, 800), Room1CameraBounds,
                triggerSize: new Vector2(80, LevelHeight));
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
            BuildFloor(2560, GroundY, 490);   // 2560..3050
            BuildFloor(3270, GroundY, 520);   // 3270..3790
            BuildFloor(4010, GroundY, 520);   // 4010..4530
            BuildFloor(4750, GroundY, 810);   // 4750..5560

            // Tiring-house stair head: completes the board surface over the cellar
            // stairwell without turning the eastward walk into a fall.
            BuildOneWayPlatform(5660, GroundY, 200);

            // Under-stage cellar.
            BuildFloor(Room2StartX, CellarY, 3200, CellarColor);
            BuildWall(2540, 1180, 360, CellarColor);
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
            BuildPlatform(2900, 880, 240, GalleryColor);
            BuildPlatform(3600, 760, 220, GalleryColor);
            BuildPlatform(4400, 880, 240, GalleryColor);
            BuildPlatform(5100, 760, 220, GalleryColor);

            BuildCheckpoint(5400, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "globe_room_stage", new Color(0.98f, 0.72f, 0.36f));

            BuildRoomTransition("globe_room_stage", new Vector2(Room2StartX, 800), Room2CameraBounds,
                triggerSize: new Vector2(80, LevelHeight),
                onEntered: _ => {
                    _stageReached = true;
                    TriggerRoom2Wave();
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
            BuildPlatform(8240, 620, 180, GalleryColor);

            // The wall that makes the climb mandatory: the yard floor dead-ends and
            // the only way through to the tiring-house is over the middle tier.
            BuildWall(8280, 700, 460);

            BuildRoomDecoration(Room3StartX, "globe_room_galleries", new Color(0.86f, 0.66f, 0.94f));

            BuildRoomTransition("globe_room_galleries", new Vector2(Room3StartX + 30, 800), Room3CameraBounds,
                triggerSize: new Vector2(80, LevelHeight),
                onEntered: _ => {
                    _galleriesReached = true;
                    TriggerRoom3Wave();
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
        /// 1,920 px of arena against the Tragedy King's authored 8.0-unit ranged
        /// band, which is 480 px at BossController's 60 px per unit. Level10ContentTests
        /// asserts that against the resource, never against a literal.
        /// </para>
        /// </summary>
        private void BuildTiringHouseStage() {
            BuildRoomBackground("BG_Room4_TiringHouse", Room4StartX, 1920, LevelHeight,
                new Color(0.19f, 0.09f, 0.13f, 0.50f));

            // Board segments with the two arena trap gaps at 8970..9190 and 9450..9670.
            BuildFloor(8320, GroundY, 650);   // 8320..8970
            BuildFloor(9670, GroundY, 570);   // 9670..10240

            // Prompt recess under the traps, and the drop-through island that always
            // returns the player to the boards regardless of trapdoor state.
            BuildFloor(8970, 1260, 700, CellarColor);
            BuildOneWayPlatform(9320, GroundY, 260);

            // Two tiered gallery balconies, one each side.
            BuildPlatform(8580, 860, 240, GalleryColor);
            BuildPlatform(8580, 620, 200, GalleryColor);
            BuildPlatform(9980, 860, 240, GalleryColor);
            BuildPlatform(9980, 620, 200, GalleryColor);

            BuildWall(10220, 0, LevelHeight);

            BuildCheckpoint(8420, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "globe_room_tiring_house", new Color(0.98f, 0.40f, 0.52f));
            BuildRoomTransition("globe_room_tiring_house", new Vector2(Room4StartX + 30, 800), Room4CameraBounds,
                triggerSize: new Vector2(80, LevelHeight));

            _kingEncounter = BuildBossEncounter(BossResourcePath, new Vector2(9900, EnemyGroundY),
                encounterName: "TragedyKingEncounter", revealDistance: 900f);
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
        /// Authored counts are the Normal-difficulty truth; ScaleEncounterCount is
        /// the only thing allowed to move them, and it never exceeds the authored
        /// table. Concurrency peaks at 4 live standards, inside the level's warm
        /// pool counts (standard_enemy 12, enemy_projectile 20).
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

        // === Era mechanic: the audience ===

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

        // Package 12 W2 (GAP-13): explicit encounter baseline.
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

        /// <summary>Test seam: whether the room-2 wave latch is set (spawn suppressed).</summary>
        public bool Room2WaveLatched => _room2WaveSpawned;

        /// <summary>Test seam: whether the room-3 wave latch is set (spawn suppressed).</summary>
        public bool Room3WaveLatched => _room3WaveSpawned;

        /// <summary>The anchor's non-encounter world state; the waves come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint1) {
                _stageReached = true;
            } else if (checkpointID == Checkpoint2) {
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
