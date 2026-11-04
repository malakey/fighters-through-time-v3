using System.Collections.Generic;
using FTT.Characters;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Environment {

    /// <summary>
    /// GR-LV4 (ruling 14; design §6): the Siege Cannon's Phase 2. "The railcar relocates along
    /// its track after every volley and the cover fences become destructible — the level's
    /// cover lanes must be re-read each cycle." The worked example is the Duke's boiler rupture,
    /// Level 2.
    ///
    /// <para>Two fences stand in the arena from the start, and they are cover through Phase 1: a
    /// shell stops on one, and nothing breaks it. Phase 2 sets
    /// <see cref="BossController.AbilityAfterVolley"/> to the kit's own <c>rail_warp</c> and makes
    /// the fences breakable: three strikes break one, and each volley fired across one spends a
    /// strike of it. A warp stops short of a standing fence (<c>RelocationRules</c>), so the
    /// railcar reaches the hero's side of it only once it falls. Every number here is
    /// provisional (<c>PROVISIONAL-BOSS-ARENA-RULES</c>).</para>
    /// </summary>
    public partial class Level11Controller {
        /// <summary>The fence centres, inside the arena, either side of the boss's spawn.</summary>
        public static readonly float[] RailcutFenceXs = { 10200f, 11200f };

        public const string RailWarpAbilityID = "boss.siege_cannon.rail_warp";
        /// <summary>A hit a volley spends of a fence it crosses, in the fence's <see cref="DamageableEnvironmentObject.HitsToBreak"/>.</summary>
        public const int RailcutFenceHitsToBreak = 3;

        private BossEncounterController _cannonEncounter;
        private BossController _cannonListened;
        private readonly List<RailcutFence> _railcutFences = new();

        public BossEncounterController CannonEncounter => _cannonEncounter;
        public IReadOnlyList<RailcutFence> RailcutFences => _railcutFences;

        /// <summary>True once Phase 2 has started its rules, and until the Cannon falls.</summary>
        public bool RailcutSecondActRaised { get; private set; }

        private void BuildRailcutFences() {
            (float width, float thickness, Color fill) = CoverProfile(CoverKind.SplitRailFence);
            for (int index = 0; index < RailcutFenceXs.Length; index++) {
                var fence = RailcutFence.Create($"level_11.railcut_fence_{index}", RailcutFenceHitsToBreak,
                    new Vector2(RailcutFenceXs[index], GroundY - thickness / 2f), new Vector2(width, thickness), fill);
                AddChild(fence);
                _railcutFences.Add(fence);
            }
        }

        private void OnCannonPhaseEntered(int phase) {
            if (phase < 1) return;
            // A phase change arrives inside the hit's physics flush.
            if (PhysicsCallbackGuard.IsInPhysicsCallback) Callable.From(RaiseRailcutSecondAct).CallDeferred();
            else RaiseRailcutSecondAct();
        }

        /// <summary>Starts the Phase 2 rules. Public and idempotent (test seam), and it does nothing once the Cannon is down.</summary>
        public void RaiseRailcutSecondAct() {
            if (RailcutSecondActRaised || IsBossDefeated) return;
            RailcutSecondActRaised = true;
            foreach (RailcutFence fence in _railcutFences) fence.Breakable = true;
            BossController cannon = _cannonEncounter?.Boss;
            if (cannon == null || !IsInstanceValid(cannon) || cannon.Data?.BossAbilities == null) return;
            foreach (EnemyAbilityData ability in cannon.Data.BossAbilities) {
                if (ability?.AbilityID == RailWarpAbilityID) cannon.AbilityAfterVolley = ability;
            }
            if (_cannonListened != cannon) {
                _cannonListened = cannon;
                cannon.VolleyEnded += OnCannonVolleyEnded;
            }
        }

        /// <summary>The Cannon is down: the hero still has to walk to the sealing anchor.</summary>
        private void EndRailcutSecondAct() {
            RailcutSecondActRaised = false;
            foreach (RailcutFence fence in _railcutFences) fence.Breakable = false;
            if (_cannonListened != null && IsInstanceValid(_cannonListened)) {
                _cannonListened.AbilityAfterVolley = null;
                _cannonListened.VolleyEnded -= OnCannonVolleyEnded;
            }
            _cannonListened = null;
        }

        /// <summary>A volley spends a strike of the first standing fence between the Cannon and the hero.</summary>
        private void OnCannonVolleyEnded(BossController cannon) {
            if (Player == null || !IsInstanceValid(Player)) return;
            FenceBetween(cannon.GlobalPosition.X, Player.GlobalPosition.X)?.TakeEnvironmentDamage(1f);
        }

        /// <summary>The standing fence nearest <paramref name="fromX"/> on the way to <paramref name="toX"/>, or null.</summary>
        public RailcutFence FenceBetween(float fromX, float toX) {
            RailcutFence nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (RailcutFence fence in _railcutFences) {
                if (!IsInstanceValid(fence) || fence.IsDestroyed) continue;
                float x = fence.GlobalPosition.X;
                if (x < Mathf.Min(fromX, toX) || x > Mathf.Max(fromX, toX)) continue;
                float distance = Mathf.Abs(x - fromX);
                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearest = fence;
                }
            }
            return nearest;
        }
    }
}
