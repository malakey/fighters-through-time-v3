using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// One side of an encounter lock (gameplay update 2026-10-12, GU-E, plan
    /// §2.3): a solid Environment wall the hero, the members, enemy shots, the M1
    /// line of sight, the relocation rule and the D9 wall bounce all meet, built
    /// once at level build with its shape <b>disabled</b> and toggled through
    /// <see cref="PhysicsSafeSetters.SetShapeDisabledSafe"/> — flush-safe, and a
    /// re-enabled shape is what <c>PlaytestNav</c> notices (a body created
    /// mid-level would be invisible to the bots for up to 20 s).
    ///
    /// <para>It is one rect from its authored top (by default
    /// <see cref="StoryGateRules.ColumnTopY"/>, far above any reach) down to the
    /// floor, so it needs no gate column and cannot be jumped. It joins
    /// <see cref="Group"/> and deliberately neither <see cref="StoryGateRules.DoorGroup"/>
    /// nor <see cref="ForcefieldBarrier"/>: the gate-column sweeps and counts in
    /// <c>ArenaGeometryContentTests</c> do not see it, and
    /// <c>EncounterLockContentTests</c> sweeps it instead. A
    /// <see cref="StaticBody2D"/>, never an <c>AnimatableBody2D</c>, which the
    /// static-geometry sweeps exclude.</para>
    ///
    /// <para>Placeholder look: a cold, jagged energy curtain in
    /// <see cref="FTT.UI.UIPalette.UnboundCold"/> — the Severed's kind of door, not
    /// the Warden portal's gold — with a column of hollow diamonds down its centre
    /// as the non-colour channel. Drawn only while closed.</para>
    /// </summary>
    public partial class EncounterLockBarrier : StaticBody2D {
        /// <summary>Every lock barrier joins this group.</summary>
        public const string Group = "story_encounter_barrier";

        /// <summary>How far the curtain's glow reaches past each face (presentation only).</summary>
        private const float GlowPixels = 10f;
        /// <summary>Vertical pitch of the jagged edge and the diamond column.</summary>
        private const float PatternPitchPixels = 48f;

        /// <summary>The owning lock's ID; diagnostics and the content sweep.</summary>
        public string LockID { get; private set; } = "";

        /// <summary>True for the barrier on the hero's way in, false for the way out.</summary>
        public bool IsEntry { get; private set; }

        /// <summary>Which way the lock's area lies from this barrier: +1 east, -1 west.</summary>
        public float AreaSide { get; private set; } = 1f;

        public float CenterX { get; private set; }
        public float TopY { get; private set; }
        public float BottomY { get; private set; }
        public float Width { get; private set; } = EncounterLockRules.DefaultBarrierWidthPixels;

        /// <summary>The collision rect, global. Content and push-out seam.</summary>
        public Rect2 Footprint => new(CenterX - Width * 0.5f, TopY, Width, BottomY - TopY);

        /// <summary>True while the wall is up.</summary>
        public bool IsClosed { get; private set; }

        /// <summary>The single collision shape; disabled while open.</summary>
        public CollisionShape2D Shape { get; private set; }

        /// <summary>
        /// Builds an open (shape disabled) barrier from <paramref name="topY"/> to
        /// <paramref name="bottomY"/>, centred on <paramref name="centerX"/>. The
        /// caller adds it to the tree.
        /// </summary>
        public static EncounterLockBarrier Create(string lockID, bool isEntry, float areaSide,
            float centerX, float topY, float bottomY, float width) {
            float top = Mathf.Min(topY, bottomY);
            float bottom = Mathf.Max(topY, bottomY);
            float clampedWidth = Mathf.Max(1f, width);
            var barrier = new EncounterLockBarrier {
                Name = $"EncounterBarrier_{(isEntry ? "Entry" : "Exit")}_{lockID}",
                LockID = lockID ?? "",
                IsEntry = isEntry,
                AreaSide = areaSide >= 0f ? 1f : -1f,
                CenterX = centerX,
                TopY = top,
                BottomY = bottom,
                Width = clampedWidth,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0,
                // Placed at its footprint's centre so the drawing and the
                // collision share one local frame.
                Position = new Vector2(centerX, (top + bottom) * 0.5f)
            };
            barrier.Shape = new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(clampedWidth, bottom - top) },
                Disabled = true
            };
            barrier.AddChild(barrier.Shape);
            barrier.Visible = false;
            return barrier;
        }

        public override void _Ready() {
            AddToGroup(Group);
            CollisionLayer = CollisionLayers.Environment;
            CollisionMask = 0;
        }

        /// <summary>
        /// Raises or drops the wall. Flush-safe: a member's death or a breakable's
        /// destruction can clear a lock from inside a hit signal.
        /// </summary>
        public void SetClosed(bool closed) {
            if (IsClosed == closed && Shape != null && Shape.Disabled == !closed) return;
            IsClosed = closed;
            Shape?.SetShapeDisabledSafe(!closed);
            Visible = closed;
            QueueRedraw();
        }

        /// <summary>
        /// The plan's "push the hero out of a closing barrier": when the wall goes
        /// up over <paramref name="body"/> (half-width <paramref name="halfWidth"/>),
        /// the body is put just past the face on the area's side and its
        /// horizontal speed zeroed, so nothing is ever left inside a closed wall.
        /// Returns true when it moved the body.
        /// </summary>
        public bool PushOut(CharacterBody2D body, float halfWidth) {
            if (body == null || !IsInstanceValid(body)) return false;
            Rect2 footprint = Footprint;
            Vector2 feet = body.GlobalPosition;
            float half = Mathf.Max(0f, halfWidth);
            bool overlapsX = feet.X + half > footprint.Position.X && feet.X - half < footprint.End.X;
            bool overlapsY = feet.Y > footprint.Position.Y && feet.Y - 1f < footprint.End.Y + 1f;
            if (!overlapsX || !overlapsY) return false;
            float x = AreaSide > 0f
                ? footprint.End.X + half + 1f
                : footprint.Position.X - half - 1f;
            body.GlobalPosition = new Vector2(x, feet.Y);
            body.Velocity = new Vector2(0f, body.Velocity.Y);
            return true;
        }

        public override void _Draw() {
            if (!IsClosed) return;
            float height = BottomY - TopY;
            float half = Width * 0.5f;
            float top = -height * 0.5f;
            // Only the part a camera can show is drawn: the level never shows
            // y < 0, and the column above it may be kilometres tall.
            float visibleTop = Mathf.Max(top, -Position.Y);
            float visibleHeight = height * 0.5f - visibleTop;
            if (visibleHeight <= 0f) return;
            Color fill = FTT.UI.UIPalette.UnboundColdDim;
            Color edge = FTT.UI.UIPalette.UnboundCold;
            DrawRect(new Rect2(-half - GlowPixels, visibleTop, Width + 2f * GlowPixels, visibleHeight),
                new Color(fill.R, fill.G, fill.B, fill.A * 0.6f));
            DrawRect(new Rect2(-half, visibleTop, Width, visibleHeight), fill);
            // The jagged edges: a zig-zag down each face (the tear, not a frame).
            int steps = Mathf.Max(1, Mathf.CeilToInt(visibleHeight / (PatternPitchPixels * 0.5f)));
            float step = visibleHeight / steps;
            for (int side = -1; side <= 1; side += 2) {
                var points = new Vector2[steps + 1];
                for (int index = 0; index <= steps; index++) {
                    float jag = index % 2 == 0 ? 0f : 6f;
                    points[index] = new Vector2(side * (half + jag), visibleTop + index * step);
                }
                DrawPolyline(points, edge, 2f);
            }
            // The non-colour channel: hollow diamonds down the centre line.
            for (float y = visibleTop + PatternPitchPixels * 0.5f; y < visibleTop + visibleHeight; y += PatternPitchPixels) {
                float r = Mathf.Min(half, 7f);
                DrawPolyline(new[] {
                    new Vector2(0f, y - r), new Vector2(r, y), new Vector2(0f, y + r),
                    new Vector2(-r, y), new Vector2(0f, y - r)
                }, edge, 1.5f);
            }
        }
    }
}
