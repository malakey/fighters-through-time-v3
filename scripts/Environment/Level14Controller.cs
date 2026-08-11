using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// One authored laser security grid: a run of <see cref="StoryCyclicHazard"/>
    /// beams sharing a single cycle length, each armed with its own phase offset so
    /// the safe windows travel along the corridor as a readable wave.
    ///
    /// The offsets are not decoration. Beam <c>i</c> is offset by
    /// <c>i * (beamSpacing / ReferenceWalkSpeed)</c>, so a player walking east at
    /// the reference speed meets every beam at the *same* point in its cycle. Enter
    /// on the beat and the whole corridor is one continuous walk; enter off the beat
    /// and every beam is wrong. That is what makes a grid learnable rather than a
    /// dice roll, and <see cref="Level14Controller.FindSafeWalkEntryTime"/> proves a
    /// solution exists for the slowest character in the roster.
    /// </summary>
    public sealed class LaserGridPattern {
        public string GridID = "";
        /// <summary>Telegraph. The beam is harmless for this long before it fires.</summary>
        public float WarningSeconds;
        /// <summary>The only phase that damages.</summary>
        public float ActiveSeconds;
        public float CooldownSeconds;
        /// <summary>Node name in the scene, world X, and the phase delay the level arms it with.</summary>
        public (string BeamName, float X, float PhaseOffsetSeconds)[] Beams =
            Array.Empty<(string, float, float)>();

        public float CycleSeconds => WarningSeconds + ActiveSeconds + CooldownSeconds;
        public int BeamCount => Beams.Length;
    }

    /// <summary>
    /// Level 14 - Neo-Earth, the Apex Archive core laboratory. The penultimate
    /// level, and the first time the campaign crosses out of history entirely: this
    /// is the cult's own home timeline, so there are no violated locals here and no
    /// era to restore. Only the fortress, and the fact that it was never built to be
    /// entered from the outside.
    ///
    /// <b>Era identity, both halves mechanically real:</b>
    /// <list type="bullet">
    /// <item><b>Laser security grids.</b> Four <see cref="LaserGridPattern"/>s of
    /// phase-offset <see cref="StoryCyclicHazard"/> beams. Each grid is a timed
    /// corridor with a travelling safe window rather than a scatter of hazards, and
    /// the three corridor grids escalate as the player goes deeper: 3 beams on a
    /// 4.2 s cycle, then 4 on 3.6 s, then 5 on 3.0 s.</item>
    /// <item><b>Anti-gravity containment pockets.</b> Three
    /// <see cref="GravityFieldZone"/>s used as *localized* shafts rather than Level
    /// 12's level-wide field. Each pocket's rungs are sized so the heaviest
    /// character clears them at that pocket's scale and the most mobile character
    /// cannot clear them at Earth-normal gravity, and each shaft's total climb
    /// exceeds every character's entire multi-jump budget - so the field is the
    /// route, not a shortcut. The pockets are authored strictly non-overlapping
    /// because <see cref="EnvironmentPlayerModifiers"/> publishes the PRODUCT of
    /// every live source (Levels 5, 8 and 12 all hit that).</item>
    /// </list>
    ///
    /// <b>The arena is the boss.</b> Archive Prime is the security core itself, so
    /// its own laser grid answers to its health: dark through phase 1, armed on a
    /// slow cycle at phase 2, tightened at phase 3, and shut down the moment it
    /// dies. Every tuning still has to pass the survivable-walk proof.
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: 14 standards, 2 elites,
    /// 1 boss, 3 extractors, cultist roster only.
    /// </summary>
    public partial class Level14Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string NeoEarthLevelID = "level_14_neo_earth";

        public override string LevelID => NeoEarthLevelID;
        public override CampaignLevel Level => CampaignLevel.NeoEarth;
        public override string LevelTitleKey => "neo_earth_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_14_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(200f, DeckY - 60f);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "neo_earth_objective_breach";
        protected override string BossObjectiveKey => "neo_earth_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "neo_earth_objective_complete";

        // === Dimensions ===

        public const float LevelWidth = 11800f;
        /// <summary>The three containment shafts climb nearly the whole height.</summary>
        public const float LevelHeight = 1800f;

        /// <summary>The main lab deck. Continuous end to end - the Archive has no pits.</summary>
        public const float DeckY = 1700f;
        /// <summary>The bulkhead crossing altitude, above the containment wing.</summary>
        public const float HighDeckY = 700f;

        public const float Room2StartX = 2900f;
        public const float Room3StartX = 6500f;
        /// <summary>West edge of the Security Core; the arena lock sits here.</summary>
        public const float ArenaStartX = 9300f;
        /// <summary>East wall of the Security Core; the arena's usable floor ends here.</summary>
        public const float ArenaEndX = 11740f;

        /// <summary>Sealed blast bulkhead: solid from the high deck down to the lab deck.</summary>
        public const float BulkheadX = 5780f;

        // === Checkpoints ===

        public const string Checkpoint0 = NeoEarthLevelID + "_checkpoint_0";
        public const string Checkpoint1 = NeoEarthLevelID + "_checkpoint_1";
        public const string Checkpoint2 = NeoEarthLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        private static readonly Vector2 Checkpoint0Position = new(260f, DeckY - 10f);
        /// <summary>Past the bulkhead and past the cargo lift, so a resume is never sealed out.</summary>
        private static readonly Vector2 Checkpoint1Position = new(6350f, DeckY - 10f);
        /// <summary>Pre-boss anchor, west of the Security Core seam and clear of every beam.</summary>
        private static readonly Vector2 Checkpoint2Position = new(9180f, DeckY - 10f);

        // === Anti-gravity containment pockets ===

        /// <summary>
        /// Authored pockets. Spans are strictly disjoint in X: two overlapping fields
        /// would multiply through <see cref="EnvironmentPlayerModifiers"/> into a
        /// scale nobody authored.
        ///
        /// The three scales were 0.34 / 0.30 / 0.26 and were scaled x0.8 by the
        /// 2026-08-10 movement retune, which cut jump height about 20%. Every shaft rung
        /// below is sized against its own pocket's reach, so the field carries the
        /// compensation and the geometry stays put; the deepening 17:15:13 ratio between
        /// the three pockets is preserved exactly (gameplay-feel plan section 9, B2).
        /// </summary>
        public static readonly (string ID, Rect2 Area, float Scale, string LabelKey)[] ContainmentPockets = {
            ("level_14.containment_alpha", new Rect2(3250f, 720f, 630f, LevelHeight - 720f), 0.272f,
                "neo_earth_pocket_alpha"),
            ("level_14.containment_beta", new Rect2(5120f, 640f, 620f, LevelHeight - 640f), 0.24f,
                "neo_earth_pocket_beta"),
            ("level_14.containment_gamma", new Rect2(8400f, 540f, 610f, LevelHeight - 540f), 0.208f,
                "neo_earth_pocket_gamma")
        };

        /// <summary>
        /// One-way rungs inside each pocket, ordered bottom to top. Every rung is
        /// authored so that the heaviest character clears it at that pocket's scale
        /// and the most mobile character cannot clear it at Earth-normal gravity.
        /// </summary>
        public static readonly (string PocketID, float FootY, (float X, float Y, float Width)[] Rungs)[] PocketShafts = {
            ("level_14.containment_alpha", DeckY, new[] {
                (3340f, 1400f, 220f), (3620f, 1100f, 220f), (3760f, 800f, 220f)
            }),
            ("level_14.containment_beta", DeckY, new[] {
                (5240f, 1370f, 220f), (5520f, 1040f, 220f), (5600f, 710f, 240f)
            }),
            ("level_14.containment_gamma", DeckY, new[] {
                (8530f, 1360f, 220f), (8790f, 1020f, 220f), (8630f, 680f, 220f)
            })
        };

        /// <summary>
        /// The Breach Gallery's portal scaffold. Deliberately NOT inside a pocket:
        /// its rungs are short enough for the heaviest character at Earth-normal
        /// gravity, so the level reads the difference between an ordinary climb and
        /// a containment shaft. There is no field here to absorb a movement retune, so
        /// the 2026-08-10 jump-height cut moved the step itself 100 -> 80 px, holding the
        /// same ~85% of the heaviest character's reach (gameplay-feel plan section 9, B2).
        /// </summary>
        public static readonly (float X, float Y, float Width)[] PortalScaffold = {
            (860f, 1620f, 200f), (1120f, 1540f, 200f), (860f, 1460f, 200f), (1120f, 1380f, 200f)
        };

        private readonly List<GravityFieldZone> _pockets = new();
        public IReadOnlyList<GravityFieldZone> Pockets => _pockets;

        // === Laser security grids ===

        /// <summary>Half-width of a CyclicHazardTemplate beam (its shape is 120 px wide).</summary>
        public const float BeamHalfWidth = 60f;
        /// <summary>Player pushbox half-width, added so the walk proof is conservative.</summary>
        public const float PlayerHalfWidth = 20f;
        public static float BeamContactHalfWidth => BeamHalfWidth + PlayerHalfWidth;

        /// <summary>
        /// Lincoln's ground speed (`MaxMoveSpeed` 4.75 x 60 px/s), the slowest in the
        /// roster. Every grid's phase offsets are derived from it, and the safe-walk
        /// proof runs at it - a faster character can always choose to walk slower,
        /// but nobody can choose to walk faster than their cap. The content test
        /// re-derives this from the nine CharacterData resources, so a roster retune
        /// that changes the slowest speed must move this constant with it (it went
        /// 330 -> 285 with the 2026-08-10 movement retune).
        /// </summary>
        public const float ReferenceWalkSpeed = 285f;

        /// <summary>Authored corridor stride between adjacent beams, in pixels.</summary>
        public const float BeamSpacing = 330f;

        /// <summary>
        /// Phase delay between adjacent beams: exactly the time the reference walker
        /// spends covering one <see cref="BeamSpacing"/>. Derived rather than authored,
        /// so the travelling safe window keeps pace with the slowest character through
        /// any future speed retune without the corridor geometry having to move.
        /// </summary>
        public static float BeamPhaseStrideSeconds => BeamSpacing / ReferenceWalkSpeed;

        private static float BeamPhase(int index) => index * BeamPhaseStrideSeconds;

        /// <summary>Every corridor beam must be readable before it fires.</summary>
        public const float MinTelegraphSeconds = 0.9f;

        public static readonly LaserGridPattern PerimeterGrid = new() {
            GridID = "level_14.perimeter_grid",
            WarningSeconds = 1.4f, ActiveSeconds = 0.9f, CooldownSeconds = 1.9f,
            Beams = new[] {
                ("PerimeterBeam1", 1500f, BeamPhase(0)),
                ("PerimeterBeam2", 1830f, BeamPhase(1)),
                ("PerimeterBeam3", 2160f, BeamPhase(2))
            }
        };

        public static readonly LaserGridPattern ContainmentGrid = new() {
            GridID = "level_14.containment_grid",
            WarningSeconds = 1.2f, ActiveSeconds = 0.9f, CooldownSeconds = 1.5f,
            Beams = new[] {
                ("ContainmentBeam1", 3980f, BeamPhase(0)),
                ("ContainmentBeam2", 4310f, BeamPhase(1)),
                ("ContainmentBeam3", 4640f, BeamPhase(2)),
                ("ContainmentBeam4", 4970f, BeamPhase(3))
            }
        };

        public static readonly LaserGridPattern CoreGrid = new() {
            GridID = "level_14.core_grid",
            WarningSeconds = 1.0f, ActiveSeconds = 0.9f, CooldownSeconds = 1.1f,
            Beams = new[] {
                ("CoreBeam1", 6900f, BeamPhase(0)),
                ("CoreBeam2", 7230f, BeamPhase(1)),
                ("CoreBeam3", 7560f, BeamPhase(2)),
                ("CoreBeam4", 7890f, BeamPhase(3)),
                ("CoreBeam5", 8220f, BeamPhase(4))
            }
        };

        /// <summary>Deepening corridors: more beams, less slack, the further in you go.</summary>
        public static readonly LaserGridPattern[] CorridorGrids = { PerimeterGrid, ContainmentGrid, CoreGrid };

        /// <summary>
        /// The arena lanes stand two corridor strides apart, so their phase offsets are
        /// two <see cref="BeamPhaseStrideSeconds"/> apart and the same travelling wave
        /// reads at the same walking speed.
        /// </summary>
        private static readonly (string BeamName, float X, float PhaseOffsetSeconds)[] ArenaBeamLayout = {
            ("SecurityCoreBeam1", 9600f, BeamPhase(0)),
            ("SecurityCoreBeam2", 10260f, BeamPhase(2)),
            ("SecurityCoreBeam3", 10920f, BeamPhase(4)),
            ("SecurityCoreBeam4", 11580f, BeamPhase(6))
        };

        /// <summary>Armed when Archive Prime drops into phase 2. Slow, generous, readable.</summary>
        public static readonly LaserGridPattern ArenaGridPhase2 = new() {
            GridID = "level_14.security_core_grid",
            WarningSeconds = 1.4f, ActiveSeconds = 0.9f, CooldownSeconds = 3.3f,
            Beams = ArenaBeamLayout
        };

        /// <summary>Phase 3: the core stops pacing itself. Same lanes, half the slack.</summary>
        public static readonly LaserGridPattern ArenaGridPhase3 = new() {
            GridID = "level_14.security_core_grid",
            WarningSeconds = 1.0f, ActiveSeconds = 0.9f, CooldownSeconds = 2.1f,
            Beams = ArenaBeamLayout
        };

        private readonly Dictionary<string, List<StoryCyclicHazard>> _grids = new();

        /// <summary>0 = dark (phase 1), 2 = the phase-2 tuning, 3 = the phase-3 tuning.</summary>
        public int ArenaGridArmedPhase { get; private set; }

        // === Encounters (locked: 14 standards / 2 elites / 1 boss / 3 extractors) ===

        /// <summary>Future Cultist standard. Act III has no brainwashed locals to press into service.</summary>
        public const string SlasherEnemyID = "chrono_slasher";
        /// <summary>Future Cultist standard, flying - the laboratory's own security drones.</summary>
        public const string DroneEnemyID = "hologram_drone";
        /// <summary>Future Cultist elite.</summary>
        public const string EliteEnemyID = "tech_enforcer";
        public const string BossResourcePath = "res://resources/Bosses/archive_prime.tres";

        private const float StandY = DeckY - 50f;

        /// <summary>
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves
        /// 2-4 arm on their room seams. Difficulty scaling takes a prefix of each
        /// wave through <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>.
        /// Archive Prime's own drone deployments are boss-driven and deliberately
        /// outside this budget.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - Breach Gallery. The perimeter watch, before the first grid.
            (SlasherEnemyID, 1, new Vector2(1300f, StandY)),
            (DroneEnemyID, 1, new Vector2(1750f, 1300f)),
            (SlasherEnemyID, 1, new Vector2(2450f, StandY)),
            // Wave 2 - Containment Wing. Drones hold the shaft mouths.
            (SlasherEnemyID, 2, new Vector2(3050f, StandY)),
            (DroneEnemyID, 2, new Vector2(3560f, 1230f)),
            (DroneEnemyID, 2, new Vector2(4460f, 1250f)),
            (EliteEnemyID, 2, new Vector2(5060f, StandY)),
            (SlasherEnemyID, 2, new Vector2(5340f, StandY)),
            // Wave 3 - Fabrication Spine, behind the tightest corridor grid.
            (SlasherEnemyID, 3, new Vector2(6750f, StandY)),
            (DroneEnemyID, 3, new Vector2(7100f, 1280f)),
            (SlasherEnemyID, 3, new Vector2(7700f, StandY)),
            (DroneEnemyID, 3, new Vector2(8150f, 1220f)),
            (EliteEnemyID, 3, new Vector2(8900f, StandY)),
            // Wave 4 - the core garrison, fought before Archive Prime reveals.
            (SlasherEnemyID, 4, new Vector2(9550f, StandY)),
            (DroneEnemyID, 4, new Vector2(9950f, 1350f)),
            (DroneEnemyID, 4, new Vector2(10450f, 1350f))
        };

        /// <summary>Three extractors, each up a real climb. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_14_extractor_0", new Vector2(1120f, 1230f)),
            ("level_14_extractor_1", new Vector2(4070f, 730f)),
            ("level_14_extractor_2", new Vector2(8990f, 610f))
        };

        public static int StandardEnemyCount => CountOf(SlasherEnemyID) + CountOf(DroneEnemyID);
        public static int EliteEnemyCount => CountOf(EliteEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) if (id == enemyID) total++;
            return total;
        }

        // === Runtime state ===

        private readonly HashSet<int> _wavesSpawned = new();
        private bool _eventsBound;

        public bool HasWaveSpawned(int wave) => _wavesSpawned.Contains(wave);

        // === Era palette: metallic and cyberpunk, deliberately not another era ===

        protected override Color FloorColor => new(0.15f, 0.16f, 0.2f);
        protected override Color FloorEdgeColor => new(0.24f, 0.86f, 0.94f);
        protected override Color PlatformColor => new(0.22f, 0.25f, 0.32f);
        protected override Color WallColor => new(0.1f, 0.11f, 0.15f);
        protected override Color HazardColor => new(0.95f, 0.2f, 0.35f);

        private static readonly Color BulkheadColor = new(0.16f, 0.18f, 0.24f);
        private static readonly Color CoreFloorColor = new(0.18f, 0.14f, 0.24f);
        private static readonly Color PocketTint = new(0.4f, 0.95f, 1f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1BreachGallery();
            BuildRoom2ContainmentWing();
            BuildRoom3FabricationSpine();
            BuildRoom4SecurityCore();
            BuildContainmentPockets();
            BuildExtractors(ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(ArenaEndX, 0f, LevelHeight);
        }

        private void BuildRungs((float X, float Y, float Width)[] rungs) {
            foreach ((float x, float y, float width) in rungs) BuildOneWayPlatform(x, y, width);
        }

        private void BuildRoom1BreachGallery() {
            BuildRoomBackground("BG_BreachGallery", 0f, Room2StartX, LevelHeight,
                new Color(0.03f, 0.05f, 0.09f, 0.6f));
            BuildFloor(0f, DeckY, Room2StartX);

            // The rift the player arrives through, and the optional scaffold beside
            // it. Ordinary rungs: no containment field, so they are cut short enough
            // for the heaviest character at Earth-normal gravity.
            BuildRungs(PortalScaffold);

            BuildCheckpoint(Checkpoint0Position.X, Checkpoint0Position.Y, Checkpoint0);
            BuildRoomDecoration(0f, "neo_earth_room_breach_gallery", new Color(0.4f, 0.9f, 1f));
        }

        private void BuildRoom2ContainmentWing() {
            float width = Room3StartX - Room2StartX;
            BuildRoomBackground("BG_ContainmentWing", Room2StartX, width, LevelHeight,
                new Color(0.04f, 0.05f, 0.11f, 0.6f));
            BuildFloor(Room2StartX, DeckY, width);

            BuildRungs(ShaftRungs("level_14.containment_alpha"));
            // Service gantry at the head of the alpha shaft; extractor 1 sits on it.
            BuildOneWayPlatform(4070f, 800f, 400f);

            BuildRungs(ShaftRungs("level_14.containment_beta"));

            // The bulkhead crossing. The wall runs from the high deck all the way
            // down to the lab deck, so the wing has exactly one exit and it is over
            // the top - which means it is through the beta containment pocket.
            BuildFloor(5720f, HighDeckY, 260f, BulkheadColor);
            BuildWall(BulkheadX, HighDeckY, DeckY - HighDeckY, BulkheadColor, 24f);

            BuildCheckpoint(Checkpoint1Position.X, Checkpoint1Position.Y, Checkpoint1);
            BuildRoomDecoration(Room2StartX, "neo_earth_room_containment_wing", new Color(0.7f, 0.55f, 1f));

            BuildRoomTransitionPair(
                Room2StartX, "neo_earth_containment_wing", "neo_earth_breach_gallery",
                new Rect2(Room2StartX - 100f, 0f, 3900f, LevelHeight),
                new Rect2(0f, 0f, 3000f, LevelHeight),
                _ => EnterContainmentWing());
        }

        private void BuildRoom3FabricationSpine() {
            float width = ArenaStartX - Room3StartX;
            BuildRoomBackground("BG_FabricationSpine", Room3StartX, width, LevelHeight,
                new Color(0.05f, 0.05f, 0.1f, 0.6f));
            BuildFloor(Room3StartX, DeckY, width);

            BuildRungs(ShaftRungs("level_14.containment_gamma"));
            // Ceiling catwalk at the head of the gamma shaft; extractor 2 sits on it.
            BuildOneWayPlatform(8940f, 680f, 400f);

            BuildCheckpoint(Checkpoint2Position.X, Checkpoint2Position.Y, Checkpoint2);
            BuildRoomDecoration(Room3StartX, "neo_earth_room_core_approach", new Color(0.95f, 0.6f, 0.35f));

            BuildRoomTransitionPair(
                Room3StartX, "neo_earth_core_approach", "neo_earth_containment_wing_return",
                new Rect2(Room3StartX - 100f, 0f, 2900f, LevelHeight),
                new Rect2(Room2StartX - 100f, 0f, 3900f, LevelHeight),
                _ => EnterFabricationSpine());
        }

        private void BuildRoom4SecurityCore() {
            float width = LevelWidth - ArenaStartX;
            BuildRoomBackground("BG_SecurityCore", ArenaStartX, width, LevelHeight,
                new Color(0.1f, 0.04f, 0.12f, 0.65f));

            // One unbroken core floor. Archive Prime is knockback-immune and its
            // laser grid already owns the arena's tempo; a hole in the floor on top
            // of that would be punishment without a read.
            BuildFloor(ArenaStartX, DeckY, ArenaEndX - ArenaStartX, CoreFloorColor);
            BuildPlatform(10100f, DeckY - 170f, 300f, CoreFloorColor);
            BuildPlatform(11100f, DeckY - 170f, 300f, CoreFloorColor);

            BuildRoomDecoration(ArenaStartX, "neo_earth_room_security_core", new Color(1f, 0.4f, 0.45f));

            // One-shot: this clamp is the arena lock.
            BuildRoomTransition("neo_earth_security_core", new Vector2(ArenaStartX + 60f, LevelHeight / 2f),
                new Rect2(ArenaStartX - 40f, 0f, LevelWidth - ArenaStartX + 40f, LevelHeight),
                new Vector2(80f, LevelHeight),
                _ => EnterSecurityCore());

            // The core floor spans 2,440 px against Archive Prime's 10.0-unit ranged
            // band (600 px at BossController's 60 px per unit); the content test
            // asserts the fit against the resource, never against a literal.
            BuildBossEncounter(BossResourcePath, new Vector2(10900f, DeckY - 50f),
                "ArchivePrimeEncounter", revealDistance: 700f);
        }

        /// <summary>Authored rungs for one containment shaft.</summary>
        public static (float X, float Y, float Width)[] ShaftRungs(string pocketID) {
            foreach ((string id, float _, (float X, float Y, float Width)[] rungs) in PocketShafts) {
                if (id == pocketID) return rungs;
            }
            return Array.Empty<(float, float, float)>();
        }

        /// <summary>
        /// Bidirectional confinement (the Level 3 / Level 12 pattern): a single
        /// forward trigger leaves the camera clamped to the last room forever once
        /// the player backtracks for an extractor, and two of the three extractors
        /// are behind optional climbs the player will want to come back for.
        /// </summary>
        private void BuildRoomTransitionPair(
            float boundaryX,
            string forwardRoomID,
            string backRoomID,
            Rect2 forwardBounds,
            Rect2 backBounds,
            Action<string> onForwardEntered = null) {
            RoomTransitionTrigger forward = BuildRoomTransition(
                forwardRoomID, new Vector2(boundaryX + 80f, LevelHeight / 2f), forwardBounds,
                new Vector2(80f, LevelHeight), onForwardEntered);
            forward.ActivateOnce = false;

            RoomTransitionTrigger back = BuildRoomTransition(
                backRoomID, new Vector2(boundaryX - 80f, LevelHeight / 2f), backBounds,
                new Vector2(80f, LevelHeight));
            back.ActivateOnce = false;
        }

        /// <summary>
        /// Code-built rather than instanced from GravityFieldZoneTemplate.tscn: the
        /// template ships a fixed 960x720 shape and all three pockets are different
        /// sizes, and overriding a SubResource shape on an instanced scene risks
        /// mutating a shape shared between the instantiations (the Level 5 flood-zone
        /// precedent). Graybox-in-code is the sanctioned Florence convention.
        /// </summary>
        private void BuildContainmentPockets() {
            foreach ((string id, Rect2 area, float scale, string labelKey) in ContainmentPockets) {
                _pockets.Add(BuildContainmentPocket(id, area, scale, labelKey));
            }
        }

        private GravityFieldZone BuildContainmentPocket(string pocketID, Rect2 area, float scale, string labelKey) {
            var field = new GravityFieldZone {
                Name = $"Pocket_{pocketID.Replace('.', '_')}",
                FieldID = pocketID,
                // Static pocket: CycleScales stays empty, so GravityScale applies.
                GravityScale = scale,
                IdleTint = new Color(PocketTint.R, PocketTint.G, PocketTint.B, 0.18f),
                Position = area.Position + area.Size / 2f,
                RewindPolicy = StoryRewindPolicy.ResetToInitialState
            };

            // Children first: GravityFieldZone resolves FieldVisualPath in _Ready,
            // which fires the moment the zone is added to the tree.
            field.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = area.Size }
            });
            field.AddChild(new ColorRect {
                Name = "Visual",
                Size = area.Size,
                Position = -area.Size / 2f,
                Color = new Color(1f, 1f, 1f, 0.5f),
                ZIndex = -5,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            var label = new Label {
                Name = "PocketLabel",
                Text = Tr(labelKey),
                Position = new Vector2(-area.Size.X / 2f, -area.Size.Y / 2f - 26f),
                CustomMinimumSize = new Vector2(area.Size.X, 22),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", PocketTint);
            field.AddChild(label);

            AddChild(field);
            return field;
        }

        // === Flow ===

        protected override void OnLevelReady() {
            CollectLaserGrids();
            ArmCorridorGrids();
            DisarmArenaGrid();
            // Area2D reports its authored initial overlaps on the first physics
            // frame, and a checkpoint resume teleports the player before one ever
            // runs - so a resumed player standing in a containment pocket would be
            // at Earth-normal gravity and unable to climb out of it. Registering
            // explicitly makes the pocket state a function of where the player
            // actually is, on entry and on every rewind (the Level 12 precedent).
            SyncContainmentPocketToPlayer();

            if (EventBus.Instance != null && !_eventsBound) {
                _eventsBound = true;
                EventBus.Instance.OnBossPhaseChanged += OnBossPhaseChanged;
                EventBus.Instance.OnRewindTriggered += OnStoryRewind;
            }
        }

        public override void _ExitTree() {
            if (_eventsBound && EventBus.Instance != null) {
                EventBus.Instance.OnBossPhaseChanged -= OnBossPhaseChanged;
                EventBus.Instance.OnRewindTriggered -= OnStoryRewind;
            }
            _eventsBound = false;
            base._ExitTree();
        }

        /// <summary>
        /// A Chronal Rewind teleports the player and resets every hazard, so both
        /// era mechanics have to be re-established in one place: the grids go back to
        /// their authored phase offsets (a StoryCyclicHazard reset on its own would
        /// collapse every beam in a grid onto the same phase and destroy the very
        /// pattern the player is learning), and the player is re-registered into
        /// exactly the pocket they were put back into.
        /// </summary>
        private void OnStoryRewind(Vector2 targetPosition) {
            ArmCorridorGrids();
            if (ArenaGridArmedPhase > 0) {
                ArmGrid(ArenaGridArmedPhase >= 3 ? ArenaGridPhase3 : ArenaGridPhase2);
            }
            SyncContainmentPocketToPlayer();
        }

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void EnterContainmentWing() {
            SetObjective("neo_earth_objective_containment");
            SpawnWave(2);
        }

        private void EnterFabricationSpine() {
            SetObjective("neo_earth_objective_core_approach");
            SpawnWave(3);
        }

        private void EnterSecurityCore() {
            SetObjective("neo_earth_objective_security_core");
            SpawnWave(4);
        }

        // === Laser grids ===

        private void CollectLaserGrids() {
            _grids.Clear();
            foreach (LaserGridPattern pattern in CorridorGrids) CollectGrid(pattern);
            CollectGrid(ArenaGridPhase2);
        }

        private void CollectGrid(LaserGridPattern pattern) {
            if (_grids.ContainsKey(pattern.GridID)) return;
            var beams = new List<StoryCyclicHazard>();
            foreach ((string beamName, float _, float _) in pattern.Beams) {
                if (GetNodeOrNull<StoryCyclicHazard>(beamName) is StoryCyclicHazard beam) {
                    // A grid beam must never fall back to RestoreCheckpointState:
                    // a rewind before any checkpoint capture would hand every beam
                    // the same phase and flatten the wave.
                    beam.RewindPolicy = StoryRewindPolicy.ResetToInitialState;
                    beams.Add(beam);
                } else {
                    GD.PushWarning($"Level 14: laser beam '{beamName}' is missing from the scene.");
                }
            }
            _grids[pattern.GridID] = beams;
        }

        /// <summary>Live beams of one authored grid, in authored order.</summary>
        public IReadOnlyList<StoryCyclicHazard> BeamsOf(string gridID) =>
            _grids.TryGetValue(gridID, out List<StoryCyclicHazard> beams)
                ? beams
                : Array.Empty<StoryCyclicHazard>();

        private void ArmCorridorGrids() {
            foreach (LaserGridPattern pattern in CorridorGrids) ArmGrid(pattern);
        }

        /// <summary>
        /// Applies one pattern's durations and phase offsets to its live beams.
        /// <see cref="StoryCyclicHazard"/> has no phase-offset export, so the offset
        /// is expressed as the length of the beam's very first cooldown; every cycle
        /// after that runs at the authored length and the relative phases hold.
        /// </summary>
        private void ArmGrid(LaserGridPattern pattern) {
            IReadOnlyList<StoryCyclicHazard> beams = BeamsOf(pattern.GridID);
            for (int index = 0; index < beams.Count && index < pattern.Beams.Length; index++) {
                StoryCyclicHazard beam = beams[index];
                if (!IsInstanceValid(beam)) continue;
                beam.WarningDuration = pattern.WarningSeconds;
                beam.ActiveDuration = pattern.ActiveSeconds;
                beam.CooldownDuration = pattern.CooldownSeconds;
                beam.Enabled = true;
                beam.ForcePhase(HazardPhase.Cooldown, pattern.Beams[index].PhaseOffsetSeconds);
            }
        }

        /// <summary>Shuts the Security Core grid down: phase 1 state, and what boss defeat restores.</summary>
        public void DisarmArenaGrid() {
            ArenaGridArmedPhase = 0;
            foreach (StoryCyclicHazard beam in BeamsOf(ArenaGridPhase2.GridID)) {
                if (!IsInstanceValid(beam)) continue;
                beam.Enabled = false;
                beam.ForcePhase(HazardPhase.Cooldown, ArenaGridPhase2.CooldownSeconds);
            }
        }

        /// <summary>
        /// Archive Prime <i>is</i> the security core, so the arena answers to its
        /// health rather than sitting beside it. Phase 1 the grid is dark and the
        /// fight is a clean duel; phase 2 arms it on the slow tuning; phase 3
        /// tightens the cycle. Every tuning still leaves a walkable lane - the
        /// content test proves it the same way it proves the corridors.
        /// </summary>
        private void OnBossPhaseChanged(int phaseIndex) {
            if (IsBossDefeated) return;
            // BossController.CurrentPhase is 0-based, so 1 is the second phase.
            if (phaseIndex >= 2) ArmArenaGrid(3);
            else if (phaseIndex == 1) ArmArenaGrid(2);
        }

        public void ArmArenaGrid(int phase) {
            if (phase <= ArenaGridArmedPhase) return;
            ArenaGridArmedPhase = phase;
            ArmGrid(phase >= 3 ? ArenaGridPhase3 : ArenaGridPhase2);
        }

        protected override void OnBossDefeated(BossEncounterController encounter, BossDefeatedPayload payload) {
            // The grid is the core's own reflex; with the core dead it goes dark
            // rather than continuing to punish the exit dialogue.
            base.OnBossDefeated(encounter, payload);
            DisarmArenaGrid();
        }

        // === Laser grid solvability (pure; shared with the content test) ===

        /// <summary>True when beam <paramref name="beamIndex"/> is in its damaging phase at <paramref name="time"/>.</summary>
        public static bool BeamIsActiveAt(LaserGridPattern pattern, int beamIndex, float time) {
            float offset = pattern.Beams[beamIndex].PhaseOffsetSeconds;
            if (time < offset) return false;
            float phase = (time - offset) % pattern.CycleSeconds;
            return phase >= pattern.WarningSeconds && phase < pattern.WarningSeconds + pattern.ActiveSeconds;
        }

        /// <summary>
        /// Earliest steady-state entry time for which a constant-speed eastbound walk
        /// crosses every beam of <paramref name="pattern"/> without ever standing in
        /// an active one, or -1 when the grid has no solution at that speed.
        ///
        /// This is the level's fairness contract in one function: a grid that cannot
        /// be walked is not a puzzle, it is a damage tax.
        /// </summary>
        public static float FindSafeWalkEntryTime(LaserGridPattern pattern, float walkSpeed) {
            if (pattern.BeamCount == 0 || walkSpeed <= 0f) return -1f;

            float approachX = pattern.Beams[0].X - 400f;
            float latestOffset = 0f;
            foreach ((string _, float _, float offset) in pattern.Beams) {
                latestOffset = Mathf.Max(latestOffset, offset);
            }

            // Search a full cycle of the steady state, so the answer describes the
            // pattern the player actually learns rather than its first-cycle warm-up.
            float searchStart = latestOffset + pattern.CycleSeconds;
            const float step = 1f / 240f;
            for (float entry = searchStart; entry <= searchStart + pattern.CycleSeconds; entry += step) {
                if (WalkClearsEveryBeam(pattern, entry, approachX, walkSpeed)) return entry;
            }
            return -1f;
        }

        private static bool WalkClearsEveryBeam(LaserGridPattern pattern, float entryTime, float approachX, float walkSpeed) {
            float half = BeamContactHalfWidth;
            for (int index = 0; index < pattern.Beams.Length; index++) {
                float beamX = pattern.Beams[index].X;
                float contactStart = entryTime + (beamX - half - approachX) / walkSpeed;
                float contactEnd = entryTime + (beamX + half - approachX) / walkSpeed;
                if (ContactOverlapsAnActiveWindow(pattern, index, contactStart, contactEnd)) return false;
            }
            return true;
        }

        private static bool ContactOverlapsAnActiveWindow(LaserGridPattern pattern, int beamIndex, float from, float to) {
            float offset = pattern.Beams[beamIndex].PhaseOffsetSeconds;
            float cycle = pattern.CycleSeconds;
            float firstActive = offset + pattern.WarningSeconds;
            int first = Mathf.Max(0, Mathf.FloorToInt((from - firstActive) / cycle) - 1);
            int last = Mathf.CeilToInt((to - firstActive) / cycle) + 1;
            for (int k = first; k <= last; k++) {
                float start = firstActive + k * cycle;
                float end = start + pattern.ActiveSeconds;
                if (to > start && from < end) return true;
            }
            return false;
        }

        // === Containment pockets ===

        /// <summary>Authored pocket whose area contains <paramref name="position"/>, or null.</summary>
        public GravityFieldZone PocketAt(Vector2 position) {
            for (int index = 0; index < ContainmentPockets.Length && index < _pockets.Count; index++) {
                if (!ContainmentPockets[index].Area.HasPoint(position)) continue;
                if (IsInstanceValid(_pockets[index])) return _pockets[index];
            }
            return null;
        }

        /// <summary>
        /// Puts the player in exactly the pocket they are standing in and out of
        /// every other one, through the shared
        /// <see cref="GravityFieldZone.SyncPlayerToField"/> helper. The target comes
        /// from this level's authored pocket table rather than the helper's geometric
        /// lookup, because <see cref="PocketAt"/> reads the authored spans (which the
        /// content test proves disjoint) rather than the built collision shapes — the
        /// same split Level 12 uses. Idempotent and consistent with the physics
        /// callbacks: a redundant Add or Remove is a no-op inside
        /// <see cref="GravityFieldZone"/>.
        /// </summary>
        public void SyncContainmentPocketToPlayer() {
            if (Player == null || !IsInstanceValid(Player)) return;
            GravityFieldZone.SyncPlayerToField(_pockets, Player, PocketAt(Player.Position));
        }

        // === Checkpoint resume ===

        protected override void MarkWavesClearedThrough(string checkpointID) {
            switch (checkpointID) {
                case Checkpoint1:
                    _wavesSpawned.Add(1);
                    _wavesSpawned.Add(2);
                    break;
                case Checkpoint2:
                    _wavesSpawned.Add(1);
                    _wavesSpawned.Add(2);
                    _wavesSpawned.Add(3);
                    break;
            }
        }

        // === Encounters ===

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
