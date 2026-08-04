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
}
