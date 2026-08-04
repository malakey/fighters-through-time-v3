using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    public partial class Level00Controller : Node2D {
        private PlayerController _player;
        private Label _objectiveLabel;
        private int _enemiesKilled;
        private int _totalEnemies;
        private bool _combatTrialActive;
        private bool _levelComplete;

        public override void _Ready() {
            BuildLevel();
            SpawnPlayer();
            BuildHUD();
            StartCombatTrial();
        }

        private void BuildLevel() {
            var bg = new ColorRect();
            bg.Name = "Background";
            bg.Size = new Vector2(4800, 1080);
            bg.Color = new Color(0.03f, 0.05f, 0.12f);
            AddChild(bg);

            BuildFloor(0, 900, 4800);

            BuildPlatform(300, 700, 250);
            BuildPlatform(700, 550, 200);
            BuildPlatform(1100, 700, 250);
            BuildPlatform(1500, 600, 220);
            BuildPlatform(1900, 720, 200);
            BuildPlatform(2300, 550, 250);
            BuildPlatform(2700, 680, 220);
            BuildPlatform(3100, 600, 200);
            BuildPlatform(3500, 720, 250);
            BuildPlatform(4000, 550, 200);

            BuildWall(0, 0, 1080);
            BuildWall(4780, 0, 1080);

            BuildDecoration();
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
            visual.Color = new Color(0.1f, 0.15f, 0.25f);
            floor.AddChild(visual);

            var gridLine = new ColorRect();
            gridLine.Size = new Vector2(width, 2);
            gridLine.Position = new Vector2(-width / 2f, 0);
            gridLine.Color = new Color(0.0f, 0.6f, 0.8f, 0.4f);
            floor.AddChild(gridLine);

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
            visual.Color = new Color(0.08f, 0.12f, 0.22f);
            plat.AddChild(visual);

            var glow = new ColorRect();
            glow.Size = new Vector2(width - 10, 2);
            glow.Position = new Vector2(-(width - 10) / 2f, -8);
            glow.Color = new Color(0.0f, 0.7f, 0.9f, 0.3f);
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
            visual.Color = new Color(0.06f, 0.08f, 0.15f);
            wall.AddChild(visual);

            AddChild(wall);
        }

        private void BuildDecoration() {
            var title = new Label();
            title.Name = "LevelTitle";
            title.Text = "CALIBRATION BAY - TRAINING SIMULATION";
            title.Position = new Vector2(1800, 30);
            title.CustomMinimumSize = new Vector2(500, 30);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", 18);
            title.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f, 0.7f));
            AddChild(title);

            for (int i = 0; i < 8; i++) {
                var gridPanel = new ColorRect();
                gridPanel.Size = new Vector2(3, 200);
                gridPanel.Position = new Vector2(600 * i, 700);
                gridPanel.Color = new Color(0.0f, 0.4f, 0.5f, 0.15f);
                AddChild(gridPanel);
            }
        }

        private void SpawnPlayer() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            _player = CharacterFactory.CreateCharacter(characterID, 0);
            _player.Name = "Player";
            _player.Position = new Vector2(400, 850);
            AddChild(_player);

            var camera = new Camera2D();
            camera.Name = "PlayerCamera";
            camera.Enabled = true;
            camera.LimitLeft = 0;
            camera.LimitRight = 4800;
            camera.LimitTop = 0;
            camera.LimitBottom = 1080;
            camera.PositionSmoothingEnabled = true;
            camera.PositionSmoothingSpeed = 5.0f;
            _player.AddChild(camera);
        }

        private void StartCombatTrial() {
            SpawnWave1();
            _combatTrialActive = true;

            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
            }
        }

        private void SpawnWave1() {
            _totalEnemies = 5;
            _enemiesKilled = 0;

            var drone1 = EnemyFactory.CreateHologramDrone(new Vector2(1200, 850));
            AddChild(drone1);

            var drone2 = EnemyFactory.CreateHologramDrone(new Vector2(1800, 850));
            AddChild(drone2);

            var drone3 = EnemyFactory.CreateHologramDrone(new Vector2(2400, 850),
                waypointA: new Vector2(2200, 850), waypointB: new Vector2(2600, 850));
            AddChild(drone3);

            var drone4 = EnemyFactory.CreateHologramDrone(new Vector2(3000, 700));
            AddChild(drone4);

            var drone5 = EnemyFactory.CreateHologramDrone(new Vector2(3600, 850),
                waypointA: new Vector2(3400, 850), waypointB: new Vector2(3800, 850));
            AddChild(drone5);
        }

        private void SpawnWave2() {
            _totalEnemies = 4;
            _enemiesKilled = 0;

            var drone1 = EnemyFactory.CreateHologramDrone(new Vector2(1500, 850));
            AddChild(drone1);

            var drone2 = EnemyFactory.CreateHologramDrone(new Vector2(2000, 550));
            AddChild(drone2);

            var drone3 = EnemyFactory.CreateHologramDrone(new Vector2(2800, 850));
            AddChild(drone3);

            var slasher = EnemyFactory.CreateChronoSlasher(new Vector2(3200, 850),
                waypointA: new Vector2(3000, 850), waypointB: new Vector2(3400, 850));
            AddChild(slasher);

            UpdateObjective("Defeat remaining holograms! (Wave 2)");
        }

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            _enemiesKilled++;
            UpdateObjective($"Enemies defeated: {_enemiesKilled}/{_totalEnemies}");

            if (_enemiesKilled >= _totalEnemies && !_levelComplete) {
                if (_totalEnemies == 5) {
                    SpawnWave2();
                } else {
                    CompleteTutorial();
                }
            }
        }

        private void CompleteTutorial() {
            _levelComplete = true;
            _combatTrialActive = false;

            if (StoryManager.Instance != null) {
                StoryManager.Instance.TutorialComplete = true;
                StoryManager.Instance.AdvanceToNextLevel();
            }

            UpdateObjective("CALIBRATION COMPLETE! Returning to Time-Ship...");

            var timer = GetTree().CreateTimer(3.0);
            timer.Timeout += () => StoryManager.Instance?.ReturnToHub();
        }

        private void BuildHUD() {
            var canvas = new CanvasLayer();
            canvas.Name = "TutorialHUD";
            canvas.Layer = 10;
            AddChild(canvas);

            var levelLabel = new Label();
            levelLabel.Text = "LEVEL 0: CHRONAL INTEGRATION";
            levelLabel.Position = new Vector2(20, 15);
            levelLabel.AddThemeFontSizeOverride("font_size", 16);
            levelLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.9f, 0.9f));
            canvas.AddChild(levelLabel);

            _objectiveLabel = new Label();
            _objectiveLabel.Text = "Defeat the hologram drones! (Wave 1)";
            _objectiveLabel.Position = new Vector2(20, 45);
            _objectiveLabel.AddThemeFontSizeOverride("font_size", 13);
            _objectiveLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.2f));
            canvas.AddChild(_objectiveLabel);

            var controlsLabel = new Label();
            controlsLabel.Text = "Move: A/D | Jump: Space | Attack: J | Special1: K | Special2: L | Block: I";
            controlsLabel.Position = new Vector2(20, 1040);
            controlsLabel.AddThemeFontSizeOverride("font_size", 12);
            controlsLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
            canvas.AddChild(controlsLabel);
        }

        private void UpdateObjective(string text) {
            if (_objectiveLabel != null) _objectiveLabel.Text = text;
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
            }
        }
    }
}
