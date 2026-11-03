using Godot;
using FTT.Core;
using FTT.UI.Motion;
using FTT.UI.Pulp;

namespace FTT.UI {

    /// <summary>
    /// Reusable Story Mode HUD, rewritten to the HUD_CONTRACT hierarchy by
    /// Package 11 A8 (F24): a safe area, a top-left vitals panel and a top-right
    /// currency/Integrity panel.
    ///
    /// <para>What it shows: portrait, HP with the Rally echo band, block charges,
    /// <b>both</b> status slots with authoritative duration radials, the Ultimate
    /// meter ring and its lock overlay with the Defy notch, the Momentum pips, the death-rewind
    /// counter, the F03 Time Freeze indicator, the Act III Beacon anchor pips, the
    /// three ability cooldown rings with their V7.5 lock overlays, the persistent
    /// Chronal Dust counter with its separately fading <c>+N</c> float, the
    /// Timeline Integrity era-clock, and the Collapse Tremor treatment — plus the
    /// repo's own level title, objective, boss bar with authored phase notches,
    /// boss intro card and checkpoint toast.</para>
    ///
    /// <para><b>It consumes; it does not poll gameplay.</b> Everything V7.5/V7.6
    /// adds arrives on the EventBus payloads plan §2.9 fixed, so the workstreams
    /// that own Integrity, Time Freeze, the unlock schedule, the Act III beacon and
    /// Defy never touch this file or its scene. Two exceptions are deliberate: the
    /// counters and the Rally band are continuous values with no natural event
    /// edge, and the status radials read the authoritative <c>StatusController</c>
    /// directly so a frozen status cannot tick down here while its real clock is
    /// stopped.</para>
    ///
    /// Presentation only. Nothing here writes gameplay state.
    /// </summary>
    public partial class StoryHUD : CanvasLayer {
        public const string SceneResourcePath = "res://scenes/ui/StoryHUD.tscn";

        /// <summary>Slots the cooldown ring row draws, left to right.</summary>
        private static readonly FTT.Core.AbilitySlot[] CooldownSlots = {
            FTT.Core.AbilitySlot.Special1,
            FTT.Core.AbilitySlot.Special2,
            FTT.Core.AbilitySlot.MovementAbility
        };

        /// <summary>
        /// The 18 px shield pips of the compact Story HUD (30 until 2026-10-27), 4 apart after the HP tube: <c>shield</c> lit,
        /// <c>shield_off</c> at .28 spent.
        /// </summary>
        public const int BlockPipSize = 18;

        /// <summary>A spent block pip's opacity (UI_SPEC_GLASS 3.4.4).</summary>
        public const float SpentPipAlpha = 0.28f;

        private static readonly StringName StoryPlayerGroup = "StoryPlayer";
        private static readonly NodePath StatusControllerPath = "StatusController";

        /// <summary>
        /// M15 (Package 12 W10): the localized accessible label for the Act III
        /// Beacon anchors, "Anchor charges: N".
        /// </summary>
        public const string AnchorChargesAccessibleKey = "hud_anchor_charges_accessible";

        /// <summary>HUD_CONTRACT: only the pooled <c>+N</c> notification fades, after one second.</summary>
        public const float DustFloatSeconds = 1.0f;

        /// <summary>Thaw warning window, in seconds before an active freeze ends.</summary>
        public const float ThawWarningSeconds = 1.0f;

        private Control _root;
        private TextureRect _portrait;
        private HBoxContainer _blockPipRow;
        private PulpIcon[] _blockPips = System.Array.Empty<PulpIcon>();
        private Control _topLeft;
        private Label _heroName;
        private Label _bossPhaseTab;
        private Label _bossHpValue;
        private Label _levelTitleLabel;
        private Label _objectiveLabel;
        private ProgressBar _hpBar;
        private ColorRect _echoBand;
        private FTT.Characters.PlayerController _echoPlayer;
        private Label _hpText;

        // Status slots (F24): fixed positions, damage left / control right.
        private Label _damageStatusIndicator;
        private RadialProgress _damageStatusRadial;
        private Label _controlStatusIndicator;
        private RadialProgress _controlStatusRadial;
        private FTT.Combat.StatusController _statusAuthority;

        // Meter ring and lock overlay (the Defy notch rides the ring). The BG is a plain Control that
        // draws nothing: it hosts the D10 "not yet" frame, while the fill (the
        // RadialProgress) takes the tint.
        private Control _meterHost;
        private RadialProgress _meterFill;
        private Label _meterLockOverlay;
        private float _meterValue;
        private readonly DefyBindingProbe _defyBinding = new();

        // Cooldown rings and their lock overlays.
        private readonly Label[] _cooldownIcons = new Label[CooldownSlots.Length];
        private readonly RadialProgress[] _cooldownRadials = new RadialProgress[CooldownSlots.Length];
        private readonly Label[] _cooldownLockOverlays = new Label[CooldownSlots.Length];

        // Temporal row.
        private Label _rewindIcon;
        private Label _rewindCountText;
        private Control _timeFreezeIndicator;
        private Label _freezeIcon;
        private Label _freezeRemainingText;
        private TimeFreezeState _timeFreezeState = TimeFreezeState.Ready;
        private float _timeFreezeSeconds;
        private Control _beaconAnchors;
        private BeaconAnchorIcon _beaconIcon;
        private HBoxContainer _anchorPipRow;
        private AnchorPip[] _anchorPips = System.Array.Empty<AnchorPip>();
        // The last authoritative charge count drawn, or -1 before the first bind.
        // A drop at unchanged capacity is an Anchor Snap and cracks the pip; any
        // other change (bind, load, restart refill) redraws without animation.
        private int _drawnAnchorCharges = -1;

        // Top-right panel.
        private Label _dustText;
        private Label _dustFloat;
        private float _dustFloatTimer;
        private Control _integrityClock;
        private RadialProgress _clockWedge;
        private Label _siphonStreams;
        private Label _integrityText;
        private IntegrityPayload _integrity = new() { Percent = 100f, Tier = IntegrityTier.Restored };
        private bool _integrityKnown;

        // Repo extras the contract does not enumerate.
        private Control _bossPanel;
        private Label _bossNameLabel;
        private ProgressBar _bossBar;
        private BossPhaseNotchOverlay _bossNotches;
        private Label _bossIntroCard;
        private float _bossIntroCardTimer;
        private Label _checkpointToast;
        private float _checkpointToastTimer;
        private Tween _bossPhaseFade;
        private ColorRect _tremorOverlay;
        private int _tremorLevel;

        private readonly HudAbilityIndicatorModel _indicators = new();
        private readonly HudOpacityBinder _opacity = new();
        private readonly UiScaleBinder _uiScale = new();

        private int _lastRewinds = int.MinValue;
        private int _lastDust = int.MinValue;

        /// <summary>The ability/status readiness model this HUD renders.</summary>
        public HudAbilityIndicatorModel Indicators => _indicators;

        /// <summary>The HudOpacity setting most recently applied to the HUD root.</summary>
        public float AppliedHudOpacity => _opacity.AppliedOpacity;

        /// <summary>The boss bar's phase-notch overlay, or null before it resolves.</summary>
        public BossPhaseNotchOverlay BossNotches => _bossNotches;

        /// <summary>Total authored shield pips (base capacity plus Resonance bonus). Test seam.</summary>
        public int BlockPipCapacity => _blockPips.Length;

        /// <summary>How many shield pips currently render lit. Test seam.</summary>
        public int LitBlockPips {
            get {
                int lit = 0;
                foreach (PulpIcon pip in _blockPips) {
                    if (pip.Modulate.A >= 1f) lit++;
                }
                return lit;
            }
        }

        /// <summary>The portrait widget, or null before the scene resolves. Test seam.</summary>
        public TextureRect Portrait => _portrait;


        /// <summary>The Time Freeze verb state currently drawn. Test seam.</summary>
        public TimeFreezeState TimeFreeze => _timeFreezeState;

        /// <summary>Act III anchor pips currently drawn. Test seam.</summary>
        public int AnchorPipCount => _anchorPips.Length;

        /// <summary>Lit Act III anchor pips. Test seam.</summary>
        public int LitAnchorPips {
            get {
                int lit = 0;
                foreach (AnchorPip pip in _anchorPips) {
                    if (pip.State == AnchorPip.PipState.Filled) lit++;
                }
                return lit;
            }
        }

        /// <summary>The Act III anchor pips, left to right. Test seam.</summary>
        public System.Collections.Generic.IReadOnlyList<AnchorPip> AnchorPips => _anchorPips;

        /// <summary>True while the Beacon icon shows the hollow zero-charge last stand. Test seam.</summary>
        public bool BeaconLastStand => _beaconIcon != null && _beaconIcon.LastStand;

        /// <summary>The localized accessible label currently on the Beacon readout. Test seam.</summary>
        public string AnchorChargesAccessibleText => _beaconAnchors?.AccessibilityName ?? string.Empty;

        /// <summary>Collapse Tremor presentation level currently drawn (0/1/2). Test seam.</summary>
        public int TremorLevel => _tremorLevel;

        /// <summary>
        /// Instantiates the authored StoryHUD scene. The scene is a committed,
        /// tested asset (Package 8 B1 removed the duplicated code-built fallback
        /// that used to shadow it — two layouts drifting apart silently was worse
        /// than one loud failure), so a missing scene is a packaging error: it is
        /// reported and a bare, harmless instance is returned rather than null,
        /// because every level controller calls into this unconditionally.
        /// </summary>
        public static StoryHUD CreateDefault() {
            if (ResourceLoader.Exists(SceneResourcePath)) {
                var packed = ResourceLoader.Load<PackedScene>(SceneResourcePath);
                if (packed?.Instantiate() is StoryHUD authored) return authored;
            }
            GD.PushError($"StoryHUD scene missing at {SceneResourcePath}; the HUD will render nothing.");
            return new StoryHUD { Name = "StoryHUD" };
        }

        public override void _Ready() {
            Layer = 10;
            ResolveUI();
            var bus = FTT.Core.EventBus.Instance;
            if (bus != null) {
                bus.OnPlayerHPChanged += OnPlayerHPChanged;
                bus.OnUltimateMeterChanged += OnUltimateMeterChanged;
                bus.OnCheckpointReached += OnCheckpointReached;
                bus.OnCooldownStarted += OnCooldownStarted;
                bus.OnCooldownComplete += OnCooldownComplete;
                bus.OnStatusEffectApplied += OnStatusEffectApplied;
                bus.OnStatusEffectCleared += OnStatusEffectCleared;
                bus.OnBossPhaseChanged += OnBossPhaseChanged;
                bus.OnBlockChargesChanged += OnBlockChargesChanged;
                // Package 11 §2.9: everything V7.5/V7.6 adds arrives here.
                bus.OnTimelineIntegrityChanged += OnTimelineIntegrityChanged;
                bus.OnCollapseTremorChanged += OnCollapseTremorChanged;
                bus.OnTimeFreezeStateChanged += OnTimeFreezeStateChanged;
                bus.OnAbilitySlotLockChanged += OnAbilitySlotLockChanged;
                bus.OnAbilityLockoutRefused += OnAbilityLockoutRefused;
                bus.OnAnchorChargesChanged += OnAnchorChargesChanged;
                bus.OnRallyEchoChanged += OnRallyEchoChanged;
                bus.OnDustAwardCollected += OnDustAwardCollected;
                bus.OnChronalDustCollected += OnChronalDustCollected;
            }
            InitializeCharacterPresentation();
            RefreshCounters(force: true);
            RefreshIndicators();
            RefreshTimeFreeze();
            RefreshIntegrity();
            _opacity.Apply(_root, force: true);
            _uiScale.Apply(_root, force: true);
            ApplyFit(force: true);
        }

        public override void _ExitTree() {
            var bus = FTT.Core.EventBus.Instance;
            if (bus == null) return;
            bus.OnPlayerHPChanged -= OnPlayerHPChanged;
            bus.OnUltimateMeterChanged -= OnUltimateMeterChanged;
            bus.OnCheckpointReached -= OnCheckpointReached;
            bus.OnCooldownStarted -= OnCooldownStarted;
            bus.OnCooldownComplete -= OnCooldownComplete;
            bus.OnStatusEffectApplied -= OnStatusEffectApplied;
            bus.OnStatusEffectCleared -= OnStatusEffectCleared;
            bus.OnBossPhaseChanged -= OnBossPhaseChanged;
            bus.OnBlockChargesChanged -= OnBlockChargesChanged;
            bus.OnTimelineIntegrityChanged -= OnTimelineIntegrityChanged;
            bus.OnCollapseTremorChanged -= OnCollapseTremorChanged;
            bus.OnTimeFreezeStateChanged -= OnTimeFreezeStateChanged;
            bus.OnAbilitySlotLockChanged -= OnAbilitySlotLockChanged;
            bus.OnAbilityLockoutRefused -= OnAbilityLockoutRefused;
            bus.OnAnchorChargesChanged -= OnAnchorChargesChanged;
            bus.OnRallyEchoChanged -= OnRallyEchoChanged;
            bus.OnDustAwardCollected -= OnDustAwardCollected;
            bus.OnChronalDustCollected -= OnChronalDustCollected;
        }

        public override void _Process(double delta) {
            RefreshCounters(force: false);
            // Polled rather than event-driven: the accessibility settings are live,
            // and there is no settings-changed event to subscribe to.
            _opacity.Apply(_root);
            _uiScale.Apply(_root);
            ApplyFit();
            // The slab can outgrow its authored rect (the Momentum pips row), after the last Resized: follow it.
            PlaceTitleRow();
            SyncStatusAuthority();
            _indicators.Tick((float)delta);
            // After the fallback tick, so the drawn arc is the hero's own value
            // this frame rather than one frame ahead of it.
            SyncCooldownAuthority();
            RefreshIndicators();
            RefreshEchoBand();
            RefreshTimeFreeze((float)delta);
            RefreshIntegrity();
            TickDustFloat((float)delta);
            if (_bossIntroCardTimer > 0f) {
                _bossIntroCardTimer -= (float)delta;
                if (_bossIntroCard != null) {
                    if (_bossIntroCardTimer <= 0f) {
                        _bossIntroCard.Visible = false;
                    } else if (_bossIntroCardTimer < PulpMotionRules.PanelOutSeconds) {
                        // MOT-A row 5's fade at the end of the beat (no sink: the card is centred on the scene).
                        _bossIntroCard.Modulate = new Color(1f, 1f, 1f, _bossIntroCardTimer / PulpMotionRules.PanelOutSeconds);
                    }
                }
            }
            if (_checkpointToastTimer > 0f) {
                _checkpointToastTimer -= (float)delta;
                if (_checkpointToast != null) {
                    if (_checkpointToastTimer <= 0f) {
                        _checkpointToast.Visible = false;
                    } else if (_checkpointToastTimer < ToastFadeSeconds) {
                        _checkpointToast.Modulate = new Color(1f, 1f, 1f, ToastAlpha(_checkpointToastTimer));
                    }
                }
            }
        }

        // === Status slots (F24) ==============================================

        /// <summary>
        /// Pulls both status slots from the live <c>StatusController</c> when one is
        /// reachable. This is what makes the radials obey the authoritative clock:
        /// under Time Freeze the controller stops ticking, so the HUD stops too,
        /// with no separate freeze handling. Since Package 12 W10 the event layer is
        /// also correct on its own (<c>StatusController</c> raises a slot-scoped
        /// clear rather than re-announcing the survivor); this sync remains the
        /// radial's clock and the backstop for a missed event.
        /// </summary>
        private void SyncStatusAuthority() {
            if (_statusAuthority == null || !IsInstanceValid(_statusAuthority)) {
                _statusAuthority = ResolveStoryPlayer()
                    ?.GetNodeOrNull<FTT.Combat.StatusController>(StatusControllerPath);
            }
            if (_statusAuthority == null || !IsInstanceValid(_statusAuthority)) return;
            _indicators.SyncStatusFromAuthority(
                _statusAuthority.ControlStatusType, _statusAuthority.ControlStatusRemaining,
                _statusAuthority.DamageStatusType, _statusAuthority.DamageStatusRemaining);
        }

        /// <summary>
        /// The ability lenses' clock (2026-10-11, reworked by MM-HUD for the Momentum gauge). Every frame the hero's own values
        /// overwrite the model: the Specials' 2 s reuse delays and the gauge (<c>MomentumPips</c>), and the movement
        /// ability's cooldown. The cooldown radials' clock (2026-10-11, PROVISIONAL-HIT-CONFIRM-RULES
        /// (9)). The radials used to count down on their own from the
        /// <c>OnCooldownStarted</c> duration, so they never saw a per-hit Momentum
        /// refund (D2), a Time Freeze pause, the Story cooldown multiplier or a
        /// cooldown restored on load. HUD_CONTRACT: "fill is written by the owner
        /// of the authoritative value" — so every frame the hero's three cooldown
        /// timers overwrite the model's remaining time, keeping the denominator the
        /// cast armed (raised only if the authority exceeds it). With no hero
        /// reachable the local countdown stays the fallback.
        /// </summary>
        private void SyncCooldownAuthority() {
            FTT.Characters.PlayerController player = ResolveStoryPlayer();
            if (player == null || !IsInstanceValid(player) || player.PlayerIndex != 0) return;
            _indicators.SyncReuseDelay(FTT.Core.AbilitySlot.Special1, player.SpecialReuseRemainingSeconds(FTT.Core.AbilitySlot.Special1));
            _indicators.SyncReuseDelay(FTT.Core.AbilitySlot.Special2, player.SpecialReuseRemainingSeconds(FTT.Core.AbilitySlot.Special2));
            _indicators.SyncMomentum(player.MomentumPips);
            _indicators.SyncCooldownRemaining(FTT.Core.AbilitySlot.MovementAbility, player.MovementAbilityCooldownTimer);
        }

        private FTT.Characters.PlayerController ResolveStoryPlayer() {
            if (_echoPlayer != null && IsInstanceValid(_echoPlayer)) return _echoPlayer;
            _echoPlayer = GetTree()?.GetFirstNodeInGroup(StoryPlayerGroup) as FTT.Characters.PlayerController;
            return _echoPlayer;
        }

        // The status each slot's glyph Label was last written for; the Label's text, tooltip and colour change only then.
        private StatusType _damageLabelShown = (StatusType)(-1), _controlLabelShown = (StatusType)(-1);

        /// <summary>
        /// Draws one status slot. An empty slot hides its glyph and radial but
        /// <b>keeps its reserved position</b> — the panel is fixed-position
        /// precisely so hiding one indicator cannot shift the other. The kept glyph
        /// Label is rewritten only when the slot's status changes, the chip only
        /// when its look does, so a steady frame allocates nothing.
        /// </summary>
        private void RefreshStatusSlot(Label indicator, RadialProgress radial, PulpStatusChip chip, bool damageSlot) {
            if (indicator == null) return;
            StatusType status = _indicators.StatusIn(damageSlot);
            ref StatusType labelShown = ref damageSlot ? ref _damageLabelShown : ref _controlLabelShown;
            ref ChipShown chipShown = ref damageSlot ? ref _damageShown : ref _controlShown;
            if (status == StatusType.None) {
                if (labelShown != StatusType.None) {
                    indicator.Text = "";
                    indicator.TooltipText = "";
                    labelShown = StatusType.None;
                }
                ApplyChip(chip, ref chipShown, StatusType.None, 0f, 0f);
                return;
            }
            // The authored status colours live with the glow arbiter; reusing them
            // keeps the HUD slot and the character's outline the same colour rather
            // than creating a second canonical palette for statuses.
            Color tint = FTT.Combat.GlowPalette.Status(status).OutlineColor;
            if (labelShown != status) {
                // Glyph plus localized name: HUD_CONTRACT forbids identifying an effect
                // by colour alone, so the shape and the tooltip both carry it.
                indicator.Text = HudAbilityIndicatorModel.StatusGlyph(status);
                indicator.TooltipText = Tr(HudAbilityIndicatorModel.StatusLabelKey(status));
                indicator.AddThemeColorOverride(FontColorName, tint);
                labelShown = status;
            }
            float remaining = _indicators.RemainingIn(damageSlot);
            ApplyChip(chip, ref chipShown, status, remaining, StatusRadialFraction(remaining));
            if (radial == null) return;
            // The chip draws the ring and the glyph; the legacy radial keeps its value, hidden.
            radial.FillColor = tint;
            radial.Fraction = StatusRadialFraction(remaining);
        }

        /// <summary>
        /// Radial fill for a remaining duration. Statuses do not publish their
        /// original duration anywhere the HUD can see, so the ring reads "seconds
        /// left against a ten-second reference" rather than inventing a second
        /// canonical duration per status type.
        /// </summary>
        private static float StatusRadialFraction(float remainingSeconds) =>
            Mathf.Clamp(remainingSeconds / 10f, 0f, 1f);

        // === Integrity clock, tremor, freeze, anchors, seal ===================

        /// <summary>
        /// F01: the Integrity era-clock. The wedge is the remaining percentage, the
        /// text the rounded value, and the siphon streams appear only while living
        /// Extractors are actually draining it.
        ///
        /// <para>The V7.1 readout this replaces inferred "draining" from a
        /// frame-to-frame fall in the percentage, so a frozen or paused clock read
        /// as steady and a single-frame rounding wobble read as a siphon. The
        /// payload carries the real siphon count instead.</para>
        /// </summary>
        private void RefreshIntegrity() {
            if (_integrityClock == null) return;
            if (!_integrityKnown && FTT.Core.StoryManager.Instance == null) {
                _integrityClock.Visible = false;
                return;
            }
            float percent = _integrityKnown
                ? _integrity.Percent
                : FTT.Core.StoryManager.Instance.TimelineIntegrityPercent;
            _integrityClock.Visible = true;
            int whole = Mathf.FloorToInt(percent);
            // The numeral and its tooltip change only when the shown whole percent (or the language) does.
            if (_integrityText != null && whole != _shownIntegrityWhole) {
                _shownIntegrityWhole = whole;
                _integrityText.Text = string.Format(Tr("hud_percent"), whole);
                _integrityText.TooltipText = string.Format(Tr("hud_integrity"), whole);
            }
            if (_clockWedge != null) {
                _clockWedge.Fraction = Mathf.Clamp(percent / 100f, 0f, 1f);
                _clockWedge.FillColor = IntegrityTint();
            }
            // F01 / UI_SPEC_GLASS 3.4.4: sealed at the pre-boss checkpoint the dial gives way to the note.
            bool sealedGauge = FTT.Core.StoryManager.Instance != null && FTT.Core.StoryManager.Instance.IsPreBossLocked;
            SetDialShown(!sealedGauge);
            if (_siphonStreams == null) return;
            bool siphoning = _integrityKnown && _integrity.LivingExtractors > 0 && !_integrity.Frozen;
            _siphonStreams.Visible = siphoning;
            if (siphoning && _integrity.LivingExtractors != _shownSiphons) {
                _shownSiphons = _integrity.LivingExtractors;
                _siphonStreams.Text = string.Format(Tr("hud_integrity_siphons"), _integrity.LivingExtractors);
                _siphonStreams.TooltipText = _siphonStreams.Text;
            }
            if (_siphonDraw != null) _siphonDraw.Count = siphoning && !sealedGauge ? _integrity.LivingExtractors : 0;
        }

        private int _shownIntegrityWhole = int.MinValue, _shownSiphons = int.MinValue;

        /// <summary>
        /// The wedge's colour (its alpha rides the wedge's SelfModulate, <see cref="WedgeAlpha"/>): calm gold while the
        /// gauge is healthy, <c>danger</c> once it falls into the Collapse Tremor's first stage (R8: the threshold is
        /// <c>TimelineIntegrityRules</c>', not a literal here), and <c>ink2</c> at .6 while the clock is frozen (AC17) —
        /// the PreBoss seal and a rewind pause both stop the drain, and a clock that still looks live while it is locked
        /// is the most misleading thing this widget can do.
        /// </summary>
        private Color IntegrityTint() {
            if (_integrityKnown && _integrity.Frozen) return new Color(UIPalette.Ink2, FrozenWedgeAlpha / WedgeAlpha);
            float percent = _integrityKnown ? _integrity.Percent : 100f;
            return FTT.Core.TimelineIntegrityRules.TremorStage(percent) >= 1 ? UIPalette.Danger : UIPalette.Res;
        }

        private void OnTimelineIntegrityChanged(IntegrityPayload payload) {
            _integrity = payload;
            _integrityKnown = true;
            RefreshIntegrity();
        }

        /// <summary>
        /// V7.6 Collapse Tremor. Level 1 (&lt;20%) is a faint cold wash over the
        /// scene; level 2 (&lt;10%) deepens it. It is a steady tint with no pulsing,
        /// which is also what C01a's reduced treatment asks for — the contract keeps
        /// crack lines and danger areas readable and removes only decorative
        /// flicker, so there is nothing here to suppress.
        /// </summary>
        private void OnCollapseTremorChanged(TremorPayload payload) {
            _tremorLevel = Mathf.Clamp(payload.Level, 0, 2);
            if (_tremorOverlay == null) return;
            _tremorOverlay.Visible = _tremorLevel > 0;
            if (_tremorLevel == 0) return;
            _tremorOverlay.Color = new Color(0.6f, 0.85f, 1f, _tremorLevel == 1 ? 0.07f : 0.14f);
        }

        private void OnTimeFreezeStateChanged(TimeFreezePayload payload) {
            _timeFreezeState = payload.State;
            _timeFreezeSeconds = Mathf.Max(0f, payload.SecondsRemaining);
            // F24: a frozen world must not tick the status radials down.
            _indicators.StatusClockSuspended = _timeFreezeState == TimeFreezeState.Active;
            RefreshTimeFreeze();
        }

        /// <summary>
        /// F03's indicator: Ready, an active 5 s countdown, or a 45 s cooldown
        /// countdown, with a thaw warning in the final second of an active freeze.
        ///
        /// <para>Its readiness is deliberately independent of the death-rewind count
        /// beside it. They were one widget before V7.6 — the retired manual rewind
        /// hung its cooldown pip off the rewind counter — and conflating them again
        /// would tell the player their freeze was gone because they were out of
        /// rewinds.</para>
        /// </summary>
        private void RefreshTimeFreeze(float delta = 0f) {
            if (_timeFreezeIndicator == null) return;
            // Gameplay review 2026-11-03 (10b++): with the verb off the indicator is hidden and never refreshed.
            // The node stays in the scene, and the slab keeps its 484 px minimum width either way.
            bool enabled = FTT.Core.TimeFreezeRules.Enabled;
            if (_timeFreezeIndicator.Visible != enabled) _timeFreezeIndicator.Visible = enabled;
            if (!enabled) return;
            if (delta > 0f && _timeFreezeSeconds > 0f) {
                _timeFreezeSeconds = Mathf.Max(0f, _timeFreezeSeconds - delta);
            }
            bool thawWarning = _timeFreezeState == TimeFreezeState.Active
                && _timeFreezeSeconds > 0f && _timeFreezeSeconds <= ThawWarningSeconds;
            // UI_SPEC_GLASS 3.4.4: ready res2 with no ring, active a ring on the remaining share, cooldown ink2.
            if (_freezeBadge != null) {
                _freezeBadge.Ring = _timeFreezeState switch {
                    TimeFreezeState.Active => _timeFreezeSeconds / FTT.Environment.TimeFreezeController.FreezeSeconds,
                    TimeFreezeState.Cooldown => _timeFreezeSeconds / FTT.Environment.TimeFreezeController.CooldownSeconds,
                    _ => 0f
                };
                _freezeBadge.RingColor = thawWarning ? UIPalette.Danger : UIPalette.Accent;
                _freezeBadge.Modulate = _timeFreezeState == TimeFreezeState.Cooldown
                    ? new Color(1f, 1f, 1f, 0.7f) : Colors.White;
            }
            if (_freezeRemainingText == null) return;
            Color tint = _timeFreezeState switch {
                TimeFreezeState.Active => thawWarning ? UIPalette.Danger : UIPalette.Ink,
                TimeFreezeState.Cooldown => UIPalette.Ink2,
                _ => UIPalette.Res2
            };
            if (tint != _freezeTint) {
                _freezeTint = tint;
                _freezeRemainingText.AddThemeColorOverride(FontColorName, tint);
            }
            // The text only changes when the state, the shown tenth / second or the thaw warning does (or the language),
            // so both strings are rebuilt on that key alone and a steady frame formats nothing.
            int shown = _timeFreezeState switch {
                TimeFreezeState.Active => Mathf.CeilToInt(_timeFreezeSeconds * 10f - 0.0001f),
                TimeFreezeState.Cooldown => Mathf.CeilToInt(_timeFreezeSeconds),
                _ => 0
            };
            int key = ((int)_timeFreezeState << 24) | ((thawWarning ? 1 : 0) << 23) | shown;
            if (key == _freezeTextKey) return;
            _freezeTextKey = key;
            string state = _timeFreezeState switch {
                TimeFreezeState.Ready => Tr("common_ready"),
                TimeFreezeState.Active => string.Format(Tr("hud_seconds_short"), PulpAbilitySlot.FormatSeconds(_timeFreezeSeconds)),
                _ => string.Format(Tr("hud_seconds_short"), Mathf.CeilToInt(_timeFreezeSeconds))
            };
            if (_freezeRemainingText.Text != state) _freezeRemainingText.Text = state;
            // The long form stays as the accessible name (and the legacy keys stay in use).
            string spoken = _timeFreezeState switch {
                TimeFreezeState.Ready => Tr("hud_time_freeze_ready"),
                TimeFreezeState.Active => string.Format(
                    Tr(thawWarning ? "hud_time_freeze_thaw" : "hud_time_freeze_active"), Mathf.CeilToInt(_timeFreezeSeconds)),
                _ => string.Format(Tr("hud_time_freeze_cooldown"), Mathf.CeilToInt(_timeFreezeSeconds))
            };
            if (_timeFreezeIndicator.TooltipText != spoken) {
                _timeFreezeIndicator.TooltipText = spoken;
                _timeFreezeIndicator.AccessibilityName = spoken;
            }
        }

        private int _freezeTextKey = -1;
        private Color _freezeTint = new(0f, 0f, 0f, 0f);

        /// <summary>The (state, shown time, warning) key the Time Freeze strings were last built for; -1 forces a rebuild. Test surface.</summary>
        internal int FreezeTextKey => _freezeTextKey;

        public override void _Notification(int what) {
            if (what != (int)NotificationTranslationChanged) return;
            // Every string the HUD formats itself is rebuilt in the new language; the measured layouts follow once the
            // labels have re-shaped (they hear the change after this node).
            _freezeTextKey = -1;
            _shownIntegrityWhole = int.MinValue;
            _shownSiphons = int.MinValue;
            _shownBossPhase = -1;
            _secondsFormat = null;
            _damageLabelShown = (StatusType)(-1);
            _controlLabelShown = (StatusType)(-1);
            if (_damageShown.Type != StatusType.None) _damageShown.Time = null;
            if (_controlShown.Type != StatusType.None) _controlShown.Time = null;
            for (int index = 0; index < _lockShown.Length; index++) _lockShown[index] = (AbilitySlotLockState)(-1);
            _lastRewinds = int.MinValue;
            _lastDust = int.MinValue;
            if (IsInsideTree()) CallDeferred(MethodName.RelayoutAfterTranslation);
        }

        private void RelayoutAfterTranslation() {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            SizeChipHosts();
            LayoutHeroName();
            RefreshBossTexts();
            LayoutBossPanel();
            LayoutIntroCard();
            if (_checkpointToast != null && _checkpointToast.Visible) {
                _checkpointToast.Text = Tr("hud_checkpoint_reached");
                PlaceCheckpointToast();
            }
        }

        private void OnAbilitySlotLockChanged(AbilitySlotLockPayload payload) {
            _indicators.SetSlotLock(payload.Slot, payload.State);
            RefreshIndicators();
        }

        /// <summary>
        /// Act III Beacon anchor charges. Data-driven pip count, built in code the
        /// same way the block pips are — authoring three would cap the readout at
        /// today's Act III maximum.
        /// </summary>
        private void OnAnchorChargesChanged(AnchorChargesPayload payload) {
            if (_beaconAnchors == null) return;
            int max = Mathf.Max(0, payload.Max);
            _beaconAnchors.Visible = max > 0;
            if (max == 0) {
                RebuildAnchorPips(0);
                _drawnAnchorCharges = -1;
                _beaconIcon?.SetLastStand(false);
                _beaconAnchors.AccessibilityName = string.Empty;
                _beaconAnchors.TooltipText = string.Empty;
                return;
            }
            bool capacityChanged = max != _anchorPips.Length;
            if (capacityChanged) RebuildAnchorPips(max);
            int charges = Mathf.Clamp(payload.Charges, 0, _anchorPips.Length);
            // M15: an Anchor Snap (the count falling at unchanged capacity) cracks
            // and fades the spent pip(s); everything else redraws in place.
            bool snap = !capacityChanged && _drawnAnchorCharges > charges;
            for (int index = 0; index < _anchorPips.Length; index++) {
                AnchorPip pip = _anchorPips[index];
                if (index < charges) {
                    pip.SetFilled(true);
                } else if (snap && index < _drawnAnchorCharges) {
                    pip.Crack();
                } else if (pip.State != AnchorPip.PipState.Cracking) {
                    pip.SetFilled(false);
                }
            }
            _drawnAnchorCharges = charges;
            // At zero the next collapse is Smothered: a hollow icon, steady tint.
            _beaconIcon?.SetLastStand(charges == 0);
            string label = string.Format(Tr(AnchorChargesAccessibleKey), charges);
            _beaconAnchors.AccessibilityName = label;
            _beaconAnchors.TooltipText = label;
        }

        private void OnRallyEchoChanged(RallyEchoPayload payload) {
            // The band geometry is polled in RefreshEchoBand; the glass tube draws it. The old reclaim flash tinted
            // the ColorRect and has no glass form (MOT-A row 8: the band is not animated); a gap since UR-HUDS.
        }

        // === Dust counter (HUD_CONTRACT) ======================================

        /// <summary>
        /// HUD_CONTRACT: "Only the pooled +N pickup notification rises and fades
        /// after one second. The numeric balance does not fade after inactivity."
        /// The float is cosmetic and cannot drive the balance — the balance is read
        /// from the committed wallet in <see cref="RefreshCounters"/>, so a mirrored
        /// event or a rollback cannot add dust twice.
        /// </summary>
        private void ShowDustFloat(int amount) {
            if (_dustFloat == null || amount <= 0) return;
            string text = string.Format(Tr("hud_dust_pickup"), amount);
            _dustFloat.Text = text;
            if (_dustFloatRise != null) _dustFloatRise.Text = text;
            _dustFloat.Visible = true;
            _dustFloatTimer = DustFloatSeconds;
            AnimateDustFloat();
        }

        /// <summary>The float's life (1 s, HUD_CONTRACT): in, held and out by MOT-A row 13, then gone.</summary>
        private void TickDustFloat(float delta) {
            if (_dustFloat == null || _dustFloatTimer <= 0f) return;
            _dustFloatTimer -= delta;
            if (_dustFloatTimer <= 0f) {
                _dustFloatTimer = 0f;
                _dustFloat.Visible = false;
                return;
            }
            AnimateDustFloat();
        }

        private void OnDustAwardCollected(DustAwardCollectedPayload payload) => ShowDustFloat(payload.Amount);

        private void OnChronalDustCollected(int amount) => ShowDustFloat(amount);

        /// <summary>
        /// V7 boss intro ritual: the localized name card over the establishing beat, before the bar sweeps in. UI_SPEC_GLASS
        /// 9.2: a flourish with the boss-red diamond above the name in T04, fading in and rising (MOT-A row 4), held for the
        /// beat, fading out at its end.
        /// </summary>
        public void ShowBossIntroCard(string nameKey, float seconds) {
            if (_bossIntroCard == null) return;
            _bossIntroCard.Text = Tr(string.IsNullOrWhiteSpace(nameKey) ? "boss" : nameKey);
            _bossIntroCard.Visible = true;
            _bossIntroCard.Modulate = Colors.White;
            _bossIntroCardTimer = Mathf.Max(0.5f, seconds);
            // PopIn resets the scale, so the name's fit is applied after it starts.
            if (_bossIntroCard.IsInsideTree()) PulpMotion.PopIn(_bossIntroCard);
            LayoutIntroCard();
        }

        /// <summary>
        /// Positions the Rally echo band from the live player's pool each frame
        /// (the drain is continuous, so events would spam). Reuses the shared
        /// FighterHudModel band arithmetic so both modes agree on the geometry.
        /// </summary>
        private void RefreshEchoBand() {
            FTT.Characters.PlayerController player = ResolveStoryPlayer();
            // UR-HUDS: the PulpBarDecor draws the band (HP + echo, in the bar's units); the authored
            // RallyEcho_Band node stays for the path pins and never shows.
            if (_echoBand != null) _echoBand.Visible = false;
            if (_hpDecor == null) return;
            if (player == null) {
                _hpDecor.EchoValue = 0f;
                return;
            }
            float band = FighterHudModel.EchoBandFraction(
                player.CurrentHP, player.EchoPool, player.MaximumHP);
            _hpDecor.EchoValue = band > 0f ? player.CurrentHP + band * player.MaximumHP : 0f;
        }

        // === Public API used by level controllers ===

        public void SetLevelTitle(string translationKey) {
            if (_levelTitleLabel != null) _levelTitleLabel.Text = Tr(translationKey);
        }

        public void SetObjective(string translationKey, params object[] arguments) {
            if (_objectiveLabel == null) return;
            string text = Tr(translationKey);
            _objectiveLabel.Text = arguments != null && arguments.Length > 0
                ? string.Format(text, arguments)
                : text;
            if (_objectiveFlag != null) _objectiveFlag.Visible = !string.IsNullOrWhiteSpace(_objectiveLabel.Text);
        }

        public void InitializePlayerVitals(int currentHP, int maximumHP, float ultimateMeter) {
            UpdateHP(currentHP, maximumHP);
            UpdateMeter(ultimateMeter);
        }

        public void ShowBossBar(string bossNameKey, int currentHP, int maximumHP) =>
            ShowBossBar(bossNameKey, currentHP, maximumHP, null);

        /// <summary>
        /// Reveals the boss bar. <paramref name="phaseThresholds"/> is the boss's
        /// authored <c>BossData.PhaseThresholds</c> array; passing it here is what
        /// puts the phase notches on the bar. A null or empty array draws a plain
        /// bar, which is correct for a single-phase boss.
        /// </summary>
        public void ShowBossBar(string bossNameKey, int currentHP, int maximumHP, float[] phaseThresholds) {
            if (_bossPanel == null) return;
            bool revealing = !_bossPanel.Visible;
            _bossPanel.Visible = true;
            _bossPhase = 1;
            _bossPhaseFade?.Kill();
            if (_bossPhaseTab != null) _bossPhaseTab.Modulate = Colors.White;
            if (_bossNameLabel != null) _bossNameLabel.Text = Tr(bossNameKey);
            if (_bossBar != null) {
                _bossBar.MaxValue = maximumHP;
                _bossBar.Value = currentHP;
            }
            _bossNotches?.SetThresholds(phaseThresholds);
            if (_bossDecor != null) {
                // A reveal stops a tracker the last hit left running (REVIEW-UI-GLASS-CODEX-2).
                _bossDecor.SnapLagTo(currentHP);
                var notches = new System.Collections.Generic.List<float>(
                    _bossNotches?.Positions ?? System.Array.Empty<float>());
                _bossDecor.PhaseNotches = notches.ToArray();
            }
            RefreshBossTexts();
            LayoutBossPanel();
            // The bar appears by MOT-A row 4 (rows 16: the boss reveal); a second reveal while it shows re-lays it only.
            if (revealing && _bossPanel.IsInsideTree()) PulpMotion.PopIn(_bossPanel);
            // The level title and objective give way to the bar while a boss is revealed (UI_SPEC_GLASS 4.4 stress frame).
            if (_topLeft != null) _topLeft.Visible = false;
        }

        public void UpdateBossHP(int currentHP) {
            bool healed = _bossBar != null && currentHP > _bossBar.Value;
            if (_bossBar != null) _bossBar.Value = currentHP;
            // A boss heal (its historical recovery) stops the hold or drain the last hit started (REVIEW-UI-GLASS-CODEX-2).
            if (healed) _bossDecor?.SnapLagTo(currentHP);
            else _bossDecor?.DropLagTo(currentHP);
            RefreshBossTexts();
        }

        public void HideBossBar() {
            if (_bossPanel != null) _bossPanel.Visible = false;
            _bossNotches?.Clear();
            if (_bossDecor != null) _bossDecor.PhaseNotches = System.Array.Empty<float>();
            if (_topLeft != null) _topLeft.Visible = true;
        }

        // === Event handlers ===

        private void OnPlayerHPChanged(FTT.Core.PlayerHPPayload payload) {
            if (payload.PlayerIndex != 0) return;
            UpdateHP(Mathf.RoundToInt(payload.CurrentHP), Mathf.RoundToInt(payload.MaxHP));
        }

        private void OnUltimateMeterChanged(FTT.Core.UltimateMeterPayload payload) {
            if (payload.PlayerIndex != 0) return;
            UpdateMeter(payload.CurrentValue);
            _indicators.SetUltimateMeter(payload.CurrentValue);
        }

        private void OnCooldownStarted(FTT.Core.CooldownPayload payload) {
            if (payload.PlayerIndex != 0) return;
            _indicators.StartCooldown(payload.Slot, payload.Duration);
        }

        private void OnCooldownComplete(FTT.Core.AbilitySlot slot) =>
            _indicators.StartCooldown(slot, 0f);

        /// <summary>
        /// D10 (2026-10-11): the "not yet" flash on the refused slot. Glass (UI_SPEC_GLASS 3.4.2, MOT-A row 17; ruling 3
        /// of TG-WIDGETS step 2): a contracting 2 px <c>danger</c> circle round the lens's readiness ring, the shape
        /// channel, with the lens tinted, the colour one (<c>NotYetFlash</c>'s circle mode). Player 0 only: the Level 13
        /// Mirror clone raises the event too, and its refusals are not the hero's.
        /// </summary>
        private void OnAbilityLockoutRefused(FTT.Core.AbilityLockoutRefusedPayload payload) {
            if (payload.PlayerIndex != 0) return;
            if (payload.Slot == FTT.Core.AbilitySlot.Ultimate) {
                if (_ultSlot != null) NotYetFlash.Play(_ultSlot.Ring, _ultSlot, NotYetShape.Circle);
                return;
            }
            if (payload.Slot is not (FTT.Core.AbilitySlot.Special1 or FTT.Core.AbilitySlot.Special2)) return;
            int index = System.Array.IndexOf(CooldownSlots, payload.Slot);
            if (index < 0 || _slots[index] == null) return;
            NotYetFlash.Play(_slots[index].Ring, _slots[index], NotYetShape.Circle);
        }

        private void OnStatusEffectApplied(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != 0) return;
            _indicators.ApplyStatus(payload.Type, payload.Duration);
        }

        private void OnStatusEffectCleared(FTT.Core.StatusEffectPayload payload) {
            if (payload.TargetIndex != 0) return;
            // Package 12 W10: StatusController publishes per-slot clears.
            if (payload.SlotScoped) {
                _indicators.ClearStatusSlot(payload.Slot == FTT.Combat.StatusSlot.Damage);
                return;
            }
            _indicators.ClearStatus(payload.Type);
        }

        private void OnBossPhaseChanged(int phaseIndex) {
            _bossNotches?.NotifyPhaseChanged(phaseIndex);
            // BossController starts at phase index 0 and publishes 1 on entering its second phase: the label shows
            // the one-based number (index + 1) while the notches keep the raw index.
            int previous = _bossPhase;
            _bossPhase = Mathf.Max(1, phaseIndex + 1);
            RefreshBossTexts();
            LayoutBossPanel();
            // MOT-A row 16: the phase label cross-fades in over 220 ms (a fade, so the same under Reduced); notches are static.
            if (_bossPhase != previous && _bossPhaseTab != null && _bossPanel != null && _bossPanel.Visible
                && _bossPhaseTab.IsInsideTree()) {
                _bossPhaseFade?.Kill();
                _bossPhaseTab.Modulate = new Color(1f, 1f, 1f, 0f);
                _bossPhaseFade = PulpMotion.NewTween(_bossPhaseTab);
                _bossPhaseFade.TweenProperty(_bossPhaseTab, "modulate:a", 1f, PulpMotionRules.PhaseLabelSeconds);
            }
        }

        private void OnBlockChargesChanged(FTT.Core.BlockChargesPayload payload) {
            if (payload.PlayerIndex != 0) return;
            // The published max is BlockSystem.MaxCharges, which CharacterFactory
            // sets from PlayerController.MaximumBlockCharges — base capacity plus
            // the Story-only Resonance bonus. Capacity changes rebuild the row.
            if (payload.MaxCharges != _blockPips.Length) RebuildBlockPips(payload.MaxCharges);
            SetBlockPips(payload.CurrentCharges);
        }

        /// <summary>
        /// UI_SPEC_GLASS 9.2: the checkpoint toast fades in rising (MOT-A row 4), holds 2 s and fades over 300 ms (the
        /// timer in <see cref="_Process"/>).
        /// </summary>
        private void OnCheckpointReached(string checkpointID) {
            if (_checkpointToast == null) return;
            _checkpointToast.Text = Tr("hud_checkpoint_reached");
            _checkpointToast.Visible = true;
            PlaceCheckpointToast();
            _checkpointToast.Modulate = Colors.White;
            _checkpointToastTimer = PulpMotionRules.PanelInSeconds + ToastHoldSeconds + ToastFadeSeconds;
            if (_checkpointToast.IsInsideTree()) PulpMotion.PopIn(_checkpointToast);
        }

        /// <summary>Seconds the checkpoint toast still shows (0 when hidden). Test seam.</summary>
        public float CheckpointToastSecondsLeft => _checkpointToastTimer;

        private void UpdateHP(int currentHP, int maximumHP) {
            if (_hpBar != null) {
                _hpBar.MaxValue = maximumHP;
                _hpBar.Value = currentHP;
            }
            if (_hpText != null) {
                // T17 (UI_SPEC_GLASS 3.4.1): the numeral, then "/max" at 18 px, dimmed, on its baseline.
                _hpText.Text = currentHP.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _hpText.TooltipText = $"{Tr("hud_hp")} {currentHP}/{maximumHP}";
                if (_hpMax != null) _hpMax.Text = "/" + maximumHP.ToString(System.Globalization.CultureInfo.InvariantCulture);
                LayoutValuePair(_hpText, _hpMax, _hpBar, HpValueRightPad);
            }
            if (_hpDecor != null) {
                // The first value, a new maximum and a heal (a rewind, the Rally reclaim, a font) put the tracker on the
                // fill and stop a hold or drain the last hit started (REVIEW-UI-GLASS-CODEX-2); a drop falls through it.
                bool fresh = !_hpSeeded || maximumHP != _hpPresentedMax;
                if (fresh || currentHP > _hpPresented) _hpDecor.SnapLagTo(currentHP);
                else _hpDecor.DropLagTo(currentHP);
                _hpSeeded = true;
                _hpPresented = currentHP;
                _hpPresentedMax = maximumHP;
                bool low = maximumHP > 0 && currentHP > 0 && currentHP / (float)maximumHP <= LowHpFraction;
                _hpDecor.Low = low;
                if (_hpFill != null) _hpFill.BgColor = low ? UIPalette.Danger : UIPalette.Hp;
            }
        }

        private void UpdateMeter(float value) {
            _meterValue = value;
            if (_meterFill == null) return;
            _meterFill.Fraction = Mathf.Clamp(value, 0f, FTT.Combat.InfluenceMeterRules.MaxValue)
                / FTT.Combat.InfluenceMeterRules.MaxValue;
            _meterFill.FillColor = value >= HudAbilityIndicatorModel.UltimateReadyMeter
                ? UIPalette.GoldBright
                : UIPalette.Meter;
        }

        private void RefreshCounters(bool force) {
            var storyManager = FTT.Core.StoryManager.Instance;
            if (storyManager == null) return;
            int rewinds = storyManager.ChronalRewindsRemaining;
            if ((force || rewinds != _lastRewinds) && _rewindCountText != null) {
                _rewindCountText.Text = string.Format(Tr("hud_rewind_count"), rewinds);
                _rewindCountText.TooltipText = string.Format(Tr("hud_rewinds"), rewinds);
                // UI_SPEC_GLASS 3.4.4: the glyph and the number turn to danger at one or none left; an empty
                // hourglass at zero dims, and deliberately raises no alarm: running out is a state.
                bool low = rewinds <= 1;
                if (low) _rewindCountText.AddThemeColorOverride(FontColorName, UIPalette.Danger);
                else _rewindCountText.RemoveThemeColorOverride(FontColorName);
                if (_rewindBadge != null) {
                    _rewindBadge.Tint = low ? UIPalette.Danger : UIPalette.Accent2;
                    _rewindBadge.Modulate = new Color(1f, 1f, 1f, rewinds <= 0 ? 0.45f : 1f);
                }
                if (_rewindIcon != null) _rewindIcon.TooltipText = _rewindCountText.TooltipText;
                _lastRewinds = rewinds;
            }
            int dust = storyManager.ChronalDustCollected;
            if ((force || dust != _lastDust) && _dustText != null) {
                // HUD_CONTRACT: this is the level's UNDEPOSITED balance, bound to the committed wallet;
                // the Repository/Beacon shows spendable dust separately. The plate shows the bare number
                // (T26, UI_SPEC_GLASS 3.4.4); the long form is its tooltip and accessible name.
                _dustText.Text = dust.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                _dustText.TooltipText = string.Format(Tr("hud_level_dust"), dust);
                _lastDust = dust;
            }
        }

        /// <summary>
        /// Repaints the three cooldown rings, their lock overlays, the Ultimate lock
        /// overlay and both status slots from the model.
        /// </summary>
        // What each kept glyph Label was last written for (0..2 the cooldowns, 3 the Ultimate): a steady frame writes nothing.
        private readonly int[] _iconReadyShown = { -1, -1, -1 };
        private readonly AbilitySlotLockState[] _lockShown = {
            (AbilitySlotLockState)(-1), (AbilitySlotLockState)(-1), (AbilitySlotLockState)(-1), (AbilitySlotLockState)(-1)
        };

        /// <summary>
        /// Repaints the three cooldown lenses and the Ultimate lens, their kept glyph Labels and lock overlays, and both
        /// status slots from the model. Runs every frame; every write is gated on a change.
        /// </summary>
        private void RefreshIndicators() {
            for (int index = 0; index < CooldownSlots.Length; index++) {
                FTT.Core.AbilitySlot slot = CooldownSlots[index];
                AbilitySlotLockState lockState = _indicators.LockState(slot);
                RadialProgress radial = _cooldownRadials[index];
                if (radial != null) radial.Fraction = _indicators.ReadinessFraction(slot);
                Label icon = _cooldownIcons[index];
                int ready = _indicators.IsReady(slot) ? 1 : 0;
                if (icon != null && ready != _iconReadyShown[index]) {
                    _iconReadyShown[index] = ready;
                    icon.AddThemeColorOverride(FontColorName, ready == 1 ? UIPalette.Res2 : UIPalette.TextDisabled);
                }
                ApplyLockOverlay(_cooldownLockOverlays[index], lockState, index);
                ApplySlot(_slots[index], lockState, ready == 1,
                    _indicators.ReadinessFraction(slot), _indicators.RemainingSeconds(slot), _indicators.IsStarved(slot));
            }
            RefreshMomentumPips();
            RefreshDefyNotch();
            AbilitySlotLockState ultLock = _indicators.LockState(FTT.Core.AbilitySlot.Ultimate);
            ApplyLockOverlay(_meterLockOverlay, ultLock, CooldownSlots.Length);
            ApplySlot(_ultSlot, ultLock, _indicators.IsReady(FTT.Core.AbilitySlot.Ultimate),
                _indicators.ReadinessFraction(FTT.Core.AbilitySlot.Ultimate), 0f, starved: false);
            RefreshStatusSlot(_damageStatusIndicator, _damageStatusRadial, _damageChip, damageSlot: true);
            RefreshStatusSlot(_controlStatusIndicator, _controlStatusRadial, _controlChip, damageSlot: false);
        }

        /// <summary>
        /// V7.5 slot locks. A locked slot is <b>shown</b>, never hidden: Dormant is the lens's dormant lock for a legacy
        /// the hero has not recovered yet, Suppressed its cold cross-out for one an Eraser took away (the lens draws them).
        /// The kept overlay Label carries the glyph and the localized name for the tests and accessibility, rewritten only
        /// when the state changes.
        /// </summary>
        private void ApplyLockOverlay(Label overlay, AbilitySlotLockState state, int index) {
            if (overlay == null || _lockShown[index] == state) return;
            _lockShown[index] = state;
            if (state == AbilitySlotLockState.Clear) {
                overlay.Visible = false;
                return;
            }
            overlay.Visible = true;
            overlay.Text = HudAbilityIndicatorModel.LockGlyph(state);
            overlay.TooltipText = Tr(HudAbilityIndicatorModel.LockLabelKey(state));
            overlay.AddThemeColorOverride(
                FontColorName,
                state == AbilitySlotLockState.Dormant ? UIPalette.Ink3 : UIPalette.UnboundCold);
        }

        // === Portrait and data-driven pips ===

        /// <summary>
        /// M-27 (audit 2026-08-08; design :2607/:2611/:2721). Resolves the locked
        /// campaign character's portrait and initial shield-charge capacity. The
        /// portrait comes straight from the authored <c>CharacterData</c> — the same
        /// resource the Fighter HUD consumes — and the capacity is
        /// <c>MaxBlockCharges</c> plus the Story-only Resonance bonus, resolved
        /// through the canonical <see cref="FTT.Environment.ResonanceProgression"/>
        /// rather than a second table.
        /// </summary>
        private void InitializeCharacterPresentation() {
            string characterID = FTT.Core.GameManager.Instance?.CurrentSession.SelectedCharacterID;
            if (string.IsNullOrWhiteSpace(characterID)) return;
            string path = $"res://resources/Characters/{characterID}_data.tres";
            if (!ResourceLoader.Exists(path)) return;
            var data = FTT.Core.AuthoredResources.Load<FTT.Characters.CharacterData>(path);
            if (data == null) return;

            if (_portrait != null) {
                _portrait.Texture = data.CharacterPortrait;
                _portrait.Visible = data.CharacterPortrait != null;
            }
            if (_roundel != null) _roundel.Portrait = data.CharacterPortrait;
            if (_heroName != null && !string.IsNullOrWhiteSpace(data.DisplayNameKey)) {
                _heroName.Text = data.DisplayNameKey;
                LayoutHeroName();
            }
            for (int index = 0; index < CooldownSlots.Length; index++) {
                if (_slots[index] != null) _slots[index].Art = AbilityIconLibrary.Load(characterID, CooldownSlots[index]);
            }
            if (_ultSlot != null) _ultSlot.Art = AbilityIconLibrary.Load(characterID, FTT.Core.AbilitySlot.Ultimate);

            int capacity = data.MaxBlockCharges;
            if (FTT.Environment.ResonanceProgression.TryResolveActive(
                    characterID, out FTT.Environment.StoryStatProfile storyStats)) {
                capacity += storyStats.BlockChargeBonus;
            }
            RebuildBlockPips(capacity);
            SetBlockPips(capacity);
        }

        /// <summary>
        /// Pip count is data-driven (base capacity plus Resonance), so the pips are
        /// code-built inside the authored row — the same deliberate pattern as the
        /// Fighter HUD's stock/shield pips.
        /// </summary>
        private void RebuildBlockPips(int capacity) {
            _blockPips = RebuildPipRow(_blockPipRow, Mathf.Max(1, capacity), BlockPipSize);
        }

        private void RebuildAnchorPips(int capacity) {
            _anchorPips = RebuildAnchorPipRow(_anchorPipRow, capacity);
        }

        /// <summary>M15: the 18 × 18 gold diamond pips (UI_SPEC_GLASS 3.4.4), one per anchor charge.</summary>
        private static AnchorPip[] RebuildAnchorPipRow(HBoxContainer row, int count) {
            if (row == null) return System.Array.Empty<AnchorPip>();
            Godot.Collections.Array<Node> children = row.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    row.RemoveChild(child);
                    child.Free();
                }
            }
            if (count <= 0) return System.Array.Empty<AnchorPip>();
            var pips = new AnchorPip[count];
            for (int index = 0; index < count; index++) {
                var pip = new AnchorPip { Name = $"Pip{index}" };
                row.AddChild(pip);
                pips[index] = pip;
            }
            return pips;
        }

        private static PulpIcon[] RebuildPipRow(HBoxContainer row, int count, int size) {
            if (row == null) return System.Array.Empty<PulpIcon>();
            Godot.Collections.Array<Node> children = row.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    row.RemoveChild(child);
                    child.Free();
                }
            }
            if (count <= 0) return System.Array.Empty<PulpIcon>();
            var pips = new PulpIcon[count];
            for (int index = 0; index < count; index++) {
                PulpIcon pip = PulpIcon.Create("shield", UIPalette.Shield, size);
                pip.Name = $"Pip{index}";
                row.AddChild(pip);
                pips[index] = pip;
            }
            return pips;
        }

        /// <summary>
        /// UI_SPEC_GLASS 3.4.4: a lit pip is the <c>shield</c> glyph in <c>shield</c> with its pale glow; a spent one the
        /// glowless <c>shield_off</c> at .28 rather than disappearing, so capacity stays readable.
        /// </summary>
        private void SetBlockPips(int filled) {
            int lit = Mathf.Clamp(filled, 0, _blockPips.Length);
            for (int index = 0; index < _blockPips.Length; index++) {
                bool on = index < lit;
                _blockPips[index].IconName = on ? "shield" : "shield_off";
                _blockPips[index].Tint = UIPalette.Shield;
                _blockPips[index].Modulate = new Color(1f, 1f, 1f, on ? 1f : SpentPipAlpha);
            }
        }

        // === UI resolution ===

        // The compact Story HUD's tree (2026-10-27): one slab, two tiers. Public so the tests bind the same paths.
        /// <summary>The left stack (the one slab): its rect is the left cluster's.</summary>
        public const string TopLeftPanelPath = "SafeArea/TopLeft_Panel";
        /// <summary>The one glass slab (<c>GlassPanelHud</c>).</summary>
        public const string HudPanelPath = "SafeArea/TopLeft_Panel/HudPanel";
        /// <summary>The slab's two-tier stack.</summary>
        public const string HudStackPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack";
        /// <summary>Tier 1's vitals column: the HP row (tube and block pips) over the status badges.</summary>
        public const string VitalsPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Top/Vitals";
        /// <summary>The HP row: the HP tube, then the block pips.</summary>
        public const string HpRowPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Top/Vitals/HpRow";
        /// <summary>Tier 2: the Ultimate, the two capsules, the hairline and the time tools.</summary>
        public const string AbilitiesPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Abilities";
        /// <summary>The column that stacks the Specials capsule over the Momentum pips (MM-HUD).</summary>
        public const string SpecialsColumnPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Abilities/SpecialsColumn";
        /// <summary>The Momentum pips' row under the Specials capsule.</summary>
        public const string MomentumPipsPath = SpecialsColumnPath + "/MomentumPips";
        /// <summary>The capsule that holds Special 1 and Special 2.</summary>
        public const string SpecialsCapsulePath = SpecialsColumnPath + "/SpecialsCapsule";
        /// <summary>The capsule that holds the Movement ability alone.</summary>
        public const string MovementCapsulePath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Abilities/MovementCapsule";
        /// <summary>The time tools column at the slab's right end (rewind and Beacon, then Time Freeze).</summary>
        public const string TimeToolsPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Abilities/TimeTools";
        /// <summary>The time tools' first row: the rewind counter, then (Act III) the Beacon anchors.</summary>
        public const string RewindRowPath = "SafeArea/TopLeft_Panel/HudPanel/Pad/Stack/Abilities/TimeTools/RewindRow";
        /// <summary>Each cooldown lens's host Label (Special 1, Special 2, Movement).</summary>
        public static readonly string[] CooldownIconPaths = {
            SpecialsCapsulePath + "/CooldownRow/Cooldown1_Icon",
            SpecialsCapsulePath + "/CooldownRow/Cooldown2_Icon",
            MovementCapsulePath + "/CooldownRow/Cooldown3_Icon"
        };
        public const string TopRightPanelPath = "SafeArea/TopRight_Panel";
        public const string CurrencyPath = "SafeArea/TopRight_Panel/DustPlate/Pad/CurrencyContainer";

        private void ResolveUI() {
            _root = GetNodeOrNull<Control>("SafeArea");
            if (_root == null) return;
            // Unscaled fonts: the whole HUD layout scales through _uiScale
            // instead, so the shared (scale-mutated) theme would double-scale.
            _root.Theme ??= UIPalette.NewUnscaledTheme();

            _topLeft = GetNodeOrNull<Control>("SafeArea/TopLeft");
            _levelTitleLabel = GetNodeOrNull<Label>("SafeArea/TopLeft/LevelTitle");
            _objectiveLabel = GetNodeOrNull<Label>("SafeArea/TopLeft/Objective");
            _portrait = GetNodeOrNull<TextureRect>($"{HudStackPath}/Top/PortraitHost/PlayerPortrait");
            _heroName = GetNodeOrNull<Label>($"{VitalsPath}/HeroName");

            _hpBar = GetNodeOrNull<ProgressBar>($"{HpRowPath}/HealthBar_BG");
            _echoBand = GetNodeOrNull<ColorRect>($"{HpRowPath}/HealthBar_BG/RallyEcho_Band");
            _hpText = GetNodeOrNull<Label>($"{HpRowPath}/HealthBar_BG/HPText");
            _hpMax = GetNodeOrNull<Label>($"{HpRowPath}/HealthBar_BG/HPMax");
            _blockPipRow = GetNodeOrNull<HBoxContainer>($"{HpRowPath}/BlockCharges_Panel");

            _damageStatusIndicator =
                GetNodeOrNull<Label>($"{VitalsPath}/StatusEffects_Panel/DamageStatus_Indicator");
            _damageStatusRadial = GetNodeOrNull<RadialProgress>(
                $"{VitalsPath}/StatusEffects_Panel/DamageStatus_Indicator/DamageStatus_DurationRadial");
            _controlStatusIndicator =
                GetNodeOrNull<Label>($"{VitalsPath}/StatusEffects_Panel/ControlStatus_Indicator");
            _controlStatusRadial = GetNodeOrNull<RadialProgress>(
                $"{VitalsPath}/StatusEffects_Panel/ControlStatus_Indicator/ControlStatus_DurationRadial");

            _meterHost = GetNodeOrNull<Control>($"{AbilitiesPath}/MeterRow/UltimateMeter_BG");
            _meterFill = GetNodeOrNull<RadialProgress>(
                $"{AbilitiesPath}/MeterRow/UltimateMeter_BG/UltimateMeter_Fill");
            _meterLockOverlay = GetNodeOrNull<Label>(
                $"{AbilitiesPath}/MeterRow/UltimateMeter_BG/UltimateMeter_LockOverlay");

            for (int index = 0; index < CooldownSlots.Length; index++) {
                string iconName = $"Cooldown{index + 1}_Icon";
                _cooldownIcons[index] = GetNodeOrNull<Label>(CooldownIconPaths[index]);
                _cooldownRadials[index] = GetNodeOrNull<RadialProgress>(
                    $"{CooldownIconPaths[index]}/Cooldown{index + 1}_Radial");
                _cooldownLockOverlays[index] = GetNodeOrNull<Label>(
                    $"{CooldownIconPaths[index]}/Cooldown{index + 1}_LockOverlay");
                if (_cooldownIcons[index] != null) {
                    _cooldownIcons[index].Text =
                        Tr(HudAbilityIndicatorModel.SlotLabelKey(CooldownSlots[index]));
                }
            }

            _rewindIcon = GetNodeOrNull<Label>($"{RewindRowPath}/RewindCounter/RewindIcon");
            _rewindCountText = GetNodeOrNull<Label>($"{RewindRowPath}/RewindCounter/RewindCountText");
            _timeFreezeIndicator = GetNodeOrNull<Control>($"{TimeToolsPath}/TimeFreezeIndicator");
            _freezeIcon = GetNodeOrNull<Label>($"{TimeToolsPath}/TimeFreezeIndicator/FreezeIcon");
            _freezeRemainingText =
                GetNodeOrNull<Label>($"{TimeToolsPath}/TimeFreezeIndicator/FreezeColumn/FreezeRemainingText");
            _beaconAnchors = GetNodeOrNull<Control>($"{RewindRowPath}/BeaconAnchors");
            _anchorPipRow = GetNodeOrNull<HBoxContainer>($"{RewindRowPath}/BeaconAnchors/AnchorPips");
            _beaconIcon = GetNodeOrNull<BeaconAnchorIcon>($"{RewindRowPath}/BeaconAnchors/BeaconIcon");

            _dustText = GetNodeOrNull<Label>($"{CurrencyPath}/ChronalDustText");
            _dustFloat = GetNodeOrNull<Label>($"{CurrencyPath}/DustPickup_Float");
            _integrityClock = GetNodeOrNull<Control>($"{TopRightPanelPath}/IntegrityClock");
            _clockWedge = GetNodeOrNull<RadialProgress>($"{TopRightPanelPath}/IntegrityClock/ClockWedge");
            _siphonStreams = GetNodeOrNull<Label>($"{TopRightPanelPath}/IntegrityClock/SiphonStreams");
            _integrityText = GetNodeOrNull<Label>($"{TopRightPanelPath}/IntegrityClock/IntegrityText");

            _bossPanel = GetNodeOrNull<Control>("SafeArea/BossPanel");
            _bossNameLabel = GetNodeOrNull<Label>("SafeArea/BossPanel/BossName");
            _bossBar = GetNodeOrNull<ProgressBar>("SafeArea/BossPanel/BossBar");
            _bossNotches = GetNodeOrNull<BossPhaseNotchOverlay>("SafeArea/BossPanel/BossBar/PhaseNotches");
            _bossIntroCard = GetNodeOrNull<Label>("SafeArea/BossIntroCard");
            _tremorOverlay = GetNodeOrNull<ColorRect>("SafeArea/TremorOverlay");
            _checkpointToast = GetNodeOrNull<Label>("SafeArea/CheckpointToast");
            _bossPhaseTab = GetNodeOrNull<Label>("SafeArea/BossPanel/BossPhaseTab");
            _bossHpValue = GetNodeOrNull<Label>("SafeArea/BossPanel/BossBar/BossHpValue");
            _bossHpMax = GetNodeOrNull<Label>("SafeArea/BossPanel/BossBar/BossHpMax");
            BuildPulpWidgets();
        }
    }
}
