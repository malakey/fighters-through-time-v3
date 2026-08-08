using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Level 0 - Chronal Integration tutorial. Three documented parts: the
    /// fracture presentation, guided calibration (attacks, special, Chronal
    /// Rewind) against a training dummy, and advanced mobility gates (double
    /// jump, dash, roll) followed by the holographic combat trial.
    /// </summary>
    public partial class Level00Controller : Node2D {
        private enum TutorialPhase { Fracture, Calibration, Mobility, CombatTrial, Complete }
        private enum CalibrationStep { BasicHits, UseSpecial, UseRewind, Done }

        private const int RequiredBasicHits = 3;

        private PlayerController _player;
        private TrainingDummy _dummy;
        private StorySceneServices _services;
        private LevelManager _levelManager;

        private TutorialPhase _phase = TutorialPhase.Fracture;
        private CalibrationStep _calibrationStep = CalibrationStep.BasicHits;
        private int _basicHitsLanded;

        private Area2D _doubleJumpGate;
        private Area2D _dashGate;
        private Area2D _rollGate;
        private bool _doubleJumpGateCleared;
        private bool _dashGateCleared;
        private bool _rollGateCleared;

        private int _enemiesKilled;
        private int _totalEnemies;
        private bool _levelComplete;

        public override void _Ready() {
            BuildLevel();
            SpawnPlayer();

            _levelManager = new LevelManager {
                Name = "LevelManager",
                LevelID = "level_00_tutorial",
                LevelDisplayName = "tutorial_level_title"
            };
            AddChild(_levelManager);
            _levelManager.SetCheckpointPosition("l00_start", new Vector2(400, 850));

            _services = StorySceneBootstrapper.Attach(
                this, "res://resources/Dialogue/level_00_dialogue.tres");
            _services.HUD?.SetLevelTitle("tutorial_level_title");
            _services.HUD?.SetObjective("tutorial_objective_fracture");

            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete += OnDialogueComplete;
                EventBus.Instance.OnCooldownStarted += OnCooldownStarted;
                EventBus.Instance.OnRewindTriggered += OnRewindTriggered;
            }

            // Defer one frame so the dialogue UI is fully in the tree before pausing.
            CallDeferred(MethodName.StartFracturePresentation);
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
                EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
                EventBus.Instance.OnCooldownStarted -= OnCooldownStarted;
                EventBus.Instance.OnRewindTriggered -= OnRewindTriggered;
            }
            if (_dummy != null && IsInstanceValid(_dummy)) _dummy.HitLanded -= OnDummyHit;
            // Pooled combat-trial enemies are parented here; hand them back or the
            // pool keeps freed references once this scene unloads.
            PoolManager.Instance?.ReleaseActiveUnder(this);
        }

        // === Part 1: fracture presentation ===

        private void StartFracturePresentation() {
            _services?.Dialogue?.StartSequence("level_00.intro");
        }

        private void OnDialogueComplete(string dialogueID) {
            switch (dialogueID) {
                case "level_00.intro":
                    BeginCalibration();
                    break;
                case "level_00.mobility_intro":
                    BeginMobilityGates();
                    break;
                case "level_00.complete":
                    ShowCompletionResults();
                    break;
            }
        }

        // === Part 2: calibration ===

        private void BeginCalibration() {
            _phase = TutorialPhase.Calibration;
            _calibrationStep = CalibrationStep.BasicHits;

            _dummy = new TrainingDummy { Name = "CalibrationDummy", Position = new Vector2(1000, 850) };
            _dummy.HitLanded += OnDummyHit;
            AddChild(_dummy);

            _services.HUD?.SetObjective("tutorial_step_attack", 0, RequiredBasicHits);
        }

        private void OnDummyHit(int damage) {
            if (_phase != TutorialPhase.Calibration || _calibrationStep != CalibrationStep.BasicHits) return;
            _basicHitsLanded++;
            _services.HUD?.SetObjective("tutorial_step_attack", _basicHitsLanded, RequiredBasicHits);
            if (_basicHitsLanded >= RequiredBasicHits) {
                _calibrationStep = CalibrationStep.UseSpecial;
                _services.HUD?.SetObjective("tutorial_step_special");
            }
        }

        private void OnCooldownStarted(CooldownPayload payload) {
            if (_phase != TutorialPhase.Calibration || _calibrationStep != CalibrationStep.UseSpecial) return;
            _calibrationStep = CalibrationStep.UseRewind;
            _services.HUD?.SetObjective("tutorial_step_rewind");
        }

        private bool _mobilityIntroPending;

        private void OnRewindTriggered(Vector2 _) {
            if (_phase != TutorialPhase.Calibration || _calibrationStep != CalibrationStep.UseRewind) return;
            _calibrationStep = CalibrationStep.Done;
            // The guided demonstration never spends the player's real pool.
            _services.RewindManager?.RefundRewind();
            // Wait for rewind playback to finish before pausing for dialogue.
            _mobilityIntroPending = true;
        }

        public override void _Process(double delta) {
            if (!_mobilityIntroPending) return;
            if (_services.RewindManager != null && _services.RewindManager.IsRewinding) return;
            _mobilityIntroPending = false;
            _services.Dialogue?.StartSequence("level_00.mobility_intro");
        }

        // === Part 3: mobility gates ===

        private void BeginMobilityGates() {
            _phase = TutorialPhase.Mobility;
            _services.HUD?.SetObjective("tutorial_step_double_jump");

            AddCheckpoint("l00_checkpoint_mobility", new Vector2(1500, 850));

            // Double-jump gate: a marker above a platform stack only reachable
            // with a full jump chain.
            BuildPlatform(1700, 720, 200);
            BuildPlatform(1900, 480, 180);
            _doubleJumpGate = BuildGateZone("DoubleJumpGate", new Vector2(1900, 400), new Vector2(160, 140),
                new Color(0.2f, 0.9f, 0.5f, 0.25f), "tutorial_gate_double_jump");

            // Dash gate: a wide marked corridor the player must cross while dashing.
            _dashGate = BuildGateZone("DashGate", new Vector2(2500, 830), new Vector2(220, 140),
                new Color(0.9f, 0.7f, 0.2f, 0.25f), "tutorial_gate_dash");

            // Roll gate: a marked strip the player must cross while rolling.
            _rollGate = BuildGateZone("RollGate", new Vector2(3100, 850), new Vector2(200, 110),
                new Color(0.4f, 0.6f, 1f, 0.25f), "tutorial_gate_roll");
        }

        private Area2D BuildGateZone(string name, Vector2 position, Vector2 size, Color color, string labelKey) {
            var zone = new Area2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = size };
            zone.AddChild(col);

            var visual = new ColorRect {
                Size = size,
                Position = -size / 2f,
                Color = color,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            zone.AddChild(visual);

            var label = new Label {
                Text = Tr(labelKey),
                Position = new Vector2(-size.X / 2f, -size.Y / 2f - 26),
                CustomMinimumSize = new Vector2(size.X, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(color.R, color.G, color.B, 0.95f));
            zone.AddChild(label);

            AddChild(zone);
            return zone;
        }

        public override void _PhysicsProcess(double delta) {
            if (_phase != TutorialPhase.Mobility || _player == null || !IsInstanceValid(_player)) return;

            if (!_doubleJumpGateCleared && ZoneContainsPlayer(_doubleJumpGate)) {
                _doubleJumpGateCleared = true;
                MarkGateCleared(_doubleJumpGate);
                _services.HUD?.SetObjective("tutorial_step_dash");
            }
            if (_doubleJumpGateCleared && !_dashGateCleared && ZoneContainsPlayer(_dashGate)
                && _player.CurrentState == CharacterState.Dashing) {
                _dashGateCleared = true;
                MarkGateCleared(_dashGate);
                _services.HUD?.SetObjective("tutorial_step_roll");
            }
            if (_dashGateCleared && !_rollGateCleared && ZoneContainsPlayer(_rollGate)
                && _player.CurrentState == CharacterState.Rolling) {
                _rollGateCleared = true;
                MarkGateCleared(_rollGate);
                BeginCombatTrial();
            }
        }

        private bool ZoneContainsPlayer(Area2D zone) {
            if (zone == null || !IsInstanceValid(zone)) return false;
            Godot.Collections.Array<Node2D> bodies = zone.GetOverlappingBodies();
            using var bodiesLifetime = bodies.AsDisposable();
            foreach (Node2D body in bodies) {
                if (body == _player) return true;
            }
            return false;
        }

        private static void MarkGateCleared(Area2D zone) {
            Godot.Collections.Array<Node> children = zone.GetChildren();
            using var childrenLifetime = children.AsDisposable();
            foreach (Node child in children) {
                if (child is ColorRect visual) visual.Color = new Color(0.2f, 0.9f, 0.4f, 0.45f);
            }
        }

        private void AddCheckpoint(string checkpointID, Vector2 position) {
            var trigger = new CheckpointTrigger {
                Name = checkpointID,
                CheckpointID = checkpointID,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = new Vector2(60, 200) };
            trigger.AddChild(col);

            var visual = new ColorRect {
                Size = new Vector2(8, 120),
                Position = new Vector2(-4, -120),
                Color = new Color(0f, 0.9f, 0.9f, 0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            trigger.AddChild(visual);
            AddChild(trigger);
        }

        // === Combat trial ===

        private void BeginCombatTrial() {
            _phase = TutorialPhase.CombatTrial;
            AddCheckpoint("l00_checkpoint_trial", new Vector2(3400, 850));
            SpawnWave1();
        }

        private void SpawnWave1() {
            _totalEnemies = StoryDifficultyTuning.ScaleEncounterCount(5, StoryDifficultyTuning.CurrentStoryDifficulty);
            _enemiesKilled = 0;
            UpdateWaveObjective("tutorial_objective_progress");

            Vector2[] spawns = {
                new(1200, 850), new(1800, 850), new(2400, 850), new(3000, 700), new(3600, 850)
            };
            for (int index = 0; index < _totalEnemies && index < spawns.Length; index++) {
                if (index == 2) {
                    EnemyFactory.SpawnHologramDrone(this, spawns[index],
                        waypointA: spawns[index] + new Vector2(-200, 0), waypointB: spawns[index] + new Vector2(200, 0));
                } else {
                    EnemyFactory.SpawnHologramDrone(this, spawns[index]);
                }
            }
            _totalEnemies = Mathf.Min(_totalEnemies, spawns.Length);
        }

        private void SpawnWave2() {
            _totalEnemies = StoryDifficultyTuning.ScaleEncounterCount(4, StoryDifficultyTuning.CurrentStoryDifficulty);
            _enemiesKilled = 0;

            Vector2[] spawns = { new(1500, 850), new(2000, 550), new(2800, 850), new(3200, 850) };
            for (int index = 0; index < _totalEnemies && index < spawns.Length; index++) {
                if (index == 3) {
                    EnemyFactory.SpawnChronoSlasher(this, spawns[index],
                        waypointA: spawns[index] + new Vector2(-200, 0), waypointB: spawns[index] + new Vector2(200, 0));
                } else {
                    EnemyFactory.SpawnHologramDrone(this, spawns[index]);
                }
            }
            _totalEnemies = Mathf.Min(_totalEnemies, spawns.Length);
            _waveTwoActive = true;
            UpdateWaveObjective("tutorial_objective_wave_two");
        }

        private bool _waveTwoActive;

        private void OnEnemyKilled(EnemyKilledPayload payload) {
            if (_phase != TutorialPhase.CombatTrial || _levelComplete) return;
            _enemiesKilled++;
            UpdateWaveObjective("tutorial_objective_progress");

            if (_enemiesKilled >= _totalEnemies) {
                if (!_waveTwoActive) SpawnWave2();
                else CompleteTutorial();
            }
        }

        private void UpdateWaveObjective(string key) {
            _services.HUD?.SetObjective(key, _enemiesKilled, _totalEnemies);
        }

        private void CompleteTutorial() {
            _levelComplete = true;
            _phase = TutorialPhase.Complete;

            if (StoryManager.Instance != null) StoryManager.Instance.TutorialComplete = true;
            // CompleteLevel raises OnLevelComplete, which advances the campaign
            // level and autosaves completion; do not also advance manually.
            _levelManager?.CompleteLevel();
            _services.HUD?.SetObjective("tutorial_objective_complete");
            _services.Dialogue?.StartSequence("level_00.complete");
        }

        private void ShowCompletionResults() {
            var results = LevelResultsPanel.CreateDefault();
            results.ReturnRequested += () => StoryManager.Instance?.ReturnToHub();
            AddChild(results);
            results.ShowResults("tutorial_level_title", StoryManager.Instance?.ChronalDustCollected ?? 0);
        }

        // === Level construction ===

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
            BuildPlatform(2300, 550, 250);
            BuildPlatform(2700, 680, 220);
            BuildPlatform(3500, 720, 250);
            BuildPlatform(4000, 550, 200);

            BuildWall(0, 0, 1080);
            BuildWall(4780, 0, 1080);

            BuildFracturePresentation();
            BuildDecoration();
        }

        /// <summary>Placeholder Chronal Rift visual for the part-one fracture presentation.</summary>
        private void BuildFracturePresentation() {
            var rift = new Node2D { Name = "ChronalFracture", Position = new Vector2(250, 700) };
            AddChild(rift);

            var core = new ColorRect {
                Size = new Vector2(50, 180),
                Position = new Vector2(-25, -90),
                Color = new Color(0.5f, 0.2f, 0.9f, 0.85f)
            };
            rift.AddChild(core);

            var glow = new ColorRect {
                Size = new Vector2(90, 220),
                Position = new Vector2(-45, -110),
                Color = new Color(0.4f, 0.1f, 0.8f, 0.25f)
            };
            rift.AddChild(glow);

            var tween = rift.CreateTween().SetLoops();
            tween.TweenProperty(core, "modulate:a", 0.45f, 0.8);
            tween.TweenProperty(core, "modulate:a", 1.0f, 0.8);

            var label = new Label {
                Text = Tr("tutorial_fracture_label"),
                Position = new Vector2(-90, -150),
                CustomMinimumSize = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.4f, 1f));
            rift.AddChild(label);
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
            title.Text = Tr("tutorial_environment_title");
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
    }
}
