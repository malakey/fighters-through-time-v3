using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Level 1 - Florence, 1503 (Steampunk Renaissance). Four-room progression:
    /// entry streets, the print-shop gear puzzle that unlocks the workshop door,
    /// the workshop gauntlet, and the Borgia Inquisitor boss arena. Rooms two and
    /// three spawn difficulty-scaled waves on first entry.
    /// </summary>
    public partial class Level01Controller : Node2D {
        private PlayerController _player;
        private BossController _boss;
        private LevelManager _levelManager;
        private StorySceneServices _services;

        private PuzzleManager _gearPuzzle;
        private StaticBody2D _workshopDoor;

        private bool _room2WaveSpawned;
        private bool _room3WaveSpawned;
        private bool _bossIntroShown;
        private bool _bossDefeated;
        private bool _levelComplete;
        private int _dustEarnedThisLevel;

        private const float LevelWidth = 11520f;
        private const float LevelHeight = 1080f;
        private const float Room2OffsetX = 5760f;
        private const float Room3OffsetX = 8640f;
        private const float Room4OffsetX = 10560f;

        public override void _Ready() {
            _levelManager = new LevelManager();
            _levelManager.Name = "LevelManager";
            _levelManager.LevelID = "level_01_florence";
            _levelManager.LevelDisplayName = "florence_level_title";
            AddChild(_levelManager);

            BuildLevel();
            SpawnPlayer();
            SpawnRoom1Enemies();

            _services = StorySceneBootstrapper.Attach(
                this, "res://resources/Dialogue/level_01_dialogue.tres");
            _services.HUD?.SetLevelTitle("florence_level_title");
            _services.HUD?.SetObjective("florence_objective_reach_boss");

            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete += OnDialogueComplete;
            }

            bool resumedMidLevel = _levelManager.LastCheckpointID.Length > 0;
            if (!resumedMidLevel) CallDeferred(MethodName.StartEntranceDialogue);
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
            }
        }

        private void StartEntranceDialogue() {
            _services?.Dialogue?.StartSequence("level_01.entrance");
        }

        private void OnDialogueComplete(string dialogueID) {
            if (dialogueID == "level_01.exit") ShowCompletionResults();
        }

        // === Level construction ===

        private void BuildLevel() {
            BuildFarBackground();
            BuildRoom1();
            BuildRoom2();
            BuildRoom3();
            BuildRoom4BossArena();
        }

        private void BuildFarBackground() {
            Texture2D texture = ResourceLoader.Load<Texture2D>(
                "res://assets/environments/florence/florence_far_background.png");
            if (texture == null) return;
            Vector2 textureSize = texture.GetSize();
            Vector2 scale = new(1920f / textureSize.X, LevelHeight / textureSize.Y);
            for (int index = 0; index < 6; index++) {
                var background = new Sprite2D {
                    Name = $"FlorenceFarBackground_{index}",
                    Texture = texture,
                    Position = new Vector2(index * 1920f + 960f, LevelHeight / 2f),
                    Scale = scale,
                    ZIndex = -20,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear
                };
                AddChild(background);
            }
        }

        private void BuildRoom1() {
            var bg1 = new ColorRect();
            bg1.Name = "BG_Room1";
            bg1.Size = new Vector2(5760, LevelHeight);
            bg1.Color = new Color(0.12f, 0.08f, 0.05f, 0.35f);
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

            BuildRoomDecoration(0, "florence_room_entry", new Color(0.7f, 0.5f, 0.2f));
        }

        private void BuildRoom2() {
            float offsetX = Room2OffsetX;

            var bg2 = new ColorRect();
            bg2.Name = "BG_Room2";
            bg2.Size = new Vector2(2880, LevelHeight);
            bg2.Position = new Vector2(offsetX, 0);
            bg2.Color = new Color(0.09f, 0.06f, 0.04f, 0.4f);
            AddChild(bg2);

            BuildFloor(offsetX, 900, 2880);
            BuildPlatform(offsetX + 300, 750, 200);
            BuildPlatform(offsetX + 700, 600, 250);
            BuildPlatform(offsetX + 1100, 450, 200);
            BuildPlatform(offsetX + 1500, 600, 220);
            BuildPlatform(offsetX + 1900, 700, 250);
            BuildPlatform(offsetX + 2400, 550, 200);

            BuildCheckpoint(offsetX + 1400, 850, "florence_checkpoint_1");
            BuildRoomDecoration(offsetX, "florence_room_print_shop", new Color(0.6f, 0.4f, 0.2f));

            BuildWaveTrigger("Room2WaveTrigger", new Vector2(offsetX + 120, 800), () => {
                if (_gearPuzzle != null && !_gearPuzzle.IsCompleted) {
                    _services?.HUD?.SetObjective("florence_objective_gears");
                }
                if (_room2WaveSpawned) return;
                _room2WaveSpawned = true;
                SpawnRoom2Enemies();
            });

            BuildGearPuzzle(offsetX);
        }

        /// <summary>
        /// Print-shop gear train: gear A drives gear B, gear B turns alone. Both
        /// must be aligned upright to unlock the workshop door into room three.
        /// </summary>
        private void BuildGearPuzzle(float offsetX) {
            _gearPuzzle = new PuzzleManager {
                Name = "GearPuzzle",
                PuzzleID = "florence_gear_puzzle",
                RequiredConditionIDs = new[] { "gear_a_aligned", "gear_b_aligned" }
            };
            AddChild(_gearPuzzle);
            _gearPuzzle.PuzzleCompleted += _ => OpenWorkshopDoor();

            var gearA = BuildGear("GearA", new Vector2(offsetX + 2000, 820), initialQuarterTurns: 1,
                conditionID: "gear_a_aligned");
            var gearB = BuildGear("GearB", new Vector2(offsetX + 2350, 820), initialQuarterTurns: 2,
                conditionID: "gear_b_aligned");
            gearA.LinkedGearPaths.Add(gearA.GetPathTo(gearB));

            // Deferred: PuzzleManager may already hold saved completion.
            CallDeferred(MethodName.ApplySavedPuzzleState);
        }

        private void ApplySavedPuzzleState() {
            if (_gearPuzzle != null && _gearPuzzle.IsCompleted) OpenWorkshopDoor();
        }

        private RotatingGear BuildGear(string name, Vector2 position, int initialQuarterTurns, string conditionID) {
            var gear = new RotatingGear {
                Name = name,
                GearID = $"florence_{name.ToLowerInvariant()}",
                Position = position,
                RotationDegrees = initialQuarterTurns * 90f,
                ConditionID = conditionID,
                TargetQuarterTurns = 0,
                // Absolute path: the puzzle manager is already in the tree.
                PuzzleManagerPath = _gearPuzzle.GetPath()
            };

            var hub = new ColorRect {
                Size = new Vector2(30, 30),
                Position = new Vector2(-15, -15),
                Color = new Color(0.55f, 0.4f, 0.15f)
            };
            gear.AddChild(hub);
            // The alignment pointer makes the current orientation readable.
            var pointer = new ColorRect {
                Size = new Vector2(10, 45),
                Position = new Vector2(-5, -60),
                Color = new Color(0.9f, 0.75f, 0.3f)
            };
            gear.AddChild(pointer);
            var toothLeft = new ColorRect {
                Size = new Vector2(45, 10),
                Position = new Vector2(-60, -5),
                Color = new Color(0.45f, 0.32f, 0.12f)
            };
            gear.AddChild(toothLeft);
            var toothRight = new ColorRect {
                Size = new Vector2(45, 10),
                Position = new Vector2(15, -5),
                Color = new Color(0.45f, 0.32f, 0.12f)
            };
            gear.AddChild(toothRight);

            AddChild(gear);

            // Sibling interaction area so the trigger and prompt do not rotate
            // with the gear.
            var interaction = new InteractionArea {
                Name = $"{name}Interaction",
                Position = position,
                TargetPath = gear.GetPath()
            };
            var shape = new CollisionShape2D();
            shape.Shape = new RectangleShape2D { Size = new Vector2(140, 160) };
            interaction.AddChild(shape);
            var prompt = new Label {
                Name = "Prompt",
                Visible = false,
                Position = new Vector2(-70, -110),
                CustomMinimumSize = new Vector2(140, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            prompt.AddThemeFontSizeOverride("font_size", 11);
            prompt.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.4f));
            interaction.AddChild(prompt);
            AddChild(interaction);

            return gear;
        }

        private void OpenWorkshopDoor() {
            if (_workshopDoor == null || !IsInstanceValid(_workshopDoor)) return;
            _workshopDoor.QueueFree();
            _workshopDoor = null;
            _services?.HUD?.SetObjective("florence_objective_reach_boss");
        }

        private void BuildRoom3() {
            float offsetX = Room3OffsetX;

            var bg3 = new ColorRect();
            bg3.Name = "BG_Room3";
            bg3.Size = new Vector2(1920, LevelHeight);
            bg3.Position = new Vector2(offsetX, 0);
            bg3.Color = new Color(0.1f, 0.07f, 0.05f, 0.4f);
            AddChild(bg3);

            BuildFloor(offsetX, 900, 1920);
            BuildPlatform(offsetX + 300, 700, 200);
            BuildPlatform(offsetX + 700, 550, 250);
            BuildPlatform(offsetX + 1100, 650, 200);
            BuildPlatform(offsetX + 1500, 500, 220);

            BuildHazardSpikes(offsetX + 400, 890, 100);
            BuildHazardSpikes(offsetX + 900, 890, 80);

            BuildRoomDecoration(offsetX, "florence_room_workshop", new Color(0.5f, 0.3f, 0.15f));

            _workshopDoor = BuildDoor("WorkshopDoor", new Vector2(offsetX, 0));

            BuildWaveTrigger("Room3WaveTrigger", new Vector2(offsetX + 150, 800), () => {
                if (_room3WaveSpawned) return;
                _room3WaveSpawned = true;
                SpawnRoom3Enemies();
            });
        }

        /// <summary>Solid placeholder door; removed when the gear puzzle completes.</summary>
        private StaticBody2D BuildDoor(string name, Vector2 position) {
            var door = new StaticBody2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = new Vector2(40, 400) };
            col.Position = new Vector2(0, 700);
            door.AddChild(col);

            var visual = new ColorRect {
                Size = new Vector2(40, 400),
                Position = new Vector2(-20, 500),
                Color = new Color(0.5f, 0.35f, 0.12f)
            };
            door.AddChild(visual);

            var lockLabel = new Label {
                Text = Tr("florence_door_locked"),
                Position = new Vector2(-90, 460),
                CustomMinimumSize = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            lockLabel.AddThemeFontSizeOverride("font_size", 11);
            lockLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.2f));
            door.AddChild(lockLabel);

            AddChild(door);
            return door;
        }

        private void BuildRoom4BossArena() {
            float offsetX = Room4OffsetX;

            var bg4 = new ColorRect();
            bg4.Name = "BG_Room4_Boss";
            bg4.Size = new Vector2(960, LevelHeight);
            bg4.Position = new Vector2(offsetX, 0);
            bg4.Color = new Color(0.15f, 0.05f, 0.05f, 0.45f);
            AddChild(bg4);

            BuildFloor(offsetX, 900, 960);
            BuildPlatform(offsetX + 300, 650, 200);
            BuildPlatform(offsetX + 660, 650, 200);

            BuildWall(offsetX, 0, LevelHeight);
            BuildWall(offsetX + 940, 0, LevelHeight);

            BuildCheckpoint(offsetX + 100, 850, "florence_checkpoint_2");
            BuildRoomDecoration(offsetX, "florence_room_boss", new Color(0.9f, 0.2f, 0.2f));

            SpawnBoss(offsetX + 700, 850);
        }

        private void BuildWaveTrigger(string name, Vector2 position, System.Action onEntered) {
            var trigger = new Area2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = new Vector2(60, 400) };
            trigger.AddChild(col);
            trigger.BodyEntered += body => {
                if (body is PlayerController) onEntered();
            };
            AddChild(trigger);
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
            _levelManager.RegisterCheckpoint(id, checkpoint.Position + checkpoint.RespawnOffset);

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
            label.Text = Tr("checkpoint");
            label.Position = new Vector2(-40, -100);
            label.CustomMinimumSize = new Vector2(80, 16);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            checkpoint.AddChild(label);

            AddChild(checkpoint);
        }

        private void BuildRoomDecoration(float offsetX, string roomNameKey, Color color) {
            var label = new Label();
            label.Text = Tr(roomNameKey);
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
            RestoreSavedCheckpoint();

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

        private void RestoreSavedCheckpoint() {
            if (SaveManager.Instance == null || GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || save.CurrentLevelID != StoryManager.GetLevelScenePath(CampaignLevel.Florence)) return;
            Vector2 position = _player.Position;
            if (_levelManager.TryGetCheckpointPosition(save.LastCheckpointID, out Vector2 checkpoint)) {
                position = checkpoint;
                _levelManager.SetCheckpointPosition(save.LastCheckpointID, checkpoint);
                // Resuming past the print shop means the waves ahead of the
                // checkpoint were already fought.
                if (save.LastCheckpointID == "florence_checkpoint_1") _room2WaveSpawned = true;
                if (save.LastCheckpointID == "florence_checkpoint_2") {
                    _room2WaveSpawned = true;
                    _room3WaveSpawned = true;
                }
            }
            _player.RestoreStoryCheckpoint(position, save.CurrentHP, save.CurrentUltimateMeter);
        }

        // === Encounters ===

        private void SpawnRoom1Enemies() {
            var difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            int count = StoryDifficultyTuning.ScaleEncounterCount(5, difficulty);

            System.Action[] spawns = {
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(800, 850),
                    waypointA: new Vector2(600, 850), waypointB: new Vector2(1000, 850)),
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(1800, 850),
                    waypointA: new Vector2(1600, 850), waypointB: new Vector2(2000, 850)),
                () => EnemyFactory.SpawnCyberGuard(this, new Vector2(2200, 850),
                    waypointA: new Vector2(2000, 850), waypointB: new Vector2(2400, 850)),
                () => EnemyFactory.SpawnCyberGuard(this, new Vector2(3000, 850),
                    waypointA: new Vector2(2800, 850), waypointB: new Vector2(3200, 850)),
                () => EnemyFactory.SpawnSteamAutomaton(this, new Vector2(4000, 850))
            };
            for (int index = 0; index < count && index < spawns.Length; index++) spawns[index]();
        }

        private void SpawnRoom2Enemies() {
            var difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            int count = StoryDifficultyTuning.ScaleEncounterCount(3, difficulty);
            float offsetX = Room2OffsetX;

            System.Action[] spawns = {
                () => EnemyFactory.SpawnCyberGuard(this, new Vector2(offsetX + 500, 850),
                    waypointA: new Vector2(offsetX + 300, 850), waypointB: new Vector2(offsetX + 700, 850)),
                () => EnemyFactory.SpawnCyberGuard(this, new Vector2(offsetX + 1200, 850)),
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(offsetX + 2000, 850),
                    waypointA: new Vector2(offsetX + 1800, 850), waypointB: new Vector2(offsetX + 2200, 850)),
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(offsetX + 900, 850))
            };
            for (int index = 0; index < count && index < spawns.Length; index++) spawns[index]();
        }

        private void SpawnRoom3Enemies() {
            var difficulty = StoryDifficultyTuning.CurrentStoryDifficulty;
            int count = StoryDifficultyTuning.ScaleEncounterCount(7, difficulty);
            float offsetX = Room3OffsetX;

            System.Action[] spawns = {
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(offsetX + 400, 850)),
                () => EnemyFactory.SpawnCyberGuard(this, new Vector2(offsetX + 600, 850),
                    waypointA: new Vector2(offsetX + 400, 850), waypointB: new Vector2(offsetX + 900, 850)),
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(offsetX + 800, 850)),
                () => EnemyFactory.SpawnSteamAutomaton(this, new Vector2(offsetX + 1000, 850)),
                () => EnemyFactory.SpawnChronoSlasher(this, new Vector2(offsetX + 1200, 850)),
                () => EnemyFactory.SpawnCyberGuard(this, new Vector2(offsetX + 1400, 850)),
                () => EnemyFactory.SpawnSteamAutomaton(this, new Vector2(offsetX + 1600, 850))
            };
            for (int index = 0; index < count && index < spawns.Length; index++) spawns[index]();
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

            var pushbox = new FTT.Combat.CombatantPushbox {
                Name = "Pushbox",
                BoxSize = new Vector2(44f, 60f),
                Position = new Vector2(0f, -35f),
                BlocksRollThrough = true,
                CollisionLayer = CollisionLayers.Enemy,
                CollisionMask = CollisionLayers.Player,
                Monitoring = false,
                Monitorable = false
            };
            pushbox.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = pushbox.BoxSize }
            });
            _boss.AddChild(pushbox);

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
            bossLabel.Text = Tr("boss_borgia_inquisitor_name").ToUpperInvariant();
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

        // === Boss and completion flow ===

        public override void _Process(double delta) {
            if (_boss == null || _bossDefeated) return;

            if (_boss.CurrentState == BossState.Dead) {
                OnBossDefeated();
                return;
            }
            if (_player == null) return;

            float dist = _player.GlobalPosition.DistanceTo(_boss.GlobalPosition);
            if (dist < 800) {
                if (!_bossIntroShown) {
                    _bossIntroShown = true;
                    _services?.HUD?.ShowBossBar("boss_borgia_inquisitor_name", _boss.CurrentHP, _boss.ScaledMaxHP);
                    _services?.HUD?.SetObjective("florence_objective_defeat_boss");
                    _services?.Dialogue?.StartSequence("level_01.boss_intro");
                }
                _services?.HUD?.UpdateBossHP(_boss.CurrentHP);
            }
        }

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            _dustEarnedThisLevel += payload.ChronalDustDrop;
            EventBus.Instance?.RaiseChronalDustCollected(payload.ChronalDustDrop);
        }

        private void OnBossDefeated() {
            _bossDefeated = true;
            _services?.HUD?.HideBossBar();
            _services?.HUD?.SetObjective("florence_objective_complete");

            // Boss dust reward is resource-authored (docs/DUST_ECONOMY.md Section 1).
            int bossDustReward = _boss?.Data?.ChronalDustDrop ?? 50;
            _dustEarnedThisLevel += bossDustReward;
            EventBus.Instance?.RaiseChronalDustCollected(bossDustReward);

            var timer = GetTree().CreateTimer(1.5);
            timer.Timeout += () => _services?.Dialogue?.StartSequence("level_01.exit");
        }

        private void ShowCompletionResults() {
            if (_levelComplete) return;
            _levelComplete = true;
            // Raises OnLevelComplete: advances the campaign and autosaves completion.
            _levelManager?.CompleteLevel();

            var results = LevelResultsPanel.CreateDefault();
            results.ReturnRequested += () => StoryManager.Instance?.ReturnToHub();
            AddChild(results);
            results.ShowResults("florence_level_title", _dustEarnedThisLevel);
        }
    }
}
