using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Core {

    public partial class InputManager : Node {
        public static InputManager Instance { get; private set; }

        public static class Actions {
            public const string MoveLeft = "gameplay_move_left";
            public const string MoveRight = "gameplay_move_right";
            public const string Jump = "gameplay_jump";
            public const string Down = "gameplay_down";
            public const string BasicAttack = "gameplay_basic_attack";
            public const string Special1 = "gameplay_special1";
            public const string Special2 = "gameplay_special2";
            public const string MovementAbility = "gameplay_movement_ability";
            public const string Block = "gameplay_block";
            public const string Roll = "gameplay_roll";
            public const string Ultimate = "gameplay_ultimate";
            public const string Interact = "gameplay_interact";
            public const string Pause = "ui_pause";
        }

        /// <summary>
        /// Actions the Controls tab may rebind, in display order (Package 8 A4).
        /// <see cref="Actions.Ultimate"/> is deliberately absent: it is authored as
        /// an LB+RB chord that <see cref="ReadUltimatePressed"/> evaluates as a
        /// conjunction, which the per-event remap UI cannot express. It is surfaced
        /// read-only instead.
        /// </summary>
        public static readonly string[] RemappableActions = {
            Actions.MoveLeft, Actions.MoveRight, Actions.Jump, Actions.Down,
            Actions.BasicAttack, Actions.Special1, Actions.Special2, Actions.MovementAbility,
            Actions.Block, Actions.Roll, Actions.Interact, Actions.Pause
        };

        /// <summary>Actions shown in the Controls tab but not rebindable.</summary>
        public static readonly string[] ReadOnlyActions = { Actions.Ultimate };

        /// <summary>Translation key naming an action in the remap UI.</summary>
        public static string ActionLabelKey(string action) => action switch {
            Actions.MoveLeft => "controls_action_move_left",
            Actions.MoveRight => "controls_action_move_right",
            Actions.Jump => "controls_action_jump",
            Actions.Down => "controls_action_down",
            Actions.BasicAttack => "controls_action_basic_attack",
            Actions.Special1 => "controls_action_special1",
            Actions.Special2 => "controls_action_special2",
            Actions.MovementAbility => "controls_action_movement_ability",
            Actions.Block => "controls_action_block",
            Actions.Roll => "controls_action_roll",
            Actions.Ultimate => "controls_action_ultimate",
            Actions.Interact => "controls_action_interact",
            Actions.Pause => "controls_action_pause",
            _ => "common_unknown"
        };

        public const int KeyboardDevice = -1;
        public const int UnassignedDevice = -2;

        private readonly Dictionary<int, int> _deviceToPlayer = new();
        private readonly Dictionary<int, int> _playerToDevice = new();
        private readonly Dictionary<int, IPlayerInputSource> _overrides = new();
        private readonly Dictionary<int, PlayerInputFrame> _frames = new();
        private readonly List<int> _connectedJoypads = new();

        [Export(PropertyHint.Range, "1,2,1")]
        public int MaxPlayers { get; set; } = 2;

        public event Action<int, int> DeviceAssigned;
        public event Action<int> PlayerDeviceDisconnected;

        public override void _Ready() {
            Instance = this;
            Input.JoyConnectionChanged += OnJoyConnectionChanged;
            RefreshConnectedDevices();
        }

        public override void _ExitTree() {
            Input.JoyConnectionChanged -= OnJoyConnectionChanged;
            if (Instance == this) Instance = null;
        }

        public override void _PhysicsProcess(double delta) {
            uint tick = unchecked((uint)Engine.GetPhysicsFrames());
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                CaptureFrame(playerIndex, tick);
            }
        }

        public PlayerInputFrame GetFrame(int playerIndex) {
            ValidatePlayerIndex(playerIndex);
            uint tick = unchecked((uint)Engine.GetPhysicsFrames());
            return CaptureFrame(playerIndex, tick);
        }

        public PlayerInputFrame CaptureFrame(int playerIndex, uint tick) {
            ValidatePlayerIndex(playerIndex);
            if (_frames.TryGetValue(playerIndex, out PlayerInputFrame cached) && cached.Tick == tick) {
                return cached;
            }

            PlayerInputFrame previous = _frames.GetValueOrDefault(playerIndex);
            PlayerInputFrame frame;
            if (_overrides.TryGetValue(playerIndex, out IPlayerInputSource source)) {
                frame = source.Sample(tick, previous);
            } else {
                int deviceId = GetDeviceForPlayer(playerIndex);
                frame = SampleGodotDevice(tick, deviceId, previous.Held);
            }

            frame.Tick = tick;
            _frames[playerIndex] = frame;
            return frame;
        }

        public void SetInputSource(int playerIndex, IPlayerInputSource source) {
            ValidatePlayerIndex(playerIndex);
            if (source == null) _overrides.Remove(playerIndex);
            else _overrides[playerIndex] = source;
            _frames.Remove(playerIndex);
        }

        public void ClearInputSource(int playerIndex) => SetInputSource(playerIndex, null);

        public void AssignKeyboardToPlayer(int playerIndex = 0) {
            AssignDevice(KeyboardDevice, playerIndex);
        }

        public void AssignJoypadToPlayer(int deviceId, int playerIndex) {
            if (!_connectedJoypads.Contains(deviceId)) {
                throw new ArgumentException($"Joypad {deviceId} is not connected.", nameof(deviceId));
            }
            AssignDevice(deviceId, playerIndex);
        }

        public void AutoAssignDevices() {
            _deviceToPlayer.Clear();
            _playerToDevice.Clear();
            _frames.Clear();

            // Keyboard is a complete first-player device. Connected joypads fill the
            // remaining local slots, enabling keyboard-versus-controller with one pad.
            AssignDevice(KeyboardDevice, 0);
            int playerIndex = 1;
            foreach (int deviceId in _connectedJoypads) {
                if (playerIndex >= MaxPlayers) break;
                AssignDevice(deviceId, playerIndex++);
            }
        }

        public int GetPlayerForDevice(int deviceId) {
            return _deviceToPlayer.TryGetValue(deviceId, out int player) ? player : -1;
        }

        public int GetDeviceForPlayer(int playerIndex) {
            ValidatePlayerIndex(playerIndex);
            return _playerToDevice.TryGetValue(playerIndex, out int device) ? device : UnassignedDevice;
        }

        public bool IsJoypadConnected(int playerIndex) => GetDeviceForPlayer(playerIndex) >= 0;
        public int ConnectedJoypadCount => _connectedJoypads.Count;

        public float GetHorizontalAxis(int playerIndex = 0) => GetFrame(playerIndex).Horizontal;
        public bool IsActionJustPressed(string action, int playerIndex = 0) => GetFrame(playerIndex).IsPressed(ToButton(action));
        public bool IsActionPressed(string action, int playerIndex = 0) => GetFrame(playerIndex).IsHeld(ToButton(action));
        public bool IsActionJustReleased(string action, int playerIndex = 0) => GetFrame(playerIndex).IsReleased(ToButton(action));

        public float GetActionStrength(string action, int playerIndex = 0) {
            PlayerInputFrame frame = GetFrame(playerIndex);
            if (action == Actions.MoveLeft) return MathF.Max(0.0f, -frame.Horizontal);
            if (action == Actions.MoveRight) return MathF.Max(0.0f, frame.Horizontal);
            return frame.IsHeld(ToButton(action)) ? 1.0f : 0.0f;
        }

        private void OnJoyConnectionChanged(long device, bool connected) {
            int deviceId = checked((int)device);
            int disconnectedPlayer = connected ? -1 : GetPlayerForDevice(deviceId);
            RefreshConnectedDevices();
            if (disconnectedPlayer >= 0) PlayerDeviceDisconnected?.Invoke(disconnectedPlayer);
        }

        private void RefreshConnectedDevices() {
            _connectedJoypads.Clear();
            foreach (int joypad in Input.GetConnectedJoypads()) _connectedJoypads.Add(joypad);
            _connectedJoypads.Sort();
            ApplyDeviceTopology();
        }

        /// <summary>
        /// Reconciles assignments with the currently connected joypad set. It
        /// drops assignments whose device vanished and fills only the slots that
        /// have no device — it must never re-run a full auto-assign, because that
        /// would silently reshuffle both fighters mid-match every time any pad is
        /// plugged in or unplugged. Exposed for tests; production calls it through
        /// <see cref="RefreshConnectedDevices"/>.
        /// </summary>
        public void ApplyDeviceTopology() {
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (!_playerToDevice.TryGetValue(playerIndex, out int deviceId)) continue;
                if (deviceId == KeyboardDevice || _connectedJoypads.Contains(deviceId)) continue;
                _playerToDevice.Remove(playerIndex);
                _deviceToPlayer.Remove(deviceId);
                _frames.Remove(playerIndex);
            }

            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (_playerToDevice.ContainsKey(playerIndex)) continue;
                // The keyboard is a complete first-player device; the remaining
                // slots draw from the unclaimed connected joypads.
                if (playerIndex == 0 && GetPlayerForDevice(KeyboardDevice) < 0) {
                    AssignDevice(KeyboardDevice, 0);
                    continue;
                }
                TryAssignFirstFreeDevice(playerIndex);
            }
        }

        /// <summary>
        /// Binds the first connected joypad not already claimed by another slot.
        /// Used by the controller-reconnect flow. Returns false when nothing is free.
        /// </summary>
        public bool TryAssignFirstFreeDevice(int playerIndex) {
            ValidatePlayerIndex(playerIndex);
            foreach (int deviceId in _connectedJoypads) {
                if (_deviceToPlayer.ContainsKey(deviceId)) continue;
                AssignDevice(deviceId, playerIndex);
                return true;
            }
            return false;
        }

        /// <summary>Test seam: replaces the connected-joypad set without touching Godot's Input singleton.</summary>
        internal void SetConnectedJoypadsForTesting(params int[] deviceIds) {
            _connectedJoypads.Clear();
            if (deviceIds != null) _connectedJoypads.AddRange(deviceIds);
            _connectedJoypads.Sort();
            ApplyDeviceTopology();
        }

        private void AssignDevice(int deviceId, int playerIndex) {
            ValidatePlayerIndex(playerIndex);

            if (_playerToDevice.TryGetValue(playerIndex, out int oldDevice)) {
                _deviceToPlayer.Remove(oldDevice);
            }
            if (_deviceToPlayer.TryGetValue(deviceId, out int oldPlayer)) {
                _playerToDevice.Remove(oldPlayer);
            }

            _deviceToPlayer[deviceId] = playerIndex;
            _playerToDevice[playerIndex] = deviceId;
            _frames.Remove(playerIndex);
            DeviceAssigned?.Invoke(playerIndex, deviceId);
        }

        private static PlayerInputFrame SampleGodotDevice(uint tick, int deviceId, GameplayButtons previousHeld) {
            if (deviceId == UnassignedDevice) return PlayerInputFrame.Create(tick, 0, 0, GameplayButtons.None, previousHeld);
            float horizontal = ReadActionStrength(Actions.MoveRight, deviceId)
                - ReadActionStrength(Actions.MoveLeft, deviceId);
            bool jump = ReadActionPressed(Actions.Jump, deviceId);
            bool down = ReadActionPressed(Actions.Down, deviceId);
            float vertical = (down ? 1.0f : 0.0f) - (jump ? 1.0f : 0.0f);

            GameplayButtons held = GameplayButtons.None;
            AddIfHeld(ref held, GameplayButtons.Jump, jump);
            AddIfHeld(ref held, GameplayButtons.Down, down);
            AddIfHeld(ref held, GameplayButtons.BasicAttack, ReadActionPressed(Actions.BasicAttack, deviceId));
            AddIfHeld(ref held, GameplayButtons.Special1, ReadActionPressed(Actions.Special1, deviceId));
            AddIfHeld(ref held, GameplayButtons.Special2, ReadActionPressed(Actions.Special2, deviceId));
            AddIfHeld(ref held, GameplayButtons.MovementAbility, ReadActionPressed(Actions.MovementAbility, deviceId));
            AddIfHeld(ref held, GameplayButtons.Block, ReadActionPressed(Actions.Block, deviceId));
            AddIfHeld(ref held, GameplayButtons.Roll, ReadActionPressed(Actions.Roll, deviceId));
            AddIfHeld(ref held, GameplayButtons.Ultimate, ReadUltimatePressed(deviceId));
            AddIfHeld(ref held, GameplayButtons.Interact, ReadActionPressed(Actions.Interact, deviceId));
            AddIfHeld(ref held, GameplayButtons.Pause, ReadActionPressed(Actions.Pause, deviceId));

            return PlayerInputFrame.Create(tick, horizontal, vertical, held, previousHeld);
        }

        /// <summary>
        /// Scratch buffer for <see cref="ReadUltimatePressed"/> (audit M-26): this
        /// runs on the 60 Hz per-player poll path, so it must not allocate a List
        /// per call. Main-thread only, like all input polling.
        /// </summary>
        private static readonly List<JoyButton> UltimateChordScratch = new();

        private static bool ReadUltimatePressed(int deviceId) {
            if (deviceId == KeyboardDevice) return ReadActionPressed(Actions.Ultimate, deviceId);

            UltimateChordScratch.Clear();
            Godot.Collections.Array<InputEvent> ultimateEvents = InputMap.ActionGetEvents(Actions.Ultimate);
            using (ultimateEvents.AsDisposable()) {
                foreach (InputEvent inputEvent in ultimateEvents) {
                    if (inputEvent is InputEventJoypadButton button) UltimateChordScratch.Add(button.ButtonIndex);
                }
            }

            if (UltimateChordScratch.Count <= 1) return ReadActionPressed(Actions.Ultimate, deviceId);
            foreach (JoyButton button in UltimateChordScratch) {
                if (!Input.IsJoyButtonPressed(deviceId, button)) return false;
            }
            return true;
        }

        private static bool ReadActionPressed(string action, int deviceId) {
            return ReadActionStrength(action, deviceId) >= InputMap.ActionGetDeadzone(action);
        }

        private static float ReadActionStrength(string action, int deviceId) {
            float strength = 0.0f;
            // M-26: the hottest poll path in the project (~12 actions x 2 players
            // x 60 Hz). Dispose the engine collection wrapper deterministically
            // instead of flooding the finalizer queue (AGENTS.md disposal rule).
            Godot.Collections.Array<InputEvent> actionEvents = InputMap.ActionGetEvents(action);
            using var lifetime = actionEvents.AsDisposable();
            foreach (InputEvent inputEvent in actionEvents) {
                if (deviceId == KeyboardDevice) {
                    if (inputEvent is InputEventKey keyEvent) {
                        Key key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
                        if (key != Key.None && Input.IsPhysicalKeyPressed(key)) strength = 1.0f;
                    } else if (inputEvent is InputEventMouseButton mouseEvent
                        && Input.IsMouseButtonPressed(mouseEvent.ButtonIndex)) {
                        strength = 1.0f;
                    }
                } else if (inputEvent is InputEventJoypadButton buttonEvent) {
                    if (Input.IsJoyButtonPressed(deviceId, buttonEvent.ButtonIndex)) strength = 1.0f;
                } else if (inputEvent is InputEventJoypadMotion motionEvent) {
                    float axis = Input.GetJoyAxis(deviceId, motionEvent.Axis);
                    float directional = axis * MathF.Sign(motionEvent.AxisValue);
                    strength = MathF.Max(strength, MathF.Max(0.0f, directional));
                }
            }
            return Math.Clamp(strength, 0.0f, 1.0f);
        }

        private static GameplayButtons ToButton(string action) => action switch {
            Actions.Jump => GameplayButtons.Jump,
            Actions.Down => GameplayButtons.Down,
            Actions.BasicAttack => GameplayButtons.BasicAttack,
            Actions.Special1 => GameplayButtons.Special1,
            Actions.Special2 => GameplayButtons.Special2,
            Actions.MovementAbility => GameplayButtons.MovementAbility,
            Actions.Block => GameplayButtons.Block,
            Actions.Roll => GameplayButtons.Roll,
            Actions.Ultimate => GameplayButtons.Ultimate,
            Actions.Interact => GameplayButtons.Interact,
            Actions.Pause => GameplayButtons.Pause,
            _ => GameplayButtons.None
        };

        private static void AddIfHeld(ref GameplayButtons held, GameplayButtons button, bool isHeld) {
            if (isHeld) held |= button;
        }

        private void ValidatePlayerIndex(int playerIndex) {
            if (playerIndex < 0 || playerIndex >= MaxPlayers) {
                throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, $"Player index must be between 0 and {MaxPlayers - 1}.");
            }
        }
    }
}
