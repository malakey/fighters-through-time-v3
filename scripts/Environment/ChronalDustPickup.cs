using Godot;
using FTT.Core;

namespace FTT.Environment {

    public partial class ChronalDustPickup : FTT.Core.PooledNode, FTT.Core.IPoolable, IStoryTimeFreezable {
        [Export] public int DustAmount = 10;
        [Export] public DustVisualTierSet VisualTiers;

        /// <summary>V7.3 Single Icon Rule: which system spawned this award.
        /// Collection attributes the amount to the matching results line.</summary>
        public DustAwardSource Source = DustAwardSource.Mob;

        /// <summary>Boss/extractor awards never expire — a milestone payout
        /// must not be lost to the 10 s kill-drop timer.</summary>
        public bool NeverExpires;

        /// <summary>Forces the Large visual tier regardless of amount (the
        /// extractor's 15 sits under the 25 threshold until the deferred
        /// economy rebalance; the icon must still read as a milestone).</summary>
        public bool ForceLargeTier;

        private const float MagnetRadius = 150f;
        private const float MagnetSpeed = 900f;
        private const float ExpirationTime = 10f;
        private float _lifetime;
        private FTT.Characters.PlayerController _magnetTarget;
        private Sprite2D _visual;
        private Sprite2D _glow;
        private float _glowTimer;

        /// <summary>
        /// History's resonance, warm gold (Package 8 B6 gave the dust its glow;
        /// Package 11 A6b named the pigment). The design's recurrence contract
        /// is explicit: <i>every Dust pickup restates the origin</i> — this is
        /// the same gold that ignites on the hero in Level 0 and the same gold
        /// the hero's persistent aura carries, which is why it reads from
        /// <see cref="FTT.UI.UIPalette.ResonanceAura"/> rather than repeating
        /// the literal.
        /// </summary>
        public static readonly Color GlowColor =
            new(FTT.UI.UIPalette.ResonanceAura, 0.5f);

        public override void _Ready() {
            AddToGroup("story_loot");
            AddToGroup("chronal_dust");
            _visual = GetNodeOrNull<Sprite2D>("Visual");
            _glow = GetNodeOrNull<Sprite2D>("Glow");
            if (_glow != null) _glow.Modulate = GlowColor;
            VisualTiers ??= FTT.Core.AuthoredResources.Load<DustVisualTierSet>("res://resources/Drops/dust_visual_tiers.tres");
            ApplyVisualTier();
        }

        public void Setup(int dustAmount) {
            DustAmount = Mathf.Max(1, dustAmount);
            ApplyVisualTier();
        }

        public void OnSpawn() {
            _lifetime = ExpirationTime;
            _magnetTarget = null;
            _glowTimer = 0f;
        }

        public void OnDespawn() {
            _magnetTarget = null;
            DustAmount = 1;
            Rotation = 0f;
            _glowTimer = 0f;
            Source = DustAwardSource.Mob;
            NeverExpires = false;
            ForceLargeTier = false;
            if (_glow != null) _glow.Scale = Vector2.One;
        }

        public override void _PhysicsProcess(double delta) {
            if (_timeFrozen) return;
            float dt = (float)delta;
            if (!NeverExpires) {
                _lifetime -= dt;
                if (_lifetime <= 0) { ReturnToPool(); return; }
            }

            if (_magnetTarget == null) {
                Godot.Collections.Array<Node> players = GetTree().GetNodesInGroup("Players");
                using var playersLifetime = players.AsDisposable();
                foreach (var node in players) {
                    if (node is FTT.Characters.PlayerController pc && GlobalPosition.DistanceTo(pc.GlobalPosition) <= MagnetRadius) {
                        _magnetTarget = pc;
                        break;
                    }
                }
            }

            if (_magnetTarget != null) {
                var dir = (_magnetTarget.GlobalPosition - GlobalPosition).Normalized();
                GlobalPosition += dir * MagnetSpeed * dt;
                if (GlobalPosition.DistanceTo(_magnetTarget.GlobalPosition) < 20f) {
                    Collect();
                }
            }
            Rotation += dt * 1.5f;

            _glowTimer += dt;
            if (_glow != null) {
                float pulse = 1f + 0.18f * Mathf.Sin(_glowTimer * 2.2f * Mathf.Tau);
                _glow.Scale = new Vector2(pulse, pulse);
            }
        }

        /// <summary>
        /// The collection site — the ONLY place a physical pickup pays the
        /// wallet (audit H-1's single-award rule, extended by the V7.3 Single
        /// Icon Rule to boss/extractor payouts). Fires the wallet event once,
        /// then the attribution payload for the itemized results, then returns
        /// to the pool. Public so tests and scripted collections can resolve a
        /// pickup without simulating the magnet.
        /// </summary>
        public void Collect() {
            FTT.Core.EventBus.Instance?.RaiseChronalDustCollected(DustAmount);
            FTT.Core.EventBus.Instance?.RaiseDustAwardCollected(new FTT.Core.DustAwardCollectedPayload {
                Amount = DustAmount,
                Source = Source
            });
            // Package 8 B5. Keyed off the collection site, not
            // OnChronalDustCollected: that event is also re-raised for every
            // enemy kill and every extractor break, which would double up with
            // those cues instead of marking a pickup.
            EnvironmentAudioCues.PlayPickup(1.35f);
            ReturnToPool();
        }

        private void ApplyVisualTier() {
            if (_visual == null || VisualTiers == null) return;
            _visual.Texture = ForceLargeTier && VisualTiers.LargeTexture != null
                ? VisualTiers.LargeTexture
                : VisualTiers.GetTexture(DustAmount);
        }

        // === IStoryTimeFreezable (V7.6 Time Freeze) ===========================

        private bool _timeFrozen;

        /// <summary>True while Time Freeze holds the world. Test seam.</summary>
        public bool IsTimeFrozen => _timeFrozen;

        /// <summary>
        /// Stops simulating in place, preserving every timer. For a pickup this
        /// is also what enforces "no pickup collection during a freeze": the
        /// magnet that resolves a collection lives in the frozen tick.
        /// </summary>
        public void SetTimeFrozen(bool frozen) => _timeFrozen = frozen;

    }
}
