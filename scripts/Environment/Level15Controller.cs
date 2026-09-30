using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Level 15 - the Library of Alexandria Restoration. <b>The final level.</b>
    ///
    /// The player returns to the moment of the original cataclysm: the same Library
    /// the game opens on, burning, at the instant the Unbound's overload
    /// fractured history. This is the only level in the campaign that is a
    /// <i>return</i> - the geography is meant to be recognised - and the era
    /// identity is that the place is dying while you cross it. Cyclic fire columns
    /// bloom through all three approach rooms and the Collapsing Hall runs an
    /// <see cref="EscapeSequenceController"/> firestorm at the player's back.
    ///
    /// Encounter economy is locked by docs/DUST_ECONOMY.md: 12 standards, 2 elites,
    /// 1 boss, 3 extractors. Act III is Unbound-only (plan section 2.4), so the
    /// garrison is <see cref="CultistEnemyID"/> with one
    /// <see cref="TechEliteEnemyID"/> and one <see cref="GuardEliteEnemyID"/>.
    ///
    /// <para><b>The ending chain, and why it is ordered this way.</b> Defeating the
    /// Apex Eraser is not the end. design-godot.md 3389 gates the ending cinematic
    /// on the boss dying <i>and</i> the Temporal Core being inserted into the
    /// Alexandria Anchor, and 2772-2778 replaces the ordinary results/hub return
    /// with credits, the campaign-completion save write, and the Main Menu. The
    /// base class's hooks are used exactly as Wave B integration intended:</para>
    ///
    /// <list type="number">
    /// <item><b>Boss defeated.</b> <c>OnBossDefeated</c> is NOT overridden, so the
    ///       base keeps <c>IsBossDefeated</c>, the completion objective, and the
    ///       boss dust tally (the Level 8 mistake).</item>
    /// <item><b>The anchor arms.</b> <see cref="StartPostBossSequence"/> calls
    ///       <see cref="ArmTemporalCore"/> first, so the gate opens on the defeat
    ///       itself and cannot be lost to a dialogue that fails to start.</item>
    /// <item><b><c>level_15.postboss</c></b> - the one
    ///       <see cref="PostBossDialogueIDs"/> beat: Sarah telling the player to
    ///       deposit the Core. The <i>ending</i> deliberately does not live here,
    ///       because this chain runs the instant the boss dies and the ending must
    ///       wait for the player.</item>
    /// <item><b>Core insertion.</b> <see cref="StartExitSequence"/> is overridden to
    ///       post the restoration objective and stop; there is no
    ///       <c>level_15.exit</c> sequence and no automatic completion. The only way
    ///       on is <see cref="TemporalCoreAnchor"/>, a real player interaction at
    ///       the Prime Anchor, which calls <c>ShowCompletionResults()</c>.</item>
    /// <item><b><c>level_15.ending</c> -> credits -> <c>IsCompleted</c> -> Main
    ///       Menu.</b> <see cref="PresentCompletion"/> hands the whole tail to
    ///       <see cref="CampaignCompletionSequence"/> and returns null. The ending
    ///       beat is played by that sequence (its <c>endingDialogueID</c> route),
    ///       never by <see cref="PostBossDialogueIDs"/> - exactly one owner, so it
    ///       cannot play twice.</item>
    /// </list>
    ///
    /// <para><b>A quit between the boss and the Core is not a dead end.</b>
    /// Package 12 W8 (N01, D12(a)) retired the Package 11 "the defeat is never
    /// persisted" rule: the base commits AwaitingSeal into the attempt record at
    /// the defeat, with <see cref="SealingAnchorID"/> = <see cref="PrimeAnchorID"/>,
    /// and a load before the Core goes in rebuilds the Eraser as defeated (never
    /// respawned), re-arms the Prime Anchor and respawns an uncollected boss
    /// pickup once. The Prime Anchor stays this level's own scene-authored anchor
    /// (<see cref="UsesGenericSealingAnchor"/> is false).</para>
    /// </summary>
    public partial class Level15Controller : StoryLevelControllerBase {

        // === Identity ===

        public const string AlexandriaLevelID = "level_15_alexandria";

        public override string LevelID => AlexandriaLevelID;
        public override CampaignLevel Level => CampaignLevel.Alexandria;
        
        /// <summary>
        /// V7.6 F01 par (Package 11 A3). <b>Provisional, not measured</b>:
        /// seeded as 6 authored rooms x 90 s, rounded up to the nearest
        /// 30 s. Recorded under VERIFY-PAR-SECONDS in
        /// docs/design-contracts/DESIGN_BUILD_DEVIATIONS.md pending the V01a
        /// per-hero median measurement pass.
        /// </summary>
        public override float ParSeconds => 540f;
        public override string LevelTitleKey => "alexandria_level_title";
        public override string DialogueSetPath => "res://resources/Dialogue/level_15_dialogue.tres";
        public override Vector2 PlayerSpawnPosition => new(220f, FloorY - 60f);

        // Package 12 W8 (GAP-03): the secret cache sits on
        // the Stacks mezzanine rung (one-way 4790, top 1150) — mid-climb to the
        // scriptorium, off the floor route and far west of the firestorm escape corridor.
        protected override Vector2? SecretCachePosition => new(4790f, 1055f);
        public override Rect2 LevelBounds => new(0, 0, LevelWidth, LevelHeight);

        /// <summary>Sarah at the rotunda door, before the Eraser reveals.</summary>
        public string PreBossDialogueID => $"{DialoguePrefix}.preboss";

        /// <summary>
        /// The authored ending script (design-godot.md 3389-3399). Played by
        /// <see cref="CampaignCompletionSequence"/> after the Core goes in - never
        /// as a post-boss beat.
        /// </summary>
        public string EndingDialogueID => $"{DialoguePrefix}.ending";

        /// <summary>
        /// There is no exit beat and no hub return: the campaign ends here. Blank so
        /// the base's "exit dialogue completed -> show results" route can never fire.
        /// </summary>
        public override string ExitDialogueID => "";

        protected override string InitialObjectiveKey => "alexandria_objective_reach_anchor";
        protected override string BossObjectiveKey => "alexandria_objective_defeat_boss";
        /// <summary>Posted the moment the Eraser dies: the restoration is what is left.</summary>
        protected override string CompletionObjectiveKey => "alexandria_objective_restore";

        /// <summary>Objective once the Core is in and the ending is rolling.</summary>
        public const string RestoredObjectiveKey = "alexandria_objective_complete";

        private static readonly string[] PostBossBeats = { "level_15.postboss" };
        protected override IReadOnlyList<string> PostBossDialogueIDs => PostBossBeats;

        // === Dimensions ===

        public const float LevelWidth = 11600f;
        public const float LevelHeight = 1600f;

        /// <summary>The great hall floor. One continuous grade - no pits in a library.</summary>
        public const float FloorY = 1400f;

        /// <summary>
        /// Gallery altitudes. Every step is 80 px because Lincoln - the heaviest
        /// character, single jump only - clears about 94 px at Earth gravity, and this
        /// level has no gravity mechanic to lift him. The step was 100 px against his
        /// pre-retune 113 px; the 2026-08-10 movement retune cut jump height about 20%
        /// and the tiers came down with it, holding the same ~85% of his reach
        /// (gameplay-feel plan section 9, B2).
        /// </summary>
        public const float Tier1Y = 1320f;
        public const float Tier2Y = 1240f;
        public const float Tier3Y = 1160f;
        public const float Tier4Y = 1080f;
        /// <summary>The scriptorium balcony: the top of the level's vertical section.</summary>
        public const float Tier5Y = 1000f;

        public const float Room2StartX = 3000f;
        public const float Room3StartX = 6400f;
        /// <summary>West edge of the Prime Anchor rotunda - the boss arena seam.</summary>
        public const float ArenaStartX = 9400f;
        /// <summary>East wall of the rotunda.</summary>
        public const float ArenaEndX = 11400f;

        // === Checkpoints ===

        public const string Checkpoint0 = AlexandriaLevelID + "_checkpoint_0";
        public const string Checkpoint1 = AlexandriaLevelID + "_checkpoint_1";
        public const string Checkpoint2 = AlexandriaLevelID + "_checkpoint_2";

        public static readonly string[] CheckpointIDs = { Checkpoint0, Checkpoint1, Checkpoint2 };

        private static readonly Vector2 Checkpoint0Position = new(260f, FloorY - 10f);
        private static readonly Vector2 Checkpoint1Position = new(6120f, FloorY - 10f);
        /// <summary>
        /// Pre-boss anchor, east of the firestorm's finish line so a resume is never
        /// dropped back into a run it already survived - and west of the rotunda, so
        /// a resume after the boss can reach the Eraser and the anchor again.
        /// </summary>
        private static readonly Vector2 Checkpoint2Position = new(9180f, FloorY - 10f);

        // === The firestorm (era identity: the library dies while you cross it) ===

        public const string EscapeSequenceID = "level_15.firestorm";
        public const float EscapeStartX = 6500f;
        public const float EscapeEndX = 9000f;
        public const float EscapeFinishX = 9100f;

        private EscapeSequenceController _escape;
        public EscapeSequenceController Firestorm => _escape;
        public bool FirestormTriggered { get; private set; }

        // === The Prime Anchor ===

        public const string PrimeAnchorID = "level_15.prime_anchor";
        public const string PrimeAnchorNodeName = "PrimeAnchor";
        public static readonly Vector2 PrimeAnchorPosition = new(11150f, FloorY);

        private TemporalCoreAnchor _anchor;
        public TemporalCoreAnchor PrimeAnchor => _anchor;

        /// <summary>Package 12 W8 (N01): the Prime Anchor is this level's sealing anchor.</summary>
        public override string SealingAnchorID => PrimeAnchorID;

        /// <summary>Package 12 W8: the scene-authored Prime Anchor, not a generic one.</summary>
        protected override bool UsesGenericSealingAnchor => false;

        /// <summary>The completion chain, once the Core is in. Null before that.</summary>
        public CampaignCompletionSequence Completion { get; private set; }

        /// <summary>
        /// Production default. A test sets this false so the chain can run to its
        /// end inside the GdUnit runner tree without <c>ChangeSceneToPacked</c>
        /// pulling the runner's own scene out from under it - the same seam
        /// <c>CampaignCompletionTests</c> uses.
        /// </summary>
        public bool ReturnToMainMenuOnCompletion { get; set; } = true;

        public bool TemporalCoreInserted => _anchor != null && IsInstanceValid(_anchor) && _anchor.IsInserted;

        // === Encounters (locked: 12 standards / 2 elites / 1 boss / 3 extractors) ===

        /// <summary>Act III is Unbound-only (plan section 2.4).</summary>
        public const string CultistEnemyID = "chrono_slasher";
        public const string TechEliteEnemyID = "tech_enforcer";
        public const string GuardEliteEnemyID = "chrono_guard_elite";
        /// <summary>
        /// V7.6 (Package 11 A7a): the Eraser's last salted appearance, on the
        /// rotunda guard. It is the correct enemy to meet immediately before the
        /// First Unbound: the hunter that has chased the hero since 4A, standing
        /// between them and the Prime Anchor with the meter they need for it.
        /// </summary>
        public const string EraserEnemyID = "unbound_eraser";
        public const string BossResourcePath = "res://resources/Bosses/apex_eraser.tres";

        private const float StandY = FloorY - 50f;

        /// <summary>
        /// Authored Normal-difficulty spawn table. Wave 1 is live on entry; waves
        /// 2-4 arm on their room seams. Difficulty scaling takes a prefix of each
        /// wave through <see cref="StoryDifficultyTuning.ScaleEncounterCount"/>.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - the burning portico.
            (CultistEnemyID, 1, new Vector2(1050f, StandY)),
            (CultistEnemyID, 1, new Vector2(1900f, StandY)),
            (CultistEnemyID, 1, new Vector2(2560f, StandY)),
            // Wave 2 - the stacks and the scriptorium climb, held by a Tech Enforcer.
            (CultistEnemyID, 2, new Vector2(3450f, StandY)),
            (CultistEnemyID, 2, new Vector2(4150f, StandY)),
            (CultistEnemyID, 2, new Vector2(5900f, StandY)),
            (TechEliteEnemyID, 2, new Vector2(4900f, StandY)),
            // Wave 3 - the collapsing hall, fought on the run.
            (CultistEnemyID, 3, new Vector2(6850f, StandY)),
            (CultistEnemyID, 3, new Vector2(7900f, StandY)),
            (CultistEnemyID, 3, new Vector2(8750f, StandY)),
            // Wave 4 - the rotunda guard, before the Eraser reveals.
            (CultistEnemyID, 4, new Vector2(9620f, StandY)),
            (CultistEnemyID, 4, new Vector2(9880f, StandY)),
            (CultistEnemyID, 4, new Vector2(10150f, StandY)),
            (GuardEliteEnemyID, 4, new Vector2(9760f, StandY)),
            (EraserEnemyID, 4, new Vector2(10420f, StandY))
        };

        /// <summary>
        /// V7.6 Resonance Hold (Package 11 A3b): Level 15's drain stand-ins are
        /// the firing channel's <b>anchor pylons</b>, holding the Prime Anchor's
        /// beam open. Mechanically an Extractor in every respect; the array name
        /// is retained for the content tests and the dust ledger.
        /// </summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_15_extractor_0", new Vector2(1740f, Tier3Y - 60f)),
            ("level_15_extractor_1", new Vector2(5620f, Tier5Y - 60f)),
            // Deliberately on the firestorm's route: a detour paid for in seconds.
            ("level_15_extractor_2", new Vector2(8220f, Tier2Y - 60f))
        };

        public static int StandardEnemyCount => CountOf(CultistEnemyID);
        public static int EliteEnemyCount =>
            CountOf(TechEliteEnemyID) + CountOf(GuardEliteEnemyID) + CountOf(EraserEnemyID);

        /// <summary>How many Erasers this level authors. Test seam for the salt rules.</summary>
        public static int EraserCount => CountOf(EraserEnemyID);

        private static int CountOf(string enemyID) {
            int total = 0;
            foreach ((string id, int _, Vector2 _) in SpawnTable) if (id == enemyID) total++;
            return total;
        }

        // === Authored galleries (one-way rungs; every step is a single jump) ===

        public static readonly (float X, float Y, float Width)[] PorticoStair = {
            (900f, Tier1Y, 240f), (1180f, Tier2Y, 220f), (1460f, Tier3Y, 220f), (1740f, Tier3Y, 260f)
        };

        /// <summary>The vertical section: five rungs from the hall floor to the balcony.</summary>
        public static readonly (float X, float Y, float Width)[] ScriptoriumClimb = {
            (4250f, Tier1Y, 240f), (4520f, Tier2Y, 220f), (4790f, Tier3Y, 220f),
            (5060f, Tier4Y, 220f), (5330f, Tier5Y, 240f), (5620f, Tier5Y, 280f)
        };

        public static readonly (float X, float Y, float Width)[] HallShelves = {
            (7500f, Tier1Y, 220f), (7760f, Tier2Y, 240f), (8220f, Tier2Y, 260f)
        };

        public static readonly (string ID, (float X, float Y, float Width)[] Rungs)[] Galleries = {
            ("portico_stair", PorticoStair),
            ("scriptorium_climb", ScriptoriumClimb),
            ("hall_shelves", HallShelves)
        };

        // === Runtime state ===

        private readonly HashSet<int> _wavesSpawned = new();
        private bool _preBossShown;

        public bool PreBossBeatPlayed => _preBossShown;
        public bool HasWaveSpawned(int wave) => _wavesSpawned.Contains(wave);

        // === Era palette: scorched marble, papyrus ash, firelight ===

        protected override Color FloorColor => new(0.24f, 0.2f, 0.17f);
        protected override Color FloorEdgeColor => new(0.72f, 0.46f, 0.24f);
        protected override Color PlatformColor => new(0.34f, 0.28f, 0.22f);
        protected override Color WallColor => new(0.17f, 0.14f, 0.12f);
        protected override Color HazardColor => new(0.85f, 0.35f, 0.12f);

        private static readonly Color RotundaColor = new(0.28f, 0.26f, 0.32f);

        // === Construction ===

        protected override void BuildLevel() {
            BuildRoom1Portico();
            BuildRoom2Stacks();
            BuildRoom3CollapsingHall();
            BuildRoom4Rotunda();
            BuildResonanceHoldNodes(ResonanceHoldVariant.AnchorPylon, ExtractorPlacements);

            BuildWall(0f, 0f, LevelHeight);
            BuildWall(ArenaEndX, 0f, LevelHeight);
        }

        private void BuildGallery((float X, float Y, float Width)[] rungs) {
            foreach ((float x, float y, float width) in rungs) BuildOneWayPlatform(x, y, width);
        }

        private void BuildRoom1Portico() {
            BuildRoomBackground("BG_Portico", 0f, Room2StartX, LevelHeight,
                new Color(0.12f, 0.06f, 0.04f, 0.55f));
            BuildFloor(0f, FloorY, Room2StartX);
            BuildGallery(PorticoStair);

            BuildCheckpoint(Checkpoint0Position.X, Checkpoint0Position.Y, Checkpoint0, CheckpointRole.Entry);
            BuildRoomDecoration(0f, "alexandria_room_portico", new Color(0.95f, 0.72f, 0.42f));
        }

        private void BuildRoom2Stacks() {
            float width = Room3StartX - Room2StartX;
            BuildRoomBackground("BG_Stacks", Room2StartX, width, LevelHeight,
                new Color(0.13f, 0.07f, 0.05f, 0.55f));
            BuildFloor(Room2StartX, FloorY, width);
            BuildGallery(ScriptoriumClimb);

            BuildCheckpoint(Checkpoint1Position.X, Checkpoint1Position.Y, Checkpoint1, CheckpointRole.Middle);
            BuildRoomDecoration(Room2StartX, "alexandria_room_stacks", new Color(0.92f, 0.66f, 0.36f));

            BuildRoomTransitionPair(
                Room2StartX, "alexandria_stacks", "alexandria_portico_return",
                new Rect2(Room2StartX - 100f, 0f, 3600f, LevelHeight),
                new Rect2(0f, 0f, Room2StartX + 100f, LevelHeight),
                _ => EnterStacks());
        }

        private void BuildRoom3CollapsingHall() {
            float width = ArenaStartX - Room3StartX;
            BuildRoomBackground("BG_CollapsingHall", Room3StartX, width, LevelHeight,
                new Color(0.16f, 0.06f, 0.04f, 0.6f));
            BuildFloor(Room3StartX, FloorY, width);
            BuildGallery(HallShelves);

            BuildCheckpoint(Checkpoint2Position.X, Checkpoint2Position.Y, Checkpoint2, CheckpointRole.PreBoss);
            BuildRoomDecoration(Room3StartX, "alexandria_room_collapse", new Color(0.98f, 0.5f, 0.24f));

            BuildRoomTransitionPair(
                Room3StartX, "alexandria_collapsing_hall", "alexandria_stacks_return",
                new Rect2(Room3StartX - 100f, 0f, 3200f, LevelHeight),
                new Rect2(Room2StartX - 100f, 0f, 3600f, LevelHeight),
                _ => EnterCollapsingHall());

            // Sarah's last briefing lands between checkpoint 2 and the rotunda, so a
            // resume at checkpoint 2 deliberately leaves it armed.
            BuildWaveTrigger("PreBossTrigger", new Vector2(9330f, FloorY - 300f),
                StartPreBossBeat, new Vector2(70f, 900f));
        }

        private void BuildRoom4Rotunda() {
            float width = LevelWidth - ArenaStartX;
            BuildRoomBackground("BG_Rotunda", ArenaStartX, width, LevelHeight,
                new Color(0.08f, 0.07f, 0.13f, 0.6f));

            // One continuous rotunda floor, walled east: the Eraser's blink and its
            // collapse pulses can throw the player around the room but never off it.
            BuildFloor(ArenaStartX, FloorY, ArenaEndX - ArenaStartX, RotundaColor);
            BuildPlatform(9900f, FloorY - 170f, 280f, RotundaColor);
            BuildPlatform(10900f, FloorY - 170f, 280f, RotundaColor);

            BuildRoomDecoration(ArenaStartX, "alexandria_room_rotunda", new Color(0.6f, 0.85f, 0.98f));

            // One-shot: this clamp is the arena lock.
            BuildRoomTransition("alexandria_rotunda", new Vector2(ArenaStartX + 60f, LevelHeight / 2f),
                new Rect2(ArenaStartX - 40f, 0f, LevelWidth - ArenaStartX + 40f, LevelHeight),
                new Vector2(80f, LevelHeight),
                _ => EnterRotunda());

            // The rotunda floor spans 2,000 px against the Eraser's 10.0-unit ranged
            // band (600 px at BossController's 60 px per unit); the content test
            // asserts the fit against the resource, not against a hardcoded width.
            BossEncounterController encounter = BuildBossEncounter(
                BossResourcePath, new Vector2(10500f, FloorY - 50f),
                "ApexEraserEncounter", revealDistance: 750f);
            BuildHistoricalRecoveryAnchor(encounter);
        }

        /// <summary>
        /// Package 12 W1 (GAP-06): the T01b destination chain's third tier. The
        /// arena must guarantee a valid fallback, so the anchor sits on the open
        /// rotunda floor at centre stage — clear of both side platforms, on
        /// support, at the boss's own standing height.
        /// </summary>
        public static readonly Vector2 HistoricalRecoveryAnchorPosition = new(10400f, FloorY - 50f);

        /// <summary>The authored anchor node. Test seam.</summary>
        public Node2D HistoricalRecoveryAnchor { get; private set; }

        private void BuildHistoricalRecoveryAnchor(BossEncounterController encounter) {
            HistoricalRecoveryAnchor = new Node2D {
                Name = "HistoricalRecoveryAnchor",
                Position = HistoricalRecoveryAnchorPosition
            };
            AddChild(HistoricalRecoveryAnchor);
            if (encounter == null) return;
            Node2D anchor = HistoricalRecoveryAnchor;
            void Assign() {
                if (encounter.Boss != null && IsInstanceValid(encounter.Boss)) {
                    encounter.Boss.HistoricalRecoveryAnchor = anchor;
                }
            }
            Assign();
            // The encounter spawns its boss in its own _Ready; if that has not
            // run yet, bind as soon as it does.
            if (encounter.Boss == null) encounter.Ready += Assign;
        }

        /// <summary>
        /// Bidirectional confinement (the Level 3 pattern): a single forward trigger
        /// leaves the camera clamped to the last room forever once the player
        /// backtracks for an extractor.
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

        // === Flow ===

        protected override void OnLevelReady() {
            _escape = GetNodeOrNull<EscapeSequenceController>("Firestorm");
            WireFirestormFinishLine();
            SettleFirestormForResume();
            ResolvePrimeAnchor();
            BindPhaseEvents();
        }

        /// <summary>
        /// The template's finish trigger sits at its own authored X; retarget it at
        /// this level's finish line and route it into the sequence (the Level 6
        /// pattern - <see cref="EscapeSequenceController"/> only self-detects the
        /// finish while the player is inside the damage front).
        /// </summary>
        private void WireFirestormFinishLine() {
            if (_escape == null) return;
            if (_escape.GetNodeOrNull<Area2D>("FinishTrigger") is not Area2D finish) return;
            finish.Position = new Vector2(EscapeFinishX, -360f);
            finish.BodyEntered += body => {
                if (body is PlayerController player) _escape.NotifyPlayerReachedFinish(player);
            };
        }

        private void ResolvePrimeAnchor() {
            _anchor = GetNodeOrNull<TemporalCoreAnchor>(PrimeAnchorNodeName);
            if (_anchor == null) {
                GD.PushError($"Level 15 is missing its '{PrimeAnchorNodeName}' TemporalCoreAnchor; " +
                             "the campaign could not be finished.");
                return;
            }
            _anchor.CoreInserted += OnTemporalCoreInserted;
            // A resume after the boss (see the class remarks: nothing persists the
            // defeat) finds the Eraser alive, so the anchor stays dormant here.
            if (IsBossDefeated) ArmTemporalCore();
        }

        protected override void SpawnInitialEnemies() => SpawnWave(1);

        private void EnterStacks() {
            SetObjective("alexandria_objective_stacks");
            SpawnWave(2);
        }

        private void EnterCollapsingHall() {
            SetObjective("alexandria_objective_escape");
            SpawnWave(3);
            BeginFirestorm();
        }

        private void EnterRotunda() {
            SetObjective("alexandria_objective_rotunda");
            SpawnWave(4);
            // The hall is behind the player and the rotunda is stone: stop the front
            // rather than letting it grind against the arena's west wall forever.
            _escape?.Halt();
        }

        private void StartPreBossBeat() {
            if (_preBossShown) return;
            _preBossShown = true;
            StartDialogue(PreBossDialogueID);
        }

        /// <summary>Starts the firestorm at the player's back. Idempotent.</summary>
        public bool BeginFirestorm() {
            if (_escape == null || FirestormTriggered || _escape.IsCompleted) return false;
            FirestormTriggered = true;
            _escape.Begin();
            return true;
        }

        // === Checkpoint resume ===

        /// <summary>The anchor's non-encounter world state; the waves come from the baseline.</summary>
        protected override void MarkWavesClearedThrough(string checkpointID) {
            if (checkpointID == Checkpoint2) {
                // Checkpoint 2 is east of the finish line, so the run is already
                // survived. Re-arming it would trap a resumed player between a
                // fresh front and the rotunda door.
                FirestormTriggered = true;
            }
        }

        // Package 12 W2 (GAP-13): explicit encounter baseline — waves 1+2 at the
        // middle anchor, 1+2+3 at the PreBoss anchor, authored rather than derived.
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            NumberedWaveBaselines(AlexandriaLevelID, new[] { 1, 2 }, new[] { 1, 2, 3 });

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        protected override void MarkEncounterCleared(string encounterID) {
            if (TryParseWaveEncounter(AlexandriaLevelID, encounterID, out int wave)) _wavesSpawned.Add(wave);
        }

        /// <summary>
        /// Resolved after <see cref="MarkWavesClearedThrough"/>, because the escape
        /// node is a scene child and is only reachable once the level is in the tree.
        /// </summary>
        private void SettleFirestormForResume() {
            if (_escape == null || !FirestormTriggered) return;
            _escape.ResetSequence();
            _escape.CompleteEscape();
        }

        // === The restoration ===

        /// <summary>
        /// Opens the Prime Anchor. Called from <see cref="StartPostBossSequence"/>
        /// on the defeat itself rather than at the end of the beat chain: a beat that
        /// fails to start must never be able to lock the player out of the ending.
        /// </summary>
        public bool ArmTemporalCore() =>
            _anchor != null && IsInstanceValid(_anchor) && _anchor.Arm();

        protected override void StartPostBossSequence() {
            ArmTemporalCore();
            base.StartPostBossSequence();
        }

        /// <summary>
        /// Level 15 has no exit beat and no hub return. The chain stops here and
        /// waits for the player to restore the anchor; <see cref="ShowCompletionResults"/>
        /// is reached only through <see cref="OnTemporalCoreInserted"/>.
        /// </summary>
        protected override void StartExitSequence() => SetObjective(CompletionObjectiveKey);

        private void OnTemporalCoreInserted(string anchorID) {
            SetObjective(RestoredObjectiveKey);
            ShowCompletionResults();
        }

        /// <summary>
        /// The campaign ending, in place of the hub results overlay:
        /// <c>level_15.ending</c> -> credits (skippable) -> <c>IsCompleted</c> ->
        /// Main Menu. The base has already set <c>LevelComplete</c> and raised
        /// <c>OnLevelComplete</c>, so this returns null.
        /// </summary>
        protected override LevelResultsPanel PresentCompletion() {
            // Package 13 W3 (S39): the hero gives the charge back at the Founding
            // and comes aboard mortal — the aura goes with the release, and the
            // chain carries on into the Time-Ship send-off and, after the credits,
            // the once-only Homecoming (S43).
            Player?.GetNodeOrNull<FTT.Combat.GlowPresentationController>(
                FTT.Combat.GlowPresentationController.NodeName)?.SetHeroAura(false);
            Completion = CampaignCompletionSequence.Begin(
                this, Services?.Dialogue, SelectedEndingDialogueID, ReturnToMainMenuOnCompletion,
                CampaignCompletionSequence.SendOffSequenceID, CampaignCompletionSequence.HomecomingSequenceID);
            return null;
        }

        // === Package 13 W3 region: the phase-3 bark (S47) ====================

        /// <summary>The non-blocking bark as the Borrowed Legacies phase begins.</summary>
        public const string PhaseThreeBarkDialogueID = "level_15.phase3_bark";

        /// <summary>True once the bark has played this level entry. Test seam.</summary>
        public bool PhaseThreeBarkPlayed { get; private set; }

        private bool _phaseEventsBound;

        /// <summary>
        /// Sarah names what the First Severed is doing as its final (Borrowed
        /// Legacies, 0-based phase 2) phase begins: "He's drawing on them — the
        /// captives!" Non-blocking, once per entry.
        /// </summary>
        public bool PlayPhaseThreeBark() {
            if (PhaseThreeBarkPlayed || IsBossDefeated) return false;
            PhaseThreeBarkPlayed = true;
            return StartDialogue(PhaseThreeBarkDialogueID);
        }

        private void OnBossPhaseChanged(int phaseIndex) {
            if (phaseIndex >= 2) PlayPhaseThreeBark();
        }

        private void BindPhaseEvents() {
            if (_phaseEventsBound || EventBus.Instance == null) return;
            EventBus.Instance.OnBossPhaseChanged += OnBossPhaseChanged;
            _phaseEventsBound = true;
        }

        public override void _ExitTree() {
            if (_phaseEventsBound && EventBus.Instance != null) {
                EventBus.Instance.OnBossPhaseChanged -= OnBossPhaseChanged;
            }
            _phaseEventsBound = false;
            base._ExitTree();
        }

        // === end Package 13 W3 region ===

        // === Package 11 A3b region: N05 ending selection ====================

        /// <summary>The scarred variant, authored by A6 as a full second sequence.</summary>
        public string ScarredEndingDialogueID => $"{DialoguePrefix}.ending_scarred";

        /// <summary>
        /// N05 (V7.6): which of the two authored endings this campaign earned.
        ///
        /// <para>An <b>equal-weight average of exactly fourteen timed levels</b> —
        /// Levels 2–15, with no hero dependence (S27 retired Level 4A; any
        /// <c>level_04a_*</c> record a v7 save still carries is dead data).
        /// Untimed Levels 0 and 1 are excluded; length, authored par, difficulty
        /// multiplier, optional dust and era size weight nothing. Each
        /// contribution is that level's PreBoss-<i>locked</i> final Integrity at
        /// full stored precision.</para>
        ///
        /// <para>The comparison is the <b>unrounded sum against 700 percentage
        /// points</b> (50% across fourteen levels), never a rounded average or a
        /// per-level rounding: display rounding cannot change which ending plays,
        /// so a run that would render as "50%" but sums below 700 gets the
        /// scarred ending. Exactly 700 selects the clean restoration. Identical
        /// on every difficulty.</para>
        ///
        /// <para>Resolved here, inside the sealing transaction and before any
        /// presentation starts, so a crash during the ending resumes the same
        /// committed choice.</para>
        /// </summary>
        public string SelectedEndingDialogueID {
            get {
                StorySaveData save = ResolveActiveSave();
                if (save == null) return EndingDialogueID;
                return StoryManager.IsCleanRestorationEnding(save)
                    ? EndingDialogueID
                    : ScarredEndingDialogueID;
            }
        }

        private static StorySaveData ResolveActiveSave() {
            if (SaveManager.Instance == null || GameManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length
                ? SaveManager.Instance.SaveSlots[slot]
                : null;
        }

        // === end Package 11 A3b region ===

        // === Encounters ===

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
