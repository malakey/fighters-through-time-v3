using System.Collections.Generic;
using Godot;

namespace FTT.Environment {
    /// <summary>Collision-preserving surface dressing sampled from the same registered environment painting.</summary>
    public static class ReferenceTerrainPresentation {
        public sealed class SurfaceVisual {
            private readonly ShaderMaterial[] _materials;
            private float _lastOpacity = float.NaN;
            public SurfaceVisual(params ShaderMaterial[] materials) => _materials = materials;
            public void SetPlateOpacity(float opacity) {
                if (Mathf.IsEqualApprox(_lastOpacity, opacity)) return;
                _lastOpacity = opacity;
                foreach (ShaderMaterial material in _materials) material.SetShaderParameter("plate_opacity", opacity);
            }
        }

        private const string ClipCode = @"
uniform vec4 plate_frame;
uniform float plate_opacity = 1.0;
varying vec2 world_point;
varying vec4 surface_tint;
void vertex() { world_point = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy; surface_tint = COLOR; }
void clip_plate(inout vec4 color) {
    if (world_point.x >= plate_frame.x && world_point.y >= plate_frame.y &&
        world_point.x <= plate_frame.x + plate_frame.z && world_point.y <= plate_frame.y + plate_frame.w)
        color.a *= 1.0 - plate_opacity;
}";
        private static readonly Shader SurfaceShader = new() { Code = "shader_type canvas_item;" + ClipCode + @"
uniform vec4 source_strip;
uniform vec2 repeat_count;
void fragment() {
    COLOR = surface_tint * texture(TEXTURE, source_strip.xy + fract(UV * repeat_count) * source_strip.zw);
    clip_plate(COLOR);
}" };
        private static readonly Shader EdgeShader = new() { Code = "shader_type canvas_item;" + ClipCode + @"
void fragment() { clip_plate(COLOR); }" };

        public static Rect2 SourceStrip(Texture2D source, StoryOpeningProfile profile) {
            float floorY = profile.Spawn.Y;
            foreach (StoryOpeningProfile.Surface surface in profile.Surfaces) {
                if (surface.Kind == "floor" && profile.Spawn.X >= surface.Rect.Position.X && profile.Spawn.X <= surface.Rect.End.X) {
                    floorY = surface.Rect.Position.Y;
                    break;
                }
            }
            Vector2 sourceSize = source.GetSize();
            Vector2 stripSize = new(sourceSize.X * 0.16f, sourceSize.Y * 0.10f);
            float x = (profile.Spawn.X - profile.Frame.Position.X) / profile.Frame.Size.X * sourceSize.X - stripSize.X / 2f;
            float y = (floorY - profile.Frame.Position.Y) / profile.Frame.Size.Y * sourceSize.Y;
            return new Rect2(Mathf.Clamp(x, 0f, sourceSize.X - stripSize.X),
                Mathf.Clamp(y, 0f, sourceSize.Y - stripSize.Y), stripSize.X, stripSize.Y);
        }

        public static SurfaceVisual Style(StaticBody2D body, Texture2D source, StoryOpeningProfile profile, float plateOpacity) {
            if (body.GetNodeOrNull<Node2D>("ReferenceTerrain") != null) return null;
            CollisionShape2D collision = null;
            foreach (Node child in body.GetChildren()) {
                if (child is CollisionShape2D shape && shape.Shape is RectangleShape2D) { collision = shape; break; }
            }
            if (collision?.Shape is not RectangleShape2D rectangle) return null;
            foreach (Node child in body.GetChildren()) {
                if (child is Control visual) visual.Hide();
            }
            Rect2 strip = SourceStrip(source, profile);
            Vector2 size = rectangle.Size;
            Vector2 topLeft = collision.Position - size / 2f;
            Vector2 sourceSize = source.GetSize();
            float texelWorldSize = profile.Frame.Size.X / sourceSize.X;
            var fillMaterial = new ShaderMaterial { Shader = SurfaceShader };
            fillMaterial.SetShaderParameter("source_strip", new Vector4(strip.Position.X / sourceSize.X,
                strip.Position.Y / sourceSize.Y, strip.Size.X / sourceSize.X, strip.Size.Y / sourceSize.Y));
            fillMaterial.SetShaderParameter("repeat_count", size / (strip.Size * texelWorldSize));
            var edgeMaterial = new ShaderMaterial { Shader = EdgeShader };
            foreach (ShaderMaterial material in new[] { fillMaterial, edgeMaterial })
                material.SetShaderParameter("plate_frame", new Vector4(profile.Frame.Position.X, profile.Frame.Position.Y,
                    profile.Frame.Size.X, profile.Frame.Size.Y));
            var root = new Node2D { Name = "ReferenceTerrain" };
            root.AddChild(new TextureRect {
                Name = "PaintedSurface", Position = topLeft, Size = size,
                Texture = source, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, Material = fillMaterial,
                Modulate = new Color(0.9f, 0.9f, 0.9f, 1f), MouseFilter = Control.MouseFilterEnum.Ignore
            });
            root.AddChild(new ColorRect {
                Name = "WalkableEdge", Position = topLeft, Size = new Vector2(size.X, 1.5f),
                Color = new Color(0.8f, 0.73f, 0.55f, 0.85f), Material = edgeMaterial,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            body.AddChild(root);
            var result = new SurfaceVisual(fillMaterial, edgeMaterial);
            result.SetPlateOpacity(plateOpacity);
            return result;
        }
    }
}
