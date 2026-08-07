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
    }

    public static class StorySceneBootstrapper {

        /// <summary>
        /// Attaches the shared Story runtime stack to a campaign scene root.
        /// The rewind manager and overlay are level-only; the hub passes
        /// <paramref name="includeRewind"/> = false.
        /// </summary>
        public static StorySceneServices Attach(
            Node sceneRoot,
            string dialogueSetPath,
            bool includeHUD = true,
            bool includeRewind = true) {
            var services = new StorySceneServices();

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

            services.Pause = new PauseMenu { Name = "PauseMenu" };
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
