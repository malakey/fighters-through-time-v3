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

        private Node2D _p1;
        private Node2D _p2;
        private bool _focusActive;
        private Vector2 _focusPosition;
        private float _focusZoom = 1f;

        /// <summary>True while a presentation focus overrides midpoint framing.</summary>
        public bool IsFocused => _focusActive;

        public override void _Ready() {
            if (_p1 == null) _p1 = GetNodeOrNull<Node2D>(Player1Path);
            if (_p2 == null) _p2 = GetNodeOrNull<Node2D>(Player2Path);
        }

        /// <summary>Assigns code-spawned fighters and snaps to their midpoint.</summary>
        public void SetPlayers(Node2D playerOne, Node2D playerTwo) {
            _p1 = playerOne;
            _p2 = playerTwo;
            if (_p1 != null && _p2 != null) {
                GlobalPosition = (_p1.GlobalPosition + _p2.GlobalPosition) / 2f;
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
            _focusZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom * 2f);
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
