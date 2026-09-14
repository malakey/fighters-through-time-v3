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
        BossIntro = 8
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
        public override void _PhysicsProcess(double delta) => TickIntegrityClock(delta);

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
            TutorialComplete = save.CompletedLevels.Contains("level_00_tutorial");
            HasSeenCollapseBeat = save.HasSeenCollapseBeat;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ActiveSaveSlot = slot;
            session.SelectedCharacterID = save.SelectedCharacterID;
            session.Difficulty = save.Difficulty;
            GameManager.Instance.CurrentSession = session;
            // V7.3 quit-fee rule: the resumed session is live again.
            SessionExitGuard.WriteMarker(slot);
            ReturnToHub();
        }

        public void LoadCurrentLevel() {
            string path = GetCurrentLevelPath();
            if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path)) {
                BeginLevelRun(resumeAttempt: IsMidLevelResume(path));
                GameManager.Instance.LoadScene(path);
            } else {
                GD.PushError($"Campaign scene is not authored yet: {path}");
            }
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
            GameManager.Instance.LoadScene("res://scenes/campaign/HubWorld.tscn");
        }

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
            if (resumeAttempt) {
                RestoreAttemptStateFromSave(GetActiveSave());
                return;
            }
            ClearLevelAttemptState();
            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
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
            return _activatedCheckpoints.Add(checkpointID);
        }

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
        }

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
            CollapsedLevel = CurrentLevel;
            CollapsedCheckpointID = checkpointID ?? "";
            PendingCollapseCause = cause;
            HasPendingTimelineRestart = true;
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
            if (!resumeFromTimelineAnchor) ClearLevelAttemptState();
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
        /// the Resonance Restored beat. The Ultimate is deliberately excluded:
        /// Level 4A is the first full-kit level and V7.5 plays the Ultimate's
        /// beat on <b>4A entry</b>, not on Level 4's exit
        /// (<see cref="TryConsumeDeferredResonanceRestored"/>).
        /// </summary>
        public bool ShouldPlayResonanceRestoredOnResults =>
            LastLegacyUnlockSlot.HasValue && LastLegacyUnlockSlot.Value != AbilitySlot.Ultimate;

        private AbilitySlot? _deferredResonanceSlot;

        /// <summary>
        /// Hands the deferred Resonance Restored beat (the Ultimate) to Level 4A's
        /// entry, exactly once. Returns false everywhere else.
        /// </summary>
        public bool TryConsumeDeferredResonanceRestored(out AbilitySlot slot) {
            slot = AbilitySlot.Ultimate;
            if (!_deferredResonanceSlot.HasValue) return false;
            slot = _deferredResonanceSlot.Value;
            _deferredResonanceSlot = null;
            return true;
        }

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
            if (milestone == AbilitySlot.Ultimate) _deferredResonanceSlot = milestone;
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
            save.IntegrityByLevel[levelID] = LastLevelIntegrityPercent;
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
    }
}
