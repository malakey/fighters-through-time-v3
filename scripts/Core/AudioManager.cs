using System.Collections.Generic;
using FTT.Environment;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// The audio framework: bus routing, the vertical-layer music director, the
    /// named snapshot/duck layer, and a pooled one-shot voice allocator.
    ///
    /// <para><b>Buses</b> come from the authored
    /// <c>resources/Audio/default_bus_layout.tres</c> registered in
    /// <c>project.godot</c>; <see cref="AudioBuses"/> only backstops a missing one.
    /// The four settings sliders still address Master/Music/SFX/UI by those names,
    /// but their values now go in as <em>base</em> volumes underneath the snapshot
    /// offsets (see <see cref="AudioSnapshotMixer"/>) instead of being written
    /// straight to the bus.</para>
    ///
    /// <para><b>Saved volumes apply at boot.</b> Previously they only took effect the
    /// first time the settings menu was opened, so a player who turned the music down
    /// and relaunched got full-volume music until they went looking for the slider.
    /// The autoload order (SaveManager before AudioManager) already made the boot
    /// read legal; <see cref="ApplySavedVolumes"/> does it.</para>
    ///
    /// <para><b>Everything blends on one pump.</b> <see cref="_Process"/> advances the
    /// stem crossfade and the snapshot tween together via
    /// <see cref="AdvanceFades"/>; tests call that directly to step time.</para>
    ///
    /// <para>Playback entry points (<see cref="PlayChirp"/>,
    /// <see cref="PlayFootstep"/>, <see cref="PlayUISound"/>, and the
    /// <c>OnEnemyPresentation</c> binder) are deliberately thin and data-driven:
    /// Package 8 B3/B5/B6 register real streams against them without changing this
    /// file.</para>
    /// </summary>
    public partial class AudioManager : Node {
        public static AudioManager Instance { get; private set; }

        // === Placeholder cue paths ===
        // The three .ogg assets sit at the exact paths their existing call sites
        // already reference, so those cues stop no-oping without touching the
        // callers. Production audio replaces the files in place.
        public const string KnockoutStingerPath = "res://audio/sfx/combat/ko_stinger.ogg";
        public const string VictoryFanfarePath = "res://audio/sfx/ui/victory_fanfare.ogg";
        public const string CountdownBlipPath = "res://audio/sfx/ui/countdown_blip.ogg";

        private const string ChirpStreamPath = "res://resources/Audio/placeholder_sfx_dialogue_chirp.tres";
        private const string FootstepStreamPath = "res://resources/Audio/placeholder_sfx_footstep.tres";
        private const string UiStreamPath = "res://resources/Audio/placeholder_sfx_ui.tres";
        private const string HitStreamPath = "res://resources/Audio/placeholder_sfx_hit.tres";
        private const string HazardStreamPath = "res://resources/Audio/placeholder_sfx_hazard.tres";

        /// <summary>Simultaneous one-shot voices. The 25th steals the oldest.</summary>
        public const int SfxPoolCapacity = 24;

        private readonly Queue<AudioStreamPlayer> _inactiveSfx = new();
        private readonly List<AudioStreamPlayer> _activeSfxOrder = new();
        private readonly HashSet<AudioStreamPlayer> _activeSfx = new();

        /// <summary>Per-key overrides for enemy/boss presentation cues, keyed as documented on
        /// <see cref="ResolvePresentationCue"/>. Package 8 B6 populates it.</summary>
        private readonly Dictionary<string, AudioStream> _presentationSounds = new();

        /// <summary>Per-surface footstep overrides. Package 8 B5 populates it.</summary>
        private readonly Dictionary<string, AudioStream> _footstepSounds = new();

        private AudioSnapshotMixer _mixer;
        private AudioStreamPlayer _musicPlayer;

        private AudioStream _chirpStream;
        private AudioStream _footstepStream;
        private AudioStream _uiStream;
        private AudioStream _hitStream;
        private AudioStream _hazardStream;
        private AudioStream _countdownStream;
        private Tween _musicFade;

        /// <summary>The vertical-layer music director. Registered per scene.</summary>
        public StemDirector Stems { get; private set; }

        public override void _Ready() {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;

            AudioBuses.EnsureBuses();
            _mixer = new AudioSnapshotMixer();

            _musicPlayer = new AudioStreamPlayer { Name = "MusicPlayer", Bus = AudioBuses.Music };
            AddChild(_musicPlayer);

            Stems = new StemDirector { Name = "StemDirector" };
            AddChild(Stems);

            BuildVoicePool();
            LoadPlaceholderCues();
            ApplySavedVolumes();

            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyPresentation += OnEnemyPresentation;
                EventBus.Instance.OnPlayerHPChanged += OnPlayerHPChangedForSnapshot;
                EventBus.Instance.OnUltimateActivation += OnUltimateActivatedForSnapshot;
            }
        }

        public override void _ExitTree() {
            if (EventBus.Instance != null) {
                EventBus.Instance.OnEnemyPresentation -= OnEnemyPresentation;
                EventBus.Instance.OnPlayerHPChanged -= OnPlayerHPChangedForSnapshot;
                EventBus.Instance.OnUltimateActivation -= OnUltimateActivatedForSnapshot;
            }
        }

        public override void _Process(double delta) {
            // GamePaused snapshot (audit H-9): this autoload runs at
            // ProcessMode.Always, so it observes SceneTree.Paused transitions from
            // both sides of a pause without PauseMenuBase having to know audio
            // exists. Any tree pause ducks — the pause menus and a
            // PausesGameplay dialogue sequence alike — and the release follows the
            // pause owner's own teardown contract.
            ObservePauseState(GetTree()?.Paused ?? false);
            AdvanceFades(delta);
        }

        /// <summary>
        /// Single pump point for every in-flight audio blend. Tests step it directly
        /// instead of waiting on engine frames. Also retires the timed
        /// UltimateCinematic window so the duck cannot outlive its cinematic.
        /// </summary>
        public void AdvanceFades(double delta) {
            Stems?.AdvanceFades(delta);
            _mixer?.AdvanceFades(delta);
            if (_ultimateWindowRemaining > 0.0) {
                _ultimateWindowRemaining -= delta;
                if (_ultimateWindowRemaining <= 0.0) {
                    _ultimateWindowRemaining = 0.0;
                    ReleaseSnapshot(AudioSnapshot.Ultimate);
                }
            }
        }

        // === Snapshot triggers (audit H-9) ===
        // The mixer machinery predates these; H-9 was the finding that three of its
        // four designed triggers were never wired. Pause is observed in _Process
        // above; LowHealth and Ultimate are driven here for Story (EventBus) and by
        // FighterSimulationDriver for Fighter (sim HP fraction / meter edge).

        /// <summary>Fraction of maximum HP below which the LowHealth snapshot engages
        /// (design-godot.md:2812 "low-health state (&lt;20% HP)"). The Fighter side
        /// shares the integer form in <see cref="FighterAudioRules.LowHealthClimaxPercent"/>.</summary>
        public const float LowHealthFraction = FighterAudioRules.LowHealthClimaxPercent / 100f;

        /// <summary>Seconds the UltimateCinematic duck holds after an activation.
        /// Sized to cover the longest authored ultimate window (Einstein's 90-frame
        /// Cosmological Constant) plus its impact beat.</summary>
        public const float UltimateSnapshotSeconds = 2.0f;

        private bool _pauseSnapshotHeld;
        private bool _lowHealthHeld;
        private double _ultimateWindowRemaining;

        /// <summary>
        /// Applies or releases the GamePaused snapshot on a pause-state edge.
        /// Public so tests can drive the transition without pausing the real tree
        /// (a leaked tree pause hangs the whole GdUnit session).
        /// </summary>
        public void ObservePauseState(bool paused) {
            if (_pauseSnapshotHeld == paused) return;
            _pauseSnapshotHeld = paused;
            if (paused) ApplySnapshot(AudioSnapshot.Pause);
            else ReleaseSnapshot(AudioSnapshot.Pause);
        }

        /// <summary>True below the design's 20% threshold. Zero HP counts as low —
        /// the tension holds until a rewind/respawn restore raises HP again.</summary>
        public static bool IsLowHealthState(float currentHP, float maxHP) =>
            maxHP > 0f && currentHP < maxHP * LowHealthFraction;

        /// <summary>
        /// Level-triggered LowHealth snapshot state. Story feeds it from HP events;
        /// the Fighter driver feeds it from the deterministic HP fraction each
        /// frame. Idempotent, so callers just report the state they see.
        /// </summary>
        public void SetLowHealth(bool active) {
            if (_lowHealthHeld == active) return;
            _lowHealthHeld = active;
            if (active) ApplySnapshot(AudioSnapshot.LowHealth);
            else ReleaseSnapshot(AudioSnapshot.LowHealth);
        }

        /// <summary>
        /// Opens (or re-arms) the timed UltimateCinematic duck. Re-activation
        /// extends rather than stacks: the snapshot is applied idempotently and the
        /// window keeps the later deadline.
        /// </summary>
        public void BeginUltimateWindow(float seconds = UltimateSnapshotSeconds) {
            _ultimateWindowRemaining = System.Math.Max(_ultimateWindowRemaining, seconds);
            ApplySnapshot(AudioSnapshot.Ultimate);
        }

        /// <summary>
        /// Clears the transient gameplay snapshots on a scene transition. The scene
        /// being left is what made the player low or cinematic; the next scene
        /// starts from its own truth. Called by <c>GameManager.LoadScene</c>.
        /// </summary>
        public void OnSceneTransitionStarted() {
            SetLowHealth(false);
            _ultimateWindowRemaining = 0.0;
            ReleaseSnapshot(AudioSnapshot.Ultimate);
        }

        private void OnPlayerHPChangedForSnapshot(PlayerHPPayload payload) {
            // Story's hero slot only. The Mirror Paradox clone reports as player 1
            // and must not muffle the mix, and in Fighter Mode the presentation
            // bodies are process-disabled so no HP events fire at all — the driver
            // calls SetLowHealth directly from simulation state.
            if (payload.PlayerIndex != 0) return;
            SetLowHealth(IsLowHealthState(payload.CurrentHP, payload.MaxHP));
        }

        private void OnUltimateActivatedForSnapshot(UltimateActivationPayload payload) =>
            BeginUltimateWindow();

        // === Volume (settings sliders + boot restore) ===

        /// <summary>
        /// Pushes the saved global volumes onto the buses at boot. Null-safe so the
        /// test host, which may run without a populated save, gets the defaults.
        /// </summary>
        public void ApplySavedVolumes() {
            GlobalSaveData data = SaveManager.Instance?.GlobalData;
            SetMasterVolume(data?.MasterVolume ?? 1.0f);
            SetMusicVolume(data?.MusicVolume ?? 0.8f);
            SetSFXVolume(data?.SFXVolume ?? 1.0f);
            SetUIVolume(data?.UIVolume ?? 1.0f);
        }

        public void SetMasterVolume(float linear) => SetBusVolume(AudioBuses.Master, linear);

        public void SetMusicVolume(float linear) => SetBusVolume(AudioBuses.Music, linear);

        public void SetSFXVolume(float linear) => SetBusVolume(AudioBuses.SFX, linear);

        public void SetUIVolume(float linear) => SetBusVolume(AudioBuses.UI, linear);

        /// <summary>The base (pre-snapshot) dB currently set for a bus.</summary>
        public float GetBusBaseVolumeDb(string busName) => _mixer?.GetBaseDb(busName) ?? 0f;

        private void SetBusVolume(string busName, float linear) {
            float clamped = Mathf.Clamp(linear, 0f, 1f);
            float decibels = clamped <= 0f ? StemDirector.SilentDb : Mathf.LinearToDb(clamped);
            _mixer?.SetBaseVolume(busName, decibels);
        }

        // === Snapshots ===

        public void ApplySnapshot(AudioSnapshot snapshot) => _mixer?.ApplySnapshot(snapshot);

        /// <summary>Applies a snapshot with a caller-supplied music duck depth.</summary>
        public void ApplySnapshot(AudioSnapshot snapshot, float musicOffsetDb) =>
            _mixer?.ApplySnapshot(snapshot, musicOffsetDb);

        public void ReleaseSnapshot(AudioSnapshot snapshot) => _mixer?.ReleaseSnapshot(snapshot);

        public void ReleaseAllSnapshots() => _mixer?.ReleaseAllSnapshots();

        public bool IsSnapshotActive(AudioSnapshot snapshot) => _mixer?.IsSnapshotActive(snapshot) ?? false;

        /// <summary>The snapshot mixer, for callers that need offsets or to settle a blend.</summary>
        public AudioSnapshotMixer Snapshots => _mixer;

        // === Stem director passthrough (the API Package 8 B5 wires scenes to) ===

        public void RegisterStageAudio(StageAudioSet set) => Stems?.RegisterStageAudio(set);

        public void SetIntensity(StemIntensity intensity) => Stems?.SetIntensity(intensity);

        public void ReleaseStageAudio() => Stems?.ReleaseStageAudio();

        // === Music (single-track playback, outside the stem director) ===

        /// <summary>
        /// Plays a single non-layered track — menus, credits, anything without a
        /// <see cref="StageAudioSet"/>. <paramref name="fadeDuration"/> now actually
        /// fades: the music bus offset is walked down and back up through the
        /// snapshot layer rather than being ignored as it was before.
        /// </summary>
        public void PlayMusic(AudioStream stream, float fadeDuration = 1.0f) {
            if (_musicPlayer == null) return;
            KillMusicFade();
            _musicPlayer.Stream = stream;
            if (stream == null) {
                _musicPlayer.Stop();
                return;
            }
            _musicPlayer.VolumeDb = fadeDuration > 0f ? StemDirector.SilentDb : 0f;
            _musicPlayer.Play();
            if (fadeDuration <= 0f) return;
            _musicFade = CreateTween();
            _musicFade.TweenProperty(_musicPlayer, "volume_db", 0f, fadeDuration);
        }

        public void StopMusic() {
            if (_musicPlayer == null) return;
            KillMusicFade();
            _musicPlayer.Stop();
            _musicPlayer.Stream = null;
            _musicPlayer.VolumeDb = 0f;
        }

        public bool IsMusicPlaying => _musicPlayer?.Playing ?? false;

        /// <summary>Volume of the single-track music player, mid-fade.</summary>
        public float MusicPlayerVolumeDb => _musicPlayer?.VolumeDb ?? StemDirector.SilentDb;

        private void KillMusicFade() {
            if (_musicFade == null) return;
            if (_musicFade.IsValid()) _musicFade.Kill();
            _musicFade = null;
        }

        // === One-shot playback ===

        /// <summary>
        /// Plays a one-shot on the SFX bus. Signature preserved for existing callers;
        /// <paramref name="position"/> remains unused until positional audio lands
        /// with production content.
        /// </summary>
        public void PlaySFX(AudioStream stream, Vector2 position = default) =>
            PlayOneShot(stream, AudioBuses.SFX, 1f, 0f);

        /// <summary>Menu clicks and other UI feedback. Routed to the UI bus.</summary>
        public void PlayUISound(AudioStream stream = null, float pitchScale = 1f) =>
            PlayOneShot(stream ?? _uiStream, AudioBuses.UI, pitchScale, 0f);

        /// <summary>
        /// One dialogue typewriter chirp. <paramref name="pitchScale"/> carries the
        /// speaker's character identity (lower for Lincoln, higher for Einstein/Tesla
        /// per the design); Package 8 B3 supplies it per line.
        /// </summary>
        public void PlayChirp(float pitchScale = 1f) =>
            PlayOneShot(_chirpStream, AudioBuses.UI, pitchScale, -6f);

        /// <summary>
        /// One footstep for the given surface. Unknown surfaces fall back to the
        /// generic placeholder, so a level that has not tagged its geometry yet is
        /// merely undifferentiated rather than silent.
        /// </summary>
        public void PlayFootstep(string surfaceId) {
            AudioStream stream = _footstepStream;
            if (!string.IsNullOrEmpty(surfaceId) && _footstepSounds.TryGetValue(surfaceId, out AudioStream registered)) {
                stream = registered;
            }
            PlayOneShot(stream, AudioBuses.Movement, SurfacePitch(surfaceId), -3f);
        }

        /// <summary>One tick of the Fighter pre-match countdown; GO is pitched up.</summary>
        public void PlayCountdownBlip(float pitchScale = 1f) =>
            PlayOneShot(_countdownStream, AudioBuses.UI, pitchScale, 0f);

        /// <summary>
        /// Placeholder surface identities get distinct pitches so a playtester can
        /// hear that the surface plumbing works before the real footstep set exists.
        /// Package 8 B5 registers real streams and these become 1.0.
        /// </summary>
        public static float SurfacePitch(string surfaceId) => surfaceId switch {
            "stone" or "marble" => 1.15f,
            "metal" => 1.30f,
            "sand" or "dirt" => 0.85f,
            "snow" => 0.75f,
            _ => 1.0f
        };

        public void RegisterFootstepSound(string surfaceId, AudioStream stream) {
            if (string.IsNullOrEmpty(surfaceId) || stream == null) return;
            _footstepSounds[surfaceId] = stream;
        }

        // === Enemy/boss presentation binding ===

        /// <summary>
        /// Registers a concrete sound for a presentation key. Keys are either a bare
        /// authored <c>PresentationEventID</c> (matches every phase) or one suffixed
        /// with <c>.telegraph</c>/<c>.active</c>/<c>.recovery</c>/<c>.death</c>
        /// (matches that phase only). Package 8 B6 owns the content.
        /// </summary>
        public void RegisterPresentationSound(string key, AudioStream stream) {
            if (string.IsNullOrEmpty(key) || stream == null) return;
            _presentationSounds[key] = stream;
        }

        public void ClearPresentationSounds() => _presentationSounds.Clear();

        /// <summary>The suffix convention the presentation keys use.</summary>
        public static string PhaseSuffix(EnemyPresentationPhase phase) => phase switch {
            EnemyPresentationPhase.Telegraph => ".telegraph",
            EnemyPresentationPhase.Active => ".active",
            EnemyPresentationPhase.Recovery => ".recovery",
            _ => ".death"
        };

        /// <summary>
        /// Telegraph and recovery are world/anticipation beats and sit on the
        /// Environmental sub-bus; the hit itself and the death land on Combat. That
        /// split is what makes the design's "level-specific ducking" possible later.
        /// </summary>
        public static string PhaseBus(EnemyPresentationPhase phase) =>
            phase == EnemyPresentationPhase.Active || phase == EnemyPresentationPhase.Death
                ? AudioBuses.Combat
                : AudioBuses.Environmental;

        /// <summary>
        /// Resolves the cue for a presentation event. Lookup order: the phase-suffixed
        /// key, then the bare event ID, then a generic per-phase placeholder.
        /// Recovery has no generic sound — a beat every enemy fires several times a
        /// second would be noise, so only an explicit registration plays one.
        /// Returns false when nothing should sound.
        /// </summary>
        public bool ResolvePresentationCue(
            EnemyPresentationPayload payload, out AudioStream stream, out string busName, out float pitchScale) {
            busName = PhaseBus(payload.Phase);
            pitchScale = payload.Phase == EnemyPresentationPhase.Death ? 0.7f : 1f;
            stream = null;

            string eventID = payload.PresentationEventID ?? "";
            string suffix = PhaseSuffix(payload.Phase);
            string suffixed = eventID.EndsWith(suffix) ? eventID : eventID + suffix;

            if (eventID.Length > 0) {
                if (_presentationSounds.TryGetValue(suffixed, out stream)) return stream != null;
                if (_presentationSounds.TryGetValue(eventID, out stream)) return stream != null;
            }

            stream = payload.Phase switch {
                EnemyPresentationPhase.Telegraph => _hazardStream,
                EnemyPresentationPhase.Active => _hitStream,
                EnemyPresentationPhase.Death => _hitStream,
                _ => null
            };
            return stream != null;
        }

        private void OnEnemyPresentation(EnemyPresentationPayload payload) {
            if (!ResolvePresentationCue(payload, out AudioStream stream, out string busName, out float pitchScale)) return;
            PlayOneShot(stream, busName, pitchScale, 0f);
        }

        // === Voice pool ===

        /// <summary>Voices currently sounding.</summary>
        public int ActiveVoiceCount => _activeSfxOrder.Count;

        /// <summary>Voices available without stealing.</summary>
        public int IdleVoiceCount => _inactiveSfx.Count;

        /// <summary>The voice that a further allocation would steal, or null while any voice is idle.</summary>
        public AudioStreamPlayer OldestActiveVoice =>
            _inactiveSfx.Count > 0 || _activeSfxOrder.Count == 0 ? null : _activeSfxOrder[0];

        /// <summary>The most recently allocated voice, or null when none are sounding.</summary>
        public AudioStreamPlayer NewestActiveVoice =>
            _activeSfxOrder.Count == 0 ? null : _activeSfxOrder[_activeSfxOrder.Count - 1];

        /// <summary>
        /// Stops every sounding one-shot and returns it to the pool. Used on scene
        /// changes, where a half-played hit from the level you just left is worse
        /// than silence.
        /// </summary>
        public void ReleaseAllVoices() {
            while (_activeSfxOrder.Count > 0) {
                AudioStreamPlayer player = _activeSfxOrder[_activeSfxOrder.Count - 1];
                _activeSfxOrder.RemoveAt(_activeSfxOrder.Count - 1);
                _activeSfx.Remove(player);
                if (player == null) continue;
                player.Stop();
                player.Stream = null;
                player.Bus = AudioBuses.SFX;
                _inactiveSfx.Enqueue(player);
            }
        }

        /// <summary>
        /// Allocates a voice and plays a one-shot on the requested bus. When every
        /// voice is busy the oldest is stopped and reused: with a hard cap, dropping
        /// the newest sound would mute exactly the hit the player just landed.
        /// </summary>
        public void PlayOneShot(AudioStream stream, string busName, float pitchScale = 1f, float volumeDb = 0f) {
            if (stream == null) return;
            AudioStreamPlayer player;
            if (_inactiveSfx.Count > 0) {
                player = _inactiveSfx.Dequeue();
            } else {
                if (_activeSfxOrder.Count == 0) return;
                player = _activeSfxOrder[0];
                _activeSfxOrder.RemoveAt(0);
                _activeSfx.Remove(player);
                player.Stop();
            }
            player.Stream = stream;
            player.Bus = busName;
            player.VolumeDb = volumeDb;
            player.PitchScale = Mathf.Clamp(pitchScale, 0.25f, 4f);
            _activeSfx.Add(player);
            _activeSfxOrder.Add(player);
            player.Play();
        }

        private void BuildVoicePool() {
            for (int index = 0; index < SfxPoolCapacity; index++) {
                var player = new AudioStreamPlayer { Name = $"PooledSFX_{index}", Bus = AudioBuses.SFX };
                AudioStreamPlayer captured = player;
                player.Finished += () => ReleaseSfx(captured);
                AddChild(player);
                _inactiveSfx.Enqueue(player);
            }
        }

        private void ReleaseSfx(AudioStreamPlayer player) {
            if (player == null || !_activeSfx.Remove(player)) return;
            _activeSfxOrder.Remove(player);
            player.Stop();
            player.Stream = null;
            player.Bus = AudioBuses.SFX;
            _inactiveSfx.Enqueue(player);
        }

        // === Placeholder cue loading ===

        private void LoadPlaceholderCues() {
            _chirpStream = LoadCue(ChirpStreamPath);
            _footstepStream = LoadCue(FootstepStreamPath);
            _uiStream = LoadCue(UiStreamPath);
            _hitStream = LoadCue(HitStreamPath);
            _hazardStream = LoadCue(HazardStreamPath);
            _countdownStream = LoadCue(CountdownBlipPath);
        }

        /// <summary>
        /// Loads a cue through <see cref="ResourceLoader"/>, not
        /// <c>AuthoredResources</c>: audio streams are streamed content and must stay
        /// out of the authored-data cache. The reference is held for the process
        /// lifetime, so there is no reload churn.
        /// </summary>
        private static AudioStream LoadCue(string path) =>
            ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
    }
}
