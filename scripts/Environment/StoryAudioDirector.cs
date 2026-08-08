using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// Per-scene Story audio owner (Package 8 B5). One of these is attached to every
    /// campaign scene by <see cref="StorySceneBootstrapper"/>; it registers the
    /// scene's <see cref="StageAudioSet"/> with the A2 stem director, moves the
    /// vertical layer as the player engages and disengages, and plays the shared
    /// environment cues (hazards, pickups, extractors).
    ///
    /// <para><b>It hands the music back on the way out.</b> The stem director is a
    /// process-lifetime autoload child, so a scene that registers a set and never
    /// releases it leaves the previous level's music playing under the next one.
    /// <see cref="_ExitTree"/> releases unconditionally, matching the project's
    /// "whoever sets it hands it back" rule for <c>SceneTree.Paused</c>.</para>
    ///
    /// <para><b>Policy lives in <see cref="StoryCombatIntensityModel"/>.</b> This node
    /// only supplies the two inputs the model cannot know: how many living hostiles
    /// are near the player, and whether a boss encounter is open. Counting is polled
    /// rather than event-driven because there is no enemy-spawned event on the bus
    /// and a wave trigger can seed several enemies in one frame; the poll runs at
    /// <see cref="PollSeconds"/>, well below the crossfade the result feeds.</para>
    /// </summary>
    public partial class StoryAudioDirector : Node {
        /// <summary>Enemies inside this radius of the player count as engaged.</summary>
        public const float DefaultEngagementRadius = 1000f;

        /// <summary>Seconds between engaged-enemy polls. Cheap, and far finer than a crossfade.</summary>
        public const float PollSeconds = 0.25f;

        /// <summary>Authored set for this scene; blank means "this scene has no music".</summary>
        [Export(PropertyHint.File, "*.tres")] public string AudioSetPath = "";

        [Export] public float EngagementRadius = DefaultEngagementRadius;

        /// <summary>Layer policy. Public so a level can widen the hold for a slow era.</summary>
        public StoryCombatIntensityModel Intensity { get; } = new();

        /// <summary>The set actually registered, or null when none resolved.</summary>
        public StageAudioSet ActiveSet { get; private set; }

        /// <summary>Engaged hostiles counted by the most recent poll.</summary>
        public int EngagedEnemyCount { get; private set; }

        private float _pollTimer;
        private bool _eventsBound;
        private StemIntensity _publishedIntensity = StemIntensity.Ambient;
        private bool _published;

        public override void _Ready() {
            ProcessMode = ProcessModeEnum.Always;
            RegisterSet();
            BindEvents();
            PublishIntensity(StemIntensity.Ambient, force: true);
        }

        public override void _ExitTree() {
            UnbindEvents();
            // Unconditional: a scene change must never leave the last level's stems
            // playing under the next one.
            if (ActiveSet != null) AudioManager.Instance?.ReleaseStageAudio();
            ActiveSet = null;
        }

        public override void _Process(double delta) {
            _pollTimer -= (float)delta;
            if (_pollTimer > 0f) return;
            float elapsed = PollSeconds - _pollTimer;
            _pollTimer = PollSeconds;
            EngagedEnemyCount = CountEngagedEnemies();
            PublishIntensity(Intensity.Advance(EngagedEnemyCount, elapsed), force: false);
        }

        // === Level flow hooks ===

        /// <summary>Boss revealed / defeated. Climax outranks the engagement layer.</summary>
        public void SetBossEngaged(bool engaged) {
            Intensity.SetBossEngaged(engaged);
            PublishIntensity(Intensity.Advance(EngagedEnemyCount, 0f), force: false);
        }

        /// <summary>
        /// Level complete: drop to the ambient bed under the results overlay. The set
        /// stays registered so the scene does not go abruptly silent; the release
        /// happens when the scene leaves the tree.
        /// </summary>
        public void ReleaseToAmbient() {
            Intensity.Reset();
            EngagedEnemyCount = 0;
            PublishIntensity(StemIntensity.Ambient, force: false);
        }

        // === Registration ===

        private void RegisterSet() {
            if (string.IsNullOrWhiteSpace(AudioSetPath)) return;
            if (!ResourceLoader.Exists(AudioSetPath)) {
                GD.PushWarning($"Story audio set missing: {AudioSetPath}");
                return;
            }
            // Authored tuning data: pinned by the cache for the process lifetime, so
            // its scripted graph is never rebuilt and reaped mid-session.
            ActiveSet = AuthoredResources.Load<StageAudioSet>(AudioSetPath);
            if (ActiveSet == null) return;
            AudioManager.Instance?.RegisterStageAudio(ActiveSet);
        }

        private void PublishIntensity(StemIntensity intensity, bool force) {
            if (!force && _published && intensity == _publishedIntensity) return;
            _published = true;
            _publishedIntensity = intensity;
            if (ActiveSet != null) AudioManager.Instance?.SetIntensity(intensity);
        }

        /// <summary>The layer this director last asked the stem director for.</summary>
        public StemIntensity PublishedIntensity => _publishedIntensity;

        // === Engagement counting ===

        private int CountEngagedEnemies() {
            SceneTree tree = GetTree();
            if (tree == null) return 0;
            if (tree.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player
                || !IsInstanceValid(player)) return 0;

            Godot.Collections.Array<Node> enemies = tree.GetNodesInGroup("Enemies");
            using var lifetime = enemies.AsDisposable();
            float radiusSquared = EngagementRadius * EngagementRadius;
            int engaged = 0;
            foreach (Node node in enemies) {
                if (node is not Node2D body || !IsInstanceValid(body)) continue;
                if (!IsHostileAlive(node)) continue;
                if (body.GlobalPosition.DistanceSquaredTo(player.GlobalPosition) <= radiusSquared) engaged++;
            }
            return engaged;
        }

        private static bool IsHostileAlive(Node node) => node switch {
            EnemyController enemy => enemy.CurrentState != EnemyState.Dead,
            BossController boss => boss.CurrentHP > 0,
            _ => false
        };

        // === Environment cues ===

        private void BindEvents() {
            if (_eventsBound || EventBus.Instance == null) return;
            _eventsBound = true;
            EventBus.Instance.OnHazardStateChanged += OnHazardStateChanged;
        }

        private void UnbindEvents() {
            if (!_eventsBound) return;
            _eventsBound = false;
            if (EventBus.Instance == null) return;
            EventBus.Instance.OnHazardStateChanged -= OnHazardStateChanged;
        }

        /// <summary>
        /// The toolkit hazards, the escape sequence, and the extractor discharge all
        /// publish through this one payload, so subscribing here covers every authored
        /// hazard without touching each template. Cooldown is silent on purpose,
        /// matching A2's rule that a recovery beat firing several times a second is
        /// noise rather than information.
        /// </summary>
        private static void OnHazardStateChanged(HazardStatePayload payload) {
            switch (payload.Phase) {
                case HazardPhase.Warning:
                    EnvironmentAudioCues.PlayHazardWarning();
                    break;
                case HazardPhase.Active:
                    EnvironmentAudioCues.PlayHazardActive();
                    break;
            }
        }
    }
}
