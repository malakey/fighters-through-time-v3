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
            public const string Ultimate = "gameplay_ultimate";
            public const string Interact = "gameplay_interact";
            public const string Pause = "ui_pause";
        }

        private readonly Dictionary<int, int> _deviceToPlayer = new();
        private readonly List<int> _connectedJoypads = new();

        public int MaxPlayers { get; set; } = 2;

        public override void _Ready() {
            Instance = this;
            Input.JoyConnectionChanged += OnJoyConnectionChanged;
            RefreshConnectedDevices();
        }

        public override void _ExitTree() {
            Input.JoyConnectionChanged -= OnJoyConnectionChanged;
        }

        private void OnJoyConnectionChanged(long device, bool connected) {
            RefreshConnectedDevices();
        }

        private void RefreshConnectedDevices() {
            _connectedJoypads.Clear();
            _deviceToPlayer.Clear();

            var joypads = Input.GetConnectedJoypads();
            foreach (int joypad in joypads) {
                _connectedJoypads.Add(joypad);
            }

            // Player 0 always uses keyboard or first joypad
            // Player 1 uses second joypad if available
            if (_connectedJoypads.Count >= 1) {
                _deviceToPlayer[_connectedJoypads[0]] = 0;
            }
            if (_connectedJoypads.Count >= 2) {
                _deviceToPlayer[_connectedJoypads[1]] = 1;
            }
        }

        public int GetPlayerForDevice(int deviceId) {
            if (_deviceToPlayer.TryGetValue(deviceId, out int player)) {
                return player;
            }
            return 0;
        }

        public int GetDeviceForPlayer(int playerIndex) {
            foreach (var kvp in _deviceToPlayer) {
                if (kvp.Value == playerIndex) return kvp.Key;
            }
            return -1;
        }

        public bool IsJoypadConnected(int playerIndex) {
            return GetDeviceForPlayer(playerIndex) >= 0;
        }

        public int ConnectedJoypadCount => _connectedJoypads.Count;

        public float GetHorizontalAxis(int playerIndex = 0) {
            float axis = 0f;
            if (Input.IsActionPressed(Actions.MoveRight)) axis += 1f;
            if (Input.IsActionPressed(Actions.MoveLeft)) axis -= 1f;
            return axis;
        }

        public bool IsActionJustPressed(string action) {
            return Input.IsActionJustPressed(action);
        }

        public bool IsActionPressed(string action) {
            return Input.IsActionPressed(action);
        }

        public bool IsActionJustReleased(string action) {
            return Input.IsActionJustReleased(action);
        }

        public float GetActionStrength(string action) {
            return Input.GetActionStrength(action);
        }
    }
}
