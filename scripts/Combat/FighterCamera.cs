using Godot;

namespace FTT.Combat {
    public partial class FighterCamera : Camera2D {
        [Export] public NodePath Player1Path;
        [Export] public NodePath Player2Path;
        [Export] public float MinZoom = 0.8f;
        [Export] public float MaxZoom = 1.5f;
        [Export] public float ZoomSpeed = 2.0f;
        [Export] public float FollowSpeed = 5.0f;
        [Export] public Vector2 MarginPadding = new(200, 100);
        /// <summary>Tightening speed while a KO focus is held; deliberately slower than the normal chase.</summary>
        [Export] public float FocusSpeed = 3.0f;

        private bool _hasReferenceFrame;
        private Vector2 _referenceCenter;
        private float _referenceZoom;
        private float _referenceFocusScale = 1f;

        /// <summary>Registers the plate's authored frame, while allowing play to leave it.</summary>
        public void ConfigureReferenceFrame(Vector2 center, float zoom) {
            _hasReferenceFrame = true;
            _referenceCenter = center;
            _referenceFocusScale = zoom / Mathf.Max(MaxZoom, 0.01f);
            _referenceZoom = zoom;
            MaxZoom = Mathf.Max(MaxZoom, zoom);
            LimitLeft = LimitTop = -100000;
            LimitRight = LimitBottom = 100000;
            GlobalPosition = center;
            Zoom = Vector2.One * zoom;
        }

        /// <summary>Stable at rest; expands for jumps, high galleries, and wide separation.</summary>
        public static (Vector2 Center, float Zoom) ReferenceFrameForPlayers(
            Vector2 center, float zoom, Vector2 viewport, Vector2 feetOne, Vector2 feetTwo) =>
            ConstrainReferenceFrame(center, zoom, zoom, viewport, feetOne, feetTwo);

        /// <summary>Projects a smoothed frame into the current HUD-safe player bounds.</summary>
        public static (Vector2 Center, float Zoom) ConstrainReferenceFrame(
            Vector2 center, float zoom, float silhouetteZoom, Vector2 viewport, Vector2 feetOne, Vector2 feetTwo) {
            // The body size is fixed by the reference zoom, even while the camera expands.
            float silhouetteHeight = 1080 * FTT.FighterSim.FighterStageVisualProfile.FighterHeightFraction / silhouetteZoom;
            Vector2 top = new(Mathf.Min(feetOne.X, feetTwo.X) - 45,
                Mathf.Min(feetOne.Y, feetTwo.Y) - silhouetteHeight - 30);
            Vector2 bottom = new(Mathf.Max(feetOne.X, feetTwo.X) + 45, Mathf.Max(feetOne.Y, feetTwo.Y) + 30);
            // HUD occupies the top of the screen. These screen-space paddings
            // scale with the logical viewport and become world distances at zoom.
            float safeTop = viewport.Y * (280f / 1080f);
            float safeBottom = viewport.Y * (30f / 1080f);
            Vector2 half = viewport / (2 * zoom);
            if (top.X >= center.X - half.X && bottom.X <= center.X + half.X
                && top.Y >= center.Y - half.Y + safeTop / zoom
                && bottom.Y <= center.Y + half.Y - safeBottom / zoom) return (center, zoom);
            // Fit the complete player bounds, then choose the closest centre to
            // the authored frame that contains them. High galleries need headroom,
            // without pulling equally far away from the empty opposite edge.
            Vector2 boundsSize = bottom - top;
            float expandedZoom = Mathf.Min(zoom, Mathf.Min(viewport.X / boundsSize.X, (viewport.Y - safeTop - safeBottom) / boundsSize.Y));
            expandedZoom = Mathf.Max(0.55f, expandedZoom);
            half = viewport / (2 * expandedZoom);
            Vector2 trackingCenter = new(
                NearestFrameCenter(center.X, bottom.X - half.X, top.X + half.X),
                NearestFrameCenter(center.Y, bottom.Y - half.Y + safeBottom / expandedZoom,
                    top.Y + half.Y - safeTop / expandedZoom));
            return (trackingCenter, expandedZoom);
        }

        private static float NearestFrameCenter(float value, float minimum, float maximum) {
            // At an exact fit, float rounding can invert the interval by an ULP.
            // At minimum zoom a very large span can genuinely have no feasible
            // centre. The midpoint balances overflow on both sides in either case.
            return minimum <= maximum ? Mathf.Clamp(value, minimum, maximum)
                : minimum + (maximum - minimum) * 0.5f;
        }

        private Node2D _p1;
        private Node2D _p2;
        private bool _focusActive;
        private Vector2 _focusPosition;
        private float _focusZoom = 1f;

        /// <summary>True while a presentation focus overrides midpoint framing.</summary>
        public bool IsFocused => _focusActive;

        /// <summary>Presentation target after accounting for the authored normal frame.</summary>
        public float FocusTargetZoom => _focusZoom;

        public override void _Ready() {
            if (_p1 == null) _p1 = GetNodeOrNull<Node2D>(Player1Path);
            if (_p2 == null) _p2 = GetNodeOrNull<Node2D>(Player2Path);
        }

        /// <summary>Assigns code-spawned fighters and snaps to their midpoint.</summary>
        public void SetPlayers(Node2D playerOne, Node2D playerTwo) {
            _p1 = playerOne;
            _p2 = playerTwo;
            if (_p1 != null && _p2 != null) {
                GlobalPosition = _hasReferenceFrame ? _referenceCenter : (_p1.GlobalPosition + _p2.GlobalPosition) / 2f;
            }
        }

        /// <summary>
        /// Presentation-only override used by the KO sequence: tightens on a world
        /// point at a fixed zoom instead of framing both fighters. It touches
        /// nothing the deterministic simulation reads.
        /// </summary>
        public void FocusOn(Vector2 globalPosition, float zoom) {
            _focusActive = true;
            _focusPosition = globalPosition;
            _focusZoom = Mathf.Clamp(zoom * _referenceFocusScale, MinZoom, MaxZoom * 2f);
        }

        /// <summary>Returns the camera to normal midpoint framing.</summary>
        public void ReleaseFocus() {
            _focusActive = false;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;

            if (_focusActive) {
                GlobalPosition = GlobalPosition.Lerp(_focusPosition, FocusSpeed * dt);
                float focusStep = Mathf.Lerp(Zoom.X, _focusZoom, FocusSpeed * dt);
                Zoom = new Vector2(focusStep, focusStep);
                return;
            }

            if (_p1 == null || _p2 == null) return;

            if (_hasReferenceFrame) {
                var frame = ReferenceFrameForPlayers(_referenceCenter, _referenceZoom,
                    GetViewportRect().Size, _p1.GlobalPosition, _p2.GlobalPosition);
                Vector2 smoothCenter = GlobalPosition.Lerp(frame.Center, Mathf.Min(1, FollowSpeed * dt));
                float smoothZoom = Mathf.Lerp(Zoom.X, frame.Zoom, Mathf.Min(1, ZoomSpeed * dt));
                var safeFrame = ConstrainReferenceFrame(smoothCenter, smoothZoom, _referenceZoom,
                    GetViewportRect().Size, _p1.GlobalPosition, _p2.GlobalPosition);
                GlobalPosition = safeFrame.Center;
                Zoom = Vector2.One * safeFrame.Zoom;
                return;
            }

            var midpoint = (_p1.GlobalPosition + _p2.GlobalPosition) / 2f;
            GlobalPosition = GlobalPosition.Lerp(midpoint, FollowSpeed * dt);

            float distance = _p1.GlobalPosition.DistanceTo(_p2.GlobalPosition);
            float targetZoom = Mathf.Clamp(800f / Mathf.Max(distance + MarginPadding.X, 1f), MinZoom, MaxZoom);
            float currentZoom = Zoom.X;
            float newZoom = Mathf.Lerp(currentZoom, targetZoom, ZoomSpeed * dt);
            Zoom = new Vector2(newZoom, newZoom);
        }
    }
}
