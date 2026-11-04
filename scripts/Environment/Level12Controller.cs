using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 12 - the Lunar Landing, 1969. The Act II finale.
    ///
    /// An Archive-controlled outpost sits half a kilometre east of Tranquility
    /// Base, riding the Apollo 11 broadcast to siphon the global attention of the
    /// milestone. The encrypted lead the player pulled out of the Titanic wreck in
    /// Level 5 ("an orbital communications relay") is what points here, and
    /// destroying the Gravity Overseer traces the coordinate signature of the
    /// cult's far-future headquarters - the hand-off into Act III.
    ///
    /// <b>Era identity: the whole level runs in low gravity.</b> Two
    /// <see cref="GravityFieldZone"/>s tile the level end to end - deliberately
    /// contiguous and deliberately NON-overlapping, because
    /// <see cref="EnvironmentPlayerModifiers"/> publishes the *product* of every
    /// live source (Levels 5 and 8 both hit that with their zone families). The
    /// regolith field is the Moon (<see cref="RegolithGravityScale"/>); the landing
    /// pad runs firmer (<see cref="PadGravityScale"/>) because the outpost's
    /// inertial dampers are strongest there, which is what keeps the Overseer's
    /// phase-3 gravity-well PULL recoverable rather than unrecoverable.
    ///
    /// Low gravity is load-bearing, not decoration. The level is 2,000 px tall and
    /// every authored rung is sized so even the heaviest single-jump character
    /// clears it on the Moon and could not clear it on Earth. The two
    /// <see cref="PathMovingPlatform"/> lifts rise further than ANY character's
    /// single lunar jump, and the spire lift - the critical-path one - rises
    /// further than any character's entire multi-jump budget, so the spire climb
    /// and the highline ferry through the curtain wall's cargo slot are the only
    /// route into the boss pad.
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: 14 standards, 2 elites,
    /// 1 boss, 4 extractors - the largest budget in the campaign so far.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L12, user
    /// request 3): every enemy is fought inside an <see cref="EncounterLock"/> —
    /// Tranquility Base (320–3000, wave 1, pre-placed), the vent field (3020–5860,
    /// wave 2, arriving through Severed tears on engage) and the spire base
    /// (6220–7540, from the room seam to the gap between the mast head and the
    /// highline ferry: wave 3 pre-placed, and the landing-pad garrison, wave 4,
    /// arriving as a second wave once the defenders are down). The three wave-3
    /// troopers that stood on the floor east of the lift — a dead end the lift
    /// skipped, between two cache vents and the cache Extractor — came onto the
    /// route, and the spire lift is held at its bottom stop until the spire is
    /// clear, so the climb is the way out of the fight rather than around it. The
    /// floor beyond the lock stays the optional, enemy-free detour to the cache
    /// Extractor. The pad east of the curtain is no longer a fight — its 1216 px
    /// between the curtain and the arena is narrower than a lock — so the PreBoss
    /// anchor, the thesis scene and the font keep their places.</para>
    ///
    /// <para><b>The Mare Crossing</b> (GR-LV4, rulings 7a, 7b, 7c, 7c+): Tranquility Base's
    /// lock now ends at 2000, and the span to the vent field (2020-3000) is a traversal
    /// stretch with the secret at the end of its route. It has no trench: the Moon has no
    /// pits and the floor is 100 px above the level's bottom edge, so the harder way is the
    /// floor under two vents, and the line runs over them. No swarm row (ruling 1b++).</para>
    /// </summary>
    public partial class Level12Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string LunarLevelID = "level_12_lunar";

        public override string LevelID => LunarLevelID;
        public override CampaignLevel Level => CampaignLevel.Lunar;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 6 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => TraversalRules.ParWithStretches(540f, 1);
        public override string LevelTitleKey => "lunar_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_12_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(220f, SurfaceY - 60f);

        // GR-LV4: the secret cache sits at the end of the Survey Line route, over the Mare
        // Crossing's first step. It stood on the upper spire-ladder cover rung (Package 12 W8);
        // that rung stays as cover.
        protected override Vector2? SecretCachePosition => new(SurveyLineEndX, 1285f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (8940) on the landing pad, 400 px short of the arena (9800). Low-gravity
        // exception: the whole level is low gravity (pad dampers 0.48); a post-wave heal
        // (GU-L12: every wave, the pad garrison included, is fought in the spire lock west of
        // the curtain, so the font stands outside every lock area).
        protected override Vector2? RestorationFontPosition => new(9400f, SurfaceY);

        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        /// <summary>
        /// Act II finale set-up: the design's authored Sarah scene (design-godot.md
        /// §16, Level 12 Pre-Boss), triggered as the player reaches the outpost
        /// entrance — the trace is "nearly done" and the guardian stands between
        /// Sarah and the answer. Since Package 12 W7 (M13) the trace completes in
        /// <see cref="PostBossDialogueID"/>, not here.
        /// </summary>
        public string PreBossDialogueID => $"{DialoguePrefix}.preboss";

        /// <summary>
        /// Package 12 W7 (M13): the trace completes AFTER the Overseer falls. The
        /// pre-boss scene is set-up only; the where-reveal — the fortress between
        /// timelines, the live signatures, <c>{CaptiveName1}</c> /
        /// <c>{CaptiveName2}</c>, the draining — is this post-boss beat, chained
        /// defeat -> postboss -> exit -> results by the base class's post-boss
        /// tail (see Level 8 for the pattern).
        /// </summary>
        public string PostBossDialogueID => $"{DialoguePrefix}.postboss";

        private string[] _postBossBeats;

        protected override IReadOnlyList<string> PostBossDialogueIDs =>
            _postBossBeats ??= new[] { PostBossDialogueID };

        /// <summary>True while the M13 post-boss trace scene is the active post-boss beat.</summary>
        public bool PostBossBeatActive => ActivePostBossDialogueID == PostBossDialogueID;

        protected override string InitialObjectiveKey => "lunar_objective_reach_outpost";
        protected override string BossObjectiveKey => "lunar_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "lunar_objective_complete";

        // === Dimensions ===

        public const float LevelWidth = 11600f;
        /// <summary>Taller than any level so far: the mast climb is the era identity.</summary>
        public const float LevelHeight = 2000f;

        /// <summary>Regolith grade. One continuous surface - the Moon has no bottomless pits.</summary>
        public const float SurfaceY = 1900f;
        public const float LedgeA = 1700f;
        public const float LedgeB = 1500f;
        public const float LedgeC = 1300f;
        /// <summary>Highline altitude: the ferry deck, both lift tops, and the mast head.</summary>
        public const float HighDeckY = 400f;

        public const float Room2StartX = 3000f;
        public const float Room3StartX = 6200f;
        /// <summary>West edge of the orbital landing pad, and the gravity seam.</summary>
        public const float ArenaStartX = 9000f;
        /// <summary>East wall of the pad; the arena's usable floor ends here.</summary>
        public const float ArenaEndX = 11400f;

        // === The outpost curtain ===

        /// <summary>
        /// The curtain is two wall segments with a cargo slot between them rather
        /// than a wall with an open top: an open top can be reached by the highest
        /// multi-jump character even on a 1,500 px wall, a slot cannot be entered
        /// from the regolith at all.
        /// </summary>
        public const float CurtainWallX = 8560f;
        /// <summary>Thickness of both curtain segments.</summary>
        private const float CurtainThickness = 24f;
        /// <summary>Bottom edge of the upper curtain segment - the slot's ceiling.</summary>
        public const float CurtainSlotTopY = 250f;
        /// <summary>Top edge of the lower curtain segment - the slot's floor.</summary>
        public const float CurtainSlotBottomY = 550f;

        /// <summary>Rise from the regolith a player would need to enter the cargo slot.</summary>
        public static float CurtainSlotRise => SurfaceY - CurtainSlotBottomY;

        /// <summary>
        /// P1 (2026-10-04): the landing pad arena. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 10500 - 700), so the pre-boss thesis
        /// scene at 8980 plays before the Overseer wakes (GU-L12: the wave-4
        /// garrison is now fought in the spire lock, west of the curtain); east
        /// edge is the pad wall.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, ArenaEndX - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10500f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 700 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 700f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

        // === Checkpoints ===

        public const string Checkpoint0 = LunarLevelID + "_checkpoint_0";
        public const string Checkpoint1 = LunarLevelID + "_checkpoint_1";
        public const string Checkpoint2 = LunarLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        private static readonly Vector2 Checkpoint0Position = new(260f, SurfaceY - 10f);
        private static readonly Vector2 Checkpoint1Position = new(6050f, SurfaceY - 10f);
        /// <summary>Pre-boss anchor: east of the curtain, so a resume is never stranded.</summary>
        private static readonly Vector2 Checkpoint2Position = new(8940f, SurfaceY - 10f);

        // === Low gravity ===

        /// <summary>
        /// Sea of Tranquility. Roughly doubled hang time without floating the controls
        /// away. Scaled 0.45 -> 0.36 (x0.8) by the 2026-08-10 movement retune, which cut
        /// jump height about 20%: every authored rung, lift and curtain altitude on this
        /// level was sized against the lunar reach, so the field carries the compensation
        /// instead of the geometry (gameplay-feel plan section 9, B2).
        /// </summary>
        public const float RegolithGravityScale = 0.36f;
        /// <summary>
        /// The pad's inertial dampers: firmer, so the Overseer's pull stays escapable.
        /// Scaled by the same x0.8, which keeps the pad/regolith contrast at exactly 4:3.
        /// </summary>
        public const float PadGravityScale = 0.48f;

        /// <summary>
        /// Authored gravity tiling. Contiguous (every span's end is the next span's
        /// start) and non-overlapping, so the level is low-gravity everywhere and no
        /// player is ever inside two fields multiplying each other.
        /// </summary>
        public static readonly (string ID, float StartX, float EndX, float Scale)[] GravityFieldSpans = {
            ("level_12.regolith_field", 0f, ArenaStartX, RegolithGravityScale),
            ("level_12.pad_dampers", ArenaStartX, LevelWidth, PadGravityScale)
        };

        private readonly List<GravityFieldZone> _gravityFields = new();
        public IReadOnlyList<GravityFieldZone> GravityFields => _gravityFields;

        // === Authored climbing ladders (one-way rungs, every ladder rises from the surface) ===

        public static readonly (float X, float Y, float Width)[] BaseCampLadder = {
            (900f, LedgeA, 240f), (1250f, LedgeB, 220f), (1600f, LedgeC, 200f)
        };

        /// <summary>Climb to the mare ferry's departure apron.</summary>
        public static readonly (float X, float Y, float Width)[] VentFieldLadder = {
            (3300f, LedgeA, 240f), (3620f, LedgeB, 220f), (3940f, LedgeB, 260f)
        };

        /// <summary>Cover platforms over the spire-base vent gauntlet.</summary>
        public static readonly (float X, float Y, float Width)[] SpireLadder = {
            (6400f, LedgeA, 240f), (6700f, LedgeB, 220f)
        };

        /// <summary>Return climb east of the curtain, so falling off the highline is not a dead end.</summary>
        public static readonly (float X, float Y, float Width)[] CurtainStairs = {
            (8680f, LedgeA, 160f), (8760f, LedgeB, 160f), (8840f, LedgeC, 160f),
            (8900f, 1100f, 160f), (8820f, 900f, 160f), (8740f, 700f, 160f), (8820f, 500f, 160f)
        };

        /// <summary>Every authored ladder, for the reachability contract.</summary>
        public static readonly (string ID, (float X, float Y, float Width)[] Rungs)[] Ladders = {
            ("base_camp", BaseCampLadder),
            ("vent_field", VentFieldLadder),
            ("relay_spire", SpireLadder),
            ("curtain_stairs", CurtainStairs)
        };

        /// <summary>The critical-path lift: no character's whole jump budget replaces it.</summary>
        public const string SpireLiftID = "level_12.lift_spire";
        public const string PocketLiftID = "level_12.lift_pocket";

        /// <summary>
        /// The spire lift's bottom stop: the deck's centre when its top lies flush
        /// with the regolith (the template deck is 32 px thick), so the stop is a
        /// walk-on one with no step. G3 (2026-10-04): it used to sit at SurfaceY
        /// itself, its deck top 16 px proud of the floor and its bottom 16 px inside
        /// it; a stop is either clear of the level geometry or flush with the top of
        /// the floor it sinks into (<c>ArenaGeometryContentTests</c>), and the deck
        /// never presses down on a hero beneath it (<see cref="PathMovingPlatform.IsHeldByBodyBelow"/>).
        /// </summary>
        public const float SpireLiftBottomY = SurfaceY + 16f;

        /// <summary>
        /// The two vertical <see cref="PathMovingPlatform"/> lifts as the deck
        /// centre's (bottom, top) altitudes, matching the authored Waypoints in
        /// Level_12_Lunar.tscn (<c>Level12ContentTests</c> pins the match).
        /// </summary>
        public static readonly (string PlatformID, float BottomY, float TopY)[] Lifts = {
            (PocketLiftID, LedgeB, HighDeckY),
            (SpireLiftID, SpireLiftBottomY, HighDeckY)
        };

        // === Encounters (locked: 14 standards / 2 elites / 1 boss / 4 extractors) ===

        /// <summary>Lunar era standard: grav-beams that PULL rather than knock back.</summary>
        public const string DiggerEnemyID = "vacuum_digger";
        /// <summary>Severed shock-trooper standard, mixed through the outpost garrison.</summary>
        public const string CultistEnemyID = "chrono_slasher";
        /// <summary>Lunar era elite.</summary>
        public const string EliteEnemyID = "void_enforcer";
        /// <summary>
        /// V7.6 (Package 11 A7a): the Eraser, salted through Levels 7-15. Not an
        /// era enemy - it is a Severed hunter that followed the hero here, which
        /// is exactly why it reads wrong against the 1969 landing pad. Never authored into a room
        /// with a Chrono-Warden before Act III (EncounterCompositionTests).
        /// </summary>
        public const string EraserEnemyID = "unbound_eraser";
        public const string BossResourcePath = "res://resources/Bosses/gravity_overseer.tres";

        private const float StandY = SurfaceY - 50f;

        /// <summary>
        /// Authored Normal-difficulty spawn table, one manifest row per wave
        /// (<c>level_12.waveN</c>: the same IDs in the same order). Each wave is an
        /// encounter-lock wave (GU-L12): waves 1 and 3 stand pre-placed and dormant
        /// from the level's build, wave 2 arrives on the vent field's engage and
        /// wave 4 reinforces the spire. Difficulty scaling takes the same per-row
        /// prefix through <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>
        /// that the reward ledger compiles.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - Tranquility Base. Diggers only: the low-gravity teaching fight.
            (DiggerEnemyID, 1, new Vector2(1100f, StandY)),
            // GR-LV4: 1900 -> 1480 and 2600 -> 1900, inside the carved lock and clear of the lander.
            (DiggerEnemyID, 1, new Vector2(1480f, StandY)),
            (DiggerEnemyID, 1, new Vector2(1900f, StandY)),
            // Wave 2 - the vent field. Severed troopers join the local excavation crews.
            (DiggerEnemyID, 2, new Vector2(3400f, StandY)),
            (CultistEnemyID, 2, new Vector2(3900f, StandY)),
            (DiggerEnemyID, 2, new Vector2(4600f, StandY)),
            (CultistEnemyID, 2, new Vector2(5300f, StandY)),
            // Wave 3 - the spire base, with the first Void Enforcer holding the mast.
            // GU-L12: the three that stood on the floor east of the lift (7600,
            // 8220, 8400) were in a dead end the spire lift skipped, between the two
            // cache vents and the cache Extractor; they moved onto the route, inside
            // the spire lock (7420, 6420, 7230).
            (DiggerEnemyID, 3, new Vector2(6600f, StandY)),
            (CultistEnemyID, 3, new Vector2(6950f, StandY)),
            (DiggerEnemyID, 3, new Vector2(7420f, StandY)),
            (CultistEnemyID, 3, new Vector2(6420f, StandY)),
            (EliteEnemyID, 3, new Vector2(7230f, StandY)),
            // Wave 4 - the landing pad garrison. GU-L12: it no longer waits on the
            // pad (9200-9900, the Eraser inside the arena at 9900); it comes down to
            // the spire base as a second wave once its defenders are down, so the pad between
            // the curtain and the arena stays the breather before the boss.
            (DiggerEnemyID, 4, new Vector2(6320f, StandY)),
            (CultistEnemyID, 4, new Vector2(6780f, StandY)),
            (CultistEnemyID, 4, new Vector2(7480f, StandY)),
            (EliteEnemyID, 4, new Vector2(7330f, StandY)),
            // The Act II finale's hunter. The low-gravity room is the cruellest
            // place to be tethered: the counterplay is to leave the circle, and
            // here leaving anywhere takes a long arc. GU-L12: it arrives with the
            // garrison (west of the arena, 9900 -> 6560) and is the last entry of
            // the wave-4 manifest row, so the ledger pays it an elite share (F05)
            // and Easy's prefix drops it exactly as the spawn prefix always did.
            (EraserEnemyID, 4, new Vector2(6560f, StandY))
        };

        /// <summary>Four extractors, each behind a real detour. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_12_extractor_0", new Vector2(1600f, LedgeC - 60f)),
            ("level_12_extractor_1", new Vector2(5700f, HighDeckY - 60f)),
            ("level_12_extractor_2", new Vector2(7350f, HighDeckY - 60f)),
            ("level_12_extractor_3", new Vector2(8060f, SurfaceY - 60f))
        };

        public static int StandardEnemyCount => CountOf(DiggerEnemyID) + CountOf(CultistEnemyID);
        public static int EliteEnemyCount => CountOf(EliteEnemyID) + CountOf(EraserEnemyID);
        /// <summary>How many Erasers this level authors. Test seam for the salt rules.</summary>
        public static int EraserCount => CountOf(EraserEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) if (id == enemyID) total++;
            return total;
        }

        // === GU-L12 (2026-10-12): the encounter locks ===========================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockTranquility = LunarLevelID + "_lock_tranquility";
        public const string LockVentField = LunarLevelID + "_lock_vent_field";
        public const string LockRelaySpire = LunarLevelID + "_lock_relay_spire";

        /// <summary>The manifest rows the lock waves are (level_12_rewards.tres).</summary>
        public const string Wave1RowID = "level_12.wave1";
        public const string Wave2RowID = "level_12.wave2";
        public const string Wave3RowID = "level_12.wave3";
        public const string Wave4RowID = "level_12.wave4";

        private const float LockBarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>Tranquility Base: everything past the entry anchor (260), carved by GR-LV4 from 3000 to 2000.</summary>
        public const float TranquilityLockMinX = 320f;
        public const float TranquilityLockMaxX = 2000f;
        /// <summary>
        /// The vent field, from the room seam to 190 px short of the Middle anchor
        /// (6050): the anchor's strike surface (6020–6080) stays past the longest
        /// basic swing of a hero pressed against the closed exit barrier (5840 + 156).
        /// </summary>
        public const float VentFieldLockMinX = Room2StartX + LockBarrierWidth;
        public const float VentFieldLockMaxX = 5860f;
        /// <summary>
        /// The spire base: from the room seam to the gap between the mast head
        /// (its deck ends at 7500) and the highline ferry's west stop (its deck
        /// starts at 7580), so the full-height exit barrier stands in that gap,
        /// cuts no platform and crosses no mover's path. The floor beyond it — the
        /// cache cul-de-sac with its two vents and Extractor — is outside the lock.
        /// </summary>
        public const float RelaySpireLockMinX = Room3StartX + LockBarrierWidth;
        public const float RelaySpireLockMaxX = 7540f;

        /// <summary>The scene's critical-path lift, and the highline ferry it leads to.</summary>
        public const string SpireLiftNodeName = "LiftSpire";
        public const string HighlineFerryNodeName = "FerryHighline";

        private EncounterLock _tranquilityLock;
        private EncounterLock _ventFieldLock;
        private EncounterLock _relaySpireLock;
        private PathMovingPlatform _spireLift;
        private PathMovingPlatform _highlineFerry;

        public EncounterLock TranquilityLock => _tranquilityLock;
        public EncounterLock VentFieldLock => _ventFieldLock;
        public EncounterLock RelaySpireLock => _relaySpireLock;
        /// <summary>Held at its bottom stop while the spire lock holds. Test seam.</summary>
        public PathMovingPlatform SpireLift => _spireLift;
        /// <summary>The highline ferry: east of the spire lock's exit, never held. Test seam.</summary>
        public PathMovingPlatform HighlineFerry => _highlineFerry;

        // === Runtime state ===

        private bool _preBossShown;
        private bool _rewindBound;

        public bool PreBossBeatPlayed => _preBossShown;

        // === Era palette ===

        protected override Color FloorColor => new(0.19f, 0.19f, 0.21f);
        protected override Color FloorEdgeColor => new(0.62f, 0.63f, 0.68f);
        protected override Color PlatformColor => new(0.3f, 0.32f, 0.38f);
        protected override Color WallColor => new(0.13f, 0.14f, 0.17f);

        private static readonly Color PadColor = new(0.24f, 0.25f, 0.32f);
        private static readonly Color CurtainColor = new(0.2f, 0.22f, 0.28f);
        private static readonly Color RegolithFieldColor = new(0.62f, 0.72f, 1f);
        private static readonly Color PadFieldColor = new(0.95f, 0.78f, 0.5f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1TranquilityBase();
            BuildMareCrossing();
            BuildRoom2VentField();
            BuildRoom3RelaySpire();
            BuildRoom4LandingPad();
            BuildGravityFields();
            BuildOverseerArena();
            BuildExtractors(ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(ArenaEndX, 0f, LevelHeight);
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L12: the three approach locks, one wave per manifest row. Built in
        /// <see cref="BuildLevel"/> so a resume past a lock builds it Cleared with
        /// no member ever spawned. The spire lift is held at its bottom stop until
        /// the spire lock clears (the plan's Level 12 ruling): the climb is the only
        /// way up to the highline. The ferry stays east of the lock's exit and is
        /// never held.
        /// </summary>
        private void BuildEncounterLocks() {
            _tranquilityLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockTranquility,
                Area = EncounterLockSpec.Span(TranquilityLockMinX, TranquilityLockMaxX, LevelHeight),
                EntryBarrierX = TranquilityLockMinX - LockBarrierWidth * 0.5f,
                ExitBarrierX = TranquilityLockMaxX + LockBarrierWidth * 0.5f,
                BarrierFloorY = SurfaceY,
                Waves = new[] { WaveFromTable(Wave1RowID, 1, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The troopers come up out of the vent field once the hero is in.
            _ventFieldLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockVentField,
                Area = EncounterLockSpec.Span(VentFieldLockMinX, VentFieldLockMaxX, LevelHeight),
                EntryBarrierX = VentFieldLockMinX - LockBarrierWidth * 0.5f,
                ExitBarrierX = VentFieldLockMaxX + LockBarrierWidth * 0.5f,
                BarrierFloorY = SurfaceY,
                Waves = new[] { WaveFromTable(Wave2RowID, 2, EncounterWaveTrigger.OnEngage) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _relaySpireLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockRelaySpire,
                Area = EncounterLockSpec.Span(RelaySpireLockMinX, RelaySpireLockMaxX, LevelHeight),
                EntryBarrierX = RelaySpireLockMinX - LockBarrierWidth * 0.5f,
                ExitBarrierX = RelaySpireLockMaxX + LockBarrierWidth * 0.5f,
                BarrierFloorY = SurfaceY,
                Waves = new[] {
                    WaveFromTable(Wave3RowID, 3, EncounterWaveTrigger.OnBuild),
                    // The landing-pad garrison comes down once the spire's defenders
                    // are all down: a second wave, not a pile-on.
                    WaveFromTable(Wave4RowID, 4, EncounterWaveTrigger.AfterPreviousCleared)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                // The way on is up: the lift starts once the spire is clear.
                GoDirection = EncounterGoDirection.Up
            });

            _spireLift = GetNodeOrNull<PathMovingPlatform>(SpireLiftNodeName);
            _highlineFerry = GetNodeOrNull<PathMovingPlatform>(HighlineFerryNodeName);
            HoldMoverWhileLockHolds(_relaySpireLock, _spireLift);
        }

        /// <summary>One lock wave: the authored table's entries for <paramref name="wave"/>, in table order.</summary>
        private static EncounterLockWave WaveFromTable(string rowID, int wave, EncounterWaveTrigger trigger,
            int aliveAtMost = 0) {
            var spawns = new List<EncounterLockSpawn>();
            foreach ((string enemyID, int entryWave, Vector2 position) in SpawnTable) {
                if (entryWave == wave) spawns.Add(new EncounterLockSpawn(enemyID, position));
            }
            return new EncounterLockWave {
                RowID = rowID, Spawns = spawns.ToArray(), Trigger = trigger, AliveAtMost = aliveAtMost
            };
        }

        /// <summary>
        /// Plan §2.3's mover hold: <paramref name="mover"/> stands still while
        /// <paramref name="encounterLock"/> holds (Armed or Engaged) and runs again
        /// on its clear. A platform starts at its first waypoint and the lock is
        /// Armed from the build, so it is held at that near stop; a resume past the
        /// lock (restored Cleared) or the test seam releases it at once.
        /// </summary>
        private void HoldMoverWhileLockHolds(EncounterLock encounterLock, PathMovingPlatform mover) {
            if (encounterLock == null || mover == null) return;
            mover.Enabled = !encounterLock.IsHolding;
            encounterLock.StateChanged += (_, _, _) => {
                if (IsInstanceValid(mover) && IsInstanceValid(encounterLock)) mover.Enabled = !encounterLock.IsHolding;
            };
        }

        private void BuildLadder((float X, float Y, float Width)[] rungs) {
            foreach ((float x, float y, float width) in rungs) BuildOneWayPlatform(x, y, width);
        }

        private void BuildRoom1TranquilityBase() {
            BuildRoomBackground("BG_TranquilityBase", 0f, Room2StartX, LevelHeight,
                new Color(0.04f, 0.04f, 0.07f, 0.55f));
            BuildFloor(0f, SurfaceY, Room2StartX);
            BuildLadder(BaseCampLadder);

            // The lander, as a graybox silhouette. Deliberately a low hop and NOT a
            // wall: anything on the main surface line taller than a heavy
            // character's lunar jump would fence Lincoln out of his own level.
            // GR-LV4: 2050 -> 1700, west of the carved lock's exit barrier.
            BuildPlatform(1700f, SurfaceY - 80f, 180f, new Color(0.55f, 0.5f, 0.35f));

            BuildCheckpoint(Checkpoint0Position.X, Checkpoint0Position.Y, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(0f, "lunar_room_tranquility_base", new Color(0.78f, 0.84f, 0.95f));
        }

        // === GR-LV4 (rulings 7a, 7b, 7c, 7c+): the Mare Crossing =================

        public const string MareCrossingStretchID = "level_12.mare_crossing";
        public const string SurveyLineRouteID = "level_12.survey_line";

        /// <summary>The stretch's span: Tranquility's exit barrier to the vent field's entry barrier.</summary>
        public const float MareCrossingMinX = TranquilityLockMaxX + LockBarrierWidth;
        public const float MareCrossingMaxX = Room2StartX;
        /// <summary>Where the line leaves the deck.</summary>
        public const float MareCrossingWestDeckEndX = 2060f;
        /// <summary>Where the line rejoins the deck.</summary>
        public const float MareCrossingEastDeckStartX = 2960f;
        /// <summary>The Survey Line's last platform, which holds the secret.</summary>
        public const float SurveyLineEndX = 2140f;

        /// <summary>
        /// The stretch between Tranquility Base and the vent field. The floor carries on under
        /// it, so nothing here is a pit, and the two vents (the scene's <c>VentRegolith</c> at
        /// 2400 and a second lane at 2720) sweep that floor. The line's tops stand above both
        /// strips. Low gravity makes every hop here easy (<c>Level12TraversalStretchTests</c>
        /// measures it), so the harder way is the floor, not the jump. Every number is provisional
        /// (<c>PROVISIONAL-TRAVERSAL-RULES</c>).
        /// </summary>
        private void BuildMareCrossing() {
            TraversalStretch crossing = BeginStretch(MareCrossingStretchID, "lunar_room_mare_crossing",
                MareCrossingMinX, MareCrossingMaxX, SurfaceY, MareCrossingWestDeckEndX, MareCrossingEastDeckStartX, 0f);
            BuildRoomDecoration(MareCrossingMinX, "lunar_room_mare_crossing", new Color(0.72f, 0.8f, 0.95f));

            TraversalSurface firstStep = BuildStretchStep(crossing, 2170f, 1790f, 200f);
            BuildStretchStep(crossing, 2430f, 1668f, 180f, StretchStepKind.Crumbling, "level_12.crumbling_mare_crossing");
            TraversalSurface surveyStep = BuildStretchStep(crossing, 2650f, 1670f, 200f, StretchStepKind.OneWay);

            StoryCyclicHazard vent = BuildCyclicHazardLane("level_12.vent_mare_crossing", 2720f, SurfaceY, 1.1f, 0.9f, 2.6f, 15);
            vent.Knockback = new Vector2(160f, -460f);

            OptionalRoute survey = BeginOptionalRoute(SurveyLineRouteID, SecretSourceID,
                SecretCachePosition.Value, surveyStep);
            BuildRouteStep(survey, 2710f, 1580f, 120f);
            TraversalSurface burstFrom = BuildRouteStep(survey, 2780f, 1490f, 160f);
            BuildRouteStep(survey, 2570f, 1390f, 180f);
            TraversalSurface end = BuildRouteStep(survey, SurveyLineEndX, 1390f, 220f);
            survey.MarkBurstShortcut(burstFrom, end);
            survey.MarkRiseShortcut(firstStep, end);
        }

        private void BuildRoom2VentField() {
            float width = Room3StartX - Room2StartX;
            BuildRoomBackground("BG_VentField", Room2StartX, width, LevelHeight,
                new Color(0.05f, 0.05f, 0.09f, 0.55f));
            BuildFloor(Room2StartX, SurfaceY, width);
            BuildLadder(VentFieldLadder);

            // High route: ferry across the mare, then the pocket lift to the cache.
            BuildPlatform(5160f, LedgeB, 240f);
            BuildPlatform(5700f, HighDeckY, 240f);

            BuildCheckpoint(Checkpoint1Position.X, Checkpoint1Position.Y, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "lunar_room_vent_field", new Color(0.7f, 0.8f, 0.95f));

            BuildRoomTransitionPair(
                Room2StartX, "lunar_vent_field", "lunar_tranquility_base",
                new Rect2(Room2StartX - 100f, 0f, 3400f, LevelHeight),
                new Rect2(0f, 0f, Room2StartX + 100f, LevelHeight),
                _ => EnterVentField());
        }

        private void BuildRoom3RelaySpire() {
            float width = ArenaStartX - Room3StartX;
            BuildRoomBackground("BG_RelaySpire", Room3StartX, width, LevelHeight,
                new Color(0.05f, 0.06f, 0.1f, 0.55f));
            BuildFloor(Room3StartX, SurfaceY, width);
            BuildLadder(SpireLadder);

            // Mast head: the spire lift lands beside it and the highline departs.
            BuildPlatform(7350f, HighDeckY, 300f);

            // The curtain: an upper and a lower segment with a cargo slot between
            // them at the highline altitude. There is no top to jump over - P5
            // (2026-10-04): the upper segment used to stop at y 0, and a lunar
            // double jump off the mast head (or its Extractor) cleared that by
            // ~700 px, so it is a gate column rising past the level top. Margin 0:
            // the curtain is itself a wall, and both segments keep one x span (the
            // slot below the column is the cargo route, not a lip).
            BuildGateColumn(CurtainWallX + CurtainThickness / 2f, CurtainThickness, CurtainSlotTopY,
                CurtainColor, margin: 0f);
            BuildWall(CurtainWallX, CurtainSlotBottomY, SurfaceY - CurtainSlotBottomY, CurtainColor, CurtainThickness);

            // East of the curtain: the highline landing and the way back up to it.
            BuildPlatform(8860f, HighDeckY, 240f);
            BuildLadder(CurtainStairs);

            BuildCheckpoint(Checkpoint2Position.X, Checkpoint2Position.Y, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "lunar_room_relay_spire", new Color(0.85f, 0.8f, 0.6f));

            BuildRoomTransitionPair(
                Room3StartX, "lunar_relay_spire", "lunar_vent_field_return",
                new Rect2(Room3StartX - 100f, 0f, 2900f, LevelHeight),
                new Rect2(Room2StartX - 100f, 0f, 3400f, LevelHeight),
                _ => EnterRelaySpire());

            // The authored Sarah scene fires at the outpost entrance, one stride
            // short of the pad seam - NOT from OnBossDefeated, so the base class
            // keeps ownership of IsBossDefeated and of the exit chain.
            BuildWaveTrigger("PreBossTrigger", new Vector2(8980f, SurfaceY - 300f),
                StartPreBossBeat, new Vector2(70f, 900f));
        }

        private void BuildRoom4LandingPad() {
            float width = LevelWidth - ArenaStartX;
            BuildRoomBackground("BG_LandingPad", ArenaStartX, width, LevelHeight,
                new Color(0.09f, 0.06f, 0.11f, 0.6f));

            // One continuous pad floor, walled at the east end: the Overseer's
            // phase-3 gravity well can drag the player around the arena but never
            // into a hole, and the west seam stays an open corridor back out.
            BuildFloor(ArenaStartX, SurfaceY, ArenaEndX - ArenaStartX, PadColor);
            BuildPlatform(9900f, SurfaceY - 170f, 280f, PadColor);
            BuildPlatform(10900f, SurfaceY - 170f, 280f, PadColor);

            BuildRoomDecoration(ArenaStartX, "lunar_room_landing_pad", new Color(0.95f, 0.55f, 0.45f));

            // One-shot: this clamp is the arena lock.
            BuildRoomTransition("lunar_landing_pad", new Vector2(ArenaStartX + 60f, LevelHeight / 2f),
                new Rect2(ArenaStartX - 40f, 0f, LevelWidth - ArenaStartX + 40f, LevelHeight),
                new Vector2(80f, LevelHeight),
                _ => EnterLandingPad());

            // The pad floor spans 2,400 px against the Overseer's 9.0-unit ranged
            // band (540 px at BossController's 60 px per unit); the content test
            // asserts the fit against the resource, not against a hardcoded width.
            _overseerEncounter = BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, SurfaceY - 50f),
                "GravityOverseerEncounter", BossArenaBounds);
            if (_overseerEncounter != null) {
                _overseerEncounter.PhaseEntered += OnOverseerPhaseEntered;
                _overseerEncounter.BossDefeated += _ => StandDownOverseerArena();
            }
        }

        /// <summary>
        /// Bidirectional confinement (the Level 3 pattern): a single forward trigger
        /// leaves the camera clamped to the last room forever once the player
        /// backtracks for an extractor, and this level is built around backtracking.
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
        /// template ships a fixed 960x720 shape and these fields are level-sized,
        /// and overriding a SubResource shape on an instanced scene risks mutating a
        /// shape shared between both instantiations (the Level 5 flood-zone
        /// precedent). Graybox-in-code is the sanctioned Florence convention.
        /// </summary>
        private void BuildGravityFields() {
            foreach ((string id, float startX, float endX, float scale) in GravityFieldSpans) {
                _gravityFields.Add(BuildGravityField(id, startX, endX, scale,
                    scale <= RegolithGravityScale ? RegolithFieldColor : PadFieldColor));
            }
        }

        private GravityFieldZone BuildGravityField(string fieldID, float startX, float endX, float scale, Color tint,
            float cycleSeconds = 6f) {
            float width = endX - startX;
            var field = new GravityFieldZone {
                Name = $"GravityField_{fieldID.Replace('.', '_')}",
                FieldID = fieldID,
                // Static field: CycleScales stays empty, so GravityScale applies.
                GravityScale = scale,
                // Read once as the zone readies: it sets the first cycle's length.
                CycleSeconds = cycleSeconds,
                IdleTint = new Color(tint.R, tint.G, tint.B, 0.16f),
                Position = new Vector2(startX + width / 2f, LevelHeight / 2f),
                RewindPolicy = StoryRewindPolicy.ResetToInitialState
            };

            // Children first: GravityFieldZone resolves FieldVisualPath in _Ready,
            // which fires the moment the zone is added to the tree.
            field.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                // Deliberately taller than the level so a vented or knocked-back
                // player can never leave the field through the top or the bottom.
                Shape = new RectangleShape2D { Size = new Vector2(width, LevelHeight * 2.5f) }
            });
            field.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, LevelHeight),
                Position = new Vector2(-width / 2f, -LevelHeight / 2f),
                Color = new Color(1f, 1f, 1f, 0.5f),
                ZIndex = -5,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            AddChild(field);
            return field;
        }

        // === Flow ===

        protected override void OnLevelReady() {
            // Area2D reports its authored initial overlaps on the first physics
            // frame, and a checkpoint resume teleports the player before one ever
            // runs - so a resumed player would stand in a low-gravity level at
            // normal gravity (or, coming out of the pad, in the wrong one).
            // Registering explicitly makes the gravity state a function of where
            // the player actually is, on entry and on every rewind.
            SyncGravityFieldToPlayer();
            if (EventBus.Instance != null && !_rewindBound) {
                _rewindBound = true;
                EventBus.Instance.OnRewindTriggered += OnStoryRewind;
            }
        }

        public override void _ExitTree() {
            if (_rewindBound && EventBus.Instance != null) {
                EventBus.Instance.OnRewindTriggered -= OnStoryRewind;
            }
            _rewindBound = false;
            base._ExitTree();
        }

        private void OnStoryRewind(Vector2 targetPosition) => SyncGravityFieldToPlayer();

        /// <summary>Authored field whose span contains <paramref name="x"/>; spans tile the level.</summary>
        public GravityFieldZone GravityFieldFor(float x) {
            for (int index = 0; index < GravityFieldSpans.Length && index < _gravityFields.Count; index++) {
                (string _, float startX, float endX, float _) = GravityFieldSpans[index];
                bool isLast = index == GravityFieldSpans.Length - 1;
                bool inside = x >= startX && (isLast ? x <= endX : x < endX);
                if (inside && IsInstanceValid(_gravityFields[index])) return _gravityFields[index];
            }
            return null;
        }

        /// <summary>
        /// Puts the player in exactly the field they are standing in and out of
        /// every other one, through the shared
        /// <see cref="GravityFieldZone.SyncPlayerToField"/> helper. The target comes
        /// from this level's authored span table rather than the helper's geometric
        /// lookup, because the spans meet exactly and
        /// <see cref="GravityFieldFor"/> owns the seam rule (half-open except the
        /// last span). A level with scene-authored fields and no span table calls
        /// <see cref="GravityFieldZone.SyncPlayerToContainingField"/> instead.
        /// </summary>
        public void SyncGravityFieldToPlayer() {
            if (Player == null || !IsInstanceValid(Player)) return;
            GravityFieldZone.SyncPlayerToField(_gravityFields, Player, GravityFieldFor(Player.Position.X));
        }

        // GU-L12: the waves are the locks' (BuildEncounterLocks). The room seams
        // post the level's own objective, which the base re-posts when a lock
        // engaged on top of it clears or reverts.
        private void EnterVentField() => SetObjective("lunar_objective_vent_field");

        private void EnterRelaySpire() => SetObjective("lunar_objective_climb_spire");

        private void EnterLandingPad() => SetObjective("lunar_objective_landing_pad");

        private void StartPreBossBeat() {
            if (_preBossShown) return;
            _preBossShown = true;
            // G4: the thesis scene lands before the Overseer, never after it falls.
            StartPreBossDialogue(PreBossDialogueID);
        }

        // === Checkpoint resume ===

        // Package 12 W2 (GAP-13) explicit encounter baseline. GU-L12: the
        // encounters are the three locks, and every anchor lists every lock whose
        // area ends before it (a resume builds those Cleared; anything later
        // re-arms in full). The pre-boss trigger stands between checkpoint 2 and
        // the pad, so a resume there deliberately leaves it armed: the thesis
        // scene must still land before the Overseer. Lunar has no other anchor
        // state, so MarkWavesClearedThrough is not overridden.
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = Array.Empty<string>(),
                [Checkpoint1] = new[] { LockTranquility, LockVentField },
                [Checkpoint2] = new[] { LockTranquility, LockVentField, LockRelaySpire }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;
    }
}
