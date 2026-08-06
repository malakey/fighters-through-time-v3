using System;

namespace FTT.Core {

    /// <summary>
    /// Converts device motion into a deterministic one-tick dash command. Digital
    /// inputs use a double tap; an analog stick uses a quick neutral-to-full flick.
    /// The resulting Dash button is serialized in PlayerInputFrame.
    /// </summary>
    public sealed class DashInputDetector {
        public const int DoubleTapWindowFrames = 15;
        private const float DigitalPressThreshold = 0.75f;
        private const float DigitalReleaseThreshold = 0.35f;
        private const float AnalogFlickThreshold = 0.85f;
        private const float AnalogNeutralThreshold = 0.30f;

        private int _previousDirection;
        private int _lastTapDirection;
        private int _lastTapTick = int.MinValue;
        private float _previousAnalogHorizontal;

        public bool Update(uint tick, float horizontal, float analogHorizontal, bool hasAnalogStick) {
            int currentDirection = Direction(horizontal, DigitalPressThreshold);
            bool newDirectionalPress = currentDirection != 0 && _previousDirection == 0;
            bool analogFlick = hasAnalogStick
                && MathF.Abs(analogHorizontal) >= AnalogFlickThreshold
                && MathF.Abs(_previousAnalogHorizontal) <= AnalogNeutralThreshold;

            bool dash = false;
            if (analogFlick) {
                dash = true;
                _lastTapDirection = 0;
                _lastTapTick = int.MinValue;
            } else if (newDirectionalPress) {
                int currentTick = unchecked((int)tick);
                long elapsed = (long)currentTick - _lastTapTick;
                dash = currentDirection == _lastTapDirection
                    && elapsed > 0
                    && elapsed <= DoubleTapWindowFrames;
                if (dash) {
                    _lastTapDirection = 0;
                    _lastTapTick = int.MinValue;
                } else {
                    _lastTapDirection = currentDirection;
                    _lastTapTick = currentTick;
                }
            }

            if (MathF.Abs(horizontal) <= DigitalReleaseThreshold) _previousDirection = 0;
            else if (currentDirection != 0) _previousDirection = currentDirection;
            _previousAnalogHorizontal = analogHorizontal;
            return dash;
        }

        public void Reset() {
            _previousDirection = 0;
            _lastTapDirection = 0;
            _lastTapTick = int.MinValue;
            _previousAnalogHorizontal = 0f;
        }

        private static int Direction(float value, float threshold) {
            if (value >= threshold) return 1;
            if (value <= -threshold) return -1;
            return 0;
        }
    }
}
