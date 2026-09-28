using System;
using System.Collections.Generic;
using FTT.Core;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace FTT.FighterSim {

    /// <summary>
    /// Headless authoritative Fighter simulation. It owns all competitive state;
    /// Godot nodes only capture inputs and present verified component values.
    /// </summary>
    public sealed class FighterSimulation {
        public const int TickRate = 60;
        public const int RollbackHistoryTicks = 120;
        private const int MaxEntities = 128;

        private readonly EcsSimulation _simulation;
        private readonly List<ICommand> _commands = new(2);
        private readonly FighterInputCommand _playerOneCommand = new();
        private readonly FighterInputCommand _playerTwoCommand = new();
        private readonly InputRecord[] _history = new InputRecord[RollbackHistoryTicks];
        private readonly long[] _hashHistory = new long[RollbackHistoryTicks];

        public FighterSimulation(
            FighterCharacterID playerOne = FighterCharacterID.Einstein,
            FighterCharacterID playerTwo = FighterCharacterID.Joan,
            int stocks = 3,
            int matchSeconds = 480,
            int seed = 2026,
            int spawnDistance = 4,
            FighterMatchRules rules = default,
            FighterStageGeometry stageGeometry = null)
            : this(FighterLoadout.Default(playerOne), FighterLoadout.Default(playerTwo), stocks, matchSeconds, seed, spawnDistance, rules, stageGeometry) {
        }

        public FighterSimulation(
            FighterLoadout playerOne,
            FighterLoadout playerTwo,
            int stocks = 3,
            int matchSeconds = 480,
            int seed = 2026,
            int spawnDistance = 4,
            FighterMatchRules rules = default,
            FighterStageGeometry stageGeometry = null) {
            WarmupRegistry.RunAll();
            FighterStageGeometry geometry = stageGeometry ?? FighterStageGeometry.Default;
            // Authored geometry owns the spawn distance; the parameter remains for
            // legacy flat-arena callers and tests.
            int resolvedSpawnDistance = stageGeometry != null ? geometry.SpawnDistance : spawnDistance;
            _simulation = new EcsSimulation(MaxEntities, RollbackHistoryTicks, deltaTimeMs: 16);
            // M08/M05 (Package 12 W3b): the loadouts' projected hit contracts,
            // static configuration shared by the systems that resolve ability
            // hits. Not snapshot state; see FighterHitContractTable.
            var hitContracts = new FighterHitContractTable(playerOne, playerTwo);
            _simulation.AddSystem(
                new FighterWorldSystem(playerOne, playerTwo, stocks, matchSeconds * TickRate, seed, resolvedSpawnDistance, rules),
                SystemPhase.PreUpdate);
            _simulation.AddSystem(new FighterInputSystem(), SystemPhase.PreUpdate);
            _simulation.AddSystem(new FighterCountdownSystem(), SystemPhase.PreUpdate);
            _simulation.AddSystem(new FighterMovementSystem(geometry), SystemPhase.Update);
            _simulation.AddSystem(new FighterAbilityEntitySystem(), SystemPhase.Update);
            _simulation.AddSystem(new FighterPushboxSystem(geometry), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterCombatSystem(hitContracts), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterProjectileSystem(hitContracts), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterPersistentObjectSystem(), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterZoneSystem(), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterHazardSystem(geometry), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterOrbSystem(geometry), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterMatchSystem(geometry), SystemPhase.LateUpdate);
            _simulation.Initialize();
        }

        public int CurrentTick => _simulation.CurrentTick;
        public long CurrentHash => _simulation.GetStateHash();

        public long Advance(PlayerInputFrame playerOne, PlayerInputFrame playerTwo) {
            int tick = CurrentTick;
            playerOne.Tick = (uint)tick;
            playerTwo.Tick = (uint)tick;
            StoreInput(tick, playerOne, playerTwo, true, true);
            return StepStoredTick(tick);
        }

        public long AdvanceWithPredictedPlayerTwo(PlayerInputFrame playerOne) {
            int tick = CurrentTick;
            playerOne.Tick = (uint)tick;
            PlayerInputFrame predicted = PredictPlayerTwo(tick);
            StoreInput(tick, playerOne, predicted, true, false);
            return StepStoredTick(tick);
        }

        public long AdvanceWithPredictedRemote(int localPlayerID, PlayerInputFrame localInput) {
            int tick = CurrentTick;
            localInput.Tick = (uint)tick;
            if (localPlayerID == 0) {
                PlayerInputFrame predicted = PredictPlayerTwo(tick);
                StoreInput(tick, localInput, predicted, true, false);
            } else if (localPlayerID == 1) {
                PlayerInputFrame predicted = PredictPlayerOne(tick);
                StoreInput(tick, predicted, localInput, false, true);
            } else {
                throw new ArgumentOutOfRangeException(nameof(localPlayerID));
            }
            return StepStoredTick(tick);
        }

        public bool CorrectPlayerTwoInput(int tick, PlayerInputFrame corrected) {
            return CorrectRemoteInput(1, tick, corrected);
        }

        public bool CorrectRemoteInput(int remotePlayerID, int tick, PlayerInputFrame corrected) {
            int currentTick = CurrentTick;
            if (tick < 0 || tick >= currentTick || currentTick - tick > RollbackHistoryTicks) return false;
            int slot = tick % RollbackHistoryTicks;
            if (_history[slot].Tick != tick || !_simulation.HasSnapshot(tick)) return false;

            corrected.Tick = (uint)tick;
            if (remotePlayerID == 0) {
                _history[slot].PlayerOne = corrected;
                _history[slot].PlayerOneKnown = true;
            } else if (remotePlayerID == 1) {
                _history[slot].PlayerTwo = corrected;
                _history[slot].PlayerTwoKnown = true;
            } else {
                return false;
            }
            _simulation.Rollback(tick);
            for (int replayTick = tick; replayTick < currentTick; replayTick++) StepStoredTick(replayTick);
            return true;
        }

        public bool TryGetFighter(int playerID, out FighterStateComponent state) {
            var filter = _simulation.Frame.Filter<FighterStateComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    state = fighter;
                    return true;
                }
            }
            state = default;
            return false;
        }

        public bool TryGetFighterRuntime(int playerID, out FighterRuntimeComponent runtime) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterRuntimeComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    runtime = _simulation.Frame.GetReadOnly<FighterRuntimeComponent>(entity);
                    return true;
                }
            }
            runtime = default;
            return false;
        }

        public bool TryGetFighterVerb(int playerID, out FighterVerbComponent verb) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterVerbComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    verb = _simulation.Frame.GetReadOnly<FighterVerbComponent>(entity);
                    return true;
                }
            }
            verb = default;
            return false;
        }

        /// <summary>
        /// Package 11 A9b: the exact historical position Echo Step would restore
        /// for <paramref name="playerID"/>, read through the shared ring accessor
        /// rather than a second copy of the resolution rule. The F19 CPU policy has
        /// to check that destination against current geometry and pit risk before it
        /// may select the action.
        ///
        /// <para>Package 11 A1c re-pointed this at the V7.6 31-sample bank. Two
        /// things changed for the CPU: the answer is now the <b>exact</b> t-30
        /// sample rather than the retired ring's 24-to-30-frames-ago approximation,
        /// and it returns <b>false</b> while a history generation is still filling,
        /// so the CPU stops proposing a destination that does not exist yet.</para>
        /// </summary>
        public bool TryGetEchoStepDestination(int playerID, out FPVector2 destination) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterEchoStepRing0Component>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter =
                    ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID != playerID) continue;
                Frame frame = _simulation.Frame;
                return FighterEchoStepHistory.TryGetLookback(ref frame, entity, out destination);
            }
            destination = FPVector2.Zero;
            return false;
        }

        /// <summary>
        /// V7.6 D01-D04 (Package 11 A1b): reads a fighter's defensive layer
        /// (component 312) - the Defy protected-recovery window and bar, the
        /// Temporal Aegis flag and the HP barrier. Snapshot and hash state.
        /// </summary>
        public bool TryGetFighterDefense(int playerID, out FighterDefenseComponent defense) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterDefenseComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    defense = _simulation.Frame.GetReadOnly<FighterDefenseComponent>(entity);
                    return true;
                }
            }
            defense = default;
            return false;
        }

        /// <summary>
        /// V7.6 F07 (Package 11 A1): reads a fighter's caster-owned Conductive
        /// mark (component 318). Snapshot and hash state like everything else.
        /// </summary>
        public bool TryGetFighterConductive(int playerID, out FighterConductiveComponent mark) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterConductiveComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    mark = _simulation.Frame.GetReadOnly<FighterConductiveComponent>(entity);
                    return true;
                }
            }
            mark = default;
            return false;
        }

        /// <summary>
        /// M05 (Package 12 W3b): reads a fighter's knockdown / get-up state
        /// (component 320). Snapshot and hash state like everything else.
        /// </summary>
        public bool TryGetFighterKnockdown(int playerID, out FighterKnockdownComponent knockdown) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterKnockdownComponent>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter = ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    knockdown = _simulation.Frame.GetReadOnly<FighterKnockdownComponent>(entity);
                    return true;
                }
            }
            knockdown = default;
            return false;
        }

        public FighterMatchComponent GetMatchState() =>
            _simulation.Frame.GetReadOnlySingleton<FighterMatchComponent>();

        /// <summary>
        /// F22 phase identity and the frozen regulation stocks-lost totals
        /// (Package 11 A1c). Restored atomically with the match state, so a result
        /// built from both is the committed one.
        /// </summary>
        public FighterSuddenDeathComponent GetSuddenDeathState() =>
            _simulation.Frame.GetReadOnlySingleton<FighterSuddenDeathComponent>();

        /// <summary>
        /// The V7.6 Echo Step history head for a player: head slot, newest tick,
        /// valid-sample count, generation and the armed activation. Exposed for the
        /// determinism suites, which have to prove the ring is one sample per tick
        /// and survives a snapshot round trip.
        /// </summary>
        public bool TryGetEchoStepRing(int playerID, out FighterEchoStepRing0Component ring) {
            var filter = _simulation.Frame.Filter<FighterStateComponent, FighterEchoStepRing0Component>();
            while (filter.Next(out EntityRef entity)) {
                ref readonly FighterStateComponent fighter =
                    ref _simulation.Frame.GetReadOnly<FighterStateComponent>(entity);
                if (fighter.PlayerID == playerID) {
                    ring = _simulation.Frame.GetReadOnly<FighterEchoStepRing0Component>(entity);
                    return true;
                }
            }
            ring = default;
            return false;
        }

        public int ProjectileCount => CountComponents<FighterProjectileComponent>();
        public int PersistentObjectCount => CountComponents<FighterPersistentObjectComponent>();
        public int HazardCount => CountComponents<FighterHazardComponent>();
        public int OrbCount => CountComponents<FighterOrbComponent>();
        public int ZoneCount => CountComponents<FighterZoneComponent>();

        public bool TryGetFirstProjectile(out FighterProjectileComponent projectile) =>
            TryGetFirstComponent(out projectile);

        public bool TryGetFirstPersistentObject(out FighterPersistentObjectComponent persistent) =>
            TryGetFirstComponent(out persistent);

        public bool TryGetFirstHazard(out FighterHazardComponent hazard) =>
            TryGetFirstComponent(out hazard);

        public bool TryGetFirstOrb(out FighterOrbComponent orb) =>
            TryGetFirstComponent(out orb);

        public bool TryGetFirstZone(out FighterZoneComponent zone) =>
            TryGetFirstComponent(out zone);

        public void CopyProjectilesTo(List<FighterProjectileComponent> destination) =>
            CopyComponentsTo(destination);

        public void CopyPersistentObjectsTo(List<FighterPersistentObjectComponent> destination) =>
            CopyComponentsTo(destination);

        public void CopyHazardsTo(List<FighterHazardComponent> destination) =>
            CopyComponentsTo(destination);

        public void CopyOrbsTo(List<FighterOrbComponent> destination) =>
            CopyComponentsTo(destination);

        public void CopyZonesTo(List<FighterZoneComponent> destination) =>
            CopyComponentsTo(destination);

        public byte[] CaptureFullState() => _simulation.SerializeFullState();

        public void RestoreFullState(byte[] state) {
            if (state == null || state.Length == 0) throw new ArgumentException("Snapshot is empty.", nameof(state));
            _simulation.RestoreFromFullState(state);
        }

        public long GetRecordedHash(int tick) {
            int slot = PositiveModulo(tick, RollbackHistoryTicks);
            return _history[slot].Tick == tick ? _hashHistory[slot] : 0L;
        }

        private long StepStoredTick(int tick) {
            int slot = PositiveModulo(tick, RollbackHistoryTicks);
            if (_history[slot].Tick != tick) throw new InvalidOperationException($"No input exists for tick {tick}.");

            _simulation.SaveSnapshot();
            SetCommand(_playerOneCommand, 0, tick, _history[slot].PlayerOne);
            SetCommand(_playerTwoCommand, 1, tick, _history[slot].PlayerTwo);
            _commands.Clear();
            _commands.Add(_playerOneCommand);
            _commands.Add(_playerTwoCommand);
            _simulation.Tick(_commands);
            long hash = _simulation.GetStateHash();
            _hashHistory[slot] = hash;
            return hash;
        }

        private void StoreInput(int tick, PlayerInputFrame p1, PlayerInputFrame p2, bool p1Known, bool p2Known) {
            int slot = PositiveModulo(tick, RollbackHistoryTicks);
            _history[slot] = new InputRecord {
                Tick = tick,
                PlayerOne = p1,
                PlayerTwo = p2,
                PlayerOneKnown = p1Known,
                PlayerTwoKnown = p2Known
            };
        }

        private PlayerInputFrame PredictPlayerTwo(int tick) {
            if (tick <= 0) return new PlayerInputFrame { Tick = (uint)tick };
            int priorSlot = PositiveModulo(tick - 1, RollbackHistoryTicks);
            if (_history[priorSlot].Tick != tick - 1) return new PlayerInputFrame { Tick = (uint)tick };
            PlayerInputFrame predicted = _history[priorSlot].PlayerTwo;
            predicted.Tick = (uint)tick;
            predicted.Pressed = GameplayButtons.None;
            predicted.Released = GameplayButtons.None;
            return predicted;
        }

        private PlayerInputFrame PredictPlayerOne(int tick) {
            if (tick <= 0) return new PlayerInputFrame { Tick = (uint)tick };
            int priorSlot = PositiveModulo(tick - 1, RollbackHistoryTicks);
            if (_history[priorSlot].Tick != tick - 1) return new PlayerInputFrame { Tick = (uint)tick };
            PlayerInputFrame predicted = _history[priorSlot].PlayerOne;
            predicted.Tick = (uint)tick;
            predicted.Pressed = GameplayButtons.None;
            predicted.Released = GameplayButtons.None;
            return predicted;
        }

        private static void SetCommand(FighterInputCommand command, int playerID, int tick, PlayerInputFrame input) {
            command.PlayerId = playerID;
            command.Tick = tick;
            command.MoveX = input.MoveX;
            command.MoveY = input.MoveY;
            command.HeldButtons = (int)input.Held;
            command.PressedButtons = (int)input.Pressed;
            command.ReleasedButtons = (int)input.Released;
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private int CountComponents<T>() where T : unmanaged, IComponent {
            int count = 0;
            var filter = _simulation.Frame.Filter<T>();
            while (filter.Next(out _)) count++;
            return count;
        }

        private bool TryGetFirstComponent<T>(out T component) where T : unmanaged, IComponent {
            var filter = _simulation.Frame.Filter<T>();
            if (filter.Next(out EntityRef entity)) {
                component = _simulation.Frame.GetReadOnly<T>(entity);
                return true;
            }
            component = default;
            return false;
        }

        private void CopyComponentsTo<T>(List<T> destination) where T : unmanaged, IComponent {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            var filter = _simulation.Frame.Filter<T>();
            while (filter.Next(out EntityRef entity)) destination.Add(_simulation.Frame.GetReadOnly<T>(entity));
        }

        private struct InputRecord {
            public int Tick;
            public PlayerInputFrame PlayerOne;
            public PlayerInputFrame PlayerTwo;
            public bool PlayerOneKnown;
            public bool PlayerTwoKnown;
        }
    }
}
