using FTT.Characters;

namespace FTT.Environment {

    public interface IInteractable {
        string InteractionID { get; }
        string PromptKey { get; }
        bool CanInteract(PlayerController player);
        void Interact(PlayerController player);
    }

    public interface IQuarterTurnTarget {
        void RotateQuarterTurn(int direction);
    }
}
