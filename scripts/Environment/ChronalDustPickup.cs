using System.Collections.Generic;
using Godot;
using FTT.Core;

namespace FTT.Environment {

    public partial class ChronalDustPickup : FTT.Core.PooledNode, FTT.Core.IPoolable, IStoryTimeFreezable {
        [Export] public int DustAmount = 10;
        // Scene-assigned scripted data, pinned on first sight (AuthoredResources.Pin).
        [Export] public DustVisualTierSet VisualTiers {
            get => _visualTiers;
            set => _visualTiers = FTT.Core.AuthoredResources.Pin(value);
        }
        private DustVisualTierSet _visualTiers;

        /// <summary>V7.3 Single Icon Rule: which system spawned this award.
        /// Collection attributes the amount to the matching results line.</summary>
        public DustAwardSource Source = DustAwardSource.Mob;

        /// <summary>
        /// Package 11 A10 (F05): the stable reward-source ID this pickup was
        /// issued against, or empty for an unledgered award. Collection commits
        /// the source claim and the wallet increment together, which is what
        /// makes a reward once-per-attempt across rewinds and resumes.
        /// </summary>
        public string SourceID = "";

        /// <summary>Boss / extractor / secret awards have no timer — a
        /// milestone payout waits on the ground until it is collected or the
        /// level's completion settles it. A mob pickup's timer collects it
        /// (D12, <c>USER-DUST-NEVER-EXPIRES</c>).</summary>
        public bool NeverExpires;

        /// <summary>Forces the Large visual tier regardless of amount (the
        /// extractor's 15 sits under the 25 threshold until the deferred
        /// economy rebalance; the icon must still read as a milestone).</summary>
        public bool ForceLargeTier;

        private const float MagnetRadius = 150f;
        private const float MagnetSpeed = 900f;
        /// <summary>
        /// A mob pickup that reaches this timer collects itself — claim, wallet
        /// and attribution, exactly as a touched pickup does (user direction
        /// D12, <c>USER-DUST-NEVER-EXPIRES</c>). It used to vanish here unpaid,
        /// which made the authored 720 / 1,000 totals unreachable.
        /// </summary>
        private const float AutoCollectSeconds = 10f;
        // Field-initialized so a hand-instantiated pickup that never went
        // through OnSpawn cannot auto-collect on its first tick.
        private float _lifetime = AutoCollectSeconds;
        private bool _collected;

        /// <summary>
        /// True from the first <see cref="Collect"/> until the next pool spawn.
        /// Test seam, and the completion settle's filter.
        /// </summary>
        public bool IsCollected => _collected;
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
            _lifetime = AutoCollectSeconds;
            _collected = false;
            _magnetTarget = null;
            _glowTimer = 0f;
        }

        public void OnDespawn() {
            // _collected deliberately survives until the next spawn: a stray
            // tick or Collect() on a parked (or deferred-release) node pays nothing.
            _lifetime = AutoCollectSeconds;
            _timeFrozen = false;
            _magnetTarget = null;
            DustAmount = 1;
            Rotation = 0f;
            _glowTimer = 0f;
            Source = DustAwardSource.Mob;
            SourceID = "";
            NeverExpires = false;
            ForceLargeTier = false;
            if (_glow != null) _glow.Scale = Vector2.One;
        }

        public override void _PhysicsProcess(double delta) {
            if (_collected) return;
            if (_timeFrozen) return;
            float dt = (float)delta;
            if (!NeverExpires) {
                _lifetime -= dt;
                // D12: the timer collects, it no longer discards.
                if (_lifetime <= 0) { Collect(); return; }
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
        ///
        /// <para>D12: idempotent — a second call on a collected or parked
        /// pickup pays nothing. Once the owning level has completed, the
        /// pickup releases <b>unpaid</b>: the completion has already deposited
        /// the wallet and re-keyed the reward directory, so a payment now would
        /// leak into the next level's bank (<c>PROVISIONAL-DUST-SETTLE-RULES</c>).</para>
        /// </summary>
        public void Collect() {
            if (_collected) return;
            _collected = true;
            // D12 (PROVISIONAL-DUST-SETTLE-RULES (3)): completion has deposited
            // the wallet and re-keyed the reward directory; nothing pays after it.
            if (LevelManager.IsLevelCompleteFor(this)) { ReturnToPool(); return; }
            // Package 11 A10 (F05): the claim commits HERE, with the wallet
            // increment, never at the kill. A pickup left on the ground keeps
            // its source spawned-but-uncollected, so a reload that discards the
            // pickup can legitimately re-issue it without ever duplicating one.
            LevelRewardDirectory.CommitClaim(SourceID);
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

        /// <summary>
        /// D12: settles every live dust pickup under <paramref name="root"/> into
        /// the wallet through <see cref="Collect"/> (claim + wallet +
        /// attribution), returning the total paid. Every source is settled — a
        /// mob drop still on its timer and, per the 2026-10-11 addendum, a boss,
        /// Extractor or Secret award left on the ground at the seal
        /// (<c>PROVISIONAL-DUST-SETTLE-RULES</c> (2)). Called by
        /// <see cref="LevelManager.CompleteLevel"/> <b>before</b> it sets
        /// <see cref="LevelManager.IsComplete"/>, so these collections still pay.
        /// Pool-parked pickups are skipped (they are Disabled).
        /// </summary>
        public static int SettleOutstandingDust(Node root) {
            if (root == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return 0;
            var live = new List<ChronalDustPickup>();
            Godot.Collections.Array<Node> members = root.GetTree().GetNodesInGroup("chronal_dust");
            using (members.AsDisposable()) {
                foreach (Node node in members) {
                    if (node is ChronalDustPickup pickup && GodotObject.IsInstanceValid(pickup)
                        && pickup.IsInsideTree() && !pickup.IsQueuedForDeletion()
                        && pickup.ProcessMode != ProcessModeEnum.Disabled
                        && !pickup.IsCollected && root.IsAncestorOf(pickup)) {
                        live.Add(pickup);
                    }
                }
            }
            // The list is built first: Collect releases the node to its pool.
            int paid = 0;
            foreach (ChronalDustPickup pickup in live) {
                paid += pickup.DustAmount;
                pickup.Collect();
            }
            return paid;
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
