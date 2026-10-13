using Godot;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 11 - Gettysburg, 1863. The campaign's most deliberately linear level
    /// (design-godot.md 99-102): a single west-to-east assault across 11,520 px of
    /// open battlefield in four rooms - the Seminary Ridge approach, the Wheatfield,
    /// the Angle on Cemetery Ridge, and the Siege Cannon's railcut.
    ///
    /// <para><b>Era identity 1 - the cover/artillery beat.</b> The cult has given the
    /// Confederate line laser-guided artillery, modelled as ten
    /// <see cref="StoryCyclicHazard"/> impact columns standing on the advance line.
    /// Every one telegraphs for roughly two seconds (the interval the design
    /// quantifies) before it fires, and <see cref="CoverPositions"/> authors a split-rail
    /// fence, sandbag redoubt, or wrecked cannon into <em>every</em> gap between two
    /// consecutive impacts - never inside one. That is the level's rhythm: read the
    /// telegraph, hold at cover, advance on the beat. `Level11ContentTests` pins the
    /// invariant, so a later layout pass cannot turn the field into a gauntlet.</para>
    ///
    /// <para><b>Era identity 2 - the shielding arrays, and how they differ from
    /// Orléans.</b> Level 2 uses the same <see cref="ShieldGeneratorTower"/> component
    /// as a <em>wall</em>: its barriers seal the corridor the player must walk through,
    /// so breaking a generator is how you pass. Here the arrays are an <em>offensive
    /// objective</em>. Both stand on a raised earthwork gallery (the Union assault lane)
    /// at <see cref="CrestTopY"/>, well above the player's walking band, and each pane
    /// seals that lane rather than the ground route below it. The player's advance is
    /// never physically blocked by a pane - it is blocked by the Confederate
    /// breastwork, and the Union engineers only drop that once <em>both</em> arrays are
    /// down. So the arrays are reached by climbing off the safe line and crossing
    /// artillery-swept ground, and destroying them opens the advance (and, incidentally,
    /// the two extractors parked further along each lane).</para>
    ///
    /// Locked encounter economy (docs/DUST_ECONOMY.md, DustEconomyTests row 11):
    /// 12 standards / 1 elite / 1 boss / 3 extractors. The authored tables below are the
    /// single source of those counts.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L8; the Level 2
    /// pattern): Seminary Ridge (300-3200); the Wheatfield (3220-6090), which also
    /// needs array Alpha down; and the Angle (6400-8890), the ridge line, the stone
    /// wall pair arriving when the line thins and the Cyber-Cavalry Commander last,
    /// whose exit barrier fills the breastwork under its column and opens on both
    /// arrays down <i>and</i> the Angle cleared (the breastwork itself still opens on
    /// the arrays alone). The artillery stays live through every lock. Six spawns
    /// that stood inside a cover piece moved clear of it, and the Middle anchor moved
    /// off the sandbags in the Wheatfield fight to the gap before the Angle (6340).</para>
    /// </summary>
    public partial class Level11Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_11_gettysburg";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/siege_cannon.tres";

        /// <summary>
        /// P1 (2026-10-04): the railcut. West edge is <see cref="BossArenaWestX"/>
        /// (spawn 10900 - 900); east edge is the railcut wall's inner face.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, 11500f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10900f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;
        public const string DialogueResourcePath = "res://resources/Dialogue/level_11_dialogue.tres";

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.Gettysburg;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 360f;
        public override string LevelTitleKey => "gettysburg_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(200, EnemyGroundY);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the west end of the Wheatfield crest (crest deck, top 560), short of the
        // Alpha shield array — reached up the drop-through steps at 4650-4920; the
        // floor route never climbs it.
        protected override Vector2? SecretCachePosition => new(5150f, 465f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (9060) on the railcut floor, 600 px short of the arena (10000).
        protected override Vector2? RestorationFontPosition => new(9400f, GroundY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "gettysburg_objective_advance";
        protected override string BossObjectiveKey => "gettysburg_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "gettysburg_objective_complete";

        // === Layout ===

        /// <summary>Wider than the usual campaign level: the assault is the point.</summary>
        public const float LevelWidth = 11520f;
        public const float LevelHeight = 1080f;
        public const float GroundY = 900f;
        public const float EnemyGroundY = 850f;

        public const float Room1StartX = 0f;
        public const float Room2StartX = 3200f;
        public const float Room3StartX = 6400f;
        public const float Room4StartX = 8960f;

        /// <summary>
        /// Walking surface of the raised Union assault lane (the earthwork gallery). Set
        /// high enough that the gallery's underside clears the tallest piece of cover on
        /// the ground route below it - the player must never be able to head-butt the
        /// gallery while taking cover under it.
        /// </summary>
        public const float CrestTopY = 560f;
        private const float CrestDeckThickness = 32f;

        /// <summary>
        /// A ShieldGeneratorTower's collision box is 112 px tall above its origin, so an
        /// array standing on the gallery deck sits exactly this high.
        /// </summary>
        public const float ArrayY = CrestTopY - 112f;

        /// <summary>
        /// ShieldGeneratorTowerTemplate hangs its 48x320 pane at a fixed local offset of
        /// (320, -48), which is not overridable without a fragile `index=` block on an
        /// instanced scene (see the L02 deviation). Every derived span below follows from
        /// it, and the content test asserts the authored scene positions against them.
        /// </summary>
        public const float BarrierLocalOffsetX = 320f;

        /// <summary>World-space centre of a pane held by an array standing at <see cref="ArrayY"/>.</summary>
        public const float BarrierCenterY = ArrayY - 48f;
        public const float BarrierHalfHeight = 160f;
        /// <summary>The template pane's width (its 48x320 shape).</summary>
        private const float BarrierPaneWidth = 48f;

        /// <summary>Top of the pane's seal. The gallery roof mass fills everything above it.</summary>
        public const float LaneRoofBottomY = BarrierCenterY - BarrierHalfHeight;   // 300
        /// <summary>Bottom of the pane's seal, flush with the gallery deck.</summary>
        public const float LaneSealBottomY = BarrierCenterY + BarrierHalfHeight;   // 620

        /// <summary>
        /// Headroom the player occupies while walking the ground route. Nothing an array
        /// holds up may intrude on this band - that is the whole difference from Orléans.
        /// </summary>
        public const float PlayerAdvanceHeadroom = 200f;

        public const float ArrayAlphaX = 5400f;
        public const float ArrayBravoX = 7500f;
        public const float LaneAlphaPaneX = ArrayAlphaX + BarrierLocalOffsetX;   // 5720
        public const float LaneBravoPaneX = ArrayBravoX + BarrierLocalOffsetX;   // 7820
        public const int ArrayCount = 2;

        /// <summary>Confederate breastwork sealing the railcut until the Union line moves.</summary>
        public const float BreastworkX = 8900f;
        private const float BreastworkWidth = 48f;
        private const float BreastworkHeight = 460f;

        /// <summary>
        /// Width of the earthwork roof mass over each lane's pane (the pane is 48
        /// wide; the roof overhangs it 46 px each side).
        /// </summary>
        private const float LaneRoofWidth = 140f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 3200, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 3200, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX, 0, 2560, LevelHeight);

        // === Artillery and cover ===

        /// <summary>
        /// X of every laser-guided artillery impact column, authored as
        /// CyclicHazardTemplate instances in the scene. The controller owns the table so
        /// the test can prove the scene has not drifted from it.
        /// </summary>
        public static readonly float[] ArtilleryImpactPoints = {
            1150f, 1900f, 3650f, 4200f, 4750f, 5300f, 5850f, 6620f, 7150f, 7700f
        };

        /// <summary>Half-width of a CyclicHazardTemplate blast column (its shape is 120 px wide).</summary>
        public const float ArtilleryBlastHalfWidth = 60f;

        /// <summary>
        /// The design calls for roughly two seconds of warning before a shell lands.
        /// Every authored lane clears this floor and the content test enforces it - a
        /// mortar with no readable telegraph turns the advance into memorisation.
        /// </summary>
        public const float MinTelegraphSeconds = 1.8f;

        public enum CoverKind { SplitRailFence, Sandbags, CannonWreck }

        /// <summary>
        /// Authored cover. Exactly one piece sits in every gap between two consecutive
        /// impact columns, and none of them stands inside a column. That pairing is the
        /// level's fairness contract, not decoration.
        /// </summary>
        public static readonly (float X, CoverKind Kind)[] CoverPositions = {
            (600f, CoverKind.SplitRailFence),
            (1520f, CoverKind.SplitRailFence),
            (2600f, CoverKind.Sandbags),
            (3925f, CoverKind.SplitRailFence),
            (4475f, CoverKind.CannonWreck),
            (5025f, CoverKind.Sandbags),
            (5575f, CoverKind.SplitRailFence),
            (6200f, CoverKind.Sandbags),
            (6885f, CoverKind.Sandbags),
            (7425f, CoverKind.SplitRailFence),
            (8180f, CoverKind.CannonWreck)
        };

        /// <summary>Tallest authored cover, measured from <see cref="GroundY"/>.</summary>
        public const float TallestCoverHeight = 140f;

        public static (float Width, float Thickness, Color Fill) CoverProfile(CoverKind kind) => kind switch {
            CoverKind.SplitRailFence => (200f, 90f, new Color(0.42f, 0.32f, 0.20f)),
            CoverKind.Sandbags => (180f, 120f, new Color(0.46f, 0.42f, 0.30f)),
            _ => (160f, 140f, new Color(0.26f, 0.24f, 0.26f))
        };

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>
        /// Era roster: `laser_rifle_infantry` (the Gettysburg standard - fast beam-bolts,
        /// which is why this level carries the campaign's highest `enemy_projectile` warm
        /// count) mixed with `chrono_slasher` (Severed shock-trooper standard, melee).
        /// <para>GU-L8 (2026-10-12): every spawn and patrol point stands clear of every
        /// cover piece (a body inside a cover's span was pushed up onto it): the first
        /// rifleman moved 1500 -> 1320, west of the 1520 fence.</para>
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room1Spawns = {
            ("laser_rifle_infantry", new Vector2(1320, EnemyGroundY), new Vector2(1240, EnemyGroundY), new Vector2(1390, EnemyGroundY)),
            ("chrono_slasher", new Vector2(2150, EnemyGroundY), new Vector2(2050, EnemyGroundY), new Vector2(2280, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(2900, EnemyGroundY), null, null)
        };

        /// <summary>
        /// The Wheatfield firefight: four live standards, three of them ranged. GU-L8:
        /// all four stood inside a cover piece; each now holds the gap just behind
        /// (east of) the cover it stood in, short of the next blast column — 3900 ->
        /// 4080, 4500 -> 4620, 5000 -> 5170, 5600 -> 5730.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room2Spawns = {
            ("laser_rifle_infantry", new Vector2(4080, EnemyGroundY), new Vector2(4050, EnemyGroundY), new Vector2(4110, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(4620, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(5170, EnemyGroundY), new Vector2(5145, EnemyGroundY), new Vector2(5210, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(5730, EnemyGroundY), null, null)
        };

        /// <summary>
        /// GU-L8: the first rifleman moved 6900 -> 7040, out of the 6885 sandbags, and
        /// the slasher's patrol (7150-7370, into the 7425 fence) shrank to 7225-7300.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room3Spawns = {
            ("laser_rifle_infantry", new Vector2(7040, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(7250, EnemyGroundY), new Vector2(7225, EnemyGroundY), new Vector2(7300, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(7700, EnemyGroundY), null, null)
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] AngleSpawns = {
            ("laser_rifle_infantry", new Vector2(8400, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(8700, EnemyGroundY), new Vector2(8620, EnemyGroundY), new Vector2(8820, EnemyGroundY))
        };

        /// <summary>
        /// The level's single elite. `cyber_cavalry_commander` carries
        /// `FrontalDamageReduction = 0.4`, so its post is authored under a drop-through
        /// platform that spans its whole patrol: the answer is to get above it and land
        /// behind, and a content test fails if a later layout pass removes the platform
        /// or collapses the patrol to a point.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] EliteSpawns = {
            ("cyber_cavalry_commander", new Vector2(8500, EnemyGroundY), new Vector2(8340, EnemyGroundY), new Vector2(8660, EnemyGroundY))
        };

        public const float FlankPlatformX = 8500f;
        public const float FlankPlatformY = 700f;
        public const float FlankPlatformWidth = 400f;

        /// <summary>
        /// 15 dust each and resource-owned. Two of the three sit on the raised Union lane
        /// beyond an array's pane, so sabotaging an array pays twice.
        /// </summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_11.extractor_ridge_battery", new Vector2(2750, 590)),
            ("level_11.extractor_wheatfield_lane", new Vector2(5980, 500)),
            ("level_11.extractor_angle_lane", new Vector2(8080, 500))
        };

        public static int AuthoredStandardCount =>
            Room1Spawns.Length + Room2Spawns.Length + Room3Spawns.Length + AngleSpawns.Length;

        public static int AuthoredEliteCount => EliteSpawns.Length;

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === GU-L8 (2026-10-12): the three encounter locks ====================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockSeminaryRidge = ID + "_lock_seminary_ridge";
        public const string LockWheatfield = ID + "_lock_wheatfield";
        public const string LockAngle = ID + "_lock_angle";

        /// <summary>The manifest rows the lock waves are (level_11_rewards.tres).</summary>
        public const string Room1RowID = "level_11.room1";
        public const string Room2RowID = "level_11.room2";
        public const string Room3RowID = "level_11.room3";
        public const string AngleRowID = "level_11.angle";
        public const string EliteRowID = "level_11.elite";

        private const float BarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>Seminary Ridge: the whole first room past the entry anchor (200).</summary>
        public const float SeminaryRidgeLockMinX = 300f;
        public const float SeminaryRidgeLockMaxX = Room2StartX;

        /// <summary>
        /// The Wheatfield, with array Alpha a condition: the Angle needs both arrays
        /// down and shuts the way back, so Alpha has to fall before the hero leaves
        /// this lock. Its exit barrier stands west of the 6200 sandbags.
        /// </summary>
        public const float WheatfieldLockMinX = Room2StartX + BarrierWidth;
        public const float WheatfieldLockMaxX = 6090f;

        /// <summary>
        /// The Middle anchor, in the gap between the 6200 sandbags and the Angle (it
        /// stood at 6250, on the sandbags in the Wheatfield fight): clear of every
        /// blast column, its strike surface beyond the longest basic swing from
        /// inside the Wheatfield.
        /// </summary>
        public const float MiddleCheckpointX = 6340f;

        /// <summary>
        /// The Angle: the ridge line, the stone wall and the commander, room 3 from its
        /// start to the breastwork. Its exit barrier fills the breastwork gate under
        /// the column, and it clears only on both arrays down and every member down.
        /// </summary>
        public const float AngleLockMinX = Room3StartX;
        public const float AngleLockMaxX = BreastworkX - BarrierWidth * 0.5f;

        /// <summary>
        /// The PreBoss anchor, moved 9060 -> 9080 so its strike surface (9050-9110)
        /// clears the longest basic swing of a hero pressed against the Angle's exit
        /// barrier (8870) by more than a few pixels.
        /// </summary>
        public const float PreBossCheckpointX = 9080f;

        private EncounterLock _seminaryRidgeLock;
        private EncounterLock _wheatfieldLock;
        private EncounterLock _angleLock;

        public EncounterLock SeminaryRidgeLock => _seminaryRidgeLock;
        public EncounterLock WheatfieldLock => _wheatfieldLock;
        public EncounterLock AngleLock => _angleLock;

        // === Runtime state ===

        private ShieldGeneratorTower _arrayAlpha;
        private ShieldGeneratorTower _arrayBravo;
        private StaticBody2D _breastwork;
        private int _arraysDown;
        private bool _advanceBegun;

        public int ArraysDestroyed => _arraysDown;
        public ShieldGeneratorTower ShieldArrayAlpha => _arrayAlpha;
        public ShieldGeneratorTower ShieldArrayBravo => _arrayBravo;

        /// <summary>True once both arrays are down and the Union line has taken the breastwork.</summary>
        public bool UnionAdvanceOpen => _arraysDown >= ArrayCount;

        /// <summary>The railcut is reachable only after the breastwork comes down.</summary>
        public bool BreastworkOpen => _breastwork == null;

        // === Palette: powder smoke, trampled wheat, Archive cyan ===

        protected override Color FloorColor => new(0.20f, 0.19f, 0.14f);
        protected override Color FloorEdgeColor => new(0.44f, 0.42f, 0.26f);
        protected override Color PlatformColor => new(0.31f, 0.29f, 0.21f);
        protected override Color WallColor => new(0.14f, 0.13f, 0.10f);
        protected override Color HazardColor => new(0.66f, 0.26f, 0.09f);

        private static readonly Color GalleryColor = new(0.27f, 0.25f, 0.19f);
        private static readonly Color EarthworkColor = new(0.17f, 0.16f, 0.12f);
        private static readonly Color BreastworkColor = new(0.30f, 0.26f, 0.18f);
        private static readonly Color RailColor = new(0.38f, 0.36f, 0.34f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1SeminaryRidge();
            BuildRoom2Wheatfield();
            BuildRoom3TheAngle();
            BuildRoom4Railcut();
            BuildCoverLine();
            BuildExtractors(ExtractorPlacements);
            WireShieldArrays();
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L8: the three fights as encounter locks, one wave per manifest row.
        /// Built after the arrays are wired (two locks need them) and in
        /// <see cref="BuildLevel"/>, so a resume past a lock builds it Cleared with no
        /// member ever spawned. The artillery stays live through every one.
        /// </summary>
        private void BuildEncounterLocks() {
            var alpha = _arrayAlpha != null ? new DamageableEnvironmentObject[] { _arrayAlpha }
                : System.Array.Empty<DamageableEnvironmentObject>();
            var bothArrays = new List<DamageableEnvironmentObject>();
            if (_arrayAlpha != null) bothArrays.Add(_arrayAlpha);
            if (_arrayBravo != null) bothArrays.Add(_arrayBravo);

            _seminaryRidgeLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockSeminaryRidge,
                Area = EncounterLockSpec.Span(SeminaryRidgeLockMinX, SeminaryRidgeLockMaxX, LevelHeight),
                EntryBarrierX = SeminaryRidgeLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = SeminaryRidgeLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = GroundY,
                Waves = new[] { EncounterLockWave.FromTable(Room1RowID, Room1Spawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The riflemen hold the Wheatfield's cover from the start. Its objective
            // is the level's own (the array count); the HUD counts the riflemen.
            _wheatfieldLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockWheatfield,
                Area = EncounterLockSpec.Span(WheatfieldLockMinX, WheatfieldLockMaxX, LevelHeight),
                EntryBarrierX = WheatfieldLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = WheatfieldLockMaxX + BarrierWidth * 0.5f,
                BarrierFloorY = GroundY,
                Waves = new[] { EncounterLockWave.FromTable(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnBuild) },
                RequiredBreakables = alpha,
                GoDirection = EncounterGoDirection.Right
            });
            // The Angle: the ridge line holds the field; the stone wall pair comes
            // over the wall when the line is down to one, and the commander rides in
            // when the wall is down to one. Both arrays down and every member down
            // open the breastwork's barrier.
            var angle = EncounterLockWave.FromTable(AngleRowID, AngleSpawns, EncounterWaveTrigger.WhenAliveAtMost);
            angle.AliveAtMost = 1;
            var elite = EncounterLockWave.FromTable(EliteRowID, EliteSpawns, EncounterWaveTrigger.WhenAliveAtMost);
            elite.AliveAtMost = 1;
            _angleLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockAngle,
                Area = EncounterLockSpec.Span(AngleLockMinX, AngleLockMaxX, LevelHeight),
                EntryBarrierX = AngleLockMinX - BarrierWidth * 0.5f,
                ExitBarrierX = BreastworkX,
                ExitBarrierYRange = new Vector2(GroundY - BreastworkHeight, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] {
                    EncounterLockWave.FromTable(Room3RowID, Room3Spawns, EncounterWaveTrigger.OnBuild),
                    angle,
                    elite
                },
                RequiredBreakables = bothArrays.ToArray(),
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>
        /// Room 1: the Union start line on Seminary Ridge. Two ranging shots, the first
        /// fence line, and a low battery ridge holding the first extractor.
        /// </summary>
        private void BuildRoom1SeminaryRidge() {
            BuildRoomBackground("BG_Room1", Room1StartX, 3200, LevelHeight, new Color(0.10f, 0.09f, 0.07f, 0.35f));
            BuildFloor(Room1StartX, GroundY, 3200);
            BuildWall(-20, 0, LevelHeight);

            // Modest verticality: a battery ridge, not a tower. G5 (2026-10-04):
            // the step sat at 730 (top 720), 58 px under the ridge Extractor's
            // underside (662, it hangs 14 px through the ridge platform) — inside
            // the one-way's 12 px landing margin below the 64 px hero, so a hero
            // jumping up through it there was pushed onto it and wedged. At 750
            // (top 740) it leaves 78 px. The step sits under the ridge's west
            // end, so the ridge is climbed from the sandbags in front of it
            // (2600, top 780): the play bot's simulated jumps find that route
            // for Lincoln, Joan and Cleopatra.
            BuildOneWayPlatform(2680, 750, 160);
            BuildPlatform(2750, 640, 240);

            BuildCheckpoint(200, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "gettysburg_room_seminary_ridge", new Color(0.85f, 0.78f, 0.42f));
            BuildRoomTransition("gettysburg_room_seminary_ridge", new Vector2(300, 600), Room1CameraBounds);
            // Package 13 W4 (S30): the Union headquarters tent and its silent telegraph.
            BuildAbsenceTrigger(new Vector2(1400, GroundY - 200));
        }

        /// <summary>
        /// Room 2: the Wheatfield. Five impact columns, the heaviest barrage in the level,
        /// and shielding array Alpha on the earthwork gallery above - reached by climbing
        /// off the safe line at x 4650-4790, which is squarely inside the 4750 column.
        /// </summary>
        private void BuildRoom2Wheatfield() {
            BuildRoomBackground("BG_Room2", Room2StartX, 3200, LevelHeight, new Color(0.09f, 0.09f, 0.06f, 0.40f));
            BuildFloor(Room2StartX, GroundY, 3200);

            BuildUnionLane("Alpha", 5000f, 6200f, LaneAlphaPaneX);
            BuildOneWayPlatform(4650, 830, 180);
            BuildOneWayPlatform(4790, 740, 180);
            BuildOneWayPlatform(4920, 650, 160);

            // GU-L8: the Middle anchor, past the Wheatfield lock and off the sandbags
            // (it stood at 6250, on the 6200 sandbags in the Wheatfield fight).
            BuildCheckpoint(MiddleCheckpointX, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "gettysburg_room_wheatfield", new Color(0.92f, 0.68f, 0.30f));
            // GU-L8: just inside the Wheatfield lock's area (it was centred on the 3200
            // seam), so a hero pressed against the ridge's exit barrier does not reach
            // it. The Wheatfield wave is its lock's; the proximity trigger is gone.
            BuildRoomTransition("gettysburg_room_wheatfield", new Vector2(WheatfieldLockMinX + 40f, 600), Room2CameraBounds,
                onEntered: _ => {
                    _advanceBegun = true;
                    RefreshObjective();
                });
        }

        /// <summary>
        /// Room 3: the Angle on Cemetery Ridge. Array Bravo's gallery runs most of the
        /// room; past its east end the stone wall holds the Cyber-Cavalry Commander, and
        /// the Confederate breastwork seals the railcut beyond.
        /// </summary>
        private void BuildRoom3TheAngle() {
            BuildRoomBackground("BG_Room3", Room3StartX, 2560, LevelHeight, new Color(0.10f, 0.09f, 0.09f, 0.40f));
            BuildFloor(Room3StartX, GroundY, 2560);

            BuildUnionLane("Bravo", 6820f, 8200f, LaneBravoPaneX);
            BuildOneWayPlatform(6480, 830, 180);
            BuildOneWayPlatform(6620, 740, 180);
            BuildOneWayPlatform(6740, 650, 160);

            // The commander's post: flank it from this platform, never head-on.
            BuildOneWayPlatform(FlankPlatformX, FlankPlatformY, FlankPlatformWidth);

            BuildRoomDecoration(Room3StartX, "gettysburg_room_angle", new Color(0.62f, 0.78f, 0.92f));
            // GU-L8: just inside the Angle lock's area (it stood at 6420, across the
            // lock's entry barrier). The ridge line, the stone wall pair and the
            // commander are the Angle lock's waves; their proximity triggers are gone.
            BuildRoomTransition("gettysburg_room_angle", new Vector2(AngleLockMinX + 60, 600), Room3CameraBounds);

            BuildBreastwork();
        }

        /// <summary>
        /// Room 4: the railcut. 2,560 px of straight track - the Siege Cannon is a
        /// railcar with a 12-unit ranged band (720 px at BossController's 60 px/unit),
        /// the widest in the roster, and it needs room to use it.
        /// </summary>
        private void BuildRoom4Railcut() {
            BuildRoomBackground("BG_Room4_Boss", Room4StartX, 2560, LevelHeight, new Color(0.15f, 0.08f, 0.06f, 0.45f));
            BuildFloor(Room4StartX, GroundY, 2560);
            BuildRail(Room4StartX, 2560);

            BuildPlatform(9700, 690, 260);
            BuildPlatform(10800, 690, 260);
            // The top perch, a double jump up from either side platform. Widened
            // 220 -> 340 by the 2026-10-12 20% movement cut (GU-C): the 310 px gap
            // to it left Lincoln (now 228 px/s) a 13 px, frame-perfect margin, and
            // the bots' arc planner none. 250 px is the old gap scaled by 0.8.
            BuildPlatform(10250, 480, 340);

            BuildWall(11500, 0, LevelHeight);

            BuildCheckpoint(PreBossCheckpointX, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "gettysburg_room_railcut", new Color(0.95f, 0.33f, 0.24f));
            BuildRoomTransition("gettysburg_room_railcut", new Vector2(Room4StartX + 30, 540), Room4CameraBounds);

            BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, EnemyGroundY),
                "siege_cannon_encounter", BossArenaBounds);
        }

        /// <summary>
        /// One raised Union assault lane: a gallery deck the arrays stand on, plus the
        /// earthwork roof above the pane so the pane genuinely fills the lane's
        /// cross-section rather than being a jumpable ornament (the L02 lesson). The deck
        /// sits far enough above <see cref="GroundY"/> that the player's own advance
        /// passes underneath it untouched.
        /// </summary>
        private void BuildUnionLane(string suffix, float startX, float endX, float paneX) {
            float width = endX - startX;
            BuildPlatform(startX + width / 2f, CrestTopY + CrestDeckThickness / 2f, width,
                GalleryColor, CrestDeckThickness);
            // Roof mass filling everything above the pane's top edge. P5
            // (2026-10-04): it used to stop at y 0, the top of the level, and a
            // strong double jump off the array's own top (y 336) cleared it
            // without breaking the array; it is a gate column now, rising past the
            // roster's reach. Same 140 px span, centred on the pane.
            BuildGateColumn(paneX, BarrierPaneWidth, LaneRoofBottomY, EarthworkColor,
                margin: (LaneRoofWidth - BarrierPaneWidth) / 2f);
            BuildLaneLabel($"Lane{suffix}Label", new Vector2(paneX, LaneRoofBottomY + 40f));
        }

        /// <summary>Confederate breastwork: objective-gated, not barrier-gated.</summary>
        private void BuildBreastwork() {
            _breastwork = BuildDoor("BreastworkGate", new Vector2(BreastworkX, GroundY),
                "gettysburg_gate_sealed", new Vector2(BreastworkWidth, BreastworkHeight), BreastworkColor);
            // Solid earth above the gate, or the whole objective is jumped. P5
            // (2026-10-04): the earth used to stop at y 0, the top of the level,
            // and the Story levels have no sky — from the Angle lane's gallery
            // deck (top 560) or its Extractor a double jump and an upward
            // Relativity Warp rise past y 0, so the breastwork could be crossed
            // with both arrays standing. It is a gate column now, rising past
            // anything the roster can reach.
            BuildGateColumn(BreastworkX, BreastworkWidth, GroundY - BreastworkHeight, EarthworkColor);
        }

        private void BuildCoverLine() {
            foreach ((float x, CoverKind kind) in CoverPositions) {
                (float width, float thickness, Color fill) = CoverProfile(kind);
                StaticBody2D cover = BuildPlatform(x, GroundY - thickness / 2f, width, fill, thickness);
                cover.Name = $"Cover_{kind}_{Mathf.RoundToInt(x)}";
                cover.AddToGroup("battlefield_cover");
            }
        }

        /// <summary>Non-colliding rail track under the boss arena; presentation only.</summary>
        private void BuildRail(float x, float width) {
            AddChild(new ColorRect {
                Name = "RailTrack",
                Size = new Vector2(width, 6),
                Position = new Vector2(x, GroundY - 6),
                Color = RailColor,
                ZIndex = -5,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }

        /// <summary>Placeholder lane signage; production art lands in Package 8.</summary>
        private Label BuildLaneLabel(string name, Vector2 position) {
            var label = new Label {
                Name = name,
                Text = Tr("gettysburg_lane_shielded"),
                Position = position + new Vector2(-160, 0),
                CustomMinimumSize = new Vector2(320, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 1f));
            AddChild(label);
            return label;
        }

        // === Shielding arrays ===

        private void WireShieldArrays() {
            _arrayAlpha = GetNodeOrNull<ShieldGeneratorTower>("ShieldArrayAlpha");
            _arrayBravo = GetNodeOrNull<ShieldGeneratorTower>("ShieldArrayBravo");
            NameBarriers(_arrayAlpha);
            NameBarriers(_arrayBravo);
            if (_arrayAlpha != null) _arrayAlpha.TowerDestroyed += OnShieldArrayDestroyed;
            if (_arrayBravo != null) _arrayBravo.TowerDestroyed += OnShieldArrayDestroyed;
        }

        private static void NameBarriers(ShieldGeneratorTower array) {
            if (array == null) return;
            int index = 0;
            foreach (ForcefieldBarrier barrier in array.Barriers()) {
                barrier.BarrierID = $"{array.ObjectID}_pane_{index++}";
            }
        }

        private void OnShieldArrayDestroyed(string arrayID) {
            _arraysDown++;
            if (UnionAdvanceOpen) OpenBreastwork();
            RefreshObjective();
        }

        /// <summary>Idempotent: <see cref="OpenDoor"/> nulls the reference on the first call.</summary>
        private void OpenBreastwork() => OpenDoor(ref _breastwork);

        /// <summary>
        /// Resume helper. Driving the array through the same
        /// <see cref="DamageableEnvironmentObject.TakeEnvironmentDamage"/> path the
        /// player's attacks take keeps the pane state, the objective counter, and the
        /// breastwork all agreeing with a run that really broke it (the L02 precedent).
        /// </summary>
        private static void ForceArrayDestroyed(ShieldGeneratorTower array) {
            if (array == null || !IsInstanceValid(array) || array.IsDestroyed) return;
            array.TakeEnvironmentDamage(array.MaxHP);
        }

        // === Encounters ===
        //
        // GU-L8 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles, never more
        // than the table. Peak concurrency is four live standards (the Wheatfield) or
        // the ridge line's last rifleman plus the stone wall pair, then one of those
        // plus the commander (the Angle), all well inside the level_11 pool config's
        // warm counts of 14 standard / 4 elite / 28 enemy_projectile.

        // === Resume and objectives ===

        // Package 12 W2 (GAP-13): explicit encounter baseline. GU-L8: the encounters
        // are the three locks; every anchor lists every lock before it, and a resume
        // builds those Cleared (the ridge wave used to respawn behind the hero on
        // every resume).
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockSeminaryRidge, LockWheatfield },
                [Checkpoint2] = new[] { LockSeminaryRidge, LockWheatfield, LockAngle }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>GU-L8: a cleared or reverted lock hands the objective back to the level's own.</summary>
        protected override void RestoreLevelObjective() => RefreshObjective();

        /// <summary>The anchor's non-encounter world state; the locks come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint1) {
                _advanceBegun = true;
                // GU-L8: the Middle anchor stands past the Wheatfield lock, which
                // needs array Alpha down, so a resume there finds Alpha broken (the
                // Level 2 generator precedent). Bravo is still the Angle's to break.
                ForceArrayDestroyed(_arrayAlpha);
            } else if (checkpointID == Checkpoint2) {
                _advanceBegun = true;
                // Checkpoint 2 stands east of the breastwork, so a resume that lost the
                // array state would strand the player in the railcut behind a sealed
                // gate with no way back to the objective that opens it.
                ForceArrayDestroyed(_arrayAlpha);
                ForceArrayDestroyed(_arrayBravo);
                OpenBreastwork();
            }
        }

        // Resume camera confinement is handled by
        // StoryLevelControllerBase.ApplyResumeCameraBounds (Wave A integration).
        protected override void OnLevelReady() {
            if (UnionAdvanceOpen) OpenBreastwork();
            RefreshObjective();
        }

        private void RefreshObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (UnionAdvanceOpen) SetObjective("gettysburg_objective_breach");
            else if (_advanceBegun || _arraysDown > 0) SetObjective("gettysburg_objective_arrays", _arraysDown, ArrayCount);
            else SetObjective(InitialObjectiveKey);
        }

        // === Authoring invariants (shared with the content test) ===

        /// <summary>True when <paramref name="x"/> stands inside an artillery blast column.</summary>
        public static bool IsInsideBlastColumn(float x) {
            foreach (float impact in ArtilleryImpactPoints) {
                if (Mathf.Abs(x - impact) < ArtilleryBlastHalfWidth) return true;
            }
            return false;
        }

        /// <summary>
        /// Every gap between two consecutive impact columns that holds no cover. The
        /// advance is only fair if this is always empty.
        /// </summary>
        public static IReadOnlyList<(float From, float To)> UncoveredArtilleryGaps() {
            var impacts = new List<float>(ArtilleryImpactPoints);
            impacts.Sort();
            var gaps = new List<(float, float)>();
            for (int index = 0; index + 1 < impacts.Count; index++) {
                float from = impacts[index];
                float to = impacts[index + 1];
                bool covered = false;
                foreach ((float x, CoverKind _) in CoverPositions) {
                    if (x > from && x < to && !IsInsideBlastColumn(x)) {
                        covered = true;
                        break;
                    }
                }
                if (!covered) gaps.Add((from, to));
            }
            return gaps;
        }
    }
}
