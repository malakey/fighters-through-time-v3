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

        public FighterMatchRules(bool itemsEnabled, int itemFrequency, bool hazardsEnabled, int hazardFrequency)
            : this(2, itemsEnabled, itemFrequency, hazardsEnabled, hazardFrequency, 1) { }

        public FighterMatchRules(int matchMode, bool itemsEnabled, int itemFrequency, bool hazardsEnabled, int hazardFrequency)
            : this(matchMode, itemsEnabled, itemFrequency, hazardsEnabled, hazardFrequency, 1) { }

        public FighterMatchRules(
            int matchMode,
            bool itemsEnabled,
            int itemFrequency,
            bool hazardsEnabled,
            int hazardFrequency,
            int stageHazardTypeID) {
            MatchMode = matchMode;
            ItemsEnabled = itemsEnabled;
            ItemFrequency = itemFrequency;
            HazardsEnabled = hazardsEnabled;
            HazardFrequency = hazardFrequency;
            StageHazardTypeID = System.Math.Clamp(stageHazardTypeID, 1, 10);
        }

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
            FighterAbilityLoadout abilityModes) {
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
            FP64.FromInt(8),
            FP64.FromInt(13),
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
        public int StatusType;
        public int StatusFrames;
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
        public FP64 StatusIntensity;
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

    [KlothoComponent(301)]
    [KlothoSingletonComponent]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterMatchComponent : IComponent {
        public int RemainingFrames;
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
        public FP64 StatusIntensity;
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

    [KlothoComponent(304, MaxCount = 16)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterHazardComponent : IComponent {
        public int EntityID;
        public int HazardTypeID;
        public int Phase;
        public int PhaseFramesRemaining;
        public int Damage;
        public int TickFramesRemaining;
        public int WarningFrames;
        public int ActiveFrames;
        public int CooldownFrames;
        public FPVector2 Position;
        public FPVector2 HalfExtents;
        public FPVector2 Knockback;
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
