using Godot;
using FTT.Core;
using FTT.Environment;

namespace FTT.Enemies {

    /// <summary>
    /// Package 12 W9 (GAP-07) — the Jackal Priest's Phase 2 sand decoy.
    ///
    /// <para>
    /// Design §6 boss table: "the sandstorm veils him — each teleport leaves a sand
    /// decoy; only the true priest's staff glows on cast, and striking a decoy
    /// triggers its burst." The decoy stands where the priest was, never
    /// telegraphs (it has no executor, so it never shows the cast tint — that is
    /// the tell), and carries an ordinary <c>EnemyHurtbox</c> so any player attack
    /// can strike it. A strike deals it nothing and credits nothing: it releases
    /// one Basic-class burst against every player inside
    /// <see cref="BossData.DecoyBurstRadius"/> and crumbles. Left alone it crumbles
    /// after <see cref="BossData.DecoyLifetimeSeconds"/>.
    /// </para>
    ///
    /// <para>
    /// Story-only placeholder presentation (a sand-tinted copy of the priest's
    /// current frame). It is <see cref="IStoryTimeFreezable"/> in the shared
    /// <c>time_freezable</c> group, so Time Freeze and the Post-Landing Hold stop
    /// its lifetime, and the shared world-held gate in <c>Hurtbox.TakeHit</c>
    /// refuses strikes while the world is held.
    /// </para>
    /// </summary>
    public partial class BossDecoy : Node2D, IStoryTimeFreezable {
        public const string GroupName = "boss_decoy";
        public static readonly Color SandTint = new(0.86f, 0.74f, 0.48f, 0.85f);

        public float LifetimeSeconds { get; private set; } = 6f;
        public float BurstRadius { get; private set; } = 150f;
        public float BurstDamage { get; private set; } = 14f;
        public string SourceBossID { get; private set; } = "";

        /// <summary>True once the decoy burst or crumbled; it is being freed.</summary>
        public bool IsSpent { get; private set; }

        /// <summary>Players the burst actually reached (test seam / presentation).</summary>
        public int LastBurstVictims { get; private set; }

        private float _remaining;
        private bool _frozen;
        private FTT.Combat.Hurtbox _hurtbox;

        /// <summary>Copies the authored tuning off the boss resource.</summary>
        public void Configure(BossData data, Texture2D silhouette, bool flipH) {
            SourceBossID = data?.BossID ?? "";
            LifetimeSeconds = Mathf.Max(0.1f, data?.DecoyLifetimeSeconds ?? 6f);
            BurstRadius = Mathf.Max(8f, data?.DecoyBurstRadius ?? 150f);
            BurstDamage = Mathf.Max(0f, data?.DecoyBurstDamage ?? 14f);
            _remaining = LifetimeSeconds;

            if (silhouette != null) {
                AddChild(new Sprite2D {
                    Name = "Silhouette",
                    Texture = silhouette,
                    FlipH = flipH,
                    Modulate = SandTint,
                    Centered = true,
                    Offset = new Vector2(0f, -silhouette.GetHeight() * 0.5f)
                });
            } else {
                AddChild(new ColorRect {
                    Name = "Silhouette",
                    Size = new Vector2(56f, 110f),
                    Position = new Vector2(-28f, -110f),
                    Color = SandTint,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }

            _hurtbox = new FTT.Combat.Hurtbox {
                Name = "Hurtbox",
                OwnerPlayerIndex = -1,
                CollisionLayer = CollisionLayers.EnemyHurtbox,
                CollisionMask = 0,
                Monitoring = false,
                Monitorable = true,
                Position = new Vector2(0f, -55f)
            };
            _hurtbox.AddChild(new CollisionShape2D {
                Shape = new RectangleShape2D { Size = new Vector2(56f, 110f) }
            });
            _hurtbox.OnHit += OnStruck;
            AddChild(_hurtbox);
        }

        public override void _Ready() {
            AddToGroup(GroupName);
            AddToGroup(TimeFreezeController.FreezableGroup);
        }

        public override void _ExitTree() {
            if (_hurtbox != null) _hurtbox.OnHit -= OnStruck;
        }

        public override void _PhysicsProcess(double delta) {
            if (_frozen || IsSpent) return;
            _remaining -= (float)delta;
            if (_remaining <= 0f) Crumble();
        }

        /// <summary>Seconds left before it crumbles on its own.</summary>
        public float RemainingSeconds => _remaining;

        private float OnStruck(FTT.Combat.HitPayload hit) {
            // Only the player strikes a decoy; enemy hitboxes share the -1 index.
            if (hit.AttackerIndex < 0) return 0f;
            Burst();
            // A decoy is not a combatant: no damage dealt, so no meter or Rally.
            return 0f;
        }

        /// <summary>
        /// The punish: one Basic-class hit on every living player inside the burst
        /// radius, routed through the player's own hit intake so block, Defy and
        /// every protection layer apply as for any enemy hit.
        /// </summary>
        public void Burst() {
            if (IsSpent) return;
            IsSpent = true;
            LastBurstVictims = 0;
            if (IsInsideTree()) {
                Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
                using var lifetime = players.AsDisposable();
                foreach (Node node in players) {
                    if (node is not FTT.Characters.PlayerController player) continue;
                    if (player.CurrentState == FTT.Characters.CharacterState.Dead) continue;
                    if (player.GlobalPosition.DistanceTo(GlobalPosition) > BurstRadius) continue;
                    bool fromRight = GlobalPosition.X > player.GlobalPosition.X;
                    var payload = new FTT.Combat.HitPayload {
                        AttackerIndex = -1,
                        AttackID = $"boss.{SourceBossID}.decoy_burst",
                        HitboxID = "decoy_burst",
                        AttackClass = FTT.Combat.AttackClass.Basic,
                        Damage = BurstDamage * StoryDifficultyTuning.GetEnemyDamageMultiplier(
                            StoryDifficultyTuning.CurrentStoryDifficulty),
                        Knockback = new Vector2(3f, -2f),
                        HitstunDuration = 0.25f,
                        HitOrigin = GlobalPosition,
                        AttackerFacingRight = !fromRight,
                        BlockChargeCost = 0,
                        Delivery = FTT.Combat.HitDelivery.DirectHit,
                        ContactId = FTT.Combat.HitClassification.NextContactId()
                    };
                    player.TakeDamage(in payload);
                    LastBurstVictims++;
                }
            }
            Retire();
        }

        private void Crumble() {
            if (IsSpent) return;
            IsSpent = true;
            Retire();
        }

        private void Retire() {
            if (_hurtbox != null) {
                _hurtbox.SetMonitorableSafe(false);
            }
            Visible = false;
            if (IsInsideTree()) QueueFree();
        }

        // === Freeze ========================================================

        /// <summary>Time Freeze and the Post-Landing Hold stop the lifetime in place.</summary>
        public void SetTimeFrozen(bool frozen) => _frozen = frozen;

        /// <summary>True while a world freeze holds the decoy. Test seam.</summary>
        public bool IsTimeFrozen => _frozen;
    }
}
