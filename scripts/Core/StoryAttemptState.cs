using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// Package 11 A3b (F10). Where a story attempt is, which decides where a load
    /// goes. Committed <b>before</b> the presentation of whatever caused the
    /// transition, so a crash mid-beat still resumes the settled outcome.
    ///
    /// <para>Append-only: the ordinals serialize into save payloads.</para>
    /// </summary>
    public enum StoryAttemptStatus {
        /// <summary>Live play. Load resumes at the last activated checkpoint.</summary>
        Active = 0,
        /// <summary>Acts I–II Timeline Collapse: the hub portal finishes the paid recovery.</summary>
        AwaitingHubResume = 1,
        /// <summary>A recovery (death / Collapse / Snap) is committed but not yet applied.</summary>
        RecoveryPending = 2,
        /// <summary>Act III terminal failure. Load returns to the Game Over screen, always.</summary>
        Smothered = 3,
        /// <summary>A completion transaction is committed and the run is routing forward.</summary>
        CompletionPending = 4,
        /// <summary>The attempt finished; its level is banked.</summary>
        Completed = 5,
        /// <summary>
        /// Migration could not reconstruct essential history. The save and every
        /// balance are preserved untouched; only an explicit Restart Level continues.
        /// </summary>
        LegacyRecoveryRequired = 6
    }

    /// <summary>What produced the committed <see cref="StoryRecoveryEvent"/>.</summary>
    public enum StoryRecoveryCause {
        None = 0,
        DeathRewind = 1,
        /// <summary>Acts I–II Timeline Collapse (hub extraction).</summary>
        Collapse = 2,
        /// <summary>Act III Anchor Snap (in place, spends one anchor charge).</summary>
        AnchorSnap = 3,
        /// <summary>Act III collapse with no anchor left. Terminal.</summary>
        Smothered = 4
    }

    /// <summary>F05 lifecycle of one stable reward source.</summary>
    public enum StoryRewardSourceState {
        Unissued = 0,
        SpawnedUncollected = 1,
        Collected = 2
    }

    /// <summary>
    /// The anchor a reconstruction rebuilds from: which fracture, its authored
    /// <see cref="FTT.Environment.CheckpointRole"/>, the authored encounter
    /// baseline that anchor restores, and the once-per-attempt ledgers.
    /// </summary>
    public class StoryCheckpointRecord {
        public string AnchorID = "";
        /// <summary>Stored as an int so <c>CheckpointRole</c> can grow without a payload break.</summary>
        public int AnchorRole;
        /// <summary>Bumped whenever a level's authored baseline layout changes.</summary>
        public int BaselineVersion = 1;
        /// <summary>Stable encounter IDs this anchor restores as already cleared.</summary>
        public List<string> BaselineEncounterIDs = new();
        public List<string> ActivatedCheckpointIDs = new();
        /// <summary>
        /// Granted-benefit IDs (Mending + rewind refresh). Distinct from the
        /// activation list: loading or a paid recovery re-reads activation, but a
        /// benefit already granted must never be granted again.
        /// </summary>
        public List<string> GrantedBenefitIDs = new();

        public void Normalize() {
            AnchorID ??= "";
            BaselineEncounterIDs ??= new List<string>();
            ActivatedCheckpointIDs ??= new List<string>();
            GrantedBenefitIDs ??= new List<string>();
            if (BaselineVersion < 1) BaselineVersion = 1;
        }
    }

    /// <summary>
    /// A Restoration Font channel that completed but has not finished crediting.
    /// An ordinary reload resumes only the uncredited remainder; a death recovery
    /// that replaces HP cancels it <b>without</b> refunding the spent use.
    /// </summary>
    public class StoryPendingHealing {
        public string SourceID = "";
        public float UncreditedAmount;
        public float RemainingSeconds;

        public bool IsActive => !string.IsNullOrWhiteSpace(SourceID) && UncreditedAmount > 0f;
        public void Clear() { SourceID = ""; UncreditedAmount = 0f; RemainingSeconds = 0f; }
        public void Normalize() {
            SourceID ??= "";
            if (UncreditedAmount < 0f) UncreditedAmount = 0f;
            if (RemainingSeconds < 0f) RemainingSeconds = 0f;
        }
    }

    /// <summary>
    /// One F05 reward source. The fixed drop outcome is stored so a reload cannot
    /// reroll a random drop, and the pickup type/location so a spawned-but-
    /// uncollected pickup restores once rather than duplicating.
    /// </summary>
    public class StoryRewardSourceRecord {
        public string SourceID = "";
        public StoryRewardSourceState State = StoryRewardSourceState.Unissued;
        public int Quantity;
        public string PickupType = "";
        public float PositionX;
        public float PositionY;
        /// <summary>-1 = no randomized outcome has been fixed yet.</summary>
        public int FixedDropOutcome = -1;

        public void Normalize() {
            SourceID ??= "";
            PickupType ??= "";
            if (Quantity < 0) Quantity = 0;
        }
    }

    /// <summary>
    /// Continuous player resources, snapshotted at least once per live second and
    /// folded into every critical event. Loading restores these; it never refills
    /// them. A temporary granted shield charge is deliberately absent — F10
    /// discards it on reconstruction.
    /// </summary>
    public class StoryPlayerResourceTimers {
        public int BlockCharges = -1;
        public float BlockRegenSeconds;
        public float BlockLockoutSeconds;
        public Dictionary<string, float> AbilityCooldowns = new();
        public int EchoStepCooldownFrames;
        /// <summary>Rally damage-taken meter earned but not yet credited.</summary>
        public float RallyUncreditedMeter;
        public float TimeFreezeCooldownSeconds;
        /// <summary>D02d Wardenclyffe remaining damage-recharge delay.</summary>
        public float WardenclyffeRechargeSeconds;

        public void Normalize() {
            AbilityCooldowns ??= new Dictionary<string, float>();
            if (BlockRegenSeconds < 0f) BlockRegenSeconds = 0f;
            if (BlockLockoutSeconds < 0f) BlockLockoutSeconds = 0f;
            if (EchoStepCooldownFrames < 0) EchoStepCooldownFrames = 0;
            if (RallyUncreditedMeter < 0f) RallyUncreditedMeter = 0f;
            if (TimeFreezeCooldownSeconds < 0f) TimeFreezeCooldownSeconds = 0f;
            if (WardenclyffeRechargeSeconds < 0f) WardenclyffeRechargeSeconds = 0f;
        }
    }

    /// <summary>
    /// A death / Collapse / Snap / Smothered outcome, resolved and committed
    /// <b>before</b> its presentation. A reload finishes the committed event once:
    /// it never re-spends, re-charges the fee, or recalculates the F11 minimum.
    /// </summary>
    public class StoryRecoveryEvent {
        public string EventID = "";
        public StoryRecoveryCause Cause = StoryRecoveryCause.None;
        public bool Applied;
        public string CheckpointID = "";
        public int CheckpointRole;
        public Difficulty Difficulty = Difficulty.Normal;
        public int ResolvedHP;
        public int ResolvedRewinds;
        public int ResolvedAnchorCharges;
        public int ResolvedWalletDust;
        public float ResolvedIntegrityPercent;
        /// <summary>Version of the authored F11 route budget used, for auditing.</summary>
        public int BudgetVersion = 1;
        /// <summary>The resolved F11 minimum, or 0 for a non-timer cause.</summary>
        public float ResolvedRecoveryMinimum;
        /// <summary>True once the 20% undeposited-dust fee has been charged for this event.</summary>
        public bool FeeCharged;

        public bool IsPending => !string.IsNullOrWhiteSpace(EventID) && !Applied;

        public void Normalize() {
            EventID ??= "";
            CheckpointID ??= "";
            if (BudgetVersion < 1) BudgetVersion = 1;
            if (ResolvedHP < 0) ResolvedHP = 0;
            if (ResolvedRewinds < 0) ResolvedRewinds = 0;
            if (ResolvedAnchorCharges < 0) ResolvedAnchorCharges = 0;
            if (ResolvedWalletDust < 0) ResolvedWalletDust = 0;
            ResolvedIntegrityPercent = Math.Clamp(ResolvedIntegrityPercent, 0f, 100f);
        }
    }

    /// <summary>
    /// N01: the boss is down and the sealing anchor is armed, but nothing is
    /// deposited or unlocked until the player accepts the sealing interaction.
    /// </summary>
    public class StorySealReadiness {
        public bool BossDefeated;
        public string SealingAnchorID = "";
        public bool PendingBossPickup;

        public void Normalize() => SealingAnchorID ??= "";
    }

    /// <summary>
    /// N01's accepted sealing transaction. Retained until its transition is
    /// finalized, so crash recovery of a completion overlay resumes the same
    /// commit rather than reopening the level or replaying the deposit.
    /// </summary>
    public class StoryCompletionTransaction {
        public string TransactionID = "";
        public int RetainedBaseDust;
        public int TierBonusDust;
        public int DepositAmount;
        public float FinalIntegrityPercent;
        public bool Sealed;
        public string Destination = "";
        public bool Applied;

        public bool IsPending => !string.IsNullOrWhiteSpace(TransactionID) && !Applied;

        public void Normalize() {
            TransactionID ??= "";
            Destination ??= "";
            if (RetainedBaseDust < 0) RetainedBaseDust = 0;
            if (TierBonusDust < 0) TierBonusDust = 0;
            if (DepositAmount < 0) DepositAmount = 0;
            FinalIntegrityPercent = Math.Clamp(FinalIntegrityPercent, 0f, 100f);
        }
    }

    /// <summary>
    /// Package 11 A3b — the F10 per-attempt record
    /// (<c>docs/design-contracts/STORY_PERSISTENCE.md</c> §"Required schema and
    /// ownership").
    ///
    /// <para><b>What an attempt is.</b> A story slot + level ID + a unique
    /// <see cref="AttemptID"/>. Loading a scene, returning from the hub, activating
    /// a checkpoint, dying, or replaying a presentation does <b>not</b> create one.
    /// Only first entry into the next uncompleted level, or an explicit full
    /// Restart Level, mints a new ID.</para>
    ///
    /// <para><b>Root fields stay root.</b> <c>CurrentHP</c>, <c>CurrentUltimateMeter</c>,
    /// <c>CurrentLives</c>, <c>CheckpointIntegrityPercent</c>, <c>LevelChronalDust</c>,
    /// <c>DepositedChronalDust</c>, <c>GridProgress</c> and the campaign-progression
    /// fields are <i>not</i> duplicated here — the contract forbids a mutable second
    /// copy of a root field inside the record.</para>
    ///
    /// <para><b>Legacy mirrors.</b> The six V7.3 loose attempt fields on
    /// <c>StorySaveData</c> (<c>ActivatedCheckpointIDs</c>, <c>DestroyedExtractorIDs</c>,
    /// <c>FoundSecretIDs</c>, <c>FontUsesConsumed</c>, <c>LevelIntegrityPercent</c>,
    /// <c>HasSeenCollapseBeat</c>) are the trustworthy existing records F10 tells the
    /// migration to derive from. They are still written in the same transaction as
    /// this record so a v5 reader is never handed a half-migrated payload; Phase C's
    /// single v5→v6 step calls <see cref="MigrateFromLegacyRoot"/> and may then retire
    /// them. Do not add a <i>new</i> consumer of the loose fields.</para>
    /// </summary>
    public class StoryAttemptState {
        public string AttemptID = "";
        public string LevelID = "";
        /// <summary>Monotonic per attempt. An older asynchronous write never replaces a newer one.</summary>
        public long Revision;
        public StoryAttemptStatus Status = StoryAttemptStatus.Active;

        /// <summary>Act III Beacon charges left. Always zero outside Act III.</summary>
        public int AnchorChargesRemaining;
        /// <summary>The cap this attempt was initialized with (Easy 3 / Normal 2 / Hard 1).</summary>
        public int AnchorChargeMaximum;

        /// <summary>F10: Story Defy History is once per <i>attempt</i>, not per level entry.</summary>
        public bool DefyHistoryUsed;

        public StoryCheckpointRecord CheckpointRecord = new();

        public float CurrentIntegrity = 100f;
        /// <summary>Immutable for the attempt: the F01 denominator is never recomputed from survivors.</summary>
        public int StartingExtractorCount;
        public List<string> DestroyedExtractorIDs = new();
        public List<string> FoundSecretIDs = new();
        public Dictionary<string, bool> PuzzleStateByID = new();

        public bool PreBossLocked;
        /// <summary>The N05 contribution, banked at the PreBoss lock. Negative = not reached.</summary>
        public float FinalGateIntegrity = -1f;

        public Dictionary<string, int> FontUsesByID = new();
        public List<string> CollectedHealingIDs = new();
        public StoryPendingHealing PendingHealing = new();

        public Dictionary<string, StoryRewardSourceRecord> RewardSources = new();
        public StoryPlayerResourceTimers PlayerResourceTimers = new();
        public StoryRecoveryEvent RecoveryEvent = new();
        public StorySealReadiness SealReadiness = new();
        public StoryCompletionTransaction CompletionTransaction = new();

        public List<string> AppliedScriptEffectIDs = new();
        public List<string> PendingGlobalSeenIDs = new();
        public Dictionary<string, bool> PresentationFlags = new();

        /// <summary>True when this record has ever been minted (as opposed to a default-constructed v5 payload).</summary>
        public bool HasAttempt => !string.IsNullOrWhiteSpace(AttemptID);

        /// <summary>
        /// Mints a brand-new attempt. The ONLY two callers are fresh entry into a
        /// level and an explicit full Restart Level (including Restart from the
        /// Smothered Game Over).
        /// </summary>
        public static StoryAttemptState CreateFresh(string levelID, int anchorCharges) => new() {
            AttemptID = Guid.NewGuid().ToString("N"),
            LevelID = levelID ?? "",
            Revision = 1,
            Status = StoryAttemptStatus.Active,
            AnchorChargesRemaining = Math.Max(0, anchorCharges),
            AnchorChargeMaximum = Math.Max(0, anchorCharges),
            CurrentIntegrity = 100f
        };

        /// <summary>Monotonic bump. Every committed change moves the revision exactly once.</summary>
        public long Bump() => ++Revision;

        /// <summary>
        /// Migrates a pre-v6 payload's loose attempt fields into this record.
        /// Called by Phase C's single v5→v6 step, and defensively at load.
        ///
        /// <para>F10's hard rule: <b>never default an active legacy attempt to full
        /// anchors / unused Defy / unused healing / unclaimed rewards.</b> A payload
        /// that is parked mid-level (a non-empty <c>LastCheckpointID</c>) but carries
        /// no attempt record cannot have its anchors, Defy or reward claims
        /// reconstructed — so it is preserved untouched and marked
        /// <see cref="StoryAttemptStatus.LegacyRecoveryRequired"/> with <b>zero</b>
        /// anchors, which the UI explains as "Restart Level to continue". A payload
        /// that is not mid-level has nothing to lose and migrates cleanly.</para>
        /// </summary>
        /// <returns>True when a migration actually ran.</returns>
        public static bool MigrateFromLegacyRoot(StorySaveData save) {
            if (save == null) return false;
            save.AttemptState ??= new StoryAttemptState();
            StoryAttemptState attempt = save.AttemptState;
            if (attempt.HasAttempt) return false;

            bool midLevel = !string.IsNullOrWhiteSpace(save.LastCheckpointID);
            attempt.AttemptID = Guid.NewGuid().ToString("N");
            attempt.LevelID = save.CurrentLevelID ?? "";
            attempt.Revision = 1;
            attempt.CurrentIntegrity = Math.Clamp(save.LevelIntegrityPercent, 0f, 100f);
            attempt.CheckpointRecord.AnchorID = save.LastCheckpointID ?? "";
            attempt.CheckpointRecord.ActivatedCheckpointIDs =
                new List<string>(save.ActivatedCheckpointIDs ?? new List<string>());
            // A checkpoint recorded as activated in v5 had already been paid its
            // Mending and rewind refresh: carrying it across as a granted benefit
            // is the conservative reading, and the alternative would pay twice.
            attempt.CheckpointRecord.GrantedBenefitIDs =
                new List<string>(attempt.CheckpointRecord.ActivatedCheckpointIDs);
            attempt.DestroyedExtractorIDs = new List<string>(save.DestroyedExtractorIDs ?? new List<string>());
            attempt.FoundSecretIDs = new List<string>(save.FoundSecretIDs ?? new List<string>());
            attempt.FontUsesByID = new Dictionary<string, int>(save.FontUsesConsumed ?? new Dictionary<string, int>());
            attempt.PresentationFlags["collapse_beat_seen"] = save.HasSeenCollapseBeat;
            attempt.PlayerResourceTimers.TimeFreezeCooldownSeconds = Math.Max(0f, save.TimeFreezeCooldownSeconds);

            if (midLevel) {
                // No anchors, no unused-Defy claim, no reward ledger can be
                // reconstructed — so claim none of them and say so.
                attempt.AnchorChargesRemaining = 0;
                attempt.AnchorChargeMaximum = 0;
                attempt.DefyHistoryUsed = true;
                attempt.Status = StoryAttemptStatus.LegacyRecoveryRequired;
            } else {
                attempt.Status = StoryAttemptStatus.Active;
            }
            attempt.Normalize();
            return true;
        }

        public void Normalize() {
            AttemptID ??= "";
            LevelID ??= "";
            if (Revision < 0) Revision = 0;
            CheckpointRecord ??= new StoryCheckpointRecord();
            CheckpointRecord.Normalize();
            DestroyedExtractorIDs ??= new List<string>();
            FoundSecretIDs ??= new List<string>();
            PuzzleStateByID ??= new Dictionary<string, bool>();
            FontUsesByID ??= new Dictionary<string, int>();
            CollectedHealingIDs ??= new List<string>();
            PendingHealing ??= new StoryPendingHealing();
            PendingHealing.Normalize();
            RewardSources ??= new Dictionary<string, StoryRewardSourceRecord>();
            foreach (StoryRewardSourceRecord record in RewardSources.Values) record?.Normalize();
            PlayerResourceTimers ??= new StoryPlayerResourceTimers();
            PlayerResourceTimers.Normalize();
            RecoveryEvent ??= new StoryRecoveryEvent();
            RecoveryEvent.Normalize();
            SealReadiness ??= new StorySealReadiness();
            SealReadiness.Normalize();
            CompletionTransaction ??= new StoryCompletionTransaction();
            CompletionTransaction.Normalize();
            AppliedScriptEffectIDs ??= new List<string>();
            PendingGlobalSeenIDs ??= new List<string>();
            PresentationFlags ??= new Dictionary<string, bool>();
            CurrentIntegrity = Math.Clamp(CurrentIntegrity, 0f, 100f);
            if (FinalGateIntegrity > 100f) FinalGateIntegrity = 100f;
            if (StartingExtractorCount < 0) StartingExtractorCount = 0;
            if (AnchorChargeMaximum < 0) AnchorChargeMaximum = 0;
            AnchorChargesRemaining = Math.Clamp(AnchorChargesRemaining, 0, AnchorChargeMaximum);
        }
    }
}
