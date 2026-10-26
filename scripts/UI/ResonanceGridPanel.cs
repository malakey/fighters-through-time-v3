using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using FTT.UI.Pulp;
using Godot;

namespace FTT.UI {

    public partial class ResonanceGridPanel : Control {
        /// <summary>V7.6 node chip size. The old 330x150 card cannot tile a nine-node mesh.</summary>
        public static readonly Vector2 NodeChipSize = new(200, 90);

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
        private StyleBoxFlat _focusStyle;
        private StyleBoxFlat _focusStylePaper;
        private StyleBoxTexture _inkChip;
        private StyleBoxTexture _paperChip;
        private readonly Dictionary<string, ResonanceUnlockResult> _chipStates = new(StringComparer.Ordinal);
        private readonly List<ResonanceNodeData> _nodes = new();
        private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);

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

            // UR-SCREENS: pulp chips. Ink and paper plates from the kit by state, a gold focus ring.
            _inkChip = PulpKit.Plate("panel_dark");
            _paperChip = PulpKit.Plate("panel_paper");
            foreach (StyleBoxTexture plate in new[] { _inkChip, _paperChip }) {
                if (plate == null) continue;
                plate.ContentMarginLeft = 12f; plate.ContentMarginRight = 38f; // room for the tier mark
                plate.ContentMarginTop = 8f; plate.ContentMarginBottom = 8f;
            }
            _focusStyle = BuildFocusStyle(UIPalette.PlateFill);
            _focusStylePaper = BuildFocusStyle(UIPalette.Paper);
            if (Root.GetNodeOrNull<PanelContainer>("Center/Panel") is { } gridPanel) {
                MenuSafeAreaFit.Attach(gridPanel);
                PulpScreenKit.DressPanel(gridPanel, "resonance_grid_tag");
            }
            PulpButtonDecor.Apply(closeButton, compact: true);
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
            Node layout = closeButton.GetParent();
            layout.AddChild(_respecButton);
            layout.MoveChild(_respecButton, closeButton.GetIndex());
            PulpButtonDecor.Apply(_respecButton, compact: true);
        }

        private static StyleBoxFlat BuildFocusStyle(Color fill) {
            var style = new StyleBoxFlat { BgColor = fill, BorderColor = UIPalette.Accent };
            style.SetBorderWidthAll(4);
            style.SetContentMarginAll(10);
            style.ContentMarginRight = 38f;
            return style;
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
                if (node == null) continue;
                int index = _nodes.Count;
                _nodes.Add(node);
                var button = new Button {
                    Name = node.NodeID,
                    CustomMinimumSize = NodeChipSize,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    FocusMode = FocusModeEnum.None
                };
                PlaceChip(button, node.LayoutPosition);
                button.Pressed += () => {
                    SetFocusedIndex(index);
                    ActivateNode(node);
                };
                var mark = new ResonanceTierMark { Tier = node.Tier, Name = "TierMark" };
                button.AddChild(mark);
                _nodeGrid.AddChild(button);
                _buttons[node.NodeID] = button;
            }
            Refresh();
            SetFocusedIndex(0);
            RebuildEdges();
        }

        /// <summary>
        /// Anchors a node chip so its CENTRE sits at the authored normalized
        /// position. Anchor-based rather than pixel-based so the constellation
        /// survives a panel resize and the accessibility UI scale.
        /// </summary>
        private static void PlaceChip(Control chip, Vector2 layoutPosition) {
            float x = Mathf.Clamp(layoutPosition.X, 0f, 1f);
            float y = Mathf.Clamp(layoutPosition.Y, 0f, 1f);
            chip.AnchorLeft = x;
            chip.AnchorRight = x;
            chip.AnchorTop = y;
            chip.AnchorBottom = y;
            chip.OffsetLeft = -NodeChipSize.X * 0.5f;
            chip.OffsetRight = NodeChipSize.X * 0.5f;
            chip.OffsetTop = -NodeChipSize.Y * 0.5f;
            chip.OffsetBottom = NodeChipSize.Y * 0.5f;
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

        /// <summary>Placeholder unlock feedback: white flash and scale pulse.</summary>
        private void PlayUnlockFlash(Button button) {
            button.PivotOffset = button.Size / 2f;
            Color settled = button.Modulate;
            button.Modulate = new Color(2.2f, 2.2f, 2.2f);
            button.Scale = new Vector2(1.12f, 1.12f);
            Tween tween = CreateTween();
            tween.SetParallel();
            tween.TweenProperty(button, "modulate", settled, 0.45f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(button, "scale", Vector2.One, 0.3f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        }

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
                // UR-SCREENS: state is a plate (paper when owned, ink otherwise) plus a dim for locked and
                // dormant; the dormant node stays unnamed in its true position (V7.6).
                button.Modulate = state switch {
                    ResonanceUnlockResult.AbilityLocked => new Color(0.55f, 0.55f, 0.62f),
                    ResonanceUnlockResult.Unlocked or ResonanceUnlockResult.AlreadyUnlocked => Colors.White,
                    _ => new Color(0.78f, 0.78f, 0.84f)
                };
            }
            ApplyChipStyles();
            RebuildEdges();
        }

        private void SetFocusedIndex(int index) {
            if (_nodes.Count == 0) return;
            _focusedIndex = Math.Clamp(index, 0, _nodes.Count - 1);
            ApplyChipStyles();
        }

        private void ApplyChipStyles() {
            for (int i = 0; i < _nodes.Count; i++) {
                if (!_buttons.TryGetValue(_nodes[i].NodeID, out Button button)) continue;
                _chipStates.TryGetValue(_nodes[i].NodeID, out ResonanceUnlockResult state);
                bool paper = state == ResonanceUnlockResult.AlreadyUnlocked;
                StyleBox box = i == _focusedIndex
                    ? (paper ? _focusStylePaper : _focusStyle)
                    : (paper ? _paperChip : _inkChip);
                if (box != null) {
                    foreach (string slot in new[] { "normal", "hover", "pressed", "focus", "disabled" }) {
                        button.AddThemeStyleboxOverride(slot, box);
                    }
                }
                Color text = paper ? UIPalette.PaperInk : UIPalette.Ink;
                foreach (string slot in new[] { "font_color", "font_hover_color", "font_pressed_color",
                             "font_focus_color", "font_hover_pressed_color", "font_disabled_color" }) {
                    button.AddThemeColorOverride(slot, text);
                }
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
