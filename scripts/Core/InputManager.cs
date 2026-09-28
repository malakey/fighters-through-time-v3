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
            /// <summary>
            /// Vertical "up" intent (gameplay feel batch §2.7). It carries no
            /// <see cref="GameplayButtons"/> bit by design: it exists purely as
            /// the negative half of <see cref="PlayerInputFrame.MoveY"/>, which
            /// both the up-attack selection and the directional Warp read.
            /// </summary>
            public const string Up = "gameplay_up";
            public const string BasicAttack = "gameplay_basic_attack";
            public const string Special1 = "gameplay_special1";
            public const string Special2 = "gameplay_special2";
            public const string MovementAbility = "gameplay_movement_ability";
            public const string Block = "gameplay_block";
            public const string Roll = "gameplay_roll";
            public const string Ultimate = "gameplay_ultimate";
            public const string Interact = "gameplay_interact";
            /// <summary>
            /// V7.6 Time Freeze (F03), Story Mode only. Replaced the retired
            /// V7.2 manual-rewind action and inherited its default events
            /// unchanged: <c>R</c> (physical keycode 82) and the gamepad
            /// Back/Select button (index 4).
            ///
            /// <para>Deliberately carries no <see cref="GameplayButtons"/> bit —
            /// that is the Story-only guarantee: the verb never reaches the
            /// deterministic input frame or the Fighter sim.</para>
            /// </summary>
            public const string TimeFreeze = "gameplay_time_freeze";

            /// <summary>
            /// C01c direct <b>Grab</b> action (Package 11 A1c). Its direct slot is
            /// authored <b>Unbound</b> on both device kinds: the shipped route is
            /// the Block+BasicAttack preset chord, and C01c's promise is that a
            /// direct bind is an addition, not a replacement. Carries
            /// <see cref="GameplayButtons.Grab"/>, so it reaches the deterministic
            /// input frame — unlike <see cref="TimeFreeze"/>, which must not.
            /// </summary>
            public const string Grab = "gameplay_grab";

            /// <summary>
            /// C01c direct <b>Echo Step</b> action (Package 11 A1c). Authored
            /// Unbound; the shipped route is the Block+Roll preset chord. Carries
            /// <see cref="GameplayButtons.EchoStep"/>.
            /// </summary>
            public const string EchoStep = "gameplay_echo_step";

            /// <summary>
            /// The retired V7.2 manual-rewind action name. Kept only so the
            /// global binding payload's key-rename migration has a literal to
            /// match; never bound, never polled.
            /// </summary>
            internal const string LegacyRewind = "gameplay_rewind";
            public const string Pause = "ui_pause";

            /// <summary>
            /// Package 12 W6 (G10): opens the session Dialogue Log while a sequence
            /// is on screen (Tab / right-stick click by default). A UI action, not a
            /// gameplay verb: it carries no <see cref="GameplayButtons"/> bit and is
            /// not a Controls-tab row.
            /// </summary>
            public const string DialogueLog = "ui_dialogue_log";
        }

        /// <summary>
        /// Actions the Controls tab may rebind, in display order (Package 8 A4).
        ///
        /// <para>Package 11 A8 / C01c: <see cref="Actions.Ultimate"/> now carries a
        /// real direct binding slot. Package 8 surfaced it read-only because the
        /// per-event remap UI cannot express the LB+RB conjunction; C01c's answer
        /// is that the chord stays a <em>fixed preset shortcut</em> alongside a
        /// single direct key/button, so the row is editable after all. The chord
        /// itself is still not a remappable event — see
        /// <see cref="InputShortcuts"/>, which owns the recipes.</para>
        /// </summary>
        public static readonly string[] RemappableActions = {
            Actions.MoveLeft, Actions.MoveRight, Actions.Jump, Actions.Down, Actions.Up,
            Actions.BasicAttack, Actions.Special1, Actions.Special2, Actions.MovementAbility,
            Actions.Block, Actions.Roll, Actions.Ultimate, Actions.Interact, Actions.TimeFreeze, Actions.Pause
        };

        /// <summary>
        /// Actions shown in the Controls tab but not rebindable. Empty since
        /// Package 11 A8 moved Ultimate into <see cref="RemappableActions"/>; kept
        /// as the seam for any future action that genuinely cannot be expressed as
        /// a per-event binding.
        /// </summary>
        public static readonly string[] ReadOnlyActions = System.Array.Empty<string>();

        /// <summary>Translation key naming an action in the remap UI.</summary>
        public static string ActionLabelKey(string action) => action switch {
            InputShortcuts.GrabAction => "controls_action_grab",
            InputShortcuts.EchoStepAction => "controls_action_echo_step",
            InputShortcuts.TimeFreezeAction => "controls_action_time_freeze",
            Actions.MoveLeft => "controls_action_move_left",
            Actions.MoveRight => "controls_action_move_right",
            Actions.Jump => "controls_action_jump",
            Actions.Down => "controls_action_down",
            Actions.Up => "controls_action_up",
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

        // Package 12 W6 (G13): local input processing that runs BEFORE
        // quantization and never reaches the simulation's rules — the Toggle
        // Block latch per player slot, and the per-device stick profiles.
        private readonly Dictionary<int, BlockToggleLatch> _blockLatches = new();
        private readonly Dictionary<int, string> _joypadGuids = new();

        /// <summary>
        /// Test seam: the Block Mode to use instead of the saved global setting.
        /// Always reset to null in a test's finally block.
        /// </summary>
        internal BlockMode? BlockModeOverrideForTesting;

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
                BlockToggleLatch latch = _blockLatches.GetValueOrDefault(playerIndex);
                frame = SampleGodotDevice(
                    tick, deviceId, previous.Held, ref latch, CurrentBlockMode(), StickProfileFor(deviceId));
                _blockLatches[playerIndex] = latch;
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
            _joypadGuids.Clear();
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
            // A new device starts from an unlatched stance: a Toggle Block latch
            // must never survive a controller hand-over.
            _blockLatches.Remove(playerIndex);
            DeviceAssigned?.Invoke(playerIndex, deviceId);
        }

        // === Package 12 W6 (G13) — Block Mode and stick profiles ============

        /// <summary>The Block Mode in force: the test override, else the saved global setting, else Hold.</summary>
        public BlockMode CurrentBlockMode() =>
            BlockModeOverrideForTesting
            ?? SaveManager.Instance?.GlobalData?.BlockInputMode
            ?? BlockMode.Hold;

        /// <summary>True while a Toggle Block latch holds a player's stance. Test/HUD surface.</summary>
        public bool IsBlockLatched(int playerIndex) =>
            _blockLatches.TryGetValue(playerIndex, out BlockToggleLatch latch) && latch.Latched;

        /// <summary>Drops every Toggle Block latch (a menu, a mode switch, a test).</summary>
        public void ClearBlockLatches() => _blockLatches.Clear();

        /// <summary>
        /// The engine's GUID for a connected joypad, or "" for the keyboard. Cached
        /// per device so the 60 Hz poll path does not allocate a string per
        /// sample; the cache resets on every connection change.
        /// </summary>
        public string JoypadGuid(int deviceId) {
            if (deviceId < 0) return "";
            if (_joypadGuids.TryGetValue(deviceId, out string cached)) return cached;
            string guid = Input.GetJoyGuid(deviceId) ?? "";
            _joypadGuids[deviceId] = guid;
            return guid;
        }

        /// <summary>The stick profile a device samples with (its own GUID entry, else the shared default).</summary>
        public StickProfile StickProfileFor(int deviceId) {
            if (deviceId < 0) return StickProfiles.DesignDefault;
            return StickProfiles.Resolve(SaveManager.Instance?.GlobalData?.StickProfiles, JoypadGuid(deviceId));
        }

        /// <summary>
        /// Processed left stick for one joypad: the radial inner deadzone with
        /// rescale. Zero for the keyboard. Applied before quantization.
        /// </summary>
        private static (float X, float Y) ReadProcessedLeftStick(int deviceId, StickProfile profile) {
            if (deviceId < 0) return (0f, 0f);
            float rawX = Input.GetJoyAxis(deviceId, JoyAxis.LeftX);
            float rawY = Input.GetJoyAxis(deviceId, JoyAxis.LeftY);
            return StickProfiles.ApplyRadialDeadzone(rawX, rawY, (profile ?? StickProfiles.DesignDefault).Deadzone);
        }

        private static PlayerInputFrame SampleGodotDevice(
            uint tick,
            int deviceId,
            GameplayButtons previousHeld,
            ref BlockToggleLatch latch,
            BlockMode blockMode,
            StickProfile stickProfile) {
            if (deviceId == UnassignedDevice) return PlayerInputFrame.Create(tick, 0, 0, GameplayButtons.None, previousHeld);
            StickProfile profile = stickProfile ?? StickProfiles.DesignDefault;
            (float stickX, float stickY) = ReadProcessedLeftStick(deviceId, profile);
            float horizontal = ReadActionStrength(Actions.MoveRight, deviceId, stickX, stickY)
                - ReadActionStrength(Actions.MoveLeft, deviceId, stickX, stickY);
            bool jump = ReadActionPressed(Actions.Jump, deviceId, stickX, stickY);
            // G13: the stick's Down reads against the device's down threshold
            // instead of the action's fixed 0.5 deadzone; digital Down inputs
            // (keys, d-pad) are unchanged.
            bool down = ReadDigitalOrThresholdDown(deviceId, stickX, stickY, profile);
            // Gameplay feel §2.7: the vertical axis is Down minus Up. Jump was
            // removed from it — holding Jump no longer reads as "up", so the
            // directional Warp and the new up-attack are driven by a real Up
            // input (W, stick up, dpad-up) instead of the jump button.
            bool up = ReadActionPressed(Actions.Up, deviceId, stickX, stickY);
            float vertical = (down ? 1.0f : 0.0f) - (up ? 1.0f : 0.0f);

            bool jumpPressed = jump && !previousHeld.HasFlag(GameplayButtons.Jump);
            bool rollRaw = ReadActionPressed(Actions.Roll, deviceId, stickX, stickY);
            bool rollPressed = rollRaw && !previousHeld.HasFlag(GameplayButtons.Roll);
            bool blockRaw = ReadActionPressed(Actions.Block, deviceId, stickX, stickY);
            // G13 (D8(a)): the Toggle latch resolves here, before the chord
            // recognizer, so a latched stance reads as "Block held" everywhere —
            // the grab chord, the tech/escape reads and the sim alike.
            bool blockHeldThisFrame = latch.Step(blockMode, blockRaw, jumpPressed, rollPressed);

            GameplayButtons held = GameplayButtons.None;
            AddIfHeld(ref held, GameplayButtons.Jump, jump);
            AddIfHeld(ref held, GameplayButtons.Down, down);
            AddIfHeld(ref held, GameplayButtons.BasicAttack, ReadActionPressed(Actions.BasicAttack, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Special1, ReadActionPressed(Actions.Special1, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Special2, ReadActionPressed(Actions.Special2, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.MovementAbility, ReadActionPressed(Actions.MovementAbility, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Block, blockHeldThisFrame);
            AddIfHeld(ref held, GameplayButtons.Roll, rollRaw);
            AddIfHeld(ref held, GameplayButtons.Ultimate, ReadUltimatePressed(deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Interact, ReadActionPressed(Actions.Interact, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Pause, ReadActionPressed(Actions.Pause, deviceId, stickX, stickY));

            // C01c combined verbs (Package 11 A1c). Both routes request the SAME
            // verb once: a direct bind, or the preset chord while that chord is
            // enabled for this device kind. Nothing here synthesizes a component
            // press, and a direct bind never disables the chord — the two simply
            // OR into one bit, so a player holding both can only fire once.
            InputDeviceKind deviceKind = deviceId == KeyboardDevice
                ? InputDeviceKind.Keyboard
                : InputDeviceKind.Joypad;
            bool grabShortcut = ShortcutEnabled(InputShortcuts.GrabAction, deviceKind);
            bool echoShortcut = ShortcutEnabled(InputShortcuts.EchoStepAction, deviceKind);
            bool blockHeld = held.HasFlag(GameplayButtons.Block);
            bool directGrab = ReadDirectAction(Actions.Grab, deviceId, stickX, stickY);
            bool directEcho = ReadDirectAction(Actions.EchoStep, deviceId, stickX, stickY);
            AddIfHeld(
                ref held,
                GameplayButtons.Grab,
                directGrab || (grabShortcut && blockHeld && held.HasFlag(GameplayButtons.BasicAttack)));
            AddIfHeld(
                ref held,
                GameplayButtons.EchoStep,
                directEcho || (echoShortcut && blockHeld && held.HasFlag(GameplayButtons.Roll)));
            // The normalized instruction to a peer: "my chords are off, so do not
            // read my component bits as a chord." Protocol v3 carries it so a
            // remote machine's own shortcut settings can never re-recognize this
            // frame differently (see GameplayButtons.DirectOrigin).
            AddIfHeld(ref held, GameplayButtons.DirectOrigin, !grabShortcut && !echoShortcut);

            // G13: a grab chord formed out of a latched stance spends the latch
            // (design: the grab chord is a legal exit). Takes effect next frame.
            if (held.HasFlag(GameplayButtons.Grab) && !previousHeld.HasFlag(GameplayButtons.Grab)) {
                latch.ReleaseAfterGrab();
            }

            return PlayerInputFrame.Create(tick, horizontal, vertical, held, previousHeld);
        }

        /// <summary>
        /// Down, with the G13 threshold. A left-stick-Y Down event reads the
        /// processed stick against the device's down threshold instead of the
        /// action's fixed deadzone; every other Down event (key, d-pad, other axes)
        /// reads exactly as before.
        /// </summary>
        private static bool ReadDigitalOrThresholdDown(int deviceId, float stickX, float stickY, StickProfile profile) {
            Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(Actions.Down);
            using var lifetime = events.AsDisposable();
            float deadzone = InputMap.ActionGetDeadzone(Actions.Down);
            foreach (InputEvent inputEvent in events) {
                if (deviceId == KeyboardDevice) {
                    if (inputEvent is InputEventKey keyEvent) {
                        Key key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
                        if (key != Key.None && Input.IsPhysicalKeyPressed(key)) return true;
                    } else if (inputEvent is InputEventMouseButton mouseEvent
                        && Input.IsMouseButtonPressed(mouseEvent.ButtonIndex)) {
                        return true;
                    }
                } else if (inputEvent is InputEventJoypadButton buttonEvent) {
                    if (Input.IsJoyButtonPressed(deviceId, buttonEvent.ButtonIndex)) return true;
                } else if (inputEvent is InputEventJoypadMotion motionEvent) {
                    if (motionEvent.Axis == JoyAxis.LeftY && motionEvent.AxisValue > 0f) {
                        if (StickProfiles.IsDownHeld(stickY, profile.DownThreshold)) return true;
                        continue;
                    }
                    float axis = ProcessedAxis(deviceId, motionEvent.Axis, stickX, stickY);
                    if (axis * MathF.Sign(motionEvent.AxisValue) >= deadzone) return true;
                }
            }
            return false;
        }

        /// <summary>The left stick reads its processed (deadzoned) value; every other axis reads raw.</summary>
        private static float ProcessedAxis(int deviceId, JoyAxis axis, float stickX, float stickY) => axis switch {
            JoyAxis.LeftX => stickX,
            JoyAxis.LeftY => stickY,
            _ => Input.GetJoyAxis(deviceId, axis)
        };

        /// <summary>
        /// A C01c direct action that the InputMap may not carry yet (the two rows
        /// are authored Unbound, and a project without them at all is a valid
        /// state for older scenes). Missing or unbound reads as "not pressed",
        /// which is exactly a refused direct action: it does nothing and spends
        /// nothing.
        /// </summary>
        private static bool ReadDirectAction(string action, int deviceId, float stickX, float stickY) =>
            InputMap.HasAction(action) && ReadActionPressed(action, deviceId, stickX, stickY);

        /// <summary>
        /// Whether a preset chord is switched on for a device kind. Reads the saved
        /// profile directly: a missing flag — and a missing SaveManager, which is
        /// every headless test — is On, reproducing the shipped chords.
        /// </summary>
        private static bool ShortcutEnabled(string action, InputDeviceKind deviceKind) =>
            SaveManager.Instance?.GlobalData?.InputBindings?.IsShortcutEnabled(action, deviceKind) ?? true;

        /// <summary>
        /// Scratch buffer for <see cref="ReadUltimatePressed"/> (audit M-26): this
        /// runs on the 60 Hz per-player poll path, so it must not allocate a List
        /// per call. Main-thread only, like all input polling.
        /// </summary>
        private static readonly List<JoyButton> UltimateChordScratch = new();

        private static bool ReadUltimatePressed(int deviceId, float stickX, float stickY) {
            if (deviceId == KeyboardDevice) return ReadActionPressed(Actions.Ultimate, deviceId, stickX, stickY);

            UltimateChordScratch.Clear();
            Godot.Collections.Array<InputEvent> ultimateEvents = InputMap.ActionGetEvents(Actions.Ultimate);
            using (ultimateEvents.AsDisposable()) {
                foreach (InputEvent inputEvent in ultimateEvents) {
                    if (inputEvent is InputEventJoypadButton button) UltimateChordScratch.Add(button.ButtonIndex);
                }
            }

            if (UltimateChordScratch.Count <= 1) return ReadActionPressed(Actions.Ultimate, deviceId, stickX, stickY);
            foreach (JoyButton button in UltimateChordScratch) {
                if (!Input.IsJoyButtonPressed(deviceId, button)) return false;
            }
            return true;
        }

        private static bool ReadActionPressed(string action, int deviceId, float stickX, float stickY) {
            return ReadActionStrength(action, deviceId, stickX, stickY) >= InputMap.ActionGetDeadzone(action);
        }

        /// <param name="stickX">The processed left-stick X (the G13 deadzone applied).</param>
        /// <param name="stickY">The processed left-stick Y.</param>
        private static float ReadActionStrength(string action, int deviceId, float stickX, float stickY) {
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
                    // G13: the left stick reads its processed (per-device
                    // deadzoned) value, so every action on it sees one stick.
                    float axis = ProcessedAxis(deviceId, motionEvent.Axis, stickX, stickY);
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
            Actions.Grab => GameplayButtons.Grab,
            Actions.EchoStep => GameplayButtons.EchoStep,
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
