using Godot;

namespace FTT.Environment {
    /// <summary>Small opening landmarks retain the existing activation and interaction nodes.</summary>
    public static class OpeningLandmarkPresentation {
        public static void Refine(Node2D parent, Rect2 frame) {
            foreach (Node child in parent.GetChildren()) {
                if (child is not Node2D node) continue;
                if (node is StaticBody2D && node.Name.ToString().StartsWith("Wall_")
                    && node.Position.X <= frame.Position.X + 20f) {
                    foreach (Node part in node.GetChildren()) {
                        if (part is CanvasItem visual) visual.Hide();
                    }
                }
                // Old drop-through rungs can overlap the right edge by a few pixels.
                // Crop only their primitive visual; keep the authored landing collider.
                if (node is OneWayPlatform && node.Position.X >= frame.End.X) {
                    foreach (Node part in node.GetChildren()) {
                        if (part is not Control visual) continue;
                        float overlap = frame.End.X - node.Position.X - visual.Position.X;
                        if (overlap <= 0f || overlap >= visual.Size.X) continue;
                        visual.Position += new Vector2(overlap, 0f);
                        visual.Size -= new Vector2(overlap, 0f);
                    }
                }
                if (node.Position.X < frame.Position.X || node.Position.X >= frame.End.X) continue;
                float floorY = node.Position.Y;
                if (parent.GetNodeOrNull<StoryOpeningPresentation>("StoryOpening")?.Profile is StoryOpeningProfile profile) {
                    foreach (StoryOpeningProfile.Surface surface in profile.Surfaces) {
                        Rect2 rect = surface.Rect;
                        if (surface.Kind == "floor" && node.Position.X >= rect.Position.X && node.Position.X <= rect.End.X) {
                            floorY = rect.Position.Y;
                            break;
                        }
                    }
                }
                if (node is CheckpointTrigger) RefineCheckpoint(node, floorY - node.Position.Y);
                if (node is WardenBeacon) RefineWardenBeacon(node, floorY - node.Position.Y);
            }
        }

        private static void RefineCheckpoint(Node2D checkpoint, float floorOffset) {
            ReplaceBeacon(checkpoint, "CheckpointVisual", floorOffset);
            foreach (Node child in checkpoint.GetChildren()) {
                if (child is Label label && label.Name != "StrikePrompt") label.Hide();
            }
        }

        private static void RefineWardenBeacon(Node2D beacon, float floorOffset) =>
            ReplaceBeacon(beacon, "BeaconVisual", floorOffset);

        private static void ReplaceBeacon(Node2D owner, string visualName, float floorOffset) {
            CanvasItem previous = owner.GetNodeOrNull<CanvasItem>(visualName);
            if (previous == null) return;
            Color tint = previous.Modulate;
            owner.RemoveChild(previous);
            previous.QueueFree();
            var visual = new Node2D {
                Name = visualName, Position = new Vector2(0f, floorOffset), Modulate = tint
            };
            owner.AddChild(visual);

            Color ink = new(0.12f, 0.11f, 0.09f);
            Color bronze = new(0.48f, 0.31f, 0.15f);
            Color gold = new(0.91f, 0.72f, 0.35f);
            Color cyan = new(0.4f, 0.89f, 0.94f);
            Vector2[] housing = {
                new(-3f, -24f), new(3f, -24f), new(5f, -21f), new(5f, -5f),
                new(3f, -2f), new(-3f, -2f), new(-5f, -5f), new(-5f, -21f)
            };
            visual.AddChild(new Polygon2D { Polygon = housing, Color = bronze });
            visual.AddChild(new Line2D { Points = housing, Closed = true, Width = 1.1f, DefaultColor = ink });
            visual.AddChild(new Line2D {
                Points = new[] { new Vector2(-3f, -22f), new Vector2(-3.5f, -20f), new Vector2(-3.5f, -6f) },
                Width = 1f, DefaultColor = gold
            });
            visual.AddChild(new Polygon2D {
                Polygon = new[] { new Vector2(-2.5f, -20f), new Vector2(2.5f, -20f),
                    new Vector2(2.5f, -6f), new Vector2(-2.5f, -6f) },
                Color = new Color(0.08f, 0.19f, 0.21f)
            });
            visual.AddChild(new Polygon2D {
                Polygon = new[] { new Vector2(0f, -18f), new Vector2(2f, -14f),
                    new Vector2(0f, -10f), new Vector2(-2f, -14f) },
                Color = cyan
            });
            visual.AddChild(new Line2D {
                Points = new[] { new Vector2(0f, -9f), new Vector2(0f, -6f) },
                Width = 1f, DefaultColor = cyan
            });
            Vector2[] foot = { new(-4f, -3f), new(4f, -3f), new(6f, -1f),
                new(6f, 0f), new(-6f, 0f), new(-6f, -1f) };
            visual.AddChild(new Polygon2D { Polygon = foot, Color = bronze });
            visual.AddChild(new Line2D { Points = foot, Closed = true, Width = 1f, DefaultColor = ink });
            visual.AddChild(new Line2D {
                Points = new[] { new Vector2(-3.5f, -2f), new Vector2(3.5f, -2f) },
                Width = 1f, DefaultColor = gold
            });
        }
    }
}
