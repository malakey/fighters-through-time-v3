using Godot;

namespace FTT.Characters {

	/// <summary>
	/// GR-AUDIO (gameplay review ruling 13, 2026-11-03): the Story half of the small combat
	/// sound set. Presentation only: every method here ends in
	/// <c>AudioManager.CoreCues.Play</c>, which owns the catalog row, the -12 dB volume, the
	/// per-row throttle and the player's separate placeholder mute; nothing is read back into
	/// the game. Fighter Mode raises the same cues from <c>FighterSimulationDriver</c> from
	/// sim state, never from here (its proxies do not run the Story swing code).
	/// </summary>
	public partial class PlayerController {
		/// <summary>The current swing's active window has seen at least one contact (landed, blocked, absorbed or refused).</summary>
		private bool _swingContacted;

		private static void PlayCombatCue(string cueID) =>
			FTT.Core.AudioManager.Instance?.CoreCues?.Play(cueID);

		/// <summary>An active window opened: nothing has been touched yet.</summary>
		private void BeginSwingContactWatch() => _swingContacted = false;

		/// <summary>
		/// A basic hitbox reached a receiver (<c>Hitbox.HitConfirmed</c> fires for every contact,
		/// a fully absorbed or refused one included), so the swing is no whiff. A landed contact
		/// plays the hit thump once per contact (the row's throttle thins a cleave).
		/// </summary>
		private void NoteSwingContact(in FTT.Combat.HitPayload payload) {
			_swingContacted = true;
			if (payload.ContactLanded) PlayCombatCue(FTT.Core.CoreMechanicCueIDs.HitBasic);
		}

		/// <summary>
		/// A swing's active window ran out on its own (not a cancel): with no contact it was a
		/// whiff, and the air sweep plays once.
		/// </summary>
		private void NoteActiveWindowClosed() {
			if (!_swingContacted) PlayCombatCue(FTT.Core.CoreMechanicCueIDs.SwingWhiff);
			_swingContacted = true; // once per window
		}

		/// <summary>A landed ability direct hit (BaseSpecial.ConfirmAbilityHit) plays the same thump.</summary>
		public static void PlayAbilityHitCue() => PlayCombatCue(FTT.Core.CoreMechanicCueIDs.HitBasic);

		/// <summary>The grab connected (Story's catch).</summary>
		private static void PlayGrabCatchCue() => PlayCombatCue(FTT.Core.CoreMechanicCueIDs.GrabCatch);

		/// <summary>An ordinary block, perfect block or guard break (ShowBlockSpark's kind).</summary>
		private static void PlayBlockCue(FTT.Combat.BlockSparkKind kind) =>
			PlayCombatCue(kind == FTT.Combat.BlockSparkKind.Perfect
				? FTT.Core.CoreMechanicCueIDs.PerfectBlock
				: FTT.Core.CoreMechanicCueIDs.Block);
	}
}
