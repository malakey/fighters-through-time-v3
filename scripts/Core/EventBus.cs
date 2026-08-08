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
        Results
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

    public enum StatusType {
        None,
        TimeDilation,
        Venom,
        StaticCharge,
        RadiantBurn,
        Root
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

        // === Chronal / Rewind Events ===
        public event Action<Vector2> OnRewindTriggered;
        public void RaiseRewindTriggered(Vector2 targetPosition) => OnRewindTriggered?.Invoke(targetPosition);

        public event Action<RewindPresentationPayload> OnRewindPresentation;
        public void RaiseRewindPresentation(RewindPresentationPayload payload) => OnRewindPresentation?.Invoke(payload);

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

        // === Level & Match Flow Events ===
        public event Action<MatchState> OnMatchStateChanged;
        public void RaiseMatchStateChanged(MatchState state) => OnMatchStateChanged?.Invoke(state);

        public event Action<string> OnLevelComplete;
        public void RaiseLevelComplete(string levelID) => OnLevelComplete?.Invoke(levelID);

        public event Action<string> OnCheckpointReached;
        public void RaiseCheckpointReached(string checkpointID) => OnCheckpointReached?.Invoke(checkpointID);

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
    }
}
