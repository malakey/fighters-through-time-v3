using FTT.Core;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// Scene-scoped bridge from <c>EventBus.OnEnemyPresentation</c> to the pooled
    /// VFX emitter. The roster authors <c>PresentationEventID</c> strings that had
    /// no consumer before Package 8; this node gives them one at placeholder
    /// fidelity, and B6 refines the per-ID mappings on the same surface.
    ///
    /// Installed on demand by the enemy/boss controllers so no level controller has
    /// to know about it, and there is at most one per scene.
    /// </summary>
    public partial class VfxPresentationBinder : Node {
        public const string NodeName = "VfxPresentationBinder";

        private bool _bound;

        public override void _Ready() {
            EventBus bus = EventBus.Instance;
            if (bus == null) return;
            _bound = true;
            bus.OnEnemyPresentation += OnEnemyPresentation;
        }

        public override void _ExitTree() {
            if (!_bound) return;
            _bound = false;
            EventBus bus = EventBus.Instance;
            if (bus != null) bus.OnEnemyPresentation -= OnEnemyPresentation;
        }

        private void OnEnemyPresentation(EnemyPresentationPayload payload) {
            if (!IsInsideTree()) return;
            VfxEmitter.EmitForPresentationEvent(
                payload.PresentationEventID, payload.Phase, payload.Position, GetParent());
        }

        /// <summary>
        /// Ensures the scene that <paramref name="anyNode"/> lives in has a binder.
        /// No-op outside the tree or when one is already installed.
        /// </summary>
        public static VfxPresentationBinder EnsureInstalled(Node anyNode) {
            if (anyNode == null || !anyNode.IsInsideTree()) return null;
            SceneTree tree = anyNode.GetTree();
            Node host = tree?.CurrentScene ?? anyNode.GetParent();
            if (host == null || !GodotObject.IsInstanceValid(host)) return null;
            var existing = host.GetNodeOrNull<VfxPresentationBinder>(NodeName);
            if (existing != null) return existing;
            var binder = new VfxPresentationBinder { Name = NodeName };
            host.AddChild(binder);
            return binder;
        }
    }
}
