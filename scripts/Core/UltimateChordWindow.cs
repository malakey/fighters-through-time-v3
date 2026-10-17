namespace FTT.Core {

    /// <summary>
    /// 9.2 (2026-10-16, CM-INPUT; ledger <c>USER-PAD-ULTIMATE-CHORD-LENIENCY</c>,
    /// agent choices <c>PROVISIONAL-ULTIMATE-CHORD-LENIENCY</c>): the gamepad
    /// Ultimate chord's leniency window, a pure input-layer state machine in the
    /// <see cref="BlockToggleLatch"/> pattern. <see cref="InputManager"/> steps one
    /// per joypad slot, before <see cref="PlayerInputFrame.Create"/>, so nothing
    /// here reaches the deterministic simulation, a snapshot, a hash or the wire.
    ///
    /// <para>The chord is the C01c recipe Movement Ability + Special 2 (LB + RB at
    /// defaults), read from the two components' live bindings. Until 2026-10-16
    /// the pad Ultimate was the Ultimate action's own two joypad events read as a
    /// conjunction, and nothing held the components back: LB a frame before RB
    /// cast the movement ability, and the chord inside the ability lockout cast
    /// it too (<c>VERIFY-GAMEPAD-ULTIMATE-CHORD</c> (a)).</para>
    ///
    /// <para><b>While the Ultimate is affordable</b> (and the chord shortcut is
    /// on): a lone component pressed while its partner is up is held back for
    /// <see cref="LeniencyFrames"/> frames. If the partner arrives, only the
    /// Ultimate is held and both components stay masked until each is released.
    /// If the window runs out, the component is emitted held, at most
    /// <see cref="LeniencyFrames"/> frames late; if it is released inside the
    /// window, it is emitted held for exactly one frame, so a tap is never lost.
    /// <b>Otherwise</b> the bits pass through as before, except that a component
    /// still masked by a chord stays masked until it is released, so letting go
    /// of a spent chord never fires its components.</para>
    /// </summary>
    public struct UltimateChordWindow {
        /// <summary>
        /// Frames a lone chord component waits for its partner. The user ruled
        /// "2–3 frames"; 3 is the agent's pick (*provisional*).
        /// </summary>
        public const int LeniencyFrames = 3;

        private enum ComponentState : byte {
            Up = 0,
            /// <summary>Pressed alone while the chord is lenient; not yet emitted.</summary>
            Waiting = 1,
            /// <summary>Emitted as itself (the window ran out, or no leniency applied).</summary>
            Emitting = 2,
            /// <summary>Part of a recognized chord; never emitted until released.</summary>
            Masked = 3
        }

        private ComponentState _movement;
        private ComponentState _special2;
        private int _movementWaited;
        private int _special2Waited;

        /// <summary>True while either component is waiting for its partner. Test/HUD surface.</summary>
        public readonly bool IsWaiting =>
            _movement == ComponentState.Waiting || _special2 == ComponentState.Waiting;

        /// <summary>
        /// Advances one frame.
        /// <paramref name="movementRaw"/> and <paramref name="special2Raw"/> are the
        /// two components' physical states on this pad; <paramref name="chordEnabled"/>
        /// is the C01c Ultimate shortcut flag for the gamepad;
        /// <paramref name="affordable"/> is whether the slot's fighter could cast
        /// its Ultimate now (the ability lockout deliberately excluded).
        /// <paramref name="settleHeld"/> (CM-FIXB-PADUI, 2026-10-16) is set on the
        /// tick <see cref="InputManager.ConsumeHeldPresses"/> covers: a component
        /// already down is then emitted at once instead of waiting, so it reaches
        /// the game as held on that tick, with its press edge consumed, as every
        /// other button does. Waiting would emit it as a fresh press
        /// <see cref="LeniencyFrames"/> ticks later, after the consume. A chord
        /// still masks.
        /// </summary>
        public Output Step(bool movementRaw, bool special2Raw, bool chordEnabled, bool affordable, bool settleHeld = false) {
            bool chord = chordEnabled && movementRaw && special2Raw;
            bool lenient = chordEnabled && affordable;
            bool maskNow = chord && lenient;
            bool movement = StepComponent(ref _movement, ref _movementWaited, movementRaw, maskNow, lenient, settleHeld);
            bool special2 = StepComponent(ref _special2, ref _special2Waited, special2Raw, maskNow, lenient, settleHeld);
            return new Output(movement, special2, chord);
        }

        /// <summary>Forgets every wait and mask (a device hand-over drops the whole window instead).</summary>
        public void Reset() {
            _movement = ComponentState.Up;
            _special2 = ComponentState.Up;
            _movementWaited = 0;
            _special2Waited = 0;
        }

        /// <summary>
        /// Forgets every wait and keeps every mask and emission (a pause or a
        /// window focus loss; CM-FIXA-INPUT, 2026-10-16). A component still held
        /// back is dropped, so a lone shoulder let go of while away never comes
        /// back as a one-frame tap. A component a chord masked stays masked until
        /// it is released — clearing it would turn LB + RB held across the pause
        /// into two fresh component presses once the Ultimate is no longer
        /// affordable — and an emitting one stays held, so it makes no new edge.
        /// </summary>
        public void ForgetWaits() {
            if (_movement == ComponentState.Waiting) _movement = ComponentState.Up;
            if (_special2 == ComponentState.Waiting) _special2 = ComponentState.Up;
            _movementWaited = 0;
            _special2Waited = 0;
        }

        private static bool StepComponent(
            ref ComponentState state, ref int waited, bool raw, bool maskNow, bool lenient, bool settleHeld) {
            switch (state) {
                case ComponentState.Waiting:
                    if (maskNow) {
                        state = ComponentState.Masked;
                        return false;
                    }
                    if (!raw) {
                        // Released inside the window: one held frame, so the
                        // tap still reaches the game as a press.
                        state = ComponentState.Up;
                        return true;
                    }
                    waited++;
                    if (!lenient || settleHeld || waited >= LeniencyFrames) {
                        state = ComponentState.Emitting;
                        return true;
                    }
                    return false;
                case ComponentState.Emitting:
                    if (!raw) {
                        state = ComponentState.Up;
                        return false;
                    }
                    if (maskNow) {
                        state = ComponentState.Masked;
                        return false;
                    }
                    return true;
                case ComponentState.Masked:
                    if (!raw) state = ComponentState.Up;
                    return false;
                default:
                    if (!raw) return false;
                    if (maskNow) {
                        state = ComponentState.Masked;
                        return false;
                    }
                    if (lenient && !settleHeld) {
                        state = ComponentState.Waiting;
                        waited = 0;
                        return false;
                    }
                    state = ComponentState.Emitting;
                    return true;
            }
        }

        /// <summary>One frame's chord-resolved bits.</summary>
        public readonly struct Output {
            public Output(bool movementHeld, bool special2Held, bool ultimateHeld) {
                MovementHeld = movementHeld;
                Special2Held = special2Held;
                UltimateHeld = ultimateHeld;
            }

            /// <summary>The Movement Ability bit the frame carries.</summary>
            public bool MovementHeld { get; }

            /// <summary>The Special 2 bit the frame carries.</summary>
            public bool Special2Held { get; }

            /// <summary>The chord's Ultimate bit (a direct bind is ORed in by the caller).</summary>
            public bool UltimateHeld { get; }
        }
    }
}
