using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;

namespace FTT.Environment {

    public partial class TestArenaController : Node2D {
        public FighterSimulationDriver FighterDriver { get; private set; }

        public override void _Ready() {
            string characterID = GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";
            string opponentID = GameManager.Instance?.CurrentSession.OpponentCharacterID ?? "joan";
            if (string.IsNullOrEmpty(opponentID)) opponentID = "joan";
            string stageID = GameManager.Instance?.CurrentSession.SelectedStageID ?? "florence_workshop";
            FighterStageCatalog catalog = FighterStageCatalog.LoadDefault();
            FighterStageData stage = catalog?.Find(stageID) ?? catalog?.FirstPlayable();
            ApplyStagePresentation(stage);

            var player = CharacterFactory.CreateCharacter(characterID, 0, applyStoryProgression: false);
            player.Name = "Player1";
            player.Position = new Vector2(700, 600);
            AddChild(player);

            var opponent = CharacterFactory.CreateCharacter(opponentID, 1, applyStoryProgression: false);
            opponent.Name = "Player2";
            opponent.Position = new Vector2(1200, 600);
            opponent.IsFacingRight = false;
            AddChild(opponent);

            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            FighterDriver = new FighterSimulationDriver { Name = "FighterSimulationDriver" };
            AddChild(FighterDriver);
            // The stage ID must be forwarded, not just the hazard identity: without
            // it the driver falls back to FighterStageGeometry.Default and a
            // TestArena-hosted match silently loses the selected stage's walls,
            // platforms, and hazard/orb anchors.
            FighterDriver.Initialize(
                player, opponent, settings, stage?.HazardTypeID ?? 1, stage?.StageID ?? stageID ?? "");
        }

        private void ApplyStagePresentation(FighterStageData stage) {
            if (stage == null) return;
            GetNodeOrNull<ColorRect>("Background").Color = stage.BackgroundColor;
            SetSurfaceColor("Ground/Visual", stage.GroundColor);
            SetSurfaceColor("Ground/TopLine", stage.AccentColor);
            foreach (string platform in new[] { "PlatformLeft", "PlatformRight", "PlatformTop" }) {
                SetSurfaceColor($"{platform}/Visual", stage.GroundColor.Lightened(0.08f));
                SetSurfaceColor($"{platform}/TopLine", stage.AccentColor);
            }
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

        private void SetSurfaceColor(string nodePath, Color color) {
            ColorRect surface = GetNodeOrNull<ColorRect>(nodePath);
            if (surface != null) surface.Color = color;
        }
    }
}
