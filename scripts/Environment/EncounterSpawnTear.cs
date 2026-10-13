using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Placeholder presentation for a lock wave's spawn-in (GU-E): a cold, jagged
    /// Severed tear — the "kind of door" the Linked arrive through, never the
    /// Warden portal's gold — that flickers at the spawn point for half a second
    /// and frees itself. Presentation only: no collision, no gameplay read, and a
    /// handful per level, so it is not pooled. Production art is Package 10.
    /// </summary>
    public partial class EncounterSpawnTear : Node2D {
        /// <summary>How long the tear stays on screen.</summary>
        public const float LifetimeSeconds = 0.5f;

        private const float HeightPixels = 120f;
        private float _age;

        /// <summary>A tear whose foot stands at <paramref name="feet"/>; the caller adds it.</summary>
        public static EncounterSpawnTear Create(Vector2 feet) =>
            new() { Name = "EncounterSpawnTear", Position = feet, ZIndex = 5 };

        public override void _Process(double delta) {
            _age += (float)delta;
            if (_age >= LifetimeSeconds) {
                QueueFree();
                return;
            }
            QueueRedraw();
        }

        public override void _Draw() {
            float fade = Mathf.Clamp(1f - _age / LifetimeSeconds, 0f, 1f);
            Color edge = FTT.UI.UIPalette.UnboundCold;
            edge.A = fade;
            float half = 14f * (0.6f + 0.4f * fade);
            var points = new Vector2[7];
            for (int index = 0; index < points.Length; index++) {
                float y = -HeightPixels * index / (points.Length - 1);
                float x = (index % 2 == 0 ? -1f : 1f) * half * (index == 0 || index == points.Length - 1 ? 0.3f : 1f);
                points[index] = new Vector2(x, y);
            }
            DrawPolyline(points, edge, 3f);
        }
    }
}
