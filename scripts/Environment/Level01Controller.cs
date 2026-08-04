using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;

namespace FTT.Environment {

    public partial class Level01Controller : Node2D {
        private PlayerController _player;
        private BossController _boss;
        private Label _objectiveLabel;
        private Label _bossHPLabel;
        private LevelManager _levelManager;
        private bool _bossDefeated;
        private bool _levelComplete;

        private const float LevelWidth = 11520f;
        private const float LevelHeight = 1080f;

        public override void _Ready() {
            _levelManager = new LevelManager();
            _levelManager.Name = "LevelManager";
            _levelManager.LevelID = "level_01_florence";
            _levelManager.LevelDisplayName = "The Steampunk Renaissance";
            AddChild(_levelManager);

            BuildLevel();
            SpawnPlayer();
            SpawnEnemies();
            BuildHUD();

            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
            }
        }

        private void BuildLevel() {
            BuildRoom1();
            BuildRoom2();
            BuildRoom3();
            BuildRoom4BossArena();
        }

        private void BuildRoom1() {
            var bg1 = new ColorRect();
            bg1.Name = "BG_Room1";
            bg1.Size = new Vector2(5760, LevelHeight);
            bg1.Color = new Color(0.12f, 0.08f, 0.05f);
            AddChild(bg1);

            BuildFloor(0, 900, 5760);
            BuildPlatform(400, 700, 300);
            BuildPlatform(900, 550, 250);
            BuildPlatform(1400, 650, 280);
            BuildPlatform(2000, 500, 220);
            BuildPlatform(2500, 700, 300);
            BuildPlatform(3200, 600, 250);
            BuildPlatform(3800, 720, 200);
            BuildPlatform(4300, 550, 250);
            BuildPlatform(5000, 650, 220);

            BuildHazardSpikes(600, 890, 150);
            BuildHazardSpikes(1600, 890, 100);
            BuildHazardSpikes(3500, 890, 120);

            BuildCheckpoint(2800, 850, "florence_checkpoint_0");

            BuildRoomDecoration(0, "ENTRY COURTYARD", new Color(0.7f, 0.5f, 0.2f));
        }

        private void BuildRoom2() {
            float offsetX = 5760;

            var bg2 = new ColorRect();
            bg2.Name = "BG_Room2";
            bg2.Size = new Vector2(2880, LevelHeight);
            bg2.Position = new Vector2(offsetX, 0);
            bg2.Color = new Color(0.09f, 0.06f, 0.04f);
            AddChild(bg2);

            BuildFloor(offsetX, 900, 2880);
            BuildPlatform(offsetX + 300, 750, 200);
            BuildPlatform(offsetX + 700, 600, 250);
            BuildPlatform(offsetX + 1100, 450, 200);
            BuildPlatform(offsetX + 1500, 600, 220);
            BuildPlatform(offsetX + 1900, 700, 250);
            BuildPlatform(offsetX + 2400, 550, 200);

            BuildCheckpoint(offsetX + 1400, 850, "florence_checkpoint_1");
            BuildRoomDecoration(offsetX, "PRINT SHOP BASEMENT", new Color(0.6f, 0.4f, 0.2f));
        }

        private void BuildRoom3() {
            float offsetX = 8640;

            var bg3 = new ColorRect();
            bg3.Name = "BG_Room3";
            bg3.Size = new Vector2(1920, LevelHeight);
            bg3.Position = new Vector2(offsetX, 0);
            bg3.Color = new Color(0.1f, 0.07f, 0.05f);
            AddChild(bg3);

            BuildFloor(offsetX, 900, 1920);
            BuildPlatform(offsetX + 300, 700, 200);
            BuildPlatform(offsetX + 700, 550, 250);
            BuildPlatform(offsetX + 1100, 650, 200);
            BuildPlatform(offsetX + 1500, 500, 220);

            BuildHazardSpikes(offsetX + 400, 890, 100);
            BuildHazardSpikes(offsetX + 900, 890, 80);

            BuildRoomDecoration(offsetX, "WORKSHOP PASSAGE", new Color(0.5f, 0.3f, 0.15f));
        }

        private void BuildRoom4BossArena() {
            float offsetX = 10560;

            var bg4 = new ColorRect();
            bg4.Name = "BG_Room4_Boss";
            bg4.Size = new Vector2(960, LevelHeight);
            bg4.Position = new Vector2(offsetX, 0);
            bg4.Color = new Color(0.15f, 0.05f, 0.05f);
            AddChild(bg4);

            BuildFloor(offsetX, 900, 960);
            BuildPlatform(offsetX + 300, 650, 200);
            BuildPlatform(offsetX + 660, 650, 200);

            BuildWall(offsetX, 0, LevelHeight);
            BuildWall(offsetX + 940, 0, LevelHeight);

            BuildCheckpoint(offsetX + 100, 850, "florence_checkpoint_2");
            BuildRoomDecoration(offsetX, "BOSS: THE BORGIA INQUISITOR", new Color(0.9f, 0.2f, 0.2f));

            SpawnBoss(offsetX + 700, 850);
        }

        private void BuildFloor(float x, float y, float width) {
            var floor = new StaticBody2D();
            floor.CollisionLayer = CollisionLayers.Environment;
            floor.CollisionMask = 0;
            floor.Name = $"Floor_{x}";
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
            visual.Color = new Color(0.25f, 0.18f, 0.1f);
            floor.AddChild(visual);

            var topLine = new ColorRect();
            topLine.Size = new Vector2(width, 3);
            topLine.Position = new Vector2(-width / 2f, 0);
            topLine.Color = new Color(0.5f, 0.35f, 0.15f);
            floor.AddChild(topLine);

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
            visual.Color = new Color(0.35f, 0.22f, 0.1f);
            plat.AddChild(visual);

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
            visual.Color = new Color(0.2f, 0.15f, 0.08f);
            wall.AddChild(visual);

            AddChild(wall);
        }

        private void BuildHazardSpikes(float x, float y, float width) {
            var hazard = new Area2D();
            hazard.Name = $"Spikes_{x}";
            hazard.Position = new Vector2(x, y);
            hazard.CollisionLayer = CollisionLayers.Trigger;
            hazard.CollisionMask = CollisionLayers.Player;

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(width, 10);
            col.Shape = shape;
            hazard.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(width, 10);
            visual.Position = new Vector2(-width / 2f, -5);
            visual.Color = new Color(0.6f, 0.2f, 0.1f);
            hazard.AddChild(visual);

            hazard.BodyEntered += (body) => {
                if (body is PlayerController pc) {
                    pc.ApplyDamage(15);
                }
            };

            AddChild(hazard);
        }

        private void BuildCheckpoint(float x, float y, string id) {
            var checkpoint = new CheckpointTrigger();
            checkpoint.Name = $"Checkpoint_{id}";
            checkpoint.CheckpointID = id;
            checkpoint.Position = new Vector2(x, y);
            checkpoint.RespawnOffset = new Vector2(0, -50);

            checkpoint.CollisionLayer = CollisionLayers.Trigger;
            checkpoint.CollisionMask = CollisionLayers.Player;

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(60, 120);
            col.Shape = shape;
            col.Position = new Vector2(0, -60);
            checkpoint.AddChild(col);

            var visual = new ColorRect();
            visual.Name = "CheckpointVisual";
            visual.Size = new Vector2(20, 80);
            visual.Position = new Vector2(-10, -80);
            visual.Color = new Color(0.0f, 0.7f, 0.9f, 0.6f);
            checkpoint.AddChild(visual);

            var label = new Label();
            label.Text = "CHECKPOINT";
            label.Position = new Vector2(-40, -100);
            label.CustomMinimumSize = new Vector2(80, 16);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            checkpoint.AddChild(label);

            AddChild(checkpoint);
        }

        private void BuildRoomDecoration(float offsetX, string roomName, Color color) {
            var label = new Label();
            label.Text = roomName;
            label.Position = new Vector2(offsetX + 50, 30);
            label.CustomMinimumSize = new Vector2(300, 25);
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", color);
            AddChild(label);
        }

        private void SpawnPlayer() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            _player = CharacterFactory.CreateCharacter(characterID, 0);
            _player.Name = "Player";
            _player.Position = new Vector2(200, 850);
            AddChild(_player);

            var camera = new Camera2D();
            camera.Name = "PlayerCamera";
            camera.Enabled = true;
            camera.LimitLeft = 0;
            camera.LimitRight = (int)LevelWidth;
            camera.LimitTop = 0;
            camera.LimitBottom = (int)LevelHeight;
            camera.PositionSmoothingEnabled = true;
            camera.PositionSmoothingSpeed = 5.0f;
            _player.AddChild(camera);
        }

        private void SpawnEnemies() {
            AddChild(EnemyFactory.CreateChronoSlasher(new Vector2(800, 850),
                waypointA: new Vector2(600, 850), waypointB: new Vector2(1000, 850)));
            AddChild(EnemyFactory.CreateChronoSlasher(new Vector2(1800, 850),
                waypointA: new Vector2(1600, 850), waypointB: new Vector2(2000, 850)));
            AddChild(EnemyFactory.CreateCyberGuard(new Vector2(2200, 850),
                waypointA: new Vector2(2000, 850), waypointB: new Vector2(2400, 850)));
            AddChild(EnemyFactory.CreateCyberGuard(new Vector2(3000, 850),
                waypointA: new Vector2(2800, 850), waypointB: new Vector2(3200, 850)));
            AddChild(EnemyFactory.CreateSteamAutomaton(new Vector2(4000, 850)));

            float r2Offset = 5760;
            AddChild(EnemyFactory.CreateCyberGuard(new Vector2(r2Offset + 500, 850),
                waypointA: new Vector2(r2Offset + 300, 850), waypointB: new Vector2(r2Offset + 700, 850)));
            AddChild(EnemyFactory.CreateCyberGuard(new Vector2(r2Offset + 1200, 850)));
            AddChild(EnemyFactory.CreateChronoSlasher(new Vector2(r2Offset + 2000, 850),
                waypointA: new Vector2(r2Offset + 1800, 850), waypointB: new Vector2(r2Offset + 2200, 850)));

            float r3Offset = 8640;
            AddChild(EnemyFactory.CreateChronoSlasher(new Vector2(r3Offset + 400, 850)));
            AddChild(EnemyFactory.CreateChronoSlasher(new Vector2(r3Offset + 800, 850)));
            AddChild(EnemyFactory.CreateChronoSlasher(new Vector2(r3Offset + 1200, 850)));
            AddChild(EnemyFactory.CreateCyberGuard(new Vector2(r3Offset + 600, 850),
                waypointA: new Vector2(r3Offset + 400, 850), waypointB: new Vector2(r3Offset + 900, 850)));
            AddChild(EnemyFactory.CreateCyberGuard(new Vector2(r3Offset + 1400, 850)));
            AddChild(EnemyFactory.CreateSteamAutomaton(new Vector2(r3Offset + 1000, 850)));
            AddChild(EnemyFactory.CreateSteamAutomaton(new Vector2(r3Offset + 1600, 850)));
        }

        private void SpawnBoss(float x, float y) {
            _boss = new BossController();
            _boss.Name = "BorgiaInquisitor";
            _boss.Position = new Vector2(x, y);

            _boss.Data = GD.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres");

            _boss.CollisionLayer = CollisionLayers.Enemy;
            _boss.CollisionMask = CollisionLayers.EnemyBodyMask;
            _boss.MotionMode = CharacterBody2D.MotionModeEnum.Grounded;
            _boss.UpDirection = Vector2.Up;
            _boss.FloorStopOnSlope = true;

            var col = new CollisionShape2D();
            col.Name = "CollisionShape2D";
            var colRect = new RectangleShape2D();
            colRect.Size = new Vector2(50, 70);
            col.Shape = colRect;
            col.Position = new Vector2(0, -35);
            _boss.AddChild(col);

            var body = new ColorRect();
            body.Name = "BossBody";
            body.Size = new Vector2(50, 70);
            body.Position = new Vector2(-25, -70);
            body.Color = new Color(0.7f, 0.1f, 0.1f);
            _boss.AddChild(body);

            var headRect = new ColorRect();
            headRect.Size = new Vector2(30, 20);
            headRect.Position = new Vector2(-15, -90);
            headRect.Color = new Color(0.5f, 0.05f, 0.05f);
            _boss.AddChild(headRect);

            var bossLabel = new Label();
            bossLabel.Text = "THE BORGIA INQUISITOR";
            bossLabel.Position = new Vector2(-80, -110);
            bossLabel.CustomMinimumSize = new Vector2(160, 20);
            bossLabel.HorizontalAlignment = HorizontalAlignment.Center;
            bossLabel.AddThemeFontSizeOverride("font_size", 10);
            bossLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f));
            _boss.AddChild(bossLabel);

            var hurtbox = new Area2D();
            hurtbox.Name = "Hurtbox";
            hurtbox.CollisionLayer = CollisionLayers.EnemyHurtbox;
            hurtbox.CollisionMask = CollisionLayers.PlayerHitbox | CollisionLayers.Projectile;
            var hurtShape = new CollisionShape2D();
            var hurtRect = new RectangleShape2D();
            hurtRect.Size = new Vector2(50, 70);
            hurtShape.Shape = hurtRect;
            hurtShape.Position = new Vector2(0, -35);
            hurtbox.AddChild(hurtShape);
            _boss.AddChild(hurtbox);

            _boss.AddToGroup("Enemies");
            AddChild(_boss);
        }

        private void BuildHUD() {
            var canvas = new CanvasLayer();
            canvas.Name = "LevelHUD";
            canvas.Layer = 10;
            AddChild(canvas);

            var levelLabel = new Label();
            levelLabel.Text = "LEVEL 1: THE STEAMPUNK RENAISSANCE - FLORENCE, 1503";
            levelLabel.Position = new Vector2(20, 15);
            levelLabel.AddThemeFontSizeOverride("font_size", 14);
            levelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.2f));
            canvas.AddChild(levelLabel);

            _objectiveLabel = new Label();
            _objectiveLabel.Text = "Clear the enemies and reach the boss!";
            _objectiveLabel.Position = new Vector2(20, 40);
            _objectiveLabel.AddThemeFontSizeOverride("font_size", 12);
            _objectiveLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.6f));
            canvas.AddChild(_objectiveLabel);

            _bossHPLabel = new Label();
            _bossHPLabel.Name = "BossHP";
            _bossHPLabel.Position = new Vector2(660, 15);
            _bossHPLabel.CustomMinimumSize = new Vector2(600, 25);
            _bossHPLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _bossHPLabel.AddThemeFontSizeOverride("font_size", 14);
            _bossHPLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f));
            _bossHPLabel.Visible = false;
            canvas.AddChild(_bossHPLabel);
        }

        public override void _Process(double delta) {
            if (_boss != null && !_bossDefeated) {
                if (_boss.CurrentState == BossState.Dead) {
                    OnBossDefeated();
                } else if (_player != null) {
                    float dist = _player.GlobalPosition.DistanceTo(_boss.GlobalPosition);
                    if (dist < 800) {
                        _bossHPLabel.Visible = true;
                        _bossHPLabel.Text = $"THE BORGIA INQUISITOR - HP: {_boss.CurrentHP}/{_boss.Data.MaxHP}";
                    }
                }
            }
        }

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            EventBus.Instance?.RaiseChronalDustCollected(payload.ChronalDustDrop);
        }

        private void OnBossDefeated() {
            _bossDefeated = true;
            _bossHPLabel.Text = "THE BORGIA INQUISITOR - DEFEATED!";

            EventBus.Instance?.RaiseChronalDustCollected(50);

            _objectiveLabel.Text = "LEVEL COMPLETE! Returning to Time-Ship...";

            var timer = GetTree().CreateTimer(4.0);
            timer.Timeout += () => {
                _levelManager?.CompleteLevel();
                StoryManager.Instance?.ReturnToHub();
            };
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
            }
        }
    }
}
