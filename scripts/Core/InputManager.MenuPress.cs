using Godot;

namespace FTT.Core {

    public partial class InputManager {

        // CM-PADUI (2026-10-16): pad-first menus. Since this workstream a pad's A
        // is ui_accept and its B ui_cancel (project.godot), and both are also
        // gameplay buttons (A Jump, B Interact). A menu acts on the press as an
        // input event, while the game reads the same button from device state on
        // the next physics tick — so without this, closing a pause with B, or a
        // dialogue with A, also reached the hero as a fresh Interact or Jump, and
        // pad A on a focused results or prompt button also jumped.

        private bool _menuPressConsumed;
        private uint _menuPressConsumedTick;

        /// <summary>
        /// CM-PADUI (2026-10-16): the press a menu has just acted on must not also
        /// act in the game or in a polled UI underneath. Every slot's sample on the
        /// <b>next</b> physics tick starts from what is held at that moment: a
        /// button already down reaches the game as held, never as a fresh press or
        /// release edge, so it acts again only after it is released and pressed
        /// anew. Held state, sticks and the Toggle Block latch are untouched, and a
        /// slot driven by an input source (the CPU, a bot, a test) is never
        /// touched. Called when a pause or a gameplay-pausing dialogue hands
        /// control back, when a Fighter-select overlay or stage phase closes, and
        /// (<see cref="_Input"/>) when a focused button takes a confirm. It covers
        /// exactly one tick, so a call that no capture follows (a synchronous
        /// test, a paused tree) cannot swallow a later press.
        /// </summary>
        public void ConsumeHeldPresses() {
            _menuPressConsumed = true;
            _menuPressConsumedTick = unchecked((uint)Engine.GetPhysicsFrames() + 1u);
        }

        /// <summary>True when <see cref="ConsumeHeldPresses"/> covers this slot's capture at this tick.</summary>
        private bool ConsumesMenuPress(int playerIndex, uint tick) =>
            _menuPressConsumed && tick == _menuPressConsumedTick && !_overrides.ContainsKey(playerIndex);

        /// <summary>
        /// CM-PADUI (2026-10-16): a <c>ui_accept</c> press that a focused, visible,
        /// enabled button is about to take (pad A, Enter, Space) is the menu's.
        /// Godot's buttons never mark it handled, and the gameplay that may run
        /// underneath a non-pausing surface — the Story results overlay, Level 0's
        /// calibration prompt, a drill card, the Fighter select's polled tokens —
        /// reads the same button as Jump on the next tick, so it is consumed there
        /// (<see cref="ConsumeHeldPresses"/>). This autoload's <c>_input</c> runs
        /// after every scene node's (reverse tree order) and before the GUI, so a
        /// scene that has already claimed the press (a join press on the Fighter
        /// select) is never seen here, and the focus owner read is the one the GUI
        /// is about to hand the press to. While the tree is paused this node gets
        /// no input, and gameplay is not running either; the unpause consumes.
        /// </summary>
        public override void _Input(InputEvent @event) {
            if (FTT.UI.PadMenuInput.FocusedButtonTakesAccept(GetViewport(), @event)) ConsumeHeldPresses();
        }
    }
}
