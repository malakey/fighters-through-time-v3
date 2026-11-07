using Godot;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 4 - Paris, 1789 (The Storming of the Bastille). Four rooms across
    /// ~10,240 px:
    /// <list type="number">
    /// <item><b>Gatehouse Yard</b> (0-2060, then the Glacis to 3200) - the mob at the gates; opening wave.</item>
    /// <item><b>Security Corridors</b> (3200-6080) - three <see cref="SearchlightZone"/>
    ///       beams in <see cref="SearchlightMode.UltimateDrain"/> mode gate the
    ///       approach; the vertical tower climb ends on a swept upper walkway.</item>
    /// <item><b>Cell Block</b> (6080-8640) - two <see cref="RescuableNPC"/> prisoners
    ///       sealed behind <see cref="DestructibleBlock"/> lock mechanisms. Freeing
    ///       both satisfies the <see cref="PuzzleManager"/> and the freed prisoners
    ///       haul the inner courtyard gate open.</item>
    /// <item><b>Inner Courtyard</b> (8640-10240) - two drawbridge walkways over a
    ///       central lower pit, with a flat pit floor wide enough for the
    ///       Governor's Guard's summoned garrison to land and path.</item>
    /// </list>
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: exactly 10 standards,
    /// 0 elites, 1 boss, 3 extractors. Boss-summoned adds are boss-driven and
    /// deliberately sit outside that budget.
    ///
    /// <para><b>The boss is the Governor's Guard</b> (Package 13 W4, design §6/S23):
    /// Governor de Launay and his garrison captain, both Linked. The legacy
    /// identifiers — <c>revolutionary_tribunal.tres</c>, the <c>Tribunal*</c>
    /// members below, the <c>RevolutionaryTribunalEncounter</c> node name and every
    /// translation key — are retained (D6); only English values and prose moved.</para>
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L4): each
    /// approach room is an <see cref="EncounterLock"/> the hero must clear — the
    /// gatehouse yard (300–2000 since GR-LV2), the corridor floor under the two ground beams
    /// (3220–4760) and the cell block (6120–8100, which also needs both prisoners
    /// freed). The walkway rioter that used to hold the upper walkway, where the
    /// floor route walked under it, now stands on the corridor floor; the cell-B
    /// rioter stays sealed in with prisoner B and is fought once lock B breaks.
    /// The courtyard gate still opens the moment the prisoners are free; the
    /// cell block's barrier opens on "freed AND cleared". The tower and the swept
    /// walkway (4780–6080) hold no enemy, so the Middle anchor stands there, and
    /// the Restoration Font and the PreBoss anchor stand between the cell block's
    /// barrier and the courtyard.</para>
    ///
    /// <para><b>The Glacis and the Battlement line</b> (gameplay review, GR-LV2: rulings
    /// 7a, 7b, 7c, 7c+). The gatehouse lock was carved to 300-2000, and the span it left,
    /// 2020-3200, is a platforming stretch over a dry moat: the barricade, a crumbling step
    /// and a wall with a ledge. Above it the Battlement line climbs to the secret. Every hero
    /// reaches it on the base kit, and a burst or a rise kit cuts it short. The puzzle in the
    /// tower gap (4760-6120) and the Tribunal's pit are untouched. The recipe is
    /// <c>docs/handoffs/GR_PILOT.md</c>.</para>
    /// </summary>
    public partial class Level04Controller : StoryLevelControllerBase {

        // === Layout constants ===

        public const float LevelWidth = 10240f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 3200f;
        private const float Room3StartX = 6080f;
        private const float Room4StartX = 8640f;

        /// <summary>Boss-arena pit floor; 100 px below the approach so adds stay reachable.</summary>
        private const float PitFloorY = 1000f;

        /// <summary>The Bastille tower's top drop-through step (centre), beside the upper walkway's west end.</summary>
        public const float TowerTopStepX = 4880f;
        public const float TowerTopStepY = 380f;

        /// <summary>The inner-courtyard gate the prisoner puzzle opens (centre X, size, fill).</summary>
        public const float CourtyardGateX = 8560f;
        private const float CourtyardGateWidth = 48f;
        private const float CourtyardGateHeight = 760f;
        private static readonly Color CourtyardGateColor = new(0.38f, 0.30f, 0.22f);

        private const string PrisonerPuzzleID = "level_04.free_prisoners";
        private const string PrisonerAConditionID = "prisoner_a_freed";
        private const string PrisonerBConditionID = "prisoner_b_freed";
        private const string BossResourcePath = "res://resources/Bosses/revolutionary_tribunal.tres";

        /// <summary>
        /// P1 (2026-10-04): the inner courtyard. The old 800 px reveal line (9400 -
        /// 800 = 8600) already sat at the courtyard entry, so the arena is the whole
        /// courtyard: entry 8640 to the east wall's inner face. Both Tribunal
        /// members share it.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(Room4StartX, 0f, 10220f - Room4StartX, LevelHeight);

        // === Authored encounter table (asserted by Level04ContentTests) ===

        /// <summary>Gatehouse yard: the crowd already fighting when the player arrives.</summary>
        public static readonly (string EnemyID, Vector2 Position)[] Room1Spawns = {
            ("chrono_rioter", new Vector2(1100, 850)),
            ("chrono_slasher", new Vector2(1900, 850)),
            // GR-LV2: 2700 -> 1450, inside the carved lock.
            ("chrono_rioter", new Vector2(1450, 850))
        };

        /// <summary>
        /// Security corridors: three on the floor between the two ground beams.
        /// GU-L4 (2026-10-13): the third rioter used to hold the upper walkway
        /// (5600, 250), 600 px above a floor route that walked straight under it
        /// and where it never came down; it now stands on the corridor floor
        /// inside the corridor lock.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] Room2Spawns = {
            ("chrono_rioter", new Vector2(3900, 850)),
            ("chrono_slasher", new Vector2(4600, 850)),
            ("chrono_rioter", new Vector2(4250, 850))
        };

        /// <summary>
        /// Cell block: the heaviest concurrent group, still inside the warm-12 cap.
        /// The rioter at 7900 stands inside cell B with prisoner B (part of the
        /// puzzle: it is fought once lock B breaks). GU-L4 (2026-10-13): the last
        /// slasher moved from 8350 (beside the PreBoss anchor, past the cell
        /// block's barrier) to 7400, between the two cells.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] Room3Spawns = {
            ("chrono_rioter", new Vector2(6400, 850)),
            ("chrono_slasher", new Vector2(7100, 850)),
            ("chrono_rioter", new Vector2(7900, 850)),
            ("chrono_slasher", new Vector2(7400, 850))
        };

        /// <summary>Three extractors, each guarding a climb the critical path can skip.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_04.extractor_barricade", new Vector2(2500, 628)),
            ("level_04.extractor_tower", new Vector2(5700, 228)),
            ("level_04.extractor_cellroof", new Vector2(7820, 524))
        };

        /// <summary>Every authored standard spawn in room order. Must total exactly 10.</summary>
        public static IEnumerable<(string EnemyID, Vector2 Position)> AllStandardSpawns {
            get {
                foreach (var spawn in Room1Spawns) yield return spawn;
                foreach (var spawn in Room2Spawns) yield return spawn;
                foreach (var spawn in Room3Spawns) yield return spawn;
            }
        }

        // === Base contract ===

        public override string LevelID => "level_04_paris";
        public override CampaignLevel Level => CampaignLevel.Paris;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s, plus the Glacis's +30 (§1.14). Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);
        public override string LevelTitleKey => "paris_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_04_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240, 850);

        // GR-LV2: the secret cache sits at the end of the Battlement line, on its last platform
        // (top 310), 95 px over it as it stood on the old barricade crest.
        protected override Vector2? SecretCachePosition => new(BattlementEndX, 215f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, just before the
        // PreBoss anchor (8480) - the courtyard starts 160 px after it - east of cell 2's far
        // wall.
        protected override Vector2? RestorationFontPosition => new(8200f, GroundY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "paris_objective_breach";
        protected override string BossObjectiveKey => "paris_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "paris_objective_complete";

        protected override Color FloorColor => new(0.20f, 0.18f, 0.17f);
        protected override Color FloorEdgeColor => new(0.44f, 0.38f, 0.30f);
        protected override Color PlatformColor => new(0.30f, 0.27f, 0.24f);
        protected override Color WallColor => new(0.14f, 0.13f, 0.13f);

        // === Scene-authored toolkit instances ===

        private readonly List<SearchlightZone> _searchlights = new();
        private readonly List<RescuableNPC> _prisoners = new();
        private readonly List<DestructibleBlock> _cellLocks = new();
        private PuzzleManager _prisonerPuzzle;
        private StaticBody2D _courtyardGate;

        public IReadOnlyList<SearchlightZone> Searchlights => _searchlights;
        public IReadOnlyList<RescuableNPC> Prisoners => _prisoners;
        public IReadOnlyList<DestructibleBlock> CellLocks => _cellLocks;
        public PuzzleManager PrisonerPuzzle => _prisonerPuzzle;

        /// <summary>False until the prisoner puzzle clears the inner-courtyard gate.</summary>
        public bool CourtyardGateOpen { get; private set; }

        /// <summary>How many prisoners have been freed (0-2). Drives the HUD counter.</summary>
        public int PrisonersFreed { get; private set; }

        /// <summary>Times a beam has caught the player; used for the alarm objective.</summary>
        public int SearchlightAlarmCount { get; private set; }

        // === GU-L4 (2026-10-12): the three encounter locks ===================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockGatehouse = ParisLevelID + "_lock_gatehouse";
        public const string LockCorridor = ParisLevelID + "_lock_corridor";
        public const string LockCells = ParisLevelID + "_lock_cells";

        /// <summary>The manifest rows the three lock waves are (level_04_rewards.tres).</summary>
        public const string Room1RowID = "level_04.room1";
        public const string Room2RowID = "level_04.room2";
        public const string Room3RowID = "level_04.room3";

        /// <summary>
        /// The gatehouse yard, east of the entry anchor (240). GR-LV2: its east end moved
        /// from the room seam (3200) to 2000, so the span past it can be the Glacis.
        /// </summary>
        public const float GatehouseLockMinX = 300f;
        public const float GatehouseLockMaxX = 2000f;
        /// <summary>
        /// The corridor floor between the two ground beams, up to the low cover's
        /// east end (4760): the tower climb and the swept walkway east of it hold no
        /// enemy, so the Middle anchor (5000) stands outside the lock.
        /// </summary>
        public const float CorridorLockMinX = Room2StartX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float CorridorLockMaxX = 4760f;
        /// <summary>
        /// The cell block, both cells inside (cell B's far wall ends at 8040), its
        /// exit short of the Restoration Font (8200) and the PreBoss anchor (8480).
        /// </summary>
        public const float CellsLockMinX = 6120f;
        public const float CellsLockMaxX = 8100f;

        /// <summary>Patrol half-span the Paris tables have always used (±200 px), clamped into each lock.</summary>
        private const float PatrolHalfSpan = 200f;

        private EncounterLock _gatehouseLock;
        private EncounterLock _corridorLock;
        private EncounterLock _cellsLock;

        public EncounterLock GatehouseLock => _gatehouseLock;
        public EncounterLock CorridorLock => _corridorLock;
        public EncounterLock CellsLock => _cellsLock;

        // === GR-LV2: the Glacis and the Battlement line =====================

        public const string GlacisStretchID = "level_04.stretch_glacis";
        public const string BattlementRouteID = "level_04.route_battlement";
        public const string SwarmRowID = "level_04.swarm1";

        public const float GlacisMinX = GatehouseLockMaxX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float GlacisMaxX = Room2StartX;
        /// <summary>Where the gatehouse's deck ends and the corridor's begins: the moat's two ramps' high joints.</summary>
        public const float GlacisWestDeckEndX = 2060f;
        public const float GlacisEastDeckStartX = 3160f;
        /// <summary>The Battlement line's last platform, which holds the secret.</summary>
        public const float BattlementEndX = 3035f;

        /// <summary>
        /// The swarm row on the gatehouse lock: the mob at the gates, 1,700 px wide after the
        /// carve, so eight (<see cref="SwarmWaveRules.RowMobsForLockWidth"/>; Easy's prefix
        /// gives five). The yard's spikes are there for a launched one.
        /// </summary>
        public static readonly float[] SwarmSpawnXs = { 520f, 760f, 980f, 1220f, 1400f, 1660f, 1820f, 1950f };

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildGatehouseYard();
            BuildGlacis();
            BuildSecurityCorridors();
            BuildCellBlock();
            BuildInnerCourtyard();
            BuildExtractors(ExtractorPlacements);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L4: the three approach rooms as encounter locks, one wave per
        /// manifest row. Built in <see cref="BuildLevel"/>, before the resume
        /// baseline, so a resume past a lock builds it Cleared with nothing spawned.
        /// </summary>
        private void BuildEncounterLocks() {
            float half = EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
            // The crowd already fighting at the gates: pre-placed.
            _gatehouseLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockGatehouse,
                Area = EncounterLockSpec.Span(GatehouseLockMinX, GatehouseLockMaxX, LevelHeight),
                EntryBarrierX = GatehouseLockMinX - half,
                ExitBarrierX = GatehouseLockMaxX + half,
                BarrierFloorY = GroundY,
                Waves = new[] {
                    LockWave(Room1RowID, Room1Spawns, EncounterWaveTrigger.OnBuild, GatehouseLockMinX, GatehouseLockMaxX),
                    SwarmWave(SwarmRowID, 850f, SwarmSpawnXs)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The garrison pours out of the corridors once the hero is in.
            _corridorLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockCorridor,
                Area = EncounterLockSpec.Span(CorridorLockMinX, CorridorLockMaxX, LevelHeight),
                EntryBarrierX = CorridorLockMinX - half,
                ExitBarrierX = CorridorLockMaxX + half,
                BarrierFloorY = GroundY,
                Waves = new[] { LockWave(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnEngage,
                    CorridorLockMinX, CorridorLockMaxX) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The cell block: its guards are posted, and its barrier also needs
            // both prisoners free. It keeps the level's prisoner objective.
            _cellsLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockCells,
                Area = EncounterLockSpec.Span(CellsLockMinX, CellsLockMaxX, LevelHeight),
                EntryBarrierX = CellsLockMinX - half,
                ExitBarrierX = CellsLockMaxX + half,
                BarrierFloorY = GroundY,
                Waves = new[] { LockWave(Room3RowID, Room3Spawns, EncounterWaveTrigger.OnBuild,
                    CellsLockMinX, CellsLockMaxX) },
                RequiredPuzzleIDs = new[] { PrisonerPuzzleID },
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>One lock wave from a Paris table, its ±200 px patrols clamped inside the lock.</summary>
        private static EncounterLockWave LockWave(string rowID, (string EnemyID, Vector2 Position)[] table,
            EncounterWaveTrigger trigger, float minX, float maxX) {
            var spawns = new EncounterLockSpawn[table.Length];
            for (int index = 0; index < table.Length; index++) {
                (string enemyID, Vector2 position) = table[index];
                float west = Mathf.Max(minX + 40f, position.X - PatrolHalfSpan);
                float east = Mathf.Min(maxX - 40f, position.X + PatrolHalfSpan);
                spawns[index] = new EncounterLockSpawn(enemyID, position,
                    new Vector2(west, position.Y), new Vector2(east, position.Y));
            }
            return new EncounterLockWave { RowID = rowID, Spawns = spawns, Trigger = trigger };
        }

        /// <summary>
        /// Godot readies children before their parent, so every authored template
        /// instance in Level_04_Paris.tscn is already live by the time BuildLevel runs.
        /// </summary>
        private void CollectAuthoredNodes() {
            _prisonerPuzzle = GetNodeOrNull<PuzzleManager>("PrisonerPuzzle");
            if (_prisonerPuzzle != null) _prisonerPuzzle.PuzzleCompleted += _ => OpenCourtyardGate();

            AddSearchlight("SearchlightGatehouse");
            AddSearchlight("SearchlightCorridor");
            AddSearchlight("SearchlightTower");

            AddPrisoner("PrisonerPuzzle/PrisonerA");
            AddPrisoner("PrisonerPuzzle/PrisonerB");

            AddCellLock("PrisonerPuzzle/CellLockA");
            AddCellLock("PrisonerPuzzle/CellLockB");
        }

        private void AddSearchlight(string nodeName) {
            if (GetNodeOrNull<SearchlightZone>(nodeName) is not SearchlightZone light) return;
            _searchlights.Add(light);
            light.PlayerDetected += _ => OnSearchlightAlarm();
        }

        private void AddPrisoner(string nodePath) {
            if (GetNodeOrNull<RescuableNPC>(nodePath) is not RescuableNPC prisoner) return;
            _prisoners.Add(prisoner);
            prisoner.Rescued += _ => OnPrisonerFreed();
        }

        private void AddCellLock(string nodePath) {
            if (GetNodeOrNull<DestructibleBlock>(nodePath) is not DestructibleBlock lockBlock) return;
            _cellLocks.Add(lockBlock);
        }

        /// <summary>Room 1: the crowd at the gates. Wide, flat, barricade platforms.</summary>
        private void BuildGatehouseYard() {
            BuildRoomBackground("BG_Room1_Gatehouse", Room1StartX, 3200, LevelHeight,
                new Color(0.11f, 0.09f, 0.10f, 0.4f));

            // GR-LV2: the yard's deck ends at the Glacis's moat.
            BuildFloor(Room1StartX, GroundY, GlacisWestDeckEndX - Room1StartX);
            BuildPlatform(700, 740, 280);
            BuildPlatform(1250, 620, 240);
            BuildPlatform(1800, 730, 260);

            BuildHazardSpikes(1550, 890, 130);

            BuildCheckpoint(240, GroundY - 50, $"{LevelID}_checkpoint_0", CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "paris_room_gatehouse", new Color(0.85f, 0.55f, 0.25f));

            BuildRoomTransition("paris_room_gatehouse", new Vector2(560, 600),
                new Rect2(Room1StartX, 0, GlacisWestDeckEndX - Room1StartX, LevelHeight));
            // Package 13 W4 (S26): the Bastille gates, and no Desmoulins to name what they came for.
            BuildAbsenceTrigger(new Vector2(1100, GroundY - 200));
        }

        /// <summary>
        /// GR-LV2: the Glacis, 2020-3200, and the Battlement line above it. The step line runs
        /// over a dry moat: the barricade (which keeps its Extractor), a crumbling step, a
        /// one-way step and a wall with a ledge. Down in the moat are the yard's second spike
        /// strip and a one-way back up, and its west ramp walks out. Every number is provisional
        /// (<c>PROVISIONAL-TRAVERSAL-RULES</c>).
        /// </summary>
        private void BuildGlacis() {
            TraversalStretch glacis = BeginStretch(GlacisStretchID, "paris_room_glacis",
                GlacisMinX, GlacisMaxX, GroundY, GlacisWestDeckEndX, GlacisEastDeckStartX, Room1StartX);
            BuildRoomDecoration(GlacisMinX, "paris_room_glacis", new Color(0.8f, 0.6f, 0.35f));
            BuildStretchTrench(glacis);
            // The deck resumes under the corridor lock's entry barrier: that barrier needs floor under it.
            BuildFloor(GlacisEastDeckStartX, GroundY, GlacisMaxX - GlacisEastDeckStartX);

            BuildStretchStep(glacis, 2170, 790, 200);
            TraversalSurface barricade = BuildStretchStep(glacis, 2500, 700, 300); // holds extractor 1
            BuildStretchStep(glacis, 2790, 720, 180, StretchStepKind.Crumbling, "level_04.crumbling_glacis");
            TraversalSurface eastStep = BuildStretchStep(glacis, 3000, 722, 200, StretchStepKind.OneWay);
            BuildStretchWallStep(glacis, 3150f, 690f, 940f);

            // Off the line: the spikes to cross in the moat, and a way back up under the one-way step.
            BuildHazardSpikes(2640, glacis.TrenchFloorY - 10f, 110);
            BuildOneWayPlatform(2980, 880, 200);

            // The Battlement line. Its base-kit line climbs west and back east, and ends in a 245 px hop.
            OptionalRoute battlement = BeginOptionalRoute(BattlementRouteID, SecretSourceID,
                SecretCachePosition.Value, barricade);
            BuildRouteStep(battlement, 2560, 530, 120);
            TraversalSurface burstFrom = BuildRouteStep(battlement, 2340, 420, 160);
            BuildRouteStep(battlement, 2590, 320, 180);
            TraversalSurface end = BuildRouteStep(battlement, BattlementEndX, 320, 220);
            battlement.MarkBurstShortcut(burstFrom, end);
            battlement.MarkRiseShortcut(eastStep, end);
        }

        /// <summary>
        /// Room 2: the era beat. Two ground-level beams gate the corridor, then a
        /// vertical tower climb on drop-through platforms ends on an upper walkway
        /// under the third beam. The searchlights themselves are authored in the
        /// scene; this only builds the geometry that makes timing them matter.
        /// </summary>
        private void BuildSecurityCorridors() {
            BuildRoomBackground("BG_Room2_Corridors", Room2StartX, 2880, LevelHeight,
                new Color(0.08f, 0.08f, 0.12f, 0.45f));

            BuildFloor(Room2StartX, GroundY, 2880);

            // Low cover between the two ground beams: the safe pockets to wait in.
            BuildPlatform(3400, 780, 200);
            BuildPlatform(4020, 760, 220);
            BuildPlatform(4660, 780, 200);

            // Bastille tower: the level's vertical section, drop-through on the way down.
            BuildOneWayPlatform(4900, 760, 220);
            BuildOneWayPlatform(5150, 620, 220);
            BuildOneWayPlatform(4950, 480, 220);
            // The top step, west of the walkway's end (4780..4980, top 370): a
            // 100 px hop from the step below, then 78 px onto the walkway — inside
            // Lincoln's single jump both times. It used to sit at 5200/380, under
            // the walkway's solid underside (308) with 62 px of headroom, less
            // than the 64 px hero, so a hero who reached it was wedged.
            BuildOneWayPlatform(TowerTopStepX, TowerTopStepY, 200);

            // Upper walkway, swept end to end by the tower beam.
            BuildPlatform(5540, 300, 1080);

            // GU-L4: the Middle anchor stands at the tower's foot, east of the
            // corridor lock (it ends at 4760), so no lock area holds it.
            BuildCheckpoint(MiddleCheckpointX, GroundY - 50, $"{LevelID}_checkpoint_1", CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "paris_room_searchlights", new Color(0.95f, 0.88f, 0.45f));

            // GU-L4: the room trigger stands just inside the corridor lock, so a
            // hero pressed against the gatehouse's exit barrier does not reach it.
            // The old wave trigger is gone: the corridor lock spawns its wave.
            BuildRoomTransition("paris_room_searchlights", new Vector2(CorridorLockMinX + 40f, 600),
                new Rect2(Room2StartX, 0, 2880, LevelHeight));
        }

        /// <summary>The Middle anchor's x (unchanged at 5000; outside the corridor lock since GU-L4).</summary>
        public const float MiddleCheckpointX = 5000f;
        /// <summary>The PreBoss anchor's x (unchanged at 8480; past the cell block's barrier since GU-L4).</summary>
        public const float PreBossCheckpointX = 8480f;

        /// <summary>
        /// Room 3: two sealed cells. Each cell is a walled alcove whose only opening
        /// is filled floor-to-ceiling by a destructible lock mechanism, so the
        /// prisoner's interaction area is physically unreachable until the lock is
        /// blown. Freeing both satisfies the puzzle and opens the courtyard gate.
        /// </summary>
        private void BuildCellBlock() {
            BuildRoomBackground("BG_Room3_Cells", Room3StartX, 2560, LevelHeight,
                new Color(0.10f, 0.08f, 0.09f, 0.5f));

            BuildFloor(Room3StartX, GroundY, 2560);
            BuildPlatform(6300, 700, 200);
            BuildPlatform(7300, 660, 220);
            BuildPlatform(8300, 700, 200);

            BuildCellShell(6820);
            BuildCellShell(7820);

            BuildCheckpoint(PreBossCheckpointX, GroundY - 50, $"{LevelID}_checkpoint_2", CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "paris_room_cells", new Color(0.7f, 0.45f, 0.5f));

            _courtyardGate = BuildDoor("CourtyardGate", new Vector2(CourtyardGateX, GroundY),
                "paris_gate_locked", new Vector2(CourtyardGateWidth, CourtyardGateHeight), CourtyardGateColor);
            // P5 (2026-10-04): the gate spans y 140..900 and nothing stood above
            // it, so a double jump off the cell-block platform (8300/700, 136 px
            // west) plus an upward Relativity Warp cleared it with both prisoners
            // still locked up. A permanent column seals the sky above it; freeing
            // the prisoners still opens the gate itself.
            BuildGateColumn(CourtyardGateX, CourtyardGateWidth, GroundY - CourtyardGateHeight, CourtyardGateColor);

            // GU-L4: the old wave trigger is gone; the cell block lock spawns its wave.
            BuildRoomTransition("paris_room_cells", new Vector2(Room3StartX, 600),
                new Rect2(Room3StartX, 0, 2560, LevelHeight));
        }

        /// <summary>
        /// Cell walls for one prisoner alcove. The ceiling slab spans the cell and
        /// the far wall closes it, leaving a single 96 px doorway on the left that
        /// the authored <see cref="DestructibleBlock"/> fills from floor to ceiling.
        /// </summary>
        private void BuildCellShell(float centerX) {
            BuildPlatform(centerX, 604, 440);              // ceiling slab, 596-612
            BuildWall(centerX + 200, 604, 296);            // far wall, down to the floor
        }

        /// <summary>
        /// Room 4: the boss arena. Two drawbridge walkways ride above a single flat
        /// pit floor - the Governor's Guard's summoned garrison needs a continuous surface to
        /// land on and path along, so the pit is deliberately not a hazard.
        /// </summary>
        private void BuildInnerCourtyard() {
            BuildRoomBackground("BG_Room4_Courtyard", Room4StartX, 1600, LevelHeight,
                new Color(0.16f, 0.06f, 0.07f, 0.5f));

            // M19: the courtyard floor is authored in three pieces so its centre can
            // collapse into the Governor's Guard's Phase 2 pit (see BuildTribunalPit).
            BuildFloor(Room4StartX, PitFloorY, TribunalPitLeftX - Room4StartX);
            BuildFloor(TribunalPitRightX, PitFloorY, Room4StartX + 1600f - TribunalPitRightX);
            BuildTribunalPit();

            BuildPlatform(9000, 780, 480);   // drawbridge walkway west
            BuildPlatform(9760, 780, 480);   // drawbridge walkway east

            // Back wall only above the entry drop, so the arena reads closed
            // without sealing the player out of room three.
            BuildWall(Room4StartX - 20, 240, 460);
            BuildWall(10220, 240, 760);

            BuildRoomDecoration(Room4StartX, "paris_room_courtyard", new Color(0.95f, 0.25f, 0.25f));

            BuildRoomTransition("paris_room_courtyard", new Vector2(Room4StartX + 60, 600),
                new Rect2(Room4StartX, 0, 1600, LevelHeight));

            _tribunalEncounter = BuildBossEncounter(BossResourcePath, new Vector2(9400, 950),
                "RevolutionaryTribunalEncounter", BossArenaBounds);
            if (_tribunalEncounter != null) _tribunalEncounter.PhaseEntered += OnTribunalPhaseEntered;
        }

        // === Package 12 W9 (M19) — the Governor's Guard's Phase 2 pit ===
        //
        // Design §6: "P2 triggers on the first member's defeat: the survivor absorbs
        // the fallen's ability set and the floor's central section collapses into
        // the level's signature pit." The squad, the absorb and the phase trigger
        // live on revolutionary_tribunal.tres / BossEncounterController; the floor
        // lives here. The pit is deliberately NON-LETHAL (lethal Story pits are held
        // under VERIFY-STORY-PITS / plan D11): a shallow trapezoid with walkable
        // ramps, so the player, the surviving member and the summoned rioters can
        // all walk in and out — it reshapes the fight, it does not end it.

        /// <summary>The collapsible centre section, west and east edges.</summary>
        public const float TribunalPitLeftX = 9200f;
        public const float TribunalPitRightX = 9660f;
        /// <summary>How far the pit floor sits below the courtyard.</summary>
        public const float TribunalPitDepth = 120f;
        /// <summary>Horizontal run of each ramp (≈40.6° — under the 45° walkable limit).</summary>
        public const float TribunalPitRampRun = 140f;
        /// <summary>
        /// The pit's flat floor, 40 px under the courtyard arena's bottom edge
        /// (<see cref="BossArenaBounds"/>, the level's bottom) — which is why the
        /// arena fall net keeps a margin (<see cref="BossController.ArenaFallNetMarginPixels"/>).
        /// </summary>
        public const float TribunalPitFloorY = PitFloorY + TribunalPitDepth;

        private BossEncounterController _tribunalEncounter;
        private StaticBody2D _tribunalPitLid;

        /// <summary>The encounter (test seam).</summary>
        public BossEncounterController TribunalEncounter => _tribunalEncounter;

        /// <summary>True once Phase 2 has collapsed the courtyard centre.</summary>
        public bool TribunalPitOpen { get; private set; }

        /// <summary>The floor section that collapses (test seam).</summary>
        public StaticBody2D TribunalPitLid => _tribunalPitLid;

        /// <summary>
        /// The pit is built up front underneath a flat lid; Phase 2 only removes the
        /// lid, so no collision body is ever created mid-fight.
        /// </summary>
        private void BuildTribunalPit() {
            float top = PitFloorY;
            float bottom = TribunalPitFloorY;
            var outline = new[] {
                new Vector2(TribunalPitLeftX, top),
                new Vector2(TribunalPitLeftX + TribunalPitRampRun, bottom),
                new Vector2(TribunalPitRightX - TribunalPitRampRun, bottom),
                new Vector2(TribunalPitRightX, top),
                new Vector2(TribunalPitRightX, bottom + 80f),
                new Vector2(TribunalPitLeftX, bottom + 80f)
            };
            var pit = new StaticBody2D {
                Name = "TribunalPitFloor",
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            pit.AddChild(new CollisionPolygon2D { Polygon = outline });
            pit.AddChild(new Polygon2D {
                Name = "Visual",
                Polygon = outline,
                Color = new Color(0.12f, 0.08f, 0.08f)
            });
            AddChild(pit);

            _tribunalPitLid = BuildFloor(TribunalPitLeftX, PitFloorY, TribunalPitRightX - TribunalPitLeftX);
            _tribunalPitLid.Name = "TribunalPitLid";
        }

        private void OnTribunalPhaseEntered(int phase) {
            if (phase >= 1) OpenTribunalPit();
        }

        /// <summary>Collapses the courtyard centre. Idempotent; public as a test seam.</summary>
        public void OpenTribunalPit() {
            if (TribunalPitOpen) return;
            TribunalPitOpen = true;
            if (_tribunalPitLid == null || !IsInstanceValid(_tribunalPitLid)) return;
            Godot.Collections.Array<Node> children = _tribunalPitLid.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is CollisionShape2D shape) shape.SetShapeDisabledSafe(true);
            }
            _tribunalPitLid.Visible = false;
        }

        // === Encounters ===
        //
        // GU-L4 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles.

        // Package 12 W2 (GAP-13): explicit encounter baseline. Paris has no
        // non-encounter anchor state, so MarkWavesClearedThrough is not overridden.
        // GU-L4: the encounters are the three locks; every anchor lists every lock
        // before it, so a resume builds those Cleared (the gatehouse used to
        // respawn behind the hero on every resume).

        private const string ParisLevelID = "level_04_paris";

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [ParisLevelID + "_checkpoint_0"] = System.Array.Empty<string>(),
                [ParisLevelID + "_checkpoint_1"] = new[] { LockGatehouse, LockCorridor },
                [ParisLevelID + "_checkpoint_2"] = new[] { LockGatehouse, LockCorridor, LockCells }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        // === Era mechanics ===

        protected override void OnLevelReady() {
            // The base already confines the camera on a resume
            // (StoryLevelControllerBase.ApplyResumeCameraBounds). Paris additionally
            // marks the starting room's trigger as crossed so it cannot re-fire, and
            // enables its encounter root - hence the full ActivateRoom, on every
            // entry rather than only on a resume.
            FindRoomTriggerContaining(Player?.Position.X ?? PlayerSpawnPosition.X)?.ActivateRoom(Player);

            // Redundant since PuzzleManager re-emits a save-restored completion
            // (Wave A integration), but kept: it is also what sets PrisonersFreed.
            if (_prisonerPuzzle is { IsCompleted: true }) {
                PrisonersFreed = _prisoners.Count;
                OpenCourtyardGate();
            } else {
                PostPrisonerObjective();
            }
        }

        /// <summary>A beam catching the player is a warning beat, not an extra wave.</summary>
        private void OnSearchlightAlarm() {
            SearchlightAlarmCount++;
            SetObjective("paris_objective_searchlight_alarm");
        }

        private void OnPrisonerFreed() {
            PrisonersFreed++;
            if (_prisonerPuzzle == null || !_prisonerPuzzle.IsCompleted) PostPrisonerObjective();
        }

        private void PostPrisonerObjective() =>
            SetObjective("paris_objective_free_prisoners", PrisonersFreed, _prisoners.Count);

        /// <summary>
        /// GU-L4: a cleared or reverted lock hands back the level's own objective,
        /// derived from live state rather than the last line posted (which may be
        /// a searchlight alarm from the middle of the fight).
        /// </summary>
        protected override void RestoreLevelObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (CourtyardGateOpen) SetObjective("paris_objective_reach_courtyard");
            else PostPrisonerObjective();
        }

        /// <summary>The freed prisoners haul the inner-courtyard gate open. Idempotent.</summary>
        private void OpenCourtyardGate() {
            if (CourtyardGateOpen) return;
            CourtyardGateOpen = true;
            OpenDoor(ref _courtyardGate);
            SetObjective("paris_objective_reach_courtyard");
        }
    }
}
