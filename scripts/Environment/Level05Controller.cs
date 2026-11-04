using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 5 - The Sinking Titanic, 1912. The Act I finale.
    ///
    /// The Severed have a siphon aboard, drinking the night's energy dry (S18/S33:
    /// they want only the energy, never a better or worse outcome), so the level
    /// is a losing race with the sea: the player climbs sternward
    /// across four listing deck sections while a level-wide
    /// <see cref="RisingWaterZone"/> escalates room by room behind them.
    ///
    /// Two independent, deliberately NON-overlapping flood zones:
    /// <list type="bullet">
    /// <item><b>Hull flood</b> (x 0 - <see cref="ArenaStartX"/>): the ship going
    ///       down by the bow. Steps up on room entry, on a mid-corridor trigger,
    ///       and then on a timer once the boat deck is reached.</item>
    /// <item><b>Arena flood</b> (<see cref="ArenaStartX"/> - level end): sealed
    ///       and inert until the Tidal Overseer reaches phase 2, which is what makes
    ///       it the design's "drowning arena hazard boss".</item>
    /// </list>
    /// They never overlap, because <see cref="EnvironmentPlayerModifiers"/>
    /// multiplies every live source: two water zones on one player would stack to a
    /// 0.25x crawl and double the drowning ticks.
    ///
    /// Because the decks rise as x grows, one global water line models a listing
    /// ship correctly on its own - the bow drowns while the stern is still dry.
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: 12 standards, 1 elite,
    /// 1 boss, 4 extractors. The Titanic is a civilian vessel with no local military
    /// population for the Severed to brainwash, so the roster is Severed-only
    /// (design-godot.md 2363): chrono_slasher standards, one tech_enforcer elite.
    /// Since S27 (Package 13 W2) it also hosts the scripted Eraser debut — one
    /// <c>unbound_eraser</c> at <see cref="EraserDebutTriggerID"/> — and pays the
    /// retired Level 4A's dust row: 55 required / 25 boss / 30 optional.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L4): the
    /// stair decks (380–3400), the boiler casing (3420–5880), the E-deck run up
    /// the listing ramp (6160–7520) and the boat deck (7540–8820) are
    /// <see cref="EncounterLock"/>s, one per room, and the hull flood rises on
    /// their clears rather than on x triggers, so the sea always comes in behind
    /// the hero instead of over a fight it cannot leave: area 1's clear floods
    /// D0/D1, area 2's clear is the boiler surge (D2/E1) and area 3a's clear
    /// floods E2, each once the hero stands on the next dry deck; area 3b's clear
    /// starts the 14 s boat-deck timer. The Eraser debut stays scripted (Sarah's bark, the F12
    /// reconstruction record) but the debut Eraser is a member of area 3a: its
    /// unscaled wave drops in once one E-deck slasher is left. Room 1 gained the
    /// room trigger and camera rect it never had, and the PreBoss anchor moved
    /// past the hull flood onto the stern rail (9060), outside every lock.</para>
    ///
    /// <para><b>The Well Deck and the Davit line</b> (gameplay review, GR-LV2: rulings 7a,
    /// 7b, 7c, 7c+). The staircase lock was carved to 380-2380, and the span it left,
    /// 2400-3400, is a platforming stretch over the open well: its trench is the first thing
    /// the sea takes (hull step 1 stands over its floor and under the deck), so a hero who
    /// falls in wades out up a walkable ramp. Above it the Davit line climbs back west to the
    /// secret. The decks and the flood's dry-from table did not move. The recipe is
    /// <c>docs/handoffs/GR_PILOT.md</c>.</para>
    /// </summary>
    public partial class Level05Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string TitanicLevelID = "level_05_titanic";

        public override string LevelID => TitanicLevelID;
        public override CampaignLevel Level => CampaignLevel.Titanic;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 3 authored rooms x 90 s, rounded up to the nearest
        /// 30 s, plus the Well Deck's +30 (§1.14). Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(270f, 1);
        public override string LevelTitleKey => "titanic_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_05_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240, 1270);

        // GR-LV2: the secret cache sits at the end of the Davit line, on its last platform
        // (top 630), 95 px over it as it stood on the old catwalk, and high above the rising
        // hull flood's last step (720).
        protected override Vector2? SecretCachePosition => new(DavitEndX, 535f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor on the stern rail, outside the hull flood (x < 8800), short of the arena
        // (9400). GU-L4 (2026-10-13): 9100 -> 9200, because the PreBoss anchor moved from
        // 8650 to 9060 (the font keeps 140 px from it and its channel circle ends 200 px
        // short of the arena).
        protected override Vector2? RestorationFontPosition => new(9200f, DeckBoat);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        /// <summary>Act I finale: the extra beat between the last wave and the boss.</summary>
        public string PreBossDialogueID => $"{DialoguePrefix}.preboss";

        protected override string InitialObjectiveKey => "titanic_objective_reach_stern";
        protected override string BossObjectiveKey => "titanic_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "titanic_objective_complete";

        // === Dimensions ===

        public const float LevelWidth = 11200f;
        public const float LevelHeight = 1400f;

        private const float Room2StartX = 3400f;
        private const float Room3StartX = 6200f;
        /// <summary>Boundary between the hull flood and the boss arena flood.</summary>
        public const float ArenaStartX = 8800f;

        // Deck heights, bow (low, left) to stern (high, right).
        private const float DeckD0 = 1300f;
        private const float DeckD1 = 1220f;
        private const float DeckD2 = 1140f;
        private const float DeckE1 = 1040f;
        private const float DeckE2 = 940f;
        private const float DeckBoat = 800f;

        // The west ends of the raised decks: each listing ramp's high joint.
        private const float DeckD1WestX = 1100f;
        private const float DeckD2WestX = 2300f;
        private const float DeckE1WestX = 4600f;
        private const float DeckE2WestX = 5500f;

        /// <summary>
        /// FIX-ENEMY (2026-10-13): the five listing ramps, bow to stern, each
        /// authored by its two joints — <c>Low</c>, where its slope leaves the top
        /// of the lower deck, and <c>High</c>, the upper deck's west top corner —
        /// so <see cref="StoryLevelControllerBase.BuildRampBetweenJoints"/> lays both joints flush by
        /// construction. The ramps used to be authored by centre, width and angle
        /// (12°, 12°, 13°, 13°, 16°): each ended 19–41 px under its upper deck's
        /// west face, a riser that stopped every walker (the hero needed a jump;
        /// a mob never got up), and its rotated corners stood 0.5–1.6 px proud of
        /// the decks, a lip that stopped a mob walking down onto the slope. The
        /// low joints are where the old slopes left their decks, so the ramps
        /// keep their footprints and steepen to 15–22°.
        /// </summary>
        public static readonly (Vector2 Low, Vector2 High)[] ListingRampJoints = {
            (new Vector2(800f, DeckD0), new Vector2(DeckD1WestX, DeckD1)),
            (new Vector2(2000f, DeckD1), new Vector2(DeckD2WestX, DeckD2)),
            (new Vector2(4280f, DeckD2), new Vector2(DeckE1WestX, DeckE1)),
            (new Vector2(5230f, DeckE1), new Vector2(DeckE2WestX, DeckE2)),
            (new Vector2(7150f, DeckE2), new Vector2(BoatDeckWestX, DeckBoat))
        };


        // === Checkpoints ===

        public const string Checkpoint0 = TitanicLevelID + "_checkpoint_0";
        public const string Checkpoint1 = TitanicLevelID + "_checkpoint_1";
        public const string Checkpoint2 = TitanicLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        // === Flooding ===

        /// <summary>Global Y the hull water line is measured down from.</summary>
        public const float HullWaterOriginY = 1360f;
        /// <summary>Water-line heights above <see cref="HullWaterOriginY"/>, one per step.</summary>
        public static readonly float[] HullStepHeights = { 0f, 170f, 340f, 510f, 640f };

        public const float ArenaWaterOriginY = 980f;
        public static readonly float[] ArenaStepHeights = { 0f, 220f, 320f };

        /// <summary>Seconds between the last two hull steps once the boat deck opens.</summary>
        private const float BoatDeckFloodSeconds = 14f;
        /// <summary>Seconds between the arena's phase-2 steps.</summary>
        private const float ArenaFloodSeconds = 8f;

        public RisingWaterZone HullFlood { get; private set; }
        public RisingWaterZone ArenaFlood { get; private set; }

        /// <summary>
        /// Hull step a checkpoint resume restores. Authored to leave every respawn
        /// point above the water line; <see cref="RestoreFloodForCheckpoint"/> then
        /// enforces that structurally rather than trusting this table's arithmetic.
        /// </summary>
        public static int HullStepForCheckpoint(string checkpointID) => checkpointID switch {
            Checkpoint1 => 2,
            Checkpoint2 => 3,
            _ => 0
        };

        // === Encounters (locked: 12 standards / 1 elite / 1 boss / 4 extractors) ===

        public const string StandardEnemyID = "chrono_slasher";
        public const string EliteEnemyID = "tech_enforcer";
        public const string BossResourcePath = "res://resources/Bosses/tidal_eraser.tres";

        /// <summary>
        /// P1 (2026-10-04): the stern rail. West edge is <see cref="BossArenaWestX"/>
        /// (spawn 10300 - 900), so the pre-boss beat (8960 since GU-L4) lands first; east edge
        /// is the stern wall's inner face. The Overseer used to fight on the boat
        /// deck at x ~8000, outside the arena entirely.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, LevelWidth - 220f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10300f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

        /// <summary>
        /// Authored Normal-difficulty spawn table, one wave per encounter lock and
        /// per manifest row (<c>level_05.wave{n}</c>). Difficulty scaling takes a
        /// prefix of each wave through <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>.
        /// GU-L4 (2026-10-13): the old wave 3 is split in two — wave 3 holds the
        /// E-deck run (area 3a), wave 4 the boat deck (area 3b) — because the
        /// boat deck's flood steps now advance on each half's clear.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - D-Deck, Grand Staircase.
            (StandardEnemyID, 1, new Vector2(1200f, DeckD1 - 20f)),
            (StandardEnemyID, 1, new Vector2(1700f, DeckD1 - 20f)),
            // GR-LV2: 2500 -> 1950 and 3100 -> 2330, inside the carved lock.
            (StandardEnemyID, 1, new Vector2(1950f, DeckD1 - 20f)),
            (StandardEnemyID, 1, new Vector2(2330f, DeckD2 - 20f)),
            // Wave 2 - E-Deck, flooding boiler casing.
            (StandardEnemyID, 2, new Vector2(3800f, DeckD2 - 20f)),
            (StandardEnemyID, 2, new Vector2(4300f, DeckD2 - 20f)),
            (StandardEnemyID, 2, new Vector2(4900f, DeckE1 - 20f)),
            (StandardEnemyID, 2, new Vector2(5600f, DeckE2 - 20f)),
            // Wave 3 - the E-deck run below the boat deck.
            (StandardEnemyID, 3, new Vector2(6600f, DeckE2 - 20f)),
            (StandardEnemyID, 3, new Vector2(7000f, DeckE2 - 20f)),
            // Wave 4 - Boat Deck, the elite holds the stern stair.
            (StandardEnemyID, 4, new Vector2(7800f, DeckBoat - 20f)),
            (StandardEnemyID, 4, new Vector2(8300f, DeckBoat - 20f)),
            (EliteEnemyID, 4, new Vector2(8500f, DeckBoat - 20f))
        };

        /// <summary>The number of authored waves (and lock waves drawn from <see cref="SpawnTable"/>).</summary>
        public const int WaveCount = 4;

        /// <summary>Four extractors, each guarding an off-route climb. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_05_extractor_0", new Vector2(1500f, 1052f)),
            ("level_05_extractor_1", new Vector2(4400f, 852f)),
            ("level_05_extractor_2", new Vector2(5700f, 772f)),
            ("level_05_extractor_3", new Vector2(7900f, 652f))
        };

        public static int StandardEnemyCount => CountOf(StandardEnemyID);
        public static int EliteEnemyCount => CountOf(EliteEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) if (id == enemyID) total++;
            return total;
        }

        // === GU-L4 (2026-10-12): the four encounter locks ====================

        /// <summary>The F10 encounter IDs of the four locks (they start with the level ID).</summary>
        public const string LockStaircase = TitanicLevelID + "_lock_staircase";
        public const string LockBoilerCasing = TitanicLevelID + "_lock_boiler_casing";
        public const string LockEDeck = TitanicLevelID + "_lock_e_deck";
        public const string LockBoatDeck = TitanicLevelID + "_lock_boat_deck";

        /// <summary>The manifest row of the debut Eraser (<c>@0</c>: never difficulty-scaled).</summary>
        public const string EraserDebutRowID = "level_05.eraser_debut";

        /// <summary>
        /// The stair decks, east of the entry anchor (300). GR-LV2: its east end moved from
        /// the room seam (3400) to 2380, on D2, so the span past it can be the Well Deck.
        /// </summary>
        public const float StaircaseLockMinX = 380f;
        public const float StaircaseLockMaxX = 2380f;
        /// <summary>The boiler casing, ending on E2 short of the Middle anchor (6100) and its swing reach.</summary>
        public const float BoilerCasingLockMinX = Room2StartX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float BoilerCasingLockMaxX = 5880f;
        /// <summary>Area 3a: the E-deck run and the listing ramp, its exit on the boat deck (from 7500).</summary>
        public const float EDeckLockMinX = 6160f;
        public const float EDeckLockMaxX = 7520f;
        /// <summary>Area 3b: the boat deck, its exit on the stern rail (from 8800), short of the PreBoss anchor.</summary>
        public const float BoatDeckLockMinX = EDeckLockMaxX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float BoatDeckLockMaxX = 8820f;

        /// <summary>
        /// The debut Eraser's spawn: on the E-deck run ahead of the hero, 110 px
        /// over the deck, so it drops in from above (area 3a's third wave).
        /// </summary>
        public static readonly Vector2 EraserDebutLockSpawn = new(6850f, DeckE2 - 110f);

        /// <summary>
        /// The Middle and PreBoss anchors. GU-L4: the PreBoss anchor moved from the
        /// boat deck (8650, inside the boat-deck fight and the hull flood) onto the
        /// stern rail (9060), past the boat deck's barrier and swing reach.
        /// </summary>
        public const float MiddleCheckpointX = 6100f;
        public const float PreBossCheckpointX = 9060f;

        /// <summary>GU-L4: the stern-rail room trigger, east of the boat deck's exit barrier (was 8800).</summary>
        public const float SternRailTriggerX = 8880f;
        /// <summary>GU-L4: the pre-boss beat's trigger, between the boat deck's barrier and the PreBoss anchor (was 8720).</summary>
        public const float PreBossTriggerX = 8960f;

        // === GR-LV2: the Well Deck and the Davit line ========================

        public const string WellDeckStretchID = "level_05.stretch_well_deck";
        public const string DavitRouteID = "level_05.route_davit";
        public const string SwarmRowID = "level_05.swarm1";

        public const float WellDeckMinX = StaircaseLockMaxX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float WellDeckMaxX = Room2StartX;
        /// <summary>Where D2 stops for the well and picks up again: the trench's two ramps' high joints.</summary>
        public const float WellDeckWestEndX = 2440f;
        public const float WellDeckEastStartX = 3360f;
        /// <summary>The Davit line's last platform, which holds the secret.</summary>
        public const float DavitEndX = 2560f;

        /// <summary>
        /// The swarm row on the boiler casing, 2,460 px wide, so eight
        /// (<see cref="SwarmWaveRules.RowMobsForLockWidth"/>), each on the deck under it: D2, E1
        /// and E2 list. Pipe B stands among them for a launched one. Easy's prefix gives the
        /// western five.
        /// </summary>
        public static readonly Vector2[] SwarmSpawns = {
            new(3640f, DeckD2 - 20f), new(3860f, DeckD2 - 20f), new(4060f, DeckD2 - 20f), new(4230f, DeckD2 - 20f),
            new(4720f, DeckE1 - 20f), new(5040f, DeckE1 - 20f), new(5600f, DeckE2 - 20f), new(5800f, DeckE2 - 20f)
        };

        private EncounterLock _staircaseLock;
        private EncounterLock _boilerCasingLock;
        private EncounterLock _eDeckLock;
        private EncounterLock _boatDeckLock;

        public EncounterLock StaircaseLock => _staircaseLock;
        public EncounterLock BoilerCasingLock => _boilerCasingLock;
        public EncounterLock EDeckLock => _eDeckLock;
        public EncounterLock BoatDeckLock => _boatDeckLock;

        // === Runtime state ===

        private Timer _boatDeckFloodTimer;
        private bool _preBossShown;
        private bool _arenaFlooded;
        private bool _phaseBound;

        // === Era palette ===

        protected override Color FloorColor => new(0.17f, 0.19f, 0.23f);
        protected override Color FloorEdgeColor => new(0.55f, 0.45f, 0.28f);
        protected override Color PlatformColor => new(0.34f, 0.27f, 0.18f);
        protected override Color WallColor => new(0.12f, 0.14f, 0.18f);

        private static readonly Color RampColor = new(0.22f, 0.2f, 0.17f);
        private static readonly Color HullWaterColor = new(0.09f, 0.22f, 0.34f, 0.5f);
        private static readonly Color ArenaWaterColor = new(0.11f, 0.26f, 0.38f, 0.55f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1GrandStaircase();
            BuildWellDeck();
            BuildRoom2BoilerCasing();
            BuildRoom3BoatDeck();
            BuildRoom4SternRail();
            BuildFloodZones();
            BuildExtractors(ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(LevelWidth - 220f, 0f, LevelHeight);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L4: the four rooms as encounter locks, one wave per manifest row
        /// (plus the debut Eraser's unscaled row in area 3a). The decks list, so
        /// each barrier stands on the deck under it. Built here, before the resume
        /// baseline, so a resume past a lock builds it Cleared.
        /// </summary>
        private void BuildEncounterLocks() {
            // D-deck, live on entry: pre-placed.
            _staircaseLock = BuildLock(LockStaircase, StaircaseLockMinX, StaircaseLockMaxX,
                entryFloorY: DeckD0, exitFloorY: DeckD2,
                new[] { WaveFromTable(1, EncounterWaveTrigger.OnBuild) },
                "encounter_lock_objective_clear");
            // The casing crew comes up the stairs once the hero is in.
            _boilerCasingLock = BuildLock(LockBoilerCasing, BoilerCasingLockMinX, BoilerCasingLockMaxX,
                entryFloorY: DeckD2, exitFloorY: DeckE2,
                new[] { WaveFromTable(2, EncounterWaveTrigger.OnEngage), CasingSwarmWave() },
                "encounter_lock_objective_clear");
            // Area 3a: the E-deck slashers are posted; the Eraser drops in once
            // one of them is left (never difficulty-scaled: the @0 debut row).
            // It keeps the level's objective so the debut bark stays readable.
            _eDeckLock = BuildLock(LockEDeck, EDeckLockMinX, EDeckLockMaxX,
                entryFloorY: DeckE2, exitFloorY: DeckBoat,
                new[] {
                    WaveFromTable(3, EncounterWaveTrigger.OnBuild),
                    new EncounterLockWave {
                        RowID = EraserDebutRowID,
                        Spawns = new[] { new EncounterLockSpawn(EraserDebutTrigger.EraserEnemyID, EraserDebutLockSpawn) },
                        Trigger = EncounterWaveTrigger.WhenAliveAtMost,
                        AliveAtMost = 1,
                        Scaled = false
                    }
                },
                "");
            // Area 3b: the elite holds the stern stair.
            _boatDeckLock = BuildLock(LockBoatDeck, BoatDeckLockMinX, BoatDeckLockMaxX,
                entryFloorY: DeckBoat, exitFloorY: DeckBoat,
                new[] { WaveFromTable(4, EncounterWaveTrigger.OnBuild) },
                "encounter_lock_objective_clear");

            foreach (EncounterLock encounterLock in new[] { _staircaseLock, _boilerCasingLock, _eDeckLock, _boatDeckLock }) {
                encounterLock.StateChanged += OnHullLockStateChanged;
            }
        }

        private EncounterLock BuildLock(string lockID, float minX, float maxX, float entryFloorY, float exitFloorY,
            EncounterLockWave[] waves, string objectiveKey) {
            float half = EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
            return BuildEncounterLock(new EncounterLockSpec {
                LockID = lockID,
                Area = EncounterLockSpec.Span(minX, maxX, LevelHeight),
                EntryBarrierX = minX - half,
                ExitBarrierX = maxX + half,
                EntryBarrierYRange = new Vector2(StoryGateRules.ColumnTopY, entryFloorY),
                ExitBarrierYRange = new Vector2(StoryGateRules.ColumnTopY, exitFloorY),
                BarrierFloorY = exitFloorY,
                Waves = waves,
                ObjectiveKey = objectiveKey,
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>One lock wave: the authored <see cref="SpawnTable"/> entries of <paramref name="wave"/>, row <c>level_05.wave{n}</c>.</summary>
        private static EncounterLockWave WaveFromTable(int wave, EncounterWaveTrigger trigger) {
            var spawns = new List<EncounterLockSpawn>();
            foreach ((string enemyID, int entryWave, Vector2 position) in SpawnTable) {
                if (entryWave == wave) spawns.Add(new EncounterLockSpawn(enemyID, position));
            }
            return new EncounterLockWave { RowID = WaveRowID(wave), Spawns = spawns.ToArray(), Trigger = trigger };
        }

        /// <summary>The swarm row, each mob on its own deck: the helper's trigger and shape, with the listing decks' heights.</summary>
        private static EncounterLockWave CasingSwarmWave() {
            var xs = new float[SwarmSpawns.Length];
            for (int index = 0; index < xs.Length; index++) xs[index] = SwarmSpawns[index].X;
            EncounterLockWave wave = SwarmWave(SwarmRowID, DeckD2 - 20f, xs);
            for (int index = 0; index < xs.Length; index++) {
                wave.Spawns[index] = new EncounterLockSpawn(wave.Spawns[index].EnemyID, SwarmSpawns[index]);
            }
            return wave;
        }

        /// <summary>The manifest row of authored wave <paramref name="wave"/>.</summary>
        public static string WaveRowID(int wave) => $"level_05.wave{wave}";

        /// <summary>
        /// Deck slab tilted about its own top-centre so walking sternward is walking
        /// uphill. Stepped decks plus these ramps read as a hull going down by the
        /// bow without the collision seams a fully rotated floor plan would create.
        /// FIX-ENEMY (2026-10-13): built from its joints (<see cref="ListingRampJoints"/>)
        /// through the shared <see cref="StoryLevelControllerBase.BuildRampBetweenJoints"/>,
        /// so neither joint leaves a riser or a lip.
        /// </summary>
        private void BuildListingRamp(int index) =>
            BuildRampBetweenJoints(ListingRampJoints[index].Low, ListingRampJoints[index].High, RampColor);

        private void BuildRoom1GrandStaircase() {
            BuildRoomBackground("BG_Room1", 0f, Room2StartX, LevelHeight, new Color(0.07f, 0.09f, 0.12f, 0.4f));

            BuildFloor(0f, DeckD0, 900f);
            BuildListingRamp(0);
            BuildFloor(DeckD1WestX, DeckD1, 1000f);
            BuildListingRamp(1);
            // GR-LV2: D2 stops at the well.
            BuildFloor(DeckD2WestX, DeckD2, WellDeckWestEndX - DeckD2WestX);

            BuildOneWayPlatform(600f, 1140f, 240f);
            BuildOneWayPlatform(1500f, 1060f, 260f);

            BuildCheckpoint(300f, DeckD0 - 10f, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(0f, "titanic_room_grand_staircase", new Color(0.8f, 0.68f, 0.4f));

            // GU-L4 (2026-10-13): room 1 had no room trigger, so its camera was the
            // whole level and its mobs had no leash room. It confines to the stair
            // decks now, from the spawn.
            BuildRoomTransition("titanic_grand_staircase", new Vector2(300f, LevelHeight / 2f),
                Room1CameraBounds, new Vector2(80f, LevelHeight));
        }

        /// <summary>GU-L4: room 1's camera rect, the stair decks. GR-LV2: up to the well, which has its own rect.</summary>
        public static readonly Rect2 Room1CameraBounds = new(0f, 0f, WellDeckWestEndX, LevelHeight);

        /// <summary>
        /// GR-LV2: the Well Deck, 2400-3400, and the Davit line above it. The step line crosses
        /// the open well: a solid step, a crumbling one and a one-way one, then down to D2. Both
        /// ramps stay walkable, because hull step 1 floods the trench, and a one-way stands dry
        /// over the water as a way up. Every number is provisional (<c>PROVISIONAL-TRAVERSAL-RULES</c>).
        /// </summary>
        private void BuildWellDeck() {
            TraversalStretch well = BeginStretch(WellDeckStretchID, "titanic_room_well_deck",
                WellDeckMinX, WellDeckMaxX, DeckD2, WellDeckWestEndX, WellDeckEastStartX, 0f);
            BuildRoomDecoration(WellDeckMinX, "titanic_room_well_deck", new Color(0.55f, 0.7f, 0.8f));
            BuildStretchTrench(well);
            // D2 resumes under the boiler casing's entry barrier: that barrier needs floor under it.
            BuildFloor(WellDeckEastStartX, DeckD2, 3500f - WellDeckEastStartX);

            TraversalSurface westStep = BuildStretchStep(well, 2560f, 1040f, 200f);
            BuildStretchStep(well, 2810f, 980f, 180f, StretchStepKind.Crumbling, "level_05.crumbling_well_deck");
            TraversalSurface eastStep = BuildStretchStep(well, 3060f, 982f, 200f, StretchStepKind.OneWay);

            // Off the line: a dry way up out of the water, under the one-way step.
            BuildOneWayPlatform(3040f, 1110f, 200f);

            // The Davit line. Its base-kit line climbs east and back west, and ends in a 245 px hop.
            OptionalRoute davit = BeginOptionalRoute(DavitRouteID, SecretSourceID,
                SecretCachePosition.Value, eastStep);
            BuildRouteStep(davit, 3120f, 850f, 120f);
            TraversalSurface burstFrom = BuildRouteStep(davit, 3255f, 740f, 160f);
            BuildRouteStep(davit, 3005f, 640f, 180f);
            TraversalSurface end = BuildRouteStep(davit, DavitEndX, 640f, 220f);
            davit.MarkBurstShortcut(burstFrom, end);
            davit.MarkRiseShortcut(westStep, end);
        }

        private void BuildRoom2BoilerCasing() {
            BuildRoomBackground("BG_Room2", Room2StartX, Room3StartX - Room2StartX, LevelHeight,
                new Color(0.06f, 0.08f, 0.1f, 0.45f));

            BuildFloor(3500f, DeckD2, 900f);
            BuildListingRamp(2);
            BuildFloor(DeckE1WestX, DeckE1, 800f);
            BuildListingRamp(3);
            BuildFloor(DeckE2WestX, DeckE2, 800f);

            BuildOneWayPlatform(3900f, 980f, 220f);
            BuildOneWayPlatform(4400f, 860f, 200f);
            BuildOneWayPlatform(5000f, 900f, 220f);
            BuildOneWayPlatform(5700f, 780f, 200f);

            BuildCheckpoint(MiddleCheckpointX, DeckE2 - 10f, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "titanic_room_boiler_casing", new Color(0.75f, 0.5f, 0.3f));

            // GU-L4: the trigger stands just inside the boiler-casing lock (a hero
            // pressed against the staircase's exit barrier does not reach it), and
            // it only moves the camera now: the casing's wave is the lock's, step 1
            // rose on the staircase's clear, and the boiler surge (step 2) is the
            // casing's own clear rather than a mid-corridor trigger that flooded the
            // fight.
            BuildRoomTransition("titanic_boiler_casing", new Vector2(BoilerCasingLockMinX + 60f, LevelHeight / 2f),
                new Rect2(Room2StartX - 100f, 0f, 3000f, LevelHeight),
                new Vector2(80f, LevelHeight));
        }

        private void BuildRoom3BoatDeck() {
            BuildRoomBackground("BG_Room3", Room3StartX, ArenaStartX - Room3StartX, LevelHeight,
                new Color(0.08f, 0.1f, 0.14f, 0.4f));

            BuildFloor(6300f, DeckE2, 1000f);
            BuildListingRamp(4);
            BuildFloor(BoatDeckWestX, DeckBoat, ArenaStartX - BoatDeckWestX);

            // Davit climb: the only dry ground once the boat deck itself goes under.
            BuildOneWayPlatform(6800f, 800f, 220f);
            BuildOneWayPlatform(7300f, 700f, 200f);
            BuildOneWayPlatform(7900f, 660f, 220f);
            BuildOneWayPlatform(8300f, 600f, 200f);
            BuildOneWayPlatform(8600f, 660f, 240f);

            BuildEraserDebut();
            BuildCheckpoint(PreBossCheckpointX, DeckBoat - 10f, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "titanic_room_boat_deck", new Color(0.6f, 0.78f, 0.9f));

            BuildRoomTransition("titanic_boat_deck", new Vector2(Room3StartX, LevelHeight / 2f),
                new Rect2(Room3StartX - 100f, 0f, 2800f, LevelHeight),
                new Vector2(80f, LevelHeight),
                _ => EnterRoom3());
        }

        private void BuildRoom4SternRail() {
            BuildRoomBackground("BG_Room4", ArenaStartX, LevelWidth - ArenaStartX, LevelHeight,
                new Color(0.12f, 0.07f, 0.09f, 0.45f));

            BuildFloor(ArenaStartX, DeckBoat, LevelWidth - ArenaStartX - 220f);
            // Two rail gantries that stay above even the deepest arena flood step.
            BuildPlatform(9400f, 600f, 280f);
            BuildPlatform(10400f, 600f, 280f);

            BuildRoomDecoration(ArenaStartX, "titanic_room_stern_rail", new Color(0.95f, 0.45f, 0.4f));

            // GU-L4: both triggers moved east of the boat deck's exit barrier (8840):
            // a room trigger the hero touches mid-fight would hand the camera to
            // the stern rail when the lock clears, and the pre-boss beat pauses the
            // game, so neither may fire inside a lock.
            BuildRoomTransition("titanic_stern_rail", new Vector2(SternRailTriggerX, LevelHeight / 2f),
                new Rect2(ArenaStartX - 40f, 0f, LevelWidth - ArenaStartX + 40f, LevelHeight),
                new Vector2(80f, LevelHeight));

            BuildWaveTrigger("PreBossTrigger", new Vector2(PreBossTriggerX, 700f),
                StartPreBossBeat, new Vector2(70f, LevelHeight));

            // Arena spans 2160 px; the boss's ranged band (10 m x 60 px/unit = 600 px)
            // and the plan's wider 12 m reading both fit with room to reposition.
            BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, DeckBoat - 50f),
                "TidalEraserEncounter", BossArenaBounds);
        }

        private void BuildFloodZones() {
            HullFlood = BuildFloodZone("HullFlood", "level_05.hull_flood",
                new Vector2(ArenaStartX / 2f, HullWaterOriginY), ArenaStartX,
                HullStepHeights, HullWaterColor, drownDamage: 5, drownGrace: 3.5f);

            ArenaFlood = BuildFloodZone("ArenaFlood", "level_05.arena_flood",
                new Vector2((ArenaStartX + LevelWidth) / 2f, ArenaWaterOriginY), LevelWidth - ArenaStartX,
                ArenaStepHeights, ArenaWaterColor, drownDamage: 4, drownGrace: 4f);
            // Sealed until the Tidal Overseer breaks the stern open in phase 2.
            ArenaFlood.Enabled = false;
        }

        private RisingWaterZone BuildFloodZone(string name, string waterID, Vector2 origin, float width,
            float[] stepHeights, Color color, int drownDamage, float drownGrace) {
            var zone = new RisingWaterZone {
                Name = name,
                WaterID = waterID,
                Position = origin,
                StepHeights = stepHeights,
                AutoAdvanceSeconds = 0f,
                SubmergedMoveMultiplier = 0.55f,
                DrownGraceSeconds = drownGrace,
                DrownTickSeconds = 1f,
                DrownDamage = drownDamage,
                VisualWidth = width,
                VisualDepthBelowLine = LevelHeight,
                RewindPolicy = StoryRewindPolicy.RestoreCheckpointState
            };

            // Children must exist before the zone enters the tree: RisingWaterZone
            // resolves WaterVisualPath in _Ready, which fires on AddChild here.
            zone.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(width, LevelHeight * 2f) },
                Position = new Vector2(0f, -LevelHeight / 2f)
            });
            zone.AddChild(new ColorRect {
                Name = "WaterVisual",
                Color = color,
                ZIndex = 5,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            AddChild(zone);
            return zone;
        }

        // === Flow ===

        protected override void OnLevelReady() {
            if (EventBus.Instance != null && !_phaseBound) {
                _phaseBound = true;
                EventBus.Instance.OnBossPhaseChanged += OnBossPhaseChanged;
            }
            BuildCaptainTaking();
        }

        public override void _ExitTree() {
            if (_phaseBound && EventBus.Instance != null) {
                EventBus.Instance.OnBossPhaseChanged -= OnBossPhaseChanged;
            }
            _phaseBound = false;
            base._ExitTree();
        }

        /// <summary>True once the hero has entered the boat-deck room (its objective is the level's own from then on).</summary>
        private bool _boatDeckReached;

        private void EnterRoom3() {
            _boatDeckReached = true;
            SetObjective("titanic_objective_boat_deck");
        }

        /// <summary>
        /// GU-L4: the hull flood rises on the locks' clears instead of on x
        /// triggers. Only a clear from Engaged counts: a lock restored Cleared by
        /// a resume baseline raises nothing (the anchor's own step is restored by
        /// <see cref="RestoreFloodForCheckpoint"/>). The staircase's clear arms
        /// step 1, the casing's step 2 (the boiler surge) and the E-deck's step 3;
        /// each rises once the hero is clear of it (<see cref="TryRaisePendingHullStep"/>).
        /// The boat deck's clear starts the 14 s timer to the last step.
        /// </summary>
        private void OnHullLockStateChanged(EncounterLock encounterLock, EncounterLockState previous, EncounterLockState current) {
            if (current != EncounterLockState.Cleared || previous != EncounterLockState.Engaged) return;
            if (encounterLock == _staircaseLock) ArmHullStep(1);
            else if (encounterLock == _boilerCasingLock) ArmHullStep(2);
            else if (encounterLock == _eDeckLock) ArmHullStep(3);
            else if (encounterLock == _boatDeckLock) StartBoatDeckFlooding();
        }

        /// <summary>
        /// The hull step a lock's clear has armed and the flood has not yet reached:
        /// it rises once the hero stands on the deck above it (0: none pending).
        /// </summary>
        public int PendingHullStep { get; private set; }

        /// <summary>
        /// Per hull step, the west end of the first deck that stays dry above it —
        /// D2 for step 1, E2 for step 2, the boat deck for step 3. A pending step
        /// rises only once the hero's feet are east of it and above the new line,
        /// so a clear never floods the deck the hero is still standing on (the
        /// water would have caught a hero who finished the casing on D2, or the
        /// E-deck run on E2, with a long wade at ×0.55 to the next dry deck).
        /// </summary>
        public static readonly float[] HullStepDryFromX = { 0f, DeckD2WestX, DeckE2WestX, BoatDeckWestX, ArenaStartX };

        /// <summary>The boat deck's west end.</summary>
        public const float BoatDeckWestX = 7500f;
        /// <summary>How far above a pending step's line the hero's feet must be before it rises behind it.</summary>
        public const float HullStepClearancePixels = 16f;

        private void ArmHullStep(int step) {
            if (HullFlood == null || !IsInstanceValid(HullFlood) || HullFlood.CurrentStep >= step) return;
            PendingHullStep = Mathf.Max(PendingHullStep, step);
            TryRaisePendingHullStep();
        }

        /// <summary>Raises the pending hull step once the hero stands clear of it. Public as a test seam.</summary>
        public bool TryRaisePendingHullStep() {
            if (PendingHullStep <= 0 || HullFlood == null || !IsInstanceValid(HullFlood)) return false;
            if (HullFlood.CurrentStep >= PendingHullStep) {
                PendingHullStep = 0;
                return false;
            }
            if (Player == null || !IsInstanceValid(Player)) return false;
            int step = Mathf.Clamp(PendingHullStep, 1, HullStepHeights.Length - 1);
            float line = HullWaterOriginY - HullStepHeights[step];
            Vector2 feet = Player.GlobalPosition;
            if (feet.X < HullStepDryFromX[step] || feet.Y >= line - HullStepClearancePixels) return false;
            PendingHullStep = 0;
            RaiseHullFlood(step);
            return true;
        }

        public override void _PhysicsProcess(double delta) {
            if (PendingHullStep > 0) TryRaisePendingHullStep();
            AdoptLockSpawnedEraserDebut();
        }

        /// <summary>
        /// The last hull step is timed rather than triggered so the boat deck reads
        /// as a losing race. Driven by an owned <see cref="Timer"/> instead of
        /// <see cref="RisingWaterZone.AutoAdvanceSeconds"/>: the component latches
        /// its auto timer in _Ready, so enabling it mid-level fires a step instantly
        /// and would double-advance on top of the room's authored step.
        /// </summary>
        private void StartBoatDeckFlooding() {
            if (_boatDeckFloodTimer != null && IsInstanceValid(_boatDeckFloodTimer)) return;
            _boatDeckFloodTimer = new Timer {
                Name = "BoatDeckFloodTimer",
                WaitTime = BoatDeckFloodSeconds,
                OneShot = false,
                Autostart = true
            };
            _boatDeckFloodTimer.Timeout += OnBoatDeckFloodTick;
            AddChild(_boatDeckFloodTimer);
        }

        private void OnBoatDeckFloodTick() {
            if (HullFlood == null || !IsInstanceValid(HullFlood)) return;
            if (!HullFlood.AdvanceWaterLevel()) StopBoatDeckFlooding();
        }

        private void StopBoatDeckFlooding() {
            if (_boatDeckFloodTimer == null || !IsInstanceValid(_boatDeckFloodTimer)) return;
            _boatDeckFloodTimer.Stop();
            _boatDeckFloodTimer.Timeout -= OnBoatDeckFloodTick;
            _boatDeckFloodTimer.QueueFree();
            _boatDeckFloodTimer = null;
        }

        /// <summary>Raises the hull water to at least <paramref name="step"/>; never lowers it.</summary>
        private void RaiseHullFlood(int step) {
            if (HullFlood == null || !IsInstanceValid(HullFlood)) return;
            if (HullFlood.CurrentStep >= step) return;
            HullFlood.SetWaterLevel(step);
        }

        private void StartPreBossBeat() {
            if (_preBossShown) return;
            _preBossShown = true;
            // G4: never after the Overseer has fallen (it would land after, or
            // over, the post-boss exchange).
            StartPreBossDialogue(PreBossDialogueID);
        }

        /// <summary>True once the pre-boss trigger has been crossed (whether or not the beat played). Test seam.</summary>
        public bool PreBossTriggerCrossed => _preBossShown;

        /// <summary>
        /// The design's "drowning arena hazard boss": phase 2 breaks the stern open.
        /// Setting AutoAdvanceSeconds on a zone whose auto timer is already expired
        /// steps it immediately, which is exactly the phase-2 beat, then again every
        /// <see cref="ArenaFloodSeconds"/>. The top step still leaves both rail
        /// gantries dry - the water applies pressure, it never becomes a kill floor.
        /// </summary>
        private void OnBossPhaseChanged(int phaseIndex) {
            if (phaseIndex < 1 || _arenaFlooded) return;
            if (ArenaFlood == null || !IsInstanceValid(ArenaFlood)) return;
            _arenaFlooded = true;
            ArenaFlood.Enabled = true;
            ArenaFlood.AutoAdvanceSeconds = ArenaFloodSeconds;
        }

        protected override void OnBossDefeated(BossEncounterController encounter, BossDefeatedPayload payload) {
            StopBoatDeckFlooding();
            if (ArenaFlood != null && IsInstanceValid(ArenaFlood)) ArenaFlood.AutoAdvanceSeconds = 0f;
            // Package 13 W4 (S19): as the Overseer falls, a cold column takes
            // Captain Smith at the wheel — before the post-boss exchange (S18).
            if (!IsBossDefeated) CaptainTaking?.Play();
            base.OnBossDefeated(encounter, payload);
        }

        // === Package 13 W2 region: the Eraser debut (S27) ====================
        // S27 retired Level 4A and moved its scripted Eraser debut here: the
        // Level 0 watcher's silhouette drops onto the listing boat deck,
        // between the Middle and PreBoss anchors. It is the F12 independent
        // route trigger — crossing it spawns one Eraser and Sarah's bark and
        // grants no checkpoint, Mending, rewind refill or Integrity lock; its
        // timing never depends on a checkpoint or on difficulty (@0 in the
        // reward manifest: never scaled).

        /// <summary>The debut's stable route-encounter and reward-source ID.</summary>
        public const string EraserDebutTriggerID = TitanicLevelID + "_eraser_debut";

        /// <summary>
        /// The trigger sits on the E-deck run of the boat deck, past the Middle
        /// anchor (6100) and well before PreBoss (9060 since GU-L4). Since GU-L4 it
        /// only places the debut record: area 3a's lock spawns the Eraser.
        /// </summary>
        public static readonly Vector2 EraserDebutPosition = new(6900f, DeckE2 - 200f);

        /// <summary>The Eraser drops in from above, onto the listing deck ahead of the player.</summary>
        public static readonly Vector2 EraserDebutSpawnOffset = new(360f, -260f);

        public EraserDebutTrigger EraserDebut { get; private set; }

        private bool _eraserDebutCleared;

        private void BuildEraserDebut() {
            EraserDebut = new EraserDebutTrigger {
                Name = "EraserDebut",
                TriggerID = EraserDebutTriggerID,
                Position = EraserDebutPosition,
                SpawnOffset = EraserDebutSpawnOffset,
                // GU-L4: area 3a's lock spawns the debut Eraser as one of its
                // waves; crossing the route trigger spawns nothing, and the
                // trigger adopts the lock's body for its bark and its record.
                RouteTriggerSpawns = false
            };
            EraserDebut.DebutBegan += OnEraserDebutBegan;
            AddChild(EraserDebut);
        }

        /// <summary>
        /// Sarah's non-blocking debut bark. Never pauses gameplay — it is a barked
        /// line over a live ambush, not a sequence.
        /// </summary>
        private void OnEraserDebutBegan(EraserDebutTrigger trigger) =>
            SetObjective(EraserDebutTrigger.DebutBarkKey);

        private int _eDeckRegisteredSeen;

        /// <summary>
        /// GU-L4: hands the debut Eraser that area 3a's lock just spawned to the
        /// <see cref="EraserDebutTrigger"/>, which raises the bark and keeps the
        /// F12 reconstruction record. Checked only when the lock registers a new
        /// member, so it costs nothing on an ordinary tick.
        /// </summary>
        private void AdoptLockSpawnedEraserDebut() {
            if (_eDeckLock == null || !IsInstanceValid(_eDeckLock) || EraserDebut == null || !IsInstanceValid(EraserDebut)) return;
            int registered = _eDeckLock.RegisteredCount;
            if (registered == _eDeckRegisteredSeen) return;
            _eDeckRegisteredSeen = registered;
            if (EraserDebut.EncounterLive || EraserDebut.EncounterCleared) return;
            foreach (EnemyController member in _eDeckLock.LivingMembers) {
                if (member.Data?.EnemyID != EraserDebut.EnemyID) continue;
                EraserDebut.AdoptEraserDebut(member);
                return;
            }
        }

        // === Package 13 W4: Captain Smith's taking and the lifeboats (S18/S19) ===

        /// <summary>The post-boss exchange at the sealing anchor (S18).</summary>
        public string PostBossDialogueID => $"{DialoguePrefix}.postboss";

        private string[] _postBossBeats;

        protected override IReadOnlyList<string> PostBossDialogueIDs =>
            _postBossBeats ??= new[] { PostBossDialogueID };

        /// <summary>
        /// Where the captain stands: on the bridge wing above the stern arena, in
        /// view of the fight and out of the player's reach.
        /// </summary>
        public static readonly Vector2 CaptainPosition = new(10820f, 520f);

        /// <summary>The restoration vignette's placeholder line: the lifeboats row clear.</summary>
        public const string LifeboatsNoticeKey = "titanic_notice_lifeboats";

        /// <summary>The captain and the beam that takes him. Built at level ready.</summary>
        public ExtractionBeamPresentation CaptainTaking { get; private set; }

        /// <summary>True once the lifeboats vignette notice has been posted.</summary>
        public bool LifeboatsVignetteShown { get; private set; }

        private void BuildCaptainTaking() {
            if (CaptainTaking != null && IsInstanceValid(CaptainTaking)) return;
            CaptainTaking = new ExtractionBeamPresentation {
                Name = "CaptainSmith",
                Position = CaptainPosition
            };
            AddChild(CaptainTaking);
            // A pre-seal reload lands after the taking: the captain is already gone.
            if (IsBossDefeated || IsRestoringAwaitingSeal) CaptainTaking.MarkTaken();
        }

        /// <summary>
        /// The exit beat is the restoration vignette; its placeholder presentation is
        /// the lifeboats rowing clear as the lights go out.
        /// </summary>
        protected override void StartExitSequence() {
            if (!LifeboatsVignetteShown) {
                LifeboatsVignetteShown = true;
                Node2D anchor = SealingAnchor != null && IsInstanceValid(SealingAnchor)
                    ? SealingAnchor
                    : Player;
                EnvironmentNotice.Post(LifeboatsNoticeKey, anchor, seconds: 4f);
            }
            base.StartExitSequence();
        }

        // === Checkpoint resume ===

        /// <summary>
        /// A resume must never drop the player into water they cannot climb out of,
        /// so the flood state is restored alongside the cleared waves. The authored
        /// per-checkpoint step is only a starting point: the loop below walks it back
        /// down until the respawn point is provably above the line, which keeps the
        /// guarantee true even if the deck heights are re-authored later.
        /// </summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            // Package 12 W2 (GAP-13): the cleared encounters come from the explicit
            // encounter baseline below; this keeps only the flood state.
            RestoreFloodForCheckpoint(checkpointID);
            // Package 13 W2: before PreBoss the debut is rebuilt and stays
            // triggerable; the PreBoss baseline declares it fought, with its
            // reward still claimed.
            EraserDebut?.RestoreFromCheckpoint(_eraserDebutCleared, rewardAlreadyClaimed: _eraserDebutCleared);
            if (checkpointID == Checkpoint2) _boatDeckReached = true;
        }

        // Package 12 W2 (GAP-13): explicit encounter baseline, authored rather
        // than derived. GU-L4: the encounters are the four locks; every anchor
        // lists every lock before it, and a resume builds those Cleared. Package
        // 13 W2 (S27): the PreBoss anchor also restores the Eraser debut as
        // fought; the middle anchor precedes it, so it stays live there.
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockStaircase, LockBoilerCasing },
                [Checkpoint2] = new[] { LockStaircase, LockBoilerCasing, LockEDeck, LockBoatDeck, EraserDebutTriggerID }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            if (encounterID == EraserDebutTriggerID) _eraserDebutCleared = true;
        }

        /// <summary>GU-L4: a cleared or reverted lock hands back the room's own objective.</summary>
        protected override void RestoreLevelObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (_boatDeckReached) SetObjective("titanic_objective_boat_deck");
            else SetObjective(InitialObjectiveKey);
        }

        public void RestoreFloodForCheckpoint(string checkpointID) {
            if (ArenaFlood != null && IsInstanceValid(ArenaFlood)) {
                ArenaFlood.Enabled = false;
                ArenaFlood.AutoAdvanceSeconds = 0f;
                ArenaFlood.SetWaterLevel(0);
                _arenaFlooded = false;
            }
            if (HullFlood == null || !IsInstanceValid(HullFlood)) return;

            HullFlood.SetWaterLevel(HullStepForCheckpoint(checkpointID));
            if (Levels == null || !Levels.TryGetCheckpointPosition(checkpointID, out Vector2 respawn)) return;
            while (HullFlood.CurrentStep > 0 && respawn.Y >= HullFlood.WaterLineGlobalY) {
                HullFlood.SetWaterLevel(HullFlood.CurrentStep - 1);
            }
        }

        /// <summary>True when a fresh spawn at this checkpoint would be above both water lines.</summary>
        public bool IsCheckpointResumeDry(string checkpointID) {
            if (Levels == null || !Levels.TryGetCheckpointPosition(checkpointID, out Vector2 respawn)) return false;
            bool hullDry = HullFlood == null || !IsInstanceValid(HullFlood) ||
                respawn.X >= ArenaStartX || respawn.Y < HullFlood.WaterLineGlobalY;
            bool arenaDry = ArenaFlood == null || !IsInstanceValid(ArenaFlood) ||
                !ArenaFlood.Enabled || respawn.X < ArenaStartX || respawn.Y < ArenaFlood.WaterLineGlobalY;
            return hullDry && arenaDry;
        }

        // === Encounters ===
        //
        // GU-L4 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles.
    }
}
