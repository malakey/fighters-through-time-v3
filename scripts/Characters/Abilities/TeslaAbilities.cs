using Godot;
using FTT.Combat;
using FTT.Characters;

namespace FTT.Characters.Abilities {

    /// <summary>
    /// Special 1 — Tesla Coil: deploys a persistent coil construct (25 HP, 30 s,
    /// max 2 active). Coils arc at nearby enemies and link into an
    /// alternating-current fence when placed within 8 world units of each other.
    /// Story-only Resonance perks: Resonant Overdrive (+5 s lifetime, 25% faster
    /// arcs) and Wardenclyffe Shield (recharging shield worth 15% of max HP while
    /// standing near an active coil).
    /// </summary>
    public partial class TeslaTeslaCoil : BaseSpecial {

        public const string ResonantOverdrivePerkKey = "resonant_overdrive";
        public const string WardenclyffeShieldPerkKey = "wardenclyffe_shield";

        private const float ShieldNearCoilRangePixels = 240f;
        private const float ShieldRechargePerSecond = 2f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            DeployCoil();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void DeployCoil() {
            if (Owner == null) return;
            if (Data?.PersistentObjectScene == null) {
                GD.PushWarning("Tesla Coil has no PersistentObjectScene authored; deploy skipped.");
                return;
            }

            int maxActive = Data.MaxActiveObjects > 0 ? Data.MaxActiveObjects : 2;
            while (CountActiveCoils() >= maxActive) {
                TeslaCoilNode oldest = FindOldestCoil();
                if (oldest == null) break;
                Owner.ActivePersistentObjects.Remove(oldest);
                oldest.ReturnToPool();
            }

            Node spawned = FTT.Core.PoolManager.Instance?.Spawn(
                Data.PersistentObjectScene,
                Owner.GlobalPosition + new Vector2(Owner.IsFacingRight ? 70f : -70f, 0f),
                Owner.GetParent());
            if (spawned is not TeslaCoilNode coil) return;

            coil.Initialize(Data, Owner, Owner.HasStoryPerk(ResonantOverdrivePerkKey));
            Owner.ActivePersistentObjects.Add(coil);
            TryLinkCoils(coil);
        }

        private void TryLinkCoils(TeslaCoilNode newest) {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is TeslaCoilNode other && other != newest && !other.IsCoilDestroyed
                    && newest.GlobalPosition.DistanceTo(other.GlobalPosition)
                        <= TeslaCoilNode.LinkRangePixels * Owner.StoryPersistentRangeMultiplier) {
                    // The newest coil drives the fence tick so exactly one member
                    // of the pair applies fence damage.
                    newest.LinkPartner(other, drivesFence: true);
                    other.LinkPartner(newest, drivesFence: false);
                    return;
                }
            }
        }

        private int CountActiveCoils() {
            int count = 0;
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is TeslaCoilNode coil && IsInstanceValid(coil) && !coil.IsCoilDestroyed) count++;
            }
            return count;
        }

        private TeslaCoilNode FindOldestCoil() {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is TeslaCoilNode coil && IsInstanceValid(coil) && !coil.IsCoilDestroyed) return coil;
            }
            return null;
        }

        public override void _PhysicsProcess(double delta) {
            base._PhysicsProcess(delta);
            UpdateWardenclyffeShield((float)delta);
        }

        /// <summary>
        /// Wardenclyffe Shield (Story-only): while Tesla stands near an active
        /// coil, a slow-recharging electromagnetic shield absorbs up to 15% of his
        /// max HP in damage. Recharge rate is a placeholder-tuning choice; the
        /// design specifies "slow-recharging" without a number.
        /// </summary>
        private void UpdateWardenclyffeShield(float dt) {
            if (Owner == null || !Owner.HasStoryPerk(WardenclyffeShieldPerkKey)) return;
            Owner.ConfigureStoryShield(0.15f * Owner.MaximumHP);
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is TeslaCoilNode coil && IsInstanceValid(coil) && !coil.IsCoilDestroyed
                    && Owner.GlobalPosition.DistanceTo(coil.GlobalPosition) <= ShieldNearCoilRangePixels) {
                    Owner.RechargeStoryShield(ShieldRechargePerSecond * dt);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Special 2 — Lorentz Pulse: a radial electromagnetic burst around Tesla that
    /// damages and Roots enemies for the authored duration. Targets carrying Tesla's
    /// V7.6 F07 Conductive MARK (not Static Charge — the split is the point of F07)
    /// take an additional chain lightning strike per active Tesla Coil. Because the
    /// mark occupies no status slot, the pulse's own Root can never evict it.
    /// Story-only Resonance perk Lorentz
    /// Attraction pulls enemies toward Tesla before rooting them and extends the
    /// root by one second.
    /// </summary>
    public partial class TeslaLorentzPulse : BaseSpecial {

        public const string LorentzAttractionPerkKey = "lorentz_attraction";

        private const float PulseRadiusPixels = 140f;
        private const float AttractionStopDistancePixels = 70f;
        private const float AttractionRootBonusSeconds = 1f;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            EmitPulse();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        private void EmitPulse() {
            if (Owner == null) return;
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return;

            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = PulseRadiusPixels },
                Transform = new Transform2D(0f, Owner.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };

            bool attraction = Owner.HasStoryPerk(LorentzAttractionPerkKey);
            // Story-only StatusDuration minors lengthen the pulse's Root.
            float rootDuration = ((Data?.StatusDuration > 0f ? Data.StatusDuration : 2f)
                + (attraction ? AttractionRootBonusSeconds : 0f))
                * Owner.StoryStatusDurationMultiplier;

            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is not Hurtbox hurtbox) continue;
                if (hurtbox.OwnerPlayerIndex == Owner.PlayerIndex) continue;

                // V7.6 F07: chain eligibility is the separate Conductive MARK,
                // owned by this Tesla — Static Charge is now a pure interrupt and
                // is never consulted here.
                bool primed = TargetHasConductiveMark(hurtbox, Owner.PlayerIndex);
                if (attraction) PullTargetTowardOwner(hurtbox);

                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "tesla_lorentz_pulse",
                    HitboxID = "pulse",
                    AttackClass = AttackClass.Special,
                    Damage = (Data?.BaseDamage ?? 12f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = Vector2.Zero,
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.Root,
                    StatusDuration = rootDuration,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.2f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.15f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);

                if (primed) ChainLightningToCoils(hurtbox);
            }
        }

        private void ChainLightningToCoils(Hurtbox target) {
            foreach (Node2D node in Owner.ActivePersistentObjects) {
                if (node is not TeslaCoilNode coil || !IsInstanceValid(coil) || coil.IsCoilDestroyed) continue;
                float dealt = target.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "tesla_lorentz_pulse",
                    HitboxID = "chain_lightning",
                    AttackClass = AttackClass.Special,
                    Damage = coil.ArcDamage * Owner.StorySpecialDamageMultiplier,
                    Knockback = Vector2.Zero,
                    HitstunDuration = 0.1f,
                    HitOrigin = coil.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = FTT.Core.StatusType.None,
                    StatusDuration = 0f,
                    StatusIntensity = 1f,
                    ScreenShakeIntensity = 0.1f,
                    ScreenShakeDuration = 0.05f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        private void PullTargetTowardOwner(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) {
                    Vector2 toOwner = Owner.GlobalPosition - body.GlobalPosition;
                    float distance = toOwner.Length();
                    if (distance > AttractionStopDistancePixels) {
                        Vector2 pulled = Owner.GlobalPosition
                            - toOwner.Normalized() * AttractionStopDistancePixels;
                        body.GlobalPosition = pulled;
                    }
                    return;
                }
                current = current.GetParent();
            }
        }

        /// <summary>
        /// V7.6 F07: the chain gate. Reads the caster-owned Conductive mark —
        /// which carries no action lock and contributes zero stagger budget — so
        /// Lorentz chains no longer depend on the target being action-locked.
        /// The mark must belong to THIS Tesla.
        /// </summary>
        private static bool TargetHasConductiveMark(Hurtbox hurtbox, int sourcePlayerIndex) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is PlayerController player) {
                    return player.HasConductiveMarkFrom(sourcePlayerIndex);
                }
                if (current is FTT.Enemies.EnemyController enemy) {
                    return enemy.HasConductiveMarkFrom(sourcePlayerIndex);
                }
                if (current is FTT.Enemies.BossController boss) {
                    return boss.HasConductiveMarkFrom(sourcePlayerIndex);
                }
                current = current.GetParent();
            }
            return false;
        }
    }

    /// <summary>
    /// Movement — Lightning Blink: Tesla becomes pure current and blinks a short
    /// distance in the held input direction, usable in the air for recovery.
    /// Distance, duration (capped at the design's 1 s limit), and cooldown come
    /// from the authored MovementAbilityData resource.
    /// </summary>
    public partial class TeslaLightningBlink : BaseSpecial {

        private const float MaxBlinkDuration = 1.0f;

        private Vector2 _blinkDirection;
        private Vector2 _startPosition;
        private float _blinkDuration = 0.2f;
        private float _blinkDistance = 160f;

        private MovementAbilityData MovementData => Data as MovementAbilityData;

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _blinkDuration = Mathf.Min(
                MovementData?.MovementDuration > 0f ? MovementData.MovementDuration : 0.2f,
                MaxBlinkDuration);
            _blinkDistance = MovementData?.DistanceMoved > 0f ? MovementData.DistanceMoved : 160f;

            float hInput = Owner.CurrentInputFrame.Horizontal;
            // Negative vertical aims upward (Godot 2D Y is down); since §2.7 it
            // comes from the Up input rather than a held Jump.
            float vInput = Owner.CurrentInputFrame.Vertical;
            _blinkDirection = new Vector2(hInput, vInput);
            if (_blinkDirection == Vector2.Zero) {
                _blinkDirection = Owner.IsFacingRight ? Vector2.Right : Vector2.Left;
            }
            _blinkDirection = _blinkDirection.Normalized();
            _startPosition = Owner.GlobalPosition;
        }

        protected override void OnActive() {
            PhaseTimer = _blinkDuration;
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
            FTT.Core.EventBus.Instance?.RaiseMovementAbilityUsed(new FTT.Core.MovementAbilityPayload {
                PlayerIndex = Owner.PlayerIndex,
                AbilityName = Data?.AbilityName ?? "Lightning Blink",
                StartPosition = _startPosition,
                EndPosition = Owner.GlobalPosition
            });
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                float speed = _blinkDistance / _blinkDuration;
                Owner.Velocity = _blinkDirection * speed;
            }
            base._PhysicsProcess(delta);
        }
    }

    /// <summary>
    /// Ultimate — Wardenclyffe Cataclysm (design Section 5): the spectral tower
    /// draws all enemies toward Tesla, strikes them with a massive multi-hit
    /// column of alternating current, and detonates every active Tesla Coil in a
    /// chain reaction. Per-hit damage, hit count, tick interval, and phase frames
    /// are the authored ultimate.tres numbers; each hit tick drags enemies toward
    /// Tesla (Lorentz Attraction-style positional pull) before the column
    /// strikes. Ultimate-class hits bypass shields and hyper-armor by the shared
    /// combat rules. The pull/column radii are placeholder tuning — the design
    /// gives no pixel numbers for either.
    /// </summary>
    public partial class TeslaWardenclyffeCataclysm : BaseSpecial {
        private const float PullRadiusPixels = 1200f;
        private const float ColumnRadiusPixels = 150f;
        private const float PullStopDistancePixels = 70f;
        private const float PullStepPixels = 120f;
        private const int FallbackTickIntervalFrames = 30;

        private int _framesInActive;
        private int _hitsDone;
        private UltimateMeter _meter;

        public override void _Ready() {
            base._Ready();
            _meter = Owner?.GetNodeOrNull<UltimateMeter>("UltimateMeter");
        }

        protected override bool Validate() {
            return base.Validate() && (_meter?.IsFull ?? false);
        }

        protected override void OnStartup() {
            UseAuthoredPhaseFrames();
            _framesInActive = 0;
            _hitsDone = 0;
            Owner.Velocity = Vector2.Zero;
            _meter?.Consume();
        }

        protected override void OnActive() {
            UseAuthoredPhaseFrames();
            ExplodeAllCoils();
        }

        protected override void OnRecovery() {
            UseAuthoredPhaseFrames();
        }

        public override void _PhysicsProcess(double delta) {
            if (CurrentPhase == AbilityPhase.Active) {
                _framesInActive++;
                int interval = Data?.DamageTickIntervalFrames > 0
                    ? Data.DamageTickIntervalFrames
                    : FallbackTickIntervalFrames;
                int hitCount = Data?.IsMultiHit == true ? Mathf.Max(1, Data.HitCount) : 1;
                if (_hitsDone < hitCount && _framesInActive % interval == 0) {
                    _hitsDone++;
                    DealColumnHit();
                }
            }
            base._PhysicsProcess(delta);
        }

        private void ExplodeAllCoils() {
            var constructs = new System.Collections.Generic.List<Node2D>(Owner.ActivePersistentObjects);
            foreach (Node2D node in constructs) {
                if (node is TeslaCoilNode coil && IsInstanceValid(coil) && !coil.IsCoilDestroyed) {
                    coil.Explode();
                }
            }
        }

        /// <summary>
        /// One column strike: every enemy in the (stage-wide) pull radius is
        /// dragged toward Tesla, then everyone inside the column takes one
        /// authored per-hit ultimate strike.
        /// </summary>
        private void DealColumnHit() {
            if (Owner == null) return;
            foreach (Hurtbox hurtbox in QueryEnemyHurtboxes(PullRadiusPixels)) {
                DragTowardOwner(hurtbox);
                if (Owner.GlobalPosition.DistanceTo(hurtbox.GlobalPosition) > ColumnRadiusPixels) continue;
                float dealt = hurtbox.TakeHit(new HitPayload {
                    AttackerIndex = Owner.PlayerIndex,
                    AttackID = Data?.AbilityID ?? "tesla_wardenclyffe_cataclysm",
                    HitboxID = "cataclysm_column",
                    AttackClass = AttackClass.Ultimate,
                    Damage = (Data?.BaseDamage ?? 18f) * Owner.StorySpecialDamageMultiplier,
                    Knockback = Data?.KnockbackForce ?? new Vector2(0f, -6f),
                    HitstunDuration = Data?.HitstunDuration ?? 0.2f,
                    HitOrigin = Owner.GlobalPosition,
                    AttackerFacingRight = Owner.IsFacingRight,
                    AppliedStatus = Data?.AppliedStatus ?? FTT.Core.StatusType.None,
                    StatusDuration = Data?.StatusDuration ?? 0f,
                    StatusIntensity = Data?.StatusIntensity ?? 1f,
                    ScreenShakeIntensity = Data?.ScreenShakeIntensity ?? 0.6f,
                    ScreenShakeDuration = Data?.ScreenShakeDuration ?? 0.3f
                });
                if (dealt > 0f) Owner.AddInfluenceFromDamageDealt(dealt);
            }
        }

        /// <summary>
        /// Positional drag toward Tesla (the Lorentz Attraction pull pattern),
        /// stepped per hit tick instead of teleporting the full distance.
        /// </summary>
        private void DragTowardOwner(Hurtbox hurtbox) {
            Node current = hurtbox.GetParent();
            while (current != null) {
                if (current is CharacterBody2D body) {
                    Vector2 toOwner = Owner.GlobalPosition - body.GlobalPosition;
                    float distance = toOwner.Length();
                    if (distance > PullStopDistancePixels) {
                        float step = Mathf.Min(PullStepPixels, distance - PullStopDistancePixels);
                        body.GlobalPosition += toOwner.Normalized() * step;
                    }
                    return;
                }
                current = current.GetParent();
            }
        }

        private System.Collections.Generic.List<Hurtbox> QueryEnemyHurtboxes(float radius) {
            var results = new System.Collections.Generic.List<Hurtbox>();
            var space = Owner.GetWorld2D()?.DirectSpaceState;
            if (space == null) return results;
            uint targetHurtboxLayer = Owner.PlayerIndex == 0
                ? FTT.Core.CollisionLayers.EnemyHurtbox
                : FTT.Core.CollisionLayers.PlayerHurtbox;
            var query = new PhysicsShapeQueryParameters2D {
                Shape = new CircleShape2D { Radius = radius },
                Transform = new Transform2D(0f, Owner.GlobalPosition),
                CollideWithAreas = true,
                CollideWithBodies = false,
                CollisionMask = targetHurtboxLayer
            };
            foreach (Godot.Collections.Dictionary result in space.IntersectShape(query, 16)) {
                if (result["collider"].AsGodotObject() is Hurtbox hurtbox
                    && hurtbox.OwnerPlayerIndex != Owner.PlayerIndex) {
                    results.Add(hurtbox);
                }
            }
            return results;
        }
    }
}
