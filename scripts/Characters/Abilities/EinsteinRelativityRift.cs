using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 2 — Relativity Rift: a localized distortion field that inflicts
    /// TimeDilation (-50% enemy movement/jump/animation speed) and continuous chip
    /// damage (authored: 1.5 per 0.5 s tick over 3 s). If Einstein stands inside
    /// his own rift he gains +25% movement speed, mirroring the Fighter-sim zone.
    /// All tuning comes from the authored AbilityData resource.
    /// </summary>
    public partial class EinsteinRelativityRift : BaseSpecial {

        private const float RiftRadius = 100f;
        private const float OwnerSpeedMultiplier = 1.25f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            EmitCharacterVfx(
                "res://scenes/vfx/einstein/EinsteinRelativityRiftVfx.tscn",
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 72f : -72f, -26f));
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            SpawnRift();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void SpawnRift() {
            if (Owner == null) return;
            float tickInterval = (Data?.DamageTickIntervalFrames ?? 30) / 60f;
            if (tickInterval <= 0f) tickInterval = 0.5f;
            SpawnPlaceholderZone(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 100f : -100f, 0f),
                Data?.BaseDamage ?? 1.5f,
                Data?.Lifetime > 0f ? Data.Lifetime : 3f,
                tickInterval,
                new Color(0.4f, 0.3f, 0.9f),
                // Story-only ZoneRadius minor ("Rift Range +10%", einstein_u2)
                // widens the rift; neutral 1f outside Story Mode.
                RiftRadius * Owner.StoryZoneRadiusMultiplier,
                Data?.AppliedStatus ?? FTT.Core.StatusType.TimeDilation,
                Data?.StatusDuration > 0f ? Data.StatusDuration : 3f,
                Data?.StatusIntensity ?? 1f,
                OwnerSpeedMultiplier);
        }
    }
}
