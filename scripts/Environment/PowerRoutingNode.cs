using Godot;
using System.Collections.Generic;

namespace FTT.Environment {

    public partial class PowerRoutingNode : Node2D {
        [Signal] public delegate void PowerChangedEventHandler(bool isPowered);

        [Export] public string PowerNodeID = "";
        [Export] public Godot.Collections.Array<NodePath> OutputPaths = new();

        private readonly HashSet<ulong> _powerSources = new();
        private readonly List<PowerRoutingNode> _connectedOutputs = new();
        protected bool LocalPowerEnabled { get; set; }
        public bool IsPowered { get; private set; }

        public override void _Ready() {
            AddToGroup("power_routing_node");
            RefreshPowerRouting();
        }

        public override void _ExitTree() => DisconnectOutputs();

        public void ReceivePower(ulong sourceID, bool powered) {
            if (powered) _powerSources.Add(sourceID);
            else _powerSources.Remove(sourceID);
            RefreshPowerRouting();
        }

        public void SetLocalPower(bool powered) {
            LocalPowerEnabled = powered;
            RefreshPowerRouting();
        }

        public void RefreshPowerRouting() {
            bool shouldBePowered = LocalPowerEnabled || _powerSources.Count > 0;
            bool stateChanged = shouldBePowered != IsPowered;
            if (stateChanged || IsPowered) DisconnectOutputs();
            IsPowered = shouldBePowered;
            if (IsPowered) ConnectOutputs();
            if (stateChanged) {
                OnPowerStateChanged(IsPowered);
                EmitSignal(SignalName.PowerChanged, IsPowered);
            }
        }

        protected virtual IEnumerable<NodePath> GetActiveOutputPaths() => OutputPaths;
        protected virtual void OnPowerStateChanged(bool powered) { }

        private void ConnectOutputs() {
            foreach (NodePath path in GetActiveOutputPaths()) {
                if (GetNodeOrNull<Node>(path) is not PowerRoutingNode output || output == this) continue;
                if (_connectedOutputs.Contains(output)) continue;
                _connectedOutputs.Add(output);
                output.ReceivePower(GetInstanceId(), true);
            }
        }

        private void DisconnectOutputs() {
            foreach (PowerRoutingNode output in _connectedOutputs) {
                if (GodotObject.IsInstanceValid(output)) output.ReceivePower(GetInstanceId(), false);
            }
            _connectedOutputs.Clear();
        }
    }
}
