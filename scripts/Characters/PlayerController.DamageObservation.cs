using System;
using FTT.Combat;

namespace FTT.Characters {

	/// <summary>
	/// CM-BOT (decision 7.2, 2026-10-16): which damage chokepoint raised one
	/// <see cref="PlayerController.DamageTaken"/> observation.
	/// </summary>
	public enum DamageObservationKind {
		/// <summary>A hurtbox contact or a direct <c>TakeDamage</c> call (<c>ResolveIncomingHit</c>).</summary>
		Hit = 0,
		/// <summary>The environmental chokepoint (<c>ApplyEnvironmentalDamage</c>): hazards, discharges, catches, debris.</summary>
		Hazard = 1,
		/// <summary>A scheduled damaging tick of an applied status (<c>ApplyPersistentDamage</c>, Venom).</summary>
		Status = 2
	}

	/// <summary>
	/// CM-BOT (decision 7.2): one positive HP loss on a Story fighter, with what
	/// is known about its source at the chokepoint that applied it. A
	/// <see cref="DamageObservationKind.Hit"/> carries the payload's own
	/// <c>SourceActorId</c> (the mob, boss, shooter or Mirror clone that owns the
	/// hitbox; 0 when the payload names none), <c>AttackID</c>, <c>HitboxID</c>
	/// (<c>"projectile"</c> for every pooled shot), <c>AttackerIndex</c> and
	/// <c>Delivery</c>. Hazard and status observations name no source: the
	/// environmental chokepoint and the status tick receive none.
	/// </summary>
	public readonly struct PlayerDamageObservation {
		public readonly DamageObservationKind Kind;
		public readonly ulong SourceActorId;
		public readonly string AttackID;
		public readonly string HitboxID;
		public readonly int AttackerIndex;
		public readonly HitDelivery Delivery;
		/// <summary>The HP actually removed (always &gt; 0).</summary>
		public readonly int Damage;

		public PlayerDamageObservation(DamageObservationKind kind, ulong sourceActorId, string attackID, string hitboxID,
			int attackerIndex, HitDelivery delivery, int damage) {
			Kind = kind;
			SourceActorId = sourceActorId;
			AttackID = attackID ?? "";
			HitboxID = hitboxID ?? "";
			AttackerIndex = attackerIndex;
			Delivery = delivery;
			Damage = damage;
		}
	}

	public partial class PlayerController {
		/// <summary>
		/// CM-BOT (decision 7.2, the mercy-invulnerability measurement): raised
		/// once for every positive HP loss through the three damage chokepoints —
		/// <c>ResolveIncomingHit</c>, the environmental chokepoint and the
		/// damaging status tick — right after the HP loss is written (and after
		/// <c>OnPlayerHPChanged</c>, and <c>OnPlayerDied</c> for a lethal hit, which
		/// <c>ApplyDamage</c> raises), before the rest of that hit's pipeline
		/// (stun, knockback, Rally, meter).
		/// <para>
		/// <b>Observation only.</b> Nothing in the game subscribes; the playtest
		/// telemetry does, through <c>PlaytestWorld</c>'s "Hero gates" seam. A
		/// subscriber must never write gameplay state: the event can run inside
		/// the physics flush. Raising it changes no value, which
		/// <c>PlaytestCombatStatsTests</c> pins (HP, meter and Rally are identical
		/// with and without a subscriber). <c>KillPlayerNonHit</c> applies no
		/// damage and raises nothing.
		/// </para>
		/// </summary>
		public event Action<PlayerDamageObservation> DamageTaken;

		/// <summary>The hurtbox path's observation: the payload names its source.</summary>
		private void ObserveDamageTaken(in HitPayload hit, int damageApplied) {
			Action<PlayerDamageObservation> handler = DamageTaken;
			if (handler == null || damageApplied <= 0) return;
			handler(new PlayerDamageObservation(DamageObservationKind.Hit, hit.SourceActorId, hit.AttackID,
				hit.HitboxID, hit.AttackerIndex, hit.Delivery, damageApplied));
		}

		/// <summary>The environmental and status paths' observation: no source is known there.</summary>
		private void ObserveDamageTaken(DamageObservationKind kind, int damageApplied) {
			Action<PlayerDamageObservation> handler = DamageTaken;
			if (handler == null || damageApplied <= 0) return;
			handler(new PlayerDamageObservation(kind, 0UL, "", "", -1,
				kind == DamageObservationKind.Status ? HitDelivery.Tick : HitDelivery.Hazard, damageApplied));
		}
	}
}
