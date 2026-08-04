using System;
using System.Collections.Generic;
using FTT.Core;
using xpTURN.Klotho.Core;
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
            int spawnDistance = 4)
            : this(FighterLoadout.Default(playerOne), FighterLoadout.Default(playerTwo), stocks, matchSeconds, seed, spawnDistance) {
        }

        public FighterSimulation(
            FighterLoadout playerOne,
            FighterLoadout playerTwo,
            int stocks = 3,
            int matchSeconds = 480,
            int seed = 2026,
            int spawnDistance = 4) {
            WarmupRegistry.RunAll();
            _simulation = new EcsSimulation(MaxEntities, RollbackHistoryTicks, deltaTimeMs: 16);
            _simulation.AddSystem(
                new FighterWorldSystem(playerOne, playerTwo, stocks, matchSeconds * TickRate, seed, spawnDistance),
                SystemPhase.PreUpdate);
            _simulation.AddSystem(new FighterInputSystem(), SystemPhase.PreUpdate);
            _simulation.AddSystem(new FighterMovementSystem(), SystemPhase.Update);
            _simulation.AddSystem(new FighterCombatSystem(), SystemPhase.PostUpdate);
            _simulation.AddSystem(new FighterMatchSystem(), SystemPhase.LateUpdate);
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

        public bool CorrectPlayerTwoInput(int tick, PlayerInputFrame corrected) {
            int currentTick = CurrentTick;
            if (tick < 0 || tick >= currentTick || currentTick - tick > RollbackHistoryTicks) return false;
            int slot = tick % RollbackHistoryTicks;
            if (_history[slot].Tick != tick || !_simulation.HasSnapshot(tick)) return false;

            corrected.Tick = (uint)tick;
            _history[slot].PlayerTwo = corrected;
            _history[slot].PlayerTwoKnown = true;
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

        public FighterMatchComponent GetMatchState() =>
            _simulation.Frame.GetReadOnlySingleton<FighterMatchComponent>();

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

        private struct InputRecord {
            public int Tick;
            public PlayerInputFrame PlayerOne;
            public PlayerInputFrame PlayerTwo;
            public bool PlayerOneKnown;
            public bool PlayerTwoKnown;
        }
    }
}
