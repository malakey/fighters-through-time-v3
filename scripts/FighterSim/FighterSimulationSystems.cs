using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    public sealed class FighterWorldSystem : IInitSystem, ISystem {
        private readonly FighterLoadout _playerOne;
        private readonly FighterLoadout _playerTwo;
        private readonly int _stockCount;
        private readonly int _matchFrames;
        private readonly int _seed;
        private readonly int _spawnDistance;
        private readonly FighterMatchRules _rules;

        public FighterWorldSystem(
            FighterLoadout playerOne,
            FighterLoadout playerTwo,
            int stockCount,
            int matchFrames,
            int seed,
            int spawnDistance,
            FighterMatchRules rules) {
            _playerOne = playerOne;
            _playerTwo = playerTwo;
            _stockCount = stockCount;
            _matchFrames = matchFrames;
            _seed = seed;
            _spawnDistance = spawnDistance;
            _rules = rules;
        }

        public void OnInit(ref Frame frame) {
            var random = new DeterministicRandom(_seed);
            (ulong state0, ulong state1) = random.GetFullState();

            EntityRef matchEntity = frame.CreateEntity();
            frame.Add(matchEntity, new FighterMatchComponent {
                RemainingFrames = _matchFrames,
                MatchState = 1,
                MatchMode = _rules.MatchMode,
                WinnerPlayerID = -1,
                WorldSeed = _seed,
                NextEntityID = 2000,
                ItemsEnabled = _rules.ItemsEnabled ? 1 : 0,
                ItemFrequency = _rules.ItemFrequency,
                HazardsEnabled = _rules.HazardsEnabled ? 1 : 0,
                HazardFrequency = _rules.HazardFrequency,
                StageHazardTypeID = _rules.StageHazardTypeID,
                NextOrbSpawnFrames = FighterSpawnIntervals.OrbFrames(_rules.ItemFrequency),
                NextHazardSpawnFrames = FighterSpawnIntervals.HazardFrames(_rules.HazardFrequency),
                RandomState0 = state0,
                RandomState1 = state1
            });

            SpawnFighter(ref frame, 0, in _playerOne, -_spawnDistance);
            SpawnFighter(ref frame, 1, in _playerTwo, _spawnDistance);
        }

        public void Update(ref Frame frame) { }

        private void SpawnFighter(ref Frame frame, int playerID, in FighterLoadout loadout, int spawnX) {
            EntityRef entity = frame.CreateEntity();
            FPVector2 spawn = new(FP64.FromInt(spawnX), FP64.Zero);
            frame.Add(entity, new FighterStateComponent {
                EntityID = 1000 + playerID,
                PlayerID = playerID,
                CharacterID = loadout.CharacterID,
                CurrentHP = loadout.MaxHP,
                MaxHP = loadout.MaxHP,
                Stocks = _stockCount,
                BlockCharges = loadout.MaxBlockCharges,
                FacingRight = playerID == 0 ? 1 : 0,
                IsGrounded = 1,
                RemainingJumps = loadout.MaxJumpCount,
                Weight = loadout.Weight,
                Influence = FP64.Zero,
                Position = spawn,
                Velocity = FPVector2.Zero,
                SpawnPosition = spawn
            });
            frame.Add(entity, new FighterRuntimeComponent {
                StatusIntensity = FP64.One,
                UsesStocks = _rules.MatchMode == (int)FTT.Core.MatchMode.TimeLimit ? 0 : 1
            });
            frame.Add(entity, new FighterTuningComponent {
                BasicDamage = loadout.BasicDamage,
                SpecialOneDamage = loadout.SpecialOneDamage,
                SpecialTwoDamage = loadout.SpecialTwoDamage,
                UltimateDamage = loadout.UltimateDamage,
                MaxBlockCharges = loadout.MaxBlockCharges,
                MaxJumpCount = loadout.MaxJumpCount,
                SpecialOneCooldownFrames = loadout.SpecialOneCooldownFrames,
                SpecialTwoCooldownFrames = loadout.SpecialTwoCooldownFrames,
                SpecialOneStatusType = loadout.SpecialOneStatusType,
                SpecialOneStatusFrames = loadout.SpecialOneStatusFrames,
                SpecialTwoStatusType = loadout.SpecialTwoStatusType,
                SpecialTwoStatusFrames = loadout.SpecialTwoStatusFrames,
                UltimateStatusType = loadout.UltimateStatusType,
                UltimateStatusFrames = loadout.UltimateStatusFrames,
                MoveSpeed = loadout.MoveSpeed,
                JumpSpeed = loadout.JumpSpeed,
                BasicKnockback = loadout.BasicKnockback,
                SpecialOneKnockback = loadout.SpecialOneKnockback,
                SpecialTwoKnockback = loadout.SpecialTwoKnockback,
                UltimateKnockback = loadout.UltimateKnockback,
                SpecialOneStatusIntensity = loadout.SpecialOneStatusIntensity,
                SpecialTwoStatusIntensity = loadout.SpecialTwoStatusIntensity,
                UltimateStatusIntensity = loadout.UltimateStatusIntensity
            });
            FighterAbilityLoadout modes = loadout.AbilityModes;
            frame.Add(entity, new FighterAbilityModeComponent {
                SpecialOneExecutionType = modes.SpecialOneExecutionType,
                SpecialTwoExecutionType = modes.SpecialTwoExecutionType,
                SpecialOneProjectileLifetimeFrames = modes.SpecialOneProjectileLifetimeFrames,
                SpecialTwoProjectileLifetimeFrames = modes.SpecialTwoProjectileLifetimeFrames,
                SpecialOnePersistentTypeID = modes.SpecialOnePersistentTypeID,
                SpecialOneMaxActiveObjects = modes.SpecialOneMaxActiveObjects,
                SpecialOnePersistentLifetimeFrames = modes.SpecialOnePersistentLifetimeFrames,
                SpecialTwoPersistentTypeID = modes.SpecialTwoPersistentTypeID,
                SpecialTwoMaxActiveObjects = modes.SpecialTwoMaxActiveObjects,
                SpecialTwoPersistentLifetimeFrames = modes.SpecialTwoPersistentLifetimeFrames,
                MovementType = modes.MovementType,
                MovementCooldownFrames = modes.MovementCooldownFrames,
                MovementDurationFrames = modes.MovementDurationFrames,
                MovementResetsJump = modes.MovementResetsJump,
                MovementGrantsHyperArmor = modes.MovementGrantsHyperArmor,
                MovementPersistentTypeID = modes.MovementPersistentTypeID,
                MovementMaxActiveObjects = modes.MovementMaxActiveObjects,
                MovementPersistentLifetimeFrames = modes.MovementPersistentLifetimeFrames,
                SpecialOneProjectileSpeed = modes.SpecialOneProjectileSpeed,
                SpecialTwoProjectileSpeed = modes.SpecialTwoProjectileSpeed,
                MovementDistance = modes.MovementDistance,
                MovementSpeed = modes.MovementSpeed
            });
        }
    }

    public sealed class FighterInputSystem : ISystem, ICommandSystem {
        public void OnCommand(ref Frame frame, ICommand command) {
            if (command is not FighterInputCommand input) return;

            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                if (fighter.PlayerID != input.PlayerId) continue;
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                runtime.MoveX = input.MoveX;
                runtime.MoveY = input.MoveY;
                runtime.HeldButtons = input.HeldButtons;
                runtime.PressedButtons = input.PressedButtons;
                runtime.ReleasedButtons = input.ReleasedButtons;
                return;
            }
        }

        public void Update(ref Frame frame) { }
    }

    public sealed class FighterMovementSystem : ISystem {
        private const int JumpButton = 1 << 0;
        private const int DownButton = 1 << 1;
        private const int RollButton = 1 << 10;
        private const int DashButton = 1 << 11;
        private static readonly FP64 FixedDelta = FP64.One / FP64.FromInt(60);
        private static readonly FP64 Gravity = FP64.FromInt(-30);
        private static readonly FP64 DashSpeedMultiplier = FP64.FromDouble(UniversalMovementRules.DashSpeedMultiplier);
        private static readonly FP64 RollSpeedMultiplier = FP64.FromDouble(UniversalMovementRules.RollSpeedMultiplier);
        private static readonly FP64 LeftWall = FP64.FromInt(-10);
        private static readonly FP64 RightWall = FP64.FromInt(10);
        private static readonly FP64 Ceiling = FP64.FromInt(9);
        private static readonly FP64 BottomBlastZone = FP64.FromInt(-5);

        public void Update(ref Frame frame) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref FighterStateComponent fighter = ref frame.Get<FighterStateComponent>(entity);
                ref FighterRuntimeComponent runtime = ref frame.Get<FighterRuntimeComponent>(entity);
                ref readonly FighterTuningComponent tuning = ref frame.GetReadOnly<FighterTuningComponent>(entity);
                TickCounters(ref fighter, ref runtime, in tuning);

                if (fighter.InvulnerabilityFrames > 0) fighter.InvulnerabilityFrames--;
                if (fighter.HitstunFrames > 0 || fighter.DazeFrames > 0) {
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    fighter.Velocity.y += Gravity * FixedDelta;
                } else {
                    bool rooted = runtime.StatusType == (int)FTT.Core.StatusType.Root;
                    FP64 statusMoveMultiplier = runtime.StatusType == (int)FTT.Core.StatusType.TimeDilation
                        ? FP64.Clamp(
                            FP64.One - runtime.StatusIntensity / FP64.FromInt(2),
                            FP64.FromDouble(0.1),
                            FP64.One)
                        : FP64.One;
                    FP64 speedBuffMultiplier = runtime.SpeedBuffFrames > 0
                        ? FP64.FromDouble(1.4)
                        : FP64.One;
                    FP64 jumpBuffMultiplier = runtime.JumpBuffFrames > 0
                        ? FP64.FromDouble(1.3)
                        : FP64.One;
                    if (rooted) FighterUniversalMovementRules.Cancel(ref runtime);
                    TryStartUniversalMovement(ref fighter, ref runtime, rooted);
                    bool movementHandled = ProcessUniversalMovement(
                        ref fighter,
                        ref runtime,
                        in tuning,
                        statusMoveMultiplier * speedBuffMultiplier);
                    if (!movementHandled) {
                        ApplyNormalMovement(
                            ref fighter,
                            ref runtime,
                            in tuning,
                            rooted,
                            statusMoveMultiplier,
                            speedBuffMultiplier,
                            jumpBuffMultiplier);
                    }
                    if (fighter.IsGrounded == 0) fighter.Velocity.y += Gravity * FixedDelta;
                }

                fighter.Position += fighter.Velocity * FixedDelta;
                fighter.Position.x = FP64.Clamp(fighter.Position.x, LeftWall, RightWall);
                if (fighter.Position.y > Ceiling) {
                    fighter.Position.y = Ceiling;
                    if (fighter.Velocity.y > FP64.Zero) fighter.Velocity.y = FP64.Zero;
                }

                if (fighter.DropThroughFrames <= 0 && fighter.Position.y <= FP64.Zero) {
                    fighter.Position.y = FP64.Zero;
                    if (fighter.Velocity.y < FP64.Zero) fighter.Velocity.y = FP64.Zero;
                    fighter.IsGrounded = 1;
                    fighter.RemainingJumps = tuning.MaxJumpCount;
                }

                if (fighter.Position.y < BottomBlastZone) {
                    FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, in tuning);
                }
            }
        }

        private static void TryStartUniversalMovement(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            bool rooted) {
            if (rooted
                || fighter.IsGrounded == 0
                || runtime.UniversalMovementState != (int)UniversalMovementPhase.None) return;
            int direction = runtime.MoveX > 0 ? 1 : runtime.MoveX < 0 ? -1 : fighter.FacingRight != 0 ? 1 : -1;
            if ((runtime.PressedButtons & RollButton) != 0) {
                runtime.UniversalMovementState = (int)UniversalMovementPhase.RollStartup;
                runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollStartupFrames;
                runtime.UniversalMovementDirection = direction;
            } else if ((runtime.PressedButtons & DashButton) != 0) {
                runtime.UniversalMovementState = (int)UniversalMovementPhase.Dash;
                runtime.UniversalMovementFramesRemaining = UniversalMovementRules.DashDurationFrames;
                runtime.UniversalMovementDirection = direction;
                fighter.FacingRight = direction > 0 ? 1 : 0;
            }
        }

        private static bool ProcessUniversalMovement(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            FP64 speedMultiplier) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            if (phase == UniversalMovementPhase.None) return false;
            FP64 direction = FP64.FromInt(runtime.UniversalMovementDirection == 0 ? 1 : runtime.UniversalMovementDirection);
            FP64 runStep = tuning.MoveSpeed / FP64.FromInt(UniversalMovementRules.RunAccelerationFrames);

            switch (phase) {
                case UniversalMovementPhase.Dash:
                    int elapsed = UniversalMovementRules.DashDurationFrames - runtime.UniversalMovementFramesRemaining;
                    bool canJumpCancel = elapsed >= UniversalMovementRules.DashCommitFrames
                        && (runtime.PressedButtons & JumpButton) != 0;
                    if (canJumpCancel) {
                        FighterUniversalMovementRules.Cancel(ref runtime);
                        return false;
                    }
                    fighter.Velocity.x = direction * tuning.MoveSpeed * speedMultiplier * DashSpeedMultiplier;
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) FighterUniversalMovementRules.Cancel(ref runtime);
                    return true;

                case UniversalMovementPhase.RollStartup:
                    fighter.Velocity.x = MoveToward(fighter.Velocity.x, FP64.Zero, runStep);
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.RollTravel;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollTravelFrames;
                    }
                    return true;

                case UniversalMovementPhase.RollTravel:
                    if (runtime.UniversalMovementFramesRemaining == UniversalMovementRules.RollTravelFrames) {
                        if (fighter.InvulnerabilityFrames < UniversalMovementRules.RollInvulnerabilityFrames) {
                            fighter.InvulnerabilityFrames = UniversalMovementRules.RollInvulnerabilityFrames;
                        }
                    }
                    fighter.Velocity.x = direction * tuning.MoveSpeed * speedMultiplier * RollSpeedMultiplier;
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) {
                        runtime.UniversalMovementState = (int)UniversalMovementPhase.RollRecovery;
                        runtime.UniversalMovementFramesRemaining = UniversalMovementRules.RollRecoveryFrames;
                        fighter.Velocity.x = FP64.Zero;
                    }
                    return true;

                case UniversalMovementPhase.RollRecovery:
                    fighter.Velocity.x = MoveToward(fighter.Velocity.x, FP64.Zero, runStep);
                    runtime.UniversalMovementFramesRemaining--;
                    if (runtime.UniversalMovementFramesRemaining <= 0) FighterUniversalMovementRules.Cancel(ref runtime);
                    return true;

                default:
                    FighterUniversalMovementRules.Cancel(ref runtime);
                    return false;
            }
        }

        private static void ApplyNormalMovement(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning,
            bool rooted,
            FP64 statusMoveMultiplier,
            FP64 speedBuffMultiplier,
            FP64 jumpBuffMultiplier) {
            FP64 input = rooted ? FP64.Zero : FP64.FromInt(runtime.MoveX) / FP64.FromInt(127);
            FP64 maximumSpeed = tuning.MoveSpeed * statusMoveMultiplier * speedBuffMultiplier;
            FP64 targetSpeed = input * maximumSpeed;
            int accelerationFrames = fighter.IsGrounded != 0
                ? UniversalMovementRules.RunAccelerationFrames
                : 4;
            fighter.Velocity.x = MoveToward(
                fighter.Velocity.x,
                targetSpeed,
                maximumSpeed / FP64.FromInt(accelerationFrames));
            if (runtime.MoveX > 0) fighter.FacingRight = 1;
            else if (runtime.MoveX < 0) fighter.FacingRight = 0;

            bool jumpPressed = (runtime.PressedButtons & JumpButton) != 0;
            bool downHeld = (runtime.HeldButtons & DownButton) != 0;
            if (jumpPressed && downHeld && fighter.IsGrounded != 0 && !rooted) {
                fighter.DropThroughFrames = 30;
                fighter.IsGrounded = 0;
                fighter.Velocity.y = FP64.FromInt(-2);
            } else if (jumpPressed && fighter.IsGrounded != 0 && !rooted) {
                fighter.Velocity.y = tuning.JumpSpeed * statusMoveMultiplier * jumpBuffMultiplier;
                fighter.IsGrounded = 0;
                fighter.RemainingJumps = tuning.MaxJumpCount - 1;
            } else if (jumpPressed && fighter.IsGrounded == 0 && fighter.RemainingJumps > 0 && !rooted) {
                fighter.Velocity.y = tuning.JumpSpeed * statusMoveMultiplier * jumpBuffMultiplier;
                fighter.RemainingJumps--;
            }
        }

        private static FP64 MoveToward(FP64 current, FP64 target, FP64 maximumDelta) {
            if (current < target) return FP64.Min(current + maximumDelta, target);
            if (current > target) return FP64.Max(current - maximumDelta, target);
            return target;
        }

        private static void TickCounters(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning) {
            if (fighter.HitstunFrames > 0) fighter.HitstunFrames--;
            if (fighter.DazeFrames > 0) fighter.DazeFrames--;
            if (fighter.HyperArmorFrames > 0) fighter.HyperArmorFrames--;
            if (fighter.DropThroughFrames > 0) fighter.DropThroughFrames--;
            if (runtime.BasicCooldownFrames > 0) runtime.BasicCooldownFrames--;
            if (runtime.SpecialOneCooldownFrames > 0) runtime.SpecialOneCooldownFrames--;
            if (runtime.SpecialTwoCooldownFrames > 0) runtime.SpecialTwoCooldownFrames--;
            if (runtime.MovementCooldownFrames > 0) runtime.MovementCooldownFrames--;
            if (runtime.SpeedBuffFrames > 0) runtime.SpeedBuffFrames--;
            if (runtime.JumpBuffFrames > 0) runtime.JumpBuffFrames--;
            if (runtime.StatusFrames > 0) {
                runtime.StatusFrames--;
                if (runtime.StatusTickFrames > 0) runtime.StatusTickFrames--;
                if (runtime.StatusType == (int)FTT.Core.StatusType.Venom && runtime.StatusTickFrames <= 0) {
                    int scaledDamage = ScaleIntegerByFP(2, runtime.StatusIntensity);
                    int damage = scaledDamage > 1 ? scaledDamage : 1;
                    int remainingHP = fighter.CurrentHP - damage;
                    fighter.CurrentHP = remainingHP > 0 ? remainingHP : 0;
                    runtime.StatusTickFrames = 60;
                    if (fighter.CurrentHP <= 0) {
                        FighterSimulationRules.ApplyStockLoss(ref fighter, ref runtime, in tuning);
                        return;
                    }
                }
                if (runtime.StatusFrames == 0) {
                    runtime.StatusType = (int)FTT.Core.StatusType.None;
                    runtime.StatusIntensity = FP64.One;
                    runtime.StatusTickFrames = 0;
                }
            }
        }

        private static int ScaleIntegerByFP(int value, FP64 scale) {
            long numerator = (long)value * scale.RawValue + FP64.One.RawValue / 2;
            return (int)(numerator / FP64.One.RawValue);
        }
    }

    internal static class FighterUniversalMovementRules {
        public static bool IsCombatLocked(in FighterRuntimeComponent runtime) {
            UniversalMovementPhase phase = (UniversalMovementPhase)runtime.UniversalMovementState;
            if (phase is UniversalMovementPhase.RollStartup
                or UniversalMovementPhase.RollTravel
                or UniversalMovementPhase.RollRecovery) return true;
            return phase == UniversalMovementPhase.Dash
                && runtime.UniversalMovementFramesRemaining
                    >= UniversalMovementRules.DashDurationFrames - UniversalMovementRules.DashCommitFrames;
        }

        public static void Cancel(ref FighterRuntimeComponent runtime) {
            runtime.UniversalMovementState = (int)UniversalMovementPhase.None;
            runtime.UniversalMovementFramesRemaining = 0;
            runtime.UniversalMovementDirection = 0;
        }
    }

    /// <summary>
    /// Deterministic horizontal jostling. Roll travel explicitly bypasses the
    /// fighter pushbox; dashes keep it and terminate when body-blocked.
    /// </summary>
    public sealed class FighterPushboxSystem : ISystem {
        public static readonly FP64 MinimumHorizontalDistance = FP64.FromDouble(0.8);
        private static readonly FP64 MaximumVerticalDistance = FP64.FromDouble(1.6);
        private static readonly FP64 LeftWall = FP64.FromInt(-10);
        private static readonly FP64 RightWall = FP64.FromInt(10);

        public void Update(ref Frame frame) {
            EntityRef firstEntity = default;
            EntityRef secondEntity = default;
            bool foundFirst = false;
            bool foundSecond = false;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == 0) { firstEntity = entity; foundFirst = true; }
                else if (fighter.PlayerID == 1) { secondEntity = entity; foundSecond = true; }
            }
            if (!foundFirst || !foundSecond) return;

            ref FighterStateComponent first = ref frame.Get<FighterStateComponent>(firstEntity);
            ref FighterStateComponent second = ref frame.Get<FighterStateComponent>(secondEntity);
            ref FighterRuntimeComponent firstRuntime = ref frame.Get<FighterRuntimeComponent>(firstEntity);
            ref FighterRuntimeComponent secondRuntime = ref frame.Get<FighterRuntimeComponent>(secondEntity);
            if (first.Stocks <= 0 || second.Stocks <= 0) return;
            if (IsRollTravel(in firstRuntime) || IsRollTravel(in secondRuntime)) return;
            if (FP64.Abs(first.Position.y - second.Position.y) >= MaximumVerticalDistance) return;

            FP64 delta = second.Position.x - first.Position.x;
            FP64 distance = FP64.Abs(delta);
            if (distance >= MinimumHorizontalDistance) return;
            FP64 overlap = MinimumHorizontalDistance - distance;

            bool firstIsLeft = delta > FP64.Zero || (delta == FP64.Zero && first.PlayerID < second.PlayerID);
            ref FighterStateComponent left = ref (firstIsLeft ? ref first : ref second);
            ref FighterStateComponent right = ref (firstIsLeft ? ref second : ref first);
            ref FighterRuntimeComponent leftRuntime = ref (firstIsLeft ? ref firstRuntime : ref secondRuntime);
            ref FighterRuntimeComponent rightRuntime = ref (firstIsLeft ? ref secondRuntime : ref firstRuntime);

            FP64 half = overlap / FP64.FromInt(2);
            FP64 leftSpace = left.Position.x - LeftWall;
            FP64 rightSpace = RightWall - right.Position.x;
            FP64 leftMove = FP64.Min(half, FP64.Max(FP64.Zero, leftSpace));
            FP64 rightMove = FP64.Min(half, FP64.Max(FP64.Zero, rightSpace));
            FP64 remaining = overlap - leftMove - rightMove;
            if (remaining > FP64.Zero) {
                FP64 extraRight = FP64.Min(remaining, FP64.Max(FP64.Zero, rightSpace - rightMove));
                rightMove += extraRight;
                remaining -= extraRight;
            }
            if (remaining > FP64.Zero) {
                leftMove += FP64.Min(remaining, FP64.Max(FP64.Zero, leftSpace - leftMove));
            }

            left.Position.x -= leftMove;
            right.Position.x += rightMove;
            if (left.Velocity.x > FP64.Zero) left.Velocity.x = FP64.Zero;
            if (right.Velocity.x < FP64.Zero) right.Velocity.x = FP64.Zero;
            StopBlockedDash(ref left, ref leftRuntime);
            StopBlockedDash(ref right, ref rightRuntime);
        }

        private static bool IsRollTravel(in FighterRuntimeComponent runtime) =>
            runtime.UniversalMovementState == (int)UniversalMovementPhase.RollTravel;

        private static void StopBlockedDash(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime) {
            if (runtime.UniversalMovementState != (int)UniversalMovementPhase.Dash) return;
            FighterUniversalMovementRules.Cancel(ref runtime);
            fighter.Velocity.x = FP64.Zero;
        }
    }

    public sealed class FighterCombatSystem : ISystem {
        private const int BasicButton = 1 << 2;
        private const int SpecialOneButton = 1 << 3;
        private const int SpecialTwoButton = 1 << 4;
        private const int UltimateButton = 1 << 7;
        private static readonly FP64 AttackRange = FP64.FromInt(2);
        private static readonly FP64 MaxInfluence = FP64.FromInt(100);

        public void Update(ref Frame frame) {
            EntityRef first = default;
            EntityRef second = default;
            bool foundFirst = false;
            bool foundSecond = false;
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent, FighterTuningComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == 0) { first = entity; foundFirst = true; }
                else if (fighter.PlayerID == 1) { second = entity; foundSecond = true; }
            }
            if (!foundFirst || !foundSecond) return;

            ref FighterStateComponent fighterOne = ref frame.Get<FighterStateComponent>(first);
            ref FighterStateComponent fighterTwo = ref frame.Get<FighterStateComponent>(second);
            ref FighterRuntimeComponent runtimeOne = ref frame.Get<FighterRuntimeComponent>(first);
            ref FighterRuntimeComponent runtimeTwo = ref frame.Get<FighterRuntimeComponent>(second);
            ref readonly FighterTuningComponent tuningOne = ref frame.GetReadOnly<FighterTuningComponent>(first);
            ref readonly FighterTuningComponent tuningTwo = ref frame.GetReadOnly<FighterTuningComponent>(second);
            AttackIntent firstIntent = BuildIntent(in fighterOne, in runtimeOne, in tuningOne, in fighterTwo);
            AttackIntent secondIntent = BuildIntent(in fighterTwo, in runtimeTwo, in tuningTwo, in fighterOne);
            ApplyIntent(ref fighterOne, ref runtimeOne, ref fighterTwo, ref runtimeTwo, in tuningTwo, in firstIntent);
            ApplyIntent(ref fighterTwo, ref runtimeTwo, ref fighterOne, ref runtimeOne, in tuningOne, in secondIntent);
        }

        private static AttackIntent BuildIntent(
            in FighterStateComponent attacker,
            in FighterRuntimeComponent attackerRuntime,
            in FighterTuningComponent tuning,
            in FighterStateComponent target) {
            if (attacker.HitstunFrames > 0
                || attacker.DazeFrames > 0
                || attacker.Stocks <= 0
                || FighterUniversalMovementRules.IsCombatLocked(in attackerRuntime)) return default;
            if (FP64.Abs(target.Position.x - attacker.Position.x) > AttackRange) return default;

            if ((attackerRuntime.PressedButtons & UltimateButton) != 0 && attacker.Influence >= MaxInfluence) {
                return new AttackIntent(
                    3,
                    tuning.UltimateDamage,
                    tuning.UltimateKnockback,
                    30,
                    tuning.UltimateStatusType,
                    tuning.UltimateStatusFrames,
                    tuning.UltimateStatusIntensity);
            }
            if ((attackerRuntime.PressedButtons & SpecialOneButton) != 0 && attackerRuntime.SpecialOneCooldownFrames <= 0) {
                return new AttackIntent(
                    2,
                    tuning.SpecialOneDamage,
                    tuning.SpecialOneKnockback,
                    18,
                    tuning.SpecialOneStatusType,
                    tuning.SpecialOneStatusFrames,
                    tuning.SpecialOneStatusIntensity,
                    tuning.SpecialOneCooldownFrames);
            }
            if ((attackerRuntime.PressedButtons & SpecialTwoButton) != 0 && attackerRuntime.SpecialTwoCooldownFrames <= 0) {
                return new AttackIntent(
                    4,
                    tuning.SpecialTwoDamage,
                    tuning.SpecialTwoKnockback,
                    18,
                    tuning.SpecialTwoStatusType,
                    tuning.SpecialTwoStatusFrames,
                    tuning.SpecialTwoStatusIntensity,
                    tuning.SpecialTwoCooldownFrames);
            }
            if ((attackerRuntime.PressedButtons & BasicButton) != 0 && attackerRuntime.BasicCooldownFrames <= 0) {
                int damage = attackerRuntime.ComboIndex == 0
                    ? tuning.BasicDamage * 8 / 10
                    : attackerRuntime.ComboIndex == 1
                        ? tuning.BasicDamage
                        : tuning.BasicDamage * 15 / 10;
                FP64 knockback = attackerRuntime.ComboIndex == 2
                    ? tuning.BasicKnockback * FP64.FromInt(2)
                    : tuning.BasicKnockback;
                return new AttackIntent(1, damage, knockback, attackerRuntime.ComboIndex == 2 ? 18 : 10);
            }
            return default;
        }

        private static void ApplyIntent(
            ref FighterStateComponent attacker,
            ref FighterRuntimeComponent attackerRuntime,
            ref FighterStateComponent target,
            ref FighterRuntimeComponent targetRuntime,
            in FighterTuningComponent targetTuning,
            in AttackIntent intent) {
            if (intent.Kind == 0) return;

            FighterUniversalMovementRules.Cancel(ref attackerRuntime);

            if (intent.Kind == 1) {
                attackerRuntime.BasicCooldownFrames = 18;
                attackerRuntime.ComboIndex = (attackerRuntime.ComboIndex + 1) % 3;
            } else if (intent.Kind == 2) {
                attackerRuntime.SpecialOneCooldownFrames = intent.CooldownFrames > 1 ? intent.CooldownFrames : 1;
            } else if (intent.Kind == 4) {
                attackerRuntime.SpecialTwoCooldownFrames = intent.CooldownFrames > 1 ? intent.CooldownFrames : 1;
            } else {
                attacker.Influence = FP64.Zero;
            }

            int attackClass = intent.Kind == 1
                ? FighterDamageRules.BasicAttackClass
                : intent.Kind == 3
                    ? FighterDamageRules.UltimateAttackClass
                    : FighterDamageRules.SpecialAttackClass;
            FighterDamageRules.ApplyFighterHit(
                ref attacker,
                ref attackerRuntime,
                ref target,
                ref targetRuntime,
                in targetTuning,
                attackClass,
                intent.Damage,
                intent.Knockback,
                intent.HitstunFrames,
                intent.StatusType,
                intent.StatusFrames,
                intent.StatusIntensity,
                attacker.Position.x);
        }

        private readonly struct AttackIntent {
            public readonly int Kind;
            public readonly int Damage;
            public readonly FP64 Knockback;
            public readonly int HitstunFrames;
            public readonly int StatusType;
            public readonly int StatusFrames;
            public readonly FP64 StatusIntensity;
            public readonly int CooldownFrames;

            public AttackIntent(
                int kind,
                int damage,
                FP64 knockback,
                int hitstunFrames,
                int statusType = (int)FTT.Core.StatusType.None,
                int statusFrames = 0,
                FP64 statusIntensity = default,
                int cooldownFrames = 600) {
                Kind = kind;
                Damage = damage;
                Knockback = knockback;
                HitstunFrames = hitstunFrames;
                StatusType = statusType;
                StatusFrames = statusFrames;
                StatusIntensity = statusIntensity;
                CooldownFrames = cooldownFrames;
            }
        }
    }

    public sealed class FighterMatchSystem : ISystem {
        public void Update(ref Frame frame) {
            ref FighterMatchComponent match = ref frame.GetSingleton<FighterMatchComponent>();
            if (match.MatchState != 1) return;

            if (match.MatchMode != (int)FTT.Core.MatchMode.Stock) match.RemainingFrames--;
            TrackKnockouts(ref frame, ref match);
            int winner = FindStockWinner(ref frame, out bool allAlive, out bool trueTie);
            bool shouldEnd = match.MatchMode switch {
                (int)FTT.Core.MatchMode.Stock => !allAlive,
                (int)FTT.Core.MatchMode.TimeLimit => match.RemainingFrames <= 0,
                _ => !allAlive || match.RemainingFrames <= 0
            };
            if (shouldEnd) {
                if (match.MatchMode == (int)FTT.Core.MatchMode.TimeLimit) {
                    trueTie = match.PlayerOneKOs == match.PlayerTwoKOs;
                    winner = trueTie ? -1 : match.PlayerOneKOs > match.PlayerTwoKOs ? 0 : 1;
                }
                match.MatchState = 2;
                match.WinnerPlayerID = winner;
                match.IsTrueTie = trueTie ? 1 : 0;
            }
        }

        private static void TrackKnockouts(ref Frame frame, ref FighterMatchComponent match) {
            var filter = frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                ref readonly FighterRuntimeComponent runtime = ref frame.GetReadOnly<FighterRuntimeComponent>(entity);
                if (fighter.PlayerID == 0) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerOneKnockoutsSuffered;
                    if (gained > 0) match.PlayerTwoKOs += gained;
                    match.LastPlayerOneKnockoutsSuffered = runtime.KnockoutsSuffered;
                } else if (fighter.PlayerID == 1) {
                    int gained = runtime.KnockoutsSuffered - match.LastPlayerTwoKnockoutsSuffered;
                    if (gained > 0) match.PlayerOneKOs += gained;
                    match.LastPlayerTwoKnockoutsSuffered = runtime.KnockoutsSuffered;
                }
            }
        }

        private static int FindStockWinner(ref Frame frame, out bool allAlive, out bool trueTie) {
            FighterStateComponent first = default;
            FighterStateComponent second = default;
            var filter = frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == 0) first = fighter;
                else if (fighter.PlayerID == 1) second = fighter;
            }

            allAlive = first.Stocks > 0 && second.Stocks > 0;
            if (first.Stocks != second.Stocks) {
                trueTie = false;
                return first.Stocks > second.Stocks ? 0 : 1;
            }

            long firstHP = (long)first.CurrentHP * second.MaxHP;
            long secondHP = (long)second.CurrentHP * first.MaxHP;
            trueTie = firstHP == secondHP;
            return trueTie ? -1 : firstHP > secondHP ? 0 : 1;
        }
    }

    internal static class FighterSimulationRules {
        public static void ApplyStockLoss(
            ref FighterStateComponent fighter,
            ref FighterRuntimeComponent runtime,
            in FighterTuningComponent tuning) {
            if (fighter.Stocks <= 0) return;
            runtime.KnockoutsSuffered++;
            if (runtime.UsesStocks != 0) fighter.Stocks--;
            fighter.Influence = fighter.Influence * FP64.FromInt(3) / FP64.FromInt(4);
            fighter.CurrentHP = fighter.MaxHP;
            fighter.BlockCharges = tuning.MaxBlockCharges;
            fighter.RemainingJumps = tuning.MaxJumpCount;
            fighter.Position = fighter.SpawnPosition;
            fighter.Velocity = FPVector2.Zero;
            fighter.IsGrounded = 1;
            fighter.InvulnerabilityFrames = 120;
            fighter.HitstunFrames = 0;
            fighter.DazeFrames = 0;
            runtime.StatusType = (int)FTT.Core.StatusType.None;
            runtime.StatusFrames = 0;
            runtime.StatusTickFrames = 0;
            runtime.StatusIntensity = FP64.One;
            FighterUniversalMovementRules.Cancel(ref runtime);
        }
    }
}
