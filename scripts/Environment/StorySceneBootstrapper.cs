using Godot;
using FTT.UI;

namespace FTT.Environment {

    /// <summary>
    /// Runtime services shared by every campaign scene. Created once per scene by
    /// <see cref="StorySceneBootstrapper.Attach"/> so hub and level controllers do
    /// not each rebuild the dialogue box, HUD, pause menu, and rewind stack.
    /// </summary>
    public sealed class StorySceneServices {
        public DialogueManager Dialogue;
        public StoryHUD HUD;
        public PauseMenu Pause;
        public RewindPresentationOverlay RewindOverlay;
        public ChronalRewindManager RewindManager;

        /// <summary>Scene music + environment cues (Package 8 B5). Null when the scene has no set.</summary>
        public StoryAudioDirector Audio;
    }

    public static class StorySceneBootstrapper {

        /// <summary>
        /// Attaches the shared Story runtime stack to a campaign scene root.
        /// The rewind manager and overlay are level-only; the hub passes
        /// <paramref name="includeRewind"/> = false.
        ///
        /// <paramref name="audioSetPath"/> is the scene's authored
        /// <see cref="StageAudioSet"/> (see <see cref="AudioSetPaths"/>); passing ""
        /// attaches no <see cref="StoryAudioDirector"/> and the scene stays silent,
        /// which is what a test fixture that only wants the dialogue stack gets.
        /// </summary>
        public static StorySceneServices Attach(
            Node sceneRoot,
            string dialogueSetPath,
            bool includeHUD = true,
            bool includeRewind = true,
            string audioSetPath = "") {
            var services = new StorySceneServices();

            if (!string.IsNullOrWhiteSpace(audioSetPath)) {
                services.Audio = new StoryAudioDirector {
                    Name = "StoryAudioDirector",
                    AudioSetPath = audioSetPath
                };
                sceneRoot.AddChild(services.Audio);
            }

            services.Dialogue = DialogueManager.CreateDefault();
            sceneRoot.AddChild(services.Dialogue);
            if (!string.IsNullOrWhiteSpace(dialogueSetPath) &&
                !services.Dialogue.RegisterSetFromPath(dialogueSetPath)) {
                GD.PushWarning($"Dialogue set could not be loaded: {dialogueSetPath}");
            }

            if (includeHUD) {
                services.HUD = StoryHUD.CreateDefault();
                sceneRoot.AddChild(services.HUD);
            }

            // Authored themed scene when present, code-built fallback otherwise.
            services.Pause = PauseMenu.CreateDefault();
            sceneRoot.AddChild(services.Pause);

            if (includeRewind) {
                services.RewindOverlay = new RewindPresentationOverlay { Name = "RewindPresentationOverlay" };
                sceneRoot.AddChild(services.RewindOverlay);

                services.RewindManager = new ChronalRewindManager { Name = "ChronalRewindManager" };
                sceneRoot.AddChild(services.RewindManager);
            }

            return services;
        }
    }
}
