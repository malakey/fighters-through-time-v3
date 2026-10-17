using Godot;
using System;
using System.Collections.Generic;

namespace FTT.Core {

    /// <summary>
    /// 9.1 (2026-10-16, CM-INPUT; ledger <c>USER-LAST-USED-DEVICE</c>, agent
    /// choices <c>PROVISIONAL-DEVICE-FOLLOW-RULES</c>): how the local slots get
    /// their devices.
    /// </summary>
    public enum LocalJoinMode {
        /// <summary>
        /// The default. Slot 0 follows the most recently used device (the
        /// keyboard or any connected pad) and slot 1 stays unassigned.
        /// </summary>
        FollowLastUsed = 0,

        /// <summary>
        /// Local Versus, from the Fighter select until the flow leaves the
        /// select and the match: no slot is pre-claimed when the join begins
        /// (CM-FIXA-INPUT, 2026-10-16), every claimed slot keeps its device, and
        /// a face, confirm or Interact press on an unclaimed device claims the
        /// lowest free slot — the first device to press is player 1, the next
        /// different one player 2.
        /// </summary>
        Joined = 1
    }

    /// <summary>
    /// The raw per-device input state <see cref="InputManager"/> polls: Godot's
    /// <see cref="Input"/> singleton in production. A test replaces it through
    /// <see cref="InputManager.DeviceActivityProbeForTesting"/> so a fake pad or
    /// keyboard drives both the device-activity poll and the per-device sample —
    /// the press that moves a slot must be the same press the frame then carries.
    /// </summary>
    internal interface IInputDeviceProbe {
        bool IsJoyButtonPressed(int device, JoyButton button);
        float GetJoyAxis(int device, JoyAxis axis);
        bool IsPhysicalKeyPressed(Key key);
        bool IsMouseButtonPressed(MouseButton button);
    }

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
            /// V7.2 manual-rewind action. Default events: keyboard <c>R</c>
            /// (physical keycode 82); gamepad <b>L3</b> (<c>JoyButton.LeftStick</c>,
            /// 7) since 2026-10-11 (user direction D11, ledger
            /// <c>USER-GAMEPAD-TIME-FREEZE-L3</c>), until then Back (4), inherited
            /// from the retired rewind.
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

        // 9.2 (2026-10-16): the pad Ultimate chord's leniency window per player
        // slot, and the last "could cast the Ultimate now" report per slot. Local
        // input processing like the Block latch: never in a snapshot or a hash.
        private readonly Dictionary<int, UltimateChordWindow> _chordWindows = new();
        private readonly Dictionary<int, UltimateReadinessReport> _ultimateReadiness = new();

        // 9.1 (2026-10-16): the join mode, the slots whose next sample is seeded
        // (a joining press must not also act), the slots a disconnect emptied
        // during a local match, and the device-activity poll's snapshots.
        private LocalJoinMode _joinMode = LocalJoinMode.FollowLastUsed;
        private readonly HashSet<int> _seedHeldSlots = new();
        private readonly HashSet<int> _slotsAwaitingReconnect = new();
        private readonly Dictionary<int, uint> _padButtonSnapshots = new();
        private readonly Dictionary<int, bool> _padStickOutSnapshots = new();
        private readonly Dictionary<int, uint> _padAxisOutSnapshots = new();
        private readonly List<Key> _pollKeys = new();
        private readonly List<bool> _pollKeyIsJoin = new();
        private readonly List<bool> _pollKeyPrevious = new();
        private readonly List<MouseButton> _pollMouseButtons = new();
        private readonly List<bool> _pollMouseIsJoin = new();
        private readonly List<bool> _pollMousePrevious = new();
        private readonly List<PolledPadAxis> _pollPadAxes = new();
        private uint _padJoinButtonMask = FaceButtonMask;
        private int _pollInputsGeneration = -1;
        private bool _activityPolled;
        private uint _lastActivityPollTick;

        /// <summary>
        /// Test seam: the Block Mode to use instead of the saved global setting.
        /// Always reset to null in a test's finally block.
        /// </summary>
        internal BlockMode? BlockModeOverrideForTesting;

        /// <summary>
        /// Test seam (9.1): replaces every raw device read — the device-activity
        /// poll and the per-device sample alike — with a fake. Always reset to
        /// null in a test's finally block.
        /// </summary>
        internal IInputDeviceProbe DeviceActivityProbeForTesting {
            get => _probeForTesting;
            set {
                _probeForTesting = value;
                // A new source of device state starts the poll from scratch: its
                // first poll records the state and reports nothing.
                _padButtonSnapshots.Clear();
                _padStickOutSnapshots.Clear();
                _padAxisOutSnapshots.Clear();
                _pollInputsGeneration = -1;
                _activityPolled = false;
            }
        }

        private IInputDeviceProbe _probeForTesting;

        private IInputDeviceProbe Probe => DeviceActivityProbeForTesting ?? GodotInputDeviceProbe.Shared;

        [Export(PropertyHint.Range, "1,2,1")]
        public int MaxPlayers { get; set; } = 2;

        public event Action<int, int> DeviceAssigned;
        public event Action<int> PlayerDeviceDisconnected;

        /// <summary>The device-follow / press-to-join mode in force (9.1).</summary>
        public LocalJoinMode JoinMode => _joinMode;

        public override void _Ready() {
            Instance = this;
            Input.JoyConnectionChanged += OnJoyConnectionChanged;
            RefreshConnectedDevices();
        }

        public override void _ExitTree() {
            Input.JoyConnectionChanged -= OnJoyConnectionChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 9.2: a pause or a window focus loss forgets every chord <b>wait</b>, so
        /// a lone shoulder let go of while away fires nothing on the way back. A
        /// chord's <b>masks</b> survive (CM-FIXA-INPUT, 2026-10-16): LB + RB still
        /// held across a pause must not come back as two fresh component presses.
        /// </summary>
        public override void _Notification(int what) {
            if (what == NotificationPaused || what == NotificationApplicationFocusOut) ForgetChordWaits();
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

            // 9.1: the first capture of a tick polls every device first, so a
            // device switch or a join lands BEFORE the slot is sampled — the
            // switching press then reaches the game as a fresh edge.
            PollDeviceActivity(tick);

            PlayerInputFrame previous = _frames.GetValueOrDefault(playerIndex);
            PlayerInputFrame frame;
            if (_overrides.TryGetValue(playerIndex, out IPlayerInputSource source)) {
                frame = source.Sample(tick, previous);
            } else {
                int deviceId = GetDeviceForPlayer(playerIndex);
                BlockToggleLatch latch = _blockLatches.GetValueOrDefault(playerIndex);
                UltimateChordWindow chord = _chordWindows.GetValueOrDefault(playerIndex);
                frame = SampleGodotDevice(
                    tick, deviceId, previous.Held, ref latch, CurrentBlockMode(), StickProfileFor(deviceId),
                    ref chord, IsUltimateAffordable(playerIndex, tick));
                _blockLatches[playerIndex] = latch;
                _chordWindows[playerIndex] = chord;
            }
            // 9.1: a slot just claimed by a joining press starts from that press
            // already held, so the press joins and does nothing else.
            if (_seedHeldSlots.Remove(playerIndex)) {
                frame.Pressed = GameplayButtons.None;
                frame.Released = GameplayButtons.None;
            }
            // CM-PADUI: a press a menu just acted on starts held the same way
            // (InputManager.MenuPress.cs).
            if (ConsumesMenuPress(playerIndex, tick)) {
                frame.Pressed = GameplayButtons.None;
                frame.Released = GameplayButtons.None;
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

        /// <summary>
        /// Resets to the boot assignment: <see cref="LocalJoinMode.FollowLastUsed"/>
        /// with the keyboard on slot 0 and every other slot unassigned (9.1 — until
        /// 2026-10-16 the connected pads also filled the remaining slots, which made
        /// a lone pad player two).
        /// </summary>
        public void AutoAssignDevices() {
            _deviceToPlayer.Clear();
            _playerToDevice.Clear();
            _frames.Clear();
            _blockLatches.Clear();
            _chordWindows.Clear();
            _seedHeldSlots.Clear();
            _slotsAwaitingReconnect.Clear();
            _joinMode = LocalJoinMode.FollowLastUsed;
            AssignDevice(KeyboardDevice, 0);
        }

        /// <summary>
        /// 9.1: enters <see cref="LocalJoinMode.Joined"/> for Local Versus. Every
        /// slot is emptied — slot 0 included (CM-FIXA-INPUT, 2026-10-16: keeping
        /// the device that last drove the menus left two pads unable to both join
        /// after keyboard- or mouse-driven menus) — and the first face, confirm or
        /// Interact press on an unclaimed device (keyboard and mouse included)
        /// claims the lowest free slot: the first device to press is player 1, the
        /// next different one player 2. An input source keeps its slot. A no-op
        /// while already joined, so Rematch and Back-to-select keep the players'
        /// devices.
        /// </summary>
        public void BeginLocalJoin() {
            if (_joinMode == LocalJoinMode.Joined) return;
            _joinMode = LocalJoinMode.Joined;
            _slotsAwaitingReconnect.Clear();
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) UnassignSlot(playerIndex);
        }

        /// <summary>
        /// 9.1: back to <see cref="LocalJoinMode.FollowLastUsed"/>. Slot 0 keeps its
        /// device and follows from here on; every other slot is emptied. A slot 0
        /// nobody claimed during the join falls back to the keyboard, as an
        /// emptied slot 0 does in <see cref="LocalJoinMode.FollowLastUsed"/>.
        /// </summary>
        public void EndLocalJoin() {
            if (_joinMode == LocalJoinMode.FollowLastUsed) return;
            _joinMode = LocalJoinMode.FollowLastUsed;
            _slotsAwaitingReconnect.Clear();
            for (int playerIndex = 1; playerIndex < MaxPlayers; playerIndex++) UnassignSlot(playerIndex);
            if (!_playerToDevice.ContainsKey(0)) AssignDevice(KeyboardDevice, 0);
        }

        /// <summary>
        /// 9.1 (CM-FIXA-INPUT, 2026-10-16): whether a GUI input event is a join
        /// press — the same set the device poll claims a slot with — from a device
        /// no slot holds, while a local join is open and a slot is free. The press
        /// then claims that slot on the next poll and must do nothing else, so the
        /// Fighter select marks such an event handled in <c>_Input</c>, before the
        /// GUI (a focused button's <c>ui_accept</c>) or its own <c>ui_cancel</c>
        /// can act on it. Classification is by event type (keys and mouse buttons
        /// are the keyboard device, a joypad event its own device), never by
        /// <c>InputEvent.Device</c> alone.
        /// </summary>
        public bool IsJoinPressFromUnclaimedDevice(InputEvent inputEvent) {
            if (_joinMode != LocalJoinMode.Joined || inputEvent == null) return false;
            if (!inputEvent.IsPressed() || inputEvent.IsEcho()) return false;
            int deviceId;
            bool faceButton = false;
            switch (inputEvent) {
                case InputEventKey:
                case InputEventMouseButton:
                    deviceId = KeyboardDevice;
                    break;
                case InputEventJoypadButton button:
                    deviceId = button.Device;
                    int index = (int)button.ButtonIndex;
                    faceButton = index >= 0 && index < PolledPadButtons && (FaceButtonMask & (1u << index)) != 0u;
                    break;
                case InputEventJoypadMotion motion:
                    deviceId = motion.Device;
                    break;
                default:
                    return false;
            }
            if (deviceId != KeyboardDevice && !_connectedJoypads.Contains(deviceId)) return false;
            if (GetPlayerForDevice(deviceId) >= 0 || !HasFreeSlot()) return false;
            if (faceButton) return true;
            foreach (string action in JoinActions) {
                if (InputMap.HasAction(action) && inputEvent.IsActionPressed(action)) return true;
            }
            return false;
        }

        /// <summary>True when some slot has neither an input source nor a device (a join press would claim it).</summary>
        public bool HasFreeSlot() {
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (!IsSlotJoined(playerIndex)) return true;
            }
            return false;
        }

        /// <summary>True when any slot has someone behind it (an input source or an assigned device).</summary>
        public bool AnySlotJoined() {
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (IsSlotJoined(playerIndex)) return true;
            }
            return false;
        }

        /// <summary>
        /// 9.1 join lifetime: a load whose destination is neither the Fighter
        /// select nor a Fighter-VS scene (<see cref="FTT.UI.LoadingScreen.SelectVariant"/>)
        /// ends a local join. Called by <see cref="GameManager.LoadScene"/>.
        /// </summary>
        public void ApplyJoinLifetimeForDestination(string destinationScenePath) {
            if (!DestinationKeepsLocalJoin(destinationScenePath)) EndLocalJoin();
        }

        /// <summary>True when a load to this scene keeps a local join (the select, or a Fighter match).</summary>
        public static bool DestinationKeepsLocalJoin(string destinationScenePath) {
            if (string.IsNullOrWhiteSpace(destinationScenePath)) return false;
            string path = destinationScenePath.Replace('\\', '/');
            return path == FTT.UI.FighterFlowRoutes.CharacterSelectScenePath
                || FTT.UI.LoadingScreen.SelectVariant(path) == FTT.UI.LoadingVariant.FighterVersus;
        }

        /// <summary>
        /// Whether a slot has someone behind it: an input source (a test, the
        /// CPU-driven Mirror clone, a bot) or an assigned device.
        /// </summary>
        public bool IsSlotJoined(int playerIndex) =>
            playerIndex >= 0 && playerIndex < MaxPlayers
            && (_overrides.ContainsKey(playerIndex) || _playerToDevice.ContainsKey(playerIndex));

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
        ///
        /// <para>9.1 (2026-10-16): no slot is filled with a pad just because one
        /// is connected — slot 0 follows the last-used device, and a local join
        /// claims slots by press. What remains: in
        /// <see cref="LocalJoinMode.FollowLastUsed"/> an emptied slot 0 falls back
        /// to the keyboard, and in <see cref="LocalJoinMode.Joined"/> a slot whose
        /// pad dropped mid-session takes the first free pad when one reconnects.</para>
        /// </summary>
        public void ApplyDeviceTopology() {
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (!_playerToDevice.TryGetValue(playerIndex, out int deviceId)) continue;
                if (deviceId == KeyboardDevice || _connectedJoypads.Contains(deviceId)) continue;
                _playerToDevice.Remove(playerIndex);
                _deviceToPlayer.Remove(deviceId);
                _frames.Remove(playerIndex);
                _chordWindows.Remove(playerIndex);
                if (_joinMode == LocalJoinMode.Joined) _slotsAwaitingReconnect.Add(playerIndex);
            }

            if (_joinMode == LocalJoinMode.FollowLastUsed) {
                // The keyboard is a complete first-player device.
                if (!_playerToDevice.ContainsKey(0) && GetPlayerForDevice(KeyboardDevice) < 0) {
                    AssignDevice(KeyboardDevice, 0);
                }
                return;
            }
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (_playerToDevice.ContainsKey(playerIndex)) continue;
                if (!_slotsAwaitingReconnect.Contains(playerIndex)) continue;
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

        /// <summary>Test seam: re-reads the real connected-joypad set after a fake one.</summary>
        internal void RestoreConnectedDevicesForTesting() => RefreshConnectedDevices();

        private void AssignDevice(int deviceId, int playerIndex) {
            ValidatePlayerIndex(playerIndex);

            if (_playerToDevice.TryGetValue(playerIndex, out int oldDevice)) {
                _deviceToPlayer.Remove(oldDevice);
            }
            if (_deviceToPlayer.TryGetValue(deviceId, out int oldPlayer)) {
                _playerToDevice.Remove(oldPlayer);
                _chordWindows.Remove(oldPlayer);
            }

            _deviceToPlayer[deviceId] = playerIndex;
            _playerToDevice[playerIndex] = deviceId;
            _frames.Remove(playerIndex);
            // A new device starts from an unlatched stance: a Toggle Block latch
            // must never survive a controller hand-over — nor a half-pressed
            // Ultimate chord (9.2).
            _blockLatches.Remove(playerIndex);
            _chordWindows.Remove(playerIndex);
            _slotsAwaitingReconnect.Remove(playerIndex);
            DeviceAssigned?.Invoke(playerIndex, deviceId);
        }

        /// <summary>Empties one slot (a join begins or ends). Raises <see cref="DeviceAssigned"/> with <see cref="UnassignedDevice"/>.</summary>
        private void UnassignSlot(int playerIndex) {
            if (!_playerToDevice.TryGetValue(playerIndex, out int deviceId)) return;
            _playerToDevice.Remove(playerIndex);
            _deviceToPlayer.Remove(deviceId);
            _frames.Remove(playerIndex);
            _blockLatches.Remove(playerIndex);
            _chordWindows.Remove(playerIndex);
            _seedHeldSlots.Remove(playerIndex);
            DeviceAssigned?.Invoke(playerIndex, UnassignedDevice);
        }

        // === 9.1 (2026-10-16) — device follow and press-to-join ==============

        /// <summary>The face buttons (A, B, X, Y = 0–3): always a join press.</summary>
        private const uint FaceButtonMask = 0b1111u;

        /// <summary>
        /// Pad buttons the activity poll reads: Godot's SDL set, 0 to
        /// <c>JoyButton.SdlMax</c> − 1 (21 buttons). The left stick is read
        /// through its <see cref="StickProfile"/> deadzone, and every other axis an
        /// action is bound to — the triggers, LT Block and RT Roll at defaults —
        /// through <see cref="PolledPadAxis"/> (CM-FIXA-INPUT, 2026-10-16).
        /// </summary>
        private const int PolledPadButtons = 21;

        /// <summary>At most this many distinct bound axis directions are polled per pad (one bit each).</summary>
        private const int MaxPolledPadAxes = 32;

        /// <summary>
        /// One bound pad axis direction the activity poll reads (CM-FIXA-INPUT,
        /// 2026-10-16): an <see cref="InputEventJoypadMotion"/> of a gameplay,
        /// direct or menu action on any axis but the left stick. It is "out" while
        /// the raw axis read in the bound direction reaches the action's own
        /// deadzone — exactly when <see cref="ReadActionPressed"/> would read the
        /// action held — and crossing out is activity, like a new button press.
        /// </summary>
        private readonly struct PolledPadAxis {
            public PolledPadAxis(JoyAxis axis, float sign, float deadzone, bool join) {
                Axis = axis;
                Sign = sign;
                Deadzone = deadzone;
                Join = join;
            }

            public JoyAxis Axis { get; }
            public float Sign { get; }
            public float Deadzone { get; }
            public bool Join { get; }
        }

        /// <summary>
        /// Actions whose keys and buttons count as a <b>join</b> press: confirm,
        /// Interact, and Basic Attack (the select screen's own confirm).
        /// </summary>
        private static readonly string[] JoinActions = {
            "ui_accept", Actions.Interact, Actions.BasicAttack
        };

        /// <summary>The menu actions whose keys also count as keyboard activity.</summary>
        private static readonly string[] PolledMenuActions = {
            "ui_accept", "ui_cancel", "ui_up", "ui_down", "ui_left", "ui_right", Actions.Pause, Actions.DialogueLog
        };

        /// <summary>Mouse buttons that always count as keyboard-and-mouse activity. Motion never does.</summary>
        private static readonly MouseButton[] PolledMouseButtons = {
            MouseButton.Left, MouseButton.Right, MouseButton.Middle
        };

        /// <summary>
        /// The device-activity poll, run once per tick by the first capture. It
        /// compares every device's state with its last poll — no input events are
        /// read, so a press a scene already marked handled still counts, and so
        /// does one made while the tree is paused (then the poll runs only when a
        /// paused UI captures a frame). A pad counts on a new button press, its
        /// left stick crossing out of its <see cref="StickProfile"/> deadzone, or
        /// (CM-FIXA-INPUT, 2026-10-16) any other bound axis — the LT Block and RT
        /// Roll triggers at defaults — crossing out of its action's deadzone; the
        /// keyboard on a new press of a key bound to a gameplay or menu action, or
        /// of a mouse button. Classification is by device kind, never by
        /// <c>InputEvent.Device</c> (Godot gives keys and pad 0 the same index).
        ///
        /// <para><see cref="LocalJoinMode.FollowLastUsed"/>: slot 0 moves to the
        /// active device (a slot with an input source is left alone).
        /// <see cref="LocalJoinMode.Joined"/>: each unclaimed device that made a
        /// join press (a mouse button bound to a join action included) claims the
        /// lowest free slot, and that slot's next sample is seeded so the joining
        /// press does nothing else.</para>
        /// </summary>
        private void PollDeviceActivity(uint tick) {
            if (_activityPolled && tick == _lastActivityPollTick) return;
            bool firstPoll = !_activityPolled;
            _activityPolled = true;
            _lastActivityPollTick = tick;
            IInputDeviceProbe probe = Probe;
            RefreshPolledInputsIfNeeded(probe);

            bool keyboardActive = false;
            bool keyboardJoin = false;
            for (int index = 0; index < _pollKeys.Count; index++) {
                bool pressed = probe.IsPhysicalKeyPressed(_pollKeys[index]);
                if (pressed && !_pollKeyPrevious[index]) {
                    keyboardActive = true;
                    if (_pollKeyIsJoin[index]) keyboardJoin = true;
                }
                _pollKeyPrevious[index] = pressed;
            }
            for (int index = 0; index < _pollMouseButtons.Count; index++) {
                bool pressed = probe.IsMouseButtonPressed(_pollMouseButtons[index]);
                if (pressed && !_pollMousePrevious[index]) {
                    keyboardActive = true;
                    // A mouse button bound to a join action (the left click is
                    // Basic Attack) joins the keyboard-and-mouse device.
                    if (_pollMouseIsJoin[index]) keyboardJoin = true;
                }
                _pollMousePrevious[index] = pressed;
            }
            if (firstPoll) {
                keyboardActive = false;
                keyboardJoin = false;
            }

            int activePad = UnassignedDevice;
            bool currentPadActive = false;
            int current = _playerToDevice.TryGetValue(0, out int slotZero) ? slotZero : UnassignedDevice;
            _joinCandidates.Clear();
            foreach (int deviceId in _connectedJoypads) {
                uint mask = 0u;
                for (int button = 0; button < PolledPadButtons; button++) {
                    if (probe.IsJoyButtonPressed(deviceId, (JoyButton)button)) mask |= 1u << button;
                }
                (float stickX, float stickY) = ReadProcessedLeftStick(deviceId, StickProfileFor(deviceId));
                bool stickOut = stickX != 0f || stickY != 0f;
                uint axisMask = 0u;
                for (int axis = 0; axis < _pollPadAxes.Count; axis++) {
                    PolledPadAxis polled = _pollPadAxes[axis];
                    if (probe.GetJoyAxis(deviceId, polled.Axis) * polled.Sign >= polled.Deadzone) axisMask |= 1u << axis;
                }
                bool known = _padButtonSnapshots.TryGetValue(deviceId, out uint previousMask);
                bool previousStickOut = _padStickOutSnapshots.GetValueOrDefault(deviceId);
                uint previousAxisMask = _padAxisOutSnapshots.GetValueOrDefault(deviceId);
                _padButtonSnapshots[deviceId] = mask;
                _padStickOutSnapshots[deviceId] = stickOut;
                _padAxisOutSnapshots[deviceId] = axisMask;
                // A pad's first poll only records it: something already held when
                // it appeared is not a new press.
                if (!known) continue;
                uint newPresses = mask & ~previousMask;
                uint newAxes = axisMask & ~previousAxisMask;
                bool active = newPresses != 0u || newAxes != 0u || (stickOut && !previousStickOut);
                if (!active) continue;
                if (deviceId == current) currentPadActive = true;
                if (activePad == UnassignedDevice) activePad = deviceId;
                if ((newPresses & _padJoinButtonMask) != 0u || AnyJoinAxis(newAxes)) _joinCandidates.Add(deviceId);
            }

            if (_joinMode == LocalJoinMode.FollowLastUsed) {
                if (_overrides.ContainsKey(0)) return;
                // The device already on slot 0 keeps it when several move at once.
                if (current == KeyboardDevice ? keyboardActive : currentPadActive) return;
                int next = keyboardActive ? KeyboardDevice : activePad;
                if (next != UnassignedDevice && next != current) AssignDevice(next, 0);
                return;
            }

            if (keyboardJoin && GetPlayerForDevice(KeyboardDevice) < 0) ClaimLowestFreeSlot(KeyboardDevice);
            foreach (int deviceId in _joinCandidates) {
                if (GetPlayerForDevice(deviceId) < 0) ClaimLowestFreeSlot(deviceId);
            }
        }

        private readonly List<int> _joinCandidates = new();

        /// <summary>Whether any newly crossed axis in <paramref name="newAxes"/> is bound to a join action.</summary>
        private bool AnyJoinAxis(uint newAxes) {
            if (newAxes == 0u) return false;
            for (int axis = 0; axis < _pollPadAxes.Count; axis++) {
                if ((newAxes & (1u << axis)) != 0u && _pollPadAxes[axis].Join) return true;
            }
            return false;
        }

        private void ClaimLowestFreeSlot(int deviceId) {
            for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++) {
                if (_overrides.ContainsKey(playerIndex) || _playerToDevice.ContainsKey(playerIndex)) continue;
                AssignDevice(deviceId, playerIndex);
                _seedHeldSlots.Add(playerIndex);
                return;
            }
        }

        /// <summary>
        /// Rebuilds the polled keyboard/mouse inputs when a binding changed
        /// (<see cref="InputBindingService.Generation"/>). Their snapshots are
        /// re-read from the current state, so a rebuild never reads as a press.
        /// </summary>
        private void RefreshPolledInputsIfNeeded(IInputDeviceProbe probe) {
            if (_pollInputsGeneration == InputBindingService.Generation && _pollKeys.Count > 0) return;
            _pollInputsGeneration = InputBindingService.Generation;
            _pollKeys.Clear();
            _pollKeyIsJoin.Clear();
            _pollKeyPrevious.Clear();
            _pollMouseButtons.Clear();
            _pollMouseIsJoin.Clear();
            _pollMousePrevious.Clear();
            _pollPadAxes.Clear();
            _padJoinButtonMask = FaceButtonMask;
            foreach (MouseButton button in PolledMouseButtons) AddPolledMouseButton(button, join: false);
            foreach (string action in RemappableActions) AddPolledAction(action);
            foreach (string action in InputShortcuts.DirectActions) AddPolledAction(action);
            foreach (string action in PolledMenuActions) AddPolledAction(action);
            for (int index = 0; index < _pollKeys.Count; index++) {
                _pollKeyPrevious.Add(probe.IsPhysicalKeyPressed(_pollKeys[index]));
            }
            for (int index = 0; index < _pollMouseButtons.Count; index++) {
                _pollMousePrevious.Add(probe.IsMouseButtonPressed(_pollMouseButtons[index]));
            }
            // The axis bits index the rebuilt list, so every known pad's axis
            // snapshot is re-read from its current state: a trigger already held
            // when a binding changed is not a new press.
            foreach (int deviceId in _connectedJoypads) {
                if (!_padButtonSnapshots.ContainsKey(deviceId)) continue;
                uint axisMask = 0u;
                for (int axis = 0; axis < _pollPadAxes.Count; axis++) {
                    PolledPadAxis polled = _pollPadAxes[axis];
                    if (probe.GetJoyAxis(deviceId, polled.Axis) * polled.Sign >= polled.Deadzone) axisMask |= 1u << axis;
                }
                _padAxisOutSnapshots[deviceId] = axisMask;
            }
        }

        private void AddPolledAction(string action) {
            if (!InputMap.HasAction(action)) return;
            bool join = Array.IndexOf(JoinActions, action) >= 0;
            Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(action);
            using var lifetime = events.AsDisposable();
            foreach (InputEvent inputEvent in events) {
                switch (inputEvent) {
                    case InputEventKey keyEvent: {
                        Key key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
                        if (key == Key.None) break;
                        int existing = _pollKeys.IndexOf(key);
                        if (existing < 0) {
                            _pollKeys.Add(key);
                            _pollKeyIsJoin.Add(join);
                        } else if (join) {
                            _pollKeyIsJoin[existing] = true;
                        }
                        break;
                    }
                    case InputEventMouseButton mouseEvent:
                        AddPolledMouseButton(mouseEvent.ButtonIndex, join);
                        break;
                    case InputEventJoypadButton buttonEvent:
                        int index = (int)buttonEvent.ButtonIndex;
                        if (join && index >= 0 && index < PolledPadButtons) _padJoinButtonMask |= 1u << index;
                        break;
                    case InputEventJoypadMotion motionEvent:
                        AddPolledPadAxis(motionEvent, InputMap.ActionGetDeadzone(action), join);
                        break;
                }
            }
        }

        private void AddPolledMouseButton(MouseButton button, bool join) {
            int existing = _pollMouseButtons.IndexOf(button);
            if (existing < 0) {
                _pollMouseButtons.Add(button);
                _pollMouseIsJoin.Add(join);
            } else if (join) {
                _pollMouseIsJoin[existing] = true;
            }
        }

        /// <summary>
        /// Adds one bound axis direction to the poll (the left stick excepted: it
        /// is read through the device's stick profile). The same direction bound
        /// twice is polled once, at the smaller deadzone, joining if either action does.
        /// </summary>
        private void AddPolledPadAxis(InputEventJoypadMotion motionEvent, float deadzone, bool join) {
            if (motionEvent.Axis == JoyAxis.LeftX || motionEvent.Axis == JoyAxis.LeftY) return;
            float sign = MathF.Sign(motionEvent.AxisValue);
            if (sign == 0f) return;
            for (int index = 0; index < _pollPadAxes.Count; index++) {
                PolledPadAxis existing = _pollPadAxes[index];
                if (existing.Axis != motionEvent.Axis || existing.Sign != sign) continue;
                _pollPadAxes[index] = new PolledPadAxis(
                    existing.Axis, sign, MathF.Min(existing.Deadzone, deadzone), existing.Join || join);
                return;
            }
            if (_pollPadAxes.Count >= MaxPolledPadAxes) return;
            _pollPadAxes.Add(new PolledPadAxis(motionEvent.Axis, sign, deadzone, join));
        }

        // === 9.2 (2026-10-16) — Ultimate readiness for the chord window ======

        /// <summary>A readiness report older than this many ticks reads as "not affordable".</summary>
        public const int UltimateReadinessMaxAgeTicks = 2;

        private readonly struct UltimateReadinessReport {
            public UltimateReadinessReport(bool affordable, uint tick) {
                Affordable = affordable;
                Tick = tick;
            }

            public bool Affordable { get; }
            public uint Tick { get; }
        }

        /// <summary>
        /// 9.2: the fighter on <paramref name="playerIndex"/> reports whether it
        /// could cast its Ultimate right now — the meter full and the cast neither
        /// locked nor otherwise refused, the ability lockout deliberately excluded
        /// (inside the lockout the chord presses only the refused, flashing
        /// Ultimate). <c>PlayerController</c> reports every Story tick and
        /// <c>FighterSimulationDriver</c> every local Fighter tick, for human slots
        /// only. A stale report (older than <see cref="UltimateReadinessMaxAgeTicks"/>)
        /// reads as not affordable, so the chord window then passes everything through.
        /// </summary>
        public void ReportUltimateAffordable(int playerIndex, bool affordable) {
            if (playerIndex < 0 || playerIndex >= MaxPlayers) return;
            _ultimateReadiness[playerIndex] =
                new UltimateReadinessReport(affordable, unchecked((uint)Engine.GetPhysicsFrames()));
        }

        /// <summary>Whether the last readiness report for the slot is fresh and affordable at <paramref name="tick"/>.</summary>
        public bool IsUltimateAffordable(int playerIndex, uint tick) {
            if (!_ultimateReadiness.TryGetValue(playerIndex, out UltimateReadinessReport report)) return false;
            if (!report.Affordable) return false;
            uint age = unchecked(tick - report.Tick);
            return age <= UltimateReadinessMaxAgeTicks;
        }

        /// <summary>True while a lone chord component on this slot is waiting for its partner. Test surface.</summary>
        public bool IsChordWaiting(int playerIndex) =>
            _chordWindows.TryGetValue(playerIndex, out UltimateChordWindow window) && window.IsWaiting;

        /// <summary>
        /// Forgets every chord <b>wait</b> and keeps every chord <b>mask</b> (pause,
        /// focus loss; CM-FIXA-INPUT, 2026-10-16). A masked component stays masked
        /// until it is released, so LB + RB held across a pause fire no component
        /// on the way back; a device hand-over still drops the whole window.
        /// </summary>
        public void ForgetChordWaits() {
            if (_chordWindows.Count == 0) return;
            _chordWindowSlotScratch.Clear();
            _chordWindowSlotScratch.AddRange(_chordWindows.Keys);
            foreach (int slot in _chordWindowSlotScratch) {
                UltimateChordWindow window = _chordWindows[slot];
                window.ForgetWaits();
                _chordWindows[slot] = window;
            }
        }

        private readonly List<int> _chordWindowSlotScratch = new();

        /// <summary>The production probe: Godot's <see cref="Input"/> singleton.</summary>
        private sealed class GodotInputDeviceProbe : IInputDeviceProbe {
            public static readonly GodotInputDeviceProbe Shared = new();
            public bool IsJoyButtonPressed(int device, JoyButton button) => Input.IsJoyButtonPressed(device, button);
            public float GetJoyAxis(int device, JoyAxis axis) => Input.GetJoyAxis(device, axis);
            public bool IsPhysicalKeyPressed(Key key) => Input.IsPhysicalKeyPressed(key);
            public bool IsMouseButtonPressed(MouseButton button) => Input.IsMouseButtonPressed(button);
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
        private (float X, float Y) ReadProcessedLeftStick(int deviceId, StickProfile profile) {
            if (deviceId < 0) return (0f, 0f);
            float rawX = Probe.GetJoyAxis(deviceId, JoyAxis.LeftX);
            float rawY = Probe.GetJoyAxis(deviceId, JoyAxis.LeftY);
            return StickProfiles.ApplyRadialDeadzone(rawX, rawY, (profile ?? StickProfiles.DesignDefault).Deadzone);
        }

        private PlayerInputFrame SampleGodotDevice(
            uint tick,
            int deviceId,
            GameplayButtons previousHeld,
            ref BlockToggleLatch latch,
            BlockMode blockMode,
            StickProfile stickProfile,
            ref UltimateChordWindow chordWindow,
            bool ultimateAffordable) {
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

            InputDeviceKind deviceKind = deviceId == KeyboardDevice
                ? InputDeviceKind.Keyboard
                : InputDeviceKind.Joypad;

            // 9.2 (2026-10-16): the gamepad Ultimate is the C01c recipe — the
            // Movement Ability and Special 2 components' live bindings, behind the
            // pad's Ultimate shortcut flag — resolved through the leniency window,
            // which may hold a lone component back for a few frames while the
            // Ultimate is affordable. A direct bind (the slot is authored empty;
            // C01c capture may fill it) and the legacy multi-event conjunction OR
            // in. The keyboard has no Ultimate chord and is never windowed.
            bool movementRaw = ReadActionPressed(Actions.MovementAbility, deviceId, stickX, stickY);
            bool special2Raw = ReadActionPressed(Actions.Special2, deviceId, stickX, stickY);
            bool ultimateHeld = ReadUltimatePressed(deviceId, stickX, stickY);
            if (deviceKind == InputDeviceKind.Joypad) {
                UltimateChordWindow.Output chord = chordWindow.Step(
                    movementRaw,
                    special2Raw,
                    ShortcutEnabled(InputShortcuts.UltimateAction, InputDeviceKind.Joypad),
                    ultimateAffordable);
                movementRaw = chord.MovementHeld;
                special2Raw = chord.Special2Held;
                ultimateHeld |= chord.UltimateHeld;
            }

            GameplayButtons held = GameplayButtons.None;
            AddIfHeld(ref held, GameplayButtons.Jump, jump);
            AddIfHeld(ref held, GameplayButtons.Down, down);
            AddIfHeld(ref held, GameplayButtons.BasicAttack, ReadActionPressed(Actions.BasicAttack, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Special1, ReadActionPressed(Actions.Special1, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Special2, special2Raw);
            AddIfHeld(ref held, GameplayButtons.MovementAbility, movementRaw);
            AddIfHeld(ref held, GameplayButtons.Block, blockHeldThisFrame);
            AddIfHeld(ref held, GameplayButtons.Roll, rollRaw);
            AddIfHeld(ref held, GameplayButtons.Ultimate, ultimateHeld);
            AddIfHeld(ref held, GameplayButtons.Interact, ReadActionPressed(Actions.Interact, deviceId, stickX, stickY));
            AddIfHeld(ref held, GameplayButtons.Pause, ReadActionPressed(Actions.Pause, deviceId, stickX, stickY));

            // C01c combined verbs (Package 11 A1c). Both routes request the SAME
            // verb once: a direct bind, or the preset chord while that chord is
            // enabled for this device kind. Nothing here synthesizes a component
            // press, and a direct bind never disables the chord — the two simply
            // OR into one bit, so a player holding both can only fire once.
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
        private bool ReadDigitalOrThresholdDown(int deviceId, float stickX, float stickY, StickProfile profile) {
            Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(Actions.Down);
            using var lifetime = events.AsDisposable();
            float deadzone = InputMap.ActionGetDeadzone(Actions.Down);
            foreach (InputEvent inputEvent in events) {
                if (deviceId == KeyboardDevice) {
                    if (inputEvent is InputEventKey keyEvent) {
                        Key key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
                        if (key != Key.None && Probe.IsPhysicalKeyPressed(key)) return true;
                    } else if (inputEvent is InputEventMouseButton mouseEvent
                        && Probe.IsMouseButtonPressed(mouseEvent.ButtonIndex)) {
                        return true;
                    }
                } else if (inputEvent is InputEventJoypadButton buttonEvent) {
                    if (Probe.IsJoyButtonPressed(deviceId, buttonEvent.ButtonIndex)) return true;
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
        private float ProcessedAxis(int deviceId, JoyAxis axis, float stickX, float stickY) => axis switch {
            JoyAxis.LeftX => stickX,
            JoyAxis.LeftY => stickY,
            _ => Probe.GetJoyAxis(deviceId, axis)
        };

        /// <summary>
        /// A C01c direct action that the InputMap may not carry yet (the two rows
        /// are authored Unbound, and a project without them at all is a valid
        /// state for older scenes). Missing or unbound reads as "not pressed",
        /// which is exactly a refused direct action: it does nothing and spends
        /// nothing.
        /// </summary>
        private bool ReadDirectAction(string action, int deviceId, float stickX, float stickY) =>
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

        /// <summary>
        /// The Ultimate action's <b>direct</b> route. On the keyboard that is its
        /// key (U). On a pad, since 9.2 (2026-10-16), the direct slot is authored
        /// empty (C01c's "initially Unbound") and the chord comes from the recipe's
        /// components through <see cref="UltimateChordWindow"/>; a single captured
        /// pad button still reads here, and an action that carries two or more pad
        /// buttons (an unrepaired legacy row) is still read as their conjunction.
        /// </summary>
        private bool ReadUltimatePressed(int deviceId, float stickX, float stickY) {
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
                if (!Probe.IsJoyButtonPressed(deviceId, button)) return false;
            }
            return true;
        }

        private bool ReadActionPressed(string action, int deviceId, float stickX, float stickY) {
            return ReadActionStrength(action, deviceId, stickX, stickY) >= InputMap.ActionGetDeadzone(action);
        }

        /// <param name="stickX">The processed left-stick X (the G13 deadzone applied).</param>
        /// <param name="stickY">The processed left-stick Y.</param>
        private float ReadActionStrength(string action, int deviceId, float stickX, float stickY) {
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
                        if (key != Key.None && Probe.IsPhysicalKeyPressed(key)) strength = 1.0f;
                    } else if (inputEvent is InputEventMouseButton mouseEvent
                        && Probe.IsMouseButtonPressed(mouseEvent.ButtonIndex)) {
                        strength = 1.0f;
                    }
                } else if (inputEvent is InputEventJoypadButton buttonEvent) {
                    if (Probe.IsJoyButtonPressed(deviceId, buttonEvent.ButtonIndex)) strength = 1.0f;
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
