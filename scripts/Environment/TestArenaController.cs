using Godot;
using FTT.Characters;

namespace FTT.Environment {

    public partial class TestArenaController : Node2D {

        public override void _Ready() {
            string characterID = FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID ?? "einstein";
            if (string.IsNullOrEmpty(characterID)) characterID = "einstein";

            var player = CharacterFactory.CreateCharacter(characterID, 0);
            player.Name = "Player";
            player.Position = new Vector2(700, 600);
            AddChild(player);

            var dummy = new TrainingDummy();
            dummy.Name = "TrainingDummy";
            dummy.Position = new Vector2(1200, 600);
            AddChild(dummy);

            var dummy2 = new TrainingDummy();
            dummy2.Name = "TrainingDummy2";
            dummy2.Position = new Vector2(500, 400);
            AddChild(dummy2);
        }
    }
}
