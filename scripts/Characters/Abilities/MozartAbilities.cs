using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Requiem Chord (M02/M03, Package 13 W7b): a straight chord of
    /// musical notes at the authored 14 units/s with <b>no range limit</b> — it
    /// crosses the stage until it strikes a target, terrain or a wall. It bursts
    /// there into the authored <c>HitCount</c> pulses (3 × 4) in a 1.2-unit
    /// radius over 0.3 s; a contact hit adds the first 4 (16 total). The contact
    /// and the first two pulses hold the target in hitstun with no impulse, so
    /// the whole burst lands; the last pulse carries the authored knockback. It
    /// hits airborne targets. <b>Tempo (M02):</b> an execution that hits an
    /// opponent (not a block) shortens Fortissimo Wave's remaining cooldown by
    /// 2 s, once per execution. Story-only Resonance perks: Requiem Crescendo (a
    /// secondary, wider shockwave at 50% of the burst total) and Rest Shield
    /// (standing still or blocking for 1.5 s grants a bubble that absorbs
    /// incoming physical projectiles, implemented as the Story
    /// projectile-immunity flag).
    ///
    /// <para>The terrain/wall burst is a local rule: W7a owns the shared
    /// burst-on-terrain projectile primitive, which was not on main when this
    /// shipped. A point query against Environment each frame bursts the chord
    /// where it meets a wall or solid terrain.</para>
    /// </summary>
    public partial class MozartRequiemChord : BaseSpecial {

        public const string RequiemCrescendoPerkKey = "requiem_crescendo";
        public const string RestShieldPerkKey = "rest_shield";

        private const float CrescendoRadiusMultiplier = 1.5f;
        private const float CrescendoDamageShare = 0.5f;
        private const float RestShieldChargeSeconds = 1.5f;
        private const float PerkRefreshSeconds = 0.1f;

        /// <summary>M03: the burst radius in pixels (1.2 units).</summary>
        public static float BurstRadiusPixels =>
            (float)KitReachRules.RequiemBurstRadiusUnits * KitMotionRules.StoryPixelsPerUnit;

        private PlaceholderProjectile _chord;
        private Vector2 _burstPosition;
        /// <summary>
        /// R12 (2026-10-04 fix pass): the execution of the cast whose chord this
        /// burst belongs to, stamped on every pulse; 0 = the current execution.
        /// </summary>
        private int _burstSerial;
        private int _pulsesRemaining;
        private int _pulseCountdownFrames;
        private bool _crescendoPending;
        private bool _executionShaved;
        private float _restShieldTimer;

        /// <summary>True once this execution has shaved Fortissimo's cooldown (test seam).</summary>
        public bool ExecutionShavedFortissimo => _executionShaved;
        /// <summary>Pulses still to land from the current burst (test seam).</summary>
        public int PendingPulses => _pulsesRemaining;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _executionShaved = false;
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
                Data?.ProjectileSpeed ?? 840f, Owner.IsFacingRight, new Color(0.6f, 0.4f, 0.8f),
                new Vector2(22, 16), Data?.ProjectileLifetime ?? 3f, contactDamage);
            if (chord == null) return;
            chord.DetonateOnImpact = true;
            // M03: the contact holds without an impulse so the burst lands.
            chord.OverrideContactKnockback(Vector2.Zero);
            chord.Impacted += OnChordImpacted;
            _chord = chord;
        }

        private void OnChordImpacted(Vector2 impactPosition) {
            PlaceholderProjectile chord = _chord;
            _chord = null;
            // M02: a contact that dealt damage is a hit, not a block.
            if (chord != null && IsInstanceValid(chord) && chord.LastImpactDamage > 0f) ShaveFortissimo();
            // R12: the burst belongs to the cast that fired the chord.
            int executionSerial = chord != null && IsInstanceValid(chord) ? chord.SourceExecutionSerial : 0;
            // Area signals fire while the physics space is locked; defer the burst
            // setup one step so the first pulse's shape cast is legal.
            CallDeferred(nameof(BeginBurst), impactPosition, executionSerial);
        }

        /// <summary>
        /// Starts the burst at <paramref name="position"/>: the authored pulses, one
        /// every six frames, credited to execution <paramref name="executionSerial"/>
        /// (R12; 0 = the current execution).
        /// </summary>
        public void BeginBurst(Vector2 position, int executionSerial) {
            if (Owner == null || !IsInstanceValid(Owner)) return;
            _burstPosition = position;
            _burstSerial = executionSerial;
            _pulsesRemaining = Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 1;
            _pulseCountdownFrames = KitReachRules.RequiemPulseIntervalFrames;
            _crescendoPending = Owner.HasStoryPerk(RequiemCrescendoPerkKey);
        }

        /// <summary>
        /// M02: shortens Fortissimo Wave's remaining cooldown by two seconds,
        /// once per Requiem execution (stacking with Resonance Momentum).
        /// </summary>
        public void ShaveFortissimo() {
            if (_executionShaved || Owner == null) return;
            _executionShaved = true;
            Owner.SpecialTwoCooldownTimer = Mathf.Max(
                0f, Owner.SpecialTwoCooldownTimer - (float)KitReachRules.RequiemFortissimoShaveSeconds);
        }

        public override void _PhysicsProcess(double delta) {
            // F5: the cast clock (base) holds through a caster freeze; the
            // chord in flight and its burst pulses are world objects and keep
            // running, as does the Rest Shield's passive charge.
            base._PhysicsProcess(delta);
            TrackChordAgainstTerrain();
            UpdateShockwavePulses();
            UpdateRestShield((float)delta);
        }

        /// <summary>M03: the chord bursts where it meets a wall or solid terrain.</summary>
        private void TrackChordAgainstTerrain() {
            if (_chord == null) return;
            if (!IsInstanceValid(_chord) || !_chord.IsInsideTree() || !_chord.CanProcess()) {
                _chord = null;
                return;
            }
            var space = _chord.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;
            var query = new PhysicsPointQueryParameters2D {
                Position = _chord.GlobalPosition,
                CollisionMask = FTT.Core.CollisionLayers.Environment,
                CollideWithBodies = true,
                CollideWithAreas = false
            };
            if (space.IntersectPoint(query, 1).Count == 0) return;
            Vector2 position = _chord.GlobalPosition;
            PlaceholderProjectile chord = _chord;
            _chord = null;
            int executionSerial = chord.SourceExecutionSerial;
            chord.Impacted -= OnChordImpacted;
            chord.ReturnToPool();
            BeginBurst(position, executionSerial);
        }

        private void UpdateShockwavePulses() {
            if (_pulsesRemaining <= 0 && !_crescendoPending) return;
            if (_pulseCountdownFrames > 0) {
                _pulseCountdownFrames--;
                if (_pulseCountdownFrames > 0) return;
            }

            if (_pulsesRemaining > 0) {
                bool finalPulse = _pulsesRemaining == 1;
                EmitShockwavePulse(BurstRadiusPixels, Data?.BaseDamage ?? 4f, "shockwave", finalPulse);
                _pulsesRemaining--;
                _pulseCountdownFrames = KitReachRules.RequiemPulseIntervalFrames;
                return;
            }

            // Requiem Crescendo (Story-only): the chord detonates a second time
            // with a wider shockwave dealing 50% of the burst total.
            float burstTotal = (Data?.BaseDamage ?? 4f)
                * (Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 1);
            EmitShockwavePulse(
                BurstRadiusPixels * CrescendoRadiusMultiplier,
                Mathf.Round(burstTotal * CrescendoDamageShare),
                "crescendo", finalPulse: true);
            _crescendoPending = false;
        }

        private void EmitShockwavePulse(float radius, float damage, string hitboxID, bool finalPulse) {
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
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;
                StrikeWithPulse(hurtbox, damage, hitboxID, finalPulse);
            }
        }

        /// <summary>One burst pulse against one hurtbox; internal so a test can drive it without a query.</summary>
        internal float StrikeWithPulse(Hurtbox hurtbox, float damage, string hitboxID, bool finalPulse) {
            HitPayload hit = Stamp(new HitPayload {
                AttackerIndex = Owner.PlayerIndex,
                AttackID = Data?.AbilityID ?? "mozart_requiem_chord",
                HitboxID = hitboxID,
                AttackClass = AttackClass.Special,
                Damage = damage * Owner.StorySpecialDamageMultiplier,
                Knockback = finalPulse ? Data?.KnockbackForce ?? new Vector2(3, -2) : Vector2.Zero,
                HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                HitOrigin = _burstPosition,
                AttackerFacingRight = Owner.IsFacingRight,
                AppliedStatus = FTT.Core.StatusType.None,
                StatusDuration = 0f,
                StatusIntensity = 1f,
                ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
            });
            if (!finalPulse) hit.Launches = false;
            hit.SourceExecutionSerial = _burstSerial;
            float dealt = hurtbox.TakeHit(hit);
            Credit(in hit, dealt, hurtbox.GlobalPosition);
            // M02: a pulse that dealt damage is a hit, not a block.
            if (dealt > 0f) ShaveFortissimo();
            return dealt;
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
    /// Special 2 — Fortissimo Wave (M01, Package 13 W7b): Mozart's haymaker,
    /// the deliberate opposite of the fast, small Requiem Chord. A slow wall of
    /// sound 1.5 units tall (the authored <c>HitboxSize</c>) travels straight
    /// along the ground at the authored 4 units/s for 6 units
    /// (<c>ProjectileSpeed</c> × <c>ProjectileLifetime</c>), then dissipates. It
    /// is a ground wave (<see cref="StoryGroundWave"/>) — not a projectile, and
    /// not a lob — but its height reaches anything that has not jumped clear,
    /// so it is not grounded-only. It deals the authored damage with the
    /// roster's heaviest horizontal pushback.
    /// </summary>
    public partial class MozartFortissimoWave : BaseSpecial {

        private const float WaveLeadPixels = 40f;

        private readonly StoryGroundWave _wave = new();

        /// <summary>The live wall of sound (test seam).</summary>
        public StoryGroundWave Wave => _wave;

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
            float speed = Data?.ProjectileSpeed > 0f ? Data.ProjectileSpeed : 240f;
            float lifetime = Data?.ProjectileLifetime > 0f ? Data.ProjectileLifetime : 1.5f;
            _wave.Start(
                Owner.GetWorld2D()?.DirectSpaceState,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? WaveLeadPixels : -WaveLeadPixels, 0f),
                Owner.IsFacingRight, speed, speed * lifetime,
                Data?.HitboxSize ?? new Vector2(30f, 90f), groundedOnly: false);
            SpawnPlaceholderZone(_wave.Front, 0f, lifetime, 1f, new Color(0.8f, 0.6f, 0.9f),
                (Data?.HitboxSize.Y ?? 90f) * 0.5f);
        }

        public override void _PhysicsProcess(double delta) {
            // F5: the cast clock (base) holds through a caster freeze; the
            // wall of sound is a world object and keeps travelling.
            base._PhysicsProcess(delta);
            if (!_wave.Active || Owner == null || !IsInstanceValid(Owner)) return;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            _wave.Advance((float)delta, Owner.GetWorld2D()?.DirectSpaceState,
                StoryShapeQuery.DeliveryMask(targetHurtboxLayer), Owner.PlayerIndex, StrikeWithWave);
        }

        /// <summary>One wall-of-sound contact, delivered once per target.</summary>
        internal void StrikeWithWave(Hurtbox hurtbox, Vector2 front) {
            string abilityID = Data?.AbilityID ?? "mozart_fortissimo_wave";
            HitPayload hit = Stamp(new HitPayload {
                AttackerIndex = Owner.PlayerIndex,
                AttackID = abilityID,
                HitboxID = "ground_wave",
                AttackClass = AttackClass.Special,
                Damage = Mathf.Round((Data?.BaseDamage ?? 24f) * Owner.StorySpecialDamageMultiplier)
                    * Owner.StoryScoped("AbilityDamage", abilityID),
                Knockback = (Data?.KnockbackForce ?? new Vector2(8f, -2f)) * Owner.StoryKnockbackMultiplier,
                HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                HitOrigin = front,
                AttackerFacingRight = _wave.MovingRight,
                AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                StatusDuration = Data?.StatusDuration ?? 0f,
                StatusIntensity = Data?.StatusIntensity ?? 1f,
                ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
            });
            float dealt = hurtbox.TakeHit(hit);
            // The impact VFX lands inside Credit, at the wave's front.
            Credit(in hit, dealt, front);
        }
    }

    /// <summary>
    /// Movement — Sonata Drift (M04, Package 13 W7b): a directable rising
    /// glissando. Mozart rises about 3 units (the authored <c>DistanceMoved</c>)
    /// along the held direction over the authored <c>MovementDuration</c> — up
    /// by default, up-left/up-right or sideways when held, never down — and
    /// leaves a 2.0-unit-wide staff platform under his feet where it ends. The
    /// staff is a one-way platform he (or anyone) can land on; it lasts the
    /// authored 3 seconds. Landing on his own staff refunds half the remaining
    /// cooldown <b>at most once per airtime</b> (reset by landing on real ground
    /// or grabbing a ledge), so he gets at most two drifts before touching down;
    /// the Story Extra Note node obeys the same limit. The Fighter sim runs the
    /// same glissando, staff and refund (D14). Story-only Resonance perk
    /// Virtuoso Dash grants +20% move speed and complete immunity to ranged
    /// projectiles while he stands on a staff.
    /// </summary>
    public partial class MozartSonataDrift : BaseSpecial {

        public const string VirtuosoDashPerkKey = "virtuoso_dash";

        private const float PlatformDropPixels = 6f;
        private const float VirtuosoSpeedMultiplier = 1.2f;
        private const float PerkRefreshSeconds = 0.1f;
        private const float DefaultDistancePixels = 180f;
        private const float DefaultDurationSeconds = 0.3f;

        private Vector2 _glissandoDirection = Vector2.Up;
        private float _glissandoSpeed;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        /// <summary>The latched glissando direction (unit, Godot Y down; test seam).</summary>
        public Vector2 GlissandoDirection => _glissandoDirection;

        /// <summary>
        /// M04: the held direction, 8-way, with any Down component dropped;
        /// neutral rises straight up.
        /// </summary>
        public static Vector2 ResolveGlissandoDirection(float horizontal, float vertical) {
            int x = horizontal > 0.3f ? 1 : horizontal < -0.3f ? -1 : 0;
            int y = vertical < -0.3f ? -1 : 0;
            if (x == 0 && y == 0) y = -1;
            return new Vector2(x, y).Normalized();
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _glissandoDirection = ResolveGlissandoDirection(
                Owner.CurrentInputFrame.Horizontal, Owner.CurrentInputFrame.Vertical);
            float distance = MovementData?.DistanceMoved > 0f ? MovementData.DistanceMoved : DefaultDistancePixels;
            float duration = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : DefaultDurationSeconds;
            _glissandoSpeed = distance / duration;
            // The opening beat holds him in place.
            Owner.Velocity = Vector2.Zero;
        }

        protected override void OnActive() {
            PhaseTimer = MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : DefaultDurationSeconds;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            Owner.Velocity = Vector2.Zero;
            // The staff is placed under his feet where the glissando ends.
            DeployPlatform();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Sonata Drift",
                StartPosition = Owner.GlobalPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        protected override void OnInterrupted() {
            if (Owner != null && IsInstanceValid(Owner)) Owner.Velocity = Vector2.Zero;
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
            // F5: the glissando holds with the frozen caster; the staff perks
            // and the charge bookkeeping below are not the cast's and keep running.
            if (CurrentPhase == AbilityPhase.Active && Owner != null && !CastClockSuspended) {
                Owner.Velocity = _glissandoDirection * _glissandoSpeed;
            }
            base._PhysicsProcess(delta);
            UpdateVirtuosoDash();
            UpdateCharges();
        }

        // === Package 12 W4: Sonata Drift charges; Package 13 W7b: the refund ==
        //
        // Story-only charges. The owner's MovementAbilityCooldownTimer stays the
        // ONE recharge clock: it times the charge currently recharging. A charge
        // that is already restored but not yet spent is "banked" here. So the
        // charges available are (timer idle ? 1 : 0) + banked, and Extra Note
        // (design §5, Low-item decision 2026-09-26) raises the cap to two:
        //   - a cast from a full stock arms the timer through the ordinary path;
        //   - a cast while the timer runs spends a banked charge and never
        //     restarts the running recharge (TryExecute(armCooldown: false));
        //   - when a recharge completes and the stock is still short, the next
        //     charge's recharge starts at once — one at a time, on the 5 s
        //     cooldown;
        //   - landing on one of his own staff platforms halves the recharging
        //     charge's REMAINING time, at most once per airtime (M04, baseline
        //     kit rule in both modes; Extra Note obeys the same limit).

        /// <summary>Remaining-time share a staff landing refunds (half).</summary>
        public const float StaffLandingRefundShare = (float)KitReachRules.SonataStaffRefundShare;

        private int _bankedCharges;
        private bool _chargesInitialized;
        private bool _wasRecharging;
        private bool _wasOnOwnStaff;
        private bool _refundUsedThisAirtime;

        /// <summary>True once a staff landing has refunded this airtime (test seam).</summary>
        public bool RefundUsedThisAirtime => _refundUsedThisAirtime;

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

        /// <summary>
        /// M04: the once-per-airtime staff refund. A fresh landing on his own
        /// staff halves the remaining cooldown unless this airtime already
        /// refunded; real ground or a ledge grab re-arms it. Returns true when a
        /// refund was granted. Public so the rule is testable without physics.
        /// </summary>
        public bool ResolveStaffRefund(bool landedOnOwnStaff, bool onRealGroundOrLedge) {
            if (onRealGroundOrLedge && !landedOnOwnStaff) {
                _refundUsedThisAirtime = false;
                return false;
            }
            if (!landedOnOwnStaff || _refundUsedThisAirtime || Owner == null) return false;
            _refundUsedThisAirtime = true;
            if (Owner.MovementAbilityCooldownTimer > 0f) {
                Owner.MovementAbilityCooldownTimer *= 1f - StaffLandingRefundShare;
            }
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
            bool realGround = !onStaff && Owner.IsOnFloor();
            bool ledge = Owner.CurrentState == CharacterState.LedgeHanging;
            ResolveStaffRefund(onStaff && !_wasOnOwnStaff, realGround || ledge);
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
            // D15 (Package 13 W6): the HitCount falling keys, then the grand-chord finale.
            _strikesRemaining = Data?.IsMultiHit == true ? Data.CinematicHitCount : 1;
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
            // F5: the strikes are paced by the cast's active window, so they
            // hold with the frozen caster (the phase clock does too).
            if (!CastClockSuspended) UpdateMeteorStrikes();
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
                CollisionMask = StoryShapeQuery.DeliveryMask(targetHurtboxLayer)
            };

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                HitPayload hit = Stamp(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "mozart_symphony_of_sorrow",
                    HitboxID = finalStrike ? "meteor_final" : "meteor",
                    AttackClass = AttackClass.Ultimate,
                    Damage = Mathf.Round(Data?.CinematicHitDamage(strikeIndex + 1) ?? 8f) * Owner.StorySpecialDamageMultiplier,
                    // Only the closing strike launches; earlier keys pin the
                    // target inside the bombardment.
                    Knockback = finalStrike ? Data?.KnockbackForce ?? new Vector2(5, -4) : Vector2.Zero,
                    HitstunDuration = (Data?.IsFinaleHit(strikeIndex + 1) ?? false)
                        ? UltimateActivationRules.FinaleHitstunSeconds
                        : Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = strikePosition,
                    AttackerFacingRight = _stormFacingRight,
                    AppliedStatus = (Data?.CinematicHitCarriesStatus(strikeIndex + 1) ?? true)
                        ? Data?.AppliedStatus ?? FTT.Core.StatusType.None
                        : FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.4f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.2f
                });
                float dealt = hurtbox.TakeHit(hit);
                // V7.6 D03h (Package 11 A1b): Ultimate-origin damage awards its caster
                // ZERO damage-dealt meter, regardless of HP removed, target count or
                // when it lands. Direct-hit Rally reclaim is retained (D03g).
                Credit(in hit, dealt, hurtbox.GlobalPosition);
            }
        }
    }
}
