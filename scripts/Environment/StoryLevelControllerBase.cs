using Godot;
using System;
using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Shared Story level scaffolding for campaign levels 2-15 (Package 5 A1).
    /// Extracted from the proven <see cref="Level01Controller"/> flow: graybox
    /// geometry builders, the "LevelManager" child, player spawn with a
    /// <see cref="StoryCameraConfiner"/>, room transitions, checkpoint resume,
    /// dust tally, boss wiring, extractor placement, and the completion chain.
    ///
    /// Florence (level 1) and the Tutorial (level 0) deliberately stay on their
    /// own controllers: this base is for new levels only.
    ///
    /// Lifecycle a subclass sees, in order:
    /// <list type="number">
    /// <item>the "LevelManager" child is created and configured</item>
    /// <item><see cref="BuildLevel"/> - geometry, checkpoints, rooms, boss anchors</item>
    /// <item>the player spawns, checkpoint resume runs (may call
    ///       <see cref="MarkWavesClearedThrough"/>), the camera is created and
    ///       linked into every room trigger</item>
    /// <item><see cref="SpawnInitialEnemies"/></item>
    /// <item>shared Story services attach, title/objective post, boss HUD wires up</item>
    /// <item><see cref="OnLevelReady"/></item>
    /// <item>the entrance dialogue fires unless this is a mid-level resume</item>
    /// </list>
    /// </summary>
    public abstract partial class StoryLevelControllerBase : Node2D {

        // === Subclass contract ===

        /// <summary>Manifest content id, e.g. "level_02_orleans". Also the LevelManager LevelID.</summary>
        public abstract string LevelID { get; }

        /// <summary>Campaign slot this scene occupies; drives the checkpoint-resume scene-path guard.</summary>
        public abstract CampaignLevel Level { get; }

        /// <summary>Translation key for the HUD level title, e.g. "orleans_level_title".</summary>
        public abstract string LevelTitleKey { get; }

        /// <summary>Dialogue set resource, e.g. "res://resources/Dialogue/level_02_dialogue.tres".</summary>
        public abstract string DialogueSetPath { get; }

        /// <summary>Where the player stands with no saved checkpoint.</summary>
        public abstract Vector2 PlayerSpawnPosition { get; }

        /// <summary>Whole-level camera limits; individual rooms narrow this through room triggers.</summary>
        public abstract Rect2 LevelBounds { get; }

        /// <summary>
        /// Dialogue sequence prefix. Defaults to the "level_NN" slice of
        /// <see cref="LevelID"/>, matching the plan's `level_NN.entrance` convention.
        /// </summary>
        public virtual string DialoguePrefix {
            get {
                string id = LevelID ?? "";
                string[] parts = id.Split('_');
                return parts.Length >= 2 ? $"{parts[0]}_{parts[1]}" : id;
            }
        }

        public virtual string EntranceDialogueID => $"{DialoguePrefix}.entrance";
        public virtual string BossIntroDialogueID => $"{DialoguePrefix}.boss_intro";
        public virtual string ExitDialogueID => $"{DialoguePrefix}.exit";

        /// <summary>Objective posted as soon as the HUD exists. Empty means "post nothing".</summary>
        protected virtual string InitialObjectiveKey => "";

        /// <summary>Objective posted when the boss encounter reveals.</summary>
        protected virtual string BossObjectiveKey => "";

        /// <summary>Objective posted once the boss is down.</summary>
        protected virtual string CompletionObjectiveKey => "";

        /// <summary>Delay between the boss dying and the exit dialogue starting.</summary>
        protected virtual float ExitDialogueDelaySeconds => 1.5f;

        /// <summary>Build graybox geometry, checkpoints, room triggers, puzzles, and boss anchors.</summary>
        protected abstract void BuildLevel();

        /// <summary>Enemies present the moment the level opens (later waves use wave triggers).</summary>
        protected virtual void SpawnInitialEnemies() { }

        /// <summary>
        /// Resume hook. Called once during spawn when the active save restores a
        /// checkpoint in this level; mark every wave ahead of that checkpoint as
        /// already fought so a resumed run does not replay cleared encounters.
        /// </summary>
        protected virtual void MarkWavesClearedThrough(string checkpointID) { }

        /// <summary>Runs after services attach and before the entrance dialogue.</summary>
        protected virtual void OnLevelReady() { }

        /// <summary>Any completed dialogue that is not the exit sequence.</summary>
        protected virtual void OnDialogueSequenceComplete(string dialogueID) { }

        // === Runtime state available to subclasses ===

        public PlayerController Player { get; private set; }
        public LevelManager Levels { get; private set; }
        public StorySceneServices Services { get; private set; }
        public StoryHUD HUD => Services?.HUD;
        public StoryCameraConfiner Camera { get; private set; }

        /// <summary>Dust tallied for the results overlay (the EventBus award is separate).</summary>
        public int DustEarnedThisLevel { get; private set; }

        /// <summary>True when the run resumed at a saved checkpoint rather than the entrance.</summary>
        public bool ResumedMidLevel { get; private set; }

        /// <summary>Checkpoint the resume restored, or "" for a fresh entry.</summary>
        public string ResumedCheckpointID { get; private set; } = "";

        public bool LevelComplete { get; private set; }

        private readonly List<RoomTransitionTrigger> _roomTriggers = new();
        private readonly List<BossEncounterController> _bossEncounters = new();
        private readonly List<ChronalExtractor> _extractors = new();

        public IReadOnlyList<RoomTransitionTrigger> RoomTriggers => _roomTriggers;
        public IReadOnlyList<BossEncounterController> BossEncounters => _bossEncounters;
        public IReadOnlyList<ChronalExtractor> Extractors => _extractors;

        private bool _eventsBound;

        // === Default graybox palette (subclasses override per era) ===

        protected virtual Color FloorColor => new(0.22f, 0.22f, 0.26f);
        protected virtual Color FloorEdgeColor => new(0.42f, 0.42f, 0.5f);
        protected virtual Color PlatformColor => new(0.3f, 0.3f, 0.36f);
        protected virtual Color WallColor => new(0.16f, 0.16f, 0.2f);
        protected virtual Color HazardColor => new(0.6f, 0.2f, 0.1f);

        protected const float DefaultFloorThickness = 40f;
        protected const float DefaultPlatformThickness = 16f;
        protected const float DefaultWallThickness = 20f;

        // === Lifecycle ===

        public override void _Ready() {
            CreateLevelManager();
            BuildLevel();
            SpawnPlayer();
            SpawnInitialEnemies();
            AttachStoryServices();
            BindEvents();
            OnLevelReady();
            // Deferred through a Callable rather than MethodName: the base is
            // abstract and its subclasses may be constructed in code, so the
            // dispatch must not depend on a bound script method name.
            if (!ResumedMidLevel) Callable.From(StartEntranceDialogue).CallDeferred();
        }

        public override void _ExitTree() => UnbindEvents();

        private void CreateLevelManager() {
            Levels = new LevelManager {
                Name = "LevelManager",
                LevelID = LevelID,
                LevelDisplayName = LevelTitleKey
            };
            AddChild(Levels);
        }

        private void AttachStoryServices() {
            Services = StorySceneBootstrapper.Attach(this, DialogueSetPath);
            Services.HUD?.SetLevelTitle(LevelTitleKey);
            if (!string.IsNullOrWhiteSpace(InitialObjectiveKey)) {
                Services.HUD?.SetObjective(InitialObjectiveKey);
            }
            foreach (BossEncounterController encounter in _bossEncounters) {
                if (IsInstanceValid(encounter)) encounter.HUD = Services.HUD;
            }
        }

        private void BindEvents() {
            if (_eventsBound || EventBus.Instance == null) return;
            _eventsBound = true;
            EventBus.Instance.OnEnemyKilled += OnEnemyKilled;
            EventBus.Instance.OnDialogueComplete += HandleDialogueComplete;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnEnemyKilled -= OnEnemyKilled;
            EventBus.Instance.OnDialogueComplete -= HandleDialogueComplete;
        }

        private void StartEntranceDialogue() {
            if (!string.IsNullOrWhiteSpace(EntranceDialogueID)) {
                Services?.Dialogue?.StartSequence(EntranceDialogueID);
            }
        }

        private void HandleDialogueComplete(string dialogueID) {
            if (dialogueID == ExitDialogueID) {
                ShowCompletionResults();
                return;
            }
            OnDialogueSequenceComplete(dialogueID);
        }

        /// <summary>Level dust tally. The award itself is re-raised for StoryManager.</summary>
        private void OnEnemyKilled(EnemyKilledPayload payload) {
            DustEarnedThisLevel += payload.ChronalDustDrop;
            EventBus.Instance?.RaiseChronalDustCollected(payload.ChronalDustDrop);
        }

        /// <summary>Adds to the results tally without re-raising a dust award.</summary>
        protected void TallyDust(int amount) => DustEarnedThisLevel += Mathf.Max(0, amount);

        // === Player, camera, and checkpoint resume ===

        private void SpawnPlayer() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            Player = CharacterFactory.CreateCharacter(characterID, 0);
            Player.Name = "Player";
            Player.Position = PlayerSpawnPosition;
            AddChild(Player);
            RestoreSavedCheckpoint();

            Camera = new StoryCameraConfiner {
                Name = "PlayerCamera",
                Enabled = true,
                ActiveBounds = LevelBounds,
                PositionSmoothingEnabled = true,
                PositionSmoothingSpeed = 5.0f
            };
            Player.AddChild(Camera);
            LinkRoomCameras();
        }

        /// <summary>
        /// Room triggers are built before the player exists, so their camera path
        /// is resolved here once the confiner is in the tree.
        /// </summary>
        private void LinkRoomCameras() {
            if (Camera == null) return;
            foreach (RoomTransitionTrigger trigger in _roomTriggers) {
                if (!IsInstanceValid(trigger)) continue;
                if (trigger.CameraPath == null || trigger.CameraPath.IsEmpty) {
                    trigger.CameraPath = trigger.GetPathTo(Camera);
                }
            }
        }

        /// <summary>
        /// Generic Florence-pattern resume: only restores when the active save is
        /// parked on THIS level's scene path, then hands the checkpoint id to
        /// <see cref="MarkWavesClearedThrough"/> so cleared waves stay cleared.
        /// </summary>
        protected void RestoreSavedCheckpoint() {
            if (Player == null || SaveManager.Instance == null || GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null) return;
            if (save.CurrentLevelID != StoryManager.GetLevelScenePath(Level)) return;

            Vector2 position = Player.Position;
            if (Levels != null && Levels.TryGetCheckpointPosition(save.LastCheckpointID, out Vector2 checkpoint)) {
                position = checkpoint;
                Levels.SetCheckpointPosition(save.LastCheckpointID, checkpoint);
                ResumedMidLevel = true;
                ResumedCheckpointID = save.LastCheckpointID;
                MarkWavesClearedThrough(save.LastCheckpointID);
            }
            Player.RestoreStoryCheckpoint(position, save.CurrentHP, save.CurrentUltimateMeter);
        }

        // === HUD helpers ===

        protected void SetLevelTitle(string translationKey) => HUD?.SetLevelTitle(translationKey);

        protected void SetObjective(string translationKey, params object[] arguments) {
            if (string.IsNullOrWhiteSpace(translationKey)) return;
            HUD?.SetObjective(translationKey, arguments);
        }

        protected bool StartDialogue(string dialogueID) =>
            !string.IsNullOrWhiteSpace(dialogueID) &&
            Services?.Dialogue?.StartSequence(dialogueID) == true;

        // === Geometry builders (graybox; Package 2 TileMap conversion is separate) ===

        protected ColorRect BuildRoomBackground(string name, float x, float width, float height, Color color) {
            var background = new ColorRect {
                Name = name,
                Size = new Vector2(width, height),
                Position = new Vector2(x, 0),
                Color = color,
                ZIndex = -10,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(background);
            return background;
        }

        protected StaticBody2D BuildFloor(float x, float y, float width, Color? fill = null, float thickness = DefaultFloorThickness) {
            var floor = new StaticBody2D {
                Name = $"Floor_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x + width / 2f, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            var collision = new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) },
                Position = new Vector2(0, thickness / 2f)
            };
            floor.AddChild(collision);

            floor.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, thickness),
                Position = new Vector2(-width / 2f, 0),
                Color = fill ?? FloorColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            floor.AddChild(new ColorRect {
                Name = "TopLine",
                Size = new Vector2(width, 3),
                Position = new Vector2(-width / 2f, 0),
                Color = FloorEdgeColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            AddChild(floor);
            return floor;
        }

        protected StaticBody2D BuildPlatform(float x, float y, float width, Color? fill = null, float thickness = DefaultPlatformThickness) {
            var platform = new StaticBody2D {
                Name = $"Platform_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            platform.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) }
            });
            platform.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, thickness),
                Position = new Vector2(-width / 2f, -thickness / 2f),
                Color = fill ?? PlatformColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            AddChild(platform);
            return platform;
        }

        /// <summary>
        /// Drop-through platform on the OneWayPlatform layer. Matches the
        /// OneWayPlatformTemplate contract (group + one-way collision margin).
        /// </summary>
        protected OneWayPlatform BuildOneWayPlatform(float x, float y, float width, Color? fill = null, float thickness = 20f) {
            var platform = new OneWayPlatform {
                Name = $"OneWay_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.OneWayPlatform,
                CollisionMask = 0
            };

            platform.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) },
                OneWayCollision = true,
                OneWayCollisionMargin = 12f
            });
            platform.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, thickness),
                Position = new Vector2(-width / 2f, -thickness / 2f),
                Color = fill ?? PlatformColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            AddChild(platform);
            return platform;
        }

        protected StaticBody2D BuildWall(float x, float y, float height, Color? fill = null, float thickness = DefaultWallThickness) {
            var wall = new StaticBody2D {
                Name = $"Wall_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            wall.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(thickness, height) },
                Position = new Vector2(thickness / 2f, height / 2f)
            });
            wall.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(thickness, height),
                Color = fill ?? WallColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            AddChild(wall);
            return wall;
        }

        protected Area2D BuildHazardSpikes(float x, float y, float width, int damage = 15, Color? fill = null, float thickness = 10f) {
            var hazard = new Area2D {
                Name = $"Spikes_{Mathf.RoundToInt(x)}_{Mathf.RoundToInt(y)}",
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };

            hazard.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(width, thickness) }
            });
            hazard.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, thickness),
                Position = new Vector2(-width / 2f, -thickness / 2f),
                Color = fill ?? HazardColor,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            hazard.BodyEntered += body => {
                if (body is PlayerController player) player.ApplyDamage(damage);
            };

            AddChild(hazard);
            return hazard;
        }

        protected CheckpointTrigger BuildCheckpoint(float x, float y, string id) {
            var checkpoint = new CheckpointTrigger {
                Name = $"Checkpoint_{id}",
                CheckpointID = id,
                Position = new Vector2(x, y),
                RespawnOffset = new Vector2(0, -50),
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            Levels?.RegisterCheckpoint(id, checkpoint.Position + checkpoint.RespawnOffset);

            checkpoint.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(60, 120) },
                Position = new Vector2(0, -60)
            });
            checkpoint.AddChild(new ColorRect {
                Name = "CheckpointVisual",
                Size = new Vector2(20, 80),
                Position = new Vector2(-10, -80),
                Color = new Color(0.0f, 0.7f, 0.9f, 0.6f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            var label = new Label {
                Name = "CheckpointLabel",
                Text = Tr("checkpoint"),
                Position = new Vector2(-40, -100),
                CustomMinimumSize = new Vector2(80, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            checkpoint.AddChild(label);

            AddChild(checkpoint);
            return checkpoint;
        }

        /// <summary>Proximity trigger that runs <paramref name="onEntered"/> the first time the player crosses it.</summary>
        protected Area2D BuildWaveTrigger(string name, Vector2 position, Action onEntered, Vector2? size = null) {
            var trigger = new Area2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            trigger.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = size ?? new Vector2(60, 400) }
            });
            trigger.BodyEntered += body => {
                if (body is PlayerController) onEntered?.Invoke();
            };
            AddChild(trigger);
            return trigger;
        }

        /// <summary>
        /// Room boundary: retargets the camera confiner, activates the room's
        /// encounter root, and raises the shared room-transition event.
        /// </summary>
        protected RoomTransitionTrigger BuildRoomTransition(
            string roomID,
            Vector2 position,
            Rect2 cameraBounds,
            Vector2? triggerSize = null,
            Action<string> onEntered = null,
            Node encounterRoot = null) {
            var trigger = new RoomTransitionTrigger {
                Name = $"RoomTransition_{roomID}",
                RoomID = roomID,
                Position = position,
                CameraBounds = cameraBounds,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            trigger.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = triggerSize ?? new Vector2(80, 900) }
            });
            AddChild(trigger);
            if (encounterRoot != null) trigger.EncounterRootPath = trigger.GetPathTo(encounterRoot);
            if (onEntered != null) trigger.RoomEntered += id => onEntered(id);
            _roomTriggers.Add(trigger);
            LinkRoomCameras();
            return trigger;
        }

        /// <summary>
        /// Solid placeholder door standing ON <paramref name="basePosition"/> (its
        /// floor contact point) and extending upward. Call <see cref="OpenDoor"/>
        /// when the gate that seals it clears.
        /// </summary>
        protected StaticBody2D BuildDoor(string name, Vector2 basePosition, string lockedLabelKey = "level_door_locked",
            Vector2? size = null, Color? fill = null) {
            Vector2 doorSize = size ?? new Vector2(40, 400);
            var door = new StaticBody2D {
                Name = name,
                Position = basePosition,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };

            door.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = doorSize },
                Position = new Vector2(0, -doorSize.Y / 2f)
            });
            door.AddChild(new ColorRect {
                Name = "Visual",
                Size = doorSize,
                Position = new Vector2(-doorSize.X / 2f, -doorSize.Y),
                Color = fill ?? new Color(0.45f, 0.35f, 0.2f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            var lockLabel = new Label {
                Name = "LockedLabel",
                Text = Tr(lockedLabelKey),
                Position = new Vector2(-90, -doorSize.Y - 40),
                CustomMinimumSize = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            lockLabel.AddThemeFontSizeOverride("font_size", 11);
            lockLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.2f));
            door.AddChild(lockLabel);

            AddChild(door);
            return door;
        }

        /// <summary>Removes a door built by <see cref="BuildDoor"/>; safe to call twice.</summary>
        protected static bool OpenDoor(ref StaticBody2D door) {
            if (door == null || !IsInstanceValid(door)) {
                door = null;
                return false;
            }
            door.QueueFree();
            door = null;
            return true;
        }

        protected Label BuildRoomDecoration(float offsetX, string roomNameKey, Color color) {
            var label = new Label {
                Name = $"RoomLabel_{roomNameKey}",
                Text = Tr(roomNameKey),
                Position = new Vector2(offsetX + 50, 30),
                CustomMinimumSize = new Vector2(300, 25),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeColorOverride("font_color", color);
            AddChild(label);
            return label;
        }

        // === Chronal Extractors ===

        public const string ExtractorTemplatePath = "res://scenes/templates/ChronalExtractorTemplate.tscn";

        /// <summary>
        /// Places one authored Chronal Extractor. Dust value stays resource-owned
        /// (docs/DUST_ECONOMY.md: 15 each) - never override it from level code.
        /// </summary>
        protected ChronalExtractor BuildExtractor(string extractorID, Vector2 position) {
            var packed = ResourceLoader.Load<PackedScene>(ExtractorTemplatePath);
            if (packed?.Instantiate() is not ChronalExtractor extractor) {
                GD.PushWarning($"Chronal Extractor template could not be instantiated for '{extractorID}'.");
                return null;
            }
            extractor.Name = $"Extractor_{extractorID}";
            extractor.ObjectID = extractorID;
            extractor.Position = position;
            AddChild(extractor);
            _extractors.Add(extractor);
            return extractor;
        }

        /// <summary>Places the level's extractors from an authored position table.</summary>
        protected void BuildExtractors(params (string ID, Vector2 Position)[] placements) {
            if (placements == null) return;
            foreach ((string id, Vector2 position) in placements) BuildExtractor(id, position);
        }

        // === Boss encounter ===

        /// <summary>
        /// Wires the reusable <see cref="BossEncounterController"/> into the level's
        /// dialogue and objective flow: reveal posts the boss objective and starts
        /// the intro sequence; defeat posts the completion objective, tallies the
        /// authored dust, and (after a beat) starts the exit sequence.
        /// </summary>
        protected BossEncounterController BuildBossEncounter(
            string bossResourcePath,
            Vector2 position,
            string encounterName = null,
            float revealDistance = 800f,
            Vector2 spawnOffset = default) {
            var data = GD.Load<BossData>(bossResourcePath);
            if (data == null) {
                GD.PushError($"Boss resource missing for level '{LevelID}': {bossResourcePath}");
                return null;
            }

            var encounter = new BossEncounterController {
                Name = encounterName ?? $"{data.BossID}_encounter",
                Position = position,
                Data = data,
                SpawnOffset = spawnOffset,
                RevealDistance = revealDistance
            };
            encounter.BossRevealed += () => OnBossRevealed(encounter);
            encounter.BossDefeated += payload => OnBossDefeated(encounter, payload);
            AddChild(encounter);
            _bossEncounters.Add(encounter);
            if (Services?.HUD != null) encounter.HUD = Services.HUD;
            return encounter;
        }

        private bool _bossIntroShown;
        private bool _bossDefeated;

        public bool IsBossDefeated => _bossDefeated;

        protected virtual void OnBossRevealed(BossEncounterController encounter) {
            if (_bossIntroShown) return;
            _bossIntroShown = true;
            SetObjective(BossObjectiveKey);
            StartDialogue(BossIntroDialogueID);
        }

        protected virtual void OnBossDefeated(BossEncounterController encounter, BossDefeatedPayload payload) {
            if (_bossDefeated) return;
            _bossDefeated = true;
            SetObjective(CompletionObjectiveKey);
            // The encounter controller already raised the dust award; only tally.
            TallyDust(payload.ChronalDustDrop);
            StartExitSequence();
        }

        /// <summary>Delayed hand-off from the boss beat to the exit dialogue.</summary>
        protected void StartExitSequence() {
            float delay = Mathf.Max(0f, ExitDialogueDelaySeconds);
            if (delay <= 0f || GetTree() == null) {
                if (!StartDialogue(ExitDialogueID)) ShowCompletionResults();
                return;
            }
            SceneTreeTimer timer = GetTree().CreateTimer(delay);
            timer.Timeout += () => {
                if (!StartDialogue(ExitDialogueID)) ShowCompletionResults();
            };
        }

        // === Completion ===

        /// <summary>
        /// Ends the level: raises OnLevelComplete (advance + autosave) and shows the
        /// results overlay whose Return button goes back to the Time-Ship hub.
        /// </summary>
        public LevelResultsPanel ShowCompletionResults() {
            if (LevelComplete) return null;
            LevelComplete = true;
            Levels?.CompleteLevel();

            var results = LevelResultsPanel.CreateDefault();
            results.ReturnRequested += () => StoryManager.Instance?.ReturnToHub();
            AddChild(results);
            results.ShowResults(LevelTitleKey, DustEarnedThisLevel);
            return results;
        }
    }
}
