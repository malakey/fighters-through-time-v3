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
    /// <para><b>A quit between the boss and the Core is not a dead end.</b> Nothing
    /// persists "the Eraser is dead": the anchor's armed state is derived from this
    /// run's encounter, and the last autosave is checkpoint 2, west of the rotunda.
    /// A resumed run therefore finds the Eraser alive, fights it again, and reaches
    /// the same ending. The ending is never gated on a flag the save did not
    /// keep.</para>
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
            (GuardEliteEnemyID, 4, new Vector2(9760f, StandY))
        };

        /// <summary>Three extractors, each behind a real detour. 15 dust each, resource-owned.</summary>
        public static readonly (string ID, Vector2 Position)[] ExtractorPlacements = {
            ("level_15_extractor_0", new Vector2(1740f, Tier3Y - 60f)),
            ("level_15_extractor_1", new Vector2(5620f, Tier5Y - 60f)),
            // Deliberately on the firestorm's route: a detour paid for in seconds.
            ("level_15_extractor_2", new Vector2(8220f, Tier2Y - 60f))
        };

        public static int StandardEnemyCount => CountOf(CultistEnemyID);
        public static int EliteEnemyCount => CountOf(TechEliteEnemyID) + CountOf(GuardEliteEnemyID);

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
            BuildExtractors(ExtractorPlacements);

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
            BuildBossEncounter(BossResourcePath, new Vector2(10500f, FloorY - 50f),
                "ApexEraserEncounter", revealDistance: 750f);
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

        protected override void MarkWavesClearedThrough(string checkpointID) {
            switch (checkpointID) {
                case Checkpoint1:
                    _wavesSpawned.Add(1);
                    _wavesSpawned.Add(2);
                    break;
                case Checkpoint2:
                    _wavesSpawned.Add(1);
                    _wavesSpawned.Add(2);
                    _wavesSpawned.Add(3);
                    // Checkpoint 2 is east of the finish line, so the run is already
                    // survived. Re-arming it would trap a resumed player between a
                    // fresh front and the rotunda door.
                    FirestormTriggered = true;
                    break;
            }
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
            Completion = CampaignCompletionSequence.Begin(
                this, Services?.Dialogue, EndingDialogueID, ReturnToMainMenuOnCompletion);
            return null;
        }

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
