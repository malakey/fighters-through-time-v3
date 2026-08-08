using Godot;
using FTT.Core;

namespace FTT.Environment {

    public partial class ChronalDustPickup : FTT.Core.PooledNode, FTT.Core.IPoolable {
        [Export] public int DustAmount = 10;
        [Export] public DustVisualTierSet VisualTiers;

        private const float MagnetRadius = 150f;
        private const float MagnetSpeed = 900f;
        private const float ExpirationTime = 10f;
        private float _lifetime;
        private FTT.Characters.PlayerController _magnetTarget;
        private Sprite2D _visual;

        public override void _Ready() {
            AddToGroup("story_loot");
            AddToGroup("chronal_dust");
            _visual = GetNodeOrNull<Sprite2D>("Visual");
            VisualTiers ??= GD.Load<DustVisualTierSet>("res://resources/Drops/dust_visual_tiers.tres");
            ApplyVisualTier();
        }

        public void Setup(int dustAmount) {
            DustAmount = Mathf.Max(1, dustAmount);
            ApplyVisualTier();
        }

        public void OnSpawn() {
            _lifetime = ExpirationTime;
            _magnetTarget = null;
        }

        public void OnDespawn() {
            _magnetTarget = null;
            DustAmount = 1;
            Rotation = 0f;
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            if (_magnetTarget == null) {
                Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
                using var playersLifetime = players.AsDisposable();
                foreach (var node in players) {
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
            Rotation += dt * 1.5f;
        }

        private void ApplyVisualTier() {
            if (_visual == null || VisualTiers == null) return;
            _visual.Texture = VisualTiers.GetTexture(DustAmount);
        }
    }
}
