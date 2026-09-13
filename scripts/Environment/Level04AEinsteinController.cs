using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Einstein's Legacy Level</b>: Princeton, 1945, the night of the
    /// beam. The hero's own nexus moment, revisited from the other side with the
    /// whole kit for the first time (V7.6; Package 11 A12).
    ///
    /// <para>This is the worked exemplar the three B-wave agents copy for the other
    /// eight heroes. Everything shared with those variants lives in
    /// <see cref="LegacyLevelControllerBase"/>; what is below is exactly the per-hero
    /// surface and nothing else — identity, the four kit-gate ability IDs, eleven
    /// authored placements, the geometry, the approach encounters, and the par.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) → the
    /// Institute lawn → <i>Warp</i> across the severed span → <i>Mass-Energy
    /// Conversion</i> shatters the Archive's containment lens → <i>Relativity Rift</i>
    /// slows the runaway chronal centrifuge → the Eraser debut on the colonnade →
    /// the <i>Cosmological Constant</i> set-piece at the Nexus Resonance Source →
    /// the late Font approach → PreBoss (strike) → the boss vault.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each, which is a
    /// V01c exception granted to Level 4A alone: each gate registers its one authored
    /// ability, and that grants no general puzzle eligibility to those abilities
    /// anywhere else in the campaign.</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant regardless of layout or
    /// enemy count. Six approach standards, no elites beyond the single scripted
    /// Eraser, one boss, one Font, no Extractors (4A's optional allocation is its
    /// secret, not a machine row).</para>
    /// </summary>
    public partial class Level04AEinsteinController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "einstein";
        public const string ID = "level_04a_einstein";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/einstein_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_einstein_dialogue.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_einstein_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_einstein_objective_gates";
        protected override string BossObjectiveKey => "legacy_einstein_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_einstein_objective_complete";

        // === The four kit gates (Einstein's canonical ability IDs) ===

        public override string MovementGateAbilityID => "einstein_relativity_warp";
        public override string SpecialOneGateAbilityID => "einstein_mass_energy_conversion";
        public override string SpecialTwoGateAbilityID => "einstein_relativity_rift";
        public override string UltimateAbilityID => "einstein_cosmological_constant";

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

        /// <summary>The severed span: the Warp gap. Wider than a double jump on purpose.</summary>
        public const float SpanGapStartX = 1500f;
        public const float SpanGapEndX = 2020f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        public override int ParSeconds => 300;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2120f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(2980f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3720f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4200f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4760f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4980f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5000f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5220f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6360f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Archive's Princeton detail. `chrono_slasher`
        /// is the Future Cultist melee standard, `tech_enforcer_drone` its ranged
        /// sibling — both era-agnostic cultist mobs, which is right for a raid on a
        /// nexus rather than a period battlefield. No authored elites: 4A's only
        /// elite is the single scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(900f, ActorGroundY)),
            ("chrono_slasher", new Vector2(1250f, ActorGroundY)),
            ("hologram_drone", new Vector2(2700f, ActorGroundY - 160f)),
            ("chrono_slasher", new Vector2(3260f, ActorGroundY)),
            ("hologram_drone", new Vector2(3960f, ActorGroundY - 200f)),
            ("chrono_slasher", new Vector2(4520f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Princeton night, lamp amber, Archive cyan ===

        protected override Color FloorColor => new(0.16f, 0.17f, 0.21f);
        protected override Color FloorEdgeColor => new(0.44f, 0.42f, 0.33f);
        protected override Color PlatformColor => new(0.26f, 0.27f, 0.32f);
        protected override Color WallColor => new(0.11f, 0.12f, 0.16f);
        protected override Color HazardColor => new(0.2f, 0.55f, 0.7f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1InstituteLawn();
            BuildRoom2ContainmentWing();
            BuildRoom3BossVault();
        }

        /// <summary>
        /// Room 1: the Institute lawn under the beam. Ends at the severed span — the
        /// ground simply stops, and the only way east is Relativity Warp.
        /// </summary>
        private void BuildRoom1InstituteLawn() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.07f, 0.08f, 0.12f, 0.4f));
            BuildFloor(Room1StartX, GroundY, SpanGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(520, 720, 240);
            BuildPlatform(940, 600, 220);
            BuildPlatform(1340, 700, 200);

            // East side of the span: the landing shelf the Warp has to reach.
            BuildFloor(SpanGapEndX, GroundY, Room2StartX - SpanGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_einstein_room_lawn", new Color(0.85f, 0.78f, 0.45f));
            BuildRoomTransition("legacy_einstein_room_lawn", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the containment wing. The Archive's lens (Special 1), the runaway
        /// centrifuge (Special 2), the colonnade where the Eraser drops in, and the
        /// Nexus Resonance Source at the far end.
        /// </summary>
        private void BuildRoom2ContainmentWing() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.06f, 0.09f, 0.13f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            BuildPlatform(2820, 700, 220);
            BuildOneWayPlatform(3180, 620, 220);
            BuildPlatform(3600, 700, 240);
            BuildPlatform(4060, 580, 220);
            BuildPlatform(4480, 700, 240);

            BuildRoomDecoration(Room2StartX, "legacy_einstein_room_containment", new Color(0.5f, 0.85f, 0.95f));
            BuildRoomTransition("legacy_einstein_room_containment",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the vault beneath the lens. 1,600 px wide so the boss's 9.0 m
        /// ranged band (540 px) and its 3.0 m melee band both have room to matter.
        /// </summary>
        private void BuildRoom3BossVault() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.1f, 0.07f, 0.14f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600);

            BuildPlatform(5640, 690, 240);
            BuildPlatform(6180, 690, 240);
            BuildPlatform(5920, 480, 220);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_einstein_room_vault", new Color(0.95f, 0.4f, 0.35f));
            BuildRoomTransition("legacy_einstein_room_vault",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;
    }
}
