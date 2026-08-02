using Godot;

namespace FTT.Environment {
    public partial class StageHazard : Node2D {
        [Export] public float WarningDuration = 2.0f;
        [Export] public float ActiveDuration = 3.0f;
        [Export] public float CooldownDuration = 30.0f;
        [Export] public float Damage = 15f;
        [Export] public bool Enabled = true;

        private FTT.Core.HazardPhase _phase = FTT.Core.HazardPhase.Cooldown;
        private float _timer;
        private Area2D _damageZone;

        public override void _Ready() {
            _damageZone = GetNodeOrNull<Area2D>("DamageZone");
            _timer = CooldownDuration;
        }

        public override void _PhysicsProcess(double delta) {
            if (!Enabled) return;
            float dt = (float)delta;
            _timer -= dt;

            if (_timer <= 0) {
                switch (_phase) {
                    case FTT.Core.HazardPhase.Cooldown:
                        _phase = FTT.Core.HazardPhase.Warning;
                        _timer = WarningDuration;
                        FTT.Core.EventBus.Instance?.RaiseHazardStateChanged(new FTT.Core.HazardStatePayload { HazardID = Name, Phase = _phase, Duration = WarningDuration });
                        break;
                    case FTT.Core.HazardPhase.Warning:
                        _phase = FTT.Core.HazardPhase.Active;
                        _timer = ActiveDuration;
                        FTT.Core.EventBus.Instance?.RaiseHazardStateChanged(new FTT.Core.HazardStatePayload { HazardID = Name, Phase = _phase, Duration = ActiveDuration });
                        break;
                    case FTT.Core.HazardPhase.Active:
                        _phase = FTT.Core.HazardPhase.Cooldown;
                        _timer = CooldownDuration;
                        FTT.Core.EventBus.Instance?.RaiseHazardStateChanged(new FTT.Core.HazardStatePayload { HazardID = Name, Phase = _phase, Duration = CooldownDuration });
                        break;
                }
            }

            if (_phase == FTT.Core.HazardPhase.Active && _damageZone != null) {
                foreach (var body in _damageZone.GetOverlappingBodies()) {
                    if (body is FTT.Characters.PlayerController pc) {
                        pc.ApplyDamage((int)(Damage * dt));
                    }
                }
            }
        }
    }
}
