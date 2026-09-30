using Godot;
using System.Collections.Generic;

namespace FTT.Core {

    public enum CampaignLevel {
        Tutorial = 0,
        Florence = 1,
        Orleans = 2,
        Chicago = 3,
        Paris = 4,
        Titanic = 5,
        Pompeii = 6,
        Nassau = 7,
        Egypt = 8,
        Berlin = 9,
        London = 10,
        Gettysburg = 11,
        Lunar = 12,
        ChronalVoid = 13,
        NeoEarth = 14,
        Alexandria = 15,

        /// <summary>
        /// V7.6 Level 4A — the per-character Legacy Level (Package 11 §2.4).
        /// Appended at 16 so <b>nothing renumbers</b>: the enum value is a stable
        /// identity, never a position in the campaign. Its place in the run is
        /// carried by <see cref="StoryManager.CampaignRoute"/>, which orders
        /// 0,1,2,3,4,<b>16</b>,5…15, and its scene path resolves per hero
        /// (<c>Level_04A_&lt;hero&gt;.tscn</c>) — the one campaign slot whose
        /// route depends on the locked character.
        /// </summary>
        LegacyNexus = 16
    }

    /// <summary>
    /// Package 11 A3 (F01): the scopes that may hold the Timeline Integrity
    /// level clock. A bitmask rather than a counter, so an unbalanced release
    /// can never strand the clock paused. The clock runs through every other
    /// live moment — including a Restoration Font channel and an active Time
    /// Freeze, which A2 deliberately does NOT pause.
    /// </summary>
    [System.Flags]
    public enum IntegrityClockPause {
        None = 0,
        /// <summary>A dialogue sequence is holding the world.</summary>
        Dialogue = 1,
        /// <summary>The pause menu (Story) is up.</summary>
        PauseMenu = 2,
        /// <summary>A death-rewind presentation or the Timeline Collapse beat.</summary>
        DeathRewind = 4,
        /// <summary>The boss-intro name-card ritual.</summary>
        BossIntro = 8,
        /// <summary>
        /// Package 13 W4 (S36): a scripted in-level presentation beat — Level 9's
        /// Eraser near-capture — that "pauses the clock for the beat, like other
        /// scripted presentations". Appended; the owner releases it on teardown.
        /// </summary>
        ScriptedBeat = 16
    }

    /// <summary>Why a Timeline Collapse is being resolved (F01 / F11).</summary>
    public enum TimelineCollapseCause {
        /// <summary>The rewind pool ran out on a death.</summary>
        Death = 0,
        /// <summary>Timeline Integrity reached zero. Only this cause grants an F11 recovery minimum.</summary>
        Timer = 1
    }

    public partial class StoryManager : Node {
        public static StoryManager Instance { get; private set; }

        public CampaignLevel CurrentLevel { get; private set; } = CampaignLevel.Tutorial;
        public int ChronalDustCollected { get; private set; }
        public int ChronalRewindsRemaining { get; set; } = 3;
        public bool TutorialComplete { get; set; }
        public bool HasPendingTimelineRestart { get; private set; }
        public CampaignLevel CollapsedLevel { get; private set; }
        public string CollapsedCheckpointID { get; private set; } = "";

        // === Per-level run statistics (Package 8 B1) ===============================
        // The results overlay wants a completion time and a rewind count and neither
        // existed anywhere: rewinds were only ever tracked as a remaining pool (which
        // a checkpoint refills, so it cannot be differenced), and nothing timed a
        // level at all. Both live here rather than on a level controller because the
        // Tutorial and Florence deliberately do not extend StoryLevelControllerBase,
        // and a stat authored twice would drift between the two families.

        /// <summary>Seconds elapsed in the level currently being played.</summary>
        public float LevelElapsedSeconds { get; private set; }

        /// <summary>Chronal Rewinds spent in the level currently being played.</summary>
        public int LevelRewindsUsed { get; private set; }

        /// <summary>True while the level timer is accumulating.</summary>
        public bool IsLevelTimerRunning { get; private set; }

        /// <summary>Completion time of the most recently finished level, frozen at completion.</summary>
        public float LastLevelCompletionSeconds { get; private set; }

        /// <summary>Rewinds spent in the most recently finished level.</summary>
        public int LastLevelRewindsUsed { get; private set; }

        // === Campaign routing (Package 11 A12) =====================================
        // The route used to be "the enum value IS the array index, advance with +1".
        // Level 4A (V7.6) breaks that: it is played between Levels 4 and 5 but must
        // not renumber anything, and its scene path depends on the locked hero. So
        // the model is now an explicit ordered route plus an ID-keyed path table.
        // Nothing indexes LevelScenePaths by enum value any more.

        /// <summary>
        /// The campaign in play order (Package 11 §2.4): Level 4A sits between
        /// Paris and the Titanic. <see cref="AdvanceToNextLevel"/> steps this list;
        /// the enum's numeric values are identities, not positions.
        /// </summary>
        private static readonly CampaignLevel[] Route = {
            CampaignLevel.Tutorial,
            CampaignLevel.Florence,
            CampaignLevel.Orleans,
            CampaignLevel.Chicago,
            CampaignLevel.Paris,
            CampaignLevel.LegacyNexus,
            CampaignLevel.Titanic,
            CampaignLevel.Pompeii,
            CampaignLevel.Nassau,
            CampaignLevel.Egypt,
            CampaignLevel.Berlin,
            CampaignLevel.London,
            CampaignLevel.Gettysburg,
            CampaignLevel.Lunar,
            CampaignLevel.ChronalVoid,
            CampaignLevel.NeoEarth,
            CampaignLevel.Alexandria,
        };

        /// <summary>The campaign in play order. Seventeen slots as of V7.6.</summary>
        public static IReadOnlyList<CampaignLevel> CampaignRoute => Route;

        /// <summary>Per-hero Level 4A scene prefix; the suffix is the lowercase character ID.</summary>
        public const string LegacyLevelScenePrefix = "res://scenes/campaign/Level_04A_";

        /// <summary>Per-hero Level 4A level-ID prefix (<c>level_04a_einstein</c>).</summary>
        public const string LegacyLevelIDPrefix = "level_04a_";

        private static readonly Dictionary<CampaignLevel, string> LevelScenePaths = new() {
            { CampaignLevel.Tutorial, "res://scenes/campaign/Level_00_Tutorial.tscn" },
            { CampaignLevel.Florence, "res://scenes/campaign/Level_01_Florence.tscn" },
            { CampaignLevel.Orleans, "res://scenes/campaign/Level_02_Orleans.tscn" },
            { CampaignLevel.Chicago, "res://scenes/campaign/Level_03_Chicago.tscn" },
            { CampaignLevel.Paris, "res://scenes/campaign/Level_04_Paris.tscn" },
            { CampaignLevel.Titanic, "res://scenes/campaign/Level_05_Titanic.tscn" },
            { CampaignLevel.Pompeii, "res://scenes/campaign/Level_06_Pompeii.tscn" },
            { CampaignLevel.Nassau, "res://scenes/campaign/Level_07_Nassau.tscn" },
            { CampaignLevel.Egypt, "res://scenes/campaign/Level_08_Egypt.tscn" },
            { CampaignLevel.Berlin, "res://scenes/campaign/Level_09_Berlin.tscn" },
            { CampaignLevel.London, "res://scenes/campaign/Level_10_Globe.tscn" },
            { CampaignLevel.Gettysburg, "res://scenes/campaign/Level_11_Gettysburg.tscn" },
            { CampaignLevel.Lunar, "res://scenes/campaign/Level_12_Lunar.tscn" },
            { CampaignLevel.ChronalVoid, "res://scenes/campaign/Level_13_ChronalVoid.tscn" },
            { CampaignLevel.NeoEarth, "res://scenes/campaign/Level_14_NeoEarth.tscn" },
            { CampaignLevel.Alexandria, "res://scenes/campaign/Level_15_Alexandria.tscn" },
        };

        public override void _Ready() {
            Instance = this;
            if (EventBus.Instance != null) {
                EventBus.Instance.OnChronalDustCollected += OnDustCollected;
                EventBus.Instance.OnLevelComplete += OnLevelComplete;
                EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnChronalDustCollected -= OnDustCollected;
                EventBus.Instance.OnLevelComplete -= OnLevelComplete;
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
            }
        }

        /// <summary>
        /// Accumulates the level timer. This is an autoload, so the timer keeps
        /// running through room transitions and dialogue but stops the moment the
        /// level reports complete; it is a wall-clock "how long did that take",
        /// not a gameplay-time measurement, and nothing simulation-side reads it.
        /// </summary>
        public override void _Process(double delta) {
            if (!IsLevelTimerRunning) return;
            LevelElapsedSeconds += (float)delta;
        }

        /// <summary>
        /// Package 11 A3 (F01): the Timeline Integrity level clock ticks on the
        /// physics step, not <c>_Process</c> — it is a gameplay resource, and a
        /// gameplay resource is spent at 60 Hz. The autoload owns it so the
        /// clock survives room transitions and scene-local teardown.
        /// </summary>
        public override void _PhysicsProcess(double delta) {
            TickIntegrityClock(delta);
            // F10 durability: continuous HP/meter/timer changes are checkpointed
            // at least once per live second. Between checkpoints none of it was
            // durable before Package 11 A3b.
            TickDurableSnapshot(delta);
        }

        public void StartCampaign(string characterID) {
            var session = GameManager.Instance.CurrentSession;
            session.SelectedCharacterID = characterID;
            GameManager.Instance.CurrentSession = session;

            ResetCampaignState(session.Difficulty);
            SaveManager.Instance?.CreateStorySlot(session.ActiveSaveSlot, characterID, session.Difficulty);
            // V7.3 quit-fee rule: a live campaign session is marked on disk;
            // an abnormal exit leaves the marker for the boot check to bill.
            SessionExitGuard.WriteMarker(session.ActiveSaveSlot);

            LoadCurrentLevel();
        }

        /// <summary>
        /// Fresh-campaign runtime state. The rewind pool starts at the difficulty's
        /// authored maximum (Easy 5 / Normal 3 / Hard 1) — the same 5/3/1
        /// <see cref="SaveManager.CreateStorySlot"/> writes to the slot, which was
        /// previously only honored on resume while a new campaign hardcoded 3
        /// (audit M-4: Easy started two rewinds short).
        ///
        /// V7.3: also clears every piece of stale singleton residue a previous
        /// campaign could leak into this one — the pending Timeline Collapse
        /// restart, the collapsed-level anchor, the boss-intro seen set, the
        /// collapse-beat flag, and the whole per-attempt registry family
        /// (activated checkpoints, destroyed extractors, found secrets, font
        /// uses, live Integrity).
        /// </summary>
        public void ResetCampaignState(Difficulty difficulty) {
            CurrentLevel = CampaignLevel.Tutorial;
            ChronalDustCollected = 0;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            TutorialComplete = false;
            HasPendingTimelineRestart = false;
            CollapsedLevel = CampaignLevel.Tutorial;
            CollapsedCheckpointID = "";
            HasSeenCollapseBeat = false;
            _bossIntrosSeen.Clear();
            ClearLevelAttemptState();
            ClearRecoveryPlacementHold();
        }

        /// <summary>
        /// Developer level select (2026-08-15, temporary while in development).
        /// Puts the campaign runtime into the state a fresh run of <paramref name="level"/>
        /// would have — selected character, difficulty, a full rewind pool, zero
        /// dust, tutorial marked complete for anything past level 0 — and clears
        /// <see cref="SessionData.ActiveSaveSlot"/> to <c>-1</c> so nothing in the
        /// level can autosave over a real slot (every save site tolerates no slot).
        /// Deliberately does <b>not</b> call <see cref="SaveManager.CreateStorySlot"/>.
        /// Split from <see cref="StartLevelDirect"/> so tests can pin the state
        /// without loading a scene.
        /// </summary>
        public void PrepareDirectLevel(CampaignLevel level, string characterID, Difficulty difficulty) {
            if (GameManager.Instance != null) {
                SessionData session = GameManager.Instance.CurrentSession;
                session.SelectedCharacterID = characterID;
                session.Difficulty = difficulty;
                session.ActiveSaveSlot = -1;
                GameManager.Instance.CurrentSession = session;
            }
            ResetCampaignState(difficulty);
            CurrentLevel = level;
            TutorialComplete = level != CampaignLevel.Tutorial;
        }

        /// <summary>Developer level select: prepare and load a level directly. See <see cref="PrepareDirectLevel"/>.</summary>
        public void StartLevelDirect(CampaignLevel level, string characterID, Difficulty difficulty) {
            PrepareDirectLevel(level, characterID, difficulty);
            LoadCurrentLevel();
        }

        public void ResumeCampaign(int slot, StorySaveData save) {
            if (save == null || GameManager.Instance == null) return;
            // Package 11 A12: a path -> ID lookup over the route, not a path ->
            // array-index scan. The save's own character resolves its 4A variant,
            // because the session has not been repointed at this slot yet.
            CurrentLevel = CampaignLevel.Tutorial;
            foreach (CampaignLevel level in Route) {
                if (GetLevelScenePath(level, save.SelectedCharacterID) == save.CurrentLevelID) {
                    CurrentLevel = level;
                    break;
                }
            }
            ChronalDustCollected = Mathf.Max(0, save.LevelChronalDust);
            ChronalRewindsRemaining = Mathf.Max(0, save.CurrentLives);
            // Package 12 W1 (R03, plan D3(a)): loading a save never grants a
            // Post-Landing Hold — only the in-session placement does.
            ClearRecoveryPlacementHold();
            TutorialComplete = save.CompletedLevels.Contains("level_00_tutorial");
            HasSeenCollapseBeat = save.HasSeenCollapseBeat;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = slot;
            session.SelectedCharacterID = save.SelectedCharacterID;
            session.Difficulty = save.Difficulty;
            GameManager.Instance.CurrentSession = session;
            // V7.3 quit-fee rule: the resumed session is live again.
            SessionExitGuard.WriteMarker(slot);
            RouteResumeByAttemptStatus(save);
        }

        /// <summary>
        /// Package 11 A3b (F10 load routing). The load destination is driven by
        /// the saved <see cref="StoryAttemptState.Status"/>, never by "always the
        /// hub":
        ///
        /// <list type="bullet">
        /// <item><b>Smothered</b> → the Game Over screen, with no HP or anchor
        /// refill and no replayed failure fee. Only an explicit Restart Level
        /// leaves it.</item>
        /// <item><b>Active / RecoveryPending</b> in <b>Act III</b> → straight back
        /// into the level at its checkpoint. The hub is unreachable until the
        /// campaign is complete.</item>
        /// <item><b>AwaitingHubResume</b>, and everything in Acts I–II → the hub,
        /// whose portal finishes the already-committed paid recovery.</item>
        /// </list>
        ///
        /// <b>None of these paths runs fresh-entry resource initialization</b> —
        /// <see cref="LoadCurrentLevel"/> resolves a mid-level resume from the
        /// parked checkpoint, and the hub route leaves the attempt alone.
        /// </summary>
        private void RouteResumeByAttemptStatus(StorySaveData save) {
            RestoreAttemptRecord(save);
            StoryAttemptStatus status = save.AttemptState?.Status ?? StoryAttemptStatus.Active;
            if (status == StoryAttemptStatus.Smothered) {
                LoadGameOverScreen();
                return;
            }
            bool actIII = IsActIIILevel(CurrentLevel);
            bool parkedInLevel = !string.IsNullOrWhiteSpace(save.LastCheckpointID);
            if (actIII && parkedInLevel && status != StoryAttemptStatus.AwaitingHubResume) {
                LoadCurrentLevel();
                return;
            }
            ReturnToHub();
        }

        public void LoadCurrentLevel() {
            string path = GetCurrentLevelPath();
            if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path)) {
                BeginLevelRun(resumeAttempt: IsMidLevelResume(path));
                RequestScene(path);
            } else {
                GD.PushError($"Campaign scene is not authored yet: {path}");
            }
        }

        // === Package 11 A3b: scene-load test seam ===========================
        // The Act III routing decisions (Snap in place, Smothered to Game Over,
        // gauntlet chaining, resume routing) are the behaviour under test, and
        // every one of them ends in a scene change. The seam records the
        // destination instead of loading it, so a suite can assert WHERE a
        // decision routed without tearing down the test scene tree.

        /// <summary>Set by a test: record the destination instead of changing scene.</summary>
        internal bool SuppressSceneLoadsForTesting { get; set; }

        /// <summary>The last scene path this manager routed to. Test seam.</summary>
        public string LastRequestedScenePath { get; private set; } = "";

        private void RequestScene(string path) {
            LastRequestedScenePath = path ?? "";
            if (SuppressSceneLoadsForTesting) return;
            GameManager.Instance?.LoadScene(path);
        }

        /// <summary>
        /// V7.3: a level entry resumes the parked attempt (rather than starting
        /// a fresh one) exactly when the active save is parked mid-level on
        /// THIS scene with a reached checkpoint. Level completion and Restart
        /// both clear <c>LastCheckpointID</c>, so a stale id can never promote
        /// a fresh entry into a resume.
        /// </summary>
        private bool IsMidLevelResume(string scenePath) {
            StorySaveData save = GetActiveSave();
            return save != null
                && save.CurrentLevelID == scenePath
                && !string.IsNullOrWhiteSpace(save.LastCheckpointID);
        }

        public void ReturnToHub() {
            StopLevelRun();
            RequestScene(HubScenePath);
        }

        /// <summary>The Time-Ship hub. Unreachable during the Act III gauntlet (V7.6).</summary>
        public const string HubScenePath = "res://scenes/campaign/HubWorld.tscn";

        /// <summary>
        /// Zeroes and starts the per-level statistics. Called on every level load,
        /// including a Timeline Collapse restart — a restarted level is a fresh
        /// attempt, and carrying the abandoned attempt's clock into it would report
        /// a completion time the player never experienced.
        ///
        /// V7.3: a FRESH entry clears the per-attempt registries, opens
        /// Integrity at 100, and refills the rewind pool to the difficulty
        /// maximum; a mid-level RESUME restores all of it from the active save
        /// and leaves the pool exactly where the save parked it.
        /// </summary>
        public void BeginLevelRun(bool resumeAttempt = false) {
            LevelElapsedSeconds = 0f;
            LevelRewindsUsed = 0;
            IsLevelTimerRunning = true;
            _durableSnapshotTimer = 0f;
            // Package 11 A3b: arm (or disarm) A3's Act III collapse hook for the
            // level about to load. In the gauntlet the timer-zero collapse plays
            // the same fracture beat and then resolves an Anchor Snap or the
            // Smothered Game Over instead of a hub extraction.
            ActIIICollapseOverride = IsActIIILevel(CurrentLevel) ? RunActIIICollapseBeat : null;
            // An Anchor Snap reconstructs the level inside the SAME attempt. It
            // reaches here through LoadCurrentLevel, and on a slotless developer
            // launch there is no parked save to recognize it by - so the Snap
            // says so explicitly rather than letting the mint branch refill the
            // anchor it just spent.
            if (_forceResumeNextRun) {
                _forceResumeNextRun = false;
                resumeAttempt = true;
            }
            if (resumeAttempt) {
                // F10: a resume never re-initializes anchors, and never mints a
                // new attempt ID. The parked record is the attempt. With no slot
                // at all (a developer direct launch) the live in-memory attempt
                // IS the record - reading a null save here would wipe it.
                StorySaveData parked = GetActiveSave();
                if (parked != null) RestoreAttemptStateFromSave(parked);
                return;
            }
            ClearLevelAttemptState();
            // Package 12 W1 (R03): a fresh entry never carries a pending
            // Post-Landing Hold (the in-session Collapse/Snap placements are
            // resumes and never reach this branch).
            ClearRecoveryPlacementHold();
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            // Package 11 A3b (F10): fresh entry is one of exactly two events that
            // mint an attempt ID and initialize the Act III anchor charges. The
            // other is an explicit full Restart Level, which reaches this same
            // branch because it clears LastCheckpointID first.
            MintFreshAttempt();
        }

        /// <summary>Stops the clock without publishing a completion result.</summary>
        public void StopLevelRun() {
            IsLevelTimerRunning = false;
            StopIntegrityClock();
        }

        /// <summary>
        /// Freezes the running statistics as the "last completed level" result the
        /// results overlay reads. Idempotent, because both the base level
        /// controller and the pre-existing Tutorial/Florence controllers reach
        /// completion through <c>OnLevelComplete</c>.
        /// </summary>
        public void CompleteLevelRun() {
            if (!IsLevelTimerRunning) return;
            IsLevelTimerRunning = false;
            LastLevelCompletionSeconds = LevelElapsedSeconds;
            LastLevelRewindsUsed = LevelRewindsUsed;
            LastLevelIntegrityPercent = TimelineIntegrityPercent;
            LastLevelSecretsFound = LevelSecretsFound;
            // Package 11 A8, ruling 2.A: the Chronal Rating is retired by V7.6.
            // Nothing computes or stores it any more; the Integrity tier is the
            // single grade the results overlay shows.
        }

        // === Timeline Integrity — the level timer (V7.6 F01, Package 11 A3) ==
        // The V7.1/V7.3 siphon model (per-machine share, 10 s grace, 600 px
        // engagement, +3/+2/+5 restoration) is gone. Integrity is now a
        // normalized level CLOCK: it opens at 100, drains globally from level
        // load whether the player has seen a machine or not, and nothing ever
        // adds a point back. Breaking a machine or finding the secret only
        // ever slows the FUTURE rate — see TimelineIntegrityRules.

        /// <summary>This attempt's Timeline Integrity, 0-100.</summary>
        public float TimelineIntegrityPercent { get; private set; } = TimelineIntegrityRules.StartPercent;

        /// <summary>Secrets found in this level attempt.</summary>
        public int LevelSecretsFound { get; private set; }

        /// <summary>Frozen at completion for the results overlay.</summary>
        public float LastLevelIntegrityPercent { get; private set; } = TimelineIntegrityRules.StartPercent;
        public int LastLevelSecretsFound { get; private set; }


        /// <summary>The current level's authored par, in seconds. 0 = untimed.</summary>
        public float ParSecondsForCurrentLevel { get; private set; }

        /// <summary>
        /// The authored starting Extractor population, captured once at level
        /// load. The F01 denominator is fixed from this and is NEVER
        /// recalculated from survivors, including on a checkpoint resume.
        /// </summary>
        public int StartingExtractorCount { get; private set; }

        /// <summary>Machines still standing this attempt. Only the numerator moves.</summary>
        public int LivingExtractorCount { get; private set; }

        /// <summary>True once any secret has been found this attempt (a permanent -0.1 on the rate).</summary>
        public bool SecretFoundThisLevel => _foundSecrets.Count > 0;

        /// <summary>True while the clock is armed for a timed level (par &gt; 0) and not locked.</summary>
        public bool IsIntegrityClockRunning =>
            ParSecondsForCurrentLevel > 0f && IsLevelTimerRunning && !IsPreBossLocked;

        /// <summary>True once the PreBoss fracture froze the gauge for good this attempt.</summary>
        public bool IsPreBossLocked { get; private set; }

        /// <summary>
        /// The F11 paid-recovery allowance banked at the last checkpoint —
        /// deliberately distinct from the live gauge, which keeps draining
        /// after the checkpoint is struck.
        /// </summary>
        public float CheckpointIntegrityPercent { get; private set; } = TimelineIntegrityRules.StartPercent;

        /// <summary>Live drain, in integrity points per second. 0 while paused or locked.</summary>
        public float CurrentIntegrityDrainPerSecond =>
            !IsIntegrityClockRunning || _integrityClockPause != IntegrityClockPause.None
                ? 0f
                : TimelineIntegrityRules.DrainPerSecond(
                    ParSecondsForCurrentLevel,
                    GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal,
                    LivingExtractorCount,
                    StartingExtractorCount,
                    SecretFoundThisLevel);

        private IntegrityClockPause _integrityClockPause = IntegrityClockPause.None;
        private bool _integrityCollapseFired;

        /// <summary>True while any pause scope holds the clock. Test seam.</summary>
        public bool IsIntegrityClockPaused => _integrityClockPause != IntegrityClockPause.None;

        /// <summary>The live pause mask. Test seam.</summary>
        public IntegrityClockPause IntegrityClockPauseScopes => _integrityClockPause;

        /// <summary>
        /// Arms the level clock. Called once per level entry from the level
        /// controller, AFTER the attempt registries are in place, so a resumed
        /// attempt's broken machines are already known. An untimed level
        /// (Tutorial, Florence) passes <c>parSeconds = 0</c> — or simply never
        /// calls this — and the gauge stays inert at 100.
        /// </summary>
        public void BeginIntegrityClock(float parSeconds, int startingExtractors) {
            ParSecondsForCurrentLevel = Mathf.Max(0f, parSeconds);
            StartingExtractorCount = Mathf.Max(0, startingExtractors);
            LivingExtractorCount = Mathf.Max(0, StartingExtractorCount - _destroyedExtractors.Count);
            _integrityClockPause = IntegrityClockPause.None;
            _integrityCollapseFired = false;
            PublishIntegrity();
        }

        /// <summary>Disarms the clock (level complete, hub, teardown).</summary>
        public void StopIntegrityClock() {
            ParSecondsForCurrentLevel = 0f;
            _integrityClockPause = IntegrityClockPause.None;
        }

        /// <summary>
        /// The pause set. The clock stops for frozen presentation only —
        /// dialogue, the pause menu, the death-rewind/collapse beat, and the
        /// boss-intro ritual. It keeps running through ALL other live play,
        /// including a Restoration Font channel and including an active Time
        /// Freeze (A2 owns the freeze and deliberately does not pause this).
        /// Idempotent by design: a mask, not a counter, so an unbalanced
        /// release can never strand the clock.
        /// </summary>
        public void SetIntegrityClockPause(IntegrityClockPause scope, bool paused) {
            if (scope == IntegrityClockPause.None) return;
            IntegrityClockPause updated = paused
                ? _integrityClockPause | scope
                : _integrityClockPause & ~scope;
            if (updated == _integrityClockPause) return;
            _integrityClockPause = updated;
            PublishIntegrity();
        }

        /// <summary>
        /// A machine broke: the numerator drops by one for the rest of the
        /// level. The gauge itself does not move — F01 is explicit that
        /// destruction buys future time, never a refill.
        /// </summary>
        public void NotifyExtractorDestroyed() {
            if (LivingExtractorCount <= 0) return;
            LivingExtractorCount--;
            PublishIntegrity();
        }

        /// <summary>
        /// PreBoss activation freezes the gauge permanently for this attempt
        /// and banks the final Integrity. Driven by the authored checkpoint
        /// ROLE, never by an ID suffix. Entry, Middle and route triggers can
        /// never call this.
        /// </summary>
        public void LockIntegrityAtPreBoss() {
            if (IsPreBossLocked) return;
            IsPreBossLocked = true;
            CheckpointIntegrityPercent = TimelineIntegrityPercent;
            // Package 11 A3b (N05): the ending average reads the PreBoss-LOCKED
            // gauge, banked here and committed with the completion transaction —
            // never a value sampled at completion, which a later rule change
            // could quietly redefine.
            _attempt.PreBossLocked = true;
            _attempt.FinalGateIntegrity = TimelineIntegrityPercent;
            PublishIntegrity();
        }

        /// <summary>
        /// Banks the live gauge as the F11 recovery allowance at a checkpoint.
        /// A locked PreBoss clock keeps the value it froze with.
        /// </summary>
        public void BankCheckpointIntegrity() {
            if (IsPreBossLocked) return;
            CheckpointIntegrityPercent = TimelineIntegrityPercent;
        }

        /// <summary>
        /// Authored F11 remaining-route budget for one checkpoint, in seconds
        /// of live play from that anchor to the PreBoss lock. Registered by
        /// the level controller as it builds its checkpoints.
        /// </summary>
        public void SetRecoveryRouteSeconds(string checkpointID, float remainingRouteSeconds) {
            if (string.IsNullOrWhiteSpace(checkpointID)) return;
            _recoveryRouteSeconds[checkpointID] = Mathf.Max(0f, remainingRouteSeconds);
        }

        public float GetRecoveryRouteSeconds(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID)
            && _recoveryRouteSeconds.TryGetValue(checkpointID, out float seconds)
                ? seconds
                : 0f;

        private readonly Dictionary<string, float> _recoveryRouteSeconds = new(System.StringComparer.Ordinal);

        /// <summary>
        /// Ticks the level clock. Runs on the autoload's physics step so the
        /// rate is frame-rate independent and survives room transitions.
        /// Clamps at zero and fires exactly one timer-caused Collapse there.
        /// </summary>
        internal void TickIntegrityClock(double delta) {
            if (!IsIntegrityClockRunning || _integrityClockPause != IntegrityClockPause.None) return;
            if (TimelineIntegrityPercent <= 0f) {
                FireIntegrityCollapse();
                return;
            }
            float before = TimelineIntegrityPercent;
            TimelineIntegrityPercent = Mathf.Max(
                0f, TimelineIntegrityPercent - CurrentIntegrityDrainPerSecond * (float)delta);
            if (!Mathf.IsEqualApprox(before, TimelineIntegrityPercent)) PublishIntegrity();
            if (TimelineIntegrityPercent <= 0f) FireIntegrityCollapse();
        }

        /// <summary>
        /// Zero Integrity is a Timeline Collapse on EVERY difficulty. Acts I-II
        /// route through the ordinary collapse beat with a TimerCaused cause.
        ///
        /// ACT III HOOK (A3b): levels 13-15 resolve an Anchor Snap (with a
        /// Warden Beacon charge) or the Smothered Game Over (with none) here
        /// instead, by assigning <see cref="ActIIICollapseOverride"/>.
        /// </summary>
        private void FireIntegrityCollapse() {
            if (_integrityCollapseFired) return;
            _integrityCollapseFired = true;
            PendingCollapseCause = TimelineCollapseCause.Timer;
            if (ActIIICollapseOverride != null) {
                ActIIICollapseOverride();
                return;
            }
            if (GetTree()?.GetFirstNodeInGroup(FTT.Environment.ChronalRewindManager.ManagerGroup)
                is FTT.Environment.ChronalRewindManager manager) {
                manager.BeginTimerCollapse();
                return;
            }
            // No rewind stack in the scene (a bare test fixture): collapse directly.
            BeginTimelineCollapse(CollapsedCheckpointID, TimelineCollapseCause.Timer);
        }

        /// <summary>
        /// Act III (A3b) replaces the Acts I-II collapse with Anchor Snap /
        /// Smothered. Left null by A3; also the collapse-at-zero test seam.
        /// </summary>
        public System.Action ActIIICollapseOverride { get; set; }

        /// <summary>What caused the collapse currently being resolved.</summary>
        public TimelineCollapseCause PendingCollapseCause { get; private set; } = TimelineCollapseCause.Death;

        private void PublishIntegrity() => EventBus.Instance?.RaiseTimelineIntegrityChanged(new IntegrityPayload {
            Percent = TimelineIntegrityPercent,
            Tier = (IntegrityTier)TimelineIntegrityRules.Tier(TimelineIntegrityPercent),
            LivingExtractors = LivingExtractorCount,
            DrainPerSecond = CurrentIntegrityDrainPerSecond,
            Frozen = IsPreBossLocked || _integrityClockPause != IntegrityClockPause.None
        });

        /// <summary>
        /// The single sink every Integrity cost draws through. Returns the
        /// integrity actually removed. Nothing in V7.6 adds a point back —
        /// there is deliberately no restoration counterpart.
        /// </summary>
        public float DrainTimelineIntegrityAmount(float amountPercent) {
            if (amountPercent <= 0f || !IsLevelTimerRunning || IsPreBossLocked) return 0f;
            float applied = Mathf.Min(amountPercent, TimelineIntegrityPercent);
            TimelineIntegrityPercent -= applied;
            if (applied > 0f) PublishIntegrity();
            return applied;
        }

        /// <summary>
        /// Counts a secret once per attempt. V7.6: a secret no longer restores
        /// integrity at all — it subtracts 0.1 from the drain FACTOR for the
        /// rest of the level, which is future time, not a refill. The
        /// <paramref name="isSpecialSecret"/> flag no longer selects an amount
        /// and is retained only so authored scenes keep compiling.
        /// </summary>
        public bool RegisterSecretFound(string secretID, bool isSpecialSecret = true) {
            if (!string.IsNullOrWhiteSpace(secretID) && !_foundSecrets.Add(secretID)) return false;
            LevelSecretsFound++;
            PublishIntegrity();
            return true;
        }

        // === Per-attempt registries (V7.3 mid-level resume) =================
        // Once-per-attempt facts that must survive a quit-and-resume and a
        // Timeline Collapse resume-from-anchor, and reset on a fresh entry or
        // a Restart Level: checkpoints stabilized (gates Mending + rewind
        // refresh), extractors destroyed, secrets found.

        private readonly HashSet<string> _activatedCheckpoints = new(System.StringComparer.Ordinal);
        private readonly HashSet<string> _destroyedExtractors = new(System.StringComparer.Ordinal);
        private readonly HashSet<string> _foundSecrets = new(System.StringComparer.Ordinal);

        /// <summary>The collapse beat has played once on this save (skippable after).</summary>
        public bool HasSeenCollapseBeat { get; private set; }

        public void MarkCollapseBeatSeen() => HasSeenCollapseBeat = true;

        /// <summary>
        /// Authoritative once-per-attempt checkpoint activation. Returns true
        /// only on the FIRST activation this attempt — the caller gates the
        /// Mending heal and the rewind-pool refresh on it, while respawn
        /// anchor and save still update on every activation.
        /// </summary>
        public bool TryActivateCheckpoint(string checkpointID) {
            if (string.IsNullOrWhiteSpace(checkpointID)) return false;
            bool first = _activatedCheckpoints.Add(checkpointID);
            // Package 11 A3b (F10): the anchor, its role, its authored encounter
            // baseline and the granted-benefit ID all commit in ONE transaction
            // with the activation. A granted benefit is recorded separately from
            // the activation because loading or a paid recovery re-reads the
            // activation set but must never pay Mending or the rewind refresh
            // a second time.
            _attempt.CheckpointRecord.AnchorID = checkpointID;
            _attempt.CheckpointRecord.AnchorRole = (int)GetCheckpointRole(checkpointID);
            if (!_attempt.CheckpointRecord.ActivatedCheckpointIDs.Contains(checkpointID)) {
                _attempt.CheckpointRecord.ActivatedCheckpointIDs.Add(checkpointID);
            }
            if (first && !_attempt.CheckpointRecord.GrantedBenefitIDs.Contains(checkpointID)) {
                _attempt.CheckpointRecord.GrantedBenefitIDs.Add(checkpointID);
            }
            _attempt.CheckpointRecord.BaselineEncounterIDs =
                new List<string>(GetEncounterBaseline(checkpointID));
            _attempt.CheckpointRecord.BaselineVersion = GetEncounterBaselineVersion(checkpointID);
            _attempt.Bump();
            return first;
        }

        /// <summary>
        /// True when this checkpoint's Mending + rewind refresh have already been
        /// paid this attempt. Distinct from <see cref="IsCheckpointActivated"/>:
        /// a migrated legacy save can be activated-but-unbenefited, and F10
        /// forbids paying a benefit twice.
        /// </summary>
        public bool HasGrantedCheckpointBenefit(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID)
            && _attempt.CheckpointRecord.GrantedBenefitIDs.Contains(checkpointID);

        // === Authored encounter baselines (F10) =============================
        // "Rebuild ordinary encounters from an authored checkpoint baseline"
        // with membership bound to STABLE ENCOUNTER IDs, never to an enemy's
        // physical position at death. The level controller registers each
        // anchor's baseline as it builds its checkpoints; StoryManager only
        // stores and persists it.

        private readonly Dictionary<string, List<string>> _encounterBaselines =
            new(System.StringComparer.Ordinal);

        /// <summary>Registers one anchor's authored baseline encounter IDs.</summary>
        public void RegisterEncounterBaseline(string checkpointID, IReadOnlyList<string> encounterIDs) =>
            RegisterEncounterBaseline(checkpointID, encounterIDs, 1);

        /// <summary>
        /// Package 12 W2 (GAP-13): registers one anchor's authored baseline with
        /// the layout version it was authored against. The version is committed
        /// into <see cref="StoryCheckpointRecord.BaselineVersion"/> on
        /// activation, so a resume can tell a record written against the current
        /// map from one written against an older layout (Package 11's derived
        /// <c>{LevelID}_wave_N</c> IDs are version 1).
        /// </summary>
        public void RegisterEncounterBaseline(string checkpointID, IReadOnlyList<string> encounterIDs, int version) {
            if (string.IsNullOrWhiteSpace(checkpointID)) return;
            var ids = new List<string>();
            foreach (string id in encounterIDs ?? System.Array.Empty<string>()) {
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id)) ids.Add(id);
            }
            _encounterBaselines[checkpointID] = ids;
            _encounterBaselineVersions[checkpointID] = System.Math.Max(1, version);
        }

        private readonly Dictionary<string, int> _encounterBaselineVersions =
            new(System.StringComparer.Ordinal);

        /// <summary>The layout version an anchor's baseline was registered with (1 when unknown).</summary>
        public int GetEncounterBaselineVersion(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID)
            && _encounterBaselineVersions.TryGetValue(checkpointID, out int version)
                ? version
                : 1;

        /// <summary>The authored baseline for an anchor, or an empty list.</summary>
        public IReadOnlyList<string> GetEncounterBaseline(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID)
            && _encounterBaselines.TryGetValue(checkpointID, out List<string> ids)
                ? ids
                : System.Array.Empty<string>();

        public bool IsCheckpointActivated(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID) && _activatedCheckpoints.Contains(checkpointID);

        // === Checkpoint roles (V7.6 F12, Package 11 A3) =====================
        // The ID -> role alias map. A level controller registers each authored
        // checkpoint's role as it builds it, so a verified existing stable ID
        // (`{levelID}_checkpoint_1`) survives while the ROLE, never the numeric
        // suffix, drives self-activation, the Hard-middle rule, the Integrity
        // freeze and save migration.

        private readonly Dictionary<string, FTT.Environment.CheckpointRole> _checkpointRoles =
            new(System.StringComparer.Ordinal);

        /// <summary>
        /// Registers an authored checkpoint's role. Re-locks the Integrity
        /// gauge when a resumed attempt had already struck its PreBoss
        /// fracture — a reload retains the lock and the banked score without
        /// refilling the gauge or replaying checkpoint benefits.
        /// </summary>
        public void RegisterCheckpointRole(string checkpointID, FTT.Environment.CheckpointRole role) {
            if (string.IsNullOrWhiteSpace(checkpointID)) return;
            _checkpointRoles[checkpointID] = role;
            if (role == FTT.Environment.CheckpointRole.PreBoss && _activatedCheckpoints.Contains(checkpointID)) {
                LockIntegrityAtPreBoss();
            }
        }

        /// <summary>
        /// The authored role for a saved checkpoint ID. Unregistered IDs fall
        /// back to <see cref="FTT.Environment.CheckpointRole.Entry"/> — the
        /// safe answer, because Entry can never lock the boss clock. F12 is
        /// explicit: never reinterpret a saved <c>_1</c> as PreBoss.
        /// </summary>
        public FTT.Environment.CheckpointRole GetCheckpointRole(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID)
            && _checkpointRoles.TryGetValue(checkpointID, out FTT.Environment.CheckpointRole role)
                ? role
                : FTT.Environment.CheckpointRole.Entry;

        public bool HasCheckpointRole(string checkpointID) =>
            !string.IsNullOrWhiteSpace(checkpointID) && _checkpointRoles.ContainsKey(checkpointID);

        public void RecordExtractorDestroyed(string extractorID) {
            if (!string.IsNullOrWhiteSpace(extractorID)) _destroyedExtractors.Add(extractorID);
        }

        public bool IsExtractorDestroyed(string extractorID) =>
            !string.IsNullOrWhiteSpace(extractorID) && _destroyedExtractors.Contains(extractorID);

        public bool IsSecretFound(string secretID) =>
            !string.IsNullOrWhiteSpace(secretID) && _foundSecrets.Contains(secretID);

        /// <summary>Fresh entry / Restart Level: no per-attempt fact survives.</summary>
        // === V7.6 Time Freeze cooldown (Package 11 A2) ======================

        /// <summary>
        /// Seconds left on the Story Time Freeze cooldown for THIS attempt.
        ///
        /// <para>Per-attempt state like the checkpoint and extractor registries:
        /// a fresh level entry and a full Restart Level both start Ready (0),
        /// while a mid-level resume restores exactly what the save parked. It is
        /// deliberately untouched by checkpoint activation, death rewind,
        /// Timeline Collapse and Anchor Snap — none of them may refresh it.</para>
        /// </summary>
        public float TimeFreezeCooldownRemaining { get; private set; }

        /// <summary>
        /// The live controller's sink. While a freeze is ACTIVE the controller
        /// pushes the conservative full cooldown so any background autosave
        /// records it; after thaw it pushes the true remainder.
        /// </summary>
        public void SetTimeFreezeCooldown(float seconds) =>
            TimeFreezeCooldownRemaining = Mathf.Max(0f, seconds);

        /// <summary>
        /// F03: an explicit Save or an exit during a live freeze ends the freeze
        /// and stores the full cooldown. Resolved through the controller's group
        /// so the save surfaces never take a direct dependency on the level's
        /// runtime stack. No-op when nothing is frozen.
        /// </summary>
        public void EndActiveTimeFreezeForExplicitSave() {
            if (GetTree()?.GetFirstNodeInGroup(
                    FTT.Environment.TimeFreezeController.ControllerGroup)
                is FTT.Environment.TimeFreezeController controller
                && IsInstanceValid(controller)) {
                controller.EndFreezeForExplicitSave();
            }
        }

        public void ClearLevelAttemptState() {
            // === Package 11 A10 region: F05 reward-source claims ==============
            // A fresh entry or a full Restart Level clears this level's source
            // claims, its pickup state, the pending tier bonus and the loss
            // tally together — a discarded attempt can never stack income onto
            // the next one. Previously deposited dust is untouched.
            FTT.Environment.LevelRewardDirectory.ResetAttempt();
            // === end Package 11 A10 region ===
            _activatedCheckpoints.Clear();
            _destroyedExtractors.Clear();
            _foundSecrets.Clear();
            _checkpointRoles.Clear();
            _recoveryRouteSeconds.Clear();
            _encounterBaselines.Clear();
            _encounterBaselineVersions.Clear();
            // Package 11 A3b: the attempt record is per-attempt state like the
            // rest of this family. It is CLEARED here but NOT re-minted — an
            // attempt ID is minted only by MintFreshAttempt(), from fresh level
            // entry or an explicit full Restart Level.
            _attempt = new StoryAttemptState();
            ClearRestorationFonts();
            TimelineIntegrityPercent = TimelineIntegrityRules.StartPercent;
            CheckpointIntegrityPercent = TimelineIntegrityRules.StartPercent;
            IsPreBossLocked = false;
            _integrityClockPause = IntegrityClockPause.None;
            _integrityCollapseFired = false;
            PendingCollapseCause = TimelineCollapseCause.Death;
            LivingExtractorCount = StartingExtractorCount;
            LevelSecretsFound = 0;
            // A fresh attempt starts with Time Freeze Ready.
            TimeFreezeCooldownRemaining = 0f;
            // Package 11 A1b (V7.6 F10): only a fresh entry or a full Restart
            // Level hands the run back its one saved life.
            StoryDefyHistoryUsed = false;
        }

        /// <summary>
        /// Per-attempt Defy History (V7.6 F10, Package 11 A1b). The authority
        /// lives here rather than on <c>PlayerController</c> so it survives a
        /// death rewind that rebuilds the player, a mid-level quit and resume,
        /// a Collapse recovery and a hub visit. <c>CharacterFactory</c> seeds
        /// the controller from it; the controller writes back on a proc.
        /// </summary>
        public bool StoryDefyHistoryUsed { get; set; }

        /// <summary>Checkpoint saves (and the collapse save) carry the attempt.</summary>
        public void WriteAttemptStateToSave(StorySaveData save) {
            if (save == null) return;
            save.ActivatedCheckpointIDs = new List<string>(_activatedCheckpoints);
            save.DestroyedExtractorIDs = new List<string>(_destroyedExtractors);
            save.FoundSecretIDs = new List<string>(_foundSecrets);
            save.FontUsesConsumed = new Dictionary<string, int>(_fontUsesConsumed);
            // Package 11 A10 (F05): the COLLECTED reward sources ride the
            // attempt, so a checkpoint resume, Anchor Snap, quit/resume or crash
            // recovery can restore the world without paying a source twice.
            save.ClaimedRewardSourceIDs = FTT.Environment.LevelRewardDirectory.ClaimedSourceIDs();
            save.LevelIntegrityPercent = TimelineIntegrityPercent;
            // F11: the paid-recovery allowance is banked separately from the
            // live gauge, which keeps draining after the fracture is struck.
            save.CheckpointIntegrityPercent = CheckpointIntegrityPercent;
            save.HasSeenCollapseBeat = HasSeenCollapseBeat;
            save.TimeFreezeCooldownSeconds = TimeFreezeCooldownRemaining;
            // Package 11 A1b (V7.6 F10): Defy History is once per ATTEMPT, and
            // it commits in the same envelope write as HP and meter.
            save.StoryDefyHistoryUsed = StoryDefyHistoryUsed;
            WriteAttemptRecord(save);
        }

        /// <summary>
        /// Package 11 A3b (F10). Folds the live attempt into the save's
        /// <see cref="StorySaveData.AttemptState"/> record, alongside the V7.3
        /// loose root fields above — which stay written as the migration source
        /// Phase C reads, and must gain no NEW consumer.
        /// </summary>
        private void WriteAttemptRecord(StorySaveData save) {
            _attempt.LevelID = CurrentLevelIDForAttempt();
            _attempt.CurrentIntegrity = TimelineIntegrityPercent;
            _attempt.StartingExtractorCount = StartingExtractorCount;
            _attempt.DestroyedExtractorIDs = new List<string>(_destroyedExtractors);
            _attempt.FoundSecretIDs = new List<string>(_foundSecrets);
            _attempt.FontUsesByID = new Dictionary<string, int>(_fontUsesConsumed);
            _attempt.PreBossLocked = IsPreBossLocked;
            // Package 12 W2 (GAP-01): the full resource record, not just the
            // Time Freeze cooldown, rides every checkpoint, durable snapshot and
            // critical event.
            CapturePlayerResources(save);
            _attempt.PlayerResourceTimers.TimeFreezeCooldownSeconds = TimeFreezeCooldownRemaining;
            _attempt.PresentationFlags["collapse_beat_seen"] = HasSeenCollapseBeat;
            _attempt.AnchorChargesRemaining = AnchorChargesRemaining;
            _attempt.AnchorChargeMaximum = AnchorChargeMaximum;
            _attempt.Normalize();
            save.AttemptState = _attempt;
            save.AnchorCharges = AnchorChargesRemaining;
        }

        // === Package 12 W2 region: F10 player resource publisher (GAP-01) ====

        /// <summary>
        /// Publishes the live hero's continuous combat resources into
        /// <see cref="StoryAttemptState.PlayerResourceTimers"/>: block charges,
        /// regen progress and shatter lockout, ability and Echo Step cooldowns,
        /// the Rally damage-taken meter not yet credited, and the D02d
        /// Wardenclyffe delay. The committed Ultimate meter is written in the
        /// SAME step, because the uncredited Rally meter is only meaningful as a
        /// pair with it — settling one against a stale copy of the other would
        /// either lose or double-pay the damage record.
        ///
        /// <para>Only a live level run publishes. Outside one (the hub, a
        /// Restart that has already cleared the attempt, the Game Over screen)
        /// whatever player happens to be in the tree is not this attempt's hero,
        /// and capturing it would hand the attempt a full, fresh set of resources
        /// — exactly the load-time refill F10 forbids.</para>
        /// </summary>
        private void CapturePlayerResources(StorySaveData save) {
            if (!IsLevelTimerRunning || !_attempt.HasAttempt) return;
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not FTT.Characters.PlayerController player
                || !IsInstanceValid(player) || !player.IsInsideTree()) {
                return;
            }
            player.CaptureStoryResourceTimers(_attempt.PlayerResourceTimers);
            if (save != null) save.CurrentUltimateMeter = player.CurrentUltimateMeter;
        }

        // === end Package 12 W2 region (resource publisher) ==================

        /// <summary>
        /// Pulls a saved attempt record back into the live manager, migrating a
        /// pre-v6 payload's loose fields first. <b>Never</b> fabricates anchors,
        /// an unused Defy or unclaimed rewards for a legacy mid-level attempt —
        /// <see cref="StoryAttemptState.MigrateFromLegacyRoot"/> marks that case
        /// <see cref="StoryAttemptStatus.LegacyRecoveryRequired"/> instead.
        /// </summary>
        private void RestoreAttemptRecord(StorySaveData save) {
            if (save == null) return;
            save.AttemptState ??= new StoryAttemptState();
            StoryAttemptState.MigrateFromLegacyRoot(save);
            _attempt = save.AttemptState;
            _attempt.Normalize();
            AnchorChargesRemaining = _attempt.AnchorChargesRemaining;
            AnchorChargeMaximum = _attempt.AnchorChargeMaximum;
            PendingAttemptNoticeKey = _attempt.Status == StoryAttemptStatus.LegacyRecoveryRequired
                ? LegacyRecoveryNoticeKey
                : "";
            if (!string.IsNullOrEmpty(PendingAttemptNoticeKey)) {
                GD.PushWarning(TranslationServer.Translate(LegacyRecoveryNoticeKey));
            }
            PublishAnchorCharges();
        }

        /// <summary>Mid-level resume: the parked attempt comes back whole.</summary>
        public void RestoreAttemptStateFromSave(StorySaveData save) {
            ClearLevelAttemptState();
            if (save == null) return;
            foreach (string id in save.ActivatedCheckpointIDs ?? new List<string>()) {
                if (!string.IsNullOrWhiteSpace(id)) _activatedCheckpoints.Add(id);
            }
            foreach (string id in save.DestroyedExtractorIDs ?? new List<string>()) {
                if (!string.IsNullOrWhiteSpace(id)) _destroyedExtractors.Add(id);
            }
            foreach (string id in save.FoundSecretIDs ?? new List<string>()) {
                if (!string.IsNullOrWhiteSpace(id)) _foundSecrets.Add(id);
            }
            if (save.FontUsesConsumed != null) {
                foreach (KeyValuePair<string, int> entry in save.FontUsesConsumed) {
                    if (!string.IsNullOrWhiteSpace(entry.Key)) _fontUsesConsumed[entry.Key] = entry.Value;
                }
            }
            // Package 11 A10 (F05): collected claims come back; the issued set
            // deliberately does not, because the reload discarded the world's
            // uncollected pickups along with it.
            FTT.Environment.LevelRewardDirectory.RestoreClaims(save.ClaimedRewardSourceIDs);
            TimelineIntegrityPercent = Mathf.Clamp(save.LevelIntegrityPercent, 0f, TimelineIntegrityRules.StartPercent);
            CheckpointIntegrityPercent = Mathf.Clamp(
                save.CheckpointIntegrityPercent, 0f, TimelineIntegrityRules.StartPercent);
            LevelSecretsFound = _foundSecrets.Count;
            HasSeenCollapseBeat = save.HasSeenCollapseBeat;
            // Package 11 A1b (V7.6 F10): the parked attempt comes back with its
            // Defy already spent - a resume can never restore the saved life.
            StoryDefyHistoryUsed = save.StoryDefyHistoryUsed;
            RestoreAttemptRecord(save);
            // F10 supersedes the V7.3 rule that a reload restores the gauge the
            // checkpoint banked: ordinary loading restores the LATEST DURABLE
            // Integrity, and CheckpointIntegrityPercent is only the paid-recovery
            // allowance. The attempt record is authoritative where it exists.
            if (_attempt.HasAttempt) {
                TimelineIntegrityPercent = Mathf.Clamp(
                    _attempt.CurrentIntegrity, 0f, TimelineIntegrityRules.StartPercent);
                // A locked boss clock stays locked on reload; it cannot be
                // re-run for a different tier.
                IsPreBossLocked = _attempt.PreBossLocked;
            }
            // A mid-level resume inherits the parked cooldown. Reload never
            // resumes a live freeze, so an activation saved mid-freeze comes
            // back as the conservative full cooldown the activation committed.
            TimeFreezeCooldownRemaining = Mathf.Max(0f, save.TimeFreezeCooldownSeconds);
        }

        /// <summary>
        /// Formats a duration as <c>M:SS</c> (or <c>H:MM:SS</c> past an hour) for
        /// the results overlay. Deliberately not localised as a pattern: digits and
        /// colons carry across every language this project plans to ship.
        /// </summary>
        public static string FormatDuration(float seconds) {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            int remainder = total % 60;
            return hours > 0
                ? $"{hours}:{minutes:00}:{remainder:00}"
                : $"{minutes}:{remainder:00}";
        }

        /// <summary>
        /// Steps <see cref="CampaignRoute"/> (Package 11 A12), so Paris advances to
        /// Level 4A and 4A advances to the Titanic. A level that is not on the route
        /// (or the finale) is a no-op, which keeps the old hard cap at Alexandria.
        /// </summary>
        public void AdvanceToNextLevel() {
            int index = RouteIndexOf(CurrentLevel);
            if (index >= 0 && index < Route.Length - 1) {
                CurrentLevel = Route[index + 1];
            }
            // The finished attempt's registries (fonts, checkpoints,
            // extractors, secrets) never leak into the next level.
            ClearLevelAttemptState();
        }

        /// <summary>Position of <paramref name="level"/> in the campaign route, or -1.</summary>
        public static int RouteIndexOf(CampaignLevel level) {
            for (int index = 0; index < Route.Length; index++) {
                if (Route[index] == level) return index;
            }
            return -1;
        }

        public string GetCurrentLevelPath() {
            return GetLevelScenePath(CurrentLevel);
        }

        /// <summary>
        /// Resolves a campaign slot to its scene, using the session's locked hero for
        /// the per-hero Level 4A. Unknown slots resolve to "" and callers refuse the
        /// route rather than changing to a missing scene.
        /// </summary>
        public static string GetLevelScenePath(CampaignLevel level) => GetLevelScenePath(level, null);

        /// <summary>
        /// Resolves a campaign slot to its scene path by ID (never by array index).
        ///
        /// <para>Level 4A is authored once per roster character, so its path is
        /// <c>Level_04A_&lt;hero&gt;.tscn</c>. <paramref name="heroCharacterID"/> wins
        /// when supplied (the resume path knows the save's character before the
        /// session does); otherwise the live session's selected character is used, and
        /// failing that the active save's locked character. With no character at all —
        /// a developer direct launch that skipped character select — this returns ""
        /// so the route is refused rather than pointed at a scene that cannot
        /// exist.</para>
        /// </summary>
        public static string GetLevelScenePath(CampaignLevel level, string heroCharacterID) {
            if (level == CampaignLevel.LegacyNexus) {
                string hero = ResolveLegacyHeroID(heroCharacterID);
                return string.IsNullOrWhiteSpace(hero) ? "" : $"{LegacyLevelScenePrefix}{hero}.tscn";
            }
            return LevelScenePaths.TryGetValue(level, out string path) ? path : "";
        }

        /// <summary>The hero whose Level 4A variant the campaign would route to right now.</summary>
        public static string ResolveLegacyHeroID(string heroCharacterID = null) {
            if (!string.IsNullOrWhiteSpace(heroCharacterID)) return heroCharacterID.ToLowerInvariant();
            string session = GameManager.Instance?.CurrentSession.SelectedCharacterID;
            if (!string.IsNullOrWhiteSpace(session)) return session.ToLowerInvariant();
            StorySaveData save = Instance?.GetActiveSave();
            return string.IsNullOrWhiteSpace(save?.SelectedCharacterID)
                ? ""
                : save.SelectedCharacterID.ToLowerInvariant();
        }

        /// <summary>The 4A level ID for a hero (<c>level_04a_einstein</c>).</summary>
        public static string LegacyLevelID(string heroCharacterID) =>
            string.IsNullOrWhiteSpace(heroCharacterID)
                ? ""
                : LegacyLevelIDPrefix + heroCharacterID.ToLowerInvariant();

        public void CollectDust(int amount) {
            ChronalDustCollected += Mathf.Max(0, amount);
        }

        public void SetRewinds(int remaining) {
            ChronalRewindsRemaining = Mathf.Max(0, remaining);
        }

        /// <summary>
        /// Overwrites the undeposited wallet — the V7.2 Restart Level path,
        /// which clears everything earned in the level ("the attempt never
        /// happened") before the scene reloads from the beginning.
        /// </summary>
        public void SetDust(int amount) {
            ChronalDustCollected = Mathf.Max(0, amount);
        }

        // === Restoration Fonts (V7.2 healing loop) ==========================
        // A font's spent state persists through rewinds and Timeline Collapse —
        // it cannot be refilled by dying — so the consumed-use registry lives
        // here, outside the level scene. It resets only on a full Restart
        // Level, a fresh level entry, or a campaign reset.
        private readonly System.Collections.Generic.Dictionary<string, int> _fontUsesConsumed = new();

        public int GetFontUsesConsumed(string fontID) =>
            !string.IsNullOrWhiteSpace(fontID) && _fontUsesConsumed.TryGetValue(fontID, out int used) ? used : 0;

        public void RecordFontUse(string fontID) {
            if (string.IsNullOrWhiteSpace(fontID)) return;
            _fontUsesConsumed[fontID] = GetFontUsesConsumed(fontID) + 1;
        }

        public void ClearRestorationFonts() => _fontUsesConsumed.Clear();

        // === Boss intro ritual (V7) =========================================
        // The name-card/establishing-beat intro plays once per boss; repeat
        // attempts after a Timeline Collapse (or a restart) skip it. The seen
        // set lives here so it survives the level reload a collapse causes.
        private readonly System.Collections.Generic.HashSet<string> _bossIntrosSeen = new();

        public bool HasSeenBossIntro(string bossID) =>
            !string.IsNullOrWhiteSpace(bossID) && _bossIntrosSeen.Contains(bossID);

        public void RecordBossIntroSeen(string bossID) {
            if (!string.IsNullOrWhiteSpace(bossID)) _bossIntrosSeen.Add(bossID);
        }

        public void ApplyTimelineCollapseDustPenalty() {
            int before = ChronalDustCollected;
            ChronalDustCollected = CalculateTimelineCollapseDust(ChronalDustCollected);
            // Package 11 A10 (F05): the 20% fee is a LOSS, itemised on the
            // results overlay of a run that survives to completion after an
            // Anchor Snap. It never unclaims a collected source — lost dust
            // cannot be recovered by killing the same source again.
            FTT.Environment.LevelRewardDirectory.RecordDustLoss(before - ChronalDustCollected);
        }

        public static int CalculateTimelineCollapseDust(int carriedDust) =>
            Mathf.FloorToInt(Mathf.Max(0, carriedDust) * 0.8f);

        /// <summary>
        /// F11 recovery minima. The granted gauge is the larger of what the
        /// anchor banked and the anchor's authored minimum
        /// (<c>max(25, ceil(allAliveRate x remainingRoute x 1.20))</c>).
        /// A budget above 100 throws out of <see cref="TimelineIntegrityRules.RecoveryMinimum"/>
        /// — an invalid content budget is reported loudly, never clamped; the
        /// collapse then falls back to the banked checkpoint gauge so the game
        /// is still playable while the authoring is fixed.
        /// </summary>
        private void ResolveTimerRecoveryIntegrity(Difficulty difficulty) {
            float banked = Mathf.Clamp(CheckpointIntegrityPercent, 0f, TimelineIntegrityRules.StartPercent);
            float par = ParSecondsForCurrentLevel;
            if (par <= 0f) {
                TimelineIntegrityPercent = banked;
                return;
            }
            float route = GetRecoveryRouteSeconds(CollapsedCheckpointID);
            try {
                TimelineIntegrityPercent =
                    TimelineIntegrityRules.TimerRecoveryIntegrity(banked, par, difficulty, route);
            } catch (System.Exception exception) {
                GD.PushError(
                    $"Invalid F11 recovery budget for '{CollapsedCheckpointID}' on {CurrentLevel}: {exception.Message}");
                TimelineIntegrityPercent = banked;
            }
            CheckpointIntegrityPercent = TimelineIntegrityPercent;
            IsPreBossLocked = false;
            _integrityCollapseFired = false;
            PublishIntegrity();
        }

        public void BeginTimelineCollapse(string checkpointID, TimelineCollapseCause cause = TimelineCollapseCause.Death) {
            // Package 11 A3b (V7.6 Act III Gauntlet): the Wardens cannot reach
            // past the Void, so there is no hub extraction in Levels 13-15. The
            // SAME chokepoint serves both triggers the design names — an
            // unprevented lethal event with no death-rewind charge (which
            // ChronalRewindManager routes here) and the Resonance Hold reaching
            // zero — so neither can bypass the anchor accounting.
            if (IsActIIILevel(CurrentLevel)) {
                ResolveActIIIFailure(checkpointID, cause);
                return;
            }
            CollapsedLevel = CurrentLevel;
            CollapsedCheckpointID = checkpointID ?? "";
            PendingCollapseCause = cause;
            HasPendingTimelineRestart = true;
            // Package 12 W1 (R03): this session's resume-from-anchor earns the
            // Post-Landing Hold. The flag lives in memory only, so a quit and a
            // reload before the portal resume never grants one (plan D3(a)).
            _collapseRecoveryHoldEligible = true;
            ApplyTimelineCollapseDustPenalty();

            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            // F11: ONLY a timer-caused Collapse that actually grants recovery
            // applies the authored minimum. Ordinary load and the death rewind
            // never do. The resolved value is written into the attempt's
            // Integrity before the save, so an interrupted recovery loads the
            // already-resolved result and never grants twice.
            if (cause == TimelineCollapseCause.Timer) ResolveTimerRecoveryIntegrity(difficulty);
            StorySaveData save = GetActiveSave();
            if (save != null) {
                save.LevelChronalDust = ChronalDustCollected;
                save.CurrentLives = ChronalRewindsRemaining;
                save.CurrentHP = GetSelectedCharacterMaximumHP();
                if (!string.IsNullOrWhiteSpace(CollapsedCheckpointID)) save.LastCheckpointID = CollapsedCheckpointID;
                // A collapse resume-from-anchor keeps the attempt's registries
                // (checkpoints stay Mended-out, extractors stay broken).
                // Acts I-II Collapse is a PAID RECOVERY inside the SAME attempt,
                // not a fresh one: the hub portal finishes it (F10).
                _attempt.Status = StoryAttemptStatus.AwaitingHubResume;
                _attempt.RecoveryEvent = BuildRecoveryEvent(
                    StoryRecoveryCause.Collapse, CollapsedCheckpointID, difficulty, feeCharged: true);
                _attempt.Bump();
                WriteAttemptStateToSave(save);
                SaveManager.Instance.SaveStorySlot(GameManager.Instance.CurrentSession.ActiveSaveSlot);
            }
            ReturnToHub();
        }

        public bool RestartCollapsedLevel(bool resumeFromTimelineAnchor) {
            if (!HasPendingTimelineRestart) return false;
            CurrentLevel = CollapsedLevel;
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            // V7.3: resuming from the Timeline Anchor keeps the attempt's
            // per-attempt state; restarting from the beginning clears it.
            if (!resumeFromTimelineAnchor) {
                ClearLevelAttemptState();
                // H02 / F02: a full Restart Level clears the open attempt's
                // wallet. With no hub deposit any more, every undeposited point
                // belongs to this attempt, so a restart must not carry it into
                // the fresh one (that is what stops a repeated level banking
                // repeat rewards).
                SetDust(0);
            }
            StorySaveData save = GetActiveSave();
            if (save != null) {
                save.CurrentLevelID = GetCurrentLevelPath();
                save.LastCheckpointID = resumeFromTimelineAnchor ? CollapsedCheckpointID : "";
                save.CurrentHP = GetSelectedCharacterMaximumHP();
                save.CurrentLives = ChronalRewindsRemaining;
                save.LevelChronalDust = ChronalDustCollected;
                WriteAttemptStateToSave(save);
                SaveManager.Instance.SaveStorySlot(GameManager.Instance.CurrentSession.ActiveSaveSlot);
            }
            HasPendingTimelineRestart = false;
            bool holdEligible = resumeFromTimelineAnchor && _collapseRecoveryHoldEligible;
            _collapseRecoveryHoldEligible = false;
            if (holdEligible) ArmRecoveryPlacementHold(StoryRecoveryHoldCause.CollapseResume);
            LoadCurrentLevel();
            return true;
        }

        private StorySaveData GetActiveSave() {
            if (SaveManager.Instance == null || GameManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            return slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length
                ? SaveManager.Instance.SaveSlots[slot]
                : null;
        }

        /// <summary>
        /// The selected character's Story maximum HP, used to refill the save when
        /// a Timeline Collapse restarts a level. Prefers the live player, whose
        /// <c>MaximumHP</c> already folds in the Resonance MaxHP bonus; with no
        /// player in the tree it falls back to the authored resource (loaded
        /// through the pinning cache — a bare <c>GD.Load</c> here was the
        /// documented crash-class violation) plus the bonus resolved from the
        /// active save. The roster check keeps an unknown or empty character ID
        /// from fabricating a resource path.
        /// </summary>
        public int GetSelectedCharacterMaximumHP() {
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                return player.MaximumHP;
            }
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "";
            if (!FTT.Characters.CharacterFactory.IsKnownCharacter(characterID)) return 100;
            FTT.Characters.CharacterData data = AuthoredResources.Load<FTT.Characters.CharacterData>(
                $"res://resources/Characters/{characterID}_data.tres");
            int resonanceBonus =
                FTT.Environment.ResonanceProgression.TryResolveActive(characterID, out FTT.Environment.StoryStatProfile profile)
                    ? profile.MaxHPBonus
                    : 0;
            return (data?.MaxHP ?? 100) + resonanceBonus;
        }

        public int DepositDustToActiveSave() {
            if (SaveManager.Instance == null || GameManager.Instance == null || ChronalDustCollected <= 0) return 0;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return 0;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            string characterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "";
            int deposited = FTT.Environment.ResonanceProgression.DepositActiveDust(save, characterID, ChronalDustCollected);
            if (deposited <= 0) return 0;
            ChronalDustCollected -= deposited;
            // V7.3 dust-dup fix: the deposit and the wallet-zero must persist
            // in ONE atomic envelope write. The save's undeposited wallet was
            // never updated here, so a process kill after the deposit landed
            // re-materialized the deposited dust into the wallet on relaunch.
            save.LevelChronalDust = ChronalDustCollected;
            EventBus.Instance?.RaiseChronalDustDeposited(
                save.DepositedChronalDust.GetValueOrDefault(characterID));
            SaveManager.Instance.SaveStorySlot(slot);
            return deposited;
        }

        private void OnDustCollected(int amount) {
            CollectDust(amount);
        }

        private void OnLevelComplete(string levelID) {
            CompleteLevelRun();
            // === Package 11 A10 region: the F05 Integrity tier bonus ==========
            // Inside the completion transaction, after CompleteLevelRun has
            // frozen LastLevelIntegrityPercent and before the save write, so the
            // same envelope that records the result also persists the bonus.
            ApplyIntegrityTierBonus();
            // === end Package 11 A10 region ===
            // === Package 11 A3b region: the Act III completion transaction ===
            // F02: the active level's earnings are undeposited until completion,
            // when the auto-deposit fires ONCE, "sealed through the Warden
            // Beacon". Level 13's dust becomes spendable at Level 14's Beacon.
            // Acts I-II keep the hub Repository as their banking event.
            CommitCompletionTransaction(levelID);
            // === end Package 11 A3b region ===
            // === Package 11 A5 region: the Legacy Unlock milestone grant ===
            // Inside the completion transaction and BEFORE
            // RecordLevelResultToSave, so the same save write that records the
            // result also persists the restored slot.
            GrantLegacyUnlockMilestone(levelID);
            // === end Package 11 A5 region ===
            RecordLevelResultToSave(levelID);
            AdvanceToNextLevel();
        }

        // === Package 11 A10 region: the F05 Integrity tier bonus ============

        /// <summary>
        /// The Integrity tier bonus paid by the most recent successful level
        /// completion (results itemisation). Zero on an untimed level.
        /// </summary>
        public int LastLevelTierBonusDust { get; private set; }

        /// <summary>
        /// F05: <c>bonus = floor(retainedBaseDust x tierRate)</c> at Restored
        /// >=50% 10% / Stabilized >=20% 5% / Fractured &lt;20% 0%, calculated
        /// <b>exactly once</b> on successful completion.
        ///
        /// <para><c>retainedBaseDust</c> is this level's collected, undeposited
        /// base dust remaining after any applied loss penalties — it is the live
        /// level wallet, which by construction excludes deposited dust, respec
        /// refunds and any prior tier bonus. The bonus is added to that wallet
        /// and banks with it; <b>no compounding, no checkpoint payout, and
        /// nothing for a failed attempt</b>.</para>
        ///
        /// <para><b>An untimed level pays nothing</b> — Level 1 has no Integrity
        /// clock, which is precisely why the all-Restored maxima are 787
        /// required and 1,094 thorough rather than 792 / 1,100.</para>
        /// </summary>
        private void ApplyIntegrityTierBonus() {
            LastLevelTierBonusDust = 0;
            FTT.Environment.LevelRewardDirectory.LastTierBonusDust = 0;
            bool levelIsTimed = ParSecondsForCurrentLevel > 0f;
            int bonus = FTT.Environment.RewardAllocator.TierBonus(
                ChronalDustCollected, LastLevelIntegrityPercent, levelIsTimed);
            if (bonus <= 0) return;
            LastLevelTierBonusDust = bonus;
            FTT.Environment.LevelRewardDirectory.LastTierBonusDust = bonus;
            // CollectDust rather than the wallet event: the level controllers
            // tally OnChronalDustCollected into their results total, and the
            // bonus is rendered as its own line on top of that total.
            CollectDust(bonus);
            StorySaveData save = GetActiveSave();
            if (save != null) save.LevelChronalDust = ChronalDustCollected;
        }

        // === end Package 11 A10 region ===

        // === Package 11 A5 region: V7.5 Legacy Unlock Schedule ==============

        /// <summary>
        /// The ability slot the most recent level completion restored, or null.
        /// Read by <c>LevelResultsPanel</c> for the "Resonance Restored" beat.
        /// </summary>
        public AbilitySlot? LastLegacyUnlockSlot { get; private set; }

        /// <summary>
        /// True when the results overlay for the level just completed should play
        /// the Resonance Restored beat — for <b>every</b> milestone slot.
        ///
        /// <para>Package 12 W7 (design §2, Level 4A "Results and economy",
        /// Low-item decision 2026-09-26): the Ultimate's beat plays on Level 4's
        /// results screen like every other unlock. The Package 11 A5 exclusion,
        /// which deferred it to 4A entry through a consume-once hand-off that no
        /// scene ever consumed, is removed. Level 4A instead opens on a flavour
        /// Nexus moment (<c>LegacyLevelControllerBase.NexusMomentDialogueID</c>)
        /// that grants nothing and is not a second unlock beat.</para>
        /// </summary>
        public bool ShouldPlayResonanceRestoredOnResults => LastLegacyUnlockSlot.HasValue;

        /// <summary>
        /// Grants the milestone slot for a completed level onto the active save's
        /// per-character unlock list, and backfills anything an older payload is
        /// missing. Idempotent — replaying a level never double-grants, and a
        /// Restart Level never removes an earned slot.
        /// </summary>
        /// <summary>
        /// Test seam: runs the milestone grant alone, without the surrounding
        /// completion transaction's timer freeze, save write and level advance.
        /// </summary>
        internal void GrantLegacyUnlockMilestoneForTests(string levelID) =>
            GrantLegacyUnlockMilestone(levelID);

        private void GrantLegacyUnlockMilestone(string levelID) {
            LastLegacyUnlockSlot = null;
            var saveManager = SaveManager.Instance;
            var gameManager = GameManager.Instance;
            if (saveManager == null || gameManager == null) return;
            int slotIndex = gameManager.CurrentSession.ActiveSaveSlot;
            if (slotIndex < 0 || slotIndex >= saveManager.SaveSlots.Length) return;
            StorySaveData save = saveManager.SaveSlots[slotIndex];
            if (save == null) return;

            string characterID = save.SelectedCharacterID;
            if (string.IsNullOrWhiteSpace(characterID)) {
                characterID = gameManager.CurrentSession.SelectedCharacterID ?? "";
            }
            if (string.IsNullOrWhiteSpace(characterID)) return;

            save.UnlockedLegacyAbilities ??= new System.Collections.Generic.Dictionary<
                string, System.Collections.Generic.List<string>>();
            if (!save.UnlockedLegacyAbilities.TryGetValue(
                    characterID, out System.Collections.Generic.List<string> granted) || granted == null) {
                granted = new System.Collections.Generic.List<string>();
                save.UnlockedLegacyAbilities[characterID] = granted;
            }

            // Backfill first: a save written before this schedule existed, or
            // one whose milestone write was lost, catches up from its own
            // completed-level history without granting a beat for it.
            foreach (string key in LegacyUnlockSchedule.UnlockedKeysFor(save.CompletedLevels)) {
                if (!granted.Contains(key)) granted.Add(key);
            }

            if (!LegacyUnlockSchedule.TryGrantedSlotForLevel(levelID, out AbilitySlot milestone)) return;
            string milestoneKey = LegacyUnlockSchedule.SlotKey(milestone);
            bool alreadyHeld = granted.Contains(milestoneKey);
            if (!alreadyHeld) granted.Add(milestoneKey);
            if (alreadyHeld) return;

            LastLegacyUnlockSlot = milestone;
            EventBus.Instance?.RaiseAbilitySlotLockChanged(new AbilitySlotLockPayload {
                Slot = milestone,
                State = AbilitySlotLockState.Clear
            });
        }

        // === end Package 11 A5 region ===

        /// <summary>
        /// Records the completed level's Timeline Integrity, secrets, and
        /// Chronal Rating on the active save slot (V7/V7.1: per-level records
        /// feed the campaign-ending average and the save-select display).
        /// </summary>
        private void RecordLevelResultToSave(string levelID) {
            var saveManager = SaveManager.Instance;
            var gameManager = GameManager.Instance;
            if (saveManager == null || gameManager == null || string.IsNullOrWhiteSpace(levelID)) return;
            int slot = gameManager.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= saveManager.SaveSlots.Length || saveManager.SaveSlots[slot] == null) return;
            StorySaveData save = saveManager.SaveSlots[slot];
            // Package 11 A3b (N05): the recorded contribution is the gauge
            // LOCKED at the PreBoss fracture, not a value re-sampled after the
            // boss. They agree today because the lock freezes the clock — but
            // the locked figure is the authored one, so it is what is stored.
            save.IntegrityByLevel[levelID] = _attempt.FinalGateIntegrity >= 0f
                ? _attempt.FinalGateIntegrity
                : LastLevelIntegrityPercent;
            // RatingByLevel is deliberately no longer written (ruling 2.A). The
            // field survives in the payload as dead data through schema v6 -- an
            // older save keeps whatever rating it already recorded.
            save.SecretsFoundByLevel[levelID] = LastLevelSecretsFound;
            saveManager.SaveStorySlot(slot);
        }

        /// <summary>
        /// Counts a spent rewind. The remaining pool cannot be differenced for this
        /// — Easy and Normal refill it at checkpoints — so the count is taken from
        /// the rewind event itself.
        /// </summary>
        private void OnRewindTriggered(Vector2 targetPosition) {
            if (IsLevelTimerRunning) LevelRewindsUsed++;
        }

        // ====================================================================
        // === Package 11 A3b — the Act III Gauntlet and the F10 attempt ======
        // ====================================================================

        /// <summary>The authored Game Over surface. Only Smothered reaches it.</summary>
        public const string GameOverScenePath = "res://scenes/ui/GameOver.tscn";

        /// <summary>
        /// A3's <see cref="ActIIICollapseOverride"/>, filled by A3b. The Act III
        /// clock reaching zero still earns its beat — the design authors the
        /// collapse as the Smothered sequence, and in Act III "the same beat
        /// plays and no rift comes" — so the fracture presentation runs first and
        /// <see cref="BeginTimelineCollapse"/> then routes to
        /// <see cref="ResolveActIIIFailure"/>. With no rewind stack in the scene
        /// (a bare test fixture) the failure resolves directly.
        /// </summary>
        /// <summary>
        /// Sarah, over an Anchor Snap: <i>"It nearly had you. Your anchor held —
        /// get up."</i> The Snap's own presentation is the collapse fracture
        /// reassembling at the anchor rather than the era falling to monochrome.
        /// </summary>
        public const string AnchorSnapLineKey = "anchor_snap_sarah_line";

        /// <summary>
        /// Surfaced when a migrated save cannot have its attempt history
        /// reconstructed. The save and every balance are preserved; the player is
        /// told an explicit Restart Level is needed. F10 forbids silently
        /// restarting, granting resources, or claiming the legacy state was
        /// recovered.
        /// </summary>
        public const string LegacyRecoveryNoticeKey = "save_notice_legacy_recovery_required";

        /// <summary>
        /// The notice the next surface should show about the loaded attempt, or
        /// "". Set on a load that resolved to
        /// <see cref="StoryAttemptStatus.LegacyRecoveryRequired"/>.
        /// </summary>
        public string PendingAttemptNoticeKey { get; private set; } = "";

        private void RunActIIICollapseBeat() {
            if (GetTree()?.GetFirstNodeInGroup(FTT.Environment.ChronalRewindManager.ManagerGroup)
                is FTT.Environment.ChronalRewindManager manager) {
                manager.BeginTimerCollapse();
                return;
            }
            ResolveActIIIFailure(CollapsedCheckpointID, TimelineCollapseCause.Timer);
        }

        /// <summary>
        /// The stable authored level ID for every shared campaign slot. Level 4A
        /// is absent because its ID is per hero
        /// (<see cref="LegacyLevelID"/>); Level 0 and 1 are present but untimed,
        /// so N05 excludes them explicitly rather than by omission.
        /// </summary>
        private static readonly Dictionary<CampaignLevel, string> LevelIDs = new() {
            { CampaignLevel.Tutorial, "level_00_tutorial" },
            { CampaignLevel.Florence, "level_01_florence" },
            { CampaignLevel.Orleans, "level_02_orleans" },
            { CampaignLevel.Chicago, "level_03_chicago" },
            { CampaignLevel.Paris, "level_04_paris" },
            { CampaignLevel.Titanic, "level_05_titanic" },
            { CampaignLevel.Pompeii, "level_06_pompeii" },
            { CampaignLevel.Nassau, "level_07_nassau" },
            { CampaignLevel.Egypt, "level_08_egypt" },
            { CampaignLevel.Berlin, "level_09_berlin" },
            { CampaignLevel.London, "level_10_globe" },
            { CampaignLevel.Gettysburg, "level_11_gettysburg" },
            { CampaignLevel.Lunar, "level_12_lunar" },
            { CampaignLevel.ChronalVoid, "level_13_chronal_void" },
            { CampaignLevel.NeoEarth, "level_14_neo_earth" },
            { CampaignLevel.Alexandria, "level_15_alexandria" },
        };

        /// <summary>The authored level ID for a campaign slot ("" for Level 4A without a hero).</summary>
        public static string GetLevelID(CampaignLevel level, string heroCharacterID = null) =>
            level == CampaignLevel.LegacyNexus
                ? LegacyLevelID(ResolveLegacyHeroID(heroCharacterID))
                : LevelIDs.TryGetValue(level, out string id) ? id : "";

        /// <summary>
        /// The three Act III levels of the V7.6 Gauntlet.
        ///
        /// <para><b>Not</b> <c>(int)level &gt;= (int)ChronalVoid</c>: Level 4A was
        /// appended at enum value <b>16</b> so nothing renumbered, and that
        /// ordinal comparison classifies the Legacy Level — played between
        /// Levels 4 and 5 — as Act III. The set is explicit for exactly that
        /// reason.</para>
        /// </summary>
        public static bool IsActIIILevel(CampaignLevel level) =>
            level == CampaignLevel.ChronalVoid
            || level == CampaignLevel.NeoEarth
            || level == CampaignLevel.Alexandria;

        /// <summary>True while the run is inside the Act III gauntlet.</summary>
        public bool IsInActIII => IsActIIILevel(CurrentLevel);

        // === The attempt record (F10) ======================================

        private StoryAttemptState _attempt = new();

        /// <summary>Set by an Anchor Snap so the reconstruction keeps the same attempt.</summary>
        private bool _forceResumeNextRun;

        /// <summary>
        /// This run's F10 attempt record. Live authority; written into the save
        /// by <see cref="WriteAttemptStateToSave"/> and restored by
        /// <see cref="RestoreAttemptStateFromSave"/>.
        /// </summary>
        public StoryAttemptState CurrentAttempt => _attempt;

        /// <summary>
        /// True only inside a minted campaign attempt (BeginLevelRun / Restart Level
        /// / a resumed save). The default record carries no AttemptID, so a Test
        /// Arena match or a headless fixture that never entered a level is NOT a
        /// live attempt and must neither read nor write per-attempt state.
        /// </summary>
        public bool HasLiveAttempt => _attempt != null && !string.IsNullOrEmpty(_attempt.AttemptID);

        /// <summary>The attempt's status. Load routing reads this and nothing else.</summary>
        public StoryAttemptStatus AttemptStatus => _attempt.Status;

        /// <summary>
        /// F10: Story Defy History is once per <b>attempt</b>. Death rewind,
        /// Collapse, Snap, a hub visit and loading never reset it; only fresh
        /// entry and an explicit full Restart Level do.
        /// </summary>
        public bool DefyHistoryUsed {
            get => _attempt.DefyHistoryUsed;
            set {
                if (_attempt.DefyHistoryUsed == value) return;
                _attempt.DefyHistoryUsed = value;
                _attempt.Bump();
                CommitCriticalEvent();
            }
        }

        private string CurrentLevelIDForAttempt() => GetLevelID(CurrentLevel);

        /// <summary>
        /// Mints a new attempt. Exactly two callers: fresh entry into a level
        /// (<see cref="BeginLevelRun"/> on the non-resume branch) and an explicit
        /// full Restart Level (<see cref="RestartLevelAttempt"/>, which routes
        /// through the same branch). Nothing else may create an attempt ID — not
        /// a scene reload, a hub return, a checkpoint, a death, or a Snap.
        /// </summary>
        private void MintFreshAttempt() {
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            int anchors = IsActIIILevel(CurrentLevel) ? AnchorChargesFor(difficulty) : 0;
            _attempt = StoryAttemptState.CreateFresh(CurrentLevelIDForAttempt(), anchors);
            AnchorChargesRemaining = anchors;
            AnchorChargeMaximum = anchors;
            PublishAnchorCharges();
        }

        /// <summary>
        /// Test/UI seam: the explicit full Restart Level. Discards the attempt's
        /// claims, uses, checkpoints and pending recovery, keeps deposited dust,
        /// the grid, completed levels and Legacy unlocks, and mints a new ID.
        /// The active wallet is the caller's to clear (the pause menu and the
        /// Game Over screen both do, under the existing Restart rule).
        /// </summary>
        public void RestartLevelAttempt() {
            ClearLevelAttemptState();
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            MintFreshAttempt();
        }

        // === Anchor charges (V7.6 Act III) ==================================

        /// <summary>Anchor charges left on the Warden Beacon. Always zero outside Act III.</summary>
        public int AnchorChargesRemaining { get; private set; }

        /// <summary>The cap this attempt initialized with (Easy 3 / Normal 2 / Hard 1).</summary>
        public int AnchorChargeMaximum { get; private set; }

        /// <summary>
        /// The Unified Difficulty Scaling row: <b>Easy 3 / Normal 2 / Hard 1</b>
        /// anchor charges per Act III level.
        /// </summary>
        public static int AnchorChargesFor(Difficulty difficulty) => difficulty switch {
            Difficulty.Easy => 3,
            Difficulty.Hard => 1,
            _ => 2
        };

        /// <summary>
        /// Spends one charge for an Anchor Snap. <b>1 → 0 still completes the
        /// Snap</b> — zero remaining is a living state, not a failure. Returns
        /// false only when there was nothing to spend, which is the Smothered
        /// branch.
        /// </summary>
        public bool SpendAnchorCharge() {
            if (AnchorChargesRemaining <= 0) return false;
            AnchorChargesRemaining--;
            _attempt.AnchorChargesRemaining = AnchorChargesRemaining;
            _attempt.Bump();
            PublishAnchorCharges();
            return true;
        }

        private void PublishAnchorCharges() => EventBus.Instance?.RaiseAnchorChargesChanged(
            new AnchorChargesPayload { Charges = AnchorChargesRemaining, Max = AnchorChargeMaximum });

        // === Anchor Snap and Smothered ======================================

        /// <summary>
        /// The Act III branch of every failure. Named by the design as replacing
        /// "Timeline Collapse's hub extraction": with a charge left it is an
        /// <b>Anchor Snap</b> back to the last activated checkpoint in place;
        /// with none it is <b>Smothered</b>, the game's only Game Over.
        /// </summary>
        public void ResolveActIIIFailure(string checkpointID, TimelineCollapseCause cause) {
            if (AnchorChargesRemaining > 0) BeginAnchorSnap(checkpointID, cause);
            else EnterSmothered(checkpointID, cause);
        }

        /// <summary>
        /// V7.6 Anchor Snap. The same priced recovery ladder as an Acts I–II
        /// Collapse — full HP, the full difficulty rewind pool, the <b>same 20%
        /// undeposited-dust fee</b>, the same Integrity allowance (checkpoint
        /// gauge for a non-timer cause, <c>max(checkpoint, F11 minimum)</c> for a
        /// timer cause) — with two differences: it spends one anchor charge, and
        /// the hero snaps back <i>in place</i> instead of being extracted to the
        /// Time-Ship. The attempt is unchanged; no new attempt ID is minted.
        /// </summary>
        public void BeginAnchorSnap(string checkpointID, TimelineCollapseCause cause = TimelineCollapseCause.Death) {
            CollapsedLevel = CurrentLevel;
            CollapsedCheckpointID = checkpointID ?? "";
            PendingCollapseCause = cause;
            HasPendingTimelineRestart = false;
            ApplyTimelineCollapseDustPenalty();
            SpendAnchorCharge();

            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            if (cause == TimelineCollapseCause.Timer) ResolveTimerRecoveryIntegrity(difficulty);

            // F10: the outcome is resolved and COMMITTED before the presentation.
            // A crash during the snap beat resumes the settled result and never
            // re-spends the anchor or re-charges the fee.
            _attempt.Status = StoryAttemptStatus.RecoveryPending;
            _attempt.RecoveryEvent = BuildRecoveryEvent(
                StoryRecoveryCause.AnchorSnap, CollapsedCheckpointID, difficulty, feeCharged: true);
            _attempt.PendingHealing.Clear();   // a death recovery cancels pending healing, no refund
            _attempt.Bump();
            PersistAttempt(save => {
                save.LevelChronalDust = ChronalDustCollected;
                save.CurrentLives = ChronalRewindsRemaining;
                save.CurrentHP = GetSelectedCharacterMaximumHP();
                if (!string.IsNullOrWhiteSpace(CollapsedCheckpointID)) save.LastCheckpointID = CollapsedCheckpointID;
            });

            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is Node2D hero) {
                FTT.Environment.EnvironmentNotice.Post(
                    AnchorSnapLineKey, hero, 3f, new Color(0.96f, 0.78f, 0.32f));
            }
            // "In place": the level is reconstructed at its last activated
            // anchor. No portal trip, no hub, same attempt.
            _attempt.Status = StoryAttemptStatus.Active;
            _attempt.RecoveryEvent.Applied = true;
            _attempt.Bump();
            _forceResumeNextRun = true;
            // Package 12 W1 (R03): the in-place reconstruction ends with the
            // Post-Landing Hold. Transient: never written to the save.
            ArmRecoveryPlacementHold(StoryRecoveryHoldCause.AnchorSnap);
            LoadCurrentLevel();
        }

        // === Package 12 W1 region: the transient R03 placement hold =========
        // The Collapse resume and the Anchor Snap both reconstruct the level
        // through a scene load, so the hold cannot start in the code that
        // decided on the recovery. It is armed here, in memory only, stamped
        // with the scene it belongs to, and consumed by that scene's
        // ChronalRewindManager on its first tick. Nothing here is persisted.

        private StoryRecoveryHoldCause? _pendingPlacementHold;
        private string _pendingPlacementHoldScene = "";
        private bool _collapseRecoveryHoldEligible;

        /// <summary>True while a placement hold is armed for the next level scene. Test seam.</summary>
        public bool HasPendingRecoveryPlacementHold => _pendingPlacementHold.HasValue;

        /// <summary>The armed cause, or null. Test seam.</summary>
        public StoryRecoveryHoldCause? PendingRecoveryPlacementHold => _pendingPlacementHold;

        private void ArmRecoveryPlacementHold(StoryRecoveryHoldCause cause) {
            _pendingPlacementHold = cause;
            _pendingPlacementHoldScene = GetCurrentLevelPath() ?? "";
        }

        /// <summary>
        /// Consumes the armed hold when <paramref name="scenePath"/> is the scene
        /// it was armed for. A mismatched scene leaves it armed (the level may not
        /// have finished loading); a fresh entry, a restart or a save load clears it.
        /// </summary>
        public bool TryConsumeRecoveryPlacementHold(string scenePath, out StoryRecoveryHoldCause cause) {
            cause = default;
            if (!_pendingPlacementHold.HasValue) return false;
            if (string.IsNullOrEmpty(scenePath) || scenePath != _pendingPlacementHoldScene) return false;
            cause = _pendingPlacementHold.Value;
            ClearRecoveryPlacementHold();
            return true;
        }

        /// <summary>Drops any armed hold and the in-session Collapse eligibility.</summary>
        public void ClearRecoveryPlacementHold() {
            _pendingPlacementHold = null;
            _pendingPlacementHoldScene = "";
            _collapseRecoveryHoldEligible = false;
        }
        // === end Package 12 W1 region ===

        /// <summary>
        /// V7.6 Smothered — a collapse with no anchor charge left, and the only
        /// Game Over in the game.
        ///
        /// <para>F10 is explicit that this terminal state <b>persists before its
        /// presentation</b>: quitting and loading return to this same Game Over
        /// without refilling HP or charges and without replaying the failure fee,
        /// and only an explicit Restart Level starts a fresh attempt. Deposited
        /// dust — including earnings from previously completed Act III levels
        /// sealed through the Beacon — is never touched.</para>
        /// </summary>
        public void EnterSmothered(string checkpointID, TimelineCollapseCause cause = TimelineCollapseCause.Death) {
            CollapsedLevel = CurrentLevel;
            CollapsedCheckpointID = checkpointID ?? "";
            PendingCollapseCause = cause;
            HasPendingTimelineRestart = false;
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;

            bool alreadySettled = _attempt.Status == StoryAttemptStatus.Smothered;
            if (!alreadySettled) {
                // The triggering collapse is ONE failure event: its fee is
                // charged here, once. Reopening the Game Over screen never
                // repeats it.
                ApplyTimelineCollapseDustPenalty();
                _attempt.RecoveryEvent = BuildRecoveryEvent(
                    StoryRecoveryCause.Smothered, CollapsedCheckpointID, difficulty, feeCharged: true);
                _attempt.RecoveryEvent.Applied = true;
                _attempt.Status = StoryAttemptStatus.Smothered;
                _attempt.Bump();
                PersistAttempt(save => {
                    save.LevelChronalDust = ChronalDustCollected;
                    if (!string.IsNullOrWhiteSpace(CollapsedCheckpointID)) save.LastCheckpointID = CollapsedCheckpointID;
                });
            }
            StopLevelRun();
            LoadGameOverScreen();
        }

        private void LoadGameOverScreen() => RequestScene(GameOverScenePath);

        /// <summary>
        /// Leaves the Smothered Game Over by the only door F10 opens: a full
        /// Restart Level. Standard Restart rules — every point of undeposited
        /// level dust is cleared, anchor charges refill, a new attempt ID is
        /// minted — while deposited dust, the grid, completed levels and Legacy
        /// unlocks all survive.
        /// </summary>
        public void RestartFromGameOver() {
            CurrentLevel = CollapsedLevel;
            SetDust(0);
            // ClearLevelAttemptState rather than RestartLevelAttempt: the mint
            // belongs to the level load below, so a Restart produces exactly ONE
            // fresh attempt rather than two in a row.
            ClearLevelAttemptState();
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
            PersistAttempt(save => {
                save.CurrentLevelID = GetCurrentLevelPath();
                save.LastCheckpointID = "";
                save.LevelChronalDust = 0;
                save.CurrentLives = ChronalRewindsRemaining;
                save.CurrentHP = GetSelectedCharacterMaximumHP();
            });
            LoadCurrentLevel();
        }

        private StoryRecoveryEvent BuildRecoveryEvent(
            StoryRecoveryCause cause, string checkpointID, Difficulty difficulty, bool feeCharged) => new() {
                EventID = System.Guid.NewGuid().ToString("N"),
                Cause = cause,
                CheckpointID = checkpointID ?? "",
                CheckpointRole = (int)GetCheckpointRole(checkpointID),
                Difficulty = difficulty,
                ResolvedHP = GetSelectedCharacterMaximumHP(),
                ResolvedRewinds = ChronalRewindsRemaining,
                ResolvedAnchorCharges = AnchorChargesRemaining,
                ResolvedWalletDust = ChronalDustCollected,
                ResolvedIntegrityPercent = TimelineIntegrityPercent,
                ResolvedRecoveryMinimum = PendingCollapseCause == TimelineCollapseCause.Timer
                    ? TimelineIntegrityPercent
                    : 0f,
                FeeCharged = feeCharged
            };

        // === Completion transaction and the Act III auto-deposit =============

        /// <summary>
        /// Commits the level's completion transaction once, before any results
        /// overlay or dialogue. <b>This is the only banking event in the game</b>
        /// (H02, 2026-09-26; Package 12 W2): every level — Acts I–II as well as
        /// the Act III gauntlet — deposits its retained wallet and tier bonus
        /// here, exactly once. The hub Repository no longer deposits anything; a
        /// hub return after a Collapse or a voluntary exit leaves the open
        /// attempt's remaining dust in <c>LevelChronalDust</c> until that level
        /// is completed, and Restart Level clears it. In Act III the deposit is
        /// "sealed through the Warden Beacon", which is what makes Level 13's
        /// dust spendable at Level 14's Beacon (F02).
        /// </summary>
        private void CommitCompletionTransaction(string levelID) {
            _attempt.CompletionTransaction = new StoryCompletionTransaction {
                TransactionID = System.Guid.NewGuid().ToString("N"),
                RetainedBaseDust = ChronalDustCollected,
                FinalIntegrityPercent = _attempt.FinalGateIntegrity >= 0f
                    ? _attempt.FinalGateIntegrity
                    : LastLevelIntegrityPercent,
                Sealed = true,
                Destination = IsActIIILevel(CurrentLevel) ? "next_level" : "hub"
            };
            _attempt.Status = StoryAttemptStatus.CompletionPending;
            _attempt.Bump();
            // H02: every level banks here, and only here.
            int deposited = DepositDustToActiveSave();
            _attempt.CompletionTransaction.DepositAmount = deposited;
            _attempt.CompletionTransaction.TierBonusDust = LastLevelTierBonusDust;
            LastCompletionDepositDust = deposited;
            _completionDepositNoticePending = deposited > 0;
            _attempt.CompletionTransaction.Applied = true;
            _attempt.Status = StoryAttemptStatus.Completed;
            _attempt.Bump();
        }

        // === Package 12 W2 region: H02 Repository ledger ===================

        /// <summary>
        /// Dust the most recent level-completion transaction banked, tier bonus
        /// included. The hub Repository shows it as the "last completed level"
        /// line of its deposit ledger. Session-scoped: a relaunch shows only the
        /// open attempt's held amount, never a fabricated figure.
        /// </summary>
        public int LastCompletionDepositDust { get; private set; }

        private bool _completionDepositNoticePending;

        /// <summary>
        /// Hands the hub its one arrival notice for the completion that just
        /// banked, exactly once. The notice reports a deposit that already
        /// happened; reading it deposits nothing.
        /// </summary>
        public bool TryConsumeCompletionDepositNotice(out int amount) {
            amount = LastCompletionDepositDust;
            if (!_completionDepositNoticePending || amount <= 0) return false;
            _completionDepositNoticePending = false;
            return true;
        }

        /// <summary>
        /// Undeposited dust held by the open attempt — the amount a Collapse or a
        /// voluntary exit left behind after its 20% fee. It banks only when that
        /// level is completed, and Restart Level clears it (H02, F02, F10).
        /// </summary>
        public int AttemptHeldDust => ChronalDustCollected;

        // === end Package 12 W2 region (H02 Repository ledger) ==============

        // === F05 reward-source claims (F10 persistence side) =================
        // "Issue once and commit the claim with the wallet/benefit; restore a
        // pending pickup once; clear on fresh/restart." A respawned enemy may
        // fight again but cannot reissue a claimed reward, and a random drop's
        // outcome is fixed at issue so a reload cannot reroll it.
        //
        // A10 owns the allocator and the spawn sites; this is the ledger they
        // commit through.

        /// <summary>The record for a stable reward source, created on first touch.</summary>
        public StoryRewardSourceRecord RewardSource(string sourceID) {
            if (string.IsNullOrWhiteSpace(sourceID)) return null;
            if (!_attempt.RewardSources.TryGetValue(sourceID, out StoryRewardSourceRecord record)) {
                record = new StoryRewardSourceRecord { SourceID = sourceID };
                _attempt.RewardSources[sourceID] = record;
            }
            return record;
        }

        /// <summary>
        /// Issues a source's reward once. Returns false when it has already been
        /// issued this attempt — which is what makes a respawned encounter
        /// harmless. <paramref name="fixedDropOutcome"/> pins a randomized result
        /// at issue time so a reload restores it rather than rerolling.
        /// </summary>
        public bool TryIssueReward(string sourceID, int quantity, string pickupType = "",
                                   Vector2 position = default, int fixedDropOutcome = -1) {
            StoryRewardSourceRecord record = RewardSource(sourceID);
            if (record == null || record.State != StoryRewardSourceState.Unissued) return false;
            record.State = StoryRewardSourceState.SpawnedUncollected;
            record.Quantity = Mathf.Max(0, quantity);
            record.PickupType = pickupType ?? "";
            record.PositionX = position.X;
            record.PositionY = position.Y;
            record.FixedDropOutcome = fixedDropOutcome;
            _attempt.Bump();
            return true;
        }

        /// <summary>
        /// Commits a claim. Returns false when the source was already collected,
        /// so a duplicate pickup callback pays nothing.
        /// </summary>
        public bool TryCollectReward(string sourceID) {
            StoryRewardSourceRecord record = RewardSource(sourceID);
            if (record == null || record.State == StoryRewardSourceState.Collected) return false;
            record.State = StoryRewardSourceState.Collected;
            _attempt.Bump();
            return true;
        }

        /// <summary>True once this source's reward has been collected this attempt.</summary>
        public bool IsRewardClaimed(string sourceID) =>
            !string.IsNullOrWhiteSpace(sourceID)
            && _attempt.RewardSources.TryGetValue(sourceID, out StoryRewardSourceRecord record)
            && record.State == StoryRewardSourceState.Collected;

        /// <summary>True when a pickup was spawned and never picked up — it restores once.</summary>
        public bool IsRewardPendingPickup(string sourceID) =>
            !string.IsNullOrWhiteSpace(sourceID)
            && _attempt.RewardSources.TryGetValue(sourceID, out StoryRewardSourceRecord record)
            && record.State == StoryRewardSourceState.SpawnedUncollected;

        // === Healing-loop persistence (F10 deltas) ===========================

        /// <summary>
        /// A Restoration Font channel completed: the use decrement and the
        /// pending heal commit <b>together</b>. A channel interrupted before it
        /// completes never reaches here, so it consumes no use and stores no heal.
        /// </summary>
        public void CommitFontChannelCompletion(string fontID, float healAmount, float durationSeconds) {
            if (string.IsNullOrWhiteSpace(fontID)) return;
            RecordFontUse(fontID);
            _attempt.PendingHealing.SourceID = fontID;
            _attempt.PendingHealing.UncreditedAmount = Mathf.Max(0f, healAmount);
            _attempt.PendingHealing.RemainingSeconds = Mathf.Max(0f, durationSeconds);
            _attempt.Bump();
            CommitCriticalEvent();
        }

        /// <summary>Credits part of a pending heal. Only the uncredited remainder survives a reload.</summary>
        public void CreditPendingHealing(float amount) {
            StoryPendingHealing pending = _attempt.PendingHealing;
            if (!pending.IsActive || amount <= 0f) return;
            pending.UncreditedAmount = Mathf.Max(0f, pending.UncreditedAmount - amount);
            if (pending.UncreditedAmount <= 0f) pending.Clear();
            _attempt.Bump();
        }

        /// <summary>The heal still owed to the player, or zero. Test seam.</summary>
        public float PendingHealingRemaining => _attempt.PendingHealing.UncreditedAmount;

        /// <summary>
        /// A death recovery that replaces HP cancels pending healing <b>without
        /// refunding the spent Font use</b> — the channel completed; the player
        /// simply did not live long enough to be paid.
        /// </summary>
        public void CancelPendingHealingForRecovery() {
            if (!_attempt.PendingHealing.IsActive) return;
            _attempt.PendingHealing.Clear();
            _attempt.Bump();
        }

        /// <summary>
        /// Records a consumed Chronal Feast / Salve pickup by its stable ID so a
        /// reload restores the claim rather than reissuing it. Returns false when
        /// it was already consumed this attempt.
        /// </summary>
        public bool TryConsumeHealingPickup(string pickupID) {
            if (string.IsNullOrWhiteSpace(pickupID)) return false;
            if (_attempt.CollectedHealingIDs.Contains(pickupID)) return false;
            _attempt.CollectedHealingIDs.Add(pickupID);
            _attempt.Bump();
            return true;
        }

        /// <summary>True once this healing pickup has been consumed this attempt.</summary>
        public bool IsHealingPickupConsumed(string pickupID) =>
            !string.IsNullOrWhiteSpace(pickupID) && _attempt.CollectedHealingIDs.Contains(pickupID);

        // === N05 ending selection ===========================================

        /// <summary>
        /// The clean-restoration threshold, in <b>summed percentage points</b>
        /// across the fifteen counted levels: 50% × 15 = 750. The comparison is
        /// deliberately against the unrounded sum, never a rounded average, so
        /// display rounding can never change the ending.
        /// </summary>
        public const float CleanEndingThresholdPoints = 750f;

        /// <summary>
        /// N05's counted set: <b>exactly fifteen</b> unique level IDs — the
        /// fourteen shared levels 2 through 15, plus the saved hero's <b>one</b>
        /// Level 4A. Untimed Levels 0 and 1 and the other eight 4A variants are
        /// excluded.
        /// </summary>
        public static List<string> RequiredEndingLevelIDs(string heroCharacterID) {
            var ids = new List<string>();
            foreach (CampaignLevel level in Route) {
                if (level == CampaignLevel.Tutorial || level == CampaignLevel.Florence) continue;
                if (level == CampaignLevel.LegacyNexus) {
                    string legacy = LegacyLevelID(ResolveLegacyHeroID(heroCharacterID));
                    if (!string.IsNullOrWhiteSpace(legacy)) ids.Add(legacy);
                    continue;
                }
                string id = GetLevelID(level);
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>
        /// Sums the recorded PreBoss-locked final Integrity across
        /// <see cref="RequiredEndingLevelIDs"/> at full stored precision. A
        /// missing record contributes zero — it cannot be guessed, and F10
        /// forbids a smaller denominator.
        /// </summary>
        public static float ResolveEndingPointTotal(StorySaveData save, string heroCharacterID) {
            if (save?.IntegrityByLevel == null) return 0f;
            float total = 0f;
            foreach (string id in RequiredEndingLevelIDs(heroCharacterID)) {
                if (save.IntegrityByLevel.TryGetValue(id, out float percent)) {
                    total += Mathf.Clamp(percent, 0f, TimelineIntegrityRules.StartPercent);
                }
            }
            return total;
        }

        /// <summary>
        /// True for the clean restoration ending. Exactly 750 points selects it;
        /// anything below selects the scarred ending, even where UI rounding
        /// would display 50%. Identical on every difficulty.
        /// </summary>
        public static bool IsCleanRestorationEnding(StorySaveData save, string heroCharacterID) =>
            ResolveEndingPointTotal(save, heroCharacterID) >= CleanEndingThresholdPoints;

        // === Durability: the ordered write and the per-second snapshot ======

        private const float DurableSnapshotIntervalSeconds = 1f;
        private float _durableSnapshotTimer;

        /// <summary>
        /// F10: HP, meter and resource timers change continuously and were not
        /// durable between checkpoints. Once per live second the live values are
        /// captured and queued through the ordered per-slot writer. A crash can
        /// still lose the last second — the contract says so explicitly — but not
        /// a whole room.
        /// </summary>
        private void TickDurableSnapshot(double delta) {
            if (!IsLevelTimerRunning || !_attempt.HasAttempt) return;
            if (_attempt.Status == StoryAttemptStatus.Smothered) return;
            _durableSnapshotTimer += (float)delta;
            if (_durableSnapshotTimer < DurableSnapshotIntervalSeconds) return;
            _durableSnapshotTimer = 0f;
            CaptureDurableSnapshot();
        }

        /// <summary>Captures live player resources into the attempt and queues one ordered write.</summary>
        internal void CaptureDurableSnapshot() {
            StorySaveData save = GetActiveSave();
            if (save == null) return;
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                save.CurrentHP = player.CurrentHP;
                save.CurrentUltimateMeter = player.CurrentUltimateMeter;
            }
            save.CurrentLives = ChronalRewindsRemaining;
            save.LevelChronalDust = ChronalDustCollected;
            _attempt.Bump();
            WriteAttemptStateToSave(save);
            SaveManager.Instance?.QueueStorySlotWrite(
                GameManager.Instance?.CurrentSession.ActiveSaveSlot ?? -1, _attempt.Revision);
        }

        /// <summary>
        /// Commits one critical event synchronously: the state is captured, the
        /// revision bumped, and the write must land before the event may be
        /// treated as safely saved. Returns false when the write failed — the
        /// caller must not present a transition as saved.
        /// </summary>
        private bool PersistAttempt(System.Action<StorySaveData> mutate = null) {
            StorySaveData save = GetActiveSave();
            if (save == null) return false;
            mutate?.Invoke(save);
            WriteAttemptStateToSave(save);
            int slot = GameManager.Instance?.CurrentSession.ActiveSaveSlot ?? -1;
            return SaveManager.Instance?.SaveStorySlot(slot) == true;
        }

        /// <summary>Public chokepoint for the F10 coupled-transaction autosave triggers.</summary>
        public bool CommitCriticalEvent() => PersistAttempt();
    }
}
