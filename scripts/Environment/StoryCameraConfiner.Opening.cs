using Godot;

namespace FTT.Environment {
    public partial class StoryCameraConfiner {
        private Rect2 _openingFrame;
        private float _openingReleaseX;
        private float _openingStartX;
        private float _openingFarthestX;
        private float _openingZoom;
        private float _openingWeight;
        private Vector2 _openingInitialFeet;
        private bool _openingOriginCaptured;

        public bool HasOpeningFraming => _openingWeight > 0f;
        public float OpeningWeight => _openingWeight;

        public void ClearOpening() {
            _openingWeight = 0f;
            WriteLimits(_ratcheting ? _working : ActiveBounds);
        }

        /// <summary>Called before EnableFollow, only on a fresh campaign entry.</summary>
        public void ConfigureOpening(Rect2 frame, float releaseX) {
            if (frame.Size.X <= 0f || frame.Size.Y <= 0f) return;
            _openingFrame = frame;
            _openingReleaseX = Mathf.Max(frame.Position.X + 1f, releaseX);
            _openingStartX = Mathf.Min(_openingReleaseX - 1f, frame.Position.X + frame.Size.X * 0.55f);
            _openingFarthestX = frame.Position.X;
            _openingZoom = Mathf.Min(StoryCameraRules.ReferenceViewportWidth / frame.Size.X,
                StoryCameraRules.ReferenceViewportHeight / frame.Size.Y);
            _openingWeight = 1f;
            _openingOriginCaptured = false;
            Zoom = Vector2.One * _openingZoom;
            WriteLimits(CurrentLimits);
        }

        private void CaptureOpeningOrigin() {
            if (_openingWeight <= 0f || _openingOriginCaptured || !IsFollowing) return;
            _openingInitialFeet = FollowTarget.GlobalPosition;
            _openingOriginCaptured = true;
        }

        private void StepOpening() {
            if (_openingWeight <= 0f || !IsFollowing) return;
            _openingFarthestX = Mathf.Max(_openingFarthestX, FollowTarget.GlobalPosition.X);
            float progress = Mathf.Clamp((_openingFarthestX - _openingStartX) /
                (_openingReleaseX - _openingStartX), 0f, 1f);
            _openingWeight = 1f - progress * progress * (3f - 2f * progress);
            // Encounter framing owns the view as soon as a live arena asks for it.
            if (FramingRequestCount > 0) _openingWeight = 0f;
            WriteLimits(_ratcheting ? _working : ActiveBounds);
        }

        private Vector2 OpeningCenter(Vector2 ordinary) {
            if (_openingWeight <= 0f) return ordinary;
            Vector2 desired = ordinary.Lerp(_openingFrame.GetCenter(), _openingWeight);
            if (!IsFollowing) return desired;
            Vector2 half = ViewSize() * 0.42f;
            if (_openingOriginCaptured) {
                Vector2 authoredOffset = (_openingInitialFeet - _openingFrame.GetCenter()).Abs();
                half = new Vector2(Mathf.Max(half.X, authoredOffset.X), Mathf.Max(half.Y, authoredOffset.Y));
            }
            Vector2 feet = FollowTarget.GlobalPosition;
            // Jumps and retreat can move the view before the authored horizontal handoff.
            return new Vector2(Mathf.Clamp(desired.X, feet.X - half.X, feet.X + half.X),
                Mathf.Clamp(desired.Y, feet.Y - half.Y, feet.Y + half.Y));
        }
    }
}
