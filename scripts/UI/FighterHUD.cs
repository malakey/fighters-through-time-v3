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
    ///
    /// <para><b>Temporal Glass (TG-HUDF, UI_SPEC_GLASS 3.4 and 4.5, HUD-F2).</b> The kit's top shade sits behind the band.
    /// Each cluster is a G5 portrait frame (110), a header (the 30 px slot shape, the name in T09 with the halo, "P1"),
    /// the 510 x 44 HP tube (P2 mirrored, R6), row 2 (stocks <c>star4_filled</c> / <c>star4</c> in the owner colour,
    /// block pips <c>shield</c> / <c>shield_off</c>, the Defy seal) and the 390 x 16 Ultimate tube (Meter style, P2
    /// mirrored). Under each cluster a lens row: S1 / S2 / MOV lenses (54, pitch 74, key labels over the scene, S1 on the
    /// outer edge) and the two status badges (40, 18 apart, damage left for both players). The glass timer plate
    /// (<c>GlassPanelTimer</c>) carries the T34 digits and the T14 mode label; Sudden Death puts its line in the label
    /// slot and widens the plate to fit. MOT-A has no hit flash: a hit holds the tracker, then drains it; healing raises
    /// the fill over 0.2 s. Each cluster and the plate keep their screen corner at every UI scale (4.5), and the HP tube
    /// shrinks before it meets the plate (<see cref="FighterHudModel.HpBarScreenWidth"/>).</para>
    /// </summary>
    public partial class FighterHUD : CanvasLayer {

        /// <summary>Authored scene path; the driver instantiates this.</summary>
        public const string ScenePath = "res://scenes/ui/FighterHUD.tscn";

        /// <summary>
        /// Cooldown slots rendered per player (M25, HUD_CONTRACT): three lenses
        /// under the cluster. P2's row is built mirrored (S1 on the outer edge).
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

        // UI_SPEC_GLASS 3.4.4 / 4.5: stocks are 28 px stars 6 apart, block pips 24 px shields 7 apart, the Defy seal 32.
        private const float StockSize = 28f;
        private const float StockSpacing = 6f;
        private const float ShieldSize = 24f;
        private const float ShieldSpacing = 7f;
        private const float StockOffAlpha = 0.22f;
        private const float ShieldOffAlpha = 0.28f;
        private const float DefyIconSize = 32f;
        private const float SlotShapeBox = 30f;
        private const float SlotShapeGlyph = 22f;
        /// <summary>The header's gaps: shape to name, name to "P1" (4.5, 3L.c).</summary>
        private const float HeaderGap = 12f;
        private const float AbilitySlotSize = 54f;
        /// <summary>Lenses at pitch 74, then 50 px (the mock's 20 + 10 + 20) to the badges, which sit 18 apart.</summary>
        private const float LensGap = 20f;
        private const float LensToChipGap = 50f;
        private const float ChipGap = 18f;
        private const float ChipBadge = 40f;
        /// <summary>The HP value sits 14 px inside the tube's inner end, "/max" 2 px after the numeral at 0.78 (T17).</summary>
        private const float HpValueInset = 14f;
        private const int HpValueGap = 2;
        private const float HpMaxAlpha = 0.78f;

        /// <summary>The stock stars: filled while held, the outline at 0.22 once lost (3.4.4).</summary>
        public const string StockLitIcon = "star4_filled", StockOffIcon = "star4";
        /// <summary>The block pips: the glowing shield while charged, the glowless grey <c>shield_off</c> at 0.28 when spent.</summary>
        public const string ShieldLitIcon = "shield", ShieldOffIcon = "shield_off";
        /// <summary>A spent pip is the shield colour's grey (3.4.4: "opacity .28 and grayscale").</summary>
        public static readonly Color ShieldOffTint = Grey(UIPalette.Shield);

        private sealed class PlayerPanel {
            public int Index;
            public Control Root;
            public TextureRect Portrait;
            public Label Name;
            public ProgressBar HP;
            public ProgressBar Meter;
            // The glass parts: the tubes over their value carriers, the HP numerals, the portrait frame, the slot shape,
            // the "P1" tag, the Defy icon and the lens row's lenses and badges.
            public PulpBarDecor HpDecor;
            public PulpBarDecor MeterDecor;
            public Label HpCur;
            public Label HpMax;
            public Label PTag;
            public Control TopRow;
            public PulpPortraitRoundel Roundel;
            public Control SlotShape;
            public PulpIcon SlotShapeIcon;
            public PulpIcon DefyIcon;
            public PulpAbilitySlot[] SlotWidgets = System.Array.Empty<PulpAbilitySlot>();
            public PulpStatusChip DamageChip;
            public PulpStatusChip ControlChip;
            public Control SlotRow;
            /// <summary>The 0.2 s healing rise of the HP fill (MOT-A row 7), while it runs.</summary>
            public Tween HealTween;
            public int PresentedHP = -1;
            public int PresentedMaxHP = -1;
            public int PresentedStocksLit = -1;
            public int PresentedShieldsLit = -1;
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
        private float _layoutScale = float.NaN;
        private readonly PlayerPanel[] _panels = new PlayerPanel[2];
        private FighterSimulationDriver _driver;
        private float _appliedOpacity = -1f;
        private readonly UiScaleBinder _uiScale = new();
        private int _presentedPalette = -1;

        // F21/F22 match context. Cached rather than passed to every per-player
        // call: both the stock display and the Defy seal need them, and the driver
        // pulls the match component once per frame.
        private int _matchMode;
        private bool _suddenDeath;
        private int _lastTimerSeconds = -1;
        private int _presentedClockSeconds = -1;
        private bool _timerDangerShown;
        /// <summary>True while the plate shows Sudden Death's line (in the label slot) instead of the digits.</summary>
        private bool _suddenDeathShown;
        private bool _timerShown;

        /// <summary>Opacity currently applied to the HUD root. Test seam.</summary>
        public float AppliedOpacity => _appliedOpacity;

        /// <summary>Whether the match clock is on screen. Test seam.</summary>
        public bool TimerVisible => _timer != null && _timer.Visible;

        /// <summary>Rendered clock copy, empty when hidden. Test seam.</summary>
        public string TimerText => _timer != null && _timer.Visible ? _timer.Text : "";

        /// <summary>The HUD's scale (the accessibility UI scale applied to the root), 1 before the first apply.</summary>
        private float HudScale {
            get {
                float scale = _uiScale.AppliedScale;
                return float.IsNaN(scale) || scale <= 0f ? 1f : scale;
            }
        }

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
            // The plate is a glass slab: the sheen and its two streaks over the nine-slice (PNL-A).
            if (_timerPlate is PanelContainer plate) PulpScreenKit.DressPanel(plate);
            _panels[0] = BindPanel("SafeArea/P1_Status", 0);
            _panels[1] = BindPanel("SafeArea/P2_Status", 1);
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

        private PlayerPanel BindPanel(string path, int index) {
            Control panelRoot = GetNodeOrNull<Control>(path);
            if (panelRoot == null) return null;
            var panel = new PlayerPanel {
                Index = index,
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
            panel.TopRow = panelRoot.GetNodeOrNull<Control>("Body/Column/TopRow");
            return panel;
        }

        /// <summary>
        /// TG-HUDF: builds the glass parts around the authored nodes of one player panel (UI_SPEC_GLASS 4.5): the 110 px
        /// portrait frame, the 30 px slot shape, the HP tube and its numerals, the Ultimate tube, the Defy icon and the
        /// lens row (three 54 px lenses and the two status badges). Nothing here is simulation state.
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

            // The header is laid out by hand on its 30 px row (LayoutHeader): the slot shape, the name (shrunk to fit)
            // and the P tag, each centred on the row as the mock's flex row centres them.
            if (panel.TopRow != null) {
                panel.SlotShape = new Control {
                    Name = "SlotShape",
                    CustomMinimumSize = new Vector2(SlotShapeBox, SlotShapeBox),
                    Size = new Vector2(SlotShapeBox, SlotShapeBox),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                panel.SlotShapeIcon = PulpIcon.Create(mirrored ? "slot_p2" : "slot_p1",
                    UIPalette.OwnerOnDark(index), SlotShapeGlyph);
                panel.SlotShapeIcon.Position = Vector2.One * ((SlotShapeBox - SlotShapeGlyph) / 2f);
                panel.SlotShape.AddChild(panel.SlotShapeIcon);
                panel.TopRow.AddChild(panel.SlotShape);
                panel.TopRow.Resized += () => LayoutHeader(panel);
            }

            // HP: the tube (Hp style; the bar's fill StyleBoxFlat stays the fill colour, so TintFill keeps working; P2's
            // EndToBegin mirrors it) and the value "86/110" 14 px inside its inner end, the "/max" on the numeral's baseline.
            if (panel.HP != null) {
                panel.HpDecor = PulpBarDecor.Attach(panel.HP);
                var value = new HBoxContainer {
                    Name = "HpValue",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Alignment = mirrored ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.End
                };
                value.AddThemeConstantOverride("separation", HpValueGap);
                value.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                if (mirrored) value.OffsetLeft = HpValueInset; else value.OffsetRight = -HpValueInset;
                panel.HpCur = new Label {
                    Name = "Cur", MouseFilter = Control.MouseFilterEnum.Ignore,
                    ThemeTypeVariation = UIPalette.LabelRoles.HudNumeral,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
                };
                panel.HpMax = new Label {
                    Name = "Max", MouseFilter = Control.MouseFilterEnum.Ignore,
                    ThemeTypeVariation = UIPalette.LabelRoles.HudNumeralSmall,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    Modulate = new Color(1f, 1f, 1f, HpMaxAlpha)
                };
                value.AddChild(panel.HpCur);
                value.AddChild(panel.HpMax);
                panel.HP.AddChild(value);
                AlignToBaseline(panel.HpCur, panel.HpMax);
            }

            // The Ultimate tube: Meter style (meter -> meter2, full: res -> res2 with its glow), no text; P2 mirrored.
            if (panel.Meter != null) panel.MeterDecor = PulpBarDecor.Attach(panel.Meter, PulpBarStyle.Meter);

            // Defy seal: the icon beside the pips (the old glyph Label stays as the carrier).
            Node pipRow = panel.Root.GetNodeOrNull("Body/Column/PipRow");
            if (pipRow != null) {
                panel.DefyIcon = PulpIcon.Create("defy_notready", UIPalette.Ink3, DefyIconSize);
                panel.DefyIcon.Name = "DefyIcon";
                pipRow.AddChild(panel.DefyIcon);
                if (mirrored) pipRow.MoveChild(panel.DefyIcon, 0);
            }

            // The lens row: S1 / S2 / MOV at 54 (pitch 74) with key labels over the scene (R9), then the two status
            // badges 18 apart. The row's own separation is 0; the gaps are spacers, so each one is the spec's.
            panel.SlotRow = GetNodeOrNull<Control>(mirrored ? "SafeArea/P2_Slots" : "SafeArea/P1_Slots");
            if (panel.SlotRow != null) {
                var widgets = new PulpAbilitySlot[Slots.Length];
                for (int slot = 0; slot < Slots.Length; slot++) {
                    widgets[slot] = new PulpAbilitySlot {
                        Name = CooldownIconNames[slot].Replace("_Cooldown", "_Slot"),
                        SlotSize = AbilitySlotSize,
                        KeyOverScene = true,
                        KeyText = FighterHudModel.CooldownGlyphKey(Slots[slot])
                    };
                }
                panel.SlotWidgets = widgets;
                // The badges sit centred on the 54 px row (4.5, 4L: the damage badge at y 199).
                panel.DamageChip = new PulpStatusChip {
                    Name = "DamageChip", BadgeSize = ChipBadge, Kind = PulpChipKind.Damage, Empty = true,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
                };
                panel.ControlChip = new PulpStatusChip {
                    Name = "ControlChip", BadgeSize = ChipBadge, Kind = PulpChipKind.Control, Empty = true,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
                };
                if (!mirrored) {
                    for (int slot = 0; slot < widgets.Length; slot++) {
                        if (slot > 0) panel.SlotRow.AddChild(Spacer("LensGap" + slot, LensGap));
                        panel.SlotRow.AddChild(widgets[slot]);
                    }
                    panel.SlotRow.AddChild(Spacer("Gap", LensToChipGap));
                    panel.SlotRow.AddChild(panel.DamageChip);
                    panel.SlotRow.AddChild(Spacer("ChipGap", ChipGap));
                    panel.SlotRow.AddChild(panel.ControlChip);
                } else {
                    // Mirrored: the pair of badges keeps damage left, control right; S1 is on the outer edge.
                    panel.SlotRow.AddChild(panel.DamageChip);
                    panel.SlotRow.AddChild(Spacer("ChipGap", ChipGap));
                    panel.SlotRow.AddChild(panel.ControlChip);
                    panel.SlotRow.AddChild(Spacer("Gap", LensToChipGap));
                    for (int slot = widgets.Length - 1; slot >= 0; slot--) {
                        panel.SlotRow.AddChild(widgets[slot]);
                        if (slot > 0) panel.SlotRow.AddChild(Spacer("LensGap" + slot, LensGap));
                    }
                }
            }
        }

        private static Control Spacer(string name, float width) => new() {
            Name = name, CustomMinimumSize = new Vector2(width, 0f), MouseFilter = Control.MouseFilterEnum.Ignore
        };

        /// <summary>
        /// Puts <paramref name="small"/>'s baseline on <paramref name="big"/>'s when both are centred in one row (T17: the
        /// "/max" follows the numeral on its baseline). Two centred lines put their baselines (ascent - descent) / 2 below
        /// the row's middle, so the smaller one is pushed down by the difference through its own stylebox's top margin
        /// (a margin of m moves a centred label's text m / 2). Theme-free and allocation-free after this one call.
        /// </summary>
        private static void AlignToBaseline(Label big, Label small) {
            Font bigFont = big.GetThemeFont("font");
            Font smallFont = small.GetThemeFont("font");
            if (bigFont == null || smallFont == null) return;
            int bigSize = big.GetThemeFontSize("font_size");
            int smallSize = small.GetThemeFontSize("font_size");
            float bigMid = bigFont.GetAscent(bigSize) - bigFont.GetDescent(bigSize);
            float smallMid = smallFont.GetAscent(smallSize) - smallFont.GetDescent(smallSize);
            float drop = Mathf.Max(0f, bigMid - smallMid);
            if (drop <= 0f) return;
            small.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginTop = drop });
        }

        /// <summary>
        /// UI_SPEC_GLASS 4.5's UI-scale rule. The root scales by s (<see cref="UiScaleBinder"/>), so each cluster's and
        /// the timer plate's pinned screen margins are written as margin / s; the HP tube takes
        /// <see cref="FighterHudModel.HpBarWidth"/> (it shrinks before it meets the plate), the Ultimate tube keeps 390 / 510
        /// of it, the lens rows keep their 46 px side margin, and the status badges drop their text column when the canvas
        /// is too narrow for it. Runs when the canvas width or the scale changes.
        /// </summary>
        private void ApplyLayout(bool force = false) {
            if (_root == null) return;
            float scale = HudScale;
            float canvas = _root.Size.X > 1f ? _root.Size.X : FighterHudModel.ReferenceCanvasWidth / scale;
            if (!force && Mathf.IsEqualApprox(canvas, _layoutCanvasWidth) && Mathf.IsEqualApprox(scale, _layoutScale)) return;
            _layoutCanvasWidth = canvas;
            _layoutScale = scale;
            float viewport = canvas * scale;
            float hp = FighterHudModel.HpBarWidth(scale, viewport);
            float meter = FighterHudModel.MeterBarWidth(hp);
            float cluster = FighterHudModel.ClusterWidth(hp);
            bool chipText = FighterHudModel.ChipsShowText(canvas);
            float margin = FighterHudModel.ClusterMargin / scale;
            float top = FighterHudModel.ClusterTop / scale;
            float rowMargin = FighterHudModel.SlotRowMargin / scale;
            for (int index = 0; index < _panels.Length; index++) {
                PlayerPanel panel = _panels[index];
                if (panel == null) continue;
                if (panel.HP != null) panel.HP.CustomMinimumSize = new Vector2(hp, FighterHudModel.HpBarHeight);
                // A narrow tube drops the "/max" so the current value stays clear of the Rally echo band.
                if (panel.HpMax != null) panel.HpMax.Visible = hp >= FighterHudModel.HpMaxTextMinWidth;
                if (panel.Meter != null) panel.Meter.CustomMinimumSize = new Vector2(meter, FighterHudModel.MeterBarHeight);
                if (panel.Root is Control root) {
                    if (index == 0) {
                        root.OffsetLeft = margin;
                        root.OffsetRight = margin + cluster;
                    } else {
                        root.OffsetRight = -margin;
                        root.OffsetLeft = -(margin + cluster);
                    }
                    root.OffsetTop = top;
                    root.OffsetBottom = top + FighterHudModel.ClusterHeight;
                }
                if (panel.SlotRow != null) {
                    panel.SlotRow.OffsetLeft = index == 0 ? rowMargin : -rowMargin;
                    panel.SlotRow.OffsetRight = index == 0 ? rowMargin : -rowMargin;
                }
                if (panel.DamageChip != null) panel.DamageChip.ShowText = chipText;
                if (panel.ControlChip != null) panel.ControlChip.ShowText = chipText;
                LayoutHeader(panel, hp);
            }
            LayoutTimer();
        }

        /// <summary>
        /// The header row (4.5, 3L.c): the 30 px slot shape at the outer end, the name 12 px in from it, "P1" 12 px after
        /// the name (P2 mirrored), each centred on the 30 px row as the mock's flex row does (the name's Cinzel line is 38 px
        /// tall against the mock's line-height 1, so it overhangs the row by 4 px each way and its baseline lands where the
        /// mock's does). A name too long for the column is scaled down to fit (<see cref="FighterHudModel.NameScale"/>),
        /// never below 18 px on screen, and ellipsized beyond that, so the cluster never grows toward the timer.
        /// </summary>
        private void LayoutHeader(PlayerPanel panel, float columnWidth = -1f) {
            Control row = panel?.TopRow;
            if (row == null || panel.Name == null) return;
            // The column's width: given by ApplyLayout (before the column re-sorts), else the row's own once laid out.
            float width = columnWidth > 0f ? columnWidth
                : row.Size.X > 1f ? row.Size.X
                : panel.HP?.CustomMinimumSize.X ?? FighterHudModel.HpBarFullWidth;
            float height = Mathf.Max(row.Size.Y, SlotShapeBox);
            bool mirrored = panel.Index == 1;
            Label name = panel.Name, tag = panel.PTag;

            Vector2 tagSize = tag?.GetCombinedMinimumSize() ?? Vector2.Zero;
            float tagRoom = tag != null && tag.Text.Length > 0 ? tagSize.X + HeaderGap : 0f;
            float textWidth = CardTextFit.TextWidth(name);
            float room = Mathf.Max(1f, width - SlotShapeBox - HeaderGap - tagRoom);
            int fontSize = name.GetThemeFontSize("font_size");
            float scale = FighterHudModel.NameScale(textWidth, room, fontSize, HudScale);
            float labelWidth = Mathf.Min(textWidth + 1f, room / scale);
            float nameHeight = name.GetCombinedMinimumSize().Y;
            float drawn = labelWidth * scale;
            float nameTop = (height - nameHeight) / 2f;

            if (panel.SlotShape != null) {
                panel.SlotShape.Position = new Vector2(mirrored ? width - SlotShapeBox : 0f, (height - SlotShapeBox) / 2f);
            }
            name.Size = new Vector2(labelWidth, nameHeight);
            name.Scale = new Vector2(scale, scale);
            if (!mirrored) {
                float left = SlotShapeBox + HeaderGap;
                name.PivotOffset = new Vector2(0f, nameHeight / 2f);
                name.Position = new Vector2(left, nameTop);
                if (tag != null) {
                    tag.Size = tagSize;
                    tag.Position = new Vector2(left + drawn + HeaderGap, (height - tagSize.Y) / 2f);
                }
            } else {
                float right = width - SlotShapeBox - HeaderGap;
                name.PivotOffset = new Vector2(labelWidth, nameHeight / 2f);
                name.Position = new Vector2(right - labelWidth, nameTop);
                if (tag != null) {
                    tag.Size = tagSize;
                    tag.Position = new Vector2(right - drawn - HeaderGap - tagSize.X, (height - tagSize.Y) / 2f);
                }
            }
        }

        /// <summary>
        /// The glass timer plate (4.5, row 5): 234 x 111.6 hung 22 px from the top centre, the T34 digits on their 60 px
        /// line, 6 px, the T14 mode label on its 21.6 px line (each label's taller box centred on its line, so its
        /// baseline sits where the mock's does). Sudden Death replaces both with one line in the label role and widens the
        /// plate to fit it, at most to the clearance the HP tubes leave (beyond that the line wraps).
        /// </summary>
        private void LayoutTimer() {
            if (_timer == null || _timerPlate == null) return;
            float scale = HudScale;
            float viewport = _root != null && _root.Size.X > 1f ? _root.Size.X * scale : FighterHudModel.ReferenceCanvasWidth;
            float top = FighterHudModel.TimerPlateTop / scale;
            float padX = FighterHudModel.TimerPlatePaddingX, padY = FighterHudModel.TimerPlatePaddingY;
            if (!_suddenDeathShown) {
                float half = FighterHudModel.TimerPlateWidth / 2f;
                float inner = half - padX;
                SetOffsets(_timerPlate, -half, top, half, top + FighterHudModel.TimerPlateHeight);
                float digits = top + padY + FighterHudModel.TimerDigitsHeight / 2f;
                CentreOnLine(_timer, -inner, inner, digits);
                if (_modeLabel != null) {
                    float caption = top + padY + FighterHudModel.TimerDigitsHeight + FighterHudModel.TimerLabelGap
                        + FighterHudModel.TimerLabelHeight / 2f;
                    CentreOnLine(_modeLabel, -inner, inner, caption);
                }
                return;
            }
            // A Label's cached minimum size does not follow a wrap or width change, so each measure asks the label
            // itself (GetMinimumSize reshapes at the label's current width) after invalidating the cache.
            _timer.AutowrapMode = TextServer.AutowrapMode.Off;
            _timer.UpdateMinimumSize();
            float natural = _timer.GetMinimumSize().X;
            float width = FighterHudModel.SuddenDeathPlateWidth(natural, FighterHudModel.TimerPlateMaxWidth(scale, viewport));
            float lineWidth = width - 2f * padX;
            if (natural > lineWidth + 0.5f) _timer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _timer.UpdateMinimumSize();
            SetOffsets(_timer, -lineWidth / 2f, top + padY, lineWidth / 2f, top + padY + 1f);
            _timer.UpdateMinimumSize();
            float lines = _timer.GetMinimumSize().Y;
            SetOffsets(_timer, -lineWidth / 2f, top + padY, lineWidth / 2f, top + padY + lines);
            SetOffsets(_timerPlate, -width / 2f, top, width / 2f, top + padY + lines + padY);
        }

        private static void SetOffsets(Control control, float left, float top, float right, float bottom) {
            control.OffsetLeft = left;
            control.OffsetTop = top;
            control.OffsetRight = right;
            control.OffsetBottom = bottom;
        }

        // A label's box is its font's line (ascent + descent), taller than the mock's CSS line box; centring the box on
        // the line's centre puts the text where the CSS puts it.
        private static void CentreOnLine(Label label, float left, float right, float centreY) {
            label.UpdateMinimumSize();
            float height = label.GetMinimumSize().Y;
            SetOffsets(label, left, centreY - height / 2f, right, centreY + height / 2f);
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
                // The authored TextureRect keeps the texture (it is the old seam); the 110 px frame draws it.
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
            _presentedPalette = (int)FTT.Combat.PlayerSlotPalettes.Active;
            ApplySlotIdentity(panel, playerIndex);

            panel.Stocks = RebuildPips(panel.StockPips, Mathf.Max(1, maxStocks), StockLitIcon, StockSize, StockSpacing,
                UIPalette.OwnerOnDark(playerIndex));
            panel.Shields = RebuildPips(panel.ShieldPips, Mathf.Max(1, maxShieldCharges), ShieldLitIcon, ShieldSize,
                ShieldSpacing, UIPalette.Shield);
            panel.PresentedStocksLit = -1;
            panel.PresentedShieldsLit = -1;
            LayoutHeader(panel);
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
                TintFill(panel.HP, FighterHudModel.HpFillColor(state.CurrentHP, state.MaxHP));
                if (panel.PresentedHP != hp || panel.PresentedMaxHP != maxHp) {
                    PresentHpChange(panel, hp, maxHp);
                    panel.PresentedHP = hp;
                    panel.PresentedMaxHP = maxHp;
                    if (panel.HpCur != null) panel.HpCur.Text = hp.ToString();
                    if (panel.HpMax != null) panel.HpMax.Text = "/" + maxHp;
                } else if (!IsHealing(panel)) {
                    panel.HP.Value = hp;
                }
                if (panel.HpDecor != null) {
                    // The Rally echo: the tube draws the band from the fill end to HP + echo (the shipped
                    // EchoBandFraction rule, in the bar's units), so nothing else draws it.
                    float band = FighterHudModel.EchoBandFraction(state.CurrentHP, echoPool, state.MaxHP);
                    panel.HpDecor.EchoValue = band > 0f ? hp + band * maxHp : 0f;
                    panel.HpDecor.Low = hp > 0 && FighterHudModel.BarFraction(hp, maxHp) <= FighterHudModel.HpDangerFraction;
                }
            }
            if (panel.Meter != null) {
                panel.Meter.MaxValue = FighterHudModel.MaxInfluence;
                panel.Meter.Value = Mathf.Clamp(state.Influence.ToFloat(), 0f, FighterHudModel.MaxInfluence);
            }

            ApplyStocks(panel, state.Stocks, force: false);
            ApplyShields(panel, state.BlockCharges, force: false);
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
        /// MOT-A row 7 on an HP change. MOT-A has no hit flash (plan 0.4 P7): a drop jumps the fill and the numerals to
        /// the authoritative value while the damage tracker holds 0.5 s, then drains over 0.6 s (a fade under Reduced
        /// Temporal Effects); a rise (healing, the Rally reclaim) raises the fill over 0.2 s with the tracker riding it,
        /// so no red ever shows on a heal. The first presentation and a new maximum jump with the tracker on the fill.
        /// </summary>
        private static void PresentHpChange(PlayerPanel panel, int hp, int maxHp) {
            bool first = panel.PresentedHP < 0;
            bool healed = !first && maxHp == panel.PresentedMaxHP && hp > panel.PresentedHP;
            float shown = (float)panel.HP.Value;
            if (panel.HealTween != null && panel.HealTween.IsValid()) panel.HealTween.Kill();
            panel.HealTween = null;
            if (healed) {
                PulpBarDecor decor = panel.HpDecor;
                ProgressBar bar = panel.HP;
                if (decor != null && decor.LagValue > shown) decor.LagValue = shown;
                panel.HealTween = PulpMotion.HealRise(bar, shown, hp, v => {
                    if (!GodotObject.IsInstanceValid(bar)) return;
                    bar.Value = v;
                    if (decor != null && GodotObject.IsInstanceValid(decor)) decor.LagValue = v;
                });
                return;
            }
            panel.HP.Value = hp;
            if (panel.HpDecor == null) return;
            if (first || maxHp != panel.PresentedMaxHP) panel.HpDecor.LagValue = hp;
            else panel.HpDecor.DropLagTo(hp);
        }

        private static bool IsHealing(PlayerPanel panel) =>
            panel.HealTween != null && panel.HealTween.IsValid() && panel.HealTween.IsRunning();

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
        /// no timer left to count. The glass plate puts that line in its label slot
        /// and widens to fit it (UI_SPEC_GLASS 4.5).
        ///
        /// <para>The last ten seconds of regulation pulse red and chime once. The
        /// chime is a single edge, not a per-frame call — a timer cue that
        /// retriggers every frame is the classic way to turn a warning into
        /// noise — and it goes out on the Critical Cues path so a background
        /// profile cannot muffle it. The clock's text is rebuilt only when its
        /// second changes, so a running match allocates nothing per frame.</para>
        /// </summary>
        public void ApplyMatchState(
            bool timerEnabled, int remainingFrames, int tickRate, int matchMode, bool suddenDeath) {
            _matchMode = matchMode;
            _suddenDeath = suddenDeath;
            if (_timer == null) return;
            SetModeLabel(matchMode);

            if (suddenDeath) {
                if (!_suddenDeathShown) EnterSuddenDeathClock();
                SetTimerShown(true);
                _lastTimerSeconds = -1;
                return;
            }
            if (_suddenDeathShown) LeaveSuddenDeathClock();

            bool visible = FighterHudModel.TimerIsVisible(timerEnabled);
            SetTimerShown(visible);
            if (!visible) {
                _lastTimerSeconds = -1;
                return;
            }
            int seconds = FighterHudModel.TotalSeconds(remainingFrames, tickRate);
            if (seconds != _presentedClockSeconds) {
                _presentedClockSeconds = seconds;
                _timer.Text = string.Format(
                    Tr("fighter_hud_clock"),
                    FighterHudModel.ClockMinutes(remainingFrames, tickRate).ToString("D2"),
                    FighterHudModel.ClockSeconds(remainingFrames, tickRate).ToString("D2"));
            }
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

        // F22 (4.5): the phase line takes the label slot in the label role (T14, upper case, Danger), the digits and the
        // mode caption leave, and the plate widens to fit the line.
        private void EnterSuddenDeathClock() {
            _suddenDeathShown = true;
            _timer.ThemeTypeVariation = UIPalette.LabelRoles.CaptionLabel;
            _timer.Uppercase = true;
            _timer.Text = Tr("fighter_hud_sudden_death_clock");
            _timer.AddThemeColorOverride("font_color", UIPalette.Danger);
            _timerDangerShown = false;
            _presentedClockSeconds = -1;
            if (_modeLabel != null) _modeLabel.Visible = false;
            LayoutTimer();
        }

        private void LeaveSuddenDeathClock() {
            _suddenDeathShown = false;
            _timer.ThemeTypeVariation = UIPalette.LabelRoles.TimerDigits;
            _timer.Uppercase = false;
            _timer.AutowrapMode = TextServer.AutowrapMode.Off;
            _timer.RemoveThemeColorOverride("font_color");
            _timerDangerShown = false;
            _presentedClockSeconds = -1;
            if (_modeLabel != null) _modeLabel.Visible = _timerShown;
            LayoutTimer();
        }

        /// <summary>Shows or hides the clock, its glass plate and the mode label together (Sudden Death has no mode label).</summary>
        private void SetTimerShown(bool shown) {
            _timerShown = shown;
            if (_timer != null) _timer.Visible = shown;
            if (_timerPlate != null) _timerPlate.Visible = shown;
            if (_modeLabel != null) _modeLabel.Visible = shown && !_suddenDeathShown;
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
            ApplyPaletteChange();
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
        /// The ownership colours follow the player-slot palette live (Settings, Gameplay, reachable from the match's
        /// pause menu): the slot shapes and the stock stars re-tint when it changes. One int compare per frame.
        /// </summary>
        private void ApplyPaletteChange() {
            int palette = (int)FTT.Combat.PlayerSlotPalettes.Active;
            if (palette == _presentedPalette) return;
            _presentedPalette = palette;
            for (int index = 0; index < _panels.Length; index++) {
                PlayerPanel panel = _panels[index];
                if (panel == null) continue;
                ApplySlotIdentity(panel, index);
                ApplyStocks(panel, panel.PresentedStocksLit < 0 ? panel.Stocks.Length : panel.PresentedStocksLit, force: true);
            }
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

        private static Color Grey(Color colour) {
            float luminance = 0.2126f * colour.R + 0.7152f * colour.G + 0.0722f * colour.B;
            return new Color(luminance, luminance, luminance, colour.A);
        }

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

        /// <summary>Rebuilds a pip row from kit icons (UI_SPEC_GLASS 3.4.4), every pip lit until the first state arrives.</summary>
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
        /// The stock stars (3.4.4): a held stock is the filled star in the owner colour, a lost one the outline at 0.22.
        /// A spent pip is dimmed rather than removed, so the panel never reflows mid-match and the player can still see
        /// the capacity they started with. Writes only when the count (or, forced, the palette) changes.
        /// </summary>
        private static void ApplyStocks(PlayerPanel panel, int filled, bool force) {
            Control[] pips = panel.Stocks;
            if (pips == null || pips.Length == 0) return;
            int lit = FighterHudModel.FilledPips(filled, pips.Length);
            if (!force && lit == panel.PresentedStocksLit) return;
            panel.PresentedStocksLit = lit;
            Color owner = UIPalette.OwnerOnDark(panel.Index);
            for (int index = 0; index < pips.Length; index++) {
                bool on = index < lit;
                if (pips[index] is PulpIcon icon) {
                    icon.IconName = on ? StockLitIcon : StockOffIcon;
                    icon.Tint = owner;
                }
                pips[index].Modulate = new Color(1f, 1f, 1f, on ? 1f : StockOffAlpha);
            }
        }

        /// <summary>The block pips (3.4.4): charged = the shield with its glow, spent = the glowless grey shield at 0.28.</summary>
        private static void ApplyShields(PlayerPanel panel, int filled, bool force) {
            Control[] pips = panel.Shields;
            if (pips == null || pips.Length == 0) return;
            int lit = FighterHudModel.FilledPips(filled, pips.Length);
            if (!force && lit == panel.PresentedShieldsLit) return;
            panel.PresentedShieldsLit = lit;
            for (int index = 0; index < pips.Length; index++) {
                bool on = index < lit;
                if (pips[index] is PulpIcon icon) {
                    icon.IconName = on ? ShieldLitIcon : ShieldOffIcon;
                    icon.Tint = on ? UIPalette.Shield : ShieldOffTint;
                }
                pips[index].Modulate = new Color(1f, 1f, 1f, on ? 1f : ShieldOffAlpha);
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
        /// M25 (HUD_CONTRACT): three cooldowns — Special 1 / Special 2 / Movement — from the runtime component's
        /// authoritative state. The visible lens draws the sweep, the readiness ring and the seconds, and plays its own
        /// single 600 ms bloom on becoming ready (MOT-A row 10; the brief ready flash). The glyph never changes, so
        /// readiness is carried by the ring, the sweep and the dimming, never by colour alone.
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
                if (!readinessChanged && panel.PresentedCooldownFrames[index] == frames) continue;
                panel.PresentedReady[index] = readyState;
                panel.PresentedCooldownFrames[index] = frames;

                RadialProgress radial = panel.CooldownRadials[index];
                if (radial != null) {
                    radial.Fraction = FighterHudModel.CooldownRadialFraction(
                        frames, panel.CooldownStartFrames[index]);
                }
                if (index < panel.SlotWidgets.Length && panel.SlotWidgets[index] != null) {
                    // The visible 54 px lens: the sweep over the elapsed share, the ring and the remaining seconds.
                    PulpAbilitySlot widget = panel.SlotWidgets[index];
                    widget.State = ready ? PulpSlotState.Ready : PulpSlotState.Cooling;
                    widget.Progress = FighterHudModel.CooldownRadialFraction(frames, panel.CooldownStartFrames[index]);
                    widget.SecondsRemaining = FighterHudModel.CooldownSeconds(frames, FighterSimulation.TickRate);
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
        /// on a player's Special 1 / Special 2 lens or Ultimate tube, for each slot
        /// <see cref="FighterLockoutRefusalRules"/> found refused by the ability
        /// lockout alone. A lens plays the glass circle round its readiness ring
        /// (UI_SPEC_GLASS 3.4.2), the lens as the tint target; the Ultimate tube
        /// the frame, its bar as the tint target (the tube follows the bar's
        /// <c>SelfModulate</c>). The hidden glyph carrier flashes too (the
        /// accessibility and test seam). The tint rides <c>SelfModulate</c>, so the
        /// readiness alpha and the ready bloom (both on <c>Modulate</c>) are untouched.
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
            // The label is the hidden carrier; the visible flash is the glass circle round the lens's ring.
            if (index < panel.SlotWidgets.Length && panel.SlotWidgets[index] != null) {
                PulpAbilitySlot widget = panel.SlotWidgets[index];
                NotYetFlash.Play(widget.Ring, widget, NotYetShape.Circle);
            }
        }

        /// <summary>
        /// G12: the slot shape (P1 ▲ / P2 ●) beside the name, in the local player-slot palette's colour on dark chrome
        /// (<see cref="UIPalette.OwnerOnDark"/>: the High-Contrast palette's black player 2 reads as its light edge).
        /// Presentation only — the palette never reaches the simulation.
        /// </summary>
        private static void ApplySlotIdentity(PlayerPanel panel, int playerIndex) {
            Color slotColour = FTT.Combat.PlayerSlotPalettes.ActiveSlotColor(playerIndex);
            Color edge = FTT.Combat.PlayerSlotPalettes.ActiveSlotEdgeColor(playerIndex);
            // The ▲ / ● Label is the hidden text carrier; the shape icon carries identity (never colour alone),
            // and the name reads in the HeroNameScene ink role.
            if (panel.SlotBadge != null) {
                panel.SlotBadge.Text = FTT.Combat.PlayerSlotPalettes.ShapeGlyph(playerIndex);
                panel.SlotBadge.AddThemeColorOverride("font_color", slotColour);
                panel.SlotBadge.AddThemeColorOverride("font_outline_color", edge);
            }
            if (panel.SlotShapeIcon != null) panel.SlotShapeIcon.Tint = UIPalette.OwnerOnDark(playerIndex);
        }

        /// <summary>
        /// Repaints a bar's fill without disturbing the themed trough. A Theme
        /// cannot express "this bar's fill colour depends on its value", so the
        /// override is duplicated from whatever box is currently in force and
        /// recoloured — and only when the band actually changed, so a match spends
        /// no frames allocating StyleBoxes. The glass tube reads this colour.
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
