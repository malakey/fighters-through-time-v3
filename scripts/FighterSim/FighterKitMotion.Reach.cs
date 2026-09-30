using FTT.Combat;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Package 13 W7b — the multi-frame kit motion of Joan, Cleopatra, Lincoln
    /// and Mozart (character review J01, A08, C03, LN02, M04), as sim-local
    /// phases of the existing universal-movement slot on
    /// <see cref="FighterRuntimeComponent"/>, exactly like the Package 12 W4
    /// blink and Spirit Strike. No new component ID: the three ints the roll
    /// already uses carry every phase, so each one snapshots and hashes for
    /// free, a hitstun-carrying hit cancels it through the existing
    /// <c>FighterUniversalMovementRules.Cancel</c> sites, and
    /// <c>IsCombatLocked</c> keeps attacks, specials and grabs out while it runs.
    ///
    /// <para>Codes 24–29 leave 21–23 free between W4's 16–20 and this block, so a
    /// parallel workstream appending after 20 cannot collide.</para>
    ///
    /// <para>Every number is the normalized loadout (the resource's frames,
    /// distance and speed) or a <see cref="KitReachRules"/> constant shared with
    /// Story. Nothing here reads a Story modifier.</para>
    /// </summary>
    public static partial class FighterKitMotion {
        /// <summary>
        /// J01 Divine Piercing: the forward lunge across the thrust flurry. The
        /// direction int packs facing × (total frames + 100 × thrusts resolved),
        /// so a hitstop that holds the lunge on a thrust frame cannot resolve
        /// that thrust twice.
        /// </summary>
        public const int PiercingLunge = 24;
        /// <summary>A08 Ascendant Wings: the rising slash-leap — the whole cast's frames at the authored speed.</summary>
        public const int WingRise = 25;
        /// <summary>A08: the held Wing-Dive — a steep forward descent; Attack cancels it into the aerial string.</summary>
        public const int WingDive = 26;
        /// <summary>C03 Desert Mirage: the 8-direction sand rush that passes through opponents.</summary>
        public const int SandRush = 27;
        /// <summary>LN02 Rail Charge (any <c>MovementType.Dash</c>): armored travel that stops on contact.</summary>
        public const int RailCharge = 28;
        /// <summary>M04 Sonata Drift: the rising glissando along the held direction.</summary>
        public const int Glissando = 29;
        private const int FirstReachPhase = PiercingLunge;
        private const int LastReachPhase = Glissando;

        private const int BasicButton = 1 << 2;
        private const int MovementButton = 1 << 5;
        private const int StickThreshold = 30;

        private static readonly FP64 LungeUnits = FP64.FromDouble(KitReachRules.DivinePiercingLungeUnits);
        private static readonly FP64 WingDiveForward = FP64.FromDouble(KitReachRules.WingDiveForwardUnitsPerSecond);
        private static readonly FP64 WingDiveDescent = FP64.FromDouble(KitReachRules.WingDiveDescentUnitsPerSecond);
        private static readonly FP64 DiagonalScale = FP64.FromDouble(0.70710678118654752);

        public static bool IsReachPhase(int state) => state >= FirstReachPhase && state <= LastReachPhase;

        /// <summary>The dive, the rush and the glissando fly on their own velocity: no gravity.</summary>
        private static bool ReachSuspendsGravity(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState is WingRise or WingDive or SandRush or Glissando;

        /// <summary>C03: the sand rush passes through opponents — the pushbox is off while it travels.</summary>
        public static bool PassesThroughFighters(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == SandRush;

        private const int ThrustPackingStride = 100;

        /// <summary>0-based count of the lunge frames already travelled (1 on the first moving tick).</summary>
        public static int PiercingElapsedFrames(in FighterRuntimeComponent runtime) =>
            PiercingTotalFrames(runtime.UniversalMovementDirection) - runtime.UniversalMovementFramesRemaining;

        public static int PiercingTotalFrames(int packed) => (packed >= 0 ? packed : -packed) % ThrustPackingStride;
        /// <summary>Thrusts already resolved in this lunge.</summary>
        public static int PiercingThrustsResolved(int packed) => (packed >= 0 ? packed : -packed) / ThrustPackingStride;

        /// <summary>Records one more resolved thrust in the lunge's packed direction.</summary>
        public static void MarkPiercingThrustResolved(ref FighterRuntimeComponent runtime) {
            int facing = PiercingFacing(runtime.UniversalMovementDirection);
            int magnitude = (runtime.UniversalMovementDirection >= 0
                ? runtime.UniversalMovementDirection
                : -runtime.UniversalMovementDirection) + ThrustPackingStride;
            runtime.UniversalMovementDirection = facing * magnitude;
        }
        public static int PiercingFacing(int packed) => packed >= 0 ? 1 : -1;
        public static int ChargeFacing(int packed) => packed >= 0 ? 1 : -1;

        // --- activation -------------------------------------------------------

        public static void StartPiercingLunge(ref FighterRuntimeComponent runtime, int facing, int activeFrames) {
            int frames = activeFrames > 0 ? activeFrames : 12;
            if (frames >= ThrustPackingStride) frames = ThrustPackingStride - 1;
            runtime.UniversalMovementState = PiercingLunge;
            runtime.UniversalMovementFramesRemaining = frames;
            runtime.UniversalMovementDirection = (facing >= 0 ? 1 : -1) * frames;
        }

        /// <summary>
        /// A08: the rising slash-leap. Story climbs at the resource's
        /// <c>MovementSpeed</c> (420 px/s) for the whole cast — startup + active
        /// + recovery, 36 frames, with no gravity in its ability state — so the
        /// sim climbs the same way and the rise height matches across modes
        /// (D13: kept, <c>VERIFY-WING-DIVE-LEAP</c>). Horizontal momentum is
        /// kept; there is no glide.
        /// </summary>
        public static void StartWingRise(
            ref FighterStateComponent fighter, ref FighterRuntimeComponent runtime, FP64 leapSpeed, int facing,
            int riseFrames) {
            fighter.Velocity.y = leapSpeed;
            fighter.IsGrounded = 0;
            runtime.FloatFrames = 0;
            runtime.UniversalMovementState = WingRise;
            runtime.UniversalMovementFramesRemaining = riseFrames > 0 ? riseFrames : 36;
            // Facing sign × leap speed in thousandths, so the rise holds its
            // authored speed without a second field.
            int milli = (int)((leapSpeed * FP64.FromInt(1000)).RawValue / FP64.One.RawValue);
            runtime.UniversalMovementDirection = (facing >= 0 ? 1 : -1) * (milli > 0 ? milli : 1);
        }

        private static int WingFacing(int packed) => packed >= 0 ? 1 : -1;

        /// <summary>C03: the 8-direction rush, direction latched from the held stick (neutral = facing).</summary>
        public static void StartSandRush(ref FighterRuntimeComponent runtime, int facing, int frames) {
            runtime.UniversalMovementState = SandRush;
            runtime.UniversalMovementFramesRemaining = frames > 0 ? frames : 15;
            runtime.UniversalMovementDirection = PackHeldDirection(in runtime, facing, allowDown: true);
        }

        /// <summary>LN02: a charge along facing for the resource's duration.</summary>
        public static void StartRailCharge(ref FighterRuntimeComponent runtime, int facing, int frames) {
            runtime.UniversalMovementState = RailCharge;
            runtime.UniversalMovementFramesRemaining = frames > 0 ? frames : 30;
            runtime.UniversalMovementDirection = facing >= 0 ? 1 : -1;
        }

        /// <summary>M04: the glissando rises along the held direction; a Down component is dropped and neutral rises straight up.</summary>
        public static void StartGlissando(ref FighterRuntimeComponent runtime, int frames) {
            runtime.UniversalMovementState = Glissando;
            runtime.UniversalMovementFramesRemaining = frames > 0 ? frames : 18;
            runtime.UniversalMovementDirection = PackHeldDirection(in runtime, facing: 0, allowDown: false);
        }

        /// <summary>The glissando's direction for a stick reading, before the cast (the staff is placed at its end).</summary>
        public static int GlissandoDirection(in FighterRuntimeComponent runtime) =>
            PackHeldDirection(in runtime, facing: 0, allowDown: false);

        /// <summary>(x + 1) + 3 × (y + 1), world Y up — the blink's packing.</summary>
        private static int PackHeldDirection(in FighterRuntimeComponent runtime, int facing, bool allowDown) {
            int directionX = runtime.MoveX > StickThreshold ? 1 : runtime.MoveX < -StickThreshold ? -1 : 0;
            int directionY = runtime.MoveY < -StickThreshold ? 1 : runtime.MoveY > StickThreshold ? -1 : 0;
            if (!allowDown && directionY < 0) directionY = 0;
            if (directionX == 0 && directionY == 0) {
                if (facing != 0) directionX = facing;
                else directionY = 1;
            }
            return (directionX + 1) + 3 * (directionY + 1);
        }

        /// <summary>A packed direction as a unit-length (diagonals normalized) world vector.</summary>
        public static FPVector2 UnitDirection(int packed) {
            int directionX = BlinkDirectionX(packed);
            int directionY = BlinkDirectionY(packed);
            FP64 scale = directionX != 0 && directionY != 0 ? DiagonalScale : FP64.One;
            return new FPVector2(FP64.FromInt(directionX) * scale, FP64.FromInt(directionY) * scale);
        }

        /// <summary>
        /// A08: pressing Attack during the Wing-Dive ends it so the same tick's
        /// basic-attack phase machine starts the aerial string. Called before
        /// <c>ProcessBasicAttackPhase</c>.
        /// </summary>
        public static void TryWingDiveAttackCancel(ref FighterRuntimeComponent runtime) {
            if (runtime.UniversalMovementState != WingDive) return;
            if ((runtime.PressedButtons & BasicButton) == 0) return;
            FighterUniversalMovementRules.Cancel(ref runtime);
        }

        /// <summary>LN02: the charge stops dead on contact — travel and armor both end.</summary>
        public static void StopRailCharge(ref FighterStateComponent fighter, ref FighterRuntimeComponent runtime) {
            FighterUniversalMovementRules.Cancel(ref runtime);
            fighter.Velocity.x = FP64.Zero;
            fighter.HyperArmorFrames = 0;
        }

        // --- per-tick advance -------------------------------------------------

        private static bool ProcessReach(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterAbilityModeComponent modes) {
            switch (runtime.UniversalMovementState) {
                case PiercingLunge: {
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        fighter.Velocity.x = FP64.Zero;
                        return false;
                    }
                    int total = PiercingTotalFrames(runtime.UniversalMovementDirection);
                    FP64 speed = LungeUnits * FP64.FromInt(FighterSimulation.TickRate) / FP64.FromInt(total > 0 ? total : 1);
                    fighter.Velocity.x = speed * FP64.FromInt(PiercingFacing(runtime.UniversalMovementDirection));
                    runtime.UniversalMovementFramesRemaining--;
                    return true;
                }

                case WingRise: {
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        // The rise is over. A held button turns it into the
                        // Wing-Dive; otherwise a normal fall from here.
                        int facing = WingFacing(runtime.UniversalMovementDirection);
                        if ((runtime.HeldButtons & MovementButton) != 0) {
                            runtime.UniversalMovementState = WingDive;
                            runtime.UniversalMovementFramesRemaining = modes.MovementDurationFrames > 0
                                ? modes.MovementDurationFrames
                                : 60;
                            runtime.UniversalMovementDirection = facing;
                            goto case WingDive;
                        }
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    int packed = runtime.UniversalMovementDirection;
                    int milli = packed >= 0 ? packed : -packed;
                    fighter.Velocity.y = FP64.FromInt(milli) / FP64.FromInt(1000);
                    fighter.IsGrounded = 0;
                    runtime.UniversalMovementFramesRemaining--;
                    return true;
                }

                case WingDive: {
                    if (runtime.UniversalMovementFramesRemaining <= 0
                        || fighter.IsGrounded != 0
                        || (runtime.HeldButtons & MovementButton) == 0) {
                        // Releasing the button (or the 1 s cap, or the ground)
                        // ends it into a normal fall.
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    int facing = runtime.UniversalMovementDirection >= 0 ? 1 : -1;
                    fighter.FacingRight = facing > 0 ? 1 : 0;
                    fighter.Velocity = new FPVector2(WingDiveForward * FP64.FromInt(facing), -WingDiveDescent);
                    runtime.UniversalMovementFramesRemaining--;
                    return true;
                }

                case SandRush:
                case Glissando: {
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        // Both end with their momentum spent (the Spirit carry's rule).
                        fighter.Velocity = FPVector2.Zero;
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    int frames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 15;
                    FP64 distance = modes.MovementDistance > FP64.Zero ? modes.MovementDistance : FP64.FromInt(3);
                    FP64 speed = distance * FP64.FromInt(FighterSimulation.TickRate) / FP64.FromInt(frames);
                    FPVector2 direction = UnitDirection(runtime.UniversalMovementDirection);
                    fighter.Velocity = new FPVector2(direction.x * speed, direction.y * speed);
                    if (direction.y > FP64.Zero) fighter.IsGrounded = 0;
                    runtime.UniversalMovementFramesRemaining--;
                    return true;
                }

                case RailCharge: {
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        fighter.Velocity.x = FP64.Zero;
                        return false;
                    }
                    int frames = modes.MovementDurationFrames > 0 ? modes.MovementDurationFrames : 30;
                    FP64 speed = modes.MovementDistance > FP64.Zero
                        ? modes.MovementDistance * FP64.FromInt(FighterSimulation.TickRate) / FP64.FromInt(frames)
                        : modes.MovementSpeed > FP64.Zero ? modes.MovementSpeed : FP64.FromInt(10);
                    fighter.Velocity.x = speed * FP64.FromInt(ChargeFacing(runtime.UniversalMovementDirection));
                    runtime.UniversalMovementFramesRemaining--;
                    return true;
                }

                default:
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    return false;
            }
        }
    }

    /// <summary>
    /// Package 13 W7b — the <b>ground wave</b> primitive (plan §7 W7b): a front
    /// that travels along a floor or platform surface. It is not a projectile for
    /// projectile-immunity purposes (Tesla's blink does not pass through it), it
    /// dissipates where its surface ends or at a wall, and a
    /// <see cref="HitsGroundedOnly"/> wave strikes only a fighter standing on
    /// that same surface — so a jump clears it.
    ///
    /// <para>It rides the existing projectile component (302), identified by its
    /// <c>ProjectileTypeID</c> (<c>CharacterID × 10 + slot</c>) — no new field,
    /// no new component. Its box is the resource's <c>HitboxSize</c>, bottom
    /// anchored on the surface; its speed and travel come from
    /// <c>ProjectileSpeed</c> × <c>ProjectileLifetime</c>.</para>
    /// </summary>
    public static class FighterGroundWave {
        private static readonly FP64 SurfaceTolerance = FP64.FromDouble(0.05);
        private static readonly FP64 SpawnForward = FP64.FromDouble(0.5);
        private static readonly FPVector2 FallbackHalfExtents = new(FP64.FromDouble(0.4), FP64.FromDouble(0.4));

        public static readonly int JoanRighteousSmite = (int)FighterCharacterID.Joan * 10 + 1;
        public static readonly int LincolnEmancipator = (int)FighterCharacterID.Lincoln * 10 + 1;
        public static readonly int MozartFortissimo = (int)FighterCharacterID.Mozart * 10 + 2;

        public static bool IsGroundWave(int projectileTypeID) =>
            projectileTypeID == JoanRighteousSmite
            || projectileTypeID == LincolnEmancipator
            || projectileTypeID == MozartFortissimo;

        public static bool IsGroundWave(int characterID, int slot) => IsGroundWave(characterID * 10 + slot);

        /// <summary>
        /// J02 and LN03 are grounded-only; M01's 1.5-unit wall of sound is a
        /// ground wave that strikes anything its height reaches.
        /// </summary>
        public static bool HitsGroundedOnly(int projectileTypeID) =>
            projectileTypeID == JoanRighteousSmite || projectileTypeID == LincolnEmancipator;

        /// <summary>The surface Y a wave rides: its box is bottom-anchored on it.</summary>
        public static FP64 SurfaceY(in FighterProjectileComponent wave) => wave.Position.y - wave.HalfExtents.y;

        /// <summary>
        /// The highest surface at or below <paramref name="y"/> under
        /// <paramref name="x"/>: an authored one-way platform, or the main floor
        /// (y = 0) where the stage has floor there. False over a pit.
        /// </summary>
        public static bool TryFindSurfaceBelow(FighterStageGeometry geometry, FP64 x, FP64 y, out FP64 surfaceY) {
            geometry ??= FighterStageGeometry.Default;
            bool found = false;
            surfaceY = FP64.Zero;
            FP64 ceiling = y + SurfaceTolerance;
            if (geometry.HasFloorSupport(x) && ceiling >= FP64.Zero) {
                surfaceY = FP64.Zero;
                found = true;
            }
            for (int index = 0; index < geometry.Platforms.Length; index++) {
                FighterStagePlatform platform = geometry.Platforms[index];
                if (!platform.Supports(x) || platform.SurfaceY > ceiling) continue;
                if (!found || platform.SurfaceY > surfaceY) {
                    surfaceY = platform.SurfaceY;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>True while the surface at <paramref name="surfaceY"/> still exists under <paramref name="x"/>.</summary>
        public static bool SurfaceContinues(FighterStageGeometry geometry, FP64 x, FP64 surfaceY) {
            geometry ??= FighterStageGeometry.Default;
            if (FP64.Abs(surfaceY) <= SurfaceTolerance) return geometry.HasFloorSupport(x);
            for (int index = 0; index < geometry.Platforms.Length; index++) {
                FighterStagePlatform platform = geometry.Platforms[index];
                if (FP64.Abs(platform.SurfaceY - surfaceY) <= SurfaceTolerance && platform.Supports(x)) return true;
            }
            return false;
        }

        /// <summary>True when the wave may strike <paramref name="target"/> this tick.</summary>
        public static bool CanStrike(in FighterProjectileComponent wave, in FighterStateComponent target) {
            if (!HitsGroundedOnly(wave.ProjectileTypeID)) return true;
            return target.IsGrounded != 0 && FP64.Abs(target.Position.y - SurfaceY(in wave)) <= SurfaceTolerance;
        }

        /// <summary>
        /// Spawns a wave on the surface under the caster. A cast over a pit
        /// finds no surface and makes no wave (the cooldown is still spent).
        /// </summary>
        public static void Spawn(
            ref Frame frame,
            FighterStageGeometry geometry,
            in FighterStateComponent owner,
            int slot,
            in FighterAbilityHitData contract,
            int damage,
            FP64 knockback,
            int statusType,
            int statusFrames,
            FP64 statusIntensity,
            int lifetimeFrames,
            FP64 speed) {
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FP64 originX = owner.Position.x + SpawnForward * FP64.FromInt(facing);
            if (!TryFindSurfaceBelow(geometry, originX, owner.Position.y, out FP64 surfaceY)) return;
            FPVector2 halfExtents = contract.HasHitbox
                ? new FPVector2(contract.HitboxHalfX, contract.HitboxHalfY)
                : FallbackHalfExtents;
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            EntityRef wave = frame.CreateEntity();
            frame.Add(wave, new FighterProjectileComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = owner.PlayerID,
                ProjectileTypeID = owner.CharacterID * 10 + slot,
                LifetimeFrames = lifetimeFrames > 0 ? lifetimeFrames : 30,
                Damage = damage,
                AttackClass = FighterDamageRules.SpecialAttackClass,
                StatusType = statusType,
                StatusFrames = statusFrames,
                HitstunFrames = contract.HitstunFrames > 0 ? contract.HitstunFrames : 18,
                StatusIntensity = statusIntensity,
                Position = new FPVector2(originX, surfaceY + halfExtents.y),
                Velocity = new FPVector2((speed > FP64.Zero ? speed : FP64.FromInt(10)) * FP64.FromInt(facing), FP64.Zero),
                HalfExtents = halfExtents,
                Knockback = new FPVector2(knockback, knockback)
            });
        }
    }

    /// <summary>
    /// Package 13 W7b — the per-kit hit rules the four kits need beyond the
    /// generic intent / projectile / zone paths: Divine Piercing's thrusts along
    /// its lunge (J01/J03), Rail Charge's stop-on-contact ram (LN02), Splitting
    /// Strike's overhead arc (LN03), the Requiem Chord's burst and its Fortissimo
    /// cooldown shave (M02/M03), Sandstorm Vortex placement (C01) and Mozart's
    /// staff platforms (M04). All of it reads the projected loadout contract.
    /// </summary>
    public static class FighterReachKitRules {
        private static readonly FPVector2 FighterHalfExtents = new(FP64.FromDouble(0.5), FP64.One);
        private static readonly FPVector2 FallbackThrustHalfExtents = new(FP64.FromDouble(0.5), FP64.FromDouble(0.42));
        private static readonly FP64 FallbackThrustOffset = FP64.FromDouble(0.5);
        private static readonly FP64 RailContactReach = FP64.FromDouble(KitReachRules.RailChargeContactReachUnits);
        private static readonly FPVector2 RailContactHalfExtents = new(FP64.FromDouble(0.5), FP64.FromDouble(0.6));
        private static readonly FP64 BurstRadius = FP64.FromDouble(KitReachRules.RequiemBurstRadiusUnits);
        private static readonly FP64 VortexThrow = FP64.FromDouble(KitReachRules.SandstormVortexMaxThrowUnits);
        private static readonly FP64 VortexRadius = FP64.FromDouble(KitReachRules.SandstormVortexRadiusUnits);
        private static readonly FP64 SplittingReach = FP64.FromDouble(KitReachRules.SplittingStrikeReachUnits);
        private static readonly FP64 StaffHalfWidth = FP64.FromDouble(KitReachRules.SonataPlatformWidthUnits / 2.0);
        private static readonly FP64 StaffHalfThickness = FP64.FromDouble(0.1);
        private static readonly FP64 StaffLandingTolerance = FP64.FromDouble(0.02);

        public static readonly int RequiemChordProjectile = (int)FighterCharacterID.Mozart * 10 + 1;
        public static readonly int RequiemBurstZone = (int)FighterCharacterID.Mozart * 10 + 1;
        public static readonly int SandstormVortexZone = (int)FighterCharacterID.Cleopatra * 10 + 2;
        public const int StaffPlatformTypeID = 5;
        /// <summary><c>FighterZoneComponent.ExecutionFlags</c>: this execution already shaved Fortissimo (M02).</summary>
        public const int FlagFortissimoShaved = 1;

        // --- J01/J03 Divine Piercing -------------------------------------------

        /// <summary>
        /// The thrust frames: the lunge's active frames split evenly across the
        /// authored hit count, the last thrust on the lunge's final frame.
        /// </summary>
        public static bool IsThrustFrame(int elapsedFrames, int totalFrames, int hitCount) {
            int thrusts = hitCount > 0 ? hitCount : 1;
            int interval = totalFrames / thrusts;
            if (interval < 1) interval = 1;
            return elapsedFrames > 0 && elapsedFrames % interval == 0 && elapsedFrames / interval <= thrusts;
        }

        /// <summary>The 1-based thrust a thrust frame fires.</summary>
        public static int ThrustIndex(int elapsedFrames, int totalFrames, int hitCount) {
            int thrusts = hitCount > 0 ? hitCount : 1;
            int interval = totalFrames / thrusts;
            if (interval < 1) interval = 1;
            return elapsedFrames / interval;
        }

        public static bool IsFinalThrust(int elapsedFrames, int totalFrames, int hitCount) {
            int thrusts = hitCount > 0 ? hitCount : 1;
            int interval = totalFrames / thrusts;
            if (interval < 1) interval = 1;
            return elapsedFrames / interval >= thrusts;
        }

        /// <summary>The thrust box in world space, ahead of the lunging attacker.</summary>
        public static void ThrustBox(
            in FighterStateComponent attacker, int facing, in FighterAbilityHitData contract,
            out FPVector2 center, out FPVector2 halfExtents) {
            FP64 offset = contract.HasHitbox ? contract.HitboxOffsetX : FallbackThrustOffset;
            halfExtents = contract.HasHitbox
                ? new FPVector2(contract.HitboxHalfX, contract.HitboxHalfY)
                : FallbackThrustHalfExtents;
            center = new FPVector2(attacker.Position.x + offset * FP64.FromInt(facing), attacker.Position.y);
        }

        // --- LN02 Rail Charge --------------------------------------------------

        public static void RailContactBox(in FighterStateComponent attacker, int facing, out FPVector2 center) =>
            center = new FPVector2(attacker.Position.x + RailContactReach * FP64.FromInt(facing), attacker.Position.y);

        public static FPVector2 RailContactExtents => RailContactHalfExtents;

        // --- LN03 Splitting Strike --------------------------------------------

        /// <summary>
        /// The 2.2-unit overhead arc: from Lincoln's front to 2.2 units ahead,
        /// from the floor to well above his head, so it catches grounded and
        /// airborne targets alike. Tested against the target's hurtbox box.
        /// </summary>
        public static bool SplittingArcReaches(in FighterStateComponent attacker, in FighterStateComponent target) {
            int facing = attacker.FacingRight != 0 ? 1 : -1;
            FP64 half = SplittingReach / FP64.FromInt(2);
            var center = new FPVector2(attacker.Position.x + half * FP64.FromInt(facing), attacker.Position.y + FP64.One);
            var halfExtents = new FPVector2(half, FP64.FromDouble(1.6));
            return FighterEntityQueries.Overlaps(in center, in halfExtents, in target.Position, in FighterHalfExtents);
        }

        /// <summary>
        /// LN03: Splitting Strike spikes an airborne target (the authored
        /// downward vector) and gives a grounded one only the horizontal half —
        /// grounded knockback with no launch (a spike into the floor moves no
        /// one). Story's <c>LincolnSplittingStrike</c> applies the same split.
        /// </summary>
        public static FP64 SplittingVertical(in FighterAbilityHitData contract, in FighterStateComponent target) =>
            target.IsGrounded != 0 ? FP64.Zero : -FP64.Abs(contract.KnockbackY);

        /// <summary>LN03: only the airborne spike launches (tumble, DI, tech).</summary>
        public static bool SplittingLaunches(in FighterAbilityHitData contract, in FighterStateComponent target) =>
            target.IsGrounded == 0 && contract.Launches;

        // --- M03 Requiem Chord burst -------------------------------------------

        /// <summary>
        /// Spawns the chord's burst: <c>HitCount</c> pulses of the per-hit
        /// damage in a 1.2-unit radius, one every six frames (the first six
        /// frames after the burst), riding a zone (309) of type 71.
        /// </summary>
        public static void SpawnRequiemBurst(
            ref Frame frame, int ownerPlayerID, in FPVector2 position, in FighterAbilityHitData contract, bool alreadyShaved) {
            int pulses = contract.HitCount > 0 ? contract.HitCount : 3;
            int interval = KitReachRules.RequiemPulseIntervalFrames;
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            EntityRef created = frame.CreateEntity();
            frame.Add(created, new FighterZoneComponent {
                EntityID = match.NextEntityID++,
                OwnerPlayerID = ownerPlayerID,
                ZoneTypeID = RequiemBurstZone,
                // Pulses land at +interval, +2·interval, …; the zone expires one
                // tick after the last.
                LifetimeFrames = interval * pulses + 1,
                TickIntervalFrames = interval,
                TickFramesRemaining = interval,
                Damage = contract.Damage > 0 ? contract.Damage : 4,
                StatusType = (int)FTT.Core.StatusType.None,
                StatusIntensity = FP64.One,
                Position = position,
                HalfExtents = new FPVector2(BurstRadius, BurstRadius),
                ExecutionFlags = alreadyShaved ? FlagFortissimoShaved : 0
            });
        }

        /// <summary>
        /// M02: a Requiem execution that HITS (not a block) shortens Fortissimo
        /// Wave's remaining cooldown by two seconds, once per execution.
        /// </summary>
        public static void ShaveFortissimo(ref FighterRuntimeComponent ownerRuntime) {
            int remaining = ownerRuntime.SpecialTwoCooldownFrames - KitReachRules.RequiemFortissimoShaveFrames;
            ownerRuntime.SpecialTwoCooldownFrames = remaining > 0 ? remaining : 0;
        }

        // --- C01 Sandstorm Vortex ----------------------------------------------

        /// <summary>
        /// The vortex is thrown up to five units ahead (held inside the walls)
        /// and snapped to the surface under that point; over a pit it stays at
        /// the caster's height.
        /// </summary>
        public static FPVector2 VortexAnchor(FighterStageGeometry geometry, in FighterStateComponent owner) {
            geometry ??= FighterStageGeometry.Default;
            int facing = owner.FacingRight != 0 ? 1 : -1;
            FP64 x = owner.Position.x + VortexThrow * FP64.FromInt(facing);
            FP64 minX = geometry.LeftWall + VortexRadius;
            FP64 maxX = geometry.RightWall - VortexRadius;
            if (x < minX) x = minX;
            if (x > maxX) x = maxX;
            FP64 y = FighterGroundWave.TryFindSurfaceBelow(geometry, x, owner.Position.y, out FP64 surfaceY)
                ? surfaceY
                : owner.Position.y;
            return new FPVector2(x, y);
        }

        public static FPVector2 VortexHalfExtents => new(VortexRadius, VortexRadius);

        // --- M04 Sonata Drift staff platforms ---------------------------------

        /// <summary>Where the glissando ends: the staff is placed under Mozart's feet there.</summary>
        public static FPVector2 GlissandoEnd(
            FighterStageGeometry geometry, in FighterStateComponent owner, int packedDirection, FP64 distance) {
            geometry ??= FighterStageGeometry.Default;
            FPVector2 direction = FighterKitMotion.UnitDirection(packedDirection);
            FP64 x = owner.Position.x + direction.x * distance;
            FP64 y = owner.Position.y + direction.y * distance;
            if (x < geometry.LeftWall) x = geometry.LeftWall;
            if (x > geometry.RightWall) x = geometry.RightWall;
            if (y > geometry.Ceiling) y = geometry.Ceiling;
            return new FPVector2(x, y);
        }

        public static FPVector2 StaffHalfExtents => new(StaffHalfWidth, StaffHalfThickness);

        /// <summary>True while <paramref name="fighter"/> stands on a live staff platform.</summary>
        public static bool StandsOnStaff(ref Frame frame, in FighterStateComponent fighter) {
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterPersistentObjectComponent staff =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                if (staff.ObjectTypeID != StaffPlatformTypeID || staff.LifetimeFrames <= 0) continue;
                if (fighter.Position.y == staff.Position.y
                    && FP64.Abs(fighter.Position.x - staff.Position.x) <= staff.HalfExtents.x) return true;
            }
            return false;
        }

        /// <summary>
        /// One-way landing on a staff: only when this tick's fall crossed its
        /// surface from above. Returns the staff owner's player ID, or -1.
        /// Any fighter may stand on a staff (Story's is a one-way platform too);
        /// only its owner earns the refund.
        /// </summary>
        public static int TryLandOnStaff(
            ref Frame frame, ref FighterStateComponent fighter, in FighterTuningComponent tuning, FP64 previousY) {
            if (fighter.Velocity.y > FP64.Zero) return -1;
            var filter = frame.Filter<FighterPersistentObjectComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterPersistentObjectComponent staff =
                    ref frame.GetReadOnly<FighterPersistentObjectComponent>(entity);
                if (staff.ObjectTypeID != StaffPlatformTypeID || staff.LifetimeFrames <= 0) continue;
                // The glissando ends a hair under its staff (fixed-point travel
                // of 3 units over 18 ticks), so the crossing test forgives a
                // sub-hundredth start below the surface.
                if (previousY < staff.Position.y - StaffLandingTolerance
                    || fighter.Position.y > staff.Position.y
                    || FP64.Abs(fighter.Position.x - staff.Position.x) > staff.HalfExtents.x) continue;
                fighter.Position.y = staff.Position.y;
                if (fighter.Velocity.y < FP64.Zero) fighter.Velocity.y = FP64.Zero;
                fighter.IsGrounded = 1;
                fighter.RemainingJumps = tuning.MaxJumpCount;
                return staff.OwnerPlayerID;
            }
            return -1;
        }

        /// <summary>
        /// M04: landing on one's OWN staff refunds half of the movement
        /// ability's remaining cooldown, at most once per airtime.
        /// </summary>
        public static void ApplyStaffRefund(ref FighterRuntimeComponent runtime, ref FighterKnockdownComponent knockdown) {
            if (knockdown.StaffRefundUsed != 0) return;
            knockdown.StaffRefundUsed = 1;
            if (runtime.MovementCooldownFrames > 0) runtime.MovementCooldownFrames /= 2;
        }
    }
}
