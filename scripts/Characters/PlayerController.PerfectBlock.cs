namespace FTT.Characters {

	/// <summary>
	/// CM-BLOCK (combat and movement update 2026-10-16, decision 3.4; findings
	/// PFC-07, FGS-06, CMB-13; ledger <c>USER-PERFECT-BLOCK</c> /
	/// <c>PROVISIONAL-PERFECT-BLOCK-RULES</c>): the Story half of the perfect
	/// block. A block raised within <see cref="FTT.Combat.BasicComboRules.PerfectBlockWindowFrames"/>
	/// (5) frames before a hit spends no charge and sets no shieldstun; there is no
	/// reflect and no attacker stun, so Foresight stays the true counter. The
	/// shared numbers and both predicates live in <see cref="FTT.Combat.BasicComboRules"/>,
	/// which the Fighter sim's <c>FighterBlockTimingRules</c> reads too.
	///
	/// <para><b>Arming.</b> <see cref="CheckBlockInput"/> enters Blocking from
	/// another state; when Block is pressed on that tick (a Toggle Block
	/// toggle-on is a press) and the stance had been down at least
	/// <see cref="FTT.Combat.BasicComboRules.PerfectBlockRearmFrames"/> (20) frames
	/// or never raised, the window is owed. Story's stance is live one tick after
	/// the press (<see cref="ProcessBlocking"/> re-asserts
	/// <c>BlockSystem.StartBlock</c>), so the window opens there, on the first live
	/// stance tick: contact ages 0–4 from that tick are perfect, as in the sim.
	/// The hit-2 Block escape enters Blocking from <c>ProcessStunned</c>, never
	/// through <see cref="CheckBlockInput"/>, so it never arms; neither does a
	/// stance held through shieldstun.</para>
	///
	/// <para><b>The clock.</b> <see cref="TickPerfectBlock"/> runs once per tick
	/// inside <c>_PhysicsProcess</c>'s Time Freeze gate, after the hitstop gate,
	/// so the fighter's own hitstop and a Time Freeze both pause the window and
	/// the lowered age. It observes the stance as the previous tick left it (the
	/// state at the start of this tick), so a stance dropped by a hit in the last
	/// physics flush counts as dropped.</para>
	///
	/// <para><b>Effect.</b> <c>BlockSystem.ResolveHit</c> reads
	/// <see cref="IsPerfectBlockWindowOpen"/>: inside the window a contact the
	/// ordinary block would absorb spends nothing, sets no shieldstun and still
	/// returns <c>Blocked</c> (so the D8 Punish window opens and Henry's Bastion
	/// grants); a Shield-Breaker still shatters (Q3) and a Guard-Crush keeps its
	/// two-charge cost. Shield of Orléans does not pay for a perfect block (its
	/// design needs a consumed charge).</para>
	/// </summary>
	public partial class PlayerController {

		private int _perfectBlockFrames;
		/// <summary>Frames since the stance last dropped; 0 = never raised (this controller's lifetime).</summary>
		private int _blockLoweredFrames;
		/// <summary>The stance (Blocking) as the last <see cref="TickPerfectBlock"/> saw it.</summary>
		private bool _blockStanceSeen;
		/// <summary>The entry press qualified; the window opens on the first live stance tick.</summary>
		private bool _perfectBlockArmPending;

		/// <summary>True while a contact the ordinary block absorbs would be perfect-blocked.</summary>
		public bool IsPerfectBlockWindowOpen => _perfectBlockFrames > 0;

		/// <summary>Perfect-block frames left (test and presentation seam).</summary>
		public int PerfectBlockFramesRemaining => _perfectBlockFrames;

		/// <summary>Perfect blocks resolved by this controller (presentation and test seam; never a rule input).</summary>
		public int PerfectBlockCount { get; private set; }

		/// <summary>
		/// One tick of the perfect-block clock, from the Time Freeze-gated counter
		/// block of <c>_PhysicsProcess</c>. The window counts down; a stance that
		/// has dropped clears it and restarts the lowered age, which otherwise
		/// grows (saturating at 255; 0 stays "never raised").
		/// </summary>
		private void TickPerfectBlock() {
			if (_perfectBlockFrames > 0) _perfectBlockFrames--;
			if (CurrentState == CharacterState.Blocking) {
				_blockStanceSeen = true;
				return;
			}
			_perfectBlockArmPending = false;
			if (_blockStanceSeen) {
				_blockStanceSeen = false;
				_perfectBlockFrames = 0;
				_blockLoweredFrames = 1;
			} else if (_blockLoweredFrames > 0 && _blockLoweredFrames < 255) {
				_blockLoweredFrames++;
			}
		}

		/// <summary>
		/// <see cref="CheckBlockInput"/> has just entered Blocking from another
		/// state: the window is owed when Block was pressed this tick and the
		/// rearm guard allows it.
		/// </summary>
		private void NoteBlockStanceRaised() {
			_perfectBlockArmPending = FTT.Combat.BasicComboRules.PerfectBlockArms(
				stanceRose: true,
				blockPressed: CurrentInputFrame.IsPressed(FTT.Core.GameplayButtons.Block),
				_blockLoweredFrames);
		}

		/// <summary>
		/// <see cref="ProcessBlocking"/>, right after <c>StartBlock</c>: the first
		/// live stance tick opens an owed window.
		/// </summary>
		private void OpenOwedPerfectBlockWindow() {
			if (!_perfectBlockArmPending) return;
			_perfectBlockArmPending = false;
			if (_blockSystem != null && !_blockSystem.IsBlocking) return;
			_perfectBlockFrames = FTT.Combat.BasicComboRules.PerfectBlockWindowFrames;
		}

		/// <summary>
		/// <c>BlockSystem.ResolveHit</c>'s record of one perfect-blocked contact:
		/// the count, and the placeholder flash through the glow arbiter (a bright,
		/// brief tint on the hit-flash channel — the arbiter has no outline flash).
		/// Safe inside a physics flush: it writes no physics state.
		/// </summary>
		public void NotePerfectBlock() {
			PerfectBlockCount++;
			_glow?.FlashHit(FTT.Combat.BlockSystem.PerfectBlockFlashColor, FTT.Combat.BlockSystem.PerfectBlockFlashSeconds);
		}
	}
}
