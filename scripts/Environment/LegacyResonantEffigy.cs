using Godot;
using FTT.Core;

namespace FTT.Environment {

    /// <summary>
    /// A Level 4A <b>resonant effigy</b> — the receiving surface for a kit gate whose
    /// authored ability cannot reach an ordinary strike surface (Package 11 B1).
    ///
    /// <para><b>Why this exists.</b> <see cref="LegacyKitGate"/>'s
    /// <see cref="LegacyGateMode.Strike"/> surface is an
    /// <see cref="EnvironmentHurtboxAdapter"/> on the <c>PersistentObject</c> layer,
    /// which is reachable by anything that delivers through a real
    /// <see cref="FTT.Combat.Hitbox"/> (<c>PlayerHitboxMask</c>) or a pooled
    /// projectile (<c>ProjectileMask</c>) — both masks include
    /// <c>PersistentObject</c>. Three shipped Story abilities deliver instead
    /// through a hand-rolled <c>PhysicsShapeQueryParameters2D</c> masked to
    /// <c>EnemyHurtbox</c> alone, so they can never touch that surface:
    /// <c>joan_divine_piercing</c> (the thrust flurry),
    /// <c>lincoln_emancipator</c>'s ground wave, and
    /// <c>leonardo_clockwork_turret</c>'s bolts. A gate keyed to one of those is
    /// unopenable in <c>Strike</c> mode and has no zone to poll in <c>Zone</c> mode.</para>
    ///
    /// <para><b>What it does.</b> It hangs a second, deliberately narrow receiver on
    /// the gate: an <see cref="EnvironmentHurtboxAdapter"/> on the
    /// <c>EnemyHurtbox</c> layer with an unowned <c>OwnerPlayerIndex</c>, so those
    /// enemy-hurtbox queries find it. Everything it receives is forwarded to
    /// <see cref="LegacyKitGate.TryResolve"/>, which still accepts <b>only</b> the
    /// gate's one authored ability ID — so this is not a loosening of V01c: a basic
    /// combo hit, another special, an enemy hitbox or an incidental contact is
    /// rejected exactly as it is on the ordinary strike surface. It deals and takes
    /// no damage, credits no meter and awards nothing, and it goes inert the moment
    /// its gate latches so it cannot keep soaking attacks or drawing turret fire.</para>
    ///
    /// <para><b>Deviation note.</b> The plan (§4 B1/B2/B3) says a hero whose special
    /// is neither the Strike nor the Zone shape needs a <i>fifth</i>
    /// <see cref="LegacyGateMode"/>. Adding one means editing A12's shared enum while
    /// B1, B2 and B3 all run in parallel against the same need, so B1 works around it
    /// in variant-owned code instead and records the request in the plan's §9. The
    /// real fix is one line in A12's <see cref="LegacyKitGate"/>: give the Strike
    /// surface the <c>EnemyHurtbox</c> layer as well, and this class disappears.</para>
    ///
    /// <para>Story-only. Nothing here is visible to <c>scripts/FighterSim/</c>.</para>
    /// </summary>
    public partial class LegacyResonantEffigy : Node2D {

        /// <summary>The gate this effigy answers for. Assign before adding to the tree.</summary>
        public LegacyKitGate Gate { get; set; }

        /// <summary>The receiving surface's size. Generous: several sources are sweeps, not points.</summary>
        [Export] public Vector2 SurfaceSize = new(120f, 190f);

        /// <summary>True once the gate has latched and the effigy has stopped receiving.</summary>
        public bool IsInert { get; private set; }

        private EnvironmentHurtboxAdapter _receiver;
        private CollisionShape2D _shape;
        private ColorRect _visual;

        public override void _Ready() {
            BuildPresentation();
            BuildReceiver();
            if (Gate != null && IsInstanceValid(Gate)) {
                Gate.Resolved += OnGateResolved;
                if (Gate.IsResolved) GoInert();
            }
        }

        public override void _ExitTree() {
            if (Gate != null && IsInstanceValid(Gate)) Gate.Resolved -= OnGateResolved;
        }

        private void BuildReceiver() {
            _receiver = new EnvironmentHurtboxAdapter {
                Name = "EffigyReceiver",
                // Unowned: every enemy-hurtbox query in the codebase skips a hurtbox
                // whose OwnerPlayerIndex matches the attacker, and -1 is never a
                // player slot.
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.EnemyHurtbox,
                CollisionMask = 0,
                // Detects nothing itself; it is purely a target.
                Monitoring = false,
                Monitorable = true
            };
            _shape = new CollisionShape2D {
                Shape = new RectangleShape2D { Size = SurfaceSize },
                Position = new Vector2(0f, -SurfaceSize.Y / 2f)
            };
            _receiver.AddChild(_shape);
            _receiver.OnHit += OnStruck;
            AddChild(_receiver);
        }

        /// <summary>
        /// Forwards to the gate and returns <b>zero damage</b>: an effigy is a
        /// mechanism, not a combat target, so it credits no Influence, no Rally echo
        /// and no reward. A hit from an enemy (unowned attacker index) is ignored.
        /// </summary>
        internal float OnStruck(FTT.Combat.HitPayload payload) {
            if (IsInert || Gate == null || !IsInstanceValid(Gate)) return 0f;
            if (payload.AttackerIndex < 0) return 0f;
            Gate.TryResolve(payload.AttackID);
            return 0f;
        }

        private void OnGateResolved(LegacyKitGate gate) => GoInert();

        /// <summary>
        /// Stops receiving. Uses the physics-safe setters: the forward above can run
        /// from inside a hit callback, where the engine refuses a direct monitoring
        /// or shape write.
        /// </summary>
        public void GoInert() {
            if (IsInert) return;
            IsInert = true;
            _receiver?.SetMonitorableSafe(false);
            _receiver?.SetMonitoringSafe(false);
            _shape?.SetShapeDisabledSafe(true);
            UpdatePresentation();
        }

        // === Presentation (graybox; Package 10 replaces it) ===

        private void BuildPresentation() {
            _visual = new ColorRect {
                Name = "EffigyVisual",
                Size = new Vector2(28, 150),
                Position = new Vector2(-14, -150),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(_visual);
            UpdatePresentation();
        }

        private void UpdatePresentation() {
            if (_visual == null) return;
            _visual.Color = IsInert
                ? new Color(0.3f, 0.9f, 0.75f, 0.3f)
                : new Color(0.9f, 0.72f, 0.3f, 0.7f);
        }
    }
}
