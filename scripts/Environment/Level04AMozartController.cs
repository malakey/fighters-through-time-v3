using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Mozart's Legacy Level</b>: Vienna, 1782, the night of the
    /// Burgtheater premiere. The hero's own nexus moment, revisited from the other
    /// side with the whole kit for the first time (V7.6; Package 11 B3).
    ///
    /// <para>Structurally identical to <see cref="Level04AEinsteinController"/>, the
    /// A12 exemplar: everything shared lives in <see cref="LegacyLevelControllerBase"/>
    /// and what is below is exactly the per-hero surface — identity, the four kit-gate
    /// ability IDs, the ten authored placements, the geometry, the approach encounters,
    /// and the par.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) on
    /// Michaelerplatz → <i>Sonata Drift</i> lays a staff platform across the gutted
    /// orchestra pit → <i>Requiem Chord</i> shatters the Archive's resonance lock on
    /// the proscenium → <i>Fortissimo Wave</i> is lobbed over the scenery flat onto
    /// the second lock → the Eraser debut in the auditorium → the <i>Symphony of
    /// Sorrow</i> set-piece at the Nexus Resonance Source on the conductor's podium →
    /// the late Font approach in the green room → PreBoss (strike) → the gilded vault
    /// beneath the stage.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each, which is a
    /// V01c exception granted to Level 4A alone: each gate registers its one authored
    /// ability, and that grants no general puzzle eligibility to those abilities
    /// anywhere else. No gate requires a purchased Resonance node — Extra Note is
    /// irrelevant to the single staff platform this route needs.</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant regardless of layout or
    /// enemy count. Six approach standards, no authored elites, one boss, one Font,
    /// no Extractors.</para>
    /// </summary>
    public partial class Level04AMozartController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "mozart";
        public const string ID = "level_04a_mozart";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/mozart_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_mozart_dialogue.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_mozart_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_mozart_objective_gates";
        protected override string BossObjectiveKey => "legacy_mozart_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_mozart_objective_complete";

        // === The four kit gates (Mozart's canonical ability IDs) ===

        public override string MovementGateAbilityID => "mozart_sonata_drift";
        public override string SpecialOneGateAbilityID => "mozart_requiem_chord";
        public override string SpecialTwoGateAbilityID => "mozart_fortissimo_wave";
        public override string UltimateAbilityID => "mozart_symphony_of_sorrow";

        /// <summary>
        /// Both of Mozart's specials are projectiles that carry their authored
        /// <c>AbilityID</c> as the hit payload's <c>AttackID</c>, so both gates are
        /// struck mechanisms. The pair stays distinguishable because a Strike gate
        /// accepts its own ability ID and nothing else: the flat Requiem Chord can
        /// never open the Fortissimo lock, or the reverse. The difference is
        /// authored in the geometry instead — the Fortissimo lock sits behind a
        /// scenery flat only the lobbed arc clears.
        /// </summary>
        protected override LegacyGateMode SpecialTwoGateMode => LegacyGateMode.Strike;

        // === Layout ===
        // Short by design (LEGACY_CHECKPOINTS.md "scoped as a short level"): three
        // rooms across 6,720 px, roughly two thirds of a shared Act I level.

        public const float LevelWidth = 6720f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float ActorGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 2560f;
        private const float Room3StartX = 5120f;

        /// <summary>The gutted orchestra pit: the Sonata Drift gap. Wider than a double jump on purpose.</summary>
        public const float PitGapStartX = 1560f;
        public const float PitGapEndX = 2080f;

        /// <summary>The scenery flat the Fortissimo Wave has to be lobbed over.</summary>
        public const float SceneryFlatX = 3620f;
        public const float SceneryFlatTopY = 740f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        public override int NexusParSeconds => 300;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);

        /// <summary>
        /// Mid-air over the pit, at staff-platform altitude. Sonata Drift lays its
        /// platform under Mozart's feet rather than moving him, so the landing box
        /// is where he stands at the cast — the 30-frame traversal grace window
        /// cannot survive a later second jump.
        /// </summary>
        protected override Vector2 MovementGatePosition => new(1820f, 720f);
        protected override Vector2 SpecialOneGatePosition => new(2980f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3840f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4240f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4780f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4980f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5060f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5260f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6380f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Archive's Vienna detail. `chrono_slasher` is
        /// the Future Cultist melee standard and `hologram_drone` its ranged sibling
        /// — both era-agnostic cultist mobs, which is right for a raid on a nexus
        /// rather than a period street. No authored elites: 4A's only elite is the
        /// single scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(880f, ActorGroundY)),
            ("hologram_drone", new Vector2(1280f, ActorGroundY - 180f)),
            ("chrono_slasher", new Vector2(2740f, ActorGroundY)),
            ("chrono_slasher", new Vector2(3300f, ActorGroundY)),
            ("hologram_drone", new Vector2(4020f, ActorGroundY - 200f)),
            ("chrono_slasher", new Vector2(4540f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette ===
        // Vienna 1782 has no existing campaign level to borrow from, so this is an
        // authored placeholder in the house style: gaslit stone outside, gilt and
        // theatre crimson inside, Archive cyan on the machinery (see the plan's §9).

        protected override Color FloorColor => new(0.18f, 0.14f, 0.16f);
        protected override Color FloorEdgeColor => new(0.62f, 0.48f, 0.26f);
        protected override Color PlatformColor => new(0.31f, 0.22f, 0.25f);
        protected override Color WallColor => new(0.12f, 0.09f, 0.12f);
        protected override Color HazardColor => new(0.2f, 0.55f, 0.7f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1Michaelerplatz();
            BuildRoom2StageAndAuditorium();
            BuildRoom3GildedVault();
        }

        /// <summary>
        /// Room 1: Michaelerplatz and the theatre's forestage. Ends at the gutted
        /// orchestra pit — the boards simply stop, and the only footing across is a
        /// staff platform laid mid-fall.
        /// </summary>
        private void BuildRoom1Michaelerplatz() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.09f, 0.07f, 0.1f, 0.42f));
            BuildFloor(Room1StartX, GroundY, PitGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(560, 720, 240);
            BuildPlatform(980, 600, 220);
            BuildPlatform(1340, 700, 200);

            // East lip of the pit: the boards the staff platform has to reach.
            BuildFloor(PitGapEndX, GroundY, Room2StartX - PitGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_mozart_room_platz", new Color(0.88f, 0.76f, 0.42f));
            BuildRoomTransition("legacy_mozart_room_platz", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the stage and the auditorium. The proscenium lock (Special 1), the
        /// second lock behind the scenery flat (Special 2), the parterre where the
        /// Eraser drops in, and the conductor's podium carrying the Nexus source.
        /// </summary>
        private void BuildRoom2StageAndAuditorium() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.13f, 0.06f, 0.09f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            BuildPlatform(2800, 700, 220);
            BuildOneWayPlatform(3180, 600, 220);

            // The scenery flat: a waist-high painted panel. Mozart can hop it, and
            // the arcing Fortissimo Wave sails over it; the flat Requiem Chord
            // fired from the boards does not.
            BuildWall(SceneryFlatX, SceneryFlatTopY, GroundY - SceneryFlatTopY);

            BuildPlatform(4060, 580, 220);
            BuildPlatform(4480, 700, 240);

            BuildRoomDecoration(Room2StartX, "legacy_mozart_room_stage", new Color(0.95f, 0.55f, 0.62f));
            BuildRoomTransition("legacy_mozart_room_stage",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the gilded vault under the stage. 1,600 px wide so the boss's
        /// 8.0 m ranged band (480 px) and its 3.0 m melee band both have room to
        /// matter.
        /// </summary>
        private void BuildRoom3GildedVault() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.14f, 0.08f, 0.06f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600);

            BuildPlatform(5640, 690, 240);
            BuildPlatform(6180, 690, 240);
            BuildPlatform(5920, 480, 220);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_mozart_room_vault", new Color(0.95f, 0.72f, 0.35f));
            BuildRoomTransition("legacy_mozart_room_vault",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;
    }
}
