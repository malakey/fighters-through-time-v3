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

        /// <summary>Glow pulses per second. Presentation only.</summary>
        [Export(PropertyHint.Range, "0,6,0.1")] public float GlowPulseHz = 1.4f;

        private float _lifetime = 30f;
        private float _bobTimer;
        private Sprite2D _icon;
        private Sprite2D _glow;
        private bool _visualsResolved;

        /// <summary>
        /// Canonical per-effect orb colour (Package 8 B6).
        ///
        /// <para>Shared with the Fighter driver's orb proxies so a green orb means
        /// "health" in both modes. Before this existed, the Story orb had no colour
        /// at all and the driver carried an anonymous switch on the effect index.</para>
        /// </summary>
        public static Color EffectColor(OrbEffect effect) => effect switch {
            OrbEffect.HPRestore => new Color(0.2f, 1f, 0.35f),
            OrbEffect.MeterBoost => new Color(1f, 0.85f, 0.1f),
            OrbEffect.SpeedBuff => new Color(0.4f, 0.75f, 1f),
            _ => new Color(0.75f, 0.35f, 1f)
        };

        public override void _Ready() {
            ResolveVisuals();
            ApplyDataVisual();
        }

        public void OnSpawn() {
            _lifetime = 30f;
            _bobTimer = 0;
            ResolveVisuals();
            ApplyDataVisual();
        }

        public void OnDespawn() {
            if (_glow != null) {
                _glow.Scale = Vector2.One;
                _glow.SelfModulate = Colors.White;
            }
        }

        public override void _PhysicsProcess(double delta) {
            float dt = (float)delta;
            _lifetime -= dt;
            if (_lifetime <= 0) { ReturnToPool(); return; }

            _bobTimer += dt;
            Position = new Vector2(Position.X, Position.Y + Mathf.Sin(_bobTimer * 3f) * 0.5f);
            ApplyGlowPulse(_bobTimer);
        }

        /// <summary>
        /// Pulse envelope at <paramref name="seconds"/>. Extracted so the breathing
        /// curve is assertable without running the scene.
        /// </summary>
        public float GlowPulseAt(float seconds) =>
            0.85f + 0.15f * Mathf.Sin(seconds * GlowPulseHz * Mathf.Tau);

        private void ApplyGlowPulse(float seconds) {
            if (_glow == null) return;
            float pulse = GlowPulseAt(seconds);
            _glow.Scale = new Vector2(pulse, pulse);
            _glow.SelfModulate = new Color(1f, 1f, 1f, pulse);
        }

        /// <summary>
        /// Pushes the authored <c>ChronalOrbData.Icon</c> onto the icon sprite and
        /// tints the additive glow by effect. <c>Icon</c> was an exported field with
        /// no consumer anywhere in the project before Package 8.
        /// </summary>
        public void ApplyDataVisual() {
            ResolveVisuals();
            if (Data == null) return;
            if (_icon != null && Data.Icon != null) _icon.Texture = Data.Icon;
            Color color = EffectColor(Data.Effect);
            if (_glow != null) _glow.Modulate = new Color(color.R, color.G, color.B, 0.55f);
            if (_icon != null) _icon.Modulate = color;
        }

        private void ResolveVisuals() {
            if (_visualsResolved && GodotObject.IsInstanceValid(_icon)) return;
            _icon = GetNodeOrNull<Sprite2D>("Icon");
            _glow = GetNodeOrNull<Sprite2D>("Glow");
            _visualsResolved = true;
        }

        public void OnPickedUp(FTT.Characters.PlayerController player) {
            if (Data == null) return;
            switch (Data.Effect) {
                case OrbEffect.HPRestore:
                    // HealStory caps against MaximumHP (Resonance bonus /
                    // encounter override included, unlike raw Data.MaxHP) and
                    // raises the HP-changed event so the HUD follows.
                    player.HealStory((int)Data.Value);
                    break;
                case OrbEffect.MeterBoost:
                    var meter = player.GetNodeOrNull<UltimateMeter>("UltimateMeter");
                    if (meter != null) {
                        meter.AddFlat(Data.Value);
                        player.CurrentUltimateMeter = meter.CurrentValue;
                    } else {
                        player.CurrentUltimateMeter = Mathf.Min(player.CurrentUltimateMeter + Data.Value, 100f);
                    }
                    break;
                case OrbEffect.ShieldRestore:
                    // Restore through the authoritative BlockSystem; the
                    // controller field is a stale display mirror that nothing
                    // combat-side reads.
                    var blockSystem = player.GetNodeOrNull<BlockSystem>("BlockSystem");
                    if (blockSystem != null) {
                        blockSystem.RestoreAllCharges();
                        player.CurrentBlockCharges = blockSystem.CurrentCharges;
                    } else {
                        player.CurrentBlockCharges = player.MaximumBlockCharges;
                    }
                    break;
            }
            ReturnToPool();
        }
    }
}
