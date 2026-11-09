using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {
    /// <summary>A registered world plate. Only explicitly tracked generated surfaces are replaced.</summary>
    public partial class StoryOpeningPresentation : Node2D {
        private const string SurfaceRectMeta = "story_opening_surface_rect";
        public StoryOpeningProfile Profile { get; private set; }
        private readonly List<(Node Parent, StaticBody2D Body)> _replaced = new();
        private readonly List<StaticBody2D> _clippedRemainders = new();
        private StoryCameraConfiner _camera;
        private StoryOpeningFigureScale _figure;
        private float _figureWeight = 1f;
        private Sprite2D _registeredPlate;
        private readonly List<ReferenceTerrainPresentation.SurfaceVisual> _terrainVisuals = new();
        public ReferenceSceneBackdrop ContinuationBackdrop { get; private set; }
        public float PlateOpacity => _registeredPlate?.Modulate.A ?? 0f;
        public float AchievedHeroHeightFraction => _figure?.AchievedFraction ?? 0f;
        public float HeroVisualMultiplier => _figure?.Multiplier ?? 1f;

        public static void TrackSurface(StaticBody2D body, Rect2 worldRect) => body.SetMeta(SurfaceRectMeta, worldRect);

        public static StoryOpeningPresentation Apply(Node2D parent, PlayerController player,
            StoryCameraConfiner camera, int level, bool resumed, IEnumerable<StaticBody2D> generatedSurfaces = null) {
            StoryOpeningProfile profile = StoryOpeningProfile.ForLevel(level);
            if (profile == null) return null;
            Texture2D texture = ResourceLoader.Load<Texture2D>(profile.TexturePath);
            if (texture == null) return null;
            var opening = new StoryOpeningPresentation {
                Name = "StoryOpening", Profile = profile, _camera = camera, _figureWeight = resumed ? 0f : 1f
            };
            opening.ContinuationBackdrop = ReferenceSceneBackdrop.Attach(parent, texture, camera);
            parent.AddChild(opening);
            opening._registeredPlate = new Sprite2D {
                Name = "RegisteredPlate", Texture = texture, Centered = false,
                Position = profile.Frame.Position,
                Scale = profile.Frame.Size / texture.GetSize(), ZIndex = -1,
                Modulate = new Color(1f, 1f, 1f, opening._figureWeight)
            };
            opening.AddChild(opening._registeredPlate);
            var generated = new List<StaticBody2D>();
            if (generatedSurfaces != null) {
                foreach (StaticBody2D body in generatedSurfaces) {
                    generated.Add(body);
                    opening.ClipSurface(body);
                }
            }
            foreach (StaticBody2D body in generated) opening.StyleSurface(body, texture);
            foreach (StaticBody2D body in opening._clippedRemainders) opening.StyleSurface(body, texture);
            foreach (Node child in parent.GetChildren()) {
                if (child is OneWayPlatform body) opening.StyleSurface(body, texture);
            }
            foreach (StoryOpeningProfile.Surface surface in profile.Surfaces) {
                Rect2 rect = surface.Rect;
                var body = new StaticBody2D {
                    Name = $"{(surface.Kind == "floor" ? "Floor" : "Platform")}_{Mathf.RoundToInt(rect.Position.X)}_{Mathf.RoundToInt(rect.Position.Y)}",
                    Position = rect.GetCenter(),
                    CollisionLayer = CollisionLayers.Environment, CollisionMask = 0
                };
                body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
                opening.AddChild(body);
                opening.StyleSurface(body, texture);
            }
            OpeningLandmarkPresentation.Refine(parent, profile.Frame);
            if (!resumed) {
                player.GlobalPosition = parent.ToGlobal(profile.Spawn);
                camera.ConfigureOpening(profile.Frame, profile.ReleaseX);
                AnimatedSprite2D sprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
                if (sprite != null) {
                    opening._figure = new StoryOpeningFigureScale(sprite, profile.Frame.Size.Y, profile.HeroHeightFraction);
                    GD.Print($"[story-opening] level={level:00} hero target={profile.HeroHeightFraction:F3} achieved={opening.AchievedHeroHeightFraction:F3} visual_multiplier={opening.HeroVisualMultiplier:F3}");
                }
            }
            return opening;
        }

        private void StyleSurface(StaticBody2D body, Texture2D texture) {
            if (!IsInstanceValid(body)) return;
            ReferenceTerrainPresentation.SurfaceVisual visual = ReferenceTerrainPresentation.Style(body, texture, Profile, _figureWeight);
            if (visual != null) _terrainVisuals.Add(visual);
        }

        /// <summary>The untouched left/right portions of a generated horizontal surface.</summary>
        public static IReadOnlyList<Rect2> OutsideOpening(Rect2 surface, Rect2 opening) {
            var result = new List<Rect2>();
            if (surface.End.X <= opening.Position.X || surface.Position.X >= opening.End.X) {
                result.Add(surface);
                return result;
            }
            if (surface.Position.X < opening.Position.X)
                result.Add(new Rect2(surface.Position, new Vector2(opening.Position.X - surface.Position.X, surface.Size.Y)));
            if (surface.End.X > opening.End.X)
                result.Add(new Rect2(opening.End.X, surface.Position.Y, surface.End.X - opening.End.X, surface.Size.Y));
            return result;
        }

        private void ClipSurface(StaticBody2D body) {
            if (!IsInstanceValid(body) || !body.HasMeta(SurfaceRectMeta) || !Mathf.IsZeroApprox(body.Rotation)) return;
            Rect2 original = body.GetMeta(SurfaceRectMeta).AsRect2();
            IReadOnlyList<Rect2> portions = OutsideOpening(original, Profile.Frame);
            if (portions.Count == 1 && portions[0] == original) return;
            // This happens during level construction, before the physics server steps.
            Node parent = body.GetParent();
            _replaced.Add((parent, body));
            parent.RemoveChild(body);
            foreach (Rect2 portion in portions) {
                var remaining = (StaticBody2D)body.Duplicate();
                string kind = body.Name.ToString().StartsWith("Floor_") ? "Floor" : "Platform";
                remaining.Name = $"{kind}_{Mathf.RoundToInt(portion.Position.X)}_{Mathf.RoundToInt(portion.Position.Y)}";
                parent.AddChild(remaining);
                _clippedRemainders.Add(remaining);
                Vector2 shift = portion.GetCenter() - original.GetCenter();
                remaining.Position += shift;
                foreach (Node child in remaining.GetChildren()) {
                    if (child is CollisionShape2D collision && collision.Shape is RectangleShape2D shape) {
                        collision.Shape = new RectangleShape2D { Size = new Vector2(portion.Size.X, shape.Size.Y) };
                    } else if (child is Control visual) {
                        visual.Position = new Vector2(-portion.Size.X / 2f, visual.Position.Y);
                        visual.Size = new Vector2(portion.Size.X, visual.Size.Y);
                    }
                }
            }
        }

        /// <summary>Restores the authored scene for a scripted change such as the tutorial battle.</summary>
        public void Deactivate() {
            _figure?.Restore();
            foreach (ReferenceTerrainPresentation.SurfaceVisual visual in _terrainVisuals) visual.SetPlateOpacity(0f);
            if (IsInstanceValid(_camera)) _camera.ClearOpening();
            foreach (StaticBody2D body in _clippedRemainders) {
                if (!IsInstanceValid(body)) continue;
                body.GetParent()?.RemoveChild(body);
                body.QueueFree();
            }
            _clippedRemainders.Clear();
            foreach (var entry in _replaced) {
                if (IsInstanceValid(entry.Parent) && IsInstanceValid(entry.Body)) entry.Parent.AddChild(entry.Body);
            }
            _replaced.Clear();
            GetParent()?.RemoveChild(this);
            QueueFree();
        }

        public override void _ExitTree() {
            _figure?.Restore();
            foreach (var entry in _replaced) {
                if (IsInstanceValid(entry.Body)) entry.Body.Free();
            }
            _replaced.Clear();
        }

        public override void _PhysicsProcess(double delta) {
            if (!IsInstanceValid(_camera)) return;
            float target = _camera.OpeningWeight;
            _figureWeight = Mathf.Lerp(_figureWeight, target,
                StoryCameraRules.DampFactor(StoryCameraRules.ZoomDampingSeconds, (float)delta));
            if (Mathf.Abs(_figureWeight - target) < 0.001f) _figureWeight = target;
            _figure?.SetWeight(_figureWeight);
            if (IsInstanceValid(_registeredPlate)) _registeredPlate.Modulate = new Color(1f, 1f, 1f, _figureWeight);
            foreach (ReferenceTerrainPresentation.SurfaceVisual visual in _terrainVisuals) visual.SetPlateOpacity(_figureWeight);
        }
    }
}
