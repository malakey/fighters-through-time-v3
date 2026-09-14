using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Level 4A — <b>Shakespeare's Legacy Level</b>: London, 1599, the Globe's
    /// opening season. His own nexus moment, revisited from the other side with the
    /// whole kit for the first time (V7.6; Package 11 B2).
    ///
    /// <para>Structurally identical to the Einstein exemplar — everything shared
    /// lives in <see cref="LegacyLevelControllerBase"/> and what is below is exactly
    /// the per-hero surface. P01 Option A: the era art and palette are Level 10's
    /// (London, 1599), reused rather than re-authored.</para>
    ///
    /// <para><b>Route</b> (LEGACY_CHECKPOINTS.md): Entry (self-activating) → the
    /// Bankside approach → <i>Prospero's Flight</i> across the collapsed span →
    /// <i>Yorick's Lament</i> cracks the death's-head bell → <i>The Tempest</i> drives
    /// the thunder-run's storm machine → the Eraser debut in the yard → the
    /// <i>All the World's a Stage</i> set-piece at the Nexus Resonance Source → the
    /// late Font approach → PreBoss (strike) → beneath the stage.</para>
    ///
    /// <para><b>Kit-keyed design.</b> The whole kit is required once each, the V01c
    /// exception granted to Level 4A alone. Every required ability is a
    /// <b>canonical kit slot</b>: the movement gate requires <i>Prospero's Flight</i>,
    /// the baseline gust/glide, and explicitly <b>not</b> The Tempest's V7.6 apex-jump
    /// traversal flag, which is a purchased Resonance node and can never be a
    /// prerequisite.</para>
    ///
    /// <para><b>Gate shapes.</b> Yorick's Lament is a projectile carrying its ability
    /// ID, so its gate is native <c>Strike</c> mode. The Tempest is neither of A12's
    /// combat shapes: it lifts the caster and pushes adjacent <i>bodies</i>, spawning
    /// no zone and raising no hitbox at all. That gate keeps its <c>Zone</c>
    /// declaration for the base's bookkeeping and is resolved by a
    /// <see cref="LegacyCastGateWatcher"/>, B2's stand-in for the fifth gate mode
    /// A12's dossier anticipated (plan §9, B2).</para>
    ///
    /// <para><b>Locked economy</b> (DUST_ECONOMY.md row 4A): 15 required-encounter +
    /// 25 boss + 10 optional. Six approach standards, no authored elites, one boss,
    /// one Font, no Extractors.</para>
    /// </summary>
    public partial class Level04AShakespeareController : LegacyLevelControllerBase {

        // === Identity ===

        public const string Hero = "shakespeare";
        public const string ID = "level_04a_shakespeare";
        public const string Checkpoint0 = ID + "_checkpoint_0";
        public const string Checkpoint1 = ID + "_checkpoint_1";
        public const string BossResource = "res://resources/Bosses/legacy/shakespeare_legacy_boss.tres";
        public const string DialogueResourcePath = "res://resources/Dialogue/level_04a_shakespeare_dialogue.tres";

        /// <summary>The era boss this variant's legacy boss duplicates (never edited).</summary>
        public const string EraBossResource = "res://resources/Bosses/tragedy_king.tres";

        /// <summary>The two authored checkpoint IDs, in route order.</summary>
        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1 };

        public override string HeroCharacterID => Hero;
        public override string LevelTitleKey => "legacy_shakespeare_level_title";
        public override string BossResourcePath => BossResource;

        protected override string InitialObjectiveKey => "legacy_shakespeare_objective_gates";
        protected override string BossObjectiveKey => "legacy_shakespeare_objective_boss";
        protected override string CompletionObjectiveKey => "legacy_shakespeare_objective_complete";

        // === The four kit gates (Shakespeare's canonical ability IDs) ===

        public override string MovementGateAbilityID => "shakespeare_prosperos_flight";
        public override string SpecialOneGateAbilityID => "shakespeare_yoricks_lament";
        public override string SpecialTwoGateAbilityID => "shakespeare_the_tempest";
        public override string UltimateAbilityID => "shakespeare_all_the_worlds_a_stage";

        // Special 1 is a real projectile strike (the base's default) and Special 2
        // keeps the base's Zone declaration while the cast watcher resolves it.

        // === Layout ===

        public const float LevelWidth = 6720f;
        public const float LevelHeight = 1080f;
        private const float GroundY = 900f;
        private const float ActorGroundY = 850f;

        private const float Room1StartX = 0f;
        private const float Room2StartX = 2560f;
        private const float Room3StartX = 5120f;

        /// <summary>The collapsed span: the Prospero's Flight gap, wider than a double jump.</summary>
        public const float SpanGapStartX = 1460f;
        public const float SpanGapEndX = 2020f;

        public static readonly Rect2 Room1CameraBounds = new(Room1StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room2CameraBounds = new(Room2StartX, 0, 2560, LevelHeight);
        public static readonly Rect2 Room3CameraBounds = new(Room3StartX - 320f, 0, 1920, LevelHeight);

        /// <summary>
        /// V01a placeholder: this route's own benchmark, never pooled with another
        /// variant's. Pending the Normal-difficulty median measurement.
        /// </summary>
        public override int NexusParSeconds => 330;

        // === Authored placements ===

        protected override Vector2 EntryCheckpointPosition => new(200f, ActorGroundY);
        protected override Vector2 MovementGatePosition => new(2120f, ActorGroundY);
        protected override Vector2 SpecialOneGatePosition => new(2940f, ActorGroundY);
        protected override Vector2 SpecialTwoGatePosition => new(3680f, ActorGroundY);
        protected override Vector2 EraserDebutPosition => new(4160f, ActorGroundY);
        protected override Vector2 NexusSourcePosition => new(4720f, ActorGroundY);
        protected override Vector2 UltimateTargetPosition => new(4940f, ActorGroundY);
        protected override Vector2 RestorationFontPosition => new(4980f, ActorGroundY);
        protected override Vector2 PreBossCheckpointPosition => new(5200f, ActorGroundY);
        protected override Vector2 BossSpawnPosition => new(6360f, ActorGroundY);

        public override Vector2 PlayerSpawnPosition => new(200f, ActorGroundY);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        // === Authored encounter table (the locked 4A ledger row lives here) ===

        /// <summary>
        /// Six approach standards: the Unbound's Bankside detail.
        /// <c>chrono_slasher</c> is the era-agnostic cultist melee standard and
        /// <c>holo_page</c> the Globe ranged standard already authored for Level 10.
        /// No authored elites: 4A's only elite is the scripted Eraser debut.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position)[] ApproachSpawns = {
            ("chrono_slasher", new Vector2(860f, ActorGroundY)),
            ("chrono_slasher", new Vector2(1200f, ActorGroundY)),
            ("holo_page", new Vector2(2680f, ActorGroundY)),
            ("chrono_slasher", new Vector2(3220f, ActorGroundY)),
            ("holo_page", new Vector2(3920f, ActorGroundY)),
            ("chrono_slasher", new Vector2(4480f, ActorGroundY))
        };

        public static int AuthoredStandardCount => ApproachSpawns.Length;

        /// <summary>4A authors no elites of its own; the Eraser is a scripted debut.</summary>
        public static int AuthoredEliteCount => 0;

        public override IReadOnlyList<(string EnemyID, Vector2 Position)> AuthoredStandardSpawns => ApproachSpawns;

        // === Palette: Level 10's London, 1599 (P01 Option A — reuse the era) ===

        protected override Color FloorColor => new(0.30f, 0.21f, 0.12f);
        protected override Color FloorEdgeColor => new(0.55f, 0.41f, 0.22f);
        protected override Color PlatformColor => new(0.38f, 0.27f, 0.16f);
        protected override Color WallColor => new(0.17f, 0.14f, 0.11f);
        protected override Color HazardColor => new(0.72f, 0.30f, 0.16f);

        private static readonly Color UnderstageFloorColor = new(0.19f, 0.14f, 0.10f);
        private static readonly Color UnderstagePlatformColor = new(0.27f, 0.20f, 0.13f);

        // === Runtime handles ===

        /// <summary>
        /// Resolves The Tempest gate. Public so the content suite can pin that the
        /// field special has a recogniser at all — without one the gate is unopenable
        /// and the route is stranded.
        /// </summary>
        public LegacyCastGateWatcher TempestWatcher { get; private set; }

        // === Construction ===

        protected override void BuildNexusGeometry() {
            BuildRoom1BanksideApproach();
            BuildRoom2GlobesYard();
            BuildRoom3BeneathTheStage();
        }

        /// <summary>
        /// Room 1: the Bankside approach at dusk. Ends at the collapsed span — the
        /// boards simply stop, and the only way east is Prospero's Flight.
        /// </summary>
        private void BuildRoom1BanksideApproach() {
            BuildRoomBackground("BG_Room1", Room1StartX, 2560, LevelHeight, new Color(0.11f, 0.08f, 0.06f, 0.4f));
            BuildFloor(Room1StartX, GroundY, SpanGapStartX);
            BuildWall(-20, 0, LevelHeight);

            BuildPlatform(460, 730, 240);
            BuildPlatform(880, 610, 220);
            BuildPlatform(1260, 700, 200);

            // East side of the span: the landing gallery the glide has to reach.
            BuildFloor(SpanGapEndX, GroundY, Room2StartX - SpanGapEndX);

            BuildRoomDecoration(Room1StartX, "legacy_shakespeare_room_bankside", new Color(0.88f, 0.70f, 0.40f));
            BuildRoomTransition("legacy_shakespeare_room_bankside", new Vector2(300, 600), Room1CameraBounds);
        }

        /// <summary>
        /// Room 2: the Globe's yard. The death's-head bell (Special 1), the
        /// thunder-run's storm machine (Special 2), the yard where the Eraser drops
        /// in, and the Nexus Resonance Source at the far end.
        /// </summary>
        private void BuildRoom2GlobesYard() {
            BuildRoomBackground("BG_Room2", Room2StartX, 2560, LevelHeight, new Color(0.10f, 0.08f, 0.07f, 0.45f));
            BuildFloor(Room2StartX, GroundY, 2560);

            BuildPlatform(2780, 700, 220);
            BuildOneWayPlatform(3140, 620, 220);
            BuildPlatform(3560, 700, 240);
            // The storm machine sits high in the tiring-house: The Tempest's lift is
            // the only way the gallery reads as reachable, and the gate is below it.
            BuildPlatform(4020, 560, 220);
            BuildPlatform(4440, 700, 240);

            BuildRoomDecoration(Room2StartX, "legacy_shakespeare_room_yard", new Color(0.92f, 0.78f, 0.45f));
            BuildRoomTransition("legacy_shakespeare_room_yard",
                new Vector2(Room2StartX + 20, 600), Room2CameraBounds);
        }

        /// <summary>
        /// Room 3: beneath the stage. 1,600 px wide so the boss's 8.0 m ranged band
        /// (480 px) and its 3.0 m melee band both have room to matter.
        /// </summary>
        private void BuildRoom3BeneathTheStage() {
            BuildRoomBackground("BG_Room3_Boss", Room3StartX, 1600, LevelHeight, new Color(0.13f, 0.07f, 0.11f, 0.5f));
            BuildFloor(Room3StartX, GroundY, 1600, UnderstageFloorColor);

            BuildPlatform(5600, 690, 240, UnderstagePlatformColor);
            BuildPlatform(6140, 690, 240, UnderstagePlatformColor);
            BuildPlatform(5880, 480, 220, UnderstagePlatformColor);

            BuildWall(6700, 0, LevelHeight);

            BuildRoomDecoration(Room3StartX, "legacy_shakespeare_room_understage", new Color(0.85f, 0.38f, 0.52f));
            BuildRoomTransition("legacy_shakespeare_room_understage",
                new Vector2(Room3StartX + 30, 540), Room3CameraBounds);
        }

        /// <summary>
        /// Attaches The Tempest recogniser. Runs after the sealed <c>BuildLevel</c>
        /// has created the gates, so the handle is live.
        /// </summary>
        protected override void OnLevelReady() {
            base.OnLevelReady();
            if (TempestWatcher != null) return;
            TempestWatcher = new LegacyCastGateWatcher {
                Name = "TempestGateWatcher",
                Gate = SpecialTwoGate,
                RequiredAbilityID = SpecialTwoGateAbilityID,
                Position = SpecialTwoGatePosition
            };
            AddChild(TempestWatcher);
        }

        /// <summary>The boss arena's width, measured for the ranged-band content test.</summary>
        public static float BossArenaWidth => LevelWidth - Room3StartX;
    }
}
