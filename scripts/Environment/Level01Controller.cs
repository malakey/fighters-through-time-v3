using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Level 1 - Florence, 1503 (Steampunk Renaissance). Four-room progression:
    /// entry streets, the print-shop gear puzzle that unlocks the workshop door,
    /// the workshop gauntlet, and the Borgia Inquisitor boss arena.
    ///
    /// <para><b>Encounter locks</b> (gameplay update 2026-10-12, GU-L0; Florence
    /// predates <see cref="StoryLevelControllerBase"/>, so it wires standalone
    /// <see cref="EncounterLock"/> nodes): the entry room is two locks — the
    /// streets (720–2330) and the piazza (2350–4430), its manifest row split in
    /// two — then the print shop (5800–8630, which also needs the gear puzzle;
    /// its exit barrier fills the workshop door under its column) and the
    /// workshop gauntlet (8900–10460). Room 1 is pre-placed, rooms 2 and 3 spawn
    /// as the hero walks in (as their wave triggers did). The entry anchor moved
    /// out of the fighting to 300, the Middle anchor past the workshop door (8840),
    /// the Restoration Font past the gauntlet (10530) and the third Extractor west
    /// of it (10150). A west wall now closes x 0.</para>
    /// </summary>
    public partial class Level01Controller : Node2D {
        private PlayerController _player;
        private BossEncounterController _bossEncounter;
        private LevelManager _levelManager;
        private StorySceneServices _services;
        private StoryCameraConfiner _camera;

        private PuzzleManager _gearPuzzle;
        private RotatingGear _gearA;
        private RotatingGear _gearB;
        private StaticBody2D _workshopDoor;

        private bool _bossIntroShown;
        private bool _bossDefeated;
        private bool _levelComplete;
        // Wallet-receipt tally (audit H-1): every OnChronalDustCollected award
        // raised while Florence is live, so the results total always equals what
        // the wallet was actually paid. _bossDustEarned labels the boss's share
        // for the itemized overlay; Florence authors no extractors (the open
        // docs/DUST_ECONOMY.md §4 gap), so its extractor line is zero.
        // Package 11 A10 (F05): the per-category labels now ride the attribution
        // payload at COLLECTION, exactly as StoryLevelControllerBase does, and
        // Florence's three authored Extractors (A3) finally have a line.
        private int _dustEarnedThisLevel;
        private int _bossDustEarned;
        private int _extractorDustEarned;
        private int _optionalDustEarned;

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

            // Package 12 W8 (N01): a pre-seal load builds the Inquisitor defeated.
            _restoringAwaitingSeal = SealReadiness.IsPreSealLoad(_levelManager.LevelID, CampaignLevel.Florence);
            if (_restoringAwaitingSeal) {
                _bossDefeated = true;
                _bossIntroShown = true;
            }
            BuildLevel();
            SpawnPlayer();
            // GU-L0: the locks arm once the resume point is known — a resume past
            // a lock builds it Cleared (no member ever spawned), the rest arm with
            // their exits shut and room 1's members standing dormant.
            ArmEncounterLocks();

            _services = StorySceneBootstrapper.Attach(
                this, "res://resources/Dialogue/level_01_dialogue.tres",
                audioSetPath: AudioSetPaths.ForStoryLevel("level_01_florence"));
            _services.HUD?.SetLevelTitle("florence_level_title");
            _services.HUD?.SetObjective("florence_objective_reach_boss");
            if (_bossEncounter != null) {
                _bossEncounter.HUD = _services.HUD;
                // Package 8 B5: Florence predates StoryLevelControllerBase, so its
                // climax wiring is done here rather than inherited.
                _bossEncounter.BossRevealed += () => _services?.Audio?.SetBossEngaged(true);
                _bossEncounter.BossDefeated += _ => _services?.Audio?.SetBossEngaged(false);
            }

            if (EventBus.Instance != null) {
                EventBus.Instance.OnChronalDustCollected += OnDustAwarded;
                EventBus.Instance.OnDustAwardCollected += OnDustAwardAttributed;
                EventBus.Instance.OnDialogueComplete += OnDialogueComplete;
            }

            bool resumedMidLevel = _levelManager.LastCheckpointID.Length > 0;
            if (!resumedMidLevel) CallDeferred(MethodName.StartEntranceDialogue);

            if (_restoringAwaitingSeal) {
                _restoringAwaitingSeal = false;
                RestoredBossPickup = SealReadiness.RestorePendingBossPickup(BossSpawnPosition, this);
                EnterAwaitingSeal();
            }
        }

        // === Package 12 W8 region: the N01 sealing anchor (GAP-05) ============
        //
        // Florence predates StoryLevelControllerBase, so it wires the same seal
        // stage by hand through the shared SealReadiness rules: the Inquisitor's
        // defeat commits AwaitingSeal (no completion, deposit or unlock), the
        // anchor arms beside the boss pickup, a single Interact commits the
        // completion transaction, and only then does level_01.exit (the
        // restoration vignette) play before the results.

        public const string SealingAnchorID = "level_01.sealing_anchor";
        private const float BossSpawnX = Room4OffsetX + 700f;

        /// <summary>P5: the permanent column over the workshop door. Test seam.</summary>
        public StaticBody2D WorkshopDoorColumn { get; private set; }

        /// <summary>The workshop door: 40 px wide, standing from y 500 to the floor at 900.</summary>
        private const float WorkshopDoorWidth = 40f;
        private const float WorkshopDoorTopY = 500f;

        /// <summary>
        /// P1 (2026-10-04): the boss room's last checkpoint, which stands in the
        /// antechamber between the doorway and the arena so it can be struck
        /// before the reveal. Test seam.
        /// </summary>
        public const string BossRoomCheckpointID = "florence_checkpoint_2";
        private const float BossRoomCheckpointX = Room4OffsetX + 100f;

        /// <summary>
        /// P1 (2026-10-04): the arena the Borgia boss is revealed by and leashed
        /// to — from the Phase 2 west barricade's inner face (the closed arena's
        /// own west edge) to the east wall's inner face. It starts east of the
        /// boss room's checkpoint (<see cref="BossRoomCheckpointID"/>, 80 px west
        /// of the edge), so the hero can strike that checkpoint from the
        /// antechamber before stepping in; the reveal used to fire the moment the
        /// hero cleared the doorway, with the last checkpoint still unstruck.
        /// </summary>
        public static readonly Rect2 BossArenaBounds = new(
            ArenaShrinkLeftWallX + BarricadeThickness, 0f,
            Room4OffsetX + 940f - (ArenaShrinkLeftWallX + BarricadeThickness), LevelHeight);

        /// <summary>
        /// P1 (2026-10-04): bottom of the arena's west wall. Below it (to the floor
        /// at 900) is the ground-level doorway into the arena, 200 px tall.
        /// </summary>
        public const float ArenaDoorwayTopY = 700f;
        private const float BossSpawnY = 850f;
        private static readonly Vector2 BossSpawnPosition = new(BossSpawnX, BossSpawnY);

        private bool _restoringAwaitingSeal;

        /// <summary>The level's sealing anchor. Test seam.</summary>
        public TemporalCoreAnchor SealingAnchor { get; private set; }

        /// <summary>True once the seal has been accepted. Test seam.</summary>
        public bool SealAccepted { get; private set; }

        /// <summary>The boss pickup a pre-seal load respawned, or null. Test seam.</summary>
        public ChronalDustPickup RestoredBossPickup { get; private set; }

        private void BuildSealingAnchor() {
            SealingAnchor = TemporalCoreAnchor.CreateSealingAnchor(
                SealingAnchorID, BossSpawnPosition + new Vector2(StoryLevelControllerBase.DerivedSealingAnchorOffsetX, 0f));
            SealingAnchor.CoreInserted += _ => AcceptSeal();
            AddChild(SealingAnchor);
        }

        private void EnterAwaitingSeal() {
            if (SealAccepted || _levelComplete || SealingAnchor == null || !IsInstanceValid(SealingAnchor)) return;
            SealingAnchor.Arm();
            _services?.HUD?.SetObjective(StoryLevelControllerBase.SealObjectiveKey);
        }

        /// <summary>The accepted seal: commit, then the vignette, then the results.</summary>
        public bool AcceptSeal() {
            if (SealAccepted || _levelComplete || !_bossDefeated) return false;
            SealAccepted = true;
            foreach (ChronalExtractor extractor in _extractors) {
                if (IsInstanceValid(extractor)) extractor.ShutDownBySeal();
            }
            CommitLevelCompletion();
            EnvironmentNotice.Post(StoryLevelControllerBase.SealedNoticeKey, SealingAnchor);
            if (_services?.Dialogue?.StartSequence("level_01.exit") != true) ShowCompletionResults();
            return true;
        }

        private void CommitLevelCompletion() {
            if (_levelComplete) return;
            _levelComplete = true;
            SealReadiness.ClearOnCompletion();
            // Raises OnLevelComplete: advances the campaign and autosaves completion.
            _levelManager?.CompleteLevel();
        }

        // === end Package 12 W8 region ==========================================

        public override void _ExitTree() {
            foreach (EncounterLock encounterLock in _encounterLocks) {
                if (encounterLock != null && IsInstanceValid(encounterLock)) encounterLock.StateChanged -= OnEncounterLockStateChanged;
            }
            if (EventBus.Instance != null) {
                EventBus.Instance.OnChronalDustCollected -= OnDustAwarded;
                EventBus.Instance.OnDustAwardCollected -= OnDustAwardAttributed;
                EventBus.Instance.OnDialogueComplete -= OnDialogueComplete;
                if (_recoveryBound) EventBus.Instance.OnRecoveryLanded -= OnRecoveryLanded;
            }
            _recoveryBound = false;
            // Pooled wave enemies are parented here; hand them back or the pool keeps
            // freed references once this scene unloads.
            PoolManager.Instance?.ReleaseActiveUnder(this);
        }

        private void StartEntranceDialogue() {
            _services?.Dialogue?.StartSequence("level_01.entrance");
        }

        private void OnDialogueComplete(string dialogueID) {
            // Package 13 W4: level_01.exit@leonardo completes under its variant ID.
            if (FTT.UI.DialogueSequenceData.BaseIDOf(dialogueID) == "level_01.exit") ShowCompletionResults();
        }

        // === Level construction ===

        private void BuildLevel() {
            BuildRoom1();
            BuildRoom2();
            BuildRoom3();
            BuildRoom4BossArena();
            BuildExtractors();
            BuildRestorationFont();
            BuildEncounterLocks();
        }

        // === D13: the Restoration Font (2026-10-11) ===

        /// <summary>
        /// D13 (PROVISIONAL-FONT-PLACEMENT): Florence's Restoration Font ID. Florence
        /// does not extend <see cref="StoryLevelControllerBase"/>, so it places its
        /// own font, exactly as it places its own Extractors.
        /// </summary>
        public const string RestorationFontID = "level_01.restoration_font";

        /// <summary>
        /// D13: on the workshop floor (top 900), past the Middle anchor
        /// (<c>florence_checkpoint_1</c>). GU-L0 (2026-10-13): it stood at 9720,
        /// inside the workshop gauntlet (now an encounter lock), so it moved past
        /// the gauntlet's exit to the room's east end (10530): the last heal
        /// before the boss room, 40 px clear of the lock's area, 130 px short of
        /// the boss-room checkpoint and 210 px short of the arena; the third
        /// Extractor moved west (10150) to keep its 320 px. The antechamber past
        /// the doorway is still too short. No hazard reaches the channel circle
        /// (the steam geyser is at 9860–9980; the first placement, x 9900, stood
        /// inside it).
        /// </summary>
        public static readonly Vector2 RestorationFontPosition = new(Room3OffsetX + 1890f, 900f);

        /// <summary>The font Florence placed. Test seam.</summary>
        public RestorationFont PlacedRestorationFont { get; private set; }

        private void BuildRestorationFont() {
            PlacedRestorationFont = RestorationFont.Create(RestorationFontID, RestorationFontPosition);
            // Completion re-keys the use registry to the next level, so the font
            // refuses once the level is complete.
            PlacedRestorationFont.RefusesWhile = () => _levelComplete;
            AddChild(PlacedRestorationFont);
        }

        /// <summary>
        /// Package 11 A3: Florence's three budgeted Chronal Extractors — the
        /// one standing content gap docs/DUST_ECONOMY.md left open, because
        /// Package 5 deliberately did not retrofit the pre-existing Tutorial
        /// and Florence controllers.
        ///
        /// Level 1 is <b>untimed</b> (it does not extend
        /// <see cref="StoryLevelControllerBase"/>, so no Integrity clock arms
        /// and no tier bonus applies). These carry presentation and dust only.
        /// The per-machine dust allocation — Level 1's whole 10 optional dust
        /// goes to its Extractors — is <b>A10's</b>; A3 authors the placements
        /// and the destroyed-registry wiring so a mid-level resume rebuilds
        /// them broken.
        ///
        /// One per traversable room, off the critical path: the entry hall's
        /// far end, the print shop past the platform climb, and the workshop
        /// behind the spike run. The boss arena gets none.
        /// </summary>
        private void BuildExtractors() {
            BuildExtractor("florence_extractor_0", new Vector2(4700f, 860f));
            BuildExtractor("florence_extractor_1", new Vector2(Room2OffsetX + 2600f, 860f));
            // GU-L0: 10340 -> 10150, so the Restoration Font past the gauntlet
            // (10530) keeps its 320 px; still behind the spike runs and the geyser.
            BuildExtractor("florence_extractor_2", new Vector2(Room3OffsetX + 1510f, 860f));
        }

        /// <summary>
        /// Places one Extractor from the shared template. Mirrors
        /// <c>StoryLevelControllerBase.BuildExtractor</c>, which Florence
        /// cannot inherit — the dust value stays resource-owned and is never
        /// overridden from level code.
        /// </summary>
        private ChronalExtractor BuildExtractor(string extractorID, Vector2 position) {
            var packed = ResourceLoader.Load<PackedScene>(
                StoryLevelControllerBase.ExtractorTemplatePath);
            if (packed?.Instantiate() is not ChronalExtractor extractor) {
                GD.PushWarning($"Chronal Extractor template could not be instantiated for '{extractorID}'.");
                return null;
            }
            extractor.Name = $"Extractor_{extractorID}";
            extractor.ObjectID = extractorID;
            extractor.Position = position;
            AddChild(extractor);
            _extractors.Add(extractor);
            if (StoryManager.Instance?.IsExtractorDestroyed(extractorID) == true) {
                extractor.RestoreDestroyedState();
            }
            return extractor;
        }

        private readonly System.Collections.Generic.List<ChronalExtractor> _extractors = new();

        /// <summary>Florence's authored Extractors. Test seam.</summary>
        public System.Collections.Generic.IReadOnlyList<ChronalExtractor> Extractors => _extractors;

        private void BuildRoom1() {
            var bg1 = new ColorRect();
            bg1.Name = "BG_Room1";
            bg1.Size = new Vector2(5760, LevelHeight);
            bg1.Color = new Color(0.12f, 0.08f, 0.05f, 0.35f);
            AddChild(bg1);

            BuildFloor(0, 900, 5760);
            // GU-L0: the missing west wall — the floor's west end used to be open
            // to a fall off the level.
            BuildWall(-20, 0, LevelHeight);
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

            // GU-L0: the entry anchor stood at 2800, in the middle of room 1's
            // fighting; it now stands at the entrance, before the streets lock.
            BuildCheckpoint(EntryCheckpointX, 850, "florence_checkpoint_0");

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

            BuildRoomDecoration(offsetX, "florence_room_print_shop", new Color(0.6f, 0.4f, 0.2f));
            // GU-L0: the Middle anchor (it stood at 7160, in the print shop's
            // fight) is built with room 3, past the workshop door; the room's wave
            // is the print-shop lock's (it spawns as the hero walks in, where the
            // wave trigger at 5880 used to fire).

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
            _gearPuzzle.PuzzleCompleted += _ => OnGearPuzzleSolved();

            _gearA = BuildGear("GearA", new Vector2(offsetX + 2000, 820), initialQuarterTurns: 1,
                conditionID: "gear_a_aligned");
            _gearB = BuildGear("GearB", new Vector2(offsetX + 2350, 820), initialQuarterTurns: 2,
                conditionID: "gear_b_aligned");
            _gearA.LinkedGearPaths.Add(_gearA.GetPathTo(_gearB));

            // Deferred: PuzzleManager may already hold saved completion.
            CallDeferred(MethodName.ApplySavedPuzzleState);
        }

        private void ApplySavedPuzzleState() {
            if (_gearPuzzle != null && _gearPuzzle.IsCompleted) OpenWorkshopDoor();
        }

        /// <summary>
        /// FIX-OBJECTIVE (2026-10-13): the solve is one-way, like the door it
        /// frees. From the solve on, the puzzle and both gears keep their state
        /// through every death rewind (whose snapshot predates the solve: the
        /// Middle anchor stands past the door), so a rewind never spins the gears
        /// back or un-solves what the print-shop lock has latched — the burned
        /// scaffolding's precedent. Also runs for a completion the save restores.
        /// </summary>
        private void OnGearPuzzleSolved() {
            _gearPuzzle.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            if (_gearA != null && IsInstanceValid(_gearA)) _gearA.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            if (_gearB != null && IsInstanceValid(_gearB)) _gearB.RewindPolicy = StoryRewindPolicy.PreserveCurrentState;
            OpenWorkshopDoor();
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
            // GU-L0: the door is open, but the print shop's barrier holds until
            // its guards are down as well.
            RefreshObjective();
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
            // GU-L0: 8840–9040 -> 8910–9040 (centre 8975, 130 wide), so the
            // workshop lock's entry barrier (8880–8900) stands clear of it.
            BuildPlatform(offsetX + 335, 700, 130);
            BuildPlatform(offsetX + 700, 550, 250);
            BuildPlatform(offsetX + 1100, 650, 200);
            BuildPlatform(offsetX + 1500, 500, 220);

            BuildHazardSpikes(offsetX + 400, 890, 100);
            BuildHazardSpikes(offsetX + 900, 890, 80);

            BuildRoomDecoration(offsetX, "florence_room_workshop", new Color(0.5f, 0.3f, 0.15f));

            _workshopDoor = BuildDoor("WorkshopDoor", new Vector2(offsetX, 0));
            // P5 (2026-10-04): the door spans y 500..900 and nothing stood above
            // it, so a double jump off the print shop's last platform (top 542)
            // cleared it with the gear puzzle unsolved. A permanent column seals
            // the sky above the door; the puzzle still opens the door itself. The
            // column is centred on the door and wider than it (StoryGateRules), so
            // the door's top is never a ledge.
            float columnWidth = StoryGateRules.ColumnWidth(WorkshopDoorWidth);
            WorkshopDoorColumn = BuildWall(offsetX - columnWidth / 2f, StoryGateRules.ColumnTopY,
                WorkshopDoorTopY - StoryGateRules.ColumnTopY, columnWidth);
            WorkshopDoorColumn.Name = "WorkshopDoorColumn";

            // GU-L0: the Middle anchor, past the workshop door and out of
            // basic-swing reach from inside the print shop's lock; the room's wave
            // is the workshop lock's (it spawns as the hero walks in, where the
            // wave trigger at 8790 used to fire).
            BuildCheckpoint(MiddleCheckpointX, 850, "florence_checkpoint_1");
        }

        /// <summary>Solid placeholder door; removed when the gear puzzle completes.</summary>
        private StaticBody2D BuildDoor(string name, Vector2 position) {
            var door = new StaticBody2D {
                Name = name,
                Position = position,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            // P5: enumerable by the gate sweep, like every base-class door.
            door.AddToGroup(StoryGateRules.DoorGroup);

            float doorHeight = 900f - WorkshopDoorTopY;
            var col = new CollisionShape2D();
            col.Shape = new RectangleShape2D { Size = new Vector2(WorkshopDoorWidth, doorHeight) };
            col.Position = new Vector2(0, WorkshopDoorTopY + doorHeight / 2f);
            door.AddChild(col);

            var visual = new ColorRect {
                Size = new Vector2(WorkshopDoorWidth, doorHeight),
                Position = new Vector2(-WorkshopDoorWidth / 2f, WorkshopDoorTopY),
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
            // M18: the two wooden scaffolding platforms burn away in Phase 2.
            _scaffolding.Add(BuildScaffolding("florence_scaffold_west", offsetX + 300, 650, 200));
            _scaffolding.Add(BuildScaffolding("florence_scaffold_east", offsetX + 660, 650, 200));

            // P1 (2026-10-04): the west wall used to run the full height (y 0..1080)
            // over a floor at 900, sealing the arena: no hero could enter it (Level
            // 1 has no movement ability yet), and the old radius reveal only ever
            // started a fight through the wall. The arena reveal needs the hero
            // inside, so the wall now stops at ArenaDoorwayTopY, leaving a ground-
            // level doorway; Phase 2's full-height burning barricade still closes it.
            BuildWall(offsetX, 0, ArenaDoorwayTopY);
            BuildWall(offsetX + 940, 0, LevelHeight);

            BuildCheckpoint(BossRoomCheckpointX, 850, BossRoomCheckpointID);
            BuildRoomDecoration(offsetX, "florence_room_boss", new Color(0.9f, 0.2f, 0.2f));

            SpawnBoss(BossSpawnX, BossSpawnY);
            BuildSealingAnchor();
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

        private StaticBody2D BuildWall(float x, float y, float height, float thickness = 20f) {
            var wall = new StaticBody2D();
            wall.CollisionLayer = CollisionLayers.Environment;
            wall.CollisionMask = 0;
            wall.Name = $"Wall_{x}";
            wall.Position = new Vector2(x, y);

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(thickness, height);
            col.Shape = shape;
            col.Position = new Vector2(thickness / 2f, height / 2f);
            wall.AddChild(col);

            var visual = new ColorRect();
            visual.Size = new Vector2(thickness, height);
            visual.Color = new Color(0.2f, 0.15f, 0.08f);
            wall.AddChild(visual);

            AddChild(wall);
            return wall;
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
                    // Hazard damage can cascade into a lethal-hit rewind; mark
                    // the physics callback so those systems defer the
                    // engine-blocked writes.
                    using var scope = PhysicsCallbackGuard.Enter();
                    // V7.3: environmental chokepoint — Defy/echo/meter. CM-BLOCK
                    // (7.4): blockable, facing the strip's centre.
                    pc.ApplyEnvironmentalHit(15, hazard.GlobalPosition, blockable: true);
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
            // V7.3 strike-to-activate, uniform across the hand-built levels:
            // only the entry checkpoint self-activates.
            checkpoint.SelfActivating = id != null && id.EndsWith("_checkpoint_0", System.StringComparison.Ordinal);
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

            // F8 (2026-10-04): the shared Story follow rig (StoryCameraRules) over
            // the same whole-level limits.
            var camera = new StoryCameraConfiner {
                Name = "PlayerCamera",
                Enabled = true,
                ActiveBounds = new Rect2(0, 0, LevelWidth, LevelHeight)
            };
            _player.AddChild(camera);
            camera.EnableFollow(_player);
            _camera = camera;
        }

        private void RestoreSavedCheckpoint() {
            if (SaveManager.Instance == null || GameManager.Instance == null) return;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return;
            StorySaveData save = SaveManager.Instance.SaveSlots[slot];
            if (save == null || save.CurrentLevelID != StoryManager.GetLevelScenePath(CampaignLevel.Florence)) return;
            Vector2 position = _player.Position;
            bool resumed = false;
            if (_levelManager.TryGetCheckpointPosition(save.LastCheckpointID, out Vector2 checkpoint)) {
                resumed = true;
                position = checkpoint;
                _levelManager.SetCheckpointPosition(save.LastCheckpointID, checkpoint);
                // GU-L0: the locks this anchor's baseline lists are built Cleared
                // (ArmEncounterLocks); every other lock re-arms in full.
                ResumedCheckpointID = save.LastCheckpointID;
            }
            // Package 12 W2 (GAP-01): the shared F10 restore — actual block,
            // cooldown and delay resources back on the rebuilt hero, and the
            // uncredited Rally meter settled exactly once.
            float settledMeter = StoryLevelControllerBase.RestoreDurableResources(_player, save, resumed);
            _player.ClearAllStatusEffects();
            _player.RestoreStoryCheckpoint(position, save.CurrentHP, settledMeter);
        }

        // === Encounters: the GU-L0 encounter locks (2026-10-13) ===============
        //
        // Florence predates StoryLevelControllerBase, so its locks are standalone
        // EncounterLock nodes wired by hand (docs/handoffs/GU_E.md, "A standalone
        // level"): built in BuildLevel, the resume baseline applied once the hero
        // stands at its checkpoint, then armed. The baselines are registered with
        // StoryManager, so the checkpoint guard holds every fracture past a lock
        // until that lock is cleared and the F10 record carries the lock IDs.
        // Authored counts are the Normal-difficulty truth; each lock wave takes
        // the per-row ScaleEncounterCount prefix the reward manifest compiles.

        public const string LevelID = "level_01_florence";

        /// <summary>The F10 encounter IDs of the four locks (they start with the level ID).</summary>
        public const string LockStreets = LevelID + "_lock_streets";
        public const string LockPiazza = LevelID + "_lock_piazza";
        public const string LockPrintShop = LevelID + "_lock_print_shop";
        public const string LockWorkshop = LevelID + "_lock_workshop";

        public const string EntryCheckpointID = "florence_checkpoint_0";
        public const string MiddleCheckpointID = "florence_checkpoint_1";
        public const string GearPuzzleID = "florence_gear_puzzle";

        /// <summary>
        /// The manifest rows (level_01_rewards.tres). GU-L0 split the entry room's
        /// <c>level_01.room1@5</c> into the streets' three and the piazza's two, so
        /// each lock is one row: the same five enemies, the same Easy three (two
        /// slashers and a guard) and the same Hard five.
        /// </summary>
        public const string Room1aRowID = "level_01.room1a";
        public const string Room1bRowID = "level_01.room1b";
        public const string Room2RowID = "level_01.room2";
        public const string Room3RowID = "level_01.room3";

        /// <summary>The entry anchor, at the entrance (it stood at 2800, mid-fight).</summary>
        public const float EntryCheckpointX = 300f;

        /// <summary>
        /// The Middle anchor, past the workshop door (it stood at 7160, inside the
        /// print shop). Its strike surface (8810–8870) lies past the roster's
        /// longest basic swing from a hero pressed against the print shop's
        /// closed exit barrier (pivot 8610) or the door (pivot 8600).
        /// </summary>
        public const float MiddleCheckpointX = Room3OffsetX + 200f;

        /// <summary>
        /// The streets: from past the first spike strip (525–675) to the gap before
        /// the 2500 platform. Every lock edge sits in a gap between platforms, so
        /// no barrier cuts one.
        /// </summary>
        public const float StreetsLockMinX = 720f;
        public const float StreetsLockMaxX = 2330f;
        /// <summary>The piazza shares the streets' exit slot (2330–2350) and runs to the gap past the 4300 platform.</summary>
        public const float PiazzaLockMinX = StreetsLockMaxX + 20f;
        public const float PiazzaLockMaxX = 4430f;
        public const float PrintShopLockMinX = 5800f;
        /// <summary>The print shop runs to the workshop door, whose barrier fills the doorway.</summary>
        public const float PrintShopLockMaxX = Room3OffsetX - 10f;
        public const float WorkshopLockMinX = Room3OffsetX + 260f;
        /// <summary>
        /// The workshop's east edge: its exit barrier (10460–10480) leaves the
        /// boss-room checkpoint's surface (10630) out of swing reach (pivot 10440)
        /// and the font's footprint (10500–10560) outside the area.
        /// </summary>
        public const float WorkshopLockMaxX = Room3OffsetX + 1820f;

        /// <summary>
        /// Room 1, the streets: the two slashers and the first guard (room1a).
        /// GU-L0: the first slasher moved 800 -> 1000 (patrol 800–1200), off the
        /// lock's entry, and the guard's patrol stops at the lock's exit (2300).
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room1aSpawns = {
            ("chrono_slasher", new Vector2(1000, 850), new Vector2(800, 850), new Vector2(1200, 850)),
            ("chrono_slasher", new Vector2(1800, 850), new Vector2(1600, 850), new Vector2(2000, 850)),
            ("cyber_guard", new Vector2(2200, 850), new Vector2(2000, 850), new Vector2(2300, 850))
        };

        /// <summary>Room 1, the piazza: the second guard and the automaton standing guard (room1b).</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room1bSpawns = {
            ("cyber_guard", new Vector2(3000, 850), new Vector2(2800, 850), new Vector2(3200, 850)),
            ("steam_automaton", new Vector2(4000, 850), null, null)
        };

        /// <summary>Room 2, the print shop (room2@3; the fourth, a slasher, is Hard's extra).</summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room2Spawns = {
            ("cyber_guard", new Vector2(Room2OffsetX + 500, 850), new Vector2(Room2OffsetX + 300, 850), new Vector2(Room2OffsetX + 700, 850)),
            ("cyber_guard", new Vector2(Room2OffsetX + 1200, 850), null, null),
            ("chrono_slasher", new Vector2(Room2OffsetX + 2000, 850), new Vector2(Room2OffsetX + 1800, 850), new Vector2(Room2OffsetX + 2200, 850)),
            ("chrono_slasher", new Vector2(Room2OffsetX + 900, 850), null, null)
        };

        /// <summary>The print shop's authored count (its manifest row's @3).</summary>
        public const int Room2AuthoredCount = 3;

        /// <summary>
        /// Room 3, the workshop gauntlet (room3@7). GU-L0: the first slasher moved
        /// from 9040 (on the 8990–9090 spike strip) to 9140.
        /// </summary>
        public static readonly (string EnemyID, Vector2 Position, Vector2? PatrolA, Vector2? PatrolB)[] Room3Spawns = {
            ("chrono_slasher", new Vector2(Room3OffsetX + 500, 850), null, null),
            ("cyber_guard", new Vector2(Room3OffsetX + 600, 850), new Vector2(Room3OffsetX + 400, 850), new Vector2(Room3OffsetX + 900, 850)),
            ("chrono_slasher", new Vector2(Room3OffsetX + 800, 850), null, null),
            ("steam_automaton", new Vector2(Room3OffsetX + 1000, 850), null, null),
            ("chrono_slasher", new Vector2(Room3OffsetX + 1200, 850), null, null),
            ("cyber_guard", new Vector2(Room3OffsetX + 1400, 850), null, null),
            ("steam_automaton", new Vector2(Room3OffsetX + 1600, 850), null, null)
        };

        private readonly System.Collections.Generic.List<EncounterLock> _encounterLocks = new();
        private EncounterLock _streetsLock;
        private EncounterLock _piazzaLock;
        private EncounterLock _printShopLock;
        private EncounterLock _workshopLock;

        /// <summary>Florence's locks, in route order. Test and bot seam.</summary>
        public System.Collections.Generic.IReadOnlyList<EncounterLock> EncounterLocks => _encounterLocks;
        public EncounterLock StreetsLock => _streetsLock;
        public EncounterLock PiazzaLock => _piazzaLock;
        public EncounterLock PrintShopLock => _printShopLock;
        public EncounterLock WorkshopLock => _workshopLock;

        /// <summary>The checkpoint this load resumed at, or "" on a fresh entry. Test seam.</summary>
        public string ResumedCheckpointID { get; private set; } = "";

        /// <summary>
        /// Every checkpoint lists every lock whose area ends before it (the F10
        /// baseline): a resume there builds those locks Cleared, and the
        /// checkpoint guard refuses the fracture until they are.
        /// </summary>
        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, string[]> EncounterBaselines =
            new System.Collections.Generic.Dictionary<string, string[]> {
                [EntryCheckpointID] = System.Array.Empty<string>(),
                [MiddleCheckpointID] = new[] { LockStreets, LockPiazza, LockPrintShop },
                [BossRoomCheckpointID] = new[] { LockStreets, LockPiazza, LockPrintShop, LockWorkshop }
            };

        private void BuildEncounterLocks() {
            float halfBarrier = EncounterLockRules.DefaultBarrierWidthPixels * 0.5f;
            var levelBounds = new Rect2(0, 0, LevelWidth, LevelHeight);
            // The streets and the piazza: room 1's patrols and its automaton,
            // pre-placed (room 1 always spawned at load). They share one 20 px slot.
            _streetsLock = AddEncounterLock(new EncounterLockSpec {
                LockID = LockStreets,
                Area = EncounterLockSpec.Span(StreetsLockMinX, StreetsLockMaxX, LevelHeight),
                LevelBounds = levelBounds,
                EntryBarrierX = StreetsLockMinX - halfBarrier,
                ExitBarrierX = StreetsLockMaxX + halfBarrier,
                BarrierFloorY = 900f,
                Waves = new[] { EncounterLockWave.FromTable(Room1aRowID, Room1aSpawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            _piazzaLock = AddEncounterLock(new EncounterLockSpec {
                LockID = LockPiazza,
                Area = EncounterLockSpec.Span(PiazzaLockMinX, PiazzaLockMaxX, LevelHeight),
                LevelBounds = levelBounds,
                EntryBarrierX = PiazzaLockMinX - halfBarrier,
                ExitBarrierX = PiazzaLockMaxX + halfBarrier,
                BarrierFloorY = 900f,
                Waves = new[] { EncounterLockWave.FromTable(Room1bRowID, Room1bSpawns, EncounterWaveTrigger.OnBuild) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The print shop: its guards come in as the hero does, and the gear
            // train must be aligned. The exit barrier fills the workshop door below
            // its permanent column; the door still opens on the solve, the barrier
            // on the clear.
            EncounterLockWave printShopWave = EncounterLockWave.FromTable(Room2RowID, Room2Spawns, EncounterWaveTrigger.OnEngage);
            printShopWave.AuthoredCount = Room2AuthoredCount;
            _printShopLock = AddEncounterLock(new EncounterLockSpec {
                LockID = LockPrintShop,
                Area = EncounterLockSpec.Span(PrintShopLockMinX, PrintShopLockMaxX, LevelHeight),
                LevelBounds = levelBounds,
                EntryBarrierX = PrintShopLockMinX - halfBarrier,
                ExitBarrierX = Room3OffsetX,
                ExitBarrierYRange = new Vector2(WorkshopDoorTopY, 900f),
                BarrierFloorY = 900f,
                Waves = new[] { printShopWave },
                RequiredPuzzleIDs = new[] { GearPuzzleID },
                GoDirection = EncounterGoDirection.Right
            });
            // The workshop gauntlet: seven of the Severed's men (two automatons)
            // pour in as the hero steps past the Middle anchor, among the spikes,
            // the geyser and the crumbling scaffolds.
            _workshopLock = AddEncounterLock(new EncounterLockSpec {
                LockID = LockWorkshop,
                Area = EncounterLockSpec.Span(WorkshopLockMinX, WorkshopLockMaxX, LevelHeight),
                LevelBounds = levelBounds,
                EntryBarrierX = WorkshopLockMinX - halfBarrier,
                ExitBarrierX = WorkshopLockMaxX + halfBarrier,
                BarrierFloorY = 900f,
                Waves = new[] { EncounterLockWave.FromTable(Room3RowID, Room3Spawns, EncounterWaveTrigger.OnEngage) },
                ObjectiveKey = "encounter_lock_objective_clear",
                GoDirection = EncounterGoDirection.Right
            });
            // The checkpoint guard reads the baselines StoryManager holds.
            foreach (System.Collections.Generic.KeyValuePair<string, string[]> entry in EncounterBaselines) {
                StoryManager.Instance?.RegisterEncounterBaseline(
                    entry.Key, entry.Value, StoryLevelControllerBase.ExplicitEncounterBaselineVersion);
            }
        }

        private EncounterLock AddEncounterLock(EncounterLockSpec spec) {
            EncounterLock encounterLock = EncounterLock.Create(spec);
            encounterLock.StateChanged += OnEncounterLockStateChanged;
            AddChild(encounterLock);
            _encounterLocks.Add(encounterLock);
            return encounterLock;
        }

        /// <summary>
        /// Hands every lock the hero and the camera, builds the ones the resume
        /// baseline lists Cleared, and arms the rest: exits shut, room 1's
        /// members dormant. A pre-seal load (the boss already down) stands past
        /// every lock.
        /// </summary>
        private void ArmEncounterLocks() {
            string[] cleared = _bossDefeated
                ? EncounterBaselines[BossRoomCheckpointID]
                : EncounterBaselines.TryGetValue(ResumedCheckpointID ?? "", out string[] ids) ? ids : System.Array.Empty<string>();
            foreach (EncounterLock encounterLock in _encounterLocks) {
                if (encounterLock == null || !IsInstanceValid(encounterLock)) continue;
                encounterLock.Hero = _player;
                encounterLock.Camera = _camera;
                if (System.Array.IndexOf(cleared, encounterLock.LockID) >= 0) encounterLock.RestoreCleared();
                encounterLock.Arm();
            }
        }

        private void OnEncounterLockStateChanged(EncounterLock encounterLock, EncounterLockState previous,
            EncounterLockState current) => RefreshObjective();

        /// <summary>
        /// The objective from live state while the hero is still on the way to the
        /// boss: an engaged lock's (the print shop's gears first), else the
        /// approach. The boss and seal beats post their own.
        /// </summary>
        private void RefreshObjective() {
            if (_bossIntroShown || _bossDefeated || _levelComplete) return;
            EncounterLock engaged = null;
            foreach (EncounterLock encounterLock in _encounterLocks) {
                if (encounterLock != null && IsInstanceValid(encounterLock)
                    && encounterLock.State == EncounterLockState.Engaged) engaged = encounterLock;
            }
            // FIX-OBJECTIVE: the gears objective reads the lock (its latch), so it
            // and the barrier always agree.
            if (engaged != null && engaged == _printShopLock && !_printShopLock.IsRequiredPuzzleMet(GearPuzzleID)) {
                _services?.HUD?.SetObjective("florence_objective_gears");
            } else if (engaged != null) {
                _services?.HUD?.SetObjective("encounter_lock_objective_clear");
            } else {
                _services?.HUD?.SetObjective("florence_objective_reach_boss");
            }
        }

        /// <summary>
        /// Room four uses the reusable <see cref="BossEncounterController"/> with the
        /// authored boss scene: real Hurtbox wiring, authored abilities, and HUD/dust
        /// flow driven by the boss events instead of per-frame polling.
        /// </summary>
        private void SpawnBoss(float x, float y) {
            _bossEncounter = new BossEncounterController {
                Name = "BorgiaInquisitorEncounter",
                Position = new Vector2(x, y),
                Data = AuthoredResources.Load<BossData>("res://resources/Bosses/borgia_inquisitor.tres"),
                RevealDistance = 800f,
                // P1 (2026-10-04): dormant until the player is inside the walled
                // arena, and leashed to it.
                ArenaBounds = BossArenaBounds,
                // Package 12 W8 (N01): a pre-seal load never respawns the boss.
                SpawnOnReady = !_restoringAwaitingSeal
            };
            _bossEncounter.BossRevealed += OnBossRevealed;
            _bossEncounter.BossDefeated += OnBossDefeated;
            _bossEncounter.PhaseEntered += OnBossPhaseEntered;
            AddChild(_bossEncounter);
        }

        // === Package 12 W9 (M18) — the Borgia Inquisitor's Phase 2 arena ===
        //
        // Design §6 / the Level 1 layout: "Phase 2 (50% HP): the scaffolding burns
        // away, shrinking the arena, and he gains an after-image dash as his third
        // ability; no speed multiplier." The dash lives on the boss resource; the
        // arena half lives here. On the encounter's Phase 1 (the second phase) both
        // scaffolding platforms run the standard Crumbling Platform beat (0.8 s
        // shake, 1.2 s collapse) and never come back, and two burning barricades
        // slide in from the outer walls across the transition window, pushing
        // anything in their way inward. The shrunk floor stays wider than the
        // Inquisitor's 7-unit ranged band.

        /// <summary>Where the barricades come to rest: inner faces 600 px apart.</summary>
        public const float ArenaShrinkLeftWallX = Room4OffsetX + 160f;
        public const float ArenaShrinkRightWallX = Room4OffsetX + 780f;
        /// <summary>The barricades' slide, inside the boss's 2 s transition window.</summary>
        public const float ArenaShrinkSeconds = 1.6f;
        private const float BarricadeThickness = 20f;
        /// <summary>A burned platform stays gone for the rest of the fight.</summary>
        private const float BurnedScaffoldRespawnSeconds = 1.0e9f;

        private readonly System.Collections.Generic.List<CrumblingPlatform> _scaffolding = new();
        private AnimatableBody2D _barricadeWest;
        private AnimatableBody2D _barricadeEast;
        private float _shrinkElapsed = -1f;
        private bool _recoveryBound;

        /// <summary>The two Phase 2 scaffolding platforms (test seam).</summary>
        public System.Collections.Generic.IReadOnlyList<CrumblingPlatform> Scaffolding => _scaffolding;

        /// <summary>True once Phase 2 has burned the scaffolding and closed the arena.</summary>
        public bool ArenaShrunk { get; private set; }

        /// <summary>The encounter (test seam).</summary>
        public BossEncounterController BossEncounter => _bossEncounter;

        /// <summary>Inner faces of the arena right now: the shrunk span once closed, else the original walls.</summary>
        public float ArenaInnerLeftX => ArenaShrunk ? ArenaShrinkLeftWallX + BarricadeThickness : Room4OffsetX + 20f;
        public float ArenaInnerRightX => ArenaShrunk ? ArenaShrinkRightWallX : Room4OffsetX + 940f;

        public AnimatableBody2D BarricadeWest => _barricadeWest;
        public AnimatableBody2D BarricadeEast => _barricadeEast;

        private CrumblingPlatform BuildScaffolding(string id, float x, float y, float width) {
            var platform = new CrumblingPlatform {
                Name = $"Scaffold_{id}",
                PlatformID = id,
                RespawnDuration = BurnedScaffoldRespawnSeconds,
                // The burn is a phase rule, not a timeline state: a death rewind
                // mid-fight must not stand the scaffolding back up under a boss
                // that stays in Phase 2.
                RewindPolicy = StoryRewindPolicy.PreserveCurrentState,
                Position = new Vector2(x, y),
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            platform.AddChild(new CollisionShape2D {
                Name = "CollisionShape2D",
                Shape = new RectangleShape2D { Size = new Vector2(width, 16) }
            });
            platform.AddChild(new ColorRect {
                Name = "Visual",
                Size = new Vector2(width, 16),
                Position = new Vector2(-width / 2f, -8),
                Color = new Color(0.35f, 0.22f, 0.1f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            AddChild(platform);
            return platform;
        }

        private void OnBossPhaseEntered(int phase) {
            if (phase < 1) return;
            // The phase change usually lands inside the killing hit's physics
            // flush, where new collision bodies may not enter the space; hop off
            // the flush. Direct (test) calls stay synchronous.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) {
                Callable.From(BeginArenaShrink).CallDeferred();
            } else {
                BeginArenaShrink();
            }
        }

        /// <summary>Phase 2's arena change. Idempotent; public as a test seam.</summary>
        public void BeginArenaShrink() {
            if (ArenaShrunk) return;
            ArenaShrunk = true;
            foreach (CrumblingPlatform platform in _scaffolding) {
                if (platform != null && IsInstanceValid(platform)) platform.TriggerCollapse();
            }
            _barricadeWest = BuildBarricade("BurningBarricadeWest", Room4OffsetX);
            _barricadeEast = BuildBarricade("BurningBarricadeEast", Room4OffsetX + 940f);
            _shrinkElapsed = 0f;
            if (!_recoveryBound && EventBus.Instance != null) {
                _recoveryBound = true;
                EventBus.Instance.OnRecoveryLanded += OnRecoveryLanded;
            }
        }

        private AnimatableBody2D BuildBarricade(string name, float x) {
            var barricade = new AnimatableBody2D {
                Name = name,
                Position = new Vector2(x, 0f),
                // Driven from _PhysicsProcess directly (the TrapdoorPlatform
                // idiom); the body still carries its motion into what it meets.
                SyncToPhysics = false,
                CollisionLayer = CollisionLayers.Environment,
                CollisionMask = 0
            };
            barricade.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(BarricadeThickness, LevelHeight) },
                Position = new Vector2(BarricadeThickness / 2f, LevelHeight / 2f)
            });
            barricade.AddChild(new ColorRect {
                Size = new Vector2(BarricadeThickness, LevelHeight),
                Color = new Color(0.95f, 0.42f, 0.12f, 0.85f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            AddChild(barricade);
            return barricade;
        }

        public override void _PhysicsProcess(double delta) {
            if (_shrinkElapsed < 0f) return;
            AdvanceArenaShrink((float)delta);
        }

        /// <summary>Slides both barricades toward their rest positions. Test seam.</summary>
        public void AdvanceArenaShrink(float delta) {
            if (_shrinkElapsed < 0f) return;
            _shrinkElapsed = Mathf.Min(ArenaShrinkSeconds, _shrinkElapsed + Mathf.Max(0f, delta));
            float t = ArenaShrinkSeconds <= 0f ? 1f : _shrinkElapsed / ArenaShrinkSeconds;
            if (_barricadeWest != null && IsInstanceValid(_barricadeWest)) {
                _barricadeWest.Position = new Vector2(Mathf.Lerp(Room4OffsetX, ArenaShrinkLeftWallX, t), 0f);
            }
            if (_barricadeEast != null && IsInstanceValid(_barricadeEast)) {
                _barricadeEast.Position = new Vector2(Mathf.Lerp(Room4OffsetX + 940f, ArenaShrinkRightWallX, t), 0f);
            }
            if (_shrinkElapsed >= ArenaShrinkSeconds) {
                _shrinkElapsed = -1f;
                KeepInsideShrunkArena(_player);
            }
        }

        /// <summary>
        /// A death rewind can land on history from before the barricades closed —
        /// possibly between an old wall and its barricade. The landing is moved
        /// inside the shrunk arena (never outside the level's play space).
        /// </summary>
        private void OnRecoveryLanded(RecoveryHoldPayload payload) => KeepInsideShrunkArena(_player);

        private void KeepInsideShrunkArena(PlayerController player) {
            if (!ArenaShrunk || player == null || !IsInstanceValid(player)) return;
            float x = player.GlobalPosition.X;
            // P1 (2026-10-04): since the west wall has a doorway, a hero can be
            // west of the arena when the barricades close (hitting the boss from
            // the doorway, or a rewind landing back in the workshop). The closed
            // arena is the only way forward, so such a hero is brought in too
            // rather than locked out with the boss.
            if (x > Room4OffsetX + 960f) return; // beyond the arena: nothing there
            float left = ArenaInnerLeftX + 24f;
            float right = ArenaInnerRightX - 24f;
            if (x >= left && x <= right) return;
            player.GlobalPosition = new Vector2(Mathf.Clamp(x, left, right), player.GlobalPosition.Y);
        }

        // === Boss and completion flow ===

        private void OnBossRevealed() {
            if (_bossIntroShown) return;
            _bossIntroShown = true;
            _services?.HUD?.SetObjective("florence_objective_defeat_boss");
            _services?.Dialogue?.StartSequence("level_01.boss_intro");
        }

        /// <summary>
        /// Wallet-receipt tally (audit H-1): kill dust is awarded solely by the
        /// physical pickup <see cref="StoryDropSystem"/> spawns, so the level must
        /// not re-raise a kill's drop; it only listens to what the wallet was paid.
        /// </summary>
        private void OnDustAwarded(int amount) => _dustEarnedThisLevel += Mathf.Max(0, amount);

        /// <summary>Package 11 A10 (F05): labels a collected award's category.</summary>
        private void OnDustAwardAttributed(DustAwardCollectedPayload payload) {
            switch (payload.Source) {
                case DustAwardSource.Boss:
                    _bossDustEarned += Mathf.Max(0, payload.Amount);
                    SealReadiness.NoteBossPickupCollected();
                    break;
                case DustAwardSource.Extractor: _extractorDustEarned += Mathf.Max(0, payload.Amount); break;
                case DustAwardSource.Secret: _optionalDustEarned += Mathf.Max(0, payload.Amount); break;
            }
        }

        private void OnBossDefeated(BossDefeatedPayload payload) {
            if (_bossDefeated) return;
            _bossDefeated = true;
            _services?.HUD?.SetObjective("florence_objective_complete");
            // Package 11 A10 (F05): the boss award is a physical pickup whose
            // wallet payment AND results label both land at collection, so the
            // defeat beat no longer labels dust the wallet has not been paid.
            // Package 12 W8 (N01): the defeat commits AwaitingSeal; after the
            // defeat beat the anchor arms and waits for the player's seal. The
            // exit sequence is the post-seal restoration vignette.
            SealReadiness.CommitDefeat(SealingAnchorID);
            if (!IsInsideTree()) { EnterAwaitingSeal(); return; }
            var timer = GetTree().CreateTimer(1.5);
            timer.Timeout += () => {
                if (IsInstanceValid(this) && IsInsideTree()) EnterAwaitingSeal();
            };
        }

        private bool _completionPresented;

        private void ShowCompletionResults() {
            if (_completionPresented) return;
            _completionPresented = true;
            CommitLevelCompletion();
            _services?.Audio?.ReleaseToAmbient();

            var results = LevelResultsPanel.CreateDefault();
            results.ReturnRequested += () => StoryManager.Instance?.ReturnToHub();
            AddChild(results);
            // Package 11 A10 (F05): the six-category itemisation. Level 1 has no
            // Integrity clock, so it can never pay a tier bonus — which is
            // precisely why the all-Restored maxima are 787 / 1,094 and not
            // 792 / 1,100. The losses line reads the value the completion froze
            // before its attempt reset (D12, RESULTS-DUST-LINES-AFTER-RESET), and
            // the dust held across a reload rides the carried-in line beside it.
            results.ShowResults(
                "florence_level_title",
                Mathf.Max(0, _dustEarnedThisLevel - _bossDustEarned
                    - _extractorDustEarned - _optionalDustEarned),
                _extractorDustEarned,
                _bossDustEarned,
                _optionalDustEarned,
                StoryManager.Instance?.LastLevelDustCarriedIn ?? 0,
                StoryManager.Instance?.LastLevelDustLost ?? 0,
                0,
                StoryManager.Instance?.LastLevelRankBonusDust ?? 0,
                StoryManager.Instance?.LastLevelRank,
                StoryManager.Instance?.LastLevelCompletionSeconds ?? 0f,
                StoryManager.Instance?.LastLevelRewindsUsed ?? 0);
        }
    }
}
