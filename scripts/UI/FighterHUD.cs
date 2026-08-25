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
            public Label Status;
            public ProgressBar HP;
            public ColorRect EchoBand;
            public ProgressBar Meter;
            public HBoxContainer StockPips;
            public HBoxContainer ShieldPips;
            public HBoxContainer Cooldowns;
            public ColorRect[] Stocks = System.Array.Empty<ColorRect>();
            public ColorRect[] Shields = System.Array.Empty<ColorRect>();
            public Label[] CooldownLabels = System.Array.Empty<Label>();
            public Panel[] CooldownPlates = System.Array.Empty<Panel>();
            // Presented-value caches. Every widget below is refreshed at 60 Hz, so
            // writing a theme override or a StyleBox unconditionally would allocate
            // on every frame of every match. Diffing keeps the steady state free.
            public int PresentedStatus = -1;
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

        /// <summary>Opacity currently applied to the HUD root. Test seam.</summary>
        public float AppliedOpacity => _appliedOpacity;

        /// <summary>Whether the match clock is on screen. Test seam.</summary>
        public bool TimerVisible => _timer != null && _timer.Visible;

        /// <summary>Rendered clock copy, empty when hidden. Test seam.</summary>
        public string TimerText => _timer != null && _timer.Visible ? _timer.Text : "";

        public override void _Ready() {
            Layer = 12;
            _root = GetNodeOrNull<Control>("Root");
            // The scene attaches the shared theme, but the whole HUD layout
            // scales through _uiScale, so the shared (scale-mutated) theme
            // would double-scale the HUD text: swap in an unscaled copy.
            if (_root != null) {
                Theme unscaled = UIPalette.NewUnscaledTheme();
                if (unscaled != null) _root.Theme = unscaled;
            }
            _timer = GetNodeOrNull<Label>("Root/MatchClock");
            _panels[0] = BindPanel("Root/PlayerOne");
            _panels[1] = BindPanel("Root/PlayerTwo");
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
                Status = panelRoot.GetNodeOrNull<Label>("Body/Column/TopRow/Status"),
                HP = panelRoot.GetNodeOrNull<ProgressBar>("Body/Column/HPBar"),
                Meter = panelRoot.GetNodeOrNull<ProgressBar>("Body/Column/MeterBar"),
                StockPips = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/PipRow/Stocks"),
                ShieldPips = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/PipRow/Shields"),
                Cooldowns = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/Cooldowns")
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

            if (panel.Status != null && panel.PresentedStatus != runtime.PresentedStatusType) {
                panel.PresentedStatus = runtime.PresentedStatusType;
                var status = (StatusType)runtime.PresentedStatusType;
                panel.Status.Visible = status != StatusType.None;
                panel.Status.Text = Tr(FighterHudModel.StatusKey(status));
                // Same authored colour the glow arbiter puts on the fighter, so the
                // pip and the outline can never name different statuses.
                panel.Status.AddThemeColorOverride(
                    "font_color",
                    status == StatusType.None
                        ? UIPalette.SlateDim
                        : FTT.Combat.GlowPalette.Status(status).OutlineColor);
            }

            UpdateCooldowns(panel, in state, in runtime, tickRate);
        }

        /// <summary>Applies match-level state: clock visibility and remaining time.</summary>
        public void ApplyMatchState(bool timerEnabled, int remainingFrames, int tickRate) {
            if (_timer == null) return;
            bool visible = FighterHudModel.TimerIsVisible(timerEnabled);
            _timer.Visible = visible;
            if (!visible) return;
            _timer.Text = string.Format(
                Tr("fighter_hud_clock"),
                FighterHudModel.ClockMinutes(remainingFrames, tickRate),
                FighterHudModel.ClockSeconds(remainingFrames, tickRate).ToString("D2"));
        }

        public override void _Process(double delta) {
            ApplyOpacity();
            _uiScale.Apply(_root);
            if (_driver == null || !IsInstanceValid(_driver) || _driver.Simulation == null) return;

            int tickRate = FighterSimulation.TickRate;
            for (int playerID = 0; playerID < 2; playerID++) {
                if (!_driver.TryGetFighter(playerID, out FighterStateComponent state)
                    || !_driver.TryGetRuntime(playerID, out FighterRuntimeComponent runtime)) continue;
                float echoPool = _driver.TryGetVerb(playerID, out FighterVerbComponent verb)
                    ? verb.EchoPool.ToFloat()
                    : 0f;
                ApplyPlayerState(playerID, in state, in runtime, tickRate, echoPool);
            }

            FighterMatchComponent match = _driver.Simulation.GetMatchState();
            ApplyMatchState(match.TimerEnabled == 1, match.RemainingFrames, tickRate);
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

        /// <summary>Active status copy, empty when no status is shown. Test seam.</summary>
        public string StatusText(int playerIndex) {
            Label status = PanelFor(playerIndex)?.Status;
            return status != null && status.Visible ? status.Text : "";
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
