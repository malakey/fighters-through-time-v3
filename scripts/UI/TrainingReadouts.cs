using System;

namespace FTT.UI {

    /// <summary>
    /// One tick of what the training room reads from the simulation's public getters. Plain data, so the tracker below is
    /// engine-free and the room is the only code that touches a sim type.
    /// </summary>
    public struct TrainingReadoutSample {
        /// <summary>The match is in progress (the countdown and a finished match count nothing).</summary>
        public bool Live;
        /// <summary>The student is frozen in its own hitstop: its phase and the stun counters do not advance this tick.</summary>
        public bool StudentFrozen;
        public int DummyHP;
        public int DummyHitstunFrames;
        public int DummyShieldStunFrames;
        /// <summary>The student's move phase: 1 startup, 2 active, 3 recovery, 0 free (idle, chain hold, anything else).</summary>
        public int StudentPhase;
    }

    /// <summary>
    /// GR-TRAIN (9c). Turns the per-tick samples into the training room's readouts: the last hit's damage and the total, the
    /// stun a hit applied, the student's startup / active / recovery frames for its last move, and the frame advantage of the
    /// last hit. Presentation only: it reads, never writes, and it holds nothing the simulation or a save could see.
    ///
    /// <para><b>Advantage</b> is measured when the student becomes free again after a hit: the dummy's stun still left at
    /// that moment, or, when the dummy was already free, minus the ticks it has been free. Both counters stand still in a
    /// hitstop, so a freeze on either side cancels out. It reads <c>null</c> until it is known.</para>
    /// </summary>
    public sealed class TrainingReadoutTracker {
        /// <summary><see cref="FramesSinceHit"/> before the dummy has been hit.</summary>
        public const int NoHit = int.MaxValue / 2;
        private const int CounterCap = 1_000_000;

        private int _previousHP;
        private int _previousHitstun;
        private int _previousShieldStun;
        private int _previousPhase;
        private bool _haveBaseline;
        private bool _advantagePending;
        private int _dummyFreeTicks;

        public int LastHitDamage { get; private set; }
        public int TotalDamage { get; private set; }
        /// <summary>The stun (hitstun, or shield stun on a block) the last hit applied.</summary>
        public int StunApplied { get; private set; }
        public int StartupFrames { get; private set; }
        public int ActiveFrames { get; private set; }
        public int RecoveryFrames { get; private set; }
        /// <summary>Live frames since the dummy was last hit; <see cref="NoHit"/> when it has not been.</summary>
        public int FramesSinceHit { get; private set; } = NoHit;
        /// <summary>The last hit's frame advantage, or null until the student is free (or before any hit).</summary>
        public int? Advantage { get; private set; }
        /// <summary>True from a hit until its advantage is known.</summary>
        public bool AdvantagePending => _advantagePending;
        /// <summary>The dummy has been hit at least once since the last reset.</summary>
        public bool HasHit { get; private set; }

        public void Reset() {
            LastHitDamage = 0;
            TotalDamage = 0;
            StunApplied = 0;
            StartupFrames = 0;
            ActiveFrames = 0;
            RecoveryFrames = 0;
            FramesSinceHit = NoHit;
            Advantage = null;
            HasHit = false;
            _advantagePending = false;
            _dummyFreeTicks = 0;
            _previousPhase = 0;
            _haveBaseline = false;
        }

        public void Observe(in TrainingReadoutSample sample) {
            if (!_haveBaseline) {
                _previousHP = sample.DummyHP;
                _previousHitstun = sample.DummyHitstunFrames;
                _previousShieldStun = sample.DummyShieldStunFrames;
                _haveBaseline = true;
            }
            if (!sample.Live) {
                _previousHP = sample.DummyHP;
                _previousHitstun = sample.DummyHitstunFrames;
                _previousShieldStun = sample.DummyShieldStunFrames;
                _previousPhase = sample.StudentPhase;
                return;
            }

            if (FramesSinceHit < CounterCap) FramesSinceHit++;

            int damage = _previousHP - sample.DummyHP;
            bool hit = damage > 0
                || sample.DummyHitstunFrames > _previousHitstun
                || sample.DummyShieldStunFrames > _previousShieldStun;
            if (hit) {
                LastHitDamage = Math.Max(0, damage);
                TotalDamage += LastHitDamage;
                StunApplied = Math.Max(sample.DummyHitstunFrames, sample.DummyShieldStunFrames);
                FramesSinceHit = 0;
                HasHit = true;
                Advantage = null;
                _advantagePending = true;
                _dummyFreeTicks = 0;
            }

            ObservePhase(in sample);
            ObserveAdvantage(in sample);

            _previousHP = sample.DummyHP;
            _previousHitstun = sample.DummyHitstunFrames;
            _previousShieldStun = sample.DummyShieldStunFrames;
            _previousPhase = sample.StudentPhase;
        }

        private void ObservePhase(in TrainingReadoutSample sample) {
            int phase = sample.StudentPhase;
            // A move starts on the first startup tick; a chained next hit starts again from startup.
            if (phase == 1 && _previousPhase != 1) {
                StartupFrames = 0;
                ActiveFrames = 0;
                RecoveryFrames = 0;
            }
            if (sample.StudentFrozen) return;
            switch (phase) {
                case 1: StartupFrames++; break;
                case 2: ActiveFrames++; break;
                case 3: RecoveryFrames++; break;
            }
        }

        private void ObserveAdvantage(in TrainingReadoutSample sample) {
            if (!_advantagePending) return;
            int stun = Math.Max(sample.DummyHitstunFrames, sample.DummyShieldStunFrames);
            bool studentFree = sample.StudentPhase == 0;
            if (!studentFree) {
                if (stun == 0 && !sample.StudentFrozen) _dummyFreeTicks++;
                return;
            }
            Advantage = stun > 0 ? stun : -_dummyFreeTicks;
            _advantagePending = false;
        }
    }
}
