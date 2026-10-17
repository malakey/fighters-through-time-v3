using FTT.Core;
using Godot;

namespace FTT.UI {

    /// <summary>
    /// CM-PADUI (2026-10-16; decision 9.1, pad-only play first-class): the
    /// Fighter select's footer (Move List, Systems Card, Back) for a pad.
    ///
    /// <para>Since CM-PADUI pad A is <c>ui_accept</c> and pad B <c>ui_cancel</c>
    /// (project.godot), so a pad can press the focused footer button. But the
    /// same pad drives a polled token: the stick and d-pad move it (and are also
    /// Godot's built-in <c>ui_left</c> / <c>ui_right</c> / <c>ui_down</c> focus
    /// moves), A steps it up (Jump) and B locks it (Interact). Two rules keep one
    /// press doing one thing:</para>
    /// <list type="bullet">
    /// <item><b>The footer is player 1's.</b> In a local join, once a device holds
    /// slot 0, a GUI confirm or focus move from any other device that is also one
    /// of that player's token inputs (pad A, the stick and d-pad, Space, the
    /// arrow keys) is swallowed before the GUI sees it; the polled flow reads
    /// device state, so that player's own token still moves. Player 2's A is only
    /// ever player 2's token Up, never player 1's Back. A press that is only a
    /// menu key (Enter, Tab) still reaches the GUI, as CM-FIXA-INPUT
    /// pinned.</item>
    /// <item><b>Up walks the footer, any token input leaves it.</b> Up (the
    /// stick, the d-pad, the arrow key) is the one direction the token flow does
    /// not read, so it walks player 1's focus round the wrapping chain (the
    /// notice, then Back, Systems Card, Move List) and A presses the focused
    /// button. Every token input player 1 makes — a stick or d-pad step, Down,
    /// Jump, a confirm, a cancel — hands the focus back to the notice, so a focus
    /// move that rode along with a token step never leaves A pointing at
    /// Back.</item>
    /// </list>
    /// <para>A press a focused button takes is consumed for the polled flow by
    /// <see cref="InputManager"/>'s <c>_Input</c>, so A on Back never also steps
    /// the token.</para>
    /// </summary>
    public partial class CharacterSelectScreen {

        // The GUI actions a pad or key press drives a focused control with.
        private static readonly string[] MenuDriveActions = {
            PadMenuInput.AcceptAction, "ui_up", "ui_down", "ui_left", "ui_right", "ui_focus_next", "ui_focus_prev"
        };

        // The gameplay actions the polled token flow reads, plus Up (the stick and
        // d-pad's fourth direction, never a menu intent from another player).
        private static readonly string[] TokenActions = {
            InputManager.Actions.MoveLeft, InputManager.Actions.MoveRight, InputManager.Actions.Down,
            InputManager.Actions.Up, InputManager.Actions.Jump, InputManager.Actions.BasicAttack,
            InputManager.Actions.Interact, InputManager.Actions.Block, InputManager.Actions.Roll
        };

        /// <summary>
        /// CM-PADUI (2026-10-16): whether Player 1's panel shows the press-to-join
        /// prompt — a Local Versus select whose slot 0 nobody has claimed yet (no
        /// device pressed, no input source). Never in Versus CPU or a Holodeck
        /// select, where player 1 follows the last-used device. Test seam.
        /// </summary>
        public bool IsPlayerOneJoinPromptShown =>
            _origin == FighterMatchOrigin.LocalVersus
            && InputManager.Instance != null
            && !InputManager.Instance.IsSlotJoined(0);

        /// <summary>
        /// True when <paramref name="inputEvent"/> is a GUI confirm or focus move
        /// that is also a token input, from a key or a pad that is not player 1's,
        /// during the token flow of a local join whose slot 0 a device holds. Mouse
        /// input is never swallowed (a pointer is unambiguous), nor is anything
        /// while a test or bot input source drives slot 0.
        /// </summary>
        private bool IsAnotherPlayersMenuPress(InputEvent inputEvent) {
            if (_phase == SelectScreenPhase.StageSelect || inputEvent == null) return false;
            InputManager input = InputManager.Instance;
            if (input == null || input.JoinMode != LocalJoinMode.Joined) return false;
            int deviceId;
            switch (inputEvent) {
                case InputEventKey:
                    deviceId = InputManager.KeyboardDevice;
                    break;
                case InputEventJoypadButton:
                case InputEventJoypadMotion:
                    deviceId = inputEvent.Device;
                    break;
                default:
                    return false;
            }
            int playerOneDevice = input.GetDeviceForPlayer(0);
            if (playerOneDevice == InputManager.UnassignedDevice || playerOneDevice == deviceId) return false;
            return MatchesAny(inputEvent, MenuDriveActions) && MatchesAny(inputEvent, TokenActions);
        }

        private static bool MatchesAny(InputEvent inputEvent, string[] actions) {
            foreach (string action in actions) {
                if (InputMap.HasAction(action) && inputEvent.IsAction(action)) return true;
            }
            return false;
        }

        /// <summary>
        /// Player 1 used their token: a footer button that holds focus hands it
        /// back to the chain's first entry (the Remote Play notice), so A is the
        /// token's Up again. Up itself is not a token input, so it never calls
        /// this and can walk the footer.
        /// </summary>
        private void ReleaseFooterFocusOnTokenInput(in PlayerInputFrame frame, float previousHorizontal) {
            bool tokenInput = frame.IsPressed(GameplayButtons.Block)
                || frame.IsPressed(GameplayButtons.Roll)
                || frame.IsPressed(GameplayButtons.BasicAttack)
                || frame.IsPressed(GameplayButtons.Interact)
                || frame.IsPressed(GameplayButtons.Down)
                || frame.IsPressed(GameplayButtons.Jump)
                || AxisStep(previousHorizontal, frame.Horizontal) != 0;
            if (!tokenInput) return;
            if (GetViewport()?.GuiGetFocusOwner() is not BaseButton focused) return;
            if (focused.GetParent() != GetNodeOrNull(SelectRoot + "ButtonRow")) return;
            FocusChain[0]?.GrabFocus();
        }

        /// <summary>
        /// CM-PADUI (2026-10-16): an onboarding overlay closed — usually on pad B,
        /// which is ui_cancel and also the token flow's Interact confirm. The
        /// polled flow resumes on the next tick and must not lock a token with the
        /// press that closed the overlay.
        /// </summary>
        private static void OnOverlayClosed() => InputManager.Instance?.ConsumeHeldPresses();
    }
}
