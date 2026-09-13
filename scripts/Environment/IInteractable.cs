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
    /// A world object that walks backward along its own recorded path during a
    /// Chronal Rewind — the "one visible world object rewinding" exemplar
    /// (PathMovingPlatform). Depth is in recorded physics frames.
    ///
    /// <para><b>V7.6:</b> this survives for the <b>death rewind only</b>. The
    /// manual scrub verb is retired, so there is no preview and therefore nothing
    /// to cancel — <c>CancelRewindScrub</c> went with it. Time Freeze uses
    /// <see cref="IStoryTimeFreezable"/> instead, which stops a platform where it
    /// stands rather than moving it.</para>
    /// </summary>
    public interface IRewindScrubbable {
        void BeginRewindScrub();
        void ApplyRewindScrub(int depthFrames);
        /// <summary>Playback finished: resume from the rewound position.</summary>
        void EndRewindScrub();
    }
}
