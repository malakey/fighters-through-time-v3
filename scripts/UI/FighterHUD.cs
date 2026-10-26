using Godot;
using FTT.Characters;
using FTT.Core;
using FTT.FighterSim;
using FTT.UI.Motion;
using FTT.UI.Pulp;

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

        /// <summary>
        /// Cooldown slots rendered per player (M25, HUD_CONTRACT): three 20 × 20
        /// radial icons beside the block charges. P2's row is authored mirrored.
        /// </summary>
        private static readonly FighterCooldownSlot[] Slots = {
            FighterCooldownSlot.SpecialOne,
            FighterCooldownSlot.SpecialTwo,
            FighterCooldownSlot.Movement
        };

        /// <summary>Authored node name of each slot's icon, in <see cref="Slots"/> order.</summary>
        public static readonly string[] CooldownIconNames = {
            "S1_Cooldown", "S2_Cooldown", "Movement_Cooldown"
        };

        // UI_SPEC 3.3.4: stocks are 28 px stars with a 6 px gap, block pips 24 px shields with a 7 px gap.
        private const float StockSize = 28f;
        private const float StockSpacing = 6f;
        private const float ShieldSize = 24f;
        private const float ShieldSpacing = 7f;
        private const float StockOffAlpha = 0.22f;
        private const float ShieldOffAlpha = 0.28f;
        private const float DefyIconSize = 32f;
        private const float SlotShapeBox = 30f;
        private const float SlotShapeGlyph = 22f;
        private const float AbilitySlotSize = 54f;
        private const float ChipBadge = 40f;
        /// <summary>Seconds the M25 "ready" flash takes to settle.</summary>
        private const float ReadyFlashSeconds = 0.3f;

        private sealed class PlayerPanel {
            public Control Root;
            public TextureRect Portrait;
            public Label Name;
            public ProgressBar HP;
            public ProgressBar Meter;
            // UR-HUDF: the GAUGE-B overlays (tracker, Rally echo, hatch, ticks), the
            // HP numerals, the full-meter gradient, the 110 px roundel, the slot
            // shape, the "P1" tag, and the Pulp widgets of the slot row.
            public PulpBarDecor HpDecor;
            public PulpBarDecor MeterDecor;
            public TextureRect MeterFull;
            public Label HpCur;
            public Label HpMax;
            public Label PTag;
            public PulpPortraitRoundel Roundel;
            public Control SlotShape;
            public PulpIcon SlotShapeIcon;
            public PulpIcon DefyIcon;
            public PulpAbilitySlot[] SlotWidgets = System.Array.Empty<PulpAbilitySlot>();
            public PulpStatusChip DamageChip;
            public PulpStatusChip ControlChip;
            public Control SlotRow;
            public readonly HitFlashLimiter FlashLimiter = new();
            public int PresentedHP = -1;
            public int PresentedMaxHP = -1;
            public int PresentedMeterFull = -1;
            public HBoxContainer StockPips;
            public Label StocksLost;
            public Label StocksLabel;
            public HBoxContainer ShieldPips;
            public HBoxContainer Cooldowns;
            public Control[] Stocks = System.Array.Empty<Control>();
            public Control[] Shields = System.Array.Empty<Control>();
            public Label[] CooldownLabels = System.Array.Empty<Label>();
            public RadialProgress[] CooldownRadials = System.Array.Empty<RadialProgress>();
            /// <summary>M25: the remaining count each cooldown started from (the radial's denominator).</summary>
            public readonly int[] CooldownStartFrames = { 0, 0, 0, 0 };
            /// <summary>G12: the P1 ▲ / P2 ● shape beside the name tag.</summary>
            public Label SlotBadge;

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
            public int PresentedDamageTenths = -1;
            public int PresentedControlTenths = -1;
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
        private Control _timerPlate;
        private Label _modeLabel;
        private float _layoutCanvasWidth = -1f;
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
            _timerPlate = GetNodeOrNull<Control>("SafeArea/TimerPlate");
            _modeLabel = GetNodeOrNull<Label>("SafeArea/ModeLabel");
            _panels[0] = BindPanel("SafeArea/P1_Status");
            _panels[1] = BindPanel("SafeArea/P2_Status");
            for (int index = 0; index < _panels.Length; index++) {
                BuildCooldownSlots(_panels[index]);
                BuildWidgets(_panels[index], index);
                ConfigurePlayer(index, null, 3, 3);
            }
            SetTimerShown(false);
            ApplyOpacity();
            _uiScale.Apply(_root, force: true);
            ApplyLayout(force: true);
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
                Cooldowns = panelRoot.GetNodeOrNull<HBoxContainer>("Body/Column/PipRow/Cooldowns"),
                SlotBadge = panelRoot.GetNodeOrNull<Label>("Body/Column/TopRow/SlotBadge"),
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
            panel.PTag = panelRoot.GetNodeOrNull<Label>("Body/Column/TopRow/PTag");
            return panel;
        }

        /// <summary>
        /// UR-HUDF: builds the Pulp widgets around the authored nodes of one player
        /// panel (UI_SPEC 4.5): the 110 px portrait roundel, the 30 px slot shape,
        /// the "P1" tag, the HP / meter tracks and overlays, the HP numerals, the
        /// full-meter gradient, the Defy icon and the slot row (three 54 px ability
        /// slots plus the two status chips). Nothing here is simulation state.
        /// </summary>
        private void BuildWidgets(PlayerPanel panel, int index) {
            if (panel == null) return;
            bool mirrored = index == 1;
            Node body = panel.Root.GetNodeOrNull("Body");
            if (body != null) {
                panel.Roundel = new PulpPortraitRoundel { Name = "PortraitRoundel", Diameter = 110f };
                body.AddChild(panel.Roundel);
                body.MoveChild(panel.Roundel, mirrored ? body.GetChildCount() - 1 : 0);
            }

            Node topRow = panel.Root.GetNodeOrNull("Body/Column/TopRow");
            if (topRow != null) {
                panel.SlotShape = new Control {
                    Name = "SlotShape",
                    CustomMinimumSize = new Vector2(SlotShapeBox, SlotShapeBox),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                panel.SlotShapeIcon = PulpIcon.Create(mirrored ? "slot_p2" : "slot_p1",
                    FTT.Combat.PlayerSlotPalettes.ActiveSlotColor(index), SlotShapeGlyph);
                panel.SlotShapeIcon.Position = Vector2.One * ((SlotShapeBox - SlotShapeGlyph) / 2f);
                panel.SlotShape.AddChild(panel.SlotShapeIcon);
                topRow.AddChild(panel.SlotShape);
                topRow.MoveChild(panel.SlotShape, mirrored ? topRow.GetChildCount() - 1 : 0);
            }

            // HP: a 44 px track (the PulpBar variation ships the 30 px one); the fill stays the
            // variation's StyleBoxFlat so TintFill keeps working.
            if (panel.HP != null) {
                OverrideTrack(panel.HP, "bar_track_h44");
                panel.HpDecor = PulpBarDecor.Attach(panel.HP);
                var value = new HBoxContainer {
                    Name = "HpValue",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Alignment = mirrored ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.End
                };
                value.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                if (mirrored) value.OffsetLeft = 26f; else value.OffsetRight = -26f;
                panel.HpCur = new Label {
                    Name = "Cur", MouseFilter = Control.MouseFilterEnum.Ignore,
                    ThemeTypeVariation = UIPalette.LabelRoles.HudNumeral,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
                };
                panel.HpMax = new Label {
                    Name = "Max", MouseFilter = Control.MouseFilterEnum.Ignore,
                    ThemeTypeVariation = UIPalette.LabelRoles.HudNumeralSmall,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
                };
                value.AddChild(panel.HpCur);
                value.AddChild(panel.HpMax);
                panel.HP.AddChild(value);
            }

            // Meter: a 16 px track, the variation's flat fill recoloured to Meter2, hatch and ticks from the
            // decor, and a gold gradient over the inner area that shows only while the meter is full.
            if (panel.Meter != null) {
                OverrideTrack(panel.Meter, "bar_track_h16");
                if (panel.Meter.GetThemeStylebox("fill") is StyleBoxFlat meterFill) {
                    var box = (StyleBoxFlat)meterFill.Duplicate();
                    box.BgColor = UIPalette.Meter2;
                    panel.Meter.AddThemeStyleboxOverride("fill", box);
                }
                StyleBox bg = panel.Meter.GetThemeStylebox("background");
                var gradient = new Gradient {
                    Offsets = new[] { 0f, 1f },
                    Colors = new[] { UIPalette.Res, UIPalette.Res2 }
                };
                var gradientTexture = new GradientTexture2D {
                    Gradient = gradient, Width = 64, Height = 4,
                    FillFrom = new Vector2(0f, 0f), FillTo = new Vector2(1f, 0f)
                };
                panel.MeterFull = new TextureRect {
                    Name = "FullGradient",
                    Texture = gradientTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false
                };
                panel.MeterFull.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                panel.MeterFull.OffsetLeft = bg?.GetContentMargin(Side.Left) ?? 3f;
                panel.MeterFull.OffsetTop = bg?.GetContentMargin(Side.Top) ?? 3f;
                panel.MeterFull.OffsetRight = -(bg?.GetContentMargin(Side.Right) ?? 3f);
                panel.MeterFull.OffsetBottom = -(bg?.GetContentMargin(Side.Bottom) ?? 3f);
                panel.Meter.AddChild(panel.MeterFull);
                panel.MeterDecor = PulpBarDecor.Attach(panel.Meter);
            }

            // Defy seal: the icon beside the pips (the old glyph Label stays as the carrier).
            Node pipRow = panel.Root.GetNodeOrNull("Body/Column/PipRow");
            if (pipRow != null) {
                panel.DefyIcon = PulpIcon.Create("defy_notready", UIPalette.Ink3, DefyIconSize);
                panel.DefyIcon.Name = "DefyIcon";
                pipRow.AddChild(panel.DefyIcon);
                if (mirrored) pipRow.MoveChild(panel.DefyIcon, 0);
            }

            // The slot row: S1 / S2 / MOV at 54 with key tags, then the two status chips.
            panel.SlotRow = GetNodeOrNull<Control>(mirrored ? "SafeArea/P2_Slots" : "SafeArea/P1_Slots");
            if (panel.SlotRow != null) {
                var widgets = new PulpAbilitySlot[Slots.Length];
                for (int slot = 0; slot < Slots.Length; slot++) {
                    widgets[slot] = new PulpAbilitySlot {
                        Name = CooldownIconNames[slot].Replace("_Cooldown", "_Slot"),
                        SlotSize = AbilitySlotSize,
                        KeyText = FighterHudModel.CooldownGlyphKey(Slots[slot])
                    };
                }
                panel.SlotWidgets = widgets;
                panel.DamageChip = new PulpStatusChip {
                    Name = "DamageChip", BadgeSize = ChipBadge, Kind = PulpChipKind.Damage, Empty = true
                };
                panel.ControlChip = new PulpStatusChip {
                    Name = "ControlChip", BadgeSize = ChipBadge, Kind = PulpChipKind.Control, Empty = true
                };
                var gap = new Control {
                    Name = "Gap", CustomMinimumSize = new Vector2(10f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore
                };
                if (!mirrored) {
                    foreach (PulpAbilitySlot w in widgets) panel.SlotRow.AddChild(w);
                    panel.SlotRow.AddChild(gap);
                    panel.SlotRow.AddChild(panel.DamageChip);
                    panel.SlotRow.AddChild(panel.ControlChip);
                } else {
                    // Mirrored: the pair of chips keeps damage left, control right; S1 is on the outer edge.
                    panel.SlotRow.AddChild(panel.DamageChip);
                    panel.SlotRow.AddChild(panel.ControlChip);
                    panel.SlotRow.AddChild(gap);
                    for (int slot = widgets.Length - 1; slot >= 0; slot--) panel.SlotRow.AddChild(widgets[slot]);
                }
            }
        }

        /// <summary>Overrides a bar's <c>background</c> with one of the kit's track plates (a scene-local look: the theme's variation ships the 30 px track).</summary>
        private static void OverrideTrack(ProgressBar bar, string stem) {
            StyleBoxTexture plate = PulpKit.Plate(stem);
            if (plate != null) bar.AddThemeStyleboxOverride("background", plate);
        }

        /// <summary>
        /// UI_SPEC 8.2 item 6 / plan item 14: the clusters are scaled per corner. The HP bars shrink before they
        /// meet the timer plate (the rule is <see cref="FighterHudModel.HpBarWidth"/>), the meter bars follow, and
        /// the status chips drop their text column when the canvas is too narrow for it. Runs when the canvas
        /// width (the root's size, which the UI scale divides) changes.
        /// </summary>
        private void ApplyLayout(bool force = false) {
            if (_root == null) return;
            float width = _root.Size.X > 1f ? _root.Size.X : FighterHudModel.ReferenceCanvasWidth;
            if (!force && Mathf.IsEqualApprox(width, _layoutCanvasWidth)) return;
            _layoutCanvasWidth = width;
            float hp = FighterHudModel.HpBarWidth(width);
            float meter = FighterHudModel.MeterBarWidth(hp);
            float cluster = FighterHudModel.ClusterWidth(hp);
            bool chipText = FighterHudModel.ChipsShowText(width);
            for (int index = 0; index < _panels.Length; index++) {
                PlayerPanel panel = _panels[index];
                if (panel == null) continue;
                if (panel.HP != null) panel.HP.CustomMinimumSize = new Vector2(hp, FighterHudModel.HpBarHeight);
                // A narrow bar drops the "/max" so the current value stays clear of the Rally echo band.
                if (panel.HpMax != null) panel.HpMax.Visible = hp >= FighterHudModel.HpMaxTextMinWidth;
                if (panel.Meter != null) panel.Meter.CustomMinimumSize = new Vector2(meter, FighterHudModel.MeterBarHeight);
                if (panel.Root is Control root) {
                    if (index == 0) {
                        root.OffsetLeft = FighterHudModel.ClusterMargin;
                        root.OffsetRight = FighterHudModel.ClusterMargin + cluster;
                    } else {
                        root.OffsetRight = -FighterHudModel.ClusterMargin;
                        root.OffsetLeft = -(FighterHudModel.ClusterMargin + cluster);
                    }
                }
                if (panel.DamageChip != null) panel.DamageChip.ShowText = chipText;
                if (panel.ControlChip != null) panel.ControlChip.ShowText = chipText;
            }
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
                // The authored TextureRect keeps the texture (it is the old seam); the 110 px roundel draws it.
                panel.Portrait.Texture = data?.CharacterPortrait;
                panel.Portrait.Visible = false;
            }
            if (panel.Roundel != null) {
                panel.Roundel.Portrait = data?.CharacterPortrait;
                panel.Roundel.Visible = data?.CharacterPortrait != null;
            }
            if (panel.SlotWidgets.Length == Slots.Length) {
                AbilitySlot[] ids = { AbilitySlot.Special1, AbilitySlot.Special2, AbilitySlot.MovementAbility };
                for (int slot = 0; slot < Slots.Length; slot++) {
                    panel.SlotWidgets[slot].Art = AbilityIconLibrary.Load(data?.CharacterID, ids[slot]);
                }
            }
            if (panel.Name != null) {
                string key = data?.DisplayNameKey ?? "";
                panel.Name.Text = string.IsNullOrWhiteSpace(key)
                    ? string.Format(Tr("fighter_hud_player"), playerIndex + 1)
                    : Tr(key);
            }
            if (panel.PTag != null) panel.PTag.Text = string.Format(Tr("fighter_hud_player"), playerIndex + 1);
            ApplySlotIdentity(panel, playerIndex);

            panel.Stocks = RebuildPips(panel.StockPips, Mathf.Max(1, maxStocks), "star4", StockSize, StockSpacing,
                FTT.Combat.PlayerSlotPalettes.ActiveSlotColor(playerIndex));
            panel.Shields = RebuildPips(panel.ShieldPips, Mathf.Max(1, maxShieldCharges), "shield", ShieldSize,
                ShieldSpacing, UIPalette.Shield);
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
                int maxHp = Mathf.Max(1, state.MaxHP);
                int hp = Mathf.Clamp(state.CurrentHP, 0, maxHp);
                panel.HP.MaxValue = maxHp;
                panel.HP.Value = hp;
                TintFill(panel.HP, FighterHudModel.HpFillColor(state.CurrentHP, state.MaxHP));
                if (panel.PresentedHP != hp || panel.PresentedMaxHP != maxHp) {
                    // UI_SPEC 5 rows 6 and 7: the tracker holds then drops, and a damaging hit flashes the
                    // bar plate black for one frame, at most once a second per bar (plan item 14, Q3).
                    bool damaged = panel.PresentedHP >= 0 && hp < panel.PresentedHP;
                    if (panel.HpDecor != null) {
                        if (panel.PresentedHP < 0) panel.HpDecor.LagValue = hp;
                        else panel.HpDecor.DropLagTo(hp);
                    }
                    if (damaged) PulpMotion.HitFlash(panel.HP, panel.FlashLimiter, Time.GetTicksMsec() / 1000.0);
                    panel.PresentedHP = hp;
                    panel.PresentedMaxHP = maxHp;
                    if (panel.HpCur != null) panel.HpCur.Text = hp.ToString();
                    if (panel.HpMax != null) panel.HpMax.Text = "/" + maxHp;
                }
                if (panel.HpDecor != null) {
                    // The Rally echo: the decor draws the band from the fill end to HP + echo (the shipped
                    // EchoBandFraction rule, in the bar's units), so nothing else draws it.
                    float band = FighterHudModel.EchoBandFraction(state.CurrentHP, echoPool, state.MaxHP);
                    panel.HpDecor.EchoValue = band > 0f ? hp + band * maxHp : 0f;
                    panel.HpDecor.Low = hp > 0 && FighterHudModel.BarFraction(hp, maxHp) <= FighterHudModel.HpDangerFraction;
                }
            }
            if (panel.Meter != null) {
                panel.Meter.MaxValue = FighterHudModel.MaxInfluence;
                float influence = Mathf.Clamp(state.Influence.ToFloat(), 0f, FighterHudModel.MaxInfluence);
                panel.Meter.Value = influence;
                int full = FighterHudModel.UltimateIsReady(influence) ? 1 : 0;
                if (full != panel.PresentedMeterFull) {
                    panel.PresentedMeterFull = full;
                    if (panel.MeterFull != null) panel.MeterFull.Visible = full == 1;
                }
            }

            SetPips(panel.Stocks, state.Stocks, StockOffAlpha);
            SetPips(panel.Shields, state.BlockCharges, ShieldOffAlpha);
            ApplyStockDisplay(panel, in state, in runtime);

            // F24: both slots render simultaneously, at fixed positions, never
            // collapsed to a single "winning" icon. The data was always there —
            // the runtime component bit-packs both — and PresentedStatusType was
            // only ever a single-pip collapse helper.
            ApplyStatusSlot(
                panel.DamageStatus, panel.DamageRadial, panel.DamageChip,
                (StatusType)runtime.DamageStatusType, runtime.DamageStatusFrames,
                tickRate, ref panel.PresentedDamageStatus, ref panel.PresentedDamageTenths);
            ApplyStatusSlot(
                panel.ControlStatus, panel.ControlRadial, panel.ControlChip,
                (StatusType)runtime.StatusType, runtime.StatusFrames,
                tickRate, ref panel.PresentedControlStatus, ref panel.PresentedControlTenths);

            ApplyDefySeal(panel, in state, playerIndex);
            UpdateCooldowns(panel, in runtime);
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
            Label indicator, RadialProgress radial, PulpStatusChip chip,
            StatusType status, int remainingFrames, int tickRate, ref int presented, ref int presentedTenths) {

            if (indicator == null) return;
            if (radial != null) {
                radial.Visible = status != StatusType.None;
                if (status != StatusType.None) {
                    radial.Fraction = FighterHudModel.StatusRadialFraction(remainingFrames, tickRate);
                }
            }
            if (chip != null && status != StatusType.None) {
                // The ring follows the authoritative frame count; the time text changes by tenths only.
                chip.Remaining = FighterHudModel.StatusRadialFraction(remainingFrames, tickRate);
                int tenths = Mathf.CeilToInt(remainingFrames * 10f / Mathf.Max(1, tickRate));
                if (tenths != presentedTenths) {
                    presentedTenths = tenths;
                    chip.TimeText = string.Format(Tr("common_seconds_short"), tenths / 10f);
                }
            }
            if (presented == (int)status) return;
            presented = (int)status;
            if (status == StatusType.None) {
                indicator.Text = "";
                indicator.TooltipText = "";
                presentedTenths = -1;
                if (chip != null) {
                    chip.Empty = true;
                    chip.TimeText = "";
                }
                return;
            }
            if (chip != null) {
                chip.Empty = false;
                chip.GlyphName = FighterHudModel.StatusIconName(status);
                chip.NameText = FighterHudModel.StatusKey(status);
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
            if (panel.DefyIcon != null) {
                panel.DefyIcon.IconName = FighterHudModel.DefyIconName(seal);
                panel.DefyIcon.Tint = FighterHudModel.DefyIconTint(seal);
            }
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
            SetModeLabel(matchMode);

            if (suddenDeath) {
                // F22: the phase text replaces the running clock; a long line needs the small role.
                SetTimerShown(true);
                _timer.ThemeTypeVariation = UIPalette.LabelRoles.StateLabel;
                _timer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _timer.Text = Tr("fighter_hud_sudden_death_clock");
                _timer.AddThemeColorOverride("font_color", UIPalette.Danger);
                _lastTimerSeconds = -1;
                return;
            }
            if (_timer.ThemeTypeVariation != UIPalette.LabelRoles.TimerDigits) {
                _timer.ThemeTypeVariation = UIPalette.LabelRoles.TimerDigits;
                _timer.AutowrapMode = TextServer.AutowrapMode.Off;
                _timer.RemoveThemeColorOverride("font_color");
                _timerDangerShown = false;
            }

            bool visible = FighterHudModel.TimerIsVisible(timerEnabled);
            SetTimerShown(visible);
            if (!visible) {
                _lastTimerSeconds = -1;
                return;
            }
            int seconds = FighterHudModel.TotalSeconds(remainingFrames, tickRate);
            _timer.Text = string.Format(
                Tr("fighter_hud_clock"),
                FighterHudModel.ClockMinutes(remainingFrames, tickRate).ToString("D2"),
                FighterHudModel.ClockSeconds(remainingFrames, tickRate).ToString("D2"));
            bool danger = FighterHudModel.IsTimerDanger(seconds);
            if (danger != _timerDangerShown) {
                _timerDangerShown = danger;
                if (danger) _timer.AddThemeColorOverride("font_color", UIPalette.Danger);
                else _timer.RemoveThemeColorOverride("font_color");
            }
            if (danger && seconds != _lastTimerSeconds && seconds == FighterHudModel.TimerDangerSeconds) {
                // C01b: a timer-danger cue is a protected warning, so it rides the
                // clear Critical Cues path rather than the background World SFX.
                AudioManager.Instance?.PlayCriticalCue(null);
            }
            _lastTimerSeconds = seconds;
        }

        /// <summary>Shows or hides the clock, its ink plate and the mode label together.</summary>
        private void SetTimerShown(bool shown) {
            if (_timer != null) _timer.Visible = shown;
            if (_timerPlate != null) _timerPlate.Visible = shown;
            if (_modeLabel != null) _modeLabel.Visible = shown;
        }

        private void SetModeLabel(int matchMode) {
            if (_modeLabel == null) return;
            string key = FighterHudModel.ModeLabelKey(matchMode);
            if (_modeLabel.Text != key) _modeLabel.Text = key;
        }

        public override void _Process(double delta) {
            ApplyOpacity();
            _uiScale.Apply(_root);
            ApplyLayout();
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
                bool hasVerb = _driver.TryGetVerb(playerID, out FighterVerbComponent verb);
                float echoPool = hasVerb ? verb.EchoPool.ToFloat() : 0f;
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
            PlayerPanel panel = PanelFor(playerIndex);
            if (panel?.HP == null || panel.HpDecor == null || panel.HP.MaxValue <= 0) return 0f;
            float end = panel.HpDecor.EchoValue;
            if (end <= panel.HP.Value) return 0f;
            return (float)((end - panel.HP.Value) / panel.HP.MaxValue);
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
            if (panel == null || index < 0 || index >= panel.CooldownLabels.Length) return false;
            return panel.PresentedReady[index] == 1;
        }

        /// <summary>A cooldown slot's radial fill (1 = ready). Test seam.</summary>
        public float CooldownFraction(int playerIndex, FighterCooldownSlot slot) {
            PlayerPanel panel = PanelFor(playerIndex);
            int index = System.Array.IndexOf(Slots, slot);
            if (panel == null || index < 0 || index >= panel.CooldownRadials.Length) return 0f;
            return panel.CooldownRadials[index]?.Fraction ?? 0f;
        }

        /// <summary>The slot-shape glyph beside a player's name tag (G12). Test seam.</summary>
        public string SlotBadgeText(int playerIndex) => PanelFor(playerIndex)?.SlotBadge?.Text ?? "";

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

        private static int CountLit(Control[] pips) {
            if (pips == null) return 0;
            int lit = 0;
            foreach (Control pip in pips) {
                if (pip.Modulate.A >= 1f) lit++;
            }
            return lit;
        }

        /// <summary>Rebuilds a pip row from kit icons (UI_SPEC 3.3.4): stocks are stars, block charges shields.</summary>
        private static Control[] RebuildPips(
            HBoxContainer container, int count, string icon, float size, float spacing, Color color) {
            if (container == null) return System.Array.Empty<Control>();
            ClearChildren(container);
            container.AddThemeConstantOverride("separation", (int)spacing);
            var pips = new Control[count];
            for (int index = 0; index < count; index++) {
                PulpIcon pip = PulpIcon.Create(icon, color, size);
                pip.Name = $"Pip{index}";
                container.AddChild(pip);
                pips[index] = pip;
            }
            return pips;
        }

        /// <summary>
        /// A spent pip is dimmed rather than removed, so the panel never reflows
        /// mid-match and the player can still see the capacity they started with.
        /// </summary>
        private static void SetPips(Control[] pips, int filled, float offAlpha) {
            if (pips == null) return;
            int lit = FighterHudModel.FilledPips(filled, pips.Length);
            for (int index = 0; index < pips.Length; index++) {
                pips[index].Modulate = new Color(1f, 1f, 1f, index < lit ? 1f : offAlpha);
            }
        }

        /// <summary>
        /// M25: binds the four authored 20 × 20 cooldown icons (a glyph Label each,
        /// with a <see cref="RadialProgress"/> child). The icons are authored, not
        /// built, because their count is fixed by the contract; only the pips stay
        /// code-built, because theirs is data-driven.
        /// </summary>
        private void BuildCooldownSlots(PlayerPanel panel) {
            if (panel?.Cooldowns == null) return;
            var labels = new Label[Slots.Length];
            var radials = new RadialProgress[Slots.Length];
            for (int index = 0; index < Slots.Length; index++) {
                Label icon = panel.Cooldowns.GetNodeOrNull<Label>(CooldownIconNames[index]);
                if (icon == null) return;
                icon.Text = Tr(FighterHudModel.CooldownGlyphKey(Slots[index]));
                labels[index] = icon;
                radials[index] = icon.GetNodeOrNull<RadialProgress>("Radial");
            }
            panel.CooldownLabels = labels;
            panel.CooldownRadials = radials;
        }

        /// <summary>
        /// M25 (HUD_CONTRACT): three radial cooldowns — Special 1 / Special 2 /
        /// Movement — from the runtime component's authoritative state, with a
        /// brief flash on becoming ready. The glyph never changes, so readiness is
        /// carried by the radial and the dimming, never by colour alone.
        /// </summary>
        private void UpdateCooldowns(PlayerPanel panel, in FighterRuntimeComponent runtime) {

            if (panel.CooldownLabels.Length != Slots.Length) return;
            for (int index = 0; index < Slots.Length; index++) {
                FighterCooldownSlot slot = Slots[index];
                int frames = slot switch {
                    FighterCooldownSlot.SpecialOne => runtime.SpecialOneCooldownFrames,
                    FighterCooldownSlot.SpecialTwo => runtime.SpecialTwoCooldownFrames,
                    _ => runtime.MovementCooldownFrames
                };
                if (frames < 0) frames = 0;
                // The radial's denominator is the count the cooldown started from:
                // latched whenever the authoritative remaining rises (a new cast, or
                // a rollback correction), released when it reaches zero.
                if (frames <= 0) panel.CooldownStartFrames[index] = 0;
                else if (frames > panel.CooldownStartFrames[index]) panel.CooldownStartFrames[index] = frames;

                bool ready = frames <= 0;
                int readyState = ready ? 1 : 0;
                bool readinessChanged = panel.PresentedReady[index] != readyState;
                bool wasCooling = panel.PresentedReady[index] == 0;
                if (!readinessChanged && panel.PresentedCooldownFrames[index] == frames) continue;
                panel.PresentedReady[index] = readyState;
                panel.PresentedCooldownFrames[index] = frames;

                RadialProgress radial = panel.CooldownRadials[index];
                if (radial != null) {
                    radial.Fraction = FighterHudModel.CooldownRadialFraction(
                        frames, panel.CooldownStartFrames[index]);
                }
                if (index < panel.SlotWidgets.Length && panel.SlotWidgets[index] != null) {
                    // The visible 54 px slot: a pie sweep over the elapsed share and the remaining seconds.
                    PulpAbilitySlot widget = panel.SlotWidgets[index];
                    widget.State = ready ? PulpSlotState.Ready : PulpSlotState.Cooling;
                    widget.Progress = FighterHudModel.CooldownRadialFraction(frames, panel.CooldownStartFrames[index]);
                    widget.SecondsRemaining = FighterHudModel.CooldownSeconds(frames, FighterSimulation.TickRate);
                    if (readinessChanged && ready && wasCooling) FlashReady(widget);
                }
                if (!readinessChanged) continue;
                Label icon = panel.CooldownLabels[index];
                icon.AddThemeColorOverride("font_color", ready ? UIPalette.Res2 : UIPalette.SlateDim);
                icon.Modulate = new Color(1f, 1f, 1f, ready ? 1f : 0.45f);
                if (radial != null) radial.FillColor = ready ? UIPalette.Accent : UIPalette.SlateDim;
            }
        }

        /// <summary>
        /// D10 (2026-10-11, ledger <c>USER-NOT-YET-FLASH</c>): the "not yet" flash
        /// on a player's Special 1 / Special 2 cooldown icon or meter bar, for
        /// each slot <see cref="FighterLockoutRefusalRules"/> found refused by the
        /// ability lockout alone. The icon or bar is both the frame host and the
        /// tint target; the tint rides <c>SelfModulate</c>, so the readiness
        /// alpha and the M25 ready flash (both on <c>Modulate</c>) are untouched.
        /// </summary>
        public void FlashLockoutRefusal(int playerIndex, FighterLockoutRefusal refused) {
            PlayerPanel panel = PanelFor(playerIndex);
            if (panel == null || refused == FighterLockoutRefusal.None) return;
            if ((refused & FighterLockoutRefusal.SpecialOne) != 0) FlashCooldownIcon(panel, FighterCooldownSlot.SpecialOne);
            if ((refused & FighterLockoutRefusal.SpecialTwo) != 0) FlashCooldownIcon(panel, FighterCooldownSlot.SpecialTwo);
            if ((refused & FighterLockoutRefusal.Ultimate) != 0) NotYetFlash.Play(panel.Meter, panel.Meter);
        }

        private static void FlashCooldownIcon(PlayerPanel panel, FighterCooldownSlot slot) {
            int index = System.Array.IndexOf(Slots, slot);
            if (index < 0 || index >= panel.CooldownLabels.Length) return;
            Label icon = panel.CooldownLabels[index];
            NotYetFlash.Play(icon, icon);
            // The label is the hidden carrier; the flash must be visible on the 54 px slot.
            if (index < panel.SlotWidgets.Length && panel.SlotWidgets[index] != null) {
                PulpAbilitySlot widget = panel.SlotWidgets[index];
                NotYetFlash.Play(widget, widget);
            }
        }

        /// <summary>M25's brief ready flash: an overbright pulse that settles to normal.</summary>
        private void FlashReady(Control icon) {
            if (!icon.IsInsideTree()) return;
            icon.Modulate = new Color(1.8f, 1.8f, 1.8f, 1f);
            Tween tween = icon.CreateTween();
            tween.TweenProperty(icon, "modulate", new Color(1f, 1f, 1f, 1f), ReadyFlashSeconds);
        }

        /// <summary>
        /// G12: the slot shape (P1 ▲ / P2 ●) beside the name tag, and the name tag
        /// itself, in the local player-slot palette with a contrasting edge.
        /// Presentation only — the palette never reaches the simulation.
        /// </summary>
        private static void ApplySlotIdentity(PlayerPanel panel, int playerIndex) {
            Color slotColour = FTT.Combat.PlayerSlotPalettes.ActiveSlotColor(playerIndex);
            Color edge = FTT.Combat.PlayerSlotPalettes.ActiveSlotEdgeColor(playerIndex);
            // The ▲ / ● Label is the hidden text carrier; the shape icon carries identity (never colour alone),
            // and the name reads in the HeroName ink role.
            if (panel.SlotBadge != null) {
                panel.SlotBadge.Text = FTT.Combat.PlayerSlotPalettes.ShapeGlyph(playerIndex);
                panel.SlotBadge.AddThemeColorOverride("font_color", slotColour);
                panel.SlotBadge.AddThemeColorOverride("font_outline_color", edge);
            }
            if (panel.SlotShapeIcon != null) panel.SlotShapeIcon.Tint = slotColour;
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
