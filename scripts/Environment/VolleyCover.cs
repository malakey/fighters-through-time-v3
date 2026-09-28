using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Package 12 W8 (M10): a piece of volley cover — a wicker mantlet or a siege
    /// trench lip — authored as a <b>child</b> of the <see cref="StoryCyclicHazard"/>
    /// it shelters from. The occlusion rule is deterministic and area-based, with no
    /// physics raycast and no randomness: a player whose body origin (the feet, per
    /// the feet-on-origin convention) lies inside <see cref="GlobalShelterRect"/>
    /// during the hazard's Active phase takes no damage from <b>that</b> hazard.
    /// A cover never shelters from any other hazard, so scoping is simply parentage.
    ///
    /// Cover is deliberately <b>non-solid</b> — it carries no collision object — so
    /// the route stays hero-agnostic: any character reaches it by walking, with
    /// basics only. The node origin sits on the ground line; the shelter rectangle
    /// rises <see cref="ShelterSize"/>.Y above it (a jumping player clears the
    /// mantlet and is exposed) and extends <see cref="FootTolerance"/> below it.
    /// </summary>
    public partial class VolleyCover : Node2D {
        /// <summary>Width and height of the sheltered area, in pixels.</summary>
        [Export] public Vector2 ShelterSize = new(72f, 110f);

        /// <summary>How far below the ground line a standing player's origin may sit.</summary>
        [Export] public float FootTolerance = 8f;

        [Export] public Color MantletColor = new(0.55f, 0.40f, 0.20f, 0.95f);

        private StoryCyclicHazard _hazard;

        /// <summary>The hazard this cover shelters from (its parent), or null.</summary>
        public StoryCyclicHazard Hazard => GetParent() as StoryCyclicHazard;

        /// <summary>The sheltered rectangle in the cover's local space.</summary>
        public Rect2 LocalShelterRect => new(
            -ShelterSize.X * 0.5f, -ShelterSize.Y, ShelterSize.X, ShelterSize.Y + FootTolerance);

        /// <summary>The sheltered rectangle in global space (translation only; cover is never rotated).</summary>
        public Rect2 GlobalShelterRect {
            get {
                Rect2 local = LocalShelterRect;
                Vector2 origin = IsInsideTree() ? GlobalPosition : Position;
                return new Rect2(origin + local.Position, local.Size);
            }
        }

        public bool Shelters(Vector2 globalPoint) => GlobalShelterRect.HasPoint(globalPoint);

        public override void _EnterTree() {
            _hazard = GetParent() as StoryCyclicHazard;
            _hazard?.RegisterCover(this);
        }

        public override void _ExitTree() {
            _hazard?.UnregisterCover(this);
            _hazard = null;
        }

        public override void _Draw() {
            // Placeholder wicker mantlet: a slanted screen with three weave bands.
            float w = ShelterSize.X;
            float h = ShelterSize.Y * 0.85f;
            var screen = new[] {
                new Vector2(-w * 0.5f, 0f), new Vector2(w * 0.5f, 0f),
                new Vector2(w * 0.35f, -h), new Vector2(-w * 0.45f, -h)
            };
            DrawColoredPolygon(screen, MantletColor);
            Color weave = MantletColor.Darkened(0.35f);
            for (int band = 1; band <= 3; band++) {
                float y = -h * band / 4f;
                DrawLine(new Vector2(-w * 0.48f, y), new Vector2(w * 0.42f, y), weave, 3f);
            }
        }
    }
}
