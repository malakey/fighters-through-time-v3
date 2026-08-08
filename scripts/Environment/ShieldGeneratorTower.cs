using Godot;
using System.Collections.Generic;

namespace FTT.Environment {

    /// <summary>
    /// Destructible generator that keeps linked <see cref="ForcefieldBarrier"/>
    /// nodes solid while it lives (Level 2 Orléans siege gates, Level 11 Gettysburg
    /// arrays). Destroying it drops every linked barrier, optionally sets a
    /// <see cref="PuzzleManager"/> condition, and raises
    /// <see cref="TowerDestroyedEventHandler"/>.
    ///
    /// Barrier state is driven from <c>ApplyStatePresentation</c>, so the inherited
    /// checkpoint/rewind restore puts the barriers back automatically.
    /// </summary>
    public partial class ShieldGeneratorTower : DamageableEnvironmentObject {
        [Signal] public delegate void TowerDestroyedEventHandler(string towerID);

        [Export] public Godot.Collections.Array<NodePath> BarrierPaths = new();
        [Export] public NodePath PuzzleManagerPath;
        /// <summary>Optional; leave blank when destruction only opens geometry.</summary>
        [Export] public string ConditionID = "";

        public override void _Ready() {
            base._Ready();
            AddToGroup("shield_generator");
            ApplyStatePresentation();
            // Barriers authored as siblings run their own _Ready after this node's,
            // which would reset them to StartActive. Re-assert once the frame settles.
            CallDeferred(MethodName.RefreshBarriers);
        }

        /// <summary>Re-asserts barrier state from this tower's alive/destroyed state.</summary>
        public void RefreshBarriers() => ApplyStatePresentation();

        public IReadOnlyList<ForcefieldBarrier> Barriers() {
            var barriers = new List<ForcefieldBarrier>();
            foreach (NodePath path in BarrierPaths) {
                if (GetNodeOrNull<ForcefieldBarrier>(path) is ForcefieldBarrier barrier) barriers.Add(barrier);
            }
            return barriers;
        }

        public int ActiveBarrierCount {
            get {
                int count = 0;
                foreach (ForcefieldBarrier barrier in Barriers()) {
                    if (barrier.IsActive) count++;
                }
                return count;
            }
        }

        protected override void OnDestroyed() {
            if (!string.IsNullOrWhiteSpace(ConditionID)) {
                GetNodeOrNull<PuzzleManager>(PuzzleManagerPath)?.SetCondition(ConditionID, true);
            }
            EmitSignal(SignalName.TowerDestroyed, ObjectID);
        }

        protected override void ApplyStatePresentation() {
            base.ApplyStatePresentation();
            if (BarrierPaths == null) return;
            foreach (ForcefieldBarrier barrier in Barriers()) barrier.SetActive(!IsDestroyed);
        }
    }
}
