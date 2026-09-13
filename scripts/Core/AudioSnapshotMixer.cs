using System.Collections.Generic;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// The named mixer states from <c>design-godot.md</c>'s "Audio Snapshots"
    /// section, extended by C01b's priority ladder. <c>NormalGameplay</c> is not a
    /// member: it is the absence of every profile, so "no profile active" and
    /// "baseline mix" cannot drift apart.
    ///
    /// <para>Declaration order is deliberately <b>not</b> the priority order — the
    /// four original members keep their ordinals so no logged or saved value
    /// shifts. <see cref="AudioSnapshotMixer.PriorityOf"/> owns the ladder.</para>
    /// </summary>
    public enum AudioSnapshot {
        /// <summary>Pause menu: background ducked and muffled. UI is never boosted.</summary>
        Pause,
        /// <summary>Below 20% HP: high end rolled off to build tension.</summary>
        LowHealth,
        /// <summary>Ultimate cinematic: the background steps back 12 dB.</summary>
        Ultimate,
        /// <summary>Death Rewind / Collapse recovery: music ducks under the sweep and tick.</summary>
        Rewind,

        // ---- C01b additions ------------------------------------------------
        // A priority row with no authored background treatment "adds none", so an
        // empty definition is a legal placeholder: the row still wins the ladder
        // and therefore still SUPPRESSES the lower rows, which is the point.

        /// <summary>Act III Anchor Snap recovery presentation.</summary>
        AnchorSnap,
        /// <summary>Defy proc, boss phase change, or other scripted presentation.</summary>
        DefyOrScripted,
        /// <summary>Story Time Freeze: the subdued time-stop treatment.</summary>
        TimeFreeze,
        /// <summary>Low Timeline Integrity / Collapse Tremor atmosphere.</summary>
        LowIntegrityTremor,
        /// <summary>Underwater or other liquid environment.</summary>
        Underwater
    }

    /// <summary>
    /// Applies <b>one</b> named background profile as an <em>offset</em> on top of
    /// the player's saved bus volumes, tweening each change rather than snapping it.
    ///
    /// <para><b>Offset-based is the whole point.</b> The rewind duck this replaced
    /// (<c>RewindPresentationOverlay</c>) snapshotted the Music bus's absolute dB on
    /// entry and wrote it back on exit, so a settings change during a rewind was
    /// silently reverted when the rewind ended. Here the mixer owns the base volume
    /// (<see cref="SetBaseVolume"/>, driven by the settings sliders and by boot-time
    /// restore) and recomputes <c>base + offset</c> whenever either side moves;
    /// neither can clobber the other.</para>
    ///
    /// <para><b>Package 11 A8 / C01b: priority selection, not additive stacking.</b>
    /// Profiles used to stack — Pause plus Ultimate ducked music by the sum, and
    /// cutoffs combined by minimum. COMFORT_SETTINGS.md C01b forbids exactly that:
    /// "Apply the winning profile's gain/filter/pitch targets once relative to the
    /// user's configured baseline… a prior 12 dB rewind duck cannot make it 24 dB."
    /// So the request set is still tracked — a profile that is genuinely active
    /// stays active — but only the single highest-priority request is applied, and
    /// releasing it <b>re-selects</b> from the remaining requests rather than
    /// blindly restoring Normal.</para>
    ///
    /// <para>Two further C01b rules land here. The Pause profile's old
    /// <c>+2 dB</c> UI lift is gone: "UI/dialogue stays clear at the user's
    /// configured level, without automatic boost." And no profile may write to a
    /// bus in <see cref="AudioBuses.ProtectedBuses"/>, which is why the filter
    /// targets are the background World SFX children rather than their shared SFX
    /// parent.</para>
    ///
    /// <para>Local presentation state: derived from authoritative game state and
    /// applied outside gameplay snapshots and hashes. Plain C# class, not a Node —
    /// <see cref="AudioManager"/> owns it and pumps <see cref="AdvanceFades"/>,
    /// which keeps the tween deterministic and testable without engine frames.</para>
    /// </summary>
    public class AudioSnapshotMixer {
        /// <summary>Cutoff at which the authored low-pass filters are effectively transparent.</summary>
        public const float TransparentCutoffHz = 20500f;

        /// <summary>Default blend time for a profile selection, in seconds.</summary>
        public const float DefaultFadeSeconds = 0.25f;

        /// <summary>
        /// Buses the background layer is allowed to move. <see cref="AudioBuses.UI"/>
        /// is deliberately absent since C01b: nothing in the mix policy may raise or
        /// lower it, so UI and dialogue keep the player's configured level.
        /// </summary>
        public static readonly string[] MixedBuses = { AudioBuses.Music, AudioBuses.SFX };

        /// <summary>
        /// Buses whose base volume the mixer owns. Master and UI carry a base (their
        /// sliders) but no profile ever offsets them — ducking Master would duck the
        /// very warnings a profile is trying to keep legible.
        /// </summary>
        public static readonly string[] BasedBuses = {
            AudioBuses.Master, AudioBuses.Music, AudioBuses.SFX, AudioBuses.UI
        };

        private readonly struct SnapshotDefinition {
            public readonly float MusicDb;
            public readonly float SfxDb;
            public readonly float MusicCutoffHz;
            public readonly float SfxCutoffHz;
            public readonly float FadeSeconds;

            public SnapshotDefinition(
                float musicDb, float sfxDb,
                float musicCutoffHz, float sfxCutoffHz, float fadeSeconds) {
                MusicDb = musicDb;
                SfxDb = sfxDb;
                MusicCutoffHz = musicCutoffHz;
                SfxCutoffHz = sfxCutoffHz;
                FadeSeconds = fadeSeconds;
            }
        }

        // Values follow design-godot.md's snapshot table. GamePaused muffles the
        // background (and no longer lifts UI); LowHealth rolls off the high end;
        // UltimateCinematic ducks 12 dB exactly once; Rewind keeps the -12 dB the
        // authored rewind payload already carried.
        //
        // The five C01b rows ship with NO authored background treatment. C01b:
        // "A priority row with no authored background treatment adds none." They
        // are still real ladder entries — winning the ladder is how Time Freeze
        // suppresses a low-health heartbeat — they simply select a transparent
        // target until content authors one.
        private static readonly Dictionary<AudioSnapshot, SnapshotDefinition> Definitions = new() {
            [AudioSnapshot.Pause] = new SnapshotDefinition(-8f, -12f, 900f, 1200f, 0.20f),
            [AudioSnapshot.LowHealth] = new SnapshotDefinition(-2f, 0f, 3200f, TransparentCutoffHz, 0.60f),
            [AudioSnapshot.Ultimate] =
                new SnapshotDefinition(-12f, -12f, TransparentCutoffHz, TransparentCutoffHz, 0.15f),
            [AudioSnapshot.Rewind] = new SnapshotDefinition(-12f, 0f, 1800f, TransparentCutoffHz, 0.30f),
            [AudioSnapshot.AnchorSnap] = Silent(0.30f),
            [AudioSnapshot.DefyOrScripted] = Silent(0.20f),
            [AudioSnapshot.TimeFreeze] = Silent(0.25f),
            [AudioSnapshot.LowIntegrityTremor] = Silent(0.50f),
            [AudioSnapshot.Underwater] = Silent(0.40f)
        };

        private static SnapshotDefinition Silent(float fadeSeconds) =>
            new(0f, 0f, TransparentCutoffHz, TransparentCutoffHz, fadeSeconds);

        /// <summary>
        /// C01b's ladder, highest first. Pause outranks everything because a paused
        /// game must not keep a combat mix under the menu; NormalGameplay is the
        /// absence of a request and scores below every row.
        /// </summary>
        public static int PriorityOf(AudioSnapshot snapshot) => snapshot switch {
            AudioSnapshot.Pause => 9,
            AudioSnapshot.Rewind => 8,
            AudioSnapshot.AnchorSnap => 8,
            AudioSnapshot.Ultimate => 7,
            AudioSnapshot.DefyOrScripted => 6,
            AudioSnapshot.TimeFreeze => 5,
            AudioSnapshot.LowIntegrityTremor => 4,
            AudioSnapshot.LowHealth => 3,
            AudioSnapshot.Underwater => 2,
            _ => 1
        };

        /// <summary>Active requests and their (possibly overridden) music offset.</summary>
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
        private bool _hasSelection;
        private AudioSnapshot _selected;

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

        /// <summary>True when this profile is one of the active <em>requests</em>.</summary>
        public bool IsSnapshotActive(AudioSnapshot snapshot) => _active.ContainsKey(snapshot);

        /// <summary>
        /// How many profiles are currently requested. Not how many are applied —
        /// since C01b that is always exactly 0 or 1.
        /// </summary>
        public int ActiveSnapshotCount => _active.Count;

        /// <summary>True when some profile currently wins the ladder.</summary>
        public bool HasSelection => _hasSelection;

        /// <summary>
        /// The one profile whose targets are applied. Meaningless while
        /// <see cref="HasSelection"/> is false.
        /// </summary>
        public AudioSnapshot SelectedSnapshot => _selected;

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
        /// value restored at boot. The background offset rides on top of it and
        /// survives the change.
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
        /// Records a profile request with an explicit music offset, for callers that
        /// carry an authored duck depth (the rewind payload's
        /// <c>MusicDuckDecibels</c>). Re-requesting an already-active profile just
        /// updates its offset — it never stacks.
        /// </summary>
        public void ApplySnapshot(AudioSnapshot snapshot, float musicOffsetDb) {
            _active[snapshot] = musicOffsetDb;
            Reselect(Definitions[snapshot].FadeSeconds);
        }

        /// <summary>
        /// Drops a request and re-selects from what is left. C01b: "Re-evaluate
        /// current active requests when a profile ends; do not blindly restore
        /// Normal or a saved stale low-health state."
        /// </summary>
        public void ReleaseSnapshot(AudioSnapshot snapshot) {
            if (!_active.Remove(snapshot)) return;
            Reselect(Definitions[snapshot].FadeSeconds);
        }

        public void ReleaseAllSnapshots() {
            if (_active.Count == 0) return;
            _active.Clear();
            Reselect(DefaultFadeSeconds);
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

        /// <summary>Advances the in-flight blend. Driven by <see cref="AudioManager"/>.</summary>
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

        /// <summary>
        /// Resolves the single winning request and blends toward <em>its</em>
        /// targets. Nothing sums. An equal-priority overlap is broken by the
        /// profile's own stable enum ordinal rather than by callback order, which is
        /// C01b's "resolve an equal-priority overlap by stable presentation identity".
        /// </summary>
        private void Reselect(float fadeSeconds) {
            _hasSelection = false;
            int bestPriority = int.MinValue;
            float music = 0f;
            float sfx = 0f;
            float musicCutoff = TransparentCutoffHz;
            float sfxCutoff = TransparentCutoffHz;

            foreach (KeyValuePair<AudioSnapshot, float> entry in _active) {
                int priority = PriorityOf(entry.Key);
                if (_hasSelection
                    && (priority < bestPriority
                        || (priority == bestPriority && (int)entry.Key >= (int)_selected))) {
                    continue;
                }
                SnapshotDefinition definition = Definitions[entry.Key];
                _hasSelection = true;
                _selected = entry.Key;
                bestPriority = priority;
                music = entry.Value;
                sfx = definition.SfxDb;
                musicCutoff = definition.MusicCutoffHz;
                sfxCutoff = definition.SfxCutoffHz;
            }

            _fadeFromDb[AudioBuses.Music] = _currentOffsetDb[AudioBuses.Music];
            _fadeFromDb[AudioBuses.SFX] = _currentOffsetDb[AudioBuses.SFX];
            _targetOffsetDb[AudioBuses.Music] = music;
            _targetOffsetDb[AudioBuses.SFX] = sfx;

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
            // C01b: background World SFX only. CriticalCues is a sibling under the
            // same SFX parent precisely so this loop cannot reach it.
            foreach (string bus in AudioBuses.BackgroundSfxBuses) WriteCutoff(bus, _currentSfxCutoff);
        }

        /// <summary>
        /// Pushes a cutoff into the bus's authored low-pass filter. Silently does
        /// nothing when the layout has no filter on that bus, so a stripped-down
        /// layout degrades to volume-only profiles instead of throwing. Refuses
        /// outright to touch a protected bus.
        /// </summary>
        private static void WriteCutoff(string busName, float cutoffHz) {
            foreach (string protectedBus in AudioBuses.ProtectedBuses) {
                if (protectedBus == busName) return;
            }
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
