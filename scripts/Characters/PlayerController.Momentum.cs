using FTT.Combat;
using FTT.Core;

namespace FTT.Characters {

	/// <summary>
	/// The Story Momentum gauge (meter and Defy update 2026-10-31, ledger
	/// <c>USER-MOMENTUM-GAUGE</c>, plan section 2.1): four pips shared by Special 1 and
	/// Special 2, in float pips (the sim counts integer units). <b>MM-RULES declares the
	/// interface inert</b>: the gauge sits at its start fill, nothing spends or refills it
	/// and <see cref="IsSpecialReady"/> still reads only the 2 s reuse-delay timers
	/// (<see cref="SpecialOneCooldownTimer"/> / <see cref="SpecialTwoCooldownTimer"/>, kept
	/// by name and re-documented as the reuse delays). MM-SMOM owns this file and gives
	/// the members their behaviour.
	/// </summary>
	public partial class PlayerController {

		/// <summary>The gauge in pips, 0 to <see cref="BasicComboRules.MomentumMaxPips"/>; 2.0 at a fresh start.</summary>
		public float MomentumPips { get; private set; } = BasicComboRules.MomentumStartPips;

		/// <summary>
		/// Sets the gauge (clamped to 0..4 pips). The save-restore path and the tests' seam.
		/// </summary>
		public void RestoreMomentumPips(float pips) {
			MomentumPips = Godot.Mathf.Clamp(pips, 0f, BasicComboRules.MomentumMaxPips);
		}

		/// <summary>
		/// True when the Special in <paramref name="slot"/> is past its reuse delay. Inert stub:
		/// the pip check joins in MM-SMOM. Other slots are never "Special ready".
		/// </summary>
		public bool IsSpecialReady(AbilitySlot slot) => SpecialReuseRemainingSeconds(slot) <= 0f
			&& (slot == AbilitySlot.Special1 || slot == AbilitySlot.Special2);

		/// <summary>Seconds left on the Special's reuse delay (0 when ready; 0 for a non-Special slot).</summary>
		public float SpecialReuseRemainingSeconds(AbilitySlot slot) => slot switch {
			AbilitySlot.Special1 => SpecialOneCooldownTimer > 0f ? SpecialOneCooldownTimer : 0f,
			AbilitySlot.Special2 => SpecialTwoCooldownTimer > 0f ? SpecialTwoCooldownTimer : 0f,
			_ => 0f
		};

		/// <summary>The gauge's regen multiplier: 1.0, or 1.1 with a MomentumRegen grid node (MM-SMOM).</summary>
		public float MomentumRegenMultiplier => 1f;
	}
}
