using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// The state model behind the three Chronal Rewind payload fields that shipped
    /// dead: <c>GhostTrailEnabled</c>, <c>ReverseSweepEnabled</c> and
    /// <c>ClockTickEnabled</c> (Package 8 B6).
    ///
    /// <para><c>ChronalRewindManager</c> has been setting all three since the rewind
    /// system landed and nothing read any of them. This class owns the edges — the
    /// sweep fires once per rewind, not once per payload; the clock ticks on a fixed
    /// cadence for as long as the rewind runs; the ghost trail follows the flag.</para>
    ///
    /// <para>Deliberately pure C# with no Godot dependency so the cadence is
    /// assertable without an engine frame, in the same spirit as A2's pumped
    /// fades.</para>
    /// </summary>
    public sealed class RewindCueState {
        /// <summary>Seconds between clock ticks while a rewind is playing.</summary>
        public const float ClockTickIntervalSeconds = 0.22f;

        private float _sinceLastTick;

        /// <summary>True while the rewind is in one of its active phases.</summary>
        public bool IsActive { get; private set; }

        /// <summary>True while historical ghost sprites should be drawn.</summary>
        public bool GhostTrailActive { get; private set; }

        /// <summary>True once the reverse sweep has been played for this rewind.</summary>
        public bool SweepPlayed { get; private set; }

        /// <summary>Clock ticks emitted since this rewind began.</summary>
        public int ClockTicks { get; private set; }

        private bool _clockEnabled;

        /// <summary>
        /// Folds one presentation payload in. Returns true when the caller should
        /// play the reverse-sweep cue — only on the rising edge of a rewind.
        /// </summary>
        public bool Apply(RewindPresentationPayload payload) {
            bool active = payload.Phase == RewindPresentationPhase.Started
                || payload.Phase == RewindPresentationPhase.Playback
                || payload.Phase == RewindPresentationPhase.TimelineCollapse;

            if (!active) {
                Reset();
                return false;
            }

            bool rising = !IsActive;
            IsActive = true;
            GhostTrailActive = payload.GhostTrailEnabled;
            _clockEnabled = payload.ClockTickEnabled;
            if (rising) {
                _sinceLastTick = ClockTickIntervalSeconds; // first tick lands immediately
                ClockTicks = 0;
                SweepPlayed = false;
            }

            if (!payload.ReverseSweepEnabled || SweepPlayed) return false;
            SweepPlayed = true;
            return true;
        }

        /// <summary>
        /// Advances the clock. Returns the number of ticks due this frame (normally
        /// zero or one; more only if the caller stalled for several intervals).
        /// </summary>
        public int Advance(float deltaSeconds) {
            if (!IsActive || !_clockEnabled || deltaSeconds <= 0f) return 0;
            _sinceLastTick += deltaSeconds;
            int due = 0;
            while (_sinceLastTick >= ClockTickIntervalSeconds) {
                _sinceLastTick -= ClockTickIntervalSeconds;
                due++;
            }
            ClockTicks += due;
            return due;
        }

        public void Reset() {
            IsActive = false;
            GhostTrailActive = false;
            SweepPlayed = false;
            _clockEnabled = false;
            ClockTicks = 0;
            _sinceLastTick = 0f;
        }
    }
}
