using FTT.Combat;

namespace FTT.Characters {

	/// <summary>
	/// The hero's half of the Resonance capstones (gameplay review 2026-11-03, plan GR-CAP; Story only):
	/// Mozart's Symphony No. 41 damage lane and the Momentum pip refund two capstones pay. The kits read
	/// the perks; their numbers are <see cref="CapstoneRules"/>.
	/// </summary>
	public partial class PlayerController {
		private readonly SymphonyStack _symphony = new();

		/// <summary>Symphony No. 41's stacks, 0 to 3. Test seam and presentation read.</summary>
		public int SymphonyStacks => _symphony.Stacks;

		/// <summary>A Special of <paramref name="slot"/> (1 or 2) started: stacks the lane if he owns the capstone.</summary>
		public void NoteSymphonySpecial(int slot) {
			if (HasStoryPerk(CapstoneRules.SymphonyNo41PerkKey)) _symphony.NoteSpecial(slot);
		}

		/// <summary>A factor of the Story ability-damage composition (<see cref="StoryAbilityHitboxDamage"/>): 1.0 for everyone else.</summary>
		private float CapstoneDamageLane => HasStoryPerk(CapstoneRules.SymphonyNo41PerkKey) ? _symphony.DamageScale : 1f;

		/// <summary>One tick of the capstones' clocks, beside the Momentum regen, so it shares its pauses.</summary>
		private void StepCapstones() {
			if (!HasStoryPerk(CapstoneRules.SymphonyNo41PerkKey)) {
				_symphony.Reset();
				return;
			}
			_symphony.Step();
		}

		/// <summary>A live respec removed the capstone: the stack history goes with it, so buying it back starts at none.</summary>
		internal void ClearCapstonesNotOwned() {
			if (!HasStoryPerk(CapstoneRules.SymphonyNo41PerkKey)) _symphony.Reset();
		}

		/// <summary>Steps the capstones' clocks without running the hero. Test seam.</summary>
		internal void StepCapstonesForTest(int frames) {
			for (int frame = 0; frame < frames; frame++) StepCapstones();
		}

		/// <summary>A capstone gives back the pip a Special costs (<see cref="BasicComboRules.MomentumSpecialCostUnits"/>).</summary>
		public void RefundMomentumPip() => RefundSpecialPip();
	}
}
