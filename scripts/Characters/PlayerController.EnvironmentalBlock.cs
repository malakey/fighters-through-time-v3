using Godot;

namespace FTT.Characters {

	/// <summary>
	/// CM-BLOCK (combat and movement update 2026-10-16, decision 7.4; ledger
	/// <c>VERIFY-STORY-HAZARD-BLOCK</c>, closed, and
	/// <c>PROVISIONAL-STORY-HAZARD-BLOCK-SOURCES</c>): a raised, front-facing block
	/// absorbs a Story hazard tick for one charge, as the sim's D03c already does.
	///
	/// <para><b>Order</b> (D01/D02b): hit eligibility and invulnerability (since
	/// CM-FIXB-STORY: dead or respawning, post-rewind protection, the roll, the
	/// landing tech, the knockdown), then the D04 Defy window, then Temporal Aegis and
	/// the HP barrier, then — for a blockable source, with the hero in Blocking,
	/// the origin in front and a charge left — the ordinary block through
	/// <c>BlockSystem.ResolveHit</c> as a Hazard-class hit (one charge, shieldstun
	/// scaled by the tick's damage, the perfect block applying, no hitstop, no
	/// attacker), then HP through the existing environmental pipeline. A block
	/// deals no HP and gives no Rally echo or meter. Henry's Bastion grants on a
	/// hazard block; Shield of Orléans does not.</para>
	///
	/// <para><b>Sources.</b> Blockable: <c>StoryCyclicHazard</c>, the Chronal
	/// Extractor discharge (its meter drain still applies), the searchlight
	/// strike, the spikes and the Collapse Tremor's falling debris. Unblockable
	/// (penalties, not hits): drowning, Level 7's haul-out, the Chronal Rift's
	/// snap, the escape catch and Level 0's scripted kill keep
	/// <see cref="ApplyEnvironmentalDamage"/>; <c>StageHazard</c> is in no scene
	/// and stays unblockable. A blockable source skips its knockback when the
	/// result is <see cref="EnvironmentalHitResult.Blocked"/>.</para>
	/// </summary>
	public partial class PlayerController {

		/// <summary>How one environmental hit resolved.</summary>
		public enum EnvironmentalHitResult {
			/// <summary>It reached the HP pipeline (an invulnerable body may still have taken nothing).</summary>
			Applied = 0,
			/// <summary>A raised front-facing block absorbed it (a shatter included).</summary>
			Blocked = 1,
			/// <summary>An invulnerable body refused it, or the D04 Defy window, Temporal Aegis or the barrier prevented it.</summary>
			Protected = 2
		}

		/// <summary>The environmental hit, without the damage it applied.</summary>
		public EnvironmentalHitResult ApplyEnvironmentalHit(
			int damage, Vector2 hitOrigin, bool blockable, bool appliesHitstop = false) =>
			ApplyEnvironmentalHit(damage, hitOrigin, blockable, out _, appliesHitstop);

		/// <summary>
		/// The one entry point for a Story hazard that a block may answer.
		/// <paramref name="hitOrigin"/> is the source's position: the front-facing
		/// test reads its X. <paramref name="damageApplied"/> is the HP lost (0 on a
		/// block or a protection).
		/// </summary>
		public EnvironmentalHitResult ApplyEnvironmentalHit(
			int damage, Vector2 hitOrigin, bool blockable, out int damageApplied, bool appliesHitstop = false) {
			damageApplied = 0;
			// CM-FIXB-STORY (findings B7 / B21; D01: hit eligibility and
			// invulnerability first): a body the HP pipeline would refuse is
			// refused here, above every layer, so a post-rewind-protected,
			// tech-locked, downed or rolling hero spends no Aegis, barrier or block
			// charge, takes no shieldstun and cannot shatter on a tick it is immune
			// to (the sim's ResolveFighterHit refuses an invulnerable fighter
			// first). Callers treat it like any unblocked result.
			if (RefusesEnvironmentalHit) return EnvironmentalHitResult.Protected;
			// D04: the protected window covers damaging stage hazards too.
			if (IsDefyProtected) return EnvironmentalHitResult.Protected;
			// D02b: Aegis and the barrier come before the block.
			int remaining = ResolveProtectionLayers(damage);
			if (damage > 0 && remaining <= 0) return EnvironmentalHitResult.Protected;
			if (blockable && remaining > 0 && TryBlockEnvironmentalHit(remaining, hitOrigin)) {
				return EnvironmentalHitResult.Blocked;
			}
			damageApplied = ApplyEnvironmentalRemainder(remaining, appliesHitstop, periodicTick: false);
			return EnvironmentalHitResult.Applied;
		}

		/// <summary>
		/// The refusals <see cref="ApplyDamage(int)"/> makes below the layers —
		/// dead or respawning, post-rewind protection, roll invulnerability, the
		/// landing tech and the knockdown — read once, before them.
		/// </summary>
		private bool RefusesEnvironmentalHit =>
			CurrentState is CharacterState.Dead or CharacterState.Respawning
			|| _rollInvulnerable
			|| IsConfirmInvulnerable;

		/// <summary>
		/// The block layer: the ordinary Story block, fed a Hazard-class payload
		/// with no attacker (index −1) and no hitstop.
		/// </summary>
		private bool TryBlockEnvironmentalHit(int damage, Vector2 hitOrigin) {
			if (CurrentState != CharacterState.Blocking || _blockSystem == null) return false;
			var hazard = new FTT.Combat.HitPayload {
				AttackerIndex = -1,
				TargetIndex = PlayerIndex,
				AttackID = "environment",
				HitboxID = "hazard",
				AttackClass = FTT.Combat.AttackClass.Hazard,
				Damage = damage,
				Knockback = Vector2.Zero,
				HitstunDuration = 0f,
				HitOrigin = hitOrigin,
				AttackerFacingRight = hitOrigin.X <= GlobalPosition.X,
				AppliedStatus = FTT.Core.StatusType.None,
				ExemptFromHitstop = true,
				Delivery = FTT.Combat.HitDelivery.Hazard,
				Origin = FTT.Combat.HitOrigin.Environment
			};
			FTT.Combat.BlockResult result = _blockSystem.ResolveHit(hazard);
			CurrentBlockCharges = _blockSystem.CurrentCharges;
			if (result == FTT.Combat.BlockResult.NotBlocked) return false;
			FTT.Core.CameraShake.Instance?.Shake(3f, 0.08f);
			if (result == FTT.Combat.BlockResult.Blocked) {
				FTT.Core.HapticFeedbackManager.Instance?.OnGuardImpact(PlayerIndex);
			}
			return true;
		}
	}
}
