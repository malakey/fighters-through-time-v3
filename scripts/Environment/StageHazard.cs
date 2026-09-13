using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {
    public partial class StageHazard : Node2D, IStoryTimeFreezable {
        [Export] public float WarningDuration = 2.0f;
        [Export] public float ActiveDuration = 3.0f;
        [Export] public float CooldownDuration = 30.0f;
        [Export] public float Damage = 15f;
        [Export] public bool Enabled = true;

        private FTT.Core.HazardPhase _phase = FTT.Core.HazardPhase.Cooldown;
        private float _timer;
        private Area2D _damageZone;
        // V7.3: per-body float damage accumulator. The old (int)(Damage * dt)
        // truncated to zero every frame at 60 FPS (15 x 1/60 = 0.25 -> 0), so
        // the hazard dealt no damage at all — and what it did deal was
        // frame-rate dependent. Fractions now bank until a whole HP is owed.
        private readonly Dictionary<ulong, float> _damageOwed = new();

        public override void _Ready() {
            _damageZone = GetNodeOrNull<Area2D>("DamageZone");
            _timer = CooldownDuration;
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
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
                        // The active window closed: unpaid fractions are void.
                        _damageOwed.Clear();
                        FTT.Core.EventBus.Instance?.RaiseHazardStateChanged(new FTT.Core.HazardStatePayload { HazardID = Name, Phase = _phase, Duration = CooldownDuration });
                        break;
                }
            }

            ProcessActiveDamage(dt);
        }

        /// <summary>
        /// V7.3: the pure banking rule — <paramref name="owed"/> accumulates
        /// damage-per-second x dt and the whole HP now due is returned (and
        /// deducted). Total damage over a window depends only on elapsed time,
        /// never on the frame rate — the old <c>(int)(Damage * dt)</c>
        /// truncated to zero every frame at 60 FPS.
        /// </summary>
        public static int AccumulateDamage(ref float owed, float damagePerSecond, float dt) {
            owed += damagePerSecond * dt;
            int whole = (int)owed;
            if (whole > 0) owed -= whole;
            return whole;
        }

        private void ProcessActiveDamage(float dt) {
            if (_phase == FTT.Core.HazardPhase.Active && _damageZone != null) {
                Godot.Collections.Array<Node2D> bodies = _damageZone.GetOverlappingBodies();
                using var bodiesLifetime = bodies.AsDisposable();
                foreach (var body in bodies) {
                    if (body is FTT.Characters.PlayerController pc) {
                        // Bank Damage-per-second x dt per body; pay out whole
                        // HP through the V7.3 environmental chokepoint. Total
                        // damage over the active window is now frame-rate
                        // independent.
                        ulong id = pc.GetInstanceId();
                        float owed = _damageOwed.GetValueOrDefault(id);
                        int due = AccumulateDamage(ref owed, Damage, dt);
                        if (due > 0) pc.ApplyEnvironmentalDamage(due);
                        _damageOwed[id] = owed;
                    }
                }
            }
        }

        // === IStoryTimeFreezable (V7.6 Time Freeze) ===========================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the world. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Stops simulating in place. Nothing else is mutated, so the phase, the
        /// timer and the position all survive and resume with no catch-up tick —
        /// the collision shape stays live throughout.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

    }
}
