namespace FTT.Core {

    /// <summary>
    /// Decides when a moving, grounded character plants a foot (Package 8 B5). Pure
    /// C#: <c>PlayerController</c> owns the state, this owns the rhythm, and the
    /// rhythm is testable without a physics frame.
    ///
    /// <para><b>Distance, not a timer.</b> A fixed frame interval makes a character
    /// walking out of a status slow-down keep a sprinter's cadence, and makes a dash
    /// sound identical to a walk. Accumulating travelled distance and firing every
    /// <see cref="StrideDistance"/> pixels means the cadence scales with speed for
    /// free — the same reason animation systems drive footsteps off root motion.</para>
    ///
    /// <para><b>The first step is immediate.</b> Becoming ineligible (airborne, or
    /// below <see cref="MinimumSpeed"/>) primes the accumulator to a full stride, so
    /// starting to run or landing plants a foot on the first eligible frame instead of
    /// half a stride later. Without that, short movements between platforms are
    /// silent.</para>
    /// </summary>
    public sealed class FootstepCadence {
        /// <summary>Pixels of ground travel between steps at the default stride.</summary>
        public const float DefaultStrideDistance = 96f;

        /// <summary>Below this horizontal speed a character is shuffling, not walking.</summary>
        public const float DefaultMinimumSpeed = 40f;

        public float StrideDistance = DefaultStrideDistance;
        public float MinimumSpeed = DefaultMinimumSpeed;

        private float _accumulated;
        private bool _primed = true;

        /// <summary>Distance banked toward the next step.</summary>
        public float AccumulatedDistance => _accumulated;

        /// <summary>True while the next eligible frame will plant a foot outright.</summary>
        public bool IsPrimed => _primed;

        /// <summary>Steps this instance has emitted since the last <see cref="Reset"/>.</summary>
        public int StepCount { get; private set; }

        /// <summary>
        /// Advances one frame. <paramref name="horizontalSpeed"/> is signed pixels per
        /// second (direction is irrelevant to the cadence); returns true on the frame
        /// a footstep should sound.
        /// </summary>
        public bool Advance(bool grounded, float horizontalSpeed, float deltaSeconds) {
            float speed = horizontalSpeed < 0f ? -horizontalSpeed : horizontalSpeed;
            if (!grounded || speed < MinimumSpeed || deltaSeconds <= 0f) {
                _primed = true;
                _accumulated = 0f;
                return false;
            }

            if (_primed) {
                _primed = false;
                _accumulated = 0f;
                StepCount++;
                return true;
            }

            _accumulated += speed * deltaSeconds;
            if (_accumulated < StrideDistance) return false;
            // Modulo, not subtraction: a frame that banks several strides at once
            // (a long stall, a movement ability landing) sounds once and drops the
            // backlog rather than machine-gunning it out over the next few frames.
            _accumulated %= StrideDistance;
            StepCount++;
            return true;
        }

        public void Reset() {
            _accumulated = 0f;
            _primed = true;
            StepCount = 0;
        }
    }
}
