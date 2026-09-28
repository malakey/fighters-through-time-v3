using System.Collections.Generic;
using Godot;
using FTT.Characters;
using FTT.Characters.Abilities;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Pocahontas's Legacy Level</b>: Tsenacommacah, 1607, the river
    /// season the Archive came for. The hero's own nexus moment, revisited from the
    /// other side with the whole kit for the first time (V7.6; Package 11 B3).
    ///
    /// <para>Structurally identical to <see cref="Level04AEinsteinController"/>, the
    /// A12 exemplar: everything shared lives in <see cref="LegacyLevelControllerBase"/>
    /// and what is below is exactly the per-hero surface — identity, the four kit-gate
    /// ability IDs, the ten authored placements, the geometry, the approach encounters,
    /// and the par.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) at the
    /// landing → <i>Breeze Glide</i> carries the river gorge where the Archive cut the
    /// bank away → <i>Spirit Strike</i> dives the eagle onto the siphon anchor →
    /// <i>Vine Snare</i> holds the weir gate open → the Eraser debut on the burnt
    /// terraces → the <i>Tidewater Tempest</i> set-piece at the Nexus Resonance Source
    /// on the council fire → the late Font approach at the spring → PreBoss (strike) →
    /// the flooded hollow.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each, which is a
    /// V01c exception granted to Level 4A alone. No gate requires a purchased
    /// Resonance node: the gorge is authored to one ordinary Breeze Glide, never to
    /// Second Glide's extra airtime entry.</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant regardless of layout or
    /// enemy count. Six approach standards, no authored elites, one boss, one Font,
    /// no Extractors.</para>
    /// </summary>
    public partial class Level04APocahontasController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "pocahontas";
        public const string ID = "level_04a_pocahontas";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/pocahontas_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_pocahontas_dialogue.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_pocahontas_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_pocahontas_objective_gates";
        protected override string BossObjectiveKey => "legacy_pocahontas_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_pocahontas_objective_complete";

        // === The four kit gates (Pocahontas's canonical ability IDs) ===

        public override string MovementGateAbilityID => "pocahontas_breeze_glide";
        public override string SpecialOneGateAbilityID => "pocahontas_spirit_strike";
        public override string SpecialTwoGateAbilityID => "pocahontas_vine_snare";
        public override string UltimateAbilityID => "pocahontas_tidewater_tempest";

        // Special 2 (the weir gate) keeps the base's Zone declaration. Vine Snare
        // deploys a pooled VineSnareNode construct rather than a story_zone, so the
        // gate turns on LegacyKitGate.ZoneAcceptsOwnedConstruct in OnLevelReady
        // (Package 12 W8, retiring B3's VineSnareGateResolver stand-in): a live snare
        // the hero deployed with Vine Snare, within the gate's ResolveRadius, holds
        // the weir open. It still accepts that ability and nothing else.

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

        /// <summary>The cut riverbank: the Breeze Glide gorge. Wider than a double jump on purpose.</summary>
        public const float GorgeGapStartX = 1520f;
        public const float GorgeGapEndX = 2040f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        public override int NexusParSeconds => 300;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);

        /// <summary>
        /// Just out over the gorge, at the launch height the glide holds. Breeze
        /// Glide dashes at 350 px/s through its 21 cast frames and then falls at only
        /// 30 px/s, so the gate's landing box sits roughly 180 px past the lip —
        /// inside the traversal grace window rather than at the far shelf, which the
        /// glide reaches seconds later.
        /// </summary>
        protected override Vector2 MovementGatePosition => new(1700f, GroundY);
        protected override Vector2 SpecialOneGatePosition => new(2940f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3680f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4180f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4740f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4960f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(5040f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5240f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6380f, ActorGroundY);

        // Package 12 W8 (GAP-03): the secret cache sits on the high ledge above the riverbank
        // (top 612): an optional detour off the floor route, reached from the
        // low step with a double jump and BEFORE the movement gate, so no kit gate
        // stands between the hero and the 4A optional allocation.
        protected override Vector2? SecretCachePosition => new(920f, 517f);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Archive's tidewater detail. `chrono_slasher`
        /// is the Future Cultist melee standard and `hologram_drone` its ranged
        /// sibling — both era-agnostic cultist mobs, which is right for a raid on a
        /// nexus rather than a period skirmish. No authored elites: 4A's only elite
        /// is the single scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(840f, ActorGroundY)),
            ("chrono_slasher", new Vector2(1220f, ActorGroundY)),
            ("hologram_drone", new Vector2(2700f, ActorGroundY - 170f)),
            ("chrono_slasher", new Vector2(3240f, ActorGroundY)),
            ("hologram_drone", new Vector2(3980f, ActorGroundY - 210f)),
            ("chrono_slasher", new Vector2(4480f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette ===
        // Tsenacommacah 1607 has no existing campaign level to borrow from, so this
        // is an authored placeholder in the house style: river silt and wet loam,
        // pine and marsh green, Archive cyan on the machinery (see the plan's §9).

        protected override Color FloorColor => new(0.15f, 0.18f, 0.15f);
        protected override Color FloorEdgeColor => new(0.44f, 0.52f, 0.34f);
        protected override Color PlatformColor => new(0.24f, 0.29f, 0.24f);
        protected override Color WallColor => new(0.1f, 0.13f, 0.12f);
        protected override Color HazardColor => new(0.2f, 0.55f, 0.7f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1Riverbank();
            BuildRoom2WeirAndTerraces();
            BuildRoom3FloodedHollow();
        }

        /// <summary>
        /// Room 1: the landing and the upper bank. Ends where the Archive's cut took
        /// the riverbank away — the ground simply stops, and the only way east is on
        /// the wind.
        /// </summary>
        private void BuildRoom1Riverbank() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.07f, 0.1f, 0.09f, 0.42f));
            BuildFloor(Room1StartX, GroundY, GorgeGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(520, 720, 240);
            BuildPlatform(920, 620, 220);
            BuildPlatform(1300, 700, 200);

            // East bank: the shelf the glide has to carry to.
            BuildFloor(GorgeGapEndX, GroundY, Room2StartX - GorgeGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_pocahontas_room_riverbank", new Color(0.7f, 0.85f, 0.55f));
            BuildRoomTransition("legacy_pocahontas_room_riverbank", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the weir and the burnt terraces. The siphon anchor the eagle dives
        /// (Special 1), the weir gate the roots hold open (Special 2), the terraces
        /// where the Eraser drops in, and the council fire carrying the Nexus source.
        /// </summary>
        private void BuildRoom2WeirAndTerraces() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.06f, 0.11f, 0.12f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            // The dive perch: the eagle's swoop starts high and arrives diagonally.
            BuildPlatform(2680, 660, 220);
            BuildOneWayPlatform(3120, 620, 220);
            BuildPlatform(3520, 720, 220);
            BuildPlatform(4020, 580, 220);
            BuildPlatform(4460, 700, 240);

            BuildRoomDecoration(Room2StartX, "legacy_pocahontas_room_weir", new Color(0.55f, 0.9f, 0.8f));
            BuildRoomTransition("legacy_pocahontas_room_weir",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the flooded hollow below the weir. 1,600 px wide so the boss's
        /// 10.0 m ranged band (600 px) and its 3.5 m melee band both have room to
        /// matter.
        /// </summary>
        private void BuildRoom3FloodedHollow() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.06f, 0.1f, 0.14f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600);

            BuildPlatform(5640, 690, 240);
            BuildPlatform(6180, 690, 240);
            BuildPlatform(5920, 480, 220);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_pocahontas_room_hollow", new Color(0.45f, 0.75f, 0.95f));
            BuildRoomTransition("legacy_pocahontas_room_hollow",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;

        // === Special 2: the construct gate ===

        /// <summary>
        /// Turns on the weir gate's owned-construct acceptance once the base has
        /// built the kit gates. <see cref="LegacyLevelControllerBase.BuildLevel"/> is
        /// sealed and runs before this hook, so the option is set here rather than in
        /// <see cref="BuildNexusGeometry"/>, where <see cref="SpecialTwoGate"/> does
        /// not exist yet. The gate reads the option every poll.
        /// </summary>
        protected override void OnLevelReady() {
            base.OnLevelReady();
            if (SpecialTwoGate == null || !IsInstanceValid(SpecialTwoGate)) return;
            SpecialTwoGate.ZoneAcceptsOwnedConstruct = true;
        }
    }
}
