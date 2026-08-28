using FTT.Core;
using GdUnit4;
using static GdUnit4.Assertions;

namespace FTT.Tests.Unit;

[TestSuite]
public class CollisionLayerTests {
    [TestCase]
    public void LayerBitsAreUniqueAndFollowTheDesignOrder() {
        uint[] layers = {
            CollisionLayers.Player, CollisionLayers.Enemy,
            CollisionLayers.PlayerHitbox, CollisionLayers.EnemyHitbox,
            CollisionLayers.PlayerHurtbox, CollisionLayers.EnemyHurtbox,
            CollisionLayers.Environment, CollisionLayers.OneWayPlatform,
            CollisionLayers.Trigger, CollisionLayers.PersistentObject,
            CollisionLayers.Projectile
        };

        for (int index = 0; index < layers.Length; index++) {
            AssertThat(layers[index]).IsEqual(1u << index);
        }
    }

    [TestCase]
    public void FriendlyHitboxesAreExcludedFromTheirOwnHurtboxes() {
        AssertThat((CollisionLayers.PlayerHitboxMask & CollisionLayers.PlayerHurtbox) == 0).IsTrue();
        AssertThat((CollisionLayers.EnemyHitboxMask & CollisionLayers.EnemyHurtbox) == 0).IsTrue();
        AssertThat((CollisionLayers.PlayerHitboxMask & CollisionLayers.EnemyHurtbox) != 0).IsTrue();
        AssertThat((CollisionLayers.EnemyHitboxMask & CollisionLayers.PlayerHurtbox) != 0).IsTrue();
    }

    [TestCase]
    public void TheStasisEchoBodyLayerReachesTheAuthoredPressurePlateMask() {
        // V7.3: the Echo's body carries Environment (the player stands on
        // their past self) plus PersistentObject, so the plate template's
        // authored mask (Player | PersistentObject = 513) physically receives
        // it, and the searchlight occlusion ray (PersistentObject bodies)
        // can find it. Environment stays so it keeps behaving as terrain.
        AssertThat(FTT.Environment.StasisEcho.BodyLayer)
            .IsEqual(CollisionLayers.Environment | CollisionLayers.PersistentObject);
        const uint plateTemplateMask = CollisionLayers.Player | CollisionLayers.PersistentObject;
        AssertThat((FTT.Environment.StasisEcho.BodyLayer & plateTemplateMask) != 0)
            .OverrideFailureMessage("The plate's authored mask must receive the Echo body.")
            .IsTrue();
        // And the Echo must NOT sit on any hitbox/hurtbox layer — plates and
        // rays receive it, the combat pipeline never does.
        const uint combatLayers = CollisionLayers.PlayerHitbox | CollisionLayers.EnemyHitbox
            | CollisionLayers.PlayerHurtbox | CollisionLayers.EnemyHurtbox;
        AssertThat(FTT.Environment.StasisEcho.BodyLayer & combatLayers).IsEqual(0u);
    }
}
