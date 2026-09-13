using Godot;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 7 - Nassau, 1715. The Golden Age of Piracy.
    ///
    /// The pirate republic has just declared independence, and the Unbound is
    /// steering the Royal Navy onto it with sub-aquatic torpedoes and a chronal
    /// targeting grid. The level is therefore a ship-to-ship crossing, not a walk:
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
    /// (rapid low-damage ranged) - mixed with `chrono_slasher` Unbound troopers, and the
    /// single elite is the `overcharged_cannon_master`.
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
        /// </summary>
        public static readonly (Vector2 Position, float WestOffsetX, float EastOffsetX, float HalfWidth)[] Skiffs = {
            (new Vector2(6700f, 916f), -140f, 200f, 120f),
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
        private const float ListRampDegrees = 10.5f;

        public static float ArenaWidth => LevelWidth - Room4StartX;

        // === Checkpoints ===

        public const string Checkpoint0 = NassauLevelID + "_checkpoint_0";
        public const string Checkpoint1 = NassauLevelID + "_checkpoint_1";
        public const string Checkpoint2 = NassauLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        /// <summary>Authored checkpoint anchors. Every one stands on a span in <see cref="SolidDeckSpans"/>.</summary>
        public static readonly (string ID, Vector2 Position)[] CheckpointPlacements = {
            (Checkpoint0, new Vector2(240f, DeckY - 20f)),
            (Checkpoint1, new Vector2(5800f, DeckY - 20f)),
            (Checkpoint2, new Vector2(8420f, DeckY - 20f))
        };

        // === Structural spans (single source for geometry and for the content tests) ===

        /// <summary>Continuous solid deck runs: (start X, end X, top Y).</summary>
        public static readonly (float StartX, float EndX, float TopY)[] SolidDeckSpans = {
            (Room1StartX, Room2StartX, DeckY),          // Nassau quay
            (Room2StartX, SwingGapStartX, DeckY),       // the Ranger's waist
            (SwingGapEndX, Room3StartX, DeckY),         // the Bonnet's waist
            (Room3StartX, ChannelStartX, DeckY),        // the gun pier
            (ChannelEndX, Room4StartX, DeckY)           // the flagship boarding ramp
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
        public const string BossResourcePath = "res://resources/Bosses/dread_admiral.tres";

        /// <summary>
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves 2
        /// and 3 arm on their room triggers, so at most five combatants are alive at
        /// once - inside the level_07_nassau_pools warm counts (standard 12, elite 4)
        /// with plenty of `enemy_projectile` headroom under its warm 26.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - the quay, under the first shells.
            (StandardEnemyID, 1, new Vector2(1200f, EnemyDeckY)),
            (CultistEnemyID, 1, new Vector2(1750f, EnemyDeckY)),
            (StandardEnemyID, 1, new Vector2(2500f, EnemyDeckY)),
            // Wave 2 - the Ranger's crew, including a marksman on the launch yard.
            (CultistEnemyID, 2, new Vector2(3000f, EnemyDeckY)),
            (StandardEnemyID, 2, new Vector2(3450f, LaunchYardY - 30f)),
            (StandardEnemyID, 2, new Vector2(5780f, EnemyDeckY)),
            // Wave 3 - the gun pier and the flagship's boarding ramp.
            (StandardEnemyID, 3, new Vector2(6180f, EnemyDeckY)),
            (CultistEnemyID, 3, new Vector2(6420f, EnemyDeckY)),
            (StandardEnemyID, 3, new Vector2(8080f, EnemyDeckY)),
            (CultistEnemyID, 3, new Vector2(8380f, EnemyDeckY)),
            (EliteEnemyID, 3, new Vector2(8230f, EnemyDeckY))
        };

        /// <summary>Three extractors, each up an off-route climb. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_07_extractor_0", new Vector2(1900f, 560f)),
            ("level_07_extractor_1", new Vector2(3150f, 420f)),
            ("level_07_extractor_2", new Vector2(7250f, 700f))
        };

        public static int StandardEnemyCount => CountOfTier(EnemyTier.Standard);
        public static int EliteEnemyCount => CountOfTier(EnemyTier.Elite);
        public static int ExtractorCount => ExtractorPlacements.Length;

        private static int CountOfTier(EnemyTier tier) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) {
                bool isElite = id == EliteEnemyID;
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

        // === Runtime state ===

        private readonly HashSet<int> _wavesSpawned = new();
        private readonly List<PendulumAnchor> _anchors = new();
        private readonly List<PathMovingPlatform> _skiffs = new();
        private bool _flagshipSighted;

        public IReadOnlyList<PendulumAnchor> RopeAnchors => _anchors;
        public IReadOnlyList<PathMovingPlatform> BoardingSkiffs => _skiffs;
        public bool HasWaveSpawned(int wave) => _wavesSpawned.Contains(wave);

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
            BuildPlatform(5750f, ReceivingYardY + DefaultPlatformThickness / 2f, 400f, YardColor);

            BuildCheckpoint(5800f, DeckY - 20f, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "nassau_room_rigging", new Color(0.55f, 0.82f, 0.9f));
            BuildRoomTransitionPair("nassau_room_harbour_return", Room1CameraBounds,
                "nassau_room_rigging", Room2CameraBounds, Room2StartX, _ => SpawnWave(2));
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

            BuildCheckpoint(8420f, DeckY - 20f, Checkpoint2, CheckpointRole.PreBoss);
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

            BuildFloor(Room4StartX, ListDeckWestY, 720f);
            BuildListingRamp(9300f, (ListDeckWestY + ListDeckMidY) / 2f, 220f);
            BuildFloor(9380f, ListDeckMidY, 640f);
            BuildListingRamp(10100f, (ListDeckMidY + ListDeckEastY) / 2f, 220f);
            BuildFloor(10180f, ListDeckEastY, LevelWidth - 10180f);

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
            BuildBossEncounter(BossResourcePath, new Vector2(10250f, ListDeckEastY - 40f),
                "DreadAdmiralEncounter", revealDistance: 900f);
        }

        /// <summary>Deck slab tilted about its own top centre; positive degrees list to starboard (east).</summary>
        private StaticBody2D BuildListingRamp(float centerX, float centerY, float width) {
            StaticBody2D ramp = BuildFloor(centerX - width / 2f, centerY, width, ListRampColor);
            ramp.RotationDegrees = ListRampDegrees;
            return ramp;
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

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void EnterBoardingChannel() {
            if (!_flagshipSighted) {
                _flagshipSighted = true;
                SetObjective("nassau_objective_board_flagship");
            }
            SpawnWave(3);
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
                    _flagshipSighted = true;
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
