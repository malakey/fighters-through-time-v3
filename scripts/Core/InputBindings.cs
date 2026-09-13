using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FTT.Core {

    /// <summary>
    /// The four event shapes an InputMap action can carry in this project. Every
    /// authored default in <c>project.godot</c> is one of these, so a saved
    /// override can express anything the project ships with.
    /// </summary>
    public enum InputBindingKind {
        Key = 0,
        MouseButton = 1,
        JoyButton = 2,
        JoyAxis = 3
    }

    /// <summary>Which physical device family an event belongs to.</summary>
    public enum InputDeviceKind {
        Keyboard = 0,
        Joypad = 1
    }

    /// <summary>
    /// One serializable InputMap event (Package 8 A4).
    ///
    /// <para><b>Why a structure and not a string.</b> The dead
    /// <c>Dictionary&lt;string,string&gt;</c> this replaces could not represent an
    /// action's real binding: <c>gameplay_move_left</c> alone ships four events
    /// (two keys, one joypad axis, one joypad button). Keys are stored as
    /// <b>physical</b> keycodes so a remap survives a keyboard-layout change,
    /// matching <see cref="InputManager"/>'s polling, which prefers
    /// <c>PhysicalKeycode</c>.</para>
    /// </summary>
    public sealed class InputBindingEvent {
        public InputBindingKind Kind = InputBindingKind.Key;

        /// <summary>Physical keycode, mouse button index, joy button index, or joy axis index.</summary>
        public int Code;

        /// <summary>-1 or +1 for <see cref="InputBindingKind.JoyAxis"/>; 0 for every other kind.</summary>
        public int AxisSign;

        public InputBindingEvent() { }

        public InputBindingEvent(InputBindingKind kind, int code, int axisSign = 0) {
            Kind = kind;
            Code = code;
            AxisSign = axisSign;
        }

        /// <summary>Clamps the payload into a shape the engine can consume.</summary>
        public void Normalize() {
            if (!Enum.IsDefined(typeof(InputBindingKind), Kind)) Kind = InputBindingKind.Key;
            Code = Math.Max(0, Code);
            if (Kind == InputBindingKind.JoyAxis) {
                AxisSign = AxisSign < 0 ? -1 : 1;
            } else {
                AxisSign = 0;
            }
        }

        /// <summary>False for payloads that cannot become a usable InputEvent.</summary>
        public bool IsValid => Code > 0
            || (Kind == InputBindingKind.JoyButton && Code == 0)
            || (Kind == InputBindingKind.JoyAxis && Code == 0);

        public InputDeviceKind DeviceKind =>
            Kind is InputBindingKind.Key or InputBindingKind.MouseButton
                ? InputDeviceKind.Keyboard
                : InputDeviceKind.Joypad;

        public bool Matches(InputBindingEvent other) =>
            other != null && other.Kind == Kind && other.Code == Code && other.AxisSign == AxisSign;

        public InputBindingEvent Clone() => new(Kind, Code, AxisSign);

        /// <summary>Stable identity used for equality/sorting in diffs and conflict scans.</summary>
        public string Signature => $"{(int)Kind}:{Code}:{AxisSign}";
    }

    /// <summary>
    /// A whole set of per-action binding events. Persisted inside the encrypted
    /// global save envelope only — binding data never leaves it.
    /// </summary>
    public sealed class InputBindingSet {
        public Dictionary<string, List<InputBindingEvent>> Actions = new();

        /// <summary>Drops empty/invalid rows and duplicate events, and clamps every event.</summary>
        public void Normalize() {
            Actions ??= new Dictionary<string, List<InputBindingEvent>>();
            var cleaned = new Dictionary<string, List<InputBindingEvent>>();
            foreach (KeyValuePair<string, List<InputBindingEvent>> pair in Actions) {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                var events = new List<InputBindingEvent>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (InputBindingEvent candidate in pair.Value) {
                    if (candidate == null) continue;
                    candidate.Normalize();
                    if (!candidate.IsValid) continue;
                    if (!seen.Add(candidate.Signature)) continue;
                    events.Add(candidate);
                }
                if (events.Count == 0) continue;
                cleaned[pair.Key.Trim()] = events;
            }
            Actions = cleaned;
        }

        public bool HasOverride(string action) =>
            !string.IsNullOrWhiteSpace(action) && Actions != null && Actions.ContainsKey(action);

        public IReadOnlyList<InputBindingEvent> For(string action) =>
            action != null && Actions != null && Actions.TryGetValue(action, out List<InputBindingEvent> events)
                ? events
                : Array.Empty<InputBindingEvent>();

        public void Set(string action, IEnumerable<InputBindingEvent> events) {
            if (string.IsNullOrWhiteSpace(action)) return;
            Actions ??= new Dictionary<string, List<InputBindingEvent>>();
            Actions[action] = events == null
                ? new List<InputBindingEvent>()
                : events.Where(candidate => candidate != null).Select(candidate => candidate.Clone()).ToList();
        }

        public void Remove(string action) {
            if (string.IsNullOrWhiteSpace(action)) return;
            Actions?.Remove(action);
        }

        public void Clear() => Actions = new Dictionary<string, List<InputBindingEvent>>();

        public InputBindingSet Clone() {
            var clone = new InputBindingSet();
            if (Actions == null) return clone;
            foreach (KeyValuePair<string, List<InputBindingEvent>> pair in Actions) {
                clone.Set(pair.Key, pair.Value);
            }
            return clone;
        }

        /// <summary>
        /// Package 11 A2 (F03 / C-2): renames a saved <c>gameplay_rewind</c>
        /// override onto <c>gameplay_time_freeze</c>.
        ///
        /// <para>The V7.2 manual-rewind action was retired and replaced by Time
        /// Freeze, which ships with <i>exactly the same</i> default events. A
        /// player who never remapped it therefore has nothing stored and needs no
        /// migration (<see cref="InputBindingService.BuildOverrides"/> only saves
        /// rows that differ from the project defaults) — but a player who DID
        /// remap it has a row keyed by the dead action name, which
        /// <see cref="InputBindingService.ApplyAction"/> now silently drops
        /// because the name is no longer in <c>RemappableActions</c>. That is the
        /// binding this moves across.</para>
        ///
        /// <para><b>An explicit newer Time Freeze bind always wins.</b> If the
        /// payload already carries a <c>gameplay_time_freeze</c> row the legacy
        /// row is discarded rather than applied, so a player who rebound the new
        /// action cannot have it overwritten by their old one.</para>
        ///
        /// <para>Returns true when the set changed. Pure data work: the Phase C
        /// closeout calls it from the single v5-to-v6 migration step.</para>
        /// </summary>
        public bool MigrateLegacyRewindAction() {
            if (Actions == null || !Actions.TryGetValue(InputManager.Actions.LegacyRewind,
                    out List<InputBindingEvent> legacy)) {
                return false;
            }
            Actions.Remove(InputManager.Actions.LegacyRewind);
            if (Actions.ContainsKey(InputManager.Actions.TimeFreeze)) return true;
            if (legacy == null || legacy.Count == 0) return true;
            Set(InputManager.Actions.TimeFreeze, legacy);
            return true;
        }

        /// <summary>True when this action's events differ from the supplied reference set.</summary>
        public bool DiffersFrom(InputBindingSet reference, string action) {
            IReadOnlyList<InputBindingEvent> mine = For(action);
            IReadOnlyList<InputBindingEvent> theirs = reference?.For(action) ?? Array.Empty<InputBindingEvent>();
            if (mine.Count != theirs.Count) return true;
            for (int index = 0; index < mine.Count; index++) {
                if (!mine[index].Matches(theirs[index])) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Conflict rules for the remap UI (Package 8 A4). Pure logic, no engine
    /// dependency, so it is testable without a scene tree.
    ///
    /// <para><b>Decision: block, do not swap.</b> The local action space is
    /// single-player — one physical event may drive exactly one action. A swap
    /// would silently move a binding the player never asked to change (and would
    /// have to invent a slot when the two actions hold different event counts),
    /// so a conflicting capture is refused with a localized explanation naming the
    /// action that already owns the event.</para>
    /// </summary>
    public static class InputBindingConflicts {
        /// <summary>
        /// Returns the action that already owns <paramref name="candidate"/>, or an
        /// empty string when the capture is free to apply. Re-binding an event the
        /// same action already holds reports that action, so the UI can explain the
        /// no-op rather than silently duplicating the event.
        /// </summary>
        public static string FindConflictingAction(
            InputBindingSet effective,
            string action,
            InputBindingEvent candidate) {
            if (effective?.Actions == null || candidate == null || string.IsNullOrWhiteSpace(action)) return "";
            candidate.Normalize();
            if (!candidate.IsValid) return "";
            foreach (KeyValuePair<string, List<InputBindingEvent>> pair in effective.Actions) {
                if (pair.Value == null) continue;
                foreach (InputBindingEvent existing in pair.Value) {
                    if (existing != null && existing.Matches(candidate)) return pair.Key;
                }
            }
            return "";
        }

        /// <summary>True when the capture may be written into the working set.</summary>
        public static bool IsFree(InputBindingSet effective, string action, InputBindingEvent candidate) =>
            FindConflictingAction(effective, action, candidate).Length == 0;
    }

    /// <summary>
    /// The engine bridge for <see cref="InputBindingSet"/>: captures the project
    /// defaults once, reads the live InputMap, applies saved overrides, and resets
    /// to the project settings.
    ///
    /// <para><b>Restore order.</b> <see cref="InputManager"/> is an autoload that
    /// loads <i>before</i> <see cref="SaveManager"/>, so the push into the InputMap
    /// lives on the SaveManager side and runs immediately after the global payload
    /// loads — before any gameplay scene polls an action.
    /// <see cref="InputManager.ReadActionStrength"/> reads
    /// <c>InputMap.ActionGetEvents</c> live, so a restored override needs no further
    /// plumbing.</para>
    /// </summary>
    public static class InputBindingService {
        private static InputBindingSet _projectDefaults;

        /// <summary>
        /// Hard cap on events applied per action from a saved payload (audit H-2
        /// blast radius). The largest authored default (move left/right) ships
        /// four events; anything far beyond that is tampered or corrupt data.
        /// </summary>
        public const int MaxEventsPerAction = 8;

        /// <summary>
        /// The only actions the restore path may touch. Mirrors the write-path
        /// filter: a tampered global payload must not rebind <c>ui_*</c>
        /// navigation or the deliberately read-only <c>gameplay_ultimate</c> chord.
        /// </summary>
        private static readonly HashSet<string> RestorableActions =
            new(InputManager.RemappableActions, StringComparer.Ordinal);

        /// <summary>The project.godot bindings, snapshotted before any override applied.</summary>
        public static InputBindingSet ProjectDefaults {
            get {
                EnsureDefaultsCaptured();
                return _projectDefaults;
            }
        }

        /// <summary>
        /// Snapshots the current InputMap as the project defaults exactly once per
        /// process. Must run before <see cref="Apply"/> has ever mutated the map.
        /// </summary>
        public static void EnsureDefaultsCaptured() {
            _projectDefaults ??= CaptureEffective();
        }

        /// <summary>Test seam: forgets the snapshot so a suite can re-capture a clean map.</summary>
        internal static void ResetCapturedDefaultsForTesting() => _projectDefaults = null;

        /// <summary>Reads the live InputMap for every rebindable and read-only action.</summary>
        public static InputBindingSet CaptureEffective() {
            var set = new InputBindingSet();
            foreach (string action in InputManager.RemappableActions) {
                List<InputBindingEvent> events = CaptureAction(action);
                if (events.Count > 0) set.Set(action, events);
            }
            return set;
        }

        /// <summary>Reads one action's live events, skipping shapes we cannot serialize.</summary>
        public static List<InputBindingEvent> CaptureAction(string action) {
            var events = new List<InputBindingEvent>();
            if (string.IsNullOrWhiteSpace(action) || !InputMap.HasAction(action)) return events;
            Godot.Collections.Array<InputEvent> actionEvents = InputMap.ActionGetEvents(action);
            using var lifetime = actionEvents.AsDisposable();
            foreach (InputEvent inputEvent in actionEvents) {
                InputBindingEvent converted = FromInputEvent(inputEvent);
                if (converted != null) events.Add(converted);
            }
            return events;
        }

        /// <summary>
        /// Pushes saved overrides into the InputMap: erase then re-add, per
        /// overridden action. Actions absent from the set keep their project
        /// defaults untouched.
        /// </summary>
        public static void Apply(InputBindingSet overrides) {
            EnsureDefaultsCaptured();
            if (overrides?.Actions == null) return;
            overrides.Normalize();
            foreach (KeyValuePair<string, List<InputBindingEvent>> pair in overrides.Actions) {
                ApplyAction(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Erase-and-add for a single action. Unknown actions are ignored, and so
        /// is anything outside <see cref="InputManager.RemappableActions"/> — the
        /// restore path previously trusted any action name in the saved payload
        /// (audit §5.2). Events beyond <see cref="MaxEventsPerAction"/> are dropped.
        /// </summary>
        public static void ApplyAction(string action, IReadOnlyList<InputBindingEvent> events) {
            if (string.IsNullOrWhiteSpace(action) || !InputMap.HasAction(action)) return;
            if (!RestorableActions.Contains(action)) return;
            if (events == null || events.Count == 0) return;
            InputMap.ActionEraseEvents(action);
            int applied = 0;
            foreach (InputBindingEvent binding in events) {
                if (applied >= MaxEventsPerAction) break;
                InputEvent inputEvent = ToInputEvent(binding);
                if (inputEvent == null) continue;
                InputMap.ActionAddEvent(action, inputEvent);
                applied++;
            }
        }

        /// <summary>Restores every action from project.godot. Pairs with clearing the saved set.</summary>
        public static void ResetToProjectDefaults() {
            EnsureDefaultsCaptured();
            InputMap.LoadFromProjectSettings();
        }

        /// <summary>Restores one action from the captured project defaults.</summary>
        public static void ResetActionToProjectDefault(string action) {
            EnsureDefaultsCaptured();
            IReadOnlyList<InputBindingEvent> defaults = _projectDefaults.For(action);
            if (defaults.Count == 0) return;
            ApplyAction(action, defaults);
        }

        /// <summary>
        /// Reduces a full effective map to only the actions that differ from the
        /// project defaults, so a later change to project.godot still reaches
        /// players who never touched that action.
        /// </summary>
        public static InputBindingSet BuildOverrides(InputBindingSet effective) {
            EnsureDefaultsCaptured();
            var overrides = new InputBindingSet();
            if (effective?.Actions == null) return overrides;
            foreach (string action in InputManager.RemappableActions) {
                if (!effective.HasOverride(action)) continue;
                if (!effective.DiffersFrom(_projectDefaults, action)) continue;
                overrides.Set(action, effective.For(action));
            }
            overrides.Normalize();
            return overrides;
        }

        public static InputBindingEvent FromInputEvent(InputEvent inputEvent) {
            switch (inputEvent) {
                case InputEventKey keyEvent: {
                    Key key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
                    return key == Key.None ? null : new InputBindingEvent(InputBindingKind.Key, (int)key);
                }
                case InputEventMouseButton mouseEvent:
                    return new InputBindingEvent(InputBindingKind.MouseButton, (int)mouseEvent.ButtonIndex);
                case InputEventJoypadButton buttonEvent:
                    return new InputBindingEvent(InputBindingKind.JoyButton, (int)buttonEvent.ButtonIndex);
                case InputEventJoypadMotion motionEvent:
                    return new InputBindingEvent(
                        InputBindingKind.JoyAxis,
                        (int)motionEvent.Axis,
                        motionEvent.AxisValue < 0f ? -1 : 1);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Builds the engine event. Device is -1 ("any device"), matching every
        /// authored default in project.godot; <see cref="InputManager"/> resolves the
        /// physical device itself rather than trusting the event's device field.
        /// </summary>
        public static InputEvent ToInputEvent(InputBindingEvent binding) {
            if (binding == null) return null;
            binding.Normalize();
            if (!binding.IsValid) return null;
            switch (binding.Kind) {
                case InputBindingKind.Key:
                    return new InputEventKey { Device = -1, PhysicalKeycode = (Key)binding.Code };
                case InputBindingKind.MouseButton:
                    return new InputEventMouseButton { Device = -1, ButtonIndex = (MouseButton)binding.Code };
                case InputBindingKind.JoyButton:
                    return new InputEventJoypadButton { Device = -1, ButtonIndex = (JoyButton)binding.Code };
                case InputBindingKind.JoyAxis:
                    return new InputEventJoypadMotion {
                        Device = -1,
                        Axis = (JoyAxis)binding.Code,
                        AxisValue = binding.AxisSign
                    };
                default:
                    return null;
            }
        }

        /// <summary>Human-readable label for a bound event; used by the remap rows.</summary>
        public static string Describe(InputBindingEvent binding) {
            if (binding == null) return TranslationServer.Translate("controls_binding_unbound");
            binding.Normalize();
            switch (binding.Kind) {
                case InputBindingKind.Key:
                    return OS.GetKeycodeString((Key)binding.Code);
                case InputBindingKind.MouseButton:
                    return string.Format(
                        TranslationServer.Translate("controls_binding_mouse"), binding.Code);
                case InputBindingKind.JoyButton:
                    return string.Format(
                        TranslationServer.Translate("controls_binding_joy_button"), binding.Code);
                case InputBindingKind.JoyAxis:
                    return string.Format(
                        TranslationServer.Translate("controls_binding_joy_axis"),
                        binding.Code,
                        binding.AxisSign < 0 ? "-" : "+");
                default:
                    return TranslationServer.Translate("controls_binding_unbound");
            }
        }

        /// <summary>Comma-joined description of an action's whole event list.</summary>
        public static string DescribeAll(IReadOnlyList<InputBindingEvent> events) {
            if (events == null || events.Count == 0) {
                return TranslationServer.Translate("controls_binding_unbound");
            }
            return string.Join(", ", events.Select(Describe));
        }
    }
}
