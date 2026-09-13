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

    /// <summary>
    /// V7.6 Time Freeze (F03): a world object that stops simulating while the
    /// Story player's Time Freeze is active and resumes from exactly where it
    /// stopped.
    ///
    /// <para><b>Deliberately NOT <see cref="IStoryRewindSimulation"/>.</b> The
    /// death-rewind freeze may cancel an enemy's attack, zero its velocity and
    /// clear enemy projectiles, because the world is about to be restored to a
    /// past state anyway. Time Freeze must do none of that: the contract is
    /// "all actors resume preserved positions, velocities, attack phases and
    /// remaining timers — no catch-up ticks, no projectile clearing, no
    /// accumulated damage". An implementer therefore only latches a flag its own
    /// tick early-returns on; it must not mutate state on the way in or out.</para>
    ///
    /// <para>Implementers keep their collision shapes live: solid geometry stays
    /// solid during a freeze and a frozen moving platform is still a floor. That
    /// is why every simulating body implements this interface instead of taking
    /// the pooled-projectile <c>ProcessMode.Disabled</c> fallback, which would
    /// drop the floor out from under a player standing on it.</para>
    /// </summary>
    public interface IStoryTimeFreezable {
        void SetTimeFrozen(bool frozen);
    }
}
