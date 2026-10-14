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
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L12, user
    /// request 3): the Portico (320–3000, wave 1), the Stacks (3020–5900, wave 2)
    /// and the mouth of the Collapsing Hall (6420–7900, wave 3 pre-placed and the
    /// rotunda guard, wave 4, sallying out once the hall's defenders are down). The rotunda
    /// apron west of the arena is only 350 px, narrower than a lock, so the guard
    /// no longer waits in the arena (four of its five stood inside it). The hall
    /// lock clears <b>before</b> the firestorm: the front starts on that clear,
    /// <see cref="FirestormHeadStartPixels"/> behind the hero, and the run from
    /// the lock's exit to the finish line (7900 → 9100, past the shelves and the
    /// third pylon) is the escape. The hero is never shut in a lock with the front
    /// at their back.</para>
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

        // D13 (PROVISIONAL-FONT-PLACEMENT): the level's Restoration Font, after the PreBoss
        // anchor (9180) on the rotunda floor, past the firestorm finish, 190 px short of the
        // arena (9750); a post-wave, post-escape heal (GU-L12: the rotunda guard is now
        // fought in the hall lock, so the font stands outside every lock area).
        protected override Vector2? RestorationFontPosition => new(9560f, FloorY);

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
        /// character, single jump only - cleared about 94 px at Earth gravity when the
        /// tiers were sized, and this level has no gravity mechanic to lift him. The
        /// step was 100 px against his pre-retune 113 px; the 2026-08-10 movement
        /// retune cut jump height about 20% and the tiers came down with it, holding
        /// ~85% of his reach (gameplay-feel plan section 9, B2). Since the 2026-10-08
        /// apex hang he clears about 133 px (StoryAirPhysics), so the steps are easy.
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

        /// <summary>
        /// GU-L12 (<i>Provisional</i>): when the hall lock clears, the front starts
        /// this far behind the hero (2.0 s of its 216 px/s), wherever the hero
        /// stands. Starting it at <see cref="EscapeStartX"/> regardless would hand
        /// a hero who finished the fight at the hall lock's east end a 1,300 px
        /// lead and no escape at all; every hero still outruns it, so only a
        /// detour (the third pylon) costs a catch. FIX-LEVEL (2026-10-13): no
        /// longer floored at <see cref="EscapeStartX"/>, which lies inside the
        /// hall lock (6420): a hero who took the last kill against the entry
        /// barrier got the front on top of them (its zone is the 320 px west of
        /// the front) and the catch knocked them behind it.
        /// </summary>
        public const float FirestormHeadStartPixels = 432f;

        /// <summary>Where the front starts when the hall lock clears with the hero at <paramref name="heroX"/>: the head start behind them, never past the front's end.</summary>
        public static float FirestormStartBehind(float heroX) =>
            Mathf.Min(heroX - FirestormHeadStartPixels, EscapeEndX);

        private EscapeSequenceController _escape;
        public EscapeSequenceController Firestorm => _escape;
        public bool FirestormTriggered { get; private set; }

        /// <summary>
        /// FIX-LEVEL (2026-10-13): latched once the run is won (the firestorm
        /// completed, or a resume past it). The firestorm's own rewind reset
        /// clears <c>IsCompleted</c> before <see cref="OnStoryRewind"/> runs, so
        /// this, not the escape, is what a death rewind reads.
        /// </summary>
        public bool FirestormSurvived { get; private set; }

        private bool _rewindBound;

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

        /// <summary>
        /// P1 (2026-10-04): the rotunda. West edge is <see cref="BossArenaWestX"/>
        /// (spawn 10500 - 750), after the pre-boss beat (9330) and the wave-4
        /// garrison; east edge is the rotunda wall. The T01b historical recovery
        /// anchor (10400) stands inside it.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(BossArenaWestX, 0f, ArenaEndX - BossArenaWestX, LevelHeight);

        /// <summary>
        /// A4 (2026-10-04): the boss's spawn X, the one value the arena's west
        /// edge derives from (the encounter is built at it).
        /// </summary>
        public const float BossSpawnX = 10500f;
        /// <summary>
        /// How far west of <see cref="BossSpawnX"/> the arena starts: the old
        /// 750 px reveal radius (ARENA-WEST-EDGE-AT-OLD-REVEAL-LINE). The
        /// arena rect is the reveal now, so this is the radius's only remaining use.
        /// </summary>
        public const float BossArenaApproachPixels = 750f;
        /// <summary>The arena's west edge (and the reveal line).</summary>
        public const float BossArenaWestX = BossSpawnX - BossArenaApproachPixels;

        private const float StandY = FloorY - 50f;

        /// <summary>
        /// Authored Normal-difficulty spawn table, one manifest row per wave
        /// (<c>level_15.waveN</c>: the same IDs in the same order). Each wave is an
        /// encounter-lock wave (GU-L12): waves 1–3 stand pre-placed and dormant
        /// from the level's build, wave 4 sallies out of the rotunda into the hall
        /// lock. Difficulty scaling takes the same per-row prefix through
        /// <see cref="StoryDifficultyTuning.ScaleEncounterCount"/> the reward
        /// ledger compiles.
        /// </summary>
        public static readonly (string EnemyID, int Wave, Vector2 Position)[] SpawnTable = {
            // Wave 1 - the burning portico.
            (CultistEnemyID, 1, new Vector2(1050f, StandY)),
            (CultistEnemyID, 1, new Vector2(1900f, StandY)),
            (CultistEnemyID, 1, new Vector2(2560f, StandY)),
            // Wave 2 - the stacks and the scriptorium climb, held by a Tech Enforcer.
            // GU-L12: the east trooper moved 5900 -> 5700, inside the Stacks lock.
            (CultistEnemyID, 2, new Vector2(3450f, StandY)),
            (CultistEnemyID, 2, new Vector2(4150f, StandY)),
            (CultistEnemyID, 2, new Vector2(5700f, StandY)),
            (TechEliteEnemyID, 2, new Vector2(4900f, StandY)),
            // Wave 3 - the mouth of the collapsing hall. GU-L12: fought in the hall
            // lock before the firestorm, not on the run (7900 -> 7650, 8750 -> 7250).
            (CultistEnemyID, 3, new Vector2(6850f, StandY)),
            (CultistEnemyID, 3, new Vector2(7650f, StandY)),
            (CultistEnemyID, 3, new Vector2(7250f, StandY)),
            // Wave 4 - the rotunda guard. GU-L12: it no longer waits inside the
            // boss arena (9760-10420); it sallies into the hall lock as a second
            // wave once the hall's defenders are down, so the rotunda is the
            // First Severed's alone.
            (CultistEnemyID, 4, new Vector2(6980f, StandY)),
            (CultistEnemyID, 4, new Vector2(7420f, StandY)),
            (CultistEnemyID, 4, new Vector2(7780f, StandY)),
            (GuardEliteEnemyID, 4, new Vector2(7600f, StandY)),
            // The tail of the wave-4 manifest row: the ledger pays it an elite
            // share (F05), and Easy's prefix (3 of 5) drops it as before.
            (EraserEnemyID, 4, new Vector2(7180f, StandY))
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

        // === GU-L12 (2026-10-12): the encounter locks ===========================

        /// <summary>The F10 encounter IDs of the three locks (they start with the level ID).</summary>
        public const string LockPortico = AlexandriaLevelID + "_lock_portico";
        public const string LockStacks = AlexandriaLevelID + "_lock_stacks";
        public const string LockCollapsingHall = AlexandriaLevelID + "_lock_collapsing_hall";

        /// <summary>The manifest rows the lock waves are (level_15_rewards.tres).</summary>
        public const string Wave1RowID = "level_15.wave1";
        public const string Wave2RowID = "level_15.wave2";
        public const string Wave3RowID = "level_15.wave3";
        public const string Wave4RowID = "level_15.wave4";

        private const float LockBarrierWidth = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>The Portico: everything past the entry anchor (260) to the Stacks seam.</summary>
        public const float PorticoLockMinX = 320f;
        public const float PorticoLockMaxX = Room2StartX;
        /// <summary>
        /// The Stacks and the scriptorium climb, to 220 px short of the Middle
        /// anchor (6120), whose strike surface (6090–6150) stays past the longest
        /// basic swing of a hero pressed against the closed exit (5880 + 156).
        /// </summary>
        public const float StacksLockMinX = Room2StartX + LockBarrierWidth;
        public const float StacksLockMaxX = 5900f;
        /// <summary>
        /// The mouth of the Collapsing Hall: from just inside the hall seam to the
        /// gap between the second and third shelves (7880–8090), leaving the run to
        /// the finish line (9100) as the escape.
        /// </summary>
        public const float CollapsingHallLockMinX = 6420f;
        public const float CollapsingHallLockMaxX = 7900f;

        private EncounterLock _porticoLock;
        private EncounterLock _stacksLock;
        private EncounterLock _collapsingHallLock;

        public EncounterLock PorticoLock => _porticoLock;
        public EncounterLock StacksLock => _stacksLock;
        public EncounterLock CollapsingHallLock => _collapsingHallLock;

        // === Runtime state ===

        private bool _preBossShown;

        public bool PreBossBeatPlayed => _preBossShown;

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
            BuildEncounterLocks();
        }

        /// <summary>
        /// GU-L12: the three approach locks, one wave per manifest row. Built in
        /// <see cref="BuildLevel"/> so a resume past a lock builds it Cleared with
        /// no member ever spawned. The hall lock's clear starts the firestorm.
        /// </summary>
        private void BuildEncounterLocks() {
            _porticoLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockPortico,
                Area = EncounterLockSpec.Span(PorticoLockMinX, PorticoLockMaxX, LevelHeight),
                EntryBarrierX = PorticoLockMinX - LockBarrierWidth * 0.5f,
                ExitBarrierX = PorticoLockMaxX + LockBarrierWidth * 0.5f,
                BarrierFloorY = FloorY,
                Waves = new[] { WaveFromTable(Wave1RowID, 1, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _stacksLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockStacks,
                Area = EncounterLockSpec.Span(StacksLockMinX, StacksLockMaxX, LevelHeight),
                EntryBarrierX = StacksLockMinX - LockBarrierWidth * 0.5f,
                ExitBarrierX = StacksLockMaxX + LockBarrierWidth * 0.5f,
                BarrierFloorY = FloorY,
                Waves = new[] { WaveFromTable(Wave2RowID, 2, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _collapsingHallLock = BuildEncounterLock(new EncounterLockSpec {
                LockID = LockCollapsingHall,
                Area = EncounterLockSpec.Span(CollapsingHallLockMinX, CollapsingHallLockMaxX, LevelHeight),
                EntryBarrierX = CollapsingHallLockMinX - LockBarrierWidth * 0.5f,
                ExitBarrierX = CollapsingHallLockMaxX + LockBarrierWidth * 0.5f,
                BarrierFloorY = FloorY,
                Waves = new[] {
                    WaveFromTable(Wave3RowID, 3, EncounterWaveTrigger.OnBuild),
                    // The rotunda guard sallies once the hall's defenders are down.
                    WaveFromTable(Wave4RowID, 4, EncounterWaveTrigger.AfterPreviousCleared)
                },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _collapsingHallLock.StateChanged += OnCollapsingHallLockStateChanged;
        }

        /// <summary>One lock wave: the authored table's entries for <paramref name="wave"/>, in table order.</summary>
        private static EncounterLockWave WaveFromTable(string rowID, int wave, EncounterWaveTrigger trigger,
            int aliveAtMost = 0) {
            var spawns = new List<EncounterLockSpawn>();
            foreach ((string enemyID, int entryWave, Vector2 position) in SpawnTable) {
                if (entryWave == wave) spawns.Add(new EncounterLockSpawn(enemyID, position));
            }
            return new EncounterLockWave {
                RowID = rowID, Spawns = spawns.ToArray(), Trigger = trigger, AliveAtMost = aliveAtMost
            };
        }

        /// <summary>
        /// The plan's Level 15 ruling: the hall lock clears before the firestorm,
        /// which starts on that clear — <see cref="FirestormHeadStartPixels"/>
        /// behind the hero. A lock rebuilt Cleared by a resume (or the test seam)
        /// starts nothing: a resume past the hall is east of the finish line, and
        /// <see cref="MarkWavesClearedThrough"/> settles the escape there.
        /// </summary>
        private void OnCollapsingHallLockStateChanged(EncounterLock encounterLock, EncounterLockState previous,
            EncounterLockState current) {
            if (current != EncounterLockState.Cleared || encounterLock.RestoredCleared) return;
            float heroX = Player != null && IsInstanceValid(Player) ? Player.GlobalPosition.X : EscapeStartX;
            BeginFirestorm(FirestormStartBehind(heroX));
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
                BossResourcePath, new Vector2(BossSpawnX, FloorY - 50f),
                "ApexEraserEncounter", BossArenaBounds);
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
            BindFirestormRewind();
            ResolvePrimeAnchor();
            BindPhaseEvents();
        }

        /// <summary>
        /// The template's finish trigger sits at its own authored X; retarget it at
        /// this level's finish line and route it into the sequence (the Level 6
        /// pattern).
        /// </summary>
        private void WireFirestormFinishLine() {
            if (_escape == null) return;
            // GU-L12 found that the trigger below, centred on the finish line,
            // meets a walking hero 20–70 px short of FinishX, so
            // NotifyPlayerReachedFinish refuses and BodyEntered never fires again
            // on the way through (the bot idled past the line waiting for it), and
            // fixed it here with a FrontAdvanced hook. FIX-LEVEL (2026-10-13): the
            // escape itself now completes on the first running tick its runner
            // stands past FinishX, the one rule Level 6 shares; the trigger stays
            // as the visible line.
            _escape.Runner = Player;
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

        // GU-L12: the waves are the locks' (BuildEncounterLocks). The room seams
        // post the level's own objective, which the base re-posts when a lock
        // engaged on top of it clears or reverts — the escape objective lands
        // with the firestorm on the hall lock's clear.
        private void EnterStacks() => SetObjective("alexandria_objective_stacks");

        private void EnterCollapsingHall() => SetObjective("alexandria_objective_escape");

        private void EnterRotunda() {
            SetObjective("alexandria_objective_rotunda");
            // The hall is behind the player and the rotunda is stone: stop the front
            // rather than letting it grind against the arena's west wall forever.
            _escape?.Halt();
        }

        private void StartPreBossBeat() {
            if (_preBossShown) return;
            _preBossShown = true;
            // G4: never once the First Severed has fallen.
            StartPreBossDialogue(PreBossDialogueID);
        }

        /// <summary>Starts the firestorm from its authored start line. Idempotent.</summary>
        public bool BeginFirestorm() => BeginFirestorm(EscapeStartX);

        /// <summary>
        /// Starts the firestorm at the player's back, its front at
        /// <paramref name="frontStartX"/> (GU-L12: the hall lock's clear passes
        /// <see cref="FirestormStartBehind"/>). Idempotent.
        /// </summary>
        public bool BeginFirestorm(float frontStartX) {
            if (_escape == null || FirestormTriggered || _escape.IsCompleted) return false;
            FirestormTriggered = true;
            // FIX-LEVEL: only the end clamps; a start west of EscapeStartX is the
            // head start behind a hero who cleared the hall near its entry.
            _escape.StartX = Mathf.Min(frontStartX, EscapeEndX);
            _escape.ResetSequence();
            _escape.Begin();
            return true;
        }

        /// <summary>
        /// FIX-LEVEL (2026-10-13): a death rewind restarts a firestorm the hero
        /// has not yet outrun, the Level 6 pattern. The Firestorm node's rewind
        /// policy is <c>ResetToInitialState</c> (the scene), so its own handler,
        /// subscribed in its <c>_Ready</c> before this one, has already put the
        /// front back at its start and stopped it; this moves that start behind
        /// the landing if the landing is west of it, and runs it again. Before,
        /// the default <c>RestoreCheckpointState</c> parked the front at the
        /// Middle anchor's capture (6500, stopped: no checkpoint can be struck
        /// while it runs) and nothing restarted it, so one death switched the
        /// level's escape off for the rest of the attempt
        /// (VERIFY-L15-ESCAPE-REWIND-HALT). A won run is completed again instead.
        /// </summary>
        private void OnStoryRewind(Vector2 landing) {
            if (_escape == null || !FirestormTriggered) return;
            if (FirestormSurvived) {
                _escape.CompleteEscape();
                return;
            }
            float start = Mathf.Min(_escape.StartX, FirestormStartBehind(landing.X));
            if (!Mathf.IsEqualApprox(start, _escape.StartX)) {
                _escape.StartX = start;
                _escape.ResetSequence();
            }
            _escape.Begin();
        }

        private void OnFirestormCompleted() => FirestormSurvived = true;

        private void BindFirestormRewind() {
            if (_escape != null) _escape.EscapeCompleted += OnFirestormCompleted;
            if (_rewindBound || EventBus.Instance == null) return;
            EventBus.Instance.OnRewindTriggered += OnStoryRewind;
            _rewindBound = true;
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

        // Package 12 W2 (GAP-13) explicit encounter baseline. GU-L12: the
        // encounters are the three locks, and every anchor lists every lock whose
        // area ends before it (a resume builds those Cleared; anything later
        // re-arms in full).
        private static readonly IReadOnlyDictionary<string, string[]> Baselines =
            new Dictionary<string, string[]> {
                [Checkpoint0] = Array.Empty<string>(),
                [Checkpoint1] = new[] { LockPortico, LockStacks },
                [Checkpoint2] = new[] { LockPortico, LockStacks, LockCollapsingHall }
            };

        protected override IReadOnlyDictionary<string, string[]> EncounterBaselineMap => Baselines;

        /// <summary>
        /// Resolved after <see cref="MarkWavesClearedThrough"/>, because the escape
        /// node is a scene child and is only reachable once the level is in the tree.
        /// </summary>
        private void SettleFirestormForResume() {
            if (_escape == null || !FirestormTriggered) return;
            FirestormSurvived = true;
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
            if (_rewindBound && EventBus.Instance != null) {
                EventBus.Instance.OnRewindTriggered -= OnStoryRewind; // FIX-LEVEL
            }
            _rewindBound = false;
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
    }
}
