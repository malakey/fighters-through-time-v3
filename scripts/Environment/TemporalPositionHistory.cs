using Godot;

namespace FTT.Environment {

    public partial class TemporalPositionHistory : Node {
        public const int HistoryFrames = 240;
        private readonly Vector2[] _positions = new Vector2[HistoryFrames];
        private int _next;
        private int _count;
        private Node2D _owner;

        public override void _Ready() => _owner = GetParent<Node2D>();

        public override void _PhysicsProcess(double delta) {
            if (_owner == null) return;
            Record(_owner.GlobalPosition);
        }

        public void Record(Vector2 position) {
            _positions[_next] = position;
            _next = (_next + 1) % HistoryFrames;
            _count = Mathf.Min(HistoryFrames, _count + 1);
        }

        public bool TryGetFramesAgo(int framesAgo, out Vector2 position) {
            if (_count == 0) { position = default; return false; }
            int clamped = Mathf.Clamp(framesAgo, 0, _count - 1);
            int index = (_next - 1 - clamped + HistoryFrames) % HistoryFrames;
            position = _positions[index];
            return framesAgo < _count;
        }
    }
}
