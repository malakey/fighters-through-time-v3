using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.UI;

namespace FTT.Environment {

    public partial class HubWorldController : Node2D {
        private const string CalibrationBaySpawnName = "CalibrationBaySpawn";
        private static readonly Vector2 CalibrationBaySpawnPosition = new(1750, 850);

        // === Package 12 W7: arrivals, crew and the act-boundary selector ===

        /// <summary>
        /// M11: every hub arrival — a level completion and a Collapse alike —
        /// lands on the Bridge, directly in front of the Chronal Repository.
        /// The Calibration Bay anchor stays for Level 0 and the Training Wing.
        /// </summary>
        public const string BridgeSpawnName = "BridgeSpawn";
        public static readonly Vector2 BridgeSpawnPosition = new(2060, 850);

        /// <summary>Medic Okafor's post on the Observation Deck (fore).</summary>
        public const string OkaforNodeName = "MedicOkafor";
        private static readonly Vector2 OkaforPosition = new(560, 860);

        private Area2D _okaforArea;
        private bool _playerInOkafor;

        // --- Package 12 W5 (M26): Holodeck return anchor ----------------------
        /// <summary>Name of the spawn marker in front of the Holodeck console.</summary>
        public const string HolodeckConsoleSpawnName = "HolodeckConsoleSpawn";
        /// <summary>
        /// Inside the console's 90 px interaction area (the console stands at
        /// x = 2700), so a returning player can reopen it without walking.
        /// </summary>
        public static readonly Vector2 HolodeckConsoleSpawnPosition = new(2680, 850);
        private bool _arriveAtHolodeckConsole;
        private bool _reopenHolodeckConsole;

        /// <summary>
        /// Holodeck exits (pause Exit, Return to Ship, Reconfigure) land in front of
        /// the console, and Reconfigure reopens its panel. Both flags are one-shot
        /// and consumed here, before anything else reads the session.
        /// </summary>
        private void ConsumeHolodeckArrival() {
            if (GameManager.Instance == null) return;
            SessionData session = GameManager.Instance.CurrentSession;
            _arriveAtHolodeckConsole = session.ArriveAtHolodeckConsole;
            _reopenHolodeckConsole = session.ReopenHolodeckConsole;
            session.ArriveAtHolodeckConsole = false;
            session.ReopenHolodeckConsole = false;
            GameManager.Instance.CurrentSession = session;
        }

        /// <summary>Where the player spawns on arrival. Test seam.</summary>
        public Vector2 ArrivalSpawnPosition =>
            _arriveAtHolodeckConsole ? HolodeckConsoleSpawnPosition : BridgeSpawnPosition;

        private PlayerController _player;
        private Area2D _portalArea;
        private Area2D _repositoryArea;
        private Area2D _holodeckArea;
        private Area2D _sarahArea;
        private Label _hudLabel;
        private Label _dustLabel;
        private Label _depositToast;
        private float _depositToastTimer;
        private bool _portalReady = true;
        private bool _playerInRepository;
        private bool _playerInHolodeck;
        private bool _playerInSarah;
        private ResonanceGridPanel _resonancePanel;
        private TimelineRestartPanel _timelineRestartPanel;
        private HolodeckConsolePanel _holodeckPanel;
        private StorySceneServices _services;

        public override void _Ready() {
            ConsumeHolodeckArrival();
            RestoreLockedStoryCharacter();
            BuildHubEnvironment();
            SpawnPlayer();
            BuildHUD();
            _services = StorySceneBootstrapper.Attach(
                this,
                "res://resources/Dialogue/hub_dialogue.tres",
                includeHUD: false,
                includeRewind: false,
                audioSetPath: AudioSetPaths.Hub);
            AnnounceCompletionDeposit();
            BindCrewDialogue();
            // Package 12 W5 (M26): Holodeck Reconfigure reopens the console panel.
            if (_reopenHolodeckConsole) OpenHolodeckConsole();
        }

        /// <summary>
        /// A campaign save locks its character for the whole playthrough. Holodeck
        /// fighter sessions may change the session character, so the hub always
        /// restores the locked character from the active story save.
        /// </summary>
        private void RestoreLockedStoryCharacter() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return;
            SessionData session = GameManager.Instance.CurrentSession;
            session.ReturnToHubAfterFighterMatch = false;
            if (session.ActiveSaveSlot >= 0 && session.ActiveSaveSlot < SaveManager.Instance.SaveSlots.Length) {
                StorySaveData save = SaveManager.Instance.SaveSlots[session.ActiveSaveSlot];
                if (save != null && !string.IsNullOrWhiteSpace(save.SelectedCharacterID)) {
                    session.SelectedCharacterID = save.SelectedCharacterID;
                }
            }
            GameManager.Instance.CurrentSession = session;
        }

        // === Package 12 W2 region: H02 — the hub never deposits ============
        //
        // Dust banks ONLY in the level-completion transaction
        // (StoryManager.CommitCompletionTransaction). The hub's old _Ready
        // auto-deposit and the Repository-interact deposit are both deleted: a
        // hub return after a Collapse or a voluntary exit deposits nothing, and
        // the open attempt keeps its remaining (post-fee) dust in the save's
        // LevelChronalDust until that level is completed. The Repository's job
        // is spending and reviewing: it shows what the last completed level
        // banked plus whatever the open attempt still holds.

        /// <summary>
        /// Arrival notice for a completion that has ALREADY banked. Reads the
        /// completion transaction's result; deposits nothing.
        /// </summary>
        private void AnnounceCompletionDeposit() {
            UpdateDustDisplay();
            if (StoryManager.Instance?.TryConsumeCompletionDepositNotice(out int banked) != true) return;
            if (_depositToast == null) return;
            _depositToast.Text = string.Format(Tr("hub_dust_deposited_toast"), banked);
            _depositToast.Visible = true;
            _depositToastTimer = 4.0f;
        }

        /// <summary>
        /// The Repository deposit ledger, as (translation key, amount) lines.
        /// Pure so it is testable without a hub scene: the last completed level's
        /// banked amount when there is one this session, then the open attempt's
        /// held, undeposited amount when it is non-zero. Neither line implies
        /// the held amount is spendable (HUD contract / F02).
        /// </summary>
        public static System.Collections.Generic.List<(string Key, int Amount)> RepositoryLedgerLines(
            int lastCompletedBanked, int attemptHeld) {
            var lines = new System.Collections.Generic.List<(string, int)>();
            if (lastCompletedBanked > 0) lines.Add((RepositoryLastBankedKey, lastCompletedBanked));
            if (attemptHeld > 0) lines.Add((RepositoryAttemptHeldKey, attemptHeld));
            return lines;
        }

        public const string RepositoryLastBankedKey = "hub_repository_last_banked";
        public const string RepositoryAttemptHeldKey = "hub_repository_attempt_held";

        private Label _repositoryLedgerLabel;

        private void UpdateRepositoryLedger() {
            if (_repositoryLedgerLabel == null) return;
            StoryManager story = StoryManager.Instance;
            var lines = RepositoryLedgerLines(
                story?.LastCompletionDepositDust ?? 0, story?.AttemptHeldDust ?? 0);
            var text = new System.Text.StringBuilder();
            foreach ((string key, int amount) in lines) {
                if (text.Length > 0) text.Append('\n');
                text.Append(string.Format(Tr(key), amount));
            }
            _repositoryLedgerLabel.Text = text.ToString();
            _repositoryLedgerLabel.Visible = lines.Count > 0;
        }

        // === end Package 12 W2 region ========================================

        public override void _Process(double delta) {
            if (_depositToastTimer > 0f) {
                _depositToastTimer -= (float)delta;
                if (_depositToastTimer <= 0f && _depositToast != null) _depositToast.Visible = false;
            }
        }

        private void BuildHubEnvironment() {
            var bg = new ColorRect();
            bg.Name = "Background";
            bg.Size = new Vector2(3840, 1080);
            bg.Position = new Vector2(0, 0);
            bg.Color = new Color(0.025f, 0.05f, 0.11f, 0.24f);
            bg.ZIndex = -250;
            bg.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(bg);

            BuildFloor(0, 900, 3840);

            BuildPlatform(400, 700, 300);
            BuildPlatform(1200, 650, 250);
            BuildPlatform(2200, 700, 280);

            BuildWall(0, 0, 1080);
            BuildWall(3820, 0, 1080);

            BuildCalibrationBayAnchor();
            BuildBridgeAnchor();
            BuildPortal();
            BuildRepository();
            BuildHolodeck();
            BuildSarahNPC();
            BuildOkaforNPC();
            BuildDecorations();
        }

        /// <summary>
        /// The Calibration Bay anchor (Training Wing). Since Package 12 W7 (M11)
        /// it is no longer the arrival point — every level completion and every
        /// Collapse lands on the Bridge (<see cref="BridgeSpawnName"/>); the bay
        /// is Level 0's room.
        /// </summary>
        private void BuildCalibrationBayAnchor() {
            var anchor = new Marker2D {
                Name = CalibrationBaySpawnName,
                Position = CalibrationBaySpawnPosition
            };
            AddChild(anchor);

            var bayLabel = new Label();
            bayLabel.Text = Tr("hub_calibration_bay");
            bayLabel.Position = new Vector2(CalibrationBaySpawnPosition.X - 90, CalibrationBaySpawnPosition.Y - 250);
            bayLabel.CustomMinimumSize = new Vector2(180, 20);
            bayLabel.HorizontalAlignment = HorizontalAlignment.Center;
            bayLabel.AddThemeFontSizeOverride("font_size", 11);
            bayLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 0.8f, 0.8f));
            AddChild(bayLabel);
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
            portalLabel.Text = Tr("hub_portal_prompt");
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
            repoLabel.Text = Tr("hub_repository");
            repoLabel.Position = new Vector2(-70, -100);
            repoLabel.CustomMinimumSize = new Vector2(140, 20);
            repoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            repoLabel.AddThemeFontSizeOverride("font_size", 10);
            repoLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.5f));
            _repositoryArea.AddChild(repoLabel);

            _repositoryArea.BodyEntered += body => {
                if (body is PlayerController) _playerInRepository = true;
            };
            _repositoryArea.BodyExited += body => {
                if (body is PlayerController) _playerInRepository = false;
            };

            AddChild(_repositoryArea);
        }

        /// <summary>
        /// Holodeck Arena Console: opens the compact in-hub configuration panel
        /// for a human-vs-CPU practice match (design Section 10.3); launching
        /// flags the session to return here.
        /// </summary>
        private void BuildHolodeck() {
            _holodeckArea = new Area2D();
            _holodeckArea.Name = "HolodeckConsole";
            _holodeckArea.Position = new Vector2(2700, 860);
            _holodeckArea.CollisionLayer = CollisionLayers.Trigger;
            _holodeckArea.CollisionMask = CollisionLayers.Player;

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(90, 90);
            col.Shape = shape;
            col.Position = new Vector2(0, -45);
            _holodeckArea.AddChild(col);

            var consoleVisual = new ColorRect();
            consoleVisual.Size = new Vector2(70, 90);
            consoleVisual.Position = new Vector2(-35, -90);
            consoleVisual.Color = new Color(0.35f, 0.2f, 0.5f, 0.85f);
            _holodeckArea.AddChild(consoleVisual);

            var hologram = new ColorRect();
            hologram.Size = new Vector2(46, 34);
            hologram.Position = new Vector2(-23, -84);
            hologram.Color = new Color(0.7f, 0.4f, 1f, 0.9f);
            _holodeckArea.AddChild(hologram);

            var holoLabel = new Label();
            holoLabel.Text = Tr("hub_holodeck");
            holoLabel.Position = new Vector2(-80, -112);
            holoLabel.CustomMinimumSize = new Vector2(160, 20);
            holoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            holoLabel.AddThemeFontSizeOverride("font_size", 10);
            holoLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.4f, 1f));
            _holodeckArea.AddChild(holoLabel);

            AddChild(new Marker2D {
                Name = HolodeckConsoleSpawnName,
                Position = HolodeckConsoleSpawnPosition
            });

            _holodeckArea.BodyEntered += body => {
                if (body is PlayerController) _playerInHolodeck = true;
            };
            _holodeckArea.BodyExited += body => {
                if (body is PlayerController) _playerInHolodeck = false;
            };

            AddChild(_holodeckArea);
        }

        /// <summary>Commander Sarah interaction point on the bridge.</summary>
        private void BuildSarahNPC() {
            _sarahArea = new Area2D();
            _sarahArea.Name = "CommanderSarah";
            _sarahArea.Position = new Vector2(3100, 860);
            _sarahArea.CollisionLayer = CollisionLayers.Trigger;
            _sarahArea.CollisionMask = CollisionLayers.Player;

            var col = new CollisionShape2D();
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(90, 100);
            col.Shape = shape;
            col.Position = new Vector2(0, -50);
            _sarahArea.AddChild(col);

            var body = new ColorRect();
            body.Size = new Vector2(34, 62);
            body.Position = new Vector2(-17, -62);
            body.Color = new Color(0.2f, 0.55f, 0.45f);
            _sarahArea.AddChild(body);

            var head = new ColorRect();
            head.Size = new Vector2(22, 18);
            head.Position = new Vector2(-11, -80);
            head.Color = new Color(0.85f, 0.7f, 0.55f);
            _sarahArea.AddChild(head);

            var npcLabel = new Label();
            npcLabel.Text = Tr("hub_npc_sarah");
            npcLabel.Position = new Vector2(-80, -104);
            npcLabel.CustomMinimumSize = new Vector2(160, 20);
            npcLabel.HorizontalAlignment = HorizontalAlignment.Center;
            npcLabel.AddThemeFontSizeOverride("font_size", 10);
            npcLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.85f, 0.65f));
            _sarahArea.AddChild(npcLabel);

            _sarahArea.BodyEntered += body2 => {
                if (body2 is PlayerController) _playerInSarah = true;
            };
            _sarahArea.BodyExited += body2 => {
                if (body2 is PlayerController) _playerInSarah = false;
            };

            AddChild(_sarahArea);
        }

        /// <summary>
        /// Design Section 10.3 "In-Hub Configuration Surface": the console opens
        /// a compact configuration panel in the hub — CPU difficulty, CPU
        /// character, stage, rules — and launches straight into the match. It
        /// deliberately does NOT route through the three-screen Fighter select.
        /// </summary>
        private void OpenHolodeckConsole() {
            _holodeckPanel = new HolodeckConsolePanel { Name = "HolodeckConsolePanel" };
            _holodeckPanel.Closed += () => {
                _holodeckPanel?.QueueFree();
                _holodeckPanel = null;
                _player.ProcessMode = ProcessModeEnum.Inherit;
            };
            _player.ProcessMode = ProcessModeEnum.Disabled;
            GetNode<CanvasLayer>("HubHUD").AddChild(_holodeckPanel);
        }

        private void BuildDecorations() {
            var shipTitle = new Label();
            shipTitle.Name = "ShipTitle";
            shipTitle.Text = Tr("hub_ship_title");
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
            _player.Position = ArrivalSpawnPosition;
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
            _hudLabel.Text = Tr("hub_interaction_help");
            _hudLabel.Position = new Vector2(20, 20);
            _hudLabel.AddThemeFontSizeOverride("font_size", 14);
            _hudLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.9f));
            canvas.AddChild(_hudLabel);

            _dustLabel = new Label();
            _dustLabel.Position = new Vector2(20, 50);
            _dustLabel.AddThemeFontSizeOverride("font_size", 13);
            _dustLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            canvas.AddChild(_dustLabel);

            // Package 12 W2 (H02): the Repository deposit ledger — the last
            // completed level's bank plus any dust an open attempt still holds.
            _repositoryLedgerLabel = new Label {
                Name = "RepositoryLedger",
                Position = new Vector2(20, 104),
                Visible = false
            };
            _repositoryLedgerLabel.AddThemeFontSizeOverride("font_size", 12);
            _repositoryLedgerLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.8f, 0.9f));
            canvas.AddChild(_repositoryLedgerLabel);
            UpdateDustDisplay();

            var nextLevelLabel = new Label();
            nextLevelLabel.Name = "NextLevelLabel";
            nextLevelLabel.Text = IsCampaignCompleted()
                ? Tr("hub_campaign_complete")
                : string.Format(
                    Tr("hub_next_mission"),
                    Tr(CampaignLevelNameKey(StoryManager.Instance?.CurrentLevel ?? CampaignLevel.Tutorial)));
            nextLevelLabel.Position = new Vector2(20, 80);
            nextLevelLabel.AddThemeFontSizeOverride("font_size", 12);
            nextLevelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.2f));
            canvas.AddChild(nextLevelLabel);

            _depositToast = new Label();
            _depositToast.Name = "DepositToast";
            _depositToast.Visible = false;
            _depositToast.Position = new Vector2(760, 140);
            _depositToast.CustomMinimumSize = new Vector2(400, 30);
            _depositToast.HorizontalAlignment = HorizontalAlignment.Center;
            _depositToast.AddThemeFontSizeOverride("font_size", 16);
            _depositToast.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.3f));
            canvas.AddChild(_depositToast);
        }

        /// <summary>
        /// Hub next-mission label key for every campaign slot. Replaces the
        /// two-case prototype switch (Package 5 A1); levels 2-15 now name
        /// themselves rather than falling back to the tutorial string.
        /// </summary>
        public static string CampaignLevelNameKey(CampaignLevel level) => level switch {
            CampaignLevel.Tutorial => "campaign_level_tutorial",
            CampaignLevel.Florence => "campaign_level_florence",
            CampaignLevel.Orleans => "campaign_level_orleans",
            CampaignLevel.Chicago => "campaign_level_chicago",
            CampaignLevel.Paris => "campaign_level_paris",
            CampaignLevel.Titanic => "campaign_level_titanic",
            CampaignLevel.Pompeii => "campaign_level_pompeii",
            CampaignLevel.Nassau => "campaign_level_nassau",
            CampaignLevel.Egypt => "campaign_level_egypt",
            CampaignLevel.Berlin => "campaign_level_berlin",
            CampaignLevel.London => "campaign_level_globe",
            CampaignLevel.Gettysburg => "campaign_level_gettysburg",
            CampaignLevel.Lunar => "campaign_level_lunar",
            CampaignLevel.ChronalVoid => "campaign_level_chronal_void",
            CampaignLevel.NeoEarth => "campaign_level_neo_earth",
            CampaignLevel.Alexandria => "campaign_level_alexandria",
            _ => "campaign_level_tutorial"
        };

        /// <summary>Post-campaign hub state: the Temporal Portal stands down once the timeline is restored.</summary>
        public static bool IsCampaignCompleted() => SaveManager.Instance?.IsActiveCampaignCompleted() == true;

        public const string TimelineRestoredMessageKey = "hub_portal_timeline_restored";

        /// <summary>What interacting with the Temporal Portal does right now.</summary>
        public enum HubPortalAction { OpenMission, TimelineRestartChoice, TimelineRestored }

        /// <summary>
        /// Portal gating, kept pure so it is testable without a hub scene.
        /// A completed campaign wins over everything: there is no next mission
        /// and no collapsed level left to restart.
        /// </summary>
        public static HubPortalAction ResolvePortalAction(bool campaignCompleted, bool pendingTimelineRestart) {
            if (campaignCompleted) return HubPortalAction.TimelineRestored;
            return pendingTimelineRestart ? HubPortalAction.TimelineRestartChoice : HubPortalAction.OpenMission;
        }

        /// <summary>
        /// The hub's dust line shows the <b>deposited, spendable</b> balance
        /// (Package 12 W2, H02). It used to show the carried wallet, which was
        /// always zero here because the hub deposited it on arrival; with the hub
        /// deposit gone the wallet is the open attempt's held dust, and showing
        /// it as "Chronal Dust" would imply it is spendable. The held amount is
        /// its own Repository ledger line instead.
        /// </summary>
        private void UpdateDustDisplay() {
            int dust = 0;
            if (SaveManager.Instance != null && GameManager.Instance != null) {
                int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
                if (slot >= 0 && slot < SaveManager.Instance.SaveSlots.Length) {
                    StorySaveData save = SaveManager.Instance.SaveSlots[slot];
                    dust = ResonanceProgression.SpendableBalance(save, save?.SelectedCharacterID);
                }
            }
            if (_dustLabel != null) _dustLabel.Text = string.Format(Tr("hub_carried_dust"), dust);
            UpdateRepositoryLedger();
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
                if (_resonancePanel != null && IsInstanceValid(_resonancePanel)) return;
                if (_timelineRestartPanel != null && IsInstanceValid(_timelineRestartPanel)) return;
                if (_holodeckPanel != null && IsInstanceValid(_holodeckPanel)) return;
                if (_services?.Dialogue != null && _services.Dialogue.IsSequenceActive) return;
                if (_playerInHolodeck) {
                    OpenHolodeckConsole();
                    return;
                }
                if (_playerInSarah) {
                    _services?.Dialogue?.StartSequence(HubDialogueSelector.SarahSequenceFor(CurrentPeriod()));
                    return;
                }
                if (_playerInOkafor) {
                    string okafor = HubDialogueSelector.OkaforSequenceFor(CurrentPeriod());
                    if (!string.IsNullOrEmpty(okafor)) _services?.Dialogue?.StartSequence(okafor);
                    return;
                }
                if (_playerInRepository) {
                    // H02: the terminal spends and reviews; it never deposits.
                    UpdateDustDisplay();
                    _resonancePanel = new ResonanceGridPanel { Name = "ResonanceGridPanel" };
                    _resonancePanel.Closed += () => {
                        _player.ProcessMode = ProcessModeEnum.Inherit;
                        _resonancePanel = null;
                        // Purchases and respec move the deposited balance.
                        UpdateDustDisplay();
                    };
                    _player.ProcessMode = ProcessModeEnum.Disabled;
                    GetNode<CanvasLayer>("HubHUD").AddChild(_resonancePanel);
                    return;
                }
                if (_playerInPortal && _portalReady) {
                    switch (ResolvePortalAction(
                        IsCampaignCompleted(),
                        StoryManager.Instance?.HasPendingTimelineRestart == true)) {
                        case HubPortalAction.TimelineRestored:
                            ShowPortalMessage(TimelineRestoredMessageKey);
                            break;
                        case HubPortalAction.TimelineRestartChoice:
                            ShowTimelineRestartChoice();
                            break;
                        default:
                            StoryManager.Instance?.LoadCurrentLevel();
                            break;
                    }
                }
            }
        }

        /// <summary>Reuses the deposit toast slot for short localized hub notices.</summary>
        public void ShowPortalMessage(string translationKey) {
            if (_depositToast == null) return;
            _depositToast.Text = Tr(translationKey);
            _depositToast.Visible = true;
            _depositToastTimer = 4.0f;
        }

        private void ShowTimelineRestartChoice() {
            _timelineRestartPanel = new TimelineRestartPanel { Name = "TimelineRestartPanel" };
            _timelineRestartPanel.RestartChosen += resumeFromAnchor => {
                _player.ProcessMode = ProcessModeEnum.Inherit;
                StoryManager.Instance?.RestartCollapsedLevel(resumeFromAnchor);
            };
            _timelineRestartPanel.Cancelled += CloseTimelineRestartChoice;
            _player.ProcessMode = ProcessModeEnum.Disabled;
            GetNode<CanvasLayer>("HubHUD").AddChild(_timelineRestartPanel);
        }

        private void CloseTimelineRestartChoice() {
            _player.ProcessMode = ProcessModeEnum.Inherit;
            _timelineRestartPanel?.QueueFree();
            _timelineRestartPanel = null;
        }

        // === Package 12 W7 region: Bridge arrival, crew and GAP-15 selector ===

        /// <summary>The Bridge arrival anchor (M11), labelled so the room reads as the spawn room.</summary>
        private void BuildBridgeAnchor() {
            AddChild(new Marker2D { Name = BridgeSpawnName, Position = BridgeSpawnPosition });

            var bridgeLabel = new Label {
                Name = "BridgeLabel",
                Text = Tr("hub_bridge"),
                Position = new Vector2(BridgeSpawnPosition.X - 90, BridgeSpawnPosition.Y - 250),
                CustomMinimumSize = new Vector2(180, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            bridgeLabel.AddThemeFontSizeOverride("font_size", 11);
            bridgeLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 0.8f, 0.8f));
            AddChild(bridgeLabel);
        }

        /// <summary>Medic Okafor on the Observation Deck: per-act readings of the locked hero.</summary>
        private void BuildOkaforNPC() {
            _okaforArea = new Area2D {
                Name = OkaforNodeName,
                Position = OkaforPosition,
                CollisionLayer = CollisionLayers.Trigger,
                CollisionMask = CollisionLayers.Player
            };
            _okaforArea.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(90, 100) },
                Position = new Vector2(0, -50)
            });
            _okaforArea.AddChild(new ColorRect {
                Size = new Vector2(34, 62),
                Position = new Vector2(-17, -62),
                Color = new Color(0.55f, 0.55f, 0.7f)
            });
            _okaforArea.AddChild(new ColorRect {
                Size = new Vector2(22, 18),
                Position = new Vector2(-11, -80),
                Color = new Color(0.45f, 0.32f, 0.22f)
            });
            var npcLabel = new Label {
                Text = Tr("hub_npc_okafor"),
                Position = new Vector2(-80, -104),
                CustomMinimumSize = new Vector2(160, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            npcLabel.AddThemeFontSizeOverride("font_size", 10);
            npcLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.95f));
            _okaforArea.AddChild(npcLabel);

            _okaforArea.BodyEntered += body => {
                if (body is PlayerController) _playerInOkafor = true;
            };
            _okaforArea.BodyExited += body => {
                if (body is PlayerController) _playerInOkafor = false;
            };
            AddChild(_okaforArea);
        }

        /// <summary>The current hub visit period for the active campaign.</summary>
        public static HubDialogueSelector.HubPeriod CurrentPeriod() =>
            HubDialogueSelector.PeriodFor(
                StoryManager.Instance?.CurrentLevel ?? CampaignLevel.Tutorial,
                IsCampaignCompleted());

        private bool _crewDialogueBound;

        /// <summary>
        /// GAP-15: the period's Sarah set plays once, on the first arrival of the
        /// period. Only a real campaign slot has a ledger to record it in, so a
        /// slotless launch (the developer level select, a test fixture) never
        /// auto-plays — and so never takes the tree's pause.
        /// </summary>
        private void BindCrewDialogue() {
            if (EventBus.Instance != null && !_crewDialogueBound) {
                EventBus.Instance.OnDialogueComplete += OnCrewDialogueComplete;
                _crewDialogueBound = true;
            }
            if (ActiveCampaignSave() == null) return;
            Callable.From(PlayArrivalDialogue).CallDeferred();
        }

        private void PlayArrivalDialogue() {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            StorySaveData save = ActiveCampaignSave();
            if (save == null) return;
            string arrival = HubDialogueSelector.ArrivalSequenceFor(
                CurrentPeriod(), id => save.ViewedDialogueIDs?.Contains(id) == true);
            if (!string.IsNullOrEmpty(arrival)) _services?.Dialogue?.StartSequence(arrival);
        }

        /// <summary>
        /// A crew set just resolved into the slot's ledger; write the slot so the
        /// once-only record survives a quit before the next checkpoint save.
        /// </summary>
        private void OnCrewDialogueComplete(string dialogueID) {
            if (string.IsNullOrEmpty(dialogueID)
                || !dialogueID.StartsWith("hub.", System.StringComparison.Ordinal)) return;
            int slot = GameManager.Instance?.CurrentSession.ActiveSaveSlot ?? -1;
            if (slot >= 0) SaveManager.Instance?.SaveStorySlot(slot);
        }

        private static StorySaveData ActiveCampaignSave() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return null;
            int slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (slot < 0 || slot >= SaveManager.Instance.SaveSlots.Length) return null;
            return SaveManager.Instance.SaveSlots[slot];
        }

        public override void _ExitTree() {
            if (_crewDialogueBound && EventBus.Instance != null) {
                EventBus.Instance.OnDialogueComplete -= OnCrewDialogueComplete;
            }
            _crewDialogueBound = false;
        }

        // === end Package 12 W7 region ===
    }
}
