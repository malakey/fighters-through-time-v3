using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// The deterministic half of the universal three-hit basic combo. All
    /// timings come from <see cref="FTT.Combat.BasicComboRules"/> — one shared
    /// rulebook with Story. The phase machine advances in
    /// <see cref="FighterMovementSystem"/> so movement locks and cancels resolve
    /// in the same tick they gate; <see cref="FighterCombatSystem"/> applies the
    /// hit during active frames.
    /// </summary>
    internal static class FighterBasicAttackRules {
        public const int PhaseNone = 0;
        public const int PhaseStartup = 1;
        public const int PhaseActive = 2;
        public const int PhaseRecovery = 3;
        public const int PhaseChainHold = 4;

        /// <summary>
        /// The swing started airborne, so landing cancels it with no lag. Since
        /// GU-A only a single strike (the air strike, an airborne up-attack, the
        /// down-air) carries it: the chain never starts in the air.
        /// </summary>
        public const int FlagAerial = 1;
        public const int FlagHitResolved = 2;
        public const int FlagBuffered = 4;
        /// <summary>Directional-attack variant latch (§2.8): up-attack.</summary>
        public const int FlagUpAttack = 8;
        /// <summary>Directional-attack variant latch (§2.8): down-air.</summary>
        public const int FlagDownAir = 16;
        /// <summary>
        /// GU-A (2026-10-12): the single air strike's variant latch, the next
        /// spare bit of the existing <c>AttackFlags</c> word (no new component
        /// state, no protocol change).
        /// </summary>
        public const int FlagAirAttack = 128;
        /// <summary>
        /// CM-MOVE (decision 5.2, 2026-10-16): the ledge attack's variant latch,
        /// the bit plan §1.5 reserved in the existing <c>AttackFlags</c> word (no
        /// new component state). A single strike outside the chain: string hit
        /// 1's box and knockback at one basic, never a launch
        /// (<c>FighterCombatSystem.ApplyLedgeAttackSwing</c>), started only by
        /// the ledge attack's climb (<see cref="StartLedgeAttack"/>).
        /// </summary>
        public const int FlagLedgeAttack = 512;
        /// <summary>Any variant bit — a single strike outside the three-hit chain.</summary>
        public const int VariantMask = FlagUpAttack | FlagDownAir | FlagAirAttack | FlagLedgeAttack;
        /// <summary>One construct sweep per swing (2026-08-11: basics damage enemy constructs).</summary>
        public const int FlagConstructHitResolved = 32;
        /// <summary>
        /// F3 (2026-10-04 feel pass): a second BasicAttack press made while the
        /// next hit was already buffered — it buffers the hit after that one, so a
        /// quick double tap during one swing is not eaten. One deep, a spare bit
        /// of the existing <c>AttackFlags</c> word: no new component state.
        /// </summary>
        public const int FlagQueuedBuffered = 64;
        /// <summary>
        /// Combat and movement update 2026-10-16, decision 10.1 (plan §2.10.1):
        /// the string box's facing, latched on every Startup → Active transition
        /// (chain continuations included) and read by the three box callers —
        /// the fighter-target chain hit, the air strike and the construct sweep —
        /// so steering mid-active turns the fighter and the sprite but never
        /// flips the box, as Story places its box once at active start. A spare
        /// bit of the existing <c>AttackFlags</c> word (plan §1.5); every swing
        /// start rewrites the flags, so it never leaks into the next swing. The
        /// Up-Attack and the Down-Air test a facing-symmetric box and need no
        /// latch. Bit 512 is reserved for CM-MOVE's <c>FlagLedgeAttack</c>.
        /// </summary>
        public const int FlagActiveFacingRight = 256;

        private const int BasicButton = 1 << 2;
        private const int BlockButton = 1 << 6;

        /// <summary>
        /// F3: buffers this tick's BasicAttack press for the next chain hit — in
        /// startup, ACTIVE frames, recovery, or the hitstop freeze — or queues it
        /// for the hit after when one is already buffered. The air strike, the
        /// directional strikes and the chain-hold window (where a press starts
        /// the next swing outright) never buffer. Story's
        /// <c>PlayerController.BufferChainPress</c> is the same rule.
        /// </summary>
        public static void BufferChainPress(ref FighterRuntimeComponent runtime) {
            if ((runtime.PressedButtons & BasicButton) == 0) return;
            if (!IsSwinging(in runtime) || IsVariantSwing(in runtime)) return;
            if ((runtime.AttackFlags & FlagBuffered) != 0) runtime.AttackFlags |= FlagQueuedBuffered;
            else runtime.AttackFlags |= FlagBuffered;
        }

        public static bool IsSwinging(in FighterRuntimeComponent runtime) =>
            runtime.AttackPhase is PhaseStartup or PhaseActive or PhaseRecovery;

        /// <summary>True while the running swing is the air strike, an up-attack or a down-air.</summary>
        public static bool IsVariantSwing(in FighterRuntimeComponent runtime) =>
            (runtime.AttackFlags & VariantMask) != 0;

        /// <summary>
        /// The string step a BasicAttack press would continue into right now
        /// (the CPU's melee band, 2026-10-11): the next hit's index while a
        /// chain swing of hit 1 or hit 2 runs, any phase, the chain hold
        /// included; otherwise 0. The finisher cannot continue, and the air
        /// strike and the directional swings stand outside the chain (GU-A), so
        /// a press during any of them can at most open a fresh string or air
        /// strike, both at hit 1's reach. It reads no ground state: a grounded
        /// string airborne mid-swing (a one-way drop-through) still reports its
        /// next hit, though that continuation now ends the string. Read-only.
        /// </summary>
        public static int NextChainStep(in FighterRuntimeComponent runtime) {
            if (runtime.AttackPhase == PhaseNone || IsVariantSwing(in runtime)) return 0;
            int index = runtime.ComboIndex < 0 ? 0 : runtime.ComboIndex;
            return index < FTT.Combat.BasicComboRules.ComboHits - 1 ? index + 1 : 0;
        }

        /// <summary>
        /// Reads the swing variant out of the held input, through the shared
        /// selection rule. Quantized MoveY &lt; -30 is the sim's "Up held"
        /// (Story uses the float equivalent, -0.25); Down is the button bit,
        /// as everywhere else in the simulation.
        /// </summary>
        public static int SelectVariant(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime) =>
            FTT.Combat.BasicComboRules.SelectAttackVariant(
                upHeld: runtime.MoveY < -30,
                downHeld: (runtime.HeldButtons & (1 << 1)) != 0,
                airborne: fighter.IsGrounded == 0);

        // V7.1 per-character string profiles, indexed by FighterCharacterID
        // (Einstein 0 .. Tubman 9). Resolved once from the shared rulebook —
        // the same profiles Story consumes — so the modes cannot drift; unknown
        // IDs fall back to the template. Package 13 W5: ordinal 8 is the
        // reserved Pocahontas slot (no row left, so it resolves the template);
        // Tubman plays the template profile.
        private static readonly FTT.Combat.BasicStringProfile[] StringProfiles = {
            FTT.Combat.BasicComboRules.StringProfileFor("einstein"),
            FTT.Combat.BasicComboRules.StringProfileFor("joan"),
            FTT.Combat.BasicComboRules.StringProfileFor("leonardo"),
            FTT.Combat.BasicComboRules.StringProfileFor("lincoln"),
            FTT.Combat.BasicComboRules.StringProfileFor("cleopatra"),
            FTT.Combat.BasicComboRules.StringProfileFor("tesla"),
            FTT.Combat.BasicComboRules.StringProfileFor("shakespeare"),
            FTT.Combat.BasicComboRules.StringProfileFor("mozart"),
            FTT.Combat.BasicComboRules.TemplateStringProfile,
            FTT.Combat.BasicComboRules.StringProfileFor("tubman")
        };

        public static FTT.Combat.BasicStringProfile StringProfileFor(int characterID) =>
            characterID >= 0 && characterID < StringProfiles.Length
                ? StringProfiles[characterID]
                : FTT.Combat.BasicComboRules.TemplateStringProfile;

        public static int StartupFramesFor(int characterID, int attackFlags, int comboStep) {
            if ((attackFlags & FlagLedgeAttack) != 0) return FTT.Combat.BasicComboRules.LedgeAttackStartupFrames;
            if ((attackFlags & FlagUpAttack) != 0) return FTT.Combat.BasicComboRules.UpAttackStartupFrames;
            if ((attackFlags & FlagDownAir) != 0) return FTT.Combat.BasicComboRules.DownAirStartupFrames;
            // GU-A: the air strike's startup is the profile's air axis.
            if ((attackFlags & FlagAirAttack) != 0) return StringProfileFor(characterID).AirAttackStartupFrames;
            // Chain opener/finisher startups are the authored per-character axis
            // (V7.1); active and recovery frames below stay universal. The
            // string is grounded only (GU-A), so it has one frame table.
            return StringProfileFor(characterID).GroundStartupFrames[comboStep];
        }

        public static int ActiveFramesFor(int attackFlags, int comboStep) {
            if ((attackFlags & FlagLedgeAttack) != 0) return FTT.Combat.BasicComboRules.LedgeAttackActiveFrames;
            if ((attackFlags & FlagUpAttack) != 0) return FTT.Combat.BasicComboRules.UpAttackActiveFrames;
            if ((attackFlags & FlagDownAir) != 0) return FTT.Combat.BasicComboRules.DownAirActiveFrames;
            if ((attackFlags & FlagAirAttack) != 0) return FTT.Combat.BasicComboRules.AirAttackActiveFrames;
            return FTT.Combat.BasicComboRules.GroundActiveFrames[comboStep];
        }

        public static int RecoveryFramesFor(int attackFlags, int comboStep) {
            if ((attackFlags & FlagLedgeAttack) != 0) return FTT.Combat.BasicComboRules.LedgeAttackRecoveryFrames;
            if ((attackFlags & FlagUpAttack) != 0) return FTT.Combat.BasicComboRules.UpAttackRecoveryFrames;
            if ((attackFlags & FlagDownAir) != 0) return FTT.Combat.BasicComboRules.DownAirRecoveryFrames;
            if ((attackFlags & FlagAirAttack) != 0) return FTT.Combat.BasicComboRules.AirAttackRecoveryFrames;
            return FTT.Combat.BasicComboRules.GroundRecoveryFrames[comboStep];
        }

        /// <summary>
        /// The grounded block stance, mirroring Story's Blocking state: grounded,
        /// free of hitstun/daze, not mid roll, not mid swing, holding Block with
        /// at least one charge and no shatter lockout (V7.3). Shieldstun locks
        /// the blocker INTO the stance regardless of held buttons. The movement
        /// lock, the attack/ability gates, and the shield-absorb rule in
        /// FighterDamageRules all key off this one predicate.
        /// </summary>
        public static bool IsBlockStance(
            in FighterStateComponent fighter,
            in FighterRuntimeComponent runtime,
            in FighterVerbComponent verb) {
            if (fighter.Stocks <= 0) return false;
            // V7.3 shieldstun: a blocked hit locks the stance up — the blocker
            // cannot drop block, attack, grab, roll, or jump until it expires.
            if (verb.ShieldStunFrames > 0) return true;
            return fighter.IsGrounded != 0
                && fighter.HitstunFrames <= 0
                && fighter.DazeFrames <= 0
                && fighter.RespawnFramesRemaining <= 0
                // V7.3: an empty shield never raises the stance, and neither
                // does the five-second shatter lockout.
                && fighter.BlockCharges > 0
                && verb.BlockLockoutFrames <= 0
                // V7.3 grab triangle: a grabbing fighter (startup, active, the
                // 24f whiff recovery) or a held victim has NO functioning
                // shield — attack beats grab.
                && verb.GrabPhase == FighterGrabRules.PhaseNone
                && verb.BeingHeld == 0
                && runtime.UniversalMovementState == (int)UniversalMovementPhase.None
                && runtime.AttackPhase == PhaseNone
                && (runtime.HeldButtons & BlockButton) != 0;
        }

        public static void CancelString(ref FighterRuntimeComponent runtime) {
            runtime.AttackPhase = PhaseNone;
            runtime.AttackPhaseFrames = 0;
            runtime.AttackFlags = 0;
            runtime.ComboIndex = 0;
        }

        public static void StartSwing(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            int comboStep,
            int variant = FTT.Combat.BasicComboRules.VariantChain) {
            bool aerial = fighter.IsGrounded == 0;
            // A single strike (the air strike or a directional attack) stands
            // outside the chain: the combo index resets to zero and never
            // advances from here. Every caller keeps the chain on the ground
            // (GU-A), so a chain swing never carries FlagAerial.
            runtime.ComboIndex = variant == FTT.Combat.BasicComboRules.VariantChain ? comboStep : 0;
            runtime.AttackPhase = PhaseStartup;
            int flags = aerial ? FlagAerial : 0;
            if (variant == FTT.Combat.BasicComboRules.VariantUpAttack) flags |= FlagUpAttack;
            else if (variant == FTT.Combat.BasicComboRules.VariantDownAir) flags |= FlagDownAir;
            else if (variant == FTT.Combat.BasicComboRules.VariantAirAttack) flags |= FlagAirAttack;
            runtime.AttackFlags = flags;
            runtime.AttackPhaseFrames = StartupFramesFor(fighter.CharacterID, flags, runtime.ComboIndex);
            // Defensive: universal movement is provably None at every current call
            // site (IsCombatLocked gates fresh swings, and no roll can start while
            // a string is active), so this cancel is a no-op today. It stays so a
            // future cancel window cannot leave a phase running under a swing.
            FighterUniversalMovementRules.Cancel(ref runtime);
            // Every swing start (each chain hit, the air strike, the up-attack,
            // the down-air) locks specials and the Ultimate; the swing itself is
            // never refused.
            // StartSwing runs in FighterMovementSystem, before the ability and
            // combat systems, so a same-tick special or Ultimate press loses to
            // the swing, as it does in Story (CheckAttackInput from neutral,
            // ChainSwingStartsThisTick inside a string). D1 (2026-10-11): it also
            // clears the landed latch, so Specials wait for THIS swing to land.
            FighterAbilityLockoutRules.ArmSwing(ref runtime);
        }

        /// <summary>
        /// CM-MOVE (decision 5.2): starts the ledge strike on the tick the ledge
        /// attack's climb completes — a grounded single strike outside the chain
        /// (<see cref="FlagLedgeAttack"/>), 8 / 4 / 14 frames from
        /// <c>BasicComboRules</c>, arming the ability lockout like every swing
        /// start. Its chokepoint call (<c>FighterActionKind.Swing</c>) was made on
        /// the press tick, at the start of the climb.
        /// </summary>
        public static void StartLedgeAttack(ref FighterStateComponent fighter, ref FighterRuntimeComponent runtime) {
            runtime.ComboIndex = 0;
            runtime.AttackPhase = PhaseStartup;
            runtime.AttackFlags = fighter.IsGrounded == 0 ? FlagAerial | FlagLedgeAttack : FlagLedgeAttack;
            runtime.AttackPhaseFrames = StartupFramesFor(fighter.CharacterID, runtime.AttackFlags, 0);
            FighterUniversalMovementRules.Cancel(ref runtime);
            FighterAbilityLockoutRules.ArmSwing(ref runtime);
        }
    }
}
