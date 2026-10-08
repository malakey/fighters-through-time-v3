using Godot;
using System;
using FTT.Core;

namespace FTT.Enemies {

    public enum EnemyAbilityPhase {
        Idle,
        Telegraph,
        Active,
        Recovery
    }

    /// <summary>
    /// Shared telegraph -> active -> recovery driver used by BOTH
    /// <see cref="EnemyController"/> elite abilities and every
    /// <see cref="BossController"/> attack. Frames advance once per physics tick
    /// (60 Hz); the caller applies <see cref="DashVelocity"/> to its body while the
    /// active phase of a ChargeDash runs. Story-only: nothing here is deterministic
    /// simulation state and none of it reaches scripts/FighterSim.
    /// </summary>
    public sealed class EnemyAbilityExecutor {
        public const string ProjectilePoolID = "enemy_projectile";
        public const string ProjectileScenePath = "res://scenes/enemies/EnemyProjectile.tscn";
        private const int ProjectilePoolWarmUp = 8;
        private const int ProjectilePoolCapacity = 40;

        private readonly Node2D _owner;
        private AnimatedSprite2D _sprite;
        private FTT.Combat.Hitbox _hitbox;
        private CollisionShape2D _hitboxShape;
        private Node2D _abilityOrigin;

        private bool _facingRight = true;
        private Vector2 _targetPosition;
        private Color _spriteBaseModulate = Colors.White;
        private bool _tintApplied;

        public EnemyAbilityExecutor(Node2D owner) {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Owning enemy/boss ID, echoed on presentation events.</summary>
        public string SourceID { get; set; } = "";

        /// <summary>Story difficulty damage scaling applied to every hitbox/projectile.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Seeded so tests and replays get repeatable spread/teleport rolls.</summary>
        public Random Rng { get; set; } = new(20260807);

        public EnemyAbilityPhase Phase { get; private set; } = EnemyAbilityPhase.Idle;
        public EnemyAbilityData ActiveAbility { get; private set; }
        public int FramesRemainingInPhase { get; private set; }
        public bool IsBusy => Phase != EnemyAbilityPhase.Idle;
        public bool IsTelegraphing => Phase == EnemyAbilityPhase.Telegraph;

        /// <summary>Horizontal velocity a ChargeDash wants applied during its active phase.</summary>
        public Vector2 DashVelocity { get; private set; }

        /// <summary>V7.2: the active ability is Guard-Crush (2 charges, orange telegraph).</summary>
        public bool ActiveGuardCrush { get; private set; }

        /// <summary>V7.2: the active ability is unblockable (boss-only, red telegraph).</summary>
        public bool ActiveUnblockable { get; private set; }

        public float ShieldDamageReduction { get; private set; }
        public float ShieldSecondsRemaining { get; private set; }
        public bool HasActiveShield => ShieldSecondsRemaining > 0f && ShieldDamageReduction > 0f;

        /// <summary>Raised the frame an ability's active phase begins.</summary>
        public event Action<EnemyAbilityData> AbilityActivated;

        /// <summary>
        /// Package 12 W9 (GAP-07): the bodies one SummonMinions cast actually put
        /// on the field, so an owner can treat them as a guarded scene (the
        /// Tragedy King's actors). Raised once per cast, possibly with an empty list.
        /// </summary>
        public event Action<EnemyAbilityData, System.Collections.Generic.IReadOnlyList<EnemyController>> MinionsSummoned;

        /// <summary>
        /// Package 12 W9 (GAP-07): a Teleport cast moved the owner; the argument is
        /// the position it vacated (the Jackal Priest leaves a decoy there).
        /// </summary>
        public event Action<EnemyAbilityData, Vector2> Teleported;

        public void Bind(AnimatedSprite2D sprite, FTT.Combat.Hitbox hitbox, Node2D abilityOrigin) {
            _sprite = sprite;
            _hitbox = hitbox;
            _abilityOrigin = abilityOrigin;
            _spriteBaseModulate = sprite?.Modulate ?? Colors.White;
            _hitboxShape = hitbox?.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (_hitboxShape == null && hitbox != null) {
                Godot.Collections.Array<Node> children = hitbox.GetChildren();
                using var childrenLifetime = children.AsDisposable();
                foreach (Node child in children) {
                    if (child is CollisionShape2D found) { _hitboxShape = found; break; }
                }
            }
            // Scene sub-resources are shared between instances by default; a local
            // copy lets every enemy resize its own hitbox per ability.
            if (_hitboxShape?.Shape is RectangleShape2D shared) {
                _hitboxShape.Shape = (Shape2D)shared.Duplicate();
            } else if (_hitboxShape != null && _hitboxShape.Shape == null) {
                _hitboxShape.Shape = new RectangleShape2D { Size = new Vector2(48f, 48f) };
            }
            _unclippedHitboxSize = Vector2.Zero;
        }

        /// <summary>
        /// The owner's outline/glow arbiter when one is attached. While set, the
        /// executor stops writing <c>Modulate</c> directly and drives the arbiter's
        /// tint-override channel instead (Package 8 A3).
        /// </summary>
        public FTT.Combat.GlowPresentationController Glow { get; set; }

        /// <summary>Records the sprite tint the telegraph restores to.</summary>
        public void SetBaseModulate(Color modulate) {
            _spriteBaseModulate = modulate;
            if (Glow != null) { Glow.SetBaseTint(modulate); return; }
            if (!_tintApplied && _sprite != null) _sprite.Modulate = modulate;
        }

        /// <summary>
        /// V7.2 classification is passed by the caller: EnemyController marks
        /// elite signature abilities Guard-Crush; BossController forwards the
        /// ability's authored IsGuardCrushing/IsUnblockable flags. Unblockable
        /// is boss-only by contract — mobs must always pass false.
        /// </summary>
        public bool Begin(
            EnemyAbilityData ability,
            Vector2 targetPosition,
            bool facingRight,
            bool guardCrush = false,
            bool unblockable = false) {
            if (ability == null) return false;
            ClearActiveEffects();
            ActiveAbility = ability;
            ActiveGuardCrush = guardCrush && !unblockable;
            ActiveUnblockable = unblockable;
            _facingRight = facingRight;
            _targetPosition = targetPosition;
            // P3: a telegraph promised to Cleopatra's sand decoy stays on the sand
            // at fire time (GAP-10c) — see ResolveFireAim.
            _telegraphedAtLure = HonoursSandDecoy
                && FTT.Characters.Abilities.SandDecoyNode.IsLureAt(targetPosition);
            Phase = EnemyAbilityPhase.Telegraph;
            FramesRemainingInPhase = Math.Max(0, ability.TelegraphFrames);
            ApplyTelegraphTint();
            RaisePresentation(EnemyPresentationPhase.Telegraph);
            if (FramesRemainingInPhase <= 0) EnterActive();
            return true;
        }

        /// <summary>One 60 Hz step. Delta only drives the shield timer.</summary>
        public void Tick(float delta) {
            if (ShieldSecondsRemaining > 0f) {
                ShieldSecondsRemaining -= delta;
                if (ShieldSecondsRemaining <= 0f) {
                    ShieldSecondsRemaining = 0f;
                    ShieldDamageReduction = 0f;
                }
            }
            if (Phase == EnemyAbilityPhase.Idle) return;

            // V7.6 F14: a Siphon Snare's active phase IS the channel. It ends the
            // moment the tether breaks — range, cover, a roll, an interrupt, an
            // emptied meter — rather than burning the authored 180 frames with
            // nothing attached.
            if (Phase == EnemyAbilityPhase.Active
                && ActiveAbility?.Archetype == EnemyAbilityArchetype.SiphonTether
                && !IsChannelling) {
                _tether = null;
                EnterRecovery();
                return;
            }

            // P2: a moving owner (a charge) re-clips its forward hitbox each frame.
            if (Phase == EnemyAbilityPhase.Active) RefreshWallClip();

            FramesRemainingInPhase--;
            if (FramesRemainingInPhase > 0) return;
            switch (Phase) {
                case EnemyAbilityPhase.Telegraph: EnterActive(); break;
                case EnemyAbilityPhase.Active: EnterRecovery(); break;
                default: EnterIdle(); break;
            }
        }

        /// <summary>Interruption during telegraph: skip the payload, keep the recovery cost.</summary>
        public void CancelIntoRecovery() {
            if (Phase == EnemyAbilityPhase.Idle) return;
            RestoreTint();
            _hitbox?.Deactivate();
            DashVelocity = Vector2.Zero;
            ReleaseSiphonTether();
            int recovery = Math.Max(1, ActiveAbility?.RecoveryFrames ?? 1);
            Phase = EnemyAbilityPhase.Recovery;
            FramesRemainingInPhase = recovery;
        }

        /// <summary>Hard stop, used by freeze/despawn/death.</summary>
        public void Cancel() {
            ClearActiveEffects();
            Phase = EnemyAbilityPhase.Idle;
            ActiveAbility = null;
            FramesRemainingInPhase = 0;
        }

        /// <summary>Full reset for pool reuse: also drops any active shield.</summary>
        public void Reset() {
            Cancel();
            ShieldDamageReduction = 0f;
            ShieldSecondsRemaining = 0f;
            WallProbeRaysCast = 0;
        }

        private void ClearActiveEffects() {
            RestoreTint();
            _hitbox?.Deactivate();
            _wallClipped = false;
            _wallClipFollowsOwner = false;
            DashVelocity = Vector2.Zero;
            // A hard stop takes the tether with it: freeze teardown, despawn and
            // death must never leave a channel draining a player nobody owns.
            ReleaseSiphonTether();
        }

        private void EnterActive() {
            RestoreTint();
            Phase = EnemyAbilityPhase.Active;
            FramesRemainingInPhase = ActiveAbility?.ResolvedActiveFrames ?? 1;
            ExecuteArchetype();
            // A Siphon cast that missed, was blocked or was refused creates no
            // lingering pulse: it goes straight to its recovery instead of holding
            // the caster still for a channel that never attached.
            if (ActiveAbility?.Archetype == EnemyAbilityArchetype.SiphonTether && !IsChannelling) {
                FramesRemainingInPhase = 1;
            }
            RaisePresentation(EnemyPresentationPhase.Active);
            AbilityActivated?.Invoke(ActiveAbility);
        }

        private void EnterRecovery() {
            _hitbox?.Deactivate();
            _wallClipped = false;
            _wallClipFollowsOwner = false;
            DashVelocity = Vector2.Zero;
            int recovery = Math.Max(0, ActiveAbility?.RecoveryFrames ?? 0);
            RaisePresentation(EnemyPresentationPhase.Recovery);
            if (recovery <= 0) {
                EnterIdle();
                return;
            }
            Phase = EnemyAbilityPhase.Recovery;
            FramesRemainingInPhase = recovery;
        }

        private void EnterIdle() {
            Phase = EnemyAbilityPhase.Idle;
            ActiveAbility = null;
            FramesRemainingInPhase = 0;
            DashVelocity = Vector2.Zero;
        }

        // === Archetype execution ===

        private void ExecuteArchetype() {
            EnemyAbilityData ability = ActiveAbility;
            if (ability == null) return;
            switch (ability.Archetype) {
                case EnemyAbilityArchetype.MeleeStrike:
                    ActivateHitbox(ability, ability.HitboxSize, ability.HitboxOffset, mirrored: true);
                    break;
                case EnemyAbilityArchetype.ChargeDash:
                    DashVelocity = new Vector2((_facingRight ? 1f : -1f) * Mathf.Abs(ability.DashSpeed), 0f);
                    ActivateHitbox(ability, ability.HitboxSize, ability.HitboxOffset, mirrored: true);
                    break;
                case EnemyAbilityArchetype.AreaPulse:
                    float diameter = Mathf.Max(8f, ability.PulseRadius * 2f);
                    ActivateHitbox(ability, new Vector2(diameter, diameter),
                        new Vector2(0f, ability.HitboxOffset.Y), mirrored: false);
                    break;
                case EnemyAbilityArchetype.Projectile:
                    SpawnProjectiles(ability, lockVertical: false);
                    break;
                case EnemyAbilityArchetype.Shockwave:
                    SpawnProjectiles(ability, lockVertical: true);
                    break;
                case EnemyAbilityArchetype.ShieldBubble:
                    ShieldDamageReduction = Mathf.Clamp(ability.ShieldDamageReduction, 0f, 1f);
                    ShieldSecondsRemaining = Mathf.Max(0f, ability.ShieldDuration);
                    break;
                case EnemyAbilityArchetype.SummonMinions:
                    SummonMinions(ability);
                    break;
                case EnemyAbilityArchetype.Teleport:
                    Teleport(ability);
                    break;
                case EnemyAbilityArchetype.PersistentFieldAtTarget:
                    SpawnPersistentField(ability);
                    break;
                case EnemyAbilityArchetype.SiphonTether:
                    BeginSiphonTether(ability);
                    break;
            }
        }

        // === V7.6 F14: the Siphon Snare's single attachment check =============

        private SiphonTetherChannel _tether;

        /// <summary>
        /// True while a <see cref="EnemyAbilityArchetype.SiphonTether"/> is
        /// maintaining. <see cref="EnemyController"/> reads this to hold the
        /// caster stationary and mute — the contract's "the Eraser is stationary
        /// and cannot attack or use the Null Lance while maintaining".
        /// </summary>
        public bool IsChannelling =>
            _tether != null && GodotObject.IsInstanceValid(_tether) && _tether.IsLive;

        /// <summary>The live tether, or null. Test seam.</summary>
        public SiphonTetherChannel ActiveTether =>
            _tether != null && GodotObject.IsInstanceValid(_tether) ? _tether : null;

        /// <summary>
        /// Outcome of the most recent Siphon cast's attachment check, so a miss, a
        /// blocked cast and a refusal stay distinguishable without inspecting the
        /// (absent) channel node.
        /// </summary>
        public SiphonAttachResult LastSiphonResult { get; private set; } = SiphonAttachResult.None;

        /// <summary>
        /// Deterministic tiebreak identity handed to a contested tether. Defaults
        /// to the owner node's name; settable so an authored encounter (or a test)
        /// can pin which of two simultaneous Erasers keeps the tether.
        /// </summary>
        public string SourceStableID { get; set; } = "";

        /// <summary>
        /// The ONE attachment check, run at active-start. Every refusal the
        /// contract names happens here and nowhere else: after this frame the
        /// tether either exists or the cast is over. The cooldown belongs to the
        /// caller and runs either way — there is no early refund.
        /// </summary>
        private void BeginSiphonTether(EnemyAbilityData ability) {
            _tether = null;
            LastSiphonResult = SiphonAttachResult.None;
            Node parent = _owner.GetParent();
            if (parent == null) return;

            float radius = Mathf.Max(8f, ability.PulseRadius);
            FTT.Characters.PlayerController target = NearestEligibleTarget(radius);
            if (target == null) { LastSiphonResult = SiphonAttachResult.NoTarget; return; }

            // A legal grounded, front-facing block at attachment prevents ALL
            // drain for one charge, with the ordinary Basic block response. It is
            // not a hit: no HP chip, no hitstun, no knockback, no lingering pulse.
            FTT.Combat.BlockResult blocked = target.TryAbsorbNonDamagingCast(
                _owner.GlobalPosition, ability.AbilityID, "siphon_snare");
            if (blocked != FTT.Combat.BlockResult.NotBlocked) {
                LastSiphonResult = SiphonAttachResult.Blocked;
                return;
            }

            var channel = new SiphonTetherChannel {
                Name = "SiphonTetherChannel",
                StableActorID = string.IsNullOrEmpty(SourceStableID) ? _owner.Name : SourceStableID
            };
            parent.AddChild(channel);
            channel.GlobalPosition = _owner.GlobalPosition;
            if (!channel.Attach(ability, _owner, target)) {
                LastSiphonResult = SiphonAttachResult.AlreadyTethered;
                channel.QueueFree();
                return;
            }
            _tether = channel;
            LastSiphonResult = SiphonAttachResult.Attached;
        }

        /// <summary>
        /// The nearest living player inside <paramref name="radius"/>
        /// centre-to-centre with unobstructed line of sight, excluding every
        /// player the contract refuses: Suppressed, at zero meter, already
        /// tethered, or invulnerable. Sweeps the <c>"Players"</c> group by
        /// distance (the extractor / dilation-field pattern), so it stays
        /// deterministic under headless direct calls where no physics flush
        /// resolves overlaps.
        /// </summary>
        private FTT.Characters.PlayerController NearestEligibleTarget(float radius) {
            SceneTree tree = _owner.IsInsideTree() ? _owner.GetTree() : null;
            if (tree == null) return null;
            Godot.Collections.Array<Node> players = tree.GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();

            FTT.Characters.PlayerController best = null;
            float bestDistance = float.MaxValue;
            foreach (Node node in players) {
                if (node is not FTT.Characters.PlayerController player) continue;
                if (!IsSiphonEligible(player)) continue;
                float distance = _owner.GlobalPosition.DistanceTo(player.GlobalPosition);
                if (distance > radius || distance >= bestDistance) continue;
                if (!SiphonTetherChannel.HasLineOfSight(_owner, _owner.GlobalPosition, player.GlobalPosition)) {
                    continue;
                }
                best = player;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// The contract's refusal set, shared by the attachment check and by the
        /// caller's "no new commitment against a zero-meter / Suppressed /
        /// invulnerable / already-tethered target".
        /// </summary>
        public static bool IsSiphonEligible(FTT.Characters.PlayerController player) {
            if (player == null || !GodotObject.IsInstanceValid(player)) return false;
            if (player.CurrentState is FTT.Characters.CharacterState.Dead
                or FTT.Characters.CharacterState.Respawning) return false;
            if (player.CurrentUltimateMeter <= 0.0001f) return false;
            if (player.HasStatusEffect(StatusType.Suppression)) return false;
            if (SiphonTetherChannel.IsTargetInvulnerable(player)) return false;
            return SiphonTetherChannel.ActiveFor(player) == null;
        }

        /// <summary>
        /// Breaks a live tether because the caster took a hit its V7.4 stagger
        /// armor did NOT reject. An armor-rejected hit never reaches this.
        /// </summary>
        public void InterruptSiphonTether() {
            if (_tether != null && GodotObject.IsInstanceValid(_tether)) {
                _tether.NotifyCasterInterrupted();
            }
            _tether = null;
        }

        /// <summary>Ends any live tether outright (death, despawn, room or level exit).</summary>
        public void ReleaseSiphonTether() {
            if (_tether != null && GodotObject.IsInstanceValid(_tether)) _tether.Release();
            _tether = null;
        }

        /// <summary>
        /// V7.3 Chrono-Warden rework: drops a <see cref="DilationFieldZone"/> at
        /// the target position CAPTURED at cast (Begin's target — the player's
        /// position when the telegraph started), so the telegraph is a promise
        /// about a place: standing still eats the field, moving answers it.
        /// </summary>
        private void SpawnPersistentField(EnemyAbilityData ability) {
            Node parent = _owner.GetParent();
            if (parent == null) return;
            var field = new DilationFieldZone { Name = "DilationFieldZone" };
            field.Configure(ability);
            parent.AddChild(field);
            field.GlobalPosition = _targetPosition;
        }

        private void ActivateHitbox(EnemyAbilityData ability, Vector2 size, Vector2 offset, bool mirrored) {
            if (_hitbox == null) return;
            float direction = _facingRight ? 1f : -1f;
            float knockbackSign = ability.IsPullKnockback ? -direction : direction;

            _hitbox.AttackID = ability.AbilityID;
            _hitbox.HitboxID = "primary";
            // V7.2 classification: every enemy attack resolves Basic-class
            // against the block (1 charge, never an instant shatter). Threat
            // beyond that is explicit: Guard-Crush costs 2, and boss-only
            // unblockables pierce the stance entirely.
            _hitbox.AttackClass = FTT.Combat.AttackClass.Basic;
            _hitbox.BlockChargeCost = ActiveGuardCrush ? 2 : 0;
            _hitbox.Unblockable = ActiveUnblockable;
            _hitbox.Damage = Mathf.Max(0f, ability.Damage) * Mathf.Max(0f, DamageMultiplier);
            _hitbox.KnockbackForce = new Vector2(
                knockbackSign * Mathf.Abs(ability.KnockbackForce.X),
                ability.KnockbackForce.Y);
            _hitbox.HitstunDuration = Mathf.Max(0f, ability.HitstunDuration);
            _hitbox.AppliedStatus = ability.AppliedStatus;
            _hitbox.StatusDuration = Mathf.Max(0f, ability.StatusDuration);
            _hitbox.StatusIntensity = ability.StatusIntensity <= 0f ? 1f : ability.StatusIntensity;
            _hitbox.OwnerPlayerIndex = -1;
            _hitbox.CollisionLayer = CollisionLayers.EnemyHitbox;
            _hitbox.CollisionMask = CollisionLayers.EnemyHitboxMask;
            _hitbox.Monitorable = true;
            _hitbox.Position = new Vector2(mirrored ? offset.X * direction : offset.X, offset.Y);
            if (_hitboxShape?.Shape is RectangleShape2D rect) {
                // A size-less ability keeps the last AUTHORED size, never a box a
                // previous swing's wall clip shrank (P2).
                if (size.X > 0f && size.Y > 0f) _unclippedHitboxSize = size;
                else if (_unclippedHitboxSize == Vector2.Zero) _unclippedHitboxSize = rect.Size;
                rect.Size = _unclippedHitboxSize;
            }
            // P2 (2026-10-04): a forward (mirrored) swing or charge stops at the
            // first wall. Area2D hitboxes ignore Environment, so the Duke's Siege
            // Smash used to land on a player standing behind a 20 px wall.
            _wallClipped = mirrored;
            _wallClipBlocked = false;
            // A3: only a charge carries its hitbox into walls as the owner moves;
            // a strike's box is fixed to a standing owner, so it clips once here.
            _wallClipFollowsOwner = mirrored && ability.Archetype == EnemyAbilityArchetype.ChargeDash;
            _wallClipOffset = offset;
            _wallClipSize = _unclippedHitboxSize != Vector2.Zero ? _unclippedHitboxSize : size;
            if (mirrored && !ClipHitboxToWalls()) {
                // The whole forward reach is behind the wall: the swing lands nothing.
                _wallClipBlocked = true;
                _hitbox.Deactivate();
                return;
            }
            _hitbox.Activate();
        }

        // === P2 (2026-10-04): wall clipping for forward hitboxes ===

        private bool _wallClipped;
        private bool _wallClipBlocked;
        /// <summary>A3: true for a ChargeDash, whose moving owner re-clips each active frame.</summary>
        private bool _wallClipFollowsOwner;
        /// <summary>A3: where the owner stood (and faced) at the last clip, so a halted charge probes nothing.</summary>
        private Vector2 _lastClipOwnerPosition;
        private bool _lastClipFacingRight;
        private Vector2 _wallClipOffset;
        private Vector2 _wallClipSize;
        /// <summary>The hitbox size last authored by an ability (before any wall clip).</summary>
        private Vector2 _unclippedHitboxSize;
        /// <summary>
        /// A3: the one ray query this executor reuses for every wall probe
        /// (RefCounted, held for the executor's life, never disposed).
        /// </summary>
        private PhysicsRayQueryParameters2D _wallProbeQuery;

        /// <summary>True when the last forward hitbox found its whole reach behind a wall. Test seam.</summary>
        public bool LastHitboxWallBlocked => _wallClipBlocked;

        /// <summary>Wall-probe rays this executor has cast since its last <see cref="Reset"/>. Test seam (A3).</summary>
        public int WallProbeRaysCast { get; private set; }

        /// <summary>The executor's reused probe query, or null before its first probe. Test seam (A3).</summary>
        internal PhysicsRayQueryParameters2D WallProbeQueryForTest => _wallProbeQuery;

        /// <summary>
        /// Recomputes the live forward hitbox from its authored offset and size
        /// against the walls in front of the owner right now
        /// (<see cref="FTT.Combat.EnemyAttackGeometryRules.ClipForwardSpan"/>).
        /// Returns false when nothing of the forward reach is left.
        /// </summary>
        private bool ClipHitboxToWalls() {
            if (_hitbox == null || _hitboxShape?.Shape is not RectangleShape2D rect) return true;
            float direction = _facingRight ? 1f : -1f;
            float near = _wallClipOffset.X - _wallClipSize.X / 2f;
            float far = _wallClipOffset.X + _wallClipSize.X / 2f;
            if (far <= 0f) return true;
            _lastClipOwnerPosition = _owner.GlobalPosition;
            _lastClipFacingRight = _facingRight;
            float wall = NearestBlockingWall(direction, far);
            if (!FTT.Combat.EnemyAttackGeometryRules.ClipForwardSpan(near, far, wall,
                    out float clippedNear, out float clippedFar)) {
                return false;
            }
            rect.Size = new Vector2(clippedFar - clippedNear, _wallClipSize.Y);
            _hitbox.Position = new Vector2(direction * (clippedNear + clippedFar) / 2f, _wallClipOffset.Y);
            return true;
        }

        /// <summary>
        /// Distance to the wall face in front of the owner along the hitbox's
        /// height, or -1 when any sampled height reaches the far edge unobstructed
        /// (the open part of the swing still lands).
        /// </summary>
        private float NearestBlockingWall(float direction, float far) {
            int rays = Math.Max(1, FTT.Combat.EnemyAttackGeometryRules.WallProbeRays);
            Vector2 owner = _owner.GlobalPosition;
            float farthestHit = -1f;
            _wallProbeQuery ??= FTT.Combat.EnvironmentProbe.CreateReusableQuery();
            for (int index = 0; index < rays; index++) {
                float fraction = (index + 1f) / (rays + 1f) - 0.5f;
                float y = owner.Y + _wallClipOffset.Y + fraction * _wallClipSize.Y;
                var from = new Vector2(owner.X, y);
                var to = new Vector2(owner.X + direction * far, y);
                WallProbeRaysCast++;
                float hit = FTT.Combat.EnvironmentProbe.FirstHitDistance(_owner, _wallProbeQuery, from, to);
                if (hit < 0f) return -1f;
                farthestHit = Mathf.Max(farthestHit, hit);
            }
            return farthestHit;
        }

        /// <summary>
        /// A charge carries its hitbox into walls as the owner moves, so the clip
        /// is recomputed every active frame the owner has moved or turned. A
        /// hitbox that ends up fully behind a wall is switched off for the rest of
        /// the phase and never re-armed, so a re-grow can never re-deliver the
        /// same swing. A3: a MeleeStrike's owner stands still for the swing, so
        /// its activation clip stands and no frame re-probes it.
        /// </summary>
        private void RefreshWallClip() {
            if (!_wallClipped || !_wallClipFollowsOwner || _wallClipBlocked
                || _hitbox == null || !_hitbox.IsActive) return;
            if (_owner.GlobalPosition == _lastClipOwnerPosition && _facingRight == _lastClipFacingRight) return;
            if (ClipHitboxToWalls()) return;
            _wallClipBlocked = true;
            _hitbox.Deactivate();
        }

        // === P3 (2026-10-04): shots re-sample and lead their target ===

        /// <summary>True when this telegraph was aimed at a live sand decoy (GAP-10c).</summary>
        private bool _telegraphedAtLure;

        /// <summary>
        /// Only an <see cref="EnemyController"/> is lured by the sand decoy
        /// (its <c>TargetAimPosition</c>); bosses ignore it.
        /// </summary>
        private bool HonoursSandDecoy => _owner is EnemyController;

        /// <summary>
        /// Where a shot fired now should aim. The telegraph captured a point; at
        /// fire time the living campaign player nearest that point (within
        /// <see cref="FTT.Combat.EnemyAttackGeometryRules.ResampleRadiusPixels"/>)
        /// is re-sampled at body height and led by its velocity over the shot's
        /// flight time. A target that crossed to the other side of the shooter
        /// during the telegraph keeps the telegraphed aim — dodging through the
        /// archer answers the promise rather than turning it around. Nobody near
        /// the point: the telegraphed point itself.
        ///
        /// <para><b>The sand decoy (GAP-10c).</b> A mob aims at Cleopatra's live
        /// decoy instead of her (<see cref="EnemyController.TargetAimPosition"/>),
        /// and she always stands within the re-sample radius of her own decoy,
        /// so a re-sample would turn every lured shot back onto her. A telegraph
        /// that was aimed at a lure keeps its telegraphed point; and a re-sampled
        /// player who raised a decoy during the telegraph is answered at the
        /// decoy, exactly as the mob's own aim would be. Bosses ignore the decoy
        /// (they never aim at it), so neither rule applies to them.</para>
        /// </summary>
        private Vector2 ResolveFireAim(EnemyAbilityData ability, Vector2 origin) {
            if (_telegraphedAtLure) return _targetPosition;
            FTT.Characters.PlayerController target = ResampleTarget();
            if (target == null) return _targetPosition;
            if (HonoursSandDecoy
                && FTT.Characters.Abilities.SandDecoyNode.TryGetLure(target, out Vector2 lure)) {
                return lure;
            }
            Vector2 aim = target.GlobalPosition
                - new Vector2(0f, FTT.Combat.EnemyAttackGeometryRules.BodyAimHeightPixels);
            Vector2 led = FTT.Combat.EnemyAttackGeometryRules.LeadPoint(
                origin, aim, target.Velocity, ability?.ProjectileSpeed ?? 0f);
            float telegraphedSide = Mathf.Sign(_targetPosition.X - origin.X);
            float firedSide = Mathf.Sign(led.X - origin.X);
            if (telegraphedSide != 0f && firedSide != 0f && telegraphedSide != firedSide) return _targetPosition;
            return led;
        }

        private FTT.Characters.PlayerController ResampleTarget() {
            SceneTree tree = _owner.IsInsideTree() ? _owner.GetTree() : null;
            if (tree == null) return null;
            Godot.Collections.Array<Node> players = tree.GetNodesInGroup("Players");
            using var playersLifetime = players.AsDisposable();
            FTT.Characters.PlayerController best = null;
            float bestDistance = FTT.Combat.EnemyAttackGeometryRules.ResampleRadiusPixels;
            foreach (Node node in players) {
                if (node is not FTT.Characters.PlayerController player || !GodotObject.IsInstanceValid(player)) continue;
                if (player == _owner || player.IsStoryHostile) continue;
                if (player.CurrentState is FTT.Characters.CharacterState.Dead
                    or FTT.Characters.CharacterState.Respawning) continue;
                float distance = player.GlobalPosition.DistanceTo(_targetPosition);
                if (distance > bestDistance) continue;
                best = player;
                bestDistance = distance;
            }
            return best;
        }

        private void SpawnProjectiles(EnemyAbilityData ability, bool lockVertical) {
            if (!EnsureProjectilePool()) return;
            Vector2 origin = _abilityOrigin != null
                ? _abilityOrigin.GlobalPosition
                : _owner.GlobalPosition + new Vector2((_facingRight ? 1f : -1f) * 24f, -40f);

            Vector2 toTarget = ResolveFireAim(ability, origin) - origin;
            Vector2 baseDirection = toTarget.LengthSquared() > 1f
                ? toTarget.Normalized()
                : new Vector2(_facingRight ? 1f : -1f, 0f);
            if (lockVertical) {
                baseDirection = new Vector2(baseDirection.X >= 0f ? 1f : -1f, 0f);
            }

            int count = Math.Max(1, ability.ProjectileCount);
            float spread = Mathf.DegToRad(Mathf.Max(0f, ability.ProjectileSpreadDegrees));
            float damage = Mathf.Max(0f, ability.Damage) * Mathf.Max(0f, DamageMultiplier);
            Node parent = _owner.GetParent();

            for (int index = 0; index < count; index++) {
                float fraction = count == 1 ? 0f : index / (count - 1f) - 0.5f;
                Vector2 direction = spread > 0f ? baseDirection.Rotated(spread * fraction) : baseDirection;
                var projectile = PoolManager.Instance.Spawn(ProjectilePoolID, origin, parent) as EnemyProjectile;
                projectile?.Setup(ability, direction * Mathf.Max(1f, ability.ProjectileSpeed), damage, SourceID,
                    lockVertical, ActiveGuardCrush, ActiveUnblockable);
                if (projectile != null) projectile.SourceIsStandardMob = _owner is EnemyController { Data.Tier: EnemyTier.Standard }; // F7 (FEEL)
            }
        }

        private void SummonMinions(EnemyAbilityData ability) {
            if (string.IsNullOrWhiteSpace(ability.SummonEnemyID)) return;
            Node parent = _owner.GetParent();
            if (parent == null) return;
            int count = Math.Max(1, ability.SummonCount);
            var spawned = new System.Collections.Generic.List<EnemyController>(count);
            for (int index = 0; index < count; index++) {
                float offsetX = (index % 2 == 0 ? -1f : 1f) * (96f + 48f * (index / 2));
                // Package 12 W2 (GAP-04): summons carry their provenance to death,
                // so they can never draw another encounter's finite reward.
                EnemyController minion = EnemyFactory.SpawnSummoned(
                    ability.SummonEnemyID, parent, _owner.GlobalPosition + new Vector2(offsetX, 0f));
                if (minion != null) spawned.Add(minion);
            }
            MinionsSummoned?.Invoke(ability, spawned);
        }

        private void Teleport(EnemyAbilityData ability) {
            Vector2 vacated = _owner.GlobalPosition;
            float span = Mathf.Max(0f, ability.TeleportRangeMax - ability.TeleportRangeMin);
            float distance = ability.TeleportRangeMin + (float)Rng.NextDouble() * span;
            // Reappear on the far side of the target so the reposition reads clearly.
            float side = _owner.GlobalPosition.X <= _targetPosition.X ? 1f : -1f;
            _owner.GlobalPosition = new Vector2(
                _targetPosition.X + side * Mathf.Max(1f, distance),
                _owner.GlobalPosition.Y);
            Teleported?.Invoke(ability, vacated);
        }

        private static bool EnsureProjectilePool() {
            PoolManager pools = PoolManager.Instance;
            if (pools == null) return false;
            if (pools.IsRegistered(ProjectilePoolID)) return true;
            PackedScene scene = GD.Load<PackedScene>(ProjectileScenePath);
            if (scene == null) return false;
            pools.RegisterPool(ProjectilePoolID, scene, ProjectilePoolWarmUp, ProjectilePoolCapacity,
                PoolOverflowPolicy.RecycleOldest);
            return true;
        }

        // === Presentation ===

        // V7.2 codified telegraph color language — the flash is a promise:
        // white/yellow = blockable (1 charge), orange = Guard-Crush (2),
        // red = Unblockable. Classification overrides the authored tint so the
        // promise holds roster-wide. V7.3 (ruling #11) makes the telegraph
        // dual-channel: the same class flags also pick a drawn glyph shape
        // (circle / diamond / X), so the promise reads without colour vision.
        private static readonly Color GuardCrushTelegraph = new(1f, 0.6f, 0.15f);
        private static readonly Color UnblockableTelegraph = new(1f, 0.25f, 0.2f);

        /// <summary>The Basic-class white/yellow family default, shared with the
        /// legacy scalar-melee fallback so no mob telegraph sits outside the
        /// three-colour language.</summary>
        public static readonly Color BasicTelegraph = new(1f, 0.95f, 0.6f);

        private TelegraphGlyph _glyph;

        /// <summary>The lazily-created glyph node, or null before the first telegraph. Test seam.</summary>
        public TelegraphGlyph Glyph => _glyph;

        /// <summary>The one shape rule — resolved from the SAME class flags as the
        /// tint, so the two channels can never disagree.</summary>
        public static TelegraphGlyphShape ResolveGlyphShape(bool guardCrush, bool unblockable) =>
            unblockable ? TelegraphGlyphShape.Unblockable
            : guardCrush ? TelegraphGlyphShape.GuardCrush
            : TelegraphGlyphShape.Basic;

        private Color TelegraphColor() {
            if (ActiveUnblockable) return UnblockableTelegraph;
            if (ActiveGuardCrush) return GuardCrushTelegraph;
            return ActiveAbility?.TelegraphTint ?? BasicTelegraph;
        }

        private void ApplyTelegraphTint() {
            if (_sprite == null || ActiveAbility == null) return;
            _spriteBaseModulate = _tintApplied ? _spriteBaseModulate : _sprite.Modulate;
            // With an arbiter attached the telegraph lives on the tint-override
            // channel, so a status effect ending cannot erase it and vice versa.
            Color tint = TelegraphColor();
            if (Glow != null) Glow.SetTintOverride(tint);
            else _sprite.Modulate = tint;
            _tintApplied = true;
            ShowGlyph(tint);
        }

        private void RestoreTint() {
            if (!_tintApplied) return;
            if (Glow != null) Glow.ClearTintOverride();
            else if (_sprite != null) _sprite.Modulate = _spriteBaseModulate;
            _tintApplied = false;
            if (_glyph != null && GodotObject.IsInstanceValid(_glyph)) _glyph.Visible = false;
        }

        /// <summary>Shows the class glyph over the owner for the telegraph window.</summary>
        private void ShowGlyph(Color tint) {
            if (_glyph == null || !GodotObject.IsInstanceValid(_glyph)) {
                _glyph = new TelegraphGlyph {
                    Name = "TelegraphGlyph",
                    Position = new Vector2(0f, -64f),
                    Visible = false
                };
                _owner.AddChild(_glyph);
            }
            // V7.6 F14: the Siphon Snare keeps its Basic-class circle (a shield
            // still answers it for one charge) and adds a second, additive tether
            // accent plus the drawn 3-unit attachment boundary. Both channels are
            // pure shape, so the promise reads without colour or flashing.
            bool siphon = ActiveAbility?.Archetype == EnemyAbilityArchetype.SiphonTether;
            _glyph.Present(
                ResolveGlyphShape(ActiveGuardCrush, ActiveUnblockable),
                tint,
                siphon ? TelegraphGlyphAccent.Tether : TelegraphGlyphAccent.None,
                siphon ? Mathf.Max(8f, ActiveAbility.PulseRadius) : 0f);
        }

        private void RaisePresentation(EnemyPresentationPhase phase) {
            EventBus.Instance?.RaiseEnemyPresentation(new EnemyPresentationPayload {
                SourceID = SourceID ?? "",
                AbilityID = ActiveAbility?.AbilityID ?? "",
                PresentationEventID = ActiveAbility?.PresentationEventID ?? "",
                Phase = phase,
                Position = _owner.GlobalPosition
            });
        }
    }
}
