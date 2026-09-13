using Godot;
using FTT.Characters;
using FTT.Core;

namespace FTT.Environment {

    public partial class InteractionArea : Area2D {
        [Export] public NodePath TargetPath = "..";
        [Export] public NodePath PromptLabelPath = "Prompt";

        private PlayerController _availablePlayer;
        private Label _prompt;

        public override void _Ready() {
            CollisionLayer = CollisionLayers.Trigger;
            CollisionMask = CollisionLayers.Player;
            _prompt = GetNodeOrNull<Label>(PromptLabelPath);
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
            RefreshPrompt();
        }

        public override void _ExitTree() {
            BodyEntered -= OnBodyEntered;
            BodyExited -= OnBodyExited;
        }

        public override void _UnhandledInput(InputEvent @event) {
            if (_availablePlayer == null || !@event.IsActionPressed(InputManager.Actions.Interact)) return;
            TryInteract(_availablePlayer);
            GetViewport()?.SetInputAsHandled();
        }

        public bool TryInteract(PlayerController player) {
            // V7.6 Time Freeze: no puzzle interaction, no puzzle progress and no
            // objective credit while the world is stopped. Gated here rather than
            // in _UnhandledInput so a scripted or test-driven interaction obeys
            // the same rule as a keypress.
            if (player != null && player.TimeFrozen) return false;
            IInteractable target = GetInteractable();
            if (player == null || target == null || !target.CanInteract(player)) return false;
            target.Interact(player);
            RefreshPrompt();
            return true;
        }

        public void RegisterPlayer(PlayerController player) {
            _availablePlayer = player;
            RefreshPrompt();
        }

        public void UnregisterPlayer(PlayerController player) {
            if (_availablePlayer == player) _availablePlayer = null;
            RefreshPrompt();
        }

        private IInteractable GetInteractable() => GetNodeOrNull<Node>(TargetPath) as IInteractable;
        private void OnBodyEntered(Node2D body) { if (body is PlayerController player) RegisterPlayer(player); }
        private void OnBodyExited(Node2D body) { if (body is PlayerController player) UnregisterPlayer(player); }

        private void RefreshPrompt() {
            if (_prompt == null) return;
            IInteractable target = GetInteractable();
            bool visible = _availablePlayer != null && target?.CanInteract(_availablePlayer) == true;
            _prompt.Visible = visible;
            _prompt.Text = visible ? Tr(target.PromptKey) : "";
        }
    }
}
