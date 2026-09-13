using System.Collections.Generic;
using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// Package 11 A3 (V7.6): the authored numbers behind the Collapse Tremor.
    /// Plain constants, no Godot types, so the pin tests read them directly.
    /// </summary>
    public static class CollapseTremorRules {

        /// <summary>Continuous background shake while any Tremor stage is live.</summary>
        public const float ContinuousShakeIntensity = 0.1f;

        /// <summary>A jolt's intensity and length.</summary>
        public const float JoltIntensity = 0.3f;
        public const float JoltSeconds = 0.4f;

        /// <summary>Stage 1 jolt cadence, in seconds (randomized inside the band).</summary>
        public const float Stage1JoltMinSeconds = 6f;
        public const float Stage1JoltMaxSeconds = 8f;

        /// <summary>Stage 2 jolt cadence: twice as urgent.</summary>
        public const float Stage2JoltMinSeconds = 3f;
        public const float Stage2JoltMaxSeconds = 4f;

        /// <summary>Debris cadence: about one every 4 s at stage 1, every 2 s at stage 2.</summary>
        public const float Stage1DebrisIntervalSeconds = 4f;
        public const float Stage2DebrisIntervalSeconds = 2f;

        /// <summary>The readable warning before a piece falls.</summary>
        public const float DebrisTelegraphSeconds = 0.75f;

        /// <summary>Damage as a fraction of the victim's MAXIMUM HP, never a flat number.</summary>
        public const float DamageFractionOfMaxHP = 0.05f;

        /// <summary>Never more than this many pieces telegraphing or falling at once.</summary>
        public const int MaxAirborneDebris = 2;

        /// <summary>How close the hero must be to an impact to be hit by it.</summary>
        public const float DebrisHitRadiusPixels = 90f;

        /// <summary>Debris never lands inside this radius of a Restoration Font or an activated fracture.</summary>
        public const float SafeZoneRadiusPixels = 220f;

        /// <summary>Horizontal spread around the hero that debris targets.</summary>
        public const float DebrisSpreadPixels = 260f;

        /// <summary>A flagged platform respawns this long after it collapses — always.</summary>
        public const float FracturePlatformRespawnSeconds = 5f;

        /// <summary>The 5%-of-max-HP hit, floored at 1 so it is never a no-op.</summary>
        public static int DebrisDamage(int maximumHP) =>
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, maximumHP) * DamageFractionOfMaxHP));

        /// <summary>Jolt cadence band for a stage.</summary>
        public static (float Min, float Max) JoltBand(int stage) => stage >= 2
            ? (Stage2JoltMinSeconds, Stage2JoltMaxSeconds)
            : (Stage1JoltMinSeconds, Stage1JoltMaxSeconds);

        /// <summary>Debris interval for a stage. 0 for a calm stage.</summary>
        public static float DebrisInterval(int stage) => stage switch {
            2 => Stage2DebrisIntervalSeconds,
            1 => Stage1DebrisIntervalSeconds,
            _ => 0f
        };
    }

    /// <summary>
    /// The Collapse Tremor: the world visibly coming apart as Timeline
    /// Integrity runs out.
    ///
    /// A stage machine driven purely by <see cref="StoryManager.TimelineIntegrityPercent"/>
    /// — stage 1 below 20%, stage 2 below 10% — attached by
    /// <see cref="StoryLevelControllerBase"/> for TIMED levels only, which is
    /// how the untimed Tutorial and Florence opt out without a special case.
    ///
    /// Stage 1: a continuous 0.1 shake, a 0.3 jolt for 0.4 s every 6-8 s, and
    /// debris about every 4 s. Stage 2: jolts every 3-4 s and debris twice as
    /// often. Flagged <c>FractureEligible</c> platforms become unstable at
    /// stage 1 and above — and every one of them respawns, so no gap can ever
    /// become uncrossable.
    ///
    /// It stops whenever the clock pauses and ends for good at the PreBoss
    /// checkpoint: the boss arena is always stable. A granted F11 timer
    /// recovery starts at or above 25%, above the threshold, so the Tremor
    /// clears on recovery and can return later.
    ///
    /// Accessibility: all shake goes through <see cref="CameraShake"/>, which
    /// already applies the Settings screen-shake scale (0 disables it
    /// entirely). Decorative glow, ghosting and heavy desaturation belong to
    /// the C01a Reduced Temporal Effects preset that A8 ships; the Tremor's
    /// TIMING is never reduced, and the danger markings are never removed.
    /// The HUD tick and the shared crack-glow overlay are A8's — this
    /// publishes <see cref="EventBus.OnCollapseTremorChanged"/> and touches no
    /// HUD file.
    /// </summary>
    public partial class CollapseTremorController : Node2D {

        /// <summary>Scene-tree group so a level or a test can find the controller.</summary>
        public const string Group = "collapse_tremor";

        /// <summary>Pooled debris scene.</summary>
        public const string DebrisScenePath = "res://scenes/vfx/falling_debris.tscn";

        /// <summary>Sarah's one-per-level bark on the first crossing into the Tremor.</summary>
        public const string FirstCrossingBarkKey = "tremor_first_crossing_bark";

        /// <summary>0 calm, 1 below 20%, 2 below 10%.</summary>
        public int Stage { get; private set; }

        /// <summary>True once the level has played its single Sarah bark.</summary>
        public bool HasPlayedFirstCrossingBark { get; private set; }

        /// <summary>True once the PreBoss lock ended the Tremor for good this attempt.</summary>
        public bool IsRetired { get; private set; }

        /// <summary>Debris pieces this controller has launched. Test seam.</summary>
        public int DebrisLaunched { get; private set; }

        /// <summary>Test seam: replaces the pooled spawn so a pin test needs no pool.</summary>
        internal System.Func<Vector2, FallingDebris> DebrisSpawnOverrideForTesting;

        private readonly List<FallingDebris> _airborne = new();
        private float _joltTimer;
        private float _debrisTimer;
        private readonly RandomNumberGenerator _random = new();

        public override void _Ready() {
            AddToGroup(Group);
            _random.Randomize();
            ArmTimers();
        }

        public override void _PhysicsProcess(double delta) => Tick((float)delta);

        /// <summary>
        /// One update. Internal and directly callable so the pin tests drive
        /// the real state machine rather than a parallel copy.
        /// </summary>
        internal void Tick(float delta) {
            StoryManager story = StoryManager.Instance;
            if (story == null) return;

            // The boss arena is always stable: once the PreBoss fracture locks
            // the gauge, the Tremor retires for the rest of the attempt.
            if (story.IsPreBossLocked) {
                Retire();
                return;
            }
            // The Tremor stops whenever the clock stops — dialogue, the pause
            // menu, the death-rewind presentation, the boss intro. A frozen
            // world does not shake.
            if (!story.IsIntegrityClockRunning || story.IsIntegrityClockPaused) {
                if (Stage != 0) SetStage(0);
                return;
            }

            int stage = TimelineIntegrityRules.TremorStage(story.TimelineIntegrityPercent);
            if (stage != Stage) SetStage(stage);
            if (Stage <= 0) return;

            CameraShake.Instance?.Shake(CollapseTremorRules.ContinuousShakeIntensity, delta * 2f);

            _joltTimer -= delta;
            if (_joltTimer <= 0f) {
                CameraShake.Instance?.Shake(CollapseTremorRules.JoltIntensity, CollapseTremorRules.JoltSeconds);
                ShakeFracturePlatforms();
                ArmJoltTimer();
            }

            _debrisTimer -= delta;
            if (_debrisTimer <= 0f) {
                TryLaunchDebris();
                _debrisTimer = CollapseTremorRules.DebrisInterval(Stage);
            }
        }

        private void SetStage(int stage) {
            Stage = Mathf.Clamp(stage, 0, 2);
            ArmTimers();
            if (Stage > 0 && !HasPlayedFirstCrossingBark) {
                HasPlayedFirstCrossingBark = true;
                // Non-blocking: a bark, never a dialogue sequence. The clock
                // keeps running through it by construction.
                EnvironmentNotice.Post(FirstCrossingBarkKey, this, seconds: 3.5f);
            }
            EventBus.Instance?.RaiseCollapseTremorChanged(new TremorPayload { Level = Stage });
        }

        private void Retire() {
            if (IsRetired) return;
            IsRetired = true;
            Stage = 0;
            EventBus.Instance?.RaiseCollapseTremorChanged(new TremorPayload { Level = 0 });
        }

        private void ArmTimers() {
            ArmJoltTimer();
            _debrisTimer = CollapseTremorRules.DebrisInterval(Stage);
        }

        private void ArmJoltTimer() {
            (float min, float max) = CollapseTremorRules.JoltBand(Stage);
            _joltTimer = _random.RandfRange(min, max);
        }

        // === Falling debris =================================================

        /// <summary>
        /// Launches one piece, unless the two-airborne cap is full or no legal
        /// landing spot exists. Debris never lands inside a Restoration Font's
        /// radius or on an activated checkpoint — a safe beat must stay safe.
        /// </summary>
        internal bool TryLaunchDebris() {
            PruneAirborne();
            if (_airborne.Count >= CollapseTremorRules.MaxAirborneDebris) return false;
            if (GetTree()?.GetFirstNodeInGroup("StoryPlayer") is not PlayerController player) return false;
            if (!GodotObject.IsInstanceValid(player)) return false;

            Vector2 target = new(
                player.GlobalPosition.X
                    + _random.RandfRange(-CollapseTremorRules.DebrisSpreadPixels, CollapseTremorRules.DebrisSpreadPixels),
                player.GlobalPosition.Y);
            if (!IsLandingLegal(target)) return false;

            FallingDebris debris = SpawnDebris(target);
            if (debris == null) return false;
            debris.Launch(target);
            _airborne.Add(debris);
            DebrisLaunched++;
            return true;
        }

        /// <summary>
        /// The exclusion rule. Internal so the pin test asserts the exact
        /// predicate the launcher uses.
        /// </summary>
        internal bool IsLandingLegal(Vector2 target) {
            Node scene = GetTree()?.CurrentScene ?? GetParent();
            if (scene == null) return true;
            var safeNodes = new List<Node>();
            CollectSafeZoneAnchors(scene, safeNodes);
            foreach (Node node in safeNodes) {
                if (node is not Node2D anchor || !GodotObject.IsInstanceValid(anchor)) continue;
                if (anchor.GlobalPosition.DistanceTo(target) <= CollapseTremorRules.SafeZoneRadiusPixels) return false;
            }
            return true;
        }

        /// <summary>
        /// Collects the world objects whose radius debris must avoid: every
        /// Restoration Font (a heal must never be interrupted by rubble) and
        /// every ALREADY-ACTIVATED fracture (a stabilized checkpoint is a safe
        /// beat by definition). Neither type carries a scene-tree group, so
        /// this walks the scene once per launch attempt — a handful of nodes,
        /// a handful of times per level.
        /// </summary>
        private static void CollectSafeZoneAnchors(Node node, List<Node> results) {
            if (node is RestorationFont) results.Add(node);
            else if (node is CheckpointTrigger { IsActivated: true }) results.Add(node);
            foreach (Node child in node.GetChildren()) CollectSafeZoneAnchors(child, results);
        }

        private FallingDebris SpawnDebris(Vector2 target) {
            if (DebrisSpawnOverrideForTesting != null) return DebrisSpawnOverrideForTesting(target);
            Node spawned = PoolManager.Instance?.Spawn(FallingDebris.PoolID, target, GetParent() ?? this);
            return spawned as FallingDebris;
        }

        private void PruneAirborne() {
            for (int index = _airborne.Count - 1; index >= 0; index--) {
                FallingDebris debris = _airborne[index];
                if (debris == null || !GodotObject.IsInstanceValid(debris) || !debris.IsAirborne) {
                    _airborne.RemoveAt(index);
                }
            }
        }

        /// <summary>Airborne pieces right now. Test seam.</summary>
        internal int AirborneCount {
            get {
                PruneAirborne();
                return _airborne.Count;
            }
        }

        // === Unstable platforms =============================================

        /// <summary>
        /// Every jolt destabilizes the flagged platforms the hero is standing
        /// near. Only <see cref="CrumblingPlatform.FractureEligible"/>
        /// platforms are ever touched — pressure plates, latched-switch gates,
        /// <c>PathMovingPlatform</c> and the pre-boss approach are excluded by
        /// simply never carrying the flag, and every flagged platform keeps its
        /// respawn timer, so no gap can become uncrossable.
        /// </summary>
        internal int ShakeFracturePlatforms() {
            SceneTree tree = GetTree();
            if (tree == null) return 0;
            int triggered = 0;
            Godot.Collections.Array<Node> hazards = tree.GetNodesInGroup("story_hazard");
            using var hazardsLifetime = hazards.AsDisposable();
            foreach (Node node in hazards) {
                if (node is not CrumblingPlatform platform || !GodotObject.IsInstanceValid(platform)) continue;
                if (!platform.FractureEligible) continue;
                if (platform.TriggerCollapse()) triggered++;
            }
            return triggered;
        }
    }
}
