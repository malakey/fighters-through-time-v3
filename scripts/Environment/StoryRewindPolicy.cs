namespace FTT.Environment {

    public enum StoryRewindPolicy {
        RestoreCheckpointState,
        ResetToInitialState,
        PreserveCurrentState
    }

    public interface IStoryRewindable {
        StoryRewindPolicy RewindPolicy { get; }
        void CaptureCheckpointState(string checkpointID);
        void ApplyStoryRewind();
    }

    public interface IStoryRewindSimulation {
        void SetStoryRewindFrozen(bool frozen);
    }
}
