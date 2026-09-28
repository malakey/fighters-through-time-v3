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
        public override float ParSeconds => 540f;
        public override string LevelTitleKey => "lunar_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_12_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(220f, SurfaceY - 60f);
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
        /// <summary>Bottom edge of the upper curtain segment - the slot's ceiling.</summary>
        public const float CurtainSlotTopY = 250f;
        /// <summary>Top edge of the lower curtain segment - the slot's floor.</summary>
        public const float CurtainSlotBottomY = 550f;

        /// <summary>Rise from the regolith a player would need to enter the cargo slot.</summary>
        public static float CurtainSlotRise => SurfaceY - CurtainSlotBottomY;

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
        /// The two vertical <see cref="PathMovingPlatform"/> lifts as (bottom, top)
        /// altitudes, matching the authored Waypoints in Level_12_Lunar.tscn.
        /// </summary>
        public static readonly (string PlatformID, float BottomY, float TopY)[] Lifts = {
            (PocketLiftID, LedgeB, HighDeckY),
            (SpireLiftID, SurfaceY, HighDeckY)
        };

        // === Encounters (locked: 14 standards / 2 elites / 1 boss / 4 extractors) ===

        /// <summary>Lunar era standard: grav-beams that PULL rather than knock back.</summary>
        public const string DiggerEnemyID = "vacuum_digger";
        /// <summary>Unbound shock-trooper standard, mixed through the outpost garrison.</summary>
        public const string CultistEnemyID = "chrono_slasher";
        /// <summary>Lunar era elite.</summary>
        public const string EliteEnemyID = "void_enforcer";
        /// <summary>
        /// V7.6 (Package 11 A7a): the Eraser, salted through Levels 7-15. Not an
        /// era enemy - it is an Unbound hunter that followed the hero here, which
        /// is exactly why it reads wrong against the 1969 landing pad. Never authored into a room
        /// with a Chrono-Warden before Act III (EncounterCompositionTests).
        /// </summary>
        public const string EraserEnemyID = "unbound_eraser";
        public const string BossResourcePath = "res://resources/Bosses/gravity_overseer.tres";

        private const float StandY = SurfaceY - 50f;

        /// <summary>
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves
        /// 2-4 arm on their room seams. Difficulty scaling takes a prefix of each
        /// wave through <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - Tranquility Base. Diggers only: the low-gravity teaching fight.
            (DiggerEnemyID, 1, new Vector2(1100f, StandY)),
            (DiggerEnemyID, 1, new Vector2(1900f, StandY)),
            (DiggerEnemyID, 1, new Vector2(2600f, StandY)),
            // Wave 2 - the vent field. Unbound troopers join the local excavation crews.
            (DiggerEnemyID, 2, new Vector2(3400f, StandY)),
            (CultistEnemyID, 2, new Vector2(3900f, StandY)),
            (DiggerEnemyID, 2, new Vector2(4600f, StandY)),
            (CultistEnemyID, 2, new Vector2(5300f, StandY)),
            // Wave 3 - the spire base, with the first Void Enforcer holding the mast.
            (DiggerEnemyID, 3, new Vector2(6600f, StandY)),
            (CultistEnemyID, 3, new Vector2(6950f, StandY)),
            (DiggerEnemyID, 3, new Vector2(7600f, StandY)),
            (CultistEnemyID, 3, new Vector2(8220f, StandY)),
            (EliteEnemyID, 3, new Vector2(8400f, StandY)),
            // Wave 4 - the landing pad garrison, fought before the Overseer reveals.
            (DiggerEnemyID, 4, new Vector2(9200f, StandY)),
            (CultistEnemyID, 4, new Vector2(9450f, StandY)),
            (CultistEnemyID, 4, new Vector2(9700f, StandY)),
            (EliteEnemyID, 4, new Vector2(9350f, StandY)),
            // The Act II finale's hunter, waiting at the pad. The low-gravity
            // room is the cruellest place to be tethered: the counterplay is to
            // leave the circle, and here leaving anywhere takes a long arc.
            (EraserEnemyID, 4, new Vector2(9900f, StandY))
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

        // === Runtime state ===

        private readonly HashSet<int> _wavesSpawned = new();
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
            BuildRoom2VentField();
            BuildRoom3RelaySpire();
            BuildRoom4LandingPad();
            BuildGravityFields();
            BuildExtractors(ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(ArenaEndX, 0f, LevelHeight);
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
            BuildPlatform(2050f, SurfaceY - 80f, 180f, new Color(0.55f, 0.5f, 0.35f));

            BuildCheckpoint(Checkpoint0Position.X, Checkpoint0Position.Y, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(0f, "lunar_room_tranquility_base", new Color(0.78f, 0.84f, 0.95f));
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
            // them at the highline altitude. There is no top to jump over.
            BuildWall(CurtainWallX, 0f, CurtainSlotTopY, CurtainColor, 24f);
            BuildWall(CurtainWallX, CurtainSlotBottomY, SurfaceY - CurtainSlotBottomY, CurtainColor, 24f);

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
            BuildBossEncounter(BossResourcePath, new Vector2(10500f, SurfaceY - 50f),
                "GravityOverseerEncounter", revealDistance: 700f);
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

        private GravityFieldZone BuildGravityField(string fieldID, float startX, float endX, float scale, Color tint) {
            float width = endX - startX;
            var field = new GravityFieldZone {
                Name = $"GravityField_{fieldID.Replace('.', '_')}",
                FieldID = fieldID,
                // Static field: CycleScales stays empty, so GravityScale applies.
                GravityScale = scale,
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

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void EnterVentField() {
            SetObjective("lunar_objective_vent_field");
            SpawnWave(2);
        }

        private void EnterRelaySpire() {
            SetObjective("lunar_objective_climb_spire");
            SpawnWave(3);
        }

        private void EnterLandingPad() {
            SetObjective("lunar_objective_landing_pad");
            SpawnWave(4);
        }

        private void StartPreBossBeat() {
            if (_preBossShown) return;
            _preBossShown = true;
            StartDialogue(PreBossDialogueID);
        }

        // === Checkpoint resume ===

        // Package 12 W2 (GAP-13): explicit encounter baseline — waves 1+2 at the
        // middle anchor, 1+2+3 at the PreBoss anchor, authored rather than
        // derived. The pre-boss trigger stands between checkpoint 2 and the pad,
        // so a resume there deliberately leaves it armed: the thesis scene must
        // still land before the Overseer. Lunar has no other anchor state, so
        // MarkWavesClearedThrough is no longer overridden.
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            NumberedWaveBaselines(LunarLevelID, new[] { 1, 2 }, new[] { 1, 2, 3 });

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            if (TryParseWaveEncounter(LunarLevelID, encounterID, out int wave)) _wavesSpawned.Add(wave);
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
