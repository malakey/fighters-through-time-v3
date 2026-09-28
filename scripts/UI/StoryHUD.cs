using Godot;
using FTT.Core;

namespace FTT.UI {

    /// <summary>
    /// Reusable Story Mode HUD, rewritten to the HUD_CONTRACT hierarchy by
    /// Package 11 A8 (F24): a safe area, a top-left vitals panel and a top-right
    /// currency/Integrity panel.
    ///
    /// <para>What it shows: portrait, HP with the Rally echo band, block charges,
    /// <b>both</b> status slots with authoritative duration radials, the Ultimate
    /// meter ring and its lock overlay, the F13 Defy seal, the death-rewind
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

        /// <summary>Design's 20x20 px shield icons; a ColorRect stands in for art.</summary>
        private const int BlockPipSize = 20;

        /// <summary>Act III anchor pips, beside the Beacon icon.</summary>
        private const int AnchorPipSize = 12;

        /// <summary>HUD_CONTRACT: only the pooled <c>+N</c> notification fades, after one second.</summary>
        public const float DustFloatSeconds = 1.0f;

        /// <summary>Thaw warning window, in seconds before an active freeze ends.</summary>
        public const float ThawWarningSeconds = 1.0f;

        private Control _root;
        private TextureRect _portrait;
        private HBoxContainer _blockPipRow;
        private ColorRect[] _blockPips = System.Array.Empty<ColorRect>();
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

        // Meter ring, lock overlay and Defy seal.
        private RadialProgress _meterFill;
        private Label _meterLockOverlay;
        private Label _defySeal;
        private DefySealState _defySealState = DefySealState.Building;

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
        private HBoxContainer _anchorPipRow;
        private ColorRect[] _anchorPips = System.Array.Empty<ColorRect>();

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
                foreach (ColorRect pip in _blockPips) {
                    if (pip.Modulate.A >= 1f) lit++;
                }
                return lit;
            }
        }

        /// <summary>The portrait widget, or null before the scene resolves. Test seam.</summary>
        public TextureRect Portrait => _portrait;

        /// <summary>The Defy seal state currently drawn. Test seam.</summary>
        public DefySealState DefySeal => _defySealState;

        /// <summary>The Time Freeze verb state currently drawn. Test seam.</summary>
        public TimeFreezeState TimeFreeze => _timeFreezeState;

        /// <summary>Act III anchor pips currently drawn. Test seam.</summary>
        public int AnchorPipCount => _anchorPips.Length;

        /// <summary>Lit Act III anchor pips. Test seam.</summary>
        public int LitAnchorPips {
            get {
                int lit = 0;
                foreach (ColorRect pip in _anchorPips) {
                    if (pip.Modulate.A >= 1f) lit++;
                }
                return lit;
            }
        }

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
                bus.OnAnchorChargesChanged += OnAnchorChargesChanged;
                bus.OnDefySealChanged += OnDefySealChanged;
                bus.OnRallyEchoChanged += OnRallyEchoChanged;
                bus.OnDustAwardCollected += OnDustAwardCollected;
                bus.OnChronalDustCollected += OnChronalDustCollected;
            }
            InitializeCharacterPresentation();
            RefreshCounters(force: true);
            RefreshIndicators();
            RefreshDefySeal();
            RefreshTimeFreeze();
            RefreshIntegrity();
            _opacity.Apply(_root, force: true);
            _uiScale.Apply(_root, force: true);
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
            bus.OnAnchorChargesChanged -= OnAnchorChargesChanged;
            bus.OnDefySealChanged -= OnDefySealChanged;
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
            SyncStatusAuthority();
            _indicators.Tick((float)delta);
            RefreshIndicators();
            RefreshEchoBand();
            RefreshTimeFreeze((float)delta);
            RefreshIntegrity();
            TickDustFloat((float)delta);
            if (_bossIntroCardTimer > 0f) {
                _bossIntroCardTimer -= (float)delta;
                if (_bossIntroCardTimer <= 0f && _bossIntroCard != null) _bossIntroCard.Visible = false;
            }
            if (_checkpointToastTimer > 0f) {
                _checkpointToastTimer -= (float)delta;
                if (_checkpointToastTimer <= 0f && _checkpointToast != null) _checkpointToast.Visible = false;
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
                    ?.GetNodeOrNull<FTT.Combat.StatusController>("StatusController");
            }
            if (_statusAuthority == null || !IsInstanceValid(_statusAuthority)) return;
            _indicators.SyncStatusFromAuthority(
                _statusAuthority.ControlStatusType, _statusAuthority.ControlStatusRemaining,
                _statusAuthority.DamageStatusType, _statusAuthority.DamageStatusRemaining);
        }

        private FTT.Characters.PlayerController ResolveStoryPlayer() {
            if (_echoPlayer != null && IsInstanceValid(_echoPlayer)) return _echoPlayer;
            _echoPlayer = GetTree()?.GetFirstNodeInGroup("StoryPlayer") as FTT.Characters.PlayerController;
            return _echoPlayer;
        }

        /// <summary>
        /// Draws one status slot. An empty slot hides its glyph and radial but
        /// <b>keeps its reserved position</b> — the panel is fixed-position
        /// precisely so hiding one indicator cannot shift the other.
        /// </summary>
        private void RefreshStatusSlot(Label indicator, RadialProgress radial, bool damageSlot) {
            if (indicator == null) return;
            StatusType status = _indicators.StatusIn(damageSlot);
            if (status == StatusType.None) {
                indicator.Text = "";
                indicator.TooltipText = "";
                if (radial != null) radial.Visible = false;
                return;
            }
            // Glyph plus localized name: HUD_CONTRACT forbids identifying an effect
            // by colour alone, so the shape and the tooltip both carry it.
            indicator.Text = HudAbilityIndicatorModel.StatusGlyph(status);
            indicator.TooltipText = Tr(HudAbilityIndicatorModel.StatusLabelKey(status));
            // The authored status colours live with the glow arbiter; reusing them
            // keeps the HUD slot and the character's outline the same colour rather
            // than creating a second canonical palette for statuses.
            Color tint = FTT.Combat.GlowPalette.Status(status).OutlineColor;
            indicator.AddThemeColorOverride("font_color", tint);
            if (radial == null) return;
            radial.Visible = true;
            radial.FillColor = tint;
            radial.Fraction = StatusRadialFraction(_indicators.RemainingIn(damageSlot));
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
            if (_integrityText != null) {
                _integrityText.Text = string.Format(Tr("hud_integrity"), Mathf.FloorToInt(percent));
                _integrityText.AddThemeColorOverride("font_color", IntegrityTint());
            }
            if (_clockWedge != null) {
                _clockWedge.Fraction = Mathf.Clamp(percent / 100f, 0f, 1f);
                _clockWedge.FillColor = IntegrityTint();
            }
            if (_siphonStreams == null) return;
            bool siphoning = _integrityKnown && _integrity.LivingExtractors > 0 && !_integrity.Frozen;
            _siphonStreams.Visible = siphoning;
            if (siphoning) {
                _siphonStreams.Text = string.Format(Tr("hud_integrity_siphons"), _integrity.LivingExtractors);
            }
        }

        /// <summary>
        /// Clock tint. A frozen gauge reads slate — the PreBoss seal and a
        /// rewind/scrub pause both stop the drain, and a clock that still looks live
        /// while it is locked is the most misleading thing this widget can do.
        /// Otherwise an active siphon reads red and the tier drives the rest.
        /// </summary>
        private Color IntegrityTint() {
            if (_integrityKnown && _integrity.Frozen) return UIPalette.SlateDim;
            if (_integrityKnown && _integrity.LivingExtractors > 0) return UIPalette.BossRed;
            float percent = _integrityKnown ? _integrity.Percent : 100f;
            if (percent < FTT.Core.TimelineIntegrityRules.StabilizedThreshold) return UIPalette.Warning;
            return UIPalette.Cyan;
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
            if (delta > 0f && _timeFreezeSeconds > 0f) {
                _timeFreezeSeconds = Mathf.Max(0f, _timeFreezeSeconds - delta);
            }
            bool thawWarning = _timeFreezeState == TimeFreezeState.Active
                && _timeFreezeSeconds > 0f && _timeFreezeSeconds <= ThawWarningSeconds;
            Color tint = _timeFreezeState switch {
                TimeFreezeState.Active => thawWarning ? UIPalette.Warning : UIPalette.Cyan,
                TimeFreezeState.Cooldown => UIPalette.TextDisabled,
                _ => UIPalette.Cyan
            };
            if (_freezeIcon != null) _freezeIcon.AddThemeColorOverride("font_color", tint);
            if (_freezeRemainingText == null) return;
            _freezeRemainingText.AddThemeColorOverride("font_color", tint);
            _freezeRemainingText.Text = _timeFreezeState switch {
                TimeFreezeState.Ready => Tr("hud_time_freeze_ready"),
                TimeFreezeState.Active => string.Format(
                    Tr(thawWarning ? "hud_time_freeze_thaw" : "hud_time_freeze_active"),
                    Mathf.CeilToInt(_timeFreezeSeconds)),
                _ => string.Format(Tr("hud_time_freeze_cooldown"), Mathf.CeilToInt(_timeFreezeSeconds))
            };
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
                return;
            }
            if (max != _anchorPips.Length) RebuildAnchorPips(max);
            int lit = Mathf.Clamp(payload.Charges, 0, _anchorPips.Length);
            for (int index = 0; index < _anchorPips.Length; index++) {
                _anchorPips[index].Modulate = new Color(1f, 1f, 1f, index < lit ? 1f : 0.18f);
            }
        }

        private void OnDefySealChanged(DefySealPayload payload) {
            if (payload.PlayerIndex != 0) return;
            _defySealState = payload.State;
            RefreshDefySeal();
        }

        /// <summary>
        /// F13's seal: shape and fill distinguish the four states, not colour alone,
        /// and nothing pulses. Spent survives any later refill — the seal changes
        /// only when the authoritative state does.
        /// </summary>
        private void RefreshDefySeal() {
            if (_defySeal == null) return;
            _defySeal.Text = DefySealModel.Glyph(_defySealState);
            _defySeal.TooltipText = Tr(DefySealModel.LabelKey(_defySealState));
            _defySeal.AddThemeColorOverride("font_color", DefySealModel.Tint(_defySealState));
        }

        private void OnRallyEchoChanged(RallyEchoPayload payload) {
            if (payload.PlayerIndex != 0 || _echoBand == null) return;
            // The band geometry still comes from the shared FighterHudModel
            // arithmetic in RefreshEchoBand; this event carries the reclaim flash,
            // which has no polled equivalent.
            if (!payload.ReclaimFlash) return;
            Color flash = UIPalette.GoldBright;
            flash.A = 0.9f;
            _echoBand.Color = flash;
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
            _dustFloat.Text = string.Format(Tr("hud_dust_pickup"), amount);
            _dustFloat.Visible = true;
            _dustFloat.Modulate = new Color(1f, 1f, 1f, 1f);
            _dustFloatTimer = DustFloatSeconds;
        }

        private void TickDustFloat(float delta) {
            if (_dustFloat == null || _dustFloatTimer <= 0f) return;
            _dustFloatTimer -= delta;
            if (_dustFloatTimer <= 0f) {
                _dustFloatTimer = 0f;
                _dustFloat.Visible = false;
                return;
            }
            _dustFloat.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_dustFloatTimer / DustFloatSeconds, 0f, 1f));
        }

        private void OnDustAwardCollected(DustAwardCollectedPayload payload) => ShowDustFloat(payload.Amount);

        private void OnChronalDustCollected(int amount) => ShowDustFloat(amount);

        /// <summary>V7 boss intro ritual: the localized name card over the
        /// establishing beat, before the bar sweeps in.</summary>
        public void ShowBossIntroCard(string nameKey, float seconds) {
            if (_bossIntroCard == null) return;
            _bossIntroCard.Text = Tr(string.IsNullOrWhiteSpace(nameKey) ? "boss" : nameKey);
            _bossIntroCard.Visible = true;
            _bossIntroCardTimer = Mathf.Max(0.5f, seconds);
        }

        /// <summary>
        /// Positions the Rally echo band from the live player's pool each frame
        /// (the drain is continuous, so events would spam). Reuses the shared
        /// FighterHudModel band arithmetic so both modes agree on the geometry.
        /// </summary>
        private void RefreshEchoBand() {
            if (_echoBand == null) return;
            FTT.Characters.PlayerController player = ResolveStoryPlayer();
            if (player == null) {
                _echoBand.Visible = false;
                return;
            }
            float band = FighterHudModel.EchoBandFraction(
                player.CurrentHP, player.EchoPool, player.MaximumHP);
            _echoBand.Visible = band > 0f;
            if (band <= 0f) return;
            float fill = FighterHudModel.BarFraction(player.CurrentHP, player.MaximumHP);
            _echoBand.AnchorLeft = fill;
            _echoBand.AnchorRight = fill + band;
            _echoBand.OffsetLeft = 0f;
            _echoBand.OffsetRight = 0f;
            // Decay a reclaim flash back to the resting band tint.
            Color resting = UIPalette.GoldBright;
            resting.A = 0.55f;
            if (_echoBand.Color.A > resting.A) {
                _echoBand.Color = _echoBand.Color.Lerp(resting, 0.15f);
            }
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
            _bossPanel.Visible = true;
            if (_bossNameLabel != null) _bossNameLabel.Text = Tr(bossNameKey);
            if (_bossBar != null) {
                _bossBar.MaxValue = maximumHP;
                _bossBar.Value = currentHP;
            }
            _bossNotches?.SetThresholds(phaseThresholds);
        }

        public void UpdateBossHP(int currentHP) {
            if (_bossBar != null) _bossBar.Value = currentHP;
        }

        public void HideBossBar() {
            if (_bossPanel != null) _bossPanel.Visible = false;
            _bossNotches?.Clear();
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

        private void OnBossPhaseChanged(int phaseIndex) => _bossNotches?.NotifyPhaseChanged(phaseIndex);

        private void OnBlockChargesChanged(FTT.Core.BlockChargesPayload payload) {
            if (payload.PlayerIndex != 0) return;
            // The published max is BlockSystem.MaxCharges, which CharacterFactory
            // sets from PlayerController.MaximumBlockCharges — base capacity plus
            // the Story-only Resonance bonus. Capacity changes rebuild the row.
            if (payload.MaxCharges != _blockPips.Length) RebuildBlockPips(payload.MaxCharges);
            SetBlockPips(payload.CurrentCharges);
        }

        private void OnCheckpointReached(string checkpointID) {
            if (_checkpointToast == null) return;
            _checkpointToast.Text = Tr("hud_checkpoint_reached");
            _checkpointToast.Visible = true;
            _checkpointToastTimer = 2.5f;
        }

        private void UpdateHP(int currentHP, int maximumHP) {
            if (_hpBar != null) {
                _hpBar.MaxValue = maximumHP;
                _hpBar.Value = currentHP;
            }
            if (_hpText != null) _hpText.Text = $"{Tr("hud_hp")} {currentHP}/{maximumHP}";
        }

        private void UpdateMeter(float value) {
            if (_meterFill == null) return;
            _meterFill.Fraction = Mathf.Clamp(value, 0f, 100f) / 100f;
            _meterFill.FillColor = value >= HudAbilityIndicatorModel.UltimateReadyMeter
                ? UIPalette.GoldBright
                : UIPalette.Cyan;
        }

        private void RefreshCounters(bool force) {
            var storyManager = FTT.Core.StoryManager.Instance;
            if (storyManager == null) return;
            int rewinds = storyManager.ChronalRewindsRemaining;
            if ((force || rewinds != _lastRewinds) && _rewindCountText != null) {
                _rewindCountText.Text = string.Format(Tr("hud_rewinds"), rewinds);
                // Red at one, an empty hourglass at zero — and deliberately no
                // alarm there: running out is a state, not an event.
                Color tint = rewinds <= 0 ? UIPalette.TextDisabled
                    : rewinds == 1 ? UIPalette.BossRed
                    : new Color(0.6f, 0.9f, 1f);
                _rewindCountText.AddThemeColorOverride("font_color", tint);
                if (_rewindIcon != null) {
                    _rewindIcon.AddThemeColorOverride("font_color", tint);
                    _rewindIcon.Modulate = new Color(1f, 1f, 1f, rewinds <= 0 ? 0.35f : 1f);
                }
                _lastRewinds = rewinds;
            }
            int dust = storyManager.ChronalDustCollected;
            if ((force || dust != _lastDust) && _dustText != null) {
                // HUD_CONTRACT: this is the level's UNDEPOSITED balance, bound to
                // the committed wallet. The Repository/Beacon shows spendable dust
                // separately — a combined balance would imply the player can spend
                // what they are still carrying.
                _dustText.Text = string.Format(Tr("hud_level_dust"), dust);
                _lastDust = dust;
            }
        }

        /// <summary>
        /// Repaints the three cooldown rings, their lock overlays, the Ultimate lock
        /// overlay and both status slots from the model.
        /// </summary>
        private void RefreshIndicators() {
            for (int index = 0; index < CooldownSlots.Length; index++) {
                FTT.Core.AbilitySlot slot = CooldownSlots[index];
                AbilitySlotLockState lockState = _indicators.LockState(slot);
                RadialProgress radial = _cooldownRadials[index];
                if (radial != null) {
                    radial.Fraction = _indicators.ReadinessFraction(slot);
                    radial.FillColor = lockState == AbilitySlotLockState.Clear
                        ? UIPalette.Cyan
                        : UIPalette.TextDisabled;
                }
                Label icon = _cooldownIcons[index];
                if (icon != null) {
                    icon.AddThemeColorOverride(
                        "font_color",
                        _indicators.IsReady(slot) ? UIPalette.TextAccent : UIPalette.TextDisabled);
                }
                ApplyLockOverlay(_cooldownLockOverlays[index], lockState);
            }
            ApplyLockOverlay(_meterLockOverlay, _indicators.LockState(FTT.Core.AbilitySlot.Ultimate));
            RefreshStatusSlot(_damageStatusIndicator, _damageStatusRadial, damageSlot: true);
            RefreshStatusSlot(_controlStatusIndicator, _controlStatusRadial, damageSlot: false);
        }

        /// <summary>
        /// V7.5 slot locks. A locked slot is <b>shown</b>, never hidden: Dormant is
        /// a dim star for a legacy the hero has not recovered yet, Suppressed a cold
        /// cross-out for one an Eraser took away. Clear removes the overlay.
        /// </summary>
        private void ApplyLockOverlay(Label overlay, AbilitySlotLockState state) {
            if (overlay == null) return;
            if (state == AbilitySlotLockState.Clear) {
                overlay.Visible = false;
                return;
            }
            overlay.Visible = true;
            overlay.Text = HudAbilityIndicatorModel.LockGlyph(state);
            overlay.TooltipText = Tr(HudAbilityIndicatorModel.LockLabelKey(state));
            overlay.AddThemeColorOverride(
                "font_color",
                state == AbilitySlotLockState.Dormant ? UIPalette.SlateDim : UIPalette.TemporalViolet);
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
            _blockPips = RebuildPipRow(_blockPipRow, Mathf.Max(1, capacity), BlockPipSize, UIPalette.Cyan);
        }

        private void RebuildAnchorPips(int capacity) {
            _anchorPips = RebuildPipRow(_anchorPipRow, capacity, AnchorPipSize, UIPalette.GoldBright);
        }

        private static ColorRect[] RebuildPipRow(HBoxContainer row, int count, int size, Color color) {
            if (row == null) return System.Array.Empty<ColorRect>();
            Godot.Collections.Array<Node> children = row.GetChildren();
            using (children.AsDisposable()) {
                foreach (Node child in children) {
                    row.RemoveChild(child);
                    child.Free();
                }
            }
            if (count <= 0) return System.Array.Empty<ColorRect>();
            var pips = new ColorRect[count];
            for (int index = 0; index < count; index++) {
                var pip = new ColorRect {
                    Name = $"Pip{index}",
                    Color = color,
                    CustomMinimumSize = new Vector2(size, size),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                row.AddChild(pip);
                pips[index] = pip;
            }
            return pips;
        }

        /// <summary>A spent pip dims rather than disappearing, so capacity stays readable.</summary>
        private void SetBlockPips(int filled) {
            int lit = Mathf.Clamp(filled, 0, _blockPips.Length);
            for (int index = 0; index < _blockPips.Length; index++) {
                _blockPips[index].Modulate = new Color(1f, 1f, 1f, index < lit ? 1f : 0.18f);
            }
        }

        // === UI resolution ===

        private const string TopLeftPanelPath = "SafeArea/TopLeft_Panel";
        private const string VitalsPath = "SafeArea/TopLeft_Panel/Vitals";
        private const string TopRightPanelPath = "SafeArea/TopRight_Panel";

        private void ResolveUI() {
            _root = GetNodeOrNull<Control>("SafeArea");
            if (_root == null) return;
            // Unscaled fonts: the whole HUD layout scales through _uiScale
            // instead, so the shared (scale-mutated) theme would double-scale.
            _root.Theme ??= UIPalette.NewUnscaledTheme();

            _levelTitleLabel = GetNodeOrNull<Label>("SafeArea/TopLeft/LevelTitle");
            _objectiveLabel = GetNodeOrNull<Label>("SafeArea/TopLeft/Objective");
            _portrait = GetNodeOrNull<TextureRect>($"{TopLeftPanelPath}/PlayerPortrait");

            _hpBar = GetNodeOrNull<ProgressBar>($"{VitalsPath}/HealthBar_BG");
            _echoBand = GetNodeOrNull<ColorRect>($"{VitalsPath}/HealthBar_BG/RallyEcho_Band");
            _hpText = GetNodeOrNull<Label>($"{VitalsPath}/HPText");
            _blockPipRow = GetNodeOrNull<HBoxContainer>($"{VitalsPath}/BlockCharges_Panel");

            _damageStatusIndicator =
                GetNodeOrNull<Label>($"{VitalsPath}/StatusEffects_Panel/DamageStatus_Indicator");
            _damageStatusRadial = GetNodeOrNull<RadialProgress>(
                $"{VitalsPath}/StatusEffects_Panel/DamageStatus_Indicator/DamageStatus_DurationRadial");
            _controlStatusIndicator =
                GetNodeOrNull<Label>($"{VitalsPath}/StatusEffects_Panel/ControlStatus_Indicator");
            _controlStatusRadial = GetNodeOrNull<RadialProgress>(
                $"{VitalsPath}/StatusEffects_Panel/ControlStatus_Indicator/ControlStatus_DurationRadial");

            _meterFill = GetNodeOrNull<RadialProgress>(
                $"{VitalsPath}/MeterRow/UltimateMeter_BG/UltimateMeter_Fill");
            _meterLockOverlay = GetNodeOrNull<Label>(
                $"{VitalsPath}/MeterRow/UltimateMeter_BG/UltimateMeter_LockOverlay");
            _defySeal = GetNodeOrNull<Label>($"{VitalsPath}/MeterRow/DefySeal");

            for (int index = 0; index < CooldownSlots.Length; index++) {
                string iconName = $"Cooldown{index + 1}_Icon";
                _cooldownIcons[index] = GetNodeOrNull<Label>($"{VitalsPath}/CooldownRow/{iconName}");
                _cooldownRadials[index] = GetNodeOrNull<RadialProgress>(
                    $"{VitalsPath}/CooldownRow/{iconName}/Cooldown{index + 1}_Radial");
                _cooldownLockOverlays[index] = GetNodeOrNull<Label>(
                    $"{VitalsPath}/CooldownRow/{iconName}/Cooldown{index + 1}_LockOverlay");
                if (_cooldownIcons[index] != null) {
                    _cooldownIcons[index].Text =
                        Tr(HudAbilityIndicatorModel.SlotLabelKey(CooldownSlots[index]));
                }
            }

            _rewindIcon = GetNodeOrNull<Label>($"{VitalsPath}/TemporalRow/RewindCounter/RewindIcon");
            _rewindCountText = GetNodeOrNull<Label>($"{VitalsPath}/TemporalRow/RewindCounter/RewindCountText");
            _timeFreezeIndicator = GetNodeOrNull<Control>($"{VitalsPath}/TemporalRow/TimeFreezeIndicator");
            _freezeIcon = GetNodeOrNull<Label>($"{VitalsPath}/TemporalRow/TimeFreezeIndicator/FreezeIcon");
            _freezeRemainingText =
                GetNodeOrNull<Label>($"{VitalsPath}/TemporalRow/TimeFreezeIndicator/FreezeRemainingText");
            _beaconAnchors = GetNodeOrNull<Control>($"{VitalsPath}/TemporalRow/BeaconAnchors");
            _anchorPipRow = GetNodeOrNull<HBoxContainer>($"{VitalsPath}/TemporalRow/BeaconAnchors/AnchorPips");

            _dustText = GetNodeOrNull<Label>($"{TopRightPanelPath}/CurrencyContainer/ChronalDustText");
            _dustFloat = GetNodeOrNull<Label>($"{TopRightPanelPath}/CurrencyContainer/DustPickup_Float");
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
        }
    }
}
