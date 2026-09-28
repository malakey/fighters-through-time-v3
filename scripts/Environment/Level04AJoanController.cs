using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Joan's Legacy Level</b>: Orléans, 1429, the assault on the
    /// Tourelles. Her nexus moment, revisited from the other side with the whole kit
    /// for the first time (V7.6; Package 11 B1).
    ///
    /// <para>Structurally a copy of the A12 Einstein exemplar: everything shared with
    /// the other eight variants lives in <see cref="LegacyLevelControllerBase"/>, and
    /// what is below is exactly the per-hero surface — identity, the four kit-gate
    /// ability IDs, the authored placements, the geometry, the approach encounters and
    /// the route's own par.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) on the
    /// drowned south bank → the Loire cut, where the boulevard's causeway is gone and
    /// the only way across is <i>Ascendant Wings</i> → <i>Righteous Smite</i> breaks
    /// the barred sluice brace → <i>Divine Piercing</i> bores out the Archive's
    /// reliquary pin → the Eraser debut on the curtain wall → the <i>Grand Crusade</i>
    /// set-piece at the Nexus Resonance Source, where the banner went up → the late
    /// Font approach → PreBoss (strike) → the banner ground.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each — the V01c
    /// exception granted to Level 4A alone. Each gate registers its one authored
    /// ability, and that grants those abilities no general puzzle eligibility anywhere
    /// else in the campaign. Every gate works on Joan's <b>baseline</b> kit: nothing
    /// here needs the purchased Wings Refresh node (design §5, F06 Joan).</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant. Six approach standards, no
    /// authored elites beyond the single scripted Eraser, one boss, one Font, no
    /// Extractors.</para>
    /// </summary>
    public partial class Level04AJoanController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "joan";
        public const string ID = "level_04a_joan";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/joan_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_joan_dialogue.tres";

        /// <summary>The era boss this variant duplicates (never edited in place).</summary>
        public const string EraBossResource = "res://resources/Bosses/siegemaster_duke.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_joan_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_joan_objective_gates";
        protected override string BossObjectiveKey => "legacy_joan_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_joan_objective_complete";

        // === The four kit gates (Joan's canonical ability IDs) ===

        public override string MovementGateAbilityID => "joan_ascendant_wings";
        public override string SpecialOneGateAbilityID => "joan_righteous_smite";
        public override string SpecialTwoGateAbilityID => "joan_divine_piercing";
        public override string UltimateAbilityID => "joan_grand_crusade";

        /// <summary>Righteous Smite's ground shockwave is a pooled projectile: an ordinary struck brace.</summary>
        protected override LegacyGateMode SpecialOneGateMode => LegacyGateMode.Strike;

        /// <summary>
        /// Divine Piercing is a stationary thrust flurry, not a zone — so the gate is
        /// a struck mechanism. Its thrusts resolve through hand-rolled enemy-hurtbox
        /// shape queries rather than a <c>Hitbox</c>, so the gate's strike surface sits
        /// on the <c>EnemyHurtbox</c> layer at the sweep size (Package 12 W8, retiring
        /// B1's <c>LegacyResonantEffigy</c> stand-in); see <see cref="OnLevelReady"/>.
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

        /// <summary>The Loire cut: the Wings gap. Wider than a double jump on purpose.</summary>
        public const float CutGapStartX = 1480f;
        public const float CutGapEndX = 2000f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        /// <summary>V01a: this route's own provisional Normal median, never pooled with another variant's.</summary>
        public override int NexusParSeconds => 285;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2100f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(3020f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3760f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4240f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4800f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4960f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5060f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5240f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6360f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Archive's detail on the boulevard.
        /// <c>chrono_slasher</c> is the Future Cultist melee standard and
        /// <c>hologram_drone</c> its ranged sibling — era-agnostic cultist mobs, which
        /// is right for a raid on a nexus rather than a period battlefield.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(880f, ActorGroundY)),
            ("hologram_drone", new Vector2(1220f, ActorGroundY - 180f)),
            ("chrono_slasher", new Vector2(2720f, ActorGroundY)),
            ("chrono_slasher", new Vector2(3300f, ActorGroundY)),
            ("hologram_drone", new Vector2(3980f, ActorGroundY - 200f)),
            ("chrono_slasher", new Vector2(4540f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Orléans siege mud, oak and fire (reused from Level 2) ===

        protected override Color FloorColor => new(0.21f, 0.19f, 0.15f);
        protected override Color FloorEdgeColor => new(0.46f, 0.40f, 0.27f);
        protected override Color PlatformColor => new(0.34f, 0.31f, 0.24f);
        protected override Color WallColor => new(0.15f, 0.14f, 0.11f);
        protected override Color HazardColor => new(0.62f, 0.24f, 0.10f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1DrownedBank();
            BuildRoom2Boulevard();
            BuildRoom3BannerGround();
        }

        /// <summary>
        /// Room 1: the drowned south bank. Ends at the Loire cut — the causeway the
        /// French crossed is simply gone, and the only way north is Ascendant Wings.
        /// </summary>
        private void BuildRoom1DrownedBank() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.10f, 0.08f, 0.06f, 0.4f));
            BuildFloor(Room1StartX, GroundY, CutGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(480, 740, 240);
            BuildPlatform(900, 620, 220);
            BuildOneWayPlatform(1280, 700, 200);

            // North side of the cut: the landing bank the Wings have to reach.
            BuildFloor(CutGapEndX, GroundY, Room2StartX - CutGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_joan_room_bank", new Color(0.85f, 0.72f, 0.4f));
            BuildRoomTransition("legacy_joan_room_bank", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the Tourelles boulevard. The barred sluice brace (Special 1), the
        /// Archive's reliquary pin (Special 2), the curtain wall where the Eraser
        /// drops in, and the Nexus Resonance Source where the banner went up.
        /// </summary>
        private void BuildRoom2Boulevard() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.12f, 0.09f, 0.06f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            BuildPlatform(2860, 700, 240);
            BuildOneWayPlatform(3220, 610, 220);
            BuildPlatform(3560, 720, 220);
            BuildPlatform(4020, 580, 240);
            BuildPlatform(4460, 700, 240);

            BuildRoomDecoration(Room2StartX, "legacy_joan_room_boulevard", new Color(0.95f, 0.6f, 0.25f));
            BuildRoomTransition("legacy_joan_room_boulevard",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the banner ground beneath the fallen tower. 1,600 px wide so the
        /// boss's 9.0 m ranged band (540 px) and its melee band both matter.
        /// </summary>
        private void BuildRoom3BannerGround() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.14f, 0.06f, 0.05f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600);

            BuildPlatform(5600, 690, 240);
            BuildPlatform(6220, 690, 240);
            BuildPlatform(5900, 470, 220);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_joan_room_banner", new Color(0.95f, 0.85f, 0.4f));
            BuildRoomTransition("legacy_joan_room_banner",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;

        // === Variant hooks ===

        /// <summary>
        /// Divine Piercing delivers through an enemy-hurtbox shape query and never
        /// reaches a PersistentObject-only surface, so the reliquary pin's strike
        /// surface sits on the <c>EnemyHurtbox</c> layer at the sweep size the retired
        /// effigy used. The gate still accepts only <c>joan_divine_piercing</c>.
        /// </summary>
        protected override void OnLevelReady() {
            base.OnLevelReady();
            if (SpecialTwoGate != null && IsInstanceValid(SpecialTwoGate)) {
                SpecialTwoGate.ConfigureStrikeSurface(LegacyKitGate.SweepStrikeSurfaceSize, onEnemyHurtbox: true);
            }
        }
    }
}
