using System.Collections.Generic;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Enemies {

    /// <summary>
    /// A mob's hazard half (gameplay review 2026-11-03, ruling 7d, plan section 1.13; Story only).
    /// Hazards hurt a mob only while it is launched or thrown; the rule and its numbers are
    /// <see cref="HazardMobRules"/>.
    /// </summary>
    public partial class EnemyController {
        // Sources that already hit during this launch. Cleared where a new launch or throw
        // begins (and on pool and rewind resets), never by ResetLaunchCarry: a Defy blast must not
        // reopen it while a thrown flight is still going.
        private readonly HashSet<ulong> _hazardSourcesThisLaunch = new();

        /// <summary>True while a hazard can hurt this mob: thrown or launched, not dormant, alive.</summary>
        public bool IsHazardVulnerable => HazardMobRules.IsVulnerable(
            _thrownFlight, _wallBounceArmed, _finisherFlight, IsEncounterDormant, CurrentState != EnemyState.Dead);

        /// <summary>True on a corpse whose killing blow was a hazard impact, until its pool reset. Read by the bot telemetry.</summary>
        public bool KilledByHazard { get; private set; }

        /// <summary>
        /// One hazard contact from <paramref name="sourceId"/>, the hazard's instance ID. Returns
        /// the HP it took. Refused, without spending the source, for a mob that is not vulnerable
        /// or that the world holds. It is not a hit: no stun, hitstop, poise, meter, Momentum,
        /// Rally or landed signal, and no defence reduces it.
        /// </summary>
        public int ApplyHazardImpact(ulong sourceId) {
            if (!IsHazardVulnerable || DrillInvulnerable || IsHazardWorldHeld) return 0;
            if (!_hazardSourcesThisLaunch.Add(sourceId)) return 0;
            // A kill can cascade into a lock's clear or a dust spawn from inside a flush.
            using var scope = PhysicsCallbackGuard.Enter();
            int damage = HazardMobRules.ImpactDamage(
                HazardMobRules.Classify(Data?.Tier == EnemyTier.Elite, Data?.EnemyID), CurrentHP, ScaledMaxHP);
            if (damage <= 0) return 0;
            // Straight off the HP, not through TakeDamage: its status multiplier could kill an
            // elite this rule has just left alive.
            CurrentHP -= damage;
            _barVisibility.NotifyDamaged();
            UpdateHPBar();
            if (IsInsideTree()) FTT.UI.FloatingDamageNumber.Show(damage, DamageNumberPosition, GetParent());
            if (CurrentHP <= 0) {
                KilledByHazard = true;
                Die();
            } else {
                PlayAnimation("hitstun");
                _glow?.FlashHit();
            }
            return damage;
        }

        private bool IsHazardWorldHeld =>
            _rewindFrozen || (IsInsideTree() && FTT.Environment.ChronalRewindManager.IsWorldHeld(GetTree()));
    }
}
