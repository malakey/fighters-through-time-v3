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

            var player = CharacterFactory.CreateCharacter(characterID, 0);
            player.Name = "Player1";
            player.Position = new Vector2(700, 600);
            AddChild(player);

            var opponent = CharacterFactory.CreateCharacter(opponentID, 1);
            opponent.Name = "Player2";
            opponent.Position = new Vector2(1200, 600);
            opponent.IsFacingRight = false;
            AddChild(opponent);

            MatchSettings settings = GameManager.Instance?.CurrentSession.MatchSettings
                ?? MatchSettings.GetDefault();
            FighterDriver = new FighterSimulationDriver { Name = "FighterSimulationDriver" };
            AddChild(FighterDriver);
            FighterDriver.Initialize(player, opponent, settings);
        }
    }
}
