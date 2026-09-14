using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    /// <summary>
    /// Level 13 - The Chronal Void. The Act III opener and the campaign's only
    /// transitional level: the rift between dimensions, entered on the coordinate
    /// trace the Gravity Overseer gave up on the Moon and exited into the assault
    /// on the Unbound Bastion.
    ///
    /// <b>Era identity - the void has no era, so it has all of them.</b> The
    /// walkable geometry above the sediment shelf is authored as fragments torn out
    /// of every level the player has already closed: an Orleans battlement stone, a
    /// Bastille ironwork span, a Pompeii basalt step, a Berlin checkpoint slab, an
    /// Alexandria cornice, Nassau planking, a Chicago girder, a Gettysburg rail
    /// fence, a Globe gallery rail, a Titanic promenade board, a Florence cog
    /// housing - plus four era fragments that have not settled and still
    /// <see cref="PathMovingPlatform"/> their way through the rift (the Florence
    /// gear, the Titanic boat deck, the lunar gantry, and the Globe stage boards).
    /// This is the one level where mixing era motifs is the point.
    ///
    /// <b>The signature mechanic is gravity that changes during play.</b> Two
    /// cycling <see cref="GravityFieldZone"/>s tile rooms 1 and 2, shifting between
    /// authored scales on a telegraphed timer, so traversal has to be re-read
    /// continuously rather than solved once. Every authored climb stays makeable at
    /// <b>every</b> scale in both cycles - that is a hard contract pinned by
    /// <c>Level13ContentTests</c> against all nine characters' authored jump
    /// physics, because a retune that quietly strands the heaviest character in a
    /// level with no floor to fall back to is not a difficulty spike, it is a
    /// soft-lock. What the cycle really changes is hang time, knockback arcs, and
    /// how much of the rift you can cross in one leap.
    ///
    /// <b><see cref="ChronalRiftZone"/> pockets tax the low road.</b> The sediment
    /// shelf runs unbroken from the entrance to the arena, so the fast route always
    /// exists - but three pockets of pooled time sit on it at half speed, and more
    /// than two seconds inside one snaps the player about three seconds into their
    /// own past with damage. The shelf is quick if you keep moving and expensive if
    /// you do not.
    ///
    /// <b>The encounter is not a boss.</b> The Mirror Paradox is a clone of the
    /// player's own locked character driven by the real Hard Fighter CPU decision
    /// table, so it is wired through <see cref="MirrorParadoxEncounterController"/>
    /// and never through <see cref="BossEncounterController"/>. Its arena is
    /// authored to read as a Fighter Mode stage - a flat unbroken floor, two
    /// symmetric platforms, symmetric spawn marks, solid bounds, and a static
    /// Earth-normal gravity pocket with no cycling and no rift in it. A mirror match
    /// has to be fair, so the void stops interfering at the arena seam.
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: 8 standards, 1 elite,
    /// <b>0 bosses</b>, 2 extractors. The locked 52-full/36-expected row already
    /// accounts for the Mirror's 50 dust, which
    /// <see cref="MirrorParadoxEncounterController"/> awards - there is deliberately
    /// no boss row here and nothing compensates for one.
    /// </summary>
    public partial class Level13Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string VoidLevelID = "level_13_chronal_void";

        public override string LevelID => VoidLevelID;
        public override CampaignLevel Level => CampaignLevel.ChronalVoid;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 5 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 450f;
        public override string LevelTitleKey => "chronal_void_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_13_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(220f, ShelfY - 60f);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        protected override string InitialObjectiveKey => "void_objective_cross_rift";
        protected override string BossObjectiveKey => "void_objective_defeat_mirror";
        protected override string CompletionObjectiveKey => "void_objective_complete";

        // === Dimensions ===

        /// <summary>Short by campaign standards on purpose: this is a transition, not an era.</summary>
        public const float LevelWidth = 9600f;
        /// <summary>Tall, because the void's only real geography is up.</summary>
        public const float LevelHeight = 2000f;

        /// <summary>
        /// The sediment shelf: everything the rift has already finished digesting,
        /// settled into one continuous surface. It is the level's floor of last
        /// resort - there is no fall death here and no bottomless pit.
        /// </summary>
        public const float ShelfY = 1900f;

        public const float Room2StartX = 3200f;
        /// <summary>West edge of the Mirror arena, and the seam where the void stops shifting.</summary>
        public const float ArenaStartX = 7200f;
        public const float ArenaEndX = 9600f;

        /// <summary>Camera band for the one flat room (the L03/L09/L10 precedent).</summary>
        public const float ArenaCameraTop = LevelHeight - 1080f;

        // === The Mirror arena (authored to read as a Fighter Mode stage) ===

        public const float ArenaCenterX = (ArenaStartX + ArenaEndX) / 2f;
        /// <summary>Both fighters start this far from the centre. Symmetry is the whole point.</summary>
        public const float ArenaSpawnOffsetX = 600f;
        public static float PlayerArenaMarkX => ArenaCenterX - ArenaSpawnOffsetX;
        public static float MirrorSpawnX => ArenaCenterX + ArenaSpawnOffsetX;

        /// <summary>Both side platforms, mirrored about <see cref="ArenaCenterX"/>.</summary>
        public const float ArenaPlatformOffsetX = 620f;
        /// <summary>
        /// Low, because the arena runs at Earth-normal gravity and Story physics caps
        /// the heaviest single-jump character at about 94 px of rise. A side platform
        /// nobody can stand on is dead geometry, so this is sized against the roster
        /// rather than against how a Fighter stage looks. Lowered 1810 -> 1825 (a 90 px
        /// rise to 75 px) by the 2026-08-10 movement retune, holding the same ~80% of the
        /// heaviest character's reach it was authored at (gameplay-feel plan section 9, B2).
        /// </summary>
        public const float ArenaPlatformY = 1825f;
        public const float ArenaPlatformWidth = 340f;
        public const float MirrorRevealDistance = 700f;

        // === Checkpoints ===

        public const string Checkpoint0 = VoidLevelID + "_checkpoint_0";
        public const string Checkpoint1 = VoidLevelID + "_checkpoint_1";
        public const string Checkpoint2 = VoidLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        private static readonly Vector2 Checkpoint0Position = new(260f, ShelfY - 10f);
        private static readonly Vector2 Checkpoint1Position = new(5180f, ShelfY - 10f);
        /// <summary>Pre-arena anchor, deliberately west of the seam so a resume is never locked in.</summary>
        private static readonly Vector2 Checkpoint2Position = new(7100f, ShelfY - 10f);

        // === Shifting gravity ===

        /// <summary>
        /// Authored gravity tiling: contiguous, non-overlapping, covering the level
        /// end to end. <see cref="EnvironmentPlayerModifiers"/> publishes the PRODUCT
        /// of every live source, so two fields over one player would multiply into a
        /// scale nobody authored (Levels 5, 8 and 12 all hit that).
        ///
        /// The two rift fields cycle on distinct periods so the level never collapses
        /// into a single timing window; the arena field is deliberately static at
        /// Earth-normal, which is what makes the mirror match a fair fight.
        /// </summary>
        public static readonly (string ID, float StartX, float EndX, float StaticScale,
            float[] CycleScales, float CycleSeconds, float TelegraphSeconds)[] GravityFieldSpans = {
            ("level_13.threshold_drift", 0f, Room2StartX, 0.40f,
                new[] { 0.55f, 0.28f, 0.40f }, 7.0f, 1.6f),
            ("level_13.mashup_drift", Room2StartX, ArenaStartX, 0.32f,
                new[] { 0.32f, 0.60f, 0.22f }, 5.5f, 1.4f),
            ("level_13.arena_stabiliser", ArenaStartX, LevelWidth, 1.0f,
                Array.Empty<float>(), 6f, 1f)
        };

        /// <summary>Every gravity scale a field can publish; a single-entry field never cycles.</summary>
        public static float[] ScalesOf(
            (string ID, float StartX, float EndX, float StaticScale,
                float[] CycleScales, float CycleSeconds, float TelegraphSeconds) span) =>
            span.CycleScales is { Length: >= 2 } ? span.CycleScales : new[] { span.StaticScale };

        /// <summary>Heaviest gravity any cycling field can reach; the reachability budget is set by this.</summary>
        public static float HeaviestCyclingScale {
            get {
                float heaviest = 0f;
                foreach (var span in GravityFieldSpans) {
                    if (span.CycleScales is not { Length: >= 2 }) continue;
                    foreach (float scale in span.CycleScales) heaviest = Mathf.Max(heaviest, scale);
                }
                return heaviest;
            }
        }

        private readonly List<GravityFieldZone> _gravityFields = new();
        public IReadOnlyList<GravityFieldZone> GravityFields => _gravityFields;

        // === Era-mashup geometry ===

        /// <summary>
        /// The settled fragments. Every one of them is a motif lifted from a level
        /// the player has already closed; the ID names the era, not the shape.
        /// Y is the platform centre, matching the base builders and the L12
        /// reachability convention.
        /// </summary>
        public static readonly (string ID, float X, float Y, float Width, string LabelKey, Color Tint)[] EraMotifs = {
            // Room 1 - the Threshold stair.
            ("motif_orleans", 760f, 1760f, 240f, "void_shard_orleans", new Color(0.62f, 0.60f, 0.55f)),
            ("motif_paris", 1060f, 1620f, 220f, "void_shard_paris", new Color(0.35f, 0.33f, 0.38f)),
            ("motif_pompeii", 1360f, 1480f, 220f, "void_shard_pompeii", new Color(0.32f, 0.24f, 0.22f)),
            ("motif_berlin", 1660f, 1340f, 220f, "void_shard_berlin", new Color(0.55f, 0.56f, 0.58f)),
            ("motif_egypt", 2380f, 950f, 260f, "void_shard_egypt", new Color(0.78f, 0.68f, 0.42f)),
            // Room 2 - the Drift.
            ("motif_nassau", 3560f, 1760f, 240f, "void_shard_nassau", new Color(0.45f, 0.32f, 0.20f)),
            ("motif_chicago", 3860f, 1620f, 220f, "void_shard_chicago", new Color(0.50f, 0.52f, 0.58f)),
            ("motif_gettysburg", 4160f, 1480f, 220f, "void_shard_gettysburg", new Color(0.42f, 0.34f, 0.24f)),
            ("motif_globe_gallery", 5320f, 1430f, 220f, "void_shard_globe", new Color(0.50f, 0.36f, 0.22f)),
            ("motif_titanic_deck", 5980f, 1000f, 260f, "void_shard_titanic", new Color(0.72f, 0.70f, 0.66f)),
            ("motif_florence_cog", 7060f, 1050f, 240f, "void_shard_florence", new Color(0.66f, 0.50f, 0.24f))
        };

        /// <summary>Centre altitude of an authored motif, or NaN when the ID is unknown.</summary>
        public static float MotifY(string motifID) {
            foreach (var motif in EraMotifs) if (motif.ID == motifID) return motif.Y;
            return float.NaN;
        }

        /// <summary>Centre X of an authored motif, or NaN when the ID is unknown.</summary>
        public static float MotifX(string motifID) {
            foreach (var motif in EraMotifs) if (motif.ID == motifID) return motif.X;
            return float.NaN;
        }

        /// <summary>
        /// Static climbs, expressed as motif IDs so the reachability contract and the
        /// geometry can never drift apart. Each chain starts standing on the shelf.
        /// </summary>
        public static readonly (string ID, float StartY, string[] MotifIDs)[] Ladders = {
            ("threshold_stair", ShelfY, new[] { "motif_orleans", "motif_paris", "motif_pompeii", "motif_berlin" }),
            ("drift_stair", ShelfY, new[] { "motif_nassau", "motif_chicago", "motif_gettysburg" })
        };

        /// <summary>
        /// The four era fragments still adrift, authored as
        /// <see cref="PathMovingPlatform"/> instances in Level_13_ChronalVoid.tscn.
        /// Board/land altitudes are motif IDs; the deck altitudes come from the
        /// scene's own waypoints, so re-authoring a drift re-checks reachability.
        /// </summary>
        public static readonly (string NodeName, string ShardID, string BoardMotif, string LandMotif, string LabelKey)[] Shards = {
            ("ShardFlorenceGear", "level_13.shard_florence_gear", "motif_berlin", "motif_egypt",
                "void_shard_florence_gear"),
            ("ShardTitanicDeck", "level_13.shard_titanic_deck", "motif_gettysburg", "motif_globe_gallery",
                "void_shard_titanic_deck"),
            ("ShardLunarGantry", "level_13.shard_lunar_gantry", "motif_globe_gallery", "motif_titanic_deck",
                "void_shard_lunar_gantry"),
            ("ShardGlobeStage", "level_13.shard_globe_stage", "motif_titanic_deck", "motif_florence_cog",
                "void_shard_globe_stage")
        };

        private readonly List<PathMovingPlatform> _driftingShards = new();
        public IReadOnlyList<PathMovingPlatform> DriftingShards => _driftingShards;

        // === Rift pockets ===

        /// <summary>Authored in the scene; collected here so tests can reason about them.</summary>
        public static readonly string[] RiftPocketNodeNames = { "RiftThreshold", "RiftMare", "RiftDeep" };

        private readonly List<ChronalRiftZone> _riftPockets = new();
        public IReadOnlyList<ChronalRiftZone> RiftPockets => _riftPockets;

        // === Encounters (locked: 8 standards / 1 elite / 0 bosses / 2 extractors) ===

        /// <summary>Alexandria's phasing standard - flying, wall-phasing, and native to a broken rift.</summary>
        public const string PhantomEnemyID = "rift_phantom";
        /// <summary>Unbound shock-trooper standard: the Unbound's own people, dug in on the approach.</summary>
        public const string CultistEnemyID = "chrono_slasher";
        public const string EliteEnemyID = "chrono_guard_elite";

        /// <summary>
        /// Deliberately <b>not</b> a BossData path. Level 13 has no boss row: the
        /// Mirror Paradox resource is owned by
        /// <see cref="MirrorParadoxEncounterController"/> and its 50 dust is already
        /// inside the locked economy row.
        /// </summary>
        public const string MirrorResourcePath = MirrorParadoxEncounterController.DefaultBossDataPath;

        private const float StandY = ShelfY - 50f;

        /// <summary>
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves 2
        /// and 3 arm on the room seam and the pre-arena approach. Difficulty scaling
        /// takes a prefix of each wave through
        /// <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - the Threshold. A phantom on the shelf, a phantom guarding the
            // first extractor's cornice, and a Unbound trooper picket in between.
            (PhantomEnemyID, 1, new Vector2(1180f, StandY)),
            (CultistEnemyID, 1, new Vector2(2620f, StandY)),
            (PhantomEnemyID, 1, new Vector2(2380f, 890f)),
            // Wave 2 - the Drift. Unbound troopers hold the shelf, phantoms work the shards.
            (CultistEnemyID, 2, new Vector2(3700f, StandY)),
            (PhantomEnemyID, 2, new Vector2(4820f, StandY)),
            (CultistEnemyID, 2, new Vector2(5560f, StandY)),
            // Wave 3 - the approach to the arena, with the level's single elite.
            (PhantomEnemyID, 3, new Vector2(5980f, 940f)),
            (CultistEnemyID, 3, new Vector2(6740f, StandY)),
            (EliteEnemyID, 3, new Vector2(7020f, StandY))
        };

        /// <summary>Two extractors, both up the drifting high road. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_13_extractor_0", new Vector2(2380f, 890f)),
            ("level_13_extractor_1", new Vector2(5980f, 940f))
        };

        public static int StandardEnemyCount => CountOf(PhantomEnemyID) + CountOf(CultistEnemyID);
        public static int EliteEnemyCount => CountOf(EliteEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) if (id == enemyID) total++;
            return total;
        }

        /// <summary>Wave 3 arms here, west of the arena seam, so the elite is fought before the lock.</summary>
        public const float PreArenaTriggerX = 6500f;

        // === Runtime state ===

        private readonly HashSet<int> _wavesSpawned = new();
        private MirrorParadoxEncounterController _mirrorEncounter;
        private bool _mirrorIntroShown;
        private bool _mirrorDefeated;
        private bool _rewindBound;

        public MirrorParadoxEncounterController MirrorEncounter => _mirrorEncounter;
        public bool MirrorIntroShown => _mirrorIntroShown;
        public bool IsMirrorDefeated => _mirrorDefeated;

        // === Void palette ===

        protected override Color FloorColor => new(0.13f, 0.11f, 0.19f);
        protected override Color FloorEdgeColor => new(0.55f, 0.45f, 0.9f);
        protected override Color PlatformColor => new(0.28f, 0.26f, 0.4f);
        protected override Color WallColor => new(0.09f, 0.08f, 0.14f);

        private static readonly Color ArenaFloorColor = new(0.2f, 0.2f, 0.28f);
        private static readonly Color ArenaPlatformColor = new(0.34f, 0.34f, 0.46f);
        private static readonly Color ThresholdFieldColor = new(0.55f, 0.45f, 1f);
        private static readonly Color DriftFieldColor = new(0.4f, 0.85f, 1f);
        private static readonly Color ArenaFieldColor = new(0.85f, 0.85f, 0.92f);

        // === Construction ===

        protected override void BuildLevel() {
            CollectAuthoredNodes();
            BuildRoom1Threshold();
            BuildRoom2Drift();
            BuildRoom3MirrorArena();
            BuildGravityFields();
            BuildExtractors(ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(ArenaEndX - DefaultWallThickness, 0f, LevelHeight);
        }

        /// <summary>
        /// Picks up the scene-authored drifting shards and rift pockets. Both
        /// template families ship hardcoded English labels and overriding a child of
        /// an instanced scene from a .tscn needs the fragile <c>index=</c> block
        /// (the L02/L08 finding), so the era captions are localized here instead.
        /// </summary>
        private void CollectAuthoredNodes() {
            foreach ((string nodeName, string shardID, string _, string _, string labelKey) in Shards) {
                if (GetNodeOrNull<PathMovingPlatform>(nodeName) is not PathMovingPlatform shard) continue;
                shard.PlatformID = shardID;
                if (shard.GetNodeOrNull<Label>("Label") is Label label) label.Text = Tr(labelKey);
                _driftingShards.Add(shard);
            }
            foreach (string nodeName in RiftPocketNodeNames) {
                if (GetNodeOrNull<ChronalRiftZone>(nodeName) is ChronalRiftZone rift) _riftPockets.Add(rift);
            }
        }

        /// <summary>One settled era fragment: a drop-through platform with its era caption.</summary>
        private void BuildEraMotif(string motifID) {
            foreach ((string id, float x, float y, float width, string labelKey, Color tint) in EraMotifs) {
                if (id != motifID) continue;
                BuildOneWayPlatform(x, y, width, tint);
                var label = new Label {
                    Name = $"MotifLabel_{id}",
                    Text = Tr(labelKey),
                    Position = new Vector2(x - width / 2f, y - 44f),
                    CustomMinimumSize = new Vector2(width, 18),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                label.AddThemeFontSizeOverride("font_size", 10);
                label.AddThemeColorOverride("font_color", new Color(tint.R, tint.G, tint.B, 0.85f));
                AddChild(label);
                return;
            }
            GD.PushWarning($"Level 13 asked for an unauthored era motif '{motifID}'.");
        }

        private void BuildRoom1Threshold() {
            BuildRoomBackground("BG_Threshold", 0f, Room2StartX, LevelHeight,
                new Color(0.05f, 0.03f, 0.10f, 0.6f));
            BuildFloor(0f, ShelfY, Room2StartX);

            foreach (string motifID in new[] { "motif_orleans", "motif_paris", "motif_pompeii", "motif_berlin", "motif_egypt" }) {
                BuildEraMotif(motifID);
            }

            BuildCheckpoint(Checkpoint0Position.X, Checkpoint0Position.Y, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(0f, "void_room_threshold", new Color(0.72f, 0.62f, 0.98f));
        }

        private void BuildRoom2Drift() {
            float width = ArenaStartX - Room2StartX;
            BuildRoomBackground("BG_Drift", Room2StartX, width, LevelHeight,
                new Color(0.04f, 0.06f, 0.11f, 0.6f));
            BuildFloor(Room2StartX, ShelfY, width);

            foreach (string motifID in new[] {
                "motif_nassau", "motif_chicago", "motif_gettysburg",
                "motif_globe_gallery", "motif_titanic_deck", "motif_florence_cog"
            }) {
                BuildEraMotif(motifID);
            }

            BuildCheckpoint(Checkpoint1Position.X, Checkpoint1Position.Y, Checkpoint1, CheckpointRole.Middle);
            BuildCheckpoint(Checkpoint2Position.X, Checkpoint2Position.Y, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room2StartX, "void_room_drift", new Color(0.55f, 0.9f, 1f));

            BuildRoomTransitionPair(
                Room2StartX, "void_drift", "void_threshold",
                new Rect2(Room2StartX - 100f, 0f, width + 200f, LevelHeight),
                new Rect2(0f, 0f, Room2StartX + 100f, LevelHeight),
                _ => EnterDrift());

            BuildWaveTrigger("PreArenaTrigger", new Vector2(PreArenaTriggerX, ShelfY - 300f),
                () => SpawnWave(3), new Vector2(70f, 900f));
        }

        /// <summary>
        /// The Mirror arena. Everything in here is authored for a fair duel rather
        /// than for the level's own mechanics: an unbroken floor, two platforms
        /// mirrored about the centre line, symmetric spawn marks, solid bounds, and
        /// (through <see cref="BuildGravityFields"/>) a static Earth-normal pocket.
        /// No cycling field and no rift pocket reaches past <see cref="ArenaStartX"/>.
        /// </summary>
        private void BuildRoom3MirrorArena() {
            float width = ArenaEndX - ArenaStartX;
            BuildRoomBackground("BG_MirrorArena", ArenaStartX, width, LevelHeight,
                new Color(0.08f, 0.07f, 0.13f, 0.72f));
            BuildFloor(ArenaStartX, ShelfY, width, ArenaFloorColor);

            BuildOneWayPlatform(ArenaCenterX - ArenaPlatformOffsetX, ArenaPlatformY,
                ArenaPlatformWidth, ArenaPlatformColor);
            BuildOneWayPlatform(ArenaCenterX + ArenaPlatformOffsetX, ArenaPlatformY,
                ArenaPlatformWidth, ArenaPlatformColor);

            BuildRoomDecoration(ArenaStartX, "void_room_mirror_arena", new Color(0.95f, 0.9f, 1f));

            // One-shot: this clamp is the arena lock, and the tight bottom-anchored
            // band is what makes a flat duelling floor read as a Fighter stage.
            BuildRoomTransition("void_mirror_arena", new Vector2(ArenaStartX + 60f, LevelHeight / 2f),
                new Rect2(ArenaStartX - 40f, ArenaCameraTop, width + 40f, 1080f),
                new Vector2(80f, LevelHeight),
                _ => EnterMirrorArena());
        }

        /// <summary>
        /// Stands the Mirror up in the arena. Deliberately NOT
        /// <see cref="StoryLevelControllerBase.BuildBossEncounter"/>: the Mirror
        /// Paradox is a CPU-driven clone of the player's own character rather than a
        /// <see cref="BossData"/> attack-pattern boss, and its 50 dust is awarded by
        /// the encounter controller itself.
        ///
        /// Built from <see cref="OnLevelReady"/> rather than from
        /// <see cref="BuildLevel"/> so the campaign avatar already exists: the clone
        /// then mirrors whoever the base class actually spawned, including the
        /// base's own fallback when a scene is opened outside a campaign session (a
        /// direct load or a headless smoke run), instead of resolving the session a
        /// second time and finding nothing to mirror.
        /// </summary>
        private void BuildMirrorEncounter() {
            if (_mirrorEncounter != null && IsInstanceValid(_mirrorEncounter)) return;
            _mirrorEncounter = new MirrorParadoxEncounterController {
                Name = "MirrorParadoxEncounter",
                Position = new Vector2(MirrorSpawnX, ShelfY - 50f),
                CharacterIDOverride = Player?.Data?.CharacterID ?? "",
                RevealDistance = MirrorRevealDistance,
                AwardDustOnDefeat = true,
                SpawnOnReady = true
            };
            _mirrorEncounter.MirrorRevealed += OnMirrorRevealed;
            _mirrorEncounter.MirrorDefeated += OnMirrorDefeated;
            // Package 8 B5 climax wiring, mirroring StoryLevelControllerBase's
            // BuildBossEncounter: the music layer binds to the encounter directly
            // rather than to the flow callbacks above, so the Act III opener's
            // boss fight escalates over the ambient stem like every other boss and
            // no override of the flow handlers can silence it (audit M-20).
            _mirrorEncounter.MirrorRevealed += () => Audio?.SetBossEngaged(true);
            _mirrorEncounter.MirrorDefeated += _ => Audio?.SetBossEngaged(false);
            AddChild(_mirrorEncounter);
        }

        /// <summary>
        /// Bidirectional confinement (the L03/L12 pattern):
        /// <see cref="RoomTransitionTrigger.ActivateOnce"/> defaults true, so a single
        /// forward trigger would clamp the camera to the last room forever the first
        /// time the player walked back for the Threshold extractor.
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
        /// template ships a fixed 960x720 shape and these fields are room-sized, and
        /// overriding a SubResource shape on an instanced scene risks mutating a
        /// shape shared between instantiations (the L05 flood-zone precedent).
        /// Graybox-in-code is the sanctioned Florence convention.
        /// </summary>
        private void BuildGravityFields() {
            foreach (var span in GravityFieldSpans) {
                Color tint = span.StartX >= ArenaStartX
                    ? ArenaFieldColor
                    : span.StartX >= Room2StartX ? DriftFieldColor : ThresholdFieldColor;
                _gravityFields.Add(BuildGravityField(span, tint));
            }
        }

        private GravityFieldZone BuildGravityField(
            (string ID, float StartX, float EndX, float StaticScale,
                float[] CycleScales, float CycleSeconds, float TelegraphSeconds) span,
            Color tint) {
            float width = span.EndX - span.StartX;
            var field = new GravityFieldZone {
                Name = $"GravityField_{span.ID.Replace('.', '_')}",
                FieldID = span.ID,
                GravityScale = span.StaticScale,
                CycleScales = span.CycleScales,
                CycleSeconds = span.CycleSeconds,
                TelegraphSeconds = span.TelegraphSeconds,
                IdleTint = new Color(tint.R, tint.G, tint.B, 0.14f),
                TelegraphTint = new Color(1f, 0.82f, 0.32f, 0.5f),
                Position = new Vector2(span.StartX + width / 2f, LevelHeight / 2f),
                // The void resets to its opening phase on a rewind: a player who just
                // died to a shift should get the same read back, not a random one.
                RewindPolicy = StoryRewindPolicy.ResetToInitialState
            };

            // Children first: GravityFieldZone resolves FieldVisualPath in _Ready,
            // which fires the moment the zone enters the tree.
            field.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                // Taller than the level, so a knocked-back or floating player never
                // leaves the field through the top or the bottom.
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
            // runs - so a resumed player would stand in a shifting-gravity level at
            // Earth-normal, or carry the arena's stabilised scale back out into the
            // rift. Registering explicitly makes gravity a function of where the
            // player actually is, on entry and on every rewind (the L12 pattern).
            SyncGravityFieldToPlayer();
            BuildMirrorEncounter();
            if (_mirrorEncounter != null && IsInstanceValid(_mirrorEncounter)) _mirrorEncounter.HUD = HUD;
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

        /// <summary>Authored field whose span contains <paramref name="x"/>; the spans tile the level.</summary>
        public GravityFieldZone GravityFieldFor(float x) {
            for (int index = 0; index < GravityFieldSpans.Length && index < _gravityFields.Count; index++) {
                var span = GravityFieldSpans[index];
                bool isLast = index == GravityFieldSpans.Length - 1;
                bool inside = x >= span.StartX && (isLast ? x <= span.EndX : x < span.EndX);
                if (inside && IsInstanceValid(_gravityFields[index])) return _gravityFields[index];
            }
            return null;
        }

        /// <summary>
        /// Puts the player in exactly the field they stand in and out of every other,
        /// through the shared <see cref="GravityFieldZone.SyncPlayerToField"/> helper.
        /// The target comes from this level's authored span table rather than the
        /// helper's geometric lookup, because the void's fields tile exactly and
        /// <see cref="GravityFieldFor"/> owns the seam rule — the same split Level 12
        /// uses. Idempotent and consistent with the physics callbacks: a redundant Add
        /// or Remove is a no-op inside <see cref="GravityFieldZone"/>.
        /// </summary>
        public void SyncGravityFieldToPlayer() {
            if (Player == null || !IsInstanceValid(Player)) return;
            GravityFieldZone.SyncPlayerToField(_gravityFields, Player, GravityFieldFor(Player.Position.X));
        }

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void EnterDrift() {
            SetObjective("void_objective_drift");
            SpawnWave(2);
        }

        private void EnterMirrorArena() => SetObjective("void_objective_face_mirror");

        // === The Mirror encounter ===

        private void OnMirrorRevealed() {
            if (_mirrorIntroShown) return;
            _mirrorIntroShown = true;
            SetObjective(BossObjectiveKey);
            StartDialogue(BossIntroDialogueID);
        }

        private void OnMirrorDefeated(BossDefeatedPayload payload) {
            if (_mirrorDefeated) return;
            _mirrorDefeated = true;
            SetObjective(CompletionObjectiveKey);
            // Package 11 A10 (F05): the Mirror now pays through the shared
            // physical-pickup path like every other boss, so the wallet AND the
            // boss results line are both raised at COLLECTION. Attributing here
            // as well would label dust the wallet has not been paid — the same
            // rule StoryLevelControllerBase already applies to every other boss.
            StartExitSequence();
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
