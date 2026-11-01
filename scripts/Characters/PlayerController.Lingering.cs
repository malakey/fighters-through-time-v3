using System;
using FTT.Combat;
using FTT.Core;
using Godot;

namespace FTT.Characters {

	/// <summary>
	/// What the Story seam <see cref="PlayerController.OwnerHitLanded"/> carries (lingering-kit
	/// pass 2026-10-31, plan 1.3). One value per LANDED (D1a) contact of the owner's.
	/// <para><b>Which payloads reach the seam.</b> Every contact delivered through
	/// <c>Hitbox.OnAreaEntered</c> (basic swings, Hitbox-delivered Specials, pooled projectiles,
	/// the turret's pooled bolt, ground waves that use a Hitbox) and every kit shape-query or
	/// ground-wave hit credited through <c>BaseSpecial.Credit</c>, whatever its delivery,
	/// origin or damage, as long as the receiver reported it landed. A contact that did not land
	/// (a whiff, a block, an invulnerability or ward refusal, a strike surface, zero damage)
	/// raises nothing. A construct node that calls <c>Hurtbox.TakeDamage</c> directly (the coil
	/// arc, the nest bite) and a zone tick (<c>PlaceholderZone</c>) never pass through either
	/// path and raise nothing; the turret's pooled bolt does (delivery <c>Construct</c>), so a
	/// subscriber must still filter with <see cref="TriggersSyncedStrike"/>.</para>
	/// </summary>
	public readonly struct OwnerHitLandedInfo {
		/// <summary>The struck hurtbox (its <c>GlobalPosition</c> is the contact; its owner is the target body).</summary>
		public readonly Hurtbox Target;
		/// <summary>The payload as stamped: <c>ContactLanded</c> is true.</summary>
		public readonly HitPayload Payload;
		/// <summary>Always true on the seam (a landed contact is the only thing raised); kept for subscribers' clarity.</summary>
		public readonly bool Landed;
		/// <summary>Where the impact reads: the struck hurtbox or a wave's front.</summary>
		public readonly Vector2 Position;

		public OwnerHitLandedInfo(Hurtbox target, in HitPayload payload, bool landed, Vector2 position) {
			Target = target;
			Payload = payload;
			Landed = landed;
			Position = position;
		}

		/// <summary>The hit is one of the owner's basic hits (string, air strike, Up-Attack, Down-Air).</summary>
		public bool IsBasicHit => BasicComboRules.IsBasicStringHitbox(Payload.HitboxID);

		/// <summary>
		/// The synced-strike trigger rule (plan 1.3): a landed <b>basic hit</b> or a landed
		/// <b>direct Special hit</b> (<c>DirectHit</c> delivery, Basic or Special origin, never
		/// Ultimate-origin). Never a construct hit, a tick, another strike (anything
		/// <c>CountsAsCombo</c>), a throw, an Ultimate or the Lorentz Pulse / its chain arcs
		/// (the discharge replaces them): that rules out loops.
		/// </summary>
		public bool TriggersSyncedStrike =>
			Landed
			&& Payload.ContactLanded
			&& !Payload.CountsAsCombo
			&& Payload.Delivery == HitDelivery.DirectHit
			&& (Payload.Origin == HitOrigin.Basic || Payload.Origin == HitOrigin.Special)
			&& Payload.HitboxID != BasicComboRules.ThrowHitboxID
			&& Payload.HitboxID != "pulse"
			&& Payload.HitboxID != "chain_lightning"
			&& Payload.Damage > 0f;
	}

	public partial class PlayerController {

		/// <summary>
		/// The Story seam "the owner's hit landed" (lingering-kit pass). Raised once per landed (D1a)
		/// contact of this player's: from <c>Hitbox.OnAreaEntered</c> right after the
		/// <c>ContactLanded</c> stamp, and from <c>BaseSpecial.Credit</c> for shape-query Specials.
		/// Subscribers (the Story constructs, the Rift's combo_3 collapse) filter with
		/// <see cref="OwnerHitLandedInfo.TriggersSyncedStrike"/> and the payload's own fields, and
		/// unsubscribe in <c>_ExitTree</c>. The handler runs inside the physics-callback guard when
		/// raised from a Hitbox, so it may not toggle monitoring or reparent collision objects
		/// (use the <c>PhysicsSafeSetters</c> / a deferred call, as every other hit consumer does).
		/// </summary>
		public event Action<OwnerHitLandedInfo> OwnerHitLanded;

		/// <summary>Raises <see cref="OwnerHitLanded"/> for a landed contact; a no-op otherwise.</summary>
		internal void RaiseOwnerHitLanded(Hurtbox target, in HitPayload payload, Vector2 position) {
			if (!payload.ContactLanded) return;
			OwnerHitLanded?.Invoke(new OwnerHitLandedInfo(target, in payload, true, position));
		}

		/// <summary>
		/// Pays a lingering-kit Momentum bonus (plan 1.2) once per event; the caller guards "once per
		/// detonation / strike / cast, never per body" and calls this only for a landed contact with
		/// damage. Reads the unit scale from <see cref="LingeringKitRules.MomentumUnitsFor"/>. No
		/// Story meter scale applies (the gauge's swing gains have none).
		/// </summary>
		public void PayLingeringMomentum(LingeringMomentumEvent kind) =>
			GainMomentumUnits(LingeringKitRules.MomentumUnitsFor(kind));

		/// <summary>
		/// The Story healing gate (plan 1.8): while Venom is active every heal is blocked except Defy's
		/// (<see cref="HealStory"/> takes <c>isDefyHeal</c>). Mobs and bosses never heal, so this is the
		/// hero and the Level 13 Mirror clone only.
		/// </summary>
		public bool IsHealingBlocked =>
			LingeringKitRules.VenomBlocksHeal(HasStatusEffect(StatusType.Venom), isDefyHeal: false);
	}
}
