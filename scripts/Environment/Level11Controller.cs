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
    /// </summary>
    public partial class Level11Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_11_gettysburg";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/siege_cannon.tres";
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
        private const float BreastworkHeight = 460f;

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
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room1Spawns = {
            ("laser_rifle_infantry", new Vector2(1500, EnemyGroundY), new Vector2(1400, EnemyGroundY), new Vector2(1620, EnemyGroundY)),
            ("chrono_slasher", new Vector2(2150, EnemyGroundY), new Vector2(2050, EnemyGroundY), new Vector2(2280, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(2900, EnemyGroundY), null, null)
        };

        /// <summary>The Wheatfield firefight: four live standards, three of them ranged.</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room2Spawns = {
            ("laser_rifle_infantry", new Vector2(3900, EnemyGroundY), new Vector2(3820, EnemyGroundY), new Vector2(4000, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(4500, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(5000, EnemyGroundY), new Vector2(4900, EnemyGroundY), new Vector2(5120, EnemyGroundY)),
            ("laser_rifle_infantry", new Vector2(5600, EnemyGroundY), null, null)
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room3Spawns = {
            ("laser_rifle_infantry", new Vector2(6900, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(7250, EnemyGroundY), new Vector2(7150, EnemyGroundY), new Vector2(7370, EnemyGroundY)),
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

        // === Runtime state ===

        private ShieldGeneratorTower _arrayAlpha;
        private ShieldGeneratorTower _arrayBravo;
        private StaticBody2D _breastwork;
        private int _arraysDown;
        private bool _advanceBegun;
        private bool _wheatfieldWaveSpawned;
        private bool _angleWaveSpawned;
        private bool _ridgeWaveSpawned;

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
        }

        /// <summary>
        /// Room 1: the Union start line on Seminary Ridge. Two ranging shots, the first
        /// fence line, and a low battery ridge holding the first extractor.
        /// </summary>
        private void BuildRoom1SeminaryRidge() {
            BuildRoomBackground("BG_Room1", Room1StartX, 3200, LevelHeight, new Color(0.10f, 0.09f, 0.07f, 0.35f));
            BuildFloor(Room1StartX, GroundY, 3200);
            BuildWall(-20, 0, LevelHeight);

            // Modest verticality: a battery ridge, not a tower.
            BuildOneWayPlatform(2680, 730, 160);
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

            BuildCheckpoint(6250, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "gettysburg_room_wheatfield", new Color(0.92f, 0.68f, 0.30f));
            BuildRoomTransition("gettysburg_room_wheatfield", new Vector2(Room2StartX, 600), Room2CameraBounds,
                onEntered: _ => {
                    _advanceBegun = true;
                    RefreshObjective();
                });
            BuildWaveTrigger("WheatfieldWaveTrigger", new Vector2(3400, 700), TriggerWheatfieldWave);
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
            BuildRoomTransition("gettysburg_room_angle", new Vector2(Room3StartX + 20, 600), Room3CameraBounds);
            BuildWaveTrigger("AngleWaveTrigger", new Vector2(6600, 700), TriggerRidgeWave);
            BuildWaveTrigger("StoneWallWaveTrigger", new Vector2(8150, 700), TriggerAngleWave);

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
            BuildPlatform(10250, 480, 220);

            BuildWall(11500, 0, LevelHeight);

            BuildCheckpoint(9060, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "gettysburg_room_railcut", new Color(0.95f, 0.33f, 0.24f));
            BuildRoomTransition("gettysburg_room_railcut", new Vector2(Room4StartX + 30, 540), Room4CameraBounds);

            BuildBossEncounter(BossResourcePath, new Vector2(10900, EnemyGroundY),
                "siege_cannon_encounter", revealDistance: 900f);
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
            // Roof mass filling everything above the pane's top edge.
            BuildPlatform(paneX, LaneRoofBottomY / 2f, 140f, EarthworkColor, LaneRoofBottomY);
            BuildLaneLabel($"Lane{suffix}Label", new Vector2(paneX, LaneRoofBottomY + 40f));
        }

        /// <summary>Confederate breastwork: objective-gated, not barrier-gated.</summary>
        private void BuildBreastwork() {
            _breastwork = BuildDoor("BreastworkGate", new Vector2(BreastworkX, GroundY),
                "gettysburg_gate_sealed", new Vector2(48, BreastworkHeight), BreastworkColor);
            // Solid earth above the gate, or the whole objective is jumped.
            BuildPlatform(BreastworkX, (GroundY - BreastworkHeight) / 2f, 60f,
                EarthworkColor, GroundY - BreastworkHeight);
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

        protected override void SpawnInitialEnemies() => SpawnWave(Room1Spawns);

        private void TriggerWheatfieldWave() {
            if (_wheatfieldWaveSpawned) return;
            _wheatfieldWaveSpawned = true;
            SpawnWave(Room2Spawns);
        }

        private void TriggerRidgeWave() {
            if (_ridgeWaveSpawned) return;
            _ridgeWaveSpawned = true;
            SpawnWave(Room3Spawns);
        }

        private void TriggerAngleWave() {
            if (_angleWaveSpawned) return;
            _angleWaveSpawned = true;
            SpawnWave(AngleSpawns);
            SpawnWave(EliteSpawns);
        }

        /// <summary>
        /// Authored counts are the Normal-difficulty truth; ScaleEncounterCount is the
        /// only thing allowed to move them, and it never exceeds the authored table.
        /// Peak concurrency is four live standards (the Wheatfield) or two standards plus
        /// the commander (the Angle), both well inside the level_11 pool config's warm
        /// counts of 14 standard / 4 elite / 28 enemy_projectile.
        /// </summary>
        private void SpawnWave((string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] wave) {
            if (wave == null) return;
            int count = StoryDifficultyTuning.ScaleEncounterCount(
                wave.Length, StoryDifficultyTuning.CurrentStoryDifficulty);
            for (int index = 0; index < count && index < wave.Length; index++) {
                (string enemyID, Vector2 position, Vector2? patrolA, Vector2? patrolB) = wave[index];
                EnemyFactory.Spawn(enemyID, this, position, patrolA, patrolB);
            }
        }

        // === Resume and objectives ===

        // Package 12 W2 (GAP-13): explicit encounter baseline.
        public const string WheatfieldWaveEncounter = ID + "_wheatfield_wave";
        public const string RidgeWaveEncounter = ID + "_ridge_wave";
        public const string AngleWaveEncounter = ID + "_angle_wave";

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { WheatfieldWaveEncounter },
                [Checkpoint2] = new[] { WheatfieldWaveEncounter, RidgeWaveEncounter, AngleWaveEncounter }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            switch (encounterID) {
                case WheatfieldWaveEncounter: _wheatfieldWaveSpawned = true; break;
                case RidgeWaveEncounter: _ridgeWaveSpawned = true; break;
                case AngleWaveEncounter: _angleWaveSpawned = true; break;
            }
        }

        /// <summary>The anchor's non-encounter world state; the waves come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint1) {
                _advanceBegun = true;
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
