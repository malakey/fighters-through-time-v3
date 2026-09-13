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
        Alexandria = 15
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

        private static readonly string[] LevelScenePaths = {
            "res://scenes/campaign/Level_00_Tutorial.tscn",
            "res://scenes/campaign/Level_01_Florence.tscn",
            "res://scenes/campaign/Level_02_Orleans.tscn",
            "res://scenes/campaign/Level_03_Chicago.tscn",
            "res://scenes/campaign/Level_04_Paris.tscn",
            "res://scenes/campaign/Level_05_Titanic.tscn",
            "res://scenes/campaign/Level_06_Pompeii.tscn",
            "res://scenes/campaign/Level_07_Nassau.tscn",
            "res://scenes/campaign/Level_08_Egypt.tscn",
            "res://scenes/campaign/Level_09_Berlin.tscn",
            "res://scenes/campaign/Level_10_Globe.tscn",
            "res://scenes/campaign/Level_11_Gettysburg.tscn",
            "res://scenes/campaign/Level_12_Lunar.tscn",
            "res://scenes/campaign/Level_13_ChronalVoid.tscn",
            "res://scenes/campaign/Level_14_NeoEarth.tscn",
            "res://scenes/campaign/Level_15_Alexandria.tscn",
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
            int levelIndex = 0;
            for (int index = 0; index < LevelScenePaths.Length; index++) {
                if (LevelScenePaths[index] == save.CurrentLevelID) {
                    levelIndex = index;
                    break;
                }
            }
            CurrentLevel = (CampaignLevel)levelIndex;
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
        public void StopLevelRun() => IsLevelTimerRunning = false;

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
            LastLevelChronalRating = ChronalRatingRules.Compute(
                TimelineIntegrityPercent,
                LevelRewindsUsed,
                LevelSecretsFound,
                secretsTotal: 1,
                LevelElapsedSeconds);
        }

        // === Timeline Integrity & secrets (V7.1) ============================

        /// <summary>The Siphon Clock: this attempt's Timeline Integrity, 0-100.</summary>
        public float TimelineIntegrityPercent { get; private set; } = TimelineIntegrityRules.StartPercent;

        /// <summary>Secrets found in this level attempt.</summary>
        public int LevelSecretsFound { get; private set; }

        /// <summary>Frozen at completion for the results overlay.</summary>
        public float LastLevelIntegrityPercent { get; private set; } = TimelineIntegrityRules.StartPercent;
        public int LastLevelSecretsFound { get; private set; }
        public string LastLevelChronalRating { get; private set; } = "";

        /// <summary>
        /// V7.3 Siphon Clock: the drain accounting lives on each engaged
        /// Extractor (which owns its 10% share cap and 10 s grace window);
        /// this is the single sink they draw through. Returns the integrity
        /// actually drained, so the caller's share ledger only counts what
        /// was really stolen. Replaces the old unbounded per-extractor-count
        /// entry point.
        /// </summary>
        public float DrainTimelineIntegrityAmount(float amountPercent) {
            if (amountPercent <= 0f || !IsLevelTimerRunning) return 0f;
            float applied = Mathf.Min(amountPercent, TimelineIntegrityPercent);
            TimelineIntegrityPercent -= applied;
            return applied;
        }

        /// <summary>V7.3 restoration paths: +3% per destroyed Extractor, +2%
        /// per ordinary secret, +5% for the special secret — capped at 100.</summary>
        public void RestoreTimelineIntegrity(float percent) {
            TimelineIntegrityPercent = Mathf.Min(
                TimelineIntegrityRules.StartPercent,
                TimelineIntegrityPercent + Mathf.Max(0f, percent));
        }

        /// <summary>
        /// Counts a secret once per attempt and pays its restoration
        /// (+2% ordinary / +5% special). Returns false when this secret was
        /// already found this attempt (including via a mid-level resume).
        /// </summary>
        public bool RegisterSecretFound(string secretID, bool isSpecialSecret = true) {
            if (!string.IsNullOrWhiteSpace(secretID) && !_foundSecrets.Add(secretID)) return false;
            LevelSecretsFound++;
            RestoreTimelineIntegrity(isSpecialSecret
                ? TimelineIntegrityRules.SpecialSecretRestorePercent
                : TimelineIntegrityRules.GenericSecretRestorePercent);
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
            _activatedCheckpoints.Clear();
            _destroyedExtractors.Clear();
            _foundSecrets.Clear();
            ClearRestorationFonts();
            TimelineIntegrityPercent = TimelineIntegrityRules.StartPercent;
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
            save.LevelIntegrityPercent = TimelineIntegrityPercent;
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
            TimelineIntegrityPercent = Mathf.Clamp(save.LevelIntegrityPercent, 0f, TimelineIntegrityRules.StartPercent);
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

        public void AdvanceToNextLevel() {
            if ((int)CurrentLevel < 15) {
                CurrentLevel = (CampaignLevel)((int)CurrentLevel + 1);
            }
            // The finished attempt's registries (fonts, checkpoints,
            // extractors, secrets) never leak into the next level.
            ClearLevelAttemptState();
        }

        public string GetCurrentLevelPath() {
            return GetLevelScenePath(CurrentLevel);
        }

        public static string GetLevelScenePath(CampaignLevel level) {
            int idx = (int)level;
            return idx >= 0 && idx < LevelScenePaths.Length ? LevelScenePaths[idx] : "";
        }

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
            ChronalDustCollected = CalculateTimelineCollapseDust(ChronalDustCollected);
        }

        public static int CalculateTimelineCollapseDust(int carriedDust) =>
            Mathf.FloorToInt(Mathf.Max(0, carriedDust) * 0.8f);

        public void BeginTimelineCollapse(string checkpointID) {
            CollapsedLevel = CurrentLevel;
            CollapsedCheckpointID = checkpointID ?? "";
            HasPendingTimelineRestart = true;
            ApplyTimelineCollapseDustPenalty();

            Difficulty difficulty = GameManager.Instance?.CurrentSession.Difficulty ?? Difficulty.Normal;
            ChronalRewindsRemaining = FTT.Environment.ChronalRewindManager.GetMaximumRewinds(difficulty);
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
            // === Package 11 A5 region: the Legacy Unlock milestone grant ===
            // Inside the completion transaction and BEFORE
            // RecordLevelResultToSave, so the same save write that records the
            // result also persists the restored slot.
            GrantLegacyUnlockMilestone(levelID);
            // === end Package 11 A5 region ===
            RecordLevelResultToSave(levelID);
            AdvanceToNextLevel();
        }

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
            save.RatingByLevel[levelID] = LastLevelChronalRating ?? "";
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
