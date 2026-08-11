using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.UI;

namespace FTT.Environment {

    public partial class HubWorldController : Node2D {
        private const string CalibrationBaySpawnName = "CalibrationBaySpawn";
        private static readonly Vector2 CalibrationBaySpawnPosition = new(1750, 850);

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
        private StorySceneServices _services;

        public override void _Ready() {
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
            AutoDepositCarriedDust();
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

        /// <summary>Returning from a mission deposits carried dust into the Repository automatically.</summary>
        private void AutoDepositCarriedDust() {
            int deposited = StoryManager.Instance?.DepositDustToActiveSave() ?? 0;
            UpdateDustDisplay();
            if (deposited <= 0 || _depositToast == null) return;
            _depositToast.Text = string.Format(Tr("hub_dust_deposited_toast"), deposited);
            _depositToast.Visible = true;
            _depositToastTimer = 4.0f;
        }

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
            BuildPortal();
            BuildRepository();
            BuildHolodeck();
            BuildSarahNPC();
            BuildDecorations();
        }

        /// <summary>
        /// The documented hub return anchor: players spawn here in front of the
        /// Chronal Repository after missions and Timeline Collapse.
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
        /// Holodeck Arena Console: opens the Fighter lobby configured for a
        /// human-vs-CPU practice match and flags the session to return here.
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

        private void OpenHolodeckLobby() {
            if (GameManager.Instance == null) return;
            SessionData session = GameManager.Instance.CurrentSession;
            session.FighterOpponentType = FighterOpponentType.Cpu;
            session.ReturnToHubAfterFighterMatch = true;
            GameManager.Instance.CurrentSession = session;
            GameManager.Instance.LoadScene("res://scenes/menus/CharacterSelect.tscn");
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
            _player.Position = CalibrationBaySpawnPosition;
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

        private void UpdateDustDisplay() {
            int dust = StoryManager.Instance?.ChronalDustCollected ?? 0;
            _dustLabel.Text = string.Format(Tr("hub_carried_dust"), dust);
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
                if (_services?.Dialogue != null && _services.Dialogue.IsSequenceActive) return;
                if (_playerInHolodeck) {
                    OpenHolodeckLobby();
                    return;
                }
                if (_playerInSarah) {
                    _services?.Dialogue?.StartSequence("hub.sarah_briefing");
                    return;
                }
                if (_playerInRepository) {
                    StoryManager.Instance?.DepositDustToActiveSave();
                    UpdateDustDisplay();
                    _resonancePanel = new ResonanceGridPanel { Name = "ResonanceGridPanel" };
                    _resonancePanel.Closed += () => {
                        _player.ProcessMode = ProcessModeEnum.Inherit;
                        _resonancePanel = null;
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
    }
}
