using Godot;

namespace FTT.Combat {

    /// <summary>
    /// The reusable placeholder effect taxonomy (Package 8 B6).
    ///
    /// <para>Five shapes cover every ability cast and every roster telegraph/active/
    /// death beat in the game. Authoring 36 bespoke ability effects and 42 bespoke
    /// roster effects would be 78 placeholder scenes to throw away at P10; a family
    /// plus a per-character or per-source accent colour carries the same
    /// information at a fraction of the content.</para>
    /// </summary>
    public enum VfxEffectFamily {
        /// <summary>Radial pop. Generic cast, teleport, dash, shield.</summary>
        Burst,
        /// <summary>Horizontally stretched arc that thins as it sweeps. Melee.</summary>
        Slash,
        /// <summary>Long thin streak along the facing axis. Projectiles and beams.</summary>
        Beam,
        /// <summary>Flattened expanding ring. Ground slams, pulses, waves.</summary>
        Shockwave,
        /// <summary>Slow rising motes. Summons, deployments, constructs.</summary>
        Summon,
        /// <summary>Fast collapsing spark. Confirmed hits.</summary>
        Impact
    }

    /// <summary>
    /// Root script for the <c>scenes/vfx/</c> taxonomy.
    ///
    /// <para>Derives from <see cref="FTT.Core.PooledPlaceholder"/> rather than
    /// re-implementing pooling: lifetime, the shared particle-budget reservation,
    /// the off-screen suspension handshake and the Story rewind contract are all
    /// inherited unchanged. This class adds only the per-family presentation curve.
    /// It re-declares <c>IPoolable</c> so the pool's interface dispatch lands on the
    /// overrides below and still chains to the base behaviour.</para>
    ///
    /// <para>The curve animates the <c>Visual</c> and <c>Particles</c> children's own
    /// transform and <c>SelfModulate</c>, never the root <c>Modulate</c> — the root is
    /// where <see cref="VfxEmitter"/> writes the caller's accent tint after spawn, and
    /// an animation that owned it would erase the character's identity every frame.</para>
    /// </summary>
    public partial class VfxEffect : FTT.Core.PooledPlaceholder, FTT.Core.IPoolable {
        [Export] public VfxEffectFamily Family = VfxEffectFamily.Burst;

        /// <summary>Upward drift applied to <c>Summon</c>, in pixels per second.</summary>
        [Export(PropertyHint.Range, "0,400,1")] public float SummonRiseSpeed = 90f;

        private Node2D _visual;
        private Node2D _particles;
        private int _ageFrames;
        private bool _resolved;
        private bool _active;

        public new void OnSpawn() {
            base.OnSpawn();
            ResolveChildren();
            _ageFrames = 0;
            _active = true;
            if (Family == VfxEffectFamily.Summon) RuntimeVelocity = new Vector2(0f, -SummonRiseSpeed);
            ApplyCurve(0f);
        }

        public new void OnDespawn() {
            _active = false;
            base.OnDespawn();
            ResolveChildren();
            if (_visual != null) {
                _visual.Scale = Vector2.One;
                _visual.Rotation = 0f;
                _visual.SelfModulate = Colors.White;
            }
            if (_particles != null) _particles.SelfModulate = Colors.White;
            _ageFrames = 0;
        }

        public override void _PhysicsProcess(double delta) {
            // The base advances position from RuntimeVelocity and owns the lifetime
            // countdown that returns this instance to the pool.
            base._PhysicsProcess(delta);
            // The base call can retire this instance on its final frame; do not
            // repaint a node the pool has already reset.
            if (!_active || LifetimeFrames <= 0 || !IsInsideTree()) return;
            _ageFrames++;
            ApplyCurve(Mathf.Clamp((float)_ageFrames / LifetimeFrames, 0f, 1f));
        }

        /// <summary>
        /// Shape of the effect at normalised age <paramref name="t"/>. Pure function
        /// of the family and the age, so a test can assert the silhouette without
        /// stepping the engine.
        /// </summary>
        public static Vector2 ScaleAt(VfxEffectFamily family, float t) {
            t = Mathf.Clamp(t, 0f, 1f);
            return family switch {
                VfxEffectFamily.Burst => new Vector2(Mathf.Lerp(0.35f, 1.5f, t), Mathf.Lerp(0.35f, 1.5f, t)),
                VfxEffectFamily.Slash => new Vector2(Mathf.Lerp(0.45f, 1.7f, t), Mathf.Lerp(1.15f, 0.45f, t)),
                VfxEffectFamily.Beam => new Vector2(Mathf.Lerp(0.25f, 2.4f, t), 0.35f),
                VfxEffectFamily.Shockwave => new Vector2(Mathf.Lerp(0.35f, 2.6f, t), Mathf.Lerp(0.3f, 0.75f, t)),
                VfxEffectFamily.Summon => new Vector2(Mathf.Lerp(0.2f, 1.0f, Mathf.Min(1f, t * 3f)),
                                                      Mathf.Lerp(0.2f, 1.0f, Mathf.Min(1f, t * 3f))),
                _ => new Vector2(Mathf.Lerp(1.25f, 0.2f, t), Mathf.Lerp(1.25f, 0.2f, t))
            };
        }

        /// <summary>Opacity at normalised age. Every family ends fully transparent.</summary>
        public static float AlphaAt(VfxEffectFamily family, float t) {
            t = Mathf.Clamp(t, 0f, 1f);
            return family switch {
                // Summons fade in before they fade out; everything else decays.
                VfxEffectFamily.Summon => Mathf.Min(1f, t * 5f) * (1f - t),
                VfxEffectFamily.Impact => 1f - t * t,
                _ => 1f - t
            };
        }

        /// <summary>Sweep angle in radians; only the slash rotates.</summary>
        public static float RotationAt(VfxEffectFamily family, float t) =>
            family == VfxEffectFamily.Slash ? Mathf.Lerp(-0.5f, 0.5f, Mathf.Clamp(t, 0f, 1f)) : 0f;

        private void ApplyCurve(float t) {
            ResolveChildren();
            float alpha = AlphaAt(Family, t);
            if (_visual != null) {
                _visual.Scale = ScaleAt(Family, t);
                _visual.Rotation = RotationAt(Family, t);
                _visual.SelfModulate = new Color(1f, 1f, 1f, alpha);
            }
            if (_particles != null) _particles.SelfModulate = new Color(1f, 1f, 1f, alpha);
        }

        private void ResolveChildren() {
            if (_resolved && GodotObject.IsInstanceValid(_visual)) return;
            _visual = GetNodeOrNull<Node2D>("Visual");
            _particles = GetNodeOrNull<Node2D>("Particles");
            _resolved = true;
        }
    }
}
