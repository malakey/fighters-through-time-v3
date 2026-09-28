using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace FTT.Core {

    public class StorySaveData {
        public int SaveVersion = SaveSchemaMigrator.CurrentVersion;
        public string SelectedCharacterID = "";
        public Difficulty Difficulty = Difficulty.Normal;
        public string CurrentLevelID = "res://scenes/campaign/Level_00_Tutorial.tscn";
        public string LastCheckpointID = "";
        public string LastViewedDialogueID = "";
        public int CurrentHP = 100;
        public int CurrentLives = 3;
        public float CurrentUltimateMeter;
        public List<string> CompletedLevels = new();
        public Dictionary<string, int> DepositedChronalDust = new();
        public int LevelChronalDust;
        public Dictionary<string, List<string>> GridProgress = new();
        public List<string> CompletedPuzzleIDs = new();
        public float PlayTimeSeconds;
        public bool IsCompleted;
        public string LastSavedTimestamp = "";
        // V7.1: Timeline Integrity per completed level (the campaign-ending
        // average input), and secrets found per level — a second, better run
        // should feel seen.
        public Dictionary<string, float> IntegrityByLevel = new();

        /// <summary>
        /// <b>Dead field.</b> V7.6 ruling 2.A retires the Chronal Rating, and
        /// Package 11 A8 removed every writer. It stays in the payload through
        /// schema v6 because deleting a field is a breaking change this package is
        /// not taking, and because an older save's recorded ratings are harmless to
        /// carry forward. Nothing may start writing it again.
        /// </summary>
        public Dictionary<string, string> RatingByLevel = new();

        public Dictionary<string, int> SecretsFoundByLevel = new();

        // === Schema v5 (V7.3, purely additive) =============================
        // Mid-level resume must restore the whole attempt, not just position:
        // checkpoints already stabilized this attempt (Mending is
        // once-per-checkpoint-per-attempt), Restoration Font uses consumed,
        // extractors already destroyed, secrets already found, and the
        // attempt's live Timeline Integrity. Plus two campaign presentation
        // flags: dialogue sequences already viewed (hold-to-skip on completed
        // saves) and whether the Timeline Collapse beat has played once
        // (first viewing is unskippable).
        public List<string> ActivatedCheckpointIDs = new();
        public Dictionary<string, int> FontUsesConsumed = new();
        public List<string> DestroyedExtractorIDs = new();
        public List<string> FoundSecretIDs = new();
        /// <summary>Package 11 A10 (F05, additive): the reward sources whose
        /// pickup this attempt actually COLLECTED. A restored enemy whose reward
        /// was collected may fight again and awards zero additional dust.</summary>
        public List<string> ClaimedRewardSourceIDs = new();
        public float LevelIntegrityPercent = 100f;
        public List<string> ViewedDialogueIDs = new();
        public bool HasSeenCollapseBeat;

        // === Package 11 A1b (V7.6 F10 Defy History, additive) ==============
        /// <summary>
        /// F10: Defy History is "once per level", and V7.6 reads that as once
        /// per ATTEMPT. Before this it was a private PlayerController field with
        /// no persistence at all, so a mid-level quit and resume - or a death
        /// rewind that rebuilt the player - handed the run a second saved life.
        /// Committed together with HP and meter at every checkpoint save, and
        /// preserved through death rewind, Collapse, Snap, checkpoints, hub
        /// visits and load. Only a fresh entry or a full Restart Level resets
        /// it. v5 payloads load with the field initializer (false), which is the
        /// correct conservative default: an unused Defy, never a spent one.
        /// </summary>
        public bool StoryDefyHistoryUsed;

        // === Package 11 A2 (V7.6 Time Freeze, additive) ====================
        // Per-attempt Time Freeze cooldown. Activation commits the conservative
        // full 45 s for any reload during that activation; an explicit Save or
        // exit ends the freeze and stores 45 s; after thaw the true remainder is
        // stored. A reload NEVER resumes or renews an active freeze. Older
        // payloads load with the field initializer (0 = Ready).
        public float TimeFreezeCooldownSeconds;
        // === Package 11 A5 (V7.5 Legacy Unlock Schedule) ==================
        // Per character, the ability slots Act I has restored. The persisted
        // keys are "movement" / "special1" / "special2" / "ultimate" —
        // FTT.Core.LegacyUnlockSchedule owns those strings, so they are a
        // serialization contract. Additive per plan §2.6: a v5 payload loads
        // with an empty dictionary and StoryManager backfills it from
        // CompletedLevels. Preserved on Restart Level — restarting a level
        // never un-earns a kit.
        public Dictionary<string, List<string>> UnlockedLegacyAbilities = new();
        // === Package 11 A3 (V7.6 F11), additive ============================
        // The paid-recovery allowance banked at the last checkpoint. Distinct
        // from LevelIntegrityPercent, which is the LIVE gauge and keeps
        // draining after the fracture is struck. Only a timer-caused Collapse
        // reads it, and only to floor the granted recovery.
        public float CheckpointIntegrityPercent = 100f;

        // === Package 11 A3b (V7.6 F10), additive ===========================
        /// <summary>
        /// The F10 per-attempt record. Everything per-attempt lives here; the
        /// root fields above stay root and are deliberately <b>not</b> duplicated
        /// inside it (STORY_PERSISTENCE: "Do not duplicate mutable copies of a
        /// root field").
        ///
        /// <para>Additive per plan §2.6: a v5 payload loads with a default-
        /// constructed record, and <see cref="StoryAttemptState.MigrateFromLegacyRoot"/>
        /// fills it from the six loose V7.3 attempt fields — the trustworthy
        /// existing records F10 tells the migration to derive from. Phase C's
        /// single v5→v6 step makes that call explicit.</para>
        /// </summary>
        public StoryAttemptState AttemptState = new();

        /// <summary>
        /// Act III Warden Beacon anchor charges left; zero outside Act III. A
        /// root mirror of <c>AttemptState.AnchorChargesRemaining</c>, kept flat
        /// because the HUD and the save-select summary read it without touching
        /// the whole attempt record.
        /// </summary>
        public int AnchorCharges;

        public void Normalize() {
            SaveVersion = SaveSchemaMigrator.CurrentVersion;
            AttemptState ??= new StoryAttemptState();
            AttemptState.Normalize();
            AnchorCharges = Math.Max(0, AnchorCharges);
            SelectedCharacterID ??= "";
            CurrentLevelID ??= "res://scenes/campaign/Level_00_Tutorial.tscn";
            LastCheckpointID ??= "";
            LastViewedDialogueID ??= "";
            CompletedLevels ??= new List<string>();
            DepositedChronalDust ??= new Dictionary<string, int>();
            GridProgress ??= new Dictionary<string, List<string>>();
            CompletedPuzzleIDs ??= new List<string>();
            LastSavedTimestamp ??= "";
            IntegrityByLevel ??= new Dictionary<string, float>();
            RatingByLevel ??= new Dictionary<string, string>();
            SecretsFoundByLevel ??= new Dictionary<string, int>();
            ActivatedCheckpointIDs ??= new List<string>();
            FontUsesConsumed ??= new Dictionary<string, int>();
            DestroyedExtractorIDs ??= new List<string>();
            FoundSecretIDs ??= new List<string>();
            ClaimedRewardSourceIDs ??= new List<string>();
            ViewedDialogueIDs ??= new List<string>();
            UnlockedLegacyAbilities ??= new Dictionary<string, List<string>>();
            foreach (string characterID in new List<string>(UnlockedLegacyAbilities.Keys)) {
                UnlockedLegacyAbilities[characterID] ??= new List<string>();
            }
            LevelIntegrityPercent = Math.Clamp(LevelIntegrityPercent, 0f, 100f);
            CheckpointIntegrityPercent = Math.Clamp(CheckpointIntegrityPercent, 0f, 100f);
            CurrentHP = Math.Max(0, CurrentHP);
            CurrentLives = Math.Max(0, CurrentLives);
            CurrentUltimateMeter = Math.Clamp(CurrentUltimateMeter, 0f, 100f);
            LevelChronalDust = Math.Max(0, LevelChronalDust);
        }
    }

    /// <summary>Persisted window mode (Package 8 A4). Stored as an int by the serializer.</summary>
    public enum WindowModeSetting {
        Windowed = 0,
        Fullscreen = 1,
        BorderlessFullscreen = 2
    }

    /// <summary>
    /// V7 "Match Settings Persist": the last-used Fighter MatchSettings, stored
    /// in the global save and pre-loaded on the next session. Additive class:
    /// Newtonsoft leaves <see cref="Saved"/> false on older payloads, so no
    /// schema bump — defaults apply until the first Fighter match is configured.
    /// </summary>
    public class SavedMatchSettings {
        /// <summary>The retired Hybrid ordinal. Recognized for decoding, never written.</summary>
        public const int LegacyHybridMode = 2;
        /// <summary>Default timer, in seconds (8:00).</summary>
        public const float DefaultTimeLimitSeconds = 480f;
        public const int MinStockCount = 1;
        public const int MaxStockCount = 5;
        public const int DefaultStockCount = 3;

        public bool Saved;
        public int Mode;
        public int StockCount = DefaultStockCount;
        public float TimeLimit = DefaultTimeLimitSeconds;
        public int ItemSpawnRate = 3;
        /// <summary>
        /// <b>Legacy, read-only.</b> The retired <c>HazardTriggerFrequency</c>
        /// ordinal (0 Off, 1 Low, 2 Medium, 3 High). Package 12 W5 stopped writing
        /// it; it survives only so <see cref="Normalize"/> can derive
        /// <see cref="StageHazardsEnabled"/> for a payload that predates the toggle,
        /// and so Phase C's single v6→v7 step can compose the same derivation.
        /// </summary>
        public int HazardRate = 3;
        /// <summary>
        /// Package 12 W5: the hazard On/Off toggle. <b>Nullable on purpose</b> — a
        /// payload written before the toggle existed deserializes it as null, which
        /// is how <see cref="Normalize"/> tells "never chosen" from an explicit
        /// Off without a schema bump. Every write sets it.
        /// </summary>
        public bool? StageHazardsEnabled;
        /// <summary>
        /// Package 12 W5 (M24, D5(b)): the Items sub-toggle "Meter pickups".
        /// Additive; an older payload loads it as the false default.
        /// </summary>
        public bool MeterPickupsEnabled;

        /// <summary>
        /// Package 12 W5 — <b>the declared v7 derivation</b> for the retired hazard
        /// selector: <c>Off → false</c>, every other stored value (Low, Medium,
        /// High, or an unknown ordinal) <c>→ true</c>. Pure and engine-free so
        /// Phase C's v6→v7 step can compose it verbatim.
        /// </summary>
        public static bool DeriveStageHazardsEnabled(int legacyHazardRate) => legacyHazardRate != 0;

        /// <summary>
        /// True once <see cref="Normalize"/> has repaired a legacy Hybrid timer, so
        /// the menus can show the repaired value before launch as F21 requires.
        /// Presentation only — never persisted.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool TimerRepaired;

        /// <summary>
        /// F21 migration (Package 11 A1c). Idempotent, and run before loaded
        /// settings are displayed or the next match is created — never against a
        /// running match.
        ///
        /// <list type="bullet">
        /// <item>A recognized legacy <b>Hybrid</b> becomes <b>timed Stock</b>, keeping
        /// a valid positive timer and stock count and every other house rule. A
        /// Hybrid timer that is Off, missing or nonfinite becomes the default 480 s
        /// and is flagged <see cref="TimerRepaired"/> so the summary can show it.
        /// A valid timed Hybrid never becomes untimed Stock.</item>
        /// <item><b>Unknown</b> mode values are <em>not</em> Hybrid: the source
        /// settings are preserved and the mode is left for the player to choose
        /// rather than guessing a winner rule. The stored value is clamped to a
        /// selectable mode so a menu cannot index past its list.</item>
        /// <item><b>Time</b> requires a positive timer; Off is unavailable there, so
        /// a Time entry with no timer is repaired to 480 s.</item>
        /// <item>Existing valid Stock settings — <b>including timer Off</b> — and
        /// valid Time settings keep their meanings.</item>
        /// </list>
        /// </summary>
        public void Normalize() {
            TimerRepaired = false;
            bool validTimer = !float.IsNaN(TimeLimit) && !float.IsInfinity(TimeLimit) && TimeLimit > 0f;

            if (Mode == LegacyHybridMode) {
                Mode = (int)MatchMode.Stock;
                if (!validTimer) {
                    TimeLimit = DefaultTimeLimitSeconds;
                    TimerRepaired = true;
                    validTimer = true;
                }
            } else if (Mode != (int)MatchMode.Stock && Mode != (int)MatchMode.TimeLimit) {
                // Unknown: preserve the source settings, require a valid selection.
                Mode = (int)MatchMode.Stock;
            }

            if (Mode == (int)MatchMode.TimeLimit && !validTimer) {
                TimeLimit = DefaultTimeLimitSeconds;
                TimerRepaired = true;
            }
            // Stock's timer may legitimately be Off (0); only a nonfinite or
            // negative value is invalid there.
            if (float.IsNaN(TimeLimit) || float.IsInfinity(TimeLimit) || TimeLimit < 0f) {
                TimeLimit = DefaultTimeLimitSeconds;
                TimerRepaired = true;
            }
            if (StockCount < MinStockCount || StockCount > MaxStockCount) {
                StockCount = DefaultStockCount;
            }
            // Package 12 W5: tolerate a pre-toggle payload until Phase C's v7 step
            // lands. Only a missing toggle is derived — an explicit value, Off
            // included, is never overwritten, so this stays idempotent.
            StageHazardsEnabled ??= DeriveStageHazardsEnabled(HazardRate);
        }
    }

    public class GlobalSaveData {
        /// <summary>
        /// Selectable window sizes, in the order the Display tab lists them. The
        /// reference canvas is 1920x1080 (AGENTS.md); every entry keeps 16:9 so the
        /// fixed `keep` aspect never letterboxes a supported choice.
        /// </summary>
        public static readonly (int Width, int Height)[] SupportedResolutions = {
            (1920, 1080), (1600, 900), (1280, 720), (1024, 576)
        };

        public static readonly string[] InitialStageIDs = {
            "florence_workshop", "orleans_vanguard", "chicago_exposition", "paris_bastille",
            "vesuvius_caldera", "nassau_flagship", "alexandria_chambers", "berlin_wall",
            "globe_theatre", "gettysburg_ridge"
        };

        public int SaveVersion = SaveSchemaMigrator.CurrentVersion;
        /// <summary>
        /// Every character is available from the start in the initial build
        /// (AGENTS.md pillar), so the unlock set is simply the whole roster.
        ///
        /// <para>Package 11 A6b: the roster is the content manifest
        /// (<see cref="CharacterRoster"/>) rather than a literal cast list, so a
        /// new roster row is unlocked by default instead of being permanently
        /// missing from every save that already exists.</para>
        ///
        /// <para><b>Filled by <see cref="SaveManager.EnsureRosterUnlocked"/> —
        /// not by this initializer and not by <see cref="Normalize"/>.</b>
        /// Reading the manifest means reading a file through Godot's
        /// <c>FileAccess</c>, and both a field initializer and <c>Normalize</c>
        /// run inside pure-C# GdUnit suites that have no Godot runtime, where
        /// that call is an access violation which takes the whole test host down
        /// (0xC0000005, observed on <c>BasicStringProfileTests</c> and
        /// <c>FighterMatchStatisticsTests</c>). <see cref="GlobalSaveData"/> is a
        /// plain data class with no engine dependency and must stay one; the
        /// engine-side fill belongs on the autoload that is always inside the
        /// engine. The stage list gets away with backfilling in <c>Normalize</c>
        /// only because <c>InitialStageIDs</c> is a literal.</para>
        /// </summary>
        public List<string> UnlockedCharacters = new();
        public List<string> UnlockedStages = new(InitialStageIDs);
        public int TotalPlayTime;
        public int TotalWins;
        public int TotalLosses;
        /// <summary>
        /// True ties logged as played matches (design Section 11: a draw is a
        /// match, not a win or loss). Additive field: Newtonsoft leaves it at its
        /// default 0 on payloads written before it existed, so no schema bump.
        /// </summary>
        public int TotalDraws;
        public Dictionary<string, int> CharacterWins = new();
        public Dictionary<string, int> CharacterLosses = new();
        public SavedMatchSettings LastMatchSettings = new();
        public float MasterVolume = 1.0f;
        public float MusicVolume = 0.8f;
        public float SFXVolume = 1.0f;
        public float UIVolume = 1.0f;
        public float HapticIntensity = 0.7f;
        public bool HapticsEnabled = true;
        public bool DamageNumbersVisible = true;
        public float HudOpacity = 1f;
        public float ScreenShakeScale = 1f;

        /// <summary>
        /// Accessibility UI scale (design "Committed Accessibility Additions"):
        /// multiplies the shared theme's font sizes and the HUD layout. 90%–140%;
        /// additive field: payloads written before it existed keep the 1f
        /// initializer, and <see cref="Normalize"/> clamps hand-edited values.
        /// </summary>
        public float UiScale = 1f;

        /// <summary>Clamp domain for <see cref="UiScale"/> (90%–140% per design).</summary>
        public const float MinUiScale = 0.9f;
        public const float MaxUiScale = 1.4f;

        /// <summary>
        /// Package 11 A8 / C01a. The <b>Reduced Temporal Effects</b> comfort
        /// preset. Additive field: a payload written before it existed keeps this
        /// <c>false</c> initializer, which is exactly the required legacy default
        /// — Off, without overwriting an explicit value and without inferring it
        /// from <see cref="ScreenShakeScale"/>. Read it through
        /// <see cref="ComfortSettings"/> rather than directly, and never from
        /// <c>scripts/FighterSim/</c>: the preset is local presentation state and
        /// stays outside match snapshots and hashes.
        /// </summary>
        public bool ReducedTemporalEffects;

        /// <summary>
        /// Package 12 W5 (G12). The local player-slot palette: 0 Default
        /// (cyan/red), 1 Blue/Orange, 2 High-Contrast. Presentation only — it
        /// recolors the ownership outline and HUD slot colours on this machine and
        /// never enters the simulation, a snapshot or a hash. Additive field: an
        /// older payload keeps the Default initializer. Read it through
        /// <c>FTT.Combat.PlayerSlotPalettes</c>.
        /// </summary>
        public int PlayerSlotPalette;

        // Display (Package 8 A4). Applied at boot by ViewportEnforcer, which is the
        // last autoload and already owns window/viewport concerns.
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public WindowModeSetting WindowMode = WindowModeSetting.Windowed;
        public bool VSyncEnabled = true;

        /// <summary>
        /// InputMap overrides (Package 8 A4). Replaces the dead
        /// <c>Dictionary&lt;string,string&gt;</c> of the same name, which was written
        /// nowhere, read nowhere, and could not represent a multi-event action.
        /// Only actions that differ from project.godot are stored.
        /// </summary>
        public InputBindingSet InputBindings = new();

        /// <summary>
        /// Package 11 A5 (V7.6 dialogue skip, plan §2.10 item 4). Dialogue
        /// sequence IDs the player has finished — or confirmed a skip on — on
        /// <b>any</b> story slot. Global on purpose: a scene seen in one
        /// playthrough should not ask "skip this? it won't replay" again in the
        /// next. It gates <b>only</b> the first-viewing confirmation; per-slot
        /// gameplay effects are still keyed on
        /// <see cref="StorySaveData.ViewedDialogueIDs"/>, so seeing a scene in
        /// another slot never suppresses this slot's rewards. Additive: an older
        /// payload loads with an empty set.
        /// </summary>
        public HashSet<string> SeenDialogueIDs = new(StringComparer.Ordinal);

        public void Normalize() {
            SaveVersion = SaveSchemaMigrator.CurrentVersion;
            // Package 11 A6b: the roster backfill deliberately does NOT live
            // here. See UnlockedCharacters' remarks — reading the content
            // manifest is Godot file I/O, and this method is called from pure-C#
            // GdUnit suites with no Godot runtime, where that is an access
            // violation that kills the test host. GlobalSaveData is a plain data
            // class with no engine dependency and must stay one;
            // SaveManager.EnsureRosterUnlocked owns the fill.
            UnlockedCharacters ??= new List<string>();
            SeenDialogueIDs ??= new HashSet<string>(StringComparer.Ordinal);
            UnlockedStages ??= new List<string>();
            foreach (string stageID in InitialStageIDs) {
                if (!UnlockedStages.Contains(stageID)) UnlockedStages.Add(stageID);
            }
            CharacterWins ??= new Dictionary<string, int>();
            CharacterLosses ??= new Dictionary<string, int>();
            TotalDraws = Math.Max(0, TotalDraws);
            InputBindings ??= new InputBindingSet();
            InputBindings.Normalize();
            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            SFXVolume = Math.Clamp(SFXVolume, 0f, 1f);
            UIVolume = Math.Clamp(UIVolume, 0f, 1f);
            HapticIntensity = Math.Clamp(HapticIntensity, 0f, 1f);
            HudOpacity = Math.Clamp(HudOpacity, 0.2f, 1f);
            ScreenShakeScale = Math.Clamp(ScreenShakeScale, 0f, 1f);
            UiScale = Math.Clamp(UiScale, MinUiScale, MaxUiScale);
            if (PlayerSlotPalette < 0 || PlayerSlotPalette > 2) PlayerSlotPalette = 0;
            NormalizeDisplay();
        }

        /// <summary>
        /// Package 11 A8. Hands the comfort preset to its runtime read point.
        /// Separate from <see cref="Normalize"/> because normalization runs on
        /// every write as well as every load, and the preset must be pushed only
        /// where "what the player chose" actually changes.
        /// </summary>
        public void ApplyComfortSettings() => ComfortSettings.Apply(ReducedTemporalEffects);

        /// <summary>
        /// Snaps the stored size onto the nearest supported resolution and clamps
        /// the window mode. A hand-edited or corrupt payload can otherwise ask the
        /// engine for a 0x0 window.
        /// </summary>
        private void NormalizeDisplay() {
            if (!Enum.IsDefined(typeof(WindowModeSetting), WindowMode)) WindowMode = WindowModeSetting.Windowed;
            int index = ResolutionIndex(ResolutionWidth, ResolutionHeight);
            (int width, int height) = SupportedResolutions[index];
            ResolutionWidth = width;
            ResolutionHeight = height;
        }

        /// <summary>
        /// Index of the supported resolution nearest to the requested size, by
        /// squared pixel-dimension distance. Never out of range.
        /// </summary>
        public static int ResolutionIndex(int width, int height) {
            int bestIndex = 0;
            long bestDistance = long.MaxValue;
            for (int index = 0; index < SupportedResolutions.Length; index++) {
                (int candidateWidth, int candidateHeight) = SupportedResolutions[index];
                long deltaWidth = candidateWidth - width;
                long deltaHeight = candidateHeight - height;
                long distance = (deltaWidth * deltaWidth) + (deltaHeight * deltaHeight);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = index;
            }
            return bestIndex;
        }
    }

    /// <summary>
    /// Versus statistics rules (design-godot.md Section 11 "Versus Statistics
    /// Logging" and "Draw/Tie Resolution"). Kept separate from the Godot node so
    /// the tallies can be exercised without a SaveManager autoload.
    /// </summary>
    public static class FighterMatchStatistics {
        /// <summary>
        /// Records one concluded local match. The overall tallies stay
        /// profile-centric (player one is the local profile, unchanged from the
        /// pre-Package-6 behaviour) while the character tallies are per-character
        /// for both fighters. A true tie counts in neither win/loss tally but is
        /// logged as a played match in <see cref="GlobalSaveData.TotalDraws"/>
        /// (design Section 11 "Draw/Tie Resolution"; audit §4 Low — previously a
        /// draw was recorded nowhere).
        /// </summary>
        public static void Record(
            GlobalSaveData data,
            string playerOneCharacterID,
            string playerTwoCharacterID,
            int winnerPlayerID,
            bool isTrueTie) {
            if (data == null) return;
            if (isTrueTie) {
                data.TotalDraws++;
                return;
            }
            data.CharacterWins ??= new Dictionary<string, int>();
            data.CharacterLosses ??= new Dictionary<string, int>();
            if (winnerPlayerID == 0) {
                data.TotalWins++;
                Increment(data.CharacterWins, playerOneCharacterID);
                Increment(data.CharacterLosses, playerTwoCharacterID);
            } else if (winnerPlayerID == 1) {
                data.TotalLosses++;
                Increment(data.CharacterWins, playerTwoCharacterID);
                Increment(data.CharacterLosses, playerOneCharacterID);
            }
        }

        public static int WinsFor(GlobalSaveData data, string characterID) =>
            Read(data?.CharacterWins, characterID);

        public static int LossesFor(GlobalSaveData data, string characterID) =>
            Read(data?.CharacterLosses, characterID);

        private static void Increment(Dictionary<string, int> tally, string characterID) {
            if (tally == null || string.IsNullOrWhiteSpace(characterID)) return;
            tally[characterID] = tally.GetValueOrDefault(characterID) + 1;
        }

        private static int Read(Dictionary<string, int> tally, string characterID) {
            if (tally == null || string.IsNullOrWhiteSpace(characterID)) return 0;
            return tally.GetValueOrDefault(characterID);
        }
    }

    public partial class SaveManager : Node {
        public static SaveManager Instance { get; private set; }

        private const string SaveDir = "user://saves/";
        private const string GlobalFileName = "global.sav";
        private ISaveKeyProvider _keyProvider;
        private byte[] _masterKey;

        /// <summary>
        /// Test seam for the legacy-migration gate (audit H-2). The gate decision
        /// is exercised directly through this override rather than by faking a
        /// release build. Always reset to null in a test's finally block.
        /// </summary>
        internal static bool? LegacyMigrationOverrideForTesting;

        /// <summary>
        /// True when the unauthenticated legacy-save fallback may run. ADR 0004
        /// frames the Base64/plain-JSON migration as a one-time development
        /// measure; in release builds an unrecognized file surfaces the existing
        /// tamper/corrupt notice instead of loading unauthenticated data and
        /// laundering it into a validly signed envelope (audit H-2).
        /// </summary>
        public static bool IsLegacyMigrationEnabled => LegacyMigrationOverrideForTesting ?? OS.IsDebugBuild();

        /// <summary>
        /// False when the per-install save key could not be provisioned (audit
        /// M-22): the manager is then in an explicit no-save state — loads report
        /// empty, saves refuse with a warning, and no save or key file is touched.
        /// </summary>
        public bool IsSaveSystemAvailable => _masterKey != null;

        public StorySaveData[] SaveSlots = new StorySaveData[3];
        public GlobalSaveData GlobalData = new();

        /// <summary>
        /// Translation key describing the last recovery/migration/corruption event,
        /// or an empty string when the load was clean. Package 8 A4 replaced the raw
        /// English strings these used to hold; the main menu surfaces
        /// <see cref="LastLoadNotice"/> when this is set.
        /// </summary>
        public string LastLoadNoticeKey { get; private set; } = "";

        /// <summary>Format arguments for <see cref="LastLoadNoticeKey"/>.</summary>
        public string[] LastLoadNoticeArgs { get; private set; } = Array.Empty<string>();

        /// <summary>The notice resolved through the translation server, or "".</summary>
        public string LastLoadNotice => FormatNotice(LastLoadNoticeKey, LastLoadNoticeArgs);

        /// <summary>Resolves a notice key/args pair. Static so UI can format a captured pair.</summary>
        public static string FormatNotice(string key, string[] args) {
            if (string.IsNullOrEmpty(key)) return "";
            string template = TranslationServer.Translate(key);
            if (args == null || args.Length == 0) return template;
            try {
                return string.Format(template, args);
            } catch (FormatException) {
                return template;
            }
        }

        private void SetNotice(string key, params string[] args) {
            LastLoadNoticeKey = key ?? "";
            LastLoadNoticeArgs = args ?? Array.Empty<string>();
        }

        private void ClearNotice() => SetNotice("");

        public override void _Ready() {
            Instance = this;
            string saveDirectory = ProjectSettings.GlobalizePath(SaveDir);
            Directory.CreateDirectory(saveDirectory);
            _keyProvider = new FileSaveKeyProvider(Path.Combine(saveDirectory, ".savekey"));
            TryProvisionMasterKey(_keyProvider);
            // Snapshot project.godot's bindings before anything can mutate the map,
            // then push the saved overrides in. InputManager is an earlier autoload
            // and polls InputMap.ActionGetEvents live, so this lands before any
            // gameplay scene reads an action.
            InputBindingService.EnsureDefaultsCaptured();
            LoadGlobalData();
            ApplySavedInputBindings();
            // C01a: the preset must be live before the first loading/portal
            // effect. GameManager.LoadScene builds that screen, and the earliest
            // it can run is after this autoload's _Ready, so this is the boundary.
            GlobalData?.ApplyComfortSettings();
            for (int slot = 0; slot < SaveSlots.Length; slot++) LoadStorySlot(slot);
            // Schema v5 -> v6, deferred half: the global seen-dialogue set is the
            // union of every slot's per-slot ViewedDialogueIDs, and the global
            // payload loads before the slots do. Runs once per upgraded payload,
            // and is idempotent if it ever runs twice.
            SeedGlobalSeenDialogueIfPending();
            // V7.6 ruling 2.B (Package 11 A3): crashes are free. The boot
            // billing is gone; the marker survives only as F10's attempt-status
            // router, which A3b consumes. The VOLUNTARY 20% exit fee the pause
            // menu charges is unchanged.
            ConsumeAbnormalExitMarker();
            if (EventBus.Instance != null) {
                // V7.3 checkpoint save ordering: persistence listens to the
                // COMMITTED half of the checkpoint event pair, which the
                // trigger raises after every gameplay consumer (rewind-pool
                // refresh included) has run — save.CurrentLives is always the
                // post-refresh value.
                EventBus.Instance.OnCheckpointCommitted += SaveCheckpoint;
                EventBus.Instance.OnLevelComplete += SaveLevelCompletion;
                EventBus.Instance.OnTalentNodeUnlocked += SaveTalentUnlock;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnCheckpointCommitted -= SaveCheckpoint;
                EventBus.Instance.OnLevelComplete -= SaveLevelCompletion;
                EventBus.Instance.OnTalentNodeUnlocked -= SaveTalentUnlock;
            }
            // Package 11 A3b (F10): an orderly exit flushes the latest state
            // before the process goes away.
            FlushStorySlotWrites();
            // Clean shutdown: the session is settled, no abnormal-exit fee.
            SessionExitGuard.ClearMarker();
            if (_masterKey != null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(_masterKey);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// V7.6 ruling 2.B (Package 11 A3): a session marker surviving to the
        /// next launch means the previous session ended abnormally — and that
        /// now costs <b>nothing</b>. The V7.3 boot billing (a 20% undeposited-
        /// dust fee plus a notice) is retired: a crash is not a player choice,
        /// and charging for one taught players to fear the power switch rather
        /// than to press Exit.
        ///
        /// The marker itself stays, because F10 repurposes it as the attempt-
        /// status router (A3b). Reading it here clears it so a settled boot
        /// never looks abnormal to the next launch.
        /// </summary>
        internal bool ConsumeAbnormalExitMarker() {
            if (!SessionExitGuard.TryReadMarker(out int slot)) return false;
            SessionExitGuard.ClearMarker();
            return slot >= 0 && slot < SaveSlots.Length && SaveSlots[slot] != null;
        }

        /// <summary>
        /// Provisions the per-install master key, entering the explicit no-save
        /// state on failure (audit M-22). A malformed or unreadable
        /// <c>.savekey</c> previously threw out of <see cref="_Ready"/>, silently
        /// skipping every load and the EventBus wiring — slots presented as empty
        /// and a new campaign could overwrite intact files. Now the failure
        /// surfaces a recoverable notice for the main menu, and neither the key
        /// file nor any save file is deleted or overwritten.
        /// </summary>
        internal bool TryProvisionMasterKey(ISaveKeyProvider provider) {
            try {
                _masterKey = provider?.GetOrCreateKey();
            } catch (Exception exception) when (
                exception is IOException
                || exception is InvalidDataException
                || exception is UnauthorizedAccessException) {
                _masterKey = null;
                GD.PushWarning($"Save key provisioning failed: {exception.Message}");
            }
            if (_masterKey != null && _masterKey.Length == 32) return true;
            _masterKey = null;
            SetNotice("save_notice_key_error");
            return false;
        }

        public StorySaveData CreateStorySlot(int slotIndex, string characterID, Difficulty difficulty) {
            ValidateSlot(slotIndex);
            var data = new StorySaveData {
                SelectedCharacterID = characterID?.Trim().ToLowerInvariant() ?? "",
                Difficulty = difficulty,
                CurrentLives = difficulty == Difficulty.Easy ? 5 : difficulty == Difficulty.Hard ? 1 : 3,
                LastSavedTimestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("O")
            };
            SaveSlots[slotIndex] = data;
            SaveStorySlot(slotIndex);
            return data;
        }

        /// <summary>
        /// Commits a story slot synchronously. This is the <b>critical-event</b>
        /// path (F10): the caller may not treat the event as safely saved, or
        /// continue past a recovery/scene-transition boundary, until it returns
        /// true. Any queued asynchronous writes for the slot are drained first so
        /// an older revision can never land on top of this one.
        /// </summary>
        public bool SaveStorySlot(int slotIndex) {
            ValidateSlot(slotIndex);
            StorySaveData data = SaveSlots[slotIndex];
            if (data == null) return false;
            FlushStorySlotWrites(slotIndex);
            data.Normalize();
            data.LastSavedTimestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
            bool written = WritePayload(
                GetStoryPath(slotIndex), "story", data.SaveVersion, JsonConvert.SerializeObject(data));
            long revision = data.AttemptState?.Revision ?? 0;
            lock (_writeLock) {
                if (written) {
                    _slotWrittenRevision[slotIndex] = Math.Max(revision, _slotWrittenRevision[slotIndex]);
                    _slotQueuedRevision[slotIndex] = _slotWrittenRevision[slotIndex];
                    LastStoryWriteFailed = false;
                } else {
                    // F10: retain the prior valid revision, report the failure,
                    // and let the caller refuse the transition.
                    LastStoryWriteFailed = true;
                }
            }
            if (!written) SetNotice(WriteFailedNoticeKey, (slotIndex + 1).ToString());
            return written;
        }

        // === Package 11 A3b — the ordered per-slot writer (F10) =============
        // "Capture immutable state on the game thread; serialize/write
        // asynchronously through one ordered writer per slot. Ignore obsolete
        // revisions; never let an older asynchronous write replace a newer one.
        // On write failure, retain the prior valid save and pending event,
        // report that saving failed, and prevent a transition from pretending it
        // saved successfully."

        /// <summary>Notice key raised when a slot write fails. A transition must not claim it saved.</summary>
        public const string WriteFailedNoticeKey = "save_notice_write_failed";

        private readonly object _writeLock = new();
        private readonly System.Threading.Tasks.Task[] _slotWriteChains =
            new System.Threading.Tasks.Task[3];
        private readonly long[] _slotWrittenRevision = new long[3];
        private readonly long[] _slotQueuedRevision = new long[3];

        /// <summary>True when the most recent story write failed. Cleared by the next success.</summary>
        public bool LastStoryWriteFailed { get; private set; }

        /// <summary>The highest attempt revision durably written for a slot. Test seam.</summary>
        public long WrittenRevision(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= _slotWrittenRevision.Length) return 0;
            lock (_writeLock) return _slotWrittenRevision[slotIndex];
        }

        /// <summary>
        /// Queues one ordered asynchronous write for a slot. The payload is
        /// serialized here, on the game thread, so the background write can never
        /// observe a half-mutated save. Returns false when the revision is
        /// obsolete (an equal or newer one is already queued) — that is the
        /// designed no-op, not a failure.
        /// </summary>
        public bool QueueStorySlotWrite(int slotIndex, long revision) {
            if (slotIndex < 0 || slotIndex >= SaveSlots.Length) return false;
            StorySaveData data = SaveSlots[slotIndex];
            if (data == null) return false;
            lock (_writeLock) {
                if (revision > 0 && revision <= _slotQueuedRevision[slotIndex]) return false;
                _slotQueuedRevision[slotIndex] = Math.Max(revision, _slotQueuedRevision[slotIndex]);
            }
            data.Normalize();
            data.LastSavedTimestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("O");
            string json = JsonConvert.SerializeObject(data);
            int schemaVersion = data.SaveVersion;
            string path = GetStoryPath(slotIndex);
            lock (_writeLock) {
                System.Threading.Tasks.Task previous =
                    _slotWriteChains[slotIndex] ?? System.Threading.Tasks.Task.CompletedTask;
                _slotWriteChains[slotIndex] = previous.ContinueWith(
                    _ => RunQueuedWrite(slotIndex, revision, path, schemaVersion, json),
                    System.Threading.Tasks.TaskScheduler.Default);
            }
            return true;
        }

        private void RunQueuedWrite(int slotIndex, long revision, string path, int schemaVersion, string json) {
            lock (_writeLock) {
                // An older asynchronous write must never replace a newer one,
                // however the scheduler reorders the continuations.
                if (revision > 0 && revision < _slotWrittenRevision[slotIndex]) return;
                bool written = WritePayload(path, "story", schemaVersion, json);
                if (written) {
                    _slotWrittenRevision[slotIndex] = Math.Max(revision, _slotWrittenRevision[slotIndex]);
                    LastStoryWriteFailed = false;
                    return;
                }
                LastStoryWriteFailed = true;
            }
        }

        /// <summary>
        /// Drains a slot's queued writes (or every slot with <c>-1</c>). Called
        /// before any synchronous critical-event commit, and at shutdown.
        /// </summary>
        public void FlushStorySlotWrites(int slotIndex = -1) {
            var pending = new List<System.Threading.Tasks.Task>();
            lock (_writeLock) {
                for (int slot = 0; slot < _slotWriteChains.Length; slot++) {
                    if (slotIndex >= 0 && slot != slotIndex) continue;
                    if (_slotWriteChains[slot] != null) pending.Add(_slotWriteChains[slot]);
                }
            }
            foreach (System.Threading.Tasks.Task task in pending) {
                try { task.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
            }
        }

        public bool DeleteStorySlot(int slotIndex) {
            ValidateSlot(slotIndex);
            try {
                AtomicSaveStore.DeleteAllCandidates(GetStoryPath(slotIndex));
                SaveSlots[slotIndex] = null;
                if (GameManager.Instance != null && GameManager.Instance.CurrentSession.ActiveSaveSlot == slotIndex) {
                    SessionData session = GameManager.Instance.CurrentSession;
                    session.ActiveSaveSlot = -1;
                    GameManager.Instance.CurrentSession = session;
                }
                ClearNotice();
                return true;
            } catch (IOException exception) {
                SetNotice("save_notice_slot_delete_failed", (slotIndex + 1).ToString(), exception.Message);
                GD.PushError(LastLoadNotice);
                return false;
            } catch (UnauthorizedAccessException exception) {
                SetNotice("save_notice_slot_delete_failed", (slotIndex + 1).ToString(), exception.Message);
                GD.PushError(LastLoadNotice);
                return false;
            }
        }

        public bool LoadStorySlot(int slotIndex) {
            ValidateSlot(slotIndex);
            // No-save state (M-22): never touch, migrate, or corrupt-flag files we
            // cannot decode for lack of a key.
            if (_masterKey == null) {
                SaveSlots[slotIndex] = null;
                return false;
            }
            string path = GetStoryPath(slotIndex);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) {
                SaveSlots[slotIndex] = null;
                return false;
            }
            if (TryLoadPayload(path, "story", out string json, out bool backupUsed)) {
                try {
                    SaveSlots[slotIndex] = SaveSchemaMigrator.DeserializeStory(json);
                    if (backupUsed) SetNotice("save_notice_slot_recovered", (slotIndex + 1).ToString());
                    return true;
                } catch (SaveVersionException exception) {
                    SetNotice("save_notice_error", exception.Message);
                    return false;
                } catch (JsonException exception) {
                    SetNotice("save_notice_error", exception.Message);
                }
            }

            try {
                if (TryLoadLegacyStory(path, out StorySaveData migrated)) {
                    SaveSlots[slotIndex] = migrated;
                    SaveStorySlot(slotIndex);
                    SetNotice("save_notice_slot_migrated", (slotIndex + 1).ToString());
                    return true;
                }
            } catch (SaveVersionException exception) {
                SetNotice("save_notice_error", exception.Message);
                return false;
            }
            SaveSlots[slotIndex] = null;
            PreserveCorruptCandidates(path);
            SetNotice("save_notice_slot_corrupt", (slotIndex + 1).ToString());
            return false;
        }

        /// <summary>
        /// Package 11 A6b: grants every manifest roster character, mirroring the
        /// <c>UnlockedStages</c> backfill in <see cref="GlobalSaveData.Normalize"/>.
        ///
        /// <para>The initial-build pillar is "all characters available from the
        /// start", so a roster row added after a save was written must become
        /// available on that save — otherwise the character would exist in the
        /// manifest, in the content and on the select grid, and be locked
        /// forever. A future unlock phase replaces this rule deliberately.</para>
        ///
        /// <para>It lives on the autoload rather than on the payload because
        /// reading the manifest is Godot file I/O and <see cref="GlobalSaveData"/>
        /// must stay engine-free; see that field's remarks.</para>
        /// </summary>
        /// <remarks><c>internal</c> rather than <c>private</c> so
        /// <c>CharacterRosterTests</c> can pin the grant directly instead of
        /// reaching it through a full encrypted global load.</remarks>
        internal static void EnsureRosterUnlocked(GlobalSaveData data) {
            if (data == null) return;
            data.UnlockedCharacters ??= new List<string>();
            foreach (string characterID in CharacterRoster.IDs) {
                if (!data.UnlockedCharacters.Contains(characterID)) {
                    data.UnlockedCharacters.Add(characterID);
                }
            }
        }

        /// <summary>
        /// True while a global payload written at schema v5 or earlier is loaded
        /// and its <c>SeenDialogueIDs</c> has not yet been seeded from the story
        /// slots. Set by <see cref="LoadGlobalData"/>, consumed once by
        /// <see cref="SeedGlobalSeenDialogueIfPending"/>.
        /// </summary>
        private bool _globalSeenDialogueSeedPending;

        /// <summary>
        /// Package 11 Phase C: the deferred half of the v5 → v6 global migration.
        /// Unions every loaded slot's <c>ViewedDialogueIDs</c> into the global
        /// seen set, then persists the upgraded payload so the seed is not
        /// recomputed on every boot. A no-op on a payload that was already v6.
        /// </summary>
        /// <remarks><c>internal</c> so <c>SaveEnvelopeTests</c> can drive it
        /// without a full encrypted boot.</remarks>
        internal void SeedGlobalSeenDialogueIfPending() {
            if (!_globalSeenDialogueSeedPending) return;
            _globalSeenDialogueSeedPending = false;
            if (GlobalData == null) return;
            int added = SaveSchemaMigrator.SeedGlobalSeenDialogue(GlobalData, SaveSlots);
            if (added > 0) SaveGlobalData();
        }

        public bool SaveGlobalData() {
            GlobalData ??= new GlobalSaveData();
            GlobalData.Normalize();
            EnsureRosterUnlocked(GlobalData);
            return WritePayload(GetGlobalPath(), "global", GlobalData.SaveVersion, JsonConvert.SerializeObject(GlobalData));
        }

        public bool LoadGlobalData() {
            // No-save state (M-22): keep defaults, leave the files alone.
            if (_masterKey == null) {
                GlobalData = new GlobalSaveData();
                // A6b: the roster unlock set is filled here, not by a field
                // initializer and not by Normalize (see UnlockedCharacters), so
                // every path that hands out a fresh payload has to ask for it or
                // a new install comes up with no selectable characters.
                EnsureRosterUnlocked(GlobalData);
                return false;
            }
            string path = GetGlobalPath();
            if (!File.Exists(path) && !File.Exists(path + ".bak")) {
                GlobalData = new GlobalSaveData();
                EnsureRosterUnlocked(GlobalData);
                return false;
            }
            if (TryLoadPayload(path, "global", out string json, out bool backupUsed)) {
                try {
                    GlobalData = SaveSchemaMigrator.DeserializeGlobal(json, out int loadedVersion);
                    _globalSeenDialogueSeedPending = loadedVersion < SaveSchemaMigrator.CurrentVersion;
                    EnsureRosterUnlocked(GlobalData);
                    if (backupUsed) SetNotice("save_notice_global_recovered");
                    return true;
                } catch (SaveVersionException exception) {
                    SetNotice("save_notice_error", exception.Message);
                    return false;
                } catch (JsonException exception) {
                    SetNotice("save_notice_error", exception.Message);
                }
            }

            try {
                if (TryLoadLegacyGlobal(path, out GlobalSaveData migrated)) {
                    GlobalData = migrated;
                    EnsureRosterUnlocked(GlobalData);
                    SaveGlobalData();
                    SetNotice("save_notice_global_migrated");
                    return true;
                }
            } catch (SaveVersionException exception) {
                SetNotice("save_notice_error", exception.Message);
                return false;
            }
            GlobalData = new GlobalSaveData();
            EnsureRosterUnlocked(GlobalData);
            PreserveCorruptCandidates(path);
            SetNotice("save_notice_global_corrupt");
            return false;
        }

        /// <summary>
        /// Pushes <see cref="GlobalSaveData.InputBindings"/> into the InputMap.
        /// Called at boot right after the global payload loads; actions without an
        /// override keep their project defaults.
        /// </summary>
        public void ApplySavedInputBindings() {
            InputBindingService.Apply(GlobalData?.InputBindings);
        }

        /// <summary>
        /// Stores the Settings tab's working map as overrides (only the actions that
        /// differ from project.godot), applies it, and persists the global payload.
        /// </summary>
        public bool PersistInputBindings(InputBindingSet effective) {
            GlobalData ??= new GlobalSaveData();
            GlobalData.InputBindings = InputBindingService.BuildOverrides(effective);
            InputBindingService.Apply(GlobalData.InputBindings);
            return SaveGlobalData();
        }

        /// <summary>
        /// Global reset-to-default: restores project.godot's InputMap and clears the
        /// saved overrides so a later project change reaches the player.
        /// </summary>
        public bool ResetInputBindingsToDefault() {
            GlobalData ??= new GlobalSaveData();
            GlobalData.InputBindings = new InputBindingSet();
            InputBindingService.ResetToProjectDefaults();
            return SaveGlobalData();
        }

        public void SaveCheckpoint(string checkpointID) {
            if (GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length) {
                // ActiveSaveSlot boots as -1 (audit Low): a checkpoint outside a
                // story session must never fabricate a slot-0 save.
                GD.PushWarning($"Checkpoint '{checkpointID}' ignored: no active story save slot.");
                return;
            }
            SaveSlots[slot] ??= new StorySaveData {
                SelectedCharacterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "",
                Difficulty = GameManager.Instance.CurrentSession.Difficulty
            };
            SaveSlots[slot].LastCheckpointID = checkpointID ?? "";
            SaveSlots[slot].CurrentLevelID = StoryManager.Instance?.GetCurrentLevelPath()
                ?? SaveSlots[slot].CurrentLevelID;
            SaveSlots[slot].CurrentLives = StoryManager.Instance?.ChronalRewindsRemaining
                ?? SaveSlots[slot].CurrentLives;
            SaveSlots[slot].LevelChronalDust = StoryManager.Instance?.ChronalDustCollected
                ?? SaveSlots[slot].LevelChronalDust;
            if (GetTree().GetFirstNodeInGroup("StoryPlayer") is FTT.Characters.PlayerController player) {
                SaveSlots[slot].CurrentHP = player.CurrentHP;
                SaveSlots[slot].CurrentUltimateMeter = player.CurrentUltimateMeter;
            }
            // V7.3 mid-level resume: the checkpoint save carries the whole
            // attempt — activated checkpoints, font uses, destroyed
            // extractors, found secrets, live Timeline Integrity.
            StoryManager.Instance?.WriteAttemptStateToSave(SaveSlots[slot]);
            // A live session at a checkpoint refreshes the exit-guard marker.
            SessionExitGuard.WriteMarker(slot);
            SaveStorySlot(slot);
        }

        /// <summary>
        /// Designed autosave trigger: Resonance Grid unlocks persist to the
        /// active story slot immediately (AGENTS.md: "Autosave occurs at
        /// checkpoints, level completion, and unlock events").
        /// </summary>
        public void SaveTalentUnlock(string nodeID) {
            if (GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length || SaveSlots[slot] == null) return;
            SaveStorySlot(slot);
        }

        /// <summary>
        /// Campaign completion (Package 5 A1). Level 15's ending chain calls this
        /// once the restoration sequence and credits finish: it is the only writer
        /// of <see cref="StorySaveData.IsCompleted"/>, so UI code never pokes the
        /// field directly. Returns false when there is no active story slot.
        /// </summary>
        public bool MarkCampaignCompleted() {
            if (GameManager.Instance == null) return false;
            return MarkCampaignCompleted(GameManager.Instance.CurrentSession.ActiveSaveSlot);
        }

        /// <summary>
        /// Package 11 A4 (Resonance V7.6, F08). The one-time, versioned, FREE
        /// respec a pre-V7.6 save needs: every grid re-authored every node ID,
        /// so a loaded payload can carry purchases that no longer name a node.
        /// This drops each such purchase and refunds its recorded retired price
        /// into that character's deposited balance - removing the purchased
        /// node and its effects, refunding exactly once, preserving campaign
        /// progress and undeposited earnings, and never granting a missing node
        /// for free. Idempotent.
        ///
        /// <para>A4 ships the function; the Phase C closeout calls it from the
        /// single v5 to v6 migration step (plan §2.6) for every loaded story
        /// payload. Wiring it here rather than inside the envelope keeps the
        /// grid knowledge in one place.</para>
        /// </summary>
        /// <returns>Total dust refunded across every character on the save.</returns>
        public static int MigrateResonanceGridsToV76(StorySaveData save) =>
            FTT.Environment.ResonanceProgression.MigrateGridProgressToV76(save);

        /// <summary>Slot-explicit campaign completion; persists the slot immediately.</summary>
        public bool MarkCampaignCompleted(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SaveSlots.Length) return false;
            StorySaveData save = SaveSlots[slotIndex];
            if (save == null) return false;
            save.IsCompleted = true;
            return SaveStorySlot(slotIndex);
        }

        /// <summary>True when the active story slot has finished the campaign.</summary>
        public bool IsActiveCampaignCompleted() => GetActiveStorySave()?.IsCompleted == true;

        public bool IsPuzzleCompleted(string puzzleID) {
            StorySaveData save = GetActiveStorySave();
            return save?.CompletedPuzzleIDs?.Contains(puzzleID) == true;
        }

        public void SetPuzzleCompleted(string puzzleID, bool completed) {
            if (string.IsNullOrWhiteSpace(puzzleID)) return;
            StorySaveData save = GetActiveStorySave(createIfMissing: true);
            if (save == null) return;
            if (completed) {
                if (!save.CompletedPuzzleIDs.Contains(puzzleID)) save.CompletedPuzzleIDs.Add(puzzleID);
            } else {
                save.CompletedPuzzleIDs.Remove(puzzleID);
            }
        }

        private StorySaveData GetActiveStorySave(bool createIfMissing = false) {
            if (GameManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length) {
                if (createIfMissing) {
                    // Same fabrication guard as SaveCheckpoint: -1 is "no session".
                    GD.PushWarning("No active story save slot; refusing to fabricate one.");
                }
                return null;
            }
            if (SaveSlots[slot] == null && createIfMissing) {
                SaveSlots[slot] = new StorySaveData {
                    SelectedCharacterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "",
                    Difficulty = GameManager.Instance.CurrentSession.Difficulty
                };
            }
            return SaveSlots[slot];
        }

        public void SaveLevelCompletion(string completedLevelID) {
            if (GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveSlots.Length || SaveSlots[slot] == null) return;
            if (!SaveSlots[slot].CompletedLevels.Contains(completedLevelID)) {
                SaveSlots[slot].CompletedLevels.Add(completedLevelID);
            }
            SaveSlots[slot].CurrentLevelID = StoryManager.Instance?.GetCurrentLevelPath()
                ?? SaveSlots[slot].CurrentLevelID;
            SaveSlots[slot].LevelChronalDust = StoryManager.Instance?.ChronalDustCollected
                ?? SaveSlots[slot].LevelChronalDust;
            // The completed attempt is over: the next level starts fresh. An
            // empty LastCheckpointID is also what marks the next entry as
            // fresh rather than a mid-level resume (V7.3).
            SaveSlots[slot].LastCheckpointID = "";
            SaveSlots[slot].ActivatedCheckpointIDs.Clear();
            SaveSlots[slot].DestroyedExtractorIDs.Clear();
            SaveSlots[slot].FoundSecretIDs.Clear();
            // Package 11 A10 (F05): a completed level's source claims retire
            // with the attempt — completed levels cannot be replayed, and the
            // next level compiles its own ledger.
            SaveSlots[slot].ClaimedRewardSourceIDs.Clear();
            SaveSlots[slot].FontUsesConsumed.Clear();
            SaveSlots[slot].LevelIntegrityPercent = 100f;
            // Package 11 A3b: the completed attempt is over. The next level's
            // entry mints its own record; leaving this one behind would let a
            // stale Smothered status or anchor count route the next load.
            SaveSlots[slot].AttemptState = new StoryAttemptState();
            SaveSlots[slot].AnchorCharges = 0;
            // Package 11 A1b (F10): and the next level's fresh entry starts
            // with Defy History unused.
            SaveSlots[slot].StoryDefyHistoryUsed = false;
            SaveStorySlot(slot);
        }

        private bool WritePayload(string path, string payloadType, int schemaVersion, string json) {
            if (_masterKey == null) {
                // Explicit no-save state (M-22): refuse loudly rather than fail
                // silently, and never write anything without a verified key.
                GD.PushWarning($"Refusing to save {payloadType} data: the save system is unavailable (save key error).");
                return false;
            }
            try {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                byte[] encoded = SaveEnvelopeCodec.Encode(payloadType, schemaVersion, json, _masterKey, timestamp);
                AtomicSaveStore.Write(path, encoded, candidate =>
                    SaveEnvelopeCodec.TryDecode(candidate, _masterKey, out DecodedSaveEnvelope decoded, out _)
                    && decoded.PayloadType == payloadType);
                return true;
            } catch (Exception exception) {
                GD.PushError($"Unable to save {payloadType} data: {exception.Message}");
                return false;
            }
        }

        private bool TryLoadPayload(string path, string expectedPayloadType, out string json, out bool backupUsed) {
            json = "";
            backupUsed = false;
            foreach ((string candidatePath, bool isBackup) in AtomicSaveStore.ReadCandidates(path)) {
                try {
                    byte[] bytes = File.ReadAllBytes(candidatePath);
                    if (!SaveEnvelopeCodec.TryDecode(bytes, _masterKey, out DecodedSaveEnvelope decoded, out _)) continue;
                    if (decoded.PayloadType != expectedPayloadType) continue;
                    json = decoded.Json;
                    backupUsed = isBackup;
                    return true;
                } catch (IOException) { }
            }
            return false;
        }

        /// <summary>
        /// One-time development migration of the pre-envelope Base64 JSON format.
        /// Gated to debug builds (audit H-2): in release this is a standing
        /// authentication bypass — plain data loads unauthenticated and is
        /// re-signed under the real key. Internal for the gate's unit test.
        /// </summary>
        internal static bool TryLoadLegacyStory(string path, out StorySaveData data) {
            data = null;
            if (!IsLegacyMigrationEnabled) return false;
            try {
                if (!File.Exists(path)) return false;
                string encoded = File.ReadAllText(path);
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                data = SaveSchemaMigrator.DeserializeStory(json);
                return true;
            } catch (Exception exception) when (exception is FormatException || exception is JsonException || exception is IOException) {
                return false;
            }
        }

        /// <summary>Raw plain-JSON legacy global load; same debug-only gate as the story path (H-2).</summary>
        internal static bool TryLoadLegacyGlobal(string path, out GlobalSaveData data) {
            data = null;
            if (!IsLegacyMigrationEnabled) return false;
            try {
                if (!File.Exists(path)) return false;
                data = SaveSchemaMigrator.DeserializeGlobal(File.ReadAllText(path));
                return true;
            } catch (Exception exception) when (exception is JsonException || exception is IOException) {
                return false;
            }
        }

        private static void PreserveCorruptCandidates(string path) {
            foreach ((string candidatePath, _) in AtomicSaveStore.ReadCandidates(path)) {
                try {
                    string corruptPath = candidatePath + ".corrupt";
                    if (!File.Exists(corruptPath)) File.Copy(candidatePath, corruptPath);
                } catch (IOException) { }
            }
        }

        private static string GetStoryPath(int slotIndex) =>
            Path.Combine(ProjectSettings.GlobalizePath(SaveDir), $"story_slot_{slotIndex}.sav");

        private static string GetGlobalPath() =>
            Path.Combine(ProjectSettings.GlobalizePath(SaveDir), GlobalFileName);

        private static void ValidateSlot(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 3) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }
    }
}
