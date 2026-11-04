using Godot;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 8 - Cleopatra's Palace, Alexandria 30 BC. Four rooms across 10,240 px
    /// split into the two zones the design calls for: a sun-bleached SURFACE where
    /// Octavian's cult-armed legion is pressing the palace, and the TOMB NETWORK
    /// below it, reached by a vertical descent down a collapsed burial shaft.
    ///
    /// Era identity is mechanical in both zones:
    /// <list type="bullet">
    /// <item>Surface: five <see cref="MovementDampenerZone"/> drifts of deep sand at
    ///       the design's ~50% slow. They are deliberately non-overlapping -
    ///       <see cref="EnvironmentPlayerModifiers"/> publishes the PRODUCT of every
    ///       live source, so two drifts over one player would compound to a crawl.</item>
    /// <item>Tombs: a four-glyph <see cref="SequenceLock"/> seals the vault route into
    ///       Cleopatra's chambers. The glyphs are mounted on the wall in an order that
    ///       is NOT the answer; the answer is carved on a relief in a side alcove
    ///       further west, so the lock is read rather than brute-forced.</item>
    /// </list>
    ///
    /// Routing the legion away from the inner chambers is the through-line: the tomb
    /// route is the way past the siege line, and the vault seal is what keeps the
    /// Romans out of it.
    ///
    /// Locked encounter economy (docs/DUST_ECONOMY.md, DustEconomyTests row 8):
    /// 10 standards / 0 elites / 1 boss / 3 extractors. The authored tables below are
    /// the single source of those counts and Level08ContentTests asserts them.
    ///
    /// This level owes a FOURTH dialogue beat: <c>level_08.postboss</c> is an authored
    /// campaign scene (design-godot.md 3368-3374) that runs between the boss dying and
    /// the ordinary exit beat. It is declared through the base class's
    /// <see cref="StoryLevelControllerBase.PostBossDialogueIDs"/> hook, which chains
    /// defeat -> postboss -> exit -> results without taking the defeat bookkeeping
    /// away from the base.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L8; the Level 2
    /// pattern): every fight is an <see cref="EncounterLock"/> the hero must clear.
    /// The dunes (300-3200); the siege line (3220-5460), whose exit barrier stands
    /// short of the shaft lip so the burial shaft is no longer a free escape; the
    /// tomb (5720-7000), the shaft and the west corridor; and the vault (7020-8310,
    /// the corridor band only), whose exit fills the vault door under its gate
    /// column and which needs the hieroglyph seal solved as well as its ward detail
    /// killed. The Middle anchor moved out of the tomb to the shaft lip (5640), the
    /// PreBoss anchor and the absence beat moved east out of swing reach of the vault,
    /// and the carved relief moved into the vault so the answer is still in reach
    /// once its lock has shut the way back. Every checkpoint lists the locks before it.</para>
    ///
    /// <para><b>The Dune Crossing</b> (gameplay review 2026-11-03, rulings 7a-7c+; GR-LV3):
    /// the dunes lock was carved to 300-2000, and the freed span is a traversal stretch
    /// (2020-3200) over a non-lethal sand trench, with one deep-sand drift lying in it. Its
    /// optional route, the Obelisk Line, holds the dune ridge's Extractor. The siege line
    /// takes the level's swarm row. Nothing past the stretch moved.</para>
    /// </summary>
    public partial class Level08Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_08_egypt";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/jackal_priest.tres";

        /// <summary>
        /// P1 (2026-10-04): the inner chamber, under its ceiling slab (840). West
        /// edge is <see cref="BossArenaWestX"/> (spawn 9800 - 900), keeping the
        /// pre-boss anchor (8420) and the absence beat (8500) outside; east edge is
        /// the chamber wall's inner face. Decoy teleports stay inside it too.
        /// </summary>
        public static readonly Rect2 BossArenaBounds =
            new(BossArenaWestX, 840f, LevelWidth - 20f - BossArenaWestX, LevelHeight - 840f);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 9800f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;
        public const string DialogueResourcePath = "res://resources/Dialogue/level_08_dialogue.tres";

        /// <summary>Stable, save-persisted puzzle id for the hieroglyph vault seal.</summary>
        public const string HieroglyphPuzzleID = "level_08.hieroglyph_lock";

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.Egypt;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass. GR-LV3 adds the Dune Crossing's +30.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);
        public override string LevelTitleKey => "egypt_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(200, 650);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the high dune spire in the siege line (Platform 3950/430, top 422), climbed
        // from the ledge at 3500 — a dead-end lookout above the dampening dunes.
        protected override Vector2? SecretCachePosition => new(3950f, 327f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (8420) on the chamber floor, 240 px short of the arena (8900).
        protected override Vector2? RestorationFontPosition => new(8660f, TombFloorY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        /// <summary>The authored Cleopatra scene; see design-godot.md 3368-3374.</summary>
        public string PostBossDialogueID => $"{DialoguePrefix}.postboss";

        protected override string InitialObjectiveKey => "egypt_objective_dunes";
        protected override string BossObjectiveKey => "egypt_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "egypt_objective_complete";

        // === Layout ===

        public const float LevelWidth = 10240f;
        /// <summary>Taller than Florence's 1080: the surface and the tombs are stacked.</summary>
        public const float LevelHeight = 1800f;

        /// <summary>Desert floor surface (rooms 1-2).</summary>
        public const float SurfaceGroundY = 700f;
        /// <summary>Tomb corridor floor (rooms 3-4).</summary>
        public const float TombFloorY = 1620f;

        private const float SurfaceEnemyY = SurfaceGroundY - 50f;
        private const float TombEnemyY = TombFloorY - 50f;

        /// <summary>East edge of the surface: past this the burial shaft opens up.</summary>
        public const float SurfaceEndX = 5700f;
        /// <summary>
        /// GU-L8 (2026-10-12): the left face of the wall that now closes the tomb
        /// corridor's west end under the surface (5680-5700, y 700-1660). The tomb
        /// floor starts at <see cref="SurfaceEndX"/> and nothing stood west of it, so
        /// a hero walking or knocked west off the corridor floor fell out of the level.
        /// </summary>
        public const float ShaftWestWallX = SurfaceEndX - DefaultWallThickness;
        /// <summary>East edge of the open shaft; the tomb ceiling starts here.</summary>
        private const float ShaftEndX = 6450f;
        /// <summary>Underside of the tomb corridor ceiling slab.</summary>
        private const float TombCeilingY = 1000f;
        /// <summary>Top of the boss chamber ceiling slab.</summary>
        private const float ChamberCeilingY = 840f;
        /// <summary>Underside of that slab - where the chamber's open air begins.</summary>
        private const float ChamberHeadroomY = ChamberCeilingY + DefaultFloorThickness;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 3200f;
        private const float Room3StartX = 5760f;
        public const float Room4StartX = 8320f;

        /// <summary>The hieroglyph-sealed vault door; the only route into the chambers.</summary>
        public const float VaultGateX = Room4StartX;
        private const float VaultDoorWidth = 48f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 3200, 1080);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, 1080);
        // The descent room spans both zones, so it gets the full level height.
        // GU-L8 (2026-10-12): it starts at the shaft's west wall (5680), not 5760,
        // so a hero dropping off the lip into the shaft is never left off screen
        // by the 1.5x camera before the tomb lock takes it.
        public static readonly Rect2 Room3CameraBounds = new(ShaftWestWallX, 0, Room4StartX - ShaftWestWallX, LevelHeight);
        // Underground: anchor the 1080 window to the lower band of the level.
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX, 720, 1920, 1080);

        /// <summary>Boss court width; the Jackal Priest's ranged band has to fit inside it.</summary>
        public const float BossArenaWidth = LevelWidth - Room4StartX;

        // === Era identity constants ===

        public const float DeepSandMultiplier = 0.5f;
        public const int DeepSandZoneCount = 5;
        public const int GlyphCount = 4;

        /// <summary>
        /// Wall order (west to east) paired with the activation order the alcove
        /// relief carves. They deliberately disagree: pressing the wall left to right
        /// fails, which is what makes the relief worth finding.
        /// </summary>
        public static readonly (string NodeName, string LabelKey, int OrderIndex)[] GlyphPlan = {
            ("GlyphFalcon", "egypt_glyph_falcon", 2),
            ("GlyphScarab", "egypt_glyph_scarab", 0),
            ("GlyphJackal", "egypt_glyph_jackal", 3),
            ("GlyphIbis", "egypt_glyph_ibis", 1)
        };

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>
        /// Era roster: `plasma_spear_ward` (Egypt standard, plasma javelins) mixed with
        /// `chrono_slasher` (Severed shock-trooper standard, melee). No elites - the locked
        /// row for level 8 is E = 0.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] DuneSpawns = {
            ("plasma_spear_ward", new Vector2(1300, SurfaceEnemyY), new Vector2(1150, SurfaceEnemyY), new Vector2(1450, SurfaceEnemyY)),
            // GR-LV3: 2100 -> 1650 and 2900 -> 1900, inside the carved lock.
            ("chrono_slasher", new Vector2(1650, SurfaceEnemyY), new Vector2(1500, SurfaceEnemyY), new Vector2(1800, SurfaceEnemyY)),
            ("plasma_spear_ward", new Vector2(1900, SurfaceEnemyY), null, null)
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] SiegeLineSpawns = {
            ("chrono_slasher", new Vector2(3800, SurfaceEnemyY), new Vector2(3650, SurfaceEnemyY), new Vector2(3950, SurfaceEnemyY)),
            ("plasma_spear_ward", new Vector2(4400, SurfaceEnemyY), null, null),
            ("chrono_slasher", new Vector2(5200, SurfaceEnemyY), new Vector2(5050, SurfaceEnemyY), new Vector2(5350, SurfaceEnemyY))
        };

        /// <summary>
        /// GU-L8 (2026-10-12): the tomb lock's pair, both inside its area (5720-7000):
        /// the slasher moved 6900 -> 6800 (its patrol pulled inside the area) and the
        /// javelin ward 7320 -> 6450, at the foot of the shaft, from the vault end of
        /// the corridor, which is now the vault lock's.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] TombSpawns = {
            ("chrono_slasher", new Vector2(6800, TombEnemyY), new Vector2(6680, TombEnemyY), new Vector2(6960, TombEnemyY)),
            ("plasma_spear_ward", new Vector2(6450, TombEnemyY), null, null)
        };

        /// <summary>Ward detail standing over the seal itself: the lock is solved under fire.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] VaultSpawns = {
            ("plasma_spear_ward", new Vector2(7920, TombEnemyY), null, null),
            ("chrono_slasher", new Vector2(8150, TombEnemyY), new Vector2(8060, TombEnemyY), new Vector2(8240, TombEnemyY))
        };

        /// <summary>15 dust each and resource-owned; never overridden from level code.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            // GR-LV3: the end of the Obelisk Line, over the Dune Crossing (it stood at 2600, 460).
            ("level_08.extractor_dune_ridge", new Vector2(ObeliskLineEndX, 38)),
            ("level_08.extractor_tomb_alcove", new Vector2(6700, 1240)),
            ("level_08.extractor_vault_ledge", new Vector2(7600, 1200))
        };

        public static int AuthoredStandardCount =>
            DuneSpawns.Length + SiegeLineSpawns.Length + TombSpawns.Length + VaultSpawns.Length;

        public static int AuthoredEliteCount => 0;

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === GU-L8 (2026-10-12): the four encounter locks ====================

        /// <summary>The F10 encounter IDs of the four locks (they start with the level ID).</summary>
        public const string LockDunes = ID + "_lock_dunes";
        public const string LockSiegeLine = ID + "_lock_siege_line";
        public const string LockTomb = ID + "_lock_tomb";
        public const string LockVault = ID + "_lock_vault";

        /// <summary>The manifest rows the four lock waves are (level_08_rewards.tres).</summary>
        public const string DuneRowID = "level_08.dune";
        public const string SiegeLineRowID = "level_08.siege_line";
        public const string TombRowID = "level_08.tomb";
        public const string VaultRowID = "level_08.vault";

        private const float BarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>
        /// The dunes: the first room past the entry anchor (200), to 2000. GR-LV3 carved it
        /// from 3200; its exit barrier stands on the deck, in the gap past the platform at
        /// 1700. The Dune Crossing takes the rest.
        /// </summary>
        public const float DuneLockMinX = 300f;
        public const float DuneLockMaxX = 2000f;

        /// <summary>
        /// The siege line: room 2 on the surface. Its exit barrier (5460-5480) stands
        /// short of the shaft lip (5700), so dropping into the burial shaft is no longer
        /// a way past the legion; the anchor between them is the Middle.
        /// </summary>
        public const float SiegeLineLockMinX = Room2StartX + BarrierWidth;
        public const float SiegeLineLockMaxX = 5460f;

        /// <summary>
        /// The Middle anchor at the shaft lip, between the siege line's exit barrier
        /// and the tomb's entry. Its strike surface (5610-5670) is beyond the longest
        /// basic swing from inside the siege line (Level08ContentTests measures it),
        /// and its respawn point is clear of the room 3 trigger (5720-5800). It used
        /// to stand at 6520, inside the tomb fight.
        /// </summary>
        public const float MiddleCheckpointX = 5640f;

        /// <summary>
        /// The tomb: the burial shaft and the west end of the corridor, full height
        /// (a hero dropping off the lip engages it mid-air), from the shaft's west
        /// wall to the vault's entry.
        /// </summary>
        public const float TombLockMinX = SurfaceEndX + BarrierWidth;
        public const float TombLockMaxX = 7000f;

        /// <summary>
        /// The vault: the east end of the corridor up to the vault door, the corridor
        /// band only (from the ceiling's underside down), so a hero walking the
        /// corridor's roof from the shaft can never engage it out of reach of its
        /// members. Its exit barrier fills the vault door under the gate column, and
        /// it clears only once the hieroglyph seal is solved as well.
        /// </summary>
        public const float VaultLockMinX = TombLockMaxX + BarrierWidth;
        public const float VaultLockMaxX = VaultGateX - BarrierWidth * 0.5f;
        public const float VaultLockTopY = TombCeilingY + DefaultFloorThickness;

        /// <summary>
        /// The PreBoss anchor, moved 8420 -> 8490 so its strike surface (8460-8520)
        /// is beyond the longest basic swing from inside the vault (a hero pressed
        /// against the vault's exit barrier stands at 8290).
        /// </summary>
        public const float PreBossCheckpointX = 8490f;

        /// <summary>The absence beat's trigger, moved 8500 -> 8560 so the PreBoss respawn never stands in it.</summary>
        public const float AbsenceTriggerX = 8560f;

        /// <summary>
        /// The carved relief that spells the glyph order. GU-L8: moved from the tomb
        /// alcove (6700, 1180) into the vault lock's area, above the vault ledge
        /// (still west of the glyph wall): once the vault engages its entry shuts,
        /// and a relief left behind it would leave only brute force.
        /// </summary>
        public static readonly Vector2 CarvedReliefPosition = new(7280f, 1360f);

        // === GR-LV3: the Dune Crossing, the Obelisk Line and the swarm row ========

        public const string CrossingStretchID = "level_08.dune_crossing";
        public const string ObeliskRouteID = "level_08.obelisk_line";
        public const string CrossingRoomKey = "egypt_room_crossing";

        /// <summary>From the dunes lock's exit barrier to the siege line's entry barrier.</summary>
        public const float CrossingMinX = DuneLockMaxX + BarrierWidth;
        public const float CrossingMaxX = Room2StartX;
        public const float CrossingWestDeckEndX = 2060f;
        public const float CrossingEastDeckStartX = 3100f;

        /// <summary>The Obelisk Line's last platform; the Extractor stands 72 px over its top.</summary>
        public const float ObeliskLineEndX = 3035f;

        /// <summary>The siege line's swarm row (level_08_rewards.tres).</summary>
        public const string SwarmRowID = "level_08.swarm1";

        /// <summary>The row, Hard's truth (a 2240 px lock takes 8), along the siege line, clear of both scorch strips.</summary>
        public static readonly float[] SwarmSpawnXs = { 3620f, 3760f, 3900f, 4060f, 4560f, 4720f, 4860f, 5200f };

        /// <summary>
        /// Base-kit pieces only. The trench is deep sand's home now: the scene's second drift
        /// lies on its floor (x 2270-2910), so a hero who falls off the line wades out.
        /// The stretch's camera keeps to the surface band, as the dunes' does.
        /// </summary>
        private void BuildDuneCrossing() {
            TraversalStretch crossing = BeginStretch(CrossingStretchID, CrossingRoomKey,
                CrossingMinX, CrossingMaxX, SurfaceGroundY, CrossingWestDeckEndX, CrossingEastDeckStartX, Room1StartX);
            crossing.Room.CameraBounds = Room1CameraBounds;
            BuildRoomDecoration(CrossingMinX, CrossingRoomKey, new Color(0.95f, 0.72f, 0.38f));
            BuildStretchTrench(crossing);
            // The deck resumes under the siege line's entry barrier: that barrier needs floor under it.
            BuildFloor(CrossingEastDeckStartX, SurfaceGroundY, CrossingMaxX - CrossingEastDeckStartX);

            BuildStretchStep(crossing, 2170, 590, 200);
            BuildStretchStep(crossing, 2430, 520, 180, StretchStepKind.Crumbling, "level_08.crumbling_dune");
            TraversalSurface middleStep = BuildStretchStep(crossing, 2700, 522, 200, StretchStepKind.OneWay);
            TraversalSurface eastStep = BuildStretchStep(crossing, 2960, 520, 200);
            // On the deck, so the east ramp stays walkable: a double jump or the ledge clears it.
            // The last step ends 80 px short of it, or a hero jumping from the deck bonks the step's underside.
            BuildStretchWallStep(crossing, 3140f, 490f, SurfaceGroundY);

            // Off the line: a way back up out of the sand.
            BuildOneWayPlatform(2640, 670, 200);

            OptionalRoute obelisk = BeginOptionalRoute(ObeliskRouteID, ExtractorPlacements[0].ID,
                ExtractorPlacements[0].Position, middleStep);
            BuildRouteStep(obelisk, 2510, 330, 120);
            TraversalSurface burstFrom = BuildRouteStep(obelisk, 2290, 220, 160);
            BuildRouteStep(obelisk, 2540, 120, 180);
            TraversalSurface end = BuildRouteStep(obelisk, ObeliskLineEndX, 120, 220);
            obelisk.MarkBurstShortcut(burstFrom, end);
            obelisk.MarkRiseShortcut(eastStep, end);
        }

        private EncounterLock _dunesLock;
        private EncounterLock _siegeLineLock;
        private EncounterLock _tombLock;
        private EncounterLock _vaultLock;

        public EncounterLock DunesLock => _dunesLock;
        public EncounterLock SiegeLineLock => _siegeLineLock;
        public EncounterLock TombLock => _tombLock;
        public EncounterLock VaultLock => _vaultLock;

        // === Runtime state ===

        private readonly List<MovementDampenerZone> _deepSand = new();
        private PuzzleManager _hieroglyphPuzzle;
        private SequenceLock _hieroglyphLock;
        private StaticBody2D _vaultDoor;
        private bool _descended;

        public IReadOnlyList<MovementDampenerZone> DeepSandZones => _deepSand;
        public PuzzleManager HieroglyphPuzzle => _hieroglyphPuzzle;
        public SequenceLock HieroglyphLock => _hieroglyphLock;
        public bool VaultDoorOpen => _vaultDoor == null || !IsInstanceValid(_vaultDoor);

        /// <summary>True while the authored Cleopatra scene is the beat on screen.</summary>
        public bool PostBossBeatActive => ActivePostBossDialogueID == PostBossDialogueID;

        // === Palette: bleached limestone, tomb shadow, Archive cyan ===

        protected override Color FloorColor => new(0.30f, 0.25f, 0.16f);
        protected override Color FloorEdgeColor => new(0.72f, 0.60f, 0.34f);
        protected override Color PlatformColor => new(0.40f, 0.34f, 0.22f);
        protected override Color WallColor => new(0.14f, 0.12f, 0.09f);
        protected override Color HazardColor => new(0.66f, 0.34f, 0.12f);

        private static readonly Color TombFloorColor = new(0.18f, 0.16f, 0.14f);
        private static readonly Color TombPlatformColor = new(0.26f, 0.23f, 0.19f);
        private static readonly Color VaultDoorColor = new(0.28f, 0.36f, 0.42f);
        private static readonly Color GlyphTextColor = new(1f, 0.82f, 0.36f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1ShiftingDunes();
            BuildDuneCrossing();
            BuildRoom2SiegeLine();
            BuildRoom3TombNetwork();
            BuildRoom4InnerChamber();
            BuildExtractors(ExtractorPlacements);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L8: the four fights as encounter locks, one wave per manifest row. Built
        /// in <see cref="BuildLevel"/> so a resume past a lock builds it Cleared with
        /// no member ever spawned.
        /// </summary>
        private void BuildEncounterLocks() {
            _dunesLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockDunes,
                Area = EncounterLockSpec.Span(DuneLockMinX, DuneLockMaxX, LevelHeight),
                EntryBarrierX = DuneLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = DuneLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = SurfaceGroundY,
                Waves = new[] { EncounterLockWave.FromTable(DuneRowID, DuneSpawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The legion's line comes over the dunes once the hero is in it.
            _siegeLineLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockSiegeLine,
                Area = EncounterLockSpec.Span(SiegeLineLockMinX, SiegeLineLockMaxX, LevelHeight),
                EntryBarrierX = SiegeLineLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = SiegeLineLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = SurfaceGroundY,
                Waves = new[] {
                    EncounterLockWave.FromTable(SiegeLineRowID, SiegeLineSpawns, EncounterWaveTrigger.OnEngage),
                    SwarmWave(SwarmRowID, SurfaceEnemyY, SwarmSpawnXs)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The shaft and the west corridor: the entry barrier stands on the shaft's
            // west wall, so the hero cannot climb back out onto the surface mid-fight;
            // the exit is the corridor's split with the vault.
            _tombLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockTomb,
                Area = EncounterLockSpec.Span(TombLockMinX, TombLockMaxX, LevelHeight),
                EntryBarrierX = TombLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = TombLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = TombFloorY,
                Waves = new[] { EncounterLockWave.FromTable(TombRowID, TombSpawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The vault: the corridor band only, its exit the sealed door under the
            // gate column, and the seal a condition. Its objective is the level's own
            // (the seal's progress); the HUD counts the ward detail.
            _vaultLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockVault,
                Area = new Rect2(VaultLockMinX, VaultLockTopY, VaultLockMaxX - VaultLockMinX, LevelHeight - VaultLockTopY),
                EntryBarrierX = VaultLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = VaultGateX,
                ExitBarrierYRange = new Vector2(TombCeilingY, TombFloorY),
                BarrierFloorY = TombFloorY,
                Waves = new[] { EncounterLockWave.FromTable(VaultRowID, VaultSpawns, EncounterWaveTrigger.OnBuild) },
                RequiredPuzzleIDs = new[] { HieroglyphPuzzleID },
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>Room 1: the open desert approach, buried under drifting sand.</summary>
        private void BuildRoom1ShiftingDunes() {
            BuildRoomBackground("BG_Room1", Room1StartX, 3200, LevelHeight, new Color(0.20f, 0.15f, 0.07f, 0.35f));
            // GR-LV3: the floor stops at the Dune Crossing's west deck; BuildDuneCrossing lays the rest.
            BuildFloor(Room1StartX, SurfaceGroundY, CrossingWestDeckEndX - Room1StartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(600, 560, 240);
            BuildPlatform(1100, 430, 200);
            BuildPlatform(1700, 570, 260);

            BuildCheckpoint(200, SurfaceEnemyY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "egypt_room_dunes", new Color(0.95f, 0.82f, 0.42f));
            BuildRoomTransition("egypt_room_dunes", new Vector2(300, 500), Room1CameraBounds,
                new Vector2(80, 1080));
        }

        /// <summary>
        /// Room 2: Octavian's siege line. The surface simply ends here - the ground
        /// gives way into a collapsed burial shaft, which is the route the design
        /// wants (down and around, away from the inner chambers).
        /// </summary>
        private void BuildRoom2SiegeLine() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.18f, 0.13f, 0.06f, 0.4f));
            BuildFloor(Room2StartX, SurfaceGroundY, SurfaceEndX - Room2StartX);

            BuildPlatform(3500, 560, 240);
            BuildPlatform(3950, 430, 200);
            BuildPlatform(4500, 570, 260);
            BuildPlatform(5100, 480, 220);
            BuildPlatform(5450, 600, 200);

            // Scorched plasma scars where the legion's new weapons have been tested.
            BuildHazardSpikes(3300, 690, 140);
            BuildHazardSpikes(5000, 690, 120);

            // GU-L8: the Middle anchor stands at the shaft lip, past the siege line's
            // exit barrier (it used to stand at 6520, inside the tomb fight).
            BuildCheckpoint(MiddleCheckpointX, SurfaceEnemyY, Checkpoint1, CheckpointRole.Middle);

            BuildRoomDecoration(Room2StartX, "egypt_room_siege", new Color(0.95f, 0.55f, 0.30f));
            // GU-L8: just inside the siege line's area (it was centred on the 3200
            // seam), so a hero pressed against the dunes' exit barrier does not reach
            // it. The siege-line wave is the lock's; its proximity trigger is gone.
            BuildRoomTransition("egypt_room_siege", new Vector2(SiegeLineLockMinX + 40f, 500), Room2CameraBounds,
                new Vector2(80, 1080),
                onEntered: _ => RefreshObjective());
        }

        /// <summary>
        /// Room 3: the vertical descent and the tomb network. The shaft drops ~920 px
        /// on drop-through ledges; the corridor below runs east to the sealed vault.
        /// </summary>
        private void BuildRoom3TombNetwork() {
            BuildRoomBackground("BG_Room3", Room3StartX, 2560, LevelHeight, new Color(0.07f, 0.06f, 0.07f, 0.5f));

            // The descent: drop-through ledges zig-zagging down the burial shaft.
            BuildOneWayPlatform(5860, 900, 220);
            BuildOneWayPlatform(6160, 1080, 220);
            BuildOneWayPlatform(5900, 1260, 220);
            BuildOneWayPlatform(6220, 1440, 220);

            // Tomb corridor floor: runs unbroken beneath the shaft and the vault.
            BuildFloor(SurfaceEndX, TombFloorY, LevelWidth - SurfaceEndX, TombFloorColor);
            // GU-L8: the corridor's west end, under the surface. The floor starts at
            // the shaft's west edge and nothing stood west of it, so a hero walking or
            // knocked off its end fell out of the level; the wall runs from the surface
            // top down past the corridor floor's underside.
            BuildWall(ShaftWestWallX, SurfaceGroundY, TombFloorY + DefaultFloorThickness - SurfaceGroundY, TombFloorColor);
            // Corridor ceiling. Starts east of the shaft so the descent stays open.
            BuildFloor(ShaftEndX, TombCeilingY, VaultGateX - ShaftEndX, TombFloorColor);

            // Side alcove holding the second extractor. Paying the detour is what
            // makes it worth taking.
            BuildPlatform(6420, 1460, 200, TombPlatformColor);
            BuildPlatform(6700, 1300, 260, TombPlatformColor);

            // Vault ledge over the corridor holding the third extractor, and (GU-L8)
            // the carved relief that spells the glyph order, inside the vault lock.
            BuildPlatform(7180, 1420, 200, TombPlatformColor);
            BuildPlatform(7600, 1260, 240, TombPlatformColor);
            BuildCarvedRelief(CarvedReliefPosition);

            BuildRoomDecoration(Room3StartX, "egypt_room_tombs", new Color(0.55f, 0.80f, 0.90f));
            // GU-L8: the tomb and vault waves are their locks'; their proximity
            // triggers are gone. Dropping into the shaft is the descent.
            BuildRoomTransition("egypt_room_tombs", new Vector2(Room3StartX, 900), Room3CameraBounds,
                new Vector2(80, LevelHeight),
                onEntered: _ => {
                    _descended = true;
                    RefreshObjective();
                });

            // The seal itself. It reaches the corridor ceiling, so the vault cannot be
            // jumped: reading the relief is the only way through.
            _vaultDoor = BuildDoor("VaultDoor", new Vector2(VaultGateX, TombFloorY), "egypt_vault_sealed",
                new Vector2(VaultDoorWidth, TombFloorY - TombCeilingY), VaultDoorColor);
            // P5 (2026-10-04): every sealed door carries a gate column. The
            // corridor ceiling already met this one, but its roof is reachable from
            // the shaft and only the chamber ceiling's dead end kept the vault
            // shut from above; the column makes the seal the door's own, as on
            // every other level.
            BuildGateColumn(VaultGateX, VaultDoorWidth, TombCeilingY, TombFloorColor);
        }

        /// <summary>
        /// Room 4: Cleopatra's inner chamber, a royal vault cut below the palace.
        /// Sandy floor plus two stone sarcophagi as raised platforms, kept to the
        /// flanks so the Jackal Priest's teleport has open anchor space mid-floor.
        /// </summary>
        private void BuildRoom4InnerChamber() {
            BuildRoomBackground("BG_Room4_Chamber", Room4StartX, BossArenaWidth, LevelHeight,
                new Color(0.16f, 0.09f, 0.14f, 0.5f));
            // Chamber ceiling: higher than the corridor's, so the fight has headroom.
            BuildFloor(Room4StartX, ChamberCeilingY, BossArenaWidth, TombFloorColor);
            // Seals the pocket above the vault door where the two ceilings step.
            BuildWall(Room4StartX, ChamberHeadroomY, TombCeilingY - ChamberHeadroomY);
            BuildWall(LevelWidth - 20f, 0, LevelHeight);

            // Sarcophagi. Flanked deliberately: the centre floor stays clear.
            BuildPlatform(8880, 1420, 260, TombPlatformColor);
            BuildPlatform(9700, 1420, 260, TombPlatformColor);

            BuildCheckpoint(PreBossCheckpointX, TombEnemyY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "egypt_room_chamber", new Color(0.95f, 0.35f, 0.45f));
            BuildRoomTransition("egypt_room_chamber", new Vector2(Room4StartX + 40, 1200), Room4CameraBounds,
                new Vector2(80, 1080));

            // Package 13 W4 (S35): the throne room, held for a queen three nights gone.
            // Before the boss reveal (BossArenaWestX), so the two sequences never collide.
            // GU-L8: east of the moved PreBoss anchor's respawn point.
            BuildAbsenceTrigger(new Vector2(AbsenceTriggerX, TombFloorY - 200));

            BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, TombEnemyY),
                "jackal_priest_encounter", BossArenaBounds);
        }

        /// <summary>
        /// The discoverable half of the hieroglyph puzzle: a wall relief in the tomb
        /// alcove listing the activation order. Placeholder signage; Package 8
        /// replaces it with an actual carved surface.
        /// </summary>
        private void BuildCarvedRelief(Vector2 position) {
            var label = new Label {
                Name = "CarvedOrderRelief",
                Text = Tr("egypt_relief_order"),
                Position = position + new Vector2(-240, -40),
                CustomMinimumSize = new Vector2(480, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            label.AddThemeColorOverride("font_color", GlyphTextColor);
            AddChild(label);
        }

        // === Hieroglyph vault seal ===

        protected override void OnLevelReady() {
            CollectDeepSand();
            WireHieroglyphLock();
            RefreshObjective();
        }

        /// <summary>
        /// Picks up the scene-authored sand drifts. Their signage is authored, not
        /// patched: each instance sets <c>LabelKey = "egypt_deep_sand"</c> on the
        /// template root, which the component resolves through
        /// <see cref="ToolkitLabel"/>. (Before the Wave B integration the template
        /// shipped a hardcoded English label and this method rewrote it.)
        /// </summary>
        private void CollectDeepSand() {
            _deepSand.Clear();
            Godot.Collections.Array<Node> children = GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is MovementDampenerZone zone) _deepSand.Add(zone);
            }
        }

        private void WireHieroglyphLock() {
            _hieroglyphPuzzle = GetNodeOrNull<PuzzleManager>("HieroglyphPuzzle");
            _hieroglyphLock = GetNodeOrNull<SequenceLock>("HieroglyphLock");
            if (_hieroglyphPuzzle == null || _hieroglyphLock == null) {
                GD.PushError("Level 8: the authored 'HieroglyphPuzzle'/'HieroglyphLock' nodes are missing.");
                return;
            }

            LabelGlyphs();
            _hieroglyphPuzzle.PuzzleCompleted += OnHieroglyphPuzzleCompleted;
            _hieroglyphLock.SequenceAdvanced += _ => RefreshObjective();
            _hieroglyphLock.SequenceReset += RefreshObjective;

            // Two ways the vault must already be open: the save carries the completion
            // (PersistCompletionToSave), or the run resumed at the pre-boss checkpoint,
            // which stands PAST the door. Either way a sealed door walls the player in.
            if (_hieroglyphPuzzle.IsCompleted || ResumedCheckpointID == Checkpoint2) OpenVault();
        }

        /// <summary>Names each glyph on the wall so the alcove relief can be matched to it.</summary>
        private void LabelGlyphs() {
            foreach ((string nodeName, string labelKey, int _) in GlyphPlan) {
                if (_hieroglyphLock.GetNodeOrNull<SequenceGlyph>(nodeName) is not SequenceGlyph glyph) continue;
                if (glyph.HasNode("GlyphName")) continue;
                var label = new Label {
                    Name = "GlyphName",
                    Text = Tr(labelKey),
                    Position = new Vector2(-70, 68),
                    CustomMinimumSize = new Vector2(140, 18),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                label.AddThemeFontSizeOverride("font_size", 11);
                label.AddThemeColorOverride("font_color", GlyphTextColor);
                glyph.AddChild(label);
            }
        }

        /// <summary>
        /// Idempotent by construction: <see cref="StoryLevelControllerBase.OpenDoor"/>
        /// nulls the reference. Required - <see cref="PuzzleManager"/> re-emits
        /// <c>PuzzleCompleted</c> one deferred frame after _Ready for a completion it
        /// restored from the save, so a resumed run runs this handler twice.
        /// </summary>
        private void OnHieroglyphPuzzleCompleted(string puzzleID) {
            OpenVault();
            RefreshObjective();
        }

        private void OpenVault() => OpenDoor(ref _vaultDoor);

        /// <summary>
        /// Test/diagnostic entry point: presses the glyphs in the carved order,
        /// exactly as four player interactions would.
        /// </summary>
        public bool SolveHieroglyphLockForTest() {
            if (_hieroglyphLock == null) return false;
            for (int order = 0; order < _hieroglyphLock.GlyphCount; order++) {
                SequenceGlyph glyph = GlyphForOrder(order);
                if (glyph == null || !_hieroglyphLock.Activate(glyph)) return false;
            }
            return _hieroglyphLock.IsCompleted;
        }

        /// <summary>The glyph whose authored <c>OrderIndex</c> is <paramref name="order"/>.</summary>
        public SequenceGlyph GlyphForOrder(int order) {
            if (_hieroglyphLock == null) return null;
            foreach (SequenceGlyph glyph in _hieroglyphLock.Glyphs) {
                if (glyph.OrderIndex == order) return glyph;
            }
            return null;
        }

        // === Encounters ===
        //
        // GU-L8 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles, never
        // more than the table. Concurrency is at most 2 + the swarm's 8 live standards, well
        // inside the level_08 pool config's warm counts (standard_enemy 18,
        // enemy_projectile 20).

        // === Resume ===

        // Package 12 W2 (GAP-13): explicit encounter baseline. GU-L8: the encounters
        // are the four locks; every anchor lists every lock before it, and a resume
        // builds those Cleared (the dune wave used to respawn behind the hero on
        // every resume).
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockDunes, LockSiegeLine },
                [Checkpoint2] = new[] { LockDunes, LockSiegeLine, LockTomb, LockVault }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>
        /// The anchor's non-encounter world state; the locks come from the baseline.
        /// GU-L8: the Middle anchor now stands on the surface at the shaft lip, so only
        /// the PreBoss anchor is past the descent (and past the vault door, which
        /// <see cref="WireHieroglyphLock"/> opens for it).
        /// </summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint2) _descended = true;
        }

        /// <summary>GU-L8: a cleared or reverted lock hands the objective back to the level's own.</summary>
        protected override void RestoreLevelObjective() => RefreshObjective();

        // === Boss beat: defeat -> authored Cleopatra scene -> exit -> results ===

        /// <summary>
        /// The authored Cleopatra scene (design-godot.md 3368-3374) runs between the
        /// Jackal Priest dying and the ordinary exit beat. Declaring it here is the
        /// whole of it: the base plays the beat after its usual defeat delay, waits
        /// for it, then starts the exit sequence — and keeps ownership of
        /// <c>IsBossDefeated</c>, the completion objective, and the dust tally.
        /// (Before the Wave B integration this level overrode <c>OnBossDefeated</c>
        /// wholesale and lost all three.)
        /// </summary>
        protected override IReadOnlyList<string> PostBossDialogueIDs =>
            _postBossBeats ??= new[] { PostBossDialogueID };

        private string[] _postBossBeats;

        // === Objectives ===

        private void RefreshObjective() {
            if (IsBossDefeated) {
                SetObjective(CompletionObjectiveKey);
            } else if (VaultDoorOpen && _descended) {
                SetObjective("egypt_objective_chamber");
            } else if (_descended && _hieroglyphLock != null && _hieroglyphLock.Progress > 0) {
                SetObjective("egypt_objective_seal", _hieroglyphLock.Progress, _hieroglyphLock.GlyphCount);
            } else if (_descended) {
                SetObjective("egypt_objective_tombs");
            } else {
                SetObjective(InitialObjectiveKey);
            }
        }
    }
}
