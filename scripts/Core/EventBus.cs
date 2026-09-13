using System;
using Godot;

namespace FTT.Core {

    // Payload structs
    public struct PlayerHPPayload {
        public int PlayerIndex;
        public float CurrentHP;
        public float MaxHP;
        public float DamageAmount;
    }

    public struct CooldownPayload {
        public int PlayerIndex;
        public AbilitySlot Slot;
        public float Duration;
    }

    /// <summary>
    /// M-27: raised by <see cref="FTT.Combat.BlockSystem"/> whenever a player's
    /// shield-charge count (or capacity) changes, so the Story HUD's pips track
    /// blocks, guard breaks, and interval regen without polling the player.
    /// </summary>
    public struct BlockChargesPayload {
        public int PlayerIndex;
        public int CurrentCharges;
        public int MaxCharges;
    }

    public enum AbilitySlot {
        Special1,
        Special2,
        MovementAbility,
        Ultimate
    }

    public struct MovementAbilityPayload {
        public int PlayerIndex;
        public string AbilityName;
        public Vector2 StartPosition;
        public Vector2 EndPosition;
    }

    public struct HazardStatePayload {
        public string HazardID;
        public HazardPhase Phase;
        public float Duration;
    }

    public enum HazardPhase {
        Warning,
        Active,
        Cooldown
    }

    public struct EnemyKilledPayload {
        public string EnemyID;
        public Vector2 Position;
        public int ChronalDustDrop;
        public bool IsElite;
        /// <summary>
        /// Per-enemy multiplier applied on top of the difficulty drop profile's
        /// random-item chance. 1.0 is the neutral default.
        /// </summary>
        public float ItemDropChanceMultiplier;
    }

    public struct BossSpawnedPayload {
        public string BossID;
        public string DisplayNameKey;
        public int CurrentHP;
        public int MaxHP;
        public Vector2 Position;
    }

    public struct BossHPPayload {
        public string BossID;
        public int CurrentHP;
        public int MaxHP;
    }

    public struct BossDefeatedPayload {
        public string BossID;
        public Vector2 Position;
        public int ChronalDustDrop;
    }

    /// <summary>
    /// V7.3 Single Icon Rule: which system produced a physical dust award.
    /// Mob is the ordinary kill-drop path; Extractor and Boss awards never
    /// expire and force the Large visual tier.
    /// </summary>
    public enum DustAwardSource {
        Mob = 0,
        Extractor = 1,
        Boss = 2
    }

    /// <summary>Attribution companion to the wallet's OnChronalDustCollected,
    /// raised once when a sourced dust pickup is actually collected.</summary>
    public struct DustAwardCollectedPayload {
        public int Amount;
        public DustAwardSource Source;
    }

    // === Package 11 A3 — Timeline Integrity as the level timer (F01) ======

    /// <summary>Level-end Integrity tier (V7.6: Restored 50+, Stabilized 20+, Fractured below).</summary>
    public enum IntegrityTier {
        Restored = 0,
        Stabilized = 1,
        Fractured = 2
    }

    /// <summary>
    /// The F01 level clock, republished whenever the gauge, the live rate or
    /// the pause state moves. A8 owns the HUD that renders it; nothing else
    /// may read the gauge by polling a level controller.
    /// </summary>
    public struct IntegrityPayload {
        public float Percent;
        public IntegrityTier Tier;
        public int LivingExtractors;
        public float DrainPerSecond;
        public bool Frozen;
    }

    /// <summary>Collapse Tremor stage: 0 = off, 1 = below 20%, 2 = below 10%.</summary>
    public struct TremorPayload {
        public int Level;
    }

    /// <summary>Phase of an enemy/boss ability the presentation layer can bind to.</summary>
    public enum EnemyPresentationPhase {
        Telegraph,
        Active,
        Recovery,
        Death
    }

    public struct EnemyPresentationPayload {
        public string SourceID;
        public string AbilityID;
        /// <summary>Authored hook string; Package 8 binds real VFX/SFX to it.</summary>
        public string PresentationEventID;
        public EnemyPresentationPhase Phase;
        public Vector2 Position;
    }

    public struct StatusEffectPayload {
        public int TargetIndex;
        public StatusType Type;
        public float Duration;
        public float Intensity;
    }

    public struct UltimateMeterPayload {
        public int PlayerIndex;
        public float CurrentValue;
        public float NormalizedValue;
        public bool IsFull;
    }

    public struct HyperArmorPayload {
        public int PlayerIndex;
        public bool IsActive;
    }

    /// <summary>
    /// A combatant's own attack connected. Raised for the attacker so feedback
    /// layers (haptics, hit VFX, hit stop) can respond without polling hitboxes.
    /// </summary>
    public struct HitConfirmPayload {
        /// <summary>Attacking player slot, or -1 for an enemy/boss attacker.</summary>
        public int PlayerIndex;
        public string AttackID;
        public float DamageApplied;
        /// <summary>True for special/ultimate class hits; drives stronger feedback.</summary>
        public bool IsHeavy;
        public Vector2 Position;
    }

    public struct UltimateActivationPayload {
        public int PlayerIndex;
        public string AbilityID;
        public Vector2 Position;
    }

    public struct PuzzleStatePayload {
        public string PuzzleID;
        public bool IsCompleted;
        public string Reason;
    }

    public struct RoomTransitionPayload {
        public string RoomID;
        public Rect2 CameraBounds;
    }

    public struct ExtractorStatePayload {
        public string ExtractorID;
        public int CurrentHP;
        public int MaxHP;
        public bool IsDestroyed;
    }

    /// <summary>
    /// V7.3: checkpoint activation with the once-per-attempt flag.
    /// FirstActivation gates the Mending heal and the rewind-pool refresh;
    /// respawn anchor and save update on every activation regardless.
    /// </summary>
    public struct CheckpointReachedPayload {
        public string CheckpointID;
        public bool FirstActivation;
    }

    public enum RewindPresentationPhase {
        Started,
        Playback,
        Landed,
        TimelineCollapse
    }

    public struct RewindPresentationPayload {
        public RewindPresentationPhase Phase;
        public Vector2 TargetPosition;
        public bool GhostTrailEnabled;
        public bool ScreenTintEnabled;
        public bool ScanlinesEnabled;
        public float MusicDuckDecibels;
        public bool ReverseSweepEnabled;
        public bool ClockTickEnabled;
        /// <summary>V7.3 Timeline Collapse beat: true when the 4 s collapse
        /// presentation may be skipped (not the first viewing).</summary>
        public bool CollapseSkipPromptEnabled;
    }

    // === Package 11 A2 — Time Freeze (F03) ================================

    /// <summary>The three states the Story HUD's Time Freeze indicator renders.</summary>
    public enum TimeFreezeState {
        Ready,
        Active,
        Cooldown
    }

    /// <summary>
    /// V7.6 Time Freeze readiness. Deliberately its own event and its own HUD
    /// surface: the design forbids attaching this cooldown to the death-rewind
    /// counter, which is what the retired V7.3 rewind pip did.
    /// </summary>
    public struct TimeFreezePayload {
        public TimeFreezeState State;
        /// <summary>Freeze seconds left while Active; cooldown seconds left while Cooldown; 0 when Ready.</summary>
        public float SecondsRemaining;
    }

    /// <summary>
    /// Local Fighter Mode presentation beats. Purely cosmetic: the deterministic
    /// simulation never reads these, and the driver raises them from its own
    /// presentation clock (design-godot.md Section 11 KO sequence). Package 8 can
    /// restyle the whole sequence by consuming this payload differently.
    /// </summary>
    public enum FighterPresentationPhase {
        /// <summary>Pre-match 3-2-1 tick. <c>CountdownValue</c> carries the digit.</summary>
        Countdown,
        /// <summary>"GO!" — the simulation is live.</summary>
        MatchStart,
        /// <summary>A fighter lost a stock but the match continues.</summary>
        StockLost,
        /// <summary>Both fighters frozen on the killing blow.</summary>
        HitFreeze,
        /// <summary>Losing fighter's defeat beat at reduced pace.</summary>
        SlowMotion,
        /// <summary>Background dim plus winner highlight.</summary>
        Spotlight,
        /// <summary>Large centred "K.O.!" stamp.</summary>
        KOStamp,
        /// <summary>Large centred "DRAW" stamp; no victory or defeat beat plays.</summary>
        DrawStamp,
        /// <summary>Winner victory-pose hold before the results screen.</summary>
        WinnerPose,
        /// <summary>Sequence finished; the results screen takes over.</summary>
        Results,
        /// <summary>V7.1 Overtime entry at 1:00 — "Timeline Destabilizing".</summary>
        OvertimeStamp,
        /// <summary>V7.1 Sudden Death entry — both fighters at 1 HP, first hit wins.</summary>
        SuddenDeathStamp
    }

    public struct FighterPresentationPayload {
        public FighterPresentationPhase Phase;
        /// <summary>Winner slot, or -1 when there is no winner yet / the match drew.</summary>
        public int WinnerPlayerID;
        /// <summary>Player slot the beat is about (stock loss, KO victim), else -1.</summary>
        public int SubjectPlayerID;
        public bool IsTrueTie;
        /// <summary>3, 2, 1 during the countdown; 0 for "GO!" and every other phase.</summary>
        public int CountdownValue;
        /// <summary>Intended length of this beat in seconds.</summary>
        public float DurationSeconds;
        /// <summary>Presentation focus point in world pixels (KO location / winner).</summary>
        public Vector2 FocusPosition;
    }

    /// <summary>
    /// Append-only. The ordinals serialize into authored <c>.tres</c> data and
    /// the deterministic simulation casts this enum to <c>int</c> in ~30 places,
    /// so a member is never reordered, renumbered, or reused.
    /// </summary>
    public enum StatusType {
        None,
        TimeDilation,
        Venom,
        StaticCharge,
        RadiantBurn,
        Root,
        /// <summary>
        /// V7.6 (Package 11 A1): the Eraser line's Null Lance. Control slot,
        /// <b>Story-only source</b> — the deterministic simulation refuses it
        /// outright (<c>FighterEntitySystems.ApplyStatus</c>). Locks the
        /// Special 1 / Special 2 / Movement Ability / Ultimate cast while
        /// cooldowns keep ticking; basics, block, grab, Rally, DI, landing tech,
        /// Defy History, death rewinds and Time Freeze are untouched.
        /// </summary>
        Suppression = 6
    }

    // === Package 11 A5: ability slot lock states + the F13 Defy seal =======
    // Appended per plan §2.9. Publishers never touch the HUD; A8 subscribes.

    /// <summary>
    /// F13 Defy History seal. <c>Building</c> = unused but below a full meter
    /// (the dim intact seal); <c>Ready</c> = lit, a full meter will refuse the
    /// next lethal hit; <c>Spent</c> = broken, and refilling the meter does not
    /// restore it; <c>Barred</c> = unavailable in this context.
    /// </summary>
    public enum DefySealState {
        Building,
        Ready,
        Spent,
        Barred
    }

    public struct DefySealPayload {
        public int PlayerIndex;
        public DefySealState State;
    }

    public enum MatchState {
        PreMatch,
        Countdown,
        InProgress,
        Paused,
        KOSequence,
        PostMatch,
        Results
    }

    // === Package 11 A1: ability-slot lock states (V7.5/V7.6) ===

    /// <summary>
    /// Why an ability slot is unavailable. <c>Dormant</c> = not yet granted by the
    /// Legacy Unlock Schedule (published by A5); <c>Suppressed</c> = the
    /// <see cref="StatusType.Suppression"/> cast lock (published by
    /// <c>PlayerController</c>); <c>Clear</c> = usable. Locked slots are shown,
    /// never hidden.
    /// </summary>
    public enum AbilitySlotLockState {
        Clear = 0,
        Dormant = 1,
        Suppressed = 2
    }

    public struct AbilitySlotLockPayload {
        public AbilitySlot Slot;
        public AbilitySlotLockState State;
    }

    public partial class EventBus : Node {
        public static EventBus Instance { get; private set; }

        public override void _Ready() {
            Instance = this;
        }

        // === Combat & Health Events ===
        public event Action<PlayerHPPayload> OnPlayerHPChanged;
        public void RaisePlayerHPChanged(PlayerHPPayload payload) => OnPlayerHPChanged?.Invoke(payload);

        public event Action<int> OnPlayerDied;
        public void RaisePlayerDied(int playerIndex) => OnPlayerDied?.Invoke(playerIndex);

        public event Action<int> OnPlayerRespawned;
        public void RaisePlayerRespawned(int playerIndex) => OnPlayerRespawned?.Invoke(playerIndex);

        public event Action<int> OnBlockBroken;
        public void RaiseBlockBroken(int playerIndex) => OnBlockBroken?.Invoke(playerIndex);

        /// <summary>
        /// A block stance absorbed a hit without breaking. Args: player index,
        /// remaining shield charges. The tutorial's block calibration counts these;
        /// feedback layers (haptics per design Guard Impact) can bind here too.
        /// </summary>
        public event Action<int, int> OnBlockAbsorbed;
        public void RaiseBlockAbsorbed(int playerIndex, int remainingCharges) =>
            OnBlockAbsorbed?.Invoke(playerIndex, remainingCharges);

        public event Action<BlockChargesPayload> OnBlockChargesChanged;
        public void RaiseBlockChargesChanged(BlockChargesPayload payload) => OnBlockChargesChanged?.Invoke(payload);

        // === Ability & Cooldown Events ===
        public event Action<CooldownPayload> OnCooldownStarted;
        public void RaiseCooldownStarted(CooldownPayload payload) => OnCooldownStarted?.Invoke(payload);

        public event Action<AbilitySlot> OnCooldownComplete;
        public void RaiseCooldownComplete(AbilitySlot slot) => OnCooldownComplete?.Invoke(slot);

        public event Action<MovementAbilityPayload> OnMovementAbilityUsed;
        public void RaiseMovementAbilityUsed(MovementAbilityPayload payload) => OnMovementAbilityUsed?.Invoke(payload);

        public event Action<UltimateMeterPayload> OnUltimateMeterChanged;
        public void RaiseUltimateMeterChanged(UltimateMeterPayload payload) => OnUltimateMeterChanged?.Invoke(payload);

        public event Action<HyperArmorPayload> OnHyperArmorChanged;
        public void RaiseHyperArmorChanged(HyperArmorPayload payload) => OnHyperArmorChanged?.Invoke(payload);

        public event Action<PuzzleStatePayload> OnPuzzleStateChanged;
        public void RaisePuzzleStateChanged(PuzzleStatePayload payload) => OnPuzzleStateChanged?.Invoke(payload);

        // === Status Effect Events ===
        public event Action<StatusEffectPayload> OnStatusEffectApplied;
        public void RaiseStatusEffectApplied(StatusEffectPayload payload) => OnStatusEffectApplied?.Invoke(payload);

        /// <summary>
        /// Raised when an active status expires or is replaced. The payload carries
        /// <c>StatusType.None</c>; presentation layers clear their status treatment.
        /// </summary>
        public event Action<StatusEffectPayload> OnStatusEffectCleared;
        public void RaiseStatusEffectCleared(StatusEffectPayload payload) => OnStatusEffectCleared?.Invoke(payload);

        /// <summary>Raised when a combatant's own attack lands, for feedback layers.</summary>
        public event Action<HitConfirmPayload> OnHitConfirm;
        public void RaiseHitConfirm(HitConfirmPayload payload) => OnHitConfirm?.Invoke(payload);

        /// <summary>Raised the frame an ultimate starts, in both modes.</summary>
        public event Action<UltimateActivationPayload> OnUltimateActivation;
        public void RaiseUltimateActivation(UltimateActivationPayload payload) => OnUltimateActivation?.Invoke(payload);

        // === Chronal / Rewind Events ===
        public event Action<Vector2> OnRewindTriggered;
        public void RaiseRewindTriggered(Vector2 targetPosition) => OnRewindTriggered?.Invoke(targetPosition);

        public event Action<RewindPresentationPayload> OnRewindPresentation;
        public void RaiseRewindPresentation(RewindPresentationPayload payload) => OnRewindPresentation?.Invoke(payload);

        // Package 11 A2 — Time Freeze readiness (Ready / Active / Cooldown).
        public event Action<TimeFreezePayload> OnTimeFreezeStateChanged;
        public void RaiseTimeFreezeStateChanged(TimeFreezePayload payload) => OnTimeFreezeStateChanged?.Invoke(payload);

        // === Fighter Mode presentation (countdown / KO sequence) ===
        public event Action<FighterPresentationPayload> OnFighterPresentation;
        public void RaiseFighterPresentation(FighterPresentationPayload payload) => OnFighterPresentation?.Invoke(payload);

        // === Dialogue Events ===
        public event Action<string> OnDialogueTriggered;
        public void RaiseDialogueTriggered(string dialogueID) => OnDialogueTriggered?.Invoke(dialogueID);

        public event Action<string> OnDialogueComplete;
        public void RaiseDialogueComplete(string dialogueID) => OnDialogueComplete?.Invoke(dialogueID);

        // === Environment & Hazard Events ===
        public event Action<HazardStatePayload> OnHazardStateChanged;
        public void RaiseHazardStateChanged(HazardStatePayload payload) => OnHazardStateChanged?.Invoke(payload);

        public event Action<RoomTransitionPayload> OnRoomTransitioned;
        public void RaiseRoomTransitioned(RoomTransitionPayload payload) => OnRoomTransitioned?.Invoke(payload);

        public event Action<ExtractorStatePayload> OnExtractorStateChanged;
        public void RaiseExtractorStateChanged(ExtractorStatePayload payload) => OnExtractorStateChanged?.Invoke(payload);

        // === Progression Events ===
        public event Action<string> OnTalentNodeUnlocked;
        public void RaiseTalentNodeUnlocked(string nodeID) => OnTalentNodeUnlocked?.Invoke(nodeID);

        public event Action<int> OnChronalDustDeposited;
        public void RaiseChronalDustDeposited(int totalDust) => OnChronalDustDeposited?.Invoke(totalDust);

        public event Action<int> OnChronalDustCollected;
        public void RaiseChronalDustCollected(int amount) => OnChronalDustCollected?.Invoke(amount);

        // V7.3 Single Icon Rule: boss/extractor dust awards spawn a physical
        // pickup and the wallet is paid at COLLECTION (RaiseChronalDustCollected
        // fires once, there). This companion payload carries the award's source
        // so the level results can attribute the amount to the right line at
        // that same moment — attribution without a second wallet payment.
        public event Action<DustAwardCollectedPayload> OnDustAwardCollected;
        public void RaiseDustAwardCollected(DustAwardCollectedPayload payload) =>
            OnDustAwardCollected?.Invoke(payload);

        // === Level & Match Flow Events ===
        public event Action<MatchState> OnMatchStateChanged;
        public void RaiseMatchStateChanged(MatchState state) => OnMatchStateChanged?.Invoke(state);

        public event Action<string> OnLevelComplete;
        public void RaiseLevelComplete(string levelID) => OnLevelComplete?.Invoke(levelID);

        // V7.3 checkpoint save ordering: activation is a two-beat event pair.
        // OnCheckpointReached (plus the payload flavor carrying the
        // once-per-attempt FirstActivation flag) is the GAMEPLAY half — state
        // capture, rewind-pool refresh, HUD toast. OnCheckpointCommitted
        // follows and is the PERSISTENCE half, so the checkpoint save always
        // captures post-refresh state (save.CurrentLives was captured
        // pre-refresh under the single event).
        public event Action<string> OnCheckpointReached;
        public event Action<CheckpointReachedPayload> OnCheckpointActivated;
        public event Action<string> OnCheckpointCommitted;

        public void RaiseCheckpointReached(string checkpointID, bool firstActivation = true) {
            OnCheckpointReached?.Invoke(checkpointID);
            OnCheckpointActivated?.Invoke(new CheckpointReachedPayload {
                CheckpointID = checkpointID,
                FirstActivation = firstActivation
            });
        }

        public void RaiseCheckpointCommitted(string checkpointID) => OnCheckpointCommitted?.Invoke(checkpointID);

        // === Enemy Events ===
        public event Action<EnemyKilledPayload> OnEnemyKilled;
        public void RaiseEnemyKilled(EnemyKilledPayload payload) => OnEnemyKilled?.Invoke(payload);

        public event Action<EnemyPresentationPayload> OnEnemyPresentation;
        public void RaiseEnemyPresentation(EnemyPresentationPayload payload) => OnEnemyPresentation?.Invoke(payload);

        // === Boss Events ===
        public event Action<int> OnBossPhaseChanged;
        public void RaiseBossPhaseChanged(int phaseIndex) => OnBossPhaseChanged?.Invoke(phaseIndex);

        public event Action<BossSpawnedPayload> OnBossSpawned;
        public void RaiseBossSpawned(BossSpawnedPayload payload) => OnBossSpawned?.Invoke(payload);

        public event Action<BossHPPayload> OnBossHPChanged;
        public void RaiseBossHPChanged(BossHPPayload payload) => OnBossHPChanged?.Invoke(payload);

        public event Action<BossDefeatedPayload> OnBossDefeated;
        public void RaiseBossDefeated(BossDefeatedPayload payload) => OnBossDefeated?.Invoke(payload);

        // === Match Reset ===
        public event Action OnMatchReset;
        public void RaiseMatchReset() => OnMatchReset?.Invoke();

        // === Package 11 A1: ability-slot lock states ===
        /// <summary>
        /// Raised on every Dormant / Suppressed / Clear transition of one Story
        /// ability slot. A1 publishes the Suppression transitions from
        /// <c>PlayerController</c>; A5 publishes Dormant from the Legacy Unlock
        /// Schedule; A8's HUD subscribes and draws the cold cross-out.
        /// </summary>
        public event Action<AbilitySlotLockPayload> OnAbilitySlotLockChanged;
        public void RaiseAbilitySlotLockChanged(AbilitySlotLockPayload payload) =>
            OnAbilitySlotLockChanged?.Invoke(payload);

// === Package 11 A5 declares / A1b takes over in Wave 2 (plan §2.9) ===
        // The F13 Defy seal. A5 publishes the Level 0 calibration's forced
        // lit → broken states so the tutorial can teach the rule; A1b becomes
        // the general publisher when it lands.
        public event Action<DefySealPayload> OnDefySealChanged;
        public void RaiseDefySealChanged(DefySealPayload payload) =>
            OnDefySealChanged?.Invoke(payload);
        // === Package 11 A3 — Timeline Integrity & Collapse Tremor ===========
        public event Action<IntegrityPayload> OnTimelineIntegrityChanged;
        public void RaiseTimelineIntegrityChanged(IntegrityPayload payload) =>
            OnTimelineIntegrityChanged?.Invoke(payload);
        public event Action<TremorPayload> OnCollapseTremorChanged;
        public void RaiseCollapseTremorChanged(TremorPayload payload) =>
            OnCollapseTremorChanged?.Invoke(payload);
    }
}
