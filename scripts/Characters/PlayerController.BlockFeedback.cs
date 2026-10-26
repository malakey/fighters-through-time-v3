using Godot;

namespace FTT.Characters {

	/// <summary>
	/// FU-BLOCK (feel and UI update 2026-10-25; ledger <c>USER-BLOCK-SUCCESS-EFFECT</c>):
	/// the Story half of the block-success effect. The block branch of
	/// <c>ResolveIncomingHit</c> and the hazard block path call
	/// <see cref="ShowBlockSpark"/> with the <see cref="FTT.Combat.BlockResult"/>;
	/// a pooled <see cref="FTT.Combat.BlockSparkEffect"/> appears at the
	/// blocker's front at chest height, under the hero's own parent (the level),
	/// and announces an ordinary block, a perfect block or a guard break.
	/// Presentation only; the Level 13 Mirror clone is a PlayerController and
	/// shows it too.
	/// </summary>
	public partial class PlayerController {
		/// <summary>How far in front of the hero's origin the effect appears, in pixels.</summary>
		public const float BlockSparkForwardPixels = 28f;
		/// <summary>How far above the hero's feet (its origin) the effect appears, in pixels.</summary>
		public const float BlockSparkRisePixels = 42f;

		private void ShowBlockSpark(FTT.Combat.BlockResult result) {
			if (result == FTT.Combat.BlockResult.NotBlocked) return;
			FTT.Combat.BlockSparkKind kind = result == FTT.Combat.BlockResult.GuardBroken
				? FTT.Combat.BlockSparkKind.Shatter
				: _blockSystem != null && _blockSystem.LastBlockWasPerfect
					? FTT.Combat.BlockSparkKind.Perfect
					: FTT.Combat.BlockSparkKind.Ordinary;
			float facing = IsFacingRight ? 1f : -1f;
			FTT.Combat.BlockSparkEffect.Show(
				kind,
				GlobalPosition + new Vector2(facing * BlockSparkForwardPixels, -BlockSparkRisePixels),
				IsFacingRight,
				GetParent());
		}
	}
}
