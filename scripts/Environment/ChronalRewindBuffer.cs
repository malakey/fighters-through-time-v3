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
    /// Fixed five-second history plus a retained last-known grounded frame. The
    /// retained frame prevents long falls from erasing every valid rewind anchor.
    /// </summary>
    public sealed class ChronalRewindBuffer {
        public const int DefaultCapacity = 300;
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

        public List<RewindFrame> BuildPlaybackPath(Vector2 checkpointFallback, int stride = 4) {
            if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
            var path = new List<RewindFrame>(_count / stride + 2);
            RewindFrame landing = default;
            bool foundLandingInRing = false;
            for (int offset = 1; offset <= _count; offset++) {
                int index = PositiveModulo(_nextIndex - offset, _frames.Length);
                RewindFrame frame = _frames[index];
                if ((offset - 1) % stride == 0) path.Add(frame);
                if (frame.IsGrounded) {
                    landing = frame;
                    foundLandingInRing = true;
                    break;
                }
            }

            if (!foundLandingInRing) {
                landing = _hasLastKnownGrounded
                    ? _lastKnownGrounded
                    : new RewindFrame(checkpointFallback, true, true, "idle");
            }
            if (path.Count == 0 || path[path.Count - 1].Position != landing.Position) path.Add(landing);
            return path;
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
