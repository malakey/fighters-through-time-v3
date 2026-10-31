using FTT.Combat;
using FTT.Core;
using FTT.Enemies;
using Godot;

namespace FTT.Characters {

	/// <summary>
	/// The Story Defy blast and heal (meter and Defy update 2026-10-31, plan sections 2.5 and 2.6,
	/// P7 to P10, P22, P23). On Defy frame 6: every hostile character within 2.5 units (150 px)
	/// of the Defier's body centre takes an assigned push away from it, a 30-frame non-launching
	/// stagger and an 8-frame hitstop; the Defier takes the 8-frame hitstop too and heals.
	/// Characters only (R4): projectiles, constructs, zones, props and strike surfaces are untouched.
	/// </summary>
	public partial class PlayerController {

		private static Vector2 BodyCentreOf(Node2D body) =>
			body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.GlobalPosition ?? body.GlobalPosition;

		private void ExecuteDefyBlast() {
			Vector2 centre = BodyCentreGlobalPosition;
			float radius = (float)BasicComboRules.DefyBlastRadiusUnits * KitMotionRules.StoryPixelsPerUnit;
			float radiusSquared = radius * radius;
			float pushX = BasicComboRules.DefyBlastPushXUnitsPerSecond * KitMotionRules.StoryPixelsPerUnit;
			float pushY = -BasicComboRules.DefyBlastPushYUnitsPerSecond * KitMotionRules.StoryPixelsPerUnit;
			int stagger = BasicComboRules.DefyBlastStaggerFrames;
			int hitstop = BasicComboRules.DefyBlastHitstopFrames;
			int blasted = 0;

			SceneTree tree = GetTree();
			if (tree != null) {
				if (IsStoryHostile) {
					// The Mirror clone's blast: the campaign hero is the only character it reaches.
					using var players = tree.GetNodesInGroup("StoryPlayer").AsDisposable();
					foreach (Node node in players) {
						if (node is not PlayerController hero || hero == this || hero.IsStoryHostile) continue;
						Vector2 delta = BodyCentreOf(hero) - centre;
						if (delta.LengthSquared() > radiusSquared) continue;
						if (hero.ApplyDefyBlast(new Vector2(PushSign(delta.X) * pushX, pushY), stagger, hitstop)) blasted++;
					}
				} else {
					using var enemies = tree.GetNodesInGroup("Enemies").AsDisposable();
					foreach (Node node in enemies) {
						if (node is not Node2D body || !IsInstanceValid(body)) continue;
						Vector2 delta = BodyCentreOf(body) - centre;
						if (delta.LengthSquared() > radiusSquared) continue;
						var push = new Vector2(PushSign(delta.X) * pushX, pushY);
						if (node is EnemyController mob) {
							if (mob.ApplyDefyBlast(push, stagger, hitstop)) blasted++;
						} else if (node is BossController boss) {
							if (boss.ApplyDefyBlast(push)) blasted++;
						}
					}
					using var mirror = tree.GetNodesInGroup(MirrorParadoxController.MirrorGroup).AsDisposable();
					foreach (Node node in mirror) {
						if (node is not PlayerController clone || clone == this || !clone.IsStoryHostile) continue;
						Vector2 delta = BodyCentreOf(clone) - centre;
						if (delta.LengthSquared() > radiusSquared) continue;
						if (clone.ApplyDefyBlast(new Vector2(PushSign(delta.X) * pushX, pushY), stagger, hitstop)) blasted++;
					}
				}
			}

			// The world freeze: the Defier takes the same 8 frames (its clock, so its recovery waits).
			ApplyHitstop(hitstop);
			// The heal (P10): the echo pool cashed in, or the 10 % floor.
			int healed = CashInEchoPool();
			DefyExecutionCount++;
			var payload = new DefyExecutedPayload {
				PlayerIndex = PlayerIndex,
				Position = centre,
				MeterBefore = _defyMeterBefore,
				TargetsBlasted = blasted,
				Healed = healed,
				Frame = (ulong)_storyFrame
			};
			EventBus.Instance?.RaiseDefyExecuted(payload);
			PresentDefyBlast(in payload);
		}

		// Away from the Defier; a dead-centre target is pushed the way the Defier faces.
		private float PushSign(float dx) => dx > 0f ? 1f : dx < 0f ? -1f : (IsFacingRight ? 1f : -1f);

		/// <summary>
		/// A Defy blast on this fighter (the Mirror clone for the hero's blast, the hero for the
		/// clone's). A fixed non-launching stagger with an assigned push and a hitstop: it ignores
		/// hyper-armour and Block (unblockable), and an invulnerable body (roll, its own Defy
		/// startup, post-rewind, tech lock, knockdown) is untouched. No damage, meter or Rally.
		/// </summary>
		public bool ApplyDefyBlast(Vector2 push, int staggerFrames, int hitstopFrames) {
			if (CurrentState is CharacterState.Dead or CharacterState.Respawning || CurrentHP <= 0) return false;
			if (IsDefyProtected || _rollInvulnerable || _postRewindInvulnerabilityFrames > 0
				|| _techInvulnerabilitySeconds > 0f || IsKnockedDown) return false;
			if (staggerFrames <= 0) return false;
			// ApplyStun cancels the swing, any executing cast (an A02 activation in its wind-up or
			// active window included) and a grab, and leaves the block stance for hitstun.
			ApplyStun(staggerFrames / 60f);
			_stunTumble = false;
			// The blast replaces whatever launch state the body carried (the sim clears it too): a
			// launch buffered under an earlier hit's freeze must not replay over the assigned push
			// when that freeze ends, and an owed slam bounce is dead.
			_hasPendingLaunch = false;
			_slamBouncePending = false;
			_slamBounceSpeed = 0f;
			// The stagger cannot be Block-cancelled (the sim's blast sets the same gate).
			_hitstunBlockCancelBlocked = true;
			Velocity = push;
			ApplyHitstop(hitstopFrames);
			return true;
		}
	}
}
