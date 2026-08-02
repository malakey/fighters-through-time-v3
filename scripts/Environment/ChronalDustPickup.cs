using Godot;

namespace FTT.Environment {

    public partial class ChronalDustPickup : FTT.Core.PooledNode, FTT.Core.IPoolable {
        [Export] public int DustAmount = 10;

        private const float MagnetRadius = 150f;
        private const float MagnetSpeed = 900f;
        private const float ExpirationTime = 10f;
        private float _lifetime;
        private FTT.Characters.PlayerController _magnetTarget;

        public void OnSpawn() {
            _lifetime = ExpirationTime;
            _magnetTarget = null;
        }

        public void OnDespawn() {
            _magnetTarget = null;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            if (_magnetTarget == null) {
                foreach (var node in GetTree().GetNodesInGroup("Players")) {
                    if (node is FTT.Characters.PlayerController pc && GlobalPosition.DistanceTo(pc.GlobalPosition) <= MagnetRadius) {
                        _magnetTarget = pc;
                        break;
                    }
                }
            }

            if (_magnetTarget != null) {
                var dir = (_magnetTarget.GlobalPosition - GlobalPosition).Normalized();
                GlobalPosition += dir * MagnetSpeed * dt;
                if (GlobalPosition.DistanceTo(_magnetTarget.GlobalPosition) < 20f) {
                    FTT.Core.EventBus.Instance?.RaiseChronalDustCollected(DustAmount);
                    ReturnToPool();
                }
            }
        }
    }
}
