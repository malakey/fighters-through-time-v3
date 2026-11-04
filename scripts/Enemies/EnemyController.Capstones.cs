using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// A mob's half of the Resonance capstones (gameplay review 2026-11-03, plan GR-CAP; Story only):
    /// Cleopatra's Dynasty's Curse, and the hazard opening Shakespeare's Curtain Call windbox gives.
    /// </summary>
    public partial class EnemyController {
        private ulong _hazardArmedUntilPhysicsFrame;

        /// <summary>True while a Curtain Call windbox holds this mob open to hazards.</summary>
        private bool IsHazardArmedByWindbox => Engine.GetPhysicsFrames() < _hazardArmedUntilPhysicsFrame;

        /// <summary>
        /// Opens this mob to hazards for <paramref name="frames"/>, as a launch would. The opening
        /// counts as a new launch for the once-per-source rule, and refreshing it does not.
        /// </summary>
        public void ArmHazardVulnerability(int frames) {
            if (!IsHazardArmedByWindbox) _hazardSourcesThisLaunch.Clear();
            _hazardArmedUntilPhysicsFrame = Engine.GetPhysicsFrames() + (ulong)Mathf.Max(0, frames);
        }

        /// <summary>
        /// Dynasty's Curse, read on the way into <see cref="Die"/> while the mob still carries its
        /// status: a mob that dies poisoned, by any killing blow, passes its Venom to the nearest
        /// living mobs in reach. Bosses are not <see cref="EnemyController"/>s, so they neither catch it
        /// nor spread it, and a dormant lock member refuses it.
        /// </summary>
        private void SpreadVenomOnDeath() {
            if (DamageStatusType != StatusType.Venom || !IsInsideTree()) return;
            FTT.Characters.PlayerController hero = FindNearestPlayer();
            if (hero == null || !hero.HasStoryPerk(CapstoneRules.DynastysCursePerkKey)) return;
            float reach = (float)CapstoneRules.VenomSpreadRadiusUnits * KitMotionRules.StoryPixelsPerUnit;
            var nearby = new List<EnemyController>();
            Godot.Collections.Array<Node> enemies = GetTree().GetNodesInGroup("Enemies");
            using var enemiesLifetime = enemies.AsDisposable();
            foreach (Node node in enemies) {
                if (node is not EnemyController other || other == this) continue;
                if (other.CurrentState == EnemyState.Dead || other.IsEncounterDormant) continue;
                if (GlobalPosition.DistanceTo(other.GlobalPosition) <= reach) nearby.Add(other);
            }
            nearby.Sort((a, b) => GlobalPosition.DistanceSquaredTo(a.GlobalPosition)
                .CompareTo(GlobalPosition.DistanceSquaredTo(b.GlobalPosition)));
            float seconds = CapstoneRules.VenomSpreadSeconds * hero.StoryStatusDurationMultiplier;
            for (int index = 0; index < nearby.Count && index < CapstoneRules.VenomSpreadMaxTargets; index++) {
                nearby[index].ApplyStatusEffect(StatusType.Venom, seconds, _damageStatusIntensity);
            }
        }
    }
}
