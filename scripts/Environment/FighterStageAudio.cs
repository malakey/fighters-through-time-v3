using FTT.Core;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Registers a Fighter stage's authored <see cref="StageAudioSet"/> with the A2
    /// stem director (Package 8 B5).
    ///
    /// <para>Shared by <c>FighterStageController</c> (the ten production stages) and
    /// <c>TestArenaController</c> (the sandbox, which hosts whichever stage the
    /// session selected). Both need the identical resolve → load → register → warn
    /// sequence, and duplicating it is exactly how one of the two silently loses its
    /// music when the path convention changes.</para>
    /// </summary>
    public static class FighterStageAudio {

        /// <summary>
        /// Resolves and registers the set for <paramref name="stageID"/>. Returns true
        /// when a set was registered, which is the caller's cue that it owes a
        /// <c>ReleaseStageAudio</c> on the way out.
        /// </summary>
        public static bool Register(string stageID) {
            StageAudioSet set = Resolve(stageID);
            if (set == null) return false;
            AudioManager.Instance?.RegisterStageAudio(set);
            return true;
        }

        /// <summary>The authored set for a stage id, or null when none exists.</summary>
        public static StageAudioSet Resolve(string stageID) {
            string path = AudioSetPaths.ForFighterStage(stageID);
            if (string.IsNullOrEmpty(path)) return null;
            if (!ResourceLoader.Exists(path)) {
                GD.PushWarning($"Fighter stage audio set missing for '{stageID}': {path}");
                return null;
            }
            return AuthoredResources.Load<StageAudioSet>(path);
        }
    }
}
