using Godot;

namespace FTT.Combat {
    public enum OrbEffect { HPRestore, MeterBoost, SpeedBuff, DamageBoost, ShieldRestore }

    [GlobalClass]
    public partial class ChronalOrbData : Resource {
        [Export] public OrbEffect Effect = OrbEffect.HPRestore;
        [Export] public float Value = 25f;
        [Export] public float Duration = 10f;
        [Export] public Texture2D Icon;
    }

    public partial class ChronalOrbItem : FTT.Core.PooledNode, FTT.Core.IPoolable {
        [Export] public ChronalOrbData Data;

        private float _lifetime = 30f;
        private float _bobTimer;

        public void OnSpawn() { _lifetime = 30f; _bobTimer = 0; }
        public void OnDespawn() { }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            _bobTimer += dt;
            Position = new Vector2(Position.X, Position.Y + Mathf.Sin(_bobTimer * 3f) * 0.5f);
        }

        public void OnPickedUp(FTT.Characters.PlayerController player) {
            if (Data == null) return;
            switch (Data.Effect) {
                case OrbEffect.HPRestore:
                    player.CurrentHP = Mathf.Min(player.CurrentHP + (int)Data.Value, player.Data?.MaxHP ?? 100);
                    break;
                case OrbEffect.MeterBoost:
                    player.CurrentUltimateMeter = Mathf.Min(player.CurrentUltimateMeter + Data.Value, 100f);
                    break;
                case OrbEffect.ShieldRestore:
                    player.CurrentBlockCharges = player.Data?.MaxBlockCharges ?? 3;
                    break;
            }
            ReturnToPool();
        }
    }
}
