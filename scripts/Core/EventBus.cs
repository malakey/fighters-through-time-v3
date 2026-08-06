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

        // === Boss Events ===
        public event Action<int> OnBossPhaseChanged;
        public void RaiseBossPhaseChanged(int phaseIndex) => OnBossPhaseChanged?.Invoke(phaseIndex);

        // === Match Reset ===
        public event Action OnMatchReset;
        public void RaiseMatchReset() => OnMatchReset?.Invoke();
    }
}
