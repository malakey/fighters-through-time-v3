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

    /// <summary>
    /// V7.2: a world object that scrubs backward along its own recorded path
    /// during a Chronal Rewind — the "one visible world object rewinding"
    /// exemplar (PathMovingPlatform). Depth is in recorded physics frames.
    /// </summary>
    public interface IRewindScrubbable {
        void BeginRewindScrub();
        void ApplyRewindScrub(int depthFrames);
        void EndRewindScrub();
    }
}
