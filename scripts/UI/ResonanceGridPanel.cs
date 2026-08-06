using System;
using System.Collections.Generic;
using FTT.Core;
using FTT.Environment;
using Godot;

namespace FTT.UI {

    public partial class ResonanceGridPanel : Control {
        public event Action Closed;
        private ResonanceGridData _grid;
        private StorySaveData _save;
        private int _slot;
        private Label _balanceLabel;
        private Label _statusLabel;
        private GridContainer _nodeGrid;
        private ConfirmationDialog _confirmation;
        private ResonanceNodeData _pendingNode;
        private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);

        public override void _Ready() {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            ColorRect shade = new() { Color = new Color(0.015f, 0.025f, 0.07f, 0.96f) };
            shade.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(shade);
            var center = new CenterContainer();
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(center);
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(1100, 760) };
            center.AddChild(panel);
            var layout = new VBoxContainer();
            layout.AddThemeConstantOverride("separation", 16);
            panel.AddChild(layout);

            var title = new Label {
                Text = Tr("resonance_title"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 32);
            title.AddThemeColorOverride("font_color", new Color(0.1f, 0.95f, 0.95f));
            layout.AddChild(title);
            _balanceLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            layout.AddChild(_balanceLabel);
            _nodeGrid = new GridContainer { Columns = 3 };
            _nodeGrid.AddThemeConstantOverride("h_separation", 18);
            _nodeGrid.AddThemeConstantOverride("v_separation", 18);
            layout.AddChild(_nodeGrid);
            _statusLabel = new Label {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            layout.AddChild(_statusLabel);
            var close = new Button { Text = Tr("common_back"), CustomMinimumSize = new Vector2(220, 48) };
            close.Pressed += Close;
            layout.AddChild(close);

            _confirmation = new ConfirmationDialog { Title = Tr("resonance_confirm_title") };
            _confirmation.Confirmed += ConfirmUnlock;
            AddChild(_confirmation);
            LoadActiveGrid();
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (@event.IsActionPressed("ui_cancel")) {
                GetViewport().SetInputAsHandled();
                Close();
            }
        }

        private void LoadActiveGrid() {
            if (GameManager.Instance == null || SaveManager.Instance == null) return;
            _slot = GameManager.Instance.CurrentSession.ActiveSaveSlot;
            if (_slot < 0 || _slot >= SaveManager.Instance.SaveSlots.Length) return;
            _save = SaveManager.Instance.SaveSlots[_slot];
            string characterID = GameManager.Instance.CurrentSession.SelectedCharacterID ?? "";
            _grid = ResourceLoader.Load<ResonanceGridData>($"res://resources/Resonance/{characterID}_grid.tres");
            if (_save == null || _grid == null) {
                _statusLabel.Text = Tr("resonance_grid_unavailable");
                return;
            }
            foreach (ResonanceNodeData node in _grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null) continue;
                var button = new Button {
                    CustomMinimumSize = new Vector2(330, 150),
                    Text = NodeText(node),
                    TooltipText = Tr(node.DescriptionKey)
                };
                button.Pressed += () => RequestUnlock(node);
                _nodeGrid.AddChild(button);
                _buttons[node.NodeID] = button;
            }
            Refresh();
        }

        private void RequestUnlock(ResonanceNodeData node) {
            _pendingNode = node;
            _confirmation.DialogText = string.Format(
                Tr("resonance_confirm_unlock"),
                Tr(node.DisplayNameKey),
                node.UnlockCost);
            _confirmation.PopupCentered();
        }

        private void ConfirmUnlock() {
            if (_pendingNode == null) return;
            ResonanceUnlockResult result = ResonanceProgression.TryUnlock(_grid, _save, _pendingNode.NodeID);
            _statusLabel.Text = Tr(ResultKey(result));
            if (result == ResonanceUnlockResult.Unlocked) SaveManager.Instance?.SaveStorySlot(_slot);
            _pendingNode = null;
            Refresh();
        }

        private void Refresh() {
            if (_save == null || _grid == null) return;
            int balance = _save.DepositedChronalDust.GetValueOrDefault(_grid.CharacterID);
            _balanceLabel.Text = string.Format(Tr("resonance_balance"), balance);
            List<string> unlocked = ResonanceProgression.GetUnlockedNodes(_save, _grid.CharacterID);
            foreach (ResonanceNodeData node in _grid.Nodes ?? Array.Empty<ResonanceNodeData>()) {
                if (node == null || !_buttons.TryGetValue(node.NodeID, out Button button)) continue;
                bool isUnlocked = unlocked.Contains(node.NodeID);
                button.Text = NodeText(node) + (isUnlocked ? $"\n{Tr("resonance_unlocked")}" : "");
                button.Disabled = isUnlocked;
                button.Modulate = isUnlocked ? new Color(0.4f, 1f, 0.9f) : Colors.White;
            }
        }

        private string NodeText(ResonanceNodeData node) =>
            $"{Tr(node.DisplayNameKey)}\n{string.Format(Tr("resonance_cost"), node.UnlockCost)}";

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
