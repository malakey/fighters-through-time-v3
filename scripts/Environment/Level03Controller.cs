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
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L0): each of
    /// the three approach rooms is an <see cref="EncounterLock"/> the hero must
    /// clear — the Midway (700–2500 since GR-LV1, its fairground crowd spawning in as the hero
    /// steps onto the midway), the Electricity Building floor (3840–5780; the
    /// crown slasher, which stood on the unreachable crown, now holds the floor
    /// under the descent) and the routing hall (6115–8290, which also needs the
    /// beam routed; its exit barrier fills the Court of Honor door under its
    /// column). One lock wave per manifest row. The Middle anchor moved from 6180
    /// to the gap between the Electricity and routing locks (6000), the PreBoss
    /// anchor from 8420 to 8500 (out of swing reach from inside the routing hall),
    /// and every checkpoint lists the locks before it.</para>
    ///
    /// <para><b>The Skyway</b> (gameplay review 2026-11-03, rulings 7a-7c+; GR-LV1):
    /// the Midway lock was carved to 700-2500, and the freed span is a traversal
    /// stretch (2520-3820) over a non-lethal trench. Its optional route, the Ferris
    /// line, holds the Midway's Extractor. The Electricity lock takes the level's
    /// swarm row. Nothing past the stretch moved.</para>
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
        /// per-hero median measurement pass. GR-LV1 adds the Skyway's +30.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(540f, 1);
        public override string LevelTitleKey => "chicago_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_03_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240, GroundY - 50);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the upper Midway balcony (Platform 1000/1100, top 1092) above the first
        // arc lamp, climbed from the low stand at 500 — a detour the floor route passes under.
        protected override Vector2? SecretCachePosition => new(1000f, 997f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (8500 since GU-L0) on the court floor, 240 px short of the arena (9000).
        protected override Vector2? RestorationFontPosition => new(8760f, GroundY);

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

        private const float CourtDoorWidth = 40f;
        private const float CourtDoorHeight = 720f;
        private static readonly Color CourtDoorColor = new(0.3f, 0.36f, 0.46f);

        /// <summary>
        /// P1 (2026-10-04): the Court of Honor arena. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 9800 - 800), so the pre-boss anchor
        /// at 8420 and the west coil platform stay outside; east edge is the court
        /// wall's inner face.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, LevelWidth - 20f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 9800f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 800 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 800f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

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

        /// <summary>Room 1 - Midway. Drones strafe the arc lamps, a Severed trooper works the crowd.</summary>
        public static readonly EnemySpawn[] MidwayWave = {
            new("voltaic_shock_drone", new Vector2(1100, 980), 260f),
            new("chrono_slasher", new Vector2(1900, GroundY - 50), 300f),
            // GR-LV1: 2900 -> 2150, inside the carved lock.
            new("voltaic_shock_drone", new Vector2(2150, 900), 300f)
        };

        /// <summary>
        /// Room 2 - Electricity Building climb. GU-L0 (2026-10-13): the third
        /// slasher used to stand on the crown platform (5240, 340), about 1060 px
        /// above the floor route, outside its aggro radius, never coming down — a
        /// member no lock could ever count as cleared. It now holds the floor
        /// under the descent (5400), where the route runs; the crown keeps its
        /// Extractor.
        /// </summary>
        public static readonly EnemySpawn[] ElectricityWave = {
            new("chrono_slasher", new Vector2(4300, GroundY - 50), 320f),
            new("voltaic_shock_drone", new Vector2(4600, 640), 240f),
            new("chrono_slasher", new Vector2(5400, GroundY - 50), 250f)
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
            // GR-LV1: the end of the Ferris line, over the Skyway (it stood on the Midway's awning).
            ("level_03_extractor_0", new Vector2(FerrisLineEndX, 738)),
            // Crown of the Electricity Building climb.
            ("level_03_extractor_1", new Vector2(5240, 300)),
            // Gallery catwalk between the two upper coils.
            ("level_03_extractor_2", new Vector2(7360, 880))
        };

        // === GU-L0 (2026-10-13): the three encounter locks ==================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockMidway = ChicagoLevelID + "_lock_midway";
        public const string LockElectricity = ChicagoLevelID + "_lock_electricity";
        public const string LockRoutingHall = ChicagoLevelID + "_lock_routing_hall";

        /// <summary>The manifest rows the three lock waves are (level_03_rewards.tres).</summary>
        public const string MidwayRowID = "level_03.midway";
        public const string ElectricityRowID = "level_03.electricity";
        public const string RoutingHallRowID = "level_03.routing_hall";

        /// <summary>
        /// The Midway lock: the fairground floor from past the entry anchor (240)
        /// and the low stand (350–650; every lock edge sits in a gap between
        /// platforms, so no barrier cuts one) to 2500. GR-LV1 carved it from 3820, just past
        /// the second arc lamp (2340-2460), so a hero pressed against its exit stands clear
        /// of the lamp. The Skyway takes the rest.
        /// </summary>
        public const float MidwayLockMinX = 700f;
        public const float MidwayLockMaxX = 2500f;

        /// <summary>
        /// The Electricity lock: the dynamo hall from the seam to the gap under
        /// the descent (between the 5620 and 5960 platforms, so its exit barrier
        /// cuts no platform).
        /// </summary>
        public const float ElectricityLockMinX = ElectricityStartX;
        public const float ElectricityLockMaxX = 5780f;

        /// <summary>
        /// The routing-hall lock: from the gap past the Middle anchor (between the
        /// 5960 and 6240 platforms) to the Court of Honor door, whose puzzle it
        /// also needs solved.
        /// </summary>
        public const float RoutingLockMinX = 6115f;
        public const float RoutingLockMaxX = CourtDoorX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;

        /// <summary>The Court of Honor door's centre x (it stands on the routing hall's east edge).</summary>
        public const float CourtDoorX = BossStartX - 20f;

        /// <summary>
        /// The Middle anchor, in the gap between the Electricity and routing locks
        /// (it stood at 6180, inside the routing hall's area). Its strike surface
        /// (5970–6030) lies past the roster's longest basic swing from a hero
        /// pressed against the Electricity lock's closed exit (pivot 5760).
        /// </summary>
        public const float MiddleCheckpointX = 6000f;

        /// <summary>
        /// The PreBoss anchor (8420 before GU-L0): its strike surface (8470–8530)
        /// lies past the longest basic swing from a hero pressed against the
        /// routing hall's closed exit barrier (pivot 8270) or the Court door
        /// (pivot 8260); the guard refuses it anyway until the hall is cleared.
        /// </summary>
        public const float PreBossCheckpointX = 8500f;

        private EncounterLock _midwayLock;
        private EncounterLock _electricityLock;
        private EncounterLock _routingLock;

        public EncounterLock MidwayLock => _midwayLock;
        public EncounterLock ElectricityLock => _electricityLock;
        public EncounterLock RoutingHallLock => _routingLock;

        // === GR-LV1: the Skyway, the Ferris line and the swarm row ===============

        public const string SkywayStretchID = "level_03.skyway";
        public const string FerrisRouteID = "level_03.ferris_line";
        public const string SkywayRoomKey = "chicago_room_skyway";

        /// <summary>From the Midway lock's exit barrier to the Electricity lock's entry barrier.</summary>
        public const float SkywayMinX = 2520f;
        public const float SkywayMaxX = ElectricityStartX - EncounterLockRules.DefaultBarrierWidthPixels;
        public const float SkywayWestDeckEndX = 2680f;
        public const float SkywayEastDeckStartX = 3660f;

        /// <summary>The Ferris line's last platform; the Extractor stands 72 px over its top.</summary>
        public const float FerrisLineEndX = 3585f;

        /// <summary>The Electricity lock's swarm row (level_03_rewards.tres).</summary>
        public const string SwarmRowID = "level_03.swarm1";

        /// <summary>The row, Hard's truth (a 1940 px lock takes 8), along the dynamo hall's floor.</summary>
        public static readonly float[] SwarmSpawnXs = { 4050f, 4230f, 4410f, 4590f, 4770f, 4950f, 5130f, 5560f };

        private void BuildSkyway() {
            TraversalStretch skyway = BeginStretch(SkywayStretchID, SkywayRoomKey,
                SkywayMinX, SkywayMaxX, GroundY, SkywayWestDeckEndX, SkywayEastDeckStartX, MidwayStartX);
            BuildRoomDecoration(SkywayMinX, SkywayRoomKey, new Color(0.7f, 0.85f, 1f));
            BuildStretchTrench(skyway);
            // The deck resumes under the Electricity lock's entry barrier: that barrier needs floor under it.
            BuildFloor(SkywayEastDeckStartX, GroundY, ElectricityStartX - SkywayEastDeckStartX);

            BuildStretchStep(skyway, 2800, 1290, 200);
            BuildStretchStep(skyway, 3040, 1220, 180, StretchStepKind.Crumbling, "level_03.crumbling_awning");
            TraversalSurface middleStep = BuildStretchStep(skyway, 3300, 1222, 200, StretchStepKind.OneWay);
            TraversalSurface eastStep = BuildStretchStep(skyway, 3520, 1220, 200);
            // On the deck, so the east ramp stays walkable: a double jump or the ledge clears it.
            // The last step ends 80 px short of it, or a hero jumping from the deck bonks the step's underside.
            BuildStretchWallStep(skyway, 3700f, 1190f, GroundY);

            // Off the line: a way back up out of the trench, and an arc lamp to cross down there.
            BuildOneWayPlatform(3260, 1390, 200);
            BuildCyclicHazardLane("level_03.arc_skyway", 3430f, skyway.TrenchFloorY, 0.9f, 0.8f, 2.4f, 14, withCover: true);

            OptionalRoute ferris = BeginOptionalRoute(FerrisRouteID, ExtractorPlacements[0].ID,
                ExtractorPlacements[0].Position, middleStep);
            BuildRouteStep(ferris, 3110, 1030, 120);
            TraversalSurface burstFrom = BuildRouteStep(ferris, 2890, 920, 160);
            BuildRouteStep(ferris, 3140, 820, 180);
            TraversalSurface end = BuildRouteStep(ferris, FerrisLineEndX, 820, 220);
            ferris.MarkBurstShortcut(burstFrom, end);
            ferris.MarkRiseShortcut(eastStep, end);
        }

        // === Runtime ===

        private PuzzleManager _beamPuzzle;
        private StaticBody2D _courtDoor;

        private readonly List<(PowerRoutingNode Tap, StoryCyclicHazard Hazard)> _crowdTaps = new();

        /// <summary>The beam-routing puzzle authored in the scene. Null outside the scene.</summary>
        public PuzzleManager BeamPuzzle => _beamPuzzle;

        /// <summary>True once the routing puzzle has opened the Court of Honor door.</summary>
        public bool CourtDoorOpen => _courtDoor == null || !IsInstanceValid(_courtDoor);

        // === Construction ===

        protected override void BuildLevel() {
            BuildMidway();
            BuildSkyway();
            BuildElectricityBuilding();
            BuildRoutingHall();
            BuildCourtOfHonor();
            BuildExtractors(ExtractorPlacements);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L0: the three approach rooms as encounter locks, one wave per
        /// manifest row. Built here — before the resume baseline is applied — so a
        /// resume past a lock builds it Cleared with no member ever spawned.
        /// </summary>
        private void BuildEncounterLocks() {
            float halfBarrier = EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
            // The Midway crowd arrives as the hero steps onto the fairground (it
            // used to spawn from a trigger at 760); the two ground rooms keep the
            // room cameras' bottom-anchored framing.
            _midwayLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockMidway,
                Area = EncounterLockSpec.Span(MidwayLockMinX, MidwayLockMaxX, LevelHeight),
                CameraBounds = new Rect2(MidwayLockMinX, GroundRoomCameraTop,
                    MidwayLockMaxX - MidwayLockMinX, LevelHeight - GroundRoomCameraTop),
                EntryBarrierX = MidwayLockMinX - halfBarrier,
                ExitBarrierX = MidwayLockMaxX + halfBarrier,
                BarrierFloorY = GroundY,
                Waves = new[] { WaveFrom(MidwayRowID, MidwayWave, EncounterWaveTrigger.OnEngage) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The dynamo hall's defenders stand waiting on its floor and climb.
            _electricityLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockElectricity,
                Area = EncounterLockSpec.Span(ElectricityLockMinX, ElectricityLockMaxX, LevelHeight),
                EntryBarrierX = ElectricityLockMinX - halfBarrier,
                ExitBarrierX = ElectricityLockMaxX + halfBarrier,
                BarrierFloorY = GroundY,
                Waves = new[] {
                    WaveFrom(ElectricityRowID, ElectricityWave, EncounterWaveTrigger.OnBuild),
                    SwarmWave(SwarmRowID, GroundY - 50f, SwarmSpawnXs)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The routing hall: its guards, and the beam routed into the receiver.
            // The exit barrier fills the Court of Honor door under its column; the
            // routing objective stays the level's own (no lock objective).
            _routingLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockRoutingHall,
                Area = EncounterLockSpec.Span(RoutingLockMinX, RoutingLockMaxX, LevelHeight),
                CameraBounds = new Rect2(RoutingLockMinX, GroundRoomCameraTop,
                    RoutingLockMaxX - RoutingLockMinX, LevelHeight - GroundRoomCameraTop),
                EntryBarrierX = RoutingLockMinX - halfBarrier,
                ExitBarrierX = CourtDoorX,
                ExitBarrierYRange = new Vector2(GroundY - CourtDoorHeight, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] { WaveFrom(RoutingHallRowID, RoutingHallWave, EncounterWaveTrigger.OnBuild) },
                RequiredPuzzleIDs = new[] { BeamPuzzleID },
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>One lock wave from an authored spawn table (a patrol radius becomes the patrol pair).</summary>
        private static EncounterLockWave WaveFrom(string rowID, EnemySpawn[] table, EncounterWaveTrigger trigger) {
            var spawns = new EncounterLockSpawn[table.Length];
            for (int index = 0; index < table.Length; index++) {
                EnemySpawn spawn = table[index];
                Vector2? patrolA = null;
                Vector2? patrolB = null;
                if (spawn.PatrolRadius > 0f) {
                    patrolA = spawn.Position - new Vector2(spawn.PatrolRadius, 0);
                    patrolB = spawn.Position + new Vector2(spawn.PatrolRadius, 0);
                }
                spawns[index] = new EncounterLockSpawn(spawn.EnemyID, spawn.Position, patrolA, patrolB);
            }
            return new EncounterLockWave { RowID = rowID, Spawns = spawns, Trigger = trigger };
        }

        private void BuildMidway() {
            float width = ElectricityStartX - MidwayStartX;
            BuildRoomBackground("BG_Midway", MidwayStartX, width, LevelHeight, new Color(0.06f, 0.08f, 0.14f, 0.55f));
            // GR-LV1: the floor stops at the Skyway's west deck; BuildSkyway lays the rest.
            BuildFloor(MidwayStartX, GroundY, SkywayWestDeckEndX - MidwayStartX);
            BuildWall(MidwayStartX, 0, LevelHeight);

            BuildPlatform(500, 1240, 300);
            BuildPlatform(1000, 1100, 260);
            BuildPlatform(1500, 1240, 280);
            // The awning climb.
            BuildOneWayPlatform(1750, 1080, 260);
            BuildOneWayPlatform(2100, 1080, 300);

            BuildCheckpoint(240, GroundY - 50, CheckpointEntry, CheckpointRole.Entry);
            BuildRoomDecoration(MidwayStartX, "chicago_room_midway", new Color(0.6f, 0.82f, 1f));
            // GU-L0: the Midway wave is the Midway lock's (it spawns as the hero
            // engages, where the wave trigger at 760 used to fire).
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

            // GU-L0: the Middle anchor stands in the gap between the Electricity
            // and routing locks (it stood at 6180, inside the routing hall's area).
            BuildCheckpoint(MiddleCheckpointX, GroundY - 50, CheckpointMid, CheckpointRole.Middle);
            BuildRoomDecoration(ElectricityStartX, "chicago_room_electricity", new Color(0.5f, 0.9f, 1f));

            BuildRoomTransitionPair(
                ElectricityStartX, "chicago_room_electricity", "chicago_room_midway",
                new Rect2(ElectricityStartX, 0, width, LevelHeight),
                new Rect2(MidwayStartX, GroundRoomCameraTop, ElectricityStartX - MidwayStartX, 1080));

            // Package 13 W4 (S23): the powerhouse, and Tesla's coat still on the hook.
            BuildAbsenceTrigger(new Vector2(3900, GroundY - 200), new Vector2(60, 600));
            // GU-L0: the Electricity wave is the Electricity lock's (pre-placed).
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

            // The door top sits at y=680, above the coil platforms - but a double
            // jump off the gallery catwalk (top 952) or its Extractor still clears
            // 680, so P5 (2026-10-04) seals everything above the door with a
            // permanent gate column. The routing puzzle opens the door; nothing
            // ever opens the sky over it.
            _courtDoor = BuildDoor("CourtOfHonorDoor", new Vector2(CourtDoorX, GroundY),
                "chicago_door_sealed", new Vector2(CourtDoorWidth, CourtDoorHeight), CourtDoorColor);
            BuildGateColumn(CourtDoorX, CourtDoorWidth, GroundY - CourtDoorHeight, CourtDoorColor);

            BuildRoomTransitionPair(
                RoutingStartX, "chicago_room_routing", "chicago_room_electricity",
                new Rect2(RoutingStartX, GroundRoomCameraTop, width, 1080),
                new Rect2(ElectricityStartX, 0, RoutingStartX - ElectricityStartX, LevelHeight),
                onForwardEntered: _ => {
                    if (_beamPuzzle == null || !_beamPuzzle.IsCompleted) {
                        SetObjective("chicago_objective_reroute");
                    }
                });
            // GU-L0: the routing-hall wave is the routing lock's (pre-placed).
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

            // GU-L0: 8420 -> 8500, out of basic-swing reach from inside the
            // routing hall's lock (its exit barrier and the door are at 8280-8320).
            BuildCheckpoint(PreBossCheckpointX, GroundY - 50, CheckpointPreBoss, CheckpointRole.PreBoss);
            BuildRoomDecoration(BossStartX, "chicago_room_boss", new Color(1f, 0.45f, 0.35f));

            BuildRoomTransition("chicago_room_boss", new Vector2(BossStartX + 80, GroundY - 400),
                new Rect2(BossStartX, GroundRoomCameraTop, width, 1080));

            _inventorEncounter = BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, GroundY - 50),
                "ChronalInventorEncounter", BossArenaBounds);
            if (_inventorEncounter != null) _inventorEncounter.PhaseEntered += OnInventorPhaseEntered;
        }

        // === Package 12 W9 (GAP-07) — the Chronal Inventor's Phase 2 coils ===
        //
        // Design §6: "P2: deploys two siphon coils at the arena corners that shield
        // him until destroyed — the level's mirror-coil routing lesson, weaponized."
        // The phase and the coils' HP are authored on chronal_inventor.tres
        // (ArenaGuardianMinPhase / ArenaGuardianHP); the corners are arena geometry,
        // so they live here. While either coil stands the Inventor refuses all
        // damage and wears the shared armor glow.

        /// <summary>The two arena corners the coils deploy to (floor level, inside the walls).</summary>
        public static readonly Vector2[] InventorCoilPositions = {
            new(BossStartX + 260f, GroundY),
            new(LevelWidth - 180f, GroundY)
        };

        private BossEncounterController _inventorEncounter;
        private readonly List<FTT.Enemies.ArenaGuardian> _inventorCoils = new();

        /// <summary>The boss encounter (test seam).</summary>
        public BossEncounterController InventorEncounter => _inventorEncounter;

        /// <summary>The Phase 2 coils, once deployed (test seam).</summary>
        public IReadOnlyList<FTT.Enemies.ArenaGuardian> InventorCoils => _inventorCoils;

        private void OnInventorPhaseEntered(int phase) {
            FTT.Enemies.BossData data = _inventorEncounter?.Data;
            if (data == null || data.ArenaGuardianMinPhase < 0 || phase != data.ArenaGuardianMinPhase) return;
            // The threshold is crossed inside a hit's physics flush, where new
            // hurtbox areas may not enter the space; hop off it. Direct calls
            // stay synchronous.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) {
                Callable.From(DeployInventorCoils).CallDeferred();
            } else {
                DeployInventorCoils();
            }
        }

        /// <summary>Builds both coils and registers them with every body. Idempotent.</summary>
        public void DeployInventorCoils() {
            if (_inventorCoils.Count > 0 || _inventorEncounter == null) return;
            int hp = Math.Max(1, _inventorEncounter.Data?.ArenaGuardianHP ?? 60);
            for (int index = 0; index < InventorCoilPositions.Length; index++) {
                var coil = new FTT.Enemies.ArenaGuardian {
                    Name = $"InventorSiphonCoil{index}",
                    GuardianID = $"{ChicagoLevelID}.inventor_coil_{index}",
                    MaxHP = hp,
                    Position = InventorCoilPositions[index]
                };
                AddChild(coil);
                _inventorCoils.Add(coil);
                foreach (FTT.Enemies.BossController body in _inventorEncounter.Members) {
                    body?.RegisterGuardian(coil);
                }
            }
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
            // GU-L0: a resume past a lock starts on the objective of the room ahead.
            if (ResumedMidLevel) RefreshApproachObjective();
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

        private void OnBeamPuzzleCompleted(string puzzleID) {
            KeepBeamRoutedThroughRewinds();
            OpenCourtDoor(announce: true);
        }

        /// <summary>
        /// FIX-OBJECTIVE (2026-10-13): the routed beam is one-way, like the door it
        /// frees. From the solve on, the puzzle and its three coils keep their state
        /// through every death rewind (whose snapshot predates the hall: the Middle
        /// anchor stands west of it and the PreBoss one is held until it clears), so
        /// a rewind never re-aims the coils, re-arms crowd tap A or un-solves what
        /// the routing lock has latched. Also runs for a completion the save restores.
        /// </summary>
        private void KeepBeamRoutedThroughRewinds() {
            _beamPuzzle.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            Godot.Collections.Array<Node> parts = _beamPuzzle.GetChildren();
            using var partsLifetime = parts.AsDisposable();
            foreach (Node part in parts) {
                if (part is ConductiveCoil coil) coil.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            }
        }

        private void OpenCourtDoor(bool announce) {
            bool opened = OpenDoor(ref _courtDoor);
            if (!announce || !opened) return;
            // GU-L0: the door is open but the routing hall's barrier holds until
            // its guards are down too; the clear hands the "press on" back.
            if (_routingLock != null && IsInstanceValid(_routingLock) && _routingLock.IsHolding) {
                SetObjective("encounter_lock_objective_clear");
            } else {
                SetObjective("chicago_objective_reach_boss");
            }
        }

        /// <summary>GU-L0: a cleared or reverted lock hands the objective back to the room ahead.</summary>
        protected override void RestoreLevelObjective() => RefreshApproachObjective();

        /// <summary>
        /// The approach objective from live state: the Court once its door and
        /// the routing hall are open, the routing puzzle once the Electricity
        /// Building is behind the hero, else the fairgrounds.
        /// </summary>
        private void RefreshApproachObjective() {
            if (IsBossDefeated) return;
            bool routingHolds = _routingLock != null && IsInstanceValid(_routingLock) && _routingLock.IsHolding;
            if (CourtDoorOpen && !routingHolds) SetObjective("chicago_objective_reach_boss");
            // FIX-OBJECTIVE: while the hall holds, the reroute objective reads the
            // lock (its latch), so it and the barrier always agree.
            else if (CourtDoorOpen) {
                SetObjective(_routingLock.IsRequiredPuzzleMet(BeamPuzzleID)
                    ? "encounter_lock_objective_clear" : "chicago_objective_reroute");
            }
            else if (_electricityLock != null && IsInstanceValid(_electricityLock) && !_electricityLock.IsHolding) {
                SetObjective("chicago_objective_reroute");
            } else {
                SetObjective(InitialObjectiveKey);
            }
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
        //
        // GU-L0 (2026-10-13): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles, never
        // more than the table.

        // Package 12 W2 (GAP-13): the explicit encounter baseline. GU-L0: the
        // encounters are the three locks; every anchor lists every lock before
        // it, and a resume builds those Cleared (Chicago has no non-encounter
        // anchor state, so MarkWavesClearedThrough is not overridden; the
        // PreBoss resume's open door is OnLevelReady's).

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [CheckpointEntry] = Array.Empty<string>(),
                [CheckpointMid] = new[] { LockMidway, LockElectricity },
                [CheckpointPreBoss] = new[] { LockMidway, LockElectricity, LockRoutingHall }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;
    }
}
