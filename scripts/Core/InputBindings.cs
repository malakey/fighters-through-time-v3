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

        /// <summary>
        /// Package 11 A8 / C01c. Slots the player deliberately cleared, keyed
        /// <c>action|deviceKind</c> through <see cref="SlotKey"/>.
        ///
        /// <para><b>Why a parallel set.</b> C01c requires that "an explicit
        /// Unbound value differs from a missing override that inherits defaults",
        /// and <see cref="Normalize"/> deletes any action row whose event list
        /// came out empty — deliberately, because an empty row is exactly what a
        /// corrupt payload produces. An empty row therefore cannot carry that
        /// meaning. This set survives normalization and is the only
        /// representation of "cleared on purpose".</para>
        ///
        /// <para>Keyed per device kind because C01c keeps keyboard/mouse and
        /// gamepad overrides independent: clearing Ultimate's gamepad slot must
        /// not clear its keyboard <c>U</c>.</para>
        /// </summary>
        public HashSet<string> UnboundActions = new(StringComparer.Ordinal);

        /// <summary>
        /// Package 11 A8 / C01c. Enable flags for the fixed preset shortcuts
        /// (Ultimate = MovementAbility + Special 2, Grab = Block held + Basic
        /// Attack, Echo Step = Block + Roll), keyed <c>action|deviceKind</c>.
        ///
        /// <para>A missing entry means "legacy payload" and reproduces today's
        /// behaviour — the shortcut is <b>On</b>. That default lives in
        /// <see cref="IsShortcutEnabled"/> rather than being materialized at load,
        /// so a payload the player never touched keeps inheriting the shipped
        /// answer if it ever changes.</para>
        /// </summary>
        public Dictionary<string, bool> ShortcutEnabled = new(StringComparer.Ordinal);

        /// <summary>The <c>action|deviceKind</c> key both C01c maps use.</summary>
        public static string SlotKey(string action, InputDeviceKind deviceKind) =>
            $"{action}|{(int)deviceKind}";

        /// <summary>Splits a <see cref="SlotKey"/> back into its parts.</summary>
        public static bool TrySplitSlotKey(string key, out string action, out InputDeviceKind deviceKind) {
            action = "";
            deviceKind = InputDeviceKind.Keyboard;
            if (string.IsNullOrWhiteSpace(key)) return false;
            int separator = key.LastIndexOf('|');
            if (separator <= 0 || separator == key.Length - 1) return false;
            if (!int.TryParse(key[(separator + 1)..], out int kind)) return false;
            if (!Enum.IsDefined(typeof(InputDeviceKind), kind)) return false;
            action = key[..separator];
            deviceKind = (InputDeviceKind)kind;
            return true;
        }

        /// <summary>True when the player explicitly cleared this action's slot on this device kind.</summary>
        public bool IsUnbound(string action, InputDeviceKind deviceKind) =>
            !string.IsNullOrWhiteSpace(action)
            && UnboundActions != null
            && UnboundActions.Contains(SlotKey(action, deviceKind));

        /// <summary>
        /// Records (or lifts) an explicit Unbound. Setting it also drops that
        /// device kind's events from the override row: the slot is empty, not
        /// merely unlisted.
        /// </summary>
        public void SetUnbound(string action, InputDeviceKind deviceKind, bool unbound) {
            if (string.IsNullOrWhiteSpace(action)) return;
            UnboundActions ??= new HashSet<string>(StringComparer.Ordinal);
            string key = SlotKey(action, deviceKind);
            if (!unbound) {
                UnboundActions.Remove(key);
                return;
            }
            UnboundActions.Add(key);
            StripDeviceKind(action, deviceKind);
        }

        /// <summary>
        /// Whether a preset shortcut is active for a device kind. A missing flag
        /// is On, reproducing the shipped chords for a legacy payload.
        /// </summary>
        public bool IsShortcutEnabled(string action, InputDeviceKind deviceKind) {
            if (string.IsNullOrWhiteSpace(action) || ShortcutEnabled == null) return true;
            return !ShortcutEnabled.TryGetValue(SlotKey(action, deviceKind), out bool enabled) || enabled;
        }

        public void SetShortcutEnabled(string action, InputDeviceKind deviceKind, bool enabled) {
            if (string.IsNullOrWhiteSpace(action)) return;
            ShortcutEnabled ??= new Dictionary<string, bool>(StringComparer.Ordinal);
            ShortcutEnabled[SlotKey(action, deviceKind)] = enabled;
        }

        /// <summary>
        /// Forgets every C01c choice for one action on one device kind. C01c's
        /// per-action reset restores that action's direct default <b>and</b> its
        /// preset flags for the selected device, which is exactly this plus a
        /// rewrite of the action's event row.
        /// </summary>
        public void ResetSlot(string action, InputDeviceKind deviceKind) {
            if (string.IsNullOrWhiteSpace(action)) return;
            UnboundActions?.Remove(SlotKey(action, deviceKind));
            ShortcutEnabled?.Remove(SlotKey(action, deviceKind));
        }

        /// <summary>Events for one action limited to one device kind.</summary>
        public List<InputBindingEvent> EventsFor(string action, InputDeviceKind deviceKind) =>
            For(action).Where(existing => existing.DeviceKind == deviceKind)
                .Select(existing => existing.Clone()).ToList();

        private void StripDeviceKind(string action, InputDeviceKind deviceKind) {
            if (Actions == null || !Actions.TryGetValue(action, out List<InputBindingEvent> events)) return;
            List<InputBindingEvent> kept = events.Where(existing => existing.DeviceKind != deviceKind).ToList();
            if (kept.Count == 0) Actions.Remove(action);
            else Actions[action] = kept;
        }

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
            NormalizeC01cMaps();
        }

        /// <summary>
        /// C01c hygiene. Malformed keys are dropped; an explicit Unbound slot
        /// additionally strips that device kind's events out of the override row,
        /// so the two halves of the payload can never disagree about one slot.
        /// </summary>
        private void NormalizeC01cMaps() {
            UnboundActions ??= new HashSet<string>(StringComparer.Ordinal);
            ShortcutEnabled ??= new Dictionary<string, bool>(StringComparer.Ordinal);

            var cleanedUnbound = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in UnboundActions) {
                if (!TrySplitSlotKey(key, out string action, out InputDeviceKind deviceKind)) continue;
                cleanedUnbound.Add(SlotKey(action, deviceKind));
                StripDeviceKind(action, deviceKind);
            }
            UnboundActions = cleanedUnbound;

            var cleanedShortcuts = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, bool> pair in ShortcutEnabled) {
                if (!TrySplitSlotKey(pair.Key, out string action, out InputDeviceKind deviceKind)) continue;
                cleanedShortcuts[SlotKey(action, deviceKind)] = pair.Value;
            }
            ShortcutEnabled = cleanedShortcuts;
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

        public void Clear() {
            Actions = new Dictionary<string, List<InputBindingEvent>>();
            UnboundActions = new HashSet<string>(StringComparer.Ordinal);
            ShortcutEnabled = new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        public InputBindingSet Clone() {
            var clone = new InputBindingSet();
            if (UnboundActions != null) {
                clone.UnboundActions = new HashSet<string>(UnboundActions, StringComparer.Ordinal);
            }
            if (ShortcutEnabled != null) {
                clone.ShortcutEnabled = new Dictionary<string, bool>(ShortcutEnabled, StringComparer.Ordinal);
            }
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
    /// Package 11 A8 / C01c. The fixed preset shortcuts and the reachability rule
    /// that keeps a control profile usable.
    ///
    /// <para>C01c is explicit that there is <b>no chord editor</b>: each shortcut's
    /// action recipe is fixed and only its component actions are remappable, so a
    /// label like "LB+RB" describes today's defaults rather than hard-coded keys.
    /// This class owns the recipes and the "is this verb reachable at all" check;
    /// recognizing a chord at runtime, the two new InputMap actions, the wire bits
    /// and protocol v3 belong to the Echo Step / input workstream (A1c).</para>
    ///
    /// <para>The three direct action names are string literals rather than
    /// <c>InputManager.Actions</c> constants on purpose: two of them
    /// (<c>gameplay_grab</c>, <c>gameplay_echo_step</c>) land with A1c and
    /// <c>gameplay_time_freeze</c> lands with A2, so every consumer here tolerates
    /// an action the InputMap does not have yet and simply renders no row for it.</para>
    /// </summary>
    public static class InputShortcuts {

        /// <summary>Direct Ultimate action (already in the InputMap as an LB+RB chord reader).</summary>
        public const string UltimateAction = "gameplay_ultimate";

        /// <summary>Direct Grab action. Added by A1c; absent until then.</summary>
        public const string GrabAction = "gameplay_grab";

        /// <summary>Direct Echo Step action. Added by A1c; absent until then.</summary>
        public const string EchoStepAction = "gameplay_echo_step";

        /// <summary>Direct Time Freeze action (A2's rename of <c>gameplay_rewind</c>). Story only.</summary>
        public const string TimeFreezeAction = "gameplay_time_freeze";

        /// <summary>
        /// Actions C01c adds direct binding slots for, in the order the Controls
        /// tab lists them after the fourteen pre-existing rows.
        /// </summary>
        public static readonly string[] DirectActions = {
            UltimateAction, GrabAction, EchoStepAction, TimeFreezeAction
        };

        /// <summary>
        /// Actions whose route must stay reachable on a device profile — either a
        /// direct bind or a valid enabled preset. Time Freeze is deliberately not
        /// here: it is Story-only and has no shortcut, so an unbound Time Freeze
        /// is a Story-profile warning rather than an invalid layout.
        /// </summary>
        public static readonly string[] RequiredReachableActions = {
            UltimateAction, GrabAction, EchoStepAction
        };

        /// <summary>One fixed preset recipe: a combined verb and the component actions that spell it.</summary>
        public sealed class Recipe {
            public Recipe(string action, string labelKey, params string[] components) {
                Action = action;
                LabelKey = labelKey;
                Components = components;
            }

            /// <summary>The combined verb the recipe requests.</summary>
            public string Action { get; }

            /// <summary>Translation key naming the shortcut in the Controls tab.</summary>
            public string LabelKey { get; }

            /// <summary>The component actions, in the order the label reads them.</summary>
            public string[] Components { get; }
        }

        /// <summary>
        /// The three authored recipes. Ultimate's is gamepad-only by design —
        /// C01c adds no new keyboard Ultimate chord — which is why
        /// <see cref="AppliesTo"/> exists rather than a flat per-device loop.
        /// </summary>
        public static readonly Recipe[] Recipes = {
            new(UltimateAction, "controls_shortcut_ultimate",
                InputManager.Actions.MovementAbility, InputManager.Actions.Special2),
            new(GrabAction, "controls_shortcut_grab",
                InputManager.Actions.Block, InputManager.Actions.BasicAttack),
            new(EchoStepAction, "controls_shortcut_echo_step",
                InputManager.Actions.Block, InputManager.Actions.Roll)
        };

        /// <summary>The recipe for an action, or null when it has none.</summary>
        public static Recipe RecipeFor(string action) {
            foreach (Recipe recipe in Recipes) {
                if (recipe.Action == action) return recipe;
            }
            return null;
        }

        /// <summary>
        /// Whether a recipe is offered on a device kind. Only the Ultimate chord
        /// is restricted: C01c enables the gamepad MovementAbility + Special 2
        /// recipe by default and adds no keyboard equivalent.
        /// </summary>
        public static bool AppliesTo(Recipe recipe, InputDeviceKind deviceKind) =>
            recipe != null && (recipe.Action != UltimateAction || deviceKind == InputDeviceKind.Joypad);

        /// <summary>Translation key naming a C01c direct action in the remap UI.</summary>
        public static string ActionLabelKey(string action) => action switch {
            UltimateAction => "controls_action_ultimate",
            GrabAction => "controls_action_grab",
            EchoStepAction => "controls_action_echo_step",
            TimeFreezeAction => "controls_action_time_freeze",
            _ => InputManager.ActionLabelKey(action)
        };

        /// <summary>
        /// True when <paramref name="action"/> has a usable direct binding on this
        /// device kind: at least one event of that kind and no explicit Unbound.
        /// </summary>
        public static bool HasDirectRoute(InputBindingSet effective, string action, InputDeviceKind deviceKind) {
            if (effective == null || string.IsNullOrWhiteSpace(action)) return false;
            if (effective.IsUnbound(action, deviceKind)) return false;
            foreach (InputBindingEvent binding in effective.For(action)) {
                if (binding != null && binding.DeviceKind == deviceKind) return true;
            }
            return false;
        }

        /// <summary>
        /// True when the preset route works on this device kind: the recipe applies
        /// there, its flag is On, and every component action has a binding of that
        /// kind. A component the player unbound breaks the chord, which is the
        /// case C01c wants explained rather than silently accepted.
        /// </summary>
        public static bool HasShortcutRoute(InputBindingSet effective, string action, InputDeviceKind deviceKind) {
            Recipe recipe = RecipeFor(action);
            if (effective == null || recipe == null || !AppliesTo(recipe, deviceKind)) return false;
            if (!effective.IsShortcutEnabled(action, deviceKind)) return false;
            foreach (string component in recipe.Components) {
                if (!HasDirectRoute(effective, component, deviceKind)) return false;
            }
            return true;
        }

        /// <summary>
        /// The actions left with no route at all on a device kind. C01c: "Do not
        /// silently leave Ultimate/Grab/Echo Step unreachable when their shortcut
        /// is disabled. Explain the missing route and keep the last valid
        /// configuration." The Settings screen renders this list and refuses to
        /// commit while it is non-empty.
        /// </summary>
        public static List<string> UnreachableActions(InputBindingSet effective, InputDeviceKind deviceKind) {
            var unreachable = new List<string>();
            foreach (string action in RequiredReachableActions) {
                // An action the InputMap does not carry yet is not configurable,
                // so it cannot be unreachable either.
                if (!InputMap.HasAction(action)) continue;
                if (HasDirectRoute(effective, action, deviceKind)) continue;
                if (HasShortcutRoute(effective, action, deviceKind)) continue;
                unreachable.Add(action);
            }
            return unreachable;
        }

        /// <summary>
        /// Validates a whole proposed profile across both device kinds before it is
        /// committed — C01c's "validate the entire proposed profile before
        /// committing it". True when every required verb keeps a route.
        /// </summary>
        public static bool ProfileIsReachable(InputBindingSet effective, out List<string> unreachable) {
            unreachable = new List<string>();
            foreach (InputDeviceKind deviceKind in new[] { InputDeviceKind.Keyboard, InputDeviceKind.Joypad }) {
                foreach (string action in UnreachableActions(effective, deviceKind)) {
                    if (!unreachable.Contains(action)) unreachable.Add(action);
                }
            }
            return unreachable.Count == 0;
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
        /// navigation.
        ///
        /// <para>Package 11 A8 / C01c: built from
        /// <see cref="InputManager.RemappableActions"/> <b>plus</b>
        /// <see cref="InputShortcuts.DirectActions"/>. The set is what
        /// <see cref="ApplyAction"/> filters against, so an action missing from it
        /// has its restore silently dropped — which is precisely how Ultimate's
        /// saved bind would have been lost when it moved out of the read-only
        /// list.</para>
        /// </summary>
        private static readonly HashSet<string> RestorableActions = BuildRestorableActions();

        private static HashSet<string> BuildRestorableActions() {
            var set = new HashSet<string>(InputManager.RemappableActions, StringComparer.Ordinal);
            foreach (string action in InputShortcuts.DirectActions) set.Add(action);
            return set;
        }

        /// <summary>
        /// Every action the Controls tab offers a direct binding slot for, in
        /// display order: the fourteen pre-existing rows then C01c's additions,
        /// de-duplicated (Ultimate now lives in both lists) and filtered to what
        /// the InputMap actually carries, so a row for an action A1c or A2 has not
        /// landed yet is simply absent rather than broken.
        /// </summary>
        public static List<string> BindableActions() {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string action in InputManager.RemappableActions) {
                if (seen.Add(action) && InputMap.HasAction(action)) ordered.Add(action);
            }
            foreach (string action in InputShortcuts.DirectActions) {
                if (seen.Add(action) && InputMap.HasAction(action)) ordered.Add(action);
            }
            return ordered;
        }

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
        public static InputBindingSet CaptureEffective() => CaptureEffective(null);

        /// <summary>
        /// Reads the live InputMap and carries the saved C01c choices forward.
        /// The map cannot express "explicitly Unbound" or a shortcut flag — those
        /// live only in the payload — so the Controls tab hands its persisted set
        /// in and gets a working set that remembers them.
        /// </summary>
        public static InputBindingSet CaptureEffective(InputBindingSet persisted) {
            var set = new InputBindingSet();
            foreach (string action in BindableActions()) {
                List<InputBindingEvent> events = CaptureAction(action);
                if (events.Count > 0) set.Set(action, events);
            }
            if (persisted?.UnboundActions != null) {
                set.UnboundActions = new HashSet<string>(persisted.UnboundActions, StringComparer.Ordinal);
            }
            if (persisted?.ShortcutEnabled != null) {
                set.ShortcutEnabled = new Dictionary<string, bool>(persisted.ShortcutEnabled, StringComparer.Ordinal);
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
            if (overrides == null) return;
            overrides.Normalize();
            if (overrides.Actions != null) {
                foreach (KeyValuePair<string, List<InputBindingEvent>> pair in overrides.Actions) {
                    ApplyAction(pair.Key, pair.Value);
                }
            }
            // C01c: an explicit Unbound is a real instruction, not a missing row.
            // Applied after the override rows so an action that carries both a
            // keyboard override and a cleared gamepad slot ends up with exactly
            // the keyboard half in the map.
            if (overrides.UnboundActions == null) return;
            foreach (string key in overrides.UnboundActions) {
                if (!InputBindingSet.TrySplitSlotKey(key, out string action, out InputDeviceKind deviceKind)) continue;
                ApplyUnboundSlot(overrides, action, deviceKind);
            }
        }

        /// <summary>
        /// Writes one deliberately cleared slot: the action keeps whatever the
        /// other device kind resolves to (its override row when one exists,
        /// otherwise the project default) and loses every event of the cleared
        /// kind. Erasing outright would take the untouched device with it.
        /// </summary>
        private static void ApplyUnboundSlot(
            InputBindingSet overrides, string action, InputDeviceKind deviceKind) {
            if (string.IsNullOrWhiteSpace(action) || !InputMap.HasAction(action)) return;
            if (!RestorableActions.Contains(action)) return;
            InputDeviceKind otherKind = deviceKind == InputDeviceKind.Keyboard
                ? InputDeviceKind.Joypad
                : InputDeviceKind.Keyboard;
            List<InputBindingEvent> survivors = overrides.IsUnbound(action, otherKind)
                ? new List<InputBindingEvent>()
                : overrides.HasOverride(action)
                    ? overrides.EventsFor(action, otherKind)
                    : _projectDefaults.EventsFor(action, otherKind);
            ApplyAction(action, survivors, allowEmpty: true);
        }

        /// <summary>
        /// Erase-and-add for a single action. Unknown actions are ignored, and so
        /// is anything outside <see cref="InputManager.RemappableActions"/> — the
        /// restore path previously trusted any action name in the saved payload
        /// (audit §5.2). Events beyond <see cref="MaxEventsPerAction"/> are dropped.
        /// </summary>
        public static void ApplyAction(string action, IReadOnlyList<InputBindingEvent> events) =>
            ApplyAction(action, events, allowEmpty: false);

        /// <summary>
        /// The empty-list overload. <paramref name="allowEmpty"/> separates
        /// "corrupt payload, ignore it" (the default, and the reason the guard
        /// below exists at all) from C01c's deliberate clear, which must be able
        /// to leave an action with no events.
        /// </summary>
        public static void ApplyAction(
            string action, IReadOnlyList<InputBindingEvent> events, bool allowEmpty) {
            if (string.IsNullOrWhiteSpace(action) || !InputMap.HasAction(action)) return;
            if (!RestorableActions.Contains(action)) return;
            if (events == null || (events.Count == 0 && !allowEmpty)) return;
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
            if (effective == null) return overrides;
            if (effective.Actions != null) {
                foreach (string action in BindableActions()) {
                    if (!effective.HasOverride(action)) continue;
                    if (!effective.DiffersFrom(_projectDefaults, action)) continue;
                    overrides.Set(action, effective.For(action));
                }
            }
            // C01c choices are never "the same as the default" — an explicit
            // Unbound and an Off shortcut are both departures from the shipped
            // profile, so they are persisted verbatim rather than diffed away.
            if (effective.UnboundActions != null) {
                overrides.UnboundActions = new HashSet<string>(effective.UnboundActions, StringComparer.Ordinal);
            }
            if (effective.ShortcutEnabled != null) {
                overrides.ShortcutEnabled =
                    new Dictionary<string, bool>(effective.ShortcutEnabled, StringComparer.Ordinal);
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
