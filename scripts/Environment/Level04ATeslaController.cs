using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Tesla's Legacy Level</b>: Chicago, 1893, the night the
    /// Exposition first ran on alternating current. His own nexus moment, revisited
    /// from the other side with the whole kit for the first time (V7.6; Package 11
    /// B2).
    ///
    /// <para>Structurally identical to the Einstein exemplar — everything shared
    /// lives in <see cref="LegacyLevelControllerBase"/> and what is below is exactly
    /// the per-hero surface. P01 Option A: the era art and palette are Level 3's
    /// (Chicago, 1893), reused rather than re-authored.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) → the
    /// Court of Honor → <i>Lightning Blink</i> across the severed gantry → a
    /// <i>Tesla Coil</i> wakes the dead resonator mast → the <i>Lorentz Pulse</i>
    /// throws the magnetised breaker → the Eraser debut on the dynamo floor → the
    /// <i>Wardenclyffe Cataclysm</i> set-piece at the Nexus Resonance Source → the
    /// late Font approach → PreBoss (strike) → the Dynamo Vault.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each, the V01c
    /// exception granted to Level 4A alone. Every required ability is a
    /// <b>canonical kit slot</b> — no gate depends on a purchased Resonance node.</para>
    ///
    /// <para><b>Gate shapes.</b> The Lorentz Pulse raises an ordinary hitbox
    /// carrying its ability ID, so its gate is native <c>Strike</c> mode (the base's
    /// Special-2 default is <c>Zone</c>, which this kit does not use). The Tesla Coil
    /// is a deployed construct — neither of A12's combat shapes, because the coil's
    /// arcs sweep only <c>EnemyHurtbox</c>-layer hurtboxes and never reach a gate
    /// surface. That gate keeps its <c>Strike</c> presentation and is resolved by a
    /// <see cref="LegacyCastGateWatcher"/>, B2's stand-in for the fifth gate mode
    /// A12's dossier anticipated (plan §9, B2).</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional. Six approach standards, no authored elites, one boss,
    /// one Font, no Extractors.</para>
    /// </summary>
    public partial class Level04ATeslaController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "tesla";
        public const string ID = "level_04a_tesla";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/tesla_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_tesla_dialogue.tres";

        /// <summary>The era boss this variant's legacy boss duplicates (never edited).</summary>
        public const string EraBossResource = "res://resources/Bosses/chronal_inventor.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_tesla_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_tesla_objective_gates";
        protected override string BossObjectiveKey => "legacy_tesla_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_tesla_objective_complete";

        // === The four kit gates (Tesla's canonical ability IDs) ===

        public override string MovementGateAbilityID => "tesla_lightning_blink";
        public override string SpecialOneGateAbilityID => "tesla_tesla_coil";
        public override string SpecialTwoGateAbilityID => "tesla_lorentz_pulse";
        public override string UltimateAbilityID => "tesla_wardenclyffe_cataclysm";

        /// <summary>The Lorentz Pulse is a struck hitbox, not a placed zone.</summary>
        protected override LegacyGateMode SpecialTwoGateMode => LegacyGateMode.Strike;

        // === Layout ===

        public const float LevelWidth = 6720f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float ActorGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 2560f;
        private const float Room3StartX = 5120f;

        /// <summary>The severed gantry: the Lightning Blink gap, wider than a double jump.</summary>
        public const float GantryGapStartX = 1520f;
        public const float GantryGapEndX = 2040f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        /// <summary>
        /// V01a placeholder: this route's own benchmark, never pooled with another
        /// variant's. Pending the Normal-difficulty median measurement.
        /// </summary>
        public override int ParSeconds => 310;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2140f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(2980f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3740f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4220f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4760f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4980f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5020f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5240f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6360f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Unbound's Exposition detail.
        /// <c>chrono_slasher</c> is the era-agnostic cultist melee standard and
        /// <c>voltaic_shock_drone</c> the Chicago ranged standard already authored
        /// for Level 3. No authored elites: 4A's only elite is the scripted Eraser
        /// debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(920f, ActorGroundY)),
            ("chrono_slasher", new Vector2(1260f, ActorGroundY)),
            ("voltaic_shock_drone", new Vector2(2720f, ActorGroundY - 160f)),
            ("chrono_slasher", new Vector2(3280f, ActorGroundY)),
            ("voltaic_shock_drone", new Vector2(3980f, ActorGroundY - 200f)),
            ("chrono_slasher", new Vector2(4540f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Level 3's Chicago, 1893 (P01 Option A — reuse the era) ===

        protected override Color FloorColor => new(0.17f, 0.19f, 0.26f);
        protected override Color FloorEdgeColor => new(0.55f, 0.72f, 0.86f);
        protected override Color PlatformColor => new(0.24f, 0.27f, 0.35f);
        protected override Color WallColor => new(0.12f, 0.14f, 0.2f);
        protected override Color HazardColor => new(0.85f, 0.72f, 0.25f);

        private static readonly Color DynamoFloorColor = new(0.15f, 0.16f, 0.22f);
        private static readonly Color DynamoPlatformColor = new(0.22f, 0.25f, 0.33f);

        // === Runtime handles ===

        /// <summary>
        /// Resolves the Tesla Coil gate. Public so the content suite can pin that
        /// the construct special has a recogniser at all — without one the gate is
        /// unopenable and the route is stranded.
        /// </summary>
        public LegacyCastGateWatcher CoilWatcher { get; private set; }

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1CourtOfHonor();
            BuildRoom2ElectricityBuilding();
            BuildRoom3DynamoVault();
        }

        /// <summary>
        /// Room 1: the Court of Honor under a hundred thousand lamps. Ends at the
        /// severed gantry — the walkway simply stops, and the only way east is
        /// Lightning Blink.
        /// </summary>
        private void BuildRoom1CourtOfHonor() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.07f, 0.09f, 0.14f, 0.4f));
            BuildFloor(Room1StartX, GroundY, GantryGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(500, 720, 240);
            BuildPlatform(920, 600, 220);
            BuildPlatform(1320, 700, 200);

            // East side of the gantry: the landing deck the Blink has to reach.
            BuildFloor(GantryGapEndX, GroundY, Room2StartX - GantryGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_tesla_room_court", new Color(0.62f, 0.82f, 0.95f));
            BuildRoomTransition("legacy_tesla_room_court", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the Electricity Building. The dead resonator mast (Special 1), the
        /// magnetised breaker (Special 2), the dynamo floor where the Eraser drops
        /// in, and the Nexus Resonance Source at the far end.
        /// </summary>
        private void BuildRoom2ElectricityBuilding() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.06f, 0.10f, 0.15f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560, DynamoFloorColor);

            BuildPlatform(2840, 700, 220, DynamoPlatformColor);
            BuildOneWayPlatform(3200, 620, 220);
            BuildPlatform(3620, 700, 240, DynamoPlatformColor);
            BuildPlatform(4080, 580, 220, DynamoPlatformColor);
            BuildPlatform(4500, 700, 240, DynamoPlatformColor);

            BuildRoomDecoration(Room2StartX, "legacy_tesla_room_electricity", new Color(0.95f, 0.85f, 0.42f));
            BuildRoomTransition("legacy_tesla_room_electricity",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the Dynamo Vault. 1,600 px wide so the boss's 10.0 m ranged band
        /// (600 px) and its 2.5 m melee band both have room to matter.
        /// </summary>
        private void BuildRoom3DynamoVault() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.10f, 0.08f, 0.15f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600, DynamoFloorColor);

            BuildPlatform(5660, 690, 240, DynamoPlatformColor);
            BuildPlatform(6200, 690, 240, DynamoPlatformColor);
            BuildPlatform(5940, 480, 220, DynamoPlatformColor);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_tesla_room_dynamo", new Color(0.98f, 0.62f, 0.30f));
            BuildRoomTransition("legacy_tesla_room_dynamo",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>
        /// Attaches the Tesla Coil recogniser. Runs after the sealed
        /// <c>BuildLevel</c> has created the gates, so the handle is live.
        /// </summary>
        protected override void OnLevelReady() {
            base.OnLevelReady();
            if (CoilWatcher != null) return;
            CoilWatcher = new LegacyCastGateWatcher {
                Name = "CoilGateWatcher",
                Gate = SpecialOneGate,
                RequiredAbilityID = SpecialOneGateAbilityID,
                Position = SpecialOneGatePosition
            };
            AddChild(CoilWatcher);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;
    }
}
