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

        /// <summary>
        /// Authored <see cref="StageAudioSet"/> for this level (Package 8 B5).
        /// Derived from <see cref="LevelID"/> so a new level inherits its music with
        /// no extra authoring; override only to point somewhere non-standard.
        /// </summary>
        protected virtual string AudioSetPath => AudioSetPaths.ForStoryLevel(LevelID);

        /// <summary>Objective posted as soon as the HUD exists. Empty means "post nothing".</summary>
        protected virtual string InitialObjectiveKey => "";

        /// <summary>Objective posted when the boss encounter reveals.</summary>
        protected virtual string BossObjectiveKey => "";

        /// <summary>Objective posted once the boss is down.</summary>
        protected virtual string CompletionObjectiveKey => "";

        /// <summary>Delay between the boss dying and the exit dialogue starting.</summary>
        protected virtual float ExitDialogueDelaySeconds => 1.5f;

        /// <summary>
        /// Authored dialogue beats that run BETWEEN the boss dying and the ordinary
        /// exit beat, in order. Empty (the default) hands defeat straight to
        /// <see cref="StartExitSequence"/>, which is what levels 2-7 and 9-12 do.
        ///
        /// A level that owes an extra scene here (Level 8's Cleopatra
        /// <c>level_08.postboss</c>) lists it instead of overriding
        /// <see cref="OnBossDefeated"/>: overriding the defeat handler wholesale
        /// loses the base's bookkeeping, so <see cref="IsBossDefeated"/> would never
        /// become true and the level would have to track its own flag.
        ///
        /// Each beat waits <see cref="ExitDialogueDelaySeconds"/> first, and a beat
        /// whose sequence will not start is skipped rather than stranding the player
        /// in a finished arena with no results overlay.
        /// </summary>
        protected virtual IReadOnlyList<string> PostBossDialogueIDs => NoPostBossBeats;

        private static readonly string[] NoPostBossBeats = Array.Empty<string>();

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

        /// <summary>Scene music and environment cues (Package 8 B5).</summary>
        public StoryAudioDirector Audio => Services?.Audio;

        /// <summary>
        /// Dust the wallet actually received while this level was live (the results
        /// total). Tallied from <c>OnChronalDustCollected</c> — the same event
        /// <see cref="StoryManager"/> banks — so this number can never disagree with
        /// what the player was paid (audit H-1/M-1).
        /// </summary>
        public int DustEarnedThisLevel { get; private set; }

        /// <summary>Boss-defeat portion of <see cref="DustEarnedThisLevel"/> (results itemization).</summary>
        public int BossDustEarned { get; private set; }

        /// <summary>Extractor-destruction portion of <see cref="DustEarnedThisLevel"/>.</summary>
        public int ExtractorDustEarned { get; private set; }

        /// <summary>
        /// Mob-kill remainder of the total: every wallet award that was not a boss
        /// or extractor payout — pickup collections from kills, plus rare chest
        /// finds. Derived rather than counted so the three lines always sum to the
        /// exact wallet total even if an award lands out of order.
        /// </summary>
        public int MobDustEarned => Mathf.Max(0, DustEarnedThisLevel - BossDustEarned - ExtractorDustEarned);

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
            ApplyResumedAttemptState();
            AttachStoryServices();
            BindEvents();
            ApplyResumeCameraBounds();
            OnLevelReady();
            // Deferred through a Callable rather than MethodName: the base is
            // abstract and its subclasses may be constructed in code, so the
            // dispatch must not depend on a bound script method name.
            if (!ResumedMidLevel) Callable.From(StartEntranceDialogue).CallDeferred();
        }

        public override void _ExitTree() {
            UnbindEvents();
            ReleasePooledContent();
        }

        /// <summary>
        /// Hands every pooled object this level owns back to <see cref="PoolManager"/>.
        /// Pooled enemies, their projectiles, and loot are parented under the level, so
        /// unloading the scene would destroy them while the pool still holds them in its
        /// active list - the next level's spawns then inherit freed references and every
        /// pool sweep throws. Scoped by ancestry rather than by the "Enemies" group so a
        /// level tearing down can never reclaim another live scene's objects (only one
        /// level is live at a time today, but the hub and the test fixtures are not).
        /// </summary>
        private void ReleasePooledContent() => PoolManager.Instance?.ReleaseActiveUnder(this);

        private void CreateLevelManager() {
            Levels = new LevelManager {
                Name = "LevelManager",
                LevelID = LevelID,
                LevelDisplayName = LevelTitleKey
            };
            AddChild(Levels);
        }

        private void AttachStoryServices() {
            Services = StorySceneBootstrapper.Attach(this, DialogueSetPath, audioSetPath: AudioSetPath);
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
            EventBus.Instance.OnChronalDustCollected += OnDustAwarded;
            EventBus.Instance.OnDustAwardCollected += OnDustAwardAttributed;
            EventBus.Instance.OnDialogueComplete += HandleDialogueComplete;
            EventBus.Instance.OnRewindTriggered += OnRewindLanded;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnChronalDustCollected -= OnDustAwarded;
            EventBus.Instance.OnDustAwardCollected -= OnDustAwardAttributed;
            EventBus.Instance.OnDialogueComplete -= HandleDialogueComplete;
            EventBus.Instance.OnRewindTriggered -= OnRewindLanded;
        }

        /// <summary>
        /// A 15 s rewind routinely lands in an earlier room, whose forward-only
        /// transition trigger will not re-fire; without this the camera stays
        /// confined to the room the player died in and the landing is
        /// off-screen. Mirrors <see cref="ApplyResumeCameraBounds"/>.
        /// </summary>
        private void OnRewindLanded(Vector2 landingPosition) {
            if (Camera == null) return;
            RoomTransitionTrigger room = FindRoomTriggerContaining(landingPosition.X);
            if (room != null) Camera.SetBounds(room.CameraBounds);
        }

        private void StartEntranceDialogue() {
            // Callable.From(...) wraps a bare managed delegate, so - unlike
            // CallDeferred(MethodName.X) - the engine does NOT drop the queued call
            // when the target node is freed. A level torn down in the same frame it
            // readied (fast scene change, a test fixture) would otherwise run this
            // against a disposed node and its disposed DialogueManager.
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            if (!string.IsNullOrWhiteSpace(EntranceDialogueID)) {
                Services?.Dialogue?.StartSequence(EntranceDialogueID);
            }
        }

        private void HandleDialogueComplete(string dialogueID) {
            if (dialogueID == ExitDialogueID) {
                ShowCompletionResults();
                return;
            }
            // Read before the hook runs: a subclass is free to change what the
            // chain looks like from inside OnDialogueSequenceComplete.
            bool finishedPostBossBeat = IsActivePostBossBeat(dialogueID);
            OnDialogueSequenceComplete(dialogueID);
            if (finishedPostBossBeat) AdvancePostBossChain();
        }

        /// <summary>
        /// Wallet-receipt tally (audit H-1). The physical <see cref="ChronalDustPickup"/>
        /// is the single awarding path for kill dust — <see cref="StoryDropSystem"/>
        /// spawns it and collection raises the award — so the level controller must
        /// never re-raise a kill's drop; it only listens to what the wallet was paid.
        /// Boss and extractor awards flow through the same event (raised once by
        /// <see cref="BossEncounterController"/> / <see cref="ChronalExtractor"/>),
        /// so the total needs no per-source special cases.
        /// </summary>
        private void OnDustAwarded(int amount) => DustEarnedThisLevel += Mathf.Max(0, amount);

        /// <summary>
        /// V7.3 Single Icon Rule: boss/extractor awards are physical pickups,
        /// so attribution now lands at COLLECTION time via this payload event —
        /// the same moment the wallet event above banks the amount. The two can
        /// therefore never disagree, and the double-pay invariant (audit H-1)
        /// holds by construction: one wallet raise, one attribution, both at
        /// the collection site.
        /// </summary>
        private void OnDustAwardAttributed(DustAwardCollectedPayload payload) {
            switch (payload.Source) {
                case DustAwardSource.Boss: AttributeBossDust(payload.Amount); break;
                case DustAwardSource.Extractor: AttributeExtractorDust(payload.Amount); break;
            }
        }

        /// <summary>
        /// Attributes an already-awarded amount to the boss line of the results
        /// itemization. Attribution only — the wallet award entered
        /// <see cref="DustEarnedThisLevel"/> through the shared
        /// <c>OnChronalDustCollected</c> subscription; adding it to the total here
        /// as well would recreate the H-1 double-pay on the results screen. Kept
        /// protected for callers outside the pickup path (Level 13's Mirror
        /// Paradox pays wallet-direct and attributes explicitly).
        /// </summary>
        protected void AttributeBossDust(int amount) => BossDustEarned += Mathf.Max(0, amount);

        /// <summary>Extractor sibling of <see cref="AttributeBossDust"/>.</summary>
        protected void AttributeExtractorDust(int amount) => ExtractorDustEarned += Mathf.Max(0, amount);

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
        /// Confines the camera to the authored room the run actually starts in.
        ///
        /// <see cref="RoomTransitionTrigger"/> only fires on a crossing, so a run
        /// resumed at checkpoint 1 or 2 stands *behind* every trigger it already
        /// passed and would keep whole-level limits for the rest of the level. Runs
        /// automatically between <see cref="BindEvents"/> and <see cref="OnLevelReady"/>.
        ///
        /// Camera bounds only, deliberately: calling
        /// <see cref="RoomTransitionTrigger.ActivateRoom"/> here would re-run the
        /// room's <c>onEntered</c> handler and replay its waves, floods, and hazard
        /// state on top of the checkpoint restore. A level that also needs the room's
        /// encounter root enabled on resume calls
        /// <see cref="FindRoomTriggerContaining"/> itself.
        ///
        /// Overlapping room bounds resolve to the last authored match.
        /// </summary>
        protected virtual void ApplyResumeCameraBounds() {
            if (!ResumedMidLevel || Camera == null || Player == null) return;
            float x = Player.Position.X;
            foreach (RoomTransitionTrigger trigger in _roomTriggers) {
                if (!IsInstanceValid(trigger)) continue;
                if (ContainsX(trigger.CameraBounds, x)) Camera.SetBounds(trigger.CameraBounds);
            }
        }

        /// <summary>First authored room trigger whose camera bounds span <paramref name="positionX"/>.</summary>
        protected RoomTransitionTrigger FindRoomTriggerContaining(float positionX) {
            foreach (RoomTransitionTrigger trigger in _roomTriggers) {
                if (IsInstanceValid(trigger) && ContainsX(trigger.CameraBounds, positionX)) return trigger;
            }
            return null;
        }

        private static bool ContainsX(Rect2 bounds, float x) => x >= bounds.Position.X && x <= bounds.End.X;

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

        /// <summary>
        /// V7.3 mid-level resume: rebuilds the attempt's world-state on top of
        /// the freshly constructed scene — extractors already destroyed this
        /// attempt come back broken (no dust, no Integrity restore, no
        /// re-registration) and secrets already found stay found (no double
        /// count). StoryManager's registries were restored from the save by
        /// <see cref="StoryManager.BeginLevelRun"/> before the scene loaded.
        /// </summary>
        protected virtual void ApplyResumedAttemptState() {
            StoryManager story = StoryManager.Instance;
            if (!ResumedMidLevel || story == null) return;
            foreach (ChronalExtractor extractor in _extractors) {
                if (IsInstanceValid(extractor) && story.IsExtractorDestroyed(extractor.ObjectID)) {
                    extractor.RestoreDestroyedState();
                }
            }
            Godot.Collections.Array<Node> caches = GetTree().GetNodesInGroup(SecretCache.Group);
            using var cachesLifetime = caches.AsDisposable();
            foreach (Node node in caches) {
                if (node is SecretCache cache && story.IsSecretFound(cache.SecretID)) {
                    cache.MarkAlreadyFound();
                }
            }
        }

        // === HUD helpers ===

        protected void SetLevelTitle(string translationKey) => HUD?.SetLevelTitle(translationKey);

        protected void SetObjective(string translationKey, params object[] arguments) {
            if (string.IsNullOrWhiteSpace(translationKey)) return;
            HUD?.SetObjective(translationKey, arguments);
        }

        /// <summary>
        /// Starts an authored sequence, reporting whether it actually began. Virtual
        /// so a level (or a test) can substitute the dialogue source without
        /// reimplementing the beat chains that call it.
        /// </summary>
        protected virtual bool StartDialogue(string dialogueID) =>
            !string.IsNullOrWhiteSpace(dialogueID) &&
            Services?.Dialogue?.StartSequence(dialogueID) == true;

        // === Geometry builders (graybox; Package 2 TileMap conversion is separate) ===

        /// <summary>
        /// Optional per-level surface textures (Package 10). Null keeps the flat
        /// ColorRect graybox visuals; see <see cref="LevelSurfaceTextures"/>.
        /// </summary>
        protected virtual LevelSurfaceTextures SurfaceTextures => null;

        /// <summary>
        /// The "Visual" child every surface builder attaches: a nine-patch when the
        /// level supplies a texture for that surface and the caller did not force an
        /// explicit fill colour, the original ColorRect otherwise.
        /// </summary>
        private static Control BuildSurfaceVisual(LevelSurfaceTextures.Entry entry, bool hasExplicitFill,
            Vector2 size, Vector2 position, Color fallback) {
            if (entry?.Texture == null || hasExplicitFill) {
                return new ColorRect {
                    Name = "Visual",
                    Size = size,
                    Position = position,
                    Color = fallback,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
            }
            return new NinePatchRect {
                Name = "Visual",
                Size = size,
                Position = position,
                Texture = entry.Texture,
                PatchMarginLeft = entry.MarginLeft,
                PatchMarginRight = entry.MarginRight,
                PatchMarginTop = entry.MarginTop,
                PatchMarginBottom = entry.MarginBottom,
                AxisStretchHorizontal = entry.TileHorizontal
                    ? NinePatchRect.AxisStretchMode.Tile : NinePatchRect.AxisStretchMode.Stretch,
                AxisStretchVertical = entry.TileVertical
                    ? NinePatchRect.AxisStretchMode.Tile : NinePatchRect.AxisStretchMode.Stretch,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
        }

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

            Control floorVisual = BuildSurfaceVisual(SurfaceTextures?.Floor, fill != null,
                new Vector2(width, thickness), new Vector2(-width / 2f, 0), fill ?? FloorColor);
            floor.AddChild(floorVisual);
            if (floorVisual is ColorRect) {
                // The textured surface carries its own lit top edge.
                floor.AddChild(new ColorRect {
                    Name = "TopLine",
                    Size = new Vector2(width, 3),
                    Position = new Vector2(-width / 2f, 0),
                    Color = FloorEdgeColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }

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
            platform.AddChild(BuildSurfaceVisual(SurfaceTextures?.Platform, fill != null,
                new Vector2(width, thickness), new Vector2(-width / 2f, -thickness / 2f), fill ?? PlatformColor));

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
            platform.AddChild(BuildSurfaceVisual(SurfaceTextures?.OneWay, fill != null,
                new Vector2(width, thickness), new Vector2(-width / 2f, -thickness / 2f), fill ?? PlatformColor));

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
            wall.AddChild(BuildSurfaceVisual(SurfaceTextures?.Wall, fill != null,
                new Vector2(thickness, height), Vector2.Zero, fill ?? WallColor));

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
                if (body is not PlayerController player) return;
                // Hazard damage can cascade into a lethal-hit rewind or a
                // guard break; mark the physics callback so those systems
                // defer the engine-blocked writes.
                using var scope = PhysicsCallbackGuard.Enter();
                ApplySpikeDamage(player, damage);
            };

            AddChild(hazard);
            return hazard;
        }

        /// <summary>
        /// V7.3: spike hazards route through the environmental-damage
        /// chokepoint like every other environmental source (Defy flag
        /// consumption, Rally echo, victim meter — never raw ApplyDamage).
        /// Static and internal so the pin test can exercise the exact path
        /// the BodyEntered lambda takes.
        /// </summary>
        internal static int ApplySpikeDamage(PlayerController player, int damage) =>
            player?.ApplyEnvironmentalDamage(damage) ?? 0;

        protected CheckpointTrigger BuildCheckpoint(float x, float y, string id) {
            var checkpoint = new CheckpointTrigger {
                Name = $"Checkpoint_{id}",
                CheckpointID = id,
                Position = new Vector2(x, y),
                RespawnOffset = new Vector2(0, -50),
                // V7.3 strike-to-activate: only the entry checkpoint
                // ("{levelID}_checkpoint_0") self-activates — the player just
                // arrived through it (design exception); the mid and pre-boss
                // fractures must be struck.
                SelfActivating = id != null && id.EndsWith("_checkpoint_0", StringComparison.Ordinal),
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
                if (body is not PlayerController) return;
                // Wave callbacks spawn collision bodies, which the engine
                // forbids during the in/out flush this signal runs in; fire
                // the callback right after the flush.
                Callable.From(() => onEntered?.Invoke()).CallDeferred();
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
            // Itemization (audit M-1, reworked by the V7.3 Single Icon Rule):
            // destruction spawns a physical pickup; the wallet payment AND the
            // extractor-line attribution both land at collection through
            // OnDustAwardCollected — nothing to wire per instance here.
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
            var data = AuthoredResources.Load<BossData>(bossResourcePath);
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
            // Package 8 B5: the climax layer is wired to the encounter directly rather
            // than to OnBossDefeated, which subclasses are free to override without
            // calling base — the music must not depend on that.
            encounter.BossRevealed += () => Audio?.SetBossEngaged(true);
            encounter.BossDefeated += _ => Audio?.SetBossEngaged(false);
            AddChild(encounter);
            _bossEncounters.Add(encounter);
            if (Services?.HUD != null) encounter.HUD = Services.HUD;
            return encounter;
        }

        private bool _bossIntroShown;
        private bool _bossDefeated;
        private int _postBossBeatIndex = -1;

        public bool IsBossDefeated => _bossDefeated;

        /// <summary>
        /// Post-boss beat currently on screen, or "" when the chain has not started
        /// or has already handed off to the exit sequence.
        /// </summary>
        public string ActivePostBossDialogueID {
            get {
                IReadOnlyList<string> beats = PostBossDialogueIDs ?? NoPostBossBeats;
                return _postBossBeatIndex >= 0 && _postBossBeatIndex < beats.Count
                    ? beats[_postBossBeatIndex]
                    : "";
            }
        }

        protected virtual void OnBossRevealed(BossEncounterController encounter) {
            if (_bossIntroShown) return;
            _bossIntroShown = true;
            SetObjective(BossObjectiveKey);
            StartDialogue(BossIntroDialogueID);
        }

        /// <summary>
        /// Boss-defeat bookkeeping. Prefer <see cref="PostBossDialogueIDs"/> or
        /// <see cref="StartPostBossSequence"/> over overriding this: everything a
        /// level normally wants to change lives after the flag, the objective, and
        /// the dust tally, and an override that skips them silently disables
        /// <see cref="IsBossDefeated"/>.
        /// </summary>
        protected virtual void OnBossDefeated(BossEncounterController encounter, BossDefeatedPayload payload) {
            if (_bossDefeated) return;
            _bossDefeated = true;
            SetObjective(CompletionObjectiveKey);
            // V7.3 Single Icon Rule: the encounter spawned a physical pickup;
            // the wallet payment and the boss-line attribution both land at
            // collection (OnDustAwardCollected) — attributing here as well
            // would label dust the wallet has not been paid.
            StartPostBossSequence();
        }

        /// <summary>
        /// Everything that happens once the boss is down and the base has settled
        /// its bookkeeping: play every <see cref="PostBossDialogueIDs"/> beat in
        /// order, then <see cref="StartExitSequence"/>.
        ///
        /// Override to replace the whole tail — Level 15 swaps the exit/results
        /// chain for the campaign completion chain — while keeping
        /// <see cref="IsBossDefeated"/>, the completion objective, and the dust
        /// tally intact.
        /// </summary>
        protected virtual void StartPostBossSequence() => AdvancePostBossChain();

        /// <summary>
        /// Plays the next unplayed <see cref="PostBossDialogueIDs"/> beat after the
        /// standard beat delay, or starts the exit sequence when the chain is spent.
        /// Called again by the base each time one of those beats completes.
        /// </summary>
        protected void AdvancePostBossChain() {
            if ((PostBossDialogueIDs ?? NoPostBossBeats).Count <= _postBossBeatIndex + 1) {
                _postBossBeatIndex = int.MaxValue;
                StartExitSequence();
                return;
            }
            RunAfterBeatDelay(PlayNextPostBossBeat);
        }

        private void PlayNextPostBossBeat() {
            IReadOnlyList<string> beats = PostBossDialogueIDs ?? NoPostBossBeats;
            while (++_postBossBeatIndex < beats.Count) {
                if (StartDialogue(beats[_postBossBeatIndex])) return;
            }
            // Nothing left, or nothing would start: never leave the player in a
            // finished arena with no results overlay.
            _postBossBeatIndex = int.MaxValue;
            StartExitSequence();
        }

        private bool IsActivePostBossBeat(string dialogueID) {
            IReadOnlyList<string> beats = PostBossDialogueIDs ?? NoPostBossBeats;
            return _postBossBeatIndex >= 0 && _postBossBeatIndex < beats.Count &&
                   beats[_postBossBeatIndex] == dialogueID;
        }

        /// <summary>Delayed hand-off from the boss beat to the exit dialogue.</summary>
        protected virtual void StartExitSequence() =>
            RunAfterBeatDelay(() => {
                if (!StartDialogue(ExitDialogueID)) ShowCompletionResults();
            });

        /// <summary>
        /// Runs <paramref name="step"/> after <see cref="ExitDialogueDelaySeconds"/>,
        /// or immediately when there is no tree to time against. The timer callback
        /// re-checks the level: a scene torn down mid-beat (fast scene change, a test
        /// fixture) must not resume the chain against a freed node.
        /// </summary>
        private void RunAfterBeatDelay(Action step) {
            float delay = Mathf.Max(0f, ExitDialogueDelaySeconds);
            if (delay <= 0f || GetTree() == null) {
                step();
                return;
            }
            SceneTreeTimer timer = GetTree().CreateTimer(delay);
            timer.Timeout += () => {
                if (!IsInstanceValid(this) || !IsInsideTree()) return;
                step();
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
            // Package 8 B5: settle back onto the ambient bed under the results overlay.
            Audio?.ReleaseToAmbient();
            return PresentCompletion();
        }

        /// <summary>
        /// What the player sees once the level is over and <c>OnLevelComplete</c>
        /// has been raised. Default: the shared results overlay whose Return button
        /// goes back to the Time-Ship hub.
        ///
        /// Level 15 overrides this to run <c>CampaignCompletionSequence</c>
        /// (credits -> campaign completion -> main menu) instead of returning to the
        /// hub, and returns null. The one-shot <see cref="LevelComplete"/> guard and
        /// the level advance stay with the base either way.
        /// </summary>
        protected virtual LevelResultsPanel PresentCompletion() {
            var results = LevelResultsPanel.CreateDefault();
            results.ReturnRequested += () => StoryManager.Instance?.ReturnToHub();
            AddChild(results);
            results.ShowResults(LevelTitleKey, MobDustEarned, ExtractorDustEarned, BossDustEarned);
            return results;
        }
    }
}
