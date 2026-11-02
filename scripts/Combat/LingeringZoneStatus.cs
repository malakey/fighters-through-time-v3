using Godot;

namespace FTT.Combat {

	/// <summary>
	/// Lingering-kit pass (KL-SZONE): applies a hitless status to a Story body found under a hurtbox
	/// or as a pulled body. <see cref="IStatusEffectTarget"/> is implemented by the player only, so a
	/// mob and a boss are named here; the status rules (stronger-wins, the boss slow floor) live in
	/// each controller's own <c>ApplyStatusEffect</c>.
	/// </summary>
	public static class LingeringZoneStatus {

		/// <summary>The body a hurtbox belongs to, walking up its parents; null when none.</summary>
		public static Node FindStatusNode(Node from) {
			Node current = from;
			while (current != null) {
				if (current is IStatusEffectTarget || current is FTT.Enemies.EnemyController
					|| current is FTT.Enemies.BossController) return current;
				current = current.GetParent();
			}
			return null;
		}

		/// <summary>Applies the status to the player, mob or boss <paramref name="node"/> is; false otherwise.</summary>
		public static bool TryApply(Node node, FTT.Core.StatusType status, float seconds, float intensity) {
			switch (node) {
				case IStatusEffectTarget target:
					target.ApplyStatusEffect(status, seconds, intensity);
					return true;
				case FTT.Enemies.EnemyController enemy:
					enemy.ApplyStatusEffect(status, seconds, intensity);
					return true;
				case FTT.Enemies.BossController boss:
					boss.ApplyStatusEffect(status, seconds, intensity);
					return true;
			}
			return false;
		}
	}
}
