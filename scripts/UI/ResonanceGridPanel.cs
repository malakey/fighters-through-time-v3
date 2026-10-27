using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI.Motion;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    public partial class ResonanceGridPanel : Control {
        /// <summary>The smallest node chip (V7.6 200 x 90). UR-POLISH sizes the real chip from its text, never below this.</summary>
        public static readonly Vector2 NodeChipSize = new(200, 90);

        /// <summary>The constellation area at UI scale 1.0 (it grows with the UI scale, and the panel fit shrinks the whole panel back).</summary>
        public static readonly Vector2 BaseGridSize = new(1480, 540);

        /// <summary>The gap kept between two chips, edge to edge.</summary>
        public const float ChipGap = 14f;

        /// <summary>Extra clear space above and below the panel so the bottom-right prompt bar (top at 1006 of 1080) never touches its frame.</summary>
        public const float PromptBarClearance = 56f;

        private const float ChipMarginLeft = 12f, ChipMarginRight = 38f, ChipMarginTop = 8f, ChipMarginBottom = 8f;

        /// <summary>The dim over the sub-screen art behind the grid (UI_SPEC_GLASS 9.2: "sub-screen art, dim .55").</summary>
        public const float ArtDimAlpha = 0.55f;

        /// <summary>The dormant glyph's size in the chip's top-right corner (where a named chip shows its tier).</summary>
        public const float DormantGlyphSize = 24f;

        /// <summary>The authored scene this panel presents (Package 8 B4).</summary>
        public const string ScenePath = "res://scenes/ui/ResonanceGrid.tscn";

        public event Action Closed;
        private ResonanceGridData _grid;
        private StorySaveData _save;
        private int _slot;
        private Label _balanceLabel;
        private Label _statusLabel;
        private Control _nodeGrid;
        private ResonanceEdgeLayer _edgeLayer;
        private ConfirmModal _confirmation;
        private ConfirmModal _respecConfirmation;
        private Button _respecButton;
        private ResonanceNodeData _pendingNode;
        private int _focusedIndex;
        // TG-SCREENS: a chip draws no box of its own (its glass tile is a GlassTileDecor behind it); this empty box only
        // carries the text margins every state shares (12 left, 38 right for the corner mark, 8 top and bottom).
        private StyleBoxEmpty _chipBox;
        private readonly Dictionary<string, ResonanceUnlockResult> _chipStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GlassTileDecor> _tiles = new(StringComparer.Ordinal);
        private readonly List<ResonanceNodeData> _nodes = new();
        private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
        private Vector2 _chipSize = NodeChipSize;
        private Vector2 _gridArea = BaseGridSize;
        private (float X, float Y)[] _placed = Array.Empty<(float X, float Y)>();

        /// <summary>The chip size the panel measured from its text. Test surface.</summary>
        public Vector2 ChipSize => _chipSize;

        /// <summary>The constellation area the panel laid the chips out in. Test surface.</summary>
        public Vector2 GridArea => _gridArea;

        /// <summary>The node chips by node ID. Test surface.</summary>
        internal IReadOnlyDictionary<string, Button> Chips => _buttons;

        /// <summary>The instantiated authored scene root. Test surface.</summary>
        public Control Root { get; private set; }

        /// <summary>The shared confirmation modal guarding a purchase.</summary>
        public ConfirmModal Confirmation => _confirmation;

        /// <summary>The confirmation modal guarding the V7.3 free respec. Test surface.</summary>
        public ConfirmModal RespecConfirmation => _respecConfirmation;

        /// <summary>The "Respec (full refund)" button. Test surface.</summary>
        public Button RespecButton => _respecButton;

        /// <summary>The node index the custom D-pad navigation is sitting on.</summary>
        public int FocusedIndex => _focusedIndex;

        /// <summary>
        /// Package 8 B4. The grid is now an authored scene at
        /// <see cref="ScenePath"/> rather than ~60 lines of construction here.
        ///
        /// <para>Following the A4 precedent for <c>Settings.tscn</c>, the scene root
        /// is script-less and this class instantiates it, so the single opener
        /// (<c>HubWorldController</c>, owned by other Package 8 workstreams) keeps
        /// working through <c>new ResonanceGridPanel()</c> unchanged.</para>
        ///
        /// <para>The node buttons themselves stay code-built: their count and copy
        /// come from the character's authored <c>ResonanceGridData</c>, so they
        /// cannot be authored in the scene without duplicating content.</para>
        /// </summary>
        public override void _Ready() {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            UIPalette.ApplyTheme(this);

            Root = InstantiateAuthoredScene();
            if (Root == null) return;
            AddChild(Root);

            _balanceLabel = Root.GetNode<Label>("Center/Panel/Layout/Balance");
            _statusLabel = Root.GetNode<Label>("Center/Panel/Layout/Status");
            _nodeGrid = Root.GetNode<Control>("Center/Panel/Layout/NodeGrid");
            Button closeButton = Root.GetNode<Button>("Center/Panel/Layout/CloseButton");
            closeButton.Pressed += Close;
            BuildRespecButton(closeButton);

            // TG-SCREENS (UI_SPEC_GLASS 9.2): a full-screen menu. The sub-screen art under a .55 dim, one glass slab
            // with the constellation tag, glass chips (the GlassTileDecor tile at chamfer 12, an edge per state) and the
            // screen streak; the chip layout, separation and fit rules are UR-POLISH's, unchanged.
            GlassScreenDressing.AddMenuArt(Root);
            if (Root.GetNodeOrNull<ColorRect>("Shade") is { } shade) shade.Color = new Color(UIPalette.ScrimFill, ArtDimAlpha);
            _chipBox = new StyleBoxEmpty {
                ContentMarginLeft = ChipMarginLeft, ContentMarginRight = ChipMarginRight, // room for the corner mark
                ContentMarginTop = ChipMarginTop, ContentMarginBottom = ChipMarginBottom
            };
            if (Root.GetNodeOrNull<PanelContainer>("Center/Panel") is { } gridPanel) {
                MenuSafeAreaFit.Attach(gridPanel, PromptBarClearance);
                PulpScreenKit.DressPanel(gridPanel, "resonance_grid_tag");
            }
            // The balance line: Outfit 500 20 in res (9.2); the nearest role is SmallLabel (500, 18), coloured res.
            _balanceLabel.AddThemeColorOverride("font_color", UIPalette.Res);
            PulpButtonDecor closeDecor = PulpButtonDecor.Apply(closeButton, compact: true);
            closeDecor.GrowToFit = true;
            closeDecor.ShowPrompt = false;
            // D6 (UI_SPEC_GLASS 3.2): the compact row shares its widest required width, at least 290, with no prompt badge.
            PulpButtonDecor.ApplyStackWidth(new[] { _respecButton, closeButton }, 290f, reservePrompt: false);
            PulpScreenKit.AddPromptBar(this, ("ui_accept", "ui_prompt_select"), ("ui_cancel", "common_back"));

            // The native ConfirmationDialog this replaces opened an OS-level window,
            // ignored the theme, and trapped no focus. A purchase is destructive
            // (dust is spent permanently), so it belongs on the shared modal.
            _confirmation = ConfirmModal.Create(
                "resonance_confirm_unlock",
                "resonance_confirm_ok",
                "resonance_confirm_cancel",
                "resonance_confirm_title");
            _confirmation.Confirmed += ConfirmUnlock;
            _confirmation.Cancelled += () => _pendingNode = null;
            AddChild(_confirmation);

            // V7.3 free respec: clearing the whole grid is destructive in
            // shape (even with a full refund), so it rides the same modal
            // idiom as a purchase.
            _respecConfirmation = ConfirmModal.Create(
                "resonance_respec_confirm",
                "resonance_confirm_ok",
                "resonance_confirm_cancel",
                "resonance_respec_title");
            _respecConfirmation.Name = "RespecConfirmModal";
            _respecConfirmation.Confirmed += ConfirmRespec;
            AddChild(_respecConfirmation);
            // The streak is a full-screen menu's (it draws at z 10); the two modals are overlays, which never show it,
            // so they draw above it.
            PulpScreenKit.AddScreenStreak(this);
            _confirmation.ZIndex = ScreenStreak.StreakZ + 1;
            _respecConfirmation.ZIndex = ScreenStreak.StreakZ + 1;
            LoadActiveGrid();
        }

        /// <summary>
        /// The node buttons are code-built from authored grid data, and this
        /// button follows them (the scene stays script-less): inserted above
        /// the Close button in the authored layout.
        /// </summary>
        private void BuildRespecButton(Button closeButton) {
            _respecButton = new Button {
                Name = "RespecButton",
                Text = "resonance_respec_button",
                ThemeTypeVariation = UIPalette.Variations.PulpButtonCompact
            };
            _respecButton.Pressed += ShowRespecConfirmation;
            // UR-POLISH: the two buttons share one row (the stacked pair pushed the panel into the prompt bar).
            var layout = (Container)closeButton.GetParent();
            var row = new HBoxContainer { Name = "ButtonRow", Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 20);
            layout.AddChild(row);
            layout.MoveChild(row, closeButton.GetIndex());
            closeButton.Reparent(row, false);
            row.AddChild(_respecButton);
            row.MoveChild(_respecButton, 0);
            _respecButton.CustomMinimumSize = new Vector2(0f, 58f);
            PulpButtonDecor respecDecor = PulpButtonDecor.Apply(_respecButton, compact: true);
            respecDecor.GrowToFit = true;
            respecDecor.ShowPrompt = false;
            PulpScreenKit.SeparateFocusRow(row);
        }

        private void ShowRespecConfirmation() {
            if (_grid == null || _save == null || _respecConfirmation == null) return;
            int spent = ResonanceProgression.CalculateSpentDust(_grid, _save);
            _respecConfirmation.SetPromptKey(string.Format(Tr("resonance_respec_confirm"), spent));
            _respecConfirmation.Open();
        }

        private void ConfirmRespec() {
            if (_grid == null || _save == null) return;
            int refunded = ResonanceProgression.RespecAll(_grid, _save);
            _statusLabel.Text = string.Format(Tr("resonance_respec_done"), refunded);
            Refresh();
            // Persist immediately, mirroring the unlock path's autosave.
            if (refunded > 0 && SaveManager.Instance != null
                && _slot >= 0 && _slot < SaveManager.Instance.SaveSlots.Length) {
                SaveManager.Instance.SaveStorySlot(_slot);
            }
        }

        private static Control InstantiateAuthoredScene() {
            if (!ResourceLoader.Exists(ScenePath)) return null;
            // A PackedScene is streamable content, not authored tuning data, so it
            // deliberately does not go through AuthoredResources' pinning cache.
            var packed = ResourceLoader.Load<PackedScene>(ScenePath);
            return packed?.InstantiateOrNull<Control>();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event == null) return;
            if (_confirmation != null && _confirmation.IsOpen) return;
            if (_respecConfirmation != null && _respecConfirmation.IsOpen) return;
            if (@event.IsActionPressed("ui_cancel")) {
                GetViewport().SetInputAsHandled();
                Close();
                return;
            }
            if (_nodes.Count == 0) return;
            int deltaColumn = 0;
            int deltaRow = 0;
            if (@event.IsActionPressed("ui_left", true)) deltaColumn = -1;
            else if (@event.IsActionPressed("ui_right", true)) deltaColumn = 1;
            else if (@event.IsActionPressed("ui_up", true)) deltaRow = -1;
            else if (@event.IsActionPressed("ui_down", true)) deltaRow = 1;
            if (deltaColumn != 0 || deltaRow != 0) {
                GetViewport().SetInputAsHandled();
                SetFocusedIndex(ResonanceGridNavigation.Move(
                    _focusedIndex, deltaColumn, deltaRow, LayoutPositions));
                return;
            }
            if (@event.IsActionPressed("ui_accept")) {
                // CM-PADUI (2026-10-16): when Close or Respec holds GUI focus (a
                // mouse click gives it), that button takes this press on release;
                // the highlighted node must not be bought on the same press.
                if (PadMenuInput.FocusedButtonTakesAccept(GetViewport(), @event)) return;
                GetViewport().SetInputAsHandled();
                ActivateNode(_nodes[_focusedIndex]);
            }
        }

        private void LoadActiveGrid() {
            if (_nodeGrid == null || GameManager.Instance == null || SaveManager.Instance == null) return;
            _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (_slot < 0 || _slot >= SaveManager.Instance.SaveSlots.Length) return;
            _save = SaveManager.Instance.SaveSlots[_slot];
            string characterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "";
            // Shared pinned instance - never ResourceLoader.Load a grid directly.
            _grid = ResonanceProgression.LoadGrid(characterID);
            if (_save == null || _grid == null) {
                _statusLabel.Text = Tr("resonance_grid_unavailable");
                return;
            }
            // V7.6: the flat 3-column GridContainer is gone. Nodes are placed
            // at their AUTHORED normalized LayoutPosition through anchors, so
            // each topology renders as designed at any panel size, and the edge
            // layer sits behind them.
            _edgeLayer = new ResonanceEdgeLayer { Name = "EdgeLayer" };
            _edgeLayer.SetAnchorsPreset(LayoutPreset.FullRect);
            _nodeGrid.AddChild(_edgeLayer);
            _nodeGrid.Resized += RebuildEdges;

            foreach (ResonanceNodeData node in _grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node != null) _nodes.Add(node);
            }
            PlanLayout();
            var planned = new List<ResonanceNodeData>(_nodes);
            _nodes.Clear();
            foreach (ResonanceNodeData node in planned) {
                int index = _nodes.Count;
                _nodes.Add(node);
                var button = new Button {
                    Name = node.NodeID,
                    CustomMinimumSize = _chipSize,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    FocusMode = FocusModeEnum.None
                };
                PlaceChip(button, _placed[index]);
                button.Pressed += () => {
                    SetFocusedIndex(index);
                    ActivateNode(node);
                };
                foreach (string slot in ChipBoxSlots) button.AddThemeStyleboxOverride(slot, _chipBox);
                // The glass tile draws behind the chip's own text (the chip button draws only that text).
                GlassTileDecor tile = GlassTileDecor.Attach(button, GlassTileDecor.ChipChamfer);
                tile.ShowBehindParent = true;
                _tiles[node.NodeID] = tile;
                var mark = new ResonanceTierMark { Tier = node.Tier, Name = "TierMark" };
                button.AddChild(mark);
                PulpIcon dormant = PulpIcon.Create("dormant", UIPalette.Ink3, DormantGlyphSize);
                dormant.Name = "DormantGlyph";
                dormant.Visible = false;
                button.AddChild(dormant);
                dormant.AnchorLeft = 1f;
                dormant.AnchorRight = 1f;
                dormant.OffsetLeft = -10f - DormantGlyphSize;
                dormant.OffsetRight = -10f;
                dormant.OffsetTop = 8f;
                dormant.OffsetBottom = 8f + DormantGlyphSize;
                _nodeGrid.AddChild(button);
                _buttons[node.NodeID] = button;
            }
            Refresh();
            SetFocusedIndex(0);
            RebuildEdges();
        }

        /// <summary>
        /// UR-POLISH. Sizes the chips from their own text (so no word breaks and every line, the state included, fits),
        /// grows the constellation with the UI scale, and separates chips the authored positions put on top of each other.
        /// </summary>
        private void PlanLayout() {
            float scale = Mathf.Max(1f, UIPalette.AppliedUiScale);
            Font font = _nodeGrid.GetThemeFont("font", "Button");
            int fontSize = _nodeGrid.GetThemeFontSize("font_size", "Button");
            float lineHeight = font != null ? font.GetHeight(fontSize) : 24f;
            float horizontal = ChipMarginLeft + ChipMarginRight;
            var texts = new List<string>();
            float longestWord = 0f;
            foreach (ResonanceNodeData node in _nodes) {
                foreach (ResonanceUnlockResult state in new[] { ResonanceUnlockResult.AlreadyUnlocked, ResonanceUnlockResult.MissingPrerequisite }) {
                    string text = NodeText(node, state);
                    texts.Add(text);
                    if (font == null) continue;
                    foreach (string word in text.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                        longestWord = Mathf.Max(longestWord, font.GetStringSize(word, HorizontalAlignment.Left, -1f, fontSize).X);
                    }
                }
            }
            float width = Mathf.Clamp(longestWord + horizontal + 8f, NodeChipSize.X, 330f * scale);
            int maxLines = 1;
            if (font != null) {
                foreach (string text in texts) {
                    var paragraph = new TextParagraph();
                    paragraph.AddString(text, font, fontSize);
                    paragraph.Width = width - horizontal;
                    paragraph.BreakFlags = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound;
                    maxLines = Mathf.Max(maxLines, paragraph.GetLineCount());
                }
            }
            float height = Mathf.Max(NodeChipSize.Y, Mathf.Ceil(maxLines * lineHeight + ChipMarginTop + ChipMarginBottom + 8f));
            _chipSize = new Vector2(Mathf.Ceil(width), height);

            var authored = new List<(float X, float Y)>(_nodes.Count);
            foreach (ResonanceNodeData node in _nodes) {
                authored.Add((Mathf.Clamp(node.LayoutPosition.X, 0f, 1f), Mathf.Clamp(node.LayoutPosition.Y, 0f, 1f)));
            }
            _gridArea = BaseGridSize * scale;
            for (int attempt = 0; attempt < 8; attempt++) {
                _placed = ResonanceChipLayout.Separate(authored, _gridArea.X, _gridArea.Y, _chipSize.X, _chipSize.Y, ChipGap, out bool resolved);
                if (resolved) break;
                _gridArea = new Vector2(_gridArea.X, _gridArea.Y * 1.1f); // grow the height first (the panel is wide already)
            }
            _nodeGrid.CustomMinimumSize = _gridArea;
        }

        /// <summary>Anchors a node chip so its CENTRE sits at the planned normalized position (survives a panel resize).</summary>
        private void PlaceChip(Control chip, (float X, float Y) position) {
            chip.AnchorLeft = position.X;
            chip.AnchorRight = position.X;
            chip.AnchorTop = position.Y;
            chip.AnchorBottom = position.Y;
            chip.OffsetLeft = -_chipSize.X * 0.5f;
            chip.OffsetRight = _chipSize.X * 0.5f;
            chip.OffsetTop = -_chipSize.Y * 0.5f;
            chip.OffsetBottom = _chipSize.Y * 0.5f;
        }

        /// <summary>
        /// Rebuilds the prerequisite edges: solid for All-of, dotted for
        /// Any-of, glowing once the prerequisite end is unlocked.
        /// </summary>
        private void RebuildEdges() {
            if (_edgeLayer == null || _grid == null || _save == null) return;
            List<string> unlocked = ResonanceProgression.GetUnlockedNodes(_save, _grid.CharacterID);
            var edges = new List<ResonanceEdgeLayer.Edge>();
            foreach (ResonanceNodeData node in _nodes) {
                if (!_buttons.TryGetValue(node.NodeID, out Button target)) continue;
                foreach (string prerequisiteID in node.PrerequisiteNodeIDs ?? Array.Empty<string>()) {
                    if (!_buttons.TryGetValue(prerequisiteID, out Button source)) continue;
                    // UR-SCREENS: the chips are plates, so an edge stops at each chip's border instead of
                    // running under it.
                    edges.Add(new ResonanceEdgeLayer.Edge(
                        ClipToRect(source.GetRect(), target.GetRect().GetCenter()),
                        ClipToRect(target.GetRect(), source.GetRect().GetCenter()),
                        node.PrerequisiteMode == PrerequisiteMode.Any,
                        unlocked.Contains(prerequisiteID)));
                }
            }
            _edgeLayer.SetEdges(edges);
        }

        /// <summary>
        /// Where the segment from <paramref name="chip"/>'s centre toward <paramref name="other"/> leaves the
        /// chip rectangle (the centre itself when the points coincide). Pure; the edge's endpoint.
        /// </summary>
        internal static Vector2 ClipToRect(Rect2 chip, Vector2 other) {
            Vector2 centre = chip.GetCenter();
            Vector2 delta = other - centre;
            if (delta.LengthSquared() < 0.0001f) return centre;
            float tx = Mathf.Abs(delta.X) < 0.0001f ? float.MaxValue : chip.Size.X / 2f / Mathf.Abs(delta.X);
            float ty = Mathf.Abs(delta.Y) < 0.0001f ? float.MaxValue : chip.Size.Y / 2f / Mathf.Abs(delta.Y);
            return centre + delta * Mathf.Min(1f, Mathf.Min(tx, ty));
        }

        /// <summary>The authored normalized positions, in node order. Test surface.</summary>
        public List<(float X, float Y)> LayoutPositions {
            get {
                var positions = new List<(float X, float Y)>(_nodes.Count);
                foreach (ResonanceNodeData node in _nodes) {
                    positions.Add((node.LayoutPosition.X, node.LayoutPosition.Y));
                }
                return positions;
            }
        }

        /// <summary>The edge layer behind the node chips. Test surface.</summary>
        public ResonanceEdgeLayer EdgeLayer => _edgeLayer;

        /// <summary>
        /// Locked nodes stay pressable so selecting them explains WHY they are
        /// locked; only the purchasable state opens the confirmation dialog.
        /// </summary>
        private void ActivateNode(ResonanceNodeData node) {
            if (node == null || _grid == null || _save == null) return;
            ResonanceUnlockResult state = ResonanceProgression.EvaluateUnlock(_grid, _save, node.NodeID);
            if (state == ResonanceUnlockResult.Unlocked) {
                RequestUnlock(node);
                return;
            }
            _statusLabel.Text = ExplainState(node, state);
        }

        private void RequestUnlock(ResonanceNodeData node) {
            _pendingNode = node;
            // Already-formatted copy, not a raw key: automatic control translation
            // leaves an unknown string untouched, so a resolved sentence is safe.
            _confirmation.SetPromptKey(string.Format(
                Tr("resonance_confirm_unlock"),
                Tr(node.DisplayNameKey),
                node.UnlockCost));
            _confirmation.Open();
        }

        private void ConfirmUnlock() {
            if (_pendingNode == null) return;
            // TryUnlock raises EventBus.OnTalentNodeUnlocked; SaveManager
            // autosaves the active story slot from that designed unlock trigger.
            // It also raises OnResonanceProgressionChanged, from which the live
            // campaign hero re-resolves its grid at once (RespecAll does too).
            ResonanceUnlockResult result = ResonanceProgression.TryUnlock(_grid, _save, _pendingNode.NodeID);
            _statusLabel.Text = Tr(ResultKey(result));
            ResonanceNodeData unlockedNode = _pendingNode;
            _pendingNode = null;
            Refresh();
            if (result == ResonanceUnlockResult.Unlocked
                && _buttons.TryGetValue(unlockedNode.NodeID, out Button button)) {
                PlayUnlockFlash(button);
            }
        }

        /// <summary>
        /// Unlock feedback (TG-SCREENS, MOT-A row 10): the chip's single 600 ms bloom, as an ability turning ready does; no
        /// overshoot, no flash. Under Reduced Temporal Effects the chip simply shows its unlocked look.
        /// </summary>
        private static void PlayUnlockFlash(Button button) => PulpMotion.Bloom(button);

        private void Refresh() {
            if (_save == null || _grid == null) return;
            int balance = _save.DepositedChronalDust.GetValueOrDefault(_grid.CharacterID);
            _balanceLabel.Text = string.Format(Tr("resonance_balance"), balance);
            foreach (ResonanceNodeData node in _nodes) {
                if (!_buttons.TryGetValue(node.NodeID, out Button button)) continue;
                ResonanceUnlockResult state = ResonanceProgression.EvaluateUnlock(_grid, _save, node.NodeID);
                button.Text = NodeText(node, state);
                button.TooltipText = BuildTooltip(node, state);
                _chipStates[node.NodeID] = state;
            }
            ApplyChipStyles();
            RebuildEdges();
        }

        private void SetFocusedIndex(int index) {
            if (_nodes.Count == 0) return;
            _focusedIndex = Math.Clamp(index, 0, _nodes.Count - 1);
            ApplyChipStyles();
        }

        private static readonly string[] ChipBoxSlots = { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" };
        private static readonly string[] ChipTextSlots = {
            "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color"
        };

        /// <summary>
        /// The glass chip's edge for a state (UI_SPEC_GLASS 9.2): unlocked the lit edge with the gold wash, available the
        /// standard edge, locked and dormant the quiet edge. The chip the D-pad sits on wears the focus edge and glow.
        /// </summary>
        public static GlassTileDecor.TileEdge ChipEdgeFor(ResonanceUnlockResult state, bool focused) {
            if (focused) return GlassTileDecor.TileEdge.Focus;
            return state switch {
                ResonanceUnlockResult.AlreadyUnlocked => GlassTileDecor.TileEdge.Lit,
                ResonanceUnlockResult.Unlocked => GlassTileDecor.TileEdge.Standard,
                _ => GlassTileDecor.TileEdge.Dim
            };
        }

        /// <summary>The chip's text colour: ink when owned or available, ink2 when locked, ink3 for a dormant star; white on the focused chip unless dormant.</summary>
        public static Color ChipTextFor(ResonanceUnlockResult state, bool focused) => state switch {
            ResonanceUnlockResult.AbilityLocked => UIPalette.Ink3,
            _ when focused => UIPalette.TextFocus,
            ResonanceUnlockResult.AlreadyUnlocked or ResonanceUnlockResult.Unlocked => UIPalette.Ink,
            _ => UIPalette.Ink2
        };

        private void ApplyChipStyles() {
            for (int i = 0; i < _nodes.Count; i++) {
                string id = _nodes[i].NodeID;
                if (!_buttons.TryGetValue(id, out Button button)) continue;
                _chipStates.TryGetValue(id, out ResonanceUnlockResult state);
                bool focused = i == _focusedIndex;
                if (_tiles.TryGetValue(id, out GlassTileDecor tile)) tile.Edge = ChipEdgeFor(state, focused);
                Color text = ChipTextFor(state, focused);
                foreach (string slot in ChipTextSlots) button.AddThemeColorOverride(slot, text);
                // A dormant star shows the dormant glyph where a named chip shows its tier (no name, no tier: V7.6).
                bool dormant = state == ResonanceUnlockResult.AbilityLocked;
                if (button.GetNodeOrNull<Control>("TierMark") is { } mark) mark.Visible = !dormant;
                if (button.GetNodeOrNull<Control>("DormantGlyph") is { } glyph) glyph.Visible = dormant;
            }
        }

        /// <summary>The state a node chip currently shows. Test surface.</summary>
        internal ResonanceUnlockResult ChipState(string nodeID) =>
            _chipStates.TryGetValue(nodeID, out ResonanceUnlockResult state) ? state : ResonanceUnlockResult.MissingPrerequisite;

        private string NodeText(ResonanceNodeData node, ResonanceUnlockResult state) {
            // A gated node is an unnamed dormant star until the Legacy ability
            // it modifies is recovered - no name, no cost, no tooltip body.
            if (state == ResonanceUnlockResult.AbilityLocked) return Tr("resonance_dormant_node");
            string body = $"{Tr(node.DisplayNameKey)}\n{string.Format(Tr("resonance_cost"), node.UnlockCost)}";
            return state switch {
                ResonanceUnlockResult.AlreadyUnlocked => $"{body}\n{Tr("resonance_unlocked")}",
                ResonanceUnlockResult.Unlocked => body,
                _ => $"{body}\n{Tr("resonance_locked_label")}"
            };
        }

        private string BuildTooltip(ResonanceNodeData node, ResonanceUnlockResult state) {
            if (state == ResonanceUnlockResult.AbilityLocked) {
                return $"{Tr("resonance_dormant_node")}\n{Tr("resonance_state_locked_ability")}";
            }
            return $"{Tr(node.DisplayNameKey)}\n{Tr(node.DescriptionKey)}\n"
                + $"{string.Format(Tr("resonance_cost"), node.UnlockCost)}\n{ExplainState(node, state)}";
        }

        private string ExplainState(ResonanceNodeData node, ResonanceUnlockResult state) {
            switch (state) {
                case ResonanceUnlockResult.Unlocked:
                    return Tr("resonance_state_available");
                case ResonanceUnlockResult.AlreadyUnlocked:
                    return Tr("resonance_result_already");
                case ResonanceUnlockResult.MissingPrerequisite:
                    // V7.6: an Any-of node needs "requires any of A, B"; the old
                    // first-unmet-only text was wrong for every Any-of node and
                    // for Shakespeare's All-of-three Majors.
                    return string.Format(
                        Tr(node.PrerequisiteMode == PrerequisiteMode.Any
                            ? "resonance_state_locked_prerequisite_any"
                            : "resonance_state_locked_prerequisite"),
                        PrerequisiteNames(node));
                case ResonanceUnlockResult.AbilityLocked:
                    return Tr("resonance_state_locked_ability");
                case ResonanceUnlockResult.InsufficientDust:
                    int balance = _save.DepositedChronalDust.GetValueOrDefault(_grid.CharacterID);
                    return string.Format(
                        Tr("resonance_state_locked_dust"),
                        Math.Max(0, node.UnlockCost - balance));
                default:
                    return Tr("resonance_result_unavailable");
            }
        }

        /// <summary>
        /// Every UNMET prerequisite, comma separated. V7.6 replaces the old
        /// first-unmet-only lookup: an Any-of node must read "requires any of
        /// A, B" and Shakespeare's Majors need all three named.
        /// </summary>
        public string PrerequisiteNames(ResonanceNodeData node) {
            List<string> unlocked = ResonanceProgression.GetUnlockedNodes(_save, _grid.CharacterID);
            var names = new List<string>();
            foreach (string prerequisiteID in node.PrerequisiteNodeIDs ?? Array.Empty<string>()) {
                if (unlocked.Contains(prerequisiteID)) continue;
                string label = prerequisiteID;
                foreach (ResonanceNodeData candidate in _nodes) {
                    if (candidate.NodeID == prerequisiteID) {
                        label = Tr(candidate.DisplayNameKey);
                        break;
                    }
                }
                names.Add(label);
            }
            return string.Join(", ", names);
        }

        private static string ResultKey(ResonanceUnlockResult result) => result switch {
            ResonanceUnlockResult.Unlocked => "resonance_result_unlocked",
            ResonanceUnlockResult.AlreadyUnlocked => "resonance_result_already",
            ResonanceUnlockResult.MissingPrerequisite => "resonance_result_prerequisite",
            ResonanceUnlockResult.InsufficientDust => "resonance_result_dust",
            ResonanceUnlockResult.AbilityLocked => "resonance_result_ability_locked",
            _ => "resonance_result_unavailable"
        };

        private void Close() {
            Closed?.Invoke();
            QueueFree();
        }
    }
}
