using Godot;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;

namespace FTT.Environment {

    /// <summary>
    /// Controller for production-contract Fighter stage scenes. The authored
    /// scene provides presentation geometry, spawn/orb/hazard markers, and a
    /// FighterCamera; this controller spawns the presentation fighters at the
    /// authored markers and starts the deterministic simulation with the
    /// stage's fixed-point geometry.
    /// </summary>
    public partial class FighterStageController : Node2D {
        /// <summary>Stage identity this scene was authored for; must match the catalog entry.</summary>
        [Export] public string StageID = "";

        public FighterSimulationDriver FighterDriver { get; private set; }
        private ShaderMaterial _referencePlateMaterial;
        private Sprite2D _referencePlate;
        private Sprite2D _referenceForeground;
        private FighterCamera _referenceCamera;
        private Vector2 _referenceFrameCenter;
        private bool _expandedReferencePresentation;
        private float _referencePresentationBlend;
        public const float ForegroundEdgeFeatherPixels = 12f;
        public static Vector2 ReferenceForegroundEdgeWidth => new(
            ForegroundEdgeFeatherPixels / (float)FighterStageVisualProfile.PlateWorldWidth,
            ForegroundEdgeFeatherPixels / (float)(FighterStageVisualProfile.ReferenceHeight * FighterStageVisualProfile.PlateScale));

        /// <summary>Only the outer five percent blends when play expands the frame.</summary>
        public static float ReferenceEdgeBlend(float zoom) => Mathf.Clamp(
            (FighterStageVisualProfile.ReferenceZoom - zoom) / 0.35f, 0, 1);

        /// <summary>Complete the layer change even when the camera settles at an intermediate zoom.</summary>
        public static (bool Expanded, float Blend) AdvanceReferencePresentation(bool expanded, float blend,
            Vector2 cameraCenter, float cameraZoom, Vector2 referenceCenter, float delta) {
            float zoomDistance = Mathf.Abs(cameraZoom - FighterStageVisualProfile.ReferenceZoom);
            float centerDistance = cameraCenter.DistanceTo(referenceCenter);
            if (expanded) {
                if (zoomDistance <= 0.005f && centerDistance <= 0.5f) expanded = false;
            } else if (zoomDistance > 0.015f || centerDistance > 1.5f) expanded = true;
            return (expanded, Mathf.MoveToward(blend, expanded ? 1 : 0, Mathf.Max(delta, 0) / 0.15f));
        }

        public override void _Process(double delta) {
            if (_referenceCamera == null) return;
            if (_referenceForeground != null) {
                var presentation = AdvanceReferencePresentation(_expandedReferencePresentation, _referencePresentationBlend,
                    _referenceCamera.GlobalPosition, _referenceCamera.Zoom.X, _referenceFrameCenter, (float)delta);
                _expandedReferencePresentation = presentation.Expanded;
                _referencePresentationBlend = presentation.Blend;
                _referencePlate.Modulate = new Color(1, 1, 1, 1 - presentation.Blend);
                _referenceForeground.Modulate = new Color(1, 1, 1, presentation.Blend);
            } else if (_referencePlateMaterial != null) {
                _referencePlateMaterial.SetShaderParameter("edge_blend", ReferenceEdgeBlend(_referenceCamera.Zoom.X));
            }
        }

        public override void _Ready() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";
            string opponentID = GameManager.Instance?.CurrentSession.OpponentCharacterID ?? "joan";
            if (string.IsNullOrEmpty(opponentID)) opponentID = "joan";

            FighterStageVisualProfile profile = FighterStageVisualProfile.Find(StageID);
            InstallReferencePlate(profile);

            FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
            FighterStageData stage = catalog?.Find(StageID) ?? catalog?.FirstPlayable();

            Vector2 spawnOne = GetNodeOrNull<Marker2D>("Spawns/Player1")?.GlobalPosition ?? new Vector2(700, 700);
            Vector2 spawnTwo = GetNodeOrNull<Marker2D>("Spawns/Player2")?.GlobalPosition ?? new Vector2(1200, 700);

            var player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
            player.Name = "Player1";
            player.Position = spawnOne;
            AddChild(player);

            var opponent = CharacterFactory.CreateCharacter(opponentID, 1, applyStoryProgression: false);
            opponent.Name = "Player2";
            opponent.Position = spawnTwo;
            opponent.IsFacingRight = false;
            AddChild(opponent);

            ApplyReferenceFigureScale(player, profile);
            ApplyReferenceFigureScale(opponent, profile);

            if (GetNodeOrNull<FighterCamera>("Camera2D") is FighterCamera camera) {
                if (profile != null) camera.ConfigureReferenceFrame(
                    new Vector2(950, (float)profile.FrameCenterY), FighterStageVisualProfile.ReferenceZoom);
                camera.SetPlayers(player, opponent);
                camera.MakeCurrent();
            }

            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            FighterDriver = new FighterSimulationDriver { Name = "FighterSimulationDriver" };
            AddChild(FighterDriver);
            FighterDriver.Initialize(player, opponent, settings, stage?.HazardTypeID ?? 1, stage?.StageID ?? StageID);

            RegisterStageAudio(stage?.StageID ?? StageID);
            AddStageTitle(stage);
        }

        /// <summary>The plate is world scenery: it shares the collision plane's camera transform.</summary>
        private void InstallReferencePlate(FighterStageVisualProfile profile) {
            if (profile == null || !ResourceLoader.Exists(profile.PlatePath)) return;
            Texture2D texture = ResourceLoader.Load<Texture2D>(profile.PlatePath);
            if (texture == null) return;
            var plate = new Sprite2D {
                Name = "ReferencePlate", Texture = texture, ZIndex = -10,
                Position = new Vector2(950, (float)profile.FrameCenterY),
                Scale = new Vector2((float)FighterStageVisualProfile.PlateWorldWidth / texture.GetWidth(),
                    (float)(FighterStageVisualProfile.ReferenceHeight * FighterStageVisualProfile.PlateScale) / texture.GetHeight()),
                TextureFilter = CanvasItem.TextureFilterEnum.Linear
            };
            _referenceFrameCenter = plate.Position;
            _referenceCamera = GetNodeOrNull<FighterCamera>("Camera2D");
            if (_referenceCamera != null) {
                Texture2D backdrop = ResourceLoader.Exists(profile.BackdropPath)
                    ? ResourceLoader.Load<Texture2D>(profile.BackdropPath) : null;
                ReferenceSceneBackdrop.Attach(this, backdrop ?? texture, _referenceCamera,
                    normalizedCrop: new Rect2(0, 0, 1, backdrop != null ? 1 : 0.65f), zIndex: -11, brightness: 0.9f);
                Texture2D foreground = ResourceLoader.Exists(profile.ForegroundPath)
                    ? ResourceLoader.Load<Texture2D>(profile.ForegroundPath) : null;
                if (foreground != null) {
                    _referenceForeground = new Sprite2D {
                        Name = "ReferenceForeground", Texture = foreground, ZIndex = -9,
                        Position = plate.Position,
                        Scale = new Vector2((float)FighterStageVisualProfile.PlateWorldWidth / foreground.GetWidth(),
                            (float)(FighterStageVisualProfile.ReferenceHeight * FighterStageVisualProfile.PlateScale) / foreground.GetHeight()),
                        Modulate = new Color(1, 1, 1, 0),
                        TextureFilter = CanvasItem.TextureFilterEnum.Linear
                    };
                    var foregroundMaterial = new ShaderMaterial {
                        Shader = ResourceLoader.Load<Shader>("res://assets/shaders/fighter_reference_edge.gdshader")
                    };
                    foregroundMaterial.SetShaderParameter("edge_blend", 1f);
                    foregroundMaterial.SetShaderParameter("edge_width", ReferenceForegroundEdgeWidth);
                    _referenceForeground.Material = foregroundMaterial;
                    AddChild(_referenceForeground);
                } else {
                    _referencePlateMaterial = new ShaderMaterial {
                        Shader = ResourceLoader.Load<Shader>("res://assets/shaders/fighter_reference_edge.gdshader")
                    };
                    plate.Material = _referencePlateMaterial;
                }
            }
            _referencePlate = plate;
            AddChild(plate);
            if (GetNodeOrNull<CanvasItem>("Presentation") is CanvasItem scenery) scenery.Hide();
            if (GetNodeOrNull<CanvasItem>("Lighting") is CanvasItem lighting) lighting.Hide();
            // Geometry nodes retain their colliders. Only their old painted primitives disappear.
            HidePrimitiveScenery(GetNodeOrNull<Node>("Geometry"));
        }

        private static void HidePrimitiveScenery(Node node) {
            if (node == null) return;
            foreach (Node child in node.GetChildren()) {
                if (child is ColorRect || child is Polygon2D || child is Line2D || child is Sprite2D) {
                    ((CanvasItem)child).Hide();
                } else HidePrimitiveScenery(child);
            }
        }

        private static void ApplyReferenceFigureScale(PlayerController player, FighterStageVisualProfile profile) {
            AnimatedSprite2D sprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (profile == null || sprite?.SpriteFrames == null || !sprite.SpriteFrames.HasAnimation("idle")) return;
            float alphaHeight = 0;
            for (int frame = 0; frame < sprite.SpriteFrames.GetFrameCount("idle"); frame++) {
                Image image = sprite.SpriteFrames.GetFrameTexture("idle", frame)?.GetImage();
                if (image != null) alphaHeight = Mathf.Max(alphaHeight, image.GetUsedRect().Size.Y);
            }
            if (alphaHeight <= 0) return;
            float currentHeight = alphaHeight * Mathf.Abs(sprite.Scale.Y);
            float figureScale = Mathf.Clamp(1080 * FighterStageVisualProfile.FighterHeightFraction
                / (currentHeight * FighterStageVisualProfile.ReferenceZoom), 0.5f, 2.5f);
            RetroSpriteScaleNormalizer.Attach(sprite, RetroSpriteScaleNormalizer.FigureKind.Character).FigureScale = figureScale;
            float height = alphaHeight * Mathf.Abs(sprite.Scale.Y);
            if (player.GetNodeOrNull<Label>("NameLabel") is Label label) label.Position = new Vector2(label.Position.X, -height - 24);
            player.SetMeta("fighter_idle_world_height", height);
            player.SetMeta("fighter_idle_screen_fraction", height * FighterStageVisualProfile.ReferenceZoom / 1080);
            player.SetMeta("fighter_figure_scale", figureScale);
            player.SetMeta("fighter_reference_feet", player.Position);
        }

        /// <summary>
        /// Package 8 B5. Registers the stage's authored music set at match start; the
        /// driver moves it to Combat when the countdown ends. Released in
        /// <see cref="_ExitTree"/> so leaving the stage does not carry its stems into
        /// the next scene.
        /// </summary>
        private void RegisterStageAudio(string stageID) {
            _audioRegistered = FighterStageAudio.Register(stageID);
        }

        private bool _audioRegistered;

        public override void _ExitTree() {
            if (!_audioRegistered) return;
            _audioRegistered = false;
            AudioManager.Instance?.ReleaseStageAudio();
        }

        private void AddStageTitle(FighterStageData stage) {
            if (stage == null) return;
            var title = new Label {
                Name = "StageTitle",
                Text = Tr(stage.DisplayNameKey),
                Position = new Vector2(760, 28),
                CustomMinimumSize = new Vector2(400, 40),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 22);
            title.AddThemeColorOverride("font_color", stage.AccentColor);
            GetNodeOrNull<CanvasLayer>("HUDLayer")?.AddChild(title);
        }
    }
}
