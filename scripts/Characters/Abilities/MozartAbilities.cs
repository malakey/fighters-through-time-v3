using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Requiem Chord: a projectile chord of musical notes that bursts
    /// into a multi-hit sonic shockwave on impact. The contact note deals one
    /// per-hit tick; the burst then delivers the authored multi-hit total
    /// (BaseDamage x HitCount) as staged expanding pulses, mirroring the
    /// EinsteinEmc2Blast detonation pattern. Timing/damage come from the authored
    /// AbilityData. Story-only Resonance perks: Requiem Crescendo (a secondary,
    /// wider shockwave at 50% of the burst total) and Rest Shield (standing
    /// still or blocking for 1.5 s grants a bubble that absorbs incoming
    /// physical projectiles, implemented as the Story projectile-immunity flag).
    /// </summary>
    public partial class MozartRequiemChord : BaseSpecial {

        public const string RequiemCrescendoPerkKey = "requiem_crescendo";
        public const string RestShieldPerkKey = "rest_shield";

        private const float BasePulseRadius = 90f;
        private const float PulseRadiusGrowth = 30f;
        private const int PulseIntervalFrames = 6;
        private const float CrescendoRadiusMultiplier = 1.5f;
        private const float CrescendoDamageShare = 0.5f;
        private const float RestShieldChargeSeconds = 1.5f;
        private const float PerkRefreshSeconds = 0.1f;

        private Vector2 _burstPosition;
        private int _pulsesRemaining;
        private int _pulseIndex;
        private int _pulseCountdownFrames;
        private bool _crescendoPending;
        private float _restShieldTimer;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            LaunchChord();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void LaunchChord() {
            if (Owner == null) return;
            float contactDamage = Mathf.Round(Data?.BaseDamage ?? 4f);
            var chord = SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 45f : -45f, -5f),
                Data?.ProjectileSpeed ?? 280f, Owner.IsFacingRight, new Color(0.6f, 0.4f, 0.8f),
                new Vector2(22, 16), Data?.ProjectileLifetime ?? 5f, contactDamage);
            if (chord != null) {
                chord.DetonateOnImpact = true;
                chord.Impacted += OnChordImpacted;
            }
        }

        private void OnChordImpacted(Vector2 impactPosition) {
            // Area signals fire while the physics space is locked; defer the burst
            // setup one step so the first pulse's shape cast is legal.
            CallDeferred(nameof(BeginBurst), impactPosition);
        }

        private void BeginBurst(Vector2 impactPosition) {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            _burstPosition = impactPosition;
            _pulsesRemaining = Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 1;
            _pulseIndex = 0;
            _pulseCountdownFrames = 0;
            _crescendoPending = Owner.HasStoryPerk(RequiemCrescendoPerkKey);
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            UpdateShockwavePulses();
            UpdateRestShield((float)delta);
        }

        private void UpdateShockwavePulses() {
            if (_pulsesRemaining <= 0 && !_crescendoPending) return;
            if (_pulseCountdownFrames > 0) {
                _pulseCountdownFrames--;
                return;
            }

            if (_pulsesRemaining > 0) {
                float radius = BasePulseRadius + _pulseIndex * PulseRadiusGrowth;
                EmitShockwavePulse(radius, Data?.BaseDamage ?? 4f, "shockwave");
                _pulseIndex++;
                _pulsesRemaining--;
                _pulseCountdownFrames = PulseIntervalFrames;
                return;
            }

            // Requiem Crescendo (Story-only): the chord detonates a second time
            // with a wider shockwave dealing 50% of the burst total.
            float finalRadius = BasePulseRadius + Mathf.Max(0, _pulseIndex - 1) * PulseRadiusGrowth;
            float burstTotal = (Data?.BaseDamage ?? 4f)
                * (Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 1);
            EmitShockwavePulse(
                finalRadius * CrescendoRadiusMultiplier,
                Mathf.Round(burstTotal * CrescendoDamageShare),
                "crescendo");
            _crescendoPending = false;
        }

        private void EmitShockwavePulse(float radius, float damage, string hitboxID) {
            if (Owner == null || !IsInstanceValid(Owner)) return;

            // Pulse flash visual only; damage is applied through the shape query.
            SpawnPlaceholderZone(_burstPosition, 0f, 0.15f, 1f, new Color(0.75f, 0.55f, 0.95f), radius);

            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = radius },
                Transform = new Transform2D(0f, _burstPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "mozart_requiem_chord",
                    HitboxID = hitboxID,
                    AttackClass = AttackClass.Special,
                    Damage = damage * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(3, -2),
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = _burstPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        /// <summary>
        /// Rest Shield (Story-only): standing still or blocking for 1.5 s wraps
        /// Mozart in a silent bubble that absorbs incoming physical projectiles.
        /// The bubble is the Story projectile-immunity flag, refreshed each frame
        /// while the rest continues; moving drops the bubble and resets the charge.
        /// </summary>
        private void UpdateRestShield(float dt) {
            if (Owner == null || !Owner.HasStoryPerk(RestShieldPerkKey)) return;
            bool resting = (Owner.CurrentState == CharacterState.Idle
                    || Owner.CurrentState == CharacterState.Blocking
                    || Owner.CurrentState == CharacterState.Crouching)
                && Owner.Velocity.Length() < 1f;
            if (!resting) {
                _restShieldTimer = 0f;
                return;
            }
            _restShieldTimer += dt;
            if (_restShieldTimer >= RestShieldChargeSeconds) {
                Owner.GrantStoryProjectileImmunity(PerkRefreshSeconds);
            }
        }
    }

    /// <summary>
    /// Special 2 — Fortissimo Wave: Mozart's haymaker, the deliberate opposite of
    /// Requiem Chord's fast flat poke (V7 directive: the two projectiles must
    /// never read as duplicates). A tall wave of sound energy lobbed on a slow
    /// arc — rising first, then crashing down under its own gravity — that holds
    /// space along its path, deals the authored damage, and shoves enemies back
    /// with the roster's heaviest horizontal knockback.
    /// </summary>
    public partial class MozartFortissimoWave : BaseSpecial {

        // The lob: launched rising at 200 px/s and pulled down at 350 px/s², the
        // wave crests ~57 px up at ~0.57 s and returns to launch height ~205 px
        // out at the authored 180 px/s travel speed, then keeps falling.
        private const float LobLaunchVelocity = -200f;
        private const float LobGravity = 350f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            EmitWave();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void EmitWave() {
            if (Owner == null) return;
            float damage = Mathf.Round((Data?.BaseDamage ?? 12f) * Owner.StorySpecialDamageMultiplier);
            var wave = SpawnPlaceholderProjectile(
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 50f : -50f, -40f),
                Data?.ProjectileSpeed ?? 180f, Owner.IsFacingRight, new Color(0.8f, 0.6f, 0.9f),
                new Vector2(30, 110), Data?.ProjectileLifetime ?? 6f, damage);
            wave?.ConfigureArc(LobLaunchVelocity, LobGravity);
        }
    }

    /// <summary>
    /// Movement — Sonata Drift: deploys a floating musical staff platform under
    /// Mozart's feet that he can run on to recover or escape. Usable in the air;
    /// the authored construct persists for the design's strict 3-second limit.
    /// Story-only Resonance perk Virtuoso Dash grants +20% move speed and
    /// complete immunity to ranged projectiles while he stands on a staff.
    /// </summary>
    public partial class MozartSonataDrift : BaseSpecial {

        public const string VirtuosoDashPerkKey = "virtuoso_dash";

        private const float PlatformDropPixels = 10f;
        private const float VirtuosoSpeedMultiplier = 1.2f;
        private const float PerkRefreshSeconds = 0.1f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            DeployPlatform();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Sonata Drift",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        /// <summary>
        /// Story-only Resonance TRAVERSAL flag (V7.6, Tier 2): two Sonata Drift
        /// staff platforms may stand at once.
        /// </summary>
        public const string ExtraNotePerkKey = "extra_note";

        /// <summary>V7.6 Extra Note: additional concurrent staff platforms.</summary>
        public const int ExtraNoteAdditionalPlatforms = 1;

        private void DeployPlatform() {
            if (Owner == null) return;
            if (Data?.PersistentObjectScene == null) {
                GD.PushWarning("Sonata Drift has no PersistentObjectScene authored; deploy skipped.");
                return;
            }

            // Extra Note (Story-only traversal node, V7.6): two staff
            // platforms may stand at once instead of one. The deploy limit is
            // already modelled, so the flag only raises the cap.
            int maxActive = Data.MaxActiveObjects > 0 ? Data.MaxActiveObjects : 1;
            if (Owner.HasStoryPerk(ExtraNotePerkKey)) maxActive += ExtraNoteAdditionalPlatforms;
            while (CountActivePlatforms() >= maxActive) {
                SonataPlatformNode oldest = FindOldestPlatform();
                if (oldest == null) break;
                Owner.ActivePersistentObjects.Remove(oldest);
                oldest.ReturnToPool();
            }

            Node spawned = FTT.Core.PoolManager.Instance?.Spawn(
                Data.PersistentObjectScene,
                Owner.GlobalPosition + new Vector2(0f, PlatformDropPixels),
                Owner.GetParent());
            if (spawned is not SonataPlatformNode platform) return;

            platform.Initialize(Data, Owner);
            Owner.ActivePersistentObjects.Add(platform);
        }

        private int CountActivePlatforms() {
            int count = 0;
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is SonataPlatformNode platform && IsInstanceValid(platform)) count++;
            }
            return count;
        }

        private SonataPlatformNode FindOldestPlatform() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is SonataPlatformNode platform && IsInstanceValid(platform)) return platform;
            }
            return null;
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            UpdateVirtuosoDash();
            UpdateCharges();
        }

        // === Package 12 W4: Sonata Drift charges and the staff refund ==========
        //
        // Story-only. The owner's MovementAbilityCooldownTimer stays the ONE
        // recharge clock: it times the charge currently recharging. A charge that
        // is already restored but not yet spent is "banked" here. So the charges
        // available are (timer idle ? 1 : 0) + banked, and Extra Note (design
        // §5, Low-item decision 2026-09-26) raises the cap to two:
        //   - a cast from a full stock arms the timer through the ordinary path;
        //   - a cast while the timer runs spends a banked charge and never
        //     restarts the running recharge (TryExecute(armCooldown: false));
        //   - when a recharge completes and the stock is still short, the next
        //     charge's recharge starts at once — one at a time, on the 5 s
        //     cooldown;
        //   - landing on one of his own staff platforms halves the recharging
        //     charge's REMAINING time, once per platform (baseline kit rule).
        // Fighter Mode never reads any of this: the sim platform is a harmless
        // marker with no walkable surface, and grid perks never reach the sim.

        /// <summary>Remaining-time share a staff landing refunds (half).</summary>
        public const float StaffLandingRefundShare = 0.5f;

        private int _bankedCharges;
        private bool _chargesInitialized;
        private bool _wasRecharging;
        private bool _wasOnOwnStaff;

        /// <summary>Two with Extra Note, one otherwise.</summary>
        public int MaxCharges => Owner != null && Owner.HasStoryPerk(ExtraNotePerkKey)
            ? 1 + ExtraNoteAdditionalPlatforms
            : 1;

        /// <summary>Charges ready to cast right now.</summary>
        public int AvailableCharges =>
            (Owner != null && Owner.MovementAbilityCooldownTimer > 0f ? 0 : 1) + _bankedCharges;

        /// <summary>A banked charge lets a cast through while the recharge timer runs.</summary>
        public override bool TryConsumeCooldownBypass() {
            if (_bankedCharges <= 0) return false;
            _bankedCharges--;
            return true;
        }

        private void UpdateCharges() {
            if (Owner == null) return;
            int maxBanked = MaxCharges - 1;
            if (_bankedCharges > maxBanked) _bankedCharges = maxBanked;
            bool recharging = Owner.MovementAbilityCooldownTimer > 0f;

            if (!_chargesInitialized) {
                // A hero who starts idle starts with a full stock.
                _chargesInitialized = true;
                if (!recharging) _bankedCharges = maxBanked;
            } else if (_wasRecharging && !recharging && _bankedCharges < maxBanked) {
                // One recharge finished and the stock is still short: bank the
                // restored charge and start recharging the next one.
                _bankedCharges++;
                RearmCooldown();
                recharging = true;
            }
            _wasRecharging = recharging;

            SonataPlatformNode landed = FindStaffUnderOwner();
            bool onStaff = landed != null;
            if (onStaff && !_wasOnOwnStaff && landed.TryConsumeLandingRefund()) {
                if (Owner.MovementAbilityCooldownTimer > 0f) {
                    Owner.MovementAbilityCooldownTimer *= 1f - StaffLandingRefundShare;
                }
            }
            _wasOnOwnStaff = onStaff;
        }

        private SonataPlatformNode FindStaffUnderOwner() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is SonataPlatformNode platform && IsInstanceValid(platform)
                    && platform.IsStandingOn(Owner)) return platform;
            }
            return null;
        }

        /// <summary>
        /// Virtuoso Dash (Story-only): while Mozart stands on one of his staff
        /// platforms he gains a 20% speed boost (via the shared temporary
        /// speed-buff mechanism, refreshed each frame) and complete immunity to
        /// ranged projectiles (the Story projectile-immunity flag).
        /// </summary>
        private void UpdateVirtuosoDash() {
            if (Owner == null || !Owner.HasStoryPerk(VirtuosoDashPerkKey)) return;
            if (!IsStandingOnStaffPlatform()) return;
            Owner.ApplyStorySpeedBuff(VirtuosoSpeedMultiplier, PerkRefreshSeconds);
            Owner.GrantStoryProjectileImmunity(PerkRefreshSeconds);
        }

        private bool IsStandingOnStaffPlatform() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is SonataPlatformNode platform && IsInstanceValid(platform)
                    && platform.IsStandingOn(Owner)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Ultimate — Symphony of Sorrow: Mozart rises into a hover and conducts a
    /// downpour of giant glowing piano keys that rain like meteors across the
    /// stage ahead of him. Canonical data-driven bombardment (audit gap X7):
    /// HitCount meteor strikes land at the authored DamageTickIntervalFrames
    /// cadence through the authored active window, each striking a
    /// HitboxSize-footprint of ground via a hurtbox shape query with the
    /// shield-bypassing Ultimate attack class; only the final strike carries
    /// the authored KnockbackForce. The hover is the shared Story float window
    /// (Owner.StoryFloatTimer) held for the active phase. Timing, damage, hit
    /// count, cadence, and knockback all come from the authored AbilityData.
    /// </summary>
    public partial class MozartSymphonyOfSorrow : BaseSpecial {

        private const float HoverRiseSpeed = -140f;
        private const float StrikeFlashSeconds = 0.15f;

        private int _strikesRemaining;
        private int _strikeIndex;
        private int _strikeCountdownFrames;
        private float _stormOriginX;
        private float _stormGroundY;
        private bool _stormFacingRight;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate()
                && (_meter?.IsFull ?? Owner.CurrentUltimateMeter >= UltimateMeter.MaxValue);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _strikesRemaining = 0;
            _strikeIndex = 0;
            _strikeCountdownFrames = 0;
            // The meteors target the ground line Mozart conducts from, captured
            // before he rises into the hover.
            _stormOriginX = Owner.GlobalPosition.X;
            _stormGroundY = Owner.GlobalPosition.Y;
            _stormFacingRight = Owner.IsFacingRight;
            Owner.Velocity = new Vector2(Owner.Velocity.X, HoverRiseSpeed);
            Owner.DrainUltimateMeter(UltimateMeter.MaxValue);
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            _strikesRemaining = Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 1;
            _strikeIndex = 0;
            _strikeCountdownFrames = 0;
            // Hover: the shared Story float window (0.15x gravity, capped fall)
            // held for the whole authored bombardment window.
            Owner.StoryFloatTimer = Mathf.Max(1, Data?.ActiveFrames ?? 180) / 60f;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        /// <summary>
        /// H-4: interrupting the bombardment releases the conducted hover — the
        /// shared Story float window must not keep softening gravity through the
        /// interposed stun. The meteor strikes themselves are phase-gated, so
        /// clearing the phase already stops them.
        /// </summary>
        protected override void OnInterrupted() {
            if (Owner != null) Owner.StoryFloatTimer = 0f;
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            UpdateMeteorStrikes();
        }

        private void UpdateMeteorStrikes() {
            if (CurrentPhase != AbilityPhase.Active || _strikesRemaining <= 0) return;
            if (_strikeCountdownFrames > 0) {
                _strikeCountdownFrames--;
                return;
            }

            bool finalStrike = _strikesRemaining == 1;
            LandMeteorStrike(_strikeIndex, finalStrike);
            _strikeIndex++;
            _strikesRemaining--;
            _strikeCountdownFrames = Mathf.Max(1, Data?.DamageTickIntervalFrames ?? 18);
        }

        /// <summary>
        /// One piano-key meteor impact: a HitboxSize ground footprint landing
        /// ahead of the cast point, tiling forward one key-width per strike so
        /// the bombardment sweeps across the stage in front of Mozart.
        /// </summary>
        private void LandMeteorStrike(int strikeIndex, bool finalStrike) {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            Vector2 footprint = Data?.HitboxSize ?? new Vector2(60, 50);
            float leadOffset = Data?.HitboxOffset.X ?? 30f;
            float advance = leadOffset + strikeIndex * footprint.X;
            var strikePosition = new Vector2(
                _stormOriginX + (_stormFacingRight ? advance : -advance),
                _stormGroundY);

            // Impact flash placeholder (mechanics only; presentation is Package 8).
            SpawnPlaceholderZone(
                strikePosition, 0f, StrikeFlashSeconds, 1f,
                new Color(0.85f, 0.8f, 1.0f), footprint.X / 2f);

            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new RectangleShape2D { Size = footprint },
                Transform = new Transform2D(0f, strikePosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "mozart_symphony_of_sorrow",
                    HitboxID = finalStrike ? "meteor_final" : "meteor",
                    AttackClass = AttackClass.Ultimate,
                    Damage = Mathf.Round(Data?.BaseDamage ?? 8f) * Owner.StorySpecialDamageMultiplier,
                    // Only the closing strike launches; earlier keys pin the
                    // target inside the bombardment.
                    Knockback = finalStrike ? Data?.KnockbackForce ?? new Vector2(5, -4) : Vector2.Zero,
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = strikePosition,
                    AttackerFacingRight = _stormFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.4f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.2f
                });
                // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its caster
                // ZERO damage-dealt meter, regardless of HP removed, target count or
                // when it lands. Direct-hit Rally reclaim is retained (D03g).
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt, ultimateOrigin: true);
            }
        }
    }
}
