using Godot;
using System.Collections.Generic;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 2 - Orléans, 1429 (Siege of Orléans). Four rooms and a stretch across
    /// 10,560 px: the French vanguard approach, the Rampart run, the English siege line
    /// under a rolling mortar barrage, a vertical battlement climb, and the Siegemaster
    /// Duke's court.
    ///
    /// Era identity is mechanical, not decorative: the Severed have given the
    /// English two kinetic <see cref="ShieldGeneratorTower"/>s, and each one keeps a
    /// <see cref="ForcefieldBarrier"/> solid across the only route forward. The
    /// player has to break the generators to advance, which is the sabotage the
    /// design asks for. Mortar fire is a lane of telegraphed
    /// <see cref="StoryCyclicHazard"/>s (warning -> active -> cooldown), authored in
    /// the scene rather than in code.
    ///
    /// Locked encounter economy (docs/DUST_ECONOMY.md, DustEconomyTests row 2):
    /// 8 standards / 0 elites / 1 boss / 3 extractors. The authored tables below are
    /// the single source of those counts and Level02ContentTests asserts them.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-E — the worked
    /// example for every other level): each of the three approach rooms is an
    /// <see cref="EncounterLock"/> the hero must clear before the way forward
    /// opens — the vanguard (300–2000), the siege line (3220–6290, which also
    /// needs generator A down; its exit barrier fills Gate A under the gate
    /// column) and the battlement floor fight before the climb (6580–8690, its
    /// exit at Gate B). One lock wave per manifest row. The Middle checkpoint
    /// moved out of the siege line to the corridor between Gate A and the
    /// battlement lock (6500), and every checkpoint lists the locks before it.</para>
    ///
    /// <para><b>The Rampart run and the Parapet line</b> (gameplay review, GR-PILOT: the
    /// worked example every later level copies; docs/handoffs/GR_PILOT.md). The vanguard
    /// lock was carved from 300–3200 to 300–2000, and the freed span is a traversal
    /// stretch, 2020–3200: a step line over a non-lethal trench, with a crumbling step, a
    /// mortar lane, a one-way back up and a wall step with a ledge. Above it the Parapet
    /// line is the optional route: the courtyard Extractor at the end of a harder
    /// base-kit line, which a burst or a rise shortens. No dust moved (F05).</para>
    /// </summary>
    public partial class Level02Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string ID = "level_02_orleans";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string Checkpoint2 = ID + "_checkpoint_2";
        public const string BossResourcePath = "res://resources/Bosses/siegemaster_duke.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_02_dialogue.tres";

        public override string LevelID => ID;
        public override CampaignLevel Level => CampaignLevel.Orleans;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 4 authored rooms x 90 s, rounded up to the nearest
        /// 30 s, plus the Rampart run's +30 (§1.14). Recorded under
        /// VERIFY-PAR-SECONDS in docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md
        /// pending the V01a per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(360f, 1);
        public override string LevelTitleKey => "orleans_level_title";
        public override string DialogueSetPath => DialogueResourcePath;
        public override Vector2 PlayerSpawnPosition => new(200, 850);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the high rampart ledge above the vanguard approach (Platform 900/570, top 562):
        // a dead-end perch two double-jump hops up from the low step at 500 — the floor
        // route walks straight under it.
        protected override Vector2? SecretCachePosition => new(900f, 467f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (9060), 120 px short of the arena (9300) - room 4's only flat ground before
        // the fight.
        protected override Vector2? RestorationFontPosition => new(9180f, GroundY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "orleans_objective_advance";
        protected override string BossObjectiveKey => "orleans_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "orleans_objective_complete";

        // === Layout ===

        public const float LevelWidth = 10560f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float EnemyGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 3200f;
        private const float Room3StartX = 6400f;
        private const float Room4StartX = 8960f;

        /// <summary>Battlement walkway surface in room three; the only route east.</summary>
        private const float WalkwayY = 560f;

        /// <summary>Shield gate columns: the barrier plane each generator holds up.</summary>
        public const float GateAX = 6300f;
        public const float GateBX = 8700f;

        /// <summary>
        /// Width of the barrier ShieldGeneratorTowerTemplate hangs centred on each
        /// gate x (its BarrierShape). The gate columns are centred on the gate and
        /// cover it (StoryGateRules); ArenaGeometryContentTests measures the live
        /// barrier against its column, so a template change cannot reopen a lip.
        /// </summary>
        private const float GateBarrierWidth = 48f;

        /// <summary>
        /// ShieldGeneratorTowerTemplate hangs its barrier at a fixed local offset, so
        /// a generator authored in the scene has to stand exactly this far west of the
        /// gate it seals. Level02ContentTests pins the scene positions against it.
        /// </summary>
        public const float BarrierLocalOffsetX = 320f;
        public const float TowerAX = GateAX - BarrierLocalOffsetX;
        public const float TowerBX = GateBX - BarrierLocalOffsetX;
        public const float TowerAY = 790f;
        public const float TowerBY = 440f;

        // GR-PILOT: room 1 ends where its deck does; the Rampart run has its own rect.
        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, RampartWestDeckEndX, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 3200, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0, 2560, LevelHeight);
        // Narrower than a 1920 viewport otherwise, which makes the confiner useless.
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX - 320f, 0, 1920, LevelHeight);

        public const int TowerCount = 2;

        /// <summary>
        /// P1 (2026-10-04): the Duke's court. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 10200 - 900), so the pre-boss anchor
        /// at 9060 stays a breather outside it; east edge is the court wall's inner
        /// face. The Duke waits dormant inside it until the player crosses in, and
        /// never leaves it (he used to march 1,250 px west and fight against the
        /// footing wall).
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, 10540f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10200f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

        // === Authored encounter tables (the locked economy row lives here) ===

        /// <summary>
        /// Era roster: `laser_archer` (Orléans standard, ranged) mixed with
        /// `chrono_slasher` (Severed shock-trooper standard, melee). No elites - the locked
        /// row for level 2 is E = 0.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room1Spawns = {
            ("laser_archer", new Vector2(1100, EnemyGroundY), new Vector2(950, EnemyGroundY), new Vector2(1250, EnemyGroundY)),
            ("chrono_slasher", new Vector2(1750, EnemyGroundY), new Vector2(1600, EnemyGroundY), new Vector2(1900, EnemyGroundY)),
            // GR-PILOT: moved from 2600 when the lock was carved to 300-2000.
            ("laser_archer", new Vector2(1900, EnemyGroundY), null, null)
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room2Spawns = {
            ("chrono_slasher", new Vector2(3900, EnemyGroundY), new Vector2(3750, EnemyGroundY), new Vector2(4050, EnemyGroundY)),
            ("laser_archer", new Vector2(4600, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(5400, EnemyGroundY), new Vector2(5250, EnemyGroundY), new Vector2(5550, EnemyGroundY))
        };

        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room3Spawns = {
            ("laser_archer", new Vector2(7100, EnemyGroundY), null, null),
            ("chrono_slasher", new Vector2(7900, EnemyGroundY), new Vector2(7750, EnemyGroundY), new Vector2(8050, EnemyGroundY))
        };

        /// <summary>15 dust each and resource-owned; never overridden from level code.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            // GR-PILOT: at the end of the Parapet line, on its last platform (top 310).
            ("level_02.extractor_courtyard", new Vector2(ParapetEndX, 238)),
            ("level_02.extractor_mortar_ledge", new Vector2(4750, 400)),
            ("level_02.extractor_battlement", new Vector2(8560, 260))
        };

        public static int AuthoredStandardCount =>
            Room1Spawns.Length + Room2Spawns.Length + Room3Spawns.Length;

        // === GU-E (2026-10-12): the three encounter locks ===================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockVanguard = ID + "_lock_vanguard";
        public const string LockSiegeLine = ID + "_lock_siege_line";
        public const string LockBattlement = ID + "_lock_battlement";

        /// <summary>The manifest rows the three lock waves are (level_02_rewards.tres).</summary>
        public const string Room1RowID = "level_02.room1";
        public const string Room2RowID = "level_02.room2";
        public const string Room3RowID = "level_02.room3";
        /// <summary>The siege line's swarm (rulings 1b, 1b+, 1b++): its own manifest row, weight 0.</summary>
        public const string SwarmRowID = "level_02.swarm1";

        /// <summary>
        /// Where the swarm comes over the earthworks. Ten mobs, because the siege line is
        /// wider than <see cref="SwarmWaveRules.LargeLockWidthPixels"/> (1b++); Easy's
        /// prefix gives seven. The manifest row lists the same ten.
        /// </summary>
        public static readonly float[] SwarmSpawnXs = { 3500f, 3900f, 4150f, 4400f, 4650f, 4950f, 5200f, 5450f, 5800f, 6050f };

        /// <summary>
        /// The vanguard lock, carved by GR-PILOT from the whole approach room (it ran to
        /// 3200) so the span east of it can be the Rampart run.
        /// </summary>
        public const float VanguardLockMinX = 300f;
        public const float VanguardLockMaxX = 2000f;
        /// <summary>The siege line lock: room 2 up to Gate A, whose generator it also needs down.</summary>
        public const float SiegeLineLockMinX = Room2StartX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float SiegeLineLockMaxX = GateAX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
        /// <summary>
        /// The battlement lock: the floor fight before the climb, up to Gate B.
        /// FIX-E (2026-10-13): it starts at 6580 (was 6460) to widen the corridor
        /// the Middle anchor stands in; the spikes (6690–6810) and both room 3
        /// spawns stay inside it.
        /// </summary>
        public const float BattlementLockMinX = 6580f;
        public const float BattlementLockMaxX = GateBX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;

        /// <summary>
        /// The Middle anchor, in the corridor between Gate A (its forcefield ends at
        /// 6324) and the battlement lock's entry barrier (6560): outside every lock
        /// area, past the siege line it used to stand inside (5300). FIX-E
        /// (2026-10-13): 6500, not 6370 — its strike surface (6470–6530) is beyond
        /// the longest basic swing from inside the siege line (a hero pressed
        /// against the exit barrier stands at 6270; the roster's longest basic hit,
        /// AttackRange minor bought, ends at about 6426), and its respawn point is clear of the
        /// room 3 trigger (6380–6460) whose camera starts at 6400. The checkpoint
        /// guard refuses it anyway until the siege line is cleared.
        /// </summary>
        public const float MiddleCheckpointX = 6500f;

        /// <summary>Gate A's column foot: the siege line's exit barrier fills the gate below it.</summary>
        private const float GateAColumnFootY = 600f;
        /// <summary>Gate B's column foot: the battlement's exit barrier fills the gate and the footing pocket below it.</summary>
        private const float GateBColumnFootY = 240f;

        // === GR-PILOT: the Rampart run and the Parapet line ===================

        public const string RampartStretchID = "level_02.stretch_rampart";
        public const string ParapetRouteID = "level_02.route_parapet";
        /// <summary>The stretch: from the vanguard's exit barrier to the siege line's entry barrier.</summary>
        public const float RampartMinX = VanguardLockMaxX + EncounterLockRules.DefaultBarrierWidthPixels;
        public const float RampartMaxX = Room2StartX;
        /// <summary>The deck ends and resumes here: the high joints of the trench's ramps.</summary>
        public const float RampartWestDeckEndX = 2060f;
        public const float RampartEastDeckStartX = 3160f;
        /// <summary>The Parapet line's last platform, which holds the courtyard Extractor.</summary>
        private const float ParapetEndX = 3035f;

        private EncounterLock _vanguardLock;
        private EncounterLock _siegeLineLock;
        private EncounterLock _battlementLock;

        public EncounterLock VanguardLock => _vanguardLock;
        public EncounterLock SiegeLineLock => _siegeLineLock;
        public EncounterLock BattlementLock => _battlementLock;

        public static int AuthoredEliteCount => 0;

        public static int AuthoredExtractorCount => ExtractorPlacements.Length;

        // === Runtime state ===

        private ShieldGeneratorTower _towerA;
        private ShieldGeneratorTower _towerB;
        private int _towersDown;
        private bool _siegeLineReached;

        public int TowersDestroyed => _towersDown;
        public ShieldGeneratorTower ShieldTowerA => _towerA;
        public ShieldGeneratorTower ShieldTowerB => _towerB;

        // === Palette: sunbaked stone, scorched earth, Archive cyan ===

        protected override Color FloorColor => new(0.21f, 0.19f, 0.15f);
        protected override Color FloorEdgeColor => new(0.46f, 0.40f, 0.27f);
        protected override Color PlatformColor => new(0.34f, 0.31f, 0.24f);
        protected override Color WallColor => new(0.15f, 0.14f, 0.11f);
        protected override Color HazardColor => new(0.62f, 0.24f, 0.10f);

        private static readonly Color GateColor = new(0.24f, 0.32f, 0.40f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1VanguardApproach();
            BuildRampartRun();
            BuildRoom2SiegeLine();
            BuildRoom3BattlementClimb();
            BuildRoom4BossCourt();
            BuildExtractors(ExtractorPlacements);
            WireShieldTowers();
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-E: the three approach rooms as encounter locks, one wave per
        /// manifest row. Built here — before the resume baseline is applied — so a
        /// resume past a lock builds it Cleared with no member ever spawned.
        /// </summary>
        private void BuildEncounterLocks() {
            _vanguardLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockVanguard,
                Area = EncounterLockSpec.Span(VanguardLockMinX, VanguardLockMaxX, LevelHeight),
                EntryBarrierX = VanguardLockMinX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,
                ExitBarrierX = VanguardLockMaxX + EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,
                BarrierFloorY = GroundY,
                Waves = new[] { EncounterLockWave.FromTable(Room1RowID, Room1Spawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The siege line's soldiers come over the earthworks once the hero is
            // in (a wave that spawns on engage), and its generator has to fall.
            _siegeLineLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockSiegeLine,
                Area = EncounterLockSpec.Span(SiegeLineLockMinX, SiegeLineLockMaxX, LevelHeight),
                EntryBarrierX = SiegeLineLockMinX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,
                ExitBarrierX = GateAX,
                ExitBarrierYRange = new Vector2(GateAColumnFootY, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] {
                    EncounterLockWave.FromTable(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnEngage),
                    SwarmWave(SwarmRowID, EnemyGroundY, SwarmSpawnXs)
                },
                RequiredBreakables = _towerA != null ? new DamageableEnvironmentObject[] { _towerA }
                    : System.Array.Empty<DamageableEnvironmentObject>(),
                GoDirection = EncounterGoDirection.Right
            });
            _battlementLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockBattlement,
                Area = EncounterLockSpec.Span(BattlementLockMinX, BattlementLockMaxX, LevelHeight),
                EntryBarrierX = BattlementLockMinX - EncounterLockRules.DefaultBarrierWidthPixels * 0.5f,
                ExitBarrierX = GateBX,
                ExitBarrierYRange = new Vector2(GateBColumnFootY, GroundY),
                BarrierFloorY = GroundY,
                Waves = new[] { EncounterLockWave.FromTable(Room3RowID, Room3Spawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>Room 1: the French vanguard's ruined approach. No gate, no waves past the opener.</summary>
        private void BuildRoom1VanguardApproach() {
            BuildRoomBackground("BG_Room1", Room1StartX, 3200, LevelHeight, new Color(0.11f, 0.09f, 0.06f, 0.35f));
            BuildFloor(Room1StartX, GroundY, RampartWestDeckEndX - Room1StartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(500, 720, 260);
            BuildPlatform(900, 570, 220);
            BuildPlatform(1350, 700, 240);
            BuildPlatform(1900, 540, 240);

            BuildHazardSpikes(1600, 890, 140);

            BuildCheckpoint(200, EnemyGroundY, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "orleans_room_vanguard", new Color(0.85f, 0.72f, 0.35f));
            BuildRoomTransition("orleans_room_vanguard", new Vector2(300, 600), Room1CameraBounds);
            // Package 13 W4 (S23): the vanguard, where the banner stands with no one holding it.
            BuildAbsenceTrigger(new Vector2(1100, GroundY - 200));
        }

        /// <summary>
        /// GR-PILOT: the Rampart run, 2020-3200, and the Parapet line above it. The step line
        /// crosses at y 712 over a trench 120 px deep; a hero who falls in walks out by the
        /// west ramp, climbs the one-way back to the line, or climbs the wall step from the
        /// east ramp. Nothing here is a new verb, and there is no rope.
        /// </summary>
        private void BuildRampartRun() {
            TraversalStretch rampart = BeginStretch(RampartStretchID, "orleans_room_rampart",
                RampartMinX, RampartMaxX, GroundY, RampartWestDeckEndX, RampartEastDeckStartX, Room1StartX);
            BuildRoomDecoration(RampartMinX, "orleans_room_rampart", new Color(0.85f, 0.6f, 0.3f));
            BuildStretchTrench(rampart);
            // The deck resumes here, under the siege line's entry barrier's west side.
            BuildFloor(RampartEastDeckStartX, GroundY, RampartMaxX - RampartEastDeckStartX);

            BuildStretchStep(rampart, 2170, 790, 200);
            BuildStretchStep(rampart, 2430, 720, 180, StretchStepKind.Crumbling, "level_02.crumbling_rampart");
            TraversalSurface middleStep = BuildStretchStep(rampart, 2700, 722, 200, StretchStepKind.OneWay);
            TraversalSurface eastStep = BuildStretchStep(rampart, 2970, 720, 200);
            BuildStretchWallStep(rampart, 3150f, 690f, 940f);

            // Off the line: the way back up out of the trench, and a mortar lane to cross down there.
            BuildOneWayPlatform(2640, 870, 200);
            BuildCyclicHazardLane("level_02.mortar_rampart", 2850f, rampart.TrenchFloorY, 1f, 0.6f, 2.6f, 18, withCover: true);

            // The Parapet line. Its base-kit line climbs west and back east, and ends in a 245 px hop.
            OptionalRoute parapet = BeginOptionalRoute(ParapetRouteID, ExtractorPlacements[0].ID,
                ExtractorPlacements[0].Position, middleStep);
            BuildRouteStep(parapet, 2560, 530, 120);
            TraversalSurface burstFrom = BuildRouteStep(parapet, 2340, 420, 160);
            BuildRouteStep(parapet, 2590, 320, 180);
            TraversalSurface end = BuildRouteStep(parapet, ParapetEndX, 320, 220);
            // A burst skips the climb's last platform. A rise goes straight up from the line's east step.
            parapet.MarkBurstShortcut(burstFrom, end);
            parapet.MarkRiseShortcut(eastStep, end);
        }

        /// <summary>
        /// Room 2: the English siege line. A four-emplacement mortar lane (authored
        /// in the scene) plus generator A, whose barrier seals the eastern gate.
        /// </summary>
        private void BuildRoom2SiegeLine() {
            BuildRoomBackground("BG_Room2", Room2StartX, 3200, LevelHeight, new Color(0.09f, 0.08f, 0.06f, 0.4f));
            BuildFloor(Room2StartX, GroundY, 3200);

            BuildPlatform(3500, 700, 220);
            BuildPlatform(4000, 600, 200);
            BuildPlatform(4500, 700, 220);
            BuildPlatform(4750, 480, 200);
            BuildPlatform(5000, 600, 200);
            BuildPlatform(5500, 700, 240);

            // GU-E: the Middle anchor stands past Gate A, outside every lock area
            // (it used to stand at 5300, in the middle of the siege line's fight).
            BuildCheckpoint(MiddleCheckpointX, EnemyGroundY, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "orleans_room_siege_line", new Color(0.9f, 0.55f, 0.25f));

            // The barrier covers x 6276..6324, y 582..902; this column seals
            // everything above it, so the gate cannot be jumped while generator A
            // still stands. P5: it rises past the top of the level
            // (StoryGateRules) instead of stopping at y 0, and it is centred on the
            // barrier and wider than it - the old left-anchored 20 px arch left the
            // barrier's west 24 px as a standable lip.
            BuildGateColumn(GateAX, GateBarrierWidth, 600, GateColor);
            BuildGateLabel("GateALabel", new Vector2(GateAX, 540));

            // GU-E: the room trigger stands just inside the siege line's area, so
            // a hero pressed against the vanguard's exit barrier does not reach it.
            BuildRoomTransition("orleans_room_siege_line", new Vector2(SiegeLineLockMinX + 40f, 600), Room2CameraBounds,
                onEntered: _ => {
                    _siegeLineReached = true;
                    RefreshObjective();
                });
        }

        /// <summary>
        /// Room 3: the vertical section. The ground route dead-ends in the battlement
        /// footing, so the only way east is a drop-through climb onto the walkway -
        /// and the walkway is sealed by generator B's barrier.
        /// </summary>
        private void BuildRoom3BattlementClimb() {
            BuildRoomBackground("BG_Room3", Room3StartX, 2560, LevelHeight, new Color(0.10f, 0.09f, 0.08f, 0.4f));
            // P5 (2026-10-04): the floor runs all the way to the court (8960). It
            // used to stop at 8900, leaving a 60 px hole east of the footing wall
            // that a player walking west out of the court fell through forever.
            BuildFloor(Room3StartX, GroundY, Room4StartX - Room3StartX);

            BuildOneWayPlatform(6900, 790, 200);
            BuildOneWayPlatform(7250, 700, 200);
            BuildOneWayPlatform(7650, 620, 200);
            BuildOneWayPlatform(8050, WalkwayY, 220);

            // Battlement walkway, 8300..8960. Solid: it is the floor of the gate.
            BuildPlatform(8630, WalkwayY, 660);
            // Side branch above the walkway holding the third extractor.
            BuildPlatform(8560, 340, 200);
            // Footing under the east end of the walkway: closes the ground route.
            BuildWall(8880, WalkwayY + DefaultPlatformThickness / 2f, LevelHeight - WalkwayY - DefaultPlatformThickness / 2f);

            // Generator B's barrier covers x 8676..8724, y 232..552; this seals
            // everything above it. P5: the old arch spanned only x 8700..8720 and
            // y 0..240, so the barrier's west 24 px was a lip at y 232 with open
            // sky, and a double jump off it (or off the battlement Extractor, top
            // ~188) went over the top. The column is centred on the barrier, wider
            // than it, and rises past the level top.
            BuildGateColumn(GateBX, GateBarrierWidth, 240, GateColor);
            BuildGateLabel("GateBLabel", new Vector2(GateBX, 200));

            BuildHazardSpikes(6750, 890, 120);

            BuildRoomDecoration(Room3StartX, "orleans_room_battlement", new Color(0.6f, 0.75f, 0.9f));
            BuildRoomTransition("orleans_room_battlement", new Vector2(Room3StartX + 20, 600), Room3CameraBounds);
        }

        /// <summary>
        /// Room 4: the battlement court. Flat floor plus three stone platforms in a
        /// triangle, 1,600 px wide so the Duke's 9.0 m ranged band (540 px) and
        /// 3.5 m melee band both have room to matter.
        /// </summary>
        private void BuildRoom4BossCourt() {
            BuildRoomBackground("BG_Room4_Boss", Room4StartX, 1600, LevelHeight, new Color(0.16f, 0.07f, 0.06f, 0.45f));
            BuildFloor(Room4StartX, GroundY, 1600);

            BuildPlatform(9500, 690, 240);
            BuildPlatform(10020, 690, 240);
            BuildPlatform(9760, 480, 220);

            BuildWall(10540, 0, LevelHeight);

            BuildCheckpoint(9060, EnemyGroundY, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room4StartX, "orleans_room_boss", new Color(0.95f, 0.3f, 0.25f));
            BuildRoomTransition("orleans_room_boss", new Vector2(Room4StartX + 30, 540), Room4CameraBounds);

            BossEncounterController duke = BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, EnemyGroundY),
                "siegemaster_duke_encounter", BossArenaBounds);
            BuildDukeSteam();
            if (duke != null) {
                duke.PhaseEntered += OnDukePhaseEntered;
                duke.BossDefeated += _ => LowerDukeSteam();
            }
        }

        // === GR-PILOT (ruling 14): the Duke's boiler rupture =================
        //
        // Design §6: "both arena edges become steam hazards, fight pushed to centre".
        // The two strips are built up front, disabled, inside the arena, so no collision
        // body is created mid-fight; his Phase 2 starts them. They are story_hazard
        // members, so the Post-Landing Hold and the rewind freeze hold them. His
        // mortar_barrage stays: it is the design's "mortars".

        /// <summary>
        /// The steam strips' x centres (the template's strip is 120 wide). The east strip is flush with
        /// the court wall; the west one stands 10 px inside the arena, which keeps the font's channel
        /// circle at 9180 clear of it (FontHazardClearance).
        /// </summary>
        public const float DukeSteamWestX = BossArenaWestX + 70f;
        public const float DukeSteamEastX = 10540f - 60f;

        private StoryCyclicHazard _dukeSteamWest;
        private StoryCyclicHazard _dukeSteamEast;

        /// <summary>True once Phase 2 has started the steam and until the Duke falls.</summary>
        public bool DukeSteamRaised { get; private set; }
        public StoryCyclicHazard DukeSteamWest => _dukeSteamWest;
        public StoryCyclicHazard DukeSteamEast => _dukeSteamEast;

        private void BuildDukeSteam() {
            _dukeSteamWest = BuildCyclicHazardLane("level_02.duke_steam_west", DukeSteamWestX, GroundY, 1f, 1.2f, 1.8f, 12, startEnabled: false);
            _dukeSteamEast = BuildCyclicHazardLane("level_02.duke_steam_east", DukeSteamEastX, GroundY, 1f, 1.2f, 1.8f, 12, startEnabled: false);
        }

        private void OnDukePhaseEntered(int phase) {
            if (phase < 1) return;
            // The phase change usually lands inside the hit's physics flush; hop off it.
            // Direct (test) calls stay synchronous.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) Callable.From(RaiseDukeSteam).CallDeferred();
            else RaiseDukeSteam();
        }

        /// <summary>Starts both steam edges on their warning. Idempotent; public as a test seam.</summary>
        public void RaiseDukeSteam() {
            if (DukeSteamRaised || IsBossDefeated || _dukeSteamWest == null || _dukeSteamEast == null) return;
            DukeSteamRaised = true;
            bool held = IsInsideTree() && ChronalRewindManager.IsWorldHeld(GetTree());
            foreach (StoryCyclicHazard steam in new[] { _dukeSteamWest, _dukeSteamEast }) {
                steam.Enabled = true;
                // Never a telegraph that starts inside the hold: it would run out unseen.
                if (!held) steam.ForcePhase(HazardPhase.Warning, steam.WarningDuration);
            }
        }

        /// <summary>Stops the steam once the Duke falls: the hero still has to walk to the sealing anchor.</summary>
        private void LowerDukeSteam() {
            DukeSteamRaised = false;
            foreach (StoryCyclicHazard steam in new[] { _dukeSteamWest, _dukeSteamEast }) {
                if (steam == null || !IsInstanceValid(steam)) continue;
                steam.Enabled = false;
                steam.ForcePhase(HazardPhase.Cooldown, steam.CooldownDuration);
            }
        }

        /// <summary>Placeholder gate signage; replaced with production art in Package 8.</summary>
        private Label BuildGateLabel(string name, Vector2 position) {
            var label = new Label {
                Name = name,
                Text = Tr("orleans_gate_shielded"),
                Position = position + new Vector2(-150, 0),
                CustomMinimumSize = new Vector2(300, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 1f));
            AddChild(label);
            return label;
        }

        // === Shield generators ===

        private void WireShieldTowers() {
            _towerA = GetNodeOrNull<ShieldGeneratorTower>("ShieldTowerA");
            _towerB = GetNodeOrNull<ShieldGeneratorTower>("ShieldTowerB");
            NameBarriers(_towerA);
            NameBarriers(_towerB);
            if (_towerA != null) _towerA.TowerDestroyed += OnShieldTowerDestroyed;
            if (_towerB != null) _towerB.TowerDestroyed += OnShieldTowerDestroyed;
        }

        private static void NameBarriers(ShieldGeneratorTower tower) {
            if (tower == null) return;
            int index = 0;
            foreach (ForcefieldBarrier barrier in tower.Barriers()) {
                barrier.BarrierID = $"{tower.ObjectID}_barrier_{index++}";
            }
        }

        private void OnShieldTowerDestroyed(string towerID) {
            _towersDown++;
            RefreshObjective();
        }

        /// <summary>
        /// Resume helper: a run restored past a gate must not find that gate sealed
        /// again. Destroying the generator is the same code path the player takes, so
        /// the barriers drop and the objective count stays honest.
        /// </summary>
        private static void ForceGateOpen(ShieldGeneratorTower tower) {
            if (tower == null || !IsInstanceValid(tower) || tower.IsDestroyed) return;
            tower.TakeEnvironmentDamage(tower.MaxHP);
        }

        // === Encounters ===
        //
        // GU-E (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles, never
        // more than the table. Concurrency stays at 3 or fewer live standards plus
        // the siege line's swarm, inside the level's warm pool counts (standard_enemy
        // 16, enemy_projectile 18).

        // === Resume and objectives ===

        // === Package 12 W2 (GAP-13): explicit encounter baseline ===========
        //
        // GU-E: the encounters are the three locks; every anchor lists every lock
        // before it, and a resume builds those Cleared (the vanguard used to
        // respawn behind the hero on every resume).

        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockVanguard, LockSiegeLine },
                [Checkpoint2] = new[] { LockVanguard, LockSiegeLine, LockBattlement }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>
        /// The anchor's non-encounter world state; the locks come from the
        /// baseline. GU-E: the Middle anchor now stands past Gate A, so a resume
        /// there drops generator A's barrier too.
        /// </summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint1) {
                _siegeLineReached = true;
                ForceGateOpen(_towerA);
            } else if (checkpointID == Checkpoint2) {
                _siegeLineReached = true;
                ForceGateOpen(_towerA);
                ForceGateOpen(_towerB);
            }
        }

        // Resume camera confinement is handled by
        // StoryLevelControllerBase.ApplyResumeCameraBounds (Wave A integration).
        protected override void OnLevelReady() => RefreshObjective();

        /// <summary>GU-E: a cleared or reverted lock hands the objective back to the level's own.</summary>
        protected override void RestoreLevelObjective() => RefreshObjective();

        private void RefreshObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (_towersDown >= TowerCount) SetObjective("orleans_objective_breach");
            else if (_siegeLineReached || _towersDown > 0) SetObjective("orleans_objective_towers", _towersDown, TowerCount);
            else SetObjective(InitialObjectiveKey);
        }
    }
}
