using FTT.Combat;
using FTT.Core;

namespace FTT.Characters {

	/// <summary>
	/// The Story Momentum gauge (meter and Defy update 2026-10-31, ledger
	/// <c>USER-MOMENTUM-GAUGE</c>, plan section 2.1): four pips shared by Special 1 and
	/// Special 2, in float pips (the sim counts integer units of
	/// <see cref="BasicComboRules.MomentumUnitsPerPip"/>). A pip is spent when a Special is
	/// accepted (<c>CheckSpecialInput</c>), a landed basic swing adds a fraction
	/// (<c>NoteSwingLanded</c>), and <c>UpdateCooldowns</c> refills it 1 pip per
	/// <see cref="BasicComboRules.MomentumRegenFramesPerPip"/> frames x
	/// <see cref="MomentumRegenMultiplier"/>, under the same pauses the cooldowns had (the hero's own
	/// hitstop, Time Freeze, the tree pause).
	/// <para><see cref="SpecialOneCooldownTimer"/> / <see cref="SpecialTwoCooldownTimer"/> keep their
	/// names and are the per-slot 2 s <b>reuse delay</b> (<see cref="BasicComboRules.SpecialReuseDelayFrames"/>),
	/// armed by <c>BaseSpecial.StartCooldown</c> and ticked in plain seconds (plan P3).</para>
	/// </summary>
	public partial class PlayerController {

		/// <summary>The F10 key under <c>StoryPlayerResourceTimers.AbilityCooldowns</c> that holds the gauge in pips (P20).</summary>
		public const string MomentumTimerKey = "momentum";

		/// <summary>Float slack so a gauge that regenerated to 0.99999 still pays for a Special.</summary>
		private const float MomentumPipEpsilon = 0.0001f;

		/// <summary>The reuse delay in seconds (the shared constant, 120 frames).</summary>
		public static float SpecialReuseDelaySeconds => BasicComboRules.SpecialReuseDelayFrames / 60f;

		/// <summary>The gauge in pips, 0 to <see cref="BasicComboRules.MomentumMaxPips"/>; 2.0 at a fresh start.</summary>
		public float MomentumPips { get; private set; } = BasicComboRules.MomentumStartPips;

		/// <summary>
		/// Sets the gauge (clamped to 0..4 pips). The save-restore path and the tests' seam.
		/// </summary>
		public void RestoreMomentumPips(float pips) {
			MomentumPips = Godot.Mathf.Clamp(pips, 0f, BasicComboRules.MomentumMaxPips);
		}

		/// <summary>True when the gauge holds a whole pip (the Special cost).</summary>
		public bool HasSpecialPip => MomentumPips >= BasicComboRules.MomentumSpecialCostUnits
			/ (float)BasicComboRules.MomentumUnitsPerPip - MomentumPipEpsilon;

		/// <summary>
		/// True when the Special in <paramref name="slot"/> is past its reuse delay and the gauge holds a
		/// pip. Other slots are never "Special ready". The Dormant overlay and the lockout are read
		/// separately.
		/// </summary>
		public bool IsSpecialReady(AbilitySlot slot) => SpecialReuseRemainingSeconds(slot) <= 0f
			&& HasSpecialPip
			&& (slot == AbilitySlot.Special1 || slot == AbilitySlot.Special2);

		/// <summary>Seconds left on the Special's reuse delay (0 when ready; 0 for a non-Special slot).</summary>
		public float SpecialReuseRemainingSeconds(AbilitySlot slot) => slot switch {
			AbilitySlot.Special1 => SpecialOneCooldownTimer > 0f ? SpecialOneCooldownTimer : 0f,
			AbilitySlot.Special2 => SpecialTwoCooldownTimer > 0f ? SpecialTwoCooldownTimer : 0f,
			_ => 0f
		};

		/// <summary>The gauge's regen multiplier: 1.0, or 1.1 with a MomentumRegen grid node (set by CharacterFactory).</summary>
		public float MomentumRegenMultiplier { get; set; } = 1f;

		/// <summary>Spends one pip for an accepted Special (never below 0).</summary>
		private void SpendSpecialPip() {
			MomentumPips = Godot.Mathf.Max(0f, MomentumPips - BasicComboRules.MomentumSpecialCostUnits
				/ (float)BasicComboRules.MomentumUnitsPerPip);
		}

		/// <summary>
		/// Spends the pip before the cast starts, so a cast that fails its own validation leaves no
		/// half-state: the pip comes back on a false return.
		/// </summary>
		private bool TryExecuteSpecialSpendingPip(BaseSpecial special) {
			SpendSpecialPip();
			if (special.TryExecute()) return true;
			RefundSpecialPip();
			return false;
		}

		/// <summary>Returns a pip a cast that failed its own validation did not use.</summary>
		private void RefundSpecialPip() {
			MomentumPips = Godot.Mathf.Min(BasicComboRules.MomentumMaxPips, MomentumPips
				+ BasicComboRules.MomentumSpecialCostUnits / (float)BasicComboRules.MomentumUnitsPerPip);
		}

		/// <summary>Adds a landed swing's gain in pips (clamped to the four-pip cap).</summary>
		private void GainMomentumUnits(int units) {
			if (units <= 0) return;
			MomentumPips = Godot.Mathf.Min(BasicComboRules.MomentumMaxPips,
				MomentumPips + units / (float)BasicComboRules.MomentumUnitsPerPip);
		}

		/// <summary>
		/// One tick of passive regen (1 pip per 12 s, no pause after a spend), called beside the
		/// cooldown ticks so it shares their pauses. Plan P3: it reads no cooldown multiplier.
		/// </summary>
		private void RegenMomentum(float dt) {
			if (MomentumPips >= BasicComboRules.MomentumMaxPips) return;
			float pipsPerSecond = 60f / BasicComboRules.MomentumRegenFramesPerPip;
			MomentumPips = Godot.Mathf.Min(BasicComboRules.MomentumMaxPips,
				MomentumPips + dt * pipsPerSecond * MomentumRegenMultiplier);
		}

		/// <summary>
		/// Scales a flat Influence-meter effect (Extractor discharge, searchlight, Siphon Snare, Shield of
		/// Orleans, the MeterBoost orb) by the D5 scale on the campaign hero only (the Mirror clone and
		/// every non-campaign build stay unscaled; never Mozart's build-rate lane, O27). A spend such as
		/// the Ultimate never goes through this.
		/// </summary>
		public float ScaleStoryFlatMeter(float amount) =>
			amount * StoryCombatRules.FlatMeterScale(AppliesStoryMeterGainScale);

		/// <summary>Writes the gauge into the F10 map (P20).</summary>
		private void CaptureMomentumTimer(System.Collections.Generic.Dictionary<string, float> cooldowns) {
			cooldowns[MomentumTimerKey] = MomentumPips;
		}

		/// <summary>
		/// Restores the gauge from the F10 map: an absent key is the start fill (2 pips, never 0 or 4),
		/// and a restored Special reuse delay never exceeds 2 s (an older save held the long cooldown).
		/// </summary>
		private void RestoreMomentumTimers(System.Collections.Generic.Dictionary<string, float> cooldowns) {
			RestoreMomentumPips(cooldowns.TryGetValue(MomentumTimerKey, out float pips)
				? pips : BasicComboRules.MomentumStartPips);
			SpecialOneCooldownTimer = Godot.Mathf.Min(SpecialOneCooldownTimer, SpecialReuseDelaySeconds);
			SpecialTwoCooldownTimer = Godot.Mathf.Min(SpecialTwoCooldownTimer, SpecialReuseDelaySeconds);
		}
	}
}
