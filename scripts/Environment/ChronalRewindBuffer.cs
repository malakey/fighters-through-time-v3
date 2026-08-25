using System;
using System.Collections.Generic;
using Godot;

namespace FTT.Environment {

    public readonly struct RewindFrame {
        public readonly Vector2 Position;
        public readonly bool IsGrounded;
        public readonly bool IsFacingRight;
        public readonly string AnimationName;

        public RewindFrame(Vector2 position, bool isGrounded, bool isFacingRight, string animationName) {
            Position = position;
            IsGrounded = isGrounded;
            IsFacingRight = isFacingRight;
            AnimationName = animationName ?? "";
        }
    }

    /// <summary>
    /// Fixed eight-second history plus a retained last-known grounded frame.
    /// The retained frame prevents long falls from erasing every valid rewind
    /// anchor. (2026-08-11: raised from five seconds by user direction — the
    /// short window produced barely any visible travel. 2026-08-15: cut from
    /// fifteen to eight seconds by user direction — fifteen was too long.)
    /// </summary>
    public sealed class ChronalRewindBuffer {
        public const int DefaultCapacity = 480;
        private readonly RewindFrame[] _frames;
        private int _nextIndex;
        private int _count;
        private RewindFrame _lastKnownGrounded;
        private bool _hasLastKnownGrounded;

        public ChronalRewindBuffer(int capacity = DefaultCapacity) {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _frames = new RewindFrame[capacity];
        }

        public int Count => _count;
        public int Capacity => _frames.Length;
        public bool HasGroundedFallback => _hasLastKnownGrounded;

        public void Record(in RewindFrame frame) {
            _frames[_nextIndex] = frame;
            _nextIndex = (_nextIndex + 1) % _frames.Length;
            if (_count < _frames.Length) _count++;
            if (frame.IsGrounded) {
                _lastKnownGrounded = frame;
                _hasLastKnownGrounded = true;
            }
        }

        public bool TryFindSafeLanding(Vector2 checkpointFallback, out RewindFrame landing) {
            for (int offset = 1; offset <= _count; offset++) {
                int index = PositiveModulo(_nextIndex - offset, _frames.Length);
                if (_frames[index].IsGrounded) {
                    landing = _frames[index];
                    return true;
                }
            }
            if (_hasLastKnownGrounded) {
                landing = _lastKnownGrounded;
                return true;
            }
            landing = new RewindFrame(checkpointFallback, true, true, "idle");
            return false;
        }

        /// <summary>
        /// Builds the newest-to-oldest playback path, landing on the grounded
        /// frame nearest <paramref name="targetDepthFrames"/>: the first
        /// grounded frame at or beyond the target, or failing that the deepest
        /// grounded frame recorded at all. The original rule — stop at the
        /// FIRST grounded frame walking backward — degenerated to a
        /// zero-distance rewind for any death on solid ground (the frame one
        /// tick before death is grounded), which read in game as the character
        /// freezing with no rewind ever happening.
        /// </summary>
        public List<RewindFrame> BuildPlaybackPath(
            Vector2 checkpointFallback, int stride = 4, int targetDepthFrames = int.MaxValue) {
            if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
            var path = new List<RewindFrame>(_count / stride + 2);
            RewindFrame landing = default;
            bool foundLanding = false;
            RewindFrame deepestGrounded = default;
            int deepestGroundedPathCount = 0;
            bool hasDeepestGrounded = false;
            for (int offset = 1; offset <= _count; offset++) {
                int index = PositiveModulo(_nextIndex - offset, _frames.Length);
                RewindFrame frame = _frames[index];
                if ((offset - 1) % stride == 0) path.Add(frame);
                if (frame.IsGrounded) {
                    deepestGrounded = frame;
                    deepestGroundedPathCount = path.Count;
                    hasDeepestGrounded = true;
                    if (offset >= targetDepthFrames) {
                        landing = frame;
                        foundLanding = true;
                        break;
                    }
                }
            }

            if (!foundLanding && hasDeepestGrounded) {
                // The ring never reached the target depth (young buffer, or an
                // airborne tail past the last grounded frame): land on the
                // deepest grounded frame and trim the path past it.
                landing = deepestGrounded;
                if (path.Count > deepestGroundedPathCount) {
                    path.RemoveRange(deepestGroundedPathCount, path.Count - deepestGroundedPathCount);
                }
                foundLanding = true;
            }
            if (!foundLanding) {
                landing = _hasLastKnownGrounded
                    ? _lastKnownGrounded
                    : new RewindFrame(checkpointFallback, true, true, "idle");
            }
            if (path.Count == 0 || path[path.Count - 1].Position != landing.Position) path.Add(landing);
            return path;
        }

        /// <summary>
        /// The frame recorded <paramref name="depthFrames"/> ticks ago (clamped
        /// to the recorded history) — the V7.2 manual scrub's preview read.
        /// </summary>
        public bool TryPeek(int depthFrames, out RewindFrame frame) {
            if (_count == 0 || depthFrames < 1) {
                frame = default;
                return false;
            }
            int offset = Math.Min(depthFrames, _count);
            frame = _frames[PositiveModulo(_nextIndex - offset, _frames.Length)];
            return true;
        }

        public void Clear() {
            Array.Clear(_frames, 0, _frames.Length);
            _nextIndex = 0;
            _count = 0;
            _lastKnownGrounded = default;
            _hasLastKnownGrounded = false;
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
