namespace FTT.Core {

    /// <summary>Authoritative Godot 2D physics layer bits from design-godot.md Section 4.</summary>
    public static class CollisionLayers {
        public const uint Player = 1u << 0;
        public const uint Enemy = 1u << 1;
        public const uint PlayerHitbox = 1u << 2;
        public const uint EnemyHitbox = 1u << 3;
        public const uint PlayerHurtbox = 1u << 4;
        public const uint EnemyHurtbox = 1u << 5;
        public const uint Environment = 1u << 6;
        public const uint OneWayPlatform = 1u << 7;
        public const uint Trigger = 1u << 8;
        public const uint PersistentObject = 1u << 9;
        public const uint Projectile = 1u << 10;

        public const uint PlayerBodyMask = Environment | OneWayPlatform | PersistentObject;
        public const uint EnemyBodyMask = Environment | OneWayPlatform | PersistentObject;
        public const uint PlayerHitboxMask = EnemyHurtbox | PersistentObject;
        public const uint EnemyHitboxMask = PlayerHurtbox | PersistentObject;
        public const uint ProjectileMask = PlayerHurtbox | EnemyHurtbox | Environment | PersistentObject;

        public static uint BodyLayerForFighterSlot(int playerIndex) => playerIndex == 0 ? Player : Enemy;
        public static uint BodyMaskForFighterSlot(int playerIndex) => playerIndex == 0 ? PlayerBodyMask : EnemyBodyMask;
        public static uint HitboxLayerForFighterSlot(int playerIndex) => playerIndex == 0 ? PlayerHitbox : EnemyHitbox;
        public static uint HitboxMaskForFighterSlot(int playerIndex) => playerIndex == 0 ? PlayerHitboxMask : EnemyHitboxMask;
        public static uint HurtboxLayerForFighterSlot(int playerIndex) => playerIndex == 0 ? PlayerHurtbox : EnemyHurtbox;
        public static uint HurtboxMaskForFighterSlot(int playerIndex) => playerIndex == 0 ? EnemyHitbox | Projectile : PlayerHitbox | Projectile;
    }
}
