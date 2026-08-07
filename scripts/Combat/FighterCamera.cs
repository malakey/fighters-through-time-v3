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

        private Node2D _p1;
        private Node2D _p2;

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

        public override void _PhysicsProcess(double delta) {
            if (_p1 == null || _p2 == null) return;
            float dt = (float)delta;

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
