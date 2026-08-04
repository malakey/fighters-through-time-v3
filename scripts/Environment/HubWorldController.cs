using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class HubWorldController : Node2D {
        private PlayerController _player;
        private Area2D _portalArea;
        private Area2D _repositoryArea;
        private Label _hudLabel;
        private Label _dustLabel;
        private bool _portalReady = true;

        public override void _Ready() {
            BuildHubEnvironment();
            SpawnPlayer();
            BuildHUD();
        }

        private void BuildHubEnvironment() {
            var bg = new ColorRect();
            bg.Name = "Background";
            bg.Size = new Vector2(3840, 1080);
            bg.Position = new Vector2(0, 0);
            bg.Color = new Color(0.05f, 0.08f, 0.15f);
            AddChild(bg);

            BuildFloor(0, 900, 3840);

            BuildPlatform(400, 700, 300);
            BuildPlatform(1200, 650, 250);
            BuildPlatform(2200, 700, 280);

            BuildWall(0, 0, 1080);
            BuildWall(3820, 0, 1080);

            BuildPortal();
            BuildRepository();
            BuildDecorations();
        }

        private void BuildFloor(float x, float y, float width) {
            var floor = new StaticBody2D();
            floor.CollisionLayer = CollisionLayers.Environment;
            floor.CollisionMask = 0;
            floor.Name = "Floor";
            floor.Position = new Vector2(x + width / 2f, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(width, 40);
            col.Shape = shape;
            col.Position = new Vector2(0, 20);
            floor.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(width, 40);
            visual.Position = new Vector2(-width / 2f, 0);
            visual.Color = new Color(0.15f, 0.2f, 0.3f);
            floor.AddChild(visual);

            var floorLine = new ColorRect();
            floorLine.Size = new Vector2(width, 3);
            floorLine.Position = new Vector2(-width / 2f, 0);
            floorLine.Color = new Color(0.0f, 0.7f, 0.8f, 0.6f);
            floor.AddChild(floorLine);

            AddChild(floor);
        }

        private void BuildPlatform(float x, float y, float width) {
            var plat = new StaticBody2D();
            plat.CollisionLayer = CollisionLayers.Environment;
            plat.CollisionMask = 0;
            plat.Name = $"Platform_{x}";
            plat.Position = new Vector2(x, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(width, 16);
            col.Shape = shape;
            plat.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(width, 16);
            visual.Position = new Vector2(-width / 2f, -8);
            visual.Color = new Color(0.12f, 0.18f, 0.28f);
            plat.AddChild(visual);

            var glow = new ColorRect();
            glow.Size = new Vector2(width, 2);
            glow.Position = new Vector2(-width / 2f, -8);
            glow.Color = new Color(0.0f, 0.6f, 0.7f, 0.4f);
            plat.AddChild(glow);

            AddChild(plat);
        }

        private void BuildWall(float x, float y, float height) {
            var wall = new StaticBody2D();
            wall.CollisionLayer = CollisionLayers.Environment;
            wall.CollisionMask = 0;
            wall.Name = $"Wall_{x}";
            wall.Position = new Vector2(x, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(20, height);
            col.Shape = shape;
            col.Position = new Vector2(10, height / 2f);
            wall.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(20, height);
            visual.Position = new Vector2(0, 0);
            visual.Color = new Color(0.1f, 0.12f, 0.2f);
            wall.AddChild(visual);

            AddChild(wall);
        }

        private void BuildPortal() {
            _portalArea = new Area2D();
            _portalArea.Name = "TemporalPortal";
            _portalArea.Position = new Vector2(3400, 850);
            _portalArea.CollisionLayer = CollisionLayers.Trigger;
            _portalArea.CollisionMask = CollisionLayers.Player;

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(100, 120);
            col.Shape = shape;
            col.Position = new Vector2(0, -60);
            _portalArea.AddChild(col);

            var portalVisual = new ColorRect();
            portalVisual.Name = "PortalVisual";
            portalVisual.Size = new Vector2(80, 140);
            portalVisual.Position = new Vector2(-40, -140);
            portalVisual.Color = new Color(0.0f, 0.9f, 0.95f, 0.6f);
            _portalArea.AddChild(portalVisual);

            var portalFrame = new ColorRect();
            portalFrame.Size = new Vector2(90, 150);
            portalFrame.Position = new Vector2(-45, -145);
            portalFrame.Color = new Color(0.2f, 0.3f, 0.5f, 0.8f);
            _portalArea.AddChild(portalFrame);
            portalVisual.ZIndex = 1;

            var portalLabel = new Label();
            portalLabel.Text = "TEMPORAL PORTAL\n[E] Enter";
            portalLabel.Position = new Vector2(-60, -170);
            portalLabel.CustomMinimumSize = new Vector2(120, 30);
            portalLabel.HorizontalAlignment = HorizontalAlignment.Center;
            portalLabel.AddThemeFontSizeOverride("font_size", 11);
            portalLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.9f, 0.9f));
            _portalArea.AddChild(portalLabel);

            _portalArea.BodyEntered += OnPortalBodyEntered;
            _portalArea.BodyExited += OnPortalBodyExited;

            AddChild(_portalArea);
        }

        private void BuildRepository() {
            _repositoryArea = new Area2D();
            _repositoryArea.Name = "ChronalRepository";
            _repositoryArea.Position = new Vector2(1920, 860);
            _repositoryArea.CollisionLayer = CollisionLayers.Trigger;
            _repositoryArea.CollisionMask = CollisionLayers.Player;

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(80, 80);
            col.Shape = shape;
            col.Position = new Vector2(0, -40);
            _repositoryArea.AddChild(col);

            var terminalVisual = new ColorRect();
            terminalVisual.Size = new Vector2(60, 80);
            terminalVisual.Position = new Vector2(-30, -80);
            terminalVisual.Color = new Color(0.1f, 0.4f, 0.3f, 0.8f);
            _repositoryArea.AddChild(terminalVisual);

            var screen = new ColorRect();
            screen.Size = new Vector2(40, 30);
            screen.Position = new Vector2(-20, -75);
            screen.Color = new Color(0.0f, 0.8f, 0.5f, 0.9f);
            _repositoryArea.AddChild(screen);

            var repoLabel = new Label();
            repoLabel.Text = "CHRONAL REPOSITORY";
            repoLabel.Position = new Vector2(-70, -100);
            repoLabel.CustomMinimumSize = new Vector2(140, 20);
            repoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            repoLabel.AddThemeFontSizeOverride("font_size", 10);
            repoLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.5f));
            _repositoryArea.AddChild(repoLabel);

            AddChild(_repositoryArea);
        }

        private void BuildDecorations() {
            var shipTitle = new Label();
            shipTitle.Name = "ShipTitle";
            shipTitle.Text = "THE ARCHIVE TIME-SHIP";
            shipTitle.Position = new Vector2(1700, 50);
            shipTitle.CustomMinimumSize = new Vector2(440, 40);
            shipTitle.HorizontalAlignment = HorizontalAlignment.Center;
            shipTitle.AddThemeFontSizeOverride("font_size", 22);
            shipTitle.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f, 0.8f));
            AddChild(shipTitle);

            for (int i = 0; i < 6; i++) {
                var panel = new ColorRect();
                panel.Size = new Vector2(40, 60);
                panel.Position = new Vector2(300 + i * 600, 200);
                panel.Color = new Color(0.08f, 0.15f, 0.25f, 0.5f);
                AddChild(panel);
            }
        }

        private void SpawnPlayer() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            _player = CharacterFactory.CreateCharacter(characterID, 0);
            _player.Name = "Player";
            _player.Position = new Vector2(600, 850);
            AddChild(_player);

            var camera = new Camera2D();
            camera.Name = "PlayerCamera";
            camera.Enabled = true;
            camera.LimitLeft = 0;
            camera.LimitRight = 3840;
            camera.LimitTop = 0;
            camera.LimitBottom = 1080;
            camera.PositionSmoothingEnabled = true;
            camera.PositionSmoothingSpeed = 5.0f;
            _player.AddChild(camera);
        }

        private void BuildHUD() {
            var canvas = new CanvasLayer();
            canvas.Name = "HubHUD";
            canvas.Layer = 10;
            AddChild(canvas);

            _hudLabel = new Label();
            _hudLabel.Text = "Archive Time-Ship | [E] Interact with Portal/Terminal";
            _hudLabel.Position = new Vector2(20, 20);
            _hudLabel.AddThemeFontSizeOverride("font_size", 14);
            _hudLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.9f));
            canvas.AddChild(_hudLabel);

            _dustLabel = new Label();
            _dustLabel.Position = new Vector2(20, 50);
            _dustLabel.AddThemeFontSizeOverride("font_size", 13);
            _dustLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            canvas.AddChild(_dustLabel);
            UpdateDustDisplay();

            var nextLevelLabel = new Label();
            nextLevelLabel.Name = "NextLevelLabel";
            var storyMgr = StoryManager.Instance;
            string nextLevel = storyMgr != null ? storyMgr.CurrentLevel.ToString() : "Tutorial";
            nextLevelLabel.Text = $"Next Mission: {nextLevel}";
            nextLevelLabel.Position = new Vector2(20, 80);
            nextLevelLabel.AddThemeFontSizeOverride("font_size", 12);
            nextLevelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.2f));
            canvas.AddChild(nextLevelLabel);
        }

        private void UpdateDustDisplay() {
            int dust = StoryManager.Instance?.ChronalDustCollected ?? 0;
            _dustLabel.Text = $"Chronal Dust: {dust}";
        }

        private bool _playerInPortal;

        private void OnPortalBodyEntered(Node2D body) {
            if (body is PlayerController) _playerInPortal = true;
        }

        private void OnPortalBodyExited(Node2D body) {
            if (body is PlayerController) _playerInPortal = false;
        }

        public override void _Input(InputEvent @event) {
            if (@event.IsActionPressed("gameplay_interact")) {
                if (_playerInPortal && _portalReady) {
                    StoryManager.Instance?.LoadCurrentLevel();
                }
            }
        }
    }
}
