using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.UI {

    public partial class ResonanceGridPanel : Control {
        private const int GridColumns = 3;

        /// <summary>The authored scene this panel presents (Package 8 B4).</summary>
        public const string ScenePath = "res://scenes/ui/ResonanceGrid.tscn";

        public event Action Closed;
        private ResonanceGridData _grid;
        private StorySaveData _save;
        private int _slot;
        private Label _balanceLabel;
        private Label _statusLabel;
        private GridContainer _nodeGrid;
        private ConfirmModal _confirmation;
        private ResonanceNodeData _pendingNode;
        private int _focusedIndex;
        private StyleBoxFlat _focusStyle;
        private readonly List<ResonanceNodeData> _nodes = new();
        private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);

        /// <summary>The instantiated authored scene root. Test surface.</summary>
        public Control Root { get; private set; }

        /// <summary>The shared confirmation modal guarding a purchase.</summary>
        public ConfirmModal Confirmation => _confirmation;

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
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            UIPalette.ApplyTheme(this);

            Root = InstantiateAuthoredScene();
            if (Root == null) return;
            AddChild(Root);

            _balanceLabel = Root.GetNode<Label>("Center/Panel/Layout/Balance");
            _statusLabel = Root.GetNode<Label>("Center/Panel/Layout/Status");
            _nodeGrid = Root.GetNode<GridContainer>("Center/Panel/Layout/NodeGrid");
            Root.GetNode<Button>("Center/Panel/Layout/CloseButton").Pressed += Close;

            _focusStyle = new StyleBoxFlat {
                BgColor = new Color(0.05f, 0.14f, 0.2f),
                BorderColor = UIPalette.Cyan
            };
            _focusStyle.SetBorderWidthAll(3);
            _focusStyle.SetContentMarginAll(8);

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
            LoadActiveGrid();
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
                    _focusedIndex, deltaColumn, deltaRow, GridColumns, _nodes.Count));
                return;
            }
            if (@event.IsActionPressed("ui_accept")) {
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
            foreach (ResonanceNodeData node in _grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null) continue;
                int index = _nodes.Count;
                _nodes.Add(node);
                var button = new Button {
                    CustomMinimumSize = new Vector2(330, 150),
                    FocusMode = FocusModeEnum.None
                };
                button.Pressed += () => {
                    SetFocusedIndex(index);
                    ActivateNode(node);
                };
                _nodeGrid.AddChild(button);
                _buttons[node.NodeID] = button;
            }
            Refresh();
            SetFocusedIndex(0);
        }

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
                button.Modulate = state switch {
                    ResonanceUnlockResult.AlreadyUnlocked => new Color(0.4f, 1f, 0.9f),
                    ResonanceUnlockResult.Unlocked => Colors.White,
                    _ => new Color(0.62f, 0.62f, 0.7f)
                };
            }
        }

        private void SetFocusedIndex(int index) {
            if (_nodes.Count == 0) return;
            _focusedIndex = Math.Clamp(index, 0, _nodes.Count - 1);
            for (int i = 0; i < _nodes.Count; i++) {
                if (!_buttons.TryGetValue(_nodes[i].NodeID, out Button button)) continue;
                if (i == _focusedIndex) {
                    button.AddThemeStyleboxOverride("normal", _focusStyle);
                    button.AddThemeStyleboxOverride("hover", _focusStyle);
                    button.AddThemeStyleboxOverride("pressed", _focusStyle);
                } else {
                    button.RemoveThemeStyleboxOverride("normal");
                    button.RemoveThemeStyleboxOverride("hover");
                    button.RemoveThemeStyleboxOverride("pressed");
                }
            }
        }

        private string NodeText(ResonanceNodeData node, ResonanceUnlockResult state) {
            string body = $"{Tr(node.DisplayNameKey)}\n{string.Format(Tr("resonance_cost"), node.UnlockCost)}";
            return state switch {
                ResonanceUnlockResult.AlreadyUnlocked => $"{body}\n{Tr("resonance_unlocked")}",
                ResonanceUnlockResult.Unlocked => body,
                _ => $"{body}\n{Tr("resonance_locked_label")}"
            };
        }

        private string BuildTooltip(ResonanceNodeData node, ResonanceUnlockResult state) =>
            $"{Tr(node.DisplayNameKey)}\n{Tr(node.DescriptionKey)}\n"
            + $"{string.Format(Tr("resonance_cost"), node.UnlockCost)}\n{ExplainState(node, state)}";

        private string ExplainState(ResonanceNodeData node, ResonanceUnlockResult state) {
            switch (state) {
                case ResonanceUnlockResult.Unlocked:
                    return Tr("resonance_state_available");
                case ResonanceUnlockResult.AlreadyUnlocked:
                    return Tr("resonance_result_already");
                case ResonanceUnlockResult.MissingPrerequisite:
                    return string.Format(
                        Tr("resonance_state_locked_prerequisite"),
                        PrerequisiteName(node));
                case ResonanceUnlockResult.InsufficientDust:
                    int balance = _save.DepositedChronalDust.GetValueOrDefault(_grid.CharacterID);
                    return string.Format(
                        Tr("resonance_state_locked_dust"),
                        Math.Max(0, node.UnlockCost - balance));
                default:
                    return Tr("resonance_result_unavailable");
            }
        }

        private string PrerequisiteName(ResonanceNodeData node) {
            List<string> unlocked = ResonanceProgression.GetUnlockedNodes(_save, _grid.CharacterID);
            foreach (string prerequisiteID in node.PrerequisiteNodeIDs ?? Array.Empty<string>()) {
                if (unlocked.Contains(prerequisiteID)) continue;
                foreach (ResonanceNodeData candidate in _nodes) {
                    if (candidate.NodeID == prerequisiteID) return Tr(candidate.DisplayNameKey);
                }
                return prerequisiteID;
            }
            return "";
        }

        private static string ResultKey(ResonanceUnlockResult result) => result switch {
            ResonanceUnlockResult.Unlocked => "resonance_result_unlocked",
            ResonanceUnlockResult.AlreadyUnlocked => "resonance_result_already",
            ResonanceUnlockResult.MissingPrerequisite => "resonance_result_prerequisite",
            ResonanceUnlockResult.InsufficientDust => "resonance_result_dust",
            _ => "resonance_result_unavailable"
        };

        private void Close() {
            Closed?.Invoke();
            QueueFree();
        }
    }
}
