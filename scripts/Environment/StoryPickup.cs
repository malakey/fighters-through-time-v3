using FTT.Characters;
using FTT.Core;
using Godot;

namespace FTT.Environment {

    public partial class StoryPickup : PooledNode, IPoolable {
        [Export] public float MagnetRadius = 150f;
        [Export] public float MagnetSpeed = 900f;
        [Export] public float ExpirationSeconds = 10f;

        public StoryPickupKind Kind { get; private set; }
        public int HealingAmount { get; private set; }
        public float BuffMultiplier { get; private set; } = 1f;
        public float BuffDurationSeconds { get; private set; }

        private float _lifetime;
        private float _hoverTime;
        private PlayerController _magnetTarget;
        private Sprite2D _visual;

        public override void _Ready() {
            AddToGroup("story_loot");
            _visual = GetNodeOrNull<Sprite2D>("Visual");
        }

        public void Setup(StoryPickupKind kind, StoryDropProfile profile) {
            Kind = kind;
            HealingAmount = profile?.HealingAmount ?? 0;
            BuffMultiplier = profile?.BuffMultiplier ?? 1f;
            BuffDurationSeconds = profile?.BuffDurationSeconds ?? 0f;
            ApplyPlaceholderPresentation();
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0f) { ReturnToPool(); return; }

            _hoverTime += dt;
            Rotation += dt * 1.8f;
            if (_visual != null) _visual.Position = new Vector2(0f, Mathf.Sin(_hoverTime * 4f) * 5f);

            ResolveMagnetTarget();
            if (_magnetTarget == null) return;
            Vector2 difference = _magnetTarget.GlobalPosition - GlobalPosition;
            if (difference.Length() > 0.01f) GlobalPosition += difference.Normalized() * MagnetSpeed * dt;
            if (GlobalPosition.DistanceTo(_magnetTarget.GlobalPosition) <= 20f) Collect(_magnetTarget);
        }

        public bool Collect(PlayerController player) {
            if (player == null) return false;
            switch (Kind) {
                case StoryPickupKind.Healing:
                    player.HealStory(HealingAmount);
                    break;
                case StoryPickupKind.DamageBuff:
                    player.ApplyStoryDamageBuff(BuffMultiplier, BuffDurationSeconds);
                    break;
                case StoryPickupKind.SpeedBuff:
                    player.ApplyStorySpeedBuff(BuffMultiplier, BuffDurationSeconds);
                    break;
            }
            ReturnToPool();
            return true;
        }

        public void OnSpawn() {
            _lifetime = ExpirationSeconds;
            _hoverTime = 0f;
            _magnetTarget = null;
            Rotation = 0f;
            Modulate = Colors.White;
        }

        public void OnDespawn() {
            _magnetTarget = null;
            HealingAmount = 0;
            BuffMultiplier = 1f;
            BuffDurationSeconds = 0f;
            Rotation = 0f;
            if (_visual != null) _visual.Position = Vector2.Zero;
        }

        private void ResolveMagnetTarget() {
            if (_magnetTarget != null && IsInstanceValid(_magnetTarget)) return;
            _magnetTarget = null;
            Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("StoryPlayer");
            using var playersLifetime = players.AsDisposable();
            foreach (Node node in players) {
                if (node is PlayerController player && GlobalPosition.DistanceTo(player.GlobalPosition) <= MagnetRadius) {
                    _magnetTarget = player;
                    return;
                }
            }
        }

        private void ApplyPlaceholderPresentation() {
            if (_visual == null) return;
            _visual.Modulate = Kind switch {
                StoryPickupKind.Healing => new Color("57df73"),
                StoryPickupKind.DamageBuff => new Color("ef554f"),
                StoryPickupKind.SpeedBuff => new Color("48dfea"),
                _ => Colors.White
            };
        }
    }
}
