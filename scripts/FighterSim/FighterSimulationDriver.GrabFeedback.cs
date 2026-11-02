using FTT.Characters;
using Godot;

namespace FTT.FighterSim {

    // Grab pass 2026-10-31 (step A): the Fighter half of the grab feedback.
    //  * The grip spark at the catch.
    //  * A distinct HELD pose for the victim and THROWN pose for the thrown one.
    // Presentation only: reads component 310 and the synced proxy, writes a pooled
    // node and the proxy's Position/Rotation. Never writes simulation state.
    //
    // The catch is identified by component 310's GrabConnectFrame (the sim tick + 1
    // of the connect, 0 = no catch). A catch is presented once per identity per
    // grabber while the grabber holds or throws, so a rollback correction that
    // removes and re-adds the same catch never replays it, and a catch that only
    // appears after a correction (a predicted whiff made real) still sparks.
    // Limit: a correction that moves a catch to a different tick shows it twice,
    // and only the last presented identity per grabber is remembered.
    public partial class FighterSimulationDriver {

        public const float GrabSparkScale = 1f;
        /// <summary>Catch spark height above the grabber's feet (pixels).</summary>
        public const float GrabSparkRisePixels = 50f;
        /// <summary>Catch spark offset in front of the grabber (pixels): between the pair.</summary>
        public const float GrabSparkForwardPixels = 36f;
        /// <summary>The held victim is lifted this many pixels off the floor.</summary>
        public const float HeldLiftPixels = 6f;
        /// <summary>The held victim leans toward the grabber by this many radians.</summary>
        public const float HeldLeanRadians = 0.12f;
        /// <summary>The hitstun sheet's first frame is the held pose, its last the thrown pose.</summary>
        public const int HeldPoseFrame = 0;
        public const int ThrownPoseFrame = 2;
        /// <summary>The grabber holds the grab sheet's last frame through the hold.</summary>
        public const int GrabHoldPoseFrame = 2;

        /// <summary>Last presented catch identity per grabber; 0 = none. A new driver starts clean.</summary>
        private readonly int[] _lastPresentedCatch = { 0, 0 };

        /// <summary>
        /// True when a grabber in the hold or throw phase carries a catch identity
        /// not yet presented. Pure.
        /// </summary>
        public static bool ShouldPresentCatch(int grabPhase, int connectFrame, int lastPresentedConnectFrame) =>
            (grabPhase == FighterGrabRules.PhaseHolding || grabPhase == FighterGrabRules.PhaseThrowAnimation)
            && connectFrame > 0 && connectFrame != lastPresentedConnectFrame;

        /// <summary>
        /// The side the grabber stands on relative to the victim, -1 / +1 (0 when
        /// level). Pure; the lean never reads the victim's facing.
        /// </summary>
        public static float TowardGrabberSign(float victimX, float grabberX) =>
            grabberX > victimX ? 1f : grabberX < victimX ? -1f : 0f;

        /// <summary>
        /// The lean of a thrown victim toward its travel, in radians (clockwise
        /// positive on screen): tilt from the vertical by atan2(vx, max(|vy|, 120)),
        /// clamped to +-1. Pure.
        /// </summary>
        public static float ThrownLeanRadians(float velocityXPixels, float velocityYPixels) {
            float lean = Mathf.Atan2(velocityXPixels, Mathf.Max(Mathf.Abs(velocityYPixels), 120f));
            return Mathf.Clamp(lean, -1f, 1f);
        }

        /// <summary>The victim is mid-throw launch: thrown, airborne or tumbling, still inside the re-grab immunity.</summary>
        public static bool IsThrownVictim(in FighterVerbComponent verb, in FighterStateComponent state) =>
            verb.BeingHeld == 0 && verb.ThrowImmunityFrames > 0 && state.HitstunFrames > 0
            && (state.IsGrounded == 0 || FighterVerbRules.IsTumbling(in verb));

        /// <summary>The hitstun frame a held or thrown victim is posed on, or -1 for neither.</summary>
        internal static int GrabVictimPoseFrame(in FighterVerbComponent verb, in FighterStateComponent state) =>
            verb.BeingHeld != 0 ? HeldPoseFrame : IsThrownVictim(in verb, in state) ? ThrownPoseFrame : -1;

        private void SyncGrabFeedback(PlayerController player, int playerID, in FighterStateComponent state) {
            if (player == null) return;
            TryGetVerb(playerID, out FighterVerbComponent verb);

            // Spark at the catch, from the grabber's side.
            if (ShouldPresentCatch(verb.GrabPhase, verb.GrabConnectFrame, _lastPresentedCatch[playerID])) {
                _lastPresentedCatch[playerID] = verb.GrabConnectFrame;
                float facing = player.IsFacingRight ? 1f : -1f;
                FTT.Combat.GrabSparkEffect.Show(
                    player.GlobalPosition + new Vector2(facing * GrabSparkForwardPixels, -GrabSparkRisePixels),
                    player.IsFacingRight, GetParent(), GrabSparkScale);
            }

            // Held / thrown poses. Rotation is rewritten every sync so it always resets.
            player.Rotation = 0f;
            if (verb.BeingHeld != 0) {
                // Lifted a few pixels, leaning toward the grabber's actual side (a rear
                // catch leaves the victim's facing unchanged, so facing says nothing).
                float towardGrabber = player.IsFacingRight ? -1f : 1f;
                if (verb.GrabPartnerPlayerID >= 0 && verb.GrabPartnerPlayerID <= 1
                    && Simulation.TryGetFighter(playerID, out FighterStateComponent self)
                    && Simulation.TryGetFighter(verb.GrabPartnerPlayerID, out FighterStateComponent partner)) {
                    float sign = TowardGrabberSign(self.Position.x.ToFloat(), partner.Position.x.ToFloat());
                    if (sign != 0f) towardGrabber = sign;
                }
                player.Position += new Vector2(0f, -HeldLiftPixels);
                player.Rotation = towardGrabber * HeldLeanRadians;
            } else if (IsThrownVictim(in verb, in state)) {
                player.Rotation = ThrownLeanRadians(player.Velocity.X, player.Velocity.Y);
            }
        }
    }
}
