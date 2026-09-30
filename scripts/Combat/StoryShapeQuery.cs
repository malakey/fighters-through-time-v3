using FTT.Core;

namespace FTT.Combat {

    /// <summary>
    /// Package 12 W8: the one collision-mask rule for Story kit deliveries that
    /// resolve through a <c>PhysicsShapeQueryParameters2D</c> straight into
    /// <see cref="Hurtbox.TakeHit"/> rather than through a <see cref="Hitbox"/>.
    ///
    /// <para>A <see cref="Hitbox"/> on a fighter slot masks
    /// <c>opposing hurtbox | PersistentObject</c>
    /// (<see cref="CollisionLayers.HitboxMaskForFighterSlot"/>), so a basic swing
    /// reaches the <c>EnvironmentHurtboxAdapter</c> strike surfaces — checkpoint
    /// fractures, Chronal Extractors, strikeable props, kit gates. Shape-query
    /// specials used to mask only the opposing hurtbox layer and so could not.
    /// <see cref="DeliveryMask"/> restores parity: the caller's opposing hurtbox
    /// layer plus <see cref="CollisionLayers.PersistentObject"/>. The caster's own
    /// <c>PlayerHurtbox</c> is never added, so a delivery can never reach its
    /// owner.</para>
    ///
    /// <para>Deliberately <b>not</b> routed through this mask (they keep the bare
    /// hurtbox layer): pure displacement queries that deal no damage (Shakespeare's
    /// storm push — a zero-damage push must not strike a
    /// checkpoint), and the three autonomous constructs (Tesla coil, Leonardo turret,
    /// serpent nest), whose queries are target <i>acquisition</i> —
    /// widening them would lock the construct onto props and its own sibling
    /// constructs.</para>
    ///
    /// <para>Gates are unchanged: <see cref="Hurtbox.TakeHit"/> carries the world
    /// hold and the Time Freeze discard for these deliveries, and every
    /// environment receiver still decides for itself what a player hit means
    /// (a checkpoint needs a player attacker; a construct ignores its owner).</para>
    /// </summary>
    public static class StoryShapeQuery {
        public static uint DeliveryMask(uint targetHurtboxLayer) =>
            targetHurtboxLayer | CollisionLayers.PersistentObject;
    }
}
