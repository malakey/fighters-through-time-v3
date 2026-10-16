namespace FTT.Characters {

	/// <summary>
	/// CM-STORY (combat and movement update 2026-10-16): the Story combat-feel
	/// members that live on the hero's controller — decision 7.1's player-side
	/// shake and decision 7.3's Story Echo Step cost. Story only; nothing in the
	/// Fighter sim reads either.
	/// </summary>
	public partial class PlayerController {

		/// <summary>
		/// 7.3 (findings CMB-16, TWO-10): the meter this body's Echo Step costs —
		/// 30 × 0.35 = 10.5 on the campaign hero, who earns meter at the D5 Story
		/// scale (<see cref="AppliesStoryMeterGainScale"/>), and the cross-mode 30
		/// on every other build: the Level 13 Mirror clone (its CPU values the step
		/// at 30), the drills, the Holodeck and the bots' unscaled A/B.
		/// <see cref="TryStartEchoStep"/> checks and drains this one value.
		/// </summary>
		public float EchoStepMeterCost =>
			FTT.Combat.StoryCombatRules.EchoStepMeterCost(AppliesStoryMeterGainScale);

		/// <summary>
		/// 7.1 (finding CMB-02): a player-sourced hit on this body — the Level 13
		/// Mirror clone struck by the hero, or the hero struck by the clone — shakes
		/// the camera by the same dealt-hit rule the mobs and bosses use
		/// (<see cref="FTT.Combat.StoryEnemyCombatRules.DealtHitShakeIntensity(FTT.Combat.AttackClass, FTT.Combat.HitOrigin, float, int, bool)"/>):
		/// a non-Ultimate Special takes the larger of its authored shake and the
		/// damage curve, an Ultimate its authored value, a basic the curve. Enemy
		/// hits on the hero (no attacker slot) keep the values passed in.
		/// </summary>
		private static void ResolvePlayerDealtShake(
			in FTT.Combat.HitPayload hit, int damageApplied, ref float intensity, ref float duration) {
			if (hit.AttackerIndex < 0) return;
			intensity = FTT.Combat.StoryEnemyCombatRules.DealtHitShakeIntensity(
				hit.AttackClass, hit.Origin, hit.ScreenShakeIntensity, damageApplied, hit.Launches);
			duration = FTT.Combat.StoryEnemyCombatRules.DealtHitShakeDuration(
				hit.AttackClass, hit.Origin, hit.ScreenShakeIntensity, hit.ScreenShakeDuration, hit.Launches);
		}
	}
}
