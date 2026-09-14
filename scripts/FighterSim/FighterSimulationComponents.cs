using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    public enum FighterCharacterID {
        Einstein = 0,
        Joan = 1,
        Leonardo = 2,
        Lincoln = 3,
        Cleopatra = 4,
        Tesla = 5,
        Shakespeare = 6,
        Mozart = 7,
        Pocahontas = 8
    }

    /// <summary>
    /// Immutable per-character ability execution configuration baked from normalized
    /// resources. Effect lifetimes reuse the shared persistent-lifetime fields for
    /// zones as well as constructs.
    /// </summary>
    public readonly struct FighterAbilityLoadout {
        public int SpecialOneExecutionType { get; init; }
        public int SpecialTwoExecutionType { get; init; }
        public int SpecialOneProjectileLifetimeFrames { get; init; }
        public int SpecialTwoProjectileLifetimeFrames { get; init; }
        public int SpecialOnePersistentTypeID { get; init; }
        public int SpecialOneMaxActiveObjects { get; init; }
        public int SpecialOnePersistentLifetimeFrames { get; init; }
        public int SpecialTwoPersistentTypeID { get; init; }
        public int SpecialTwoMaxActiveObjects { get; init; }
        public int SpecialTwoPersistentLifetimeFrames { get; init; }
        public int SpecialOneTickIntervalFrames { get; init; }
        public int SpecialTwoTickIntervalFrames { get; init; }
        public int MovementType { get; init; }
        public int MovementCooldownFrames { get; init; }
        public int MovementDurationFrames { get; init; }
        public int MovementResetsJump { get; init; }
        public int MovementGrantsHyperArmor { get; init; }
        public int MovementPersistentTypeID { get; init; }
        public int MovementMaxActiveObjects { get; init; }
        public int MovementPersistentLifetimeFrames { get; init; }
        public FP64 SpecialOneProjectileSpeed { get; init; }
        public FP64 SpecialTwoProjectileSpeed { get; init; }
        public FP64 MovementDistance { get; init; }
        public FP64 MovementSpeed { get; init; }

        public static FighterAbilityLoadout Default => new() {
            SpecialOneExecutionType = 2,
            SpecialTwoExecutionType = 2,
            SpecialOneProjectileLifetimeFrames = 300,
            SpecialTwoProjectileLifetimeFrames = 300,
            MovementType = 2,
            MovementCooldownFrames = 300,
            MovementDurationFrames = 12,
            SpecialOneProjectileSpeed = FP64.Zero,
            SpecialTwoProjectileSpeed = FP64.Zero,
            MovementDistance = FP64.FromInt(2),
            MovementSpeed = FP64.FromInt(10)
        };
    }

    public readonly struct FighterMatchRules {
        public readonly int MatchMode;
        public readonly bool ItemsEnabled;
        public readonly int ItemFrequency;
        public readonly bool HazardsEnabled;
        public readonly int HazardFrequency;
        public readonly int StageHazardTypeID;
        /// <summary>
        /// Pre-match 3-2-1 countdown length. Deterministic state: it is written
        /// into <see cref="FighterMatchComponent.CountdownFramesRemaining"/> and
        /// participates in every snapshot and hash. Defaults to zero so headless
        /// scenarios start live; the Godot driver passes
        /// <see cref="FighterMatchFlowRules.CountdownFrames"/>.
        /// </summary>
        public readonly int PreMatchCountdownFrames;

        public FighterMatchRules(bool itemsEnabled, int itemFrequency, bool hazardsEnabled, int hazardFrequency)
            : this(2, itemsEnabled, itemFrequency, hazardsEnabled, hazardFrequency, 1, 0) { }

        public FighterMatchRules(int matchMode, bool itemsEnabled, int itemFrequency, bool hazardsEnabled, int hazardFrequency)
            : this(matchMode, itemsEnabled, itemFrequency, hazardsEnabled, hazardFrequency, 1, 0) { }

        public FighterMatchRules(
            int matchMode,
            bool itemsEnabled,
            int itemFrequency,
            bool hazardsEnabled,
            int hazardFrequency,
            int stageHazardTypeID)
            : this(matchMode, itemsEnabled, itemFrequency, hazardsEnabled, hazardFrequency, stageHazardTypeID, 0) { }

        public FighterMatchRules(
            int matchMode,
            bool itemsEnabled,
            int itemFrequency,
            bool hazardsEnabled,
            int hazardFrequency,
            int stageHazardTypeID,
            int preMatchCountdownFrames) {
            MatchMode = matchMode;
            ItemsEnabled = itemsEnabled;
            ItemFrequency = itemFrequency;
            HazardsEnabled = hazardsEnabled;
            HazardFrequency = hazardFrequency;
            StageHazardTypeID = System.Math.Clamp(stageHazardTypeID, 1, 10);
            PreMatchCountdownFrames = preMatchCountdownFrames > 0 ? preMatchCountdownFrames : 0;
        }

        public FighterMatchRules WithCountdown(int preMatchCountdownFrames) => new(
            MatchMode,
            ItemsEnabled,
            ItemFrequency,
            HazardsEnabled,
            HazardFrequency,
            StageHazardTypeID,
            preMatchCountdownFrames);

        public static FighterMatchRules Disabled => new(2, false, 0, false, 0);
    }

    public readonly struct FighterLoadout {
        public readonly int CharacterID;
        public readonly int MaxHP;
        public readonly int MaxBlockCharges;
        public readonly int MaxJumpCount;
        public readonly int BasicDamage;
        public readonly int SpecialOneDamage;
        public readonly int SpecialTwoDamage;
        public readonly int UltimateDamage;
        public readonly int SpecialOneCooldownFrames;
        public readonly int SpecialTwoCooldownFrames;
        public readonly int SpecialOneStatusType;
        public readonly int SpecialOneStatusFrames;
        public readonly int SpecialTwoStatusType;
        public readonly int SpecialTwoStatusFrames;
        public readonly int UltimateStatusType;
        public readonly int UltimateStatusFrames;
        public readonly FP64 Weight;
        public readonly FP64 MoveSpeed;
        public readonly FP64 JumpSpeed;
        public readonly FP64 BasicKnockback;
        public readonly FP64 SpecialOneKnockback;
        public readonly FP64 SpecialTwoKnockback;
        public readonly FP64 UltimateKnockback;
        public readonly FP64 SpecialOneStatusIntensity;
        public readonly FP64 SpecialTwoStatusIntensity;
        public readonly FP64 UltimateStatusIntensity;
        public readonly FighterAbilityLoadout AbilityModes;
        /// <summary>Aerial input responsiveness (CharacterData.AirControlMultiplier); 0 = unset, treated as 1.0.</summary>
        public readonly FP64 AirControl;

        public FighterLoadout(
            int characterID,
            int maxHP,
            int maxBlockCharges,
            int maxJumpCount,
            int basicDamage,
            int specialOneDamage,
            int specialTwoDamage,
            int ultimateDamage,
            int specialOneCooldownFrames,
            int specialTwoCooldownFrames,
            int specialOneStatusType,
            int specialOneStatusFrames,
            int specialTwoStatusType,
            int specialTwoStatusFrames,
            int ultimateStatusType,
            int ultimateStatusFrames,
            FP64 weight,
            FP64 moveSpeed,
            FP64 jumpSpeed,
            FP64 basicKnockback,
            FP64 specialOneKnockback,
            FP64 specialTwoKnockback,
            FP64 ultimateKnockback,
            FP64 specialOneStatusIntensity,
            FP64 specialTwoStatusIntensity,
            FP64 ultimateStatusIntensity,
            FighterAbilityLoadout abilityModes,
            FP64 airControl = default) {
            CharacterID = characterID;
            MaxHP = maxHP;
            MaxBlockCharges = maxBlockCharges;
            MaxJumpCount = maxJumpCount;
            BasicDamage = basicDamage;
            SpecialOneDamage = specialOneDamage;
            SpecialTwoDamage = specialTwoDamage;
            UltimateDamage = ultimateDamage;
            SpecialOneCooldownFrames = specialOneCooldownFrames;
            SpecialTwoCooldownFrames = specialTwoCooldownFrames;
            SpecialOneStatusType = specialOneStatusType;
            SpecialOneStatusFrames = specialOneStatusFrames;
            SpecialTwoStatusType = specialTwoStatusType;
            SpecialTwoStatusFrames = specialTwoStatusFrames;
            UltimateStatusType = ultimateStatusType;
            UltimateStatusFrames = ultimateStatusFrames;
            Weight = weight;
            MoveSpeed = moveSpeed;
            JumpSpeed = jumpSpeed;
            BasicKnockback = basicKnockback;
            SpecialOneKnockback = specialOneKnockback;
            SpecialTwoKnockback = specialTwoKnockback;
            UltimateKnockback = ultimateKnockback;
            SpecialOneStatusIntensity = specialOneStatusIntensity;
            SpecialTwoStatusIntensity = specialTwoStatusIntensity;
            UltimateStatusIntensity = ultimateStatusIntensity;
            AbilityModes = abilityModes;
            // V7.2 wiring of the previously dead CharacterData stat: aerial
            // input responsiveness. Zero (older callers / Default) reads as 1.0
            // at the consumption site.
            AirControl = airControl;
        }

        public static FighterLoadout Default(FighterCharacterID characterID) => new(
            (int)characterID,
            100,
            3,
            2,
            10,
            14,
            12,
            20,
            600,
            600,
            (int)FTT.Core.StatusType.None,
            0,
            (int)FTT.Core.StatusType.None,
            0,
            (int)FTT.Core.StatusType.None,
            0,
            FP64.One,
            // Movement feel batch 2026-08-10 §2.1/§2.2: synthetic default kit
            // moves at the roster's post-retune mid speed (8 → 7) and jumps to
            // the post-retune envelope (13 → 11.5; apex ≈2.11, double ≈4.21).
            FP64.FromInt(7),
            FP64.FromDouble(11.5),
            FP64.FromInt(3),
            FP64.FromInt(4),
            FP64.FromInt(4),
            FP64.FromInt(5),
            FP64.One,
            FP64.One,
            FP64.One,
            FighterAbilityLoadout.Default);
    }

    [KlothoComponent(300, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterStateComponent : IComponent {
        public int EntityID;
        public int PlayerID;
        public int CharacterID;
        public int CurrentHP;
        public int MaxHP;
        public int Stocks;
        public int BlockCharges;
        public int FacingRight;
        public int IsGrounded;
        public int InvulnerabilityFrames;
        public int HitstunFrames;
        public int DazeFrames;
        public int HyperArmorFrames;
        public int DropThroughFrames;
        public int RemainingJumps;
        /// <summary>
        /// Frames left before the Chronal Respawn Platform dissolves. Non-zero is
        /// itself the "on platform" phase: the drop always zeroes it in the same
        /// frame it reaches zero, so <c>&gt; 0</c> and "held by the platform" are
        /// the same condition. Kept as one field because
        /// <see cref="FighterStateComponent"/> sits exactly on Klotho's 128-byte
        /// component budget.
        /// </summary>
        public int RespawnFramesRemaining;
        public FP64 Weight;
        public FP64 Influence;
        public FPVector2 Position;
        public FPVector2 Velocity;
        public FPVector2 SpawnPosition;
    }

    [KlothoComponent(305, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterRuntimeComponent : IComponent {
        public int BasicCooldownFrames;
        public int SpecialOneCooldownFrames;
        public int SpecialTwoCooldownFrames;
        public int MovementCooldownFrames;
        public int ComboIndex;
        // V7 two-slot status (mirrors StatusController): a control status
        // (TimeDilation / StaticCharge / Root) and a damage status (Venom /
        // RadiantBurn) coexist. Both slots are bit-packed into the two ints
        // below — low 16 bits control, high 16 bits damage — because this
        // component sits at Klotho's full 128-byte budget and cannot grow.
        // Read/write through the StatusType / DamageStatusType /
        // StatusFrames / DamageStatusFrames properties at the end of the
        // struct, never through these fields.
        private int _statusTypesPacked;
        private int _statusFramesPacked;
        /// <summary>Venom's 1 s tick countdown; damage slot only.</summary>
        public int StatusTickFrames;
        public int MoveX;
        public int MoveY;
        public int HeldButtons;
        public int PressedButtons;
        public int ReleasedButtons;
        public int SpeedBuffFrames;
        public int JumpBuffFrames;
        public int AegisHits;
        public int UsesStocks;
        public int KnockoutsSuffered;
        public int UniversalMovementState;
        public int UniversalMovementFramesRemaining;
        public int UniversalMovementDirection;
        public int ZoneSpeedBonusFrames;
        public int FloatFrames;
        /// <summary>
        /// Basic-combo phase machine (FighterBasicAttackRules): 0 none, 1 startup,
        /// 2 active, 3 recovery, 4 chain hold. Timings come from
        /// FTT.Combat.BasicComboRules so both modes run the same string. With the
        /// four attack/block fields and §2.11's three ledge fields this component
        /// sits at exactly 128 bytes — Klotho's whole per-component budget. Any
        /// further sim state needs its own component, not another int here.
        /// </summary>
        public int AttackPhase;
        public int AttackPhaseFrames;
        /// <summary>Bit flags: 1 aerial string, 2 hit resolved, 4 next hit buffered.</summary>
        public int AttackFlags;
        /// <summary>Countdown to the next block-charge regeneration.</summary>
        public int BlockRegenFrames;
        /// <summary>
        /// Ledge hang anchor (gameplay-feel plan §2.11), encoded as
        /// <c>platformIndex * 2 + side</c> where side 0 is the platform's left edge
        /// and 1 its right edge. <c>-1</c> means "not hanging" and is the only
        /// "am I hanging" test in the simulation. The anchor position itself is
        /// re-derived from the stage geometry every tick, because geometry is
        /// constant for the match and never enters a snapshot.
        /// </summary>
        public int LedgeAnchor;
        /// <summary>Frames spent on the current hang; auto-release fires at 300.</summary>
        public int LedgeStateFrames;
        /// <summary>Regrab lockout after a Down release or an auto-release.</summary>
        public int LedgeRegrabLockoutFrames;
        // Per-slot status intensities in thousandths (the FP64 the single-slot
        // system stored here split into two ints of the same total size). The
        // milli quantization is deterministic: authored intensities round-trip
        // through FP64.FromInt(n) / FP64.FromInt(1000) identically every frame.
        private int _controlStatusIntensityMilli;
        private int _damageStatusIntensityMilli;

        /// <summary>Control-slot status type (TimeDilation / StaticCharge / Root).</summary>
        public int StatusType {
            readonly get => _statusTypesPacked & 0xFFFF;
            set => _statusTypesPacked = (_statusTypesPacked & unchecked((int)0xFFFF0000)) | (value & 0xFFFF);
        }
        /// <summary>Damage-slot status type (Venom / RadiantBurn).</summary>
        public int DamageStatusType {
            readonly get => (_statusTypesPacked >> 16) & 0xFFFF;
            set => _statusTypesPacked = (_statusTypesPacked & 0xFFFF) | ((value & 0xFFFF) << 16);
        }
        public int StatusFrames {
            readonly get => _statusFramesPacked & 0xFFFF;
            set => _statusFramesPacked = (_statusFramesPacked & unchecked((int)0xFFFF0000)) | (value & 0xFFFF);
        }
        public int DamageStatusFrames {
            readonly get => (_statusFramesPacked >> 16) & 0xFFFF;
            set => _statusFramesPacked = (_statusFramesPacked & 0xFFFF) | ((value & 0xFFFF) << 16);
        }
        public FP64 StatusIntensity {
            readonly get => FP64.FromInt(_controlStatusIntensityMilli) / FP64.FromInt(1000);
            set => _controlStatusIntensityMilli = QuantizeMilli(value);
        }
        public FP64 DamageStatusIntensity {
            readonly get => FP64.FromInt(_damageStatusIntensityMilli) / FP64.FromInt(1000);
            set => _damageStatusIntensityMilli = QuantizeMilli(value);
        }
        // Round-to-nearest so float-authored intensities that sit a hair below
        // their decimal (0.7f = 0.69999998) still land on the intended value.
        private static int QuantizeMilli(FP64 value) =>
            (int)(((value * FP64.FromInt(1000)).RawValue + FP64.One.RawValue / 2) / FP64.One.RawValue);
        /// <summary>Single-pip presentation: the control status leads; a lone DoT/vulnerability shows otherwise.</summary>
        public readonly int PresentedStatusType =>
            StatusType != (int)FTT.Core.StatusType.None ? StatusType : DamageStatusType;
    }

    [KlothoComponent(306, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterTuningComponent : IComponent {
        public int BasicDamage;
        public int SpecialOneDamage;
        public int SpecialTwoDamage;
        public int UltimateDamage;
        public int MaxBlockCharges;
        public int MaxJumpCount;
        public int SpecialOneCooldownFrames;
        public int SpecialTwoCooldownFrames;
        public int SpecialOneStatusType;
        public int SpecialOneStatusFrames;
        public int SpecialTwoStatusType;
        public int SpecialTwoStatusFrames;
        public int UltimateStatusType;
        public int UltimateStatusFrames;
        public FP64 MoveSpeed;
        public FP64 JumpSpeed;
        public FP64 BasicKnockback;
        public FP64 SpecialOneKnockback;
        public FP64 SpecialTwoKnockback;
        public FP64 UltimateKnockback;
        public FP64 SpecialOneStatusIntensity;
        public FP64 SpecialTwoStatusIntensity;
        public FP64 UltimateStatusIntensity;
        /// <summary>Aerial input responsiveness (V7.2 wiring); 0 from hand-built tuning reads as 1.0.</summary>
        public FP64 AirControl;
    }

    [KlothoComponent(307, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterAbilityModeComponent : IComponent {
        public int SpecialOneExecutionType;
        public int SpecialTwoExecutionType;
        public int SpecialOneProjectileLifetimeFrames;
        public int SpecialTwoProjectileLifetimeFrames;
        public int SpecialOnePersistentTypeID;
        public int SpecialOneMaxActiveObjects;
        public int SpecialOnePersistentLifetimeFrames;
        public int SpecialTwoPersistentTypeID;
        public int SpecialTwoMaxActiveObjects;
        public int SpecialTwoPersistentLifetimeFrames;
        public int SpecialOneTickIntervalFrames;
        public int SpecialTwoTickIntervalFrames;
        public int MovementType;
        public int MovementCooldownFrames;
        public int MovementDurationFrames;
        public int MovementResetsJump;
        public int MovementGrantsHyperArmor;
        public int MovementPersistentTypeID;
        public int MovementMaxActiveObjects;
        public int MovementPersistentLifetimeFrames;
        public FP64 SpecialOneProjectileSpeed;
        public FP64 SpecialTwoProjectileSpeed;
        public FP64 MovementDistance;
        public FP64 MovementSpeed;
    }

    /// <summary>
    /// V7/V7.1/V7.2 verb-layer snapshot state (design §4 "Recoverable Health" /
    /// "Time Systems" / hitstop / grabs). FighterRuntimeComponent is exactly
    /// full at its 128-byte budget, so this state lives on its own component,
    /// exactly as the design mandates ("requires a new Klotho component,
    /// ID 310+").
    /// </summary>
    [KlothoComponent(310, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterVerbComponent : IComponent {
        // --- Hitstop (V7 universal rule) ---
        /// <summary>Frames this fighter is fully suspended (timers, velocity, phases).</summary>
        public int HitstopFrames;
        /// <summary>1 while a launching hit's velocity awaits DI resolution at hitstop end.</summary>
        public int PendingLaunchActive;
        public FP64 PendingLaunchX;
        public FP64 PendingLaunchY;
        // --- Landing tech ---
        /// <summary>1 while in launched hitstun (tumble); cleared on landing/tech/hitstun end.</summary>
        public int Tumble;
        /// <summary>Invulnerable in-place recovery after a successful tech (no actions, no movement).</summary>
        public int TechLockoutFrames;
        // --- Rally / Desperation / Defy History (V7.1) ---
        public FP64 EchoPool;
        public FP64 EchoDrainPerFrame;
        public int DefyHistoryUsed;
        /// <summary>1 while the match is in Overtime (final minute of a timed match).</summary>
        public int OvertimeActive;
        // --- Resonance Momentum (V7.1) ---
        public int MomentumRefundsSlotOne;
        public int MomentumRefundsSlotTwo;
        // --- Echo Step (V7.1) ---
        public int EchoStepCooldownFrames;
        public int EchoStepWindupFrames;
        public FP64 EchoStepDestX;
        public FP64 EchoStepDestY;
        // --- Grabs & Throws (V7.2) ---
        /// <summary>0 none, 1 startup, 2 active, 3 whiff recovery, 4 holding, 5 throw animation.</summary>
        public int GrabPhase;
        public int GrabPhaseFrames;
        /// <summary>Direction held when the throw resolves: 0 forward, 1 up, 2 back.</summary>
        public int ThrowDirection;
        /// <summary>1 while this fighter is held by the opponent's grab.</summary>
        public int BeingHeld;
        /// <summary>No-regrab window after being thrown.</summary>
        public int ThrowImmunityFrames;
        // --- Block model closure (V7.3) ---
        /// <summary>Frames the blocker is locked into the stance after a non-shatter blocked hit.</summary>
        public int ShieldStunFrames;
        /// <summary>Shatter lockout: no stance and no regen while &gt; 0 (5 s).</summary>
        public int BlockLockoutFrames;
        /// <summary>1 while the current hitstun cannot be block-cancelled (string hit 1).</summary>
        public int HitstunBlockCancelBlocked;
        // --- Ledge V7 (consumed by the ledge-trump PR; snapshotted with the rest) ---
        /// <summary>Ledge grabs taken since the fighter was last grounded.</summary>
        public int LedgeGrabsThisAirtime;
    }

    /// <summary>
    /// V7.6 D01–D04 defensive layer (Package 11 A1b), deterministic half.
    /// <see cref="FighterRuntimeComponent"/> is exactly full at 128 bytes and
    /// <see cref="FighterVerbComponent"/> has four bytes of headroom, so the
    /// defensive contract's new snapshot state lives here. Klotho component ID
    /// assigned by Package 11 §2.7 — A1b only.
    ///
    /// <para><b>D04 protected recovery.</b> A successful Defy sets
    /// <see cref="DefyProtectionAwaitControl"/> and <see cref="DefyProtectionFrames"/>
    /// (60) together with <see cref="DefyProcIdentity"/>. The countdown does not
    /// start until the survivor's first resumed normal-control tick, and pauses
    /// during hitstop and suspended-combat presentations. The proc identity is
    /// the resimulation dedupe: a rollback that replays the same proc must not
    /// stage a second presentation.</para>
    ///
    /// <para><b>D02b barrier layer.</b> The simulation has no Story grid perks,
    /// so <see cref="BarrierPoints"/> is zero in every shipped Fighter match —
    /// but D02b's ordering is authored here rather than left implicit, so a
    /// future authored Fighter barrier resolves in the contract's order
    /// (invulnerability → Aegis → barrier → block) without another rewrite.</para>
    /// </summary>
    [KlothoComponent(312, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterDefenseComponent : IComponent {
        /// <summary>1 while a Defy survivor is still in its presentation and has not resumed control.</summary>
        public int DefyProtectionAwaitControl;
        /// <summary>Remaining protected ticks once control resumed; 60 on a fresh proc.</summary>
        public int DefyProtectionFrames;
        /// <summary>Deterministic identity of the proc that installed this window; 0 when none.</summary>
        public int DefyProcIdentity;
        /// <summary>
        /// V7.6 F22/F13: 1 while this context BARS Defy — Sudden Death today.
        /// Deliberately separate from <c>FighterVerbComponent.DefyHistoryUsed</c>
        /// so the seal can show <b>Unavailable</b> without the underlying
        /// <b>Spent</b> flag being overwritten, which is exactly what the old
        /// pre-mark on Sudden Death entry conflated.
        /// </summary>
        public int DefyBarred;
        /// <summary>D02e: one active Temporal Aegis per recipient, a flag and never a count.</summary>
        public int AegisActive;
        /// <summary>D02b finite HP barrier: remaining absorption.</summary>
        public int BarrierPoints;
        /// <summary>The barrier's normal cap, for a D02a refresh.</summary>
        public int BarrierCapacity;
        /// <summary>D02c lifetime in active ticks; -1 means "no expiry" (Wardenclyffe-shaped).</summary>
        public int BarrierRemainingFrames;
        /// <summary>Which shield effect owns the live barrier instance; 0 when none.</summary>
        public int BarrierEffectID;
    }

    /// <summary>
    /// Echo Step's per-fighter position ring (V7.1): 5 entries sampled every
    /// 6 frames; the oldest sample approximates "30 frames ago". Snapshot state
    /// — the ring is part of the rollback hash like everything else.
    /// </summary>
    [KlothoComponent(311, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterEchoRingComponent : IComponent {
        public FP64 Sample0X; public FP64 Sample0Y;
        public FP64 Sample1X; public FP64 Sample1Y;
        public FP64 Sample2X; public FP64 Sample2Y;
        public FP64 Sample3X; public FP64 Sample3Y;
        public FP64 Sample4X; public FP64 Sample4Y;
        public int RingIndex;
        public int SampleCountdown;
    }

    /// <summary>
    /// V7.6 F07 (Package 11 A1): the caster-owned Conductive MARK, deterministic
    /// half. A mark is NOT a status — it occupies neither status slot, causes no
    /// action lock, and contributes zero stagger budget — so it lives in its own
    /// component rather than in the full FighterRuntimeComponent.
    ///
    /// All three fields are snapshot and hash state with stable owner ordering
    /// and no wall-clock timer. <see cref="ChainConsumedExecutionID"/> is the
    /// per-execution guard the design requires: a restored multi-hit Lorentz
    /// Pulse must not duplicate chains, so an execution that already consumed
    /// this mark is remembered and a rollback replays the same decision.
    /// Klotho component ID assigned by Package 11 §2.7 — A1 only.
    /// </summary>
    [KlothoComponent(318, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterConductiveComponent : IComponent {
        /// <summary>Frames left on the mark; 0 when unmarked.</summary>
        public int FramesRemaining;
        /// <summary>Player ID that applied the live mark; -1 when unmarked.</summary>
        public int SourcePlayerID;
        /// <summary>
        /// The attacker execution ID that has already cashed this mark for a
        /// chain, or 0 when none has. Deterministic, never a wall-clock value.
        /// </summary>
        public int ChainConsumedExecutionID;
    }

    [KlothoComponent(301)]
    [KlothoSingletonComponent]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterMatchComponent : IComponent {
        public int RemainingFrames;
        /// <summary>1 when the match clock is live (V7: Stock mode defaults to the
        /// 8:00 timer too, configurable including Off = 0 matchSeconds).</summary>
        public int TimerEnabled;
        public int MatchState;
        public int MatchMode;
        public int WinnerPlayerID;
        public int IsTrueTie;
        public int WorldSeed;
        public int NextEntityID;
        public int ItemsEnabled;
        public int ItemFrequency;
        public int HazardsEnabled;
        public int HazardFrequency;
        public int StageHazardTypeID;
        public int NextOrbSpawnFrames;
        public int NextHazardSpawnFrames;
        public int PlayerOneKOs;
        public int PlayerTwoKOs;
        public int LastPlayerOneKnockoutsSuffered;
        public int LastPlayerTwoKnockoutsSuffered;
        /// <summary>Frames left in the pre-match 3-2-1 countdown while MatchState is 0.</summary>
        public int CountdownFramesRemaining;
        /// <summary>Frames left in the "GO!" banner window on the first live frames.</summary>
        public int GoBannerFramesRemaining;
        /// <summary>V7.1: 1 while Sudden Death is live — a true tie at expiry
        /// respawned both fighters at 1 HP, no timer, hazards accelerated,
        /// first KO wins and a double-KO is the recorded Draw.</summary>
        public int SuddenDeathActive;
        public ulong RandomState0;
        public ulong RandomState1;
    }

    [KlothoComponent(302, MaxCount = 64)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterProjectileComponent : IComponent {
        public int EntityID;
        public int OwnerPlayerID;
        public int ProjectileTypeID;
        public int LifetimeFrames;
        public int Damage;
        public int AttackClass;
        public int StatusType;
        public int StatusFrames;
        public int HitstunFrames;
        /// <summary>
        /// V7.6 D03h (Package 11 A1b): 1 when this projectile was spawned by an
        /// Ultimate execution, so its damage awards the caster zero damage-dealt
        /// meter no matter when it lands. Snapshot state — attribution has to
        /// survive the cinematic ending, owner interruption and rollback.
        /// </summary>
        public int UltimateOrigin;
        public FP64 StatusIntensity;
        /// <summary>Downward pull in units/s² for lobbed arcs (Mozart's Fortissimo Wave); zero = flat flight.</summary>
        public FP64 GravityPerSecond;
        public FPVector2 Position;
        public FPVector2 Velocity;
        public FPVector2 HalfExtents;
        public FPVector2 Knockback;
    }

    [KlothoComponent(303, MaxCount = 16)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterPersistentObjectComponent : IComponent {
        public int EntityID;
        public int OwnerPlayerID;
        public int ObjectTypeID;
        public int CurrentHP;
        public int MaxHP;
        public int LifetimeFrames;
        public int ActionCooldownFrames;
        public int MaxDeployLimit;
        public int Damage;
        public int BaseActionCooldownFrames;
        public int RemainingAttacks;
        public int StatusType;
        public int StatusFrames;
        /// <summary>Coil-link fence tick countdown; driven by the lower-EntityID coil of a linked pair.</summary>
        public int LinkTickFramesRemaining;
        public FP64 AttackRange;
        public FP64 Knockback;
        public FPVector2 Position;
        public FPVector2 HalfExtents;
    }

    /// <summary>
    /// One live era hazard. <c>Phase</c> is 0 = warning (telegraph, no damage),
    /// 1 = active, 2 = recovery (the post-active settle window whose length is
    /// <c>CooldownFrames</c>); the entity is destroyed when recovery ends.
    /// <para><c>SubTypeID</c> encodes a per-type sub-state (Vesuvius: 0 = falling
    /// rock, 1 = ground time-dilation pool). <c>Velocity</c> is in world units per
    /// <em>frame</em> (not per second) and is integrated directly, so a rolling or
    /// sweeping hazard stays exactly reproducible. <c>HitMask</c> holds one bit per
    /// player for one-shot hazards. <c>DwellFramesPlayerOne/Two</c> count the
    /// consecutive frames a fighter has satisfied the type's trigger condition
    /// (Berlin: inside the beam; Globe: standing still).</para>
    /// </summary>
    [KlothoComponent(304, MaxCount = 16)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterHazardComponent : IComponent {
        public int EntityID;
        public int HazardTypeID;
        public int SubTypeID;
        public int Phase;
        public int PhaseFramesRemaining;
        public int Damage;
        public int TickFramesRemaining;
        public int WarningFrames;
        public int ActiveFrames;
        public int CooldownFrames;
        public int HitMask;
        public int DwellFramesPlayerOne;
        public int DwellFramesPlayerTwo;
        public FPVector2 Position;
        public FPVector2 HalfExtents;
        public FPVector2 Knockback;
        public FPVector2 Velocity;
    }

    [KlothoComponent(308, MaxCount = 16)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterOrbComponent : IComponent {
        public int EntityID;
        public int EffectType;
        public int LifetimeFrames;
        public FPVector2 Position;
        public FPVector2 HalfExtents;
    }

    /// <summary>
    /// Deterministic Area-execution effect (Relativity Rift, Sandstorm Vortex, ...).
    /// ZoneTypeID encodes CharacterID * 10 + special slot for per-character rules.
    /// </summary>
    [KlothoComponent(309, MaxCount = 8)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterZoneComponent : IComponent {
        public int EntityID;
        public int OwnerPlayerID;
        public int ZoneTypeID;
        public int LifetimeFrames;
        public int TickIntervalFrames;
        public int TickFramesRemaining;
        public int Damage;
        public int StatusType;
        public int StatusFrames;
        public int GrantsOwnerSpeedBonus;
        public FP64 StatusIntensity;
        public FPVector2 Position;
        public FPVector2 HalfExtents;
    }
}
