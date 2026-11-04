using FTT.Characters;
using Godot;

namespace FTT.Combat {

    /// <summary>
    /// The Story half of the lingering-kit pass's construct strikes (plan 1.3, 1.4):
    /// a <b>synced strike</b> is the strong hit a Tesla coil, Leonardo turret or Cleopatra nest
    /// fires when its owner's own hit lands on a target in the construct's reach, and the
    /// <b>Lorentz discharge</b> is the coil burst Tesla's Pulse cashes out. Both are
    /// <see cref="HitDelivery.Construct"/> payloads carrying <see cref="HitPayload.CountsAsCombo"/>.
    /// <para>Pure helpers only; the nodes own the charges. Nothing under
    /// <c>scripts/FighterSim/</c> reads this (the sim has its own
    /// <c>FighterLingeringKitRules</c>).</para>
    /// </summary>
    public static class ConstructStrikes {
        public const string CoilSyncedArcHitboxID = "coil_synced_arc";
        public const string TurretSyncedBoltHitboxID = "turret_synced_bolt";
        /// <summary>A Codex Atlanticus bolt after its first contact: still a strike, but it pays no second Momentum.</summary>
        public const string TurretPiercedBoltHitboxID = "turret_pierced_bolt";
        public const string NestSyncedLungeHitboxID = "nest_synced_lunge";
        public const string CoilDischargeHitboxID = "coil_discharge";

        /// <summary>
        /// A construct strike or discharge: <c>CountsAsCombo</c> on a Construct delivery. The
        /// one test the victims key their exemptions on (S2 chain proration, standard poise,
        /// the max-hitstun rule) so a plain construct tick or a Special's own marker never match.
        /// </summary>
        public static bool IsStrike(in HitPayload payload) =>
            payload.CountsAsCombo && payload.Delivery == HitDelivery.Construct;

        /// <summary>
        /// Review round 1 (finding 3): every construct-delivered hit (a synced strike, a discharge, a
        /// construct's OWN attack, a fence tick) only ever LENGTHENS a stun the victim already has: it
        /// never shortens or cancels a hold, hitstun, launch or tumble. The sim's twin is
        /// <c>preserveStun: true</c>.
        /// </summary>
        public static bool KeepsCurrentStun(in HitPayload payload) => payload.Delivery == HitDelivery.Construct;

        /// <summary>A synced strike (pays the 0.05-pip Momentum bonus), as opposed to the discharge (paid once per Pulse).</summary>
        public static bool IsSyncedStrike(in HitPayload payload) =>
            IsStrike(in payload)
            && (payload.HitboxID == CoilSyncedArcHitboxID
                || payload.HitboxID == TurretSyncedBoltHitboxID
                || payload.HitboxID == NestSyncedLungeHitboxID);

        /// <summary>The synced reach: a 2D circle (plan 1.6) between a construct and the struck hurtbox.</summary>
        public static bool InReach(Vector2 construct, Vector2 target, double reachUnits, float rangeMultiplier = 1f) {
            double reachPixels = reachUnits * KitMotionRules.StoryPixelsPerUnit * rangeMultiplier;
            return construct.DistanceSquaredTo(target) <= reachPixels * reachPixels;
        }

        /// <summary>
        /// Delivers a stamped strike payload to <paramref name="target"/>: the hit, the D1a stamp
        /// from the receiver, the owner's Influence credit (Construct delivery, so no Rally), and the
        /// owner's seam (<see cref="PlayerController.RaiseOwnerHitLanded"/>), which pays the
        /// synced-strike Momentum once. Returns whether the contact landed.
        /// </summary>
        public static bool Deliver(PlayerController owner, Hurtbox target, in HitPayload payload, out float dealt) {
            dealt = 0f;
            if (target == null || !GodotObject.IsInstanceValid(target)) return false;
            dealt = target.TakeHit(payload);
            HitPayload landed = payload;
            landed.ContactLanded = target.DidLand(payload.ContactId);
            if (owner != null && GodotObject.IsInstanceValid(owner)) {
                BaseSpecial.CreditDealt(owner, in landed, dealt);
                owner.RaiseOwnerHitLanded(target, in landed, target.GlobalPosition);
            }
            return landed.ContactLanded;
        }
    }
}
