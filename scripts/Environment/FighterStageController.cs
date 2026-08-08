using Godot;
using FTT.Characters;
using FTT.Combat;
using FTT.Core;
using FTT.FighterSim;

namespace FTT.Environment {

    /// <summary>
    /// Controller for production-contract Fighter stage scenes. The authored
    /// scene provides presentation geometry, spawn/orb/hazard markers, and a
    /// FighterCamera; this controller spawns the presentation fighters at the
    /// authored markers and starts the deterministic simulation with the
    /// stage's fixed-point geometry.
    /// </summary>
    public partial class FighterStageController : Node2D {
        /// <summary>Stage identity this scene was authored for; must match the catalog entry.</summary>
        [Export] public string StageID = "";

        public FighterSimulationDriver FighterDriver { get; private set; }

        public override void _Ready() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";
            string opponentID = GameManager.Instance?.CurrentSession.OpponentCharacterID ?? "joan";
            if (string.IsNullOrEmpty(opponentID)) opponentID = "joan";

            FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
            FighterStageData stage = catalog?.Find(StageID) ?? catalog?.FirstPlayable();

            Vector2 spawnOne = GetNodeOrNull<Marker2D>("Spawns/Player1")?.GlobalPosition ?? new Vector2(700, 700);
            Vector2 spawnTwo = GetNodeOrNull<Marker2D>("Spawns/Player2")?.GlobalPosition ?? new Vector2(1200, 700);

            var player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
            player.Name = "Player1";
            player.Position = spawnOne;
            AddChild(player);

            var opponent = CharacterFactory.CreateCharacter(opponentID, 1, applyStoryProgression: false);
            opponent.Name = "Player2";
            opponent.Position = spawnTwo;
            opponent.IsFacingRight = false;
            AddChild(opponent);

            if (GetNodeOrNull<FighterCamera>("Camera2D") is FighterCamera camera) {
                camera.SetPlayers(player, opponent);
                camera.MakeCurrent();
            }

            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            FighterDriver = new FighterSimulationDriver { Name = "FighterSimulationDriver" };
            AddChild(FighterDriver);
            FighterDriver.Initialize(player, opponent, settings, stage?.HazardTypeID ?? 1, stage?.StageID ?? StageID);

            RegisterStageAudio(stage?.StageID ?? StageID);
            AddStageTitle(stage);
        }

        /// <summary>
        /// Package 8 B5. Registers the stage's authored music set at match start; the
        /// driver moves it to Combat when the countdown ends. Released in
        /// <see cref="_ExitTree"/> so leaving the stage does not carry its stems into
        /// the next scene.
        /// </summary>
        private void RegisterStageAudio(string stageID) {
            _audioRegistered = FighterStageAudio.Register(stageID);
        }

        private bool _audioRegistered;

        public override void _ExitTree() {
            if (!_audioRegistered) return;
            _audioRegistered = false;
            AudioManager.Instance?.ReleaseStageAudio();
        }

        private void AddStageTitle(FighterStageData stage) {
            if (stage == null) return;
            var title = new Label {
                Name = "StageTitle",
                Text = Tr(stage.DisplayNameKey),
                Position = new Vector2(760, 28),
                CustomMinimumSize = new Vector2(400, 40),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 22);
            title.AddThemeColorOverride("font_color", stage.AccentColor);
            GetNodeOrNull<CanvasLayer>("HUDLayer")?.AddChild(title);
        }
    }
}
