using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Cleopatra's Legacy Level</b>: Alexandria, 30 BC, the night the
    /// basket came to the Royal Mausoleum. Her own nexus moment, revisited from the
    /// other side with the whole kit for the first time (V7.6; Package 11 B2).
    ///
    /// <para>Structurally identical to the Einstein exemplar — everything shared
    /// lives in <see cref="LegacyLevelControllerBase"/> and what is below is exactly
    /// the per-hero surface: identity, the four kit-gate ability IDs, ten authored
    /// placements, the geometry, the approach encounters and this route's own par.
    /// P01 Option A: the era art and palette are Level 8's (Alexandria), reused
    /// rather than re-authored.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) → the
    /// harbour steps → <i>Desert Mirage</i> across the flooded causeway → the
    /// <i>Serpent Nest</i> brood opens the cobra seal → the <i>Sandstorm Vortex</i>
    /// holds the sand-choked lock → the Eraser debut in the colonnade → the
    /// <i>Wrath of the Nile</i> set-piece at the Nexus Resonance Source → the late
    /// Font approach → PreBoss (strike) → the Asp's Hall.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each, the V01c
    /// exception granted to Level 4A alone: each gate registers its one authored
    /// ability, and that grants those abilities no general puzzle eligibility
    /// anywhere else in the campaign. Every required ability is a <b>canonical kit
    /// slot</b> — no gate depends on a purchased Resonance node.</para>
    ///
    /// <para><b>Gate shapes.</b> The Sandstorm Vortex is an ordinary placed zone, so
    /// its gate is native <c>Zone</c> mode. The Serpent Nest is a deployed
    /// construct, so its gate is <see cref="LegacyGateMode.CastWatch"/> (Package 12
    /// W8, retiring B2's <c>LegacyCastGateWatcher</c> stand-in): casting the nest
    /// within the gate's cast radius latches it, and the gate keeps the strike
    /// surface it always had — on the <c>EnemyHurtbox</c> layer — so the brood's
    /// own bites can still open the cobra seal.</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant regardless of layout or
    /// enemy count. Six approach standards, no authored elites, one boss, one Font,
    /// no Extractors.</para>
    /// </summary>
    public partial class Level04ACleopatraController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "cleopatra";
        public const string ID = "level_04a_cleopatra";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/cleopatra_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_cleopatra_dialogue.tres";

        /// <summary>The era boss this variant's legacy boss duplicates (never edited).</summary>
        public const string EraBossResource = "res://resources/Bosses/jackal_priest.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_cleopatra_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_cleopatra_objective_gates";
        protected override string BossObjectiveKey => "legacy_cleopatra_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_cleopatra_objective_complete";

        // === The four kit gates (Cleopatra's canonical ability IDs) ===

        public override string MovementGateAbilityID => "cleopatra_desert_mirage";
        public override string SpecialOneGateAbilityID => "cleopatra_serpent_nest";
        public override string SpecialTwoGateAbilityID => "cleopatra_sandstorm_vortex";
        public override string UltimateAbilityID => "cleopatra_wrath_of_the_nile";

        /// <summary>The Serpent Nest is a deployed construct: the cast is what the seal answers.</summary>
        protected override LegacyGateMode SpecialOneGateMode => LegacyGateMode.CastWatch;

        // Special 2 is a real placed zone, so the base's Zone default is already correct.

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

        /// <summary>The flooded causeway: the Desert Mirage gap, wider than a double jump.</summary>
        public const float CausewayGapStartX = 1480f;
        public const float CausewayGapEndX = 2000f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        /// <summary>
        /// V01a placeholder: this route's own benchmark, never pooled with another
        /// variant's. Pending the Normal-difficulty median measurement.
        /// </summary>
        public override int NexusParSeconds => 320;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2100f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(2960f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3700f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4180f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4740f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4960f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5000f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5220f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6360f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Unbound's Alexandria detail.
        /// <c>chrono_slasher</c> is the era-agnostic cultist melee standard and
        /// <c>plasma_spear_ward</c> the palace-guard standard already authored for
        /// Level 8. No authored elites: 4A's only elite is the scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(880f, ActorGroundY)),
            ("chrono_slasher", new Vector2(1220f, ActorGroundY)),
            ("plasma_spear_ward", new Vector2(2700f, ActorGroundY)),
            ("chrono_slasher", new Vector2(3240f, ActorGroundY)),
            ("plasma_spear_ward", new Vector2(3940f, ActorGroundY)),
            ("chrono_slasher", new Vector2(4500f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Level 8's Alexandria (P01 Option A — reuse the era) ===

        protected override Color FloorColor => new(0.30f, 0.25f, 0.16f);
        protected override Color FloorEdgeColor => new(0.72f, 0.60f, 0.34f);
        protected override Color PlatformColor => new(0.40f, 0.34f, 0.22f);
        protected override Color WallColor => new(0.14f, 0.12f, 0.09f);
        protected override Color HazardColor => new(0.66f, 0.34f, 0.12f);

        private static readonly Color MausoleumFloorColor = new(0.20f, 0.18f, 0.15f);
        private static readonly Color MausoleumPlatformColor = new(0.28f, 0.25f, 0.20f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1HarbourSteps();
            BuildRoom2RoyalMausoleum();
            BuildRoom3AspsHall();
        }

        /// <summary>
        /// Room 1: the harbour steps under the Pharos light. Ends at the flooded
        /// causeway — the stone simply stops, and the only way east is Desert Mirage.
        /// </summary>
        private void BuildRoom1HarbourSteps() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.10f, 0.09f, 0.07f, 0.4f));
            BuildFloor(Room1StartX, GroundY, CausewayGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(480, 730, 240);
            BuildPlatform(900, 610, 220);
            BuildPlatform(1300, 700, 200);

            // East side of the causeway: the landing quay the Mirage has to reach.
            BuildFloor(CausewayGapEndX, GroundY, Room2StartX - CausewayGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_cleopatra_room_harbour", new Color(0.92f, 0.80f, 0.46f));
            BuildRoomTransition("legacy_cleopatra_room_harbour", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the Royal Mausoleum. The cobra seal (Special 1), the sand-choked
        /// lock (Special 2), the colonnade where the Eraser drops in, and the Nexus
        /// Resonance Source at the far end.
        /// </summary>
        private void BuildRoom2RoyalMausoleum() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.12f, 0.10f, 0.08f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560, MausoleumFloorColor);

            BuildPlatform(2800, 700, 220, MausoleumPlatformColor);
            BuildOneWayPlatform(3160, 620, 220);
            BuildPlatform(3580, 700, 240, MausoleumPlatformColor);
            BuildPlatform(4040, 580, 220, MausoleumPlatformColor);
            BuildPlatform(4460, 700, 240, MausoleumPlatformColor);

            BuildRoomDecoration(Room2StartX, "legacy_cleopatra_room_mausoleum", new Color(0.86f, 0.72f, 0.38f));
            BuildRoomTransition("legacy_cleopatra_room_mausoleum",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the Asp's Hall. 1,600 px wide so the boss's 8.0 m ranged band
        /// (480 px) and its 3.0 m melee band both have room to matter.
        /// </summary>
        private void BuildRoom3AspsHall() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.14f, 0.08f, 0.10f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600, MausoleumFloorColor);

            BuildPlatform(5620, 690, 240, MausoleumPlatformColor);
            BuildPlatform(6160, 690, 240, MausoleumPlatformColor);
            BuildPlatform(5900, 480, 220, MausoleumPlatformColor);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_cleopatra_room_asp_hall", new Color(0.95f, 0.42f, 0.40f));
            BuildRoomTransition("legacy_cleopatra_room_asp_hall",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>
        /// Gives the CastWatch nest gate its strike surface, which a CastWatch gate
        /// does not build by default: the brood's bites query the <c>EnemyHurtbox</c>
        /// layer and still carry the ability ID. Runs after the sealed
        /// <c>BuildLevel</c> has created the gates.
        /// </summary>
        protected override void OnLevelReady() {
            base.OnLevelReady();
            if (SpecialOneGate != null && IsInstanceValid(SpecialOneGate)) {
                SpecialOneGate.ConfigureStrikeSurface(onEnemyHurtbox: true);
            }
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;
    }
}
