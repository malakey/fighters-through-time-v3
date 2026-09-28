using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Leonardo's Legacy Level</b>: Florence, 1503, the workshop he
    /// stepped out of and never came back to. His nexus moment, revisited from the
    /// other side with the whole kit for the first time (V7.6; Package 11 B1).
    ///
    /// <para>Structurally a copy of the A12 Einstein exemplar: everything shared with
    /// the other eight variants lives in <see cref="LegacyLevelControllerBase"/>, and
    /// what is below is exactly the per-hero surface.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) in the
    /// Oltrarno yard → the Arno span, where the bridge deck is gone and the only way
    /// across is the <i>Ornithopter Flight</i> → the <i>Golden Ratio</i> spiral has to
    /// be held over the Archive's proportion frame → the <i>Clockwork Turret</i>
    /// batters open the sighting armature → the Eraser debut in the long gallery →
    /// the <i>Vitruvian Matrix</i> set-piece at the Nexus Resonance Source, on the
    /// unfinished ornithopter → the late Font approach → PreBoss (strike) → the
    /// commission hall.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each — the V01c
    /// exception granted to Level 4A alone — and every gate works on Leonardo's
    /// <b>baseline</b> kit, with no purchased Resonance node.</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional, identical for every variant.</para>
    /// </summary>
    public partial class Level04ALeonardoController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "leonardo";
        public const string ID = "level_04a_leonardo";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/leonardo_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_leonardo_dialogue.tres";

        /// <summary>The era boss this variant duplicates (never edited in place).</summary>
        public const string EraBossResource = "res://resources/Bosses/borgia_inquisitor.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_leonardo_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_leonardo_objective_gates";
        protected override string BossObjectiveKey => "legacy_leonardo_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_leonardo_objective_complete";

        // === The four kit gates (Leonardo's canonical ability IDs) ===

        public override string MovementGateAbilityID => "leonardo_ornithopter_flight";
        public override string SpecialOneGateAbilityID => "leonardo_golden_ratio";
        public override string SpecialTwoGateAbilityID => "leonardo_clockwork_turret";
        public override string UltimateAbilityID => "leonardo_vitruvian_matrix";

        /// <summary>The Golden Ratio spiral is a placed zone: the proportion frame reads the field.</summary>
        protected override LegacyGateMode SpecialOneGateMode => LegacyGateMode.Zone;

        /// <summary>
        /// The Clockwork Turret is a deployed construct whose bolts are the strike, so
        /// the armature is a struck mechanism. Those bolts resolve through a
        /// hand-rolled enemy-hurtbox shape query rather than a <c>Hitbox</c>, so the
        /// gate's strike surface sits on the <c>EnemyHurtbox</c> layer at the sweep
        /// size (Package 12 W8, retiring B1's <c>LegacyResonantEffigy</c> stand-in);
        /// see <see cref="OnLevelReady"/>.
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

        /// <summary>The Arno span: the Flight gap. Wider than a double jump on purpose.</summary>
        public const float SpanGapStartX = 1560f;
        public const float SpanGapEndX = 2040f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        /// <summary>V01a: this route's own provisional Normal median, never pooled with another variant's.</summary>
        public override int NexusParSeconds => 320;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2140f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(2940f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3680f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4160f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4720f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4880f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(4980f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5200f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6320f, ActorGroundY);

        // Package 12 W8 (GAP-03): the secret cache sits on the high drop-through ledge above the Arno approach
        // (top 610): an optional detour off the floor route, reached from the
        // low step with a double jump and BEFORE the movement gate, so no kit gate
        // stands between the hero and the 4A optional allocation.
        protected override Vector2? SecretCachePosition => new(960f, 515f);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Archive's detail inside the workshop quarter.
        /// No authored elites — 4A's only elite is the single scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(880f, ActorGroundY)),
            ("chrono_slasher", new Vector2(1240f, ActorGroundY)),
            ("hologram_drone", new Vector2(2700f, ActorGroundY - 170f)),
            ("chrono_slasher", new Vector2(3240f, ActorGroundY)),
            ("hologram_drone", new Vector2(4020f, ActorGroundY - 210f)),
            ("chrono_slasher", new Vector2(4480f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Florence workshop — oiled walnut, brass and lamp amber ===

        protected override Color FloorColor => new(0.20f, 0.15f, 0.09f);
        protected override Color FloorEdgeColor => new(0.52f, 0.38f, 0.16f);
        protected override Color PlatformColor => new(0.33f, 0.24f, 0.13f);
        protected override Color WallColor => new(0.13f, 0.10f, 0.06f);
        protected override Color HazardColor => new(0.70f, 0.32f, 0.12f);

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1OltrarnoYard();
            BuildRoom2Workshop();
            BuildRoom3CommissionHall();
        }

        /// <summary>
        /// Room 1: the Oltrarno yard and the broken span. The bridge deck is gone; the
        /// only way over the Arno is the ornithopter.
        /// </summary>
        private void BuildRoom1OltrarnoYard() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.11f, 0.08f, 0.05f, 0.4f));
            BuildFloor(Room1StartX, GroundY, SpanGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(540, 730, 240);
            BuildOneWayPlatform(960, 620, 220);
            BuildPlatform(1320, 700, 200);

            // North bank: the landing the Flight has to reach.
            BuildFloor(SpanGapEndX, GroundY, Room2StartX - SpanGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_leonardo_room_arno", new Color(0.85f, 0.65f, 0.3f));
            BuildRoomTransition("legacy_leonardo_room_arno", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the workshop and its long gallery. The proportion frame (Special 1),
        /// the sighting armature (Special 2), the gallery where the Eraser drops in,
        /// and the Nexus Resonance Source on the unfinished ornithopter.
        /// </summary>
        private void BuildRoom2Workshop() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.13f, 0.10f, 0.06f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            BuildPlatform(2780, 710, 240);
            BuildPlatform(3140, 600, 220);
            BuildOneWayPlatform(3520, 700, 240);
            BuildPlatform(3980, 570, 220);
            BuildPlatform(4400, 690, 240);

            BuildRoomDecoration(Room2StartX, "legacy_leonardo_room_workshop", new Color(0.95f, 0.78f, 0.35f));
            BuildRoomTransition("legacy_leonardo_room_workshop",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: the commission hall. 1,600 px wide so the boss's 7.0 m ranged band
        /// (420 px) and its melee band both matter.
        /// </summary>
        private void BuildRoom3CommissionHall() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.15f, 0.05f, 0.05f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600);

            BuildPlatform(5560, 690, 240);
            BuildPlatform(6180, 690, 240);
            BuildPlatform(5880, 480, 220);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_leonardo_room_hall", new Color(0.95f, 0.35f, 0.35f));
            BuildRoomTransition("legacy_leonardo_room_hall",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;

        // === Variant hooks ===

        /// <summary>
        /// Turret bolts deliver through an enemy-hurtbox shape query and never reach a
        /// PersistentObject-only surface, so the sighting armature's strike surface
        /// sits on the <c>EnemyHurtbox</c> layer at the sweep size the retired effigy
        /// used. The gate still accepts only <c>leonardo_clockwork_turret</c>.
        /// </summary>
        protected override void OnLevelReady() {
            base.OnLevelReady();
            if (SpecialTwoGate != null && IsInstanceValid(SpecialTwoGate)) {
                SpecialTwoGate.ConfigureStrikeSurface(LegacyKitGate.SweepStrikeSurfaceSize, onEnemyHurtbox: true);
            }
        }
    }
}
