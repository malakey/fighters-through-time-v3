using FTT.Environment;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// Vertical music layers, lowest to highest. Matches the order of
    /// <see cref="StageAudioSet.Stems"/>.
    /// </summary>
    public enum StemIntensity {
        /// <summary>Exploration/puzzle bed.</summary>
        Ambient = 0,
        /// <summary>Enemies engaged: percussion and bass layered in.</summary>
        Combat = 1,
        /// <summary>Boss encounter, or a Fighter match's last stock.</summary>
        Climax = 2
    }

    /// <summary>
    /// Plays a scene's <see cref="StageAudioSet"/> as three synchronized layers and
    /// crossfades between intensities, per <c>design-godot.md</c>'s "Vertical
    /// Layering (Combat Intensity)".
    ///
    /// <para><b>Synchronized, not restarted.</b> All three stems start in the same
    /// frame and keep running; an intensity change only moves volumes. That is what
    /// <see cref="StageAudioSet.StemsAreSynchronized"/> and the shared
    /// <see cref="StageAudioSet.LoopSeconds"/> buy — the layers stay phase-aligned,
    /// so combat can drop in mid-bar without a seam. A set that declares itself
    /// unsynchronized falls back to playing one layer at a time and hard-switching,
    /// which is audibly worse but correct for stems that were not bar-matched.</para>
    ///
    /// <para><b>The mix is additive.</b> Ambient stays at full level at every
    /// intensity (it is the bed, not a layer to be replaced), combat adds on top from
    /// <see cref="StemIntensity.Combat"/>, and climax adds on top of both. See
    /// <see cref="TargetMix"/>.</para>
    ///
    /// <para>Fades are a linear amplitude interpolation over the set's authored
    /// <see cref="StageAudioSet.CrossfadeSeconds"/>, driven by
    /// <see cref="AdvanceFades"/> rather than a <c>Tween</c> node — one pump point
    /// for the whole audio framework, no per-transition node churn, and a
    /// deterministic seam the tests can step frame by frame.</para>
    ///
    /// <para>Owned by <see cref="AudioManager"/>. Scenes talk to it through
    /// <c>AudioManager.RegisterStageAudio</c> / <c>SetIntensity</c> /
    /// <c>ReleaseStageAudio</c>.</para>
    /// </summary>
    public partial class StemDirector : Node {
        /// <summary>Level written to a fully faded-out stem. Below audibility, but finite.</summary>
        public const float SilentDb = -60f;

        private const int StemCount = 3;

        // Additive vertical layering: [ambient, combat, climax] amplitude per intensity.
        private static readonly float[][] MixTable = {
            new[] { 1f, 0f, 0f },
            new[] { 1f, 1f, 0f },
            new[] { 1f, 1f, 1f }
        };

        private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[StemCount];
        private readonly float[] _currentLevel = new float[StemCount];
        private readonly float[] _targetLevel = new float[StemCount];

        private float _crossfadeSeconds = 2f;
        private bool _synchronized = true;

        /// <summary>The registered set, or null when no scene has registered one.</summary>
        public StageAudioSet ActiveSet { get; private set; }

        public StemIntensity CurrentIntensity { get; private set; } = StemIntensity.Ambient;

        public bool IsActive => ActiveSet != null;

        /// <summary>Target amplitudes for an intensity, in stem order.</summary>
        public static float[] TargetMix(StemIntensity intensity) {
            int index = Mathf.Clamp((int)intensity, 0, MixTable.Length - 1);
            return MixTable[index];
        }

        public override void _Ready() {
            // Music must keep mixing (and keep crossfading) while the tree is paused,
            // otherwise the pause snapshot's duck would freeze halfway applied.
            ProcessMode = ProcessModeEnum.Always;
            for (int index = 0; index < StemCount; index++) {
                var player = new AudioStreamPlayer {
                    Name = $"Stem{(StemIntensity)index}",
                    Bus = AudioBuses.Music,
                    VolumeDb = SilentDb
                };
                AddChild(player);
                _players[index] = player;
            }
        }

        /// <summary>
        /// Registers a scene's music set and starts it at
        /// <see cref="StemIntensity.Ambient"/>. Replaces any previously registered
        /// set; a scene that forgets to release is therefore not a leak, only a seam.
        /// </summary>
        public void RegisterStageAudio(StageAudioSet set) {
            ReleaseStageAudio();
            if (set == null) return;

            ActiveSet = set;
            _crossfadeSeconds = Mathf.Max(0.01f, set.CrossfadeSeconds);
            _synchronized = set.StemsAreSynchronized;
            CurrentIntensity = StemIntensity.Ambient;

            AudioStream[] stems = set.Stems();
            float[] mix = TargetMix(CurrentIntensity);
            for (int index = 0; index < StemCount; index++) {
                _currentLevel[index] = mix[index];
                _targetLevel[index] = mix[index];
                AudioStreamPlayer player = _players[index];
                if (player == null) continue;
                player.Stream = stems[index];
                ApplyLevel(index);
                bool shouldPlay = stems[index] != null && (_synchronized || mix[index] > 0f);
                if (shouldPlay) player.Play();
            }
        }

        /// <summary>
        /// Moves the mix to a new intensity. Idempotent, and safe to call before any
        /// set is registered — the requested intensity is remembered so a set
        /// registered later does not start at the wrong layer.
        /// </summary>
        public void SetIntensity(StemIntensity intensity) {
            if (CurrentIntensity == intensity && IsActive) return;
            CurrentIntensity = intensity;
            if (!IsActive) return;

            float[] mix = TargetMix(intensity);
            for (int index = 0; index < StemCount; index++) _targetLevel[index] = mix[index];

            if (_synchronized) return;

            // Unsynchronized stems cannot be mixed, only swapped.
            for (int index = 0; index < StemCount; index++) {
                _currentLevel[index] = _targetLevel[index];
                ApplyLevel(index);
                AudioStreamPlayer player = _players[index];
                if (player == null || player.Stream == null) continue;
                if (_currentLevel[index] > 0f) {
                    if (!player.Playing) player.Play();
                } else if (player.Playing) {
                    player.Stop();
                }
            }
        }

        /// <summary>Stops and unbinds the registered set. Safe to call twice.</summary>
        public void ReleaseStageAudio() {
            ActiveSet = null;
            CurrentIntensity = StemIntensity.Ambient;
            for (int index = 0; index < StemCount; index++) {
                _currentLevel[index] = 0f;
                _targetLevel[index] = 0f;
                AudioStreamPlayer player = _players[index];
                if (player == null) continue;
                if (player.Playing) player.Stop();
                player.Stream = null;
                player.VolumeDb = SilentDb;
            }
        }

        /// <summary>Advances the crossfade. Driven by <see cref="AudioManager"/>.</summary>
        public void AdvanceFades(double delta) {
            if (!IsActive) return;
            float step = (float)delta / _crossfadeSeconds;
            for (int index = 0; index < StemCount; index++) {
                if (Mathf.IsEqualApprox(_currentLevel[index], _targetLevel[index])) continue;
                _currentLevel[index] = Mathf.MoveToward(_currentLevel[index], _targetLevel[index], step);
                ApplyLevel(index);
            }
        }

        /// <summary>True once every stem has reached its target level.</summary>
        public bool IsCrossfadeComplete() {
            for (int index = 0; index < StemCount; index++) {
                if (!Mathf.IsEqualApprox(_currentLevel[index], _targetLevel[index])) return false;
            }
            return true;
        }

        /// <summary>Current amplitude (0-1) of a stem, mid-crossfade.</summary>
        public float GetStemLevel(StemIntensity stem) => _currentLevel[(int)stem];

        /// <summary>Level the crossfade is heading for.</summary>
        public float GetTargetStemLevel(StemIntensity stem) => _targetLevel[(int)stem];

        /// <summary>The dB actually written to the stem's player.</summary>
        public float GetStemVolumeDb(StemIntensity stem) => _players[(int)stem]?.VolumeDb ?? SilentDb;

        public bool IsStemPlaying(StemIntensity stem) => _players[(int)stem]?.Playing ?? false;

        public AudioStream GetStemStream(StemIntensity stem) => _players[(int)stem]?.Stream;

        private void ApplyLevel(int index) {
            AudioStreamPlayer player = _players[index];
            if (player == null) return;
            float level = _currentLevel[index];
            player.VolumeDb = level <= 0f ? SilentDb : Mathf.Max(SilentDb, Mathf.LinearToDb(level));
        }
    }
}
