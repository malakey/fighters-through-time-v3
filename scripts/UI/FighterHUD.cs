using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;

namespace FTT.UI {

    /// <summary>
    /// Package 8 B2. The production Fighter Mode HUD: per-player vitals panels
    /// (portrait, name, HP, Influence meter, stock pips, shield-charge pips,
    /// cooldown slots, active status) and a centred match clock.
    ///
    /// It replaces <c>TestArenaHUD</c>'s four debug text labels as the thing a
    /// player actually reads. The debug labels are not deleted — they stay in the
    /// stage scenes as a toggleable developer layer (see
    /// <see cref="TestArenaHUD"/>) — because their state/tick/hash line is the only
    /// in-game view of deterministic simulation state.
    ///
    /// The HUD is attached by <c>FighterSimulationDriver</c> rather than authored
    /// into each stage, so all ten production stages and the Test Arena get it
    /// from one place.
    ///
    /// Presentation only: it reads component values the driver already pulled and
    /// writes nothing back. <see cref="ApplyPlayerState"/> and
    /// <see cref="ApplyMatchState"/> take those values as parameters rather than
    /// reaching into the simulation themselves, which is also what lets a test
    /// drive the whole surface from hand-built structs.
    /// </summary>
    public partial class FighterHUD : CanvasLayer {

        /// <summary>Authored scene path; the driver instantiates this.</summary>
        public const string ScenePath = "res://scenes/ui/FighterHUD.tscn";

        /// <summary>Cooldown slots rendered per player, in display order.</summary>
        private static readonly FighterCooldownSlot[] Slots = {
            FighterCooldownSlot.SpecialOne,
            FighterCooldownSlot.SpecialTwo,
            FighterCooldownSlot.Movement,
            FighterCooldownSlot.Ultimate
        };

        private const int PipSize = 16;
        private const int PipSpacing = 4;
        private const int CooldownSlotWidth = 56;
        private const int CooldownSlotHeight = 34;

        private sealed class PlayerPanel {
            public Control Root;
            public TextureRect Portrait;
            public Label Name;
            public ProgressBar HP;
            public ColorRect EchoBand;
            public ProgressBar Meter;
            public HBoxContainer StockPips;
            public Label StocksLost;
            public Label StocksLabel;
            public HBoxContainer ShieldPips;
            public HBoxContainer Cooldowns;
            public ColorRect[] Stocks = System.Array.Empty<ColorRect>();
            public ColorRect[] Shields = System.Array.Empty<ColorRect>();
            public Label[] CooldownLabels = System.Array.Empty<Label>();
            public Panel[] CooldownPlates = System.Array.Empty<Panel>();

            // F24: two fixed 24 x 24 status slots, damage left / control right —
            // in that order for BOTH players. Mirroring P2's panel layout is a
            // readability choice; reversing the semantic order of the two slots
            // would make the same icon mean different things per side, which the
            // contract calls out by name.
            public Label DamageStatus;
            public RadialProgress DamageRadial;
            public Label ControlStatus;
            public RadialProgress ControlRadial;

            /// <summary>F13 seal beside this fighter's meter.</summary>
            public Label DefySeal;
            public DefySealState PresentedSeal = (DefySealState)(-1);

            // Presented-value caches. Every widget below is refreshed at 60 Hz, so
            // writing a theme override or a StyleBox unconditionally would allocate
            // on every frame of every match. Diffing keeps the steady state free.
            public int PresentedDamageStatus = -1;
            public int PresentedControlStatus = -1;
            public int PresentedStocksLost = -1;
            // Tri-state (-1 unknown / 0 cooling / 1 ready). A plain bool cannot
            // express "never presented", so the first refresh of a slot that is
            // already cooling would diff as unchanged and leave the plate showing
            // the available treatment.
            public readonly int[] PresentedReady = { -1, -1, -1, -1 };
            public readonly int[] PresentedCooldownFrames = { -1, -1, -1, -1 };
        }

        private Control _root;
        private Label _timer;
        private readonly PlayerPanel[] _panels = new PlayerPanel[2];
        private FighterSimulationDriver _driver;
        private float _appliedOpacity = -1f;
        private readonly UiScaleBinder _uiScale = new();

        // F21/F22 match context. Cached rather than passed to every per-player
        // call: both the stock display and the Defy seal need them, and the driver
        // pulls the match component once per frame.
        private int _matchMode;
        private bool _suddenDeath;
        private int _lastTimerSeconds = -1;
        private bool _timerDangerShown;

        /// <summary>Opacity currently applied to the HUD root. Test seam.</summary>
        public float AppliedOpacity => _appliedOpacity;

        /// <summary>Whether the match clock is on screen. Test seam.</summary>
        public bool TimerVisible => _timer != null && _timer.Visible;

        /// <summary>Rendered clock copy, empty when hidden. Test seam.</summary>
        public string TimerText => _timer != null && _timer.Visible ? _timer.Text : "";

        public override void _Ready() {
            Layer = 12;
            _root = GetNodeOrNull<Control>("SafeArea");
            // The scene attaches the shared theme, but the whole HUD layout
            // scales through _uiScale, so the shared (scale-mutated) theme
            // would double-scale the HUD text: swap in an unscaled copy.
            if (_root != null) {
                Theme unscaled = UIPalette.NewUnscaledTheme();
                if (unscaled != null) _root.Theme = unscaled;
            }
            _timer = GetNodeOrNull<Label>("SafeArea/MatchClock");
            _panels[0] = BindPanel("SafeArea/P1_Status");
            _panels[1] = BindPanel("SafeArea/P2_Status");
            for (int index = 0; index < _panels.Length; index++) {
                BuildCooldownSlots(_panels[index]);
                ConfigurePlayer(index, null, 3, 3);
            }
            if (_timer != null) _timer.Visible = false;
            ApplyOpacity();
            _uiScale.Apply(_root, force: true);
        }

        private PlayerPanel BindPanel(string path) {
            Control panelRoot = GetNodeOrNull<Control>(path);
            if (panelRoot == null) return null;
            var panel = new PlayerPanel {
                Root = panelRoot,
                Portrait = panelRoot.GetNodeOrNull<TextureRect>("Body/Portrait"),
                Name = panelRoot.GetNodeOrNull<Label>("Body/Column/TopRow/Name"),
                HP = panelRoot.GetNodeOrNull<ProgressBar>("Body/Column/HPBar"),
                Meter = panelRoot.GetNodeOrNull<ProgressBar>("Body/Column/MeterBar"),
                StockPips = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/PipRow/Stocks"),
                StocksLost = panelRoot.GetNodeOrNull<Label>("Body/Column/PipRow/StocksLost"),
                StocksLabel = panelRoot.GetNodeOrNull<Label>("Body/Column/PipRow/StocksLabel"),
                ShieldPips = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/PipRow/Shields"),
                Cooldowns = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/Cooldowns"),
                DamageStatus = panelRoot.GetNodeOrNull<Label>(
                    "Body/Column/TopRow/StatusEffects_Panel/DamageStatus_Indicator"),
                DamageRadial = panelRoot.GetNodeOrNull<RadialProgress>(
                    "Body/Column/TopRow/StatusEffects_Panel/DamageStatus_Indicator/DamageStatus_DurationRadial"),
                ControlStatus = panelRoot.GetNodeOrNull<Label>(
                    "Body/Column/TopRow/StatusEffects_Panel/ControlStatus_Indicator"),
                ControlRadial = panelRoot.GetNodeOrNull<RadialProgress>(
                    "Body/Column/TopRow/StatusEffects_Panel/ControlStatus_Indicator/ControlStatus_DurationRadial"),
                DefySeal = panelRoot.GetNodeOrNull<Label>("Body/Column/DefySeal")
            };
            // Rally echo band (V7.1): a gold sliver inside the HP bar, sitting
            // directly above the current fill — the HP this player wins back by
            // landing a direct hit before the drain empties it. Built in code so
            // the authored scene stays untouched.
            if (panel.HP != null) {
                Color echoTint = UIPalette.GoldBright;
                echoTint.A = 0.55f;
                panel.EchoBand = new ColorRect {
                    Name = "EchoBand",
                    Color = echoTint,
                    Visible = false,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                panel.EchoBand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                panel.HP.AddChild(panel.EchoBand);
            }
            return panel;
        }

        /// <summary>
        /// Wires the HUD to a live driver. Called from the driver's UI-attach
        /// block, which is the one place that already holds both presentation
        /// fighters and therefore both <see cref="CharacterData"/> resources.
        /// </summary>
        public void Bind(FighterSimulationDriver driver, PlayerController playerOne, PlayerController playerTwo) {
            _driver = driver;
            int stocks = GameManager.Instance?.CurrentSession.MatchSettings.StockCount ?? 3;
            ConfigurePlayer(0, playerOne?.Data, stocks, playerOne?.Data?.MaxBlockCharges ?? 3);
            ConfigurePlayer(1, playerTwo?.Data, stocks, playerTwo?.Data?.MaxBlockCharges ?? 3);
        }

        /// <summary>
        /// Sets the fixed half of a player's panel: portrait, name, and how many
        /// pips exist. Pip counts are data-driven (stock rules, per-character
        /// shield capacity), which is why they are built here rather than authored
        /// into the scene.
        /// </summary>
        public void ConfigurePlayer(int playerIndex, CharacterData data, int maxStocks, int maxShieldCharges) {
            PlayerPanel panel = PanelFor(playerIndex);
            if (panel == null) return;

            if (panel.Portrait != null) {
                panel.Portrait.Texture = data?.CharacterPortrait;
                panel.Portrait.Visible = data?.CharacterPortrait != null;
            }
            if (panel.Name != null) {
                string key = data?.DisplayNameKey ?? "";
                panel.Name.Text = string.IsNullOrWhiteSpace(key)
                    ? string.Format(Tr("fighter_hud_player"), playerIndex + 1)
                    : Tr(key);
            }

            panel.Stocks = RebuildPips(panel.StockPips, Mathf.Max(1, maxStocks), UIPalette.GoldBright);
            panel.Shields = RebuildPips(panel.ShieldPips, Mathf.Max(1, maxShieldCharges), UIPalette.Cyan);
        }

        /// <summary>
        /// Mirrors one player's deterministic component values onto the panel.
        /// Takes the values rather than a player id so the surface can be driven
        /// from a test without a simulation.
        /// </summary>
        public void ApplyPlayerState(
            int playerIndex,
            in FighterStateComponent state,
            in FighterRuntimeComponent runtime,
            int tickRate,
            float echoPool = 0f) {

            PlayerPanel panel = PanelFor(playerIndex);
            if (panel == null) return;

            if (panel.HP != null) {
                panel.HP.MaxValue = Mathf.Max(1, state.MaxHP);
                panel.HP.Value = Mathf.Clamp(state.CurrentHP, 0, Mathf.Max(1, state.MaxHP));
                TintFill(panel.HP, FighterHudModel.HpFillColor(state.CurrentHP, state.MaxHP));
                if (panel.EchoBand != null) {
                    float band = FighterHudModel.EchoBandFraction(state.CurrentHP, echoPool, state.MaxHP);
                    panel.EchoBand.Visible = band > 0f;
                    if (band > 0f) {
                        float fill = FighterHudModel.BarFraction(state.CurrentHP, state.MaxHP);
                        panel.EchoBand.AnchorLeft = fill;
                        panel.EchoBand.AnchorRight = fill + band;
                        panel.EchoBand.OffsetLeft = 0f;
                        panel.EchoBand.OffsetRight = 0f;
                    }
                }
            }
            if (panel.Meter != null) {
                panel.Meter.MaxValue = FighterHudModel.MaxInfluence;
                panel.Meter.Value = Mathf.Clamp(state.Influence.ToFloat(), 0f, FighterHudModel.MaxInfluence);
            }

            SetPips(panel.Stocks, state.Stocks);
            SetPips(panel.Shields, state.BlockCharges);
            ApplyStockDisplay(panel, in state, in runtime);

            // F24: both slots render simultaneously, at fixed positions, never
            // collapsed to a single "winning" icon. The data was always there —
            // the runtime component bit-packs both — and PresentedStatusType was
            // only ever a single-pip collapse helper.
            ApplyStatusSlot(
                panel.DamageStatus, panel.DamageRadial,
                (StatusType)runtime.DamageStatusType, runtime.DamageStatusFrames,
                tickRate, ref panel.PresentedDamageStatus);
            ApplyStatusSlot(
                panel.ControlStatus, panel.ControlRadial,
                (StatusType)runtime.StatusType, runtime.StatusFrames,
                tickRate, ref panel.PresentedControlStatus);

            ApplyDefySeal(panel, in state, playerIndex);
            UpdateCooldowns(panel, in state, in runtime, tickRate);
        }

        /// <summary>
        /// F24's per-slot renderer. An empty slot hides its glyph and radial but
        /// keeps its reserved position, because the panel is fixed-position — the
        /// whole point of the contract's "hiding one TextureRect does not shift the
        /// other".
        ///
        /// <para>The radial reads the authoritative <c>StatusFrames</c> the
        /// simulation owns, so a paused or frozen match stops it without the HUD
        /// having to know that happened.</para>
        /// </summary>
        private void ApplyStatusSlot(
            Label indicator, RadialProgress radial,
            StatusType status, int remainingFrames, int tickRate, ref int presented) {

            if (indicator == null) return;
            if (radial != null) {
                radial.Visible = status != StatusType.None;
                if (status != StatusType.None) {
                    radial.Fraction = FighterHudModel.StatusRadialFraction(remainingFrames, tickRate);
                }
            }
            if (presented == (int)status) return;
            presented = (int)status;
            if (status == StatusType.None) {
                indicator.Text = "";
                indicator.TooltipText = "";
                return;
            }
            // Glyph plus localized name: colour alone must not identify an effect.
            indicator.Text = HudAbilityIndicatorModel.StatusGlyph(status);
            indicator.TooltipText = Tr(FighterHudModel.StatusKey(status));
            // Same authored colour the glow arbiter puts on the fighter, so the
            // slot and the outline can never name different statuses.
            indicator.AddThemeColorOverride(
                "font_color", FTT.Combat.GlowPalette.Status(status).OutlineColor);
        }

        /// <summary>
        /// F21's stock display. Stock mode keeps the finite pips; Time mode has
        /// unlimited respawns, so the pips would be a lie — they are replaced by a
        /// "Stocks lost: N" label reading the victim-side counter the simulation
        /// already increments for every cause of death.
        ///
        /// <para>Lower is better there, which is exactly why the contract forbids
        /// labelling it KOs scored, points or remaining lives.</para>
        /// </summary>
        private void ApplyStockDisplay(
            PlayerPanel panel, in FighterStateComponent state, in FighterRuntimeComponent runtime) {
            bool timeMode = FighterHudModel.UsesStocksLostDisplay(_matchMode);
            if (panel.StockPips != null) panel.StockPips.Visible = !timeMode;
            if (panel.StocksLabel != null) panel.StocksLabel.Visible = !timeMode;
            if (panel.StocksLost == null) return;
            panel.StocksLost.Visible = timeMode;
            if (!timeMode) return;
            int lost = Mathf.Max(0, runtime.KnockoutsSuffered);
            if (panel.PresentedStocksLost == lost) return;
            panel.PresentedStocksLost = lost;
            panel.StocksLost.Text = string.Format(Tr("fighter_hud_stocks_lost"), lost);
        }

        /// <summary>
        /// F13's seal, mirrored on both fighters. Derived after the gameplay update
        /// from meter, the authoritative used flag, life state and the Sudden Death
        /// phase — never from a cosmetic flag of its own, so a rollback that
        /// restores an unspent Defy restores an intact seal for free.
        /// </summary>
        private void ApplyDefySeal(PlayerPanel panel, in FighterStateComponent state, int playerIndex) {
            if (panel.DefySeal == null) return;
            bool used = false;
            if (_driver != null && IsInstanceValid(_driver)
                && _driver.TryGetVerb(playerIndex, out FighterVerbComponent verb)) {
                used = verb.DefyHistoryUsed != 0;
            }
            DefySealState seal = DefySealModel.Resolve(
                state.Influence.ToFloat(), used, state.CurrentHP > 0, _suddenDeath);
            if (panel.PresentedSeal == seal) return;
            panel.PresentedSeal = seal;
            panel.DefySeal.Text = DefySealModel.Glyph(seal);
            panel.DefySeal.TooltipText = Tr(DefySealModel.LabelKey(seal));
            panel.DefySeal.AddThemeColorOverride("font_color", DefySealModel.Tint(seal));
        }

        /// <summary>Applies match-level state: clock visibility and remaining time.</summary>
        public void ApplyMatchState(bool timerEnabled, int remainingFrames, int tickRate) =>
            ApplyMatchState(timerEnabled, remainingFrames, tickRate, _matchMode, _suddenDeath);

        /// <summary>
        /// The full match-state surface. Adds F21's mode (which decides the stock
        /// display) and F22's phase, which replaces the running clock outright:
        /// "Sudden Death — next death loses" is the whole readout, because there is
        /// no timer left to count.
        ///
        /// <para>The last ten seconds of regulation pulse red and chime once. The
        /// chime is a single edge, not a per-frame call — a timer cue that
        /// retriggers every frame is the classic way to turn a warning into
        /// noise — and it goes out on the Critical Cues path so a background
        /// profile cannot muffle it.</para>
        /// </summary>
        public void ApplyMatchState(
            bool timerEnabled, int remainingFrames, int tickRate, int matchMode, bool suddenDeath) {
            _matchMode = matchMode;
            _suddenDeath = suddenDeath;
            if (_timer == null) return;

            if (suddenDeath) {
                _timer.Visible = true;
                _timer.Text = Tr("fighter_hud_sudden_death_clock");
                _timer.AddThemeColorOverride("font_color", UIPalette.BossRed);
                _lastTimerSeconds = -1;
                return;
            }

            bool visible = FighterHudModel.TimerIsVisible(timerEnabled);
            _timer.Visible = visible;
            if (!visible) {
                _lastTimerSeconds = -1;
                return;
            }
            int seconds = FighterHudModel.TotalSeconds(remainingFrames, tickRate);
            _timer.Text = string.Format(
                Tr("fighter_hud_clock"),
                FighterHudModel.ClockMinutes(remainingFrames, tickRate),
                FighterHudModel.ClockSeconds(remainingFrames, tickRate).ToString("D2"));
            bool danger = FighterHudModel.IsTimerDanger(seconds);
            if (danger != _timerDangerShown) {
                _timerDangerShown = danger;
                _timer.AddThemeColorOverride("font_color", danger ? UIPalette.BossRed : UIPalette.Gold);
            }
            if (danger && seconds != _lastTimerSeconds && seconds == FighterHudModel.TimerDangerSeconds) {
                // C01b: a timer-danger cue is a protected warning, so it rides the
                // clear Critical Cues path rather than the background World SFX.
                AudioManager.Instance?.PlayCriticalCue(null);
            }
            _lastTimerSeconds = seconds;
        }

        public override void _Process(double delta) {
            ApplyOpacity();
            _uiScale.Apply(_root);
            if (_driver == null || !IsInstanceValid(_driver) || _driver.Simulation == null) return;

            int tickRate = FighterSimulation.TickRate;
            // Match context first: the per-player panels read the mode (stock
            // display) and the Sudden Death phase (Defy seal) from it.
            FighterMatchComponent matchState = _driver.Simulation.GetMatchState();
            _matchMode = matchState.MatchMode;
            _suddenDeath = matchState.SuddenDeathActive != 0;
            for (int playerID = 0; playerID < 2; playerID++) {
                if (!_driver.TryGetFighter(playerID, out FighterStateComponent state)
                    || !_driver.TryGetRuntime(playerID, out FighterRuntimeComponent runtime)) continue;
                float echoPool = _driver.TryGetVerb(playerID, out FighterVerbComponent verb)
                    ? verb.EchoPool.ToFloat()
                    : 0f;
                ApplyPlayerState(playerID, in state, in runtime, tickRate, echoPool);
            }

            ApplyMatchState(
                matchState.TimerEnabled == 1, matchState.RemainingFrames, tickRate,
                matchState.MatchMode, matchState.SuddenDeathActive != 0);
        }

        /// <summary>
        /// Reads <c>HudOpacity</c> every frame rather than once in
        /// <c>_Ready</c>. §2.10: the accessibility settings are live — a player who
        /// lowers HUD opacity from the in-match pause menu must see it take effect
        /// without restarting the match.
        /// </summary>
        private void ApplyOpacity() {
            float target = FighterHudModel.ResolveOpacity(
                SaveManager.Instance?.GlobalData?.HudOpacity ?? 1f);
            if (Mathf.IsEqualApprox(target, _appliedOpacity)) return;
            _appliedOpacity = target;
            if (_root != null) _root.Modulate = new Color(1f, 1f, 1f, target);
        }

        // ---- Test seams -----------------------------------------------------

        /// <summary>HP bar fill in 0..1 for a player. Test seam.</summary>
        public float HPFraction(int playerIndex) {
            ProgressBar bar = PanelFor(playerIndex)?.HP;
            return bar == null || bar.MaxValue <= 0 ? 0f : (float)(bar.Value / bar.MaxValue);
        }

        /// <summary>Rally echo band width in 0..1 of the HP bar, 0 when hidden. Test seam.</summary>
        public float EchoBandWidth(int playerIndex) {
            ColorRect band = PanelFor(playerIndex)?.EchoBand;
            if (band == null || !band.Visible) return 0f;
            return Mathf.Max(0f, band.AnchorRight - band.AnchorLeft);
        }

        /// <summary>Meter fill in 0..1 for a player. Test seam.</summary>
        public float MeterFraction(int playerIndex) {
            ProgressBar bar = PanelFor(playerIndex)?.Meter;
            return bar == null || bar.MaxValue <= 0 ? 0f : (float)(bar.Value / bar.MaxValue);
        }

        /// <summary>Lit stock pips for a player. Test seam.</summary>
        public int LitStockPips(int playerIndex) => CountLit(PanelFor(playerIndex)?.Stocks);

        /// <summary>Lit shield-charge pips for a player. Test seam.</summary>
        public int LitShieldPips(int playerIndex) => CountLit(PanelFor(playerIndex)?.Shields);

        /// <summary>Total authored stock pips for a player. Test seam.</summary>
        public int StockPipCapacity(int playerIndex) => PanelFor(playerIndex)?.Stocks.Length ?? 0;

        /// <summary>Total authored shield pips for a player. Test seam.</summary>
        public int ShieldPipCapacity(int playerIndex) => PanelFor(playerIndex)?.Shields.Length ?? 0;

        /// <summary>Text rendered in a cooldown slot. Test seam.</summary>
        public string CooldownText(int playerIndex, FighterCooldownSlot slot) {
            PlayerPanel panel = PanelFor(playerIndex);
            int index = System.Array.IndexOf(Slots, slot);
            if (panel == null || index < 0 || index >= panel.CooldownLabels.Length) return "";
            return panel.CooldownLabels[index].Text;
        }

        /// <summary>Whether a cooldown slot reads as available. Test seam.</summary>
        public bool CooldownIsReady(int playerIndex, FighterCooldownSlot slot) {
            PlayerPanel panel = PanelFor(playerIndex);
            int index = System.Array.IndexOf(Slots, slot);
            if (panel == null || index < 0 || index >= panel.CooldownPlates.Length) return false;
            return panel.CooldownPlates[index].Modulate.A >= 1f;
        }

        /// <summary>
        /// Localized name in one status slot, empty when the slot is free. Test
        /// seam. Reads the tooltip rather than the glyph because the glyph is
        /// deliberately not a translatable string.
        /// </summary>
        public string StatusText(int playerIndex, bool damageSlot) {
            PlayerPanel panel = PanelFor(playerIndex);
            Label slot = damageSlot ? panel?.DamageStatus : panel?.ControlStatus;
            return slot == null ? "" : slot.TooltipText;
        }

        /// <summary>
        /// Whether a status slot's glyph is currently drawn. An empty slot renders
        /// nothing but keeps its reserved position, so this reports the glyph, not
        /// the node's visibility. Test seam.
        /// </summary>
        public bool StatusSlotOccupied(int playerIndex, bool damageSlot) {
            PlayerPanel panel = PanelFor(playerIndex);
            Label slot = damageSlot ? panel?.DamageStatus : panel?.ControlStatus;
            return slot != null && !string.IsNullOrEmpty(slot.Text);
        }

        /// <summary>
        /// A status slot's index inside its fixed-position panel. This — not a
        /// pixel position, which is zero until a layout pass runs — is what makes
        /// "damage left, control right" structurally true, and it is the same
        /// order for both players by contract. Test seam.
        /// </summary>
        public int StatusSlotIndex(int playerIndex, bool damageSlot) {
            PlayerPanel panel = PanelFor(playerIndex);
            Label slot = damageSlot ? panel?.DamageStatus : panel?.ControlStatus;
            return slot?.GetIndex() ?? -1;
        }

        /// <summary>
        /// Whether a status slot still occupies its reserved space. F24: "Hide an
        /// empty slot's icon/radial while retaining its reserved position", so this
        /// stays true whether or not the slot holds a status — hiding the NODE is
        /// what would let the surviving indicator slide across. Test seam.
        /// </summary>
        public bool StatusSlotReserved(int playerIndex, bool damageSlot) {
            PlayerPanel panel = PanelFor(playerIndex);
            Label slot = damageSlot ? panel?.DamageStatus : panel?.ControlStatus;
            return slot != null && slot.Visible && slot.CustomMinimumSize.X >= 24f;
        }

        /// <summary>The Defy seal state drawn for a fighter. Test seam.</summary>
        public DefySealState DefySealFor(int playerIndex) =>
            PanelFor(playerIndex)?.PresentedSeal ?? DefySealState.Building;

        /// <summary>Whether the finite stock pips are on screen for a fighter. Test seam.</summary>
        public bool StockPipsVisible(int playerIndex) =>
            PanelFor(playerIndex)?.StockPips?.Visible ?? false;

        /// <summary>The F21 "Stocks lost: N" copy, empty when hidden. Test seam.</summary>
        public string StocksLostText(int playerIndex) {
            Label label = PanelFor(playerIndex)?.StocksLost;
            return label != null && label.Visible ? label.Text : "";
        }

        // ---- Construction helpers -------------------------------------------

        private PlayerPanel PanelFor(int playerIndex) =>
            playerIndex >= 0 && playerIndex < _panels.Length ? _panels[playerIndex] : null;

        /// <summary>
        /// Frees a container's children immediately rather than queueing them.
        /// A queued child is detached-but-alive until the frame ends, which is
        /// exactly the orphan state the pip rebuild would leave behind on every
        /// reconfigure — and the array wrapper is disposed deterministically per
        /// the repository's collection rule.
        /// </summary>
        private static void ClearChildren(Node container) {
            Godot.Collections.Array<Node> children = container.GetChildren();
            using var lifetime = children.AsDisposable();
            foreach (Node child in children) {
                container.RemoveChild(child);
                child.Free();
            }
        }

        private static int CountLit(ColorRect[] pips) {
            if (pips == null) return 0;
            int lit = 0;
            foreach (ColorRect pip in pips) {
                if (pip.Modulate.A >= 1f) lit++;
            }
            return lit;
        }

        private static ColorRect[] RebuildPips(HBoxContainer container, int count, Color color) {
            if (container == null) return System.Array.Empty<ColorRect>();
            ClearChildren(container);
            container.AddThemeConstantOverride("separation", PipSpacing);
            var pips = new ColorRect[count];
            for (int index = 0; index < count; index++) {
                var pip = new ColorRect {
                    Name = $"Pip{index}",
                    Color = color,
                    CustomMinimumSize = new Vector2(PipSize, PipSize),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                container.AddChild(pip);
                pips[index] = pip;
            }
            return pips;
        }

        /// <summary>
        /// A spent pip is dimmed rather than removed, so the panel never reflows
        /// mid-match and the player can still see the capacity they started with.
        /// </summary>
        private static void SetPips(ColorRect[] pips, int filled) {
            if (pips == null) return;
            int lit = FighterHudModel.FilledPips(filled, pips.Length);
            for (int index = 0; index < pips.Length; index++) {
                pips[index].Modulate = new Color(1f, 1f, 1f, index < lit ? 1f : 0.18f);
            }
        }

        private void BuildCooldownSlots(PlayerPanel panel) {
            if (panel?.Cooldowns == null) return;
            ClearChildren(panel.Cooldowns);
            panel.Cooldowns.AddThemeConstantOverride("separation", 6);
            var plates = new Panel[Slots.Length];
            var labels = new Label[Slots.Length];
            for (int index = 0; index < Slots.Length; index++) {
                var plate = new Panel {
                    Name = $"Slot{index}",
                    CustomMinimumSize = new Vector2(CooldownSlotWidth, CooldownSlotHeight),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                var label = new Label {
                    Name = "Value",
                    Text = Tr(FighterHudModel.CooldownGlyphKey(Slots[index])),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                label.AddThemeFontSizeOverride("font_size", UIPalette.SmallFontSize);
                plate.AddChild(label);
                panel.Cooldowns.AddChild(plate);
                plates[index] = plate;
                labels[index] = label;
            }
            panel.CooldownPlates = plates;
            panel.CooldownLabels = labels;
        }

        private static void UpdateCooldowns(
            PlayerPanel panel,
            in FighterStateComponent state,
            in FighterRuntimeComponent runtime,
            int tickRate) {

            if (panel.CooldownLabels.Length != Slots.Length) return;
            for (int index = 0; index < Slots.Length; index++) {
                FighterCooldownSlot slot = Slots[index];
                bool ready;
                int frames = 0;
                if (slot == FighterCooldownSlot.Ultimate) {
                    // The ultimate is meter-gated, not cooldown-gated: a full
                    // Influence meter is what makes the slot available.
                    ready = FighterHudModel.UltimateIsReady(state.Influence.ToFloat());
                } else {
                    frames = slot switch {
                        FighterCooldownSlot.SpecialOne => runtime.SpecialOneCooldownFrames,
                        FighterCooldownSlot.SpecialTwo => runtime.SpecialTwoCooldownFrames,
                        _ => runtime.MovementCooldownFrames
                    };
                    ready = frames <= 0;
                }

                int readyState = ready ? 1 : 0;
                bool readinessChanged = panel.PresentedReady[index] != readyState;
                if (!readinessChanged && panel.PresentedCooldownFrames[index] == frames) continue;
                panel.PresentedReady[index] = readyState;
                panel.PresentedCooldownFrames[index] = frames;

                Label label = panel.CooldownLabels[index];
                label.Text = ready
                    ? TranslationServer.Translate(FighterHudModel.CooldownGlyphKey(slot))
                    : string.Format(
                        TranslationServer.Translate("common_seconds_short"),
                        FighterHudModel.CooldownSeconds(frames, tickRate));
                if (!readinessChanged) continue;
                label.AddThemeColorOverride("font_color", ready ? UIPalette.Cyan : UIPalette.SlateDim);
                panel.CooldownPlates[index].Modulate = new Color(1f, 1f, 1f, ready ? 1f : 0.45f);
            }
        }

        /// <summary>
        /// Repaints a bar's fill without disturbing the themed trough. A Theme
        /// cannot express "this bar's fill colour depends on its value", so the
        /// override is duplicated from whatever box is currently in force and
        /// recoloured — and only when the band actually changed, so a match spends
        /// no frames allocating StyleBoxes.
        /// </summary>
        private static void TintFill(ProgressBar bar, Color color) {
            if (bar.GetThemeStylebox("fill") is not StyleBoxFlat current) return;
            if (current.BgColor == color) return;
            var box = (StyleBoxFlat)current.Duplicate();
            box.BgColor = color;
            bar.AddThemeStyleboxOverride("fill", box);
        }
    }
}
