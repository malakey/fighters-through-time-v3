using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 7 - Nassau, 1715. The Golden Age of Piracy.
    ///
    /// The Republic of Pirates is at its height (S31: it never declared
    /// independence, and it ended in 1718 with the King's pardon), and the Severed
    /// have moored their siphons in its harbor, guarded by Linked crews with
    /// sub-aquatic torpedoes and a chronal targeting grid. The level is therefore a ship-to-ship crossing, not a walk:
    /// four rooms strung between hulls with open water in two of them.
    ///
    /// Era identity, all mechanically real:
    /// <list type="bullet">
    /// <item><b>Rope swings.</b> Three <see cref="PendulumAnchor"/>s hang over a
    ///       <see cref="GapWidth"/>-px channel between two hulls. There is no floor
    ///       under them, and the span is far wider than any character can jump, so
    ///       the swing is the route. Each anchor carries a
    ///       <see cref="LedgeGrabPoint"/>: grabbing reuses the tested LedgeHanging
    ///       state, the anchor carries its occupant along the arc, and letting go
    ///       hands over the measured tangential velocity.</item>
    /// <item><b>Boarding skiffs.</b> Three <see cref="PathMovingPlatform"/>s ferry
    ///       the player between fixed pilings across the second channel.</item>
    /// <item><b>Mortar fire.</b> Telegraphed <see cref="StoryCyclicHazard"/>s
    ///       (warning -> active -> cooldown) walk the quay and the channel.</item>
    /// <item><b>The burning flagship.</b> The boss deck is a <i>static</i> authored
    ///       slope listing to starboard, on fire, with two horizontal wooden yards.</item>
    /// </list>
    ///
    /// Deliberate contrasts with its neighbours. Level 10 (Globe) reuses
    /// <see cref="PendulumAnchor"/> for tight indoor stage rigging; Nassau's are long
    /// open-air swings over water between separate hulls, chained so no single grab
    /// is enough. Level 5 (Titanic) lists <i>and floods</i> through an escalating
    /// <see cref="RisingWaterZone"/>; Nassau's list never moves - this level has no
    /// rising water at all, and its sea is a fixed surface that fishes you out onto
    /// the deck you launched from rather than drowning you.
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: 10 standards, 1 elite,
    /// 1 boss, 3 extractors. Roster is the Nassau era pair - `laser_pistol_deckhand`
    /// (rapid low-damage ranged) - mixed with `chrono_slasher` Severed troopers, and the
    /// single elite is the `overcharged_cannon_master`.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L4). Both
    /// channels are open water, so the two locks sit on the decks and their
    /// barriers stand on deck: the <b>quay</b> (300–3500, the whole Nassau quay
    /// and the Ranger's waist up to the launch yard's lip, so the ropes are out of
    /// reach until it clears) and the <b>boarding action</b> (5780–8480, from the
    /// Bonnet's waist across the boarding channel to the flagship's ramp). The
    /// boarding crew on the ramp musters once the Bonnet and the gun pier are
    /// clear, and the Eraser, which used to spawn over open water at 7900, is
    /// among them on the ramp. No skiff or rope sweeps across a barrier, every
    /// sea haul-out lands on a deck inside the lock the hero is in (or one already
    /// cleared), and the Middle and PreBoss anchors stand outside both locks: the
    /// Middle on the Bonnet's waist (5700), the PreBoss on the flagship's west
    /// deck (8680), clear of both elites.</para>
    /// </summary>
    public partial class Level07Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string NassauLevelID = "level_07_nassau";

        public override string LevelID => NassauLevelID;
        public override CampaignLevel Level => CampaignLevel.Nassau;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 7 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 630f;
        public override string LevelTitleKey => "nassau_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_07_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(240f, 850f);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the crane jib above the quay (Platform 900/640, top 632), climbed from the
        // low crate at 500 — off the harbour floor route, above the mortar's strike line.
        protected override Vector2? SecretCachePosition => new(900f, 537f);

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor on the flagship's west deck, outside the mortar channel, short of the arena
        // (9350). GU-L4 (2026-10-13): 8700 -> 9100, east of FlagshipFireA, because the PreBoss
        // anchor moved onto the west deck (8680). FlagshipFireA's column (scene, x 8840–8960)
        // ends 140 px west of the font, so the hero's body anywhere in the 90 px channel
        // circle keeps 30 px from it, and the circle ends 160 px short of the arena.
        protected override Vector2? RestorationFontPosition => new(9100f, ListDeckWestY);

        public override Rect2 LevelBounds => new(0f, 0f, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "nassau_objective_reach_moorings";
        protected override string BossObjectiveKey => "nassau_objective_defeat_boss";
        protected override string CompletionObjectiveKey => "nassau_objective_complete";

        // === Dimensions ===

        public const float LevelWidth = 10600f;
        /// <summary>Taller than Florence: the rigging climbs and the swing arc need headroom.</summary>
        public const float LevelHeight = 1200f;

        /// <summary>Every walkable hull deck in the level sits on this line.</summary>
        public const float DeckY = 900f;
        private const float EnemyDeckY = 850f;

        public const float Room1StartX = 0f;
        public const float Room2StartX = 2800f;
        public const float Room3StartX = 6000f;
        public const float Room4StartX = 8500f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0f, 2800f, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0f, 3200f, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX, 0f, 2500f, LevelHeight);
        public static readonly Rect2 Room4CameraBounds = new(Room4StartX, 0f, LevelWidth - Room4StartX, LevelHeight);

        // === Room 2: the rope-swing channel ===

        /// <summary>East edge of the Ranger's rigging yard - where the player lets go of solid ground.</summary>
        public const float SwingGapStartX = 3600f;
        /// <summary>West edge of the Bonnet's receiving yard.</summary>
        public const float SwingGapEndX = 5600f;
        public const float GapWidth = SwingGapEndX - SwingGapStartX;

        /// <summary>Rigging yard the swing launches from (top surface).</summary>
        public const float LaunchYardY = 672f;
        /// <summary>Receiving yard on the far hull (top surface).</summary>
        public const float ReceivingYardY = 692f;
        /// <summary>West lip of the receiving yard - the first solid thing east of the water.</summary>
        public const float ReceivingYardWestX = 5550f;
        /// <summary>East end of the receiving yard, over the Bonnet's waist (the yard is solid, 400 px).</summary>
        public const float ReceivingYardEastX = 5950f;

        /// <summary>
        /// Anchor pivot line. The template hangs its grab area 320 px below the pivot
        /// and its hang marker 360 px below, so at rest the player hangs at y = 760
        /// and the arc tops out at y = 622 at the swing extremes.
        /// </summary>
        public const float AnchorPivotY = 400f;
        /// <summary>Template rope length from pivot to the LedgeGrabPoint centre.</summary>
        public const float AnchorGrabRadius = 320f;
        /// <summary>Template rope length from pivot to the HangAnchor the occupant is pinned to.</summary>
        public const float AnchorHangRadius = 360f;
        /// <summary>
        /// Swing amplitude. A rope's horizontal reach is <c>radius * sin(amplitude)</c>,
        /// so this decides how much of the 1,950 px launch-yard-to-receiving-yard span
        /// the three ropes cover and how much is left over as player hops. Raised
        /// 45 -> 52 by the 2026-08-10 movement retune: the roster's jump height dropped
        /// about 20%, so the ropes reach further to keep every hop inside the weakest
        /// character's budget (gameplay-feel plan section 9, B2).
        /// </summary>
        public const float AnchorAmplitudeDegrees = 52f;

        /// <summary>Authored pivot X of each rope, west to east. Scene positions are pinned to these.</summary>
        public static readonly float[] AnchorPivotsX = { 3950f, 4570f, 5190f };
        public static readonly string[] AnchorNodeNames = { "RopeSwingA", "RopeSwingB", "RopeSwingC" };

        // === Room 3: the boarding channel ===

        public const float ChannelStartX = 6500f;
        public const float ChannelEndX = 8000f;

        /// <summary>Fixed pilings in the channel: (centre X, top Y, width).</summary>
        public static readonly (float CenterX, float TopY, float Width)[] Pilings = {
            (7000f, 840f, 140f),
            (7550f, 800f, 140f),
            // Off-route perch above the line, guarding the third extractor.
            (7250f, 760f, 140f)
        };

        public static readonly string[] SkiffNodeNames = { "SkiffA", "SkiffB", "SkiffC" };

        /// <summary>
        /// Authored skiff runs: (authored position, west waypoint offset, east waypoint
        /// offset, half width). The scene is pinned to these and
        /// <see cref="SkiffTravelSpans"/> derives the water the player is over while
        /// riding, so a checkpoint can be proven never to land on a departed skiff.
        /// <para>G3 (2026-10-04): the first skiff's west stop used to slide 60 px into
        /// the gun pier (its deck flush with the pier top, its hull inside the pier);
        /// it now berths against the pier's face at <see cref="ChannelStartX"/>, the
        /// same boarding edge, without entering the geometry.</para>
        /// </summary>
        public static readonly (Vector2 Position, float WestOffsetX, float EastOffsetX, float HalfWidth)[] Skiffs = {
            (new Vector2(6700f, 916f), -80f, 200f, 120f),
            (new Vector2(7275f, 876f), -155f, 155f, 120f),
            (new Vector2(7830f, 866f), -150f, 150f, 120f)
        };

        // === Room 4: the burning flagship ===

        /// <summary>
        /// Static list to starboard. Stepped slabs joined by short rotated ramps -
        /// a fully rotated deck plan produces collision seams at the joins (the
        /// Level 5 finding), and unlike Level 5 none of this ever moves.
        /// </summary>
        public const float ListDeckWestY = 880f;
        public const float ListDeckMidY = 920f;
        public const float ListDeckEastY = 960f;

        public static float ArenaWidth => LevelWidth - Room4StartX;

        // === Checkpoints ===

        public const string Checkpoint0 = NassauLevelID + "_checkpoint_0";
        public const string Checkpoint1 = NassauLevelID + "_checkpoint_1";
        public const string Checkpoint2 = NassauLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        /// <summary>
        /// Authored checkpoint anchors. Every one stands on a span in <see cref="SolidDeckSpans"/>.
        /// GU-L4 (2026-10-13): the Middle anchor moved 5800 -> 5700 (west of the
        /// boarding lock, which starts at 5780) and the PreBoss anchor 8420 -> 8680,
        /// off the boarding ramp (inside the boarding fight, beside both elites) onto
        /// the flagship's west deck, past the lock's exit and its swing reach.
        /// FIX-LEVEL (2026-10-13): the Middle anchor moved again, from the Bonnet's
        /// deck at 5700 up onto the receiving yard at 5650, where rope C lands the
        /// hero. At 5700 it stood under the solid yard, reachable only from the
        /// yard's east end (5950), past the boarding lock's engage line (5824): the
        /// lock engaged and shut its entry barrier between the hero and the anchor,
        /// so the boarding fight had no checkpoint. On the yard it is struck before
        /// the hero walks on (its surface ends at 5680; the engage line is 5824).
        /// </summary>
        public static readonly (string ID, Vector2 Position)[] CheckpointPlacements = {
            (Checkpoint0, new Vector2(240f, DeckY - 20f)),
            (Checkpoint1, new Vector2(5650f, ReceivingYardY - 20f)),
            (Checkpoint2, new Vector2(8680f, ListDeckWestY - 20f))
        };

        // === Structural spans (single source for geometry and for the content tests) ===

        /// <summary>East end of the flagship's west deck (the first listing ramp starts beyond it).</summary>
        public const float FlagshipWestDeckEndX = Room4StartX + 720f;
        /// <summary>The flagship's mid deck, between the two listing ramps.</summary>
        private const float FlagshipMidDeckWestX = 9380f;
        private const float FlagshipMidDeckEndX = FlagshipMidDeckWestX + 640f;
        private const float FlagshipEastDeckWestX = 10180f;

        /// <summary>
        /// The flagship's two listing ramps, west to east, each authored by its
        /// joints (2026-10-13, the Level 5 fix applied here): <c>Low</c>, where its
        /// slope meets the lower deck east of it, and <c>High</c>, the upper deck's
        /// east top corner. Authored by centre, width and a 10.5° tilt, each ended
        /// 5.2 px under its upper deck's east end, a riser that stopped a hero
        /// walking west uphill. The low joints are the old slopes' east ends, so the
        /// ramps keep their footprints and steepen to about 12°.
        /// </summary>
        public static readonly (Vector2 Low, Vector2 High)[] ListingRampJoints = {
            (new Vector2(9408f, ListDeckMidY), new Vector2(FlagshipWestDeckEndX, ListDeckWestY)),
            (new Vector2(10208f, ListDeckEastY), new Vector2(FlagshipMidDeckEndX, ListDeckMidY))
        };

        /// <summary>Continuous solid deck runs: (start X, end X, top Y).</summary>
        public static readonly (float StartX, float EndX, float TopY)[] SolidDeckSpans = {
            (Room1StartX, Room2StartX, DeckY),          // Nassau quay
            (Room2StartX, SwingGapStartX, DeckY),       // the Ranger's waist
            (SwingGapEndX, Room3StartX, DeckY),         // the Bonnet's waist
            (Room3StartX, ChannelStartX, DeckY),        // the gun pier
            (ChannelEndX, Room4StartX, DeckY),          // the flagship boarding ramp
            // GU-L4: the flagship's west deck, where the PreBoss anchor now stands.
            (Room4StartX, FlagshipWestDeckEndX, ListDeckWestY),
            // FIX-LEVEL: the receiving yard's run over the Bonnet's hull (its
            // 50 px west lip overhangs the water and is left out), where the
            // Middle anchor now stands.
            (SwingGapEndX, ReceivingYardEastX, ReceivingYardY)
        };

        /// <summary>Open water the player cannot walk on: (start X, end X, rescue anchor).</summary>
        public static readonly (string ID, float StartX, float EndX, Vector2 Rescue)[] WaterSpans = {
            ("level_07.sea_ranger_channel", SwingGapStartX, SwingGapEndX, new Vector2(3400f, DeckY - 40f)),
            ("level_07.sea_boarding_channel", ChannelStartX, ChannelEndX, new Vector2(6350f, DeckY - 40f))
        };

        /// <summary>Sea surface line; the undertow area starts here and runs to the level floor.</summary>
        public const float SeaSurfaceY = 1040f;
        /// <summary>What going over the side costs. Heavy, but never an instadeath - rewind stays meaningful.</summary>
        public const int UndertowDamage = 18;

        // === Encounters (locked: 10 standards / 1 elite / 1 boss / 3 extractors) ===

        public const string StandardEnemyID = "laser_pistol_deckhand";
        public const string CultistEnemyID = "chrono_slasher";
        public const string EliteEnemyID = "overcharged_cannon_master";
        /// <summary>
        /// V7.6 (Package 11 A7a): the Eraser, salted through Levels 7-15. Not an
        /// era enemy - it is a Severed hunter that followed the hero here, which
        /// is exactly why it reads wrong against a 1715 gun pier. Never authored into a room
        /// with a Chrono-Warden before Act III (EncounterCompositionTests).
        /// </summary>
        public const string EraserEnemyID = "unbound_eraser";
        public const string BossResourcePath = "res://resources/Bosses/dread_admiral.tres";

        /// <summary>
        /// P1 (2026-10-04): the burning flagship. West edge is
        /// <see cref="BossArenaWestX"/> (spawn 10250 - 900); east edge is the stern
        /// wall's inner face.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, LevelWidth - 20f - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10250f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 900 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 900f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

        /// <summary>
        /// Authored Normal-difficulty spawn table, one wave per lock wave and per
        /// manifest row (<c>level_07.wave{n}</c>). Difficulty scaling takes a prefix
        /// of each wave. GU-L4 (2026-10-13) re-partitioned it to the two locks: the
        /// quay lock takes waves 1 (pre-placed) and 2 (the Ranger's crew, when one
        /// of wave 1 is left); the boarding lock takes waves 3 (the Bonnet and the
        /// gun pier, pre-placed) and 4 (the flagship's boarding crew, mustering on
        /// the ramp once wave 3 is down). The deckhand that stood on the Bonnet in
        /// wave 2 joined wave 3, and the Eraser — which spawned at 7900, over open
        /// water, and fell forever whenever skiff C was not under it — stands on the
        /// ramp in wave 4. At most seven combatants are alive at once, inside the
        /// level_07_nassau_pools warm counts (standard 12, elite 4).
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - the quay, under the first shells.
            (StandardEnemyID, 1, new Vector2(1200f, EnemyDeckY)),
            (CultistEnemyID, 1, new Vector2(1750f, EnemyDeckY)),
            (StandardEnemyID, 1, new Vector2(2500f, EnemyDeckY)),
            // Wave 2 - the Ranger's crew, including a marksman on the launch yard.
            (CultistEnemyID, 2, new Vector2(3000f, EnemyDeckY)),
            (StandardEnemyID, 2, new Vector2(3450f, LaunchYardY - 30f)),
            // Wave 3 - the Bonnet's waist and the gun pier.
            (StandardEnemyID, 3, new Vector2(5900f, EnemyDeckY)),
            (CultistEnemyID, 3, new Vector2(6420f, EnemyDeckY)),
            (StandardEnemyID, 3, new Vector2(6180f, EnemyDeckY)),
            // Wave 4 - the flagship's boarding ramp.
            (StandardEnemyID, 4, new Vector2(8080f, EnemyDeckY)),
            (CultistEnemyID, 4, new Vector2(8380f, EnemyDeckY)),
            (EliteEnemyID, 4, new Vector2(8230f, EnemyDeckY)),
            // The hunter on the boarding ramp: a visored silhouette among the
            // flagship's crew, in nothing anyone in 1715 could have made.
            (EraserEnemyID, 4, new Vector2(8150f, EnemyDeckY))
        };

        /// <summary>The number of authored waves (lock waves drawn from <see cref="SpawnTable"/>).</summary>
        public const int WaveCount = 4;

        /// <summary>Three extractors, each up an off-route climb. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_07_extractor_0", new Vector2(1900f, 560f)),
            ("level_07_extractor_1", new Vector2(3150f, 420f)),
            ("level_07_extractor_2", new Vector2(7250f, 700f))
        };

        public static int StandardEnemyCount => CountOfTier(EnemyTier.Standard);
        public static int EliteEnemyCount => CountOfTier(EnemyTier.Elite);
        public static int ExtractorCount => ExtractorPlacements.Length;

        /// <summary>How many Erasers this level authors. Test seam for the salt rules.</summary>
        public static int EraserCount {
            get {
                int total = 0;
                foreach ((string id, int _, Vector2 _) in SpawnTable) if (id == EraserEnemyID) total++;
                return total;
            }
        }

        private static int CountOfTier(EnemyTier tier) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) {
                bool isElite = id == EliteEnemyID || id == EraserEnemyID;
                if (isElite == (tier == EnemyTier.Elite)) total++;
            }
            return total;
        }

        /// <summary>Horizontal envelope a skiff sweeps, so a checkpoint can be proven clear of it.</summary>
        public static IEnumerable<(float StartX, float EndX)> SkiffTravelSpans {
            get {
                foreach ((Vector2 position, float west, float east, float half) in Skiffs) {
                    yield return (position.X + west - half, position.X + east + half);
                }
            }
        }

        // === GU-L4 (2026-10-12): the two encounter locks ======================

        /// <summary>The F10 encounter IDs of the two locks (they start with the level ID).</summary>
        public const string LockQuay = NassauLevelID + "_lock_quay";
        public const string LockBoarding = NassauLevelID + "_lock_boarding";

        /// <summary>
        /// The quay lock: the Nassau quay and the Ranger's waist, east of the entry
        /// anchor (240) to the launch yard before the rope channel. Its exit
        /// barrier stands 100 px west of the gap, beyond the last crew member;
        /// no rope is reachable before the clear. 3200 px, one authored room.
        /// </summary>
        public const float QuayLockMinX = 300f;
        public const float QuayLockMaxX = 3500f;
        /// <summary>
        /// The boarding lock: from the Bonnet's waist (east of the Middle anchor at
        /// 5700) across the boarding channel to the flagship's ramp, its exit on the
        /// ramp's last 20 px. The channel is water: the lock's barriers stand on the
        /// two decks at its ends, and every skiff runs inside it.
        /// </summary>
        public const float BoardingLockMinX = 5780f;
        public const float BoardingLockMaxX = Room4StartX - EncounterLockRules.DefaultBarrierWidthPixels;

        private EncounterLock _quayLock;
        private EncounterLock _boardingLock;
        private int _boardingRegisteredSeen;

        public EncounterLock QuayLock => _quayLock;
        public EncounterLock BoardingLock => _boardingLock;

        /// <summary>The manifest row of authored wave <paramref name="wave"/>.</summary>
        public static string WaveRowID(int wave) => $"level_07.wave{wave}";

        // === Runtime state ===

        private readonly List<PendulumAnchor> _anchors = new();
        private readonly List<PathMovingPlatform> _skiffs = new();
        private bool _flagshipSighted;

        public IReadOnlyList<PendulumAnchor> RopeAnchors => _anchors;
        public IReadOnlyList<PathMovingPlatform> BoardingSkiffs => _skiffs;

        // === Era palette: tarred timber, tropical night, powder flash ===

        protected override Color FloorColor => new(0.20f, 0.15f, 0.11f);
        protected override Color FloorEdgeColor => new(0.52f, 0.40f, 0.24f);
        protected override Color PlatformColor => new(0.33f, 0.25f, 0.16f);
        protected override Color WallColor => new(0.13f, 0.11f, 0.09f);
        protected override Color HazardColor => new(0.68f, 0.28f, 0.09f);

        private static readonly Color SeaColor = new(0.06f, 0.19f, 0.28f, 0.55f);
        private static readonly Color ListRampColor = new(0.24f, 0.17f, 0.12f);
        private static readonly Color YardColor = new(0.40f, 0.30f, 0.18f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1Quay();
            BuildRoom2RiggingCrossing();
            BuildRoom3BoardingChannel();
            BuildRoom4BurningFlagship();
            BuildSeaSurfaces();
            BuildExtractors(ExtractorPlacements);
            BindAuthoredEraMechanics();
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L4: the two decks with enemies as encounter locks, one lock wave per
        /// manifest row. Built here, before the resume baseline, so a resume past
        /// a lock builds it Cleared.
        /// </summary>
        private void BuildEncounterLocks() {
            float half = EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
            // The quay: the dockside crew is already fighting; the Ranger's crew
            // comes down the gangplank when one of them is left.
            _quayLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockQuay,
                Area = EncounterLockSpec.Span(QuayLockMinX, QuayLockMaxX, LevelHeight),
                EntryBarrierX = QuayLockMinX - half,
                ExitBarrierX = QuayLockMaxX + half,
                BarrierFloorY = DeckY,
                Waves = new[] {
                    WaveFromTable(1, EncounterWaveTrigger.OnBuild),
                    WaveFromTable(2, EncounterWaveTrigger.WhenAliveAtMost, aliveAtMost: 1)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The boarding action: the Bonnet and the gun pier are held; the
            // flagship's boarding crew musters on the ramp once they are down.
            _boardingLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockBoarding,
                Area = EncounterLockSpec.Span(BoardingLockMinX, BoardingLockMaxX, LevelHeight),
                EntryBarrierX = BoardingLockMinX - half,
                ExitBarrierX = BoardingLockMaxX + half,
                BarrierFloorY = DeckY,
                Waves = new[] {
                    WaveFromTable(3, EncounterWaveTrigger.OnBuild),
                    WaveFromTable(4, EncounterWaveTrigger.AfterPreviousCleared)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
        }

        /// <summary>One lock wave: the authored <see cref="SpawnTable"/> entries of <paramref name="wave"/>, row <c>level_07.wave{n}</c>.</summary>
        private static EncounterLockWave WaveFromTable(int wave, EncounterWaveTrigger trigger, int aliveAtMost = 0) {
            var spawns = new List<EncounterLockSpawn>();
            foreach ((string enemyID, int entryWave, Vector2 position) in SpawnTable) {
                if (entryWave == wave) spawns.Add(new EncounterLockSpawn(enemyID, position));
            }
            return new EncounterLockWave {
                RowID = WaveRowID(wave),
                Spawns = spawns.ToArray(),
                Trigger = trigger,
                AliveAtMost = aliveAtMost
            };
        }

        /// <summary>Room 1: the free port itself, already under naval bombardment.</summary>
        private void BuildRoom1Quay() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2800f, LevelHeight, new Color(0.05f, 0.08f, 0.11f, 0.4f));
            BuildFloor(Room1StartX, DeckY, 2800f);
            BuildWall(-20f, 0f, LevelHeight);

            BuildPlatform(500f, 760f, 240f);
            BuildPlatform(900f, 640f, 220f);
            BuildPlatform(1400f, 740f, 260f);
            // Careening crane, off the direct line: two steps up to the first extractor.
            BuildPlatform(1650f, 680f, 180f);
            BuildPlatform(1900f, 620f, 240f);
            BuildPlatform(2350f, 760f, 240f);

            BuildCheckpoint(240f, DeckY - 20f, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(Room1StartX, "nassau_room_harbour", new Color(0.92f, 0.78f, 0.42f));
            BuildRoomTransition("nassau_room_harbour", new Vector2(300f, 600f), Room1CameraBounds,
                new Vector2(80f, LevelHeight));
        }

        /// <summary>
        /// Room 2: the signature crossing. The Ranger's rigging climbs to a launch
        /// yard; east of it there is nothing but water for <see cref="GapWidth"/> px,
        /// and the only handholds are the three ropes.
        /// </summary>
        private void BuildRoom2RiggingCrossing() {
            BuildRoomBackground("BG_Room2", Room2StartX, 3200f, LevelHeight, new Color(0.04f, 0.09f, 0.14f, 0.45f));

            // The Ranger, moored alongside.
            BuildFloor(Room2StartX, DeckY, SwingGapStartX - Room2StartX);
            BuildPlatform(2950f, 820f, 200f);
            BuildPlatform(3200f, 760f, 200f);
            // Launch yard: 3300..3600, top surface at LaunchYardY.
            BuildPlatform(3450f, LaunchYardY + DefaultPlatformThickness / 2f, 300f, YardColor);
            // Ratlines up to the crow's nest, holding the second extractor.
            BuildPlatform(2900f, 640f, 160f);
            BuildPlatform(3020f, 540f, 160f);
            BuildPlatform(3150f, 480f, 180f);

            // The Bonnet, taken as a prize and drifting alongside.
            BuildFloor(SwingGapEndX, DeckY, Room3StartX - SwingGapEndX);
            BuildPlatform((ReceivingYardWestX + ReceivingYardEastX) / 2f, ReceivingYardY + DefaultPlatformThickness / 2f,
                ReceivingYardEastX - ReceivingYardWestX, YardColor);

            Vector2 middle = CheckpointPlacements[1].Position;
            BuildCheckpoint(middle.X, middle.Y, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "nassau_room_rigging", new Color(0.55f, 0.82f, 0.9f));
            // GU-L4: the Ranger's crew is the quay lock's second wave now, so the
            // seam only moves the camera.
            BuildRoomTransitionPair("nassau_room_harbour_return", Room1CameraBounds,
                "nassau_room_rigging", Room2CameraBounds, Room2StartX, null);
        }

        /// <summary>
        /// Room 3: the boarding action. Fixed pilings plus three skiffs under walking
        /// mortar fire; the skiffs are the only way onto the flagship's ramp.
        /// </summary>
        private void BuildRoom3BoardingChannel() {
            BuildRoomBackground("BG_Room3", Room3StartX, 2500f, LevelHeight, new Color(0.05f, 0.10f, 0.13f, 0.45f));

            BuildFloor(Room3StartX, DeckY, ChannelStartX - Room3StartX);
            BuildFloor(ChannelEndX, DeckY, Room4StartX - ChannelEndX);

            foreach ((float centerX, float topY, float width) in Pilings) {
                BuildPlatform(centerX, topY + DefaultPlatformThickness / 2f, width, YardColor);
            }

            // GU-L4: the PreBoss anchor stands on the flagship's west deck, past the
            // boarding lock (it used to stand on the ramp, beside both elites).
            Vector2 preBoss = CheckpointPlacements[2].Position;
            BuildCheckpoint(preBoss.X, preBoss.Y, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "nassau_room_channel", new Color(0.9f, 0.6f, 0.3f));
            BuildRoomTransitionPair("nassau_room_rigging_return", Room2CameraBounds,
                "nassau_room_channel", Room3CameraBounds, Room3StartX, _ => EnterBoardingChannel());
        }

        /// <summary>
        /// Bidirectional room seam (the Level 3 pattern). A single one-shot trigger
        /// leaves the camera clamped to the room ahead forever once the player walks
        /// back, and both of Nassau's first two seams are contiguous deck the player
        /// really can walk back across. Two repeatable 80 px triggers straddle the
        /// seam 160 px apart, so their bodies never overlap and each crossing binds
        /// the room the player is actually standing in.
        ///
        /// The boss seam deliberately stays a single one-shot trigger: that clamp is
        /// the arena lock.
        /// </summary>
        private void BuildRoomTransitionPair(
            string westRoomID, Rect2 westBounds,
            string eastRoomID, Rect2 eastBounds,
            float seamX, System.Action<string> onEastEntered) {
            var size = new Vector2(80f, LevelHeight);
            RoomTransitionTrigger west = BuildRoomTransition(
                westRoomID, new Vector2(seamX - 80f, LevelHeight / 2f), westBounds, size);
            west.ActivateOnce = false;
            RoomTransitionTrigger east = BuildRoomTransition(
                eastRoomID, new Vector2(seamX + 80f, LevelHeight / 2f), eastBounds, size, onEastEntered);
            east.ActivateOnce = false;
        }

        /// <summary>
        /// Room 4: the Dread Admiral's flagship, burning and listing to starboard.
        /// The list is a static authored slope - stepped slabs bridged by short rotated
        /// ramps - and the two wooden yards stay dead horizontal above it, which is
        /// what makes the tilt legible.
        /// </summary>
        private void BuildRoom4BurningFlagship() {
            BuildRoomBackground("BG_Room4", Room4StartX, ArenaWidth, LevelHeight, new Color(0.16f, 0.07f, 0.05f, 0.5f));

            BuildFloor(Room4StartX, ListDeckWestY, FlagshipWestDeckEndX - Room4StartX);
            BuildRampBetweenJoints(ListingRampJoints[0].Low, ListingRampJoints[0].High, ListRampColor);
            BuildFloor(FlagshipMidDeckWestX, ListDeckMidY, FlagshipMidDeckEndX - FlagshipMidDeckWestX);
            BuildRampBetweenJoints(ListingRampJoints[1].Low, ListingRampJoints[1].High, ListRampColor);
            BuildFloor(FlagshipEastDeckWestX, ListDeckEastY, LevelWidth - FlagshipEastDeckWestX);

            // Horizontal wooden yards, per the plan's arena note.
            BuildPlatform(9300f, 700f, 300f, YardColor);
            BuildPlatform(10100f, 700f, 300f, YardColor);

            BuildWall(LevelWidth - 20f, 0f, LevelHeight);

            BuildRoomDecoration(Room4StartX, "nassau_room_flagship", new Color(0.98f, 0.42f, 0.32f));
            BuildRoomTransition("nassau_room_flagship", new Vector2(Room4StartX + 40f, 600f), Room4CameraBounds,
                new Vector2(80f, LevelHeight));

            // 2100 px of deck against the Admiral's authored 11.0-unit ranged band
            // (660 px at BossController's 60 px per unit), so the gatling zoner can
            // actually use its range instead of being pinned in melee.
            BuildBossEncounter(BossResourcePath, new Vector2(BossSpawnX, ListDeckEastY - 40f),
                "DreadAdmiralEncounter", BossArenaBounds);
        }

        /// <summary>
        /// The sea. Not a rising water zone and not a kill plane: going over the side
        /// costs <see cref="UndertowDamage"/> and dumps the player back on the deck
        /// they launched from, so a missed swing is a setback rather than a death.
        /// </summary>
        private void BuildSeaSurfaces() {
            foreach ((string id, float startX, float endX, Vector2 rescue) in WaterSpans) {
                float width = endX - startX;
                var sea = new Area2D {
                    Name = $"Sea_{id.Replace("level_07.", "")}",
                    Position = new Vector2(startX + width / 2f, SeaSurfaceY),
                    CollisionLayer = CollisionLayers.Trigger,
                    CollisionMask = CollisionLayers.Player
                };
                float depth = LevelHeight - SeaSurfaceY + 400f;
                sea.AddChild(new CollisionShape2D {
                    Shape = new RectangleShape2D { Size = new Vector2(width, depth) },
                    Position = new Vector2(0f, depth / 2f)
                });
                sea.AddChild(new ColorRect {
                    Name = "Visual",
                    Size = new Vector2(width, depth),
                    Position = new Vector2(-width / 2f, 0f),
                    Color = SeaColor,
                    ZIndex = 4,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
                Vector2 anchor = rescue;
                sea.BodyEntered += body => {
                    if (body is not PlayerController player) return;
                    // The haul-out damages the player, which can cascade into
                    // a lethal-hit rewind; mark the physics callback so those
                    // systems defer the engine-blocked writes.
                    using var scope = PhysicsCallbackGuard.Enter();
                    HaulOutOfTheWater(player, anchor);
                };
                AddChild(sea);
            }
        }

        /// <summary>Fishes a player out of the sea onto the authored near-side deck.</summary>
        public void HaulOutOfTheWater(PlayerController player, Vector2 rescueAnchor) {
            if (player == null || !IsInstanceValid(player)) return;
            // V7.3: environmental chokepoint — Defy/echo/meter accounting.
            player.ApplyEnvironmentalDamage(UndertowDamage);
            if (player.CurrentHP <= 0) return;
            player.GlobalPosition = rescueAnchor;
            player.Velocity = Vector2.Zero;
        }

        /// <summary>
        /// Collects the scene-authored era mechanics. Ropes and skiffs are template
        /// instances rather than code-built so their tuning stays inspector-editable,
        /// and the content tests pin their scene positions to the constants above.
        /// </summary>
        private void BindAuthoredEraMechanics() {
            _anchors.Clear();
            foreach (string name in AnchorNodeNames) {
                if (GetNodeOrNull<PendulumAnchor>(name) is PendulumAnchor anchor) _anchors.Add(anchor);
            }
            _skiffs.Clear();
            foreach (string name in SkiffNodeNames) {
                if (GetNodeOrNull<PathMovingPlatform>(name) is PathMovingPlatform skiff) _skiffs.Add(skiff);
            }
        }

        // === Flow ===

        private void EnterBoardingChannel() {
            if (!_flagshipSighted) {
                _flagshipSighted = true;
                SetObjective("nassau_objective_board_flagship");
            }
        }

        /// <summary>
        /// GU-L4: Sarah's bark on the first salted Eraser fires when the boarding
        /// lock spawns it (the ramp crew's wave). Checked only when the lock
        /// registers new members.
        /// </summary>
        public override void _PhysicsProcess(double delta) {
            if (FirstEraserBarkShown || _boardingLock == null || !IsInstanceValid(_boardingLock)) return;
            int registered = _boardingLock.RegisteredCount;
            if (registered == _boardingRegisteredSeen) return;
            _boardingRegisteredSeen = registered;
            foreach (EnemyController member in _boardingLock.LivingMembers) {
                if (member.Data?.EnemyID != EraserEnemyID) continue;
                PostFirstEraserBark();
                return;
            }
        }

        /// <summary>GU-L4: a cleared or reverted lock hands back the level's own objective.</summary>
        protected override void RestoreLevelObjective() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (_flagshipSighted) SetObjective("nassau_objective_board_flagship");
            else SetObjective(InitialObjectiveKey);
        }

        protected override void OnLevelReady() {
            if (IsBossDefeated) SetObjective(CompletionObjectiveKey);
            else if (_flagshipSighted) SetObjective("nassau_objective_board_flagship");
        }

        // === Checkpoint resume ===

        /// <summary>
        /// Every checkpoint stands on a span in <see cref="SolidDeckSpans"/>, so a
        /// resume can never strand the player mid-swing or on a skiff that has since
        /// sailed. All this has to do is keep cleared waves cleared.
        /// </summary>
        /// <summary>The anchor's non-encounter world state; the waves come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint2) _flagshipSighted = true;
        }

        // Package 12 W2 (GAP-13): explicit encounter baseline, authored rather than
        // derived. GU-L4: the encounters are the two locks; every anchor lists
        // every lock before it, and a resume builds those Cleared.
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = System.Array.Empty<string>(),
                [Checkpoint1] = new[] { LockQuay },
                [Checkpoint2] = new[] { LockQuay, LockBoarding }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        // === Encounters ===
        //
        // GU-L4 (2026-10-12): every encounter is a lock wave (BuildEncounterLocks).
        // Authored counts are the Normal-difficulty truth; the lock takes the same
        // per-row ScaleEncounterCount prefix the reward manifest compiles.

        // === Package 13 W4 (S35): the first salted Eraser ===

        /// <summary>Sarah's non-blocking bark on Level 7's first Eraser encounter.</summary>
        public const string FirstEraserBarkKey = "dlg_l07_eraser_bark";

        /// <summary>Seconds the bark stays up.</summary>
        public const float EraserBarkSeconds = 4.5f;

        /// <summary>True once the bark has been raised this scene. Test seam.</summary>
        public bool FirstEraserBarkShown { get; private set; }

        /// <summary>
        /// Non-blocking, like the Level 5 debut bark: a world-space notice over the
        /// hero, never a pausing dialogue sequence, so the ambush plays out live.
        /// </summary>
        public bool PostFirstEraserBark() {
            if (FirstEraserBarkShown) return false;
            FirstEraserBarkShown = true;
            if (Player != null && IsInstanceValid(Player)) {
                EnvironmentNotice.Post(FirstEraserBarkKey, Player, EraserBarkSeconds);
            }
            return true;
        }
    }
}
