using System.Collections.Generic;
using Godot;

namespace FTT.Core {

    /// <summary>
    /// H04 (Package 12 W10): plays the GDD's core-mechanic cues from the
    /// <see cref="CoreMechanicCueCatalog"/>. A child of <see cref="AudioManager"/>.
    ///
    /// <para><b>Story</b> cues come from existing <see cref="EventBus"/> events —
    /// Time Freeze state, the Defy seal, the Ultimate meter, the Rally echo, the
    /// Collapse Tremor level and a Story death. Nothing publishes on its behalf and
    /// no gameplay file was edited for it. <b>Fighter</b> cues are raised by
    /// <c>FighterSimulationDriver</c> from its own read-only presentation pass
    /// through <see cref="Play"/> / <see cref="PlayStageHazard"/>, so nothing here
    /// ever enters the deterministic simulation.</para>
    ///
    /// <para>Routing is the catalog's: a Critical row always goes through
    /// <see cref="AudioManager.PlayCriticalCue"/> (the C01b clear path that the
    /// W6 category mutes never mute or reroute); any other row plays on its
    /// authored bus. Every stream is placeholder silence.</para>
    /// </summary>
    public partial class CoreMechanicCuePresenter : Node {
        public const string NodeName = "CoreMechanicCues";

        /// <summary>The thaw warning's three ticks span the final 0.5 s of a freeze.</summary>
        public const float ThawWarningLeadSeconds = 0.5f;

        /// <summary>Integrity tick cadence at Tremor level 1 (&lt; 20 %) and 2 (&lt; 10 %).</summary>
        public const float IntegrityTickSecondsLevelOne = 1.0f;
        public const float IntegrityTickSecondsLevelTwo = 0.5f;

        private AudioManager _audio;
        private CoreMechanicCueCatalog _catalog;
        private bool _bound;

        private TimeFreezeState _freezeState = TimeFreezeState.Ready;
        private bool _thawWarningArmed;
        private bool _thawWarningPlayed;
        private float _thawWarningCountdown;

        private int _tremorLevel;
        private float _integrityTickTimer;

        private readonly Dictionary<int, DefySealState> _defyStates = new();
        private readonly Dictionary<int, bool> _meterFull = new();

        /// <summary>The catalog this presenter plays from, or null when it failed to load.</summary>
        public CoreMechanicCueCatalog Catalog => _catalog;

        /// <summary>Most recently played cue ID. Test seam.</summary>
        public string LastCueID { get; private set; } = "";

        /// <summary>Bus the most recent cue was routed to. Test seam.</summary>
        public string LastRoutedBus { get; private set; } = "";

        /// <summary>Total cues routed since boot. Test seam.</summary>
        public int CuesPlayed { get; private set; }

        /// <summary>Current Integrity tick interval in seconds; 0 when the tick is off.</summary>
        public float IntegrityTickInterval => _tremorLevel switch {
            1 => IntegrityTickSecondsLevelOne,
            >= 2 => IntegrityTickSecondsLevelTwo,
            _ => 0f
        };

        public void Initialize(AudioManager audio) {
            _audio = audio;
            _catalog = CoreMechanicCueCatalog.LoadDefault();
            // W1's Post-Landing thaw warning plays ChronalRewindManager.ThawCueStream;
            // the catalog is where that stream lives now.
            CoreMechanicCueEntry thaw = _catalog?.Find(CoreMechanicCueIDs.PostLandingThawWarning);
            if (thaw?.Stream != null && FTT.Environment.ChronalRewindManager.ThawCueStream == null) {
                FTT.Environment.ChronalRewindManager.ThawCueStream = thaw.Stream;
            }
        }

        public override void _Ready() {
            // Pausable: a paused tree (pause menu) holds the thaw-warning and
            // Integrity-tick clocks, exactly as it holds the clocks they follow.
            ProcessMode = ProcessModeEnum.Pausable;
            Bind();
        }

        public override void _ExitTree() => Unbind();

        // === Routing ==========================================================

        /// <summary>
        /// Plays a catalog row. Returns false when the row is missing. A Critical
        /// row routes through <see cref="AudioManager.PlayCriticalCue"/>.
        /// </summary>
        public bool Play(string cueID, float pitchScale = 1f) {
            CoreMechanicCueEntry entry = _catalog?.Find(cueID);
            if (entry == null) return false;
            Route(entry.CueID, entry.Stream, entry.Critical, entry.Bus, pitchScale);
            return true;
        }

        /// <summary>
        /// The Fighter stage hazard's warning (<paramref name="warning"/>) or impact
        /// cue from the active <c>StageAudioSet</c>'s slot, falling back to the
        /// catalog row. Always the Critical path.
        /// </summary>
        public void PlayStageHazard(bool warning) {
            FTT.Environment.StageAudioSet set = _audio?.Stems?.ActiveSet;
            AudioStream slot = warning ? set?.HazardWarningCue : set?.HazardImpactCue;
            string fallbackID = warning
                ? CoreMechanicCueIDs.StageHazardWarningFallback
                : CoreMechanicCueIDs.StageHazardImpactFallback;
            if (slot != null) {
                Route(fallbackID, slot, critical: true, AudioBuses.CriticalCues, 1f);
                return;
            }
            Play(fallbackID);
        }

        /// <summary>The winner's era fanfare (or the shared one); the Draw sting for a tie.</summary>
        public void PlayResultFanfare(string winnerHeroID, bool isDraw) {
            if (isDraw) {
                Play(CoreMechanicCueIDs.DrawSting);
                return;
            }
            CoreMechanicCueEntry entry = _catalog?.VictoryFanfareFor(winnerHeroID);
            if (entry != null) Route(entry.CueID, entry.Stream, entry.Critical, entry.Bus, 1f);
        }

        private void Route(string cueID, AudioStream stream, bool critical, string bus, float pitchScale) {
            string resolved = critical ? AudioBuses.CriticalCues : (string.IsNullOrEmpty(bus) ? AudioBuses.SFX : bus);
            LastCueID = cueID;
            LastRoutedBus = resolved;
            CuesPlayed++;
            if (_audio == null) return;
            if (critical) _audio.PlayCriticalCue(stream, pitchScale);
            else _audio.PlayOneShot(stream, resolved, pitchScale);
        }

        /// <summary>Drops per-scene cue state on a scene change.</summary>
        public void ResetTransient() {
            _freezeState = TimeFreezeState.Ready;
            _thawWarningArmed = false;
            _thawWarningPlayed = false;
            _tremorLevel = 0;
            _integrityTickTimer = 0f;
            _defyStates.Clear();
            _meterFull.Clear();
        }

        // === Story subscriptions =============================================

        private void Bind() {
            if (_bound || EventBus.Instance == null) return;
            EventBus bus = EventBus.Instance;
            _bound = true;
            bus.OnTimeFreezeStateChanged += OnTimeFreezeStateChanged;
            bus.OnDefySealChanged += OnDefySealChanged;
            bus.OnUltimateMeterChanged += OnUltimateMeterChanged;
            bus.OnRallyEchoChanged += OnRallyEchoChanged;
            bus.OnCollapseTremorChanged += OnCollapseTremorChanged;
            bus.OnPlayerDied += OnPlayerDied;
        }

        private void Unbind() {
            if (!_bound || EventBus.Instance == null) return;
            EventBus bus = EventBus.Instance;
            _bound = false;
            bus.OnTimeFreezeStateChanged -= OnTimeFreezeStateChanged;
            bus.OnDefySealChanged -= OnDefySealChanged;
            bus.OnUltimateMeterChanged -= OnUltimateMeterChanged;
            bus.OnRallyEchoChanged -= OnRallyEchoChanged;
            bus.OnCollapseTremorChanged -= OnCollapseTremorChanged;
            bus.OnPlayerDied -= OnPlayerDied;
        }

        /// <summary>Test seam: drives the Time Freeze handler without a controller.</summary>
        public void OnTimeFreezeStateChanged(TimeFreezePayload payload) {
            TimeFreezeState previous = _freezeState;
            _freezeState = payload.State;
            if (payload.State == TimeFreezeState.Active && previous != TimeFreezeState.Active) {
                Play(CoreMechanicCueIDs.TimeFreezeActivate);
                Play(CoreMechanicCueIDs.TimeFreezeDrone);
                _thawWarningArmed = false;
                _thawWarningPlayed = false;
            } else if (payload.State != TimeFreezeState.Active && previous == TimeFreezeState.Active) {
                _thawWarningArmed = false;
                Play(CoreMechanicCueIDs.TimeFreezeThawRelease);
                return;
            }
            if (payload.State != TimeFreezeState.Active || _thawWarningPlayed) return;
            // The controller publishes on each whole-second change, so the last
            // published second arms a local countdown to the final 0.5 s.
            if (payload.SecondsRemaining <= 1.0f) {
                _thawWarningArmed = true;
                _thawWarningCountdown = Mathf.Max(0f, payload.SecondsRemaining - ThawWarningLeadSeconds);
                if (_thawWarningCountdown <= 0f) FireThawWarning();
            }
        }

        private void FireThawWarning() {
            _thawWarningArmed = false;
            _thawWarningPlayed = true;
            Play(CoreMechanicCueIDs.TimeFreezeThawWarning);
        }

        private void OnDefySealChanged(DefySealPayload payload) {
            _defyStates.TryGetValue(payload.PlayerIndex, out DefySealState previous);
            _defyStates[payload.PlayerIndex] = payload.State;
            if (payload.State == DefySealState.Spent && previous == DefySealState.Ready) {
                Play(CoreMechanicCueIDs.DefyToll);
                Play(CoreMechanicCueIDs.DefySealShatter);
            }
        }

        private void OnUltimateMeterChanged(UltimateMeterPayload payload) {
            _meterFull.TryGetValue(payload.PlayerIndex, out bool wasFull);
            _meterFull[payload.PlayerIndex] = payload.IsFull;
            if (payload.IsFull && !wasFull) Play(CoreMechanicCueIDs.UltimateReady);
        }

        private void OnRallyEchoChanged(RallyEchoPayload payload) {
            if (!payload.ReclaimFlash) return;
            Play(CoreMechanicCueIDs.RallyReclaim, RallyReclaimPitch(payload.PoolFraction));
        }

        /// <summary>Pitched upward as more of the pool returns (the pool left shrinks).</summary>
        public static float RallyReclaimPitch(float poolFractionLeft) =>
            1f + 0.5f * (1f - Mathf.Clamp(poolFractionLeft, 0f, 1f));

        private void OnCollapseTremorChanged(TremorPayload payload) {
            int previous = _tremorLevel;
            _tremorLevel = Mathf.Max(0, payload.Level);
            if (_tremorLevel == 0) {
                _integrityTickTimer = 0f;
            } else if (previous == 0) {
                _integrityTickTimer = IntegrityTickInterval;
            } else {
                _integrityTickTimer = Mathf.Min(_integrityTickTimer, IntegrityTickInterval);
            }
        }

        // A Story death is the hero's (slot 0). The Mirror Paradox clone reports as
        // slot 1 and is a boss defeat, not a KO; Fighter presentation bodies never
        // raise this event (the driver plays its own stinger from sim state).
        private void OnPlayerDied(int playerIndex) {
            if (playerIndex == 0) Play(CoreMechanicCueIDs.KoStinger);
        }

        /// <summary>Advances the thaw-warning and Integrity-tick clocks. Public for headless tests.</summary>
        public void Advance(float seconds) {
            if (_thawWarningArmed && _freezeState == TimeFreezeState.Active) {
                _thawWarningCountdown -= seconds;
                if (_thawWarningCountdown <= 0f) FireThawWarning();
            }
            float interval = IntegrityTickInterval;
            if (interval > 0f) {
                _integrityTickTimer -= seconds;
                if (_integrityTickTimer <= 0f) {
                    Play(CoreMechanicCueIDs.IntegrityTick);
                    _integrityTickTimer += interval;
                    if (_integrityTickTimer <= 0f) _integrityTickTimer = interval;
                }
            }
        }

        public override void _Process(double delta) => Advance((float)delta);
    }
}
