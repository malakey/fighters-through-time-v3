using Godot;
using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 9 - Hakata Bay, 1281 (the second Mongol invasion of Japan; S32 replaced
    /// the retired Berlin 1961 level - the scene ID <c>Level_09_Berlin</c>, the
    /// <c>level_09_berlin</c> level ID, the <c>CampaignLevel.Berlin</c> member and
    /// every <c>berlin_*</c> translation key are retained per D6). The campaign's
    /// designated stealth level: samurai night raids on the moored fleet, with the
    /// Severed's storm-anchors drinking the typhoon that is coming. Four rooms across
    /// 10,560 px:
    /// <list type="number">
    /// <item><b>The Stone Seawall</b> (0-2880) - wet timber and sand under the
    ///       seawall; the opening firefight, no lanterns yet.</item>
    /// <item><b>The Moored Fleet - Lantern Watch</b> (2880-6400) - the era beat.
    ///       Three fleet lanterns (<see cref="SearchlightZone"/> in
    ///       <see cref="SearchlightMode.DelayedStrike"/> mode, the Level 4 searchlight
    ///       remixed as the design asks) with deliberately desynced sweeps, three
    ///       shadowed cover pockets in the gaps between their swept footprints, a
    ///       mast climb on drop-through platforms, and the three lantern signal lines
    ///       that darken them.</item>
    /// <item><b>The Storm-Anchor Ship</b> (6400-8640) - past the barred boarding
    ///       gate; the rigging climb, the heaviest wave led by the level's single
    ///       elite (the Thunderbomb Engineer), and the S36 Eraser near-capture.</item>
    /// <item><b>The Flagship Deck</b> (8640-10560) - the Thunderbomb General's
    ///       flagship duel: a flat deck, two gunwale barricades, and two high castle
    ///       decks.</item>
    /// </list>
    ///
    /// <para><b>How the stealth reads, and how it differs from Paris.</b> Level 4 runs
    /// the same component in <see cref="SearchlightMode.UltimateDrain"/>: standing in a
    /// Paris beam is an attrition tax on the Ultimate meter that a player can simply
    /// eat. Hakata's lanterns never touch the meter. Exposure accumulates silently for
    /// <see cref="SearchlightZone.ExposureGraceSeconds"/> (1.5 s, the figure the design
    /// quantifies) and then a thunder-bomb volley lands with knockback, and keeps
    /// landing every 1.5 s while the player stays lit. Crossing is therefore a timing
    /// problem with a real fail state rather than a resource cost, and the answer is
    /// either the sweep window or one of the three un-swept cover pockets.</para>
    ///
    /// <para><b>Agency over patience.</b> Each lantern is fed by a
    /// <see cref="DestructibleBlock"/> signal line placed past it. Cutting a line
    /// permanently darkens its lantern, so the gauntlet gets progressively safer as
    /// the player works forward instead of demanding the same patience three times.
    /// All three lines satisfy the <see cref="PuzzleManager"/> that unbars the
    /// boarding gate into room three. (The node names - <c>FeedRelay*</c>,
    /// <c>Searchlight*</c>, <c>RadarGate</c> - are retained authored identifiers.)</para>
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 12 standards,
    /// 1 elite, 1 boss, 3 extractors. The near-capture Eraser is a presentation
    /// silhouette (<see cref="NearCaptureBeat"/>), never a spawned enemy.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L8; the Level 2
    /// pattern): the seawall (300-2860); the Lantern Watch (3150-6350), which clears
    /// only once all three signal lines are cut <i>and</i> its watch is down (the
    /// lanterns stay live through the fight) and whose exit fills the boarding gate
    /// under its column; and the storm-anchor ship (6620-8560), engaged only after
    /// the near-capture, whose exit barrier closes the gap under the flagship's
    /// partial wall. The walkway and gantry archers moved down onto the deck, the
    /// Middle anchor moved out of the Lantern Watch to just past the boarding gate
    /// (6570), and the PreBoss anchor and the Font moved out of the ship's lock into
    /// the flagship's antechamber.</para>
    ///
    /// <para><b>The Beach Run</b> (gameplay review 2026-11-03, rulings 7a-7c+; GR-LV3): the
    /// seawall lock was carved to 300-2000, and the freed span and the old gap are a traversal
    /// stretch (2020-3130) over a non-lethal trench. Its optional route, the Mast Line, holds
    /// the ruins Extractor, and the secret moved to a perch over the first cover pocket. The
    /// seawall takes the swarm row. The Lantern Watch, its stealth and the near-capture are
    /// untouched: nothing east of the stretch moved.</para>
    ///
    /// <para><b>The General's lanterns</b> (ruling 14): at his Phase 2 three fleet lanterns
    /// light over the flagship, sweeping the deck in the delayed-strike mode the Lantern
    /// Watch teaches.</para>
    /// </summary>
    public partial class Level09Controller : StoryLevelControllerBase {

        // === Layout ===

        public const float LevelWidth = 10560f;
        public const float LevelHeight = 1400f;
        public const float GroundY = 1200f;

        /// <summary>
        /// Ground rooms confine to a bottom-anchored 1080-tall window. The level is
        /// 1400 px tall so the guard towers have real headroom; only the two rooms
        /// that actually contain a climb use the full height.
        /// </summary>
        public const float GroundRoomCameraTop = LevelHeight - 1080f;

        /// <summary>The two castle decks in room 4 (centre Y; 420 px wide, the default slab thickness).</summary>
        public const float CastleDeckY = 800f;
        public const float WestCastleDeckX = 9100f;
        public const float EastCastleDeckX = 10100f;
        public const float CastleDeckWidth = 420f;

        public const float Room1StartX = 0f;
        public const float Room2StartX = 2880f;
        public const float Room3StartX = 6400f;
        public const float Room4StartX = 8640f;

        private const float RadarGateX = 6360f;
        private const float RadarGateWidth = 48f;
        private const float RadarGateHeight = 900f;
        private static readonly Color RadarGateColor = new(0.30f, 0.33f, 0.38f);

        /// <summary>
        /// P1 (2026-10-04): the flagship deck. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 9600 - 800); east edge is the stern
        /// wall's inner face.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, LevelWidth - 20f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 9600f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 800 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 800f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

        /// <summary>Depth of the authored searchlight cone, from SearchlightZoneTemplate.tscn.</summary>
        public const float SearchlightConeDepth = 520f;

        /// <summary>Half-width of the authored searchlight cone at full depth.</summary>
        public const float SearchlightConeHalfWidth = 120f;

        private const string FeedPuzzleID = "level_09.cut_feeds";
        private const string BossResourcePath = "res://resources/Bosses/iron_chancellor.tres";

        // === Authored encounter table (asserted by Level09ContentTests) ===

        /// <summary>The Hakata era standard, the Lantern-Eye Archer: a long-telegraph, long-range spotter.</summary>
        public const string SentryEnemyID = "infrared_border_sentry";

        /// <summary>The Severed shock-trooper standard mixed through every era roster.</summary>
        public const string CultistEnemyID = "chrono_slasher";

        /// <summary>The Hakata elite, the Thunderbomb Engineer. Exactly one, per the locked economy row.</summary>
        public const string EliteEnemyID = "neural_mech_walker";

        /// <summary>
        /// V7.1 design law, first actually placed in V7.6 (Package 11 A7a): the
        /// Chrono-Warden is "salted through Levels 7-15 alongside the
        /// Tech-Enforcer" and had never been spawned in any level (recon G8). The
        /// Lantern Watch is the right room for it - a Dilation Field dropped on the
        /// lantern lane turns a timing puzzle into a real problem. No Eraser enemy
        /// is spawned here (the S36 near-capture Eraser is a presentation silhouette):
        /// the two may not share a level before Act III.
        /// </summary>
        public const string WardenEnemyID = "chrono_warden";

        /// <summary>
        /// Authored Normal-difficulty spawn table. Each wave is one encounter lock's
        /// wave (GU-L8): wave 1 the seawall's, wave 2 the Lantern Watch's, wave 3 the
        /// storm-anchor ship's (<see cref="BuildEncounterLocks"/>), and each is exactly
        /// its manifest row. <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>
        /// takes a prefix of each wave, so the hardest concurrency is wave 3's six
        /// bodies - inside level_09_pool_config's standard warm 14 / elite warm 4 caps.
        /// <para>GU-L8 (2026-10-12) moves, all onto the route a slowest hero fights
        /// on: the Lantern Watch trooper 4100 -> 4420 (it overlapped the west relay's
        /// cover slab, 3960-4280, and was pushed up beside the relay); the gangway
        /// archer (5900, 460) on the watchtower walkway, 688 px up a climb, down to the
        /// deck at the tower's foot (5650); and the rigging archer (7350, 570) on the
        /// mast gantry, whose patrol ran off the gantry's east end, down to the deck
        /// under it.</para>
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - the stone seawall. An archer on the rubble, Severed troopers on the sand.
            // GR-LV3: the second trooper 2450 -> 1640, inside the carved lock.
            (CultistEnemyID, 1, new Vector2(1000f, 1150f)),
            (SentryEnemyID, 1, new Vector2(1700f, 990f)),
            (CultistEnemyID, 1, new Vector2(1640f, 1150f)),
            // Wave 2 - the Lantern Watch. Archers hold the lanterns; one guards the tower's foot.
            (SentryEnemyID, 2, new Vector2(3400f, 1150f)),
            (CultistEnemyID, 2, new Vector2(4420f, 1150f)),
            (SentryEnemyID, 2, new Vector2(5000f, 1150f)),
            (SentryEnemyID, 2, new Vector2(5650f, 1150f)),
            (WardenEnemyID, 2, new Vector2(4600f, 1150f)),
            // Wave 3 - the storm-anchor ship. The Thunderbomb Engineer anchors the deck.
            (SentryEnemyID, 3, new Vector2(6800f, 1150f)),
            (CultistEnemyID, 3, new Vector2(7100f, 1150f)),
            (SentryEnemyID, 3, new Vector2(7350f, 1150f)),
            (EliteEnemyID, 3, new Vector2(7600f, 1150f)),
            (CultistEnemyID, 3, new Vector2(7900f, 1150f)),
            (SentryEnemyID, 3, new Vector2(8200f, 1150f))
        };

        /// <summary>The patrol leg every Hakata spawn gets either side of its post.</summary>
        public const float PatrolHalfSpanPixels = 220f;

        /// <summary>
        /// The three authored cover slabs in the Lantern Watch, as (centre X, width).
        /// Each one is deliberately authored in a gap between two searchlight
        /// footprints, so it is genuinely safe ground; the content test proves it
        /// against <see cref="SweptFootprint"/> rather than trusting these numbers.
        /// </summary>
        public static readonly (float CenterX, float Width)[] CoverPockets = {
            (3010f, 240f),   // west of the first beam
            (4120f, 320f),   // the pocket between the two ground beams
            (5200f, 260f)    // east of the second beam (checkpoint 1 stood under it until GU-L8)
        };

        /// <summary>
        /// Three extractors, each guarding a climb the critical path can skip.
        /// <para>G6 (2026-10-04): the watchtower machine used to stand at
        /// (5620, 480), sunk through the upper walkway's west end (deck 5550..6250,
        /// top 512) and leaving a 14 px strip where the tower climb arrives, with
        /// the machine walling off the walk to the tower signal line. It now rests on
        /// the deck (bottom = deck top) flush with the walkway's east end, past the
        /// tower line, so the climb lands on open deck and the line is reached first
        /// (the tower line's node, <c>FeedRelayTower</c> in the scene, moved west from
        /// x 6150 to 6020 to make room; its ID and condition are unchanged).
        /// The radar-mast machine had the same fault on its gantry (7040..7460, top
        /// 612): sunk through the west end where the rigging climb arrives, leaving
        /// 34 px. It rests flush with the gantry's east end instead.</para>
        /// </summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            // GR-LV3: the end of the Mast Line, over the Beach Run (it stood at 2300, 940).
            ("level_09.extractor_ruins", new Vector2(MastLineEndX, 538f)),
            ("level_09.extractor_watchtower", new Vector2(6194f, 440f)),
            ("level_09.extractor_radar_mast", new Vector2(7404f, 540f))
        };

        public static int StandardEnemyCount => CountOf(SentryEnemyID) + CountOf(CultistEnemyID);
        public static int EliteEnemyCount => CountOf(EliteEnemyID) + CountOf(WardenEnemyID);

        /// <summary>How many Chrono-Wardens this level authors. Test seam for the salt rules.</summary>
        public static int WardenCount => CountOf(WardenEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) {
                if (id == enemyID) total++;
            }
            return total;
        }

        // === GU-L8 (2026-10-12): the three encounter locks ====================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockSeawall = "level_09_berlin_lock_seawall";
        public const string LockLanternWatch = "level_09_berlin_lock_lantern_watch";
        public const string LockStormAnchor = "level_09_berlin_lock_storm_anchor";

        /// <summary>The manifest rows the three lock waves are (level_09_rewards.tres), by spawn-table wave.</summary>
        public static string WaveRowID(int wave) => $"level_09.wave{wave}";

        private const float BarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>The seawall: room 1 past the entry anchor (240), to 2000. GR-LV3 carved it from 2860; the Beach Run takes the rest.</summary>
        public const float SeawallLockMinX = 300f;
        public const float SeawallLockMaxX = 2000f;

        /// <summary>
        /// The Lantern Watch, one authored room wide (3200) and ending at the boarding
        /// gate, so it starts east of the first cover pocket (2890-3130), which stays a
        /// breather outside every lock. It clears on "every signal line cut AND every
        /// watchman down": the feed puzzle is a condition. The lanterns stay live.
        /// </summary>
        public const float LanternWatchLockMinX = 3150f;
        public const float LanternWatchLockMaxX = RadarGateX - BarrierWidth * 0.5f;

        /// <summary>
        /// The Middle anchor, just past the boarding gate and east of the near-capture
        /// trigger: its strike surface (6540-6600) is beyond the longest basic swing
        /// from inside the Lantern Watch, and its respawn point is clear of the snare.
        /// It stood at 5200, in the middle of the lantern fight.
        /// </summary>
        public const float MiddleCheckpointX = 6570f;

        /// <summary>
        /// The storm-anchor ship, engaged only after the near-capture: its west edge
        /// lies past the snare's full-height trigger, so the beat always fires first.
        /// Its exit barrier closes the gap under the flagship's partial west wall.
        /// </summary>
        public const float StormAnchorLockMinX = MiddleCheckpointX + 50f;
        public const float StormAnchorLockMaxX = FlagshipWestWallX;

        /// <summary>
        /// The left face of the flagship's partial west wall (y 300-800). GU-L8: moved
        /// 8620 -> 8560 so the storm-anchor lock's exit barrier, which fills the gap
        /// under it, leaves room for the PreBoss anchor and the Font outside every lock
        /// and the PreBoss strike surface beyond the longest swing from inside the ship.
        /// </summary>
        public const float FlagshipWestWallX = 8560f;
        private const float FlagshipWestWallBottomY = 800f;

        /// <summary>The PreBoss anchor, in the flagship's antechamber (8580-8800); it stood at 8480, inside the ship's fight.</summary>
        public const float PreBossCheckpointX = 8740f;

        private EncounterLock _seawallLock;
        private EncounterLock _lanternWatchLock;
        private EncounterLock _stormAnchorLock;

        public EncounterLock SeawallLock => _seawallLock;
        public EncounterLock LanternWatchLock => _lanternWatchLock;
        public EncounterLock StormAnchorLock => _stormAnchorLock;

        // === GR-LV3: the Beach Run, the Mast Line and the swarm row ===============

        public const string BeachRunStretchID = "level_09.beach_run";
        public const string MastRouteID = "level_09.mast_line";
        public const string BeachRunRoomKey = "berlin_room_beach_run";

        /// <summary>From the seawall's exit barrier to the Lantern Watch's entry barrier.</summary>
        public const float BeachRunMinX = SeawallLockMaxX + BarrierWidth;
        public const float BeachRunMaxX = LanternWatchLockMinX - BarrierWidth * 0.5f;
        public const float BeachRunWestDeckEndX = 2060f;
        public const float BeachRunEastDeckStartX = 2880f;

        /// <summary>The Mast Line's last platform; the Extractor stands 72 px over its top.</summary>
        public const float MastLineEndX = 2865f;

        /// <summary>The perch the secret stands on, off the line and over the first cover pocket.</summary>
        public const float SecretPerchX = 3040f;

        /// <summary>The seawall's swarm row (level_09_rewards.tres).</summary>
        public const string SwarmRowID = "level_09.swarm1";

        /// <summary>The row, Hard's truth (a 1700 px lock takes 8), along the sand, clear of both caltrop strips.</summary>
        public static readonly float[] SwarmSpawnXs = { 520f, 680f, 860f, 1040f, 1240f, 1600f, 1720f, 1960f };

        /// <summary>
        /// Base-kit pieces only. The first cover pocket (2890-3130) stays where it is, so this
        /// stretch ends on the line's last step and the deck under the pocket, with no wall.
        /// The camera keeps to room 1's bottom-anchored band.
        /// </summary>
        private void BuildBeachRun() {
            TraversalStretch beach = BeginStretch(BeachRunStretchID, BeachRunRoomKey,
                BeachRunMinX, BeachRunMaxX, GroundY, BeachRunWestDeckEndX, BeachRunEastDeckStartX, Room1StartX);
            beach.Room.CameraBounds = new Rect2(Room1StartX, GroundRoomCameraTop, BeachRunMaxX - Room1StartX, 1080f);
            BuildRoomDecoration(BeachRunMinX, BeachRunRoomKey, new Color(0.75f, 0.82f, 0.9f));
            BuildStretchTrench(beach);
            // The deck resumes short of the seam, for the pocket and the Lantern Watch's barrier.
            BuildFloor(BeachRunEastDeckStartX, GroundY, Room2StartX - BeachRunEastDeckStartX);

            BuildStretchStep(beach, 2170f, 1090f, 200f);
            BuildStretchStep(beach, 2430f, 1020f, 180f, StretchStepKind.Crumbling, "level_09.crumbling_jetty");
            TraversalSurface middleStep = BuildStretchStep(beach, 2680f, 1022f, 180f, StretchStepKind.OneWay);
            TraversalSurface eastStep = BuildStretchStep(beach, 2870f, 1020f, 160f);

            // Off the line: a way back up out of the trench, a thunder-bomb lane to cross down
            // there, and the secret's perch, one short hop from the last step.
            BuildOneWayPlatform(2640f, 1190f, 200f);
            BuildCyclicHazardLane("level_09.thunderbomb_beach", 2450f, beach.TrenchFloorY, 1f, 0.6f, 2.6f, 18, withCover: true);
            BuildPlatform(SecretPerchX, 960f, 160f, WetTimberColor);

            OptionalRoute mast = BeginOptionalRoute(MastRouteID, ExtractorPlacements[0].ID,
                ExtractorPlacements[0].Position, middleStep);
            BuildRouteStep(mast, 2450f, 830f, 120f);
            TraversalSurface burstFrom = BuildRouteStep(mast, 2170f, 720f, 160f);
            BuildRouteStep(mast, 2420f, 620f, 180f);
            TraversalSurface end = BuildRouteStep(mast, MastLineEndX, 620f, 220f);
            mast.MarkBurstShortcut(burstFrom, end);
            mast.MarkRiseShortcut(eastStep, end);
        }

        /// <summary>One spawn-table wave as a lock wave: its manifest row, its posts, their patrol legs.</summary>
        public static EncounterLockWave LockWaveFor(int wave, EncounterWaveTrigger trigger) {
            var spawns = new List<EncounterLockSpawn>();
            foreach ((string enemyID, int waveIndex, Vector2 position) in SpawnTable) {
                if (waveIndex != wave) continue;
                spawns.Add(new EncounterLockSpawn(enemyID, position,
                    position + new Vector2(-PatrolHalfSpanPixels, 0f), position + new Vector2(PatrolHalfSpanPixels, 0f)));
            }
            return new EncounterLockWave { RowID = WaveRowID(wave), Spawns = spawns.ToArray(), Trigger = trigger };
        }

        // === Base contract ===

        public override string LevelID => "level_09_berlin";
        public override CampaignLevel Level => CampaignLevel.Berlin;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass. GR-LV3 adds the Beach Run's +30.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);
        public override string LevelTitleKey => "berlin_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_09_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240f, 1150f);

        // Package 12 W8 (GAP-03), moved by GR-LV3: the secret cache sits on a dead-end perch
        // over the first cover pocket (top 952), off the floor route. It stood on a rubble
        // crest at 2650/860, whose underside the Beach Run's line would have bonked.
        protected override Vector2? SecretCachePosition => new(SecretPerchX, 857f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, just before the
        // PreBoss anchor. GU-L8 (2026-10-12): moved 8150 -> 8610, out of the storm-anchor
        // ship's lock into the flagship's antechamber, between the lock's exit barrier
        // (8560-8580) and the PreBoss anchor (8740), its channel circle short of the arena.
        // FIX-OBJECTIVE (2026-10-13): 8610 -> 8640. At 8610 a hero pressed against the
        // closed exit barrier from inside the ship (pivot 8540) stood 70 px from the
        // font, inside its 90 px channel circle, and could heal mid-fight through the
        // barrier. At 8640 that pivot is 100 px away (the channel circle never straddles
        // a lock barrier; EncounterLockContentTests), the font is 112 px from the PreBoss
        // anchor, and its channel circle (to 8730) still stops 70 px short of the arena (8800).
        protected override Vector2? RestorationFontPosition => new(8640f, GroundY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "berlin_objective_infiltrate";
        protected override string BossObjectiveKey => "berlin_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "berlin_objective_complete";

        // === Night-sea graybox palette (S32: wet deck and sand, lantern-lit) ===

        protected override Color FloorColor => new(0.28f, 0.22f, 0.17f);
        protected override Color FloorEdgeColor => new(0.62f, 0.52f, 0.36f);
        protected override Color PlatformColor => new(0.36f, 0.27f, 0.19f);
        protected override Color WallColor => new(0.10f, 0.12f, 0.18f);
        protected override Color HazardColor => new(0.92f, 0.52f, 0.20f);

        private static readonly Color WetTimberColor = new(0.42f, 0.33f, 0.24f);
        private static readonly Color CoverPocketColor = new(0.16f, 0.18f, 0.24f);

        /// <summary>The lit fleet-lantern tint over the retained searchlight cone.</summary>
        public static readonly Color LanternBeamModulate = new(1.0f, 0.74f, 0.38f, 1f);
        private static readonly Color DarkenedBeamModulate = new(0.25f, 0.27f, 0.32f, 0.35f);

        // === Scene-authored toolkit instances ===

        /// <summary>One lantern signal line (a retained "feed relay" node) and the lantern it lights.</summary>
        public readonly record struct FeedLink(string FeedNodeName, string LightNodeName, string ConditionID);

        /// <summary>
        /// Relay-to-beam wiring. Each relay is authored past the beam it cuts, so the
        /// player crosses a beam once and can then delete it for good.
        /// </summary>
        public static readonly FeedLink[] FeedLinks = {
            new("FeedRelayWest", "SearchlightStripWest", "feed_west_cut"),
            new("FeedRelayEast", "SearchlightStripEast", "feed_east_cut"),
            new("FeedRelayTower", "SearchlightWatchtower", "feed_tower_cut")
        };

        private readonly List<SearchlightZone> _searchlights = new();
        private readonly List<DestructibleBlock> _feedRelays = new();
        private readonly Dictionary<DestructibleBlock, FeedLink> _feedByRelay = new();
        private readonly Dictionary<string, SearchlightZone> _lightsByName = new(StringComparer.Ordinal);
        private PuzzleManager _feedPuzzle;
        private StaticBody2D _radarGate;

        public IReadOnlyList<SearchlightZone> Searchlights => _searchlights;
        public IReadOnlyList<DestructibleBlock> FeedRelays => _feedRelays;
        public PuzzleManager FeedPuzzle => _feedPuzzle;

        /// <summary>False until every lantern signal line is cut and the boarding gate (RadarGate) lifts.</summary>
        public bool RadarGateOpen { get; private set; }

        /// <summary>How many relays have been cut (0-3). Drives the HUD counter.</summary>
        public int FeedsCut { get; private set; }

        /// <summary>Times a beam has caught the player. Drives the exposure warning objective.</summary>
        public int ExposureAlarmCount { get; private set; }

        private bool _resumedPastTheGate;

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildCheckpointCharlie();
            BuildBeachRun();
            BuildDeathStrip();
            BuildRadarYard();
            BuildBunkerStreet();
            BuildExtractors(ExtractorPlacements);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L8: the three fights as encounter locks, one wave per manifest row.
        /// Built in <see cref="BuildLevel"/> so a resume past a lock builds it Cleared
        /// with no member ever spawned.
        /// </summary>
        private void BuildEncounterLocks() {
            _seawallLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockSeawall,
                Area = EncounterLockSpec.Span(SeawallLockMinX, SeawallLockMaxX, LevelHeight),
                EntryBarrierX = SeawallLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = SeawallLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = GroundY,
                Waves = new[] {
                    LockWaveFor(1, EncounterWaveTrigger.OnBuild),
                    SwarmWave(SwarmRowID, GroundY - 50f, SwarmSpawnXs)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The watch holds the lanterns from the start; the lock needs every signal
            // line cut as well as its watchmen down, and its exit fills the boarding
            // gate under the gate column. Its objective is the level's own (the cut
            // count); the HUD counts the watch.
            _lanternWatchLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockLanternWatch,
                Area = EncounterLockSpec.Span(LanternWatchLockMinX, LanternWatchLockMaxX, LevelHeight),
                EntryBarrierX = LanternWatchLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = RadarGateX,
                ExitBarrierYRange = new Vector2(GroundY - RadarGateHeight, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] { LockWaveFor(2, EncounterWaveTrigger.OnBuild) },
                RequiredPuzzleIDs = new[] { FeedPuzzleID },
                GoDirection = EncounterGoDirection.Right
            });
            // The ship's crew comes for the hero the moment it is in, after the snare
            // lets go; the exit barrier closes the gap under the flagship's west wall.
            _stormAnchorLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockStormAnchor,
                Area = EncounterLockSpec.Span(StormAnchorLockMinX, StormAnchorLockMaxX, LevelHeight),
                EntryBarrierX = StormAnchorLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = StormAnchorLockMaxX + BarrierWidth * 0.5f,
                ExitBarrierYRange = new Vector2(FlagshipWestWallBottomY, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] { LockWaveFor(3, EncounterWaveTrigger.OnEngage) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>
        /// Godot readies a packed scene's children before its root, so every authored
        /// template instance in Level_09_Berlin.tscn is already live here.
        /// </summary>
        private void CollectAuthoredNodes() {
            _feedPuzzle = GetNodeOrNull<PuzzleManager>("FeedPuzzle");
            // PuzzleManager re-emits a save-restored completion one deferred frame
            // after _Ready (Package 5 Wave A integration), so this handler is
            // deliberately idempotent.
            if (_feedPuzzle != null) _feedPuzzle.PuzzleCompleted += _ => OnAllFeedsCut();
            // FIX-OBJECTIVE (2026-10-13): a cut line is one-way — its lantern never
            // relights — so the feed puzzle keeps its conditions through every death
            // rewind (whose snapshot predates the cuts: every relay is inside the
            // Lantern Watch, and the Middle anchor is past its gate), and a relay
            // stays cut once cut (OnFeedRelayCut). Without it a rewind re-built the
            // relays and reset the puzzle under dark lanterns, and the Lantern
            // Watch, which needs the puzzle, could never clear.
            if (_feedPuzzle != null) _feedPuzzle.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;

            foreach (FeedLink link in FeedLinks) {
                if (GetNodeOrNull<SearchlightZone>(link.LightNodeName) is SearchlightZone light) {
                    _searchlights.Add(light);
                    _lightsByName[link.LightNodeName] = light;
                    light.PlayerDetected += _ => OnBeamExposure();
                    // S32 reskin: the searchlight cone reads as a warm fleet lantern.
                    if (light.GetNodeOrNull<CanvasItem>("Visual") is CanvasItem cone) cone.Modulate = LanternBeamModulate;
                }
                if (GetNodeOrNull<DestructibleBlock>($"FeedPuzzle/{link.FeedNodeName}") is DestructibleBlock relay) {
                    _feedRelays.Add(relay);
                    _feedByRelay[relay] = link;
                    relay.Destroyed += () => OnFeedRelayCut(relay);
                }
            }
        }

        /// <summary>Room 1: the stone seawall. Wet sand, timber and stone climbs, caltrops.</summary>
        private void BuildCheckpointCharlie() {
            BuildRoomBackground("BG_Room1_CheckpointCharlie", Room1StartX, 2880f, LevelHeight,
                new Color(0.14f, 0.16f, 0.20f, 0.45f));

            // GR-LV3: the floor stops at the Beach Run's west deck; BuildBeachRun lays the rest.
            BuildFloor(Room1StartX, GroundY, BeachRunWestDeckEndX - Room1StartX);
            BuildPlatform(600f, 1060f, 300f, WetTimberColor);
            BuildPlatform(1150f, 940f, 260f, WetTimberColor);
            BuildPlatform(1700f, 1050f, 280f, WetTimberColor);

            BuildHazardSpikes(1450f, 1190f, 140f);
            // GR-LV3: 2050 -> 1850, inside the carved lock.
            BuildHazardSpikes(1850f, 1190f, 120f);

            BuildCheckpoint(240f, GroundY - 50f, CheckpointID(0), CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "berlin_room_checkpoint_charlie", new Color(0.80f, 0.86f, 0.95f));

            BuildRoomTransition("berlin_room_checkpoint_charlie", new Vector2(560f, 900f),
                new Rect2(Room1StartX, GroundRoomCameraTop, 2880f, 1080f),
                triggerSize: new Vector2(80f, LevelHeight));
        }

        /// <summary>
        /// Room 2: the Lantern Watch stealth gauntlet. The lanterns themselves are authored in the
        /// scene; this builds the geometry that makes timing them matter - three
        /// cover pockets sitting in the gaps between the swept cone footprints, and
        /// the mast climb that ends on the lantern-swept upper gangway.
        /// </summary>
        private void BuildDeathStrip() {
            BuildRoomBackground("BG_Room2_DeathStrip", Room2StartX, 3520f, LevelHeight,
                new Color(0.10f, 0.12f, 0.17f, 0.5f));

            BuildFloor(Room2StartX, GroundY, 3520f);

            // Cover pockets. Every slab's span is authored OUTSIDE every beam's swept
            // footprint (Level09ContentTests pins that), so these are genuine safe
            // ground rather than decoration - the Area2D cone has no occlusion, so
            // cover here is positional, and the pockets are where the cones never go.
            foreach ((float x, float width) in CoverPockets) {
                BuildPlatform(x, 1120f, width, CoverPocketColor);
            }

            // Guard tower: the level's first vertical climb, drop-through on the way down.
            BuildOneWayPlatform(5450f, 1040f, 220f);
            BuildOneWayPlatform(5700f, 900f, 220f);
            BuildOneWayPlatform(5480f, 760f, 220f);
            BuildOneWayPlatform(5760f, 640f, 200f);

            // Upper walkway, swept end to end by the watchtower beam.
            BuildPlatform(5900f, 520f, 700f);

            BuildRoomDecoration(Room2StartX, "berlin_room_death_strip", new Color(1.0f, 0.92f, 0.55f));

            // GR-LV3: just inside the Lantern Watch's area (it stood at 2920), so the Beach
            // Run keeps its own camera up to the entry barrier.
            BuildRoomTransition("berlin_room_death_strip", new Vector2(LanternWatchLockMinX + 40f, 700f),
                new Rect2(Room2StartX, 0f, 3520f, LevelHeight),
                triggerSize: new Vector2(80f, LevelHeight));

            _radarGate = BuildDoor("RadarGate", new Vector2(RadarGateX, GroundY),
                "berlin_gate_locked", new Vector2(RadarGateWidth, RadarGateHeight), RadarGateColor);
            // P5 (2026-10-04): the gate top is y 300 and the swept upper gangway
            // (top 512) ends 86 px short of it, so one double jump carried the hero
            // over the boarding gate with every signal line still live. The column
            // seals the sky above it permanently (centred on the gate and wider than
            // it, so the gate's top is never a ledge); cutting the lines opens the gate.
            BuildGateColumn(RadarGateX, RadarGateWidth, GroundY - RadarGateHeight, RadarGateColor);
            // GU-L8: the Lantern Watch's wave is its lock's; the proximity trigger is gone.
        }

        /// <summary>Room 3: the storm-anchor ship, past the boarding gate the signal lines unbar.</summary>
        private void BuildRadarYard() {
            BuildRoomBackground("BG_Room3_RadarYard", Room3StartX, 2240f, LevelHeight,
                new Color(0.12f, 0.14f, 0.18f, 0.5f));

            BuildFloor(Room3StartX, GroundY, 2240f);

            // Rigging: the second mast climb.
            BuildOneWayPlatform(6900f, 1040f, 220f);
            BuildOneWayPlatform(7150f, 900f, 220f);
            BuildOneWayPlatform(6950f, 760f, 200f);
            BuildPlatform(7250f, 620f, 420f);   // mast gantry, holds extractor 3

            BuildPlatform(7900f, 1040f, 260f, WetTimberColor);
            BuildPlatform(8300f, 920f, 240f, WetTimberColor);

            // GU-L8: the Middle anchor, just past the boarding gate and the snare,
            // outside every lock (it stood at 5200, inside the Lantern Watch).
            BuildCheckpoint(MiddleCheckpointX, GroundY - 50f, CheckpointID(1), CheckpointRole.Middle);
            BuildRoomDecoration(Room3StartX, "berlin_room_radar_yard", new Color(0.60f, 0.85f, 0.70f));

            BuildRoomTransition("berlin_room_radar_yard", new Vector2(Room3StartX + 80f, 700f),
                new Rect2(Room3StartX, 0f, 2240f, LevelHeight),
                triggerSize: new Vector2(80f, LevelHeight));

            BuildNearCapture();
            // GU-L8: the ship's wave is its lock's (OnEngage); the proximity trigger is gone.
        }

        // === Package 13 W4 (S36): the Eraser near-capture ===

        /// <summary>
        /// Where the snare catches the hero: just past the boarding gate, before the
        /// storm-anchor ship's lock can engage, in the dark after the lanterns. No
        /// Chrono-Warden shares this room (the Act I/II salt rule), and the Eraser is a
        /// silhouette.
        /// </summary>
        public const float NearCaptureTriggerX = Room3StartX + 100f;

        /// <summary>
        /// GU-L8 (2026-10-12): the snare's trigger runs the level's full height (it
        /// was 400 px tall, centred on the deck), so no jump carries the hero over it
        /// into the storm-anchor lock: the beat always fires before that lock engages.
        /// </summary>
        public static readonly Vector2 NearCaptureTriggerSize = new(60f, LevelHeight);

        /// <summary>The scripted beat. Built with the room.</summary>
        public NearCaptureBeat NearCapture { get; private set; }

        private void BuildNearCapture() {
            NearCapture = new NearCaptureBeat {
                Name = "NearCaptureBeat",
                StartDialogue = id => StartDialogue(id)
            };
            AddChild(NearCapture);
            BuildWaveTrigger("NearCaptureTrigger", new Vector2(NearCaptureTriggerX, LevelHeight * 0.5f),
                () => BeginNearCapture(), NearCaptureTriggerSize);
        }

        /// <summary>Snares the hero once. Public so tests can drive it.</summary>
        public bool BeginNearCapture() =>
            NearCapture != null && IsInstanceValid(NearCapture) && NearCapture.Begin(Player);

        protected override void OnDialogueSequenceComplete(string dialogueID) =>
            NearCapture?.OnDialogueComplete(dialogueID);

        /// <summary>
        /// Room 4: the flagship duel. A flat deck with two waist-high gunwale
        /// barricades for the General's shockwaves to break line on, and the two high
        /// castle decks. The west wall stops above the
        /// entry so the player is not sealed out of the storm-anchor ship.
        /// </summary>
        private void BuildBunkerStreet() {
            BuildRoomBackground("BG_Room4_BunkerStreet", Room4StartX, 1920f, LevelHeight,
                new Color(0.18f, 0.14f, 0.16f, 0.5f));

            BuildFloor(Room4StartX, GroundY, 1920f);

            BuildPlatform(WestCastleDeckX, CastleDeckY, CastleDeckWidth);
            BuildPlatform(EastCastleDeckX, CastleDeckY, CastleDeckWidth);

            BuildPlatform(9280f, 1090f, 200f, CoverPocketColor);   // fortification
            BuildPlatform(9960f, 1090f, 200f, CoverPocketColor);   // fortification

            // Partial: the entry stays open below, and (GU-L8) the storm-anchor lock's
            // exit barrier fills that opening until the ship is cleared. Moved 8620 ->
            // 8560 to make the antechamber for the PreBoss anchor and the Font.
            BuildWall(FlagshipWestWallX, 300f, FlagshipWestWallBottomY - 300f);

            // GU-L8: the PreBoss anchor, past the ship's lock (it stood at 8480, inside it).
            BuildCheckpoint(PreBossCheckpointX, GroundY - 50f, CheckpointID(2), CheckpointRole.PreBoss);
            // The east boundary runs to the top of the level (2026-10-08), as Levels
            // 2 and 6 do: it used to stop at y 440, 352 px over the east castle
            // deck. Five heroes' double jumps already cleared that, and the apex
            // hang added Tesla and Leonardo, so a hero could leave the level.
            // Level09ContentTests pins the full height.
            BuildWall(LevelWidth - 20f, 0f, GroundY);

            BuildRoomDecoration(Room4StartX, "berlin_room_bunker_street", new Color(0.95f, 0.35f, 0.30f));

            BuildRoomTransition("berlin_room_bunker_street", new Vector2(Room4StartX + 60f, 900f),
                new Rect2(Room4StartX, GroundRoomCameraTop, 1920f, 1080f),
                triggerSize: new Vector2(80f, LevelHeight));

            BossEncounterController general = BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, 1150f),
                "IronChancellorEncounter", BossArenaBounds);
            BuildGeneralLanterns();
            if (general != null) {
                general.PhaseEntered += OnGeneralPhaseEntered;
                general.BossDefeated += _ => DouseGeneralLanterns();
            }
        }

        // === GR-LV3 (ruling 14): the Thunderbomb General's fleet lanterns ======
        //
        // Design §6: "fleet lanterns sweep the deck; a caught player takes a volley". The Lantern
        // Watch's beam in its delayed-strike mode, built dark so no body wakes mid-fight, its
        // sweep footprints inside the arena. They are story_hazard members, so the Post-Landing
        // Hold and the rewind freeze hold them. His artillery_barrage stays (ruling 4a); no
        // signal line feeds these, so a catch posts no objective.

        private const string SearchlightTemplatePath = "res://scenes/templates/SearchlightZoneTemplate.tscn";

        /// <summary>The three beams: x, sweep period and arc. Deliberately desynced, as the Lantern Watch's are.</summary>
        public static readonly (string ID, float X, float PeriodSeconds, float ArcDegrees)[] GeneralLanternPlan = {
            ("level_09.general_lantern_a", 9180f, 4.7f, 36f),
            ("level_09.general_lantern_b", 9720f, 5.9f, 30f),
            ("level_09.general_lantern_c", 10230f, 3.8f, 36f)
        };

        /// <summary>The lanterns' y: the cone's 520 px depth ends on the deck.</summary>
        public const float GeneralLanternY = GroundY - SearchlightConeDepth;

        /// <summary>What a catch costs: the General's own artillery_barrage figure.</summary>
        public const int GeneralLanternStrikeDamage = 20;

        private readonly List<SearchlightZone> _generalLanterns = new();

        /// <summary>True once Phase 2 has lit the lanterns and until the General falls.</summary>
        public bool GeneralLanternsLit { get; private set; }
        public IReadOnlyList<SearchlightZone> GeneralLanterns => _generalLanterns;

        private void BuildGeneralLanterns() {
            foreach ((string id, float x, float period, float arc) in GeneralLanternPlan) {
                var lantern = ResourceLoader.Load<PackedScene>(SearchlightTemplatePath).Instantiate<SearchlightZone>();
                lantern.Name = $"GeneralLantern_{id[^1]}";
                lantern.SearchlightID = id;
                lantern.Position = new Vector2(x, GeneralLanternY);
                lantern.Mode = SearchlightMode.DelayedStrike;
                lantern.SweepPeriodSeconds = period;
                lantern.SweepArcDegrees = arc;
                lantern.StrikeDamage = GeneralLanternStrikeDamage;
                lantern.LabelKey = "";
                lantern.Enabled = false;
                lantern.Monitoring = false;
                lantern.Visible = false;
                lantern.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
                AddChild(lantern);
                if (lantern.GetNodeOrNull<CanvasItem>("Visual") is CanvasItem cone) cone.Modulate = LanternBeamModulate;
                _generalLanterns.Add(lantern);
            }
        }

        private void OnGeneralPhaseEntered(int phase) {
            if (phase < 1) return;
            // The phase change usually lands inside the hit's physics flush; hop off it.
            // Direct (test) calls stay synchronous.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) Callable.From(LightGeneralLanterns).CallDeferred();
            else LightGeneralLanterns();
        }

        /// <summary>Lights the three lanterns. Idempotent; public as a test seam.</summary>
        public void LightGeneralLanterns() {
            if (GeneralLanternsLit || IsBossDefeated || _generalLanterns.Count == 0) return;
            GeneralLanternsLit = true;
            foreach (SearchlightZone lantern in _generalLanterns) {
                lantern.Enabled = true;
                lantern.Visible = true;
                lantern.Monitoring = true;
            }
        }

        /// <summary>Douses them once the General falls: the hero still has to walk to the sealing anchor.</summary>
        private void DouseGeneralLanterns() {
            GeneralLanternsLit = false;
            foreach (SearchlightZone lantern in _generalLanterns) {
                if (!IsInstanceValid(lantern)) continue;
                lantern.Enabled = false;
                lantern.Visible = false;
                if (Player != null) lantern.RemovePlayer(Player);
                lantern.SetDeferred(Area2D.PropertyName.Monitoring, false);
            }
        }

        public string CheckpointID(int index) => $"{LevelID}_checkpoint_{index}";

        // === Encounters ===
        //
        // GU-L8 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks),
        // each exactly its manifest row; the lock takes the same per-row
        // ScaleEncounterCount prefix the reward manifest compiles.

        // Package 12 W2 (GAP-13): explicit encounter baseline. GU-L8: the encounters
        // are the three locks; every anchor lists every lock before it, and a resume
        // builds those Cleared (the seawall wave used to respawn behind the hero on
        // every resume).
        private const string BerlinLevelID = "level_09_berlin";

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [BerlinLevelID + "_checkpoint_0"] = Array.Empty<string>(),
                [BerlinLevelID + "_checkpoint_1"] = new[] { LockSeawall, LockLanternWatch },
                [BerlinLevelID + "_checkpoint_2"] = new[] { LockSeawall, LockLanternWatch, LockStormAnchor }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>The anchor's non-encounter world state; the locks come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == CheckpointID(1) || checkpointID == CheckpointID(2)) {
                // Both mid-level anchors stand east of the boarding gate (GU-L8 moved
                // the Middle past it, and its baseline lists the Lantern Watch, which
                // needs every line cut). A resume that lost the puzzle flag would
                // strand the player behind a sealed gate with the gauntlet still live
                // behind it, so the gate opens and the beams go dark structurally
                // rather than relying on the save.
                _resumedPastTheGate = true;
            }
        }

        // === Era mechanics ===

        protected override void OnLevelReady() {
            if (_resumedPastTheGate || _feedPuzzle is { IsCompleted: true }) {
                // Blow the relays through the same path the player's attacks take, so
                // the counter, the conditions, and the darkened beams all agree with
                // a run that actually cut them (the Level 2 generator precedent).
                CutRemainingRelays();
                OnAllFeedsCut();
            } else {
                PostFeedObjective();
            }
        }

        private void CutRemainingRelays() {
            foreach (DestructibleBlock relay in _feedRelays) {
                if (!IsInstanceValid(relay)) continue;
                for (int guard = 0; guard < 8 && !relay.IsDestroyed; guard++) {
                    relay.TakeEnvironmentDamage(relay.MaxHP);
                }
            }
        }

        /// <summary>
        /// Hakata's lanterns never touch the Ultimate meter; a catch is a warning that a
        /// thunder-bomb volley lands in <see cref="SearchlightZone.ExposureGraceSeconds"/>.
        /// The 12-standard budget is locked, so this posts an objective rather than an
        /// alarm wave.
        /// </summary>
        private void OnBeamExposure() {
            ExposureAlarmCount++;
            SetObjective("berlin_objective_exposed");
        }

        /// <summary>
        /// One signal line cut. FIX-OBJECTIVE (2026-10-13): the relay stays cut
        /// through every later rewind, and the line's condition is set whether or
        /// not its lantern is already dark (it used to be skipped for a dark one,
        /// so a relay a rewind had re-built could never count again). The counter
        /// counts lines, not cuts.
        /// </summary>
        private void OnFeedRelayCut(DestructibleBlock relay) {
            if (relay == null || !_feedByRelay.TryGetValue(relay, out FeedLink link)) return;
            relay.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            DarkenSearchlight(link.LightNodeName);
            bool newlyCut = _feedPuzzle == null || _feedPuzzle.SetCondition(link.ConditionID, true);
            if (newlyCut) FeedsCut++;
            if (_feedPuzzle is not { IsCompleted: true }) PostFeedObjective();
        }

        /// <summary>
        /// Kills one beam for good. <see cref="SearchlightZone.Enabled"/> stops the
        /// sweep and the exposure tick; dropping the tracked player as well means a
        /// player standing in the cone when the relay blows is released instead of
        /// being frozen mid-countdown. Monitoring is deferred because this runs inside
        /// an Area2D hit callback. Idempotent.
        /// </summary>
        private bool DarkenSearchlight(string lightNodeName) {
            if (!_lightsByName.TryGetValue(lightNodeName, out SearchlightZone light)) return false;
            if (!IsInstanceValid(light) || !light.Enabled) return false;
            light.Enabled = false;
            if (Player != null) light.RemovePlayer(Player);
            light.SetDeferred(Area2D.PropertyName.Monitoring, false);
            if (light.GetNodeOrNull<CanvasItem>("Visual") is CanvasItem beam) beam.Modulate = DarkenedBeamModulate;
            return true;
        }

        /// <summary>Every relay is cut: the gate lifts and the whole strip goes dark. Idempotent.</summary>
        private void OnAllFeedsCut() {
            if (RadarGateOpen) return;
            RadarGateOpen = true;
            foreach (FeedLink link in FeedLinks) DarkenSearchlight(link.LightNodeName);
            OpenDoor(ref _radarGate);
            SetObjective("berlin_objective_reach_bunker");
        }

        private void PostFeedObjective() =>
            SetObjective("berlin_objective_cut_feeds", FeedsCut, FeedLinks.Length);

        /// <summary>
        /// GU-L8: a cleared or reverted lock hands the objective back to the level's
        /// own, recomputed from live state (the base would re-post the last one set,
        /// which can be a stale "exposed" warning).
        /// </summary>
        protected override void RestoreLevelObjective() {
            if (RadarGateOpen) SetObjective("berlin_objective_reach_bunker");
            else PostFeedObjective();
        }

        // === Searchlight footprint geometry (shared with Level09ContentTests) ===

        /// <summary>
        /// Conservative world-space span an arc-sweeping searchlight cone can reach.
        /// The authored cone is an isoceles triangle from the apex down to
        /// <see cref="SearchlightConeDepth"/> with half-width
        /// <see cref="SearchlightConeHalfWidth"/>; rotating it by half the sweep arc
        /// pushes its far corner out to
        /// <c>halfWidth*cos(a) + depth*sin(a)</c>.
        ///
        /// Used to author checkpoints and cover pockets outside every beam, and
        /// asserted by the content test so re-tuning a sweep arc cannot silently drop
        /// a respawn point into a live beam.
        /// </summary>
        public static Rect2 SweptFootprint(Vector2 origin, float sweepArcDegrees) {
            float halfArc = Mathf.DegToRad(Mathf.Abs(sweepArcDegrees) * 0.5f);
            float reach = SearchlightConeHalfWidth * Mathf.Cos(halfArc) + SearchlightConeDepth * Mathf.Sin(halfArc);
            return new Rect2(origin.X - reach, origin.Y, reach * 2f, SearchlightConeDepth);
        }

        /// <summary>Swept footprints of every authored beam, in scene order.</summary>
        public IEnumerable<Rect2> SearchlightFootprints() {
            foreach (SearchlightZone light in _searchlights) {
                if (IsInstanceValid(light)) yield return SweptFootprint(light.Position, light.SweepArcDegrees);
            }
        }
    }
}
