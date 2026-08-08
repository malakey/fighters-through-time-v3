using System.Collections.Generic;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// The named mixer states from <c>design-godot.md</c>'s "Audio Snapshots"
    /// section. <c>NormalGameplay</c> is not a member: it is the absence of every
    /// snapshot, so that "no snapshot active" and "baseline mix" cannot drift apart.
    /// </summary>
    public enum AudioSnapshot {
        /// <summary>Pause menu: BGM/SFX ducked and muffled, UI lifted so prompts read.</summary>
        Pause,
        /// <summary>Below 20% HP: high end rolled off to build tension.</summary>
        LowHealth,
        /// <summary>Ultimate cinematic: everything but the ultimate itself steps back 12 dB.</summary>
        Ultimate,
        /// <summary>Chronal Rewind: music ducks under the rewind sweep and tick.</summary>
        Rewind
    }

    /// <summary>
    /// Applies named snapshots as <em>offsets</em> on top of the player's saved bus
    /// volumes, tweening each change rather than snapping it.
    ///
    /// <para>Offset-based is the whole point. The rewind duck this replaces
    /// (<c>RewindPresentationOverlay</c>) snapshotted the Music bus's absolute dB on
    /// entry and wrote it back on exit, so a settings change during a rewind was
    /// silently reverted when the rewind ended. Here the mixer owns the base volume
    /// (<see cref="SetBaseVolume"/>, driven by the settings sliders and by boot-time
    /// restore) and recomputes <c>base + offset</c> whenever either side moves;
    /// neither can clobber the other.</para>
    ///
    /// <para>Snapshots stack additively: Pause plus Ultimate ducks music by the sum.
    /// Filter cutoffs stack by <em>minimum</em> instead — the most aggressive active
    /// muffle wins, and releasing it restores the next-most-aggressive rather than
    /// jumping straight back to transparent.</para>
    ///
    /// <para>Plain C# class, not a Node: <see cref="AudioManager"/> owns it and pumps
    /// <see cref="AdvanceFades"/>. That keeps the tween deterministic and testable
    /// without waiting on engine frames.</para>
    /// </summary>
    public class AudioSnapshotMixer {
        /// <summary>Cutoff at which the authored low-pass filters are effectively transparent.</summary>
        public const float TransparentCutoffHz = 20500f;

        /// <summary>Default blend time for a snapshot apply/release, in seconds.</summary>
        public const float DefaultFadeSeconds = 0.25f;

        /// <summary>Buses the snapshot layer is allowed to move.</summary>
        public static readonly string[] MixedBuses = { AudioBuses.Music, AudioBuses.SFX, AudioBuses.UI };

        /// <summary>
        /// Buses whose base volume the mixer owns. Master carries a base (the master
        /// slider) but no snapshot ever offsets it — ducking the master would duck
        /// the very UI prompts a snapshot is trying to keep legible.
        /// </summary>
        public static readonly string[] BasedBuses = { AudioBuses.Master, AudioBuses.Music, AudioBuses.SFX, AudioBuses.UI };

        private readonly struct SnapshotDefinition {
            public readonly float MusicDb;
            public readonly float SfxDb;
            public readonly float UiDb;
            public readonly float MusicCutoffHz;
            public readonly float SfxCutoffHz;
            public readonly float FadeSeconds;

            public SnapshotDefinition(
                float musicDb, float sfxDb, float uiDb,
                float musicCutoffHz, float sfxCutoffHz, float fadeSeconds) {
                MusicDb = musicDb;
                SfxDb = sfxDb;
                UiDb = uiDb;
                MusicCutoffHz = musicCutoffHz;
                SfxCutoffHz = sfxCutoffHz;
                FadeSeconds = fadeSeconds;
            }
        }

        // Values follow design-godot.md: GamePaused muffles BGM/SFX and lifts UI;
        // LowHealth rolls off the high end; UltimateCinematic ducks 12 dB; Rewind
        // matches the -12 dB the authored rewind payload already carried.
        private static readonly Dictionary<AudioSnapshot, SnapshotDefinition> Definitions = new() {
            [AudioSnapshot.Pause] = new SnapshotDefinition(-8f, -12f, 2f, 900f, 1200f, 0.20f),
            [AudioSnapshot.LowHealth] = new SnapshotDefinition(-2f, 0f, 0f, 3200f, TransparentCutoffHz, 0.60f),
            [AudioSnapshot.Ultimate] = new SnapshotDefinition(-12f, -12f, 0f, TransparentCutoffHz, TransparentCutoffHz, 0.15f),
            [AudioSnapshot.Rewind] = new SnapshotDefinition(-12f, 0f, 0f, 1800f, TransparentCutoffHz, 0.30f)
        };

        /// <summary>Active snapshots and their (possibly overridden) music offset.</summary>
        private readonly Dictionary<AudioSnapshot, float> _active = new();

        private readonly Dictionary<string, float> _baseDb = new();
        private readonly Dictionary<string, float> _currentOffsetDb = new();
        private readonly Dictionary<string, float> _fadeFromDb = new();
        private readonly Dictionary<string, float> _targetOffsetDb = new();

        private float _currentMusicCutoff = TransparentCutoffHz;
        private float _targetMusicCutoff = TransparentCutoffHz;
        private float _fadeFromMusicCutoff = TransparentCutoffHz;
        private float _currentSfxCutoff = TransparentCutoffHz;
        private float _targetSfxCutoff = TransparentCutoffHz;
        private float _fadeFromSfxCutoff = TransparentCutoffHz;

        private float _fadeSeconds = DefaultFadeSeconds;
        private float _fadeElapsed;

        public AudioSnapshotMixer() {
            foreach (string bus in BasedBuses) _baseDb[bus] = 0f;
            foreach (string bus in MixedBuses) {
                _currentOffsetDb[bus] = 0f;
                _fadeFromDb[bus] = 0f;
                _targetOffsetDb[bus] = 0f;
            }
        }

        /// <summary>True while the mixer is still blending toward its targets.</summary>
        public bool IsFading => _fadeElapsed < _fadeSeconds;

        public bool IsSnapshotActive(AudioSnapshot snapshot) => _active.ContainsKey(snapshot);

        public int ActiveSnapshotCount => _active.Count;

        /// <summary>Offset currently written to the bus, mid-tween.</summary>
        public float GetOffsetDb(string busName) =>
            _currentOffsetDb.TryGetValue(busName, out float value) ? value : 0f;

        /// <summary>Offset the tween is heading for.</summary>
        public float GetTargetOffsetDb(string busName) =>
            _targetOffsetDb.TryGetValue(busName, out float value) ? value : 0f;

        public float GetBaseDb(string busName) =>
            _baseDb.TryGetValue(busName, out float value) ? value : 0f;

        public float CurrentMusicCutoffHz => _currentMusicCutoff;
        public float TargetMusicCutoffHz => _targetMusicCutoff;
        public float CurrentSfxCutoffHz => _currentSfxCutoff;

        /// <summary>
        /// Sets a bus's baseline volume — the settings slider value, or the saved
        /// value restored at boot. Snapshot offsets ride on top of it and survive
        /// the change.
        /// </summary>
        public void SetBaseVolume(string busName, float decibels) {
            if (!_baseDb.ContainsKey(busName)) {
                // A bus outside the mixer's ownership is written straight through.
                AudioServer.SetBusVolumeDb(AudioBuses.Resolve(busName), decibels);
                return;
            }
            _baseDb[busName] = decibels;
            WriteBus(busName);
        }

        public void ApplySnapshot(AudioSnapshot snapshot) => ApplySnapshot(snapshot, Definitions[snapshot].MusicDb);

        /// <summary>
        /// Applies a snapshot with an explicit music offset, for callers that carry
        /// an authored duck depth (the rewind payload's <c>MusicDuckDecibels</c>).
        /// Re-applying an already-active snapshot just updates its offset.
        /// </summary>
        public void ApplySnapshot(AudioSnapshot snapshot, float musicOffsetDb) {
            _active[snapshot] = musicOffsetDb;
            Retarget(Definitions[snapshot].FadeSeconds);
        }

        public void ReleaseSnapshot(AudioSnapshot snapshot) {
            if (!_active.Remove(snapshot)) return;
            Retarget(Definitions[snapshot].FadeSeconds);
        }

        public void ReleaseAllSnapshots() {
            if (_active.Count == 0) return;
            _active.Clear();
            Retarget(DefaultFadeSeconds);
        }

        /// <summary>
        /// Skips the remainder of the current blend. Used on scene teardown, where
        /// there is no frame left to finish it in.
        /// </summary>
        public void SettleImmediately() {
            _fadeElapsed = _fadeSeconds;
            foreach (string bus in MixedBuses) {
                _currentOffsetDb[bus] = _targetOffsetDb[bus];
                WriteBus(bus);
            }
            _currentMusicCutoff = _targetMusicCutoff;
            _currentSfxCutoff = _targetSfxCutoff;
            WriteCutoffs();
        }

        /// <summary>Advances every in-flight blend. Driven by <see cref="AudioManager"/>.</summary>
        public void AdvanceFades(double delta) {
            if (!IsFading) return;
            _fadeElapsed = Mathf.Min(_fadeSeconds, _fadeElapsed + (float)delta);
            float t = _fadeSeconds <= 0f ? 1f : _fadeElapsed / _fadeSeconds;

            foreach (string bus in MixedBuses) {
                _currentOffsetDb[bus] = Mathf.Lerp(_fadeFromDb[bus], _targetOffsetDb[bus], t);
                WriteBus(bus);
            }
            _currentMusicCutoff = Mathf.Lerp(_fadeFromMusicCutoff, _targetMusicCutoff, t);
            _currentSfxCutoff = Mathf.Lerp(_fadeFromSfxCutoff, _targetSfxCutoff, t);
            WriteCutoffs();
        }

        private void Retarget(float fadeSeconds) {
            float music = 0f;
            float sfx = 0f;
            float ui = 0f;
            float musicCutoff = TransparentCutoffHz;
            float sfxCutoff = TransparentCutoffHz;

            foreach (KeyValuePair<AudioSnapshot, float> entry in _active) {
                SnapshotDefinition definition = Definitions[entry.Key];
                music += entry.Value;
                sfx += definition.SfxDb;
                ui += definition.UiDb;
                musicCutoff = Mathf.Min(musicCutoff, definition.MusicCutoffHz);
                sfxCutoff = Mathf.Min(sfxCutoff, definition.SfxCutoffHz);
            }

            _fadeFromDb[AudioBuses.Music] = _currentOffsetDb[AudioBuses.Music];
            _fadeFromDb[AudioBuses.SFX] = _currentOffsetDb[AudioBuses.SFX];
            _fadeFromDb[AudioBuses.UI] = _currentOffsetDb[AudioBuses.UI];
            _targetOffsetDb[AudioBuses.Music] = music;
            _targetOffsetDb[AudioBuses.SFX] = sfx;
            _targetOffsetDb[AudioBuses.UI] = ui;

            _fadeFromMusicCutoff = _currentMusicCutoff;
            _fadeFromSfxCutoff = _currentSfxCutoff;
            _targetMusicCutoff = musicCutoff;
            _targetSfxCutoff = sfxCutoff;

            _fadeSeconds = Mathf.Max(0.001f, fadeSeconds);
            _fadeElapsed = 0f;
        }

        private void WriteBus(string busName) {
            int index = AudioServer.GetBusIndex(busName);
            if (index < 0) return;
            float baseDb = _baseDb.TryGetValue(busName, out float b) ? b : 0f;
            float offsetDb = _currentOffsetDb.TryGetValue(busName, out float o) ? o : 0f;
            AudioServer.SetBusVolumeDb(index, baseDb + offsetDb);
        }

        private void WriteCutoffs() {
            WriteCutoff(AudioBuses.Music, _currentMusicCutoff);
            WriteCutoff(AudioBuses.SFX, _currentSfxCutoff);
        }

        /// <summary>
        /// Pushes a cutoff into the bus's authored low-pass filter. Silently does
        /// nothing when the layout has no filter on that bus, so a stripped-down
        /// layout degrades to volume-only snapshots instead of throwing.
        /// </summary>
        private static void WriteCutoff(string busName, float cutoffHz) {
            int index = AudioServer.GetBusIndex(busName);
            if (index < 0) return;
            int effectCount = AudioServer.GetBusEffectCount(index);
            for (int effectIndex = 0; effectIndex < effectCount; effectIndex++) {
                if (AudioServer.GetBusEffect(index, effectIndex) is AudioEffectLowPassFilter filter) {
                    filter.CutoffHz = cutoffHz;
                    return;
                }
            }
        }
    }
}
