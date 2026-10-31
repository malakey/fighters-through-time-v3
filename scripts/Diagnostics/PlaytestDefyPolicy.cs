using System.Collections.Generic;

namespace FTT.Diagnostics {

    /// <summary>
    /// When the <c>play</c> bot presses Defy (meter and Defy update 2026-10-31, plan section 2.13,
    /// O24, P26; ledger <c>PROVISIONAL-BOT-DEFY-POLICY</c>). Pure C#: the bot feeds it the hero's
    /// readable state, and it answers yes or no and remembers the hit frames and the last Defy.
    /// Only <see cref="FightStyle.Play"/> ever Defies; <c>spam</c>, <c>basics</c>, <c>bypass</c> and
    /// the feel probe never do, so the probes stay comparable.
    /// </summary>
    public sealed class PlaytestDefyPolicy {
        /// <summary>Influence the hero must hold (the Defy cost, read by the caller from BasicComboRules).</summary>
        public const int MinHitsInString = 2;
        /// <summary>The string window in frames: this many hits inside it is "a string".</summary>
        public const int StringWindowFrames = 60;
        /// <summary>Foes inside <see cref="EnemyRadiusPixels"/> needed.</summary>
        public const int MinEnemiesNear = 2;
        public const float EnemyRadiusPixels = 250f;
        /// <summary>Frames that must pass after the last Defy press.</summary>
        public const int SpacingFrames = 300;

        private readonly Queue<int> _hitFrames = new();
        private int _lastDefyFrame = int.MinValue / 2;

        /// <summary>Whether this style ever presses Defy.</summary>
        public static bool StyleDefies(FightStyle style) => style == FightStyle.Play;

        /// <summary>Records one discrete hit the hero took (periodic ticks are not hits).</summary>
        public void NoteHit(int frame) {
            _hitFrames.Enqueue(frame);
            while (_hitFrames.Count > 0 && frame - _hitFrames.Peek() > StringWindowFrames) _hitFrames.Dequeue();
        }

        /// <summary>Records a Defy press.</summary>
        public void NoteDefyPressed(int frame) => _lastDefyFrame = frame;

        /// <summary>Hits taken in the <see cref="StringWindowFrames"/> frames up to and including <paramref name="frame"/>.</summary>
        public int HitsInWindow(int frame) {
            int count = 0;
            foreach (int hit in _hitFrames) if (frame - hit <= StringWindowFrames) count++;
            return count;
        }

        /// <summary>
        /// True when <paramref name="style"/> should press Defy now: the style Defies, the hero can
        /// (<paramref name="canDefy"/>, which already holds the 50 Influence and the state refusals),
        /// it is in hitstun inside a string (2+ hits in 60 frames), 2+ foes stand within 250 px and
        /// 300+ frames have passed since the last Defy.
        /// </summary>
        public bool ShouldDefy(FightStyle style, int frame, bool canDefy, bool inHitstun, int enemiesNear) {
            if (!StyleDefies(style) || !canDefy || !inHitstun) return false;
            if (enemiesNear < MinEnemiesNear) return false;
            if (frame - _lastDefyFrame < SpacingFrames) return false;
            return HitsInWindow(frame) >= MinHitsInString;
        }
    }
}
