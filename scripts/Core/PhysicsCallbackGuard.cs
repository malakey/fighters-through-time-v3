using Godot;

namespace FTT.Core {

    /// <summary>
    /// Tracks when C# code is running inside a physics in/out signal callback
    /// (AreaEntered/BodyEntered handlers and everything they cascade into).
    /// While the physics space is flushing queries the engine rejects
    /// reparenting collision objects, toggling Area2D monitoring/monitorable,
    /// and disabling collision shapes; the engine exposes no query for that
    /// state, so the signal entry points mark it here and the pool/combat code
    /// defers exactly those operations. Everywhere else — including tests that
    /// drive the same methods by direct call — behavior stays synchronous.
    /// </summary>
    public static class PhysicsCallbackGuard {
        public static bool IsInPhysicsCallback { get; private set; }

        public readonly struct Scope : System.IDisposable {
            private readonly bool _previous;
            internal Scope(bool previous) { _previous = previous; }
            public void Dispose() => IsInPhysicsCallback = _previous;
        }

        /// <summary>Marks the current call stack as a physics callback until the scope is disposed. Reentrant.</summary>
        public static Scope Enter() {
            var scope = new Scope(IsInPhysicsCallback);
            IsInPhysicsCallback = true;
            return scope;
        }
    }

    /// <summary>
    /// Physics-state setters that fall back to <c>SetDeferred</c> while inside
    /// a physics callback, where the engine blocks the direct write with
    /// "Function blocked during in/out signal" / "Can't change this state
    /// while flushing queries".
    /// </summary>
    public static class PhysicsSafeSetters {
        public static void SetMonitoringSafe(this Area2D area, bool enabled) {
            if (PhysicsCallbackGuard.IsInPhysicsCallback) area.SetDeferred(Area2D.PropertyName.Monitoring, enabled);
            else area.Monitoring = enabled;
        }

        public static void SetMonitorableSafe(this Area2D area, bool enabled) {
            if (PhysicsCallbackGuard.IsInPhysicsCallback) area.SetDeferred(Area2D.PropertyName.Monitorable, enabled);
            else area.Monitorable = enabled;
        }

        public static void SetShapeDisabledSafe(this CollisionShape2D shape, bool disabled) {
            if (PhysicsCallbackGuard.IsInPhysicsCallback) shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, disabled);
            else shape.Disabled = disabled;
        }

        /// <summary>
        /// ProcessMode writes disable/enable any CollisionObject in the
        /// subtree, which the engine blocks mid-flush the same way it blocks a
        /// direct shape toggle.
        /// </summary>
        public static void SetProcessModeSafe(this Node node, Node.ProcessModeEnum mode) {
            if (PhysicsCallbackGuard.IsInPhysicsCallback) node.SetDeferred(Node.PropertyName.ProcessMode, (long)mode);
            else node.ProcessMode = mode;
        }
    }
}
