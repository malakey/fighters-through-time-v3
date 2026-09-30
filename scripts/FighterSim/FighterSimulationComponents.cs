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
        /// <summary>
        /// Package 13 W5 (D3): Pocahontas left the roster (character review
        /// P01). The ordinal is RESERVED — this enum is append-only because it
        /// serializes into snapshots and the network protocol — so it is never
        /// removed, renumbered or reused. <c>FighterLoadoutFactory</c> no longer
        /// resolves any character ID to it.
        /// </summary>
        [System.Obsolete("Reserved ordinal: Pocahontas was removed from the roster (Package 13 W5, D3). Never reuse 8.")]
        Pocahontas = 8,
        /// <summary>Package 13 W5 (D3): Harriet Tubman, appended; protocol v4.</summary>
        Tubman = 9
    }

    /// <summary>
    /// M08 (Package 12 W3): one ability's authored hit contract, projected from
    /// <c>AbilityData</c> into the match loadout. LOADOUT data, not snapshot
    /// state: it is never written into a Klotho component, so it moves no hash
    /// and costs no component ID. The enum fields hold the
    /// <c>FTT.Combat.BlockClass</c> / <c>HitDelivery</c> / <c>HitOrigin</c>
    /// ordinals as ints, the same convention the other loadout enums use.
    ///
    /// <para>Nothing in the tick reads these yet. The sim still applies its own
    /// 18/30-frame special/ultimate hitstun (the deferred M-13 remainder), and
    /// W3b consumes <see cref="Launches"/> with component 320.</para>
    /// </summary>
    public readonly struct FighterAbilityHitData {
        public int HitstunFrames { get; init; }
        public int BlockClass { get; init; }
        public bool Launches { get; init; }
        public int Delivery { get; init; }
        public int Origin { get; init; }
        /// <summary>
        /// D10 (Package 13 W1): the authored knockback as a SIGNED vector in sim
        /// world units — <see cref="KnockbackX"/> the horizontal magnitude
        /// (pushed away from the hit origin) and <see cref="KnockbackY"/> the
        /// Y-up vertical, negative for a spike. Replaces the retired
        /// <c>max(|x|, |y|)</c> flattening for every path that reads the
        /// contract. False for a slot with no projected ability, which keeps
        /// the tuning scalar.
        /// </summary>
        public bool HasKnockbackVector { get; init; }
        public FP64 KnockbackX { get; init; }
        public FP64 KnockbackY { get; init; }

        /// <summary>
        /// Package 13 W7a — the burst-on-terrain projectile primitive, projected
        /// from <c>AbilityData.ProjectileBurstRadius</c> /
        /// <c>ProjectileContactDamage</c> / <c>ProjectileBurstsOnTerrain</c>.
        /// Loadout data like the rest of this struct: no snapshot field, no
        /// component ID. A positive <see cref="BurstRadius"/> makes the slot's
        /// projectile two-stage — its spawn damage is the burst, and the direct
        /// contact deals <see cref="ContactDamage"/>.
        /// </summary>
        public FP64 BurstRadius { get; init; }
        public int ContactDamage { get; init; }
        public bool BurstsOnTerrain { get; init; }
        public bool Bursts => BurstRadius > FP64.Zero;

        // --- Package 13 W7b: per-hit shape and damage, loadout only ---------

        /// <summary>
        /// W7b: the authored per-hit <c>BaseDamage</c>, rounded — NOT the folded
        /// multi-hit total the tuning component carries. Divine Piercing's thrust
        /// (8), the Requiem Chord's contact and each burst pulse (4), and Rail
        /// Charge's contact (6) read it. Zero for a slot with no projected ability.
        /// </summary>
        public int Damage { get; init; }
        /// <summary>W7b: the authored <c>HitCount</c> (thrusts, burst pulses); at least 1.</summary>
        public int HitCount { get; init; }
        /// <summary>W7b: the authored active frames (Divine Piercing's lunge length).</summary>
        public int ActiveFrames { get; init; }
        /// <summary>W7b: startup + active + recovery (Ascendant Wings' rise lasts the whole cast).</summary>
        public int TotalFrames { get; init; }
        /// <summary>W7b: the authored <c>HitboxSize</c> as world-unit half extents (0 when unprojected).</summary>
        public FP64 HitboxHalfX { get; init; }
        public FP64 HitboxHalfY { get; init; }
        /// <summary>W7b: the authored <c>HitboxOffset</c> in world units, Godot Y-down → sim Y-up.</summary>
        public FP64 HitboxOffsetX { get; init; }
        public FP64 HitboxOffsetY { get; init; }
        /// <summary>W7b: true when an authored hitbox was projected.</summary>
        public bool HasHitbox => HitboxHalfX > FP64.Zero && HitboxHalfY > FP64.Zero;

        /// <summary>A01: true when the ability is an authored Shield-Breaker.</summary>
        public bool ShieldBreaker => BlockClass == (int)FTT.Combat.BlockClass.ShieldBreaker;

        /// <summary>Today's generic Special contract: 12 f, Special, launches, direct, Special origin.</summary>
        public static FighterAbilityHitData DefaultSpecial => new() {
            HitstunFrames = 12,
            BlockClass = (int)FTT.Combat.BlockClass.Special,
            Launches = true,
            Delivery = (int)FTT.Combat.HitDelivery.DirectHit,
            Origin = (int)FTT.Combat.HitOrigin.Special
        };

        /// <summary>Today's generic Ultimate contract: 12 f, Unblockable, launches, direct, Ultimate origin.</summary>
        public static FighterAbilityHitData DefaultUltimate => new() {
            HitstunFrames = 12,
            BlockClass = (int)FTT.Combat.BlockClass.Unblockable,
            Launches = true,
            Delivery = (int)FTT.Combat.HitDelivery.DirectHit,
            Origin = (int)FTT.Combat.HitOrigin.Ultimate
        };
    }

    /// <summary>
    /// M08/M05 (Package 12 W3b): the two fighters' projected hit contracts,
    /// handed to the systems at construction exactly like the stage geometry.
    /// Static match configuration — built once from the two loadouts, never
    /// mutated, never written into a component — so it is not snapshot or hash
    /// state and needs no component ID. It is how the tick reads the authored
    /// <see cref="FighterAbilityHitData.HitstunFrames"/> and
    /// <see cref="FighterAbilityHitData.Launches"/> (closing the generic half of
    /// <c>DEFER-SIM-ABILITY-HITSTUN</c>).
    /// </summary>
    public sealed class FighterHitContractTable {
        /// <summary>Ability slot keys, matching the zone/projectile type-ID slot digit.</summary>
        public const int SlotSpecialOne = 1;
        public const int SlotSpecialTwo = 2;
        public const int SlotUltimate = 3;

        private readonly FighterAbilityLoadout _playerOne;
        private readonly FighterAbilityLoadout _playerTwo;

        public static readonly FighterHitContractTable Default =
            new(FighterAbilityLoadout.Default, FighterAbilityLoadout.Default);

        public FighterHitContractTable(FighterAbilityLoadout playerOne, FighterAbilityLoadout playerTwo) {
            _playerOne = playerOne;
            _playerTwo = playerTwo;
        }

        public FighterHitContractTable(FighterLoadout playerOne, FighterLoadout playerTwo)
            : this(playerOne.AbilityModes, playerTwo.AbilityModes) { }

        /// <summary>The authored hit contract of <paramref name="playerID"/>'s ability in <paramref name="slot"/>.</summary>
        public FighterAbilityHitData For(int playerID, int slot) {
            FighterAbilityLoadout loadout = playerID == 1 ? _playerTwo : _playerOne;
            return slot switch {
                SlotSpecialOne => loadout.SpecialOneHit,
                SlotSpecialTwo => loadout.SpecialTwoHit,
                SlotUltimate => loadout.UltimateHit,
                _ => loadout.MovementHit
            };
        }

        /// <summary>
        /// A02/D15 (Package 13 W6): <paramref name="playerID"/>'s Ultimate
        /// sequence and activation strike — loadout data, never snapshot state.
        /// A hand-built loadout that never set it reads the generic default.
        /// </summary>
        public FighterUltimateData UltimateFor(int playerID) {
            FighterUltimateData data = (playerID == 1 ? _playerTwo : _playerOne).Ultimate;
            return data.IsUnset ? FighterUltimateData.Default : data;
        }
    }

    /// <summary>
    /// Immutable per-character ability execution configuration baked from normalized
    /// resources. Effect lifetimes reuse the shared persistent-lifetime fields for
    /// zones as well as constructs.
    /// </summary>
    public readonly struct FighterAbilityLoadout {
        // --- M08 (Package 12 W3) hit contracts, per slot ---
        public FighterAbilityHitData SpecialOneHit { get; init; }
        public FighterAbilityHitData SpecialTwoHit { get; init; }
        public FighterAbilityHitData UltimateHit { get; init; }
        public FighterAbilityHitData MovementHit { get; init; }
        /// <summary>A02/D15 (Package 13 W6): the Ultimate's sequence and activation strike.</summary>
        public FighterUltimateData Ultimate { get; init; }

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
            MovementSpeed = FP64.FromInt(10),
            SpecialOneHit = FighterAbilityHitData.DefaultSpecial,
            SpecialTwoHit = FighterAbilityHitData.DefaultSpecial,
            UltimateHit = FighterAbilityHitData.DefaultUltimate,
            MovementHit = FighterAbilityHitData.DefaultSpecial,
            Ultimate = FighterUltimateData.Default
        };
    }

    public readonly struct FighterMatchRules {
        public readonly int MatchMode;
        public readonly bool ItemsEnabled;
        public readonly int ItemFrequency;
        public readonly bool HazardsEnabled;
        /// <summary>
        /// Package 12 W5: the frames between hazard activations when hazards are
        /// On. Resolved at construction — a caller passing 0 gets the stage's
        /// authored cadence (<see cref="FighterHazardCadence.AuthoredFrames"/>),
        /// which is what every production match does. The retired
        /// <c>HazardTriggerFrequency</c> ordinal it replaces is gone from the
        /// rules and the match component.
        /// </summary>
        public readonly int HazardCadenceFrames;
        public readonly int StageHazardTypeID;
        /// <summary>
        /// Package 12 W5 (M24, D5(b)): Resonance Surge joins the orb draw only
        /// when this is set. Off by default, so default schedules are unchanged.
        /// </summary>
        public readonly bool MeterPickupsEnabled;
        /// <summary>
        /// Pre-match 3-2-1 countdown length. Deterministic state: it is written
        /// into <see cref="FighterMatchComponent.CountdownFramesRemaining"/> and
        /// participates in every snapshot and hash. Defaults to zero so headless
        /// scenarios start live; the Godot driver passes
        /// <see cref="FighterMatchFlowRules.CountdownFrames"/>.
        /// </summary>
        public readonly int PreMatchCountdownFrames;

        public FighterMatchRules(bool itemsEnabled, int itemFrequency, bool hazardsEnabled, int hazardCadenceFrames)
            : this((int)FTT.Core.MatchMode.Stock, itemsEnabled, itemFrequency, hazardsEnabled, hazardCadenceFrames, 1, 0) { }

        public FighterMatchRules(int matchMode, bool itemsEnabled, int itemFrequency, bool hazardsEnabled, int hazardCadenceFrames)
            : this(matchMode, itemsEnabled, itemFrequency, hazardsEnabled, hazardCadenceFrames, 1, 0) { }

        public FighterMatchRules(
            int matchMode,
            bool itemsEnabled,
            int itemFrequency,
            bool hazardsEnabled,
            int hazardCadenceFrames,
            int stageHazardTypeID)
            : this(matchMode, itemsEnabled, itemFrequency, hazardsEnabled, hazardCadenceFrames, stageHazardTypeID, 0) { }

        /// <param name="hazardCadenceFrames">
        /// 0 = the stage's authored cadence (every production match). A positive
        /// override is a test seam and must be at least
        /// <see cref="FighterHazardCadence.MinimumOverrideFrames"/>: a value below
        /// that can only be a stale <c>HazardTriggerFrequency</c> ordinal from
        /// before Package 12, so it throws rather than silently running hazards
        /// every couple of frames.
        /// </param>
        public FighterMatchRules(
            int matchMode,
            bool itemsEnabled,
            int itemFrequency,
            bool hazardsEnabled,
            int hazardCadenceFrames,
            int stageHazardTypeID,
            int preMatchCountdownFrames,
            bool meterPickupsEnabled = false) {
            if (hazardCadenceFrames < 0
                || (hazardCadenceFrames > 0 && hazardCadenceFrames < FighterHazardCadence.MinimumOverrideFrames)) {
                throw new System.ArgumentOutOfRangeException(
                    nameof(hazardCadenceFrames), hazardCadenceFrames,
                    "Hazard cadence is in frames (0 = the stage's authored cadence); the retired frequency ordinal is not accepted.");
            }
            MatchMode = matchMode;
            ItemsEnabled = itemsEnabled;
            ItemFrequency = itemFrequency;
            HazardsEnabled = hazardsEnabled;
            StageHazardTypeID = System.Math.Clamp(stageHazardTypeID, 1, 10);
            HazardCadenceFrames = hazardCadenceFrames > 0
                ? hazardCadenceFrames
                : FighterHazardCadence.AuthoredFrames(StageHazardTypeID);
            PreMatchCountdownFrames = preMatchCountdownFrames > 0 ? preMatchCountdownFrames : 0;
            MeterPickupsEnabled = meterPickupsEnabled;
        }

        public FighterMatchRules WithCountdown(int preMatchCountdownFrames) => new(
            MatchMode,
            ItemsEnabled,
            ItemFrequency,
            HazardsEnabled,
            HazardCadenceFrames,
            StageHazardTypeID,
            preMatchCountdownFrames,
            MeterPickupsEnabled);

        /// <summary>
        /// Items and hazards off. F21 (Package 11 A1c): the mode is explicit Stock
        /// now — it used to be the literal <c>2</c>, the retired Hybrid ordinal,
        /// which only worked because <c>FighterMatchSystem.Update</c> carried a
        /// silent <c>_ =&gt;</c> default arm. That arm is gone.
        /// </summary>
        public static FighterMatchRules Disabled =>
            new((int)FTT.Core.MatchMode.Stock, false, 0, false, 0);
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
        /// <summary>
        /// F23 (Package 11 A1c): the grab partner and the once-only throw-damage
        /// guard, packed into this component's <b>last</b> four bytes. The partner
        /// used to be resolved positionally by the two-fighter systems and the
        /// throw had no guard beyond the phase machine; both are explicit snapshot
        /// state now. Read and write through <see cref="GrabPartnerPlayerID"/> and
        /// <see cref="ThrowDamageApplied"/>, never through this field. Packing
        /// rather than two ints is forced: 19 ints + 6 FP64 was 124 B and Klotho's
        /// per-component budget is 128 (plan §2.7).
        /// </summary>
        private int _grabPartnerState;

        /// <summary>Player ID this fighter is grabbing or held by; -1 when unattached.</summary>
        public int GrabPartnerPlayerID {
            readonly get => (_grabPartnerState & 0xFF) == 0xFF ? -1 : _grabPartnerState & 0xFF;
            set => _grabPartnerState = (_grabPartnerState & ~0xFF) | (value < 0 ? 0xFF : value & 0xFF);
        }

        /// <summary>Once-only guard: this grab's throw has already dealt its damage.</summary>
        public bool ThrowDamageApplied {
            readonly get => (_grabPartnerState & 0x100) != 0;
            set => _grabPartnerState = value
                ? _grabPartnerState | 0x100
                : _grabPartnerState & ~0x100;
        }

        /// <summary>Clears the packed grab attachment: partner -1, throw guard down.</summary>
        public void ClearGrabPartner() => _grabPartnerState = 0xFF;
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
    /// Slice 0 of the V7.6 Echo Step position ring (Package 11 A1c, plan §2.7):
    /// samples 0..5 of the 31-sample, one-per-tick history.
    /// <para>31 x FPVector2 is 496 bytes, so the ring cannot live in one Klotho
    /// component; §2.7 assigns the bank IDs 313-317. Every slice is snapshot and
    /// hash state exactly like the component it replaced (the retired ID 311
    /// <c>FighterEchoRingComponent</c>, 5 samples every 6 frames).</para>
    /// </summary>
    [KlothoComponent(313, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterEchoStepRing0Component : IComponent {
        public FP64 Sample00X; public FP64 Sample00Y;
        public FP64 Sample01X; public FP64 Sample01Y;
        public FP64 Sample02X; public FP64 Sample02Y;
        public FP64 Sample03X; public FP64 Sample03Y;
        public FP64 Sample04X; public FP64 Sample04Y;
        public FP64 Sample05X; public FP64 Sample05Y;
        /// <summary>Next slot to overwrite, in whole-ring coordinates (0..30).</summary>
        public int Head;
        /// <summary>Tick ID of the newest sample. Monotonic, one per simulation tick.</summary>
        public int LatestTick;
        /// <summary>Real samples written since the last generation reset, capped at <see cref="FighterEchoStepRing.SampleCount"/>.</summary>
        public int ValidCount;
        /// <summary>Bumped on every forced relocation (spawn, respawn, stock loss, Sudden Death setup).</summary>
        public int Generation;
        /// <summary>
        /// The <see cref="FighterRuntimeComponent.KnockoutsSuffered"/> value this
        /// generation was opened at. It is how a stock loss resets the history
        /// without <c>FighterSimulationRules.ApplyStockLoss</c> needing a
        /// <c>Frame</c>: the sampler resets whenever the two disagree. Pure
        /// snapshot state, so a rollback across a KO restores the same answer.
        /// </summary>
        public int LifeEpoch;
        /// <summary>1 while an accepted activation's wind-up owns a locked destination.</summary>
        public int Armed;
        /// <summary>Tick ID the armed activation was accepted on.</summary>
        public int ActivationTick;

        /// <summary>Reads a sample by its index within this slice.</summary>
        public readonly FPVector2 Local(int index) => index switch {
            0 => new FPVector2(Sample00X, Sample00Y),
            1 => new FPVector2(Sample01X, Sample01Y),
            2 => new FPVector2(Sample02X, Sample02Y),
            3 => new FPVector2(Sample03X, Sample03Y),
            4 => new FPVector2(Sample04X, Sample04Y),
            _ => new FPVector2(Sample05X, Sample05Y)
        };

        /// <summary>Writes a sample by its index within this slice.</summary>
        public void SetLocal(int index, in FPVector2 value) {
            switch (index) {
                case 0: Sample00X = value.x; Sample00Y = value.y; break;
                case 1: Sample01X = value.x; Sample01Y = value.y; break;
                case 2: Sample02X = value.x; Sample02Y = value.y; break;
                case 3: Sample03X = value.x; Sample03Y = value.y; break;
                case 4: Sample04X = value.x; Sample04Y = value.y; break;
                default: Sample05X = value.x; Sample05Y = value.y; break;
            }
        }
    }

    /// <summary>
    /// Slice 1 of the V7.6 Echo Step position ring (Package 11 A1c, plan §2.7):
    /// samples 6..11 of the 31-sample, one-per-tick history.
    /// <para>31 x FPVector2 is 496 bytes, so the ring cannot live in one Klotho
    /// component; §2.7 assigns the bank IDs 313-317. Every slice is snapshot and
    /// hash state exactly like the component it replaced (the retired ID 311
    /// <c>FighterEchoRingComponent</c>, 5 samples every 6 frames).</para>
    /// </summary>
    [KlothoComponent(314, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterEchoStepRing1Component : IComponent {
        public FP64 Sample06X; public FP64 Sample06Y;
        public FP64 Sample07X; public FP64 Sample07Y;
        public FP64 Sample08X; public FP64 Sample08Y;
        public FP64 Sample09X; public FP64 Sample09Y;
        public FP64 Sample10X; public FP64 Sample10Y;
        public FP64 Sample11X; public FP64 Sample11Y;
        /// <summary>Reads a sample by its index within this slice.</summary>
        public readonly FPVector2 Local(int index) => index switch {
            0 => new FPVector2(Sample06X, Sample06Y),
            1 => new FPVector2(Sample07X, Sample07Y),
            2 => new FPVector2(Sample08X, Sample08Y),
            3 => new FPVector2(Sample09X, Sample09Y),
            4 => new FPVector2(Sample10X, Sample10Y),
            _ => new FPVector2(Sample11X, Sample11Y)
        };

        /// <summary>Writes a sample by its index within this slice.</summary>
        public void SetLocal(int index, in FPVector2 value) {
            switch (index) {
                case 0: Sample06X = value.x; Sample06Y = value.y; break;
                case 1: Sample07X = value.x; Sample07Y = value.y; break;
                case 2: Sample08X = value.x; Sample08Y = value.y; break;
                case 3: Sample09X = value.x; Sample09Y = value.y; break;
                case 4: Sample10X = value.x; Sample10Y = value.y; break;
                default: Sample11X = value.x; Sample11Y = value.y; break;
            }
        }
    }

    /// <summary>
    /// Slice 2 of the V7.6 Echo Step position ring (Package 11 A1c, plan §2.7):
    /// samples 12..17 of the 31-sample, one-per-tick history.
    /// <para>31 x FPVector2 is 496 bytes, so the ring cannot live in one Klotho
    /// component; §2.7 assigns the bank IDs 313-317. Every slice is snapshot and
    /// hash state exactly like the component it replaced (the retired ID 311
    /// <c>FighterEchoRingComponent</c>, 5 samples every 6 frames).</para>
    /// </summary>
    [KlothoComponent(315, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterEchoStepRing2Component : IComponent {
        public FP64 Sample12X; public FP64 Sample12Y;
        public FP64 Sample13X; public FP64 Sample13Y;
        public FP64 Sample14X; public FP64 Sample14Y;
        public FP64 Sample15X; public FP64 Sample15Y;
        public FP64 Sample16X; public FP64 Sample16Y;
        public FP64 Sample17X; public FP64 Sample17Y;
        /// <summary>Reads a sample by its index within this slice.</summary>
        public readonly FPVector2 Local(int index) => index switch {
            0 => new FPVector2(Sample12X, Sample12Y),
            1 => new FPVector2(Sample13X, Sample13Y),
            2 => new FPVector2(Sample14X, Sample14Y),
            3 => new FPVector2(Sample15X, Sample15Y),
            4 => new FPVector2(Sample16X, Sample16Y),
            _ => new FPVector2(Sample17X, Sample17Y)
        };

        /// <summary>Writes a sample by its index within this slice.</summary>
        public void SetLocal(int index, in FPVector2 value) {
            switch (index) {
                case 0: Sample12X = value.x; Sample12Y = value.y; break;
                case 1: Sample13X = value.x; Sample13Y = value.y; break;
                case 2: Sample14X = value.x; Sample14Y = value.y; break;
                case 3: Sample15X = value.x; Sample15Y = value.y; break;
                case 4: Sample16X = value.x; Sample16Y = value.y; break;
                default: Sample17X = value.x; Sample17Y = value.y; break;
            }
        }
    }

    /// <summary>
    /// Slice 3 of the V7.6 Echo Step position ring (Package 11 A1c, plan §2.7):
    /// samples 18..23 of the 31-sample, one-per-tick history.
    /// <para>31 x FPVector2 is 496 bytes, so the ring cannot live in one Klotho
    /// component; §2.7 assigns the bank IDs 313-317. Every slice is snapshot and
    /// hash state exactly like the component it replaced (the retired ID 311
    /// <c>FighterEchoRingComponent</c>, 5 samples every 6 frames).</para>
    /// </summary>
    [KlothoComponent(316, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterEchoStepRing3Component : IComponent {
        public FP64 Sample18X; public FP64 Sample18Y;
        public FP64 Sample19X; public FP64 Sample19Y;
        public FP64 Sample20X; public FP64 Sample20Y;
        public FP64 Sample21X; public FP64 Sample21Y;
        public FP64 Sample22X; public FP64 Sample22Y;
        public FP64 Sample23X; public FP64 Sample23Y;
        /// <summary>Reads a sample by its index within this slice.</summary>
        public readonly FPVector2 Local(int index) => index switch {
            0 => new FPVector2(Sample18X, Sample18Y),
            1 => new FPVector2(Sample19X, Sample19Y),
            2 => new FPVector2(Sample20X, Sample20Y),
            3 => new FPVector2(Sample21X, Sample21Y),
            4 => new FPVector2(Sample22X, Sample22Y),
            _ => new FPVector2(Sample23X, Sample23Y)
        };

        /// <summary>Writes a sample by its index within this slice.</summary>
        public void SetLocal(int index, in FPVector2 value) {
            switch (index) {
                case 0: Sample18X = value.x; Sample18Y = value.y; break;
                case 1: Sample19X = value.x; Sample19Y = value.y; break;
                case 2: Sample20X = value.x; Sample20Y = value.y; break;
                case 3: Sample21X = value.x; Sample21Y = value.y; break;
                case 4: Sample22X = value.x; Sample22Y = value.y; break;
                default: Sample23X = value.x; Sample23Y = value.y; break;
            }
        }
    }

    /// <summary>
    /// Slice 4 of the V7.6 Echo Step position ring (Package 11 A1c, plan §2.7):
    /// samples 24..30 of the 31-sample, one-per-tick history.
    /// <para>31 x FPVector2 is 496 bytes, so the ring cannot live in one Klotho
    /// component; §2.7 assigns the bank IDs 313-317. Every slice is snapshot and
    /// hash state exactly like the component it replaced (the retired ID 311
    /// <c>FighterEchoRingComponent</c>, 5 samples every 6 frames).</para>
    /// </summary>
    [KlothoComponent(317, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterEchoStepRing4Component : IComponent {
        public FP64 Sample24X; public FP64 Sample24Y;
        public FP64 Sample25X; public FP64 Sample25Y;
        public FP64 Sample26X; public FP64 Sample26Y;
        public FP64 Sample27X; public FP64 Sample27Y;
        public FP64 Sample28X; public FP64 Sample28Y;
        public FP64 Sample29X; public FP64 Sample29Y;
        public FP64 Sample30X; public FP64 Sample30Y;
        /// <summary>Reads a sample by its index within this slice.</summary>
        public readonly FPVector2 Local(int index) => index switch {
            0 => new FPVector2(Sample24X, Sample24Y),
            1 => new FPVector2(Sample25X, Sample25Y),
            2 => new FPVector2(Sample26X, Sample26Y),
            3 => new FPVector2(Sample27X, Sample27Y),
            4 => new FPVector2(Sample28X, Sample28Y),
            5 => new FPVector2(Sample29X, Sample29Y),
            _ => new FPVector2(Sample30X, Sample30Y)
        };

        /// <summary>Writes a sample by its index within this slice.</summary>
        public void SetLocal(int index, in FPVector2 value) {
            switch (index) {
                case 0: Sample24X = value.x; Sample24Y = value.y; break;
                case 1: Sample25X = value.x; Sample25Y = value.y; break;
                case 2: Sample26X = value.x; Sample26Y = value.y; break;
                case 3: Sample27X = value.x; Sample27Y = value.y; break;
                case 4: Sample28X = value.x; Sample28Y = value.y; break;
                case 5: Sample29X = value.x; Sample29Y = value.y; break;
                default: Sample30X = value.x; Sample30Y = value.y; break;
            }
        }
    }

    /// <summary>
    /// Whole-ring addressing for the V7.6 Echo Step position history (Package 11
    /// A1c). The bank is five Klotho components (IDs 313-317) holding
    /// <see cref="SampleCount"/> = 31 consecutive per-tick position samples; this
    /// class is the only place that knows how a whole-ring slot maps onto a slice,
    /// so no consumer ever has to.
    ///
    /// <para>Why 31 and not 30: the newest sample is written at the START of tick
    /// <c>t</c>, before action input and movement, so <c>t - 30</c> must survive
    /// that write. 31 slots is the smallest ring where it does.</para>
    /// </summary>
    public static class FighterEchoStepRing {
        /// <summary>Consecutive per-tick samples retained: t, t-1, ... t-30.</summary>
        public const int SampleCount = 31;

        /// <summary>Samples in slices 0-3; slice 4 holds the remaining seven.</summary>
        private const int SliceStride = 6;

        /// <summary>Reads whole-ring slot <paramref name="slot"/> (0..30).</summary>
        public static FPVector2 Read(
            in FighterEchoStepRing0Component slice0,
            in FighterEchoStepRing1Component slice1,
            in FighterEchoStepRing2Component slice2,
            in FighterEchoStepRing3Component slice3,
            in FighterEchoStepRing4Component slice4,
            int slot) {
            int wrapped = Wrap(slot);
            return wrapped switch {
                < SliceStride => slice0.Local(wrapped),
                < SliceStride * 2 => slice1.Local(wrapped - SliceStride),
                < SliceStride * 3 => slice2.Local(wrapped - SliceStride * 2),
                < SliceStride * 4 => slice3.Local(wrapped - SliceStride * 3),
                _ => slice4.Local(wrapped - SliceStride * 4)
            };
        }

        /// <summary>Writes whole-ring slot <paramref name="slot"/> (0..30).</summary>
        public static void Write(
            ref FighterEchoStepRing0Component slice0,
            ref FighterEchoStepRing1Component slice1,
            ref FighterEchoStepRing2Component slice2,
            ref FighterEchoStepRing3Component slice3,
            ref FighterEchoStepRing4Component slice4,
            int slot,
            in FPVector2 value) {
            int wrapped = Wrap(slot);
            if (wrapped < SliceStride) slice0.SetLocal(wrapped, in value);
            else if (wrapped < SliceStride * 2) slice1.SetLocal(wrapped - SliceStride, in value);
            else if (wrapped < SliceStride * 3) slice2.SetLocal(wrapped - SliceStride * 2, in value);
            else if (wrapped < SliceStride * 4) slice3.SetLocal(wrapped - SliceStride * 3, in value);
            else slice4.SetLocal(wrapped - SliceStride * 4, in value);
        }

        /// <summary>Non-negative modulo; the ring index is never allowed to go negative.</summary>
        public static int Wrap(int slot) {
            int wrapped = slot % SampleCount;
            return wrapped < 0 ? wrapped + SampleCount : wrapped;
        }
    }

    /// <summary>
    /// F22 Sudden Death phase state (Package 11 A1c, plan §2.7 ID 319). A
    /// singleton beside <see cref="FighterMatchComponent"/> holding the pieces the
    /// contract requires to be committed <em>with</em> the phase transition:
    /// the phase generation (so a late regulation event is discarded by identity
    /// rather than by timing), the frozen regulation stocks-lost totals, and the
    /// per-fighter once-only respawn-HP grant.
    ///
    /// <para>The Defy disable is deliberately <b>not</b> stored here: it is derived
    /// from <see cref="FighterMatchComponent.SuddenDeathActive"/> at the read site,
    /// so nothing can restore a stale copy of it.</para>
    /// </summary>
    [KlothoComponent(319)]
    [KlothoSingletonComponent]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterSuddenDeathComponent : IComponent {
        /// <summary>
        /// Bumped once on entry. Zero means regulation has never ended; every
        /// gameplay event carries no generation of its own, so this is read as
        /// "the phase identity the match is currently in".
        /// </summary>
        public int PhaseGeneration;
        /// <summary>Player one's regulation stocks-lost total, frozen at entry.</summary>
        public int FrozenPlayerOneStocksLost;
        /// <summary>Player two's regulation stocks-lost total, frozen at entry.</summary>
        public int FrozenPlayerTwoStocksLost;
        /// <summary>1 once player one's dead-entry respawn HP has been granted (F22 Option A: exactly once).</summary>
        public int PlayerOneRespawnHPGranted;
        /// <summary>1 once player two's dead-entry respawn HP has been granted.</summary>
        public int PlayerTwoRespawnHPGranted;
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

    /// <summary>
    /// M05 knockdown and get-up (Package 12 W3b; Klotho ID 320, allocated by
    /// the Package 12 plan §3.3 — the only ID this plan hands out). A tumbling
    /// victim who misses the landing tech enters a 30-frame invulnerable
    /// knockdown, a sub-phase of <c>Stunned</c>, then a neutral (10 f) or roll
    /// (14 f) get-up chosen from the held direction on the frame the knockdown
    /// ends. Neither get-up is invulnerable, and both lock every action.
    ///
    /// <para>Seven ints and one FP64, 36 bytes, since Package 13 W1 added the
    /// A03 air-dodge latch and the A12 slam-bounce pair and W7b the M04 staff-refund latch (plan D7 allocated
    /// them here; no new component ID). Snapshot and hash state like every
    /// other component. Zero-initialised means "not down, dodge available, no
    /// bounce owed": every field's inactive value is 0, so a Klotho default
    /// instance is already correct. The knockdown fields are written only
    /// through <see cref="FighterKnockdownRules"/>, the air dodge through
    /// <see cref="FighterAirDodgeRules"/> and the bounce through
    /// <see cref="FighterSlamRules"/>.</para>
    /// </summary>
    [KlothoComponent(320, MaxCount = 2)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct FighterKnockdownComponent : IComponent {
        /// <summary>Knockdown frames left; &gt; 0 means the fighter is down (and invulnerable).</summary>
        public int KnockdownFrames;
        /// <summary><c>BasicComboRules.GetUpNone / GetUpNeutral / GetUpRoll</c>.</summary>
        public int GetUpKind;
        /// <summary>Get-up frames left; the get-up ends and control returns at 0.</summary>
        public int GetUpFrames;
        /// <summary>Roll get-up direction: -1 left, +1 right, 0 for a neutral get-up.</summary>
        public int GetUpDirection;

        // --- Package 13 W1 (plan D7: 320 carries the landing/aerial state) ---

        /// <summary>
        /// A03: 1 once the fighter has air-dodged this airtime. Refreshed by
        /// landing and by a ledge grab; cleared on stock loss.
        /// </summary>
        public int AirDodgeUsed;
        /// <summary>
        /// A12: 1 while a Down-Air slam's single forced ground bounce is owed —
        /// set when the slam lands, consumed by the first floor or one-way
        /// surface contact during that tumble (untechable), cleared when the
        /// tumble ends another way or on stock loss.
        /// </summary>
        public int SlamBouncePending;
        /// <summary>
        /// A12: the bounce's upward speed — the slam's own scaled vertical
        /// magnitude (the Up-Attack's 2.5× vertical profile, weight and low-HP
        /// scaled at the slam). Zero when no bounce is owed.
        /// </summary>
        public FP64 SlamBounceSpeed;

        // --- Package 13 W7b (plan D7: 320 carries the landing/aerial state) ---

        /// <summary>
        /// M04: 1 once a Sonata Drift staff landing has refunded half the movement
        /// cooldown this airtime. Reset by landing on real ground (an authored
        /// surface, not a staff) or by a ledge grab.
        /// </summary>
        public int StaffRefundUsed;
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
        /// <summary>
        /// Package 12 W5: the resolved hazard cadence in frames (the stage's
        /// authored value in every production match). Replaces the retired
        /// <c>HazardFrequency</c> ordinal in the same slot, so the component did
        /// not grow for it.
        /// </summary>
        public int HazardCadenceFrames;
        public int StageHazardTypeID;
        /// <summary>
        /// Package 12 W5 (M24): 1 when Resonance Surge is in the orb draw. Snapshot
        /// state because it selects the width of a seeded draw.
        /// </summary>
        public int MeterPickupsEnabled;
        public int NextOrbSpawnFrames;
        public int NextHazardSpawnFrames;
        /// <summary>
        /// F21 (Package 11 A1c): stocks player one has <b>lost</b>, every cause.
        /// The field it replaces (<c>PlayerOneKOs</c>) counted KOs player one
        /// SCORED, and the Time-mode winner test read it as "most KOs scored
        /// wins" — the opposite bookkeeping to the contract. Fed from the victim's
        /// own <see cref="FighterRuntimeComponent.KnockoutsSuffered"/> through the
        /// single chokepoint <c>FighterSimulationRules.ApplyStockLoss</c>, so
        /// every cause (opponent damage, pit, hazard, self-KO, DoT, a surviving
        /// projectile or construct) increments it exactly once, atomically with
        /// the living-to-KO transition and before respawn.
        /// </summary>
        public int PlayerOneStocksLost;
        /// <inheritdoc cref="PlayerOneStocksLost"/>
        public int PlayerTwoStocksLost;
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
        /// <summary>
        /// Package 13 W7a (L03/D14): 1 once this Clockwork Turret has fired the
        /// owner's once-per-flight Ornithopter glide bolt; cleared when a new
        /// flight starts. Zero on every other construct.
        /// </summary>
        public int GlideBoltSpent;
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
        /// <summary>
        /// Package 13 W7a (E01 Rift Collapse): frames left in the collapse pull
        /// of Einstein's Relativity Rift. Zero on every other zone and on a rift
        /// that has not collapsed. While positive the rift no longer ticks or
        /// buffs; it drags the caught opponents to its centre and launches them
        /// when the count reaches zero.
        /// </summary>
        public int CollapseFramesRemaining;
        /// <summary>
        /// One bit per PlayerID caught (bit 0 = P1, bit 1 = P2): by a rift's E01
        /// collapse, or by The Tempest's windbox (S01), which keeps pushing a
        /// caught opponent for the rest of the storm. Zero on every other zone.
        /// </summary>
        public int CollapseTargetMask;
        /// <summary>
        /// Package 13 W7b: per-execution flags a zone carries for its whole life
        /// (<c>FighterReachKitRules.FlagFortissimoShaved</c>: the Requiem Chord
        /// execution this burst belongs to has already shaved Fortissimo's
        /// cooldown — M02's once-per-execution rule). Zero for every other zone.
        /// </summary>
        public int ExecutionFlags;
        public FP64 StatusIntensity;
        public FPVector2 Position;
        public FPVector2 HalfExtents;
    }
}
