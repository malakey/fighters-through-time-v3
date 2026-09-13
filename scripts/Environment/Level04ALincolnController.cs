using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Lincoln's Legacy Level</b>: Gettysburg, 1863, the dedication he
    /// never reached. His nexus moment, revisited from the other side with the whole
    /// kit for the first time (V7.6; Package 11 B1).
    ///
    /// <para>Structurally a copy of the A12 Einstein exemplar: everything shared with
    /// the other eight variants lives in <see cref="LegacyLevelControllerBase"/>, and
    /// what is below is exactly the per-hero surface.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) on the
    /// Emmitsburg road → the railway cut, where the trestle is down and the only way
    /// across is the armored <i>Rail Charge</i> → <i>The Emancipator</i>'s ground wave
    /// has to roll through the Archive's buried conduit → <i>Splitting Strike</i>
    /// shears the anchor stake off Cemetery Ridge → the Eraser debut among the
    /// gun carriages → the <i>Union Indestructible</i> set-piece at the Nexus
    /// Resonance Source, on the dedication cornerstone → the late Font approach →
    /// PreBoss (strike) → the empty platform.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each — the V01c
    /// exception granted to Level 4A alone. Every gate works on Lincoln's
    /// <b>baseline</b> kit: nothing here needs the purchased Rail Breaker node
    /// (design §5, F06 Lincoln).</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant.</para>
    /// </summary>
    public partial class Level04ALincolnController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "lincoln";
        public const string ID = "level_04a_lincoln";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/lincoln_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_lincoln_dialogue.tres";

        /// <summary>The era boss this variant duplicates (never edited in place).</summary>
        public const string EraBossResource = "res://resources/Bosses/siege_cannon.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_lincoln_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_lincoln_objective_gates";
        protected override string BossObjectiveKey => "legacy_lincoln_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_lincoln_objective_complete";

        // === The four kit gates (Lincoln's canonical ability IDs) ===

        public override string MovementGateAbilityID => "lincoln_rail_charge";
        public override string SpecialOneGateAbilityID => "lincoln_emancipator";
        public override string SpecialTwoGateAbilityID => "lincoln_splitting_strike";
        public override string UltimateAbilityID => "lincoln_union_indestructible";

        /// <summary>
        /// The Emancipator's travelling ground wave lays a live <c>story_zone</c>
        /// along its path carrying the ability ID, so the buried conduit reads the
        /// wave passing over it rather than a direct contact.
        /// </summary>
        protected override LegacyGateMode SpecialOneGateMode => LegacyGateMode.Zone;

        /// <summary>
        /// Splitting Strike swings a real overhead <c>Hitbox</c>, whose mask already
        /// includes <c>PersistentObject</c> — an ordinary struck mechanism, no effigy
        /// needed.
        /// </summary>
        protected override LegacyGateMode SpecialTwoGateMode => LegacyGateMode.Strike;

        // === Layout ===

        public const float LevelWidth = 6720f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float ActorGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 2560f;
        private const float Room3StartX = 5120f;

        /// <summary>The railway cut: the Rail Charge gap. Wider than a double jump on purpose.</summary>
        public const float CutGapStartX = 1420f;
        public const float CutGapEndX = 1980f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        /// <summary>V01a: this route's own provisional Normal median, never pooled with another variant's.</summary>
        public override int NexusParSeconds => 300;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2080f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(3060f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3800f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4280f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4820f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4980f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5080f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5280f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6400f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Archive's detail holding the ridge.
        /// No authored elites — 4A's only elite is the single scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(920f, ActorGroundY)),
            ("hologram_drone", new Vector2(1180f, ActorGroundY - 190f)),
            ("chrono_slasher", new Vector2(2760f, ActorGroundY)),
            ("chrono_slasher", new Vector2(3340f, ActorGroundY)),
            ("hologram_drone", new Vector2(4040f, ActorGroundY - 200f)),
            ("chrono_slasher", new Vector2(4600f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Gettysburg — trampled wheat, powder smoke, cannon iron ===

        protected override Color FloorColor => new(0.20f, 0.19f, 0.14f);
        protected override Color FloorEdgeColor => new(0.44f, 0.42f, 0.26f);
        protected override Color PlatformColor => new(0.31f, 0.29f, 0.21f);
        protected override Color WallColor => new(0.14f, 0.13f, 0.10f);
        protected override Color HazardColor => new(0.66f, 0.26f, 0.09f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1RailwayCut();
            BuildRoom2CemeteryRidge();
            BuildRoom3DedicationPlatform();
        }

        /// <summary>
        /// Room 1: the Emmitsburg road down to the unfinished railway cut. The trestle
        /// is down; the only way across is the armored charge.
        /// </summary>
        private void BuildRoom1RailwayCut() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.09f, 0.09f, 0.07f, 0.4f));
            BuildFloor(Room1StartX, GroundY, CutGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(500, 740, 240);
            BuildPlatform(920, 630, 220);
            BuildOneWayPlatform(1240, 710, 180);

            // East lip of the cut: the landing the charge has to reach.
            BuildFloor(CutGapEndX, GroundY, Room2StartX - CutGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_lincoln_room_cut", new Color(0.8f, 0.78f, 0.5f));
            BuildRoomTransition("legacy_lincoln_room_cut", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: Cemetery Ridge. The buried conduit (Special 1), the anchor stake
        /// (Special 2), the gun line where the Eraser drops in, and the Nexus
        /// Resonance Source on the dedication cornerstone.
        /// </summary>
        private void BuildRoom2CemeteryRidge() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.10f, 0.10f, 0.08f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            BuildPlatform(2840, 700, 240);
            BuildOneWayPlatform(3260, 620, 220);
            BuildPlatform(3620, 720, 220);
            BuildPlatform(4080, 590, 240);
            BuildPlatform(4520, 700, 240);

            BuildRoomDecoration(Room2StartX, "legacy_lincoln_room_ridge", new Color(0.9f, 0.72f, 0.35f));
            BuildRoomTransition("legacy_lincoln_room_ridge",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the dedication platform. 1,600 px wide so the boss's 12.0 m ranged
        /// band (720 px) and its melee band both matter.
        /// </summary>
        private void BuildRoom3DedicationPlatform() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.12f, 0.08f, 0.06f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600);

            BuildPlatform(5620, 700, 240);
            BuildPlatform(6240, 700, 240);
            BuildPlatform(5940, 490, 220);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_lincoln_room_platform", new Color(0.9f, 0.85f, 0.6f));
            BuildRoomTransition("legacy_lincoln_room_platform",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;
    }
}
