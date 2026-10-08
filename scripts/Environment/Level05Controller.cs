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
    /// </summary>
    public partial class Level05Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string TitanicLevelID = "level_05_titanic";

        public override string LevelID => TitanicLevelID;
        public override CampaignLevel Level => CampaignLevel.Titanic;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 3 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 270f;
        public override string LevelTitleKey => "titanic_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_05_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240, 1270);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the top boiler-casing catwalk (one-way 3050/880, top 870), the last rung
        // of the stair-deck climb — a dead end the deck route walks beneath, and high
        // above the rising hull flood.
        protected override Vector2? SecretCachePosition => new(3050f, 775f);
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
        /// (spawn 10300 - 900), so the pre-boss beat at 8720 lands first; east edge
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
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves 2
        /// and 3 arm on their room triggers. Difficulty scaling takes a prefix of
        /// each wave through <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - D-Deck, Grand Staircase.
            (StandardEnemyID, 1, new Vector2(1200f, DeckD1 - 20f)),
            (StandardEnemyID, 1, new Vector2(1700f, DeckD1 - 20f)),
            (StandardEnemyID, 1, new Vector2(2500f, DeckD2 - 20f)),
            (StandardEnemyID, 1, new Vector2(3100f, DeckD2 - 20f)),
            // Wave 2 - E-Deck, flooding boiler casing.
            (StandardEnemyID, 2, new Vector2(3800f, DeckD2 - 20f)),
            (StandardEnemyID, 2, new Vector2(4300f, DeckD2 - 20f)),
            (StandardEnemyID, 2, new Vector2(4900f, DeckE1 - 20f)),
            (StandardEnemyID, 2, new Vector2(5600f, DeckE2 - 20f)),
            // Wave 3 - Boat Deck, the elite holds the stern stair.
            (StandardEnemyID, 3, new Vector2(6600f, DeckE2 - 20f)),
            (StandardEnemyID, 3, new Vector2(7000f, DeckE2 - 20f)),
            (StandardEnemyID, 3, new Vector2(7800f, DeckBoat - 20f)),
            (StandardEnemyID, 3, new Vector2(8300f, DeckBoat - 20f)),
            (EliteEnemyID, 3, new Vector2(8500f, DeckBoat - 20f))
        };

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

        // === Runtime state ===

        private readonly HashSet<int> _wavesSpawned = new();
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
            BuildRoom2BoilerCasing();
            BuildRoom3BoatDeck();
            BuildRoom4SternRail();
            BuildFloodZones();
            BuildExtractors(ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(LevelWidth - 220f, 0f, LevelHeight);
        }

        /// <summary>
        /// Deck slab tilted about its own top-centre so walking sternward is walking
        /// uphill. Stepped decks plus these ramps read as a hull going down by the
        /// bow without the collision seams a fully rotated floor plan would create.
        /// </summary>
        private StaticBody2D BuildListingRamp(float centerX, float centerY, float width, float degrees) {
            StaticBody2D ramp = BuildFloor(centerX - width / 2f, centerY, width, RampColor);
            ramp.RotationDegrees = degrees;
            return ramp;
        }

        private void BuildRoom1GrandStaircase() {
            BuildRoomBackground("BG_Room1", 0f, Room2StartX, LevelHeight, new Color(0.07f, 0.09f, 0.12f, 0.4f));

            BuildFloor(0f, DeckD0, 900f);
            BuildListingRamp(1000f, 1260f, 400f, -12f);
            BuildFloor(1100f, DeckD1, 1000f);
            BuildListingRamp(2200f, 1180f, 400f, -12f);
            BuildFloor(2300f, DeckD2, 1200f);

            BuildOneWayPlatform(600f, 1140f, 240f);
            BuildOneWayPlatform(1500f, 1060f, 260f);
            BuildOneWayPlatform(2600f, 980f, 240f);
            BuildOneWayPlatform(3050f, 880f, 220f);

            BuildCheckpoint(300f, DeckD0 - 10f, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(0f, "titanic_room_grand_staircase", new Color(0.8f, 0.68f, 0.4f));
        }

        private void BuildRoom2BoilerCasing() {
            BuildRoomBackground("BG_Room2", Room2StartX, Room3StartX - Room2StartX, LevelHeight,
                new Color(0.06f, 0.08f, 0.1f, 0.45f));

            BuildFloor(3500f, DeckD2, 900f);
            BuildListingRamp(4500f, 1090f, 440f, -13f);
            BuildFloor(4600f, DeckE1, 800f);
            BuildListingRamp(5450f, 990f, 440f, -13f);
            BuildFloor(5500f, DeckE2, 800f);

            BuildOneWayPlatform(3900f, 980f, 220f);
            BuildOneWayPlatform(4400f, 860f, 200f);
            BuildOneWayPlatform(5000f, 900f, 220f);
            BuildOneWayPlatform(5700f, 780f, 200f);

            BuildCheckpoint(6100f, DeckE2 - 10f, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "titanic_room_boiler_casing", new Color(0.75f, 0.5f, 0.3f));

            BuildRoomTransition("titanic_boiler_casing", new Vector2(Room2StartX, LevelHeight / 2f),
                new Rect2(Room2StartX - 100f, 0f, 3000f, LevelHeight),
                new Vector2(80f, LevelHeight),
                _ => EnterRoom2());

            // Mid-corridor surge: the casing fills while the player is still in it.
            BuildWaveTrigger("BoilerSurgeTrigger", new Vector2(5000f, 950f),
                () => RaiseHullFlood(2), new Vector2(70f, LevelHeight));
        }

        private void BuildRoom3BoatDeck() {
            BuildRoomBackground("BG_Room3", Room3StartX, ArenaStartX - Room3StartX, LevelHeight,
                new Color(0.08f, 0.1f, 0.14f, 0.4f));

            BuildFloor(6300f, DeckE2, 1000f);
            BuildListingRamp(7400f, 870f, 500f, -16f);
            BuildFloor(7500f, DeckBoat, 1300f);

            // Davit climb: the only dry ground once the boat deck itself goes under.
            BuildOneWayPlatform(6800f, 800f, 220f);
            BuildOneWayPlatform(7300f, 700f, 200f);
            BuildOneWayPlatform(7900f, 660f, 220f);
            BuildOneWayPlatform(8300f, 600f, 200f);
            BuildOneWayPlatform(8600f, 660f, 240f);

            BuildEraserDebut();
            BuildCheckpoint(8650f, DeckBoat - 10f, Checkpoint2, CheckpointRole.PreBoss);
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

            BuildRoomTransition("titanic_stern_rail", new Vector2(ArenaStartX, LevelHeight / 2f),
                new Rect2(ArenaStartX - 40f, 0f, LevelWidth - ArenaStartX + 40f, LevelHeight),
                new Vector2(80f, LevelHeight));

            BuildWaveTrigger("PreBossTrigger", new Vector2(8720f, 700f),
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

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void EnterRoom2() {
            RaiseHullFlood(1);
            SpawnWave(2);
        }

        private void EnterRoom3() {
            RaiseHullFlood(3);
            SetObjective("titanic_objective_boat_deck");
            SpawnWave(3);
            StartBoatDeckFlooding();
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
        /// anchor (6100) and well before PreBoss (8650).
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
                SpawnOffset = EraserDebutSpawnOffset
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
            // Package 12 W2 (GAP-13): the cleared waves come from the explicit
            // encounter baseline below; this keeps only the flood state.
            RestoreFloodForCheckpoint(checkpointID);
            // Package 13 W2: before PreBoss the debut is rebuilt and stays
            // triggerable; the PreBoss baseline declares it fought, with its
            // reward still claimed.
            EraserDebut?.RestoreFromCheckpoint(_eraserDebutCleared, rewardAlreadyClaimed: _eraserDebutCleared);
        }

        // Package 12 W2 (GAP-13): explicit encounter baseline — waves 1+2 at the
        // middle anchor, 1+2+3 at the PreBoss anchor, authored rather than derived.
        // Package 13 W2 (S27): the PreBoss anchor also restores the Eraser debut
        // as fought; the middle anchor precedes it, so it stays live there.
        private static readonly IReadOnlyDictionary<string, string[]> Baselines = BuildBaselines();

        private static IReadOnlyDictionary<string, string[]> BuildBaselines() {
            var map = new Dictionary<string, string[]>(
                NumberedWaveBaselines(TitanicLevelID, new[] { 1, 2 }, new[] { 1, 2, 3 }));
            var preBoss = new List<string>(map[Checkpoint2]) { EraserDebutTriggerID };
            map[Checkpoint2] = preBoss.ToArray();
            return map;
        }

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            if (encounterID == EraserDebutTriggerID) {
                _eraserDebutCleared = true;
                return;
            }
            if (TryParseWaveEncounter(TitanicLevelID, encounterID, out int wave)) _wavesSpawned.Add(wave);
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

        public bool HasWaveSpawned(int wave) => _wavesSpawned.Contains(wave);

        private void SpawnWave(int wave) {
            if (!_wavesSpawned.Add(wave)) return;

            var authored = new List<(string EnemyID, Vector2 Position)>();
            foreach ((string enemyID, int entryWave, Vector2 position) in SpawnTable) {
                if (entryWave == wave) authored.Add((enemyID, position));
            }
            if (authored.Count == 0) return;

            int count = StoryDifficultyTuning.ScaleEncounterCount(
                authored.Count, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < authored.Count; index++) {
                EnemyFactory.Spawn(authored[index].EnemyID, this, authored[index].Position);
            }
        }
    }
}
